using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

/// <summary>
/// Hybrid analytics cache: in-memory L1 + Redis L2.
///
/// Multi-instance correctness comes from generation tokens stored in Redis (see
/// <see cref="GenerationalAnalyticsCacheService"/>): an invalidation on instance B writes a new token,
/// instance A reads the token on its next lookup and can no longer reach its old L1/L2 entries.
///
/// Redis outage: as soon as a Redis call fails, every local token is rotated (old L1 entries become
/// unreachable because another instance may have invalidated them without us seeing it) and the cache
/// continues process-local. When Redis answers again, every root is bumped in Redis before it is trusted,
/// so values written before or during the outage are never resurrected. Disabling Redis by toggle is
/// treated the same way.
/// </summary>
public class HybridCacheService : GenerationalAnalyticsCacheService
{
    private readonly IDistributedCache? _distributedCache;
    private readonly TimeSpan _redisCheckInterval = TimeSpan.FromMinutes(1);
    private readonly SemaphoreSlim _recoveryLock = new(1, 1);
    private volatile bool _redisAvailable;
    private volatile bool _redisUserEnabled = true;
    private volatile bool _needsRecoveryBump;
    private long _lastRedisCheckTicks = DateTimeOffset.MinValue.UtcTicks;

    public HybridCacheService(
        IMemoryCache memoryCache,
        ILogger<HybridCacheService> logger,
        IDistributedCache? distributedCache = null,
        TimeProvider? timeProvider = null)
        : base(memoryCache, logger, timeProvider)
    {
        _distributedCache = distributedCache;
        _redisAvailable = distributedCache != null;
    }

    public override bool IsRedisAvailable => _distributedCache != null && _redisAvailable;

    public override bool IsRedisEnabled => _redisUserEnabled;

    public override void SetRedisEnabled(bool enabled)
    {
        if (_redisUserEnabled == enabled)
        {
            return;
        }

        // Invalidations made while Redis was off were local only: re-enabling must bump Redis first,
        // disabling must forget L1 entries that were keyed by Redis tokens.
        RotateAllLocalTokens();
        _needsRecoveryBump = true;
        _redisUserEnabled = enabled;
        Logger.LogInformation("Redis cache {State} by user toggle", enabled ? "ENABLED" : "DISABLED");
    }

    protected override string CacheModeName => UseDistributed ? "redis" : "in-memory";

    protected override bool UseDistributed => _distributedCache != null && _redisUserEnabled && _redisAvailable && !_needsRecoveryBump;

    protected override async Task EnsureDistributedStateAsync(CancellationToken ct)
    {
        if (_distributedCache == null || !_redisUserEnabled)
        {
            return;
        }

        if (_redisAvailable && !_needsRecoveryBump)
        {
            return;
        }

        var now = Time.GetUtcNow().UtcTicks;
        var last = Interlocked.Read(ref _lastRedisCheckTicks);
        if (_redisAvailable == false && now - last < _redisCheckInterval.Ticks)
        {
            return;
        }

        if (!await _recoveryLock.WaitAsync(0, ct))
        {
            return;
        }

        try
        {
            Interlocked.Exchange(ref _lastRedisCheckTicks, now);
            await _distributedCache.GetStringAsync("__health_check__", ct);
            _redisAvailable = true;

            if (_needsRecoveryBump)
            {
                if (!await BumpAllDistributedRootsAsync(ct))
                {
                    return;
                }

                _needsRecoveryBump = false;
                Logger.LogWarning("Redis analytics cache recovered; all generations bumped before trusting Redis again");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnDistributedFailure(ex, "health-check", "__health_check__");
        }
        finally
        {
            _recoveryLock.Release();
        }
    }

    protected override async Task<string?> ReadDistributedAsync(string key, CancellationToken ct)
        => await _distributedCache!.GetStringAsync(key, ct);

    protected override Task WriteDistributedAsync(string key, string value, TimeSpan ttl, CancellationToken ct)
        => _distributedCache!.SetStringAsync(key, value, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);

    protected override Task RemoveDistributedAsync(string key, CancellationToken ct)
        => _distributedCache!.RemoveAsync(key, ct);

    protected override void OnDistributedFailure(Exception ex, string operation, string key)
    {
        Logger.LogWarning(ex, "Redis {Operation} failed for {Key}; analytics cache continues process-local until Redis recovers", operation, key);
        if (_redisAvailable || !_needsRecoveryBump)
        {
            RotateAllLocalTokens();
        }

        _redisAvailable = false;
        _needsRecoveryBump = true;
        Interlocked.Exchange(ref _lastRedisCheckTicks, Time.GetUtcNow().UtcTicks);
    }
}
