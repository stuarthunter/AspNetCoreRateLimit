using System.Collections.Generic;

namespace AspNetCoreRateLimit.Models
{
    public class IpRateLimitPolicies
    {
        public List<IpRateLimitPolicy> IpRules { get; set; }
    }
}
