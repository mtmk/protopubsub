using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ProtoPubSub
{
    internal interface ILink
    {
        void Connect();
        void Disconnect();
        object Read(Type type);
        void Write(object o);
        void Listen(Action<ILink> connected, Action<ILink> disconnected);
    }

    internal class LinkTcp : ILink
    {
        private TcpClient _client;
        private TcpListener _listener;
        private Action<ILink> _connected;
        private Action<ILink> _disconnected;

        public void Connect()
        {
            _client = new TcpClient();
        }

        public void Disconnect()
        {
            if (_client != null) _client.Close();
            if(_listener != null) _listener.Stop();
        }

        public object Read(Type type)
        {
            throw new NotImplementedException();
        }

        public void Write(object o)
        {
            throw new NotImplementedException();
        }

        public void Listen(Action<ILink> connected, Action<ILink> disconnected)
        {
            _listener = new TcpListener(new IPEndPoint(IPAddress.Any, 0));
            _listener.Start();
            _connected = connected;
            _disconnected = disconnected;
            _listener.BeginAcceptTcpClient(AcceptTcpClient, null);
        }

        private void AcceptTcpClient(IAsyncResult ar)
        {
            var client = _listener.EndAcceptTcpClient(ar);
            _listener.BeginAcceptTcpClient(AcceptTcpClient, null);
            new Thread
        }
    }
}