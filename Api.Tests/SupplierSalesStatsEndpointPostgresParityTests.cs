using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Application.Config;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests;

public sealed class SupplierSalesStatsEndpointPostgresParityTests : IClassFixture<PostgresContainerFixture>
{
    private const string RequestPath = "/api/analytics/supplier-sales-stats?fromDate=2026-07-01&toDate=2026-07-07&dataScope=all";
    private readonly PostgresContainerFixture _fixture;

    public SupplierSalesStatsEndpointPostgresParityTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SupplierEndpoint_PostgresParity_CacheHitAndIntegrityChangePreserveValuesAndRefreshGate()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"rq487_supplier_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await CreateFixtureAsync(connectionString!);

        var cache = new RecordingAnalyticsCacheService();
        var registry = new OperationsAnalyticsIntegrityRegistry();
        SetBoundSupplierEvidence(
            registry,
            OperationsAnalyticsIntegrityStates.Verified,
            "rq487-endpoint-verified-evidence",
            blocksDecisionSignals: false,
            trigger: "rq487_fixture_verified",
            summary: "Fixture integrity evidence is verified.");
        await using var factory = new SupplierEndpointFactory(connectionString!, cache, registry);
        using var client = factory.CreateClient();

        var missBody = await GetSuccessfulBodyAsync(client);
        using var missDocument = JsonDocument.Parse(missBody);
        var miss = missDocument.RootElement;
        AssertFullSupplierContract(miss);
        Assert.Equal(540m, miss.GetProperty("totals").GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal(5, miss.GetProperty("totals").GetProperty("ukupnaKolicina").GetInt32());
        Assert.Equal(100m, miss.GetProperty("totals").GetProperty("previousPeriodRevenue").GetDecimal());
        Assert.Equal(1, miss.GetProperty("totals").GetProperty("previousPeriodUnits").GetInt32());
        Assert.Equal(340m, miss.GetProperty("totals").GetProperty("snapshotCostRevenue").GetDecimal());
        Assert.False(miss.GetProperty("recommendationAllowed").GetBoolean());

        var repeatedBody = await GetSuccessfulBodyAsync(client);
        Assert.Equal(missBody, repeatedBody);
        var firstKeyMetrics = Assert.Single(cache.SupplierPayloadMetrics.Values);
        Assert.Equal(2, firstKeyMetrics.GetCalls);
        Assert.Equal(1, firstKeyMetrics.Hits);
        Assert.Equal(1, firstKeyMetrics.Misses);

        const string changedEvidenceId = "rq487-endpoint-drift-evidence";
        SetBoundSupplierEvidence(
            registry,
            OperationsAnalyticsIntegrityStates.DriftDetected,
            changedEvidenceId,
            blocksDecisionSignals: true,
            trigger: "rq487_fixture_drift",
            summary: "Fixture integrity gate changed during the test.");

        var changedGateBody = await GetSuccessfulBodyAsync(client);
        using var changedGateDocument = JsonDocument.Parse(changedGateBody);
        var changedGate = changedGateDocument.RootElement;
        Assert.NotEqual(missBody, changedGateBody);
        Assert.Equal(changedEvidenceId, GetPropertyIgnoreCase(changedGate.GetProperty("meta"), "operationsIntegrityEvidenceId").GetString());
        Assert.Equal(OperationsAnalyticsIntegrityStates.DriftDetected, GetPropertyIgnoreCase(changedGate.GetProperty("meta"), "operationsIntegrityStatus").GetString());
        Assert.False(changedGate.GetProperty("recommendationAllowed").GetBoolean());
        Assert.Equal(miss.GetProperty("totals").GetRawText(), changedGate.GetProperty("totals").GetRawText());
        Assert.Equal(miss.GetProperty("dataQuality").GetRawText(), changedGate.GetProperty("dataQuality").GetRawText());
        AssertSupplierAnalyticsValuesEqual(miss.GetProperty("suppliers"), changedGate.GetProperty("suppliers"));
        Assert.All(
            changedGate.GetProperty("suppliers").EnumerateArray(),
            supplier => Assert.False(supplier.GetProperty("recommendation").GetProperty("recommendationAllowed").GetBoolean()));

        var metricsByKey = cache.SupplierPayloadMetrics.Values
            .Select(metrics => (metrics.GetCalls, metrics.Hits, metrics.Misses))
            .OrderByDescending(metrics => metrics.GetCalls)
            .ToArray();
        Assert.Equal(2, metricsByKey.Length);
        Assert.Equal((2, 1, 1), metricsByKey[0]);
        Assert.Equal((1, 0, 1), metricsByKey[1]);

        var changedGateHitBody = await GetSuccessfulBodyAsync(client);
        Assert.Equal(changedGateBody, changedGateHitBody);
        Assert.All(
            cache.SupplierPayloadMetrics.Values,
            metrics => Assert.Equal((2, 1, 1), (metrics.GetCalls, metrics.Hits, metrics.Misses)));
    }

