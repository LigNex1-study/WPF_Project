namespace WpfApp5.Models
{
    /// <summary>
    /// 시스템 성능 지표를 담는 데이터 클래스입니다.
    /// CPU, 메모리, 네트워크 등의 정보를 저장합니다.
    /// </summary>
    public class SystemMetrics
    {
        /// <summary>
        /// CPU 사용률 (0.0 ~ 100.0)
        /// 예: 45.5는 CPU가 45.5% 사용 중임을 의미합니다.
        /// </summary>
        public double CpuUsage { get; set; }

        /// <summary>
        /// RAM 사용률 (0.0 ~ 100.0)
        /// 전체 메모리 대비 사용 중인 메모리의 비율입니다.
        /// </summary>
        public double RamUsagePercent { get; set; }

        /// <summary>
        /// 현재 사용 중인 RAM 용량 (GB 단위)
        /// 예: 8.5는 8.5GB를 사용 중임을 의미합니다.
        /// </summary>
        public double RamUsed { get; set; }

        /// <summary>
        /// 전체 RAM 용량 (GB 단위)
        /// 예: 16.0은 시스템에 16GB RAM이 설치되어 있음을 의미합니다.
        /// </summary>
        public double RamTotal { get; set; }

        /// <summary>
        /// 네트워크 속도 (Mbps 단위)
        /// 초당 전송되는 데이터량을 나타냅니다.
        /// 예: 10.5는 초당 10.5Mbps를 전송 중임을 의미합니다.
        /// </summary>
        public double NetworkSpeed { get; set; }
    }
}
