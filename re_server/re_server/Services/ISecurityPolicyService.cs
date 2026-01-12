using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace re_server.Services
{
    public interface ISecurityPolicyService
    {
        bool Check(string url, string forbiddenText, out string matchedKeyword);
    }
}
