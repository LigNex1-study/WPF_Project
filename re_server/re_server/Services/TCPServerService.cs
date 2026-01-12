using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace re_server.Services
{
    public class TcpServerService : IServerService
    {
        public event Action<string>? ClientConnected;
        public event Action<string, string>? ClientMessageReceived;
        public event Action<string>? ClientDisconnected;

        private TcpListener? _listener;
        private readonly ConcurrentDictionary<string, TcpClient> _clients = new();

        private bool _running = false;

        public async Task StartAsync(int port)
        {
            if (_running) return;

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _running = true;

            while (_running)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
                catch
                {
                    if (!_running) break;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            string ip = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

            _clients[ip] = client;
            ClientConnected?.Invoke(ip);

            try
            {
                using var stream = client.GetStream();
                var buffer = new byte[1024];

                while (_running)
                {
                    int read;
                    try
                    {
                        read = await stream.ReadAsync(buffer, 0, buffer.Length);
                    }
                    catch
                    {
                        break;
                    }

                    if (read == 0) break;

                    var msg = Encoding.UTF8.GetString(buffer, 0, read).Trim();
                    ClientMessageReceived?.Invoke(ip, msg);
                }
            }
            finally
            {
                _clients.TryRemove(ip, out _);
                ClientDisconnected?.Invoke(ip);

                try { client.Close(); } catch { }
            }
        }

        public void Stop()
        {
            _running = false;
            _listener?.Stop();

            foreach (var kv in _clients)
            {
                try { kv.Value.Close(); } catch { }
            }

            _clients.Clear();
        }

        public void SendMessage(string ip, string message)
        {
            if (_clients.TryGetValue(ip, out var client))
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(message);
                    client.GetStream().Write(bytes, 0, bytes.Length);
                }
                catch
                {
                    // 무시
                }
            }
        }
    }
}
