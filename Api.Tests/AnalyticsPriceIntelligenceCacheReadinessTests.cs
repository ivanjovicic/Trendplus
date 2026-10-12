using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;
using Infrastructure.Seed;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Collection(IntelligencePostgresCollection.Name)]
public sealed class AnalyticsPriceIntelligenceCacheReadinessTests
{
    private const string PriceSqlPath = "Database/Analytics/Intelligence/023_price_intelligence_v1.sql";
    private const string PriceView = "analytics_intel.vw_price_intelligence_v1";
    private const string PriceCache = "analytics_intel.mv_price_intelligence_v1_cache";

    private static readonly (string SqlFilePath, string[] RequiredRelations)[] PriceCacheScripts =
    [
        (
            PriceSqlPath,
            [PriceView, PriceCache]
        )
    ];

    private readonly IntelligencePostgresFixture _fixture;

    public AnalyticsPriceIntelligenceCacheReadinessTests(IntelligencePostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SemanticSqlChange_RefreshesSameShapeCacheAndPriceEndpointFailsClosed()
    {
        var database = await CreatePriceDatabaseAsync("semantic_change", withLegacySemantics: true, empty: false);
        await using var connection = await _fixture.OpenConnectionAsync(database);
        var legacyRelationOid = await ScalarLongAsync(connection, $"SELECT '{PriceCache}'::regclass::oid::bigint;");
        Assert.Equal(0.75m, await ScalarDecimalAsync(connection, $"SELECT discount_depth FROM {PriceCache} WHERE article_id = 42;"));

        await using var host = await PriceEndpointTestHost.StartAsync(database.ConnectionString);
        using (var pending = await host.Client.GetAsync("/api/analytics/intelligence/price-intelligence"))
        {
            Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, pending.StatusCode);
        }

        var generationId = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            PriceCacheScripts);
        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            PriceCacheScripts,
            generationId);

        // The columns did not change, so startup must refresh the existing MV in place.
        Assert.Equal(legacyRelationOid, await ScalarLongAsync(connection, $"SELECT '{PriceCache}'::regclass::oid::bigint;"));
        Assert.Equal(0.25m, await ScalarDecimalAsync(connection, $"SELECT discount_depth FROM {PriceCache} WHERE article_id = 42;"));
        Assert.Equal(0L, await DifferenceCountAsync(connection, PriceView, PriceCache));
        await AssertCacheReadyForCurrentSqlAsync(connection);

        using (var ready = await host.Client.GetAsync("/api/analytics/intelligence/price-intelligence"))
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, ready.StatusCode);
            using var document = await JsonDocument.ParseAsync(await ready.Content.ReadAsStreamAsync());
            Assert.Equal(2, document.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(2, document.RootElement.GetProperty("items").GetArrayLength());
        }

        await ExecuteAsync(connection, $"""
            UPDATE analytics_intel.cache_refresh_state
            SET build_state = 'pending', last_refreshed_at_utc = NULL
            WHERE cache_name = '{PriceCache}';
            """);
        await AssertUnavailableAsync(host.Client);

        await ExecuteAsync(connection, $"""
            UPDATE analytics_intel.cache_refresh_state
            SET build_state = 'failed', failure_code = 'startup_build_failed'
            WHERE cache_name = '{PriceCache}';
            """);
        await AssertUnavailableAsync(host.Client);

        var currentHash = await ScalarStringAsync(connection, $"SELECT expected_script_hash FROM analytics_intel.cache_refresh_state WHERE cache_name = '{PriceCache}';");
        await ExecuteAsync(connection, $"""
            UPDATE analytics_intel.cache_refresh_state
            SET build_state = 'ready', last_refreshed_at_utc = clock_timestamp(), failure_code = NULL
            WHERE cache_name = '{PriceCache}';
            UPDATE "__StartupSqlScriptHistory"
            SET "ScriptHash" = repeat('0', 64)
            WHERE "ScriptPath" = '{PriceSqlPath}';
            """);
        await AssertUnavailableAsync(host.Client);

        await ExecuteAsync(connection, $"""
            UPDATE "__StartupSqlScriptHistory"
            SET "ScriptHash" = '{currentHash}'
            WHERE "ScriptPath" = '{PriceSqlPath}';
            DROP MATERIALIZED VIEW {PriceCache};
            """);
        await AssertUnavailableAsync(host.Client);
    }

    [Fact]
    public async Task ReadyEmptyPriceCache_Is200AndPendingEmptyCacheIs503()
    {
        var database = await CreatePriceDatabaseAsync("empty", withLegacySemantics: false, empty: true);
        await using var host = await PriceEndpointTestHost.StartAsync(database.ConnectionString);

        using (var empty = await host.Client.GetAsync("/api/analytics/intelligence/price-intelligence"))
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, empty.StatusCode);
            using var document = await JsonDocument.ParseAsync(await empty.Content.ReadAsStreamAsync());
            Assert.Equal(0, document.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Empty(document.RootElement.GetProperty("items").EnumerateArray());
        }

        await using var connection = await _fixture.OpenConnectionAsync(database);
        await ExecuteAsync(connection, $"""
            UPDATE analytics_intel.cache_refresh_state
            SET build_state = 'pending', last_refreshed_at_utc = NULL
            WHERE cache_name = '{PriceCache}';
            """);
        await AssertUnavailableAsync(host.Client);
    }

    [Fact]
    public async Task PriceSnapshotDates_AreUtcStableAcrossTimeZonesAndRefreshExistingPageMetadata()
    {
        const string utcDateProjection = "(pd.\"Timestamp\" AT TIME ZONE 'UTC')::date AS price_date";
        const string sessionDateProjection = "pd.\"Timestamp\"::date AS price_date";
        var database = await CreatePriceDatabaseAsync("utc_dates", withLegacySemantics: false, empty: true);
        await using var connection = await _fixture.OpenConnectionAsync(database);

        Assert.Equal("timestamp with time zone", await ScalarStringAsync(connection, """
            SELECT data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'ProductsDim' AND column_name = 'Timestamp';
            """));

        await ExecuteAsync(connection, """
            INSERT INTO "ProductsDim" (
                "ProductId", "ProductName", "PLU", "Timestamp", "Category", "Brand",
                "SalePrice", "FirstSalePrice", "PurchasePriceRsd", "Kolicina"
            ) VALUES
                (7001, 'March before DST', 'DST-7001', TIMESTAMPTZ '2026-03-08 00:30:00+00', 'DST footwear', 'UTC brand', 100, 120, 50, 10),
                (7002, 'March after DST', 'DST-7002', TIMESTAMPTZ '2026-03-08 10:30:00+00', 'DST footwear', 'UTC brand', 200, 240, 100, 10),
                (7003, 'November before DST', 'DST-7003', TIMESTAMPTZ '2026-11-01 00:30:00+00', 'DST footwear', 'UTC brand', 300, 360, 150, 10),
                (7004, 'November after DST', 'DST-7004', TIMESTAMPTZ '2026-11-01 09:30:00+00', 'DST footwear', 'UTC brand', 400, 480, 200, 10);
            """);

        var expectedUtcRows = new[]
        {
            "7001:2026-03-08",
            "7002:2026-03-08",
            "7003:2026-11-01",
            "7004:2026-11-01"
        };
        var zones = new[]
        {
            (TimeZone: "UTC", LocalRows: new[] { "7001:2026-03-08", "7002:2026-03-08", "7003:2026-11-01", "7004:2026-11-01" }),
            (TimeZone: "Pacific/Kiritimati", LocalRows: new[] { "7001:2026-03-08", "7002:2026-03-09", "7003:2026-11-01", "7004:2026-11-01" }),
            (TimeZone: "America/Los_Angeles", LocalRows: new[] { "7001:2026-03-07", "7002:2026-03-08", "7003:2026-10-31", "7004:2026-11-01" })
        };

        foreach (var zone in zones)
        {
            await SetTimeZoneAsync(connection, zone.TimeZone);
            Assert.Equal(expectedUtcRows, await ReadProductPriceDatesAsync(connection, PriceView));
            Assert.Equal(zone.LocalRows, await ReadProductTimestampLocalDatesAsync(connection));
        }

        var currentSql = await File.ReadAllTextAsync(FindRepositoryFile(PriceSqlPath));
        Assert.Contains(utcDateProjection, currentSql);
        var legacySql = currentSql.Replace(utcDateProjection, sessionDateProjection, StringComparison.Ordinal);
        Assert.NotEqual(currentSql, legacySql);

        // Recreate a populated pre-fix cache with the old session-local projection, then prove
        // startup replaces the same-shaped materialized view and refreshes it from current SQL.
        await EnsureStartupHistoryAsync(connection);
        await SetTimeZoneAsync(connection, "America/Los_Angeles");
        await ExecuteAsync(connection, legacySql);
        await RecordStartupHistoryAsync(connection, PriceSqlPath, ComputeStartupHash(legacySql));
        await ExecuteAsync(connection, $"REFRESH MATERIALIZED VIEW {PriceCache};");
        Assert.Equal(
            new[] { "7001:2026-03-07", "7002:2026-03-08", "7003:2026-10-31", "7004:2026-11-01" },
            await ReadProductPriceDatesAsync(connection, PriceCache));

        var cacheOid = await ScalarLongAsync(connection, $"SELECT '{PriceCache}'::regclass::oid::bigint;");
        var cacheIndexes = await ReadCacheIndexDefinitionsAsync(connection);
        var generationId = await DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync(
            database.ConnectionString,
            PriceCacheScripts);
        await DatabaseInitializer.RunAnalyticsIntelligenceBuildAsync(
            database.ConnectionString,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            PriceCacheScripts,
            generationId);

        Assert.Equal(cacheOid, await ScalarLongAsync(connection, $"SELECT '{PriceCache}'::regclass::oid::bigint;"));
        Assert.Equal(cacheIndexes, await ReadCacheIndexDefinitionsAsync(connection));
        Assert.Equal(0L, await DifferenceCountAsync(connection, PriceView, PriceCache));
        await AssertCacheReadyForCurrentSqlAsync(connection);
        Assert.Equal(0.4m, await ScalarDecimalAsync(connection, $"SELECT price_index_vs_category FROM {PriceView} WHERE article_id = 7001;"));
        Assert.Equal(1.6m, await ScalarDecimalAsync(connection, $"SELECT price_index_vs_category FROM {PriceCache} WHERE article_id = 7004;"));

        foreach (var zone in zones)
        {
            await SetTimeZoneAsync(connection, zone.TimeZone);
            Assert.Equal(expectedUtcRows, await ReadProductPriceDatesAsync(connection, PriceView));
            Assert.Equal(expectedUtcRows, await ReadProductPriceDatesAsync(connection, PriceCache));
        }

        var apiConnectionString = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Options = "-c TimeZone=America/Los_Angeles"
        }.ConnectionString;
        await using (var apiSession = new NpgsqlConnection(apiConnectionString))
        {
            await apiSession.OpenAsync();
            Assert.Equal("America/Los_Angeles", await ScalarStringAsync(apiSession, "SELECT current_setting('TimeZone');"));
        }

        await using var host = await PriceEndpointTestHost.StartAsync(apiConnectionString);
        using (var firstPage = await host.Client.GetAsync(
                   "/api/analytics/intelligence/price-intelligence?page=1&pageSize=2&sortBy=priceDate&sortDir=desc"))
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, firstPage.StatusCode);
            using var document = await JsonDocument.ParseAsync(await firstPage.Content.ReadAsStreamAsync());
            var root = document.RootElement;
            Assert.Equal(4, root.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, root.GetProperty("page").GetInt32());
            Assert.Equal(2, root.GetProperty("pageSize").GetInt32());
            Assert.StartsWith("2026-11-01", root.GetProperty("asOfDate").GetString());
            var items = root.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(new[] { 7003, 7004 }, items.Select(item => item.GetProperty("articleId").GetInt32()));
            Assert.All(items, item => Assert.StartsWith("2026-11-01", item.GetProperty("priceDate").GetString()));
        }

        using (var secondPage = await host.Client.GetAsync(
                   "/api/analytics/intelligence/price-intelligence?page=2&pageSize=2&sortBy=priceDate&sortDir=desc"))
        {
            Assert.Equal(System.Net.HttpStatusCode.OK, secondPage.StatusCode);
            using var document = await JsonDocument.ParseAsync(await secondPage.Content.ReadAsStreamAsync());
            var root = document.RootElement;
            Assert.StartsWith("2026-11-01", root.GetProperty("asOfDate").GetString());
            var items = root.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(new[] { 7001, 7002 }, items.Select(item => item.GetProperty("articleId").GetInt32()));
            Assert.All(items, item => Assert.StartsWith("2026-03-08", item.GetProperty("priceDate").GetString()));
        }
    }

    private async Task<IntelligenceTestDatabase> CreatePriceDatabaseAsync(string purpose, bool withLegacySemantics, bool empty)
    {
        var database = await _fixture.CreateDatabaseAsync($"rq622_{purpose}");
        await using var connection = await _fixture.OpenConnectionAsync(database);

        var seedPath = FindRepositoryFile("Api.Tests/Fixtures/analytics-intelligence-smoke-seed.sql");
        await ExecuteAsync(connection, await File.ReadAllTextAsync(seedPath));
        await ExecuteAsync(connection, "ALTER TABLE \"ProductsDim\" ADD COLUMN \"ProductName\" text; ALTER TABLE \"ProductsDim\" ADD COLUMN \"PLU\" text;");
        if (empty)
        {
            await ExecuteAsync(connection, "DELETE FROM \"ProductsDim\";");
        }

        await ExecuteSqlFileAsync(connection, "Database/Analytics/Intelligence/020_create_intelligence_schema.sql");
        var priceSql = await File.ReadAllTextAsync(FindRepositoryFile(PriceSqlPath));
        if (withLegacySemantics)
        {
            await EnsureStartupHistoryAsync(connection);
            const string currentFormula = "ELSE (pp.list_price - pp.net_price) / NULLIF(pp.list_price, 0)";
            const string legacyFormula = "ELSE pp.net_price / NULLIF(pp.list_price, 0)";
            Assert.Equal(2, priceSql.Split(currentFormula, StringSplitOptions.None).Length);
            var oldSql = priceSql.Replace(currentFormula, legacyFormula, StringComparison.Ordinal);
            await ExecuteAsync(connection, oldSql);
            await RecordStartupHistoryAsync(connection, PriceSqlPath, ComputeStartupHash(oldSql));
        }
        else
        {
            await ExecuteAsync(connection, priceSql);
        }

        return database;
    }

    private static async Task AssertCacheReadyForCurrentSqlAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT state.build_state, state.expected_script_hash, history."ScriptHash", state.last_refreshed_at_utc IS NOT NULL
            FROM analytics_intel.cache_refresh_state AS state
            JOIN "__StartupSqlScriptHistory" AS history ON history."ScriptPath" = state.sql_file_path
            WHERE state.cache_name = @cacheName AND state.sql_file_path = @sqlPath;
            """, connection);
        command.Parameters.AddWithValue("cacheName", PriceCache);
        command.Parameters.AddWithValue("sqlPath", PriceSqlPath);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("ready", reader.GetString(0));
        Assert.Equal(reader.GetString(1), reader.GetString(2));
        Assert.True(reader.GetBoolean(3));
    }

    private static async Task AssertUnavailableAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/analytics/intelligence/price-intelligence");
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static async Task<long> DifferenceCountAsync(NpgsqlConnection connection, string view, string cache)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT COUNT(*) FROM (
                (SELECT * FROM {view} EXCEPT ALL SELECT * FROM {cache})
                UNION ALL
                (SELECT * FROM {cache} EXCEPT ALL SELECT * FROM {view})
            ) AS differences;
            """, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string[]> ReadProductPriceDatesAsync(NpgsqlConnection connection, string relation)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT article_id::text || ':' || price_date::text
            FROM {relation}
            WHERE article_id BETWEEN 7001 AND 7004
            ORDER BY article_id;
            """, connection);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(reader.GetString(0));
        return rows.ToArray();
    }

    private static async Task<string[]> ReadProductTimestampLocalDatesAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT "ProductId"::text || ':' || "Timestamp"::date::text
            FROM "ProductsDim"
            WHERE "ProductId" BETWEEN 7001 AND 7004
            ORDER BY "ProductId";
            """, connection);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(reader.GetString(0));
        return rows.ToArray();
    }

    private static async Task<string[]> ReadCacheIndexDefinitionsAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("""
            SELECT indexname || ':' || indexdef
            FROM pg_indexes
            WHERE schemaname = 'analytics_intel' AND tablename = 'mv_price_intelligence_v1_cache'
            ORDER BY indexname;
            """, connection);
        var indexes = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            indexes.Add(reader.GetString(0));
        return indexes.ToArray();
    }

    private static async Task SetTimeZoneAsync(NpgsqlConnection connection, string timeZone)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('TimeZone', @timeZone, false);", connection);
        command.Parameters.AddWithValue("timeZone", timeZone);
        await command.ExecuteScalarAsync();
    }

    private static async Task<long> ScalarLongAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ScalarDecimalAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ScalarStringAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static async Task ExecuteSqlFileAsync(NpgsqlConnection connection, string relativePath) =>
        await ExecuteAsync(connection, await File.ReadAllTextAsync(FindRepositoryFile(relativePath)));

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 0 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureStartupHistoryAsync(NpgsqlConnection connection) =>
        await ExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS "__StartupSqlScriptHistory" (
                "ScriptPath" character varying(512) PRIMARY KEY,
                "ScriptHash" character varying(64) NOT NULL,
                "AppliedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW(),
                "DurationMs" bigint NULL
            );
            """);

    private static async Task RecordStartupHistoryAsync(NpgsqlConnection connection, string path, string hash)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO "__StartupSqlScriptHistory" ("ScriptPath", "ScriptHash") VALUES (@path, @hash);
            """, connection);
        command.Parameters.AddWithValue("path", path);
        command.Parameters.AddWithValue("hash", hash);
        await command.ExecuteNonQueryAsync();
    }

    private static string ComputeStartupHash(string sql)
    {
        var batches = sql.Split("-- SQL_BATCH_BREAK", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }

    private sealed class PriceEndpointTestHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public static async Task<PriceEndpointTestHost> StartAsync(string connectionString)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddRateLimiter(options =>
                options.AddPolicy("analytics", _ => RateLimitPartition.GetFixedWindowLimiter(
                    "rq622-price-intelligence",
                    static _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = false
                    })));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DOTNET_ENVIRONMENT"] = Environments.Development,
                ["ConnectionStrings:AnalyticsConnection"] = connectionString
            });

            var app = builder.Build();
            app.UseRouting();
            app.UseRateLimiter();
            app.MapAnalyticsIntelligenceEndpoints();
            await app.StartAsync();
            return new PriceEndpointTestHost(app, app.GetTestClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }
}
