using System;
using System.Net.Sockets;
using System.Text;

namespace Server
{
    internal sealed class ClientHandler : IHandler
    {
        private readonly Socket _clientSocket;
        private readonly Server _server;
        private bool _isRunning = true;

        public ClientHandler(Socket clientSocket, Server server)
        {
            _clientSocket = clientSocket;
            _server = server;
        }

        public void Handle()
        {
            _server.AddClient(this);
            var buffer = new byte[1024];

            try
            {
                int bytesRead = _clientSocket.Receive(buffer);
                if (bytesRead == 0) return;

                var generatorId = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Generator {generatorId} se registrovao");

                while (_isRunning)
                {
                    bytesRead = _clientSocket.Receive(buffer);
                    if (bytesRead == 0) break;

                    var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    var parts = data.Split(';');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], out var activePower) &&
                        double.TryParse(parts[1], out var reactivePower))
                    {
                        _server.AddProductionData(generatorId, activePower, reactivePower);
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Greska: Socket code {e.SocketErrorCode}");
            }
            finally
            {
                _clientSocket.Close();
                _server.RemoveClient(this);
            }
        }

        public void Stop()
        {
            _isRunning = false;
        }
    }
}
