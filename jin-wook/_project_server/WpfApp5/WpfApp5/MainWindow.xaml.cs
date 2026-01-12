using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Web.WebView2.Core;
using WpfApp5.Models;
using WpfApp5.Services;

namespace WpfApp5
{
    /// <summary>
    /// 메인 윈도우 클래스입니다.
    /// 사용자 인터페이스(UI)를 관리하고, 서버와 성능 모니터의 이벤트를 처리합니다.
    /// </summary>
    public partial class MainWindow : Window
    {
        // ========== UI 데이터 바인딩용 속성 ==========
        // LiveCharts 라이브러리에서 사용하는 차트 데이터입니다.
        // XAML에서 {Binding CpuChartValues}로 바인딩됩니다.

        /// <summary>
        /// CPU 사용률 차트 데이터
        /// 최근 30개의 CPU 사용률 값을 저장합니다.
        /// </summary>
        public ChartValues<double> CpuChartValues { get; set; } = new ChartValues<double>();

        /// <summary>
        /// 네트워크 속도 차트 데이터
        /// 최근 30개의 네트워크 속도 값을 저장합니다.
        /// </summary>
        public ChartValues<double> NetChartValues { get; set; } = new ChartValues<double>();

        // ========== 서비스 객체 ==========

        /// <summary>
        /// 모니터링 서버 객체
        /// 클라이언트 연결을 관리하고 메시지를 처리합니다.
        /// </summary>
        private MonitoringServer _server;

        /// <summary>
        /// 성능 모니터 객체
        /// 시스템 리소스(CPU, RAM, 네트워크)를 모니터링합니다.
        /// </summary>
        private PerformanceMonitor _performanceMonitor;

        /// <summary>
        /// 현재 선택된 클라이언트의 IP 주소
        /// 사용자가 목록에서 클라이언트를 선택하면 이 값이 설정됩니다.
        /// </summary>
        private string selectedClientIP = "";

        /// <summary>
        /// 생성자: MainWindow를 초기화합니다.
        /// UI 컴포넌트를 초기화하고, 서버와 성능 모니터를 설정합니다.
        /// </summary>
        public MainWindow()
        {
            // XAML에서 정의한 UI 컴포넌트를 초기화합니다
            InitializeComponent();

            // 데이터 바인딩을 위해 이 윈도우를 DataContext로 설정합니다
            // 이렇게 하면 XAML에서 {Binding CpuChartValues} 같은 바인딩이 작동합니다
            DataContext = this;

            // ========== 서버 초기화 ==========
            _server = new MonitoringServer();

            // 서버 이벤트에 메서드를 연결합니다 (이벤트 구독)
            // 클라이언트가 연결되면 OnClientConnected 메서드가 호출됩니다
            _server.ClientConnected += OnClientConnected;
            _server.ClientDisconnected += OnClientDisconnected;
            _server.UrlReceived += OnUrlReceived;
            _server.SecurityAlert += OnSecurityAlert;

            // ========== 성능 모니터 초기화 ==========
            _performanceMonitor = new PerformanceMonitor();

            // 성능 지표가 업데이트되면 OnMetricsUpdated 메서드가 호출됩니다
            _performanceMonitor.MetricsUpdated += OnMetricsUpdated;

            // 웹뷰 초기화 (비동기)
            InitializeBrowser();

            // 성능 모니터링 시작 (1초마다 업데이트)
            _performanceMonitor.StartMonitoring();
        }

        /// <summary>
        /// WebView2 브라우저를 초기화하는 메서드입니다.
        /// 웹페이지를 표시하기 위해 필요한 초기화 작업을 수행합니다.
        /// </summary>
        private async void InitializeBrowser()
        {
            try
            {
                // WebView2 런타임이 준비될 때까지 대기합니다
                // null을 전달하면 기본 환경을 사용합니다
                await MyWebView.EnsureCoreWebView2Async(null);
            }
            catch { }
        }

        /// <summary>
        /// 클라이언트가 연결되었을 때 호출되는 이벤트 핸들러입니다.
        /// UI의 클라이언트 목록에 IP 주소를 추가합니다.
        /// </summary>
        /// <param name="ip">연결된 클라이언트의 IP 주소</param>
        private void OnClientConnected(string ip)
        {
            // UI 스레드에서 실행되도록 Dispatcher.Invoke를 사용합니다
            // 서버는 별도의 스레드에서 실행되므로, UI를 업데이트하려면 UI 스레드로 전환해야 합니다
            Dispatcher.Invoke(() =>
            {
                // 중복 추가를 방지하기 위해 이미 목록에 있는지 확인합니다
                if (!ClientListBox.Items.Contains(ip))
                {
                    // 클라이언트 목록에 IP 주소 추가
                    ClientListBox.Items.Add(ip);
                }
            });
        }

