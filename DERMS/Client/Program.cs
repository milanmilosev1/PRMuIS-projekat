namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string ip = "192.168.8.100";
            int port = 50001;
            DERGenerator generator = new DERGenerator();
            generator.SimulateGenerator(ip, port);
            SenzorVremenskihPrilika senzori = new SenzorVremenskihPrilika();
            senzori.WeatherSensor();
        }
    }
}
