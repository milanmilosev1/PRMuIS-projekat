using DERMS.Enums;
using System;

namespace DERMS.Networking
{
    [Serializable]
    public class Envelope
    {
        public MessageType Type { get; set; }
        public string GeneratorId { get; set; }
        public string Text { get; set; }
        public byte[] Payload { get; set; }

        public Envelope()
        {
            Type = MessageType.None;
            GeneratorId = "";
            Text = "";
            Payload = new byte[0];
        }

        public override string ToString()
        {
            int tlen = 0;
            int plen = 0;

            tlen = (Text == null) ? 0 : Text.Length;
            plen = (Payload == null) ? 0 : Payload.Length;

            return "Envelope[" + Type + "] GenId=" + (GeneratorId ?? "") +
                   " TextLen=" + tlen + " PayloadLen=" + plen;
        }
    }
}
