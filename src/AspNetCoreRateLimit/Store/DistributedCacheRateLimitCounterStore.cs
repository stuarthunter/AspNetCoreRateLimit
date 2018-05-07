using System;
using System.Threading.Tasks;
using AspNetRateLimit.Common.Models;
using AspNetRateLimit.Common.Store;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace AspNetCoreRateLimit.Store
{
    public class DistributedCacheRateLimitCounterStore : IRateLimitCounterStore
    {
        private readonly IDistributedCache _memoryCache;

        public DistributedCacheRateLimitCounterStore(IDistributedCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public async Task<RateLimitResult> AddRequestAsync(string id, RateLimitRule rule)
        {
            // TODO: REQUIRES REVIEW 
            // RateLimitCounter should not be serialised to distributed cache.
            // Directly store underlying data to facilitate atomic distributed updates.
            // REDIS example: http://tech.domain.com.au/2017/11/protect-your-api-resources-with-rate-limiting/

            var counter = await GetAsync(id);
            if (counter == null)
            {
                counter = new RateLimitCounter(rule.UseSlidingExpiration, rule.PeriodTimeSpan);
                await SetAsync(id, counter, rule.PeriodTimeSpan, rule.UseSlidingExpiration);

                return new RateLimitResult
                {
                    Success = true,
                    Remaining = rule.Limit - 1,
                    Expiry = DateTime.UtcNow.Add(rule.PeriodTimeSpan)
                };
            }

            return counter.AddRequest(rule.Limit);
        }

        private async Task<RateLimitCounter> GetAsync(string id)
        {
            var stored = await _memoryCache.GetStringAsync(id);
            return !string.IsNullOrEmpty(stored) 
                ? JsonConvert.DeserializeObject<RateLimitCounter>(stored) 
                : null;
        }

        private async Task SetAsync(string id, RateLimitCounter counter, TimeSpan expirationPeriod, bool slidingExpiration)
        {
            var options = new DistributedCacheEntryOptions();
            if (slidingExpiration)
            {
                options.SetSlidingExpiration(expirationPeriod);
            }
            else
            {
                options.SetAbsoluteExpiration(expirationPeriod);
            }
            await _memoryCache.SetStringAsync(id, JsonConvert.SerializeObject(counter), options);
        }
    }
}
