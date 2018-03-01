using System;
using AspNetCoreRateLimit.Models;
using StackExchange.Redis;

namespace AspNetCoreRateLimit.Store
{
    public class RedisRateLimitCounterStore : IRateLimitCounterStore
    {
        private readonly ConnectionMultiplexer _connection;

        private const string IncrementScript = @"
            local count = redis.call('INCR', KEYS[1])
            if (count == 1) then
                redis.call('EXPIRE', KEYS[1], ARGV[1])
                return '1:' .. ARGV[1]
            end
            local ttl = redis.call('TTL', KEYS[1])
            return count .. ':' .. ttl
            ";

        private const string IncrementScriptSliding = @"
            local exists = redis.call('EXISTS', KEYS[1])
            if (exists == 0) then
                redis.call('ZADD', KEYS[1], ARGV[1], ARGV[2])
                redis.call('EXPIRE', KEYS[1], ARGV[3])
                return '1:' .. ARGV[1]
            end
			redis.call('EXPIRE', KEYS[1], ARGV[3])
            redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, ARGV[4])
            local count = redis.call('ZCARD', KEYS[1])
            if (count < tonumber(ARGV[5])) then
                redis.call('ZADD', KEYS[1], ARGV[1], ARGV[2]);
            end
            count = count + 1
            local minScore = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')[2]
            return count .. ':' .. minScore
            ";

        public RedisRateLimitCounterStore(string connectionString)
        {
            _connection = ConnectionMultiplexer.Connect(connectionString);
        }

        public RateLimitResult AddRequest(string id, RateLimitRule rule)
        {
            var db = _connection.GetDatabase();
            var key = $"RATELIMIT:{id}";
            int count;
            DateTime expiry;

            if (!rule.UseSlidingExpiration)
            {
                // todo: convert to async
                // todo: preload script on server - see https://www.redisgreen.net/blog/intro-to-lua-for-redis-programmers/
                var ttl = (int) rule.PeriodTimeSpan.TotalSeconds; // ARGV[1]
                var result = (string) db.ScriptEvaluate(IncrementScript,
                    new RedisKey[] { key }, new RedisValue[] { ttl });

                var parts = result.Split(':');
                count = int.Parse(parts[0]);
                expiry = DateTime.UtcNow.AddSeconds(int.Parse(parts[1]));
            }
            else
            {
                var timestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond; // ARGV[1]
                var identifier = Guid.NewGuid().ToString(); // ARGV[2]
                var ttl = (int) rule.PeriodTimeSpan.TotalSeconds; // ARGV[3]
                var minTimestamp = DateTime.UtcNow.Subtract(rule.PeriodTimeSpan).Ticks / TimeSpan.TicksPerMillisecond; // ARGV[4]
                var limit = rule.Limit;  // ARGV[5]
                var result = (string)db.ScriptEvaluate(IncrementScriptSliding,
                    new RedisKey[] { key }, new RedisValue[] { timestamp, identifier, ttl, minTimestamp, limit });

                var parts = result.Split(':');
                count = int.Parse(parts[0]);
                expiry = new DateTime(long.Parse(parts[1]) * TimeSpan.TicksPerMillisecond).ToUniversalTime().Add(rule.PeriodTimeSpan);
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
