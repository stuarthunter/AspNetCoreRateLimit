using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace AspNetCoreRateLimit.Models
{
    /// <summary>
    /// Stores the initial access time and the numbers of calls made from that point
    /// </summary>
    public class RateLimitCounter
    {
        private readonly bool _useSlidingExpiration;
        private readonly TimeSpan _period;
        private readonly DateTime? _timestamp;
        private int _requestCount;
        private readonly ConcurrentQueue<long> _requests;

        private readonly object _lock = new object();

        public RateLimitCounter(bool useSlidingExpiration, TimeSpan period)
        {
            _useSlidingExpiration = useSlidingExpiration;
            _period = period;

            if (_useSlidingExpiration)
            {
                _requests = new ConcurrentQueue<long>();
                _requests.Enqueue(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
            }
            else
            {
                _timestamp = DateTime.UtcNow;
                _requestCount = 1;
            }
        }

        public bool IsExpired
        {
            get
            {
                if (_useSlidingExpiration)
                {
                    return false;
                }

                Debug.Assert(_timestamp != null, "Timestamp is not null");
                return _timestamp.Value.Add(_period) < DateTime.UtcNow;
            }
        }
        
        [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
        public RateLimitResult AddRequest(int limit)
        {
            int count;
            DateTime expiry;

            if (_useSlidingExpiration)
            {
                // remove expired requests
                var minTimestamp = DateTime.UtcNow.Subtract(_period).Ticks / TimeSpan.TicksPerMillisecond;
                
                if (_requests.TryPeek(out var timestamp) && timestamp <= minTimestamp)
                {
                    lock (_lock)
                    {
                        while (_requests.TryPeek(out timestamp) && timestamp <= minTimestamp)
                        {
                            _requests.TryDequeue(out timestamp);
                        }
                    }
                }

                count = _requests.Count;
                // calculate expiry from timestamp of first request inside period
                expiry = timestamp > 0 
                    ? new DateTime(timestamp * TimeSpan.TicksPerMillisecond).ToUniversalTime().Add(_period) 
                    : DateTime.UtcNow.Add(_period);
            }
            else
            {
                count = _requestCount;
                Debug.Assert(_timestamp != null, "Timestamp is not null");
                expiry = _timestamp.Value.Add(_period);
            }

            // check limit
            if (count >= limit)
            {
                return new RateLimitResult
                {
                    Success = false,
                    Remaining = 0,
                    Expiry = expiry
                };
            }

            // add request
            if (_useSlidingExpiration)
            {
                _requests.Enqueue(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
            }
            else
            {
                Interlocked.Increment(ref _requestCount);
            }

            return new RateLimitResult
            {
                Success = true,
                Remaining = limit - count - 1,
                Expiry = expiry
            };
        }
    }
}
