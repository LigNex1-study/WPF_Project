using System;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;

namespace WpfApp4
{
    public partial class MainWindow : Window
    {
        private ClientViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            // ViewModel 객체를 생성하고 이 화면의 데이터 주인(DataContext)으로 설정합니다.
            _viewModel = new ClientViewModel();
            this.DataContext = _viewModel;
        }

        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            // 버튼을 누르면 ViewModel에 있는 연결 함수를 호출합니다.
            await _viewModel.ConnectToServer();
        }
    }
    // INotifyPropertyChanged는 UI에 값이 바뀌었다고 알려주는 역할을 합니다.
    public class ClientViewModel : INotifyPropertyChanged
    {
        private TcpClient _client;
        private string _serverIp = "127.0.0.1";
        private string _status = "상태: 연결 대기 중";
        private bool _canConnect = true;

        // UI의 TextBox와 연결될 속성
        public string ServerIp { get => _serverIp; set { _serverIp = value; OnPropertyChanged("ServerIp"); } }

        // UI의 상태 레이블과 연결될 속성
        public string Status { get => _status; set { _status = value; OnPropertyChanged("Status"); } }

        // 버튼 활성화 여부와 연결될 속성
        public bool CanConnect { get => _canConnect; set { _canConnect = value; OnPropertyChanged("CanConnect"); } }

        // [연결 로직]
        public async Task ConnectToServer()
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(ServerIp, 5000);

                Status = "상태: 연결 성공 (감시 중)";
                CanConnect = false;

                // 1. 서버가 보내는 경고 메시지를 기다리는 스레드 시작
                _ = ListenForServerAlerts();

                // 2. 브라우저 URL을 체크해서 서버로 보내는 타이머 시작
                StartUrlMonitoring();
            }
            catch (Exception ex)
            {
                MessageBox.Show("서버 연결에 실패했습니다: " + ex.Message);
            }
        }

        // [서버 경고 수신 로직] 서버에서 ALERT 메시지를 보내면 클라이언트 화면에 팝업을 띄움
        private async Task ListenForServerAlerts()
        {
            byte[] buffer = new byte[1024];
            NetworkStream stream = _client.GetStream();

            while (_client.Connected)
            {
                try
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, read);
                        if (message.Contains("ALERT"))
                        {
                            // UI 스레드에서 메시지 박스를 띄웁니다.
                            Application.Current.Dispatcher.Invoke(() => {
                                MessageBox.Show(message, "⚠️ 관리자 주의", MessageBoxButton.OK, MessageBoxImage.Warning);
                            });
                        }
                    }
                }
                catch { break; }
            }
            Status = "상태: 서버와 연결이 끊겼습니다.";
            CanConnect = true;
        }

        // [브라우저 감시 로직]
        private void StartUrlMonitoring()
        {
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            string lastUrl = "";

            timer.Tick += async (s, e) => {
                string currentUrl = GetBrowserUrl();
                if (!string.IsNullOrEmpty(currentUrl) && currentUrl != lastUrl)
                {
                    lastUrl = currentUrl;
                    byte[] data = Encoding.UTF8.GetBytes(currentUrl);
                    if (_client != null && _client.Connected)
                    {
                        await _client.GetStream().WriteAsync(data, 0, data.Length);
                    }
                }
            };
            timer.Start();
        }

        private string GetBrowserUrl()
        {
            try
            {
                var root = AutomationElement.RootElement;
                var condition = new OrCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "주소창 및 검색창"),
                    new PropertyCondition(AutomationElement.NameProperty, "Address and search bar"));
                var element = root.FindFirst(TreeScope.Descendants, condition);
                return ((ValuePattern)element?.GetCurrentPattern(ValuePattern.Pattern))?.Current.Value;
            }
            catch { return null; }
        }

        // MVVM 필수 이벤트
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}