using System.Text;
using Application.Analytics;
using Infrastructure.Services.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsCacheAdminServiceTests
{
    [Theory]
    [InlineData(AnalyticsCachePolicy.DashboardFamily, "analytics:dashboard-bootstrap:")]
    [InlineData(AnalyticsCachePolicy.DashboardFamily, "analytics:summary:v2:")]
    [InlineData(AnalyticsCachePolicy.DashboardFamily, "analytics:daily:v3:")]
    [InlineData(AnalyticsCachePolicy.ProductDecisionCenterFamily, "analytics:product-decision-center:")]
    [InlineData(AnalyticsCachePolicy.SupplierDecisionHubFamily, "analytics:supplier-decision-hub:")]
    [InlineData(AnalyticsCachePolicy.InventoryFamily, "analytics:inventory:")]
    [InlineData(AnalyticsCachePolicy.DataQualityFamily, "analytics:data-quality:")]
    [InlineData(AnalyticsCachePolicy.PrePostFamily, "analytics:vendor-sales-nivelacija:")]
    [InlineData(AnalyticsCachePolicy.NivelacijaFamily, "analytics:vendor-sales-nivelacija:")]
    [InlineData("vendor-sales-nivelacija", "analytics:vendor-sales-nivelacija-options:")]
    [InlineData(AnalyticsCachePolicy.PreNivelacijaPrioritetiFamily, "analytics:pre-nivelacija-priority:")]
    [InlineData("pre-nivelacija", "analytics:pre-nivelacija-priority:")]
    [InlineData(AnalyticsCachePolicy.ColorSalesFamily, "analytics:color-sales-stats:")]
    [InlineData(AnalyticsCachePolicy.SupplierSalesFamily, "analytics:supplier-sales-stats:")]
    [InlineData(AnalyticsCachePolicy.ShoeTypeSalesFamily, "analytics:shoe-type-sales-stats:")]
    [InlineData(AnalyticsCachePolicy.ReportsFamily, "analytics:analytics-report:")]
    public void ResolveFamilyPrefixes_ContainsRealKeyPrefix(string family, string expectedPrefix)
    {
        var actual = AnalyticsCachePolicy.ResolveFamilyPrefixes(family);

        Assert.Contains(expectedPrefix, actual);
        Assert.All(actual, prefix => Assert.StartsWith(AnalyticsCacheKeys.Prefix, prefix, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("dashbord")]
    [InlineData("analytics")]
    [InlineData("*")]
    public void ResolveFamilyPrefixes_UnknownFamily_IsRejected(string family)
    {
        Assert.False(AnalyticsCachePolicy.IsKnownFamily(family));
        Assert.Throws<ArgumentException>(() => AnalyticsCachePolicy.ResolveFamilyPrefixes(family));
    }

    [Fact]
    public void ResolveFamilyPrefixes_All_IsTheWholeNamespace()
    {
        Assert.Equal([AnalyticsCacheKeys.Prefix], AnalyticsCachePolicy.ResolveFamilyPrefixes(AnalyticsCachePolicy.AllFamilies));
    }

    [Fact]
    public async Task ClearFamiliesAsync_RemovesEachFamilyPrefix_AndPersistsSharedState()
    {
        var cache = new RecordingAnalyticsCacheService
        {
            IsRedisAvailable = true,
            IsRedisEnabled = true
        };
        var distributedCache = new TestDistributedCache();
        var sut = new AnalyticsCacheAdminService(
            cache,
            distributedCache,
            NullLogger<AnalyticsCacheAdminService>.Instance);
        string[] families =
        [
            AnalyticsCachePolicy.DashboardFamily,
            AnalyticsCachePolicy.DataQualityFamily,
            AnalyticsCachePolicy.PreNivelacijaPrioritetiFamily,
            AnalyticsCachePolicy.ReportsFamily
        ];

        var state = await sut.ClearFamiliesAsync(families, CancellationToken.None);

        Assert.Equal(
            families.SelectMany(AnalyticsCachePolicy.ResolveFamilyPrefixes).Distinct(StringComparer.Ordinal).ToArray(),
            cache.RemovedPrefixes);
        Assert.Contains("analytics:summary:v2:", cache.RemovedPrefixes);
        Assert.Contains("analytics:pre-nivelacija-priority:", cache.RemovedPrefixes);
        Assert.True(state.IsShared);
        Assert.Equal("redis", state.Storage);
        Assert.Null(state.Warning);
        Assert.Equal("dashboard,data-quality,pre-nivelacija-prioriteti,reports", state.LastClearFamily);
        Assert.NotNull(state.LastClearAtUtc);
        Assert.NotNull(state.LastAnalyticsCacheClearAtUtc);
        Assert.NotNull(state.LastReportCacheClearAtUtc);
        Assert.True(state.ReportCacheVersion >= 2);

        var reloaded = await sut.GetStateAsync(CancellationToken.None);
        Assert.True(reloaded.IsShared);
        Assert.Equal("redis", reloaded.Storage);
        Assert.Equal(state.LastClearFamily, reloaded.LastClearFamily);
        Assert.Equal(state.LastClearAtUtc, reloaded.LastClearAtUtc);
    }

    [Fact]
    public async Task ClearFamiliesAsync_WithNoFamilies_IsRejectedInsteadOfClearingEverything()
    {
        var cache = new RecordingAnalyticsCacheService();
        var sut = new AnalyticsCacheAdminService(
            cache,
            distributedCache: null,
            NullLogger<AnalyticsCacheAdminService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.ClearFamiliesAsync([], CancellationToken.None));
        Assert.Empty(cache.RemovedPrefixes);
    }

    [Fact]
    public async Task ClearAsync_UnknownFamily_IsRejectedAndRemovesNothing()
    {
        var cache = new RecordingAnalyticsCacheService();
        var sut = new AnalyticsCacheAdminService(
            cache,
            distributedCache: null,
            NullLogger<AnalyticsCacheAdminService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.ClearAsync("dashbord", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.ClearFamiliesAsync(["dashboard", "nope"], CancellationToken.None));
        Assert.Empty(cache.RemovedPrefixes);
    }

    [Fact]
    public async Task ClearAsync_All_ClearsWholeNamespace()
    {
        var cache = new RecordingAnalyticsCacheService();
        var sut = new AnalyticsCacheAdminService(
            cache,
            distributedCache: null,
            NullLogger<AnalyticsCacheAdminService>.Instance);

        var state = await sut.ClearAsync(AnalyticsCachePolicy.AllFamilies, CancellationToken.None);

        Assert.Equal([AnalyticsCacheKeys.Prefix], cache.RemovedPrefixes);
        Assert.Equal("all", state.LastClearFamily);
        Assert.True(state.ReportCacheVersion >= 2);
    }

    [Fact]
    public async Task BumpReportCacheVersionAsync_ConcurrentBumps_NeverCollapse()
    {
        var sut = new AnalyticsCacheAdminService(
            new RecordingAnalyticsCacheService(),
            distributedCache: null,
            NullLogger<AnalyticsCacheAdminService>.Instance);
        var before = await sut.GetReportCacheVersionAsync();

        var versions = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => sut.BumpReportCacheVersionAsync())));

        Assert.Equal(32, versions.Distinct().Count());
        Assert.Equal(before + 32, await sut.GetReportCacheVersionAsync());
    }

    [Fact]
    public async Task ClearAsync_ReportsFamily_BumpsReportVersionToken()
    {
        var cache = new RecordingAnalyticsCacheService
        {
            IsRedisAvailable = false,
            IsRedisEnabled = false
        };
        var sut = new AnalyticsCacheAdminService(
            cache,
            distributedCache: null,
            NullLogger<AnalyticsCacheAdminService>.Instance);

        var before = await sut.GetReportCacheVersionAsync();
        var state = await sut.ClearAsync(AnalyticsCachePolicy.ReportsFamily, CancellationToken.None);
        var after = await sut.GetReportCacheVersionAsync();

        Assert.Equal([AnalyticsCacheKeys.ReportNamespace], cache.RemovedPrefixes);
        Assert.Equal(before + 1, after);
        Assert.Equal(after, state.ReportCacheVersion);
        Assert.NotNull(state.LastReportCacheClearAtUtc);
    }

    [Fact]
    public async Task ClearFamiliesAsync_CoreFamilies_BumpsReportCacheVersion()
    {
        // CoreFamilies includes ReportsFamily → bumps report version
        // This is the path taken by NightlyAnalyticsRefreshWorker and AccessImportService
        var cache = new RecordingAnalyticsCacheService();
        var sut = new AnalyticsCacheAdminService(
            cache,
            distributedCache: null,
            NullLogger<AnalyticsCacheAdminService>.Instance);

        var before = await sut.GetReportCacheVersionAsync();
        var state = await sut.ClearFamiliesAsync(AnalyticsCachePolicy.CoreFamilies, CancellationToken.None);

        Assert.True(state.ReportCacheVersion > before);
        Assert.NotNull(state.LastReportCacheClearAtUtc);
        Assert.NotNull(state.LastAnalyticsCacheClearAtUtc);
        Assert.Contains(AnalyticsCacheKeys.ReportNamespace, cache.RemovedPrefixes);
    }

    [Fact]
    public async Task ClearFamiliesAsync_NonReportFamily_DoesNotBumpReportVersion()
    {
        // Clearing only a non-report family must NOT bump the report cache version
        var cache = new RecordingAnalyticsCacheService();
        var sut = new AnalyticsCacheAdminService(
            cache,
            distributedCache: null,
            NullLogger<AnalyticsCacheAdminService>.Instance);

        var before = await sut.GetReportCacheVersionAsync();
        var state = await sut.ClearFamiliesAsync(
            [AnalyticsCachePolicy.DashboardFamily],
            CancellationToken.None);

        Assert.Equal(before, state.ReportCacheVersion);
        Assert.Null(state.LastReportCacheClearAtUtc);
        Assert.NotNull(state.LastAnalyticsCacheClearAtUtc);
    }

    [Fact]
    public async Task ReportCacheVersion_AfterVersionBump_SupplierReportKeyBecomesStale()
    {
        // Full stale-key contract: after version bump, cache key generated with old version
        // no longer matches the key used by the handler → old cached entry won't be returned.
        var cache = new RecordingAnalyticsCacheService();
        var sut = new AnalyticsCacheAdminService(
            cache,
            distributedCache: null,
            NullLogger<AnalyticsCacheAdminService>.Instance);
        var fromUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        var vBefore = await sut.GetReportCacheVersionAsync();
        await sut.ClearAsync(AnalyticsCachePolicy.ReportsFamily, CancellationToken.None);
        var vAfter = await sut.GetReportCacheVersionAsync();

        var staleKey = AnalyticsCacheKeys.SupplierDecisionReport(
            fromUtc, toUtc, null, null, null, null, false, false, null, null, "all",
            reportCacheVersion: vBefore);
        var freshKey = AnalyticsCacheKeys.SupplierDecisionReport(
            fromUtc, toUtc, null, null, null, null, false, false, null, null, "all",
            reportCacheVersion: vAfter);

        Assert.Equal(vBefore + 1, vAfter);
        Assert.NotEqual(staleKey, freshKey);
        Assert.Contains($":rv:{vAfter}:", freshKey);
        Assert.DoesNotContain($":rv:{vAfter}:", staleKey);
    }

    private sealed class RecordingAnalyticsCacheService : IAnalyticsCacheService
    {
        public List<string> RemovedPrefixes { get; } = [];

        public bool IsRedisAvailable { get; set; }

        public bool IsRedisEnabled { get; set; }

        public CacheFootprintSnapshot GetFootprintSnapshot()
            => new("disabled", false, false, 0);

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
            => Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
        {
            RemovedPrefixes.Add(prefix);
            return Task.CompletedTask;
        }

        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => factory();

        public void SetRedisEnabled(bool enabled)
            => IsRedisEnabled = enabled;
    }

    private sealed class TestDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _storage = new(StringComparer.Ordinal);

        public byte[]? Get(string key)
            => _storage.TryGetValue(key, out var value) ? value : null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => Task.FromResult(Get(key));

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
            => Task.CompletedTask;

        public void Remove(string key)
            => _storage.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            => _storage[key] = value;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }
}
