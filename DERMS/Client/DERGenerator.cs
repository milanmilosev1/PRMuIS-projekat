using DERMS;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

               

                Console.ReadLine(); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greska pri pokretanju klijenta: {ex.Message}");
            }
        }
    }

}
