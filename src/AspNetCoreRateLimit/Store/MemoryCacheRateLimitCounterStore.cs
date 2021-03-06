﻿using System;
using System.Threading.Tasks;
using AspNetRateLimit.Common.Models;
using AspNetRateLimit.Common.Store;
using Microsoft.Extensions.Caching.Memory;

namespace AspNetCoreRateLimit.Store
{
    public class MemoryCacheRateLimitCounterStore : IRateLimitCounterStore
    {
        private readonly IMemoryCache _memoryCache;
        private readonly object _lock = new object();

        public MemoryCacheRateLimitCounterStore(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public Task<RateLimitResult> AddRequestAsync(string id, RateLimitRule rule)
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
                        counter = new RateLimitCounter(rule.UseSlidingExpiration, rule.PeriodTimeSpan);
                        Set(id, counter, rule.PeriodTimeSpan, rule.UseSlidingExpiration);

                        return Task.FromResult(new RateLimitResult
                        {
                            Success = true,
                            Remaining = rule.Limit - 1,
                            Expiry = DateTime.UtcNow.Add(rule.PeriodTimeSpan)
                        });
                    }
                }
            }

            return Task.FromResult(counter.AddRequest(rule.Limit));
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
