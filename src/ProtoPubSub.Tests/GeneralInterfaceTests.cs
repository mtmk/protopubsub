using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ProtoBuf;
using Xunit;

namespace ProtoPubSub.Tests
{
    public class GeneralInterfaceTests
    {
        [Fact]
        public void Basic_scenario()
        {
            var r = new ManualResetEvent(false);

            var publisher = new Publisher<Message1>("mem://message1");
            publisher.Start();

            var subscriber = new Subscriber<Message1>("mem://message1");
            subscriber.Subscribe(m =>
                                 {
                                     Console.WriteLine(m.Name);
                                     Console.WriteLine(m.Number);
                                     if (m.Name=="DONE") r.Set();
                                 });

            publisher.Publish(new Message1 { Name = "A", Number = 1 });
            publisher.Publish(new Message1 { Name = "B", Number = 2 });
            publisher.Publish(new Message1 { Name = "C", Number = 3 });
            publisher.Publish(new Message1 { Name = "DONE", Number = 4 });

            r.WaitOne();

            publisher.Stop();
            subscriber.Close();
        }
    }

    class Packate<THeader, TMessage>
    {
        public TMessage Message;
        public THeader Header;
    }

    public class Subscriber<T>
    {
        private readonly object _sync = new object();
        private readonly BlockingCollection<Packate<SimpleHeaderMessage, T>> _q = new BlockingCollection<Packate<SimpleHeaderMessage, T>>(1000);
        private readonly Link _link;

        private Thread _consumerThread;
        private CancellationTokenSource _cancellation;
        private Thread _readerThread;

        public Subscriber(string uri)
        {
            _link = new Link(uri);
        }

        public void Subscribe(Action<T> receiver)
        {
            lock (_sync)
            {
                _link.Connect();
                _cancellation = new CancellationTokenSource();
                var token = _cancellation.Token;
                _readerThread = new Thread(() =>
                                     {
                                         while (!token.IsCancellationRequested)
                                         {
                                             try
                                             {
                                                 var header = _link.Read(typeof(SimpleHeaderMessage)) as SimpleHeaderMessage;
                                                 var type = Type.GetType(header.TypeName);
                                                 var message = (T)_link.Read(type);
                                                 try
                                                 {
                                                     _q.Add(
                                                         new Packate<SimpleHeaderMessage, T>
                                                         {
                                                             Header = header,
                                                             Message = message
                                                         }, token);
                                                 }
                                                 catch (OperationCanceledException)
                                                 {
                                                     break;
                                                 }
                                             }
                                             catch (Exception e)
                                             {
                                                 if (!token.IsCancellationRequested)
                                                 {
                                                     Console.WriteLine("error:subs-reader: " + e);
                                                     Reconnect();
                                                 }
                                             }
                                         }
                                     }) { IsBackground = true, Name = "subs-reader" };
                _readerThread.Start();

                _consumerThread = new Thread(() =>
                                     {
                                         while (!token.IsCancellationRequested)
                                         {
                                             try
                                             {
                                                 try
                                                 {
                                                     var package = _q.Take(token);
                                                     receiver(package.Message);
                                                 }
                                                 catch (OperationCanceledException)
                                                 {
                                                     break;
                                                 }
                                             }
                                             catch (Exception e)
                                             {
                                                 Console.WriteLine("error:subs-consumer "+e);
                                             }
                                         }
                                     }) { IsBackground = true, Name = "subs-consumer" };
                _consumerThread.Start();
            }
        }

        private void Reconnect()
        {
            if (!Monitor.TryEnter(_sync))
            {
                _cancellation.Token.WaitHandle.WaitOne(100);
                return;
            }
            try
            {
                _link.Disconnect();
                if (_cancellation.Token.WaitHandle.WaitOne(1000)) return;
                _link.Connect();
            }
            finally
            {
                Monitor.Exit(_sync);
            }
        }

        public void Close()
        {
            lock (_sync)
            {
                if (_cancellation != null) _cancellation.Cancel();

                _link.Disconnect();

                if (_consumerThread != null) _consumerThread.Join();
                if (_readerThread != null) _readerThread.Join();

                _cancellation = null;
                _consumerThread = null;
                _readerThread = null;
            }
        }
    }

    internal class Link
    {
        class Con
        {
            public BlockingCollection<object> Q = new BlockingCollection<object>(1000);
            public Action<Link> Connected;
            public Action<Link> Disconnected;
        }
        private static readonly Dictionary<string, Con> Mem = new Dictionary<string, Con>();
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        private readonly Con _con;
        private CancellationToken _token;

        public Link(string uri)
        {
            _token = _cancellation.Token;
            lock (Mem)
            {
                if (!Mem.ContainsKey(uri)) Mem[uri] = new Con();
                _con = Mem[uri];
            }
        }

        public void Connect()
        {
            lock (_con)
            {
                if (_con.Connected != null)
                    _con.Connected(this);
            }
        }

        public void Disconnect()
        {
            _cancellation.Cancel();
            lock (_con)
            {
                if (_con.Disconnected != null)
                    _con.Disconnected(this);
            }
        }

        public object Read(Type type)
        {
            try
            {
                var o = _con.Q.Take(_token);
                var clone = Serializer.NonGeneric.DeepClone(o);
                return clone;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public void Write(object o)
        {
            var clone = Serializer.NonGeneric.DeepClone(o);
            try
            {
                _con.Q.Add(clone, _token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Listen(Action<Link> connected, Action<Link> disconnected)
        {
            lock (_con)
            {
                _con.Connected += connected;
                _con.Disconnected += disconnected;
            }
        }
    }


    public class Publisher<T>
    {
        class Sub
        {
            public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            public readonly BlockingCollection<Packate<SimpleHeaderMessage, T>> Q = new BlockingCollection<Packate<SimpleHeaderMessage, T>>(1000);
            public Thread WriterThread;
            public Link Link;
        }

        private readonly Link _link;
        private readonly List<Sub> _subs = new List<Sub>();

        public Publisher(string uri)
        {
            _link = new Link(uri);
        }

        public void Start()
        {
            _link.Listen(
            s =>
            {
                lock (_subs)
                {
                    var sub = new Sub { Link = s };
                    var q = sub.Q;
                    var token = sub.Cancellation.Token;
                    var writerThread = new Thread(() =>
                    {
                        while (!token.IsCancellationRequested)
                        {
                            var packate = q.Take(token);
                            s.Write(packate.Header);
                            s.Write(packate.Message);
                        }
                    }) { IsBackground = true, Name = "pub-writer" };
                    writerThread.Start();
                    sub.WriterThread = writerThread;
                    _subs.Add(sub);
                }
            },
            s =>
            {
                lock (_subs)
                {
                    var sub = _subs.Find(x => x.Link == s);
                    if (sub != null)
                    {
                        _subs.Remove(sub);
                        sub.Cancellation.Cancel();
                        sub.WriterThread.Join();
                    }
                }
            });
        }

        public void Publish(T message)
        {
            foreach (var s in _subs.ToList())
            {
                s.Q.Add(new Packate<SimpleHeaderMessage, T>
                        {
                            Header =
                                new SimpleHeaderMessage
                                {
                                    TypeName =
                                        message.GetType()
                                        .FullName
                                },
                            Message = message
                        });
            }
        }

        public void Stop()
        {
            lock (_subs)
            {
                foreach (var s in _subs)
                {
                    s.Cancellation.Cancel();
                    s.WriterThread.Join();
                }
                _subs.Clear();
            }
        }
    }
}
