using Application.Analytics;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class OperationsAnalyticsIntegrityCacheClearTests
{
    [Fact]
    public async Task ClearAsync_MarksOperationsIntegrityUnverified()
    {
        var cache = new StubAnalyticsCacheService();
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var admin = new AnalyticsCacheAdminService(
            cache,
            distributedCache: null,
            NullLogger<AnalyticsCacheAdminService>.Instance,
            registry);

        await admin.ClearAsync("supplier-sales");

        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, registry.Current.Status);
        Assert.True(registry.Current.BlocksDecisionSignals);
    }

    private sealed class StubAnalyticsCacheService : IAnalyticsCacheService
    {
        public bool IsRedisAvailable => false;
        public bool IsRedisEnabled => false;

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
            => Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;

        public Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
            => factory();

        public void SetRedisEnabled(bool enabled) { }

        public CacheFootprintSnapshot GetFootprintSnapshot()
            => new("in-memory", false, false, 0);
    }
}
