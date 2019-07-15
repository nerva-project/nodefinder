using System.Collections.Generic;
using System;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nerva.Rpc.Daemon;
using Nerva.Rpc;
using Log = AngryWasp.Logger.Log;
using AngryWasp.Helpers;
using System.Net;
using System.Text.RegularExpressions;

namespace Nerva.NodeFinder
{
    public static class MainClass
    {
        private const int PORT = 17565;

        [STAThread]
        public static void Main(string[] args)
        {
            Log.CreateInstance(true);
            Console.WriteLine($"Starting: {DateTime.Now}");
            int tc = 0;
            List<Thread> threads = new List<Thread>();

            //scan everything
            if (args.Length == 0)
            {
                for (uint i = 0; i < 240; i++)
                {
                    uint start = ToUint($"{i}.0.0.0");
                    uint end = ToUint($"{i}.255.255.255");

                    if (i == 0 || i == 10 || i == 127)
                        continue;

                    Thread thread = new Thread(new ThreadStart(() =>
                    {
                        Log.Instance.Write($"Starting thread {tc++}: {start} - {end}");
                        for (uint j = start; j <= end; j++)
                        {
                            string ip = ToIP(j);
                            Task<long> task = Task.Run<long>(async () => await Ping(ip, PORT, 250));

                            if (task.Result != -1)
                            {
                                Log.Instance.SetColor(ConsoleColor.Cyan);
                                Log.Instance.Write($"{ip}: Open");
                                Log.Instance.SetColor(ConsoleColor.White);
#pragma warning disable 4014
                                new AddPeer(new AddPeerRequestData
                                {
                                    Host = ip
                                }, null, null).RunAsync();
#pragma warning restore 4014
                            }
                        }
                    }));

                    thread.Start();
                    threads.Add(thread);
                }
            }
            else
            {
                foreach (string r in args)
                {
                    string sip, eip;
                    if (!CidrToIpRange(r, out sip, out eip))
                    {
                        Log.Instance.Write("Failed to parse argument");
                        return;
                    }

                    uint start = ToUint(sip);
                    uint end = ToUint(eip);

                    Thread thread = new Thread(new ThreadStart(() =>
                    {
                        Log.Instance.Write($"Starting thread {tc++}: {sip} - {eip}");
                        for (uint j = start; j <= end; j++)
                        {
                            string ip = ToIP(j);
                            //Log.Instance.SetColor(ConsoleColor.DarkMagenta);
                            //Log.Instance.Write($"{ip}: Checking");
                            //Log.Instance.SetColor(ConsoleColor.White);
                            Task<long> task = Task.Run<long>(async () => await Ping(ip, PORT, 250));
                            if (task.Result != -1)
                            {
                                Log.Instance.SetColor(ConsoleColor.Cyan);
                                Log.Instance.Write($"{ip}: Open");
                                Log.Instance.SetColor(ConsoleColor.White);
#pragma warning disable 4014
                                new AddPeer(new AddPeerRequestData
                                {
                                    Host = ip
                                }, null, null).RunAsync();
#pragma warning restore 4014
                                }
                        }
                    }));

                    thread.Start();
                    threads.Add(thread);
                }
            }

            foreach (Thread t in threads)
                t.Join();

            Console.WriteLine($"Ending: {DateTime.Now}");
        }

        private static bool CidrToIpRange(string input, out string start, out string end)
        {
            start = end = null;

            if (!input.Contains("/"))
                return false;

            string[] s1 = input.Split('/');
            uint mask = ~(0xFFFFFFFF >> int.Parse(s1[1]));

            uint ip = ToUint(s1[0]);
            start = ToIP(ip & mask);
            end = ToIP((ip & mask) | ~mask);
            return true;
        }

        private static string ToIP(uint ip)
        {
            return String.Format("{0}.{1}.{2}.{3}",
                ip >> 24, (ip >> 16) & 0xff, (ip >> 8) & 0xff, ip & 0xff);
        }

        private static uint ToUint(string ip)
        {
            var address = IPAddress.Parse(ip);
            byte[] b = address.GetAddressBytes();
            Array.Reverse(b);
            return BitShifter.ToUInt(b);
        }

        private static async Task<long> Ping(string host, int port, int timeOut)
        {
            long elapsed = -1;
            Stopwatch watch = new Stopwatch();

            using (TcpClient tcp = new TcpClient())
            {
                try
                {
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    {
                        StartConnection(host, port, tcp, watch, cts);
                        await Task.Delay(timeOut, cts.Token);
                    }
                }
                catch { }
                finally
                {
                    if (tcp.Connected)
                    {
                        tcp.GetStream().Close();
                        elapsed = watch.ElapsedMilliseconds;
                    }
                    tcp.Close();
                }
            }

            return elapsed;
        }

        private static async void StartConnection(string host, int port, TcpClient tcp, Stopwatch watch, CancellationTokenSource cts)
        {
            try
            {
                watch.Start();
                await tcp.ConnectAsync(host, port);
                watch.Stop();
                cts.Cancel();
            }
            catch { }
        }
    }
}
