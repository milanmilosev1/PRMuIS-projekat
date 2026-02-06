using Modeli;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Server
{
    internal class Server
    {
        public IPAddress IpAddress { get; }
        public int Port { get; }
        public IList<ClientHandler> Clients { get; }
        public IList<Proizvodnja> ProductionData { get; }

        private readonly Socket _serverSocket;
        private readonly IPEndPoint _localEndPoint;

        public Server(IPAddress ipAddress, int port)
        {
            IpAddress = ipAddress;
            Port = port;

            Clients = new List<ClientHandler>();
            ProductionData = new List<Proizvodnja>();

            _localEndPoint = new IPEndPoint(ipAddress, port);

            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _serverSocket.Blocking = false;
            _serverSocket.Bind(_localEndPoint);
            _serverSocket.Listen(10);

            Console.WriteLine($"Server pokrenut na {IpAddress}:{Port}");
        }

        public void Run()
        {
            while (true)
            {
                AcceptClients();
                PollClients();
            }
        }

        private void AcceptClients()
        {
            if (_serverSocket.Poll(1000 * 1000, SelectMode.SelectRead))
            {
                try
                {
                    var clientSocket = _serverSocket.Accept();
                    clientSocket.Blocking = false;

                    var handler = new ClientHandler(clientSocket);
                    Clients.Add(handler);

                    Console.WriteLine("Novi generator povezan");
                }
                catch (SocketException)
                {
                    
                }
            }
        }

        private void PollClients()
        {
            foreach (var client in Clients.ToList())
            {
                try
                {
                    if (client.Socket.Poll(1000 * 1000, SelectMode.SelectRead))
                    {
                        var message = client.Receive();

                        if (message == null)
                        {
                            Clients.Remove(client);
                            Console.WriteLine("Generator diskonektovan");
                            continue;
                        }

                        ProcessMessage(client, message);
                    }
                }
                catch (SocketException)
                {
                    Clients.Remove(client);
                }
            }
        }

        private void ProcessMessage(ClientHandler client, string message)
        {
            if (!client.IsRegistered)
            {
                client.GeneratorId = message;
                client.IsRegistered = true;

                Console.WriteLine($"Generator {client.GeneratorId} se registrovao");
                return;
            }

            var parts = message.Split(';');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out double activePower) &&
                double.TryParse(parts[1], out double reactivePower))
            {
                var prod = new Proizvodnja
                {
                    Id = client.GeneratorId,
                    ActivePower = activePower,
                    ReactivePower = reactivePower
                };

                ProductionData.Add(prod);

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss}] {prod.Id} | P={activePower:F2} kW | Q={reactivePower:F2} kVAr"
                );
            }
        }
    }
}
