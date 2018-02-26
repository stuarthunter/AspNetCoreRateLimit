using System;
using System.Collections.Generic;
using System.Linq;

namespace AspNetCoreRateLimit.Models
{
    public class RateLimitRules : List<RateLimitRule>
    {
        public IEnumerable<RateLimitRule> GetEndpointRules(string httpVerb, string path)
        {
            return this.Where(x => $"{httpVerb}:{path}".StartsWith(x.Endpoint, StringComparison.OrdinalIgnoreCase) || $"*:{path}".StartsWith(x.Endpoint, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<RateLimitRule> GetGlobalRules(string httpVerb)
        {
            return this.Where(x => $"{httpVerb}:*".Equals(x.Endpoint, StringComparison.OrdinalIgnoreCase) || x.Endpoint == "*:*" || x.Endpoint == "*");
        }
    }
}
