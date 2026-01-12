using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace re_server.Models
{
    public class PerformanceData
    {
        public double CpuUsagePercent { get; set; }     // 0~100
        public double TotalMemoryGb { get; set; }       // 총 메모리
        public double UsedMemoryGb { get; set; }        // 사용중 메모리
        public double NetworkMbps { get; set; }         // 전체 네트워크 Mbps
    }
}
