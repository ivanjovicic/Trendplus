using System.Reflection;
using System.Runtime.ExceptionServices;
using Application.Artikli.Common.Interfaces;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Workers;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsAggregationWorkerTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public AnalyticsAggregationWorkerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task RefreshAnalyticsAsync_WhenSuccessful_InvalidatesDashboardAndAggregateBackedPrefixes()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_analytics_agg_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var harness = CreateHarness(connectionString!, useInMemoryDatabase: false);

        await InvokeRefreshAnalyticsAsync(harness.Worker);

        Assert.Equal(ExpectedRemovedPrefixes(), harness.Cache.RemovedPrefixes);

        var state = await harness.CacheAdmin.GetStateAsync(CancellationToken.None);
        Assert.Equal(AnalyticsCachePolicy.DashboardFamily, state.LastClearFamily);
        Assert.NotNull(state.LastClearAtUtc);
        Assert.NotNull(state.LastAnalyticsCacheClearAtUtc);
        Assert.Null(state.LastReportCacheClearAtUtc);
        Assert.Equal(1, state.ReportCacheVersion);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task RefreshAnalyticsAsync_WhenConnectionStringMissing_DoesNotInvalidateCache()
    {
        await using var harness = CreateHarness(connectionString: null, useInMemoryDatabase: true);

        await InvokeRefreshAnalyticsAsync(harness.Worker);

        Assert.Empty(harness.Cache.RemovedPrefixes);

        var state = await harness.CacheAdmin.GetStateAsync(CancellationToken.None);
        Assert.Null(state.LastClearAtUtc);
        Assert.Null(state.LastClearFamily);
        Assert.Null(state.LastAnalyticsCacheClearAtUtc);
        Assert.Null(state.LastReportCacheClearAtUtc);
        Assert.Equal(1, state.ReportCacheVersion);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task RefreshAnalyticsAsync_WhenRefreshFails_DoesNotInvalidateCache()
    {
        await using var harness = CreateHarness(
            connectionString: "Host=127.0.0.1;Port=1;Database=trendplus_agg_failure;Username=invalid;Password=invalid;Timeout=1;Command Timeout=1;Pooling=false",
            useInMemoryDatabase: false);

        await InvokeRefreshAnalyticsAsync(harness.Worker);

        Assert.Empty(harness.Cache.RemovedPrefixes);

        var state = await harness.CacheAdmin.GetStateAsync(CancellationToken.None);
        Assert.Null(state.LastClearAtUtc);
        Assert.Null(state.LastClearFamily);
        Assert.Null(state.LastAnalyticsCacheClearAtUtc);
        Assert.Null(state.LastReportCacheClearAtUtc);
        Assert.Equal(1, state.ReportCacheVersion);
    }

    private static List<string> ExpectedRemovedPrefixes() =>
    [
        AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.DashboardFamily),
        AnalyticsCacheKeys.DashboardBootstrapPrefix,
        AnalyticsCacheKeys.DashboardAdvancedPrefix,
        AnalyticsCacheKeys.SalesSummaryPrefix,
        AnalyticsCacheKeys.DailySalesPrefix,
        AnalyticsCacheKeys.CategoryDataPrefix,
        AnalyticsCacheKeys.GenderDataPrefix,
        AnalyticsCacheKeys.SupplierDataPrefix,
        AnalyticsCacheKeys.TopProductsPrefix,
        AnalyticsCacheKeys.TopProductsAdvancedPrefix
    ];

    private static async Task InvokeRefreshAnalyticsAsync(AnalyticsAggregationWorker worker)
    {
        var method = typeof(AnalyticsAggregationWorker).GetMethod(
            "RefreshAnalyticsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        try
        {
            var task = (Task)method!.Invoke(worker, [CancellationToken.None])!;
            await task;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static WorkerHarness CreateHarness(string? connectionString, bool useInMemoryDatabase)
    {
        var services = new ServiceCollection();
        var recordingCache = new RecordingAnalyticsCacheService();
        services.AddSingleton(recordingCache);
        services.AddSingleton<IAnalyticsCacheService>(recordingCache);
        services.AddDistributedMemoryCache();
        services.AddSingleton<AnalyticsCacheAdminService>();
        services.AddSingleton<WorkerHealthService>();
        services.AddSingleton(new WorkerRuntimeControlService(initialEnabled: true, runtimeToggleAllowed: true, initialSource: "tests"));
        services.AddSingleton<ILogger<AnalyticsCacheAdminService>>(NullLogger<AnalyticsCacheAdminService>.Instance);

        if (useInMemoryDatabase)
        {
            services.AddDbContext<TrendplusDbContext>(options => options.UseInMemoryDatabase($"trendplus-agg-worker-{Guid.NewGuid():N}"));
        }
        else
        {
            services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(connectionString!));
        }

        services.AddScoped<ITrendplusDbContext>(sp => sp.GetRequiredService<TrendplusDbContext>());

        var provider = services.BuildServiceProvider(validateScopes: true);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new AnalyticsAggregationWorker(
            scopeFactory,
            NullLogger<AnalyticsAggregationWorker>.Instance,
            provider.GetRequiredService<WorkerHealthService>(),
            provider.GetRequiredService<WorkerRuntimeControlService>(),
            new WorkerRuntimePolicyService(scopeFactory, NullLogger<WorkerRuntimePolicyService>.Instance));

        return new WorkerHarness(
            provider,
            worker,
            recordingCache,
            provider.GetRequiredService<AnalyticsCacheAdminService>());
    }

    private sealed class WorkerHarness : IAsyncDisposable
    {
        public WorkerHarness(
            ServiceProvider provider,
            AnalyticsAggregationWorker worker,
            RecordingAnalyticsCacheService cache,
            AnalyticsCacheAdminService cacheAdmin)
        {
            Provider = provider;
            Worker = worker;
            Cache = cache;
            CacheAdmin = cacheAdmin;
        }

        public ServiceProvider Provider { get; }
        public AnalyticsAggregationWorker Worker { get; }
        public RecordingAnalyticsCacheService Cache { get; }
        public AnalyticsCacheAdminService CacheAdmin { get; }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
        }
    }

    private sealed class RecordingAnalyticsCacheService : IAnalyticsCacheService
    {
        public List<string> RemovedPrefixes { get; } = [];

        public bool IsRedisAvailable { get; set; }
        public bool IsRedisEnabled { get; set; }

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
}
