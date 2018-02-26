using System;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreRateLimit.Net
{
    public class RemoteIpParser : IIpAddressParser
    {
        public IPAddress ParseIp(string ipAddress)
        {
            // use first IP address
            var target = ipAddress.Split(',').First().Trim();

            // handle IPV4
            var parts = target.Split(':');
            if (parts.Length <= 2)
            {
                return IPAddress.Parse(parts[0]);
            }

            // handle IPV6
            if (target.StartsWith("["))
            {
                var end = target.IndexOf("]", StringComparison.OrdinalIgnoreCase);
                if (end > 0)
                {
                    return IPAddress.Parse(target.Substring(0, end + 1));
                }
            }

            return IPAddress.Parse(target);
        }

        public virtual IPAddress GetClientIp(HttpContext context)
        {
            return context.Connection.RemoteIpAddress;
        }
    }
}
