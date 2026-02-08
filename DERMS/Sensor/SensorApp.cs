using DERMS.Enums;
using DERMS.Models;
using DERMS.Networking;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace DERMS.Sensor
{
    internal static class SensorApp
    {
        private static Socket _sock;

        private static GeneratorType _type = GeneratorType.Solar;

        private static DateTime _lastSend = DateTime.MinValue;
        private static int _sendPeriodMs = 1000;

        private static Random _rng = new Random();

        public static void Run(string[] args)
        {
            Console.WriteLine("=== DERMS SENSOR ===");

            Console.WriteLine("Generator IP (ENTER za 127.0.0.1):");
            string ip = Safe.ReadLineNonNull();
            if (string.IsNullOrWhiteSpace(ip)) ip = "127.0.0.1";

            Console.WriteLine("Generator TCP sensor port (sa generatora):");
            int port = Safe.ToInt(Safe.ReadLineNonNull(), 0);

            Console.WriteLine("Tip generatora (1=Solar, 2=Wind):");
            int t = Safe.ToInt(Safe.ReadLineNonNull(), 1);
            _type = (t == 2) ? GeneratorType.Wind : GeneratorType.Solar;

            if (!Connect(ip.Trim(), port))
            {
                Console.WriteLine("Neuspesno povezivanje na generator.");
                Cleanup();
                return;
            }

            Console.WriteLine("Povezan na generator: " + ip + ":" + port);
            Console.WriteLine("Senzor tip: " + _type);
            Console.WriteLine("Komande: Q (izlaz)");
            Console.WriteLine();

            bool running = true;
            while (running)
            {
                MaybeSend();

                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true);
                    if (k.Key == ConsoleKey.Q) running = false;
                }

                WaitSmall();
            }

            Cleanup();
            Console.WriteLine("Senzor ugasen.");
        }

        private static bool Connect(string ip, int port)
        {
            try
            {
                IPAddress addr = IPAddress.Parse(ip);

                _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _sock.Blocking = true;
                _sock.Connect(new IPEndPoint(addr, port));

                return true;
            }
            catch (FormatException)
            {
                Console.WriteLine("[SENSOR] Nevalidan IP format.");
                return false;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Connect] {ex.SocketErrorCode}");
                return false;
            }
        }

        private static void WaitSmall()
        {
            try
            {
                if (_sock == null) return;

                List<Socket> wl = new List<Socket> { _sock };
                Socket.Select(null, wl, null, 50 * 1000);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Wait] {ex.SocketErrorCode}");
            }
        }

        private static void MaybeSend()
        {
            if (_sock == null) return;

            if (_lastSend != DateTime.MinValue)
            {
                double ms = (DateTime.Now - _lastSend).TotalMilliseconds;
                if (ms < _sendPeriodMs) return;
            }

            Envelope env = new Envelope();
            env.Type = MessageType.Sensor_Data;

            if (_type == GeneratorType.Solar)
            {
                SolarSensorData sd = BuildSolarSensorData(DateTime.Now);
                env.Payload = Serializer.SerializeObject(sd);
                env.Text = "";

                bool ok = TrySendFramedEnvelope(_sock, env);
                if (ok)
                {
                    _lastSend = DateTime.Now;
                    Console.WriteLine("[SEND] " + sd.ToString());
                }
                else
                {
                    Console.WriteLine("[SEND] Solar failed");
                }
            }
            else
            {
                WindSensorData wd = BuildWindSensorData(DateTime.Now);
                env.Payload = Serializer.SerializeObject(wd);
                env.Text = "";

                bool ok = TrySendFramedEnvelope(_sock, env);
                if (ok)
                {
                    _lastSend = DateTime.Now;
                    Console.WriteLine("[SEND] " + wd.ToString());
                }
                else
                {
                    Console.WriteLine("[SEND] Wind failed");
                }
            }
        }

        private static SolarSensorData BuildSolarSensorData(DateTime now)
        {
            SolarSensorData sd = new SolarSensorData();
            sd.Time = now;

            double ins = 0.0;

            int h = now.Hour;

            if (h >= 12 && h <= 14)
            {
                ins = 1050.0;
            }
            else if (h < 12)
            {
                int diff = 12 - h;
                ins = 1050.0 - diff * 50.0;
            }
            else
            {
                int diff = h - 14;
                ins = 1050.0 - diff * 50.0;
            }

            if (ins < 0.0) ins = 0.0;

            double tcell = 25.0 + 0.025 * ins;

            sd.INS = ins;
            sd.Tcell = tcell;

            return sd;
        }

        private static WindSensorData BuildWindSensorData(DateTime now)
        {
            WindSensorData wd = new WindSensorData();
            wd.Time = now;

            double v = 0.0;
            try
            {
                v = _rng.NextDouble() * 30.0;
            }
            catch
            {
                v = 0.0;
            }

            wd.WindSpeed = v;
            return wd;
        }

        private static bool TrySendFramedEnvelope(Socket s, Envelope env)
        {
            try
            {
                if (s == null) return false;
                if (env == null) env = new Envelope();

                byte[] payload = Serializer.SerializeObject(env);
                if (payload == null) payload = new byte[0];

                int len = payload.Length;
                byte[] lenBytes = BitConverter.GetBytes(len);

                if (!SendAll(s, lenBytes)) return false;
                if (len > 0)
                {
                    if (!SendAll(s, payload)) return false;
                }

                return true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Send framed] {ex.SocketErrorCode}");
                return false;
            }
        }

        private static bool SendAll(Socket s, byte[] data)
        {
            try
            {
                if (s == null) return false;
                if (data == null) return false;

                int total = 0;
                while (total < data.Length)
                {
                    int sent = s.Send(data, total, data.Length - total, SocketFlags.None);
                    if (sent <= 0) return false;
                    total += sent;
                }
                return true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: SendAll] {ex.SocketErrorCode}");
                return false;
            }
        }

        private static void Cleanup()
        {
            try
            {
                if (_sock != null)
                {
                    _sock.Close();
                    _sock = null;
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Cleanup] {ex.SocketErrorCode}");
                _sock = null;
            }
        }
    }
}
