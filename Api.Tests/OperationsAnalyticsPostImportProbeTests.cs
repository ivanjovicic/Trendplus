using Application.Analytics;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class OperationsAnalyticsPostImportProbeTests
{
    [Fact]
    public async Task ClearFamiliesAsync_CanSuppressBackgroundIntegrityProbe()
    {
        var cache = new StubAnalyticsCacheService();
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var scopeFactory = new CountingScopeFactory();
        var admin = new AnalyticsCacheAdminService(
            cache,
            distributedCache: null,
            NullLogger<AnalyticsCacheAdminService>.Instance,
            registry,
            scopeFactory);

        await admin.ClearFamiliesAsync(
            new[] { "supplier-sales", "reports" },
            CancellationToken.None,
            scheduleIntegrityProbe: false);

        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, registry.Current.Status);
        Assert.Equal(0, scopeFactory.CreateCount);
    }

    [Fact]
    public async Task ClearFamiliesAsync_SchedulesBackgroundIntegrityProbeByDefault()
    {
        var cache = new StubAnalyticsCacheService();
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var scopeFactory = new CountingScopeFactory();
        var admin = new AnalyticsCacheAdminService(
            cache,
            distributedCache: null,
            NullLogger<AnalyticsCacheAdminService>.Instance,
            registry,
            scopeFactory);

        await admin.ClearFamiliesAsync(new[] { "supplier-sales" }, CancellationToken.None);

        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, registry.Current.Status);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (scopeFactory.CreateCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(scopeFactory.CreateCount >= 1);
    }

    [Fact]
    public void AccessImportSchedulesPostImportProbeAfterCommitAndSuppressesCacheClearProbe()
    {
        var source = ReadRepoFile("Api/Services/AccessImportService.cs");
        Assert.Contains("SchedulePostImportIntegrityProbe", source);
        Assert.Contains("scheduleIntegrityProbe: false", source);
        Assert.Contains("access_import:{batchId}", source);
        Assert.Contains("RunBoundedProbeAsync()", source);
        Assert.DoesNotContain(
            "_operationsIntegrityRegistry?.MarkUnverified(\n                            \"access_import\",",
            source.Replace("\r\n", "\n"));
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private sealed class CountingScopeFactory : IServiceScopeFactory
    {
        private int _createCount;
        public int CreateCount => Volatile.Read(ref _createCount);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _createCount);
            var services = new ServiceCollection();
            services.AddSingleton<IOperationsAnalyticsIntegrityService, NoopIntegrityService>();
            return services.BuildServiceProvider().CreateScope();
        }
    }

    private sealed class NoopIntegrityService : IOperationsAnalyticsIntegrityService
    {
        public void MarkUnverified(string trigger, string summary) { }

        public Task MarkUnverifiedAsync(string trigger, string summary, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<OperationsAnalyticsIntegritySnapshot> RunBoundedProbeAsync(CancellationToken ct = default)
            => Task.FromResult(OperationsAnalyticsIntegritySnapshot.Degraded("test", "noop", "noop"));
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
