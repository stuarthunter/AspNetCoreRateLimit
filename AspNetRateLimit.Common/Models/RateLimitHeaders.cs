
namespace AspNetRateLimit.Common.Models
{
    public class RateLimitHeaders
    {
        public string Limit { get; set; }

        public string Remaining { get; set; }

        public string Reset { get; set; }
    }
}
