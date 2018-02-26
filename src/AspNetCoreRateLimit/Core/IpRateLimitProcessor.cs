using System.Collections.Generic;
using System.Linq;
using AspNetCoreRateLimit.Models;

namespace AspNetCoreRateLimit.Core
{
    public class IpRateLimitProcessor : RateLimitProcessor
    {
        private readonly IpRateLimitOptions _options;
        private readonly IIpPolicyStore _policyStore;
        private readonly IIpAddressParser _ipParser;

        public IpRateLimitProcessor(IpRateLimitOptions options,
            IRateLimitCounterStore counterStore,
            IIpPolicyStore policyStore,
            IIpAddressParser ipParser)
            : base(options, counterStore)
        {
            _options = options;
            _policyStore = policyStore;
            _ipParser = ipParser;
        }

        public override string ComputeCounterKey(ClientRequestIdentity requestIdentity, RateLimitRule rule)
        {
            return $"{_options.RateLimitCounterPrefix}_{requestIdentity.ClientIp}_{rule.Period}_{rule.Endpoint}";
        }

        public override List<RateLimitRule> GetMatchingRules(ClientRequestIdentity identity)
        {
            var result = new List<RateLimitRule>();

            // get matching IP rules
            var policies = _policyStore.Get($"{_options.IpPolicyPrefix}");
            if (policies?.IpRules != null && policies.IpRules.Any())
            {
                // search for rules with IP intervals containing client IP
                var matchPolicies = policies.IpRules.Where(x => _ipParser.ContainsIp(x.Ip, identity.ClientIp));
                var policyRules = new RateLimitRules();
                foreach (var policy in matchPolicies)
                {
                    policyRules.AddRange(policy.Rules);
                }

                if (_options.EnableEndpointRateLimiting)
                {
                    var rules = policyRules.GetEndpointRules(identity.HttpVerb, identity.Path);
                    result.AddRange(rules);
                }
                else
                {
                    var rules = policyRules.GetGlobalRules(identity.HttpVerb);
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

        public override bool IsWhitelisted(ClientRequestIdentity requestIdentity)
        {
            if (_options.IpWhitelist != null && _ipParser.ContainsIp(_options.IpWhitelist, requestIdentity.ClientIp))
            {
                return true;
            }

            return base.IsWhitelisted(requestIdentity);
        }
    }
}
