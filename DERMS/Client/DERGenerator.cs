using DERMS;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client
{
    internal sealed class DERGenerator
    {
        public void SimulateGenerator(string ip, int port)
        {
            Console.WriteLine("=== DER GENERATOR KONFIGURACIJA ===");
            Console.WriteLine("1 - Solarni panel (SP)");
            Console.WriteLine("2 - Vetrogenerator (VG)");
            string izbor = Console.ReadLine();

            double nominalnaSnaga = 0;
            TipGeneratora tip;

            if (izbor == "1")
            {
                tip = TipGeneratora.SP;
                while (true)
                {
                    Console.Write("Unesite nominalnu snagu (100 - 500 kW): ");
                    if (double.TryParse(Console.ReadLine(), NumberStyles.Float, CultureInfo.InvariantCulture, out nominalnaSnaga) &&
                        nominalnaSnaga >= 100 && nominalnaSnaga <= 500)
                        break;
                }
            }
            else
            {
                tip = TipGeneratora.VG;
                while (true)
                {
                    Console.Write("Unesite nominalnu snagu (500 - 1000 kW): ");
                    if (double.TryParse(Console.ReadLine(), NumberStyles.Float, CultureInfo.InvariantCulture, out nominalnaSnaga) &&
                        nominalnaSnaga >= 500 && nominalnaSnaga <= 1000)
                        break;
                }
            }

            try
            {
                TcpClient dispecerClient = new TcpClient(ip, port);
                dispecerClient.Connect(IPAddress.Parse(ip), port);
                string generatorId = tip.ToString() + "_" + Guid.NewGuid().ToString().Substring(0, 4);
                byte[] idBytes = Encoding.UTF8.GetBytes(generatorId + "\n");
                dispecerClient.GetStream().Write(idBytes, 0, idBytes.Length);

                UdpClient udpControl = new UdpClient(0);

                TcpListener tcpSensor = new TcpListener(IPAddress.Any, 0);
                tcpSensor.Start();

                Console.WriteLine($"\nREGISTROVAN: {generatorId}");
                Console.WriteLine($"Upravljacka (UDP port): {((IPEndPoint)udpControl.Client.LocalEndPoint).Port}");
                Console.WriteLine($"Senzorska (TCP port): {((IPEndPoint)tcpSensor.LocalEndpoint).Port}\n");

                Socket sensorSocket = tcpSensor.AcceptSocket();
                sensorSocket.Blocking = false;

                sensorSocket.Send(Encoding.UTF8.GetBytes(tip.ToString()));

                while (true)
                {
                    List<Socket> readList = new List<Socket> { sensorSocket };
                    Socket.Select(readList, null, null, 1000 * 1000);

                    if (readList.Count == 0)
                        continue;

                    byte[] buffer = new byte[1024];
                    int bytesRead = sensorSocket.Receive(buffer);
                    if (bytesRead == 0) break;

                    string data = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    double p, q;

                    if (tip == TipGeneratora.SP)
                    {
                        string[] delovi = data.Split(',');
                        double ins = double.Parse(delovi[0], CultureInfo.InvariantCulture);
                        double tcell = double.Parse(delovi[1], CultureInfo.InvariantCulture);

                        p = nominalnaSnaga * ins * 0.00095 * (1 - 0.005 * (tcell - 25));
                        q = 0.0;
                    }
                    else
                    {
                        double v = double.Parse(data, CultureInfo.InvariantCulture);

                        if (v < 3.5 || v > 25.0) p = 0.0;
                        else if (v < 14.0) p = (v - 3.5) * 0.035;
                        else p = nominalnaSnaga;

                        q = p * 0.05;
                    }

                    string slanje =
                        p.ToString(CultureInfo.InvariantCulture) + ";" +
                        q.ToString(CultureInfo.InvariantCulture) + "\n";

                    byte[] slanjeBytes = Encoding.UTF8.GetBytes(slanje);
                    dispecerClient.GetStream().Write(slanjeBytes, 0, slanjeBytes.Length);

                    Console.WriteLine($"Poslato: P={p:F2}kW, Q={q:F2}kVAr");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greska: " + ex.Message);
            }
        }
    }
}
