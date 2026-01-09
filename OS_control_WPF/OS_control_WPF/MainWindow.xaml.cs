using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using SystemMonitor.Helpers;
using LiveCharts;
using System.ComponentModel;
using System.Net.NetworkInformation;

namespace SystemMonitor
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private SystemInfoProvider infoProvider = new SystemInfoProvider();
        private DispatcherTimer timer = new DispatcherTimer();
        private long _lastBytes = 0;
        public ChartValues<float> CpuChartValues { get; set; } = new ChartValues<float>();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            // 그래프 초기 공간 확보
            for (int i = 0; i < 30; i++)
            {
                CpuChartValues.Add(0);
            }

            // 타이머 설정 및 이벤트 연결 (풀어서 작성)
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            RefreshProcessList();
        }

        // 1초마다 실행되는 업데이트 로직
        private void Timer_Tick(object sender, EventArgs e)
        {
            // 하드웨어 수치 가져오기
            float cpuUsage = infoProvider.GetCpuUsage();
            float ramUsage = infoProvider.GetRamUsage();
            int wifiStrength = infoProvider.GetWifiSignalStrength();

            // UI 컨트롤에 값 적용
            CpuBar.Value = cpuUsage;
            RamBar.Value = ramUsage;
            WifiBar.Value = wifiStrength;

            // 그래프 데이터 갱신
            CpuChartValues.Add(cpuUsage);
            if (CpuChartValues.Count > 30)
            {
                CpuChartValues.RemoveAt(0);
            }

            // 네트워크 속도 업데이트 호출
            UpdateNetSpeed();
        }

        // 네트워크 속도를 계산하는 로직 (foreach문 사용)
        private void UpdateNetSpeed()
        {
            long totalBytesReceived = 0;
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface ni in interfaces)
            {
                // 연결이 활성화된 네트워크 어댑터만 합산
                if (ni.OperationalStatus == OperationalStatus.Up)
                {
                    totalBytesReceived += ni.GetIPStatistics().BytesReceived;
                }
            }

            if (_lastBytes > 0)
            {
                long diff = totalBytesReceived - _lastBytes;
                // Byte를 Mbps로 변환하는 공식
                double mbps = (diff * 8) / 1024.0 / 1024.0;
                NetSpeedText.Text = Math.Round(mbps, 2).ToString() + " Mbps";
            }
            _lastBytes = totalBytesReceived;
        }

        // 프로세스 리스트를 가져오고 중복을 제거하는 로직 (foreach/조건문 사용)
        private void RefreshProcessList()
        {
            Process[] allProcesses = Process.GetProcesses();
            List<Process> uniqueProcesses = new List<Process>();
            List<string> seenNames = new List<string>();

            foreach (Process p in allProcesses)
            {
                // 이미 리스트에 추가된 이름인지 확인 (중복 제거 조건문)
                bool isAlreadyAdded = false;
                foreach (string name in seenNames)
                {
                    if (name == p.ProcessName)
                    {
                        isAlreadyAdded = true;
                        break;
                    }
                }

                if (isAlreadyAdded == false)
                {
                    uniqueProcesses.Add(p);
                    seenNames.Add(p.ProcessName);
                }
            }

            // 이름순으로 간단한 정렬 (IComparer 사용 대신 리스트 정렬 기능 사용)
            uniqueProcesses.Sort((x, y) => string.Compare(x.ProcessName, y.ProcessName));

            ProcessList.ItemsSource = uniqueProcesses;
        }

        // 네트워크 스캔 버튼 클릭 이벤트
        private async void ScanBtn_Click(object sender, RoutedEventArgs e)
        {
            BtnScan.IsEnabled = false;
            BtnScan.Content = "스캔 중...";
            DeviceListBox.Items.Clear();

            // 비동기로 기기 목록 가져오기
            List<string> devices = await infoProvider.ScanNetworkDevices();

            foreach (string device in devices)
            {
                DeviceListBox.Items.Add(device);
            }

            BtnScan.IsEnabled = true;
            BtnScan.Content = "Scan Network Devices";
        }

        private void KillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessList.SelectedItem is Process selected)
            {
                try
                {
                    selected.Kill();
                    // 종료 후 리스트 새로고침
                    System.Threading.Thread.Sleep(500); // 프로세스가 완전히 종료될 시간 대기
                    RefreshProcessList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("종료 실패: " + ex.Message);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}