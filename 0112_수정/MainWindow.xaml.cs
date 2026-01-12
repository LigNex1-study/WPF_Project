using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation; // 참조 추가 필수
using System.Windows.Threading;

namespace WpfApp4
{
    public partial class MainWindow : Window
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private DispatcherTimer _urlCheckTimer;
        private string _lastSentUrl = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _client = new TcpClient();
                // 사용자가 입력한 IP와 서버 포트 5000번으로 연결
                await _client.ConnectAsync(IpAddressInput.Text, 5000);
                _stream = _client.GetStream();

                StatusLabel.Text = "상태: 서버 연결 성공 (실시간 감시 중)";
                StatusLabel.Foreground = System.Windows.Media.Brushes.Green;
                ConnectBtn.IsEnabled = false;
                IpAddressInput.IsEnabled = false;

                // 서버의 경고 메시지를 수신하는 루프 시작
                _ = ListenForAlerts();

                // 2초마다 브라우저 URL을 확인하는 타이머 가동
                _urlCheckTimer = new DispatcherTimer();
                _urlCheckTimer.Interval = TimeSpan.FromSeconds(2);
                _urlCheckTimer.Tick += (s, ev) => MonitorBrowser();
                _urlCheckTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"서버 연결 실패: {ex.Message}", "연결 오류");
            }
        }

        private void MonitorBrowser()
        {
            string currentUrl = GetBrowserUrl();

            // 주소가 비어있지 않고, 마지막으로 보낸 주소와 다를 때만 서버로 전송
            if (!string.IsNullOrEmpty(currentUrl) && currentUrl != _lastSentUrl)
            {
                _lastSentUrl = currentUrl;
                SendUrlToServer(currentUrl);
            }
        }

        private string GetBrowserUrl()
        {
            try
            {
                // AutomationElement를 찾기 위한 루트 요소
                AutomationElement root = AutomationElement.RootElement;

                // 'Condition' 대신 'System.Windows.Automation.Condition'을 명시하거나 
                // 아래와 같이 정확한 타입을 지정합니다.
                System.Windows.Automation.Condition searchCondition = new OrCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "주소창 및 검색창"), // 한국어
                    new PropertyCondition(AutomationElement.NameProperty, "Address and search bar") // 영어
                );

                // 나머지 코드는 동일합니다.
                AutomationElement element = root.FindFirst(TreeScope.Descendants, searchCondition);

                if (element != null)
                {
                    var pattern = element.GetCurrentPattern(ValuePattern.Pattern) as ValuePattern;
                    return pattern?.Current.Value;
                }
            }
            catch { }
            return null;
        }

        private async void SendUrlToServer(string url)
        {
            if (_stream == null) return;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(url);
                await _stream.WriteAsync(data, 0, data.Length);
            }
            catch { }
        }

        private async Task ListenForAlerts()
        {
            byte[] buffer = new byte[1024];
            while (_client != null && _client.Connected)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        // 서버로부터 ALERT 메시지를 받으면 경고창 출력
                        if (message.Contains("ALERT"))
                        {
                            MessageBox.Show(message, "⚠️ 관리자 경고", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        }
                    }
                }
                catch { break; }
            }

            Dispatcher.Invoke(() => {
                StatusLabel.Text = "상태: 서버와 연결이 끊어짐";
                StatusLabel.Foreground = System.Windows.Media.Brushes.Red;
            });
        }
    }
}