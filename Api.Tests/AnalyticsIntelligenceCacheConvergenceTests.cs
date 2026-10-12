using Infrastructure.Seed;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NpgsqlTypes;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Collection(IntelligencePostgresCollection.Name)]
public sealed class AnalyticsIntelligenceCacheConvergenceTests
{
    private static readonly (string SqlFilePath, string[] RequiredRelations)[] CacheScripts =
    [
        (
            "Database/Analytics/Intelligence/021_product_demand_signals_v1.sql",
            ["analytics_intel.vw_product_demand_signals_v1", "analytics_intel.mv_product_demand_signals_v1_cache"]
        ),
        (
            "Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql",
            ["analytics_intel.vw_inventory_risk_signals_v1", "analytics_intel.mv_inventory_risk_signals_v1_cache"]
        ),
        (
            "Database/Analytics/Intelligence/024_trend_momentum_v1.sql",
            ["analytics_intel.vw_trend_momentum_v1", "analytics_intel.mv_trend_momentum_v1_cache"]
        )
    ];

    private static readonly (string View, string Cache)[] CachePairs =
    [
        ("analytics_intel.vw_product_demand_signals_v1", "analytics_intel.mv_product_demand_signals_v1_cache"),
        ("analytics_intel.vw_inventory_risk_signals_v1", "analytics_intel.mv_inventory_risk_signals_v1_cache"),
        ("analytics_intel.vw_trend_momentum_v1", "analytics_intel.mv_trend_momentum_v1_cache")
    ];

    private readonly IntelligencePostgresFixture _fixture;

    public AnalyticsIntelligenceCacheConvergenceTests(IntelligencePostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ChangedSaleJoinSql_RefreshesExistingDemandRiskAndTrendCaches()
    {
        var database = await CreatePreR613CacheDatabaseAsync("parity");
        await using var connection = await _fixture.OpenConnectionAsync(database);

        foreach (var (view, cache) in CachePairs)
            Assert.Equal(0L, await DifferenceCountAsync(connection, view, cache));

        var generationId = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            CacheScripts);
        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            CacheScripts,
            generationId);

        foreach (var (view, cache) in CachePairs)
            Assert.Equal(0L, await DifferenceCountAsync(connection, view, cache));

        await AssertCorrectDemandStoreDateAndQuantityAsync(connection);
        await AssertInventoryDateAndQuantityAsync(connection);
        await AssertTrendDateAsync(connection);
        await AssertStableOutputColumnsAsync(connection);
        await AssertAllCachesReadyForCurrentSqlAsync(connection);

