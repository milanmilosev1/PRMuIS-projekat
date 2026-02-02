using DERMS.Modeli;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Server
{
    internal class Server
    {
        public IPAddress IpAddress { get; set; }
        public int Port { get; set; }
        public IList<ClientHandler> Clients { get; set; }
        public IList<Proizvodnja> ProductionData { get; set; }
        private readonly IPEndPoint _localEndPoint;

        public Server(IPAddress ipAddress, int port)
        {
            IpAddress = ipAddress;
            Port = port;       
            Clients = new List<ClientHandler>();
            ProductionData = new List<Proizvodnja>();
            _localEndPoint = new IPEndPoint(ipAddress, port);
            Start();
        }
        
        public void Start()
        {
            var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(_localEndPoint);
            serverSocket.Listen(10);

            Console.WriteLine($"Server pokrenut na {IpAddress}:{Port}");
            AcceptClients();
        }

        private void AcceptClients()
        {
            foreach(var client in Clients)
            {
                client.Handle();
            }
        }

        public void AddProductionData(string generatorId, double activePower, double reactivePower)
        {
            var prod = new Proizvodnja
            {
                Id = generatorId + "",
                ActivePower = activePower,
                ReactivePower = reactivePower
            };

            ProductionData.Add(prod);
        }

        public bool RemoveClient(ClientHandler clientHandler)
        {
            return Clients.Remove(clientHandler);
        }

        public void AddClient(ClientHandler clientHandler)
        {
            Clients.Add(clientHandler);
        }
    }
}
