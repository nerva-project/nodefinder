using System;
using System.Net.Sockets;
using AngryWasp.Logger;
using AngryWasp.Helpers;

namespace Nerva.NodeFinder
{
    public static class MainClass
    {
        [STAThread]
        public static int Main(string[] args)
        {
            CommandLineParser clp = CommandLineParser.Parse(args);
            Log.CreateInstance(true);

            if (clp["--host"] == null)
            {
                Log.Instance.Write(Log_Severity.Fatal, "Need a host");
                return -1;
            }

            string host = clp["--host"].Value;
            TcpClient client;

            if (!NetworkConnection.Ping(host, out client))
            {
                Log.Instance.Write(Log_Severity.Error, "Validation failed. No ping");
                return 0;
            }

            if (!NetworkConnection.VerifyPing(host, client))
            {
                Log.Instance.Write(Log_Severity.Error, "Validation failed. Node not detected");
                return 0;
            }

            Log.Instance.Write("Verified OK");
            return 1;
        }
    }
}
