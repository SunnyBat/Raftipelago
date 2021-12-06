using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ArchipelagoProxy
{
    public class ProxyServer : SocketInteractor
    {
        private Socket _listener;

        public ProxyServer(int port)
        {
            _listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IPHostEntry localhostIPEntry = Dns.GetHostEntry("localhost");
            IPAddress localhostIPAddress = localhostIPEntry.AddressList[0];
            _listener.Bind(new IPEndPoint(localhostIPAddress, port));
        }

        public void InteractUntilConnectionClosed()
        {
            // Specify how many requests a Socket can listen before it gives Server busy response.
            _listener.Listen(1);
            Console.WriteLine("Waiting for a connection...");
            Socket handler = _listener.Accept();
            this.InteractUntilConnectionClosed(handler);
            handler.Shutdown(SocketShutdown.Both);
            handler.Close();
        }
    }
}
