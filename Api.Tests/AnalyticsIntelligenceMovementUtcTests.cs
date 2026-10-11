using Domain.Model;
using Infrastructure.DbContexts;
using Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using System.Security.Cryptography;
using System.Text;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Collection(IntelligencePostgresCollection.Name)]
public sealed class AnalyticsIntelligenceMovementUtcTests
{
    private const int ArticleId = 618001;
    private const int UnknownArticleId = 618002;
    private const string MovementSqlPath = "Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql";
    private const string InventoryRiskView = "analytics_intel.vw_inventory_risk_signals_v1";
    private const string InventoryRiskCache = "analytics_intel.mv_inventory_risk_signals_v1_cache";

    private static readonly (string SqlFilePath, string[] RequiredRelations)[] MovementCacheScripts =
    [
        (MovementSqlPath, [InventoryRiskView, InventoryRiskCache])
    ];

    private readonly IntelligencePostgresFixture _fixture;

    public AnalyticsIntelligenceMovementUtcTests(IntelligencePostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InventoryMovementSignals_AreUtcDeterministicAcrossSessionTimeZones()
    {
        var database = await _fixture.CreateDatabaseAsync("movement_utc_oracle");
        var utcToday = DateTime.UtcNow.Date;
        var midnightUtc = DateTime.SpecifyKind(utcToday.AddDays(-1), DateTimeKind.Utc);
        var beforeMidnightUtc = midnightUtc.AddSeconds(-1);
        var afterMidnightUtc = midnightUtc.AddSeconds(1);
        var beforeDate = DateOnly.FromDateTime(beforeMidnightUtc);
        var afterDate = DateOnly.FromDateTime(afterMidnightUtc);

        await CreateAndSeedSourceTablesAsync(database, beforeMidnightUtc, afterMidnightUtc);
        await AssertInventoryMovementTimestampMappingAsync(database);
        await _fixture.ExecuteIntelligenceScriptsAsync(
            database,
            "Database/Analytics/Intelligence/020_create_intelligence_schema.sql",
            MovementSqlPath);

        var (utcSignals, losAngelesSignals) = await ReadSignalsAcrossTimeZonesAsync(database);

        Assert.Equal(utcSignals, losAngelesSignals);

        var signalBeforeMidnight = Assert.Single(utcSignals, row => row.ArticleId == ArticleId && row.Date == beforeDate);
        var signalAfterMidnight = Assert.Single(utcSignals, row => row.ArticleId == ArticleId && row.Date == afterDate);
        var signalBeforeBothMovements = Assert.Single(utcSignals, row => row.ArticleId == ArticleId && row.Date == beforeDate.AddDays(-1));

        Assert.Equal(13m, signalBeforeMidnight.StockQty);
        Assert.Equal(0.2m, signalBeforeMidnight.AvgDailySales30d);
        Assert.Equal(20m, signalAfterMidnight.StockQty);
        Assert.Equal(0.133333m, signalAfterMidnight.AvgDailySales30d);
        Assert.Equal(6m, signalBeforeBothMovements.StockQty);

        var unknownInventory = Assert.Single(utcSignals, row => row.ArticleId == UnknownArticleId && row.Date == afterDate);
        Assert.Null(unknownInventory.StockQty);
        Assert.Null(unknownInventory.StockoutDays);
    }

    [Fact]
    public async Task ChangedMovementDateSql_RefreshesExistingCacheAndStaysUnavailableUntilRetry()
    {
        var database = await _fixture.CreateDatabaseAsync("movement_cache_upgrade");
        var utcToday = DateTime.UtcNow.Date;
        var midnightUtc = DateTime.SpecifyKind(utcToday.AddDays(-1), DateTimeKind.Utc);
        var beforeMidnightUtc = midnightUtc.AddSeconds(-1);
        var afterMidnightUtc = midnightUtc.AddSeconds(1);
        var beforeDate = DateOnly.FromDateTime(beforeMidnightUtc);
        var afterDate = DateOnly.FromDateTime(afterMidnightUtc);

        await CreateAndSeedSourceTablesAsync(database, beforeMidnightUtc, afterMidnightUtc);
        await AssertInventoryMovementTimestampMappingAsync(database);

        var currentSql = await File.ReadAllTextAsync(FindRepositoryFile(MovementSqlPath));
        const string utcDayExpression = "(imf.\"Datum\" AT TIME ZONE 'UTC')::date";
        const string utcRangeExpression = "imf.\"Datum\" >= ((s.as_of_date - s.horizon_days)::timestamp AT TIME ZONE 'UTC')";
        Assert.Equal(2, CountOccurrences(currentSql, utcDayExpression));
        Assert.Equal(1, CountOccurrences(currentSql, utcRangeExpression));

        // Recreate the previous session-dependent movement expressions and a cache built from them.
        var legacySql = currentSql
            .Replace(utcDayExpression, "DATE_TRUNC('day', imf.\"Datum\")::date", StringComparison.Ordinal)
            .Replace(utcRangeExpression, "imf.\"Datum\"::date >= s.as_of_date - s.horizon_days", StringComparison.Ordinal);
        Assert.NotEqual(currentSql, legacySql);
        var legacyHash = ComputeStartupHash(legacySql);

        await _fixture.RunFreshBootstrapAsync(database.ConnectionString, async _ =>
        {
            await using var connection = await _fixture.OpenConnectionAsync(database);
            await ExecuteSqlAsync(connection, await File.ReadAllTextAsync(
                FindRepositoryFile("Database/Analytics/Intelligence/020_create_intelligence_schema.sql")));
            await ExecuteSqlAsync(connection, """
                CREATE TABLE "__StartupSqlScriptHistory" (
                    "ScriptPath" character varying(512) PRIMARY KEY,
                    "ScriptHash" character varying(64) NOT NULL,
                    "AppliedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                    "DurationMs" bigint NULL
                );
                SELECT set_config('TimeZone', 'America/Los_Angeles', false);
                """);
            await ExecuteSqlAsync(connection, legacySql);

            await using var recordLegacyHash = new NpgsqlCommand("""
                INSERT INTO "__StartupSqlScriptHistory" ("ScriptPath", "ScriptHash")
                VALUES (@path, @hash);
                UPDATE analytics_intel.cache_refresh_state
                SET build_state = 'ready',
                    expected_script_hash = @hash,
                    last_refreshed_at_utc = clock_timestamp(),
                    failure_code = NULL
                WHERE cache_name = @cacheName;
                """, connection);
            recordLegacyHash.Parameters.AddWithValue("path", MovementSqlPath);
            recordLegacyHash.Parameters.AddWithValue("hash", legacyHash);
            recordLegacyHash.Parameters.AddWithValue("cacheName", InventoryRiskCache);
            await recordLegacyHash.ExecuteNonQueryAsync();
        });

        await using var utcConnection = await _fixture.OpenConnectionAsync(database);
        await SetTimeZoneAsync(utcConnection, "UTC");
        Assert.Equal("ready", await GetBuildStateAsync(utcConnection));
        Assert.True(await DifferenceCountAsync(utcConnection) > 0,
            "The simulated prior cache must contain the reproduced session-timezone drift.");

        var upgradeGeneration = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            MovementCacheScripts);
        Assert.Equal("pending", await GetBuildStateAsync(utcConnection));
        await AssertCacheUnavailableAsync(database.ConnectionString);

        await ExecuteSqlAsync(utcConnection,
            "ALTER TABLE \"InventoryMovementFacts\" RENAME COLUMN \"Datum\" TO \"DatumUnavailable\";");
        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            MovementCacheScripts,
            upgradeGeneration);
        Assert.Equal("failed", await GetBuildStateAsync(utcConnection));
        await AssertCacheUnavailableAsync(database.ConnectionString);

