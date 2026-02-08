using DERMS.Enums;
using System;
using System.Collections.Generic;

namespace DERMS.Networking
{
    public static class Safe
    {
        public static string ReadLineNonNull()
        {

            string s = Console.ReadLine();
            return s == null ? "" : s;
        }

        public static string TrimOrEmpty(string s)
        {

            if (s == null) return "";
            return s.Trim();
        }

        public static int ToInt(string s, int def)
        {

            if (string.IsNullOrWhiteSpace(s)) return def;
            int x;
            if (int.TryParse(s.Trim(), out x)) return x;
            return def;
        }

        public static double ToDouble(string s, double def)
        {
            if (string.IsNullOrWhiteSpace(s)) return def;
            double x;
            if (double.TryParse(s.Trim(), out x)) return x;
            return def;
        }

        public static List<T> EmptyList<T>()
        {
            return new List<T>();
        }

        public static string NewGeneratorId(GeneratorType type)
        {

            string prefix = (type == GeneratorType.Wind) ? "WI" : "SO";

            int n = 0;
            try { n = new Random().Next(100000, 999999); }
            catch { n = (int)(DateTime.Now.Ticks % 900000) + 100000; }

            return prefix + n.ToString();

        }
    }
}
