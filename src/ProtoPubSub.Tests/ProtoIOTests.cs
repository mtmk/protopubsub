using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using Xunit;

namespace ProtoPubSub.Tests
{
    public class ProtoIoTests
    {
        [Fact]
        public void Basic()
        {
            var p1 = new ProtoIo("mem://test");
            p1.Listen(c => { });
            p1.On(m => Console.WriteLine("P1 TEST RECEIVED: " + m));

            var p2 = new ProtoIo("mem://test");
            p2.Connect();
            p2.On(m =>
                  {
                      Console.WriteLine("P2 TEST RECEIVED: " + m);
                  });

            p2.Emit("B");
            p1.Emit("A");

            p2.Close();
            p1.Close();
        }
    }

    public class ProtoIo
    {
        static BlockingCollection<string> S = new BlockingCollection<string>(1000);
        static BlockingCollection<string> C = new BlockingCollection<string>(1000);

        private readonly string _uri;
        private BlockingCollection<string> _q1;
        private BlockingCollection<string> _q2;
        private Thread _consumer;
        private Action<string> _callback;
        private string _tag;

        public ProtoIo(string uri)
        {
            _uri = uri;
        }

        public void Listen(Action<ProtoIo> connected = null)
        {
            _q1 = S; _q2 = C;
            _tag = "SERVER";

            Log(_tag, "Listen");

            _consumer = new Thread(() =>
            {
                while (true)
                {
                    string message = _q1.Take();
                    Log(_tag, "Received: " + message);
                    if (message.StartsWith("__CLOSE"))
                    {
                        _q2.Add("__CLOSE");
                        break;
                    }
                    _callback(message);
                }
                Log(_tag, "Exit consumer thread");
            }) { IsBackground = true, Name = "consumer" };

            _consumer.Start();
        }

        private void Log(string tag, string log)
        {
            //Console.Error.WriteLine("[" + tag +"] " + log);
        }

        public void Connect()
        {
            _q1 = C; _q2 = S;
            _tag = "CLIENT";

            Log(_tag, "Connect");

            _consumer = new Thread(() =>
            {
                while (true)
                {
                    string message = _q1.Take();
                    Log(_tag, "Received: " + message);
                    if (message.StartsWith("__CLOSE"))
                    {
                        _q2.Add("__CLOSE");
                        break;
                    }
                    _callback(message);
                }
                Log(_tag, "Exit consumer thread");
            }) { IsBackground = true, Name = "consumer" };

            _consumer.Start();
        }

        public void On(Action<string> callback)
        {
            Log(_tag, "Registered callback");
            _callback = callback;
        }

        public void Emit(string message)
        {
            Log(_tag, "Emit " + message);
            _q2.Add(message);
        }

        public void Close()
        {
            Log(_tag, "Close");

            _q2.Add("__CLOSE");

            if (!_consumer.Join(1000))
            {
                Log(_tag, "Abort");
            }
        }
    }
}