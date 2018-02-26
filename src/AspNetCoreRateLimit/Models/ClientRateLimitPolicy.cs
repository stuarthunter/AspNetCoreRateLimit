using System;
using System.Collections.Generic;
using System.Linq;

namespace AspNetCoreRateLimit.Models
{
    public class ClientRateLimitPolicy
    {
        public string ClientId { get; set; }
        public RateLimitRules Rules { get; set; }
    }
}
