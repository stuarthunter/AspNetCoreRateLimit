using System.Collections.Generic;

namespace AspNetRateLimit.Common.Models
{
    public class ClientRateLimitPolicies
    {
        public List<ClientRateLimitPolicy> ClientRules { get; set; }
    }
}
