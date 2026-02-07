using Modeli;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        private bool _running = true;

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

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;   // ne ubij proces odmah
                _running = false;
            };
        }

        public void Run()
        {
            while (_running)
            {
                // polling model: Select nad server socketom + svim klijentima :contentReference[oaicite:2]{index=2}
                var readList = new List<Socket> { _serverSocket };
                readList.AddRange(Clients.Select(c => c.Socket));

                Socket.Select(readList, null, null, 1_000_000); // ~1s

                // 1) Accept novih
                if (readList.Contains(_serverSocket))
                    AcceptClient();

                // 2) Obradi klijente koji imaju podatke
                foreach (var sock in readList.Where(s => s != _serverSocket).ToList())
                {
                    var client = Clients.FirstOrDefault(c => c.Socket == sock);
                    if (client == null) continue;

                    try
                    {
                        var lines = client.ReceiveLines();

                        if (lines == null)
                        {
                            Clients.Remove(client);
                            Console.WriteLine("Generator diskonektovan");
                            continue;
                        }

                        foreach (var line in lines)
                            ProcessMessage(client, line);
                    }
                    catch (SocketException)
                    {
                        Clients.Remove(client);
                        try { client.Socket.Close(); } catch { }
                        Console.WriteLine("Generator diskonektovan (greska na soketu)");
                    }
                }
            }

            PrintStatistics(); // zad. 9 :contentReference[oaicite:3]{index=3}
            CloseAll();
        }

        private void AcceptClient()
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
                // normalno u polling režimu
            }
        }

        private void ProcessMessage(ClientHandler client, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (!client.IsRegistered)
            {
                client.GeneratorId = message.Trim();
                client.IsRegistered = true;

                Console.WriteLine($"Generator {client.GeneratorId} se registrovao");
                return;
            }

            var parts = message.Split(';');
            if (parts.Length != 2) return;

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double activePower))
                return;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double reactivePower))
                return;

            var prod = new Proizvodnja
            {
                Id = client.GeneratorId,
                ActivePower = activePower,
                ReactivePower = reactivePower
            };

            ProductionData.Add(prod);

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {prod.Id} | P={activePower:F2} kW | Q={reactivePower:F2} kVAr");
        }

        private void PrintStatistics()
        {
            // Tip je prva 2 karaktera u ID-u (SP_... / VG_...) :contentReference[oaicite:6]{index=6}
            var sp = ProductionData.Where(p => p.Id != null && p.Id.StartsWith("SP")).ToList();
            var vg = ProductionData.Where(p => p.Id != null && p.Id.StartsWith("VG")).ToList();

            double avgSp = sp.Count > 0 ? sp.Average(p => p.ActivePower) : 0.0;
            double avgVg = vg.Count > 0 ? vg.Average(p => p.ActivePower) : 0.0;
            double totalQ = ProductionData.Sum(p => p.ReactivePower);

            Console.WriteLine("\n=== STATISTIKA ===");
            Console.WriteLine($"Prosecna aktivna snaga (SP): {avgSp:F2} kW");
            Console.WriteLine($"Prosecna aktivna snaga (VG): {avgVg:F2} kW");
            Console.WriteLine($"Ukupno proizvedena reaktivna snaga: {totalQ:F2} kVAr");
        }

        private void CloseAll()
        {
            foreach (var c in Clients.ToList())
            {
                try { c.Socket.Close(); } catch { }
            }
            Clients.Clear();

            try { _serverSocket.Close(); } catch { }
        }
    }
}
