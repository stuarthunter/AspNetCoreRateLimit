﻿using System;

namespace AspNetCoreRateLimit
{
    public class RateLimitResult
    {
        public bool Success { get; set; }

        public int Remaining { get; set; }

        public DateTime Expiry { get; set; }
    }
}
