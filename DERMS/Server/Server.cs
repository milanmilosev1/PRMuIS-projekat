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

            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                Blocking = false
            };
            _serverSocket.Bind(_localEndPoint);
            _serverSocket.Listen(10);

            Console.WriteLine($"Server pokrenut na {IpAddress}:{Port}");

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                _running = false;
            };
        }

        public void Run()
        {
            while (_running)
            {
                if (_serverSocket.Poll(1000 * 1000, SelectMode.SelectRead)) // 1 sekunda
                {
                    AcceptClient();
                }

                foreach (var client in Clients.ToList())
                {
                    try
                    {
                        if (client.Socket.Poll(1000, SelectMode.SelectRead)) // 1 ms
                        {
                            var lines = client.ReceiveLines();

                            if (lines == null)
                            {
                                // Konekcija zatvorena
                                Clients.Remove(client);
                                Console.WriteLine($"Generator {client.GeneratorId} diskonektovan");
                                continue;
                            }

                            foreach (var line in lines)
                            {
                                ProcessMessage(client, line);
                            }
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode == SocketError.ConnectionReset)
                        {
                            Clients.Remove(client);
                            try { client.Socket.Close(); } catch { }
                            Console.WriteLine($"Generator {client.GeneratorId} diskonektovan (reset veze)");
                        }
                        else
                        {
                            Console.WriteLine($"Socket greška: {ex.SocketErrorCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Greška pri obradi klijenta: {ex.Message}");
                    }
                }
            }


            PrintStatistics();
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

                Console.WriteLine($"Novi generator povezan: {clientSocket.RemoteEndPoint}");
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.WouldBlock)
                    Console.WriteLine($"Greska pri prihvatanju klijenta: {ex.Message}");
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
                Console.WriteLine($"Poruka: {message}");
                return;
            }

            var parts = message.Split(';');
            if (parts.Length != 3) return;

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
            Console.WriteLine("\n=== STATISTIKA (pri zaustavljanju servera) ===");

            var solarGroups = ProductionData
                .Where(p => p.Id != null && p.Id.Length >= 2 && p.Id.Substring(0, 2).ToUpper() == "SP")
                .GroupBy(p => p.Id)
                .ToList();

            var windGroups = ProductionData
                .Where(p => p.Id != null && p.Id.Length >= 2 && p.Id.Substring(0, 2).ToUpper() == "VG")
                .GroupBy(p => p.Id)
                .ToList();

            if (solarGroups.Any())
            {
                double totalSolarActive = solarGroups.Sum(g => g.Sum(p => p.ActivePower));
                double avgSolar = totalSolarActive / solarGroups.Count(g => g.Any());
                Console.WriteLine($"Prosecna proizvodnja solarnih panela: {avgSolar:F2} kW");

                foreach (var group in solarGroups)
                {
                    Console.WriteLine($"  {group.Key}: {group.Average(p => p.ActivePower):F2} kW (prosek)");
                }
            }
            else
            {
                Console.WriteLine("Nema podataka o solarnim panelima.");
            }

            if (windGroups.Any())
            {
                double totalWindActive = windGroups.Sum(g => g.Sum(p => p.ActivePower));
                double avgWind = totalWindActive / windGroups.Count(g => g.Any());
                Console.WriteLine($"Prosecna proizvodnja vetrogeneratora: {avgWind:F2} kW");

                foreach (var group in windGroups)
                {
                    Console.WriteLine($"  {group.Key}: {group.Average(p => p.ActivePower):F2} kW (prosek)");
                }
            }
            else
            {
                Console.WriteLine("Nema podataka o vetrogeneratorima.");
            }

            double totalReactive = ProductionData.Sum(p => p.ReactivePower);
            Console.WriteLine($"Ukupno proizvedena reaktivna snaga: {totalReactive:F2} kVAr");

            Console.WriteLine($"Ukupno primljenih merenja: {ProductionData.Count}");
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
