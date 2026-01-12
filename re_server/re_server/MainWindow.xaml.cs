using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using re_server.Services;
using re_server.ViewModels;

namespace re_server
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();

            _vm = new MainViewModel(
                new TcpServerService(),
                new PerformanceService(),
                new SecurityPolicyService()
            );

            _vm.RequestNavigate += url =>
            {
                try
                {
                    if (MyWebView.CoreWebView2 != null)
                        MyWebView.CoreWebView2.Navigate(url);
                }
                catch { }
            };

            _vm.AlertRequested += msg =>
            {
                MessageBox.Show(msg, "보안 경고", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            DataContext = _vm;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await MyWebView.EnsureCoreWebView2Async();
        }
    }
}