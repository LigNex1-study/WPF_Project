using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using re_server.Models;
using System.Management;

namespace re_server.Services
{
    public class PerformanceService : IPerformanceService
    {
        public event Action<PerformanceData>? PerformanceUpdated;

        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _ramCounter;
        private readonly List<PerformanceCounter> _netCounters = new();

        private Timer? _timer;
        private double _totalMemoryGb = 16.0; // 기본값

        private bool _running = false;

        public PerformanceService()
        {
            LoadTotalRam();
            InitCounters();
        }

        private void LoadTotalRam()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    double bytes = Convert.ToDouble(obj["TotalPhysicalMemory"]);
                    _totalMemoryGb = bytes / (1024 * 1024 * 1024);
                }
            }
            catch
            {
                _totalMemoryGb = 16.0; // fallback
            }
        }

        private void InitCounters()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                var category = new PerformanceCounterCategory("Network Interface");
                string[] instances = category.GetInstanceNames();

                foreach (var nic in instances)
                {
                    _netCounters.Add(new PerformanceCounter("Network Interface", "Bytes Total/sec", nic));
                }

                _cpuCounter.NextValue(); // 첫 값 무시용
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PerformanceCounter 초기화 실패: {ex.Message}");
            }
        }

        public void Start()
        {
            if (_running) return;
            _running = true;

            _timer = new Timer(UpdateMetrics, null, 0, 1000);
        }

        private void UpdateMetrics(object? state)
        {
            if (!_running) return;

            try
            {
                double cpu = _cpuCounter != null ? Math.Round(_cpuCounter.NextValue(), 1) : 0;

                float freeRamMb = _ramCounter != null ? _ramCounter.NextValue() : 0;
                double freeRamGb = freeRamMb / 1024.0;
                double usedRamGb = _totalMemoryGb - freeRamGb;

                double bytesTotal = 0;
                foreach (var nic in _netCounters)
                {
                    try
                    {
                        bytesTotal += nic.NextValue();
                    }
                    catch { }
                }

                double netMbps = (bytesTotal * 8) / (1024 * 1024); // Mbps

                PerformanceUpdated?.Invoke(new PerformanceData
                {
                    CpuUsagePercent = cpu,
                    TotalMemoryGb = _totalMemoryGb,
                    UsedMemoryGb = usedRamGb,
                    NetworkMbps = netMbps
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"성능 수집 오류: {ex.Message}");
            }
        }

        public void Stop()
        {
            _running = false;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Dispose()
        {
            Stop();
            _timer?.Dispose();
            _cpuCounter?.Dispose();
            _ramCounter?.Dispose();
            foreach (var net in _netCounters) net.Dispose();
        }
    }
}
