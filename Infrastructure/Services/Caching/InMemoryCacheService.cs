using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

/// <summary>
/// Process-local analytics cache. Uses the same generation-based invalidation and JSON storage as
/// <see cref="HybridCacheService"/>, but never talks to a shared store (single-instance deployments).
/// </summary>
public class InMemoryCacheService : GenerationalAnalyticsCacheService
{
    public InMemoryCacheService(IMemoryCache cache, ILogger<InMemoryCacheService> logger, TimeProvider? timeProvider = null)
        : base(cache, logger, timeProvider)
    {
    }

    public override bool IsRedisAvailable => false;

    public override bool IsRedisEnabled => false;

    public override void SetRedisEnabled(bool enabled)
    {
        // no-op: no Redis in the in-memory implementation
    }

    protected override string CacheModeName => "in-memory";
}
