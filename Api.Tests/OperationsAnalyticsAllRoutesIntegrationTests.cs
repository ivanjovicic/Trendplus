using System.Net;
using System.Net.Http;
using System.Text.Json;
using Application.Artikli.Common.Interfaces;
using Api.Tests;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Npgsql;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Integration")]
public sealed class OperationsAnalyticsAllRoutesIntegrationTests
    : IClassFixture<PostgresContainerFixture>
{
    private const int StartupWarmupMaxAttempts = 3;
    private const string FromDate = "2026-07-01";
    private const string ToDate = "2026-07-07";
    private const string NivelacijaEventDate = "2026-08-01T00:00:00Z";

    private readonly PostgresContainerFixture _postgres;
    private readonly ITestOutputHelper _output;

    public OperationsAnalyticsAllRoutesIntegrationTests(PostgresContainerFixture postgres, ITestOutputHelper output)
    {
        _postgres = postgres;
        _output = output;
    }

    [Fact(DisplayName = "RQ561 certifies the six current Operations screens against one adversarial fixture")]
    public async Task SharedFixture_ReconcilesSixCurrentScreensAndEmitsRouteVerdicts()
    {
        Assert.True(_postgres.IsAvailable, "The RQ561 certification requires its disposable PostgreSQL Testcontainer.");
        var connectionString = await _postgres.TryCreateDatabaseConnectionStringAsync($"rq561_operations_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await SeedSharedFixtureAsync(connectionString!);
        await using var factory = new OperationsEndpointFactory(connectionString!);
        using var client = factory.CreateClient();
        var verdicts = new List<RouteVerdict>(capacity: 6);

        var inventory = await GetJsonAsync(client, "/api/analytics/inventory/list?page=1&pageSize=100&dataScope=all");
        var inventoryItems = inventory.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(inventoryItems, item => item.GetProperty("plu").GetString() == "OOS-101");
        Assert.Contains(inventoryItems, item => item.GetProperty("plu").GetString() == "EMPTY-104");
        Assert.Contains(
            inventoryItems,
            item => item.GetProperty("plu").GetString() == "OOS-101"
                && item.GetProperty("recommendationAllowed").GetBoolean() == false);
        verdicts.Add(new("/analytics/inventory", "/api/analytics/inventory/list", 1, 1, "PASS", "empty-stock and unavailable-recommendation rows asserted"));

        var shoeType = await GetJsonAsync(
            client,
            $"/api/analytics/shoe-type-sales-stats?fromDate={FromDate}&toDate={ToDate}&dataScope=all");
        Assert.Equal(5, shoeType.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(540m, shoeType.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        verdicts.Add(new("/analytics/shoe-type-sales-stats", "/api/analytics/shoe-type-sales-stats", 1, 1, "PASS", "fixture total asserted"));

        var daily = await GetJsonAsync(
            client,
            $"/api/analytics/daily-sales?fromDate={FromDate}&toDate={ToDate}&dataScope=all");
        var dailyMetadata = daily.GetProperty("metadata");
        Assert.Equal(5, dailyMetadata.GetProperty("totalItemsInRange").GetInt32());
        Assert.Equal(
            540m,
            daily.GetProperty("dateRows").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal($"{FromDate}T00:00:00Z", daily.GetProperty("requestedFrom").GetString());
        Assert.Equal($"{ToDate}T00:00:00Z", daily.GetProperty("requestedTo").GetString());
        Assert.Equal("all", daily.GetProperty("dataScope").GetString());
        verdicts.Add(new("/analytics/daily-sales", "/api/analytics/daily-sales", 1, 1, "PASS", "period, data scope and fixture totals asserted"));

        var vendorNivelacija = await GetJsonAsync(
            client,
            $"/api/analytics/vendor-sales-nivelacija?eventDate={Uri.EscapeDataString(NivelacijaEventDate)}&dataScope=all");
        Assert.False(vendorNivelacija.GetProperty("meta").GetProperty("success").GetBoolean(), vendorNivelacija.GetRawText());
        Assert.Equal("vendor_sales_nivelacija_contract_missing", vendorNivelacija.GetProperty("meta").GetProperty("errorCode").GetString());
        Assert.Equal("unavailable", vendorNivelacija.GetProperty("dataCoverageStatus").GetString());
        Assert.False(vendorNivelacija.GetProperty("recommendationAllowed").GetBoolean());
        verdicts.Add(new(
            "/analytics/nivelacije-pre-post",
            "/api/analytics/vendor-sales-nivelacija",
            1,
            1,
            "UNVERIFIED",
            "Route executed; unresolved maturity/overlap semantics are not certified by the current fixture."));

        var color = await GetJsonAsync(
            client,
            $"/api/analytics/color-sales-stats?fromDate={FromDate}&toDate={ToDate}&dataScope=all");
        Assert.Equal(3, color.GetProperty("colors").GetArrayLength());
        Assert.Equal(5, color.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(540m, color.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        verdicts.Add(new("/analytics/color-sales-stats", "/api/analytics/color-sales-stats", 1, 1, "PASS", "visible color buckets and fixture totals asserted"));

        var preNivelacija = await GetJsonAsync(
            client,
            "/api/analytics/pre-nivelacija-prioriteti?dataScope=all&page=1&pageSize=100&focus=all");
        Assert.True(preNivelacija.GetProperty("meta").GetProperty("success").GetBoolean());
        Assert.Equal(1, preNivelacija.GetProperty("totalCandidates").GetInt32());
        Assert.Contains(
            preNivelacija.GetProperty("candidates").EnumerateArray(),
            candidate => candidate.GetProperty("sku").GetString() == "PRE-105");
        verdicts.Add(new("/analytics/pre-nivelacija-prioriteti", "/api/analytics/pre-nivelacija-prioriteti", 1, 1, "PASS", "candidate count and visible SKU asserted"));

        var report = new
        {
            manifestId = "operations-six-screen-certification-2026-10-03",
            expectedRoutes = 6,
            executedRoutes = verdicts.Sum(item => item.ExecutedCount),
            verdict = verdicts.Any(item => item.Verdict != "PASS") ? "UNVERIFIED" : "PASS",
            routes = verdicts
        };
        _output.WriteLine(JsonSerializer.Serialize(report));
        Assert.Equal(6, verdicts.Count);
        Assert.All(verdicts, item => Assert.Equal(item.ExpectedCount, item.ExecutedCount));
        Assert.Equal(6, report.executedRoutes);
        Assert.Equal("UNVERIFIED", report.verdict);
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var response = await client.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(body);
                return document.RootElement.Clone();
            }

            var isDatabaseWarmup = IsDatabaseWarmupResponse(response.StatusCode, body, out var retryAfterSeconds);
            if (attempt >= StartupWarmupMaxAttempts || !isDatabaseWarmup)
            {
                Assert.Fail($"{path} returned {(int)response.StatusCode}: {body}");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(retryAfterSeconds, 1, 10)));
        }
    }

    private static bool IsDatabaseWarmupResponse(
        HttpStatusCode statusCode,
        string body,
        out int retryAfterSeconds)
    {
        retryAfterSeconds = 1;
        if (statusCode != HttpStatusCode.ServiceUnavailable)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            return root.TryGetProperty("status", out var status)
                && status.GetString() == "starting"
                && root.TryGetProperty("reason", out var reason)
                && reason.GetString() == "db_warmup"
                && (!root.TryGetProperty("retryAfterSeconds", out var retryAfter)
                    || retryAfter.TryGetInt32(out retryAfterSeconds));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task SeedSharedFixtureAsync(string connectionString)
    {
        await using (var db = new TrendplusDbContext(
                         new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options))
        {
            await db.Database.MigrateAsync();
        }
        await using (var analyticsDb = new AnalyticsDbContext(
                         new DbContextOptionsBuilder<AnalyticsDbContext>().UseNpgsql(connectionString).Options))
        {
            await analyticsDb.Database.MigrateAsync();
        }

        var fixturePath = FindRepositoryFile("Api.Tests", "Fixtures", "operations-analytics-all-routes-seed.sql");
        var sql = await File.ReadAllTextAsync(fixturePath);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection)
        {
            CommandTimeout = 120
        };
        await command.ExecuteNonQueryAsync();
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

    private sealed record RouteVerdict(
        string WebRoute,
        string ApiRoute,
        int ExpectedCount,
        int ExecutedCount,
        string Verdict,
        string Evidence);

    private sealed class OperationsEndpointFactory(string connectionString) : WebApplicationFactory<global::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Database:AutoMigrate"] = "false",
                    ["StartupReadiness:GateApiTraffic"] = "false",
                    ["PROCESS_TYPE"] = "web",
                    ["Workers:Enabled"] = "false",
                    ["Caching:Provider"] = "disabled",
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["ConnectionStrings:AnalyticsConnection"] = connectionString,
                    ["ConnectionStrings:OpenProductTrainingConnection"] = connectionString
                }));

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
