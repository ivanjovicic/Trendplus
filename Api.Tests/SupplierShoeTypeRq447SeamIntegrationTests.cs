using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Integration")]
public sealed class SupplierShoeTypeRq447SeamIntegrationTests
    : IClassFixture<WebApplicationFactory<global::Program>>
{
    private const int StartupWarmupRetryLimit = 3;
    private const string JulyFrom = "2026-07-01";
    private const string JulyTo = "2026-07-07";
    private const string BoundaryFrom = "2026-09-01";
    private const string BoundaryTo = "2026-09-02";
    private readonly WebApplicationFactory<global::Program> _factory;

    public SupplierShoeTypeRq447SeamIntegrationTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory;
    }

    [OperationsIntegrationFact(DisplayName = "RQ447 OP2-01 date boundaries use the same half-open certified window")]
    public async Task Op201_DateBoundaries_ExcludeUpperBoundaryAndPreserveSignedRows()
    {
        await SeedSharedFixtureAsync();
        using var client = _factory.CreateClient();

        var dayOne = await GetJsonAsync(
            client,
            $"/api/analytics/supplier-sales-stats?fromDate={BoundaryFrom}&toDate={BoundaryTo}&dataScope=all");
        var dayTwo = await GetJsonAsync(
            client,
            "/api/analytics/supplier-sales-stats?fromDate=2026-09-02&toDate=2026-09-03&dataScope=all");

        // The canonical retail population excludes only DUG/KOREKCIJA headers.
        // Keep signed returns and the unknown-dimension bucket in the observed
        // total so the boundary proof cannot silently turn into a positive-only
        // or known-identity-only calculation.
        Assert.Equal(590m, dayOne.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal(6, dayOne.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(120m, dayTwo.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal(1, dayTwo.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
    }

    [OperationsIntegrationFact(DisplayName = "RQ447 OP2-04 canonical Supplier and Shoe Type route totals stay in parity")]
    public async Task Op204_CanonicalRouteFamilies_KeepScopeTotalsInParity()
    {
        await SeedSharedFixtureAsync();
        using var client = _factory.CreateClient();

        var supplier = await GetJsonAsync(
            client,
            $"/api/analytics/supplier-sales-stats?fromDate={JulyFrom}&toDate={JulyTo}&dataScope=all");
        var shoeType = await GetJsonAsync(
            client,
            $"/api/analytics/shoe-type-sales-stats?fromDate={JulyFrom}&toDate={JulyTo}&dataScope=all");

        Assert.Equal(
            supplier.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal(),
            shoeType.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal(
            supplier.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32(),
            shoeType.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal("all", supplier.GetProperty("dataScope").GetString());
        Assert.Equal("all", shoeType.GetProperty("dataScope").GetString());
    }

    [OperationsIntegrationFact(DisplayName = "RQ447 OP2-05 backend status remains distinct from actionability")]
    public async Task Op205_BackendRecommendationStatus_IsNotMaskedByActionabilityGate()
    {
        await SeedSharedFixtureAsync();
        using var client = _factory.CreateClient();
        var response = await GetJsonAsync(
            client,
            $"/api/analytics/supplier-sales-stats?fromDate={JulyFrom}&toDate={JulyTo}&dataScope=all");

        foreach (var row in response.GetProperty("suppliers").EnumerateArray())
        {
            var recommendation = row.GetProperty("recommendation");
            var status = recommendation.GetProperty("status").GetString();
            Assert.False(string.IsNullOrWhiteSpace(status));
            Assert.True(recommendation.TryGetProperty("recommendationAllowed", out var allowed));
            Assert.True(allowed.ValueKind is JsonValueKind.True or JsonValueKind.False);
        }
    }

    [OperationsIntegrationFact(DisplayName = "RQ447 OP2-14 requested and effective period remain inspectable")]
    public async Task Op214_PeriodMetadata_ReflectsRequestedAndEffectiveWindow()
    {
        await SeedSharedFixtureAsync();
        using var client = _factory.CreateClient();
        var response = await GetJsonAsync(
            client,
            "/api/analytics/shoe-type-sales-stats?fromDate=2026-07-01&toDate=2026-07-02&dataScope=all");

        Assert.Equal(DateTime.Parse("2026-07-01T00:00:00Z"), response.GetProperty("fromDate").GetDateTime());
        Assert.Equal(DateTime.Parse("2026-07-02T00:00:00Z"), response.GetProperty("toDate").GetDateTime());
        Assert.NotEqual(JsonValueKind.Null, response.GetProperty("dataWindowFrom").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, response.GetProperty("dataWindowTo").ValueKind);
        Assert.Equal("all", response.GetProperty("dataScope").GetString());
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var response = await client.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(body);
                return document.RootElement.Clone();
            }

            var isDatabaseWarmup = IsDatabaseWarmupResponse(response.StatusCode, body, out var retryAfterSeconds);
            if (attempt >= StartupWarmupRetryLimit || !isDatabaseWarmup)
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
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "A PostgreSQL connection string is required for RQ447 seam proof.");

        var fixturePath = FindRepositoryFile("Api.Tests", "Fixtures", "operations-analytics-all-routes-seed.sql");
        var sql = await File.ReadAllTextAsync(fixturePath);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
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
