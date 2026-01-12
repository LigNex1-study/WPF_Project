using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace re_server.Services
{
    public interface IServerService
    {
        event Action<string>? ClientConnected;
        event Action<string, string>? ClientMessageReceived;
        event Action<string>? ClientDisconnected;

        Task StartAsync(int port);
        void Stop();
        void SendMessage(string ip, string message);
    }
}
