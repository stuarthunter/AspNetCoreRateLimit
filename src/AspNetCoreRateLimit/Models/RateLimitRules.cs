using System;
using System.Collections.Generic;
using System.Linq;

namespace AspNetCoreRateLimit.Models
{
    public class RateLimitRules : List<RateLimitRule>
    {
        public IEnumerable<RateLimitRule> GetEndpointRules(string httpVerb, string path)
        {
            // get rules matching endpoint
            var rules = this.Where(x => $"{httpVerb}:{path}".StartsWith(x.Endpoint, StringComparison.OrdinalIgnoreCase) || $"*:{path}".StartsWith(x.Endpoint, StringComparison.OrdinalIgnoreCase));
            
            // filter to single rule for each period
            return rules.GroupBy(x => x.Period).Select(x => x.OrderBy(y => y.Limit).ThenBy(y => y.Endpoint.Length).First());
        }

        public IEnumerable<RateLimitRule> GetGlobalRules(string httpVerb)
        {
            // get global rules
            var rules = this.Where(x => $"{httpVerb}:*".Equals(x.Endpoint, StringComparison.OrdinalIgnoreCase) || x.Endpoint == "*:*" || x.Endpoint == "*");

            // filter to single rule for each period
            return rules.GroupBy(x => x.Period).Select(x => x.OrderBy(y => y.Limit).ThenBy(y => y.Endpoint.Length).First());
        }
    }
}
