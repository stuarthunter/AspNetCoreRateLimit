﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AspNetCoreRateLimit.Extensions;
using AspNetRateLimit.Common.Models;
using AspNetRateLimit.Common;
using AspNetRateLimit.Common.Store;

namespace AspNetCoreRateLimit
{
    public class IpRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IpRateLimitOptions _options;
        private readonly ILogger<IpRateLimitMiddleware> _logger;
        private readonly IpRateLimitProcessor _processor;
        
        public IpRateLimitMiddleware(RequestDelegate next, 
            IOptions<IpRateLimitOptions> options,
            IRateLimitCounterStore counterStore,
            IIpPolicyStore policyStore,
            ILogger<IpRateLimitMiddleware> logger)
        {
            _next = next;
            _options = options.Value;
            _logger = logger;
            _processor = new IpRateLimitProcessor(_options, counterStore, policyStore);
        }

        public async Task Invoke(HttpContext httpContext)
        {
            // check if rate limiting is enabled
            if (_options == null)
            {
                await _next.Invoke(httpContext);
                return;
            }

            // get request details
            var clientRequest = httpContext.GetClientRequest(_options.ClientIdHeader, _options.RealIpHeader);

            // check white list
            if (_processor.IsWhitelisted(clientRequest))
            {
                await _next.Invoke(httpContext);
                return;
            }

            var rules = _processor.GetMatchingRules(clientRequest);
            RateLimitResult result = null;
            foreach (var rule in rules)
            {
                // if limit is zero or less, block the request.
                if (rule.Limit <= 0)
                {
                    // log blocked request
                    LogBlockedRequest(httpContext, clientRequest, rule);

                    // return quote exceeded
                    await ReturnQuotaExceededResponse(httpContext, rule);
                    return;
                }

                // process request
                result = await _processor.ProcessRequestAsync(clientRequest, rule);

                // check if limit is exceeded
                if (!result.Success)
                {
                    //compute retry after value
                    var retryAfter = Convert.ToInt32((result.Expiry - DateTime.UtcNow).TotalSeconds)
                        .ToString(CultureInfo.InvariantCulture);

                    // log blocked request
                    LogBlockedRequest(httpContext, clientRequest, rule);

                    // return quote exceeded
                    await ReturnQuotaExceededResponse(httpContext, rule, retryAfter);
                    return;
                }
            }

            // set X-Rate-Limit headers
            if (result != null && !_options.DisableRateLimitHeaders)
            {
                var rule = rules.Last();
                var headers = new RateLimitHeaders
                {
                    Reset = result.Expiry.ToString("o", DateTimeFormatInfo.InvariantInfo),
                    Limit = rule.Period,
                    Remaining = result.Remaining.ToString()
                };

                httpContext.Response.OnStarting(state => {
                    try
                    {
                        var context = (HttpContext)state;
                        context.Response.Headers["X-Rate-Limit-Limit"] = headers.Limit;
                        context.Response.Headers["X-Rate-Limit-Remaining"] = headers.Remaining;
                        context.Response.Headers["X-Rate-Limit-Reset"] = headers.Reset;
                    }
                    catch
                    {
                        // ignore exception adding headers
                    }
                    return Task.FromResult(0);
                }, httpContext);
            }

            await _next.Invoke(httpContext);
        }

        private Task ReturnQuotaExceededResponse(HttpContext httpContext, RateLimitRule rule, string retryAfter = null)
        {
            var message = string.IsNullOrEmpty(_options.QuotaExceededMessage) ? $"API calls quota exceeded! maximum admitted {rule.Limit} per {rule.Period}." : _options.QuotaExceededMessage;

            if (!_options.DisableRateLimitHeaders && !string.IsNullOrEmpty(retryAfter))
            {
                httpContext.Response.Headers["Retry-After"] = retryAfter;
            }

            httpContext.Response.StatusCode = _options.HttpStatusCode;
            return httpContext.Response.WriteAsync(message);
        }

        private void LogBlockedRequest(HttpContext httpContext, ClientRequest identity, RateLimitRule rule)
        {
            _logger.LogInformation($"Request {identity.HttpVerb}:{identity.Path} from IP {identity.ClientIpAddress} has been blocked, quota {rule.Limit}/{rule.Period} exceeded. Blocked by rule {rule.Endpoint}. TraceIdentifier {httpContext.TraceIdentifier}.");
        }
    }
}
