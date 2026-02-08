using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client
{
    internal sealed class SenzorVremenskihPrilika
    {
        public void WeatherSensor()
        {
            var sensorSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine("=== SENZOR VREMENSKIH PRILIKA ===");
            Console.Write("Unesite IP: ");
            string ip = Console.ReadLine();
            Console.Write("Unesite port: ");
            int port = int.Parse(Console.ReadLine());

            try
            {
                sensorSocket.Connect(new IPEndPoint(IPAddress.Parse(ip), port));
                sensorSocket.Blocking = false;

                string tip = ReceiveOncePolling(sensorSocket);
                Console.WriteLine($"Primljen tip: {tip}");

                Random rand = new Random();

                while (true)
                {
                    var writeList = new List<Socket> { sensorSocket };
                    Socket.Select(null, writeList, null, 1_000_000);
                    if (writeList.Count == 0) continue;

                    string poruka;

                    if (tip == "SP")
                    {
                        int sat = DateTime.Now.Hour;

                        double ins = 1050.0;
                        double temp = 30.0;

                        if (sat < 12)
                        {
                            ins -= (12 - sat) * 200.0;
                            temp -= (12 - sat) * 4.0;
                        }
                        else if (sat > 14)
                        {
                            ins -= (sat - 14) * 200.0;
                            temp -= (sat - 14) * 4.0;
                        }

                        double tCell;
                        if (temp > 25.0) tCell = 25.0;
                        else tCell = temp + 0.025 * ins;

                        poruka =
                            ins.ToString(CultureInfo.InvariantCulture) + "," +
                            tCell.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        double brzina = rand.NextDouble() * 30.0;
                        poruka = brzina.ToString(CultureInfo.InvariantCulture);
                    }

                    sensorSocket.Send(Encoding.UTF8.GetBytes(poruka));
                    Console.WriteLine("Poslato: " + poruka);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greska: " + ex.Message);
            }
        }

        private static string ReceiveOncePolling(Socket s)
        {
            var buffer = new byte[1024];

            while (true)
            {
                var readList = new List<Socket> { s };
                Socket.Select(readList, null, null, 1_000_000);
                if (readList.Count == 0) continue;

                int n = s.Receive(buffer);
                if (n <= 0) throw new SocketException();

                return Encoding.UTF8.GetString(buffer, 0, n).Trim();
            }
        }
    }
}
