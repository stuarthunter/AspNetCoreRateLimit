using System;
using System.Collections.Generic;
using AspNetCoreRateLimit.Net;

namespace AspNetCoreRateLimit.Models
{
    public class IpRateLimitPolicy
    {
        private string _ip;
        private Lazy<IpAddressRange> _ipAddressRange;

        public string Ip
        {
            get => _ip;
            set
            {
                _ip = value;
                _ipAddressRange = new Lazy<IpAddressRange>(() => new IpAddressRange(_ip));
            }
        }

        public IpAddressRange IpAddressRange => _ipAddressRange.Value;

        public List<RateLimitRule> Rules { get; set; }
    }
}
