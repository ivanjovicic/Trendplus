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
