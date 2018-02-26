using System.Collections.Generic;

namespace AspNetCoreRateLimit.Models
{
    public class ClientRateLimitPolicies
    {
        public List<ClientRateLimitPolicy> ClientRules { get; set; }
    }
}
