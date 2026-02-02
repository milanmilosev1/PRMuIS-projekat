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
            try
            {
                _server.AddClient(this);
                byte[] buffer = new byte[1024];
                int bytesRead = _clientSocket.Receive(buffer);
                var generatorId = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                Console.WriteLine($"Generator {generatorId} se registrovao");

                while (_isRunning)
                {
                    bytesRead = _clientSocket.Receive(buffer);
                    if (bytesRead == 0) break;

                    string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    var parts = data.Split(';');
                    if(parts.Length == 2)
                    {
                        var activePower = double.Parse(parts[0]);
                        var reactivePower = double.Parse(parts[1]);
                        _server.AddProductionData(generatorId, activePower, reactivePower);
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Socket error: " + ex.SocketErrorCode);
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
