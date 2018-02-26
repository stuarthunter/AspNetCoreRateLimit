using System.Collections.Generic;
using AspNetCoreRateLimit.Models;

namespace AspNetCoreRateLimit
{
    public class ClientRateLimitPolicies
    {
        public List<ClientRateLimitPolicy> ClientRules { get; set; }
    }
}
