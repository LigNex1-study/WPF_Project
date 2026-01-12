using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp4.Services
{
    public class TcpNetworkService : INetworkService
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _listenCts;

        public bool IsConnected => _client?.Connected == true;

        public event Action<string>? MessageReceived;
        public event Action<string>? Disconnected;

        public async Task ConnectAsync(string ip, int port, CancellationToken token)
        {
            if (IsConnected) return;

            _client = new TcpClient();
            await _client.ConnectAsync(ip, port);

            _stream = _client.GetStream();

            _listenCts?.Cancel();
            _listenCts = CancellationTokenSource.CreateLinkedTokenSource(token);

            _ = ListenLoop(_listenCts.Token);
        }

        public async Task DisconnectAsync(string reason)
        {
            try { _listenCts?.Cancel(); } catch { }

            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }

            _stream = null;
            _client = null;

            await Task.CompletedTask;
            Disconnected?.Invoke(reason);
        }

        public async Task SendAsync(string text, CancellationToken token)
        {
            if (!IsConnected || _stream == null) return;

            byte[] data = Encoding.UTF8.GetBytes(text);
            await _stream.WriteAsync(data, 0, data.Length, token);
        }

        private async Task ListenLoop(CancellationToken token)
        {
            if (_stream == null || _client == null) return;

            byte[] buffer = new byte[2048];

            try
            {
                while (!token.IsCancellationRequested && _client.Connected)
                {
                    int read = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read <= 0) break;

                    var msg = Encoding.UTF8.GetString(buffer, 0, read);
                    MessageReceived?.Invoke(msg);
                }

                if (!token.IsCancellationRequested)
                    Disconnected?.Invoke("서버 연결 종료");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    Disconnected?.Invoke("수신 오류: " + ex.Message);
            }
        }

        public void Dispose()
        {
            _ = DisconnectAsync("Dispose");
        }
    }
}