        await ExecuteSqlAsync(utcConnection,
            "ALTER TABLE \"InventoryMovementFacts\" RENAME COLUMN \"DatumUnavailable\" TO \"Datum\";");
        var retryGeneration = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            MovementCacheScripts);
        Assert.Equal("pending", await GetBuildStateAsync(utcConnection));
        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            NullLogger.Instance,
            MovementCacheScripts,
            retryGeneration);

        Assert.Equal("ready", await GetBuildStateAsync(utcConnection));
        Assert.True(await CacheHashMatchesStartupHistoryAsync(utcConnection));
        Assert.Equal(0L, await DifferenceCountAsync(utcConnection));
        await AssertMovementCacheIndexesAsync(utcConnection);
        await AssertBoundedMovementCachePlanAsync(utcConnection);

        var afterRefresh = await ReadSignalsAsync(database, "UTC");
        var beforeSignal = Assert.Single(afterRefresh, row => row.ArticleId == ArticleId && row.Date == beforeDate);
        var afterSignal = Assert.Single(afterRefresh, row => row.ArticleId == ArticleId && row.Date == afterDate);
        Assert.Equal(13m, beforeSignal.StockQty);
        Assert.Equal(20m, afterSignal.StockQty);
    }

    private async Task CreateAndSeedSourceTablesAsync(
        IntelligenceTestDatabase database,
        DateTime beforeMidnightUtc,
        DateTime afterMidnightUtc)
    {
        await using var connection = await _fixture.OpenConnectionAsync(database);
        await using (var schema = new NpgsqlCommand("""
            CREATE TABLE "ProductsDim" (
                "ProductId" integer PRIMARY KEY,
                "Kolicina" integer NULL,
                "MinimalnaKolicina" integer NULL,
                "PurchasePriceRsd" numeric(18,2) NULL,
                "PurchasePrice" numeric(18,2) NULL,
                "Timestamp" timestamp with time zone NOT NULL
            );
            CREATE TABLE "SalesFacts" (
                "SaleId" integer PRIMARY KEY,
                "SaleTimestampUtc" timestamp with time zone NOT NULL,
                "BrojRacuna" text NULL
            );
            CREATE TABLE "SalesLineFacts" (
                "SaleId" integer NOT NULL,
                "ProductId" integer NOT NULL,
                "Qty" integer NOT NULL
            );
            CREATE TABLE "InventoryMovementFacts" (
                "ArtikalId" integer NULL,
                "Datum" timestamp with time zone NOT NULL,
                "TipPromene" character varying(100) NOT NULL,
                "Kolicina" integer NULL
            );
            CREATE INDEX "IX_InventoryMovementFacts_Datum" ON "InventoryMovementFacts" ("Datum");
            CREATE INDEX "IX_InventoryMovementFacts_ArtikalId_Datum" ON "InventoryMovementFacts" ("ArtikalId", "Datum");
            INSERT INTO "ProductsDim" (
                "ProductId", "Kolicina", "MinimalnaKolicina", "PurchasePriceRsd", "Timestamp")
            VALUES (@articleId, 20, 5, 100, clock_timestamp());
            """, connection))
        {
            schema.Parameters.AddWithValue("articleId", ArticleId);
            await schema.ExecuteNonQueryAsync();
        }

        await using var seed = new NpgsqlCommand("""
            INSERT INTO "SalesFacts" ("SaleId", "SaleTimestampUtc", "BrojRacuna") VALUES
                (618101, @beforeMidnight, 'RQ618-BEFORE'),
                (618102, @afterMidnight, 'RQ618-AFTER');
            INSERT INTO "SalesLineFacts" ("SaleId", "ProductId", "Qty") VALUES
                (618101, @articleId, 6),
                (618102, @articleId, -2);
            INSERT INTO "InventoryMovementFacts" ("ArtikalId", "Datum", "TipPromene", "Kolicina") VALUES
                (@articleId, @beforeMidnight, 'Ulaz robe', 10),
                (@articleId, @beforeMidnight, 'Prenos ulaz', 4),
                (@articleId, @beforeMidnight, 'Prenos izlaz', 3),
                (@articleId, @beforeMidnight, 'Povrat kupca', 2),
                (@articleId, @afterMidnight, 'Ulaz robe', 5),
                (@articleId, @afterMidnight, 'Prenos ulaz', 1),
                (@articleId, @afterMidnight, 'Prenos izlaz', 2),
                (@articleId, @afterMidnight, 'Povrat kupca', 1),
                (@unknownArticleId, @afterMidnight, 'Ulaz robe', 7);
            """, connection);
        seed.Parameters.AddWithValue("beforeMidnight", NpgsqlDbType.TimestampTz, beforeMidnightUtc);
        seed.Parameters.AddWithValue("afterMidnight", NpgsqlDbType.TimestampTz, afterMidnightUtc);
        seed.Parameters.AddWithValue("articleId", ArticleId);
        seed.Parameters.AddWithValue("unknownArticleId", UnknownArticleId);
        await seed.ExecuteNonQueryAsync();
    }

    private async Task AssertInventoryMovementTimestampMappingAsync(IntelligenceTestDatabase database)
    {
        var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql(database.ConnectionString)
            .Options;
        await using var context = new AnalyticsDbContext(options);
        var property = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IProperty>(
            context.Model.FindEntityType(typeof(InventoryMovementFact))?.FindProperty(nameof(InventoryMovementFact.Datum)));
        var mappedStoreType = property.GetRelationalTypeMapping().StoreType;

        await using var connection = await _fixture.OpenConnectionAsync(database);
        await using var command = new NpgsqlCommand("""
            SELECT format_type(attribute.atttypid, attribute.atttypmod)
            FROM pg_attribute AS attribute
            WHERE attribute.attrelid = '"InventoryMovementFacts"'::regclass
              AND attribute.attname = 'Datum'
              AND NOT attribute.attisdropped;
            """, connection);
        var physicalStoreType = (string?)await command.ExecuteScalarAsync();

        Assert.Equal("timestamp with time zone", mappedStoreType);
        Assert.Equal(mappedStoreType, physicalStoreType);

        await using var indexCommand = new NpgsqlCommand("""
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = 'InventoryMovementFacts'
              AND indexname = ANY(@indexNames);
            """, connection);
        indexCommand.Parameters.AddWithValue("indexNames", new[]
        {
            "IX_InventoryMovementFacts_Datum",
            "IX_InventoryMovementFacts_ArtikalId_Datum"
        });
        Assert.Equal(2L, Convert.ToInt64(await indexCommand.ExecuteScalarAsync()));
    }

    private async Task<IReadOnlyList<InventorySignal>> ReadSignalsAsync(
        IntelligenceTestDatabase database,
        string timeZone)
    {
        await using var connection = await _fixture.OpenConnectionAsync(database);
        await SetTimeZoneAsync(connection, timeZone);
        return await ReadSignalsAsync(connection, transaction: null);
    }

    private async Task<(IReadOnlyList<InventorySignal> Utc, IReadOnlyList<InventorySignal> LosAngeles)>
        ReadSignalsAcrossTimeZonesAsync(IntelligenceTestDatabase database)
    {
        await using var connection = await _fixture.OpenConnectionAsync(database);
        await using var transaction = await connection.BeginTransactionAsync();

        // CURRENT_TIMESTAMP is fixed at transaction start so both session zones share one as-of date.
        await SetTimeZoneAsync(connection, "UTC", transaction);
        var utcSignals = await ReadSignalsAsync(connection, transaction);
        await SetTimeZoneAsync(connection, "America/Los_Angeles", transaction);
        var losAngelesSignals = await ReadSignalsAsync(connection, transaction);

        await transaction.RollbackAsync();
        return (utcSignals, losAngelesSignals);
    }

    private static async Task<IReadOnlyList<InventorySignal>> ReadSignalsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        await using var command = new NpgsqlCommand("""
            SELECT article_id, date, stock_qty, avg_daily_sales_30d,
                   days_of_cover, stock_turn, stockout_days, low_stock_days, dead_stock_risk
            FROM analytics_intel.vw_inventory_risk_signals_v1
            WHERE article_id = ANY(@articleIds)
            ORDER BY article_id, date;
            """, connection, transaction);
        command.Parameters.AddWithValue("articleIds", new[] { ArticleId, UnknownArticleId });

        var signals = new List<InventorySignal>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            signals.Add(new InventorySignal(
                reader.GetInt32(0),
                DateOnly.FromDateTime(reader.GetDateTime(1)),
                ReadNullableDecimal(reader, 2),
                reader.GetDecimal(3),
                ReadNullableDecimal(reader, 4),
                ReadNullableDecimal(reader, 5),
                ReadNullableInt(reader, 6),
                ReadNullableInt(reader, 7),
                ReadNullableDecimal(reader, 8)));
        }

        return signals;
    }

    private static decimal? ReadNullableDecimal(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

    private static int? ReadNullableInt(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private async Task<string> GetBuildStateAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT build_state FROM analytics_intel.cache_refresh_state WHERE cache_name = @cacheName;",
            connection);
        command.Parameters.AddWithValue("cacheName", InventoryRiskCache);
        return (string)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Inventory risk cache refresh state is missing."));
    }

    private async Task<long> DifferenceCountAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT COUNT(*)
            FROM (
                (SELECT * FROM {InventoryRiskView} EXCEPT ALL SELECT * FROM {InventoryRiskCache})
                UNION ALL
                (SELECT * FROM {InventoryRiskCache} EXCEPT ALL SELECT * FROM {InventoryRiskView})
            ) AS differences;
            """, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> CacheHashMatchesStartupHistoryAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1
                FROM analytics_intel.cache_refresh_state AS state
                JOIN "__StartupSqlScriptHistory" AS history
                  ON history."ScriptPath" = state.sql_file_path
                 AND history."ScriptHash" = state.expected_script_hash
                WHERE state.cache_name = @cacheName
                  AND state.build_state = 'ready'
                  AND state.last_refreshed_at_utc IS NOT NULL
            );
            """, connection);
        command.Parameters.AddWithValue("cacheName", InventoryRiskCache);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task AssertMovementCacheIndexesAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE schemaname = 'analytics_intel'
              AND tablename = 'mv_inventory_risk_signals_v1_cache';
            """, connection);
        var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            definitions.Add(reader.GetString(0), reader.GetString(1));

        Assert.Contains("idx_mv_inventory_risk_signals_v1_cache_pk", definitions.Keys);
        Assert.Contains("idx_mv_inventory_risk_signals_v1_cache_date", definitions.Keys);
        Assert.Contains("UNIQUE", definitions["idx_mv_inventory_risk_signals_v1_cache_pk"], StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertBoundedMovementCachePlanAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand($"""
            EXPLAIN
            SELECT article_id, date, stock_qty
            FROM {InventoryRiskCache}
            WHERE article_id = {ArticleId}
            ORDER BY date DESC
            LIMIT 30;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var plan = new StringBuilder();
        while (await reader.ReadAsync())
            plan.AppendLine(reader.GetString(0));

        Assert.Contains("Limit", plan.ToString(), StringComparison.Ordinal);
    }

    private static async Task AssertCacheUnavailableAsync(string connectionString)
    {
        await Assert.ThrowsAsync<AnalyticsIntelligenceEndpoints.IntelligenceCacheUnavailableException>(
            () => AnalyticsIntelligenceEndpoints.QueryInventoryRiskSignalsAsync(
                connectionString,
                new InventoryRiskSignalsRequest(null, 1, null, null, null, null, false, 1, 5, null, null),
                CancellationToken.None));
    }

    private static async Task SetTimeZoneAsync(
        NpgsqlConnection connection,
        string timeZone,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('TimeZone', @timeZone, @isLocal);", connection, transaction);
        command.Parameters.AddWithValue("timeZone", timeZone);
        command.Parameters.AddWithValue("isLocal", transaction is not null);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteSqlAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection)
        {
            CommandTimeout = 0
        };
        await command.ExecuteNonQueryAsync();
    }

    private static int CountOccurrences(string value, string token) =>
        (value.Length - value.Replace(token, string.Empty, StringComparison.Ordinal).Length) / token.Length;

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

        throw new FileNotFoundException($"Repository file was not found: {relativePath}", relativePath);
    }

    private sealed record InventorySignal(
        int ArticleId,
        DateOnly Date,
        decimal? StockQty,
        decimal AvgDailySales30d,
        decimal? DaysOfCover,
        decimal? StockTurn,
        int? StockoutDays,
        int? LowStockDays,
        decimal? DeadStockRisk);
}
