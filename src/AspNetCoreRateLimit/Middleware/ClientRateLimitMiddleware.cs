using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace AspNetCoreRateLimit
{
    public class ClientRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ClientRateLimitMiddleware> _logger;
        private readonly ClientRateLimitProcessor _processor;
        private readonly ClientRateLimitOptions _options;

        public ClientRateLimitMiddleware(RequestDelegate next,
            IOptions<ClientRateLimitOptions> options,
            IRateLimitCounterStore counterStore,
            IClientPolicyStore policyStore,
            ILogger<ClientRateLimitMiddleware> logger
            )
        {
            _next = next;
            _options = options.Value;
            _logger = logger;

            _processor = new ClientRateLimitProcessor(_options, counterStore, policyStore);
        }

        public async Task Invoke(HttpContext httpContext)
        {
            // check if rate limiting is enabled
            if (_options == null)
            {
                await _next.Invoke(httpContext);
                return;
            }

            // compute identity from request
            var identity = SetIdentity(httpContext);

            // check white list
            if (_processor.IsWhitelisted(identity))
            {
                await _next.Invoke(httpContext);
                return;
            }

            var rules = _processor.GetMatchingRules(identity);
            RateLimitResult result = null;

            foreach (var rule in rules)
            {
                // if limit is zero or less, block the request.
                if (rule.Limit <= 0)
                {
                    // log blocked request
                    LogBlockedRequest(httpContext, identity, rule);

                    // break execution
                    await ReturnQuotaExceededResponse(httpContext, rule);
                    return;
                }

                // process request
                result = _processor.ProcessRequest(identity, rule);

                // check if limit is exceeded
                if (!result.Success)
                {
                    //compute retry after value
                    var retryAfter = Convert.ToInt32((result.Expiry - DateTime.UtcNow).TotalSeconds).ToString(CultureInfo.InvariantCulture);

                    // log blocked request
                    LogBlockedRequest(httpContext, identity, rule);

                    // break execution
                    await ReturnQuotaExceededResponse(httpContext, rule, retryAfter);
                    return;
                }
            }

            //set X-Rate-Limit headers for the longest period
            if(result != null && !_options.DisableRateLimitHeaders)
            {
                var rule = rules.Last();
                var headers = _processor.GetRateLimitHeaders(rule, result);
                headers.Context = httpContext;

                httpContext.Response.OnStarting(SetRateLimitHeaders, headers);
            }

            await _next.Invoke(httpContext);
        }

        public virtual ClientRequestIdentity SetIdentity(HttpContext httpContext)
        {
            var clientId = "anon";
            if (httpContext.Request.Headers.Keys.Contains(_options.ClientIdHeader,StringComparer.CurrentCultureIgnoreCase))
            {
                clientId = httpContext.Request.Headers[_options.ClientIdHeader].First();
            }

            return new ClientRequestIdentity
            {
                Path = httpContext.Request.Path.ToString().ToLowerInvariant(),
                HttpVerb = httpContext.Request.Method.ToLowerInvariant(),
                ClientId = clientId
            };
        }

        public virtual Task ReturnQuotaExceededResponse(HttpContext httpContext, RateLimitRule rule)
        {
            return ReturnQuotaExceededResponse(httpContext, rule, null);
        }

        public virtual Task ReturnQuotaExceededResponse(HttpContext httpContext, RateLimitRule rule, string retryAfter)
        {
            var message = string.IsNullOrEmpty(_options.QuotaExceededMessage) ? $"API calls quota exceeded! Maximum admitted {rule.Limit} per {rule.Period}." : _options.QuotaExceededMessage;

            if (!_options.DisableRateLimitHeaders && !string.IsNullOrEmpty(retryAfter))
            {
                httpContext.Response.Headers["Retry-After"] = retryAfter;
            }

            httpContext.Response.StatusCode = _options.HttpStatusCode;
            return httpContext.Response.WriteAsync(message);
        }

        public virtual void LogBlockedRequest(HttpContext httpContext, ClientRequestIdentity identity, RateLimitRule rule)
        {
            _logger.LogInformation($"Request {identity.HttpVerb}:{identity.Path} from ClientId {identity.ClientId} has been blocked, quota {rule.Limit}/{rule.Period} exceeded. Blocked by rule {rule.Endpoint}. TraceIdentifier {httpContext.TraceIdentifier}.");
        }

        private Task SetRateLimitHeaders(object rateLimitHeaders)
        {
            var headers = (RateLimitHeaders)rateLimitHeaders;

            headers.Context.Response.Headers["X-Rate-Limit-Limit"] = headers.Limit;
            headers.Context.Response.Headers["X-Rate-Limit-Remaining"] = headers.Remaining;
            headers.Context.Response.Headers["X-Rate-Limit-Reset"] = headers.Reset;

            return Task.CompletedTask;
        }
    }
}
