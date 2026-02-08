using DERMS.Enums;
using DERMS.Models;
using DERMS.Networking;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace DERMS.Generator
{
    internal static class GeneratorApp
    {
        private static Socket _serverSock;

        private static Socket _sensorListen;
        private static Socket _sensorClient;

        private static Socket _udpControl;
        private static IPEndPoint _udpBindEp;

        private static GeneratorInfo _info = new GeneratorInfo();

        private static SolarSensorData _lastSolar = new SolarSensorData();
        private static WindSensorData _lastWind = new WindSensorData();

        private static DateTime _lastProdSend = DateTime.MinValue;
        private static int _sendPeriodMs = 1000;

        public static void Run(string[] args)
        {
            Console.WriteLine("=== DERMS GENERATOR ===");

            GeneratorType type = ReadTypeFromUser();
            double pn = ReadNominalPower(type);

            string genId = Safe.NewGeneratorId(type);

            _info = new GeneratorInfo();
            _info.GeneratorId = genId;
            _info.Type = type;
            _info.NominalPowerKw = pn;

            _info.GeneratorIp = "127.0.0.1";

            _info.UdpControlPort = NetConfig.DefaultGeneratorUdpControlPort;
            _info.TcpSensorPort = NetConfig.DefaultGeneratorTcpSensorPort;

            if (!InitUdpControl(_info.UdpControlPort))
            {
                Console.WriteLine("Neuspesno init UDP control.");
                Cleanup();
                return;
            }

            if (!InitSensorListener(_info.TcpSensorPort))
            {
                Console.WriteLine("Neuspesno init TCP sensor listener.");
                Cleanup();
                return;
            }

            Console.WriteLine("Server IP (ENTER za 127.0.0.1):");
            string serverIp = Safe.ReadLineNonNull();
            if (string.IsNullOrWhiteSpace(serverIp)) serverIp = "127.0.0.1";

            if (!ConnectToServer(serverIp.Trim(), NetConfig.ServerTcpPort))
            {
                Console.WriteLine("Neuspesno povezivanje na DERMS server.");
                Cleanup();
                return;
            }

            Console.WriteLine("Generator ID: " + _info.GeneratorId);
            Console.WriteLine("Tip: " + _info.Type + " | Pn=" + _info.NominalPowerKw + " kW");
            Console.WriteLine("UDP control port: " + _info.UdpControlPort);
            Console.WriteLine("TCP sensor port: " + _info.TcpSensorPort);
            Console.WriteLine();

            SendRegister();

            Console.WriteLine("Komande: Q (izlaz)");
            Console.WriteLine();

            bool running = true;
            while (running)
            {
                AcceptSensorIfAny();

                ReceiveSensorIfAny();

                ReceiveUdpControlIfAny();

                MaybeSendProduction();

                ReceiveServerIfAny();

                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true);
                    if (k.Key == ConsoleKey.Q) running = false;
                }
            }

            Cleanup();
            Console.WriteLine("Generator ugasen.");
        }

        private static GeneratorType ReadTypeFromUser()
        {
            Console.WriteLine("Izaberi tip generatora:");
            Console.WriteLine("1) Solar");
            Console.WriteLine("2) Wind");
            Console.Write(">> ");

            int x = Safe.ToInt(Safe.ReadLineNonNull(), 1);
            if (x == 2) return GeneratorType.Wind;
            return GeneratorType.Solar;
        }

        private static double ReadNominalPower(GeneratorType t)
        {
            if (t == GeneratorType.Solar)
            {
                Console.WriteLine("Unesi nominalnu snagu (100-500 kW):");
                Console.Write(">> ");
                double pn = Safe.ToDouble(Safe.ReadLineNonNull(), 100.0);
                if (pn < 100) pn = 100;
                if (pn > 500) pn = 500;
                return pn;
            }

            Console.WriteLine("Unesi nominalnu snagu (500-1000 kW):");
            Console.Write(">> ");
            double p2 = Safe.ToDouble(Safe.ReadLineNonNull(), 500.0);
            if (p2 < 500) p2 = 500;
            if (p2 > 1000) p2 = 1000;
            return p2;
        }

        private static bool InitUdpControl(int port)
        {
            try
            {
                _udpControl = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _udpControl.Blocking = false;

                _udpControl.Bind(new IPEndPoint(IPAddress.Any, 0));

                var local = (IPEndPoint)_udpControl.LocalEndPoint;
                _info.UdpControlPort = local.Port;

                return true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: UDP init] {ex.SocketErrorCode}");
                return false;
            }
        }

        private static bool InitSensorListener(int port)
        {
            try
            {
                _sensorListen = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    Blocking = false
                };

                _sensorListen.Bind(new IPEndPoint(IPAddress.Any, 0));
                _sensorListen.Listen(5);

                var local = (IPEndPoint)_sensorListen.LocalEndPoint;
                _info.TcpSensorPort = local.Port;

                return true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Sensor listen init] {ex.SocketErrorCode}");
                return false;
            }
        }

        private static bool ConnectToServer(string ip, int port)
        {
            try
            {
                _serverSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    Blocking = true
                };

                IPAddress addr = IPAddress.Parse(ip);
                _serverSock.Connect(new IPEndPoint(addr, port));

                return true;
            }
            catch (FormatException)
            {
                Console.WriteLine("[GEN] Nevalidan IP format.");
                return false;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Connect server] {ex.SocketErrorCode}");
                return false;
            }
        }

        private static void AcceptSensorIfAny()
        {
            if (_sensorListen == null) return;
            if (_sensorClient != null) return;

            try
            {
                List<Socket> rl = new List<Socket> { _sensorListen };
                Socket.Select(rl, null, null, 50 * 1000);

                if (rl.Count > 0 && rl[0] == _sensorListen)
                {
                    _sensorClient = _sensorListen.Accept();
                    _sensorClient.Blocking = true;

                    string who = (_sensorClient.RemoteEndPoint != null) ? _sensorClient.RemoteEndPoint.ToString() : "";
                    Console.WriteLine("[SENSOR CONNECT] " + who);
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Accept sensor] {ex.SocketErrorCode}");
            }
        }

        private static void ReceiveSensorIfAny()
        {
            if (_sensorClient == null) return;

            try
            {
                int avail = _sensorClient.Available;
                if (avail < 4) return;

                Envelope env = TryReceiveFramedEnvelope(_sensorClient) ?? new Envelope();
                bool empty = (env.Type == MessageType.None)
                             && (string.IsNullOrEmpty(env.Text))
                             && (env.Payload == null || env.Payload.Length == 0);

                if (empty) return;

                if (env.Type == MessageType.Sensor_Data)
                {
                    if (_info.Type == GeneratorType.Solar)
                    {
                        SolarSensorData sd = Serializer.DeserializeObject<SolarSensorData>(env.Payload) ?? new SolarSensorData();
                        _lastSolar = sd;

                        Console.WriteLine("[SENSOR] " + sd.ToString());
                    }
                    else
                    {
                        WindSensorData wd = Serializer.DeserializeObject<WindSensorData>(env.Payload) ?? new WindSensorData();
                        _lastWind = wd;

                        Console.WriteLine("[SENSOR] " + wd.ToString());
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Receive sensor] {ex.SocketErrorCode}");
                CloseSensorClient();
            }
        }

        private static void CloseSensorClient()
        {
            try
            {
                if (_sensorClient != null)
                {
                    _sensorClient.Close();
                    _sensorClient = null;
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Close sensor] {ex.SocketErrorCode}");
                _sensorClient = null;
            }
        }

        private static void ReceiveUdpControlIfAny()
        {
            if (_udpControl == null) return;

            try
            {
                bool readable = _udpControl.Poll(0, SelectMode.SelectRead);
                if (!readable) return;

                EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] buf = new byte[NetConfig.BufferSize];

                int r = _udpControl.ReceiveFrom(buf, ref remote);
                if (r <= 0) return;

                byte[] data = new byte[r];
                Buffer.BlockCopy(buf, 0, data, 0, r);

                Envelope env = Serializer.DeserializeObject<Envelope>(data) ?? new Envelope();
                Console.WriteLine("[UDP CONTROL] From " + remote.ToString() + " -> " + env.Type + " | " + (env.Text ?? ""));

            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: UDP receive] {ex.SocketErrorCode}");
            }
        }

        private static void ReceiveServerIfAny()
        {
            if (_serverSock == null) return;

            try
            {
                int avail = _serverSock.Available;
                if (avail < 4) return;

                Envelope env = TryReceiveFramedEnvelope(_serverSock) ?? new Envelope();
                bool empty = (env.Type == MessageType.None)
                             && (string.IsNullOrEmpty(env.Text))
                             && (env.Payload == null || env.Payload.Length == 0);

                if (empty) return;

                if (env.Type == MessageType.Pong)
                {
                    Console.WriteLine("[SERVER] " + (env.Text ?? ""));
                }
                else if (env.Type == MessageType.Error)
                {
                    Console.WriteLine("[SERVER ERROR] " + (env.Text ?? ""));
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Receive server] {ex.SocketErrorCode}");
            }
        }

        private static void SendRegister()
        {
            if (_serverSock == null) return;

            Envelope env = new Envelope
            {
                Type = MessageType.Generator_Register,
                GeneratorId = _info.GeneratorId,
                Text = "",
                Payload = Serializer.SerializeObject(_info)
            };

            bool ok = TrySendFramedEnvelope(_serverSock, env);
            if (ok) Console.WriteLine("[SEND] REGISTER");
            else Console.WriteLine("[SEND] REGISTER failed");
        }

        private static void MaybeSendProduction()
        {
            if (_serverSock == null) return;

            if (_lastProdSend != DateTime.MinValue)
            {
                double ms = (DateTime.Now - _lastProdSend).TotalMilliseconds;
                if (ms < _sendPeriodMs) return;
            }

            double p = 0.0;
            double q = 0.0;

            if (_info.Type == GeneratorType.Solar)
            {
                double ins = _lastSolar != null ? _lastSolar.INS : 0.0;
                double tcell = _lastSolar != null ? _lastSolar.Tcell : 25.0;

                double tempFactor = 1.0 - 0.005 * (tcell - 25.0);
                p = _info.NominalPowerKw * ins * 0.00095 * tempFactor;

                q = 0.0;
            }
            else
            {
                double v = _lastWind != null ? _lastWind.WindSpeed : 0.0;

                if (v < 3.5 || v > 25.0) p = 0.0;
                else if (v >= 3.5 && v < 14.0) p = (v - 3.5) * 0.035;
                else p = _info.NominalPowerKw;

                q = 0.05 * p;
            }

            Envelope env = new Envelope
            {
                Type = MessageType.Generator_Production,
                GeneratorId = _info.GeneratorId,
                Text = p.ToString() + "|" + q.ToString(),
                Payload = new byte[0]
            };

            bool ok = TrySendFramedEnvelope(_serverSock, env);
            if (ok)
            {
                _lastProdSend = DateTime.Now;
                Console.WriteLine("[SEND] PROD P=" + p + " Q=" + q);
            }
            else
            {
                Console.WriteLine("[SEND] PROD failed");
            }
        }

        private static bool TrySendFramedEnvelope(Socket s, Envelope env)
        {
            try
            {
                if (s == null) return false;
                if (env == null) env = new Envelope();

                byte[] payload = Serializer.SerializeObject(env) ?? (new byte[0]);
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

        private static Envelope TryReceiveFramedEnvelope(Socket s)
        {
            try
            {
                if (s == null) return new Envelope();

                byte[] lenBuf = ReceiveExactly(s, 4);
                if (lenBuf == null || lenBuf.Length != 4) return new Envelope();

                int len = BitConverter.ToInt32(lenBuf, 0);
                if (len <= 0 || len > 5_000_000) return new Envelope();

                byte[] payload = ReceiveExactly(s, len);
                if (payload == null || payload.Length != len) return new Envelope();

                Envelope env = Serializer.DeserializeObject<Envelope>(payload) ?? new Envelope();
                return env;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Receive framed] {ex.SocketErrorCode}");
                return new Envelope();
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

        private static byte[] ReceiveExactly(Socket s, int n)
        {
            try
            {
                if (s == null) return new byte[0];
                if (n <= 0) return new byte[0];

                byte[] buf = new byte[n];
                int read = 0;

                while (read < n)
                {
                    int r = s.Receive(buf, read, n - read, SocketFlags.None);
                    if (r <= 0) return new byte[0];
                    read += r;
                }
                return buf;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: ReceiveExactly] {ex.SocketErrorCode}");
                return new byte[0];
            }
        }

        private static void Cleanup()
        {
            try 
            { 
                if (_sensorClient != null) _sensorClient.Close();
            } 
            catch (SocketException ex)
            { 
                Console.WriteLine($"[SOCKET: Cleanup sensorClient] {ex.SocketErrorCode}");
            }

            try
            { 
                if (_sensorListen != null) _sensorListen.Close();
            } 
            catch (SocketException ex)
            { 
                Console.WriteLine($"[SOCKET: Cleanup sensorListen] {ex.SocketErrorCode}");
            }

            try
            { 
                if (_udpControl != null) _udpControl.Close();
            }
            catch (SocketException ex) 
            { 
                Console.WriteLine($"[SOCKET: Cleanup udp] {ex.SocketErrorCode}");
            }

            try
            {
                if (_serverSock != null) _serverSock.Close();
            }
            catch (SocketException ex)
            { 
                Console.WriteLine($"[SOCKET: Cleanup serverSock] {ex.SocketErrorCode}");
            }

            _sensorClient = null;
            _sensorListen = null;
            _udpControl = null;
            _serverSock = null;
        }
    }
}
