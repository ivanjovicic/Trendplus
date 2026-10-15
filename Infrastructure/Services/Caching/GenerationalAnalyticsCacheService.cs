using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Caching;

/// <summary>
/// Shared implementation for the in-memory and hybrid (memory + Redis) analytics caches.
///
/// Invalidation uses versioned generations instead of key enumeration:
/// - every root prefix from <see cref="AnalyticsCachePolicy.GenerationRoots"/> has a generation token;
/// - the physical key is the logical key plus a fingerprint of the tokens of every root covering it;
/// - invalidating a prefix writes a fresh unique token (no read-modify-write), which makes every older
///   physical key unreachable on every instance that reads the same token store (Redis when enabled).
///
/// Values are stored as serialized JSON (L1 and L2), so callers always get a private copy and can never
/// mutate a shared cached object. Writes carry the generation observed before the value was computed and
/// are skipped when an invalidation happened meanwhile.
/// </summary>
public abstract class GenerationalAnalyticsCacheService : IAnalyticsCacheService
{
    internal const string GenerationKeyPrefix = "analytics-gen:v1:";
    private const string InitialToken = "0";
    private static readonly TimeSpan GenerationTokenLifetime = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaxSlidingExpiration = TimeSpan.FromMinutes(2);

    private static readonly Meter CacheMeter = new("Trendplus.AnalyticsCache");
    private static readonly Counter<long> HitCounter = CacheMeter.CreateCounter<long>("analytics_cache_hits");
    private static readonly Counter<long> MissCounter = CacheMeter.CreateCounter<long>("analytics_cache_misses");
    private static readonly Counter<long> FactoryCounter = CacheMeter.CreateCounter<long>("analytics_cache_factory_executions");
    private static readonly Counter<long> StaleSkipCounter = CacheMeter.CreateCounter<long>("analytics_cache_stale_writes_skipped");
    private static readonly Counter<long> InvalidationCounter = CacheMeter.CreateCounter<long>("analytics_cache_invalidations");

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };

    private sealed record L1Entry(byte[] Json, DateTimeOffset ExpiresAtUtc, string TypeName);

    private sealed class L2Envelope
    {
        [JsonPropertyName("exp")]
        public long ExpiresAtUnixMs { get; set; }

        [JsonPropertyName("v")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("t")]
        public string TypeName { get; set; } = string.Empty;
    }

    private readonly IMemoryCache _memoryCache;
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.Ordinal);
    private readonly AnalyticsCacheKeyedLock _keyLocks = new();
    private readonly ConcurrentDictionary<string, string> _localTokens = new(StringComparer.Ordinal);
    private readonly string[] _roots;

    private long _l1Hits;
    private long _l2Hits;
    private long _misses;
    private long _factoryExecutions;
    private long _staleWritesSkipped;
    private long _invalidations;
    private long _serializationFailures;

    protected GenerationalAnalyticsCacheService(IMemoryCache memoryCache, ILogger logger, TimeProvider? timeProvider)
    {
        _memoryCache = memoryCache;
        Logger = logger;
        Time = timeProvider ?? TimeProvider.System;
        _roots = AnalyticsCachePolicy.GenerationRoots
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static root => root.Length)
            .ThenBy(static root => root, StringComparer.Ordinal)
            .ToArray();
    }

    protected ILogger Logger { get; }

    protected TimeProvider Time { get; }

    public abstract bool IsRedisAvailable { get; }

    public abstract bool IsRedisEnabled { get; }

    public abstract void SetRedisEnabled(bool enabled);

    protected abstract string CacheModeName { get; }

    public CacheFootprintSnapshot GetFootprintSnapshot() => new(
        CacheMode: CacheModeName,
        RedisEnabled: IsRedisEnabled,
        RedisAvailable: IsRedisAvailable,
        TrackedKeyCount: _keys.Count);

    public AnalyticsCacheMetricsSnapshot GetMetricsSnapshot() => new(
        Interlocked.Read(ref _l1Hits),
        Interlocked.Read(ref _l2Hits),
        Interlocked.Read(ref _misses),
        Interlocked.Read(ref _factoryExecutions),
        Interlocked.Read(ref _staleWritesSkipped),
        Interlocked.Read(ref _invalidations),
        Interlocked.Read(ref _serializationFailures));

    // ---- L2 hooks (no-op for the pure in-memory cache) ----

    /// <summary>True when a shared store is currently used for tokens and values.</summary>
    protected virtual bool UseDistributed => false;

    /// <summary>Called before each operation so the hybrid cache can probe Redis recovery.</summary>
    protected virtual Task EnsureDistributedStateAsync(CancellationToken ct) => Task.CompletedTask;

    protected virtual Task<string?> ReadDistributedAsync(string key, CancellationToken ct) => Task.FromResult<string?>(null);

    protected virtual Task WriteDistributedAsync(string key, string value, TimeSpan ttl, CancellationToken ct) => Task.CompletedTask;

    protected virtual Task RemoveDistributedAsync(string key, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Hybrid cache reports a Redis failure here; it must stop trusting L1 entries.</summary>
    protected virtual void OnDistributedFailure(Exception ex, string operation, string key)
    {
    }

    // ---- generations ----

    internal IReadOnlyList<string> GetCoveringRoots(string key)
    {
        var covering = new List<string>(3);
        foreach (var root in _roots)
        {
            if (key.StartsWith(root, StringComparison.Ordinal))
            {
                covering.Add(root);
            }
        }

        return covering;
    }

    /// <summary>Rotates every local token so all L1 entries become unreachable (used on Redis outage/toggle).</summary>
    protected void RotateAllLocalTokens()
    {
        foreach (var root in _roots)
        {
            _localTokens[root] = NewToken();
        }
    }

    /// <summary>Writes a fresh token for every root into the shared store (used after Redis recovery).</summary>
    protected async Task<bool> BumpAllDistributedRootsAsync(CancellationToken ct)
    {
        foreach (var root in _roots)
        {
            // Written directly: callers keep the shared store untrusted until every root is bumped.
            var token = NewToken();
            try
            {
                await WriteDistributedAsync(GenerationKeyPrefix + root, token, GenerationTokenLifetime, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnDistributedFailure(ex, "generation-recovery", root);
                return false;
            }

            _localTokens[root] = token;
        }

        return true;
    }

    private static string NewToken() => Guid.NewGuid().ToString("N");

    private async Task<string> ComputeFingerprintAsync(string key, CancellationToken ct)
    {
        var roots = GetCoveringRoots(key);
        if (roots.Count == 0)
        {
            return string.Empty;
        }

        var tokens = new string[roots.Count];
        if (UseDistributed)
        {
            try
            {
                var reads = roots.Select(root => ReadDistributedAsync(GenerationKeyPrefix + root, ct)).ToArray();
                var values = await Task.WhenAll(reads);
                for (var i = 0; i < roots.Count; i++)
                {
                    tokens[i] = string.IsNullOrEmpty(values[i]) ? InitialToken : values[i]!;
                }

                return Hash(roots, tokens, "r");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnDistributedFailure(ex, "generation-read", key);
            }
        }

        for (var i = 0; i < roots.Count; i++)
        {
            tokens[i] = _localTokens.GetOrAdd(roots[i], InitialToken);
        }

        return Hash(roots, tokens, "l");
    }

    private static string Hash(IReadOnlyList<string> roots, string[] tokens, string source)
    {
        var builder = new StringBuilder(source);
        for (var i = 0; i < roots.Count; i++)
        {
            builder.Append('|').Append(roots[i]).Append('=').Append(tokens[i]);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash, 0, 8);
    }

    private static string PhysicalKey(string key, string fingerprint)
        => fingerprint.Length == 0 ? key : key + "|g:" + fingerprint;

    private async Task<bool> TryWriteTokenAsync(string root, string token, CancellationToken ct)
    {
        _localTokens[root] = token;
        if (!UseDistributed)
        {
            return true;
        }

        try
        {
            await WriteDistributedAsync(GenerationKeyPrefix + root, token, GenerationTokenLifetime, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            OnDistributedFailure(ex, "generation-write", root);
            return false;
        }
    }

    // ---- IAnalyticsCacheService ----

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        => (await GetWithGenerationAsync<T>(key, ct)).Value;

    public async Task<AnalyticsCacheLookup<T>> GetWithGenerationAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        await EnsureDistributedStateAsync(ct);
        var fingerprint = await ComputeFingerprintAsync(key, ct);
        var stamp = new AnalyticsCacheGenerationStamp(fingerprint);
        var physicalKey = PhysicalKey(key, fingerprint);
        var now = Time.GetUtcNow();

        if (_memoryCache.TryGetValue(physicalKey, out L1Entry? entry) && entry is not null && entry.ExpiresAtUtc > now
            && IsCompatibleType<T>(entry.TypeName))
        {
            var value = TryDeserialize<T>(entry.Json, key);
            if (value is not null)
            {
                RecordHit(ref _l1Hits, "l1");
                return new AnalyticsCacheLookup<T>(value, stamp);
            }
        }

        if (UseDistributed)
        {
            try
            {
                var raw = await ReadDistributedAsync(physicalKey, ct);
                if (!string.IsNullOrEmpty(raw))
                {
                    var envelope = JsonSerializer.Deserialize<L2Envelope>(raw, JsonOptions);
                    if (envelope is not null)
                    {
                        var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(envelope.ExpiresAtUnixMs);
                        var remaining = expiresAt - now;
                        if (remaining > TimeSpan.Zero && IsCompatibleType<T>(envelope.TypeName))
                        {
                            var bytes = Encoding.UTF8.GetBytes(envelope.Value);
                            var value = TryDeserialize<T>(bytes, key);
                            if (value is not null)
                            {
                                // Promotion keeps the remaining L2 lifetime instead of restarting the TTL.
                                SetL1(physicalKey, bytes, expiresAt, remaining, envelope.TypeName);
                                RecordHit(ref _l2Hits, "l2");
                                return new AnalyticsCacheLookup<T>(value, stamp);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not JsonException)
            {
                OnDistributedFailure(ex, "get", key);
            }
            catch (JsonException ex)
            {
                Logger.LogWarning(ex, "Analytics cache L2 entry for {Key} is not a valid envelope; treated as miss", key);
            }
        }

        Interlocked.Increment(ref _misses);
        MissCounter.Add(1);
        return new AnalyticsCacheLookup<T>(null, stamp);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
        => await SetIfGenerationAsync(key, value, expiration, null, ct);

    public async Task<bool> SetIfGenerationAsync<T>(
        string key,
        T value,
        TimeSpan? expiration,
        AnalyticsCacheGenerationStamp? stamp,
        CancellationToken ct = default) where T : class
    {
        if (value is null)
        {
            return false;
        }

        await EnsureDistributedStateAsync(ct);
        var fingerprint = await ComputeFingerprintAsync(key, ct);
        if (stamp is not null && !string.Equals(stamp.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _staleWritesSkipped);
            StaleSkipCounter.Add(1);
            Logger.LogDebug("Analytics cache write skipped for {Key}: generation changed while the value was computed", key);
            return false;
        }

        byte[] json;
        try
        {
            json = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), JsonOptions);
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException)
        {
            Interlocked.Increment(ref _serializationFailures);
            Logger.LogWarning(ex, "Analytics cache value for {Key} is not serializable; not cached", key);
            return false;
        }

        var ttl = expiration ?? CacheExpiration.Medium;
        if (ttl <= TimeSpan.Zero)
        {
            return false;
        }

        var expiresAt = Time.GetUtcNow() + ttl;
        var physicalKey = PhysicalKey(key, fingerprint);
        SetL1(physicalKey, json, expiresAt, ttl, StoredTypeName(value));

        if (UseDistributed)
        {
            try
            {
                var envelope = JsonSerializer.Serialize(new L2Envelope
                {
                    ExpiresAtUnixMs = expiresAt.ToUnixTimeMilliseconds(),
                    Value = Encoding.UTF8.GetString(json),
                    TypeName = StoredTypeName(value)
                }, JsonOptions);
                await WriteDistributedAsync(physicalKey, envelope, ttl, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnDistributedFailure(ex, "set", key);
            }
        }

        return true;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await EnsureDistributedStateAsync(ct);
        RemoveLocalPhysicalKeys(key, exactLogicalKey: true);
        var physicalKey = PhysicalKey(key, await ComputeFingerprintAsync(key, ct));
        if (UseDistributed)
        {
            try
            {
                await RemoveDistributedAsync(physicalKey, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnDistributedFailure(ex, "remove", key);
            }
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        await EnsureDistributedStateAsync(ct);

        // Exact registered root: bump only that generation. Any other analytics prefix bumps the
        // global analytics root (conservative over-invalidation, never under-invalidation).
        string? rootToBump = null;
        if (_roots.Contains(prefix, StringComparer.Ordinal))
        {
            rootToBump = prefix;
        }
        else if (prefix.StartsWith(AnalyticsCacheKeys.Prefix, StringComparison.Ordinal)
                 || AnalyticsCacheKeys.Prefix.StartsWith(prefix, StringComparison.Ordinal))
        {
            rootToBump = AnalyticsCacheKeys.Prefix;
        }

        if (rootToBump is not null)
        {
            await TryWriteTokenAsync(rootToBump, NewToken(), ct);
        }

        var removed = RemoveLocalPhysicalKeys(prefix, exactLogicalKey: false);
        Interlocked.Increment(ref _invalidations);
        InvalidationCounter.Add(1);
        Logger.LogInformation(
            "Analytics cache invalidated prefix {Prefix} (generation root {Root}); freed {Count} local entries",
            prefix,
            rootToBump ?? "(none)",
            removed);
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        var lookup = await GetWithGenerationAsync<T>(key, ct);
        if (lookup.Value is not null)
        {
            return lookup.Value;
        }

        using (await _keyLocks.AcquireAsync(key, ct))
        {
            lookup = await GetWithGenerationAsync<T>(key, ct);
            if (lookup.Value is not null)
            {
                return lookup.Value;
            }

            Interlocked.Increment(ref _factoryExecutions);
            FactoryCounter.Add(1);
            var value = await factory();
            await SetIfGenerationAsync(key, value, expiration, lookup.Stamp, ct);
            return value;
        }
    }

    // ---- helpers ----

    internal int LockCount => _keyLocks.Count;

    private T? TryDeserialize<T>(byte[] json, string key) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            Interlocked.Increment(ref _serializationFailures);
            Logger.LogWarning(ex, "Analytics cache entry for {Key} could not be deserialized; treated as miss", key);
            return null;
        }
    }

    private void SetL1(string physicalKey, byte[] json, DateTimeOffset expiresAt, TimeSpan ttl, string typeName)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            SlidingExpiration = ttl < MaxSlidingExpiration ? ttl : MaxSlidingExpiration,
            Priority = CacheItemPriority.Normal,
            Size = json.LongLength > 0 ? json.LongLength : 1
        };
        options.RegisterPostEvictionCallback(static (evictedKey, _, _, state) =>
        {
            ((ConcurrentDictionary<string, byte>)state!).TryRemove(evictedKey.ToString()!, out _);
        }, _keys);

        _memoryCache.Set(physicalKey, new L1Entry(json, expiresAt, typeName), options);
        _keys.TryAdd(physicalKey, 0);
    }

    private int RemoveLocalPhysicalKeys(string keyOrPrefix, bool exactLogicalKey)
    {
        var removed = 0;
        foreach (var physicalKey in _keys.Keys)
        {
            var matches = exactLogicalKey
                ? physicalKey.Equals(keyOrPrefix, StringComparison.Ordinal)
                  || physicalKey.StartsWith(keyOrPrefix + "|g:", StringComparison.Ordinal)
                : physicalKey.StartsWith(keyOrPrefix, StringComparison.Ordinal);
            if (!matches)
            {
                continue;
            }

            _memoryCache.Remove(physicalKey);
            if (_keys.TryRemove(physicalKey, out _))
            {
                removed++;
            }
        }

        return removed;
    }

    private static string TypeNameOf<T>() => typeof(T).FullName ?? typeof(T).Name;

    private static string StoredTypeName(object value) => value.GetType().FullName ?? value.GetType().Name;

    /// <summary>
    /// A stored value is only returned as the type it was stored as (object readers get a JsonElement view).
    /// This keeps the old in-memory semantics where a type mismatch was a miss, never a silently empty object.
    /// </summary>
    private static bool IsCompatibleType<T>(string storedTypeName)
        => typeof(T) == typeof(object)
           || typeof(T).IsInterface
           || typeof(T).IsAbstract
           || string.Equals(storedTypeName, TypeNameOf<T>(), StringComparison.Ordinal);

    private static void RecordHit(ref long counter, string level)
    {
        Interlocked.Increment(ref counter);
        HitCounter.Add(1, new KeyValuePair<string, object?>("level", level));
    }
}
