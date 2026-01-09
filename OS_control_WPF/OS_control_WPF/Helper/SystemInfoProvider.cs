using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using ArpLookup;
using ManagedNativeWifi;

namespace SystemMonitor.Helpers
{
    public class SystemInfoProvider
    {
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;
        private float totalRamMb;

        public SystemInfoProvider()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                totalRamMb = GetTotalMemoryInMb();
            }
            catch { }
        }

        public float GetCpuUsage() => (float)Math.Round(cpuCounter?.NextValue() ?? 0, 1);

        public float GetRamUsage()
        {
            try
            {
                float availableRam = ramCounter?.NextValue() ?? 0;
                float usedPercent = ((totalRamMb - availableRam) / totalRamMb) * 100;
                return (float)Math.Round(usedPercent, 1);
            }
            catch { return 0; }
        }

        private float GetTotalMemoryInMb()
        {
            try
            {
                ObjectQuery query = new ObjectQuery("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                    return (ulong)obj["TotalVisibleMemorySize"] / 1024f;
            }
            catch { }
            return 16384f;
        }

        public int GetWifiSignalStrength()
        {
            try
            {
                var connectedNetwork = NativeWifi.EnumerateConnectedNetworkSsids().FirstOrDefault();
                if (connectedNetwork == null) return 0;

                var networks = NativeWifi.EnumerateAvailableNetworks();
                foreach (var network in networks)
                {
                    if (network.Ssid.ToString() == connectedNetwork.ToString())
                        return network.SignalQuality;
                }
            }
            catch { }
            return 0;
        }

        public async Task<List<string>> ScanNetworkDevices()
        {
            List<string> foundDevices = new List<string>();
            string myIpPrefix = "";

            // 내 IP 주소의 앞자리(C-Class) 추출
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var localIp = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (localIp == null) return new List<string> { "네트워크 연결 없음" };

            string strIp = localIp.ToString();
            myIpPrefix = strIp.Substring(0, strIp.LastIndexOf('.') + 1);

            // 1~254번 IP에 동시 핑 전송
            var pingTasks = new List<Task<PingReply>>();
            for (int i = 1; i <= 254; i++)
            {
                Ping p = new Ping();
                pingTasks.Add(p.SendPingAsync(myIpPrefix + i, 150));
            }

            var replies = await Task.WhenAll(pingTasks);
            foreach (var reply in replies)
            {
                if (reply != null && reply.Status == IPStatus.Success)
                {
                    try
                    {
                        var mac = await Arp.LookupAsync(reply.Address);
                        foundDevices.Add($"IP: {reply.Address} | MAC: {mac?.ToString() ?? "Unknown"}");
                    }
                    catch { foundDevices.Add($"IP: {reply.Address} | (정보 제한)"); }
                }
            }
            return foundDevices.OrderBy(d => d).ToList();
        }
    }
}