    [Fact]
    public async Task NivelacijaEndpoints_ExposeTheSameFlatRateEffectAndExplicitBasisAcrossOverviewDimensionsAndPrePost()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"rq550_parity_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await CreateFixtureAsync(connectionString!);
        await SeedRq550FlatRateArticleAsync(connectionString!);

        var cache = new RecordingAnalyticsCacheService();
        var registry = new OperationsAnalyticsIntegrityRegistry();
        registry.Set(new OperationsAnalyticsIntegritySnapshot(
            OperationsAnalyticsIntegrityStates.Verified,
            "rq550-flat-rate-evidence",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "rq550_fixture_verified",
            "Fixture integrity evidence is verified.",
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false));
        await using var factory = new SupplierEndpointFactory(connectionString!, cache, registry);
        using var client = factory.CreateClient();

        var supplier = await GetSuccessfulJsonAsync(client,
            "/api/analytics/supplier-sales-stats?fromDate=2026-07-01&toDate=2026-08-01&dataScope=all");
        var shoeType = await GetSuccessfulJsonAsync(client,
            "/api/analytics/shoe-type-sales-stats?fromDate=2026-07-01&toDate=2026-08-01&dataScope=all");
        var color = await GetSuccessfulJsonAsync(client,
            "/api/analytics/color-sales-stats?fromDate=2026-07-01&toDate=2026-08-01&dataScope=all");
        var prePost = await GetSuccessfulJsonAsync(client,
            "/api/analytics/vendor-sales-nivelacija?vendorId=8&eventDate=2026-07-16&includeInactive=true&dataScope=existing");
        var supplierRow = supplier.GetProperty("suppliers").EnumerateArray()
            .Single(row => row.GetProperty("dobavljacNaziv").GetString() == "RQ550 supplier");
        var shoeRow = shoeType.GetProperty("shoeTypes").EnumerateArray()
            .Single(row => row.GetProperty("tipObuceNaziv").GetString() == "Cizme");
        var colorRow = color.GetProperty("colors").EnumerateArray()
            .Single(row => row.GetProperty("boja").GetString() == "RQ550 Violet");
        Assert.Equal(0d, supplierRow.GetProperty("prePostNivelacijaRevenueImpactPct").GetDouble());
        Assert.Equal(0d, supplierRow.GetProperty("prePostNivelacijaUnitsImpactPct").GetDouble());
        Assert.Equal(0d, shoeRow.GetProperty("prePostNivelacijaRevenueImpactPct").GetDouble());
        Assert.Equal(0d, shoeRow.GetProperty("prePostNivelacijaUnitsImpactPct").GetDouble());
        Assert.Equal(0d, colorRow.GetProperty("prePostNivelacijaRevenueImpactPct").GetDouble());
        Assert.Equal(0d, colorRow.GetProperty("prePostNivelacijaUnitsImpactPct").GetDouble());
        Assert.Equal(JsonValueKind.Null, supplierRow.GetProperty("promenaPrometa").ValueKind);
        Assert.Equal(JsonValueKind.Null, shoeRow.GetProperty("promenaPrometa").ValueKind);
        Assert.Equal(JsonValueKind.Null, colorRow.GetProperty("promenaPrometa").ValueKind);
        Assert.Equal(JsonValueKind.Null, supplier.GetProperty("totals").GetProperty("promenaPrometaPct").ValueKind);

