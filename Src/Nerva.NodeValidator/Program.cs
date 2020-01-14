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
            int port = 17565;
            if (clp["--testnet"] != null)
                port = 18565;
            NetworkConnection nc = new NetworkConnection();
            object pl = nc.Run(host, port);

            return (pl != null) ? 1 : 0;
        }
    }
}
