using System;
using AngryWasp.Logger;
using AngryWasp.Helpers;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Nerva.Levin;
using System.Threading;
using System.Linq;
using System.Diagnostics;

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
            {
                Log.Instance.Write(Log_Severity.Fatal, "Need a host");
            }

            string host = clp["--host"].Value;

            Crawler c = new Crawler();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Crawling seed {host}");
            Console.ForegroundColor = ConsoleColor.White;
            c.ProbeNode(host);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Found {c.AllNodes.Count} nodes, verified {c.VerifiedNodes.Count}");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }

    public class Crawler
    {
        HashSet<string> verifiedNodes = new HashSet<string>();
        HashSet<string> allNodes = new HashSet<string>();

        public HashSet<string> AllNodes => allNodes;
        public HashSet<string> VerifiedNodes => verifiedNodes;

        public void ProbeNode(string host, int port = 17565)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"{host.PadLeft(15)}: ");

            NetworkConnection nc = new NetworkConnection();
            object pl = nc.Run(host, port);

            if (pl != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("OK");
                Console.ForegroundColor = ConsoleColor.White;
                verifiedNodes.Add(host);

                object[] sec = (object[])pl;
                foreach (var s in sec)
                {
                    Section entry = (Section)s;
                    Section adr = (Section)entry.Entries["adr"];
                    Section addr = (Section)adr.Entries["addr"];

                    long lastSeen = (long)entry.Entries["last_seen"];
                    ulong now = DateTimeHelper.TimestampNow();
                    ulong diff = now - (ulong)lastSeen;

                    uint ipInt = 0;
                    
                    if (addr.Entries.ContainsKey("m_ip"))
                        ipInt = (uint)addr.Entries["m_ip"];

                    if (ipInt == 0)
                        continue;

                    string ip = ToIP(ipInt);
                    
                    if (!allNodes.Contains(ip))
                    {
                        allNodes.Add(ip);
                        ProbeNode(ip);
                    }
                    else
                    {
                        //Console.ForegroundColor = ConsoleColor.DarkGray;
                        //Console.Write($"{ip.PadLeft(15)}: ");
                        //Console.ForegroundColor = ConsoleColor.Cyan;
                        //Console.WriteLine("DUP");
                        //Console.ForegroundColor = ConsoleColor.White;
                    }
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("BAD");
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
