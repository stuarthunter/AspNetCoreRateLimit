using AspNetCoreRateLimit.Models;

namespace AspNetCoreRateLimit.Store
{
    public interface IRateLimitCounterStore
    {
        RateLimitResult AddRequest(string id, RateLimitRule rule);
    }
}