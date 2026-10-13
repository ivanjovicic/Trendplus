using System.Net.Http;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Trendplus2.Tests.Analytics;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Integration")]
public sealed class SupplierShoeTypeIndependentOracleIntegrationTests
    : IClassFixture<WebApplicationFactory<global::Program>>
{
    private const string FromDate = "2026-07-01";
    private const string ToDate = "2026-07-07";

    private readonly WebApplicationFactory<global::Program> _factory;

    public SupplierShoeTypeIndependentOracleIntegrationTests(WebApplicationFactory<global::Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("StartupReadiness:GateApiTraffic", "false"));
    }

    [OperationsIntegrationFact(DisplayName = "Supplier and Shoe Type endpoints reconcile to the raw-fact oracle (RQ407 fixture, all scope)")]
    public async Task SharedFixture_EndpointsMatchIndependentOracle_AllScope()
    {
        await SeedSharedFixtureAsync();
        var filters = BuildFilters("all", storeId: null);
        await AssertEndpointsMatchOracleAsync(filters);
    }

    [OperationsIntegrationFact(DisplayName = "Supplier and Shoe Type oracle respects imported dataScope")]
    public async Task SharedFixture_EndpointsMatchIndependentOracle_ImportedScope()
    {
        await SeedSharedFixtureAsync();
        var filters = BuildFilters("imported", storeId: null);
        await AssertEndpointsMatchOracleAsync(filters);
    }

    [OperationsIntegrationFact(DisplayName = "Supplier and Shoe Type oracle respects existing dataScope and store filter")]
    public async Task SharedFixture_EndpointsMatchIndependentOracle_ExistingScopeAndStore()
    {
        await SeedSharedFixtureAsync();
        var filters = BuildFilters("existing", storeId: 1);
        await AssertEndpointsMatchOracleAsync(filters);
    }

    [OperationsIntegrationFact(DisplayName = "Supplier and Shoe Type scope follows sale header when article origin disagrees")]
    public async Task SharedFixture_HeaderOriginWinsOverCurrentArticleOrigin()
    {
        await SeedSharedFixtureAsync();
        await AssertEndpointsMatchOracleAsync(BuildFilters("imported", 1, "2026-09-10", "2026-09-11"));
        await AssertEndpointsMatchOracleAsync(BuildFilters("existing", 1, "2026-09-10", "2026-09-11"));
    }

    [OperationsIntegrationFact(DisplayName = "Historical supplier attribution ignores current article master mutation")]
    public async Task SharedFixture_AttributionFrozenAfterMasterMutation()
    {
        await SeedSharedFixtureAsync();
        var connectionString = RequireConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var mutate = new NpgsqlCommand(
                         """UPDATE "Artikli" SET "IDDobavljac" = 999, "IDTipObuce" = 999 WHERE "PLU" = 'OPS-101';""",
                         connection))
        {
            await mutate.ExecuteNonQueryAsync();
        }

        var filters = BuildFilters("all", storeId: null);
        await AssertEndpointsMatchOracleAsync(filters);
    }

    private async Task AssertEndpointsMatchOracleAsync(SupplierShoeTypeRawFactOracle.Filters filters)
    {
        var connectionString = RequireConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var oracleTotals = await SupplierShoeTypeRawFactOracle.QueryTotalsAsync(connection, filters);
        var oracleSuppliers = await SupplierShoeTypeRawFactOracle.QuerySupplierBucketsAsync(connection, filters);
        var oracleShoeTypes = await SupplierShoeTypeRawFactOracle.QueryShoeTypeBucketsAsync(connection, filters);

        Assert.Equal(oracleTotals.SaleLineCount, oracleSuppliers.Sum(b => b.SaleLineCount));
        Assert.Equal(oracleTotals.SaleLineCount, oracleShoeTypes.Sum(b => b.SaleLineCount));
        Assert.Equal(oracleTotals.TotalUnits, oracleSuppliers.Sum(b => b.Units));
        Assert.Equal(oracleTotals.TotalUnits, oracleShoeTypes.Sum(b => b.Units));
        Assert.Equal(oracleTotals.TotalRevenue, oracleSuppliers.Sum(b => b.Revenue));
        Assert.Equal(oracleTotals.TotalRevenue, oracleShoeTypes.Sum(b => b.Revenue));

        using var client = _factory.CreateClient();
        await WaitForActualReadinessAsync(client);
        var scopeQuery = $"&dataScope={Uri.EscapeDataString(filters.DataScope)}";
        var storeQuery = filters.StoreId.HasValue ? $"&storeId={filters.StoreId.Value}" : string.Empty;
        var fromQuery = $"fromDate={filters.FromUtc:yyyy-MM-dd}&toDate={filters.ToUtc:yyyy-MM-dd}";

        var supplier = await GetJsonAsync(
            client,
            $"/api/analytics/supplier-sales-stats?{fromQuery}{scopeQuery}{storeQuery}");
        var shoeType = await GetJsonAsync(
            client,
            $"/api/analytics/shoe-type-sales-stats?{fromQuery}{scopeQuery}{storeQuery}");

        var endpointSupplierRevenue = supplier.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal();
        var endpointSupplierUnits = supplier.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32();
        var endpointShoeRevenue = shoeType.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal();
        var endpointShoeUnits = shoeType.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32();

        Assert.Equal(oracleTotals.TotalRevenue, endpointSupplierRevenue);
        Assert.Equal(oracleTotals.TotalUnits, endpointSupplierUnits);
        Assert.Equal(oracleTotals.TotalRevenue, endpointShoeRevenue);
        Assert.Equal(oracleTotals.TotalUnits, endpointShoeUnits);

        var supplierRows = supplier.GetProperty("suppliers").EnumerateArray().ToArray();
        AssertSupplierRowsMatchOracle(supplierRows, oracleSuppliers, oracleTotals.TotalRevenue);

        var shoeRows = shoeType.GetProperty("shoeTypes").EnumerateArray().ToArray();
        AssertShoeTypeRowsMatchOracle(shoeRows, oracleShoeTypes, oracleTotals.TotalRevenue);
    }

    private static void AssertSupplierRowsMatchOracle(
        JsonElement[] supplierRows,
        IReadOnlyList<SupplierShoeTypeRawFactOracle.Bucket> oracleBuckets,
        decimal oracleTotalRevenue)
    {
        var oracleById = oracleBuckets.ToDictionary(
            b => b.DimensionId?.ToString() ?? "unknown",
            b => b,
            StringComparer.Ordinal);

        var endpointRevenue = 0m;
        var endpointUnits = 0;
        foreach (var row in supplierRows)
        {
            var isUnknown = row.GetProperty("isUnknown").GetBoolean();
            int? supplierId = row.TryGetProperty("dobavljacId", out var idElement) && idElement.ValueKind != JsonValueKind.Null
                ? idElement.GetInt32()
                : null;

            if (isUnknown)
                supplierId = null;

            var supplierKey = supplierId?.ToString() ?? "unknown";
            Assert.True(oracleById.TryGetValue(supplierKey, out var oracleBucket), $"Unexpected supplier row id={supplierKey}");
            var rowRevenue = row.GetProperty("ukupanPromet").GetDecimal();
            var rowUnits = row.GetProperty("ukupnaKolicina").GetInt32();
            Assert.Equal(oracleBucket.Revenue, rowRevenue);
            Assert.Equal(oracleBucket.Units, rowUnits);
            endpointRevenue += rowRevenue;
            endpointUnits += rowUnits;

            if (oracleTotalRevenue > 0m)
            {
                var share = row.GetProperty("sharePct").GetDouble();
                var expectedShare = Math.Round((double)(rowRevenue / oracleTotalRevenue * 100m), 2);
                Assert.Equal(expectedShare, share, precision: 2);
            }
        }

        Assert.Equal(oracleTotalRevenue, endpointRevenue);
        Assert.Equal(oracleBuckets.Sum(b => b.Units), endpointUnits);
        Assert.Equal(oracleBuckets.Count, supplierRows.Length);
    }

    private static void AssertShoeTypeRowsMatchOracle(
        JsonElement[] shoeRows,
        IReadOnlyList<SupplierShoeTypeRawFactOracle.Bucket> oracleBuckets,
        decimal oracleTotalRevenue)
    {
        var oracleById = oracleBuckets.ToDictionary(
            b => b.DimensionId?.ToString() ?? "unknown",
            b => b,
            StringComparer.Ordinal);

        var endpointRevenue = 0m;
        var endpointUnits = 0;
        foreach (var row in shoeRows)
        {
            var isUnknown = row.TryGetProperty("isUnknown", out var unknownElement) && unknownElement.GetBoolean();
            int? shoeTypeId = row.TryGetProperty("tipObuceId", out var idElement) && idElement.ValueKind != JsonValueKind.Null
                ? idElement.GetInt32()
                : null;

            if (isUnknown)
                shoeTypeId = null;

            var shoeTypeKey = shoeTypeId?.ToString() ?? "unknown";
            Assert.True(oracleById.TryGetValue(shoeTypeKey, out var oracleBucket), $"Unexpected shoe type row id={shoeTypeKey}");
            var rowRevenue = row.GetProperty("ukupanPromet").GetDecimal();
            var rowUnits = row.GetProperty("ukupnaKolicina").GetInt32();
            Assert.Equal(oracleBucket.Revenue, rowRevenue);
            Assert.Equal(oracleBucket.Units, rowUnits);
            endpointRevenue += rowRevenue;
            endpointUnits += rowUnits;

            if (oracleTotalRevenue > 0m)
            {
                var share = row.GetProperty("sharePct").GetDouble();
                var expectedShare = Math.Round((double)(rowRevenue / oracleTotalRevenue * 100m), 2);
                Assert.Equal(expectedShare, share, precision: 2);
            }
        }

        Assert.Equal(oracleTotalRevenue, endpointRevenue);
        Assert.Equal(oracleBuckets.Sum(b => b.Units), endpointUnits);
        Assert.Equal(oracleBuckets.Count, shoeRows.Length);
    }

    private static SupplierShoeTypeRawFactOracle.Filters BuildFilters(
        string dataScope,
        int? storeId,
        string fromDate = FromDate,
        string toDate = ToDate)
    {
        var fromUtc = DateTime.SpecifyKind(DateTime.Parse(fromDate), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(DateTime.Parse(toDate), DateTimeKind.Utc);
        return new SupplierShoeTypeRawFactOracle.Filters(fromUtc, toUtc, storeId, dataScope);
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{path} returned {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task WaitForActualReadinessAsync(HttpClient client)
    {
        // CI runs the actual PostgreSQL warmup in Testing. The 180s bound covers
        // 12 attempts, two 5s DB probes per attempt and 5s backoff (175s maximum).
        // Keep readiness real before analytics requests; never disable the API gate.
        var timeout = TimeSpan.FromSeconds(180);
        var stopwatch = Stopwatch.StartNew();
        string? lastBody = null;
        HttpStatusCode? lastStatus = null;

        while (stopwatch.Elapsed < timeout)
        {
            using var response = await client.GetAsync("/ready");
            lastStatus = response.StatusCode;
            lastBody = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(lastBody);
            var root = document.RootElement;
            var reason = root.TryGetProperty("reason", out var reasonValue)
                ? reasonValue.GetString()
                : null;
            var initializationState = root.TryGetProperty("databaseInitialization", out var initialization)
                && initialization.TryGetProperty("state", out var stateValue)
                    ? stateValue.GetString()
                    : null;

            if (string.Equals(initializationState, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "schema_incompatible", StringComparison.OrdinalIgnoreCase)
                || string.Equals(reason, "schema_verification_failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"API startup reached a terminal readiness failure after {stopwatch.Elapsed}. " +
                    $"HTTP {(int)response.StatusCode}: {lastBody}");
            }

            var ready = root.TryGetProperty("ready", out var readyValue) && readyValue.GetBoolean();
            var databasesOk = root.TryGetProperty("db", out var dbValue)
                && dbValue.TryGetProperty("ok", out var dbOkValue)
                && dbOkValue.GetBoolean();
            if (response.IsSuccessStatusCode && ready && databasesOk)
                return;

            if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
            {
                throw new InvalidOperationException(
                    $"Unexpected /ready response during RQ453 startup. HTTP {(int)response.StatusCode}: {lastBody}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(
            $"API did not become ready within {timeout}. Last /ready response: HTTP {(int?)lastStatus}: {lastBody}");
    }

    private static string RequireConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("TRENDPLUS_TEST_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString), "A PostgreSQL connection string is required for the independent oracle proof.");
        return connectionString;
    }

    private static async Task SeedSharedFixtureAsync()
    {
        var connectionString = RequireConnectionString();
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
