using System;

namespace Modeli
{
    [Serializable]
    internal class Proizvodnja
    {
        public string Id { get; set; } = string.Empty;
        public double ActivePower { get; set; }
        public double ReactivePower { get; set; }
    }
}
