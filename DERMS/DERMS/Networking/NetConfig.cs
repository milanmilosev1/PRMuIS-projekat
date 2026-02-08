namespace DERMS.Networking
{
    public static class NetConfig
    {
        public const int ServerTcpPort = 50005;

        public const int BufferSize = 8192;

        public const int SelectTimeoutUs = 200 * 1000; 
        public const int PollTimeoutUs = 1000 * 1000; 

        public const int DefaultGeneratorUdpControlPort = 50030;
        public const int DefaultGeneratorTcpSensorPort = 50031;
    }
}
