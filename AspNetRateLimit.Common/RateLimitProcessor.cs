﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AspNetRateLimit.Common.Models;
using AspNetRateLimit.Common.Store;

namespace AspNetRateLimit.Common
{
    public abstract class RateLimitProcessor
    {
        private readonly RateLimitOptions _options;
        private readonly IRateLimitCounterStore _counterStore;

        protected RateLimitProcessor(RateLimitOptions options, IRateLimitCounterStore counterStore)
        {
            _options = options;
            _counterStore = counterStore;
        }

        public abstract string ComputeCounterKey(ClientRequest clientRequest, RateLimitRule rule);

        public abstract List<RateLimitRule> GetMatchingRules(ClientRequest clientRequest);

        public async Task<RateLimitResult> ProcessRequestAsync(ClientRequest requestIdentity, RateLimitRule rule)
        {
            var key = ComputeCounterKey(requestIdentity, rule);
            //var keyHash = ComputeKeyHash(key);
            return await _counterStore.AddRequestAsync(key, rule);
        }

        public virtual bool IsWhitelisted(ClientRequest clientRequest)
        {
            if (_options.ClientWhitelist != null && _options.ClientWhitelist.Contains(clientRequest.ClientId))
            {
                return true;
            }

            if (_options.EndpointWhitelist != null && _options.EndpointWhitelist.Any())
            {
                if (_options.EndpointWhitelist.Any(x => $"{clientRequest.HttpVerb}:{clientRequest.Path}".StartsWith(x, StringComparison.OrdinalIgnoreCase))
                    || _options.EndpointWhitelist.Any(x => $"*:{clientRequest.Path}".StartsWith(x, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        public List<RateLimitRule> GetMatchingGeneralRules(ClientRequest clientRequest)
        {
            if (_options.GeneralRules == null || !_options.GeneralRules.Any())
            {
                return null;
            }

            var result = new List<RateLimitRule>();
            var rulePeriodComparer = new RateLimitRule.PeriodComparer();

            if (_options.EnableEndpointRateLimiting)
            {
                var endpointRules = _options.GeneralRules.GetEndpointRules(clientRequest.HttpVerb, clientRequest.Path);
                result.AddRange(endpointRules);
            }

            var globalRules = _options.GeneralRules.GetGlobalRules(clientRequest.HttpVerb);
            result.AddRange(globalRules.Except(result, rulePeriodComparer));

            return result;
        }

        private static string ComputeKeyHash(string key)
        {
            using (var algorithm = System.Security.Cryptography.SHA256.Create())
            {
                var data = algorithm.ComputeHash(Encoding.UTF8.GetBytes(key));
                var hash = new StringBuilder();
                foreach (var x in data)
                {
                    hash.Append(x.ToString("x2"));
                }
                return hash.ToString();
            }
        }
    }
}
