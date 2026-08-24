using System.Reflection;
using System.Runtime.ExceptionServices;
using Application.Analytics.Queries.GetInventoryForecast;
using Infrastructure.DbContexts;
using Infrastructure.Seed;
using Infrastructure.Services;
using Infrastructure.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests;

public sealed class DatabaseInitializerP0IntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public DatabaseInitializerP0IntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveSnapshotAsync_PersistsHistoryRow_WhenCancellationTokenIsPassedSeparately()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var trendConnectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_execsql_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(trendConnectionString))
        {
            return;
        }

        await using var trendDb = CreateTrendDbContext(trendConnectionString);
        await trendDb.Database.EnsureCreatedAsync();

        var service = new AnalyticsDataQualityHistoryService(trendDb);
        var snapshot = new AnalyticsDataQualityHealthSnapshot
        {
            GeneratedAtUtc = DateTime.UtcNow,
            LookbackDays = 30,
            WindowFromUtc = DateTime.UtcNow.AddDays(-30),
            WindowToUtc = DateTime.UtcNow,
            OrphanArticleCount = 2,
            TotalRevenue = 1000m,
            MissingCostRevenue = 100m,
            MissingCostRevenueSharePct = 10.0,
            UnknownSupplierRevenue = 50m,
            UnknownSupplierRevenueSharePct = 5.0
        };

        await service.SaveSnapshotAsync(snapshot, "all", CancellationToken.None);

        await using var verifyConnection = new NpgsqlConnection(trendConnectionString);
        await verifyConnection.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand(
            """
            SELECT COUNT(*)
            FROM analytics_data_quality_history
            WHERE snapshot_date_utc = @snapshotDate
              AND lookback_days = @lookbackDays
              AND data_scope = @dataScope;
            """,
            verifyConnection);
        verifyCommand.Parameters.AddWithValue("snapshotDate", snapshot.GeneratedAtUtc.Date);
        verifyCommand.Parameters.AddWithValue("lookbackDays", snapshot.LookbackDays);
        verifyCommand.Parameters.AddWithValue("dataScope", "all");

        var count = (long)(await verifyCommand.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task ForecastMaterializer_PersistsTrustedSnapshot_AndPairsObservedEvidence()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var analyticsConnectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_forecast_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(analyticsConnectionString))
        {
            return;
        }

        ResetForecastSchemaGuard();

        await using var analyticsDb = CreateAnalyticsDbContext(analyticsConnectionString);
        await analyticsDb.Database.EnsureCreatedAsync();

        var basisDate = new DateTime(2026, 8, 18);
        var issuedAtUtc = new DateTime(2026, 8, 21, 10, 15, 0, DateTimeKind.Utc);
        var updatedIssuedAtUtc = new DateTime(2026, 8, 21, 11, 30, 0, DateTimeKind.Utc);

        await using (var setupConnection = new NpgsqlConnection(analyticsConnectionString))
        {
            await setupConnection.OpenAsync();

            await using var setupCommand = new NpgsqlCommand(
                """
                CREATE SCHEMA IF NOT EXISTS analytics_intel;

                CREATE TABLE IF NOT EXISTS analytics_intel.inventory_observed_daily_snapshot (
                    article_id integer NOT NULL,
                    store_id integer NOT NULL,
                    snapshot_date date NOT NULL,
                    on_hand_qty numeric(18, 4) NOT NULL,
                    captured_at_utc timestamptz NOT NULL DEFAULT now(),
                    source_system text NOT NULL
                );

                CREATE OR REPLACE VIEW analytics_intel.vw_inventory_daily_stock_v1 AS
                SELECT
                    article_id,
                    store_id,
                    snapshot_date AS date,
                    on_hand_qty AS observed_qty,
                    NULL::numeric(18, 4) AS reconstructed_qty,
                    on_hand_qty AS stock_qty,
                    'observed'::text AS provenance,
                    captured_at_utc,
                    source_system
                FROM analytics_intel.inventory_observed_daily_snapshot;
                """,
                setupConnection);
            await setupCommand.ExecuteNonQueryAsync();

            await using var seedObservedCommand = new NpgsqlCommand(
                """
                INSERT INTO analytics_intel.inventory_observed_daily_snapshot (
                    article_id,
                    store_id,
                    snapshot_date,
                    on_hand_qty,
                    captured_at_utc,
                    source_system
                )
                VALUES (
                    @articleId,
                    @storeId,
                    @snapshotDate,
                    @onHandQty,
                    @capturedAtUtc,
                    @sourceSystem
                );
                """,
                setupConnection);
            seedObservedCommand.Parameters.AddWithValue("articleId", 301);
            seedObservedCommand.Parameters.AddWithValue("storeId", 7);
            seedObservedCommand.Parameters.AddWithValue("snapshotDate", basisDate.AddDays(7));
            seedObservedCommand.Parameters.AddWithValue("onHandQty", 9m);
            seedObservedCommand.Parameters.AddWithValue("capturedAtUtc", issuedAtUtc);
            seedObservedCommand.Parameters.AddWithValue("sourceSystem", "fixture");
            await seedObservedCommand.ExecuteNonQueryAsync();
        }

        var service = new InventoryForecastSnapshotMaterializerService(
            analyticsDb,
            NullLogger<InventoryForecastSnapshotMaterializerService>.Instance);

        var firstResult = await service.UpsertAsync(
            new InventoryForecastSnapshotMaterializationRequest(
                SkuId: 301,
                StoreId: 7,
                SupplierId: 19,
                SizeCode: "42",
                ForecastBasisDateUtc: basisDate,
                IssuedAtUtc: issuedAtUtc,
                MaterializerOwner: "forecast-worker",
                ProvenanceStatus: "trusted",
                SnapshotFreshnessUtc: issuedAtUtc,
                Forecast7d: 11m,
                Forecast14d: 8m,
                Forecast28d: 4m,
                ProbabilityOfOOSIn7d: 0.25m,
                OverstockRisk: 0.10m,
                ConfidenceScore: 0.82m,
                Explanation: "Trusted forecast snapshot"),
            CancellationToken.None);

        var secondResult = await service.UpsertAsync(
            new InventoryForecastSnapshotMaterializationRequest(
                SkuId: 301,
                StoreId: 7,
                SupplierId: 19,
                SizeCode: "42",
                ForecastBasisDateUtc: basisDate,
                IssuedAtUtc: updatedIssuedAtUtc,
                MaterializerOwner: "forecast-worker",
                ProvenanceStatus: "trusted",
                SnapshotFreshnessUtc: updatedIssuedAtUtc,
                Forecast7d: 12m,
                Forecast14d: 9m,
                Forecast28d: 5m,
                ProbabilityOfOOSIn7d: 0.35m,
                OverstockRisk: 0.15m,
                ConfidenceScore: 0.91m,
                Explanation: "Trusted forecast snapshot refreshed"),
            CancellationToken.None);

        Assert.True(firstResult.ForecastSnapshotId > 0);
        Assert.Equal(firstResult.ForecastSnapshotId, secondResult.ForecastSnapshotId);

        await using (var verifyConnection = new NpgsqlConnection(analyticsConnectionString))
        {
            await verifyConnection.OpenAsync();

            await using var verifySnapshotCommand = new NpgsqlCommand(
                """
                SELECT forecast_7d, forecast_14d, forecast_28d, issued_at_utc, provenance_status, materializer_owner
                FROM analytics_inventory_forecast_snapshot
                WHERE sku_id = @skuId
                  AND store_id = @storeId
                  AND supplier_id = @supplierId
                  AND size_code = @sizeCode
                  AND forecast_basis_date = @forecastBasisDate;
                """,
                verifyConnection);
            verifySnapshotCommand.Parameters.AddWithValue("skuId", 301);
            verifySnapshotCommand.Parameters.AddWithValue("storeId", 7);
            verifySnapshotCommand.Parameters.AddWithValue("supplierId", 19);
            verifySnapshotCommand.Parameters.AddWithValue("sizeCode", "42");
            verifySnapshotCommand.Parameters.AddWithValue("forecastBasisDate", basisDate);

            await using var snapshotReader = await verifySnapshotCommand.ExecuteReaderAsync();
            Assert.True(await snapshotReader.ReadAsync());
            Assert.Equal(12m, snapshotReader.GetDecimal(0));
            Assert.Equal(9m, snapshotReader.GetDecimal(1));
            Assert.Equal(5m, snapshotReader.GetDecimal(2));
            Assert.Equal(updatedIssuedAtUtc, snapshotReader.GetDateTime(3));
            Assert.Equal("trusted", snapshotReader.GetString(4));
            Assert.Equal("forecast-worker", snapshotReader.GetString(5));
            Assert.False(await snapshotReader.ReadAsync());
        }

        var pairs = await service.ListObservedPairingsAsync(
            new InventoryForecastObservedPairQuery(SkuId: 301, StoreId: 7, SupplierId: 19, SizeCode: "42"),
            CancellationToken.None);

        Assert.Equal(3, pairs.Count);

        var pairedObserved = Assert.Single(pairs, item => item.HorizonDays == 7);
        Assert.Equal("paired_observed", pairedObserved.PairingStatus);
        Assert.Equal(12m, pairedObserved.ForecastValue);
        Assert.Equal(9m, pairedObserved.ObservedQty);
        Assert.Equal(9m, pairedObserved.StockQty);
        Assert.Equal("observed", pairedObserved.ObservedProvenance);
        Assert.Equal("forecast-worker", pairedObserved.MaterializerOwner);
        Assert.Equal("trusted", pairedObserved.ProvenanceStatus);
        Assert.Equal(updatedIssuedAtUtc, pairedObserved.IssuedAtUtc);
        Assert.Equal(basisDate.AddDays(7), pairedObserved.ObservedDate);

        var missingObserved = Assert.Single(pairs, item => item.HorizonDays == 14);
        Assert.Equal("missing_observed_window", missingObserved.PairingStatus);
        Assert.Null(missingObserved.ObservedQty);
        Assert.Null(missingObserved.StockQty);
    }

    [Fact]
    public async Task ForecastMaterializer_StaleAndMismatchedScopesRemainUnpaired()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var analyticsConnectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_forecast_scope_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(analyticsConnectionString))
        {
            return;
        }

        ResetForecastSchemaGuard();

        await using var analyticsDb = CreateAnalyticsDbContext(analyticsConnectionString);
        await analyticsDb.Database.EnsureCreatedAsync();

        var basisDate = new DateTime(2026, 8, 18);
        var observedAtUtc = new DateTime(2026, 8, 21, 10, 15, 0, DateTimeKind.Utc);
        var staleIssuedAtUtc = new DateTime(2026, 8, 21, 11, 30, 0, DateTimeKind.Utc);
        var mismatchedIssuedAtUtc = new DateTime(2026, 8, 21, 12, 30, 0, DateTimeKind.Utc);

        await using (var setupConnection = new NpgsqlConnection(analyticsConnectionString))
        {
            await setupConnection.OpenAsync();

            await using var setupCommand = new NpgsqlCommand(
                """
                CREATE SCHEMA IF NOT EXISTS analytics_intel;

                CREATE TABLE IF NOT EXISTS analytics_intel.inventory_observed_daily_snapshot (
                    article_id integer NOT NULL,
                    store_id integer NOT NULL,
                    snapshot_date date NOT NULL,
                    on_hand_qty numeric(18, 4) NOT NULL,
                    captured_at_utc timestamptz NOT NULL DEFAULT now(),
                    source_system text NOT NULL
                );

                CREATE OR REPLACE VIEW analytics_intel.vw_inventory_daily_stock_v1 AS
                SELECT
                    article_id,
                    store_id,
                    snapshot_date AS date,
                    on_hand_qty AS observed_qty,
                    NULL::numeric(18, 4) AS reconstructed_qty,
                    on_hand_qty AS stock_qty,
                    'observed'::text AS provenance,
                    captured_at_utc,
                    source_system
                FROM analytics_intel.inventory_observed_daily_snapshot;
                """,
                setupConnection);
            await setupCommand.ExecuteNonQueryAsync();

            await using var seedObservedCommand = new NpgsqlCommand(
                """
                INSERT INTO analytics_intel.inventory_observed_daily_snapshot (
                    article_id,
                    store_id,
                    snapshot_date,
                    on_hand_qty,
                    captured_at_utc,
                    source_system
                )
                VALUES (
                    @articleId,
                    @storeId,
                    @snapshotDate,
                    @onHandQty,
                    @capturedAtUtc,
                    @sourceSystem
                );
                """,
                setupConnection);

            seedObservedCommand.Parameters.AddWithValue("articleId", 401);
            seedObservedCommand.Parameters.AddWithValue("storeId", 7);
            seedObservedCommand.Parameters.AddWithValue("snapshotDate", basisDate.AddDays(7));
            seedObservedCommand.Parameters.AddWithValue("onHandQty", 13m);
            seedObservedCommand.Parameters.AddWithValue("capturedAtUtc", observedAtUtc);
            seedObservedCommand.Parameters.AddWithValue("sourceSystem", "fixture");
            await seedObservedCommand.ExecuteNonQueryAsync();
        }

        var service = new InventoryForecastSnapshotMaterializerService(
            analyticsDb,
            NullLogger<InventoryForecastSnapshotMaterializerService>.Instance);

        await service.UpsertAsync(
            new InventoryForecastSnapshotMaterializationRequest(
                SkuId: 401,
                StoreId: 7,
                SupplierId: 19,
                SizeCode: "42",
                ForecastBasisDateUtc: basisDate,
                IssuedAtUtc: staleIssuedAtUtc,
                MaterializerOwner: "forecast-worker",
                ProvenanceStatus: "stale",
                SnapshotFreshnessUtc: staleIssuedAtUtc,
                Forecast7d: 11m,
                Forecast14d: 8m,
                Forecast28d: 4m,
                ProbabilityOfOOSIn7d: 0.25m,
                OverstockRisk: 0.10m,
                ConfidenceScore: 0.82m,
                Explanation: "Stale forecast snapshot"),
            CancellationToken.None);

        await service.UpsertAsync(
            new InventoryForecastSnapshotMaterializationRequest(
                SkuId: 402,
                StoreId: 8,
                SupplierId: 19,
                SizeCode: "42",
                ForecastBasisDateUtc: basisDate,
                IssuedAtUtc: mismatchedIssuedAtUtc,
                MaterializerOwner: "forecast-worker",
                ProvenanceStatus: "trusted",
                SnapshotFreshnessUtc: mismatchedIssuedAtUtc,
                Forecast7d: 12m,
                Forecast14d: 9m,
                Forecast28d: 5m,
                ProbabilityOfOOSIn7d: 0.35m,
                OverstockRisk: 0.15m,
                ConfidenceScore: 0.91m,
                Explanation: "Trusted forecast snapshot for a different store"),
            CancellationToken.None);

        var stalePairs = await service.ListObservedPairingsAsync(
            new InventoryForecastObservedPairQuery(SkuId: 401, StoreId: 7, SupplierId: 19, SizeCode: "42"),
            CancellationToken.None);

        Assert.Equal(3, stalePairs.Count);
        var stalePair = Assert.Single(stalePairs, item => item.HorizonDays == 7);
        Assert.Equal("stale", stalePair.ProvenanceStatus);
        Assert.Equal("stale", stalePair.PairingStatus);
        Assert.Equal(13m, stalePair.ObservedQty);
        Assert.Equal(13m, stalePair.StockQty);
        Assert.NotEqual("paired_observed", stalePair.PairingStatus);

        var mismatchedPairs = await service.ListObservedPairingsAsync(
            new InventoryForecastObservedPairQuery(SkuId: 402, StoreId: 8, SupplierId: 19, SizeCode: "42"),
            CancellationToken.None);

        Assert.Equal(3, mismatchedPairs.Count);
        var mismatchedPair = Assert.Single(mismatchedPairs, item => item.HorizonDays == 7);
        Assert.Equal("trusted", mismatchedPair.ProvenanceStatus);
        Assert.Equal("missing_observed_window", mismatchedPair.PairingStatus);
        Assert.Null(mismatchedPair.ObservedQty);
        Assert.Null(mismatchedPair.StockQty);
    }

    [Fact]
    public async Task DeferredBackfillPattern_CreatesFreshDbContexts_AfterScopedContextsAreDisposed()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var trendConnectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_trend_{Guid.NewGuid():N}");
        var analyticsConnectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"tp_analytics_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(trendConnectionString) || string.IsNullOrWhiteSpace(analyticsConnectionString))
        {
            return;
        }

        await using (var scopedTrendDb = CreateTrendDbContext(trendConnectionString))
        await using (var scopedAnalyticsDb = CreateAnalyticsDbContext(analyticsConnectionString))
        {
            await scopedTrendDb.Database.EnsureCreatedAsync();
            await scopedAnalyticsDb.Database.EnsureCreatedAsync();
        }

        var createTrendDb = GetRequiredPrivateStaticMethod(
            typeof(DatabaseInitializer),
            "CreateTrendplusBackfillDbContext");
        var createAnalyticsDb = GetRequiredPrivateStaticMethod(
            typeof(DatabaseInitializer),
            "CreateAnalyticsBackfillDbContext");
        var backfillSales = GetRequiredPrivateStaticMethod(
            typeof(DatabaseInitializer),
            "BackfillSalesFactsAsync");
        var backfillReturns = GetRequiredPrivateStaticMethod(
            typeof(DatabaseInitializer),
            "BackfillReturnFactsAsync");

        await using var trendBackfillDb = (TrendplusDbContext)(createTrendDb.Invoke(
            null,
            new object[] { trendConnectionString, 30 }) ?? throw new InvalidOperationException("Trend backfill DbContext creation failed."));
        await using var analyticsBackfillDb = (AnalyticsDbContext)(createAnalyticsDb.Invoke(
            null,
            new object[] { analyticsConnectionString, 30 }) ?? throw new InvalidOperationException("Analytics backfill DbContext creation failed."));

        await InvokePrivateAsync(backfillSales, trendBackfillDb, analyticsBackfillDb, NullLogger.Instance);
        await InvokePrivateAsync(backfillReturns, trendBackfillDb, analyticsBackfillDb, NullLogger.Instance);

        Assert.Equal(0, await analyticsBackfillDb.SalesFacts.CountAsync());
        Assert.Equal(0, await analyticsBackfillDb.ReturnFacts.CountAsync());
    }

    private static TrendplusDbContext CreateTrendDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new TrendplusDbContext(options);
    }

    private static AnalyticsDbContext CreateAnalyticsDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AnalyticsDbContext(options);
    }

    private static MethodInfo GetRequiredPrivateStaticMethod(Type type, string methodName)
    {
        return type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Missing method {type.FullName}.{methodName}.");
    }

    private static async Task InvokePrivateAsync(MethodInfo method, params object[] args)
    {
        try
        {
            var result = method.Invoke(null, args)
                ?? throw new InvalidOperationException($"Method {method.Name} did not return a Task.");
            if (result is not Task task)
            {
                throw new InvalidOperationException($"Method {method.Name} did not return a Task.");
            }
            await task;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static void ResetForecastSchemaGuard()
    {
        var field = typeof(InventoryForecastSnapshotMaterializerService).GetField(
            "_schemaEnsured",
            BindingFlags.NonPublic | BindingFlags.Static);

        field?.SetValue(null, false);
    }
}

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public bool IsAvailable { get; private set; }

    public string AdminConnectionString => _container?.GetConnectionString() ?? string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("postgres")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _container.StartAsync();
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;

            if (string.Equals(
                    Environment.GetEnvironmentVariable("CI"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "PostgreSQL integration tests are mandatory in CI, but the Testcontainers fixture could not start.",
                    ex);
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public async Task<string?> TryCreateDatabaseConnectionStringAsync(string databaseName)
    {
        if (!IsAvailable)
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();

        await using (var dropCommand = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE);", connection))
        {
            await dropCommand.ExecuteNonQueryAsync();
        }

        await using (var createCommand = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\";", connection))
        {
            await createCommand.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Database = databaseName
        };
        return builder.ConnectionString;
    }
}
