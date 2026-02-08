using System;

namespace DERMS.Server
{
    [Serializable]
    public class Production
    {
        public string GeneratorId { get; set; }
        public double P { get; set; }
        public double Q { get; set; }
        public DateTime ReceivedAt { get; set; }

        public Production()
        {
            GeneratorId = "";
            P = 0.0;
            Q = 0.0;
            ReceivedAt = DateTime.MinValue;
        }

        public override string ToString()
        {
            string id = GeneratorId ?? "";

            string t = (ReceivedAt == DateTime.MinValue) ? "" : ReceivedAt.ToString("yyyy-MM-dd HH:mm:ss");

            return "Production GenId=" + id + " P=" + P + " Q=" + Q + " @ " + t;
        }
    }
}