        /// <summary>
        /// 클라이언트 연결이 끊어졌을 때 호출되는 이벤트 핸들러입니다.
        /// UI의 클라이언트 목록에서 IP 주소를 제거합니다.
        /// </summary>
        /// <param name="ip">연결이 끊어진 클라이언트의 IP 주소</param>
        private void OnClientDisconnected(string ip)
        {
            Dispatcher.Invoke(() =>
            {
                // 클라이언트 목록에서 IP 주소 제거
                ClientListBox.Items.Remove(ip);

                // 만약 연결이 끊어진 클라이언트가 현재 선택된 클라이언트라면
                if (selectedClientIP == ip)
                {
                    // 선택 해제
                    selectedClientIP = "";
                    DetailHeader.Text = "모니터링 대상을 선택하세요";
                    UpdateWebView("about:blank");  // 웹뷰를 빈 페이지로 설정
                }
            });
        }

        /// <summary>
        /// 클라이언트로부터 URL을 받았을 때 호출되는 이벤트 핸들러입니다.
        /// 선택된 클라이언트의 URL이면 웹뷰를 업데이트합니다.
        /// </summary>
        /// <param name="ip">클라이언트의 IP 주소</param>
        /// <param name="url">받은 URL</param>
        private void OnUrlReceived(string ip, string url)
        {
            Dispatcher.Invoke(() =>
            {
                // 현재 선택된 클라이언트의 URL이면 웹뷰를 업데이트합니다
                if (selectedClientIP == ip)
                {
                    UpdateWebView(url);
                }
            });
        }

