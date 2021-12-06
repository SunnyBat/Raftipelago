using System.Net;
using System.Net.Sockets;

namespace ArchipelagoProxy
{
    public class ProxyClient : SocketInteractor
    {
        private string _address;
        private int _port;
        public ProxyClient(string address, int port)
        {
            this._address = address;
            this._port = port;
        }

        public void ListenUntilConnectionClosed()
        {
            Socket handler = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IPHostEntry ipEntry = Dns.GetHostEntry(_address);
            IPAddress ipAddress = ipEntry.AddressList[0];
            IPEndPoint endPoint = new IPEndPoint(ipAddress, _port);
            handler.Connect(endPoint);

            this.InteractUntilConnectionClosed(handler);
            handler.Shutdown(SocketShutdown.Both);
            handler.Close();
        }
    }
}
