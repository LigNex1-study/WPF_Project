using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Windows.Threading;
using WpfApp5.Models;

namespace WpfApp5.Services
{
    /// <summary>
    /// 시스템 성능을 모니터링하는 클래스입니다.
    /// CPU, 메모리, 네트워크 사용량을 실시간으로 측정합니다.
    /// </summary>
    public class PerformanceMonitor
    {
        /// <summary>
        /// CPU 사용률을 측정하는 성능 카운터
        /// Windows의 Performance Counter를 사용하여 CPU 사용률을 가져옵니다.
        /// </summary>
        private PerformanceCounter _cpuCounter;

        /// <summary>
        /// 사용 가능한 메모리를 측정하는 성능 카운터
        /// 현재 사용 가능한 RAM 용량(MB 단위)을 가져옵니다.
        /// </summary>
        private PerformanceCounter _ramCounter;

        /// <summary>
        /// 네트워크 인터페이스별 성능 카운터 목록
        /// 컴퓨터에 여러 네트워크 인터페이스(이더넷, Wi-Fi 등)가 있을 수 있으므로 리스트로 관리합니다.
        /// </summary>
        private List<PerformanceCounter> _netCounters;

        /// <summary>
        /// 시스템의 전체 메모리 용량 (GB 단위)
        /// 기본값은 16GB이지만, 실제 하드웨어 메모리 용량을 자동으로 감지합니다.
        /// </summary>
        private double _totalMemoryGb = 16.0;

        /// <summary>
        /// 성능 지표를 주기적으로 업데이트하는 타이머
        /// 1초마다 CPU, RAM, 네트워크 정보를 갱신합니다.
        /// </summary>
        private DispatcherTimer _timer;

        /// <summary>
        /// 성능 지표가 업데이트되었을 때 발생하는 이벤트
        /// 매개변수: 업데이트된 성능 지표 (SystemMetrics 객체)
        /// </summary>
        public event Action<SystemMetrics> MetricsUpdated;

        /// <summary>
        /// 생성자: PerformanceMonitor 객체를 초기화합니다.
        /// 실제 메모리 용량을 확인하고 성능 카운터를 설정합니다.
        /// </summary>
        public PerformanceMonitor()
        {
            // 실제 하드웨어 메모리 용량 확인
            GetTotalMemory();

            // 성능 카운터 초기화 (CPU, RAM, 네트워크)
            InitCounters();
        }

        /// <summary>
        /// 성능 모니터링을 시작하는 메서드입니다.
        /// 1초마다 성능 지표를 업데이트하고 이벤트를 발생시킵니다.
        /// </summary>
        public void StartMonitoring()
        {
            // 이미 타이머가 실행 중이면 중복 실행을 방지합니다
            if (_timer != null) return;

            // 1초 간격으로 실행되는 타이머 생성
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

            // 타이머가 실행될 때마다 UpdateMetrics 메서드를 호출합니다
            _timer.Tick += (s, e) => UpdateMetrics();

            // 타이머 시작
            _timer.Start();
        }

        /// <summary>
        /// 성능 모니터링을 중지하는 메서드입니다.
        /// </summary>
        public void StopMonitoring()
        {
            // 타이머 중지 및 해제
            _timer?.Stop();
            _timer = null;
        }

        /// <summary>
        /// 실제 하드웨어 메모리 용량을 가져오는 메서드입니다.
        /// WMI(Windows Management Instrumentation)를 사용하여 시스템 정보를 조회합니다.
        /// </summary>
        private void GetTotalMemory()
        {
            try
            {
                // WMI 쿼리를 사용하여 시스템의 총 물리적 메모리를 조회합니다
                // Win32_ComputerSystem 클래스에서 TotalPhysicalMemory 속성을 가져옵니다
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    // 조회 결과를 순회합니다
                    foreach (var obj in searcher.Get())
                    {
                        // 바이트 단위로 메모리 용량을 가져옵니다
                        double totalBytes = Convert.ToDouble(obj["TotalPhysicalMemory"]);

                        // 바이트를 GB로 변환합니다
                        // 1024 * 1024 * 1024 = 1GB (바이트)
                        _totalMemoryGb = totalBytes / (1024.0 * 1024.0 * 1024.0);
                    }
                }
            }
            catch
            {
                // 오류 발생 시 기본값(16GB)을 사용합니다
                _totalMemoryGb = 16.0;
            }
        }

