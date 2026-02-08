namespace DERMS.Enums
{
    public enum MessageType
    {
        None = 0,

        Generator_Register = 10,
        Generator_Production = 11,
        Sensor_Data = 20,
        Control_Command = 30,
        Ping = 90,
        Pong = 91,
        Error = 99
    }
}
