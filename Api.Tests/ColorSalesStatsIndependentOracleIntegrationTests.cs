using System.Net.Http;
using System.Text.Json;
using Api.Tests;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Integration")]
[Trait("Suite", "AnalyticsAdversarial")]
public sealed class ColorSalesStatsIndependentOracleIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private const string JulyFrom = "2026-07-01";
    private const string JulyTo = "2026-07-07";
    private readonly PostgresContainerFixture _postgres;

    public ColorSalesStatsIndependentOracleIntegrationTests(PostgresContainerFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Color buckets match the independent raw-fact oracle across data scopes")]
    public async Task SharedFixture_ColorBucketsMatchOracleAcrossScopesAndUnknownColor()
    {
        var connectionString = await CreateSeededDatabaseAsync("rq597_color_scope");
        await using var factory = new ColorEndpointFactory(connectionString);
        using var client = factory.CreateClient();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var allFilters = BuildFilters("all", storeId: null);
        var importedFilters = BuildFilters("imported", storeId: null);
        var existingStoreFilters = BuildFilters("existing", storeId: 1);

        var allBuckets = await AssertEndpointMatchesOracleAsync(client, connection, allFilters);
        var importedBuckets = await AssertEndpointMatchesOracleAsync(client, connection, importedFilters);
        var existingStoreBuckets = await AssertEndpointMatchesOracleAsync(client, connection, existingStoreFilters);

        Assert.Contains(allBuckets.Keys, ColorIdentityPolicy.IsUnknown);
        Assert.False(allBuckets.Keys.Order(StringComparer.Ordinal).SequenceEqual(importedBuckets.Keys.Order(StringComparer.Ordinal)));
        Assert.False(allBuckets.Keys.Order(StringComparer.Ordinal).SequenceEqual(existingStoreBuckets.Keys.Order(StringComparer.Ordinal)));
        Assert.Contains(importedBuckets.Keys, key => key == ColorIdentityPolicy.Key("Plava"));
    }

    [Fact(DisplayName = "Color buckets preserve half-open boundaries and exclude DUG/KOREKCIJA receipts")]
    public async Task SharedFixture_ColorBucketsMatchOracleForReceiptAndDateBoundaries()
    {
        var connectionString = await CreateSeededDatabaseAsync("rq597_color_bounds");
        await using var factory = new ColorEndpointFactory(connectionString);
        using var client = factory.CreateClient();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var septemberFirst = BuildFilters(
            "all",
            storeId: null,
            fromDate: "2026-09-01",
            toDate: "2026-09-02");
        var septemberSecond = BuildFilters(
            "all",
            storeId: null,
            fromDate: "2026-09-02",
            toDate: "2026-09-03");

        var firstDayBuckets = await AssertEndpointMatchesOracleAsync(client, connection, septemberFirst);
        var secondDayBuckets = await AssertEndpointMatchesOracleAsync(client, connection, septemberSecond);

        var firstDayTotals = await OperationsAnalyticsRawFactOracle.QueryTotalsAsync(connection, septemberFirst);
        var secondDayTotals = await OperationsAnalyticsRawFactOracle.QueryTotalsAsync(connection, septemberSecond);
        Assert.Equal(9, firstDayTotals.SaleLineCount); // ten fixture lines in the day, less the DUG receipt.
        Assert.Equal(1, secondDayTotals.SaleLineCount); // the other Sep 2 line is KOREKCIJA.
        var boundaryBucket = secondDayBuckets[ColorIdentityPolicy.Key("Siva")];
        Assert.Equal(1, boundaryBucket.Units);
        Assert.Equal(120m, boundaryBucket.Revenue);
    }

    private static async Task<Dictionary<string, ColorBucket>> AssertEndpointMatchesOracleAsync(
        HttpClient client,
        NpgsqlConnection connection,
        OperationsAnalyticsRawFactOracle.Filters filters)
    {
        var oracleTotals = await OperationsAnalyticsRawFactOracle.QueryTotalsAsync(connection, filters);
        var oracleBuckets = await OperationsAnalyticsRawFactOracle.QueryColorBucketsAsync(connection, filters);
        Assert.Equal(oracleTotals.SaleLineCount, oracleBuckets.Sum(bucket => bucket.SaleLineCount));
        Assert.Equal(oracleTotals.TotalUnits, oracleBuckets.Sum(bucket => bucket.Units));
        Assert.Equal(oracleTotals.TotalRevenue, oracleBuckets.Sum(bucket => bucket.Revenue));

        var path = $"/api/analytics/color-sales-stats?fromDate={filters.FromUtc:yyyy-MM-dd}" +
                   $"&toDate={filters.ToUtc:yyyy-MM-dd}&dataScope={Uri.EscapeDataString(filters.DataScope)}" +
                   (filters.StoreId.HasValue ? $"&storeId={filters.StoreId.Value}" : string.Empty);
        var response = await GetJsonAsync(client, path);
        var endpointRows = response.GetProperty("colors").EnumerateArray().ToArray();
        var endpointBuckets = endpointRows.ToDictionary(
            row => ColorIdentityPolicy.Key(row.GetProperty("boja").GetString()),
            row => new ColorBucket(
                row.GetProperty("ukupnaKolicina").GetInt32(),
                row.GetProperty("ukupanPromet").GetDecimal()),
            StringComparer.Ordinal);
        var oracleByKey = oracleBuckets.ToDictionary(bucket => bucket.DimensionKey, StringComparer.Ordinal);

        Assert.Equal(oracleByKey.Keys.Order(StringComparer.Ordinal).ToArray(), endpointBuckets.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(oracleByKey.Count, endpointBuckets.Count);
        foreach (var (key, oracleBucket) in oracleByKey)
        {
            var endpointBucket = endpointBuckets[key];
            Assert.Equal(oracleBucket.Units, endpointBucket.Units);
            Assert.Equal(oracleBucket.Revenue, endpointBucket.Revenue);
        }

        Assert.Equal(oracleTotals.TotalUnits, endpointBuckets.Values.Sum(bucket => bucket.Units));
        Assert.Equal(oracleTotals.TotalRevenue, endpointBuckets.Values.Sum(bucket => bucket.Revenue));
        return endpointBuckets;
    }

    private async Task<string> CreateSeededDatabaseAsync(string databasePrefix)
    {
        Assert.True(_postgres.IsAvailable, "RQ597 requires its disposable PostgreSQL Testcontainer.");
        var connectionString = await _postgres.TryCreateDatabaseConnectionStringAsync($"{databasePrefix}_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        var databaseConnectionString = connectionString!;

        await using (var db = new TrendplusDbContext(
                         new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(databaseConnectionString).Options))
            await db.Database.MigrateAsync();
        await using (var analyticsDb = new AnalyticsDbContext(
                         new DbContextOptionsBuilder<AnalyticsDbContext>().UseNpgsql(databaseConnectionString).Options))
            await analyticsDb.Database.MigrateAsync();

        var fixturePath = FindRepositoryFile("Api.Tests", "Fixtures", "operations-analytics-all-routes-seed.sql");
        var sql = await File.ReadAllTextAsync(fixturePath);
        await using var connection = new NpgsqlConnection(databaseConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
        return databaseConnectionString;
    }

    private static OperationsAnalyticsRawFactOracle.Filters BuildFilters(
        string dataScope,
        int? storeId,
        string fromDate = JulyFrom,
        string toDate = JulyTo)
    {
        var fromUtc = DateTime.SpecifyKind(DateTime.Parse(fromDate), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(DateTime.Parse(toDate), DateTimeKind.Utc);
        return new OperationsAnalyticsRawFactOracle.Filters(fromUtc, toUtc, storeId, dataScope);
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{path} returned {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository fixture: {Path.Combine(segments)}");
    }

    private sealed record ColorBucket(int Units, decimal Revenue);

    private sealed class ColorEndpointFactory(string connectionString) : WebApplicationFactory<global::Program>
    {
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
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:AnalyticsConnection"] = connectionString,
                ["ConnectionStrings:OpenProductTrainingConnection"] = connectionString,
                ["DailySales:TimeZoneId"] = "Europe/Belgrade"
            };

            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<ITrendplusDbContext>();
                services.AddDbContextFactory<TrendplusDbContext>(options => options.UseNpgsql(connectionString));
                services.AddScoped<TrendplusDbContext>(provider =>
                    provider.GetRequiredService<IDbContextFactory<TrendplusDbContext>>().CreateDbContext());
                services.AddScoped<ITrendplusDbContext>(provider => provider.GetRequiredService<TrendplusDbContext>());
                services.RemoveAll<DbContextOptions<AnalyticsDbContext>>();
                services.RemoveAll<AnalyticsDbContext>();
                services.RemoveAll<IAnalyticsDbContext>();
                services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(connectionString));
                services.AddScoped<IAnalyticsDbContext>(provider => provider.GetRequiredService<AnalyticsDbContext>());
            });
        }
    }
}
