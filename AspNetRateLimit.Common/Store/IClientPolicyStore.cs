﻿using AspNetRateLimit.Common.Models;

namespace AspNetRateLimit.Common.Store
{
    public interface IClientPolicyStore
    {
        bool Exists(string id);
        ClientRateLimitPolicy Get(string id);
        void Remove(string id);
        void Set(string id, ClientRateLimitPolicy policy);
    }
}