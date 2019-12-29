using System;
using AngryWasp.Logger;
using AngryWasp.Helpers;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Nerva.Levin;
using System.Threading;
using System.Linq;

namespace Nerva.NodeFinder
{
    public static class MainClass
    {
        [STAThread]
        public static void Main(string[] args)
        {
            CommandLineParser clp = CommandLineParser.Parse(args);
            Log.CreateInstance(false);
            Log.Instance.SetColor(ConsoleColor.DarkGray);

            if (clp["--host"] == null)
                Log.Instance.Write(Log_Severity.Fatal, "Need a host");

            Crawler c = new Crawler();
            c.ProbeNode(clp["--host"].Value);
            Thread worker = c.CreateWorkerThread();

            worker.Start();
            worker.Join();
        }
    }

    public class Crawler
    {
        ConcurrentDictionary<string, Tuple<string, ushort, ushort>> workQueue = new ConcurrentDictionary<string, Tuple<string, ushort, ushort>>();
        HashSet<string> allNodes = new HashSet<string>();
        
        int foundCount = 0;
        public Thread CreateWorkerThread()
        {
            return new Thread(new ThreadStart(() =>
            {
                ulong timeWithoutWork = 0;
                while (true)
                {
                    if (workQueue.IsEmpty)
                    {
                        if (timeWithoutWork >= 60 * 1000 * 10)
                        {
                            //10 minutes without work
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Time without work exceeded 10 minutes");
                            Console.WriteLine($"Found {foundCount} nodes");
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                        timeWithoutWork += 100;
                        Thread.Sleep(100);
                        continue;
                    }

                    Tuple<string, ushort, ushort> val;
                    //val.Item3 contains the rpc port. do we need it?
                    if (workQueue.TryRemove(workQueue.Keys.First(), out val))
                    {
                        timeWithoutWork = 0;
                        ProbeNode(val.Item1, val.Item2);
                    }
                }
            }));
        }

        public void ProbeNode(string host, int port = 17565)
        {
            allNodes.Add(host);

            NetworkConnection nc = new NetworkConnection();
            object pl = nc.Run(host, port);

            if (pl != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"SUCCESS: {host}:{port}");
                Console.ForegroundColor = ConsoleColor.White;

                ++foundCount;

                object[] sec = (object[])pl;
                foreach (var s in sec)
                {
                    Section entry = (Section)s;
                    Section adr = (Section)entry.Entries["adr"];
                    Section addr = (Section)adr.Entries["addr"];

                    long lastSeen = (long)entry.Entries["last_seen"];
                    ulong now = DateTimeHelper.TimestampNow();
                    ulong diff = now - (ulong)lastSeen;

                    if (diff > 60 * 60 * 4)
                        continue; //last seen more than 4 hour ago

                    uint ipInt = (uint)addr.Entries["m_ip"];
                    if (ipInt == 0)
                        continue;

                    string ip = ToIP(ipInt);
                    ushort p2pPort = (ushort)addr.Entries["m_port"];
                    ushort rpcPort = (ushort)entry.Entries["rpc_port"];

                    if (!allNodes.Contains(ip))
                        workQueue.TryAdd(ip, new Tuple<string, ushort, ushort>(ip, p2pPort, rpcPort));
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($" FAILED: {host}:{port}");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        public static string ToIP(uint ip)
        {
            return String.Format("{3}.{2}.{1}.{0}",
                ip >> 24, (ip >> 16) & 0xff, (ip >> 8) & 0xff, ip & 0xff);
        }
    }
}
