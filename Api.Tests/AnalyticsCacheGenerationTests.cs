using System.Collections.Concurrent;
using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Application.Analytics;
using Infrastructure.Services.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Generation-based invalidation, concurrency and multi-instance behaviour of the analytics cache
/// (matrix B/C/D/E). Two HybridCacheService instances sharing one distributed store model two app instances.
/// </summary>
public sealed class AnalyticsCacheGenerationTests
{
    private static readonly string SupplierKey = AnalyticsCacheKeys.SupplierSalesStats(
        new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        storeId: 4,
        sezonaId: null,
        dataScope: "all",
        activeSnapshotBatchId: 7,
        integrityEvidenceId: "ev-1");

    private static readonly string ShoeKey = AnalyticsCacheKeys.ShoeTypeSalesStats(
        new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        storeId: 4,
        sezonaId: null,
        dataScope: "all",
        activeSnapshotBatchId: 7,
        integrityEvidenceId: "ev-1");

    public sealed class Payload
    {
        public string Value { get; set; } = string.Empty;

        public List<int> Items { get; set; } = [];
    }

    public sealed class OtherPayload
    {
        public int Count { get; set; }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    /// <summary>Distributed cache that can be switched into an outage.</summary>
    private sealed class FlakyDistributedCache : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));

        public volatile bool Down;

        private void ThrowIfDown()
        {
            if (Down)
            {
                throw new InvalidOperationException("redis down");
            }
        }

        public byte[]? Get(string key) { ThrowIfDown(); return _inner.Get(key); }
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) { ThrowIfDown(); return _inner.GetAsync(key, token); }
        public void Refresh(string key) { ThrowIfDown(); _inner.Refresh(key); }
        public Task RefreshAsync(string key, CancellationToken token = default) { ThrowIfDown(); return _inner.RefreshAsync(key, token); }
        public void Remove(string key) { ThrowIfDown(); _inner.Remove(key); }
        public Task RemoveAsync(string key, CancellationToken token = default) { ThrowIfDown(); return _inner.RemoveAsync(key, token); }
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { ThrowIfDown(); _inner.Set(key, value, options); }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) { ThrowIfDown(); return _inner.SetAsync(key, value, options, token); }
    }

    private static HybridCacheService Hybrid(IDistributedCache? shared, TimeProvider? time = null)
        => new(new MemoryCache(new MemoryCacheOptions()), NullLogger<HybridCacheService>.Instance, shared, time);

    private static InMemoryCacheService InMemory(TimeProvider? time = null)
        => new(new MemoryCache(new MemoryCacheOptions()), NullLogger<InMemoryCacheService>.Instance, time);

    public static TheoryData<string> Modes => new() { "memory", "hybrid-local", "hybrid-redis" };

    private static IAnalyticsCacheService Create(string mode) => mode switch
    {
        "memory" => InMemory(),
        "hybrid-local" => Hybrid(null),
        "hybrid-redis" => Hybrid(new FlakyDistributedCache()),
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task FamilyInvalidation_MakesConcreteKeyUnavailable(string mode)
    {
        var cache = Create(mode);
        var admin = new AnalyticsCacheAdminService(cache, null, NullLogger<AnalyticsCacheAdminService>.Instance);
        await cache.SetAsync(SupplierKey, new Payload { Value = "old" }, TimeSpan.FromMinutes(10));
        await cache.SetAsync(ShoeKey, new Payload { Value = "old" }, TimeSpan.FromMinutes(10));

        await admin.ClearAsync(AnalyticsCachePolicy.SupplierSalesFamily);

        Assert.Null(await cache.GetAsync<Payload>(SupplierKey));
        Assert.Equal("old", (await cache.GetAsync<Payload>(ShoeKey))?.Value);

        await admin.ClearAsync(AnalyticsCachePolicy.ShoeTypeSalesFamily);
        Assert.Null(await cache.GetAsync<Payload>(ShoeKey));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task EveryCoreFamily_InvalidatesItsOwnKeys(string mode)
    {
        var cache = Create(mode);
        var admin = new AnalyticsCacheAdminService(cache, null, NullLogger<AnalyticsCacheAdminService>.Instance);
        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var keysByFamily = new Dictionary<string, string>
        {
            [AnalyticsCachePolicy.DashboardFamily] = AnalyticsCacheKeys.SalesSummary(from, to, 1, null, "all"),
            [AnalyticsCachePolicy.ColorSalesFamily] = AnalyticsCacheKeys.ColorSalesStats(from, to, 1, null, "all"),
            [AnalyticsCachePolicy.SupplierSalesFamily] = SupplierKey,
            [AnalyticsCachePolicy.ShoeTypeSalesFamily] = ShoeKey,
            [AnalyticsCachePolicy.InventoryFamily] = AnalyticsCacheKeys.Inventory(2),
        };

        foreach (var (family, key) in keysByFamily)
        {
            await cache.SetAsync(key, new Payload { Value = family }, TimeSpan.FromMinutes(10));
            await admin.ClearAsync(family);
            Assert.True(
                await cache.GetAsync<Payload>(key) is null,
                $"family '{family}' must invalidate key '{key}'");
        }
    }

    [Fact]
    public async Task MultiInstance_AcachesBinvalidates_BothSeeNewValue()
    {
        var shared = new FlakyDistributedCache();
        var a = Hybrid(shared);
        var b = Hybrid(shared);

        await a.SetAsync(SupplierKey, new Payload { Value = "v1" }, TimeSpan.FromMinutes(10));
        Assert.Equal("v1", (await a.GetAsync<Payload>(SupplierKey))?.Value);
        Assert.Equal("v1", (await b.GetAsync<Payload>(SupplierKey))?.Value); // L2 hit, promoted to B's L1

        await b.RemoveByPrefixAsync("analytics:supplier-sales-stats:");

        Assert.Null(await a.GetAsync<Payload>(SupplierKey)); // A's L1 entry is unreachable
        Assert.Null(await b.GetAsync<Payload>(SupplierKey));

        var fromA = await a.GetOrSetAsync(SupplierKey, () => Task.FromResult(new Payload { Value = "v2" }), TimeSpan.FromMinutes(10));
        var fromB = await b.GetOrSetAsync(SupplierKey, () => Task.FromResult(new Payload { Value = "WRONG" }), TimeSpan.FromMinutes(10));
        Assert.Equal("v2", fromA.Value);
        Assert.Equal("v2", fromB.Value);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task SlowFactory_DoesNotRepublishAfterInvalidation(string mode)
    {
        var cache = Create(mode);
        var release = new TaskCompletionSource();
        var started = new TaskCompletionSource();

        var pending = cache.GetOrSetAsync(SupplierKey, async () =>
        {
            started.SetResult();
            await release.Task;
            return new Payload { Value = "computed-before-import" };
        }, TimeSpan.FromMinutes(10));

        await started.Task;
        await cache.RemoveByPrefixAsync("analytics:supplier-sales-stats:"); // import commit + invalidation
        release.SetResult();

        Assert.Equal("computed-before-import", (await pending).Value); // the caller still gets its answer
        Assert.Null(await cache.GetAsync<Payload>(SupplierKey)); // but it is never cached as current
        Assert.True(cache.GetMetricsSnapshot().StaleWritesSkipped >= 1);
    }

    [Fact]
    public async Task SlowFactory_OnOtherInstance_DoesNotRepublish()
    {
        var shared = new FlakyDistributedCache();
        var a = Hybrid(shared);
        var b = Hybrid(shared);
        var lookup = await a.GetWithGenerationAsync<Payload>(SupplierKey);

        await b.RemoveByPrefixAsync(AnalyticsCacheKeys.Prefix);

        Assert.False(await a.SetIfGenerationAsync(SupplierKey, new Payload { Value = "stale" }, TimeSpan.FromMinutes(5), lookup.Stamp));
        Assert.Null(await b.GetAsync<Payload>(SupplierKey));
        Assert.Null(await a.GetAsync<Payload>(SupplierKey));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task ConcurrentMisses_RunFactoryOnce_AndReleaseLocks(string mode)
    {
        var cache = Create(mode);
        var calls = 0;
        var gate = new TaskCompletionSource();

        var tasks = Enumerable.Range(0, 25).Select(_ => cache.GetOrSetAsync(SupplierKey, async () =>
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return new Payload { Value = "once" };
        }, TimeSpan.FromMinutes(10))).ToArray();

        await Task.Delay(50);
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, calls);
        Assert.All(results, r => Assert.Equal("once", r.Value));
        Assert.Equal(0, ((GenerationalAnalyticsCacheService)cache).LockCount);
    }

    [Fact]
    public async Task RedisOutage_DoesNotResurrectInvalidatedValues()
    {
        var time = new ManualTimeProvider();
        var shared = new FlakyDistributedCache();
        var a = Hybrid(shared, time);
        var b = Hybrid(shared, time);
        await a.SetAsync(SupplierKey, new Payload { Value = "before-outage" }, TimeSpan.FromMinutes(30));
        Assert.Equal("before-outage", (await a.GetAsync<Payload>(SupplierKey))?.Value);

        shared.Down = true;
        // A notices the outage and stops trusting its L1 (B may invalidate without A seeing it).
        Assert.Null(await a.GetAsync<Payload>(SupplierKey));
        await a.SetAsync(SupplierKey, new Payload { Value = "during-outage" }, TimeSpan.FromMinutes(30));
        Assert.Equal("during-outage", (await a.GetAsync<Payload>(SupplierKey))?.Value);

        shared.Down = false;
        await b.RemoveByPrefixAsync(AnalyticsCacheKeys.Prefix); // invalidation while A still thinks Redis is down
        time.Advance(TimeSpan.FromMinutes(2)); // health-check interval elapsed

        // A recovers: all generations are bumped before Redis is trusted again; neither old value comes back.
        Assert.Null(await a.GetAsync<Payload>(SupplierKey));
        Assert.Null(await b.GetAsync<Payload>(SupplierKey));
        Assert.True(a.IsRedisAvailable);
    }

    [Fact]
    public async Task MemoryOnlyCache_RespectsConfiguredTtlInsteadOfTwoMinuteSlidingExpiry()
    {
        var time = new ManualTimeProvider();
        var cache = new InMemoryCacheService(
            new MemoryCache(new MemoryCacheOptions { TimeProvider = time }),
            NullLogger<InMemoryCacheService>.Instance,
            time);

        await cache.SetAsync(SupplierKey, new Payload { Value = "long-lived" }, TimeSpan.FromMinutes(10));
        time.Advance(TimeSpan.FromMinutes(3));
        Assert.Equal("long-lived", (await cache.GetAsync<Payload>(SupplierKey))?.Value);

        time.Advance(TimeSpan.FromMinutes(8));
        Assert.Null(await cache.GetAsync<Payload>(SupplierKey));
    }

    [Fact]
    public async Task L2Promotion_KeepsRemainingTtl()
    {
        var time = new ManualTimeProvider();
        var shared = new FlakyDistributedCache();
        var a = Hybrid(shared, time);
        var b = Hybrid(shared, time);

        await a.SetAsync(SupplierKey, new Payload { Value = "v" }, TimeSpan.FromMinutes(10));
        time.Advance(TimeSpan.FromMinutes(9));
        Assert.Equal("v", (await b.GetAsync<Payload>(SupplierKey))?.Value); // promoted with ~1 minute left

        time.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await b.GetAsync<Payload>(SupplierKey)); // not a fresh 5 minute TTL
        Assert.Null(await a.GetAsync<Payload>(SupplierKey));
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task CachedObjects_CannotBeMutatedByCallers(string mode)
    {
        var cache = Create(mode);
        var original = new Payload { Value = "v", Items = [1, 2] };
        await cache.SetAsync(SupplierKey, original, TimeSpan.FromMinutes(10));

        original.Value = "mutated-after-set";
        var first = await cache.GetAsync<Payload>(SupplierKey);
        first!.Value = "mutated-by-request";
        first.Items.Add(99);

        var second = await cache.GetAsync<Payload>(SupplierKey);
        Assert.Equal("v", second!.Value);
        Assert.Equal([1, 2], second.Items);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task TypeMismatch_IsAMiss_NotAnEmptyObject(string mode)
    {
        var cache = Create(mode);
        await cache.SetAsync(SupplierKey, new Payload { Value = "v" }, TimeSpan.FromMinutes(10));

        Assert.Null(await cache.GetAsync<OtherPayload>(SupplierKey));
        Assert.NotNull(await cache.GetAsync<Payload>(SupplierKey));
    }

    [Fact]
    public async Task DisabledCache_AlwaysComputes()
    {
        var cache = new DisabledAnalyticsCacheService();
        var calls = 0;
        for (var i = 0; i < 3; i++)
        {
            await cache.GetOrSetAsync(SupplierKey, () => { calls++; return Task.FromResult(new Payload()); });
        }

        Assert.Equal(3, calls);
        Assert.Null(await cache.GetAsync<Payload>(SupplierKey));
    }

    [Fact]
    public async Task PolicyCache_ConcurrentMisses_ComputeOnce_AndCarryRealMetadata()
    {
        var cache = InMemory();
        var calls = 0;
        var gate = new TaskCompletionSource();
        var key = AnalyticsCacheKeys.DashboardBootstrap(new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), 1, null, "all");

        var tasks = Enumerable.Range(0, 10).Select(_ => CachedAnalyticsEndpoints.GetOrSetWithPolicyAsync(
            cache,
            key,
            AnalyticsCachePolicy.DashboardFamily,
            AnalyticsCachePolicy.DashboardBootstrap,
            async () =>
            {
                Interlocked.Increment(ref calls);
                await gate.Task;
                return new Payload { Value = "dash" };
            },
            CancellationToken.None)).ToArray();
        await Task.Delay(50);
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, calls);
        Assert.Single(results, r => !r.CacheHit);
        var created = results.Single(r => !r.CacheHit).Metadata.CreatedAtUtc;
        Assert.All(results, r => Assert.Equal(created, r.Metadata.CreatedAtUtc));
        Assert.NotEqual(default, created);
    }

    [Fact]
    public async Task PolicyCache_PartialResult_IsReturnedButNotCached()
    {
        var cache = InMemory();
        var calls = 0;
        var key = AnalyticsCacheKeys.DashboardBootstrap(new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), 1, null, "all");

        for (var i = 0; i < 2; i++)
        {
            var result = await CachedAnalyticsEndpoints.GetOrSetWithPolicyAsync(
                cache,
                key,
                AnalyticsCachePolicy.DashboardFamily,
                AnalyticsCachePolicy.DashboardBootstrap,
                () =>
                {
                    calls++;
                    return Task.FromResult(new AnalyticsDashboardBootstrapDto { Errors = ["Sazetak prodaje nije dostupan."] });
                },
                CancellationToken.None,
                isCacheable: static r => r.Errors.Count == 0 && r.Meta?.IsPartial != true);
            Assert.False(result.CacheHit);
        }

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task PolicyCache_EntryWithoutMetadata_IsAMiss_NotAFakeFreshHit()
    {
        var cache = InMemory();
        var key = AnalyticsCacheKeys.DashboardBootstrap(new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), 1, null, "all");
        await cache.SetAsync(
            CachedAnalyticsEndpoints.PolicyEntryKey(key),
            new CachedAnalyticsEndpoints.PolicyCacheEnvelope<Payload> { Value = new Payload { Value = "legacy" }, Metadata = null });

        var result = await CachedAnalyticsEndpoints.GetOrSetWithPolicyAsync(
            cache,
            key,
            AnalyticsCachePolicy.DashboardFamily,
            AnalyticsCachePolicy.DashboardBootstrap,
            () => Task.FromResult(new Payload { Value = "fresh" }),
            CancellationToken.None);

        Assert.False(result.CacheHit);
        Assert.Equal("fresh", result.Value.Value);
    }

    [Fact]
    public async Task PolicyCache_InvalidationDuringFactory_IsNotStored()
    {
        var cache = InMemory();
        var key = AnalyticsCacheKeys.DashboardBootstrap(new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), 1, null, "all");
        var admin = new AnalyticsCacheAdminService(cache, null, NullLogger<AnalyticsCacheAdminService>.Instance);

        await CachedAnalyticsEndpoints.GetOrSetWithPolicyAsync(
            cache,
            key,
            AnalyticsCachePolicy.DashboardFamily,
            AnalyticsCachePolicy.DashboardBootstrap,
            async () =>
            {
                await admin.ClearAsync(AnalyticsCachePolicy.DashboardFamily);
                return new Payload { Value = "pre-import" };
            },
            CancellationToken.None);

        var next = await CachedAnalyticsEndpoints.GetOrSetWithPolicyAsync(
            cache,
            key,
            AnalyticsCachePolicy.DashboardFamily,
            AnalyticsCachePolicy.DashboardBootstrap,
            () => Task.FromResult(new Payload { Value = "post-import" }),
            CancellationToken.None);

        Assert.False(next.CacheHit);
        Assert.Equal("post-import", next.Value.Value);
    }

    [Fact]
    public void ShoeTypeKey_SeparatesIntegrityEvidenceSnapshotAndScope()
    {
        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var keys = new[]
        {
            AnalyticsCacheKeys.ShoeTypeSalesStats(from, to, 4, null, "all", 7, "ev-1"),
            AnalyticsCacheKeys.ShoeTypeSalesStats(from, to, 4, null, "all", 7, "ev-2"),
            AnalyticsCacheKeys.ShoeTypeSalesStats(from, to, 4, null, "all", 8, "ev-1"),
            AnalyticsCacheKeys.ShoeTypeSalesStats(from, to, 4, null, "imported", 7, "ev-1"),
            AnalyticsCacheKeys.ShoeTypeSalesStats(from, to, 5, null, "all", 7, "ev-1"),
            AnalyticsCacheKeys.ShoeTypeSalesStats(from, to.AddDays(1), 4, null, "all", 7, "ev-1"),
        };

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.StartsWith("analytics:shoe-type-sales-stats:", key, StringComparison.Ordinal));
    }

    [Fact]
    public void EveryGenerationRoot_BelongsToAnalyticsNamespace()
    {
        Assert.Contains(AnalyticsCacheKeys.Prefix, AnalyticsCachePolicy.GenerationRoots);
        Assert.All(AnalyticsCachePolicy.GenerationRoots, root => Assert.StartsWith(AnalyticsCacheKeys.Prefix, root, StringComparison.Ordinal));
    }
}
