using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ProtoBuf;

namespace ProtoPubSub
{
    internal class LinkMem : ILink
    {

        private class Con
        {
            internal readonly BlockingCollection<object> Q = new BlockingCollection<object>(1000);
            internal Action<ILink> Connected;
            internal Action<ILink> Disconnected;
        }

        private static readonly Dictionary<string, Con> Mem = new Dictionary<string, Con>();
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        private readonly Con _con;
        private readonly CancellationToken _token;

        public LinkMem(string uri)
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

        public void Listen(Action<ILink> connected, Action<ILink> disconnected)
        {
            lock (_con)
            {
                _con.Connected += connected;
                _con.Disconnected += disconnected;
            }
        }
    }
}