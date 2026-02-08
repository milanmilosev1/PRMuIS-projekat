using System;
using System.Collections.Generic;
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
        private readonly StringBuilder _incoming = new StringBuilder();

        public ClientHandler(Socket socket)
        {
            Socket = socket;
        }
        public List<string> ReceiveLines()
        {
            var messages = new List<string>();

            try
            {
                while (Socket.Available > 0)
                {
                    int bytesRead = Socket.Receive(_buffer, 0, _buffer.Length, SocketFlags.None);
                    if (bytesRead == 0)
                    {
                        return null;
                    }

                    string data = Encoding.UTF8.GetString(_buffer, 0, bytesRead);
                    var lines = data.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            messages.Add(line.Trim());
                    }
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    return messages;
                }
                throw;
            }

            return messages;
        }
    }
}
