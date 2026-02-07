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

        /// <summary>
        /// Prima bajtove sa TCP stream-a i vraća sve kompletne poruke (linije) koje su stigle.
        /// Poruke su razdvojene sa '\n'.
        /// </summary>
        public List<string> ReceiveLines()
        {
            int bytesRead = Socket.Receive(_buffer);

            if (bytesRead == 0)
            {
                try { Socket.Close(); } catch { }
                return null; // disconnect
            }

            _incoming.Append(Encoding.UTF8.GetString(_buffer, 0, bytesRead));

            var lines = new List<string>();

            while (true)
            {
                string all = _incoming.ToString();
                int nl = all.IndexOf('\n');
                if (nl < 0) break;

                string line = all.Substring(0, nl).Trim();
                lines.Add(line);

                _incoming.Clear();
                _incoming.Append(all.Substring(nl + 1));
            }

            return lines;
        }
    }
}
