using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using WpfApp4.Helpers;
using WpfApp4.Services;

namespace WpfApp4.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private const int SERVER_PORT = 5000;

        private readonly INetworkService _network;
        private readonly IBrowserMonitorService _browser;
        private readonly IDialogService _dialog;
        private readonly Dispatcher _uiDispatcher;

        private CancellationTokenSource? _cts;
        private DispatcherTimer? _timer;
        private string _lastUrl = "";

        private string _serverIp = "192.168.2.156";
        private string _status = "상태: 연결 대기 중";
        private bool _isConnected;
        private bool _isMonitoring;
        private bool _isBusy;

        public ObservableCollection<string> Logs { get; } = new();

        public ICommand ToggleConnectCommand { get; }
        public ICommand ToggleMonitorCommand { get; }
        public ICommand ClearLogCommand { get; }

        public MainViewModel(
            INetworkService network,
            IBrowserMonitorService browser,
            IDialogService dialog,
            Dispatcher uiDispatcher)
        {
            _network = network;
            _browser = browser;
            _dialog = dialog;
            _uiDispatcher = uiDispatcher;

            _network.MessageReceived += OnServerMessage;
            _network.Disconnected += OnDisconnected;

            ToggleConnectCommand = new AsyncRelayCommand(async () =>
            {
                if (IsConnected) await DisconnectAsync("사용자 요청");
                else await ConnectAsync();
            }, () => CanToggleConnect);

            ToggleMonitorCommand = new RelayCommand(() => ToggleMonitoring(), () => CanToggleMonitor);
            ClearLogCommand = new RelayCommand(() => ClearLogs());
        }

        public string ServerIp
        {
            get => _serverIp;
            set { _serverIp = value; OnPropertyChanged(nameof(ServerIp)); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set
            {
                if (_isConnected == value) return;
                _isConnected = value;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectButtonText));
                OnPropertyChanged(nameof(CanToggleMonitor));
                OnPropertyChanged(nameof(CanToggleConnect));
                RaiseCommandStates();
            }
        }

        public bool IsMonitoring
        {
            get => _isMonitoring;
            private set
            {
                if (_isMonitoring == value) return;
                _isMonitoring = value;
                OnPropertyChanged(nameof(IsMonitoring));
                OnPropertyChanged(nameof(MonitorButtonText));
            }
        }

        public bool CanToggleConnect => !_isBusy;
        public string ConnectButtonText => IsConnected ? "연결 해제" : "연결";

        public bool CanToggleMonitor => IsConnected;
        public string MonitorButtonText => IsMonitoring ? "감시 OFF" : "감시 ON";

        private void RaiseCommandStates()
        {
            (ToggleConnectCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (ToggleMonitorCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task ConnectAsync()
        {
            if (_isBusy) return;

            _isBusy = true;
            OnPropertyChanged(nameof(CanToggleConnect));
            RaiseCommandStates();

            AddLog($"서버 연결 시도: {ServerIp}:{SERVER_PORT}");
            Status = "상태: 서버 연결 시도 중...";

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                await _network.ConnectAsync(ServerIp, SERVER_PORT, _cts.Token);

                IsConnected = true;
                Status = "상태: 연결 성공";
                AddLog("서버 연결 성공");

                // 연결되면 감시 ON
                StartMonitoring();
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Status = "상태: 연결 실패";
                AddLog($"연결 실패: {ex.Message}");

                _dialog.ShowError("서버 연결에 실패했습니다: " + ex.Message, "연결 실패");
                await _network.DisconnectAsync("연결 실패 정리");
            }
            finally
            {
                _isBusy = false;
                OnPropertyChanged(nameof(CanToggleConnect));
                RaiseCommandStates();
            }
        }

        private async Task DisconnectAsync(string reason)
        {
            if (_isBusy) return;

            _isBusy = true;
            OnPropertyChanged(nameof(CanToggleConnect));
            RaiseCommandStates();

            AddLog($"연결 해제: {reason}");
            Status = "상태: 연결 해제 중...";

            try
            {
                StopMonitoring();
                _cts?.Cancel();
                await _network.DisconnectAsync(reason);
            }
            finally
            {
                IsConnected = false;
                Status = "상태: 연결 대기 중";

                _isBusy = false;
                OnPropertyChanged(nameof(CanToggleConnect));
                RaiseCommandStates();
            }
        }

        public void ToggleMonitoring()
        {
            if (!IsConnected) return;
            if (IsMonitoring) StopMonitoring();
            else StartMonitoring();
        }

        private void StartMonitoring()
        {
            if (_timer != null) return;

            _lastUrl = "";
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };

            _timer.Tick += async (s, e) =>
            {
                try
                {
                    if (!IsConnected || _cts == null) return;

                    var url = _browser.GetCurrentUrl() ?? "";
                    if (!string.IsNullOrWhiteSpace(url) && url != _lastUrl)
                    {
                        _lastUrl = url;
                        await _network.SendAsync(url, _cts.Token);
                        AddLog($"URL 전송: {Shorten(url, 120)}");
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"URL 감시 오류: {ex.Message}");
                }
            };

            _timer.Start();
            IsMonitoring = true;
            AddLog("감시 시작(URL 모니터링 ON)");
            Status = "상태: 감시 중";
        }

        private void StopMonitoring()
        {
            if (_timer == null) return;

            _timer.Stop();
            _timer = null;

            IsMonitoring = false;
            AddLog("감시 중지(URL 모니터링 OFF)");

            if (IsConnected)
                Status = "상태: 연결됨(감시 OFF)";
        }

        private void OnServerMessage(string message)
        {
            // 네트워크 스레드 → UI 스레드로
            _uiDispatcher.Invoke(() =>
            {
                AddLog($"서버 수신: {Shorten(message.Replace("\r", "").Replace("\n", " "), 140)}");

                if (message.Contains("ALERT"))
                {
                    _dialog.ShowWarning(message, "⚠ 관리자 주의");
                    AddLog("ALERT 처리: MessageBox 표시");
                }
            });
        }

        private void OnDisconnected(string reason)
        {
            _uiDispatcher.Invoke(() =>
            {
                AddLog("서버 연결 종료/끊김 감지: " + reason);
                StopMonitoring();
                _cts?.Cancel();

                IsConnected = false;
                Status = "상태: 연결 대기 중";
            });
        }

        public void ClearLogs()
        {
            _uiDispatcher.Invoke(() => Logs.Clear());
            AddLog("로그 초기화");
        }

        private void AddLog(string msg)
        {
            _uiDispatcher.Invoke(() =>
            {
                Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
                if (Logs.Count > 250) Logs.RemoveAt(Logs.Count - 1);
            });
        }

        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
