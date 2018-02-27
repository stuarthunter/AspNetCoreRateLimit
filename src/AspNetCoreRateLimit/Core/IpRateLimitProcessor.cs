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
            return $"{_options.RateLimitCounterPrefix}_{clientRequest.ClientIp}_{rule.Period}_{rule.Endpoint}";
        }

        public override List<RateLimitRule> GetMatchingRules(ClientRequest clientRequest)
        {
            var result = new List<RateLimitRule>();

            // get matching IP rules
            var policies = _policyStore.Get($"{_options.IpPolicyPrefix}");
            if (policies?.IpRules != null && policies.IpRules.Any())
            {
                // search for rules with IP intervals containing client IP
                var clientIp = _ipParser.ParseIp(clientRequest.ClientIp);
                var matchPolicies = policies.IpRules.Where(x => x.IpAddressRange.Contains(clientIp));
                var policyRules = new RateLimitRules();
                foreach (var policy in matchPolicies)
                {
                    policyRules.AddRange(policy.Rules);
                }

                if (_options.EnableEndpointRateLimiting)
                {
                    var rules = policyRules.GetEndpointRules(clientRequest.HttpVerb, clientRequest.Path);
                    result.AddRange(rules);
                }
                else
                {
                    var rules = policyRules.GetGlobalRules(clientRequest.HttpVerb);
                    result.AddRange(rules);
                }

                // get the most restrictive limit for each period 
                result = result.GroupBy(x => x.Period)
                    .Select(x => x.OrderBy(y => y.Limit).First())
                    .ToList();
            }

            // add general rule if no specific client rule exists for period
            var generalRules = GetMatchingGeneralRules(clientRequest);
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
