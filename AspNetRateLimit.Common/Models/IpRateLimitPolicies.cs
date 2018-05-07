using System.Collections.Generic;

namespace AspNetRateLimit.Common.Models
{
    public class IpRateLimitPolicies
    {
        public List<IpRateLimitPolicy> IpRules { get; set; }
    }
}
