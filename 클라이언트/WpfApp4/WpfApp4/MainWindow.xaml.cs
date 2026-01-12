using System;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WpfApp4
{
    public partial class MainWindow : Window
    {
        private ClientViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new ClientViewModel();
            DataContext = _viewModel;

            // VM 상태 변화 감시 -> LED 애니메이션 On/Off
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            UpdateLed(_viewModel.IsConnected); // 초기 상태 반영
        }

        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ConnectToServer();
            // 연결 성공/실패 여부는 VM의 IsConnected로 자동 반영됨
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ClientViewModel.IsConnected))
            {
                UpdateLed(_viewModel.IsConnected);
            }
        }

        private void UpdateLed(bool isConnected)
        {
            var storyboard = (Storyboard)FindResource("BlinkStoryboard");

            if (isConnected)
            {
                // 🔴 빨간색 + 깜빡임 시작
                StatusLed.Fill = Brushes.Red;
                storyboard.Begin(this, true);  // controllable = true
            }
            else
            {
                // ⚫ 회색 + 깜빡임 정지
                storyboard.Stop(this);
                StatusLed.Opacity = 1;
                StatusLed.Fill = Brushes.Gray;
            }
        }
    }

    public class ClientViewModel : INotifyPropertyChanged
    {
        private TcpClient? _client;
        private string _serverIp = "127.0.0.1";
        private string _status = "상태: 연결 대기 중";
        private bool _canConnect = true;
        private bool _isConnected = false;

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

        public bool CanConnect
        {
            get => _canConnect;
            set { _canConnect = value; OnPropertyChanged(nameof(CanConnect)); }
        }

        // ✅ MainWindow가 이 값으로 LED 애니메이션 제어
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(nameof(IsConnected)); }
        }

        public async Task ConnectToServer()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(ServerIp, 5000);

                Status = "상태: 연결 성공 (감시 중)";
                CanConnect = false;
                IsConnected = true;

                _ = ListenForServerAlerts();
                StartUrlMonitoring();
            }
            catch (Exception ex)
            {
                MessageBox.Show("서버 연결에 실패했습니다: " + ex.Message);
                Status = "상태: 연결 실패";
                CanConnect = true;
                IsConnected = false;
            }
        }

        private async Task ListenForServerAlerts()
        {
            if (_client == null) return;

            byte[] buffer = new byte[1024];
            NetworkStream stream = _client.GetStream();

            try
            {
                while (_client.Connected)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, read);

                    if (message.Contains("ALERT"))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(message, "⚠️ 관리자 주의", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                    }
                }
            }
            catch
            {
                // 연결 끊김/예외 발생 시 아래에서 상태 갱신
            }

            Status = "상태: 서버와 연결이 끊겼습니다.";
            CanConnect = true;
            IsConnected = false;

            try { _client?.Close(); } catch { }
        }

        private void StartUrlMonitoring()
        {
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            string lastUrl = "";

            timer.Tick += async (s, e) =>
            {
                if (_client == null || !_client.Connected) return;

                string currentUrl = GetBrowserUrl();
                if (!string.IsNullOrEmpty(currentUrl) && currentUrl != lastUrl)
                {
                    lastUrl = currentUrl;
                    byte[] data = Encoding.UTF8.GetBytes(currentUrl);
                    await _client.GetStream().WriteAsync(data, 0, data.Length);
                }
            };

            timer.Start();
        }

        private string? GetBrowserUrl()
        {
            try
            {
                var root = AutomationElement.RootElement;
                var condition = new OrCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "주소창 및 검색창"),
                    new PropertyCondition(AutomationElement.NameProperty, "Address and search bar"));

                var element = root.FindFirst(TreeScope.Descendants, condition);

                if (element == null) return null;
                if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out object patternObj)) return null;

                return ((ValuePattern)patternObj).Current.Value;
            }
            catch
            {
                return null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
