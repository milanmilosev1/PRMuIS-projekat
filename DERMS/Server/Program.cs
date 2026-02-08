using System.Net;

namespace Server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IPAddress ipAddress = IPAddress.Parse("192.168.8.100");
            var server = new Server(ipAddress, 50001);
            server.Run();
        }
    }
}
