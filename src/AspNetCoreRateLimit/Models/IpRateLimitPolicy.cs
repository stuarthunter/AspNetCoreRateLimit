using System.Collections.Generic;

namespace AspNetCoreRateLimit.Models
{
    public class IpRateLimitPolicy
    {
        public string Ip { get; set; }
        public List<RateLimitRule> Rules { get; set; }
    }
}