        var refreshedAt = await GetRefreshTimesAsync(connection);
        var repeatGeneration = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            CacheScripts);
        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            CacheScripts,
            repeatGeneration);
        Assert.Equal(refreshedAt, await GetRefreshTimesAsync(connection));
    }

    [Fact]
    public async Task AsOfInputs_ExcludeFutureSalesMovementsAndTrendHistoryFromChangedHashCaches()
    {
        var database = await CreatePreR613CacheDatabaseAsync("asof");
        await using var connection = await _fixture.OpenConnectionAsync(database);
        await ExecuteAsync(connection, "SET TIME ZONE 'Pacific/Kiritimati';");

        // Use the database's UTC as-of date, then construct the boundary fixtures explicitly.
        // The oracle below derives inclusion from date membership and source keys, independently
        // of the production SQL's timestamp predicates.
        await using var dateCommand = new NpgsqlCommand(
            "SELECT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date::timestamp;",
            connection);
        var utcToday = DateTime.SpecifyKind((DateTime)(await dateCommand.ExecuteScalarAsync())!, DateTimeKind.Utc);
        var utcYesterdayEnd = utcToday.AddMilliseconds(-1);
        var utcTodayEnd = utcToday.AddDays(1).AddMilliseconds(-1);
        var utcTomorrow = utcToday.AddDays(1);
        var utcDay30 = utcToday.AddDays(30);
        var utcDay30End = utcDay30.AddDays(1).AddMilliseconds(-1);

        await using (var seed = new NpgsqlCommand("""
            INSERT INTO "SalesFacts" ("Id", "SaleId", "StoreId", "SaleTimestampUtc", "BrojRacuna", "DataOrigin")
            VALUES
                (80001, 81001, 7, @todayEnd, 'R-ASOF-TODAY', 'demo'),
                (80002, 81002, 7, @todayEnd, 'R-ASOF-RETURN', 'access'),
                (80003, 81003, 7, @tomorrow, 'R-FUTURE-TOMORROW', 'existing'),
                (81001, 81101, 7, @day30End, 'R-ID-COLLISION-FUTURE', 'existing'),
                (80004, 81004, 7, @day30End, 'R-FUTURE-30', 'access'),
                (81005, 81005, 19, @tomorrow, 'R-FUTURE-ONLY', 'demo'),
                (81006, 81006, 19, @day30End, 'R-FUTURE-ONLY-30', 'access'),
                (80007, 81007, 7, @todayEnd, '  dUg  ', 'existing'),
                (80008, 81008, 7, @todayEnd, ' KoReKcIjA ', 'demo');

            INSERT INTO "SalesLineFacts" ("SaleId", "ProductId", "Qty")
            VALUES
                (81001, 42, 5),
                (81002, 42, -2),
                (81003, 42, 100),
                (81101, 42, 777),
                (81004, 42, 1000),
                (81005, 8101, 7),
                (81006, 8101, 11),
                (81007, 42, 900),
                (81008, 42, 800),
                (999999, 42, 9999);

            INSERT INTO "ProductsDim" ("ProductId", "Timestamp", "Kolicina")
            VALUES (905, @todayEnd, 0), (906, @todayEnd, NULL);

            INSERT INTO "InventoryMovementFacts" ("ArtikalId", "Datum", "TipPromene", "Kolicina")
            VALUES
                (42, @yesterdayEnd, 'Povrat kupca', 1),
                (42, @todayEnd, 'Ulaz robe', 2),
                (42, @tomorrow, 'Prenos ulaz', 100),
                (42, @day30End, 'Prenos izlaz', 200),
                (8202, @tomorrow, 'Ulaz robe', 10),
                (8202, @day30End, 'Prenos ulaz', 20);

            INSERT INTO "TrendHistory" ("LocalProductId", "FinalGlobalScore", "Date", "CreatedAt")
            VALUES
                (42, 79, @todayEnd, @todayEnd),
                (42, 99, @tomorrow, @tomorrow),
                (42, 100, @day30End, @day30End),
                (8101, 99, @tomorrow, @tomorrow),
                (8101, 100, @day30End, @day30End),
                (8103, 88, @tomorrow, @tomorrow),
                (8103, 100, @day30End, @day30End);
            """, connection))
        {
            seed.Parameters.AddWithValue("todayEnd", utcTodayEnd);
            seed.Parameters.AddWithValue("yesterdayEnd", utcYesterdayEnd);
            seed.Parameters.AddWithValue("tomorrow", utcTomorrow);
            seed.Parameters.AddWithValue("day30End", utcDay30End);
            await seed.ExecuteNonQueryAsync();
        }

        // Pre-fix view assertions prove today's bad population: future-only source rows enter
        // observed_pairs/observed_products, and a future TrendHistory score wins latest-row order.
        Assert.True(await ScalarLongAsync(connection, """
            SELECT COUNT(*) FROM analytics_intel.vw_product_demand_signals_v1 WHERE article_id = 8101;
            """) > 0);
        Assert.True(await ScalarLongAsync(connection, """
            SELECT COUNT(*) FROM analytics_intel.vw_inventory_risk_signals_v1 WHERE article_id = 8202;
            """) > 0);
        Assert.True(await ScalarLongAsync(connection, """
            SELECT COUNT(*) FROM analytics_intel.vw_inventory_risk_signals_v1 WHERE article_id = 8101;
            """) > 0);
        Assert.True(await ScalarLongAsync(connection, """
            SELECT COUNT(*) FROM analytics_intel.vw_trend_momentum_v1 WHERE article_id = 8103;
            """) > 0);
        Assert.Equal(100m, await ScalarDecimalAsync(connection, """
            SELECT external_trend_score FROM analytics_intel.vw_trend_momentum_v1 WHERE article_id = 42;
            """));

        // This is an existing populated cache built from an old SQL hash. A correction must
        // replace the view definition and refresh that cache as one successful generation.
        var oldState = await GetCacheStateSnapshotAsync(connection);
        var generationId = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            CacheScripts);
        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            CacheScripts,
            generationId);

        var allOriginHeaders = new[]
        {
            (SaleId: 50001, ArticleId: 42, StoreId: 7, Day: DateOnly.FromDateTime(utcToday.AddDays(-1)), Receipt: "R-BASE", Quantity: 3),
            (SaleId: 81001, ArticleId: 42, StoreId: 7, Day: DateOnly.FromDateTime(utcToday), Receipt: "R-ASOF-TODAY", Quantity: 5),
            (SaleId: 81002, ArticleId: 42, StoreId: 7, Day: DateOnly.FromDateTime(utcToday), Receipt: "R-ASOF-RETURN", Quantity: -2),
        };
        var expectedVelocity = allOriginHeaders
            .Where(row => row.Day <= DateOnly.FromDateTime(utcToday)
                && row.Receipt is not "DUG" and not "KOREKCIJA")
            .Sum(row => row.Quantity);
        Assert.Equal(6, expectedVelocity); // independent all-origin date/receipt oracle

        var asOfDate = DateOnly.FromDateTime(utcToday);
        var netByDay = allOriginHeaders
            .Where(row => row.Day >= asOfDate.AddDays(-28) && row.Day <= asOfDate)
            .GroupBy(row => row.Day)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Quantity));
        var netUnits = Enumerable.Range(0, 29)
            .Select(day => netByDay.GetValueOrDefault(asOfDate.AddDays(day - 28)))
            .ToArray();
        var averageX = 14d;
        var averageY = netUnits.Average();
        var expectedAcceleration = decimal.Round((decimal)(
            Enumerable.Range(0, 29).Sum(day => (day - averageX) * (netUnits[day] - averageY))
            / Enumerable.Range(0, 29).Sum(day => Math.Pow(day - averageX, 2))), 6);
        var grossByDay = allOriginHeaders
            .Where(row => row.Day >= asOfDate.AddDays(-28) && row.Day <= asOfDate && row.Quantity > 0)
            .GroupBy(row => row.Day)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Quantity));
        var grossDailyUnits = Enumerable.Range(0, 29)
            .Select(day => (decimal)grossByDay.GetValueOrDefault(asOfDate.AddDays(day - 28)))
            .Where(units => units > 0)
            .ToArray();
        var grossTotal = grossDailyUnits.Sum();
        var expectedEntropy = decimal.Round((decimal)(-
            grossDailyUnits.Sum(units =>
            {
                var share = (double)(units / grossTotal);
                return share * Math.Log(share);
            }) / Math.Log(grossDailyUnits.Length)), 6);

        await using (var actual = new NpgsqlCommand("""
            SELECT sales_velocity, source_rows
            FROM analytics_intel.vw_product_demand_signals_v1
            WHERE article_id = 42 AND store_id = 7 AND date = @asOf;
            """, connection))
        {
            actual.Parameters.AddWithValue("asOf", DateOnly.FromDateTime(utcToday));
            await using var reader = await actual.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(expectedVelocity, reader.GetInt32(0));
            Assert.Equal(3, reader.GetInt32(1)); // yesterday plus two same-day rows; orphan and excluded receipts absent
        }

        Assert.Equal(0L, await ScalarLongAsync(connection, """
            SELECT COUNT(*) FROM analytics_intel.vw_product_demand_signals_v1 WHERE article_id = 8101;
            """));
        Assert.Equal(0L, await ScalarLongAsync(connection, """
            SELECT COUNT(*) FROM analytics_intel.vw_inventory_risk_signals_v1 WHERE article_id IN (8101, 8202);
            """));
        Assert.Equal(0L, await ScalarLongAsync(connection, """
            SELECT COUNT(*) FROM analytics_intel.vw_trend_momentum_v1 WHERE article_id IN (8101, 8103);
            """));
        Assert.Equal(79m, await ScalarDecimalAsync(connection, """
            SELECT external_trend_score FROM analytics_intel.vw_trend_momentum_v1 WHERE article_id = 42;
            """));
        await using (var trend = new NpgsqlCommand("""
            SELECT local_sales_acceleration, trend_entropy
            FROM analytics_intel.vw_trend_momentum_v1 WHERE article_id = 42;
            """, connection))
        {
            await using var reader = await trend.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(expectedAcceleration, reader.GetDecimal(0));
            Assert.Equal(expectedEntropy, reader.GetDecimal(1));
        }
        Assert.Equal(0.2m, await ScalarDecimalAsync(connection, """
            SELECT avg_daily_sales_30d FROM analytics_intel.vw_inventory_risk_signals_v1
            WHERE article_id = 42 AND date = @asOf;
            """, asOfDate));
        Assert.Equal(0m, await ScalarDecimalAsync(connection, """
            SELECT stock_qty FROM analytics_intel.vw_inventory_risk_signals_v1
            WHERE article_id = 905 AND date = @asOf;
            """, DateOnly.FromDateTime(utcToday)));
        Assert.True(await ScalarIsNullAsync(connection, """
            SELECT stock_qty FROM analytics_intel.vw_inventory_risk_signals_v1
            WHERE article_id = 906 AND date = @asOf;
            """, DateOnly.FromDateTime(utcToday)));

        foreach (var (view, cache) in CachePairs)
            Assert.Equal(0L, await DifferenceCountAsync(connection, view, cache));
        Assert.NotEqual(
            string.Join("\n", oldState),
            string.Join("\n", await GetCacheStateSnapshotAsync(connection)));
        await AssertAllCachesReadyForCurrentSqlAsync(connection);

        // The changed-hash failure path must still fail closed at the HTTP boundary.
        await using (var staleHash = new NpgsqlCommand("""
            UPDATE "__StartupSqlScriptHistory"
            SET "ScriptHash" = repeat('0', 64)
            WHERE "ScriptPath" = @path;
            """, connection))
        {
            staleHash.Parameters.AddWithValue("path", CacheScripts[0].SqlFilePath);
            await staleHash.ExecuteNonQueryAsync();
        }
        var failedGeneration = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            CacheScripts);
        await using (var failure = new NpgsqlCommand("ALTER TABLE \"SalesFacts\" RENAME COLUMN \"SaleId\" TO \"UnavailableSaleId\";", connection))
            await failure.ExecuteNonQueryAsync();
        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            CacheScripts,
            failedGeneration);
        var httpResult = await AnalyticsIntelligenceEndpoints.ExecuteCacheQueryAsync(
            () => AnalyticsIntelligenceEndpoints.QueryDemandSignalsAsync(
                database.ConnectionString,
                new DemandSignalsRequest(null, 1, 42, null, null, null, null, null, 1, 5, null, null),
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(httpResult).StatusCode);
    }

    [Fact]
    public async Task ChangedViewSchema_RebuildsCacheWithoutASecondRefresh()
    {
        var database = await CreatePreR613CacheDatabaseAsync("schema");
        await using var connection = await _fixture.OpenConnectionAsync(database);

        await ExecuteAsync(connection, """
            DROP MATERIALIZED VIEW analytics_intel.mv_product_demand_signals_v1_cache;
            DROP VIEW analytics_intel.vw_product_demand_signals_v1;
            CREATE VIEW analytics_intel.vw_product_demand_signals_v1 AS SELECT 1 AS stale_schema;
            """);

        var generationId = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            CacheScripts);
        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            CacheScripts,
            generationId);

        await AssertColumnNamesAsync(
            connection,
            "analytics_intel.vw_product_demand_signals_v1",
            [
                "article_id", "store_id", "date", "sales_velocity", "demand_acceleration",
                "demand_state", "days_since_last_sale", "launch_age_days", "store_coverage", "source_rows"
            ]);
        Assert.Equal(
            0L,
            await DifferenceCountAsync(
                connection,
                "analytics_intel.vw_product_demand_signals_v1",
                "analytics_intel.mv_product_demand_signals_v1_cache"));
        await AssertAllCachesReadyForCurrentSqlAsync(connection);
    }

    [Fact]
    public async Task AdvisoryLockContention_LeavesCachePendingThenRetriesWithoutNightlyWorker()
    {
        var database = await CreatePreR613CacheDatabaseAsync("lock");
        await using var connection = await _fixture.OpenConnectionAsync(database);
        var generationId = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            CacheScripts);

        await using (var lockCommand = new NpgsqlCommand("SELECT pg_try_advisory_lock(987654323);", connection))
            Assert.True((bool)(await lockCommand.ExecuteScalarAsync())!);

        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            CacheScripts,
            generationId);

        Assert.Equal("pending", await GetBuildStateAsync(connection, CachePairs[0].Cache));
        await AssertDemandCacheUnavailableAsync(database.ConnectionString);

        await using (var releaseCommand = new NpgsqlCommand("SELECT pg_advisory_unlock(987654323);", connection))
            Assert.True((bool)(await releaseCommand.ExecuteScalarAsync())!);

        var retryGeneration = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            CacheScripts);
        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            CacheScripts,
            retryGeneration);

        Assert.Equal("ready", await GetBuildStateAsync(connection, CachePairs[0].Cache));
        foreach (var (view, cache) in CachePairs)
            Assert.Equal(0L, await DifferenceCountAsync(connection, view, cache));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompetingStartupPreparation_CannotOrphanTheWinningBuildGeneration(bool reverseStartupOrder)
    {
        var database = await CreatePreR613CacheDatabaseAsync("competing_startup");
        await using var connection = await _fixture.OpenConnectionAsync(database);
        var stateBeforeContendedStartups = await GetCacheStateSnapshotAsync(connection);

        // Hold the real database lock while two independent startup tasks contend. Neither task
        // may publish a pending generation before it owns the lock.
        await using (var lockCommand = new NpgsqlCommand("SELECT pg_try_advisory_lock(987654323);", connection))
            Assert.True((bool)(await lockCommand.ExecuteScalarAsync())!);

        var startupA = () => DatabaseInitializer.RunAnalyticsIntelligenceCacheStartupBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            CacheScripts);
        var startupB = () => DatabaseInitializer.RunAnalyticsIntelligenceCacheStartupBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            CacheScripts);
        var first = reverseStartupOrder ? startupB() : startupA();
        var second = reverseStartupOrder ? startupA() : startupB();

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.Equal(stateBeforeContendedStartups, await GetCacheStateSnapshotAsync(connection));

        await using (var releaseCommand = new NpgsqlCommand("SELECT pg_advisory_unlock(987654323);", connection))
            Assert.True((bool)(await releaseCommand.ExecuteScalarAsync())!);

        await Task.WhenAll(first, second);

        foreach (var (view, cache) in CachePairs)
        {
            Assert.Equal("ready", await GetBuildStateAsync(connection, cache));
            Assert.Equal(0L, await DifferenceCountAsync(connection, view, cache));
        }
    }

    [Fact]
    public async Task InterruptedOrFailedBuild_IsUnavailableAndPersistsSafeFailureState()
    {
        var database = await CreatePreR613CacheDatabaseAsync("failure");
        await using var connection = await _fixture.OpenConnectionAsync(database);
        var generationId = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            CacheScripts);

        // Simulate the host stopping after it records pending but before the background task runs.
        await AssertDemandCacheUnavailableAsync(database.ConnectionString);
        Assert.Equal("pending", await GetBuildStateAsync(connection, CachePairs[0].Cache));

        // A renamed source column makes the real SQL upgrade fail before it can refresh.
        await ExecuteAsync(connection, "ALTER TABLE \"SalesFacts\" RENAME COLUMN \"SaleId\" TO \"UnavailableSaleId\";");
        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            CacheScripts,
            generationId);

        foreach (var (_, cache) in CachePairs)
        {
            Assert.Equal("failed", await GetBuildStateAsync(connection, cache));
            await AssertCacheUnavailableAsync(database.ConnectionString, cache);
        }

        await using var failureCodeCommand = new NpgsqlCommand("""
            SELECT DISTINCT failure_code
            FROM analytics_intel.cache_refresh_state
            WHERE build_generation_id = @generationId;
            """, connection);
        failureCodeCommand.Parameters.AddWithValue("generationId", generationId);
        Assert.Equal("startup_build_failed", await failureCodeCommand.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ReadyEmptyCache_Is200WhilePendingCacheIs503()
    {
        var database = await _fixture.CreateDatabaseAsync("rq615_empty_state");
        await using var connection = await _fixture.OpenConnectionAsync(database);
        var seedPath = FindRepositoryFile("Api.Tests/Fixtures/analytics-intelligence-smoke-seed.sql");
        await ExecuteAsync(connection, await File.ReadAllTextAsync(seedPath));
        await ExecuteAsync(connection, "DELETE FROM \"SalesLineFacts\"; DELETE FROM \"SalesFacts\"; DELETE FROM \"ProductsDim\"; DELETE FROM \"SuppliersDim\";");
        await ExecuteAsync(connection, """
            ALTER TABLE "ProductsDim" ADD COLUMN "ProductName" text;
            ALTER TABLE "ProductsDim" ADD COLUMN "PLU" text;
            CREATE TABLE "StoresDim" (
                "StoreKey" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "StoreId" integer NOT NULL,
                "StoreName" text NOT NULL DEFAULT '',
                "City" text
            );
            """);
        await ExecuteScriptAsync(connection, "Database/Analytics/Intelligence/020_create_intelligence_schema.sql");
        foreach (var (relativePath, _) in CacheScripts)
            await ExecuteScriptAsync(connection, relativePath);

        var request = new DemandSignalsRequest(null, 1, null, null, null, null, null, null, 1, 5, null, null);
        var readyResult = await AnalyticsIntelligenceEndpoints.ExecuteCacheQueryAsync(
            () => AnalyticsIntelligenceEndpoints.QueryDemandSignalsAsync(
                database.ConnectionString,
                request,
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(readyResult).StatusCode);

        Assert.Equal(0L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM analytics_intel.mv_product_demand_signals_v1_cache;"));
        await ExecuteAsync(connection, """
            UPDATE analytics_intel.cache_refresh_state
            SET build_state = 'pending', last_refreshed_at_utc = NULL
            WHERE cache_name = 'analytics_intel.mv_product_demand_signals_v1_cache';
            """);
        var pendingResult = await AnalyticsIntelligenceEndpoints.ExecuteCacheQueryAsync(
            () => AnalyticsIntelligenceEndpoints.QueryDemandSignalsAsync(
                database.ConnectionString,
                request,
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsAssignableFrom<IStatusCodeHttpResult>(pendingResult).StatusCode);
    }

    private async Task<IntelligenceTestDatabase> CreatePreR613CacheDatabaseAsync(string purpose)
    {
        var database = await _fixture.CreateDatabaseAsync($"rq615_cache_{purpose}");
        await using var connection = await _fixture.OpenConnectionAsync(database);

        var seedPath = FindRepositoryFile("Api.Tests/Fixtures/analytics-intelligence-smoke-seed.sql");
        await ExecuteAsync(connection, await File.ReadAllTextAsync(seedPath));
        await ExecuteScriptAsync(connection, "Database/Analytics/Intelligence/020_create_intelligence_schema.sql");

        // SaleId 50001 belongs to the recent Store 7 sale above, while another header's
        // surrogate Id collides with that SaleId and points to an old sale in Store 91.
        await ExecuteAsync(connection, """
            INSERT INTO "SalesFacts" ("Id", "SaleId", "StoreId", "SaleTimestampUtc")
            VALUES (50001, 91001, 91, ((CURRENT_DATE - 200)::timestamp + interval '10 hours') AT TIME ZONE 'UTC');
            """);

        await EnsureStartupHistoryAsync(connection);
        foreach (var (relativePath, _) in CacheScripts)
        {
            var sql = await File.ReadAllTextAsync(FindRepositoryFile(relativePath));
            var correctedJoin = "sf.\"SaleId\" = slf.\"SaleId\"";
            var legacyJoin = "sf.\"Id\" = slf.\"SaleId\"";
            Assert.Single(Regex.Matches(sql, Regex.Escape(correctedJoin)).Cast<Match>());

            var legacySql = sql.Replace(correctedJoin, legacyJoin, StringComparison.Ordinal);
            string[] legacyAsOfBounds = relativePath switch
            {
                "Database/Analytics/Intelligence/021_product_demand_signals_v1.sql" =>
                    ["AND sf.\"SaleTimestampUtc\" < ((s.as_of_date + 1)::timestamp AT TIME ZONE 'UTC')"],
                "Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql" =>
                    [
                        "AND sf.\"SaleTimestampUtc\" < ((s.as_of_date + 1)::timestamp AT TIME ZONE 'UTC')",
                        "AND imf.\"Datum\" < ((s.as_of_date + 1)::timestamp AT TIME ZONE 'UTC')"
                    ],
                "Database/Analytics/Intelligence/024_trend_momentum_v1.sql" =>
                    [
                        "AND th.\"Date\" < ((s.as_of_date + 1)::timestamp AT TIME ZONE 'UTC')",
                        "AND sf.\"SaleTimestampUtc\" < ((s.as_of_date + 1)::timestamp AT TIME ZONE 'UTC')"
                    ],
                _ => throw new ArgumentOutOfRangeException(nameof(relativePath))
            };
            foreach (var upperBound in legacyAsOfBounds)
            {
                Assert.Single(Regex.Matches(sql, Regex.Escape(upperBound)).Cast<Match>());
                legacySql = legacySql.Replace(upperBound + Environment.NewLine, string.Empty, StringComparison.Ordinal);
            }
            await ExecuteAsync(connection, legacySql);
            await RecordStartupHistoryAsync(connection, relativePath, ComputeStartupHash(legacySql));
        }

        return database;
    }

    private static async Task AssertCorrectDemandStoreDateAndQuantityAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT store_id, date, sales_velocity
            FROM analytics_intel.mv_product_demand_signals_v1_cache
            WHERE article_id = 42
              AND date = CURRENT_DATE - 1;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(7, reader.GetInt32(0));
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1)), DateOnly.FromDateTime(reader.GetDateTime(1)));
        Assert.Equal(3m, reader.GetDecimal(2));
    }

    private static async Task AssertInventoryDateAndQuantityAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT date, stock_qty
            FROM analytics_intel.mv_inventory_risk_signals_v1_cache
            WHERE article_id = 42
            ORDER BY date DESC
            LIMIT 1;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.Date), DateOnly.FromDateTime(reader.GetDateTime(0)));
        Assert.Equal(12m, reader.GetDecimal(1));
    }

    private static async Task AssertTrendDateAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT signal_date
            FROM analytics_intel.mv_trend_momentum_v1_cache
            WHERE article_id = 42;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.Date), DateOnly.FromDateTime(reader.GetDateTime(0)));
    }

    private static async Task AssertStableOutputColumnsAsync(NpgsqlConnection connection)
    {
        await AssertColumnNamesAsync(
            connection,
            "analytics_intel.vw_inventory_risk_signals_v1",
            ["article_id", "date", "stock_qty", "avg_daily_sales_30d", "days_of_cover", "stock_turn", "stockout_days", "low_stock_days", "dead_stock_risk"]);
        await AssertColumnNamesAsync(
            connection,
            "analytics_intel.vw_trend_momentum_v1",
            ["article_id", "signal_date", "external_trend_score", "local_sales_acceleration", "trend_entropy"]);
    }

    private static async Task AssertAllCachesReadyForCurrentSqlAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT COUNT(*)
            FROM analytics_intel.cache_refresh_state AS state
            JOIN "__StartupSqlScriptHistory" AS history
              ON history."ScriptPath" = state.sql_file_path
             AND history."ScriptHash" = state.expected_script_hash
            WHERE state.cache_name = ANY(@cacheNames)
              AND state.build_state = 'ready'
              AND state.last_refreshed_at_utc IS NOT NULL;
            """, connection);
        command.Parameters.AddWithValue("cacheNames", CachePairs.Select(pair => pair.Cache).ToArray());
        Assert.Equal(CachePairs.Length, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    private static async Task AssertDemandCacheUnavailableAsync(string connectionString) =>
        await AssertCacheUnavailableAsync(connectionString, CachePairs[0].Cache);

    private static async Task AssertCacheUnavailableAsync(string connectionString, string cacheName)
    {
        var exception = await Assert.ThrowsAsync<AnalyticsIntelligenceEndpoints.IntelligenceCacheUnavailableException>(
            () => cacheName switch
            {
                "analytics_intel.mv_product_demand_signals_v1_cache" => AnalyticsIntelligenceEndpoints.QueryDemandSignalsAsync(
                    connectionString,
                    new DemandSignalsRequest(null, 1, null, null, null, null, null, null, 1, 5, null, null),
                    CancellationToken.None),
                "analytics_intel.mv_inventory_risk_signals_v1_cache" => AnalyticsIntelligenceEndpoints.QueryInventoryRiskSignalsAsync(
                    connectionString,
                    new InventoryRiskSignalsRequest(null, 1, null, null, null, null, false, 1, 5, null, null),
                    CancellationToken.None),
                "analytics_intel.mv_trend_momentum_v1_cache" => AnalyticsIntelligenceEndpoints.QueryTrendMomentumAsync(
                    connectionString,
                    new TrendMomentumRequest(null, null, null, null, null, 1, 5, null, null),
                    CancellationToken.None),
                _ => throw new ArgumentOutOfRangeException(nameof(cacheName))
            });
        Assert.NotNull(exception);
    }

    private static async Task<string> GetBuildStateAsync(NpgsqlConnection connection, string cacheName)
    {
        await using var command = new NpgsqlCommand(
            "SELECT build_state FROM analytics_intel.cache_refresh_state WHERE cache_name = @cacheName;",
            connection);
        command.Parameters.AddWithValue("cacheName", cacheName);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string[]> GetCacheStateSnapshotAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT cache_name, build_state, COALESCE(build_generation_id::text, ''), COALESCE(expected_script_hash, '')
            FROM analytics_intel.cache_refresh_state
            WHERE cache_name = ANY(@cacheNames)
            ORDER BY cache_name;
            """, connection);
        command.Parameters.AddWithValue("cacheNames", CachePairs.Select(pair => pair.Cache).ToArray());
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add($"{reader.GetString(0)}|{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetString(3)}");
        return rows.ToArray();
    }

    private static async Task<long> ScalarLongAsync(
        NpgsqlConnection connection,
        string sql,
        DateOnly? asOf = null)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        if (asOf.HasValue)
            command.Parameters.AddWithValue("asOf", NpgsqlDbType.Date, asOf.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ScalarDecimalAsync(
        NpgsqlConnection connection,
        string sql,
        DateOnly? asOf = null)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        if (asOf.HasValue)
            command.Parameters.AddWithValue("asOf", NpgsqlDbType.Date, asOf.Value);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> ScalarIsNullAsync(
        NpgsqlConnection connection,
        string sql,
        DateOnly? asOf = null)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        if (asOf.HasValue)
            command.Parameters.AddWithValue("asOf", NpgsqlDbType.Date, asOf.Value);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull;
    }

    private static async Task<DateTime[]> GetRefreshTimesAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT last_refreshed_at_utc
            FROM analytics_intel.cache_refresh_state
            WHERE cache_name = ANY(@cacheNames)
            ORDER BY cache_name;
            """, connection);
        command.Parameters.AddWithValue("cacheNames", CachePairs.Select(pair => pair.Cache).ToArray());
        var times = new List<DateTime>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            times.Add(reader.GetDateTime(0));
        return times.ToArray();
    }

    private static async Task AssertColumnNamesAsync(NpgsqlConnection connection, string viewName, string[] expected)
    {
        var pieces = viewName.Split('.', 2);
        await using var command = new NpgsqlCommand("""
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @name
            ORDER BY ordinal_position;
            """, connection);
        command.Parameters.AddWithValue("schema", pieces[0]);
        command.Parameters.AddWithValue("name", pieces[1]);
        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));
        Assert.Equal(expected, columns);
    }

    private static async Task ExecuteScriptAsync(NpgsqlConnection connection, string relativePath) =>
        await ExecuteAsync(connection, await File.ReadAllTextAsync(FindRepositoryFile(relativePath)));

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection)
        {
            CommandTimeout = 0
        };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureStartupHistoryAsync(NpgsqlConnection connection)
    {
        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS "__StartupSqlScriptHistory" (
                "ScriptPath" character varying(512) PRIMARY KEY,
                "ScriptHash" character varying(64) NOT NULL,
                "AppliedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                "DurationMs" bigint NULL
            );
            """);
    }

    private static async Task RecordStartupHistoryAsync(NpgsqlConnection connection, string scriptPath, string scriptHash)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO "__StartupSqlScriptHistory" ("ScriptPath", "ScriptHash")
            VALUES (@path, @hash);
            """, connection);
        command.Parameters.AddWithValue("path", scriptPath);
        command.Parameters.AddWithValue("hash", scriptHash);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> DifferenceCountAsync(NpgsqlConnection connection, string view, string cache)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT COUNT(*)
            FROM (
                (SELECT * FROM {view} EXCEPT ALL SELECT * FROM {cache})
                UNION ALL
                (SELECT * FROM {cache} EXCEPT ALL SELECT * FROM {view})
            ) AS differences;
            """, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static string ComputeStartupHash(string sql)
    {
        var batches = sql.Split(
            "-- SQL_BATCH_BREAK",
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var normalized = batches.Length == 0
            ? sql
            : string.Join(Environment.NewLine + "-- SQL_BATCH_BREAK" + Environment.NewLine, batches);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
