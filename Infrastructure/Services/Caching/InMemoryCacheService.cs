using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

/// <summary>
/// In-Memory implementacija cache servisa.
/// BESPLATNO - koristi samo RAM servera.
/// Idealno za single-instance deployment.
/// </summary>
public class InMemoryCacheService : IAnalyticsCacheService
{
    private sealed class CacheKeyLock
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int RefCount;
    }

    private readonly IMemoryCache _cache;
    private readonly ILogger<InMemoryCacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<string, CacheKeyLock> _keyLocks = new();

    public InMemoryCacheService(IMemoryCache cache, ILogger<InMemoryCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public bool IsRedisAvailable => false;
    public bool IsRedisEnabled => false;
    public void SetRedisEnabled(bool enabled) { /* no-op: no Redis in InMemory implementation */ }

    public CacheFootprintSnapshot GetFootprintSnapshot()
    {
        return new CacheFootprintSnapshot(
            CacheMode: "in-memory",
            RedisEnabled: false,
            RedisAvailable: false,
            TrackedKeyCount: _keys.Count);
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            if (_cache.TryGetValue(key, out T? value))
            {
                _logger.LogDebug("Cache HIT: {Key}", key);
                return Task.FromResult(value);
            }
            
            _logger.LogDebug("Cache MISS: {Key}", key);
            return Task.FromResult<T?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET error for key: {Key}", key);
            return Task.FromResult<T?>(null);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        try
        {
            var options = new MemoryCacheEntryOptions();
            
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration.Value;
            }
            else
            {
                options.AbsoluteExpirationRelativeToNow = CacheExpiration.Medium;
            }
            
            // Sliding expiration - produžava cache ako se koristi
            options.SlidingExpiration = TimeSpan.FromMinutes(2);
            
            // Prioritet - analytics podaci imaju normalan prioritet
            options.Priority = CacheItemPriority.Normal;
            
            // Callback kada se ukloni iz cache-a
            options.RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
            {
                _keys.TryRemove(evictedKey.ToString()!, out _);
                _logger.LogDebug("Cache EVICTED: {Key}, Reason: {Reason}", evictedKey, reason);
            });

            _cache.Set(key, value, options);
            _keys.TryAdd(key, 0);
            
            _logger.LogDebug("Cache SET: {Key}, Expiration: {Expiration}", key, expiration ?? CacheExpiration.Medium);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET error for key: {Key}", key);
        }
        
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
            _logger.LogDebug("Cache REMOVE: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE error for key: {Key}", key);
        }
        
        return Task.CompletedTask;
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var keysToRemove = _keys.Keys.Where(k => k.StartsWith(prefix)).ToList();
            
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
                _keys.TryRemove(key, out _);
            }
            
            _logger.LogInformation("Cache INVALIDATE by prefix: {Prefix}, Removed: {Count} keys", prefix, keysToRemove.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE by prefix error: {Prefix}", prefix);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        // Prvo pokušaj dohvatiti iz cache-a
        var cached = await GetAsync<T>(key, ct);
        if (cached != null)
        {
            return cached;
        }

        // Ako nema u cache-u, izvrši factory i sačuvaj.
        // Lock je per-key da različiti cache key-evi ne čekaju jedni druge.
        var keyLock = _keyLocks.GetOrAdd(key, static _ => new CacheKeyLock());
        Interlocked.Increment(ref keyLock.RefCount);
        var keyLockAcquired = false;

        try
        {
            await keyLock.Semaphore.WaitAsync(ct);
            keyLockAcquired = true;

            // Double-check nakon lock-a
            cached = await GetAsync<T>(key, ct);
            if (cached != null)
            {
                return cached;
            }

            var value = await factory();
            await SetAsync(key, value, expiration, ct);
            return value;
        }
        finally
        {
            if (keyLockAcquired)
            {
                keyLock.Semaphore.Release();
            }

            if (Interlocked.Decrement(ref keyLock.RefCount) == 0)
            {
                _keyLocks.TryRemove(new KeyValuePair<string, CacheKeyLock>(key, keyLock));
            }
        }
    }
}
