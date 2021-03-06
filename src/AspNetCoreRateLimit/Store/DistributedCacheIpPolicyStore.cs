﻿using AspNetRateLimit.Common.Models;
using AspNetRateLimit.Common.Store;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace AspNetCoreRateLimit.Store
{
    public class DistributedCacheIpPolicyStore : IIpPolicyStore
    {
        // TODO: REQUIRES REVIEW 
        // No need to fetch from remote store for every request
        // Cache locally for 1 min?
        // IpAddressRange not persisted
        // Existing stored values will be overwritten on startup

        private readonly IDistributedCache _memoryCache;

        public DistributedCacheIpPolicyStore(IDistributedCache memoryCache, 
            IOptions<IpRateLimitOptions> options = null, 
            IOptions<IpRateLimitPolicies> policies = null)
        {
            _memoryCache = memoryCache;

            // save IP rules defined in appsettings in distributed cache on startup
            if (options?.Value != null && policies?.Value?.IpRules != null)
            {
                Set($"{options.Value.IpPolicyPrefix}", policies.Value);
            }
        }

        public void Set(string id, IpRateLimitPolicies policy)
        {
            _memoryCache.SetString(id, JsonConvert.SerializeObject(policy));
        }

        public bool Exists(string id)
        {
            var stored = _memoryCache.GetString(id);
            return !string.IsNullOrEmpty(stored);
        }

        public IpRateLimitPolicies Get(string id)
        {
            var stored = _memoryCache.GetString(id);
            if (!string.IsNullOrEmpty(stored))
            {
                return JsonConvert.DeserializeObject<IpRateLimitPolicies>(stored);
            }
            return null;
        }

        public void Remove(string id)
        {
            _memoryCache.Remove(id);
        }
    }
}
