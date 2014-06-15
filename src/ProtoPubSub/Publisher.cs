using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ProtoPubSub
{
    public class Publisher<T>
    {
        class Sub
        {
            public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            public readonly BlockingCollection<Packate<SimpleHeaderMessage, T>> Q = new BlockingCollection<Packate<SimpleHeaderMessage, T>>(1000);
            public Thread WriterThread;
            public ILink Link;
        }

        private readonly ILink _link;
        private readonly List<Sub> _subs = new List<Sub>();

        public Publisher(string uri)
        {
            _link = new LinkMem(uri);
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