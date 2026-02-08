using System;

namespace DERMS.Models
{
    [Serializable]
    public class ControlCommand
    {
        public string Command { get; set; }
        public string Arg { get; set; }

        public ControlCommand()
        {
            Command = "";
            Arg = "";
        }

        public override string ToString()
        {
            return "CMD=" + (Command ?? "") + " ARG=" + (Arg ?? "");
        }
    }
}
