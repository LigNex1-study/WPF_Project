using System;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp4.Services
{
    public interface INetworkService : IDisposable
    {
        bool IsConnected { get; }

        event Action<string>? MessageReceived;
        event Action<string>? Disconnected; // reason

        Task ConnectAsync(string ip, int port, CancellationToken token);
        Task DisconnectAsync(string reason);
        Task SendAsync(string text, CancellationToken token);
    }
}
