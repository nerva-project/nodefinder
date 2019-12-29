using System;
using System.Net.Sockets;
using AngryWasp.Logger;
using AngryWasp.Helpers;

namespace Nerva.NodeFinder
{
    public static class MainClass
    {
        [STAThread]
        public static void Main(string[] args)
        {
            CommandLineParser clp = CommandLineParser.Parse(args);
            Log.CreateInstance(true);

            if (clp["--host"] == null)
                Log.Instance.Write(Log_Severity.Fatal, "Need a host");

            string host = clp["--host"].Value;
            TcpClient client;

            if (!NetworkConnection.Ping(host, out client))
            {
                Log.Instance.Write(Log_Severity.Error, "Validation failed. No ping");
                return;
            }

            if (!NetworkConnection.VerifyPing(host, client))
            {
                Log.Instance.Write(Log_Severity.Error, "Validation failed. Node not detected");
                return;
            }

            Log.Instance.Write("Verified OK");
        }
    }
}
