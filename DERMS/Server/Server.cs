using DERMS.Enums;
using DERMS.Models;
using DERMS.Networking;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace DERMS.Server
{
    internal static class ServerApp
    {
        private static Socket _listen;
        private static readonly List<Socket> _clients = new List<Socket>();

        private static readonly Dictionary<string, GeneratorInfo> _generators = new Dictionary<string, GeneratorInfo>();
        private static readonly List<Production> _productions = new List<Production>();

        public static void Run()
        {
            Console.WriteLine("=== DERMS SERVER (TCP) ===");

            if (!InitListen())
            {
                Console.WriteLine("Server ne moze da startuje TCP listen. Kraj.");
                return;
            }

            Console.WriteLine("TCP listen port: " + NetConfig.ServerTcpPort);
            Console.WriteLine("Komande: Q (kraj + statistika)");
            Console.WriteLine();

            bool running = true;
            while (running)
            {
                AcceptAndReadOnce();

                if (Console.KeyAvailable)
                {
                    var k = Console.ReadKey(true);
                    if (k.Key == ConsoleKey.Q) running = false;
                }
            }

            PrintStats();
            ShutdownAll();
            Console.WriteLine("Server ugasen.");
        }

        private static bool InitListen()
        {
            try
            {
                _listen = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listen.Blocking = false;

                _listen.Bind(new IPEndPoint(IPAddress.Any, NetConfig.ServerTcpPort));
                _listen.Listen(50);

                return true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: InitListen] {ex.SocketErrorCode}");

                if (_listen != null) _listen.Close();
                _listen = null;
                return false;
            }
        }

        private static void AcceptAndReadOnce()
        {
            if (_listen == null) return;

            List<Socket> readList = new List<Socket>();
            readList.Add(_listen);

            for (int i = 0; i < _clients.Count; i++)
            {
                Socket c = _clients[i];
                if (c != null) readList.Add(c);
            }

            try
            {
                Socket.Select(readList, null, null, NetConfig.SelectTimeoutUs);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Select] {ex.SocketErrorCode}");
                return;
            }

            for (int i = 0; i < readList.Count; i++)
            {
                Socket s = readList[i];
                if (s == null) continue;

                if (s == _listen)
                {
                    AcceptClient();
                    continue;
                }

                if (IsSocketDead(s))
                {
                    RemoveClient(s);
                    continue;
                }

                int avail = 0;
                try
                {
                    avail = s.Available;
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"[SOCKET: Available] {ex.SocketErrorCode}");
                    RemoveClient(s);
                    continue;
                }

                if (avail < 4) continue;

                Envelope env = TryReceiveFramedEnvelope(s);
                bool empty = (env.Type == MessageType.None)
                             && (string.IsNullOrEmpty(env.Text))
                             && (env.Payload == null || env.Payload.Length == 0);

                if (empty)
                {
                    if (IsSocketDead(s)) RemoveClient(s);
                    continue;
                }

                HandleEnvelope(s, env);
            }
        }

        private static void AcceptClient()
        {
            if (_listen == null) return;

            Socket c = null;
            try
            {
                c = _listen.Accept();
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Accept] {ex.SocketErrorCode}");
                return;
            }

            if (c == null) return;

            try
            {
                c.Blocking = true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: SetBlocking(client)] {ex.SocketErrorCode}");
            }

            _clients.Add(c);

            string who = (c.RemoteEndPoint != null) ? c.RemoteEndPoint.ToString() : "";
            Console.WriteLine("[CONNECT] " + who);
        }

        private static void RemoveClient(Socket c)
        {
            if (c == null) return;

            string who = (c.RemoteEndPoint != null) ? c.RemoteEndPoint.ToString() : "";
            Console.WriteLine("[DISCONNECT] " + who);

            _clients.Remove(c);

            try
            {
                c.Close();
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Close client] {ex.SocketErrorCode}");
            }
        }

        private static bool IsSocketDead(Socket s)
        {
            if (s == null) return true;

            try
            {
                bool read = s.Poll(0, SelectMode.SelectRead);
                int avail = s.Available;
                if (read && avail == 0) return true;
                return false;
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Poll/Available] {ex.SocketErrorCode}");
                return true;
            }
        }

        private static void HandleEnvelope(Socket client, Envelope env)
        {
            if (env == null) env = new Envelope();

            if (env.Type == MessageType.Generator_Register)
            {
                GeneratorInfo info = Serializer.DeserializeObject<GeneratorInfo>(env.Payload);
                if (info == null) info = new GeneratorInfo();

                if (string.IsNullOrWhiteSpace(info.GeneratorId))
                    info.GeneratorId = env.GeneratorId ?? "";

                if (string.IsNullOrWhiteSpace(info.GeneratorId))
                    info.GeneratorId = "UN" + DateTime.Now.Ticks;

                _generators[info.GeneratorId] = info;

                Console.WriteLine("[REGISTER] " + info.ToString());

                TrySendFramedEnvelope(client, new Envelope
                {
                    Type = MessageType.Pong,
                    GeneratorId = info.GeneratorId,
                    Text = "REGISTERED"
                });

                return;
            }

            if (env.Type == MessageType.Generator_Production)
            {
                string genId = env.GeneratorId ?? "";

                if (string.IsNullOrWhiteSpace(genId))
                {
                    string t = env.Text ?? "";
                    string[] parts = t.Split('|');
                    if (parts.Length >= 3) genId = parts[0];
                }

                double p = 0.0;
                double q = 0.0;
                bool parsed = false;

                try
                {
                    string t = env.Text ?? "";
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        string[] a = t.Split('|');

                        if (a.Length == 2)
                        {
                            p = Safe.ToDouble(a[0], 0.0);
                            q = Safe.ToDouble(a[1], 0.0);
                            parsed = true;
                        }
                        else if (a.Length >= 3)
                        {
                            p = Safe.ToDouble(a[1], 0.0);
                            q = Safe.ToDouble(a[2], 0.0);
                            parsed = true;
                        }
                        else
                        {
                            string[] b = t.Split(';');
                            if (b.Length == 2)
                            {
                                p = Safe.ToDouble(b[0], 0.0);
                                q = Safe.ToDouble(b[1], 0.0);
                                parsed = true;
                            }
                        }
                    }
                }
                catch
                {
                    parsed = false;
                }

                if (!parsed)
                {
                    p = 0.0;
                    q = 0.0;
                }

                Production pr = new Production
                {
                    GeneratorId = genId,
                    P = p,
                    Q = q,
                    ReceivedAt = DateTime.Now
                };

                _productions.Add(pr);

                Console.WriteLine("[PROD] " + pr.GeneratorId + " P=" + pr.P + " Q=" + pr.Q + " @ " + pr.ReceivedAt.ToString("HH:mm:ss"));
                return;
            }

            if (env.Type == MessageType.Ping)
            {
                TrySendFramedEnvelope(client, new Envelope { Type = MessageType.Pong, Text = "PONG" });
                return;
            }

            Console.WriteLine("[INFO] " + env.ToString());
        }

        private static bool TrySendFramedEnvelope(Socket s, Envelope env)
        {
            if (s == null) return false;
            if (env == null) env = new Envelope();

            byte[] payload = Serializer.SerializeObject(env);
            if (payload == null) payload = new byte[0];

            int len = payload.Length;
            byte[] lenBytes = BitConverter.GetBytes(len);

            if (!SendAll(s, lenBytes)) return false;
            if (len > 0) return SendAll(s, payload);

            return true;
        }

        private static Envelope TryReceiveFramedEnvelope(Socket s)
        {
            if (s == null) return new Envelope();

            byte[] lenBuf = ReceiveExactly(s, 4);
            if (lenBuf == null || lenBuf.Length != 4) return new Envelope();

            int len = 0;
            try
            {
                len = BitConverter.ToInt32(lenBuf, 0);
            }
            catch
            {
                return new Envelope();
            }

            if (len <= 0 || len > 5_000_000) return new Envelope();

            byte[] payload = ReceiveExactly(s, len);
            if (payload == null || payload.Length != len) return new Envelope();

            Envelope env = Serializer.DeserializeObject<Envelope>(payload);
            if (env == null) env = new Envelope();
            return env;
        }

        private static bool SendAll(Socket s, byte[] data)
        {
            if (s == null) return false;
            if (data == null) return false;

            try
            {
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
            if (s == null) return new byte[0];
            if (n <= 0) return new byte[0];

            try
            {
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

        private static void PrintStats()
        {
            Console.WriteLine();
            Console.WriteLine("=== STATISTIKA ===");

            double sumSolar = 0.0;
            int cntSolar = 0;

            double sumWind = 0.0;
            int cntWind = 0;

            double totalQ = 0.0;

            for (int i = 0; i < _productions.Count; i++)
            {
                Production p = _productions[i];
                if (p == null) continue;

                string id = p.GeneratorId ?? "";

                GeneratorType type = GeneratorType.Solar;
                bool hasType = false;

                if (!string.IsNullOrWhiteSpace(id) && _generators.ContainsKey(id))
                {
                    type = _generators[id].Type;
                    hasType = true;
                }

                if (!hasType && id.Length >= 2)
                {
                    string pref = id.Substring(0, 2).ToUpperInvariant();
                    type = (pref == "WI") ? GeneratorType.Wind : GeneratorType.Solar;
                }

                totalQ += p.Q;

                if (type == GeneratorType.Wind)
                {
                    sumWind += p.P;
                    cntWind++;
                }
                else
                {
                    sumSolar += p.P;
                    cntSolar++;
                }
            }

            double avgSolar = (cntSolar == 0) ? 0.0 : (sumSolar / cntSolar);
            double avgWind = (cntWind == 0) ? 0.0 : (sumWind / cntWind);

            Console.WriteLine("Solar avg P = " + avgSolar + " (N=" + cntSolar + ")");
            Console.WriteLine("Wind  avg P = " + avgWind + " (N=" + cntWind + ")");
            Console.WriteLine("Ukupno Q = " + totalQ);
            Console.WriteLine("==================");
            Console.WriteLine();
        }

        private static void ShutdownAll()
        {
            for (int i = _clients.Count - 1; i >= 0; i--)
            {
                Socket c = _clients[i];
                _clients.RemoveAt(i);

                try
                {
                    if (c != null) c.Close();
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"[SOCKET: Close client] {ex.SocketErrorCode}");
                }
            }

            try
            {
                if (_listen != null) _listen.Close();
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[SOCKET: Close listen] {ex.SocketErrorCode}");
            }

            _listen = null;
        }
    }
}
