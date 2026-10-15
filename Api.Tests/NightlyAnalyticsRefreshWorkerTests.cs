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

        // Every real prefix of every core family is invalidated exactly once (families share some prefixes).
        Assert.Equal(
            AnalyticsCachePolicy.CoreFamilies
                .SelectMany(AnalyticsCachePolicy.ResolveFamilyPrefixes)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static prefix => prefix, StringComparer.Ordinal)
                .ToArray(),
            harness.Cache.RemovedPrefixes.OrderBy(static prefix => prefix, StringComparer.Ordinal).ToArray());
        Assert.Contains("analytics:shoe-type-sales-stats:", harness.Cache.RemovedPrefixes);
        Assert.Contains("analytics:supplier-sales-stats:", harness.Cache.RemovedPrefixes);
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

    [PostgresRequiredTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunNightlyRefreshAsync_RecordsIntelligenceCacheGenerationForSameAndSeparateDatabase(bool separateAnalyticsDatabase)
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var trendplusConnectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"rq624_worker_trend_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(trendplusConnectionString));
        var analyticsConnectionString = separateAnalyticsDatabase
            ? await _fixture.TryCreateDatabaseConnectionStringAsync($"rq624_worker_analytics_{Guid.NewGuid():N}")
            : trendplusConnectionString;
        Assert.False(string.IsNullOrWhiteSpace(analyticsConnectionString));

        const string coreView = "public.rq624_worker_core_mv";
        const string cacheName = "analytics_intel.mv_product_demand_signals_v1_cache";
        await CreateMaterializedViewAsync(trendplusConnectionString!, "rq624_worker_core_mv");
        await CreateIntelligenceCacheAsync(analyticsConnectionString!, cacheName);
        if (separateAnalyticsDatabase)
        {
            await CreateMaterializedViewAsync(analyticsConnectionString!, "rq624_worker_core_mv");
        }

        await using (var mutateSource = new NpgsqlConnection(analyticsConnectionString))
        {
            await mutateSource.OpenAsync();
            await using var update = new NpgsqlCommand(
                "UPDATE analytics_intel.rq624_source_probe SET source_value = 2;",
                mutateSource);
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }

        await using var harness = CreateHarness(
            trendplusConnectionString!,
            [coreView],
            analyticsConnectionString,
            [cacheName]);
        await InvokeRefreshAsync(harness.Worker);

        await using var analyticsConnection = new NpgsqlConnection(analyticsConnectionString);
        await analyticsConnection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT last_refresh_status,
                   last_successful_as_of_date_utc,
                   last_successful_refresh_at_utc,
                   last_successful_generation_id,
                   last_successful_refresh_source,
                   expected_script_hash,
                   (SELECT source_value FROM analytics_intel.mv_product_demand_signals_v1_cache),
                   (SELECT MAX(date)::date FROM analytics_intel.mv_product_demand_signals_v1_cache)
            FROM analytics_intel.cache_refresh_state
            WHERE cache_name = 'analytics_intel.mv_product_demand_signals_v1_cache';
            """, analyticsConnection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("succeeded", reader.GetString(0));
        Assert.False(reader.IsDBNull(1));
        Assert.False(reader.IsDBNull(2));
        Assert.False(reader.IsDBNull(3));
        Assert.Equal("worker", reader.GetString(4));
        Assert.Equal("stable-hash", reader.GetString(5));
        Assert.Equal(2, reader.GetInt32(6));
        Assert.Equal(DateOnly.FromDateTime(reader.GetDateTime(1)), DateOnly.FromDateTime(reader.GetDateTime(7)));
    }

    [PostgresRequiredFact]
    public async Task RunNightlyRefreshAsync_RecordsFailedIntelligenceAttemptAndPreservesLastSuccess()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"rq624_worker_failed_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        const string coreView = "public.rq624_worker_failed_core_mv";
        const string missingCache = "analytics_intel.mv_product_demand_signals_v1_cache";
        await CreateMaterializedViewAsync(connectionString!, "rq624_worker_failed_core_mv");
        await CreateIntelligenceCacheStateAsync(connectionString!, missingCache);

        var previousGenerationId = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var seedState = new NpgsqlCommand("""
                UPDATE analytics_intel.cache_refresh_state
                SET last_successful_as_of_date_utc = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date - 1,
                    last_successful_generation_id = @generationId,
                    last_successful_refresh_source = 'startup',
                    last_successful_refresh_at_utc = clock_timestamp()
                WHERE cache_name = 'analytics_intel.mv_product_demand_signals_v1_cache';
                """, connection);
            seedState.Parameters.AddWithValue("generationId", previousGenerationId);
            Assert.Equal(1, await seedState.ExecuteNonQueryAsync());
        }

        await using var harness = CreateHarness(
            connectionString!,
            [coreView],
            intelligenceMaterializedViews: [missingCache]);
        await InvokeRefreshAsync(harness.Worker);

        await using var verifyConnection = new NpgsqlConnection(connectionString);
        await verifyConnection.OpenAsync();
        await using var verify = new NpgsqlCommand("""
            SELECT last_refresh_status,
                   last_refresh_failure_code,
                   last_successful_generation_id,
                   last_successful_as_of_date_utc
            FROM analytics_intel.cache_refresh_state
            WHERE cache_name = 'analytics_intel.mv_product_demand_signals_v1_cache';
            """, verifyConnection);
        await using var reader = await verify.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("failed", reader.GetString(0));
        Assert.Equal("materialized_view_missing", reader.GetString(1));
        Assert.Equal(previousGenerationId, reader.GetGuid(2));
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1)), DateOnly.FromDateTime(reader.GetDateTime(3)));
    }

    [PostgresRequiredFact]
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

        Assert.Equal(
            AnalyticsCachePolicy.CoreFamilies
                .SelectMany(AnalyticsCachePolicy.ResolveFamilyPrefixes)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            harness.Cache.RemovedPrefixes.Count);
        Assert.Contains(
            AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.DashboardFamily),
            harness.Cache.RemovedPrefixes);
        Assert.Contains(
            AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.ReportsFamily),
            harness.Cache.RemovedPrefixes);
    }

    [PostgresRequiredFact]
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

    private static async Task CreateIntelligenceCacheAsync(string connectionString, string cacheName)
    {
        await CreateIntelligenceCacheStateAsync(connectionString, cacheName);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            CREATE TABLE analytics_intel.rq624_source_probe (
                signal_date date NOT NULL,
                source_value integer NOT NULL);
            INSERT INTO analytics_intel.rq624_source_probe (signal_date, source_value)
            VALUES ((CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date, 1);
            CREATE MATERIALIZED VIEW analytics_intel.mv_product_demand_signals_v1_cache AS
            SELECT signal_date AS date, source_value
            FROM analytics_intel.rq624_source_probe;
            """, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CreateIntelligenceCacheStateAsync(string connectionString, string cacheName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            CREATE SCHEMA IF NOT EXISTS analytics_intel;
            CREATE TABLE IF NOT EXISTS analytics_intel.cache_refresh_state (
                cache_name text PRIMARY KEY,
                sql_file_path varchar(512) NOT NULL,
                expected_script_hash varchar(64) DEFAULT 'stable-hash',
                metadata_version smallint NOT NULL DEFAULT 1,
                last_refresh_attempt_at_utc timestamptz,
                last_refresh_status text NOT NULL DEFAULT 'unknown',
                last_refresh_failure_code text,
                last_refreshed_at_utc timestamptz,
                last_successful_refresh_at_utc timestamptz,
                last_successful_as_of_date_utc date,
                last_successful_generation_id uuid,
                last_successful_run_id bigint,
                last_successful_refresh_source text,
                updated_at_utc timestamptz NOT NULL DEFAULT clock_timestamp()
            );
            INSERT INTO analytics_intel.cache_refresh_state (cache_name, sql_file_path, last_refresh_status)
            VALUES (
                @cacheName,
                'Database/Analytics/Intelligence/021_product_demand_signals_v1.sql',
                'unknown');
            """, connection);
        command.Parameters.AddWithValue("cacheName", cacheName);
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
        IReadOnlyCollection<string> materializedViews,
        string? analyticsConnectionString = null,
        IReadOnlyCollection<string>? intelligenceMaterializedViews = null)
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
        services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(analyticsConnectionString ?? connectionString));
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
                IntelligenceMaterializedViewsToRefresh = intelligenceMaterializedViews?.ToList() ?? [],
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
