// Lua scripting reference: https://www.redisgreen.net/blog/intro-to-lua-for-redis-programmers/

using System;
using System.Linq;
using System.Threading.Tasks;
using AspNetCoreRateLimit.Models;
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

        private static readonly Lazy<LoadedLuaScript> LoadedIncrementScript = new Lazy<LoadedLuaScript>(() => LoadScript(IncrementScript));
        private static readonly Lazy<LoadedLuaScript> LoadedIncrementScriptSliding = new Lazy<LoadedLuaScript>(() => LoadScript(IncrementScriptSliding));

        // todo: inject singleton Redis ConnectionMultiplexer via DI instead of using lazy static reference
        private static Lazy<ConnectionMultiplexer> _redis;

        public RedisRateLimitCounterStore(string connectionString)
        {
            if (_redis == null)
            {
                _redis = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(connectionString));
            }
        }

        public async Task<RateLimitResult> AddRequestAsync(string id, RateLimitRule rule)
        {
            var db = _redis.Value.GetDatabase();
            var key = $"RATELIMIT::{id}";
            int count;
            DateTime expiry;

            if (!rule.UseSlidingExpiration)
            {
                var result = await LoadedIncrementScript.Value.EvaluateAsync(db, new
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
                var result = await LoadedIncrementScriptSliding.Value.EvaluateAsync(db, new
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

        private static LoadedLuaScript LoadScript(string script)
        {
            var preparedScript = LuaScript.Prepare(script);
            LoadedLuaScript loadedScript = null;

            // load script on all servers
            // note: only need to store single instance of loaded script as it is a wrapper around the original prepared script
            foreach (var server in _redis.Value.GetEndPoints().Select(x => _redis.Value.GetServer(x)))
            {
                loadedScript = preparedScript.Load(server);
            }

            return loadedScript;
        }
    }
}
