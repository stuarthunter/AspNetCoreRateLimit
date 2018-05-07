using System;
using System.Linq;
using System.Net;
using AspNetRateLimit.Common.Models;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreRateLimit.Extensions
{
    public static class HttpContextExtensions
    {
        public static ClientRequest GetClientRequest(this HttpContext httpContext, string clientIdHeader)
        {
            var clientId = "(anon)";
            if (!string.IsNullOrEmpty(clientIdHeader) && httpContext.Request.Headers.Keys.Contains(clientIdHeader, StringComparer.OrdinalIgnoreCase))
            {
                var value = httpContext.Request.Headers[clientIdHeader].First();
                if (!string.IsNullOrWhiteSpace(clientId))
                {
                    clientId = value;
                }
            }

            return new ClientRequest
            {
                ClientIpAddress = null,
                ClientId = clientId,
                HttpVerb = httpContext.Request.Method.ToLower(),
                Path = httpContext.Request.Path.ToString().ToLower()
            };
        }

        public static ClientRequest GetClientRequest(this HttpContext httpContext, string clientIdHeader, string realIpHeader)
        {
            var result = GetClientRequest(httpContext, clientIdHeader);

            if (!string.IsNullOrEmpty(realIpHeader)
                && httpContext.Request.Headers.Keys.Contains(realIpHeader, StringComparer.OrdinalIgnoreCase)
                && TryParseIpHeader(httpContext.Request.Headers[realIpHeader].First(), out var clientIp))
            {
                result.ClientIpAddress = clientIp;
            }
            else
            {
                result.ClientIpAddress = httpContext.Connection?.RemoteIpAddress ?? IPAddress.None;
            }

            return result;
        }

        private static bool TryParseIpHeader(string value, out IPAddress result)
        {
            result = null;

            // use last IP address
            // note: if using X-Forwarded-For then first value is the real IP but can be spoofed if added by user
            var target = value.Split(',').Last().Trim();

            // handle IPV4
            var parts = target.Split(':');
            if (parts.Length <= 2)
            {
                return IPAddress.TryParse(parts[0], out result);
            }

            // handle IPV6
            if (target.StartsWith("["))
            {
                var end = target.IndexOf("]", StringComparison.OrdinalIgnoreCase);
                if (end > 0)
                {
                    return IPAddress.TryParse(target.Substring(0, end + 1), out result);
                }
            }

            return IPAddress.TryParse(target, out result);
        }
    }
}
