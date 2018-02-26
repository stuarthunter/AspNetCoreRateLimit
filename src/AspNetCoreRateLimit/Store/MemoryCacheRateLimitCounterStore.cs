using System;
using AspNetCoreRateLimit.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AspNetCoreRateLimit.Store
{
    public class MemoryCacheRateLimitCounterStore: IRateLimitCounterStore
    {
        private readonly IMemoryCache _memoryCache;
        private readonly object _lock = new object();

        public MemoryCacheRateLimitCounterStore(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public RateLimitResult AddRequest(string id, RateLimitRule rule)
        {
            var counter = Get(id);
            if (counter == null || counter.IsExpired)
            {
                // serial create of new rate limit counter
                lock (_lock)
                {
                    counter = Get(id);
                    if (counter == null || counter.IsExpired)
                    {
                        var periodTimeSpan = rule.GetPeriodTimeSpan();
                        counter = new RateLimitCounter(rule.UseSlidingExpiration, periodTimeSpan);
                        Set(id, counter, periodTimeSpan, rule.UseSlidingExpiration);

                        return new RateLimitResult
                        {
                            Success = true,
                            Remaining = rule.Limit - 1,
                            Expiry = DateTime.UtcNow.Add(periodTimeSpan)
                        };
                    }
                }
            }

            return counter.AddRequest(rule.Limit);
        }

        private RateLimitCounter Get(string id)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            return _memoryCache.TryGetValue(id, out RateLimitCounter stored) ? stored : null;
        }

        private void Set(string id, RateLimitCounter counter, TimeSpan expirationPeriod, bool slidingExpiration)
        {
            var options = new MemoryCacheEntryOptions();
            if (slidingExpiration)
            {
                options.SetSlidingExpiration(expirationPeriod);
            }
            else
            {
                options.SetAbsoluteExpiration(expirationPeriod);
            }
            _memoryCache.Set(id, counter, options);
        }
    }
}
