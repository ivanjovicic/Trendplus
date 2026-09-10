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
using Npgsql;
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
        Assert.Equal("dashboard,supplier-decision-hub", state.LastClearFamily);
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

    [Trait("Category", "Integration")]
    [Fact]
    public async Task RefreshAnalyticsAsync_WhenAggregateTableIsMissing_DoesNotInvalidateCache()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_analytics_missing_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var harness = CreateHarness(connectionString!, useInMemoryDatabase: false);

        await InvokeRefreshAnalyticsAsync(harness.Worker);

        Assert.Empty(harness.Cache.RemovedPrefixes);

        var state = await harness.CacheAdmin.GetStateAsync(CancellationToken.None);
        Assert.Null(state.LastClearAtUtc);
        Assert.Null(state.LastClearFamily);
        Assert.Null(state.LastAnalyticsCacheClearAtUtc);
        Assert.Null(state.LastReportCacheClearAtUtc);
        Assert.Equal(1, state.ReportCacheVersion);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task AggregateReplacementTransaction_WhenInsertFails_RollsBackDelete()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_analytics_atomicity_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var harness = CreateHarness(connectionString!, useInMemoryDatabase: false);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var setup = new NpgsqlCommand(
            """
            CREATE TEMP TABLE "AggregateAtomicityProbe" ("Date" date PRIMARY KEY, "Value" integer);
            INSERT INTO "AggregateAtomicityProbe" ("Date", "Value") VALUES (@date::DATE, 7);
            """,
            connection))
        {
            setup.Parameters.AddWithValue("date", DateTime.UtcNow.Date);
            await setup.ExecuteNonQueryAsync();
        }

        var method = typeof(AnalyticsAggregationWorker).GetMethod(
            "ExecuteAggregateReplacementTransactionAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var replacementTask = (Task)method!.Invoke(
            harness.Worker,
            [
                connection,
                DateTime.UtcNow.Date,
                "DELETE FROM \"AggregateAtomicityProbe\" WHERE \"Date\" = @date::DATE;",
                "INSERT INTO \"AggregateAtomicityProbe\" (\"Date\", \"Value\") VALUES (@date::DATE, 1 / 0);",
                CancellationToken.None
            ])!;

        await Assert.ThrowsAsync<PostgresException>(async () => await replacementTask);

        await using var verify = new NpgsqlCommand(
            "SELECT \"Value\" FROM \"AggregateAtomicityProbe\" WHERE \"Date\" = @date::DATE;",
            connection);
        verify.Parameters.AddWithValue("date", DateTime.UtcNow.Date);
        Assert.Equal(7, await verify.ExecuteScalarAsync());
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task DimensionalAggregates_IncludeOrphanSalesLines_AndReconcileWithDailyTotal()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"tp_analytics_orphan_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var harness = CreateHarness(connectionString!, useInMemoryDatabase: false);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var setup = new NpgsqlCommand(
            """
            CREATE TABLE prodaja_zaglavlje (
                id bigint PRIMARY KEY,
                datum_prodaje timestamptz NOT NULL
            );
            CREATE TABLE prodaja_stavke (
                id bigint PRIMARY KEY,
                id_prodaja bigint NOT NULL,
                id_artikal bigint NOT NULL,
                kolicina numeric(18,2) NOT NULL,
                cena numeric(18,2) NOT NULL
            );
            CREATE TABLE ""Artikli"" (
                ""Id" bigint PRIMARY KEY,
                ""Kategorija" text NULL,
                ""Pol" text NULL,
                ""IDDobavljac" bigint NULL
            );
            CREATE TABLE ""Dobavljaci"" (
                ""Id" bigint PRIMARY KEY,
                ""Naziv" text NULL
            );
            CREATE TABLE ""AnalyticsDailySummary"" (
                ""Date" date PRIMARY KEY,
                ""TotalRevenue" numeric(18,2),
                ""TotalTransactions" bigint,
                ""TotalUnits" numeric(18,2),
                ""AvgBasketValue" numeric(18,2),
                ""AvgItemPrice" numeric(18,2),
                ""BasketStdDev" numeric(18,2),
                ""ItemPriceStdDev" numeric(18,2),
                ""EffectiveTransactionCount" numeric(18,2),
                ""DataConfidence" numeric(18,2),
                ""UpdatedAt" timestamptz
            );
            CREATE TABLE ""AnalyticsCategorySummary"" (
                ""Date" date NOT NULL,
                ""Kategorija" text NOT NULL,
                ""TotalRevenue" numeric(18,2),
                ""TotalUnits" numeric(18,2),
                ""TransactionCount" bigint,
                ""UpdatedAt" timestamptz
            );
            CREATE TABLE ""AnalyticsSupplierSummary"" (
                ""Date" date NOT NULL,
                ""DobavljacId" bigint NULL,
                ""DobavljacNaziv" text NOT NULL,
                ""TotalRevenue" numeric(18,2),
                ""TotalUnits" numeric(18,2),
                ""TransactionCount" bigint,
                ""UpdatedAt" timestamptz
            );
            CREATE TABLE ""AnalyticsGenderSummary"" (
                ""Date" date NOT NULL,
                ""Pol" text NOT NULL,
                ""TotalRevenue" numeric(18,2),
                ""TotalUnits" numeric(18,2),
                ""UpdatedAt" timestamptz
            );
            INSERT INTO prodaja_zaglavlje (id, datum_prodaje) VALUES (1, '2026-09-10T10:00:00Z');
            INSERT INTO prodaja_stavke (id, id_prodaja, id_artikal, kolicina, cena)
            VALUES (1, 1, 10, 2, 100), (2, 1, 999, 3, 50);
            INSERT INTO ""Artikli"" (""Id"", ""Kategorija"", ""Pol"", ""IDDobavljac"")
            VALUES (10, 'Obuca', 'M', 20);
            INSERT INTO ""Dobavljaci"" (""Id"", ""Naziv"") VALUES (20, 'Dobavljac A');
            """,
            connection))
        {
            await setup.ExecuteNonQueryAsync();
        }

        var date = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(await InvokeAggregateAsync(harness.Worker, "RefreshDailySummaryAsync", connection, date));
        Assert.True(await InvokeAggregateAsync(harness.Worker, "RefreshCategorySummaryAsync", connection, date));
        Assert.True(await InvokeAggregateAsync(harness.Worker, "RefreshSupplierSummaryAsync", connection, date));
        Assert.True(await InvokeAggregateAsync(harness.Worker, "RefreshGenderSummaryAsync", connection, date));

        const decimal expectedRevenue = 350m;
        Assert.Equal(expectedRevenue, await ReadDecimalAsync(connection, "SELECT \"TotalRevenue\" FROM \"AnalyticsDailySummary\" WHERE \"Date\" = DATE '2026-09-10';"));
        Assert.Equal(expectedRevenue, await ReadDecimalAsync(connection, "SELECT SUM(\"TotalRevenue\") FROM \"AnalyticsCategorySummary\" WHERE \"Date\" = DATE '2026-09-10';"));
        Assert.Equal(expectedRevenue, await ReadDecimalAsync(connection, "SELECT SUM(\"TotalRevenue\") FROM \"AnalyticsSupplierSummary\" WHERE \"Date\" = DATE '2026-09-10';"));
        Assert.Equal(expectedRevenue, await ReadDecimalAsync(connection, "SELECT SUM(\"TotalRevenue\") FROM \"AnalyticsGenderSummary\" WHERE \"Date\" = DATE '2026-09-10';"));
        Assert.Equal("Nepoznato", await ReadStringAsync(connection, "SELECT \"Kategorija\" FROM \"AnalyticsCategorySummary\" WHERE \"Date\" = DATE '2026-09-10' AND \"TotalRevenue\" = 150;"));
        Assert.Equal("Nepoznato", await ReadStringAsync(connection, "SELECT \"DobavljacNaziv\" FROM \"AnalyticsSupplierSummary\" WHERE \"Date\" = DATE '2026-09-10' AND \"TotalRevenue\" = 150;"));
        Assert.Equal("Neodređeno", await ReadStringAsync(connection, "SELECT \"Pol\" FROM \"AnalyticsGenderSummary\" WHERE \"Date\" = DATE '2026-09-10' AND \"TotalRevenue\" = 150;"));
    }

    private static List<string> ExpectedRemovedPrefixes() =>
    [
        AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.DashboardFamily),
        AnalyticsCachePolicy.ResolveFamilyPrefix(AnalyticsCachePolicy.SupplierDecisionHubFamily),
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

    private static async Task<bool> InvokeAggregateAsync(
        AnalyticsAggregationWorker worker,
        string methodName,
        NpgsqlConnection connection,
        DateTime date)
    {
        var method = typeof(AnalyticsAggregationWorker).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = (Task<bool>)method!.Invoke(worker, [connection, date, CancellationToken.None])!;
        return await task;
    }

    private static async Task<decimal> ReadDecimalAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ReadStringAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (string)(await command.ExecuteScalarAsync())!;
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
