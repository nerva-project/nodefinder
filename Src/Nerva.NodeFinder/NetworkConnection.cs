using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using AngryWasp.Helpers;
using AngryWasp.Logger;
using Nerva.Levin;
using Nerva.Levin.Requests;
using Nerva.Rpc.Daemon;

namespace Nerva.NodeFinder
{
    public class NetworkConnection
    {
        public static bool Ping(string host, out TcpClient client, int port = 17565, int timeout = 1500)
        {
            try
            {
                client = new TcpClient();
                if (!client.ConnectAsync(host, port).Wait(timeout))
                {
                    client = null;
                    return false;
                }

                return true;
            }
            catch
            {
                client = null;
                return false;
            }
        }

        public static void VerifyPing(string host, Tuple<TcpClient, ThreadInfo> input)
        {
            Log.Instance.Write($"{host}: Verifying");

            try
            {
                NetworkStream ns = input.Item1.GetStream();

                object peerlist = null;

                byte[] handshake = new Handshake().Create();
                Log.Instance.Write($"{host}: Sending handshake packet");

                ns.Write(handshake, 0, handshake.Length);
                ns.ReadTimeout = 5000;

                Header header; Section section;
                while (Read(host, ns, out header, out section))
                {
                    if (header.Command == Constants.P2P_COMMAND_HANDSHAKE)
                        peerlist = section.Entries["local_peerlist_new"];

                    if (!ns.DataAvailable)
                    {
                        Log.Instance.Write($"{host}: Network stream ended");
                        break;
                    }
                }

                ns.Close();
                input.Item1.Close();

                if (peerlist != null)
                {
                    input.Item2.Found++;
                    ProcessRemotePeerList(input.Item2, peerlist);
                    Log.Instance.SetColor(ConsoleColor.Magenta);
                    Log.Instance.Write($"{host}: Adding peer");
                    Log.Instance.SetColor(ConsoleColor.White);

    #pragma warning disable 4014
                    new AddPeer(new AddPeerRequestData
                    {
                        Host = host
                    }, null, null).RunAsync();
    #pragma warning restore 4014
                }
            }
            catch
            {
                Log.Instance.Write($"{host}: Check failed");
            }
        }

        private static bool Read(string host, NetworkStream ns, out Header header, out Section section)
        {
            header = null;
            section = null;

            byte[] headerBuffer = new byte[33];

            int offset = 0;
            int i = ns.Read(headerBuffer, 0, headerBuffer.Length);

            if (i < headerBuffer.Length)
            {
                Log.Instance.Write($"{host}: Buffer is insufficient length");
                return false;
            }

            header = Header.FromBytes(headerBuffer, ref offset);

            if (header == null)
            {
                Log.Instance.Write($"{host}: Invalid response from remote node");
                return false;
            }

            offset = 0;
            byte[] buffer = new byte[header.Cb];
            i = ns.Read(buffer, 0, buffer.Length);

            if (i < buffer.Length)
            {
                Log.Instance.Write($"{host}: Invalid response from remote node");
                return false;
            }

            section = null;

            switch (header.Command)
            {
                case Constants.P2P_COMMAND_HANDSHAKE:
                    section = new Handshake().Read(header, buffer, ref offset);
                    break;
                case Constants.P2P_COMMAND_REQUEST_SUPPORT_FLAGS:
                    section = new SupportFlags().Read(header, buffer, ref offset);
                    break;
                default:
                    Log.Instance.Write(Log_Severity.Error, $"Command {header.Command} is not yet supported");
                    return false;
            }

            Log.Instance.Write($"{host}: Read data package {header.Command}");

            return true;
        }

        private static void ProcessRemotePeerList(ThreadInfo ti, object pl)
        {
            long tsNow = (long)DateTimeHelper.TimestampNow();
            foreach (var obj in (Array)pl)
            {
                Section sec = (Section)obj;
                Section adr = (Section)sec.Entries["adr"];
                uint ipInt = (uint)((Section)adr.Entries["addr"]).Entries["m_ip"];
                string ip = Helpers.ToIP(ipInt);
                ushort rpcPort = (ushort)sec.Entries["rpc_port"];
                long lastSeen = (long)sec.Entries["last_seen"];

                if (tsNow - lastSeen > (60 * 60 * 8))
                    continue; //skip stale peers

                if (rpcPort != 0)
                    Log.Instance.Write($"Found open RPC port @ {ip}:{rpcPort}");
                
                TcpClient client;
                
                if (NetworkConnection.Ping(ip, out client))
                {
                    Log.Instance.Write($"{ip}: Ping success");
                    WorkQueue.Push(ip, client, ti);
                }
                else
                    Log.Instance.Write($"{ip}: Ping failed");
            }
        }
    }
}