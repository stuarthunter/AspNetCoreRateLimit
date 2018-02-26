using System.Collections.Generic;
using AspNetCoreRateLimit.Models;

namespace AspNetCoreRateLimit
{
    public class IpRateLimitPolicy
    {
        public string Ip { get; set; }
        public List<RateLimitRule> Rules { get; set; }
    }
}
