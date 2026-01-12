using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using re_server.Models;

namespace re_server.Services
{
    public interface IPerformanceService : IDisposable
    {
        event Action<PerformanceData>? PerformanceUpdated;
        void Start();
        void Stop();
    }
}
