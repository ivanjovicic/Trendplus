using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Trendplus2.Tests;

/// <summary>
/// Explicit cross-screen promet equality on one fixture/window.
/// RQ561 already asserts the same absolute numbers separately; this test binds
/// Daily == Supplier == Shoe Type == Color to each other so one screen cannot
/// drift while the others stay green.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Suite", "AnalyticsAdversarial")]
public sealed class AnalyticsCrossScreenRevenueInvariantIntegrationTests
    : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;

    public AnalyticsCrossScreenRevenueInvariantIntegrationTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory;
    }

    [OperationsIntegrationFact(DisplayName = "Cross-screen promet: Daily == Supplier == Shoe Type == Color (July RQ407 window)")]
    public async Task JulyWindow_FourScreens_ShareIdenticalCertifiedPromet()
    {
        await SeedSharedFixtureAsync();
        using var client = _factory.CreateClient();
        const string from = "2026-07-01";
        const string to = "2026-07-07";
        const string query = $"fromDate={from}&toDate={to}&dataScope=all";

        var supplier = await GetJsonAsync(client, $"/api/analytics/supplier-sales-stats?{query}");
        var shoeType = await GetJsonAsync(client, $"/api/analytics/shoe-type-sales-stats?{query}");
        var color = await GetJsonAsync(client, $"/api/analytics/color-sales-stats?{query}");
        var daily = await GetJsonAsync(client, $"/api/analytics/daily-sales?{query}");

        var supplierRevenue = supplier.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal();
        var shoeRevenue = shoeType.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal();
        var colorRevenue = color.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal();
        var dailyRevenue = daily.GetProperty("dateRows").EnumerateArray()
            .Sum(row => row.GetProperty("totalRevenue").GetDecimal());

        var supplierUnits = supplier.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32();
        var shoeUnits = shoeType.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32();
        var colorUnits = color.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32();
        var dailyUnits = daily.GetProperty("metadata").GetProperty("totalItemsInRange").GetInt32();

        // Hand expected from RQ407 fixture / independent oracle.
        Assert.Equal(540m, supplierRevenue);
        Assert.Equal(supplierRevenue, shoeRevenue);
        Assert.Equal(supplierRevenue, colorRevenue);
        Assert.Equal(supplierRevenue, dailyRevenue);
        Assert.Equal(5, supplierUnits);
        Assert.Equal(supplierUnits, shoeUnits);
        Assert.Equal(supplierUnits, colorUnits);
        Assert.Equal(supplierUnits, dailyUnits);
    }

    [OperationsIntegrationFact(DisplayName = "Cross-screen promet: store isolation stays equal across four screens")]
    public async Task ImportedStore2_FourScreens_ShareIdenticalCertifiedPromet()
    {
        await SeedSharedFixtureAsync();
        using var client = _factory.CreateClient();
        const string query = "fromDate=2026-09-01&toDate=2026-09-02&dataScope=imported&storeId=2";

        var supplier = await GetJsonAsync(client, $"/api/analytics/supplier-sales-stats?{query}");
        var shoeType = await GetJsonAsync(client, $"/api/analytics/shoe-type-sales-stats?{query}");
        var color = await GetJsonAsync(client, $"/api/analytics/color-sales-stats?{query}");
        var daily = await GetJsonAsync(client, $"/api/analytics/daily-sales?{query}&topN=25");

        var supplierRevenue = supplier.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal();
        var shoeRevenue = shoeType.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal();
        var colorRevenue = color.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal();
        var dailyRevenue = daily.GetProperty("dateRows").EnumerateArray()
            .Sum(row => row.GetProperty("totalRevenue").GetDecimal());

        Assert.Equal(240m, supplierRevenue);
        Assert.Equal(supplierRevenue, shoeRevenue);
        Assert.Equal(supplierRevenue, colorRevenue);
        Assert.Equal(supplierRevenue, dailyRevenue);
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{path} returned {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task SeedSharedFixtureAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("TRENDPLUS_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "PostgreSQL connection string required.");

        var fixturePath = FindRepositoryFile("Api.Tests", "Fixtures", "operations-analytics-all-routes-seed.sql");
        var sql = await File.ReadAllTextAsync(fixturePath);
        await using var connection = new Npgsql.NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
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
