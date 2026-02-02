using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            DERGenerator generator = new DERGenerator();
            generator.SimulateGenerator();
            SenzorVremenskihPrilika senzori = new SenzorVremenskihPrilika();
            senzori.WeatherSensor();

        }
    }
      
         
       
}
