using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using AngryWasp.Logger;

namespace Nerva.NodeFinder
{
    public static class WorkQueue
    {
        private static Dictionary<string, TcpClient> queue = new Dictionary<string, TcpClient>();

        public static bool HasWork => queue.Count > 0;

        public static void Push(string value, TcpClient client)
        {
            Log.Instance.Write($"{value} added to work queue");
            queue.Add(value, client);
        }

        public static bool Pop(out string host, out TcpClient client)
        {
            host = null;
            client = null;

            if (!HasWork)
                return false;

            host = queue.ElementAt(0).Key;
            client = queue[host];
            queue.Remove(host);
            return true;
        }

        public static bool Contains(string value)
        {
            return queue.ContainsKey(value);
        }
    }
}