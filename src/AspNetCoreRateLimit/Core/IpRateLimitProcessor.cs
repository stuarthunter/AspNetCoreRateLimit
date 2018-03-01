using System;
using System.Collections.Generic;
using System.Linq;
using AspNetCoreRateLimit.Models;
using AspNetCoreRateLimit.Net;
using AspNetCoreRateLimit.Store;
using Microsoft.AspNetCore.Http;

namespace AspNetCoreRateLimit.Core
{
    public class IpRateLimitProcessor : RateLimitProcessor
    {
        private readonly IpRateLimitOptions _options;
        private readonly IIpPolicyStore _policyStore;
        private readonly IIpAddressParser _ipParser;
        private readonly List<IpAddressRange> _ipAddressRangeWhitelist;

        public IpRateLimitProcessor(IpRateLimitOptions options,
            IRateLimitCounterStore counterStore,
            IIpPolicyStore policyStore,
            IIpAddressParser ipParser)
            : base(options, counterStore)
        {
            _options = options;
            _policyStore = policyStore;
            _ipParser = ipParser;

            // parse IP whitelist
            if (_options.IpWhitelist != null)
            {
                _ipAddressRangeWhitelist = _options.IpWhitelist.Select(x => new IpAddressRange(x)).ToList();
            }
        }

        public override string ComputeCounterKey(ClientRequest clientRequest, RateLimitRule rule)
        {
            return $"{_options.RateLimitCounterPrefix}_{clientRequest.ClientIp}_{rule.Period}{(rule.UseSlidingExpiration ? "*" : "")}_{rule.Endpoint}";
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
                var clientIp = _ipParser.ParseIp(clientRequest.ClientIp);
                var policyRules = new RateLimitRules();
                policyRules.AddRange(policies.IpRules.Where(x => x.IpAddressRange.Contains(clientIp)).SelectMany(x => x.Rules));

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

        public override ClientRequest GetClientRequest(HttpContext httpContext)
        {
            var clientRequest = base.GetClientRequest(httpContext);

            var clientIp = _ipParser.GetClientIp(httpContext);
            if (clientIp != null)
            {
                clientRequest.ClientIp = clientIp.ToString();
            }

            return clientRequest;
        }

        public override bool IsWhitelisted(ClientRequest clientRequest)
        {
            if (base.IsWhitelisted(clientRequest))
            {
                return true;
            }

            if (_ipAddressRangeWhitelist != null)
            {
                var clientIp = _ipParser.ParseIp(clientRequest.ClientIp);
                if (_ipAddressRangeWhitelist.Any(x => x.Contains(clientIp)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
