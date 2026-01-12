using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LiveCharts; // ChartValues
using re_server.Models;
using re_server.Services;
using re_server.Utils;

namespace re_server.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IServerService _serverService;
        private readonly IPerformanceService _performanceService;
        private readonly ISecurityPolicyService _securityPolicyService;

        public ObservableCollection<ClientInfo> Clients { get; } = new();

        private ClientInfo? _selectedClient;
        public ClientInfo? SelectedClient
        {
            get => _selectedClient;
            set
            {
                if (SetProperty(ref _selectedClient, value))
                {
                    OnPropertyChanged(nameof(SelectedClientUrl));
                    OnPropertyChanged(nameof(SelectedClientIpText));

                    if (value != null && !string.IsNullOrWhiteSpace(value.Url))
                        RequestNavigate?.Invoke(value.Url);
                }
            }
        }

        public string? SelectedClientUrl => SelectedClient?.Url;

        public string SelectedClientIpText =>
            SelectedClient == null
                ? "모니터링 대상을 선택하세요"
                : $"모니터링 대상: {SelectedClient.Ip}";

        private int _port = 5000;
        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        private bool _isServerRunning;
        public bool IsServerRunning
        {
            get => _isServerRunning;
            private set
            {
                if (SetProperty(ref _isServerRunning, value))
                    OnPropertyChanged(nameof(ServerStatusBrush));
            }
        }

        private string _statusText = "서버 중지됨";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public Brush ServerStatusBrush => IsServerRunning ? Brushes.LimeGreen : Brushes.Red;

        private string _forbiddenKeywordsText = "youtube.com, facebook.com, twitter.com";
        public string ForbiddenKeywordsText
        {
            get => _forbiddenKeywordsText;
            set => SetProperty(ref _forbiddenKeywordsText, value);
        }

        // ======== 성능 모니터링 속성 ======== //

        private double _cpuUsage;
        public double CpuUsage
        {
            get => _cpuUsage;
            set => SetProperty(ref _cpuUsage, value);
        }

        private string _ramText = "";
        public string RamText
        {
            get => _ramText;
            set => SetProperty(ref _ramText, value);
        }

        private double _ramUsagePercent;
        public double RamUsagePercent
        {
            get => _ramUsagePercent;
            set => SetProperty(ref _ramUsagePercent, value);
        }

        private double _netMbps;
        public double NetMbps
        {
            get => _netMbps;
            set => SetProperty(ref _netMbps, value);
        }

        public ChartValues<double> CpuChartValues { get; } = new();
        public ChartValues<double> NetChartValues { get; } = new();

        // ======== View와 통신하는 이벤트 ======== //

        public event Action<string>? RequestNavigate;
        public event Action<string>? AlertRequested;

        // ======== Commands ======== //

        private readonly ICommand _startServerCommand;
        public ICommand StartServerCommand => _startServerCommand;

        public MainViewModel(
            IServerService serverService,
            IPerformanceService performanceService,
            ISecurityPolicyService securityPolicyService)
        {
            _serverService = serverService;
            _performanceService = performanceService;
            _securityPolicyService = securityPolicyService;

            _startServerCommand = new RelayCommand(_ => StartServer(), _ => !IsServerRunning);

            _serverService.ClientConnected += OnClientConnected;
            _serverService.ClientMessageReceived += OnClientMessageReceived;
            _serverService.ClientDisconnected += OnClientDisconnected;

            _performanceService.PerformanceUpdated += OnPerformanceUpdated;
            _performanceService.Start();
        }

        private void StartServer()
        {
            StatusText = $"서버 가동 중 (포트: {Port})";
            IsServerRunning = true;
            _ = _serverService.StartAsync(Port);
        }

        private void OnClientConnected(string ip)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Clients.Add(new ClientInfo(ip));
            });
        }

        private void OnClientDisconnected(string ip)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var target = Clients.FirstOrDefault(c => c.Ip == ip);
                if (target != null)
                    Clients.Remove(target);

                if (SelectedClient?.Ip == ip)
                    SelectedClient = null;
            });
        }

        private void OnClientMessageReceived(string ip, string message)
        {
            Console.WriteLine($"[RECV] {ip} => {message}");
            System.Diagnostics.Debug.WriteLine($"[RECV] {ip} => {message}");

            Application.Current.Dispatcher.Invoke(() =>
            {
                var client = Clients.FirstOrDefault(c => c.Ip == ip);
                if (client == null) return;

                // 1) URL 정규화
                var url = message.Trim().ToLower();

                if (string.IsNullOrWhiteSpace(url))
                    return;

                // UI Automation 값 필터링 (한국어/영어 대응)
                if (url.Contains("검색") || url.Contains("입력") || url.Contains("search"))
                    return;

                // 스킴 강제
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    url = "https://" + url;

                client.Url = url;

                // 2) 선택된 클라이언트면 화면 변경
                if (SelectedClient?.Ip == ip)
                {
                    RequestNavigate?.Invoke(client.Url);
                    OnPropertyChanged(nameof(SelectedClientUrl));
                }

                // 3) 보안 체크
                if (_securityPolicyService.Check(url, ForbiddenKeywordsText, out var keyword))
                {
                    AlertRequested?.Invoke($"[차단 감지]\n사용자: {ip}\n주소: {url}\n키워드: {keyword}");
                    _serverService.SendMessage(ip, $"ALERT: [{keyword}] 접속 감지됨. 종료하세요!");
                }
            });
        }


        private void OnPerformanceUpdated(PerformanceData data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CpuUsage = data.CpuUsagePercent;
                RamText = $"{data.UsedMemoryGb:F1} / {data.TotalMemoryGb:F1} GB";
                RamUsagePercent = (data.UsedMemoryGb / data.TotalMemoryGb) * 100;
                NetMbps = data.NetworkMbps;

                CpuChartValues.Add(CpuUsage);
                if (CpuChartValues.Count > 30)
                    CpuChartValues.RemoveAt(0);

                NetChartValues.Add(NetMbps);
                if (NetChartValues.Count > 30)
                    NetChartValues.RemoveAt(0);
            });
        }
    }
}
