using System.Collections.Concurrent;
using System.Text.Json;
using Api.Services;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Domain.Model.Prodaja;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Integration")]
[Trait("Suite", "RQ605")]
public sealed class Rq605LegacySalesPostgresIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private static readonly DateTime CurrentFromUtc = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CurrentToUtc = new(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public Rq605LegacySalesPostgresIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "RQ605 cached sales and comparison match the independent PostgreSQL operational oracle")]
    public async Task CachedLegacySales_UsesScopeReceiptPolicySignedReturnsAndScopedCacheKeys()
    {
        var connectionString = await CreateDatabaseAsync("tp_rq605_sales");
        if (connectionString is null)
            return;

        await using (var trendDb = CreateTrendDb(connectionString))
        await using (var analyticsDb = CreateAnalyticsDb(connectionString))
        {
            await trendDb.Database.MigrateAsync();
            await analyticsDb.Database.MigrateAsync();

            await using var setup = new NpgsqlConnection(connectionString);
            await setup.OpenAsync();
            await using var command = new NpgsqlCommand("""
                INSERT INTO "Artikli" ("Id", "PLU", "Naziv", "UpdatedAt", "DataOrigin")
                VALUES (960500, 'RQ605-TEST', 'RQ605 test article', '2026-10-01T00:00:00Z', 'existing');

                INSERT INTO prodaja_zaglavlje
                    (id, broj_racuna, datum_prodaje, id_objekat, data_origin)
                VALUES
                    (960501, 'RQ605-IMPORTED', '2026-10-20T00:00:00Z', 1, 'access'),
                    (960502, 'RQ605-EXISTING', '2026-10-20T00:00:00Z', 1, 'existing'),
                    (960503, '  dUg  ', '2026-10-20T00:00:00Z', 1, 'existing'),
                    (960504, 'RQ605-RETURN', '2026-10-20T00:00:00Z', 1, 'access'),
                    (960505, 'RQ605-PREVIOUS', '2026-10-19T00:00:00Z', 1, 'access');

                INSERT INTO prodaja_stavke (id, id_prodaja, id_artikal, kolicina, cena)
                VALUES
                    (960501, 960501, 960500, 2, 100),
                    (960502, 960502, 960500, 3, 50),
                    (960503, 960503, 960500, 5, 100),
                    (960504, 960504, 960500, -1, 80),
                    (960505, 960505, 960500, 1, 50);

                -- A stale/orphan analytical fact must not leak into the operational population.
                INSERT INTO "SalesFacts"
                    ("SaleId", "BrojRacuna", "SaleTimestampUtc", "StoreId", "PaymentType",
                     "TotalAmount", "TotalUnits", "TotalLines")
                VALUES (969999, 'RQ605-ORPHAN-FACT', '2026-10-20T00:00:00Z', 1, 'cash',
                        2676700, 999, 1);
                """, setup);
            await command.ExecuteNonQueryAsync();
        }

        var cache = new RecordingAnalyticsCacheService();
        await using var factory = new Rq605EndpointFactory(connectionString, cache);
        using var client = factory.CreateClient();
        await using var oracleConnection = new NpgsqlConnection(connectionString);
        await oracleConnection.OpenAsync();

        foreach (var scope in new[] { "imported", "existing", "all" })
        {
            var expected = await QueryOperationalOracleAsync(oracleConnection, CurrentFromUtc, CurrentToUtc, scope);
            var response = await GetJsonAsync(
                client,
                $"/api/analytics/cached/sales/daily?fromDate={CurrentFromUtc:yyyy-MM-dd}&toDate={CurrentToUtc:yyyy-MM-dd}&dataScope={scope}");
            var row = Assert.Single(response.GetProperty("items").EnumerateArray());

            Assert.Equal(expected.Revenue, row.GetProperty("totalRevenue").GetDecimal());
            Assert.Equal(expected.Transactions, row.GetProperty("transactionCount").GetInt32());
            Assert.Equal(expected.Units, row.GetProperty("totalUnits").GetInt32());
            Assert.Equal(scope, response.GetProperty("meta").GetProperty("requestedDataScope").GetString());
            Assert.Equal(scope, response.GetProperty("meta").GetProperty("effectiveDataScope").GetString());
            Assert.Equal(SalesDataScopePolicy.Source, response.GetProperty("meta").GetProperty("dataScopeSource").GetString());
        }

        var importedDaily = await GetJsonAsync(
            client,
            "/api/analytics/cached/sales/daily?fromDate=2026-10-20&toDate=2026-10-21&dataScope=imported");
        Assert.Equal(120m, importedDaily.GetProperty("items")[0].GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(1, importedDaily.GetProperty("items")[0].GetProperty("totalUnits").GetInt32());

        var summary = await GetJsonAsync(
            client,
            "/api/analytics/cached/sales/summary?fromDate=2026-10-20&toDate=2026-10-21&dataScope=all");
        Assert.Equal(270m, summary.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(3, summary.GetProperty("totalTransactions").GetInt32());
        Assert.Equal(4, summary.GetProperty("totalUnits").GetInt32());

        var comparison = await GetJsonAsync(
            client,
            "/api/analytics/sales/comparison?fromDate=2026-10-20&toDate=2026-10-21&dataScope=imported");
        Assert.Equal(120m, comparison.GetProperty("current").GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(50m, comparison.GetProperty("previous").GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(140m, comparison.GetProperty("change").GetProperty("revenue").GetDecimal());

        // The second imported daily request is a cache hit; different scopes must use different keys.
        Assert.True(cache.GetCountForScope("imported") >= 2);
        Assert.Contains(cache.Keys, key => key.Contains("scope:imported", StringComparison.Ordinal));
        Assert.Contains(cache.Keys, key => key.Contains("scope:existing", StringComparison.Ordinal));
        Assert.Contains(cache.Keys, key => key.Contains("scope:all", StringComparison.Ordinal));
        Assert.Equal(1, cache.FactoryCallsForScope("imported"));
    }

    private async Task<string?> CreateDatabaseAsync(string prefix)
    {
        if (!_fixture.IsAvailable)
            return null;

        return await _fixture.TryCreateDatabaseConnectionStringAsync($"{prefix}_{Guid.NewGuid():N}");
    }

    private static TrendplusDbContext CreateTrendDb(string connectionString)
        => new(new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options);

    private static AnalyticsDbContext CreateAnalyticsDb(string connectionString)
        => new(new DbContextOptionsBuilder<AnalyticsDbContext>().UseNpgsql(connectionString).Options);

    private static async Task<(decimal Revenue, int Transactions, int Units)> QueryOperationalOracleAsync(
        NpgsqlConnection connection,
        DateTime fromUtc,
        DateTime toUtc,
        string scope)
    {
        const string sql = """
            SELECT COALESCE(SUM(ps.kolicina * ps.cena), 0),
                   COUNT(DISTINCT p.id)::int,
                   COALESCE(SUM(ps.kolicina), 0)::int
            FROM prodaja_zaglavlje p
            JOIN prodaja_stavke ps ON ps.id_prodaja = p.id
            WHERE p.datum_prodaje >= @fromUtc
              AND p.datum_prodaje <= @toUtc
              AND upper(trim(coalesce(p.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
              AND (
                    @scope = 'all'
                 OR (@scope = 'imported' AND p.data_origin = 'access')
                 OR (@scope = 'existing' AND (p.data_origin = 'existing' OR p.data_origin IS NULL OR p.data_origin = ''))
              );
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("fromUtc", fromUtc);
        command.Parameters.AddWithValue("toUtc", toUtc);
        command.Parameters.AddWithValue("scope", scope);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetDecimal(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{path} returned {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}

internal sealed class Rq605EndpointFactory : WebApplicationFactory<global::Program>
{
    private readonly string _connectionString;
    private readonly IAnalyticsCacheService _cache;

    public Rq605EndpointFactory(string connectionString, IAnalyticsCacheService cache)
    {
        _connectionString = connectionString;
        _cache = cache;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Database:AutoMigrate"] = "false",
            ["StartupReadiness:GateApiTraffic"] = "false",
            ["PROCESS_TYPE"] = "web",
            ["Workers:Enabled"] = "false",
            ["AnalyticsCache:Provider"] = "disabled",
            ["Analytics:AllowLoopbackInProduction"] = "true",
            ["ConnectionStrings:DefaultConnection"] = _connectionString,
            ["ConnectionStrings:AnalyticsConnection"] = _connectionString,
            ["ConnectionStrings:OpenProductTrainingConnection"] = _connectionString,
            ["DailySales:TimeZoneId"] = "Europe/Belgrade"
        };

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
            services.RemoveAll<TrendplusDbContext>();
            services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
            services.RemoveAll<ITrendplusDbContext>();
            services.AddDbContextFactory<TrendplusDbContext>(options => options.UseNpgsql(_connectionString));
            services.AddScoped<TrendplusDbContext>(provider =>
                provider.GetRequiredService<IDbContextFactory<TrendplusDbContext>>().CreateDbContext());
            services.AddScoped<ITrendplusDbContext>(provider => provider.GetRequiredService<TrendplusDbContext>());

            services.RemoveAll<DbContextOptions<AnalyticsDbContext>>();
            services.RemoveAll<AnalyticsDbContext>();
            services.RemoveAll<IAnalyticsDbContext>();
            services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(_connectionString));
            services.AddScoped<IAnalyticsDbContext>(provider => provider.GetRequiredService<AnalyticsDbContext>());

            services.RemoveAll<IAnalyticsCacheService>();
            services.AddSingleton(_cache);
            services.AddSingleton<IAnalyticsCacheService>(_cache);
        });
    }
}

internal sealed class RecordingAnalyticsCacheService : IAnalyticsCacheService
{
    private readonly ConcurrentDictionary<string, object> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _gets = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _factories = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Keys => _entries.Keys.ToArray();
    public bool IsRedisAvailable => false;
    public bool IsRedisEnabled => false;
    public CacheFootprintSnapshot GetFootprintSnapshot() => new("test", false, false, _entries.Count);
    public void SetRedisEnabled(bool enabled) { }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        _gets.AddOrUpdate(key, 1, (_, count) => count + 1);
        return Task.FromResult(_entries.TryGetValue(key, out var value) && value is T typed ? typed : null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        _entries[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        foreach (var key in _entries.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
            _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
            return cached;

        _factories.AddOrUpdate(key, 1, (_, count) => count + 1);
        var created = await factory();
        await SetAsync(key, created, expiration, ct);
        return created;
    }

    public int GetCountForScope(string scope)
        => _gets.Where(item => item.Key.Contains($"scope:{scope}", StringComparison.Ordinal)).Sum(item => item.Value);

    public int FactoryCallsForScope(string scope)
        => _factories.Where(item => item.Key.Contains($"scope:{scope}", StringComparison.Ordinal)).Sum(item => item.Value);
}