        AssertBasis(supplier, "overview", "all_sales_in_period", "latest_nivelacija_before_period_end_per_article");
        AssertBasis(shoeType, "shoe_type", "articles_with_nivelacija_event_and_sales_in_period", "latest_nivelacija_before_period_end_per_article");
        AssertBasis(color, "color", "articles_with_nivelacija_event_and_sales_in_period", "latest_nivelacija_before_period_end_per_article");
        AssertBasis(prePost, "assortment", "latest_price_event_per_article_including_increases", "latest_price_event_per_article",
            "fixed_30d_pre_post_revenue_and_units_pct");
        var prePostArticles = prePost.GetProperty("articleStats").EnumerateArray().ToArray();
        Assert.True(prePostArticles.Any(row => row.GetProperty("sku").GetString() == "RQ550-FLAT"), prePost.GetRawText());
        var prePostArticle = prePostArticles.Single(row => row.GetProperty("sku").GetString() == "RQ550-FLAT");
        Assert.Equal(150, prePostArticle.GetProperty("preQty").GetInt32());
        Assert.Equal(150, prePostArticle.GetProperty("postQty").GetInt32());
        Assert.Equal(0m, prePostArticle.GetProperty("changePercent").GetDecimal());
    }

    [Fact]
    public async Task NivelacijaEndpoints_KeepTheSameNonZeroEffectForOneArticleAndEvent()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"rq541_parity_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await CreateFixtureAsync(connectionString!);
        await SeedRq550FlatRateArticleAsync(connectionString!, beforeEventUnits: 5, afterEventUnits: 10);

        var cache = new RecordingAnalyticsCacheService();
        var registry = new OperationsAnalyticsIntegrityRegistry();
        registry.Set(new OperationsAnalyticsIntegritySnapshot(
            OperationsAnalyticsIntegrityStates.Verified,
            "rq541-nonzero-parity-evidence",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "rq541_fixture_verified",
            "Fixture integrity evidence is verified.",
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false));
        await using var factory = new SupplierEndpointFactory(connectionString!, cache, registry);
        using var client = factory.CreateClient();

        var supplier = await GetSuccessfulJsonAsync(client,
            "/api/analytics/supplier-sales-stats?fromDate=2026-07-01&toDate=2026-08-01&dataScope=all");
        var shoeType = await GetSuccessfulJsonAsync(client,
            "/api/analytics/shoe-type-sales-stats?fromDate=2026-07-01&toDate=2026-08-01&dataScope=all");
        var color = await GetSuccessfulJsonAsync(client,
            "/api/analytics/color-sales-stats?fromDate=2026-07-01&toDate=2026-08-01&dataScope=all");
        var prePost = await GetSuccessfulJsonAsync(client,
            "/api/analytics/vendor-sales-nivelacija?vendorId=8&eventDate=2026-07-16&includeInactive=true&dataScope=existing");

        var supplierRow = supplier.GetProperty("suppliers").EnumerateArray()
            .Single(row => row.GetProperty("dobavljacNaziv").GetString() == "RQ550 supplier");
        var shoeRow = shoeType.GetProperty("shoeTypes").EnumerateArray()
            .Single(row => row.GetProperty("tipObuceNaziv").GetString() == "Cizme");
        var colorRow = color.GetProperty("colors").EnumerateArray()
            .Single(row => row.GetProperty("boja").GetString() == "RQ550 Violet");
        var prePostArticle = prePost.GetProperty("articleStats").EnumerateArray()
            .Single(row => row.GetProperty("sku").GetString() == "RQ550-FLAT");

        Assert.Equal(100d, supplierRow.GetProperty("prePostNivelacijaRevenueImpactPct").GetDouble());
        Assert.Equal(100d, shoeRow.GetProperty("prePostNivelacijaRevenueImpactPct").GetDouble());
        Assert.Equal(100d, colorRow.GetProperty("prePostNivelacijaRevenueImpactPct").GetDouble());
        Assert.Equal(100m, prePostArticle.GetProperty("changePercent").GetDecimal());
        AssertBasis(supplier, "overview", "all_sales_in_period", "latest_nivelacija_before_period_end_per_article");
        AssertBasis(shoeType, "shoe_type", "articles_with_nivelacija_event_and_sales_in_period", "latest_nivelacija_before_period_end_per_article");
        AssertBasis(color, "color", "articles_with_nivelacija_event_and_sales_in_period", "latest_nivelacija_before_period_end_per_article");
    }

    private static async Task SeedRq550FlatRateArticleAsync(string connectionString, int beforeEventUnits = 10, int afterEventUnits = 10)
    {
        var setupSql = $"""
            INSERT INTO "Dobavljaci" ("Id", "Naziv", "DataOrigin") VALUES (8, 'RQ550 supplier', 'existing');
            INSERT INTO "Artikli"
                ("Id", "PLU", "Naziv", "NabavnaCena", "NabavnaCenaDin", "PrvaProdajnaCena", "ProdajnaCena",
                 "IDDobavljac", "IDTipObuce", "UpdatedAt", "Kolicina", "MinimalnaKolicina", "IDObjekat",
                 "IDSezona", "Kategorija", "Pol", "Velicina", "Boja", "DataOrigin")
            VALUES (19, 'RQ550-FLAT', 'RQ550 equal daily article', 5, 5, 10, 10, 8, 5, '2026-07-01T00:00:00Z',
                    0, 0, 1, 1, 'Obuca', 'Unisex', '42', 'RQ550 Violet', 'existing');

            INSERT INTO prodaja_zaglavlje
                (id, broj_racuna, datum_prodaje, id_objekat, korisnik_ime, data_origin)
            SELECT 1000 + n, 'RQ550-DAY-' || n, '2026-07-01T00:00:00Z'::timestamptz + (n - 1) * interval '1 day', 1, 'rq550', 'existing'
            FROM generate_series(1, 30) AS n;

            INSERT INTO prodaja_stavke
                (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
            SELECT 1000 + n, 1000 + n, 19, CASE WHEN n < 16 THEN {beforeEventUnits} ELSE {afterEventUnits} END, 10, 5, 8, 5, 'sale_snapshot'
            FROM generate_series(1, 30) AS n;

            INSERT INTO "DnevnikPromena"
                ("Id", "TipPromene", "Datum", "Iznos", "DobavljacId", "ArtikalId", "StaraProdajnaCena", "NovaProdajnaCena", "Kolicina", "IDObjekat", "DataOrigin")
            VALUES (1000, 'Nivelacija', '2026-07-16T00:00:00Z', 10, 8, 19, 11, 10, 1, 1, 'existing');
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(setupSql, connection);
        await command.ExecuteNonQueryAsync();

        var viewSqlPath = FindRepositoryFile("Database", "Analytics", "014_CreateVendorSalesNivelacijaViews.sql");
        var viewSql = await File.ReadAllTextAsync(viewSqlPath);
        await using var views = new NpgsqlCommand(viewSql, connection) { CommandTimeout = 120 };
        await views.ExecuteNonQueryAsync();
    }

    private static async Task<JsonElement> GetSuccessfulJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected HTTP 200 for {path}, got {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static void AssertBasis(
        JsonElement response,
        string tab,
        string cohort,
        string eventSelection,
        string effectMetric = "equal_duration_observed_pre_post_revenue_and_units_change_pct")
    {
        var basis = GetPropertyIgnoreCase(response.GetProperty("meta"), "basis");
        Assert.Equal(tab, GetPropertyIgnoreCase(basis, "tab").GetString());
        Assert.Equal(cohort, GetPropertyIgnoreCase(basis, "cohort").GetString());
        Assert.Equal(effectMetric, GetPropertyIgnoreCase(basis, "effectMetric").GetString());
        Assert.Equal(eventSelection, GetPropertyIgnoreCase(basis, "eventSelection").GetString());
    }

    private static void SetBoundSupplierEvidence(
        OperationsAnalyticsIntegrityRegistry registry,
        string status,
        string evidenceId,
        bool blocksDecisionSignals,
        string trigger,
        string summary)
    {
        const string family = OperationsAnalyticsIntegrityFamilies.SupplierShoeType;
        var fromUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc);
        var generation = registry.GetGeneration(family);
        registry.Set(new OperationsAnalyticsIntegritySnapshot(
            status,
            evidenceId,
            DateTime.UtcNow,
            status == OperationsAnalyticsIntegrityStates.Verified ? DateTime.UtcNow : null,
            trigger,
            summary,
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            blocksDecisionSignals)
        {
            Family = family,
            SourceGeneration = generation,
            ContextFingerprint = OperationsAnalyticsIntegrityContextPolicy.CreateFingerprint(
                family,
                generation,
                fromUtc,
                toUtc,
                "all",
                null)
        });
    }

    private static async Task CreateFixtureAsync(string connectionString)
    {
        await using (var db = new TrendplusDbContext(
                         new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var seedPath = FindRepositoryFile("Api.Tests", "Fixtures", "operations-analytics-all-routes-seed.sql");
        var seedSql = await File.ReadAllTextAsync(seedPath);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var seed = new NpgsqlCommand(seedSql, connection) { CommandTimeout = 120 })
            await seed.ExecuteNonQueryAsync();

        const string supplementalSql = """
            INSERT INTO prodaja_zaglavlje
                (id, broj_racuna, datum_prodaje, id_objekat, korisnik_ime, data_origin)
            VALUES (24, 'RQ487-PREVIOUS', '2026-06-30T12:00:00Z', 1, 'rq487', 'existing');

            INSERT INTO prodaja_stavke
                (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
            VALUES (24, 24, 2, 1, 100, 50, 2, 2, 'sale_snapshot');

            INSERT INTO "DnevnikPromena"
                ("Id", "TipPromene", "Datum", "Iznos", "ArtikalId", "IDObjekat", "DataOrigin")
            VALUES
                (2, 'Nivelacija', '2026-06-15T00:00:00Z', 0, 1, 1, 'existing'),
                (3, 'NivelacijaCena', '2026-07-03T00:00:00Z', 0, 2, 1, 'existing'),
                (4, 'Nivelacija', '2025-01-01T00:00:00Z', 0, 18, 1, 'existing');

            INSERT INTO analytics_cost_snapshot_batches
                (id, scope, status, created_at_utc, generated_at_utc, activated_at_utc, created_by, row_count)
            VALUES (1, 'access_origin', 'active', '2026-10-01T00:00:00Z', '2026-10-01T00:00:00Z', '2026-10-01T00:00:00Z', 'RQ487 fixture', 102);

            INSERT INTO analytics_sale_line_cost_snapshots
                (id, batch_id, prodaja_stavka_id, resolved_unit_cost, cost_source, artikal_id)
            VALUES
                (1, 1, 3, 65, 1, 2),
                (2, 1, 4, 40, 1, 3);

            INSERT INTO analytics_sale_line_cost_snapshots
                (id, batch_id, prodaja_stavka_id, resolved_unit_cost, cost_source, artikal_id)
            SELECT 2 + n, 1, 1000 + n, 20, 1, 18
            FROM generate_series(1, 100) AS n;
            """;
        await using var supplemental = new NpgsqlCommand(supplementalSql, connection);
        await supplemental.ExecuteNonQueryAsync();
    }

    private static async Task<string> GetSuccessfulBodyAsync(HttpClient client)
    {
        using var response = await client.GetAsync(RequestPath);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body;
    }

    private static void AssertFullSupplierContract(JsonElement root)
    {
        Assert.Equal(3, root.GetProperty("suppliers").GetArrayLength());
        Assert.Equal("all", root.GetProperty("dataScope").GetString());
        Assert.True(GetPropertyIgnoreCase(root.GetProperty("meta"), "success").GetBoolean());
        Assert.Equal("verified", GetPropertyIgnoreCase(root.GetProperty("meta"), "operationsIntegrityStatus").GetString());
        Assert.NotEqual(JsonValueKind.Undefined, GetPropertyIgnoreCase(root.GetProperty("meta"), "context").ValueKind);
        Assert.Equal(0d, root.GetProperty("dataQuality").GetProperty("missingCostRevenueSharePct").GetDouble());
        Assert.Equal(
            "historical_sale_line_then_snapshot_then_product_fallback_then_unavailable",
            root.GetProperty("dataQuality").GetProperty("costSourceBasis").GetString());

        var suppliers = root.GetProperty("suppliers").EnumerateArray().ToArray();
        var known = suppliers.Single(supplier => supplier.GetProperty("dobavljacNaziv").GetString() == "Dobavljac C");
        var unknown = suppliers.Single(supplier => supplier.GetProperty("isUnknown").GetBoolean());
        Assert.Equal(260m, known.GetProperty("ukupanPromet").GetDecimal());
        Assert.Equal(260m, known.GetProperty("snapshotCostRevenue").GetDecimal());
        Assert.Equal(1, known.GetProperty("previousPeriodUnits").GetInt32());
        Assert.Equal(80m, unknown.GetProperty("ukupanPromet").GetDecimal());
        Assert.True(unknown.GetProperty("recommendation").TryGetProperty("status", out _));
        Assert.True(known.GetProperty("recommendation").TryGetProperty("reasonCodes", out _));
        Assert.All(suppliers, supplier =>
        {
            Assert.True(supplier.TryGetProperty("marginQualityTier", out _));
            Assert.True(supplier.TryGetProperty("historicalCostCoveragePct", out _));
            Assert.True(supplier.TryGetProperty("snapshotCostCoveragePct", out _));
            Assert.True(supplier.TryGetProperty("prePostNivelacijaRevenueImpactPct", out _));
            Assert.True(supplier.GetProperty("recommendation").TryGetProperty("recommendationAllowed", out _));
        });
    }

    private static void AssertSupplierAnalyticsValuesEqual(JsonElement baseline, JsonElement current)
    {
        var baselineRows = baseline.EnumerateArray().ToDictionary(row => row.GetProperty("dobavljacNaziv").GetString()!, StringComparer.Ordinal);
        var currentRows = current.EnumerateArray().ToDictionary(row => row.GetProperty("dobavljacNaziv").GetString()!, StringComparer.Ordinal);
        Assert.Equal(baselineRows.Keys.OrderBy(key => key), currentRows.Keys.OrderBy(key => key));

        foreach (var (key, baselineRow) in baselineRows)
        {
            var currentRow = currentRows[key];
            foreach (var property in new[]
            {
                "ukupanPromet", "ukupnaKolicina", "revenueWithCost", "marginContribution", "totalCost",
                "historicalCostRevenue", "snapshotCostRevenue", "noCostRevenue", "marginPct",
                "previousPeriodRevenue", "previousPeriodUnits", "preNivelacijePromet", "posleNivelacijePromet",
                "comparablePreNivelacijePromet", "comparablePostNivelacijePromet", "sharePct", "marginQualityTier"
            })
            {
                Assert.Equal(baselineRow.GetProperty(property).GetRawText(), currentRow.GetProperty(property).GetRawText());
            }

            Assert.Equal(
                baselineRow.GetProperty("recommendation").GetProperty("status").GetString(),
                currentRow.GetProperty("recommendation").GetProperty("status").GetString());
            Assert.Equal(
                baselineRow.GetProperty("recommendation").GetProperty("reasonCodes").GetRawText(),
                currentRow.GetProperty("recommendation").GetProperty("reasonCodes").GetRawText());
        }
    }

    private static JsonElement GetPropertyIgnoreCase(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        throw new KeyNotFoundException($"Property '{name}' was not found.");
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

    private sealed class SupplierEndpointFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _connectionString;
        private readonly RecordingAnalyticsCacheService _cache;
        private readonly OperationsAnalyticsIntegrityRegistry _registry;

        public SupplierEndpointFactory(
            string connectionString,
            RecordingAnalyticsCacheService cache,
            OperationsAnalyticsIntegrityRegistry registry)
        {
            _connectionString = connectionString;
            _cache = cache;
            _registry = registry;
        }

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
                    ["Analytics:UseSnapshotCost"] = "true",
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["ConnectionStrings:AnalyticsConnection"] = _connectionString,
                    ["ConnectionStrings:OpenProductTrainingConnection"] = _connectionString
                }));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<ITrendplusDbContext>();
                services.AddDbContextFactory<TrendplusDbContext>(options => options.UseNpgsql(_connectionString));
                services.AddDbContext<TrendplusDbContext>(options => options.UseNpgsql(_connectionString));
                services.AddScoped<ITrendplusDbContext>(provider => provider.GetRequiredService<TrendplusDbContext>());

                services.RemoveAll<IAnalyticsCacheService>();
                services.AddSingleton<IAnalyticsCacheService>(_cache);
                services.RemoveAll<OperationsAnalyticsIntegrityRegistry>();
                services.AddSingleton(_registry);
                services.Configure<AnalyticsSnapshotOptions>(options => options.UseSnapshotCost = true);
                services.Configure<PerformanceLoggingOptions>(options => options.CaptureHttpRequests = false);
            });
        }
    }

    private sealed class RecordingAnalyticsCacheService : IAnalyticsCacheService
    {
        private readonly ConcurrentDictionary<string, object> _entries = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, SupplierPayloadMetric> _supplierPayloadMetrics = new(StringComparer.Ordinal);

        public bool IsRedisAvailable => false;
        public bool IsRedisEnabled => false;
        public IReadOnlyDictionary<string, SupplierPayloadMetric> SupplierPayloadMetrics => _supplierPayloadMetrics;

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
        {
            var isSupplierPayload = key.Contains("supplier-sales-stats:v8:", StringComparison.Ordinal)
                && !key.EndsWith(":metadata", StringComparison.Ordinal);
            var exists = _entries.TryGetValue(key, out var value) && value is T;
            if (isSupplierPayload)
            {
                _supplierPayloadMetrics.AddOrUpdate(
                    key,
                    _ => new SupplierPayloadMetric(1, exists ? 1 : 0, exists ? 0 : 1),
                    (_, metric) => metric with
                    {
                        GetCalls = metric.GetCalls + 1,
                        Hits = metric.Hits + (exists ? 1 : 0),
                        Misses = metric.Misses + (exists ? 0 : 1)
                    });
            }

            return Task.FromResult(exists ? (T?)value : null);
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

            var value = await factory();
            await SetAsync(key, value, expiration, ct);
            return value;
        }

        public void SetRedisEnabled(bool enabled) { }
        public CacheFootprintSnapshot GetFootprintSnapshot() => new("test", false, false, _entries.Count);
    }

    private sealed record SupplierPayloadMetric(int GetCalls, int Hits, int Misses);
}
