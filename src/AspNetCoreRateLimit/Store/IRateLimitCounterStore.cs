
namespace AspNetCoreRateLimit
{
    public interface IRateLimitCounterStore
    {
        RateLimitResult AddRequest(string id, RateLimitRule rule);
    }
}