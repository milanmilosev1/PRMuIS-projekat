using DERMS;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Client
{
    internal sealed class DERGenerator
    {
        public void SimulateGenerator()
        {
            Console.WriteLine("=== DER GENERATOR KONFIGURACIJA ===");
            Console.WriteLine("Izaberite tip generatora:");
            Console.WriteLine("1 - Solarni panel");
            Console.WriteLine("2 - Vetrogenerator");
            string izbor = Console.ReadLine();

            double nominalnaSnaga = 0;
            TipGeneratora tip;
            switch(izbor)
            { 
                case "1":


                    tip = TipGeneratora.SP; // Solarni panel
                    while (true)
                     {
                        Console.Write("Unesite nominalnu snagu (100 - 500 kW): ");
                        if (double.TryParse(Console.ReadLine(), out nominalnaSnaga) && nominalnaSnaga >= 100 && nominalnaSnaga <= 500)
                            break;
                        Console.WriteLine("Nevalidan unos! Snaga mora biti izmedju 100 i 500 kW.");
                     }
                        break;

                case "2":
            
                    tip = TipGeneratora.VG; // Vetrogenerator
                    while (true)
                    {
                        Console.Write("Unesite nominalnu snagu (500 - 1000 kW): ");
                        if (double.TryParse(Console.ReadLine(), out nominalnaSnaga) && nominalnaSnaga >= 500 && nominalnaSnaga <= 1000)
                            break;
                        Console.WriteLine("Nevalidan unos! Snaga mora biti izmedju 500 i 1000 kW.");
                    }
                    break;

                default:
                    Console.WriteLine("Nevalidan izbor tipa generatora. Izlazim iz programa.");
                    return;

            }

            try
            {
                
                TcpClient dispecerClient = new TcpClient("127.0.0.1", 8000);
                Console.WriteLine("\n Uspesno povezan sa dispecerskim serverom.");


               
                string generatorId = tip.ToString() + "_" + Guid.NewGuid().ToString().Substring(0, 4);
                byte[] idBytes = Encoding.UTF8.GetBytes(generatorId);
                dispecerClient.GetStream().Write(idBytes, 0, idBytes.Length);

               
                UdpClient udpControl = new UdpClient(0); 
                IPEndPoint udpEp = (IPEndPoint)udpControl.Client.LocalEndPoint;

                
                TcpListener tcpSensor = new TcpListener(IPAddress.Any, 0);
                tcpSensor.Start();
                IPEndPoint tcpEp = (IPEndPoint)tcpSensor.LocalEndpoint;

                //Ispis 
                Console.WriteLine("\n========================================");
                Console.WriteLine($"GENERATOR REGISTROVAN: {generatorId}");
                Console.WriteLine($"Nominalna snaga: {nominalnaSnaga} kW");
                Console.WriteLine($"Upravljacka uticnica (UDP): {udpEp.Address}:{udpEp.Port}");
                Console.WriteLine($"Senzorska uticnica (TCP): {tcpEp.Address}:{tcpEp.Port}");
                Console.WriteLine("========================================\n");

                Console.WriteLine("Klijent je spreman. Cekam senzor...");

                Socket sensorSocket = tcpSensor.AcceptSocket(); 
                sensorSocket.Blocking = false; 
                Console.WriteLine("Senzor povezan.");
                byte[] tipZaSenzor = Encoding.UTF8.GetBytes(tip.ToString());
                sensorSocket.Send(tipZaSenzor);
                while (true)
                {
                    List<Socket> readList = new List<Socket> { sensorSocket };
                    Socket.Select(readList, null, null, 1000000); 

                    if (readList.Count > 0)
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = sensorSocket.Receive(buffer); 
                        if (bytesRead == 0) break;

                        string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        double p = 0;
                        double q = 0;

                        if (tip == TipGeneratora.SP) // Zadatak 5 
                        {
                            string[] delovi = data.Split(',');
                            double ins = double.Parse(delovi[0]);
                            double tcell = double.Parse(delovi[1]);
                            p = nominalnaSnaga * ins * 0.00095 * (1 - 0.005 * (tcell - 25));
                            q = 0;
                        }
                        else if (tip == TipGeneratora.VG) // Zadatak 6
                        {
                            double v = double.Parse(data);
                            if (v < 3.5 || v > 25)
                            {
                                p = 0;
                            }
                            else if (v >= 3.5 && v < 14)
                            {
                                p = (v - 3.5) * 0.035; 
                            }
                            else
                            {
                                p = nominalnaSnaga; 
                            }
                            q = p * 0.05; // Reaktivna je 5% aktivne 
                        }

                        string slanje = p.ToString() + ";" + q.ToString();
                        byte[] slanjeBytes = Encoding.UTF8.GetBytes(slanje);
                        dispecerClient.GetStream().Write(slanjeBytes, 0, slanjeBytes.Length);

                        Console.WriteLine($"Poslato dispeceru: P={p:F2}kW, Q={q:F2}kVAr");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greska pri pokretanju klijenta: {ex.Message}");
            }
        }
    }

}
