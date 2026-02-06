using System.Net.Sockets;
using System.Text;

namespace Server
{
    internal sealed class ClientHandler
    {
        public Socket Socket { get; }
        public string GeneratorId { get; set; }
        public bool IsRegistered { get; set; }

        private readonly byte[] _buffer = new byte[1024];

        public ClientHandler(Socket socket)
        {
            Socket = socket;
        }

        public string Receive()
        {
            int bytesRead = Socket.Receive(_buffer);

            if (bytesRead == 0)
            {
                Socket.Close();
                return null;
            }

            return Encoding.UTF8.GetString(_buffer, 0, bytesRead);
        }
    }
}
