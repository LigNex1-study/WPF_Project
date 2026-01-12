using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Management; // WMI 사용을 위해 필요
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Web.WebView2.Core;

namespace WpfApp5
{
    public partial class MainWindow : Window
    {
        public ChartValues<double> CpuChartValues { get; set; } = new ChartValues<double>();
        public ChartValues<double> NetChartValues { get; set; } = new ChartValues<double>();

        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;
        private List<PerformanceCounter> netCounters = new List<PerformanceCounter>(); // 실제 네트워크 측정을 위한 리스트

        private Dictionary<string, string> clientData = new Dictionary<string, string>();
        private string selectedClientIP = "";
        private TcpListener _server;
        private double _totalMemoryGb = 16.0; // 기본값

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            GetTotalMemory();   // 1. 실제 RAM 용량 확인
            InitCounters();     // 2. 카운터 초기화 (네트워크 포함)
            StartMonitoring();  // 3. 실시간 모니터링 시작
            InitializeBrowser();
        }

        private async void InitializeBrowser()
        {
            try { await MyWebView.EnsureCoreWebView2Async(null); } catch { }
        }

        // 실제 하드웨어 총 메모리 용량 가져오기
        private void GetTotalMemory()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        double totalBytes = Convert.ToDouble(obj["TotalPhysicalMemory"]);
                        _totalMemoryGb = totalBytes / (1024.0 * 1024.0 * 1024.0);
                    }
                }
            }
            catch { _totalMemoryGb = 16.0; }
        }

        private void InitCounters()
        {
            try
            {
                // CPU/RAM 카운터
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "Available MBytes");

                // 실제 네트워크 인터페이스 카운터 등록
                PerformanceCounterCategory netCategory = new PerformanceCounterCategory("Network Interface");
                string[] instances = netCategory.GetInstanceNames();
                foreach (string instance in instances)
                {
                    // 초당 총 바이트(송신+수신)를 측정하는 카운터 추가
                    netCounters.Add(new PerformanceCounter("Network Interface", "Bytes Total/sec", instance));
                }

                cpuCounter.NextValue(); // 첫 값 초기화
            }
            catch (Exception ex) { Debug.WriteLine("카운터 초기화 오류: " + ex.Message); }
        }

        private void StartMonitoring()
        {
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) => {
                // 1. CPU 업데이트
                double cpuVal = cpuCounter != null ? Math.Round(cpuCounter.NextValue(), 1) : 0;
                CpuBar.Value = cpuVal;
                CpuText.Text = $"{cpuVal}%";
                CpuChartValues.Add(cpuVal);
                if (CpuChartValues.Count > 30) CpuChartValues.RemoveAt(0);

                // 2. RAM 업데이트 (실제 용량 기준 계산)
                float ramFreeMBytes = ramCounter != null ? ramCounter.NextValue() : 0;
                double ramFreeGb = ramFreeMBytes / 1024.0;
                double ramUsedGb = _totalMemoryGb - ramFreeGb;

                RamBar.Value = (ramUsedGb / _totalMemoryGb) * 100;
                RamText.Text = $"{ramUsedGb:F1} / {_totalMemoryGb:F1} GB";

                // 3. 네트워크 업데이트 (실제 데이터 전송량 계산)
                double totalBytesSec = 0;
                foreach (var counter in netCounters)
                {
                    try { totalBytesSec += counter.NextValue(); } catch { }
                }

                // Bytes/sec -> Mbps 변환 (8비트 곱하고 1024^2로 나눔)
                double netMbps = (totalBytesSec * 8) / (1024.0 * 1024.0);

                NetText.Text = $"{netMbps:F2} Mbps";
                NetChartValues.Add(netMbps);
                if (NetChartValues.Count > 30) NetChartValues.RemoveAt(0);
            };
            timer.Start();
        }

        private async void StartServer_Click(object sender, RoutedEventArgs e)
        {
            if (_server != null) return;
            try
            {
                _server = new TcpListener(IPAddress.Any, 5000);
                _server.Start();
                StatusDot.Fill = System.Windows.Media.Brushes.LimeGreen;
                StatusText.Text = "서버 가동 중 (포트: 5000)";

                while (true)
                {
                    TcpClient client = await _server.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            string ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            Dispatcher.Invoke(() => {
                if (!clientData.ContainsKey(ip))
                {
                    clientData.Add(ip, "");
                    ClientListBox.Items.Add(ip);
                }
            });

            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[1024];
                    while (true)
                    {
                        int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0) break;

                        string url = Encoding.UTF8.GetString(buffer, 0, read).ToLower().Trim();
                        clientData[ip] = url;

                        Dispatcher.Invoke(() => {
                            CheckSecurityAlert(ip, url, client);
                            if (selectedClientIP == ip) UpdateWebView(url);
                        });
                    }
                }
            }
            catch { }
            finally
            {
                Dispatcher.Invoke(() => {
                    clientData.Remove(ip);
                    ClientListBox.Items.Remove(ip);
                });
            }
        }

        private void CheckSecurityAlert(string ip, string url, TcpClient client)
        {
            if (string.IsNullOrEmpty(ForbiddenUrlInput.Text)) return;

            string[] forbiddenKeywords = ForbiddenUrlInput.Text.Split(',');
            foreach (string keyword in forbiddenKeywords)
            {
                string k = keyword.Trim().ToLower();
                if (!string.IsNullOrEmpty(k) && url.Contains(k))
                {
                    MessageBox.Show($"[차단 감지] 사용자: {ip}\n주소: {url}", "보안 경고", MessageBoxButton.OK, MessageBoxImage.Error);
                    try
                    {
                        byte[] alertData = Encoding.UTF8.GetBytes($"ALERT: [{k}] 접속이 감지되었습니다. 즉시 종료하세요!");
                        client.GetStream().Write(alertData, 0, alertData.Length);
                    }
                    catch { }
                    break;
                }
            }
        }

        private void UpdateWebView(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (!url.StartsWith("http")) url = "https://" + url;
            try { if (MyWebView.CoreWebView2 != null) MyWebView.CoreWebView2.Navigate(url); } catch { }
        }

        private void ClientListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ClientListBox.SelectedItem != null)
            {
                selectedClientIP = ClientListBox.SelectedItem.ToString();
                DetailHeader.Text = $"📍 실시간 모니터링: {selectedClientIP}";
                UpdateWebView(clientData[selectedClientIP]);
            }
        }
    }
}