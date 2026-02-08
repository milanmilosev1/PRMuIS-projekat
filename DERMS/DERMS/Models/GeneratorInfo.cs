using DERMS.Enums;
using System;

namespace DERMS.Models
{
    [Serializable]
    public class GeneratorInfo
    {
        public string GeneratorId { get; set; }
        public GeneratorType Type { get; set; }
        public double NominalPowerKw { get; set; }
        public string GeneratorIp { get; set; }
        public int UdpControlPort { get; set; }
        public int TcpSensorPort { get; set; }

        public GeneratorInfo()
        {
            GeneratorId = "";
            Type = GeneratorType.Solar;
            NominalPowerKw = 0.0;
            GeneratorIp = "127.0.0.1";
            UdpControlPort = 0;
            TcpSensorPort = 0;
        }

        public override string ToString()
        {
            return "Gen[" + (GeneratorId ?? "") + "] " + Type +
                   " Pn=" + NominalPowerKw +
                   " IP=" + (GeneratorIp ?? "") +
                   " UDP=" + UdpControlPort +
                   " TCPsensor=" + TcpSensorPort;
        }
    }
}
