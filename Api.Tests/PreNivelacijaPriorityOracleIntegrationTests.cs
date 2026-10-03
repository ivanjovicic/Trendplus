using System.Net;
using System.Text.Json;
using Application.Artikli.Common.Interfaces;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests;

public sealed class PreNivelacijaPriorityOracleIntegrationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public PreNivelacijaPriorityOracleIntegrationTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PreNivelacijaEndpoint_MatchesIndependentPostgresOracle_ForAllStoreAndImportedScopes()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await CreateSeededDatabaseAsync();
        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<HybridCacheService>.Instance);
        await using var factory = new PreNivelacijaEndpointFactory(connectionString, cache);
        using var client = factory.CreateClient();

        using var all = await GetJsonAsync(client, "all");
        var allOracle = await ReadIndependentOracleAsync(connectionString, "all", storeId: null);
        AssertGoldenFacts("all", allOracle);
        AssertEndpointMatchesOracle(all.RootElement, allOracle);
        AssertScoringAndScenarioOracle(all.RootElement, allOracle);
        Assert.Equal(8, all.RootElement.GetProperty("summary").GetProperty("candidatesCount").GetInt32());
        Assert.Equal(8, all.RootElement.GetProperty("totalCandidates").GetInt32());
        Assert.Equal(2, all.RootElement.GetProperty("evidenceWindow").GetProperty("candidatesWithReturns").GetInt32());
        Assert.Equal("certified_retail_excludes_trimmed_case_insensitive_dug_korekcija",
            all.RootElement.GetProperty("evidenceWindow").GetProperty("receiptPopulationPolicy").GetString());
        Assert.Equal(1, await CountChainWideMarkdownEventsAsync(connectionString));
        AssertKnownNvF3BaselineIsVisible(all.RootElement);
        AssertSummaryAndQueuesAreBoundToTheCandidatePopulation(all.RootElement);

        using var oneStore = await GetJsonAsync(client, "all", storeId: 1);
        var storeOracle = await ReadIndependentOracleAsync(connectionString, "all", storeId: 1);
        AssertGoldenFacts("store1", storeOracle);
        AssertEndpointMatchesOracle(oneStore.RootElement, storeOracle);
        Assert.Equal(4, oneStore.RootElement.GetProperty("totalCandidates").GetInt32());
        Assert.All(oneStore.RootElement.GetProperty("candidates").EnumerateArray(),
            item => Assert.Equal(1, item.GetProperty("storeId").GetInt32()));

        using var imported = await GetJsonAsync(client, "imported");
        var importedOracle = await ReadIndependentOracleAsync(connectionString, "imported", storeId: null);
        AssertGoldenFacts("imported", importedOracle);
        AssertEndpointMatchesOracle(imported.RootElement, importedOracle);
        Assert.Equal(4, imported.RootElement.GetProperty("totalCandidates").GetInt32());
        Assert.All(imported.RootElement.GetProperty("candidates").EnumerateArray(),
            item => Assert.Equal(1, item.GetProperty("storeId").GetInt32()));
    }

    [Fact]
    public async Task PreNivelacijaEndpoint_MarkdownQueryFailureIsNotCached_AndNextRequestRecovers()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await CreateSeededDatabaseAsync();
        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<HybridCacheService>.Instance);
        await using var factory = new PreNivelacijaEndpointFactory(connectionString, cache);
        using var client = factory.CreateClient();

        await ExecuteAsync(connectionString, "ALTER TABLE \"DnevnikPromena\" RENAME TO rq548_markdown_events_unavailable;");
        using (var failed = await GetJsonAsync(client, "all"))
        {
            var root = failed.RootElement;
            Assert.False(root.GetProperty("meta").GetProperty("success").GetBoolean());
            Assert.Equal("pre_nivelacija_markdown_unavailable", root.GetProperty("meta").GetProperty("errorCode").GetString());
            Assert.Equal(0, root.GetProperty("totalCandidates").GetInt32());
        }

        Assert.Equal(0, cache.GetFootprintSnapshot().TrackedKeyCount);
        await ExecuteAsync(connectionString, "ALTER TABLE rq548_markdown_events_unavailable RENAME TO \"DnevnikPromena\";");

        using var recovered = await GetJsonAsync(client, "all");
        Assert.True(recovered.RootElement.GetProperty("meta").GetProperty("success").GetBoolean());
        Assert.Equal(8, recovered.RootElement.GetProperty("totalCandidates").GetInt32());
        Assert.True(cache.GetFootprintSnapshot().TrackedKeyCount > 0);
    }

    [Fact]
    public async Task PreNivelacijaEndpoint_UnknownReceiptAndSalesHistoryIsExplicitlyBlocked()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await CreateSeededDatabaseAsync();
        await ExecuteAsync(connectionString, """
            INSERT INTO "Artikli"
                ("Id", "PLU", "Naziv", "NabavnaCena", "NabavnaCenaDin", "PrvaProdajnaCena", "ProdajnaCena",
                 "IDDobavljac", "IDTipObuce", "UpdatedAt", "Kolicina", "IDObjekat", "Kategorija", "DataOrigin")
            VALUES
                (107, 'RQ539-S1-UNKNOWN', 'RQ539 unknown receipt and sales', 50, 50, 100, 100,
                 1, 1, CURRENT_TIMESTAMP - INTERVAL '200 days', 9, 1, 'Obuca', 'access');
            SELECT setval(pg_get_serial_sequence('"Artikli"', 'Id'), 207, true);
            """);

        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<HybridCacheService>.Instance);
        await using var factory = new PreNivelacijaEndpointFactory(connectionString, cache);
        using var client = factory.CreateClient();

        using var response = await GetJsonAsync(client, "all", storeId: 1);
        var candidate = response.RootElement.GetProperty("candidates").EnumerateArray()
            .Single(item => item.GetProperty("sku").GetString() == "RQ539-S1-UNKNOWN");
        Assert.Equal(JsonValueKind.Null, candidate.GetProperty("firstReceiptDateUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, candidate.GetProperty("daysSinceLastSale").ValueKind);
        Assert.Equal("unknown", candidate.GetProperty("receiptEvidenceStatus").GetString());
        Assert.Equal("never_sold", candidate.GetProperty("salesHistoryStatus").GetString());
        Assert.False(candidate.GetProperty("recommendationAllowed").GetBoolean());
        Assert.Contains("receipt_date_unknown", candidate.GetProperty("recommendation").GetProperty("reasonCodes")
            .EnumerateArray().Select(code => code.GetString()));
    }

    private async Task<string> CreateSeededDatabaseAsync()
    {
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync(
            $"rq548_pre_nivelacija_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using (var db = new TrendplusDbContext(
                         new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var seedPath = FindRepoFile("Api.Tests", "Fixtures", "pre-nivelacija-priority-oracle-seed.sql");
        var seedSql = await File.ReadAllTextAsync(seedPath);
        await ExecuteAsync(connectionString!, seedSql);
        return connectionString!;
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string dataScope, int? storeId = null)
    {
        var store = storeId.HasValue ? $"&storeId={storeId.Value}" : string.Empty;
        using var response = await client.GetAsync(
            $"/api/analytics/pre-nivelacija-prioriteti?page=1&pageSize=100&focus=all&dataScope={dataScope}{store}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(body);
    }

    private static async Task<IReadOnlyList<OracleRow>> ReadIndependentOracleAsync(
        string connectionString,
        string dataScope,
        int? storeId)
    {
        const string sql = """
            WITH sales AS (
                SELECT ps.id_artikal,
                       p.id_objekat AS store_id,
                       SUM(ps.kolicina)::integer AS units_180,
                       SUM(ps.kolicina) FILTER (WHERE ps.kolicina > 0)::integer AS positive_units_180,
                       SUM(ps.kolicina) FILTER (WHERE ps.kolicina < 0)::integer AS negative_units_180,
                       COUNT(*)::integer AS sales_row_count,
                       MAX(p.datum_prodaje) FILTER (WHERE ps.kolicina > 0) AS latest_positive_sale
                FROM prodaja_stavke ps
                JOIN prodaja_zaglavlje p ON p.id = ps.id_prodaja
                WHERE p.datum_prodaje >= CURRENT_TIMESTAMP - INTERVAL '180 days'
                  AND p.datum_prodaje <= CURRENT_TIMESTAMP
                  AND UPPER(BTRIM(COALESCE(p.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
                  AND (@scope = 'all'
                       OR (@scope = 'imported' AND p.data_origin = 'access')
                       OR (@scope = 'existing' AND COALESCE(p.data_origin, '') IN ('', 'existing')))
                GROUP BY ps.id_artikal, p.id_objekat
            ), markdown AS (
                SELECT dp."ArtikalId" AS article_id,
                       dp."IDObjekat" AS store_id,
                       COUNT(*)::integer AS markdown_events,
                       COALESCE(ROUND(AVG(
                           ((dp."StaraProdajnaCena" - dp."NovaProdajnaCena") / dp."StaraProdajnaCena") * 100
                       ) FILTER (
                           WHERE dp."StaraProdajnaCena" > 0
                             AND dp."NovaProdajnaCena" < dp."StaraProdajnaCena"
                       ), 2), 0)::numeric AS average_markdown_pct
                FROM "DnevnikPromena" dp
                WHERE dp."ArtikalId" IS NOT NULL
                  AND dp."Datum" >= CURRENT_TIMESTAMP - INTERVAL '180 days'
                  AND dp."Datum" <= CURRENT_TIMESTAMP
                  AND dp."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
                  AND (@scope = 'all'
                       OR (@scope = 'imported' AND dp."DataOrigin" = 'access')
                       OR (@scope = 'existing' AND COALESCE(dp."DataOrigin", '') IN ('', 'existing')))
                GROUP BY dp."ArtikalId", dp."IDObjekat"
            )
            SELECT a."Id", a."PLU", a."IDObjekat", a."Kolicina",
                   COALESCE(s.units_180, 0), COALESCE(s.positive_units_180, 0),
                   COALESCE(s.negative_units_180, 0), COALESCE(s.sales_row_count, 0),
                   CASE WHEN s.latest_positive_sale IS NULL THEN 999
                        ELSE (CURRENT_DATE - s.latest_positive_sale::date)::integer END,
                   COALESCE(m.markdown_events, 0), COALESCE(m.average_markdown_pct, 0),
                   COALESCE(a."ProdajnaCena", a."PrvaProdajnaCena"),
                   COALESCE(a."NabavnaCenaDin", a."NabavnaCena"), a."DataOrigin"
            FROM "Artikli" a
            LEFT JOIN sales s ON s.id_artikal = a."Id" AND s.store_id IS NOT DISTINCT FROM a."IDObjekat"
            LEFT JOIN markdown m ON m.article_id = a."Id" AND m.store_id IS NOT DISTINCT FROM a."IDObjekat"
            WHERE COALESCE(a."Kolicina", 0) > 0
              AND (@scope = 'all'
                   OR (@scope = 'imported' AND a."DataOrigin" = 'access')
                   OR (@scope = 'existing' AND COALESCE(a."DataOrigin", '') IN ('', 'existing')))
              AND (@store_id IS NULL OR a."IDObjekat" = @store_id)
            ORDER BY a."Id";
            """;

        var rows = new List<OracleRow>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("scope", dataScope);
        command.Parameters.Add(new NpgsqlParameter("store_id", NpgsqlDbType.Integer)
        {
            Value = (object?)storeId ?? DBNull.Value
        });
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new OracleRow(
                reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7),
                reader.GetInt32(8), reader.GetInt32(9), reader.GetDecimal(10), reader.GetDecimal(11),
                reader.GetDecimal(12), reader.GetString(13)));
        }

        return rows;
    }

    private static void AssertGoldenFacts(string scope, IReadOnlyList<OracleRow> actual)
    {
        var path = FindRepoFile("Api.Tests", "Fixtures", "pre-nivelacija-prioriteti-golden.json");
        using var golden = JsonDocument.Parse(File.ReadAllText(path));
        var expected = golden.RootElement.GetProperty(scope).EnumerateArray().ToArray();
        Assert.Equal(expected.Length, actual.Count);

        for (var index = 0; index < expected.Length; index++)
        {
            var row = expected[index];
            var observed = actual[index];
            Assert.Equal(row.GetProperty("articleId").GetInt32(), observed.ArticleId);
            Assert.Equal(row.GetProperty("sku").GetString(), observed.Sku);
            Assert.Equal(row.GetProperty("storeId").GetInt32(), observed.StoreId);
            Assert.Equal(row.GetProperty("stockUnits").GetInt32(), observed.StockUnits);
            Assert.Equal(row.GetProperty("units180").GetInt32(), observed.Units180);
            Assert.Equal(row.GetProperty("positiveUnits180").GetInt32(), observed.PositiveUnits180);
            Assert.Equal(row.GetProperty("negativeUnits180").GetInt32(), observed.NegativeUnits180);
            Assert.Equal(row.GetProperty("salesRows180").GetInt32(), observed.SalesRowCount);
            Assert.Equal(row.GetProperty("daysSinceLastSale").GetInt32(), observed.DaysSinceLastSale);
            Assert.Equal(row.GetProperty("markdownEvents").GetInt32(), observed.MarkdownEvents);
            Assert.Equal(row.GetProperty("averageMarkdownPct").GetDecimal(), observed.AverageMarkdownPct);
            Assert.Equal(row.GetProperty("sellingPrice").GetDecimal(), observed.SellingPrice);
            Assert.Equal(row.GetProperty("purchasePrice").GetDecimal(), observed.PurchasePrice);
            Assert.Equal(row.GetProperty("dataOrigin").GetString(), observed.DataOrigin);
        }
    }

    private static void AssertEndpointMatchesOracle(JsonElement root, IReadOnlyList<OracleRow> oracle)
    {
        var candidates = root.GetProperty("candidates").EnumerateArray().ToDictionary(
            item => item.GetProperty("artikalId").GetInt32());
        var actionableOracle = oracle.Where(row => !row.Sku.EndsWith("-NEW", StringComparison.Ordinal)
            && !row.Sku.EndsWith("-EXCLUDED", StringComparison.Ordinal)).ToArray();
        var newStock = root.GetProperty("queues").GetProperty("newStock").EnumerateArray().ToArray();
        Assert.Equal(oracle.Count - actionableOracle.Length, newStock.Length);
        Assert.Equal(actionableOracle.Length, root.GetProperty("totalCandidates").GetInt32());
        Assert.Equal(actionableOracle.Length, candidates.Count);

        foreach (var expected in actionableOracle)
        {
            var candidate = candidates[expected.ArticleId];
            Assert.Equal(expected.Sku, candidate.GetProperty("sku").GetString());
            Assert.Equal(expected.StoreId, candidate.GetProperty("storeId").GetInt32());
            Assert.Equal(expected.StockUnits, candidate.GetProperty("stockUnits").GetInt32());
            Assert.Equal(expected.Units180, candidate.GetProperty("units180").GetInt32());
            Assert.Equal(expected.PositiveUnits180, candidate.GetProperty("positiveUnits180").GetInt32());
            Assert.Equal(expected.NegativeUnits180, candidate.GetProperty("negativeUnits180").GetInt32());
            if (expected.DaysSinceLastSale == 999)
            {
                Assert.Equal(JsonValueKind.Null, candidate.GetProperty("daysSinceLastSale").ValueKind);
                Assert.Equal("never_sold", candidate.GetProperty("salesHistoryStatus").GetString());
            }
            else
            {
                Assert.Equal(expected.DaysSinceLastSale, candidate.GetProperty("daysSinceLastSale").GetInt32());
            }
            Assert.Equal(expected.MarkdownEvents, candidate.GetProperty("markdownEvents").GetInt32());
            Assert.Equal(expected.AverageMarkdownPct, candidate.GetProperty("avgMarkdownPct").GetDecimal());
            var expectedSalesStatus = expected.SalesRowCount == 0 ? "no_sales_in_window" : "positive_net_sales";
            Assert.Equal(expectedSalesStatus, candidate.GetProperty("salesEvidenceStatus").GetString());
        }
    }

    private static void AssertScoringAndScenarioOracle(JsonElement root, IReadOnlyList<OracleRow> rows)
    {
        var actionableRows = rows.Where(row => !row.Sku.EndsWith("-NEW", StringComparison.Ordinal)
            && !row.Sku.EndsWith("-EXCLUDED", StringComparison.Ordinal)).ToArray();
        var maxStock = actionableRows.Max(row => row.StockUnits);
        var maxVelocity = actionableRows.Max(row => row.Units180 / 180m);
        var candidates = root.GetProperty("candidates").EnumerateArray().ToDictionary(
            item => item.GetProperty("artikalId").GetInt32());

        foreach (var row in actionableRows)
        {
            var item = candidates[row.ArticleId];
            var velocity = Round2Or4(row.Units180 / 180m, 4);
            var grossMargin = row.SellingPrice <= 0m || row.PurchasePrice <= 0m
                ? 0m
                : Round2((row.SellingPrice - row.PurchasePrice) / row.SellingPrice * 100m);
            var breakdown = item.GetProperty("scoreBreakdown");
            var stockPressure = Clamp(row.StockUnits * 100m / maxStock);
            var velocityRisk = 100m - Clamp(velocity * 100m / maxVelocity);
            var recencyRisk = Clamp(Math.Min(row.DaysSinceLastSale, 180) * 100m / 180m);
            var markdownOpportunity = Clamp(100m - (row.MarkdownEvents * 20m + row.AverageMarkdownPct * 0.5m));
            var hasCompleteEvidence = row.SalesRowCount > 0
                && row.Units180 > 0
                && row.SellingPrice > 0m
                && row.PurchasePrice > 0m;
            var marginPotential = hasCompleteEvidence ? Clamp(grossMargin * 100m / 60m) : 0m;
            Assert.Equal(Round2(stockPressure), breakdown.GetProperty("stockPressure").GetDecimal());
            Assert.Equal(Round2(velocityRisk), breakdown.GetProperty("velocityRisk").GetDecimal());
            Assert.Equal(Round2(recencyRisk), breakdown.GetProperty("recencyRisk").GetDecimal());
            Assert.Equal(Round2(markdownOpportunity), breakdown.GetProperty("markdownOpportunity").GetDecimal());
            Assert.Equal(Round2(marginPotential), breakdown.GetProperty("marginPotential").GetDecimal());
            const decimal seasonRecencyBoost = 30m; // fixture articles have no season; endpoint's explicit fallback
            Assert.Equal(seasonRecencyBoost, breakdown.GetProperty("seasonRecencyBoost").GetDecimal());

            var score = Round2(Clamp(
                0.30m * Round2(stockPressure)
                + 0.25m * Round2(velocityRisk)
                + 0.20m * Round2(recencyRisk)
                + 0.10m * Round2(markdownOpportunity)
                + 0.10m * Round2(marginPotential)
                + 0.05m * seasonRecencyBoost));
            Assert.Equal(score, item.GetProperty("preNivelacijaScore").GetDecimal());
            Assert.Equal(grossMargin, item.GetProperty("grossMarginPctEst").GetDecimal());

            var baselineUnits = (row.Units180 + 0.05m) / 181m;
            var highlightMultiplier = 1m + 0.15m + score / 100m * 0.30m;
            var expectedHighlightUnits = ScenarioUnits(baselineUnits, row.StockUnits, highlightMultiplier);
            var discount = Math.Clamp(0.08m + row.MarkdownEvents * 0.02m + row.AverageMarkdownPct / 200m, 0.08m, 0.35m);
            var expectedMarkdownUnits = ScenarioUnits(baselineUnits, row.StockUnits, 1m + discount * 1.8m);
            var highlight = item.GetProperty("scenarioHighlightNow");
            var markdown = item.GetProperty("scenarioMarkdownNow");
            Assert.Equal(expectedHighlightUnits, highlight.GetProperty("expectedUnits30d").GetInt32());
            Assert.Equal(expectedMarkdownUnits, markdown.GetProperty("expectedUnits30d").GetInt32());
            Assert.Equal(Round2(expectedHighlightUnits * row.SellingPrice), highlight.GetProperty("expectedRevenue30d").GetDecimal());
            Assert.Equal(Round2(expectedMarkdownUnits * row.SellingPrice * (1m - discount)), markdown.GetProperty("expectedRevenue30d").GetDecimal());
        }
    }

    private static void AssertKnownNvF3BaselineIsVisible(JsonElement root)
    {
        var candidates = root.GetProperty("candidates").EnumerateArray().ToDictionary(
            item => item.GetProperty("sku").GetString()!);
        Assert.DoesNotContain(candidates.Keys, sku => sku.EndsWith("-NEW", StringComparison.Ordinal)
            || sku.EndsWith("-EXCLUDED", StringComparison.Ordinal));
        var newStock = root.GetProperty("queues").GetProperty("newStock").EnumerateArray()
            .Single(item => item.GetProperty("sku").GetString() == "RQ548-S1-NEW");
        Assert.Equal(3, newStock.GetProperty("daysSinceReceipt").GetInt32());
        Assert.Equal("new_stock", newStock.GetProperty("reasonCode").GetString());
        var neverSold = candidates["RQ548-S1-NEVER"];
        Assert.Equal(JsonValueKind.Null, neverSold.GetProperty("daysSinceLastSale").ValueKind);
        Assert.Equal("never_sold", neverSold.GetProperty("salesHistoryStatus").GetString());
        Assert.Equal("high", neverSold.GetProperty("priorityBand").GetString());

        var belowCost = candidates["RQ548-S1-BELOW"];
        Assert.Equal(-25m, belowCost.GetProperty("grossMarginPctEst").GetDecimal());
        Assert.True(belowCost.GetProperty("belowCost").GetBoolean());
        Assert.True(belowCost.GetProperty("hasCompleteEvidence").GetBoolean());
        Assert.True(belowCost.GetProperty("scenarioHighlightNow").GetProperty("expectedMargin30d").GetDecimal() < 0m);
        Assert.True(belowCost.GetProperty("scenarioMarkdownNow").GetProperty("expectedMargin30d").GetDecimal() < 0m);
        Assert.True(belowCost.GetProperty("marginDeltaHighlightVsMarkdown").GetDecimal() > 0m);
        Assert.True(root.GetProperty("summary").GetProperty("estimatedAvoidableMarkdownLossCoverageEligible").GetInt32() > 0);
        Assert.True(root.GetProperty("summary").GetProperty("estimatedAvoidableMarkdownLoss").GetDecimal() > 0m);
    }

    private static void AssertSummaryAndQueuesAreBoundToTheCandidatePopulation(JsonElement root)
    {
        var items = root.GetProperty("candidates").EnumerateArray().ToArray();
        var summary = root.GetProperty("summary");
        var queues = root.GetProperty("queues");
        var expectedHigh = items.Where(item => item.GetProperty("priorityBand").GetString() == "high").ToArray();
        Assert.Equal(expectedHigh.Length, summary.GetProperty("highPriorityCount").GetInt32());
        Assert.Equal(expectedHigh.Sum(item => item.GetProperty("stockUnits").GetInt32()),
            summary.GetProperty("totalStockAtRisk").GetInt32());
        Assert.Equal(items.Length, summary.GetProperty("totalStockAtRiskCoverageTotal").GetInt32());

        var expectedHighlightQueue = items
            .Where(item => item.GetProperty("priorityBand").GetString() == "high"
                && item.GetProperty("recommendationAllowed").GetBoolean())
            .Select(item => item.GetProperty("artikalId").GetInt32()).Order().ToArray();
        var actualHighlightQueue = queues.GetProperty("highlightNow").EnumerateArray()
            .Select(item => item.GetProperty("artikalId").GetInt32()).Order().ToArray();
        Assert.Equal(expectedHighlightQueue, actualHighlightQueue);
        Assert.Equal(expectedHighlightQueue.Length, queues.GetProperty("highlightNowTotal").GetInt32());
    }

    private static async Task<int> CountChainWideMarkdownEventsAsync(string connectionString)
    {
        const string sql = """
            SELECT COUNT(*)::integer FROM "DnevnikPromena"
            WHERE "TipPromene" IN ('Nivelacija', 'Nivelacija cena')
              AND "IDObjekat" IS NULL
              AND "ArtikalId" = 102;
            """;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync() ?? 0);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    private static int ScenarioUnits(decimal baseline, int stock, decimal multiplier)
    {
        if (stock <= 0 || baseline <= 0m || multiplier <= 0m)
            return 0;
        return (int)Math.Clamp(Math.Round((double)(baseline * 30m * multiplier), MidpointRounding.AwayFromZero), 0, stock);
    }

    private static decimal Clamp(decimal value) => Math.Clamp(value, 0m, 100m);
    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal Round2Or4(decimal value, int digits) => decimal.Round(value, digits, MidpointRounding.AwayFromZero);

    private static string FindRepoFile(params string[] segments)
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

    private sealed record OracleRow(
        int ArticleId,
        string Sku,
        int StoreId,
        int StockUnits,
        int Units180,
        int PositiveUnits180,
        int NegativeUnits180,
        int SalesRowCount,
        int DaysSinceLastSale,
        int MarkdownEvents,
        decimal AverageMarkdownPct,
        decimal SellingPrice,
        decimal PurchasePrice,
        string DataOrigin);

    private sealed class PreNivelacijaEndpointFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _connectionString;
        private readonly IAnalyticsCacheService _cache;

        public PreNivelacijaEndpointFactory(string connectionString, IAnalyticsCacheService cache)
        {
            _connectionString = connectionString;
            _cache = cache;
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
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["ConnectionStrings:AnalyticsConnection"] = _connectionString,
                    ["ConnectionStrings:OpenProductTrainingConnection"] = _connectionString,
                    ["PerformanceLogging:CaptureHttpRequests"] = "false"
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
                services.RemoveAll<DbContextOptions<AnalyticsDbContext>>();
                services.RemoveAll<AnalyticsDbContext>();
                services.RemoveAll<IAnalyticsDbContext>();
                services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(_connectionString));
                services.AddScoped<IAnalyticsDbContext>(provider => provider.GetRequiredService<AnalyticsDbContext>());
                services.RemoveAll<IAnalyticsCacheService>();
                services.AddSingleton<IAnalyticsCacheService>(_cache);
            });
        }
    }
}
