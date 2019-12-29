using System;
using System.Net;
using AngryWasp.Helpers;

namespace Nerva.NodeFinder
{
    public static class Helpers
    {
        public static bool CidrToIpRange(string input, out string start, out string end)
        {
            start = end = null;

            string[] s1 = input.Split('/');
            if (s1.Length == 1)
            {
                start = end = s1[0];
                return true;
            }

            uint mask = ~(0xFFFFFFFF >> int.Parse(s1[1]));

            uint ip = ToUint(s1[0]);
            start = ToIP(ip & mask);
            end = ToIP((ip & mask) | ~mask);
            return true;
        }

        public static string ToIP(uint ip)
        {
            return String.Format("{0}.{1}.{2}.{3}",
                ip >> 24, (ip >> 16) & 0xff, (ip >> 8) & 0xff, ip & 0xff);
        }

        public static uint ToUint(string ip)
        {
            var address = IPAddress.Parse(ip);
            byte[] b = address.GetAddressBytes();
            Array.Reverse(b);
            return BitShifter.ToUInt(b);
        }
    }
}