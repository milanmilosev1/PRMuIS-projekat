using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Client
{
    internal sealed class SenzorVremenskihPrilika
    {
        public void WeatherSensor()
        {
            Socket sensorSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Console.WriteLine("=== SENZOR VREMENSKIH PRILIKA ===");
            Console.Write("Unesite IP adresu generatora: ");
            string ip = Console.ReadLine();
            Console.Write("Unesite port senzorske utičnice: ");
            int port = int.Parse(Console.ReadLine());
            try
            {
               
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
                sensorSocket.Connect(endPoint);

        
                sensorSocket.Blocking = false;
                Console.WriteLine("Veza uspostavljena. Utičnica je u neblokirajućem režimu.");
                string tip = "";
                List<Socket> readSockets = new List<Socket> { sensorSocket };
                Socket.Select(readSockets, null, null, 1000000);

                if (readSockets.Count > 0)
                {
                    byte[] buffer = new byte[1024];
                    int bytesReceived = sensorSocket.Receive(buffer); 
                    tip = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                }

                Console.WriteLine("Povezan sa: " + tip);

                while (true)
                {
                    string poruka = "";

                    
                    if (tip.Contains("SP"))
                    {
                        int sat = DateTime.Now.Hour;
                        double ins = 1050; // Osuncanost za period 12-14h 
                        double temp = 30;  // Temperatura za period 12-14h 

                        if (sat < 12)
                        {
                            int razlika = 12 - sat;
                            ins -= razlika * 200; 
                            temp -= razlika * 4;  
                        }
                        else if (sat > 14)
                        {
                            int razlika = sat - 14;
                            ins -= razlika * 200;
                            temp -= razlika * 4;
                        }


                        double tCell;
                        if(temp >= 25)
                        {
                            tCell = 25;
                        }
                        else
                        {
                            tCell = temp + 0.025 * ins;
                        }
                        poruka = ins.ToString() + "," + tCell.ToString();

                    }
                    
                    else if (tip.Contains("VG"))
                    {
                        Random r = new Random();
                        double brzinaVetra = r.NextDouble() * 30.0; // Opseg 0.0 - 30.0 
                        poruka = brzinaVetra.ToString();
                    }

                  
                    List<Socket> checkWrite = new List<Socket> { sensorSocket };
                    Socket.Select(null, checkWrite, null, 1000000);

                    if (checkWrite.Contains(sensorSocket))
                    {
                        byte[] data = Encoding.UTF8.GetBytes(poruka);
                        sensorSocket.Send(data); 
                        Console.WriteLine("Poslato generatoru: " + poruka);
                    }

                    // Pauza od 5 sekundi između ocitavanja
                    Thread.Sleep(5000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greska: " + ex.Message);
            }
            finally
            {
              
                if (sensorSocket.Connected)
                {
                    sensorSocket.Shutdown(SocketShutdown.Both);
                    sensorSocket.Close();
                }
            }
        }


    }

    

}
