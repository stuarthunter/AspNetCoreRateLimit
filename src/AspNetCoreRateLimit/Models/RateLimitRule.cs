using System;

namespace AspNetCoreRateLimit
{
    public class RateLimitRule
    {
        /// <summary>
        /// HTTP verb and path 
        /// </summary>
        /// <example>
        /// get:/api/values
        /// *:/api/values
        /// *
        /// </example>
        public string Endpoint { get; set; }

        /// <summary>
        /// Rate limit period as in 1s, 1m, 1h
        /// </summary>
        public string Period { get; set; }

        public TimeSpan? PeriodTimespan { get; set; }

        /// <summary>
        /// Maximum number of requests that a client can make in a defined period
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Determines whether to use sliding expiration.  Not recommended for rules with high limits as individual request timestamps are stored.
        /// </summary>
        public bool UseSlidingExpiration { get; set; }
    }
}
