using System.Net;
using System.Globalization;
using System.Text.Json;
using System.Net.Http.Json;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Infrastructure.DbContexts;
using Infrastructure.Services;
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
using Trendplus2.Endpoints;
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
    public async Task PreNivelacija_AnchorsToRetailSalesHorizon_AndKeepsExcludedStockInSeparateQueues()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await CreateSeededDatabaseAsync();
        await ExecuteAsync(connectionString, """
            UPDATE prodaja_zaglavlje SET datum_prodaje = datum_prodaje - INTERVAL '60 days' WHERE id_objekat IN (1, 2);
            UPDATE "DnevnikPromena" SET "Datum" = "Datum" - INTERVAL '60 days';
            INSERT INTO "StoresDim" ("StoreKey", "StoreId", "StoreName", "DataOrigin")
            VALUES (3, 3, 'STARO', 'existing'), (20828, 20828, 'Trend PLUS 2', 'existing');
            INSERT INTO "TipoviObuce" ("Id", "Naziv", "DataOrigin") VALUES (2, 'Oprema', 'existing');
            INSERT INTO "Artikli"
                ("Id", "PLU", "Naziv", "NabavnaCena", "NabavnaCenaDin", "PrvaProdajnaCena", "ProdajnaCena",
                 "IDDobavljac", "IDTipObuce", "UpdatedAt", "Kolicina", "IDObjekat", "Kategorija", "DataOrigin")
            VALUES
                (107, 'RQ571-STARO', 'Legacy store stock', 50, 50, 100, 100, 1, 1, CURRENT_TIMESTAMP, 8, 3, 'Obuca', 'existing'),
                (108, 'RQ571-OPREMA', 'Non footwear stock', 50, 50, 100, 100, 1, 2, CURRENT_TIMESTAMP, 6, 1, 'Oprema', 'existing'),
                (109, 'RQ571-20828', 'Excluded object stock', 50, 50, 100, 100, 1, 1, CURRENT_TIMESTAMP, 4, 20828, 'Obuca', 'existing');
            INSERT INTO prodaja_zaglavlje (id, broj_racuna, datum_prodaje, id_objekat, korisnik_ime, data_origin)
            VALUES (90, 'RQ571-EXCLUDED-LATEST', CURRENT_TIMESTAMP, 3, 'rq571', 'existing');
            INSERT INTO prodaja_stavke
                (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
            VALUES (90, 90, 107, 3, 100, 50, 1, 1, 'sale_snapshot');
            SELECT setval(pg_get_serial_sequence('"Artikli"', 'Id'), 209, true);
            """);

        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<HybridCacheService>.Instance);
        await using var factory = new PreNivelacijaEndpointFactory(connectionString, cache);
        using var client = factory.CreateClient();
        using var response = await GetJsonAsync(client, "all");
        var root = response.RootElement;
        var evidenceWindow = root.GetProperty("evidenceWindow");
        Assert.Equal("source_horizon", evidenceWindow.GetProperty("anchorBasis").GetString());
        Assert.Equal(
            DateTime.Parse(evidenceWindow.GetProperty("observedSourceHorizonUtc").GetString()!, CultureInfo.InvariantCulture).Date,
            DateTime.Parse(evidenceWindow.GetProperty("anchorDateUtc").GetString()!, CultureInfo.InvariantCulture).Date);
        var anchoredCandidate = root.GetProperty("candidates").EnumerateArray()
            .Single(item => item.GetProperty("artikalId").GetInt32() == 101);
        Assert.Equal(4, anchoredCandidate.GetProperty("daysSinceLastSale").GetInt32());
        Assert.DoesNotContain(root.GetProperty("candidates").EnumerateArray(),
            item => new[] { 107, 108, 109 }.Contains(item.GetProperty("artikalId").GetInt32()));

        var queues = root.GetProperty("queues");
        var legacyCleanup = queues.GetProperty("legacyCleanup").EnumerateArray().ToArray();
        Assert.Contains(legacyCleanup, item => item.GetProperty("artikalId").GetInt32() == 107);
        Assert.Contains(legacyCleanup, item => item.GetProperty("artikalId").GetInt32() == 109);
        Assert.All(legacyCleanup, item => Assert.False(item.GetProperty("recommendationAllowed").GetBoolean()));
        var nonFootwearCleanup = queues.GetProperty("nonFootwearCleanup").EnumerateArray().ToArray();
        var equipment = Assert.Single(nonFootwearCleanup);
        Assert.Equal(108, equipment.GetProperty("artikalId").GetInt32());
        Assert.Contains("non_footwear", equipment.GetProperty("reasonCodes").EnumerateArray().Select(item => item.GetString()));
        Assert.False(equipment.GetProperty("recommendationAllowed").GetBoolean());
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
        Assert.Equal("unknown", candidate.GetProperty("stockAgeStatus").GetString());
        Assert.Equal("never_sold", candidate.GetProperty("salesHistoryStatus").GetString());
        Assert.False(candidate.GetProperty("recommendationAllowed").GetBoolean());
        Assert.NotEqual("high", candidate.GetProperty("priorityBand").GetString());
        Assert.Equal(0, candidate.GetProperty("scoreBreakdown").GetProperty("recencyRisk").GetDecimal());
        Assert.Contains("receipt_date_unknown", candidate.GetProperty("recommendation").GetProperty("reasonCodes")
            .EnumerateArray().Select(code => code.GetString()));
    }

    [Fact]
    public async Task NivelacijaWrite_ValidatesPriceRequiresAdminAndRecordsChainWideAuditFields()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await CreateSeededDatabaseAsync();
        await ExecuteAsync(connectionString, """
            INSERT INTO "DnevnikPromena"
                ("TipPromene", "Datum", "Iznos", "ArtikalId", "StaraProdajnaCena", "NovaProdajnaCena", "IDObjekat", "DataOrigin")
            VALUES ('Nivelacija', CURRENT_TIMESTAMP - INTERVAL '100 days', 0, 202, 100, 80, NULL, 'existing');
            """);
        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<HybridCacheService>.Instance);
        await using var factory = new PreNivelacijaEndpointFactory(connectionString, cache);
        using var client = factory.CreateClient();

        var payload = new
        {
            artikalId = 202,
            novaProdajnaCena = 60m,
            komentar = "test override",
            storeId = (int?)null,
            overrideMaximumMarkdown = true
        };
        var rejectedPayload = new
        {
            artikalId = 202,
            novaProdajnaCena = 60m,
            komentar = "over limit",
            storeId = (int?)null,
            overrideMaximumMarkdown = false
        };
        var invalidPricePayload = new
        {
            artikalId = 202,
            novaProdajnaCena = 0m,
            komentar = "invalid",
            storeId = (int?)null,
            overrideMaximumMarkdown = true
        };

        using (var unauthorized = await client.PostAsJsonAsync("/api/nivelacija", payload))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        }

        client.DefaultRequestHeaders.Add("X-Admin-Key", "rq538-test-key");
        using (var rejected = await client.PostAsJsonAsync("/api/nivelacija", rejectedPayload))
        {
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            var rejectedBody = await rejected.Content.ReadAsStringAsync();
            Assert.Contains("nivelacija_markdown_limit_exceeded", rejectedBody, StringComparison.Ordinal);
        }

        using (var invalidPrice = await client.PostAsJsonAsync("/api/nivelacija", invalidPricePayload))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalidPrice.StatusCode);
        }

        var integrityRegistry = factory.Services.GetRequiredService<OperationsAnalyticsIntegrityRegistry>();
        var nivelacijaFamily = OperationsAnalyticsIntegrityFamilies.Nivelacija;
        var priorGeneration = integrityRegistry.GetGeneration(nivelacijaFamily);
        integrityRegistry.Set(new OperationsAnalyticsIntegritySnapshot(
            OperationsAnalyticsIntegrityStates.Verified,
            "pre-write-nivelacija-evidence",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "bounded_probe",
            "Prior bounded evidence.",
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false)
        {
            Family = nivelacijaFamily,
            SourceGeneration = priorGeneration,
            ContextFingerprint = "pre-write-context"
        });

        using (var accepted = await client.PostAsJsonAsync("/api/nivelacija", payload))
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        Assert.NotEqual(priorGeneration, integrityRegistry.GetGeneration(nivelacijaFamily));
        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, integrityRegistry.GetCurrent(nivelacijaFamily).Status);
        Assert.NotEqual("pre-write-nivelacija-evidence", integrityRegistry.GetCurrent(nivelacijaFamily).EvidenceId);

        await using var db = new TrendplusDbContext(
            new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options);
        var mutationEvidence = await db.OperationsAnalyticsIntegrityEvidence.AsNoTracking()
            .Where(row => row.Family == nivelacijaFamily && row.Trigger == "nivelacija_write")
            .OrderBy(row => row.CheckedAtUtc)
            .ToListAsync();
        Assert.True(mutationEvidence.Count >= 2, "The write must durably record its fail-closed marker before committing and again after completion.");
        Assert.All(mutationEvidence, evidence => Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, evidence.Status));

        var article = await db.Artikli.AsNoTracking().SingleAsync(item => item.Id == 202);
        Assert.Equal(60m, article.ProdajnaCena);
        var supplierSplitEvents = await SupplierSalesStatsQuerySupport.LoadFirstNivelacijaByArticleAsync(
            db,
            [202],
            toUtc: null,
            storeId: 2,
            CancellationToken.None);
        Assert.True(supplierSplitEvents[202] < DateTime.UtcNow.AddDays(-99));

        var priceEvent = await db.DnevnikPromena.AsNoTracking()
            .Where(item => item.ArtikalId == 202 && item.TipPromene == "Nivelacija cena")
            .OrderByDescending(item => item.Id)
            .FirstAsync();
        Assert.Null(priceEvent.IDObjekat);
        Assert.Equal(1, priceEvent.DobavljacId);
        Assert.Equal("admin-api-key", priceEvent.KorisnikIme);
        Assert.Equal("existing", priceEvent.DataOrigin);
        Assert.Contains("Override maksimalnog sniženja", priceEvent.Komentar, StringComparison.Ordinal);

        await ExecuteAsync(connectionString, "ALTER TABLE \"DnevnikPromena\" RENAME TO rq538_unavailable_events;");
        using var failedWrite = await client.PostAsJsonAsync("/api/nivelacija", new
        {
            artikalId = 202,
            novaProdajnaCena = 70m,
            komentar = "failure path",
            storeId = (int?)null,
            overrideMaximumMarkdown = false
        });
        Assert.Equal(HttpStatusCode.InternalServerError, failedWrite.StatusCode);
        var failureBody = await failedWrite.Content.ReadAsStringAsync();
        Assert.Contains("correlationId", failureBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rq538_unavailable_events", failureBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NivelacijeHistory_ListsBothEventTypesAndUsesBelgradeHalfOpenCalendarDays()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await CreateSeededDatabaseAsync();
        await ExecuteAsync(connectionString, """
            INSERT INTO "DnevnikPromena"
                ("TipPromene", "Datum", "Iznos", "ArtikalId", "StaraProdajnaCena", "NovaProdajnaCena", "IDObjekat", "DataOrigin", "Komentar")
            VALUES
                ('Nivelacija',      TIMESTAMPTZ '2026-03-28 22:59:59+00', 0, 102, 100, 90, NULL, 'access', 'before-start'),
                ('Nivelacija',      TIMESTAMPTZ '2026-03-28 23:00:00+00', 0, 102, 100, 90, NULL, 'access', 'start'),
                ('Nivelacija cena', TIMESTAMPTZ '2026-03-29 21:59:59+00', 0, 102, 100, 90, 1, 'existing', 'last-inside'),
                ('Nivelacija cena', TIMESTAMPTZ '2026-03-29 22:00:00+00', 0, 102, 100, 90, 1, 'existing', 'exclusive-end');
            """);

        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<HybridCacheService>.Instance);
        await using var factory = new PreNivelacijaEndpointFactory(connectionString, cache);
        using var client = factory.CreateClient();

        using var allResponse = await client.GetAsync("/api/nivelacije?pageNumber=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, allResponse.StatusCode);
        using var allJson = JsonDocument.Parse(await allResponse.Content.ReadAsStringAsync());
        var rows = allJson.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(rows, row => row.GetProperty("tipPromene").GetString() == "Nivelacija");
        Assert.Contains(rows, row => row.GetProperty("tipPromene").GetString() == "Nivelacija cena");
        Assert.Contains(rows, row => row.GetProperty("idObjekat").ValueKind == JsonValueKind.Null);

        using var dayResponse = await client.GetAsync(
            "/api/nivelacije?pageNumber=1&pageSize=50&fromDate=2026-03-29&toDate=2026-03-29");
        Assert.Equal(HttpStatusCode.OK, dayResponse.StatusCode);
        using var dayJson = JsonDocument.Parse(await dayResponse.Content.ReadAsStringAsync());
        var comments = dayJson.RootElement.GetProperty("items").EnumerateArray()
            .Select(row => row.GetProperty("komentar").GetString())
            .ToArray();
        Assert.Equal(["last-inside", "start"], comments.OrderBy(comment => comment, StringComparer.Ordinal).ToArray());
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
            WITH anchor AS (
                SELECT COALESCE(MAX(p.datum_prodaje)::date, CURRENT_DATE) AS anchor_date
                FROM prodaja_zaglavlje p
                JOIN prodaja_stavke ps ON ps.id_prodaja = p.id
                JOIN "StoresDim" active_store ON active_store."StoreId" = p.id_objekat
                WHERE UPPER(BTRIM(COALESCE(p.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
                  AND UPPER(BTRIM(active_store."StoreName")) IN ('TREND PLUS 1', 'TREND PLUS 2')
                  AND (@scope = 'all'
                       OR (@scope = 'imported' AND p.data_origin = 'access')
                       OR (@scope = 'existing' AND COALESCE(p.data_origin, '') IN ('', 'existing')))
            ), sales AS (
                SELECT ps.id_artikal,
                       p.id_objekat AS store_id,
                       SUM(ps.kolicina)::integer AS units_180,
                       SUM(ps.kolicina) FILTER (WHERE ps.kolicina > 0)::integer AS positive_units_180,
                       SUM(ps.kolicina) FILTER (WHERE ps.kolicina < 0)::integer AS negative_units_180,
                       COUNT(*)::integer AS sales_row_count,
                       MAX(p.datum_prodaje) FILTER (WHERE ps.kolicina > 0) AS latest_positive_sale
                FROM prodaja_stavke ps
                JOIN prodaja_zaglavlje p ON p.id = ps.id_prodaja
                CROSS JOIN anchor
                WHERE p.datum_prodaje >= anchor.anchor_date - 179
                  AND p.datum_prodaje < anchor.anchor_date + 1
                  AND UPPER(BTRIM(COALESCE(p.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
                  AND (@scope = 'all'
                       OR (@scope = 'imported' AND p.data_origin = 'access')
                       OR (@scope = 'existing' AND COALESCE(p.data_origin, '') IN ('', 'existing')))
                GROUP BY ps.id_artikal, p.id_objekat
            ), receipts AS (
                SELECT dp."ArtikalId" AS article_id,
                       dp."IDObjekat" AS store_id,
                       MIN(dp."Datum") AS first_receipt
                FROM "DnevnikPromena" dp
                WHERE dp."TipPromene" = 'Ulaz robe'
                  AND (@scope = 'all'
                       OR (@scope = 'imported' AND dp."DataOrigin" = 'access')
                       OR (@scope = 'existing' AND COALESCE(dp."DataOrigin", '') IN ('', 'existing')))
                GROUP BY dp."ArtikalId", dp."IDObjekat"
            ), markdown AS (
                SELECT a."Id" AS article_id,
                       COUNT(dp."Id")::integer AS markdown_events,
                       COALESCE(ROUND(AVG(
                           ((dp."StaraProdajnaCena" - dp."NovaProdajnaCena") / dp."StaraProdajnaCena") * 100
                       ) FILTER (
                           WHERE dp."StaraProdajnaCena" > 0
                             AND dp."NovaProdajnaCena" < dp."StaraProdajnaCena"
                       ), 2), 0)::numeric AS average_markdown_pct
                FROM "Artikli" a
                LEFT JOIN "DnevnikPromena" dp
                  ON dp."ArtikalId" = a."Id"
                 AND (dp."IDObjekat" IS NULL OR dp."IDObjekat" IS NOT DISTINCT FROM a."IDObjekat")
                 AND dp."Datum" >= CURRENT_TIMESTAMP - INTERVAL '180 days'
                 AND dp."Datum" <= CURRENT_TIMESTAMP
                 AND dp."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
                 AND (@scope = 'all'
                      OR (@scope = 'imported' AND dp."DataOrigin" = 'access')
                      OR (@scope = 'existing' AND COALESCE(dp."DataOrigin", '') IN ('', 'existing')))
                GROUP BY a."Id"
            )
            SELECT a."Id", a."PLU", a."IDObjekat", a."Kolicina",
                   COALESCE(s.units_180, 0), COALESCE(s.positive_units_180, 0),
                   COALESCE(s.negative_units_180, 0), COALESCE(s.sales_row_count, 0),
                   CASE WHEN s.latest_positive_sale IS NULL THEN NULL
                        ELSE ((SELECT anchor_date FROM anchor) - s.latest_positive_sale::date)::integer END,
                   COALESCE(m.markdown_events, 0), COALESCE(m.average_markdown_pct, 0),
                   COALESCE(a."ProdajnaCena", a."PrvaProdajnaCena"),
                   COALESCE(a."NabavnaCenaDin", a."NabavnaCena"), a."DataOrigin",
                   CASE WHEN r.first_receipt IS NULL THEN NULL
                        ELSE ((SELECT anchor_date FROM anchor) - r.first_receipt::date)::integer END
            FROM "Artikli" a
            LEFT JOIN sales s ON s.id_artikal = a."Id" AND s.store_id IS NOT DISTINCT FROM a."IDObjekat"
            LEFT JOIN markdown m ON m.article_id = a."Id"
            LEFT JOIN receipts r ON r.article_id = a."Id" AND r.store_id IS NOT DISTINCT FROM a."IDObjekat"
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
                reader.IsDBNull(8) ? null : reader.GetInt32(8), reader.GetInt32(9), reader.GetDecimal(10), reader.GetDecimal(11),
                reader.GetDecimal(12), reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetInt32(14)));
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
            var expectedDaysSinceLastSale = row.GetProperty("daysSinceLastSale");
            Assert.Equal(expectedDaysSinceLastSale.ValueKind == JsonValueKind.Null
                    ? null
                    : expectedDaysSinceLastSale.GetInt32(),
                observed.DaysSinceLastSale);
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
            if (!expected.DaysSinceLastSale.HasValue)
            {
                Assert.Equal(JsonValueKind.Null, candidate.GetProperty("daysSinceLastSale").ValueKind);
                Assert.Equal("never_sold", candidate.GetProperty("salesHistoryStatus").GetString());
            }
            else
            {
                Assert.Equal(expected.DaysSinceLastSale.Value, candidate.GetProperty("daysSinceLastSale").GetInt32());
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
            var recencyRisk = Clamp(Math.Min(row.DaysSinceLastSale ?? row.DaysSinceReceipt ?? 0, 180) * 100m / 180m);
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
        Assert.Equal(0, newStock.GetProperty("daysSinceReceipt").GetInt32());
        Assert.Equal("new_stock", newStock.GetProperty("stockAgeStatus").GetString());
        Assert.Equal("new_stock", newStock.GetProperty("reasonCode").GetString());
        var neverSold = candidates["RQ548-S1-NEVER"];
        Assert.Equal(JsonValueKind.Null, neverSold.GetProperty("daysSinceLastSale").ValueKind);
        Assert.Equal("never_sold", neverSold.GetProperty("salesHistoryStatus").GetString());
        Assert.Equal("established", neverSold.GetProperty("stockAgeStatus").GetString());
        Assert.Equal("high", neverSold.GetProperty("priorityBand").GetString());
        var firstSaleFallback = candidates["RQ548-S1-NET"];
        Assert.Equal("first_sale_fallback", firstSaleFallback.GetProperty("receiptEvidenceStatus").GetString());
        Assert.Equal(87, firstSaleFallback.GetProperty("daysSinceReceipt").GetInt32());

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

    [Fact]
    public async Task PreNivelacijaEndpoint_ConfiguredNewStockAgeThresholdChangesClassification()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await CreateSeededDatabaseAsync();
        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<HybridCacheService>.Instance);
        await using var factory = new PreNivelacijaEndpointFactory(connectionString, cache, minimumNewStockAgeDays: 2);
        using var client = factory.CreateClient();

        using var response = await GetJsonAsync(client, "all", storeId: 1);
        var newStock = response.RootElement.GetProperty("queues").GetProperty("newStock").EnumerateArray()
            .Single(item => item.GetProperty("sku").GetString() == "RQ548-S1-NEW");
        Assert.Equal("new_stock", newStock.GetProperty("stockAgeStatus").GetString());
        Assert.Equal(0, newStock.GetProperty("daysSinceReceipt").GetInt32());
    }

    [Fact]
    public async Task PreNivelacijaEndpoint_ReceiptOlderThanDefaultThresholdIsNotProtectedAsNewStock()
    {
        if (!_fixture.IsAvailable)
            return;

        var connectionString = await CreateSeededDatabaseAsync();
        await ExecuteAsync(connectionString, """
            INSERT INTO "Artikli"
                ("Id", "PLU", "Naziv", "NabavnaCena", "NabavnaCenaDin", "PrvaProdajnaCena", "ProdajnaCena",
                 "IDDobavljac", "IDTipObuce", "UpdatedAt", "Kolicina", "IDObjekat", "Kategorija", "DataOrigin")
            VALUES
                (108, 'RQ539-S1-OLDER', 'RQ539 receipt older than threshold', 50, 50, 100, 100,
                 1, 1, CURRENT_TIMESTAMP - INTERVAL '40 days', 9, 1, 'Obuca', 'access');
            INSERT INTO "DnevnikPromena"
                ("TipPromene", "Datum", "Iznos", "ArtikalId", "IDObjekat", "DataOrigin")
            VALUES ('Ulaz robe', CURRENT_TIMESTAMP - INTERVAL '40 days', 0, 108, 1, 'access');
            """);

        var cache = new HybridCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<HybridCacheService>.Instance);
        await using var factory = new PreNivelacijaEndpointFactory(connectionString, cache);
        using var client = factory.CreateClient();

        using var response = await GetJsonAsync(client, "all", storeId: 1);
        var candidate = response.RootElement.GetProperty("candidates").EnumerateArray()
            .Single(item => item.GetProperty("sku").GetString() == "RQ539-S1-OLDER");
        Assert.Equal("established", candidate.GetProperty("stockAgeStatus").GetString());
        Assert.Equal(37, candidate.GetProperty("daysSinceReceipt").GetInt32());
        Assert.DoesNotContain(response.RootElement.GetProperty("queues").GetProperty("newStock").EnumerateArray(),
            item => item.GetProperty("sku").GetString() == "RQ539-S1-OLDER");
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
        int? DaysSinceLastSale,
        int MarkdownEvents,
        decimal AverageMarkdownPct,
        decimal SellingPrice,
        decimal PurchasePrice,
        string DataOrigin,
        int? DaysSinceReceipt);

    private sealed class PreNivelacijaEndpointFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _connectionString;
        private readonly IAnalyticsCacheService _cache;
        private readonly int? _minimumNewStockAgeDays;

        public PreNivelacijaEndpointFactory(
            string connectionString,
            IAnalyticsCacheService cache,
            int? minimumNewStockAgeDays = null)
        {
            _connectionString = connectionString;
            _cache = cache;
            _minimumNewStockAgeDays = minimumNewStockAgeDays;
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
                    ["Admin:ApiKey"] = "rq538-test-key",
                    ["Pricing:Nivelacija:MaximumMarkdownPercent"] = "35",
                    ["Caching:Provider"] = "disabled",
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["ConnectionStrings:AnalyticsConnection"] = _connectionString,
                    ["ConnectionStrings:OpenProductTrainingConnection"] = _connectionString,
                    ["PerformanceLogging:CaptureHttpRequests"] = "false"
                }.Concat(_minimumNewStockAgeDays.HasValue
                    ? new Dictionary<string, string?>
                    {
                        ["Analytics:PreNivelacija:MinimumNewStockAgeDays"] = _minimumNewStockAgeDays.Value.ToString(CultureInfo.InvariantCulture)
                    }
                    : new Dictionary<string, string?>())));

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
