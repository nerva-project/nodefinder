using System.Collections.Generic;
using System;
using System.Net.Sockets;
using System.Threading;
using Log = AngryWasp.Logger.Log;
using AngryWasp.Logger;

namespace Nerva.NodeFinder
{
    public static class MainClass
    {
        private static Dictionary<int, Thread> threads = new Dictionary<int, Thread>();

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

                    CreateScanThread(index++, $"{i}.0.0.0", $"{i}.255.255.255", (id) => {
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

                    CreateScanThread(index++, sip, eip, (id) => {
                        threads.Remove(id);
                    });
                }
            }

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
                while(true)
                {
                    if (!WorkQueue.HasWork)
                    {
                        if (threads.Count == 0)
                            break;

                        Thread.Sleep(1000);
                        continue;
                    }

                    string host;
                    TcpClient client;
                    if (!WorkQueue.Pop(out host, out client))
                        continue;

                    NetworkConnection.VerifyPing(host, client);
                }
            }));

            thread.Start();
            return thread;
        }

        private static void CreateScanThread(int index, string startIp, string endIp, Action<int> finishedAction)
        {
            uint start = Helpers.ToUint(startIp);
            uint end = Helpers.ToUint(endIp);

            Thread thread = new Thread(new ThreadStart(() =>
            {
                for (uint j = start; j <= end; j++)
                {
                    string ip = Helpers.ToIP(j);
                    TcpClient client;
                    if (NetworkConnection.Ping(ip, out client))
                        WorkQueue.Push(ip, client);
                }

                finishedAction.Invoke(index);
            }));

            thread.Start();
            threads.Add(index, thread);
        }
    }
}
