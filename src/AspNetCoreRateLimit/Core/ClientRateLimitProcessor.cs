using System.Collections.Generic;
using System.Linq;
using AspNetCoreRateLimit.Models;

namespace AspNetCoreRateLimit.Core
{
    public class ClientRateLimitProcessor : RateLimitProcessor
    {
        private readonly ClientRateLimitOptions _options;
        private readonly IClientPolicyStore _policyStore;

        public ClientRateLimitProcessor(ClientRateLimitOptions options,
            IRateLimitCounterStore counterStore,
            IClientPolicyStore policyStore) 
            : base(options, counterStore)
        {
            _options = options;
            _policyStore = policyStore;
        }

        public override string ComputeCounterKey(ClientRequestIdentity requestIdentity, RateLimitRule rule)
        {
            return $"{_options.RateLimitCounterPrefix}_{requestIdentity.ClientId}_{rule.Period}_{rule.Endpoint}";
        }

        public override List<RateLimitRule> GetMatchingRules(ClientRequestIdentity identity)
        {
            var result = new List<RateLimitRule>();

            // get matching client rules
            var policy = _policyStore.Get($"{_options.ClientPolicyPrefix}_{identity.ClientId}");
            if (policy?.Rules != null && policy.Rules.Any())
            {
                if (_options.EnableEndpointRateLimiting)
                {
                    var rules = policy.Rules.GetEndpointRules(identity.HttpVerb, identity.Path);
                    result.AddRange(rules);
                }
                else
                {
                    var rules = policy.Rules.GetGlobalRules(identity.HttpVerb);
                    result.AddRange(rules);
                }

                // get the most restrictive limit for each period 
                result = result.GroupBy(x => x.Period)
                    .Select(x => x.OrderBy(y => y.Limit).First())
                    .ToList();
            }

            // add general rule if no specific client rule exists for period
            var generalRules = GetMatchingGeneralRules(identity);
            if (generalRules != null && generalRules.Any())
            {
                foreach (var limit in generalRules)
                {
                    if (!result.Exists(x => x.Period == limit.Period))
                    {
                        result.Add(limit);
                    }
                }
            }
            
            // order by period
            result = result.OrderBy(x => x.GetPeriodTimeSpan()).ToList();
            if (_options.StackBlockedRequests)
            {
                result.Reverse();
            }

            return result;
        }
    }
}
