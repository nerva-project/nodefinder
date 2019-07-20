using System.Collections.Generic;
using System;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nerva.Rpc.Daemon;
using Log = AngryWasp.Logger.Log;
using AngryWasp.Helpers;
using System.Net;
using Nerva.Levin.Requests;

namespace Nerva.NodeFinder
{
    public static class MainClass
    {
        private const int PORT = 17565;

        private static List<Thread> threads = new List<Thread>();

        [STAThread]
        public static void Main(string[] args)
        {
            Log.CreateInstance(true);
            Console.WriteLine($"Starting: {DateTime.Now}");

            //scan everything
            if (args.Length == 0)
            {
                for (uint i = 0; i < 240; i++)
                {
                    if (i == 0 || i == 10 || i == 127)
                        continue;

                    CreateScanThread($"{i}.0.0.0", $"{i}.255.255.255");
                }
            }
            else
            {
                foreach (string r in args)
                {
                    string sip, eip;
                    if (!Helpers.CidrToIpRange(r, out sip, out eip))
                    {
                        Log.Instance.Write("Failed to parse argument");
                        return;
                    }

                    CreateScanThread(sip, eip);
                }
            }

            foreach (Thread t in threads)
                t.Join();

            Console.WriteLine($"Ending: {DateTime.Now}");
        }

        private static void CreateScanThread(string startIp, string endIp)
        {
            uint start = Helpers.ToUint(startIp);
            uint end = Helpers.ToUint(endIp);

            Thread thread = new Thread(new ThreadStart(() =>
            {
                Log.Instance.Write($"Starting thread: {startIp} - {endIp}");
                for (uint j = start; j <= end; j++)
                {
                    string ip =Helpers. ToIP(j);
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
                        NetworkStream ns = tcp.GetStream();
                        //todo: send handshake to confirm there is a node listening
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
