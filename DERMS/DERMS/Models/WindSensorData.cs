using System;

namespace DERMS.Models
{
    [Serializable]
    public class WindSensorData
    {
        public double WindSpeed { get; set; }
        public DateTime Time { get; set; }

        public WindSensorData()
        {
            WindSpeed = 0.0;
            Time = DateTime.Now;
        }

        public override string ToString()
        {
            return "WindSensor v=" + WindSpeed + " t=" + Time.ToString("HH:mm:ss");
        }
    }
}
