using System.Collections.Generic;
using System.Linq;
using AspNetCoreRateLimit.Models;
using AspNetCoreRateLimit.Store;

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

        public override string ComputeCounterKey(ClientRequest clientRequest, RateLimitRule rule)
        {
            return $"{_options.RateLimitCounterPrefix}_{clientRequest.ClientId}_{rule.Period}_{rule.Endpoint}";
        }

        public override List<RateLimitRule> GetMatchingRules(ClientRequest clientRequest)
        {
            var result = new List<RateLimitRule>();
            var rulePeriodComparer = new RateLimitRule.PeriodComparer();

            // get matching client rules
            var policy = _policyStore.Get($"{_options.ClientPolicyPrefix}_{clientRequest.ClientId}");
            if (policy?.Rules != null && policy.Rules.Any())
            {
                if (_options.EnableEndpointRateLimiting)
                {
                    var endpointRules = policy.Rules.GetEndpointRules(clientRequest.HttpVerb, clientRequest.Path);
                    result.AddRange(endpointRules);
                }

                // add client specific global rules where no rule exists for period
                var globalRules = policy.Rules.GetGlobalRules(clientRequest.HttpVerb);
                result.AddRange(globalRules.Except(result, rulePeriodComparer));
            }

            // add general rules where no rule exists for period
            var generalRules = GetMatchingGeneralRules(clientRequest);
            if (generalRules != null)
            {
                result.AddRange(generalRules.Except(result, rulePeriodComparer));
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
