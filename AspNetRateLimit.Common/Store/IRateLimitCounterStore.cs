using System.Threading.Tasks;
using AspNetRateLimit.Common.Models;

namespace AspNetRateLimit.Common.Store
{
    public interface IRateLimitCounterStore
    {
        Task<RateLimitResult> AddRequestAsync(string id, RateLimitRule rule);
    }
}