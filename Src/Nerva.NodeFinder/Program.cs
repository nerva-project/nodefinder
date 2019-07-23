using System.Collections.Generic;
using System;
using System.Net.Sockets;
using System.Threading;
using Log = AngryWasp.Logger.Log;
using AngryWasp.Logger;

namespace Nerva.NodeFinder
{
    public class ThreadInfo
    {
        public ulong ScannedHosts { get; set; } = 0;
        public Thread Thread { get; set; }

        public int Index { get; set; } = 0;

        public int Pinged { get; set; } = 0;

        public int Found { get; set; } = 0;

        public void PrintInfo()
        {
            Log.Instance.Write($"Thread {Index}: Scanned {ScannedHosts}, Pinged {Pinged}, Found {Found}");
        }
    }
    public static class MainClass
    {
        private static Dictionary<int, ThreadInfo> threads = new Dictionary<int, ThreadInfo>();

        [STAThread]
        public static void Main(string[] args)
        {
            Log.CreateInstance(true);
            Console.WriteLine($"Starting: {DateTime.Now}");

            //scan everything
            int index = 0;
            if (args.Length == 0)
            {
                for (uint i = 0; i < 240; i++)
                {
                    if (i == 0 || i == 10 || i == 127)
                        continue;

                    CreateScanThread(index++, $"{i}.0.0.0", $"{i}.255.255.255", (id) =>
                    {
                        threads.Remove(id);
                    });
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

                    CreateScanThread(index++, sip, eip, (id) =>
                    {
                        threads.Remove(id);
                    });
                }
            }

            Thread listener = new Thread(new ThreadStart(() =>
            {
                while(true)
                {
                    if (Console.ReadKey().Key == ConsoleKey.R)
                    {
                        Console.Clear();
                        foreach (var t in threads)
                            t.Value.PrintInfo();
                    }
                }
            }));

            listener.Start();

            Thread thread = RunPingValidatorThread();
            thread.Join();

            if (WorkQueue.HasWork)
                Log.Instance.Write(Log_Severity.Error, "Work queue still has work");

            if (threads.Count > 0)
                Log.Instance.Write(Log_Severity.Error, "Threads are still active");

            Log.Instance.Write($"Ending: {DateTime.Now}");
        }

        private static Thread RunPingValidatorThread()
        {
            Thread thread = new Thread(new ThreadStart(() =>
            {
                while (true)
                {
                    if (!WorkQueue.HasWork)
                    {
                        if (threads.Count == 0)
                            break;

                        Thread.Sleep(1000);
                        continue;
                    }

                    string host;
                    Tuple<TcpClient, ThreadInfo> result;
                    if (!WorkQueue.Pop(out host, out result))
                        continue;

                    NetworkConnection.VerifyPing(host, result);
                }
            }));

            thread.Start();
            return thread;
        }

        private static void CreateScanThread(int index, string startIp, string endIp, Action<int> finishedAction)
        {
            uint start = Helpers.ToUint(startIp);
            uint end = Helpers.ToUint(endIp);

            ThreadInfo ti = new ThreadInfo();
            ti.Index = index;

            ti.Thread = new Thread(new ThreadStart(() =>
            {
                for (uint j = start; j <= end; j++)
                {
                    string ip = Helpers.ToIP(j);
                    TcpClient client;

                    if (NetworkConnection.Ping(ip, out client))
                    {
                        ti.Pinged++;
                        WorkQueue.Push(ip, client, ti);
                    }

                    ti.ScannedHosts++;
                }

                finishedAction.Invoke(index);
            }));

            threads.Add(index, ti);
            ti.Thread.Start();
        }
    }
}
