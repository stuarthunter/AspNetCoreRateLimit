﻿
namespace AspNetRateLimit.Common.Models
{
    public class ClientRateLimitPolicy
    {
        public string ClientId { get; set; }
        public RateLimitRules Rules { get; set; }
    }
}
