using System;

namespace AspNetCoreRateLimit.Models
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

        /// <summary>
        /// Maximum number of requests that a client can make in a defined period
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Determines whether to use sliding expiration window for request counter
        /// </summary>
        public bool UseSlidingExpiration { get; set; }

        public TimeSpan GetPeriodTimeSpan()
        {
            if (string.IsNullOrEmpty(Period))
            {
                throw new FormatException("Period is empty.");
            }

            var l = Period.Length - 1;
            if (l < 1 || !double.TryParse(Period.Substring(0, l), out var value) || value <= 0)
            {
                throw new FormatException($"Period '{Period}' can't be converted to TimeSpan.");
            }

            var type = Period.Substring(l, 1);
            switch (type.ToLower())
            {
                case "d": return TimeSpan.FromDays(value);
                case "h": return TimeSpan.FromHours(value);
                case "m": return TimeSpan.FromMinutes(value);
                case "s": return TimeSpan.FromSeconds(value);
                default: throw new FormatException($"Perios '{Period}' can't be converted to TimeSpan.");
            }
        }
    }
}
