using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using AngryWasp.Logger;
using Nerva.Levin;
using Nerva.Levin.Requests;

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

        public static bool VerifyPing(string host, TcpClient client)
        {
            Log.Instance.Write($"{host}: Validating");

            try
            {
                NetworkStream ns = client.GetStream();

                object peerlist = null;

                byte[] handshake = new Handshake().Create();
                Log.Instance.Write($"{host}: Sending handshake packet");

                ns.Write(handshake, 0, handshake.Length);
                ns.ReadTimeout = 30000;

                Header header; Section section;
                while (Read(host, ns, out header, out section))
                {
                    if (header.Command == Constants.P2P_COMMAND_HANDSHAKE)
                    {
                        peerlist = section.Entries["local_peerlist_new"];
                        Log.Instance.Write("Retrieved peer list. Disconnecting");
                        break;
                    }
                }

                ns.Close();
                client.Close();

                if (peerlist != null)
                    return true;
            }
            catch (Exception ex)
            {
                Log.Instance.WriteNonFatalException(ex);
                if (client != null)
                    client.Close();
            }

            return false;
        }

        private static bool Read(string host, NetworkStream ns, out Header header, out Section section)
        {
            Thread.Sleep(25);

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
                Log.Instance.Write(Log_Severity.Error, $"{host}: Invalid response from remote node");
                return false;
            }

            offset = 0;
            byte[] buffer = new byte[header.Cb];

            int x = 0;
            int y = 0;
            do
            {
                try
                {
                    y = ns.Read(buffer, x, buffer.Length);
                    x += y;
                    Thread.Sleep(25);
                }
                catch (Exception ex)
                {
                    Log.Instance.WriteNonFatalException(ex);
                    return false;
                }

            } while (x < buffer.Length);

            //read loop broke before we read as many bytes as the header says we need
            if (x < buffer.Length)
            {
                Log.Instance.Write(Log_Severity.Error, $"{host}: Invalid response from remote node");
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
                    if (header.Command >= 2001 && header.Command <= 2009)
                        Log.Instance.Write($"Unsupported protocol notification {header.Command}");
                    else
                        Log.Instance.Write(Log_Severity.Warning, $"Command {header.Command} is not yet supported");
                    return false;
            }

            Log.Instance.Write($"{host}: Read data package {header.Command}");

            return true;
        }
    }
}