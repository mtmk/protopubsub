using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ProtoPubSub
{
    public class Subscriber<T>
    {
        private readonly object _sync = new object();
        private readonly BlockingCollection<Packate<SimpleHeaderMessage, T>> _q = new BlockingCollection<Packate<SimpleHeaderMessage, T>>(1000);
        private readonly ILink _link;

        private Thread _consumerThread;
        private CancellationTokenSource _cancellation;
        private Thread _readerThread;

        public Subscriber(string uri)
        {
            _link = new LinkMem(uri);
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
}