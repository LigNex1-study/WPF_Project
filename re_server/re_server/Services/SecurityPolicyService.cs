using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace re_server.Services
{
    public class SecurityPolicyService : ISecurityPolicyService
    {
        public bool Check(string url, string forbiddenText, out string matchedKeyword)
        {
            matchedKeyword = string.Empty;
            if (string.IsNullOrWhiteSpace(forbiddenText) || string.IsNullOrWhiteSpace(url))
                return false;

            var loweredUrl = url.ToLowerInvariant();

            var keywords = forbiddenText
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim().ToLowerInvariant())
                .Where(k => !string.IsNullOrEmpty(k));

            foreach (var k in keywords)
            {
                if (loweredUrl.Contains(k))
                {
                    matchedKeyword = k;
                    return true;
                }
            }

            return false;
        }
    }
}
