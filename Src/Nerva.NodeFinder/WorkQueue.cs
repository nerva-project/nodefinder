using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using AngryWasp.Logger;

namespace Nerva.NodeFinder
{
    public static class WorkQueue
    {
        private static Dictionary<string, Tuple<TcpClient, ThreadInfo>> queue = new Dictionary<string, Tuple<TcpClient, ThreadInfo>>();

        public static bool HasWork => queue.Count > 0;

        public static void Push(string value, TcpClient client, ThreadInfo threadInfo)
        {
            if (queue.ContainsKey(value))
                return;

            Log.Instance.Write($"{value} added to work queue");
            queue.Add(value, new Tuple<TcpClient, ThreadInfo>(client, threadInfo));
        }

        public static bool Pop(out string host, out Tuple<TcpClient, ThreadInfo> result)
        {
            host = null;
            result = null;

            if (!HasWork)
                return false;

            host = queue.ElementAt(0).Key;
            result = queue[host];
            queue.Remove(host);
            return true;
        }

        public static bool Contains(string value)
        {
            return queue.ContainsKey(value);
        }
    }
}