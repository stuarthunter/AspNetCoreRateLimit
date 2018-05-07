// Lua scripting reference: https://www.redisgreen.net/blog/intro-to-lua-for-redis-programmers/

using System;
using System.Threading.Tasks;
using AspNetCoreRateLimit.Helpers;
using AspNetRateLimit.Common.Models;
using AspNetRateLimit.Common.Store;
using StackExchange.Redis;

namespace AspNetCoreRateLimit.Store
{
    public class RedisRateLimitCounterStore : IRateLimitCounterStore
    {
        private const string IncrementScript = @"
            local count = redis.call('INCR', @key)
            if (count == 1) then
                redis.call('EXPIRE', @key, @ttl)
                return '1:' .. @ttl
            end
            local ttl = redis.call('TTL', @key)
            return count .. ':' .. ttl
            ";

        private const string IncrementScriptSliding = @"
            local exists = redis.call('EXISTS', @key)
            if (exists == 0) then
                redis.call('ZADD', @key, @timestamp, @identifier)
                redis.call('EXPIRE', @key, @ttl)
                return '1:' .. @timestamp
            end
			redis.call('EXPIRE', @key, @ttl)
            redis.call('ZREMRANGEBYSCORE', @key, 0, @minTimestamp)
            local count = redis.call('ZCARD', @key)
            if (count < tonumber(@limit)) then
                redis.call('ZADD', @key, @timestamp, @identifier);
            end
            count = count + 1
            local minScore = redis.call('ZRANGE', @key, 0, 0, 'WITHSCORES')[2]
            return count .. ':' .. minScore
            ";

        private readonly RedisConnection _connection;

        public RedisRateLimitCounterStore(RedisConnection connection)
        {
            _connection = connection;
            _connection.RegisterScript("INCREMENT", IncrementScript);
            _connection.RegisterScript("INCREMENT_SLIDING", IncrementScriptSliding);
        }

        public async Task<RateLimitResult> AddRequestAsync(string id, RateLimitRule rule)
        {
            var key = $"RATELIMIT::{id}";
            int count;
            DateTime expiry;

            if (!rule.UseSlidingExpiration)
            {
                var result = await _connection.ExecuteScriptAsync("INCREMENT", new
                {
                    key = (RedisKey) key,
                    ttl = (int) rule.PeriodTimeSpan.TotalSeconds
                });
                
                var parts = ((string) result).Split(':');
                count = int.Parse(parts[0]);
                expiry = DateTime.UtcNow.AddSeconds(int.Parse(parts[1]));
            }
            else
            {
                var result = await _connection.ExecuteScriptAsync("INCREMENT_SLIDING", new
                {
                    key = (RedisKey) key,
                    timestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond,
                    identifier = Guid.NewGuid().ToString(),
                    ttl = (int) rule.PeriodTimeSpan.TotalSeconds,
                    minTimestamp = DateTime.UtcNow.Subtract(rule.PeriodTimeSpan).Ticks / TimeSpan.TicksPerMillisecond,
                    limit = rule.Limit
                });

                var parts = ((string) result).Split(':');
                count = int.Parse(parts[0]);
                expiry = new DateTime(long.Parse(parts[1]) * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc).Add(rule.PeriodTimeSpan);
            }

            return new RateLimitResult
            {
                Success = count <= rule.Limit,
                Remaining = count <= rule.Limit ? 0 : rule.Limit - count,
                Expiry = expiry
            };
        }
    }
}
