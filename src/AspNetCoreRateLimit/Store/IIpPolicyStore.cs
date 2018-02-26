using AspNetCoreRateLimit.Models;

namespace AspNetCoreRateLimit.Store
{
    public interface IIpPolicyStore
    {
        bool Exists(string id);
        IpRateLimitPolicies Get(string id);
        void Remove(string id);
        void Set(string id, IpRateLimitPolicies policy);
    }
}