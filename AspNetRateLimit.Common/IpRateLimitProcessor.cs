using System.Collections.Generic;
using System.Linq;
using System.Net;
using AspNetRateLimit.Common.Models;
using AspNetRateLimit.Common.Store;

namespace AspNetRateLimit.Common
{
    public class IpRateLimitProcessor : RateLimitProcessor
    {
        private readonly IpRateLimitOptions _options;
        private readonly IIpPolicyStore _policyStore;
        private readonly List<IpAddressRange> _ipAddressRangeWhitelist;

        public IpRateLimitProcessor(IpRateLimitOptions options,
            IRateLimitCounterStore counterStore,
            IIpPolicyStore policyStore)
            : base(options, counterStore)
        {
            _options = options;
            _policyStore = policyStore;

            // parse IP whitelist
            if (_options.IpWhitelist != null)
            {
                _ipAddressRangeWhitelist = _options.IpWhitelist.Select(x => new IpAddressRange(x)).ToList();
            }
        }

        public override string ComputeCounterKey(ClientRequest clientRequest, RateLimitRule rule)
        {
            return $"{_options.RateLimitCounterPrefix}_{clientRequest.ClientIpAddress}_{rule.Period}{(rule.UseSlidingExpiration ? "*" : "")}_{rule.Endpoint}";
        }

        public override List<RateLimitRule> GetMatchingRules(ClientRequest clientRequest)
        {
            var result = new List<RateLimitRule>();
            var rulePeriodComparer = new RateLimitRule.PeriodComparer();

            // get matching IP rules
            var policies = _policyStore.Get($"{_options.IpPolicyPrefix}");
            if (policies?.IpRules != null && policies.IpRules.Any())
            {
                // search for rules with IP intervals containing client IP
                var policyRules = new RateLimitRules();
                policyRules.AddRange(policies.IpRules.Where(x => x.IpAddressRange.Contains(clientRequest.ClientIpAddress)).SelectMany(x => x.Rules));

                if (policyRules.Any())
                {
                    if (_options.EnableEndpointRateLimiting)
                    {
                        var endpointRules = policyRules.GetEndpointRules(clientRequest.HttpVerb, clientRequest.Path);
                        result.AddRange(endpointRules);
                    }

                    // add client specific global rules where no rule exists for period
                    var globalRules = policyRules.GetGlobalRules(clientRequest.HttpVerb);
                    result.AddRange(globalRules.Except(result, rulePeriodComparer));
                }
            }

            // add general rule where no rule exists for period
            var generalRules = GetMatchingGeneralRules(clientRequest);
            if (generalRules != null)
            {
                result.AddRange(generalRules.Except(result, rulePeriodComparer));
            }

            // order by period
            result = result.OrderBy(x => x.PeriodTimeSpan).ToList();
            if (_options.StackBlockedRequests)
            {
                result.Reverse();
            }

            return result;
        }

        public override bool IsWhitelisted(ClientRequest clientRequest)
        {
            if (base.IsWhitelisted(clientRequest))
            {
                return true;
            }

            return _ipAddressRangeWhitelist != null 
                && !clientRequest.ClientIpAddress.Equals(IPAddress.None) 
                && _ipAddressRangeWhitelist.Any(x => x.Contains(clientRequest.ClientIpAddress));
        }
    }
}
