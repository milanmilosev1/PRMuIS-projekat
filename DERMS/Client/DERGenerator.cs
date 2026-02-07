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
        public void SimulateGenerator()
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
                // TCP konekcija sa dispečerom (serverom)
                TcpClient dispecerClient = new TcpClient("192.168.56.1", 50001);

                // ID mora prvo da ode (jedna linija)
                string generatorId = tip.ToString() + "_" + Guid.NewGuid().ToString().Substring(0, 4);
                byte[] idBytes = Encoding.UTF8.GetBytes(generatorId + "\n");
                dispecerClient.GetStream().Write(idBytes, 0, idBytes.Length);

                // Utičnice (po zadatku): UDP upravljačka + TCP senzorska
                UdpClient udpControl = new UdpClient(0);

                TcpListener tcpSensor = new TcpListener(IPAddress.Any, 0);
                tcpSensor.Start();

                Console.WriteLine($"\nREGISTROVAN: {generatorId}");
                Console.WriteLine($"Upravljacka (UDP port): {((IPEndPoint)udpControl.Client.LocalEndPoint).Port}");
                Console.WriteLine($"Senzorska (TCP port): {((IPEndPoint)tcpSensor.LocalEndpoint).Port}\n");

                Socket sensorSocket = tcpSensor.AcceptSocket();
                sensorSocket.Blocking = false;

                // Pošalji tip senzoru (SP ili VG)
                sensorSocket.Send(Encoding.UTF8.GetBytes(tip.ToString()));

                while (true)
                {
                    // polling čitanje senzora
                    List<Socket> readList = new List<Socket> { sensorSocket };
                    Socket.Select(readList, null, null, 1_000_000);

                    if (readList.Count == 0)
                        continue;

                    byte[] buffer = new byte[1024];
                    int bytesRead = sensorSocket.Receive(buffer);
                    if (bytesRead == 0) break;

                    string data = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    double p, q;

                    if (tip == TipGeneratora.SP)
                    {
                        // INS,Tcell
                        string[] delovi = data.Split(',');
                        double ins = double.Parse(delovi[0], CultureInfo.InvariantCulture);
                        double tcell = double.Parse(delovi[1], CultureInfo.InvariantCulture);

                        // P = Pn * INS * 0.00095 * (1 - 0.005*(Tcell-25)), Q=0 :contentReference[oaicite:7]{index=7}
                        p = nominalnaSnaga * ins * 0.00095 * (1 - 0.005 * (tcell - 25));
                        q = 0.0;
                    }
                    else
                    {
                        // brzina vetra
                        double v = double.Parse(data, CultureInfo.InvariantCulture);

                        // Po PDF-u: ako v<3.5 ili v>25 => 0; 3.5-14 => (v-3.5)*0.035; >=14 => Pn :contentReference[oaicite:8]{index=8}
                        if (v < 3.5 || v > 25.0) p = 0.0;
                        else if (v < 14.0) p = (v - 3.5) * 0.035;
                        else p = nominalnaSnaga;

                        q = p * 0.05;
                    }

                    // P;Q ide kao linija (ovo popravlja "NE RADI")
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
