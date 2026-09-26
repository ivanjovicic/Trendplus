using System.Net;
using System.Net.Http;
using System.Text.Json;
using Npgsql;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Integration")]
public sealed class OperationsAnalyticsAllRoutesIntegrationTests
    : IClassFixture<WebApplicationFactory<global::Program>>
{
    private const int StartupWarmupMaxAttempts = 3;
    private const string FromDate = "2026-07-01";
    private const string ToDate = "2026-07-07";
    private const string NivelacijaEventDate = "2026-08-01T00:00:00Z";

    private readonly WebApplicationFactory<global::Program> _factory;

    public OperationsAnalyticsAllRoutesIntegrationTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory;
    }

    [OperationsIntegrationFact(DisplayName = "One deterministic source reconciles all eight Operations route families")]
    public async Task SharedFixture_ReconcilesAllEightRouteFamilies()
    {
        await SeedSharedFixtureAsync();
        using var client = _factory.CreateClient();

        var inventory = await GetJsonAsync(client, "/api/analytics/inventory/list?page=1&pageSize=100&dataScope=all");
        var inventoryItems = inventory.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(inventoryItems, item => item.GetProperty("plu").GetString() == "OOS-101");
        Assert.Contains(inventoryItems, item => item.GetProperty("plu").GetString() == "EMPTY-104");
        Assert.Contains(
            inventoryItems,
            item => item.GetProperty("plu").GetString() == "OOS-101"
                && item.GetProperty("recommendationAllowed").GetBoolean() == false);

        var supplier = await GetJsonAsync(
            client,
            $"/api/analytics/supplier-sales-stats?fromDate={FromDate}&toDate={ToDate}&dataScope=all");
        Assert.Equal(5, supplier.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(540m, supplier.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());

        var shoeType = await GetJsonAsync(
            client,
            $"/api/analytics/shoe-type-sales-stats?fromDate={FromDate}&toDate={ToDate}&dataScope=all");
        Assert.Equal(5, shoeType.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(540m, shoeType.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());

        var daily = await GetJsonAsync(
            client,
            $"/api/analytics/daily-sales?fromDate={FromDate}&toDate={ToDate}&dataScope=all");
        var dailyMetadata = daily.GetProperty("metadata");
        Assert.Equal(5, dailyMetadata.GetProperty("totalItemsInRange").GetInt32());
        Assert.Equal(
            540m,
            daily.GetProperty("dateRows").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));

        var vendorNivelacija = await GetJsonAsync(
            client,
            $"/api/analytics/vendor-sales-nivelacija?eventDate={Uri.EscapeDataString(NivelacijaEventDate)}&dataScope=all");
        Assert.True(
            vendorNivelacija.GetProperty("meta").GetProperty("success").GetBoolean(),
            vendorNivelacija.GetRawText());
        Assert.Equal(200m, vendorNivelacija.GetProperty("totals").GetProperty("preRevenue").GetDecimal());
        Assert.Equal(270m, vendorNivelacija.GetProperty("totals").GetProperty("postRevenue").GetDecimal());
        Assert.Equal(35m, vendorNivelacija.GetProperty("totals").GetProperty("changePercent").GetDecimal());

        var color = await GetJsonAsync(
            client,
            $"/api/analytics/color-sales-stats?fromDate={FromDate}&toDate={ToDate}&dataScope=all");
        Assert.Equal(3, color.GetProperty("colors").GetArrayLength());
        Assert.Equal(5, color.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(540m, color.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());

        var preNivelacija = await GetJsonAsync(
            client,
            "/api/analytics/pre-nivelacija-prioriteti?dataScope=all&page=1&pageSize=100&focus=all");
        Assert.True(preNivelacija.GetProperty("meta").GetProperty("success").GetBoolean());
        Assert.Equal(1, preNivelacija.GetProperty("totalCandidates").GetInt32());
        Assert.Contains(
            preNivelacija.GetProperty("candidates").EnumerateArray(),
            candidate => candidate.GetProperty("sku").GetString() == "PRE-105");

        // Supplier Footwear is the type-insight projection of the same
        // vendor/nivelacija fact response, so assert the projection contract too.
        Assert.True(vendorNivelacija.GetProperty("typeInsightsAuthoritative").GetBoolean());
        Assert.Equal(1, vendorNivelacija.GetProperty("articleStats").GetArrayLength());
        Assert.Equal("NIV-101", vendorNivelacija.GetProperty("articleStats")[0].GetProperty("sku").GetString());
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

    private static async Task SeedSharedFixtureAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("TRENDPLUS_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "A PostgreSQL connection string is required for the live Operations proof.");

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
}
