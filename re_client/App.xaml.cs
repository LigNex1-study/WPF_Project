using System.Windows;
using WpfApp4.Services;
using WpfApp4.ViewModels;

namespace WpfApp4
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Services (수동 DI)
            INetworkService network = new TcpNetworkService();
            IBrowserMonitorService browser = new BrowserMonitorService();
            IDialogService dialog = new DialogService();

            // ViewModel
            var vm = new MainViewModel(network, browser, dialog, Dispatcher);

            // View
            var window = new MainWindow
            {
                DataContext = vm
            };
            window.Show();
        }
    }
}
