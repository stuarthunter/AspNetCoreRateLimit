using System;
using System.Collections.Generic;
using AspNetCoreRateLimit.Net;

namespace AspNetCoreRateLimit.Models
{
    public class IpRateLimitPolicy
    {
        private string _ip;
        
        public string Ip
        {
            get => _ip;
            set
            {
                _ip = value;
                IpAddressRange = new IpAddressRange(_ip);
            }
        }

        public IpAddressRange IpAddressRange { get; private set; }

        public List<RateLimitRule> Rules { get; set; }
    }
}
