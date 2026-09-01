using System.Reflection;
using System.Runtime.ExceptionServices;
using Application.Artikli.Common.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Workers;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsDataQualityHealthWorkerTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public AnalyticsDataQualityHealthWorkerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task RefreshDataQualityAsync_WhenSuccessful_InvalidatesTrustBearingFamiliesAndReports()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_dq_worker_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using (var db = new TrendplusDbContext(
            new DbContextOptionsBuilder<TrendplusDbContext>()
                .UseNpgsql(connectionString!)
                .Options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        await using var harness = CreateHarness(connectionString!);

        await InvokeRefreshDataQualityAsync(harness.Worker);

        Assert.Equal(ExpectedRemovedPrefixes(), harness.Cache.RemovedPrefixes);

        var state = await harness.CacheAdmin.GetStateAsync(CancellationToken.None);
        Assert.Equal("dashboard,product-decision-center,supplier-decision-hub,inventory,data-quality,reports", state.LastClearFamily);
        Assert.NotNull(state.LastClearAtUtc);
        Assert.NotNull(state.LastAnalyticsCacheClearAtUtc);
        Assert.NotNull(state.LastReportCacheClearAtUtc);
        Assert.True(state.ReportCacheVersion >= 2);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task RefreshDataQualityAsync_WhenCaptureFails_DoesNotInvalidateCache()
    {
        await using var harness = CreateHarness(
            "Host=127.0.0.1;Port=1;Database=trendplus_dq_worker_failure;Username=invalid;Password=invalid;Timeout=1;Command Timeout=1;Pooling=false");

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokeRefreshDataQualityAsync(harness.Worker));

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
        AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.ProductDecisionCenterFamily),
        AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.SupplierDecisionHubFamily),
        AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.InventoryFamily),
        AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.DataQualityFamily),
        AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.ReportsFamily)
    ];

    private static async Task InvokeRefreshDataQualityAsync(AnalyticsDataQualityHealthWorker worker)
    {
        var method = typeof(AnalyticsDataQualityHealthWorker).GetMethod(
            "RefreshDataQualityAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        try
        {
            var task = (Task)method!.Invoke(worker, [null, null, CancellationToken.None])!;
            await task;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static WorkerHarness CreateHarness(string connectionString)
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
        services.AddScoped<AnalyticsDataQualityHealthService>();
        services.AddScoped<AnalyticsDataQualityHistoryService>();
        services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ITrendplusDbContext>(sp => sp.GetRequiredService<TrendplusDbContext>());

        var provider = services.BuildServiceProvider(validateScopes: true);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new AnalyticsDataQualityHealthWorker(
            scopeFactory,
            NullLogger<AnalyticsDataQualityHealthWorker>.Instance,
            provider.GetRequiredService<WorkerHealthService>(),
            provider.GetRequiredService<WorkerRuntimeControlService>(),
            new WorkerRuntimePolicyService(scopeFactory, NullLogger<WorkerRuntimePolicyService>.Instance),
            Options.Create(new AnalyticsDataQualityHealthOptions
            {
                Enabled = true,
                StartupDelaySeconds = 0,
                PauseCheckSeconds = 0,
                PollIntervalMinutes = 5,
                LookbackDays = 30
            }));

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
            AnalyticsDataQualityHealthWorker worker,
            RecordingAnalyticsCacheService cache,
            AnalyticsCacheAdminService cacheAdmin)
        {
            Provider = provider;
            Worker = worker;
            Cache = cache;
            CacheAdmin = cacheAdmin;
        }

        public ServiceProvider Provider { get; }
        public AnalyticsDataQualityHealthWorker Worker { get; }
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
}