        /// <summary>
        /// 성능 카운터를 초기화하는 메서드입니다.
        /// CPU, RAM, 네트워크 인터페이스의 성능 카운터를 설정합니다.
        /// </summary>
        private void InitCounters()
        {
            try
            {
                // ========== CPU 카운터 초기화 ==========
                // "Processor" 카테고리에서 "% Processor Time" 카운터를 가져옵니다
                // "_Total" 인스턴스는 모든 CPU 코어의 평균 사용률을 의미합니다
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

                // ========== RAM 카운터 초기화 ==========
                // "Memory" 카테고리에서 "Available MBytes" 카운터를 가져옵니다
                // 사용 가능한 메모리 용량을 MB 단위로 가져옵니다
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                // ========== 네트워크 카운터 초기화 ==========
                // 네트워크 인터페이스 카운터 목록 초기화
                _netCounters = new List<PerformanceCounter>();

                // "Network Interface" 카테고리의 모든 인스턴스 이름을 가져옵니다
                // 인스턴스는 각 네트워크 인터페이스(이더넷, Wi-Fi 등)를 의미합니다
                PerformanceCounterCategory netCategory = new PerformanceCounterCategory("Network Interface");
                string[] instances = netCategory.GetInstanceNames();

                // 각 네트워크 인터페이스에 대해 카운터를 생성합니다
                foreach (string instance in instances)
                {
                    // "Bytes Total/sec": 초당 전송되는 총 바이트 수 (송신 + 수신)
                    _netCounters.Add(new PerformanceCounter("Network Interface", "Bytes Total/sec", instance));
                }

                // CPU 카운터의 첫 번째 값을 초기화합니다
                // PerformanceCounter는 첫 번째 호출 시 부정확한 값을 반환할 수 있으므로 미리 호출합니다
                _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                // 카운터 초기화 실패 시 오류 메시지를 출력합니다
                Debug.WriteLine("카운터 초기화 오류: " + ex.Message);
            }
        }

        /// <summary>
        /// 성능 지표를 업데이트하는 메서드입니다.
        /// CPU, RAM, 네트워크 정보를 수집하여 이벤트를 발생시킵니다.
        /// </summary>
        private void UpdateMetrics()
        {
            // 성능 지표를 담을 객체 생성
            var metrics = new SystemMetrics();

            // ========== CPU 사용률 업데이트 ==========
            // NextValue()를 호출하여 현재 CPU 사용률을 가져옵니다
            // Math.Round(값, 1): 소수점 첫째 자리까지 반올림
            metrics.CpuUsage = _cpuCounter != null ? Math.Round(_cpuCounter.NextValue(), 1) : 0;

            // ========== RAM 사용량 업데이트 ==========
            // 사용 가능한 메모리를 MB 단위로 가져옵니다
            float ramFreeMBytes = _ramCounter != null ? _ramCounter.NextValue() : 0;

            // MB를 GB로 변환합니다 (1024MB = 1GB)
            double ramFreeGb = ramFreeMBytes / 1024.0;

            // 사용 중인 메모리 = 전체 메모리 - 사용 가능한 메모리
            metrics.RamUsed = _totalMemoryGb - ramFreeGb;
            metrics.RamTotal = _totalMemoryGb;

            // RAM 사용률 계산 (사용 중인 메모리 / 전체 메모리 * 100)
            metrics.RamUsagePercent = (metrics.RamUsed / _totalMemoryGb) * 100;

            // ========== 네트워크 속도 업데이트 ==========
            // 모든 네트워크 인터페이스의 데이터 전송량을 합산합니다
            double totalBytesSec = 0;
            foreach (var counter in _netCounters)
            {
                try
                {
                    // 각 인터페이스의 초당 바이트 수를 더합니다
                    totalBytesSec += counter.NextValue();
                }
                catch { }
            }

            // ========== 단위 변환 ==========
            // Bytes/sec를 Mbps로 변환합니다
            // 1 byte = 8 bits
            // 1 Mbps = 1,024,000 bits/sec = 1,024,000 / 8 bytes/sec = 128,000 bytes/sec
            // 하지만 일반적으로 1 Mbps = 1,000,000 bits/sec로 계산하므로
            // (bytes/sec * 8) / (1024 * 1024) = Mbps
            metrics.NetworkSpeed = (totalBytesSec * 8) / (1024.0 * 1024.0);

            // 성능 지표 업데이트 이벤트 발생 (UI에 알림)
            MetricsUpdated?.Invoke(metrics);
        }

        /// <summary>
        /// 리소스를 정리하는 메서드입니다.
        /// 성능 카운터와 타이머를 해제합니다.
        /// </summary>
        public void Dispose()
        {
            // 모니터링 중지
            StopMonitoring();

            // 성능 카운터 해제
            _cpuCounter?.Dispose();
            _ramCounter?.Dispose();

            // 네트워크 카운터 해제
            foreach (var counter in _netCounters)
            {
                counter?.Dispose();
            }

            // 네트워크 카운터 목록 비우기
            _netCounters?.Clear();
        }
    }
}
