using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

/// <summary>
/// Hibridni cache servis: In-Memory (L1) + Redis (L2).
/// 
/// ARHITEKTURA:
/// - L1 (In-Memory): Brz, lokalni, besplatan, ali nije deljen između instanci
/// - L2 (Redis): Sporiji, deljeni, opcioni (besplatan tier na većini cloud provajdera)
/// 
/// FLOW:
/// GET: L1 -> L2 -> Database
/// SET: L1 + L2 (paralelno)
/// INVALIDATE: L1 + L2 (paralelno)
/// 
/// FALLBACK:
/// Ako Redis nije dostupan, koristi se samo In-Memory.
/// </summary>
public class HybridCacheService : IAnalyticsCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly ILogger<HybridCacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _redisAvailable;
    private bool _redisUserEnabled = true;
    private DateTime _lastRedisCheck = DateTime.MinValue;
    private readonly TimeSpan _redisCheckInterval = TimeSpan.FromMinutes(1);

    public HybridCacheService(
        IMemoryCache memoryCache,
        ILogger<HybridCacheService> logger,
        IDistributedCache? distributedCache = null)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _logger = logger;
        _redisAvailable = distributedCache != null;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public bool IsRedisAvailable
    {
        get
        {
            if (DateTime.UtcNow - _lastRedisCheck > _redisCheckInterval)
                CheckRedisAvailability();
            return _redisAvailable;
        }
    }

    public bool IsRedisEnabled => _redisUserEnabled;

    public void SetRedisEnabled(bool enabled)
    {
        _redisUserEnabled = enabled;
        _logger.LogInformation("Redis cache {State} by user toggle", enabled ? "ENABLED" : "DISABLED");
    }

    private void CheckRedisAvailability()
    {
        if (_distributedCache == null)
        {
            _redisAvailable = false;
            return;
        }

        try
        {
            // Probaj jednostavnu operaciju
            _distributedCache.GetString("__health_check__");
            _redisAvailable = true;
        }
        catch
        {
            _redisAvailable = false;
            _logger.LogWarning("Redis is not available, falling back to In-Memory only");
        }
        
        _lastRedisCheck = DateTime.UtcNow;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        // L1: In-Memory (najbrže)
        if (_memoryCache.TryGetValue(key, out T? memoryValue))
        {
            _logger.LogDebug("Cache L1 HIT: {Key}", key);
            return memoryValue;
        }

        // L2: Redis (ako je dostupan i ukljucen)
        if (_redisUserEnabled && _redisAvailable && _distributedCache != null)
        {
            try
            {
                var redisValue = await _distributedCache.GetStringAsync(key, ct);
                if (!string.IsNullOrEmpty(redisValue))
                {
                    var value = JsonSerializer.Deserialize<T>(redisValue, _jsonOptions);
                    if (value != null)
                    {
                        // Promovisi u L1 cache
                        SetMemoryCache(key, value, GetPromotionExpiration(key));
                        _logger.LogDebug("Cache L2 HIT (promoted to L1): {Key}", key);
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis GET error for key: {Key}", key);
                _redisAvailable = false;
            }
        }

        _logger.LogDebug("Cache MISS: {Key}", key);
        return null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        var exp = expiration ?? CacheExpiration.Medium;
        
        // L1: In-Memory (uvek)
        SetMemoryCache(key, value, exp);

        // L2: Redis (ako je dostupan i ukljucen)
        if (_redisUserEnabled && _redisAvailable && _distributedCache != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = exp
                };
                
                await _distributedCache.SetStringAsync(key, json, options, ct);
                _logger.LogDebug("Cache L1+L2 SET: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis SET error for key: {Key}", key);
                _redisAvailable = false;
            }
        }
        else
        {
            _logger.LogDebug("Cache L1 SET (Redis unavailable): {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        // L1: In-Memory
        _memoryCache.Remove(key);
        _keys.TryRemove(key, out _);

        // L2: Redis
        if (_redisUserEnabled && _redisAvailable && _distributedCache != null)
        {
            try
            {
                await _distributedCache.RemoveAsync(key, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis REMOVE error for key: {Key}", key);
            }
        }
        
        _logger.LogDebug("Cache REMOVE: {Key}", key);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // L1: In-Memory - ukloni sve sa prefixom
            var keysToRemove = _keys.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
            {
                _memoryCache.Remove(key);
                _keys.TryRemove(key, out _);
            }
            
            // L2: Redis - NAPOMENA: Redis ne podržava wildcard delete bez SCAN
            // Za jednostavnost, brišemo poznate ključeve
            if (_redisUserEnabled && _redisAvailable && _distributedCache != null)
            {
                foreach (var key in keysToRemove)
                {
                    try
                    {
                        await _distributedCache.RemoveAsync(key, ct);
                    }
                    catch { /* ignore individual failures */ }
                }
            }
            
            _logger.LogInformation("Cache INVALIDATE by prefix: {Prefix}, Removed: {Count} keys", prefix, keysToRemove.Count);
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

        // Ako nema u cache-u, izvrši factory i sačuvaj
        await _lock.WaitAsync(ct);
        try
        {
            // Double-check nakon lock-a
            cached = await GetAsync<T>(key, ct);
            if (cached != null)
            {
                return cached;
            }

            _logger.LogDebug("Cache FACTORY executing for: {Key}", key);
            var value = await factory();
            await SetAsync(key, value, expiration, ct);
            return value;
        }
        finally
        {
            _lock.Release();
        }
    }

    private void SetMemoryCache<T>(string key, T value, TimeSpan expiration)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration,
            SlidingExpiration = TimeSpan.FromMinutes(2),
            Priority = CacheItemPriority.Normal
        };
        
        options.RegisterPostEvictionCallback((evictedKey, _, reason, _) =>
        {
            _keys.TryRemove(evictedKey.ToString()!, out _);
        });

        _memoryCache.Set(key, value, options);
        _keys.TryAdd(key, 0);
    }

    private static TimeSpan GetPromotionExpiration(string key)
    {
        if (key.StartsWith(AnalyticsCacheKeys.ObservabilityLogsPrefix, StringComparison.Ordinal))
        {
            return CacheExpiration.ObservabilityLive;
        }

        if (key.StartsWith(AnalyticsCacheKeys.ObservabilityPerformancePrefix, StringComparison.Ordinal))
        {
            return CacheExpiration.ObservabilitySummary;
        }

        return CacheExpiration.Medium;
    }
}
