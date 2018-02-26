using AspNetCoreRateLimit.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AspNetCoreRateLimit.Store
{
    public class MemoryCacheClientPolicyStore: IClientPolicyStore
    {
        private readonly IMemoryCache _memoryCache;

        public MemoryCacheClientPolicyStore(IMemoryCache memoryCache, 
            IOptions<ClientRateLimitOptions> options = null, 
            IOptions<ClientRateLimitPolicies> policies = null)
        {
            _memoryCache = memoryCache;

            //save client rules defined in appsettings in cache on startup
            if(options?.Value != null && policies?.Value?.ClientRules != null)
            {
                foreach (var rule in policies.Value.ClientRules)
                {
                    Set($"{options.Value.ClientPolicyPrefix}_{rule.ClientId}",
                        new ClientRateLimitPolicy
                        {
                            ClientId = rule.ClientId,
                            Rules = rule.Rules
                        });
                }
            }
        }

        public void Set(string id, ClientRateLimitPolicy policy)
        {
            _memoryCache.Set(id, policy);
        }

        public bool Exists(string id)
        {
            return _memoryCache.TryGetValue(id, out ClientRateLimitPolicy _);
        }

        public ClientRateLimitPolicy Get(string id)
        {
            if (_memoryCache.TryGetValue(id, out ClientRateLimitPolicy stored))
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
