using System;

namespace DERMS.Models
{
    [Serializable]
    public class SolarSensorData
    {
        public double INS { get; set; }
        public double Tcell { get; set; }
        public DateTime Time { get; set; }

        public SolarSensorData()
        {
            INS = 0.0;
            Tcell = 0.0;
            Time = DateTime.Now;
        }

        public override string ToString()
        {
            return "SolarSensor INS=" + INS + " Tcell=" + Tcell + " t=" + Time.ToString("HH:mm:ss");
        }
    }
}
