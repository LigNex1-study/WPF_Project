using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using re_server.ViewModels;

namespace re_server.Models
{
    public class ClientInfo : ViewModelBase
    {
        public string Ip { get; }

        private string _url = string.Empty;
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        public ClientInfo(string ip)
        {
            Ip = ip;
        }

        public override string ToString() => Ip;
    }
}
