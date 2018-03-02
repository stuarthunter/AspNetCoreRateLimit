using System.Threading.Tasks;
using AspNetCoreRateLimit.Models;

namespace AspNetCoreRateLimit.Store
{
    public interface IRateLimitCounterStore
    {
        Task<RateLimitResult> AddRequestAsync(string id, RateLimitRule rule);
    }
}