        /// <summary>
        /// 보안 경고가 발생했을 때 호출되는 이벤트 핸들러입니다.
        /// 차단된 사이트에 접속이 감지되면 경고 메시지를 표시합니다.
        /// </summary>
        /// <param name="ip">클라이언트의 IP 주소</param>
        /// <param name="url">차단된 URL</param>
        private void OnSecurityAlert(string ip, string url)
        {
            Dispatcher.Invoke(() =>
            {
                // 경고 메시지 박스 표시
                MessageBox.Show($"[차단 감지] 사용자: {ip}\n주소: {url}",
                    "보안 경고", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        /// <summary>
        /// 성능 지표가 업데이트되었을 때 호출되는 이벤트 핸들러입니다.
        /// UI의 ProgressBar, TextBlock, 차트를 업데이트합니다.
        /// </summary>
        /// <param name="metrics">업데이트된 성능 지표</param>
        private void OnMetricsUpdated(SystemMetrics metrics)
        {
            Dispatcher.Invoke(() =>
            {
                // ========== CPU 정보 업데이트 ==========
                // ProgressBar의 값을 CPU 사용률로 설정 (0~100)
                CpuBar.Value = metrics.CpuUsage;
                // 텍스트에 CPU 사용률 표시 (예: "45.5%")
                CpuText.Text = $"{metrics.CpuUsage:F1}%";

                // ========== RAM 정보 업데이트 ==========
                // ProgressBar의 값을 RAM 사용률로 설정 (0~100)
                RamBar.Value = metrics.RamUsagePercent;
                // 텍스트에 RAM 사용량 표시 (예: "8.5 / 16.0 GB")
                RamText.Text = $"{metrics.RamUsed:F1} / {metrics.RamTotal:F1} GB";

                // ========== 네트워크 정보 업데이트 ==========
                // 텍스트에 네트워크 속도 표시 (예: "10.50 Mbps")
                NetText.Text = $"{metrics.NetworkSpeed:F2} Mbps";

                // ========== 차트 데이터 업데이트 ==========
                // CPU 사용률을 차트 데이터에 추가
                CpuChartValues.Add(metrics.CpuUsage);
                // 최근 30개만 유지 (오래된 데이터 제거)
                if (CpuChartValues.Count > 30) CpuChartValues.RemoveAt(0);

                // 네트워크 속도를 차트 데이터에 추가
                NetChartValues.Add(metrics.NetworkSpeed);
                // 최근 30개만 유지
                if (NetChartValues.Count > 30) NetChartValues.RemoveAt(0);
            });
        }

        /// <summary>
        /// "보안 서버 가동" 버튼을 클릭했을 때 호출되는 이벤트 핸들러입니다.
        /// 서버를 시작하고 UI 상태를 업데이트합니다.
        /// </summary>
        /// <param name="sender">이벤트를 발생시킨 객체 (버튼)</param>
        /// <param name="e">이벤트 인자</param>
        private async void StartServer_Click(object sender, RoutedEventArgs e)
        {
            // 이미 서버가 실행 중이면 경고 메시지를 표시하고 종료합니다
            if (_server.IsRunning)
            {
                MessageBox.Show("서버가 이미 실행 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 서버 시작 (비동기)
                await _server.StartAsync();

                // ========== UI 상태 업데이트 ==========
                // 상태 표시등을 녹색으로 변경 (서버 실행 중)
                StatusDot.Fill = Brushes.LimeGreen;
                // 상태 텍스트 업데이트
                StatusText.Text = $"서버 가동 중 (포트: 5000, 최대 {MonitoringServer.MAX_CLIENTS}명)";
                // 시작 버튼 비활성화
                StartServerBtn.IsEnabled = false;
                // 중지 버튼 활성화
                StopServerBtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                // 서버 시작 실패 시 오류 메시지 표시
                MessageBox.Show($"서버 시작 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// "서버 중지" 버튼을 클릭했을 때 호출되는 이벤트 핸들러입니다.
        /// 서버를 중지하고 UI 상태를 업데이트합니다.
        /// </summary>
        /// <param name="sender">이벤트를 발생시킨 객체 (버튼)</param>
        /// <param name="e">이벤트 인자</param>
        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 서버 중지
                _server.Stop();

                // ========== UI 상태 업데이트 ==========
                // 상태 표시등을 빨간색으로 변경 (서버 중지됨)
                StatusDot.Fill = Brushes.Red;
                // 상태 텍스트 업데이트
                StatusText.Text = "서버 중지됨";
                // 시작 버튼 활성화
                StartServerBtn.IsEnabled = true;
                // 중지 버튼 비활성화
                StopServerBtn.IsEnabled = false;
            }
            catch (Exception ex)
            {
                // 서버 중지 실패 시 오류 메시지 표시
                MessageBox.Show($"서버 중지 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// WebView2에 URL을 표시하는 메서드입니다.
        /// 선택된 클라이언트가 접속한 웹사이트를 미리보기로 보여줍니다.
        /// </summary>
        /// <param name="url">표시할 URL</param>
        private void UpdateWebView(string url)
        {
            // URL이 비어있거나 빈 페이지면 웹뷰를 초기화합니다
            if (string.IsNullOrEmpty(url) || url == "about:blank")
            {
                try
                {
                    if (MyWebView.CoreWebView2 != null)
                        MyWebView.CoreWebView2.Navigate("about:blank");
                }
                catch { }
                return;
            }

            // URL에 프로토콜(http:// 또는 https://)이 없으면 자동으로 https://를 추가합니다
            if (!url.StartsWith("http")) url = "https://" + url;

            try
            {
                // WebView2가 초기화되어 있으면 URL로 이동합니다
                if (MyWebView.CoreWebView2 != null)
                    MyWebView.CoreWebView2.Navigate(url);
            }
            catch { }
        }

        /// <summary>
        /// 클라이언트 목록에서 선택이 변경되었을 때 호출되는 이벤트 핸들러입니다.
        /// 선택된 클라이언트의 웹사이트를 웹뷰에 표시합니다.
        /// </summary>
        /// <param name="sender">이벤트를 발생시킨 객체 (ListBox)</param>
        /// <param name="e">선택 변경 이벤트 인자</param>
        private void ClientListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 선택된 항목이 있으면
            if (ClientListBox.SelectedItem != null)
            {
                // 선택된 IP 주소를 저장합니다
                selectedClientIP = ClientListBox.SelectedItem.ToString();

                // 헤더 텍스트를 업데이트합니다
                DetailHeader.Text = $"📍 실시간 모니터링: {selectedClientIP}";

                // 서버에서 해당 클라이언트의 현재 URL을 가져옵니다
                string url = _server.GetClientUrl(selectedClientIP);

                // URL이 있으면 웹뷰에 표시하고, 없으면 빈 페이지를 표시합니다
                if (!string.IsNullOrEmpty(url))
                {
                    UpdateWebView(url);
                }
                else
                {
                    UpdateWebView("about:blank");
                }
            }
        }

        /// <summary>
        /// 차단 키워드 입력란의 텍스트가 변경되었을 때 호출되는 이벤트 핸들러입니다.
        /// 모든 클라이언트의 현재 URL을 새 키워드로 다시 검사합니다.
        /// </summary>
        /// <param name="sender">이벤트를 발생시킨 객체 (TextBox)</param>
        /// <param name="e">텍스트 변경 이벤트 인자</param>
        private void ForbiddenUrlInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 서버가 실행 중이 아니면 검사를 수행하지 않습니다
            if (!_server.IsRunning) return;

            // 입력된 텍스트를 쉼표(,)로 분리하여 키워드 목록을 만듭니다
            // Where: 빈 문자열이나 공백만 있는 항목 제거
            // Select: 각 키워드의 앞뒤 공백 제거
            // ToList: 리스트로 변환
            var keywords = ForbiddenUrlInput.Text.Split(',')
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .ToList();

            // 현재 연결된 모든 클라이언트의 URL을 검사합니다
            foreach (var ip in _server.GetClientIps())
            {
                // 클라이언트의 현재 URL을 가져옵니다
                string url = _server.GetClientUrl(ip);

                // URL이 있으면 보안 검사를 수행합니다
                if (!string.IsNullOrEmpty(url))
                {
                    _server.CheckSecurityAlert(ip, url, keywords);
                }
            }
        }

        /// <summary>
        /// 윈도우가 닫힐 때 호출되는 메서드입니다.
        /// 서버와 성능 모니터를 정리하고 리소스를 해제합니다.
        /// </summary>
        /// <param name="e">이벤트 인자</param>
        protected override void OnClosed(EventArgs e)
        {
            // 서버 중지
            _server?.Stop();

            // 성능 모니터링 중지 및 리소스 해제
            _performanceMonitor?.StopMonitoring();
            _performanceMonitor?.Dispose();

            // 기본 OnClosed 메서드 호출
            base.OnClosed(e);
        }
    }
}
