using System.Net;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreRateLimit.Net
{
    public interface IIpAddressParser
    {
        IPAddress GetClientIp(HttpContext context);

        IPAddress ParseIp(string ipAddress);
    }
}
