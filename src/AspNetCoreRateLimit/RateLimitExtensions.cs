using Microsoft.AspNetCore.Builder;

namespace AspNetCoreRateLimit
{
    public static class RateLimitExtensions
    {
        public static IApplicationBuilder UseIpRateLimiting(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<IpRateLimitMiddleware>();
        }

        public static IApplicationBuilder UseClientRateLimiting(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ClientRateLimitMiddleware>();
        }
    }
}
