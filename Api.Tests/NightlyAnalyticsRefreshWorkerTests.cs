using System.Reflection;
using System.Runtime.ExceptionServices;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Workers;
using Xunit;

namespace Api.Tests;

public sealed class NightlyAnalyticsRefreshWorkerTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public NightlyAnalyticsRefreshWorkerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InvalidateAnalyticsCacheAsync_ClearsCoreFamilies()
    {
        await using var harness = CreateHarness(
            "Host=127.0.0.1;Port=1;Database=not-used;Username=invalid;Password=invalid",
            []);
        var warnings = new List<string>();

        await InvokeCacheInvalidationAsync(harness.Worker, harness.Provider, warnings);

        Assert.Equal(AnalyticsCachePolicy.CoreFamilies.Length, harness.Cache.RemovedPrefixes.Count);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ShouldInvalidateAnalyticsCache_RequiresCompletedMaterializedView()
    {
        var method = typeof(NightlyAnalyticsRefreshWorker).GetMethod(
            "ShouldInvalidateAnalyticsCache",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.False((bool)method!.Invoke(null, [Array.Empty<string>()])!);
        Assert.True((bool)method.Invoke(null, [new[] { "public.completed_mv" }])!);
    }

    [Fact]
    public async Task RunNightlyRefreshAsync_WhenSomeViewsCompleteBeforeFailure_InvalidatesCache()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_rq207_partial_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await CreateMaterializedViewAsync(connectionString!, "rq207_completed_mv");

        await using var harness = CreateHarness(
            connectionString!,
            ["public.rq207_completed_mv", "public.rq207_missing_mv"]);

        await InvokeRefreshAsync(harness.Worker);

        Assert.Equal(AnalyticsCachePolicy.CoreFamilies.Length, harness.Cache.RemovedPrefixes.Count);
        Assert.Contains(
            AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.DashboardFamily),
            harness.Cache.RemovedPrefixes);
        Assert.Contains(
            AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.ReportsFamily),
            harness.Cache.RemovedPrefixes);
    }

    [Fact]
    public async Task RunNightlyRefreshAsync_WhenNoViewCompletes_DoesNotInvalidateCache()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_rq207_empty_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var harness = CreateHarness(
            connectionString!,
            ["public.rq207_missing_mv"]);

        await InvokeRefreshAsync(harness.Worker);

        Assert.Empty(harness.Cache.RemovedPrefixes);
    }

    private static async Task CreateMaterializedViewAsync(string connectionString, string viewName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"CREATE MATERIALIZED VIEW public.\"{viewName}\" AS SELECT 1 AS value;",
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InvokeRefreshAsync(NightlyAnalyticsRefreshWorker worker)
    {
        var method = typeof(NightlyAnalyticsRefreshWorker).GetMethod(
            "RunNightlyRefreshAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        try
        {
            var task = (Task)method!.Invoke(worker, ["test", CancellationToken.None])!;
            await task;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static async Task InvokeCacheInvalidationAsync(
        NightlyAnalyticsRefreshWorker worker,
        IServiceProvider serviceProvider,
        List<string> warnings)
    {
        var method = typeof(NightlyAnalyticsRefreshWorker).GetMethod(
            "InvalidateAnalyticsCacheAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        try
        {
            var task = (Task)method!.Invoke(worker, [serviceProvider, warnings, CancellationToken.None])!;
            await task;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static WorkerHarness CreateHarness(
        string connectionString,
        IReadOnlyCollection<string> materializedViews)
    {
        var services = new ServiceCollection();
        var recordingCache = new RecordingAnalyticsCacheService();

        services.AddSingleton(recordingCache);
        services.AddSingleton<IAnalyticsCacheService>(recordingCache);
        services.AddDistributedMemoryCache();
        services.AddSingleton<AnalyticsCacheAdminService>();
        services.AddSingleton<WorkerHealthService>();
        services.AddSingleton(new WorkerRuntimeControlService(initialEnabled: true, runtimeToggleAllowed: true, initialSource: "tests"));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<ILogger<AnalyticsCacheAdminService>>(NullLogger<AnalyticsCacheAdminService>.Instance);
        services.AddSingleton<ILogger<AnalyticsRefreshRunRecorder>>(NullLogger<AnalyticsRefreshRunRecorder>.Instance);
        services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(connectionString));
        services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<AnalyticsRefreshRunRecorder>();

        var provider = services.BuildServiceProvider(validateScopes: true);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var worker = new NightlyAnalyticsRefreshWorker(
            scopeFactory,
            NullLogger<NightlyAnalyticsRefreshWorker>.Instance,
            provider.GetRequiredService<WorkerHealthService>(),
            provider.GetRequiredService<WorkerRuntimeControlService>(),
            new WorkerRuntimePolicyService(scopeFactory, NullLogger<WorkerRuntimePolicyService>.Instance),
            Options.Create(new NightlyAnalyticsRefreshOptions
            {
                RefreshConcurrently = false,
                MaterializedViewsToRefresh = materializedViews.ToList(),
                IntelligenceMaterializedViewsToRefresh = [],
                OpenTrainingMaterializedViewsToRefresh = [],
                VacuumAnalyzeTargets = [],
                QueueSupplierRankingTraining = false,
                CommandTimeoutSeconds = 10
            }));

        return new WorkerHarness(provider, worker, recordingCache);
    }

    private sealed class WorkerHarness : IAsyncDisposable
    {
        public WorkerHarness(
            ServiceProvider provider,
            NightlyAnalyticsRefreshWorker worker,
            RecordingAnalyticsCacheService cache)
        {
            Provider = provider;
            Worker = worker;
            Cache = cache;
        }

        public ServiceProvider Provider { get; }
        public NightlyAnalyticsRefreshWorker Worker { get; }
        public RecordingAnalyticsCacheService Cache { get; }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    private sealed class RecordingAnalyticsCacheService : IAnalyticsCacheService
    {
        public List<string> RemovedPrefixes { get; } = [];

        public bool IsRedisAvailable => false;
        public bool IsRedisEnabled => false;

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
        {
        }
    }
}
