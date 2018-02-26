using AspNetCoreRateLimit.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AspNetCoreRateLimit.Store
{
    public class MemoryCacheIpPolicyStore : IIpPolicyStore
    {
        private readonly IMemoryCache _memoryCache;

        public MemoryCacheIpPolicyStore(IMemoryCache memoryCache, 
            IOptions<IpRateLimitOptions> options = null, 
            IOptions<IpRateLimitPolicies> policies = null)
        {
            _memoryCache = memoryCache;

            //save ip rules defined in appsettings in cache on startup
            if (options?.Value != null && policies?.Value?.IpRules != null)
            {
                Set($"{options.Value.IpPolicyPrefix}", policies.Value);
            }
        }

        public void Set(string id, IpRateLimitPolicies policy)
        {
            _memoryCache.Set(id, policy);
        }

        public bool Exists(string id)
        {
            return _memoryCache.TryGetValue(id, out IpRateLimitPolicies _);
        }

        public IpRateLimitPolicies Get(string id)
        {
            if (_memoryCache.TryGetValue(id, out IpRateLimitPolicies stored))
            {
                return stored;
            }

            return null;
        }

        public void Remove(string id)
        {
            _memoryCache.Remove(id);
        }
    }
}
