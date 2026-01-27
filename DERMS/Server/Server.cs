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

        public Server(IPAddress ipAddress, int port)
        {
            IpAddress = ipAddress;
            Port = port;       
            Clients = new List<ClientHandler>();
        }
        
        public void Start()
        {
            var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint localEndPoint = new IPEndPoint(IpAddress, Port);
            serverSocket.Bind(localEndPoint);
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
            //TODO: Implementacija za cuvanje podataka
            // kontam da smestim u ovaj DERMS projekat neki kao persistence layer al nzm videcemo sve
            throw new NotImplementedException();
        }

        public bool RemoveClient(ClientHandler clientHandler)
        {
            return Clients.Remove(clientHandler);
        }
    }
}
