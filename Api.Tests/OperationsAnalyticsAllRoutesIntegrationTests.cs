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
        var requestCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        using var client = factory.CreateDefaultClient(new RouteExecutionCounter(requestCounts));
        var verdicts = new List<RouteVerdict>(capacity: 6);

        var inventory = await GetJsonAsync(client, "/api/analytics/inventory/list?page=1&pageSize=100&dataScope=all");
        var inventoryItems = inventory.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(inventoryItems, item => item.GetProperty("plu").GetString() == "OOS-101");
        Assert.Contains(inventoryItems, item => item.GetProperty("plu").GetString() == "EMPTY-104");
        Assert.Contains(
            inventoryItems,
            item => item.GetProperty("plu").GetString() == "OOS-101"
                && item.GetProperty("recommendationAllowed").GetBoolean() == false);
        var importedInventory = await GetJsonAsync(
            client,
            "/api/analytics/inventory/list?page=1&pageSize=100&storeId=2&dataScope=imported");
        var importedInventoryItems = importedInventory.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(importedInventoryItems, item => item.GetProperty("plu").GetString() == "OPS-102");
        Assert.DoesNotContain(importedInventoryItems, item => item.GetProperty("plu").GetString() == "EMPTY-104");
        var existingInventory = await GetJsonAsync(
            client,
            "/api/analytics/inventory/list?page=1&pageSize=100&storeId=2&dataScope=existing");
        Assert.Contains(existingInventory.GetProperty("items").EnumerateArray(), item => item.GetProperty("plu").GetString() == "EMPTY-104");
        Assert.DoesNotContain(existingInventory.GetProperty("items").EnumerateArray(), item => item.GetProperty("plu").GetString() == "OPS-102");
        verdicts.Add(new("/analytics/inventory", "/api/analytics/inventory/list", 3, 3, "PASS", "empty-stock, unavailable recommendation, store and product-origin scope asserted"));

        var shoeType = await GetJsonAsync(
            client,
            $"/api/analytics/shoe-type-sales-stats?fromDate={FromDate}&toDate={ToDate}&dataScope=all");
        Assert.Equal(5, shoeType.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(540m, shoeType.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        var shoeTypeBoundary = await GetJsonAsync(
            client,
            "/api/analytics/shoe-type-sales-stats?fromDate=2026-09-01&toDate=2026-09-02&dataScope=all");
        Assert.Equal(6, shoeTypeBoundary.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(590m, shoeTypeBoundary.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal(440m, shoeTypeBoundary.GetProperty("dataQuality").GetProperty("costCoveredRevenue").GetDecimal());
        Assert.Equal(150m, shoeTypeBoundary.GetProperty("dataQuality").GetProperty("noCostRevenue").GetDecimal());
        Assert.All(shoeTypeBoundary.GetProperty("shoeTypes").EnumerateArray(), bucket =>
        {
            Assert.Equal("net_sales_signed", bucket.GetProperty("sharePctBasis").GetString());
            Assert.Equal(590d, bucket.GetProperty("sharePctDenominator").GetDouble());
        });
        Assert.Equal("all", shoeTypeBoundary.GetProperty("dataScope").GetString());
        Assert.Equal("all", shoeTypeBoundary.GetProperty("meta").GetProperty("effectiveDataScope").GetString());
        Assert.Equal("2026-09-01T00:00:00Z", shoeTypeBoundary.GetProperty("fromDate").GetString());
        Assert.NotEqual(JsonValueKind.Null, shoeTypeBoundary.GetProperty("dataWindowFrom").ValueKind);
        var previousOnlyType = Assert.Single(
            shoeTypeBoundary.GetProperty("shoeTypes").EnumerateArray(),
            bucket => bucket.GetProperty("tipObuceId").ValueKind == JsonValueKind.Number
                && bucket.GetProperty("tipObuceId").GetInt32() == 5);
        Assert.True(previousOnlyType.GetProperty("isPreviousOnly").GetBoolean());
        Assert.Equal(180m, previousOnlyType.GetProperty("previousPeriodRevenue").GetDecimal());
        Assert.Equal(0m, previousOnlyType.GetProperty("ukupanPromet").GetDecimal());
        var shoeTypeImportedStore2 = await GetJsonAsync(
            client,
            "/api/analytics/shoe-type-sales-stats?fromDate=2026-09-01&toDate=2026-09-02&dataScope=imported&storeId=2");
        Assert.Equal(2, shoeTypeImportedStore2.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(240m, shoeTypeImportedStore2.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal("imported", shoeTypeImportedStore2.GetProperty("dataScope").GetString());
        Assert.Equal(2, shoeTypeImportedStore2.GetProperty("shoeTypes").GetArrayLength());
        var shoeHeaderImported = await GetJsonAsync(
            client,
            "/api/analytics/shoe-type-sales-stats?fromDate=2026-09-10&toDate=2026-09-11&dataScope=imported");
        var shoeHeaderExisting = await GetJsonAsync(
            client,
            "/api/analytics/shoe-type-sales-stats?fromDate=2026-09-10&toDate=2026-09-11&dataScope=existing");
        Assert.Equal(200m, shoeHeaderImported.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal(390m, shoeHeaderExisting.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        var shoeTypeNextDay = await GetJsonAsync(
            client,
            "/api/analytics/shoe-type-sales-stats?fromDate=2026-09-02&toDate=2026-09-03&dataScope=all");
        Assert.Equal(1, shoeTypeNextDay.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(120m, shoeTypeNextDay.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        verdicts.Add(new("/analytics/shoe-type-sales-stats", "/api/analytics/shoe-type-sales-stats", 6, 6, "PASS", "July baseline, RQ446 half-open/adjacent-day boundaries, signed return, previous-only category, cost/denominators, store scope and RQ494 sale-header provenance asserted"));

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
        Assert.Equal("all", daily.GetProperty("meta").GetProperty("effectiveDataScope").GetString());
        Assert.Equal("sale_snapshot", daily.GetProperty("meta").GetProperty("attributionBasis").GetString());
        Assert.Equal("Europe/Belgrade", daily.GetProperty("metadata").GetProperty("shiftTimeZone").GetString());
        Assert.Equal("mixed", daily.GetProperty("metadata").GetProperty("shiftTimestampBasis").GetString());
        var dailyBoundary = await GetJsonAsync(
            client,
            "/api/analytics/daily-sales?fromDate=2026-09-01&toDate=2026-09-01&dataScope=all&topN=25");
        Assert.Equal(6, dailyBoundary.GetProperty("metadata").GetProperty("totalItemsInRange").GetInt32());
        Assert.Equal(590m, dailyBoundary.GetProperty("dateRows").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(9, dailyBoundary.GetProperty("metadata").GetProperty("nonStandardReceiptCount").GetInt32());
        Assert.Equal("all", dailyBoundary.GetProperty("metadata").GetProperty("diagnosticsDataScope").GetString());
        Assert.Equal("all", dailyBoundary.GetProperty("meta").GetProperty("effectiveDataScope").GetString());
        var dailyImportedStore2 = await GetJsonAsync(
            client,
            "/api/analytics/daily-sales?fromDate=2026-09-01&toDate=2026-09-01&dataScope=imported&storeId=2&topN=25");
        Assert.Equal(2, dailyImportedStore2.GetProperty("metadata").GetProperty("totalItemsInRange").GetInt32());
        Assert.Equal(240m, dailyImportedStore2.GetProperty("dateRows").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(2, dailyImportedStore2.GetProperty("storeId").GetInt32());
        Assert.Equal("imported", dailyImportedStore2.GetProperty("meta").GetProperty("effectiveDataScope").GetString());
        var dailyHeaderImported = await GetJsonAsync(
            client,
            "/api/analytics/daily-sales?fromDate=2026-09-10&toDate=2026-09-10&dataScope=imported&topN=25");
        var dailyHeaderExisting = await GetJsonAsync(
            client,
            "/api/analytics/daily-sales?fromDate=2026-09-10&toDate=2026-09-10&dataScope=existing&topN=25");
        Assert.Equal(2, dailyHeaderImported.GetProperty("metadata").GetProperty("totalItemsInRange").GetInt32());
        Assert.Equal(200m, dailyHeaderImported.GetProperty("dateRows").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(3, dailyHeaderExisting.GetProperty("metadata").GetProperty("totalItemsInRange").GetInt32());
        Assert.Equal(390m, dailyHeaderExisting.GetProperty("dateRows").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        verdicts.Add(new("/analytics/daily-sales", "/api/analytics/daily-sales", 5, 5, "PASS", "period, source attribution, DUG diagnostic, signed/boundary total, store scope and RQ494 header-origin population asserted"));

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
        var colorBoundary = await GetJsonAsync(
            client,
            "/api/analytics/color-sales-stats?fromDate=2026-09-01&toDate=2026-09-02&dataScope=all");
        Assert.Equal(6, colorBoundary.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(590m, colorBoundary.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.All(colorBoundary.GetProperty("colors").EnumerateArray(), bucket =>
        {
            Assert.Equal("net_sales_signed", bucket.GetProperty("sharePctBasis").GetString());
            Assert.Equal(590d, bucket.GetProperty("sharePctDenominator").GetDouble());
        });
        Assert.Equal("all", colorBoundary.GetProperty("dataScope").GetString());
        Assert.Equal("sales_header_origin_all_origins_allowed", colorBoundary.GetProperty("lineage").GetProperty("originPolicy").GetString());
        var colorImportedStore2 = await GetJsonAsync(
            client,
            "/api/analytics/color-sales-stats?fromDate=2026-09-01&toDate=2026-09-02&dataScope=imported&storeId=2");
        Assert.Equal(2, colorImportedStore2.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(240m, colorImportedStore2.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal("imported", colorImportedStore2.GetProperty("dataScope").GetString());
        Assert.Equal(2, colorImportedStore2.GetProperty("colors").GetArrayLength());
        var colorHeaderImported = await GetJsonAsync(
            client,
            "/api/analytics/color-sales-stats?fromDate=2026-09-10&toDate=2026-09-11&dataScope=imported");
        var colorHeaderExisting = await GetJsonAsync(
            client,
            "/api/analytics/color-sales-stats?fromDate=2026-09-10&toDate=2026-09-11&dataScope=existing");
        Assert.Equal(200m, colorHeaderImported.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal(390m, colorHeaderExisting.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        verdicts.Add(new("/analytics/color-sales-stats", "/api/analytics/color-sales-stats", 5, 5, "PASS", "visible color buckets, RQ446 totals, lineage, store scope, bucket denominators and RQ494 header-origin population asserted"));

        var preNivelacija = await GetJsonAsync(
            client,
            "/api/analytics/pre-nivelacija-prioriteti?dataScope=all&page=1&pageSize=100&focus=all");
        Assert.True(preNivelacija.GetProperty("meta").GetProperty("success").GetBoolean());
        Assert.Equal(1, preNivelacija.GetProperty("totalCandidates").GetInt32());
        Assert.Contains(
            preNivelacija.GetProperty("candidates").EnumerateArray(),
            candidate => candidate.GetProperty("sku").GetString() == "PRE-105");
        var preNivelacijaImported = await GetJsonAsync(
            client,
            "/api/analytics/pre-nivelacija-prioriteti?dataScope=imported&page=1&pageSize=100&focus=all");
        Assert.Equal("imported", preNivelacijaImported.GetProperty("meta").GetProperty("effectiveDataScope").GetString());
        Assert.Equal("pre_nivelacija_product_origin_filter", preNivelacijaImported.GetProperty("meta").GetProperty("dataScopeSource").GetString());
        Assert.DoesNotContain(
            preNivelacijaImported.GetProperty("candidates").EnumerateArray(),
            candidate => candidate.GetProperty("sku").GetString() == "PRE-105");
        verdicts.Add(new("/analytics/pre-nivelacija-prioriteti", "/api/analytics/pre-nivelacija-prioriteti", 2, 2, "PASS", "candidate count, visible SKU and imported-scope exclusion asserted"));

        await SeedNegativeEntityCaseAsync(connectionString!);
        var negativeStoreInventory = await GetJsonAsync(
            client,
            "/api/analytics/inventory/list?page=1&pageSize=100&storeId=-1&dataScope=all");
        var negativeStoreItems = negativeStoreInventory.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(negativeStoreItems, item => item.GetProperty("plu").GetString() == "RQ561-NEGATIVE-ENTITY");
        Assert.Contains(negativeStoreItems, item => item.GetProperty("plu").GetString() == "RQ561-NEGATIVE-NULL");

        var negativeStoreShoeType = await GetJsonAsync(
            client,
            "/api/analytics/shoe-type-sales-stats?fromDate=2026-09-12&toDate=2026-09-13&storeId=-1&dataScope=all");
        Assert.Equal(2, negativeStoreShoeType.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(250m, negativeStoreShoeType.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Contains(negativeStoreShoeType.GetProperty("shoeTypes").EnumerateArray(), bucket =>
            bucket.GetProperty("tipObuceId").ValueKind == JsonValueKind.Number
            && bucket.GetProperty("tipObuceId").GetInt32() == -1
            && bucket.GetProperty("ukupanPromet").GetDecimal() == 125m);
        Assert.Contains(negativeStoreShoeType.GetProperty("shoeTypes").EnumerateArray(), bucket =>
            bucket.GetProperty("tipObuceId").ValueKind == JsonValueKind.Null
            && bucket.GetProperty("ukupanPromet").GetDecimal() == 125m);

        var negativeStoreDaily = await GetJsonAsync(
            client,
            "/api/analytics/daily-sales?fromDate=2026-09-12&toDate=2026-09-12&storeId=-1&dataScope=all&topN=25");
        Assert.Equal(2, negativeStoreDaily.GetProperty("metadata").GetProperty("totalItemsInRange").GetInt32());
        Assert.Equal(250m, negativeStoreDaily.GetProperty("dateRows").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(-1, negativeStoreDaily.GetProperty("storeId").GetInt32());

        var negativeStoreColor = await GetJsonAsync(
            client,
            "/api/analytics/color-sales-stats?fromDate=2026-09-12&toDate=2026-09-13&storeId=-1&dataScope=all");
        Assert.Equal(2, negativeStoreColor.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(250m, negativeStoreColor.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal(-1, negativeStoreColor.GetProperty("storeId").GetInt32());
        verdicts[0] = verdicts[0] with { ExpectedCount = 4, Evidence = "empty-stock, unavailable recommendation, product-origin scopes, store 2 and real negative store -1 asserted" };
        verdicts[1] = verdicts[1] with { ExpectedCount = 7, Evidence = "July baseline, RQ446 boundaries/returns/previous-only/cost/denominators, store-origin provenance and distinct -1 versus null dimensions asserted" };
        verdicts[2] = verdicts[2] with { ExpectedCount = 6, Evidence = "period/source attribution, UTC plus legacy Access wall-clock bases, DUG diagnostics, RQ446/RQ494 scope, store 2 and negative store -1 asserted" };
        verdicts[4] = verdicts[4] with { ExpectedCount = 6, Evidence = "visible buckets, signed boundary totals, lineage/denominators, RQ494 header origin, store 2 and negative store -1 asserted" };

        for (var index = 0; index < verdicts.Count; index++)
        {
            var current = verdicts[index];
            verdicts[index] = current with
            {
                ExecutedCount = requestCounts.GetValueOrDefault(current.ApiRoute)
            };
        }

        var report = new
        {
            manifestId = "operations-six-screen-certification-2026-10-03",
            expectedRoutes = 6,
            executedRoutes = verdicts.Count(item => item.ExecutedCount > 0),
            expectedCases = 26,
            executedCases = verdicts.Sum(item => item.ExpectedCount),
            verdict = verdicts.Any(item => item.Verdict != "PASS") ? "UNVERIFIED" : "PASS",
            routes = verdicts
        };
        _output.WriteLine(JsonSerializer.Serialize(report));
        Assert.Equal(6, verdicts.Count);
        Assert.All(verdicts, item => Assert.Equal(item.ExpectedCount, item.ExecutedCount));
        Assert.Equal(6, report.executedRoutes);
        Assert.Equal(report.expectedCases, report.executedCases);
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

    private static async Task SeedNegativeEntityCaseAsync(string connectionString)
    {
        const string sql = """
            INSERT INTO "Dobavljaci" ("Id", "Naziv", "DataOrigin")
            OVERRIDING SYSTEM VALUE VALUES (-1, 'RQ561 negative supplier', 'existing');
            INSERT INTO "TipoviObuce" ("Id", "Naziv", "DataOrigin")
            OVERRIDING SYSTEM VALUE VALUES (-1, 'RQ561 negative shoe type', 'existing');

            INSERT INTO "Artikli"
              ("PLU", "Naziv", "NabavnaCena", "NabavnaCenaDin", "PrvaProdajnaCena", "ProdajnaCena",
               "IDDobavljac", "IDTipObuce", "UpdatedAt", "Kolicina", "MinimalnaKolicina", "IDObjekat",
               "IDSezona", "Kategorija", "Pol", "Velicina", "Boja", "DataOrigin")
            VALUES
              ('RQ561-NEGATIVE-ENTITY', 'RQ561 negative IDs stay real', 75, 75, 125, 125, -1, -1,
               '2026-09-12T00:00:00Z', 1, 0, -1, 1, 'Obuca', 'Unisex', '42', 'Negativna', 'existing'),
              ('RQ561-NEGATIVE-NULL', 'RQ561 null IDs stay distinct', NULL, NULL, 125, 125, NULL, NULL,
               '2026-09-12T00:00:00Z', 1, 0, -1, 1, 'Obuca', 'Unisex', '41', 'Nepoznata', 'existing');

            INSERT INTO prodaja_zaglavlje
              (id, broj_racuna, datum_prodaje, id_objekat, korisnik_ime, data_origin, source_timestamp_basis)
            VALUES
              (24, 'RQ561-NEGATIVE-ENTITY', '2026-09-12T09:00:00Z', -1, 'rq561', 'existing', 'utc_instant'),
              (25, 'RQ561-NEGATIVE-NULL', '2026-09-12T10:00:00Z', -1, 'rq561', 'existing', 'utc_instant');

            INSERT INTO prodaja_stavke
              (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
            SELECT 24, 24, a."Id", 1, 125, 75, -1, -1, 'sale_snapshot'
              FROM "Artikli" a WHERE a."PLU" = 'RQ561-NEGATIVE-ENTITY';
            INSERT INTO prodaja_stavke
              (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
            SELECT 25, 25, a."Id", 1, 125, NULL, NULL, NULL, 'sale_snapshot'
              FROM "Artikli" a WHERE a."PLU" = 'RQ561-NEGATIVE-NULL';
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 30 };
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

    private sealed class RouteExecutionCounter(IDictionary<string, int> counts) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            var path = request.RequestUri?.AbsolutePath;
            if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(path))
            {
                counts.TryGetValue(path, out var currentCount);
                counts[path] = currentCount + 1;
            }

            return response;
        }
    }

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
                    ["ConnectionStrings:OpenProductTrainingConnection"] = connectionString,
                    ["DailySales:TimeZoneId"] = "Europe/Belgrade"
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
