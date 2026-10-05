using System.Net;
using System.Net.Http;
using System.Text.Json;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Api.Tests;
using Infrastructure.DbContexts;
using Infrastructure.Services;
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
        var rq568Fixture = await SeedSharedFixtureAsync(connectionString!);
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
            "/api/analytics/daily-sales?fromDate=2026-09-01&toDate=2026-09-02&dataScope=all&topN=25");
        Assert.Equal(6, dailyBoundary.GetProperty("metadata").GetProperty("totalItemsInRange").GetInt32());
        Assert.Equal(590m, dailyBoundary.GetProperty("dateRows").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(9, dailyBoundary.GetProperty("metadata").GetProperty("nonStandardReceiptCount").GetInt32());
        Assert.Equal("all", dailyBoundary.GetProperty("metadata").GetProperty("diagnosticsDataScope").GetString());
        Assert.Equal("all", dailyBoundary.GetProperty("meta").GetProperty("effectiveDataScope").GetString());
        var dailyImportedStore2 = await GetJsonAsync(
            client,
            "/api/analytics/daily-sales?fromDate=2026-09-01&toDate=2026-09-02&dataScope=imported&storeId=2&topN=25");
        Assert.Equal(2, dailyImportedStore2.GetProperty("metadata").GetProperty("totalItemsInRange").GetInt32());
        Assert.Equal(240m, dailyImportedStore2.GetProperty("dateRows").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(2, dailyImportedStore2.GetProperty("storeId").GetInt32());
        Assert.Equal("imported", dailyImportedStore2.GetProperty("meta").GetProperty("effectiveDataScope").GetString());
        var dailyHeaderImported = await GetJsonAsync(
            client,
            "/api/analytics/daily-sales?fromDate=2026-09-10&toDate=2026-09-11&dataScope=imported&topN=25");
        var dailyHeaderExisting = await GetJsonAsync(
            client,
            "/api/analytics/daily-sales?fromDate=2026-09-10&toDate=2026-09-11&dataScope=existing&topN=25");
        Assert.Equal(2, dailyHeaderImported.GetProperty("metadata").GetProperty("totalItemsInRange").GetInt32());
        Assert.Equal(200m, dailyHeaderImported.GetProperty("dateRows").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        Assert.Equal(3, dailyHeaderExisting.GetProperty("metadata").GetProperty("totalItemsInRange").GetInt32());
        Assert.Equal(390m, dailyHeaderExisting.GetProperty("dateRows").EnumerateArray().Sum(row => row.GetProperty("totalRevenue").GetDecimal()));
        verdicts.Add(new("/analytics/daily-sales", "/api/analytics/daily-sales", 5, 5, "PASS", "period, source attribution, DUG diagnostic, signed/boundary total, store scope and RQ494 header-origin population asserted"));

        var markdown = await GetJsonAsync(
            client,
            $"/api/analytics/vendor-sales-nivelacija?eventDate={rq568Fixture.AnchorDate:yyyy-MM-dd}&dataScope=all");
        Assert.True(markdown.GetProperty("meta").GetProperty("success").GetBoolean(), markdown.GetRawText());
        AssertEndpointArticleMatchesOracle(markdown, "RQ568-MARKDOWN", rq568Fixture.OracleRows[56801], rq568Fixture.AsOfDate);
        var markdownArticle = Assert.Single(markdown.GetProperty("articleStats").EnumerateArray(), article => article.GetProperty("sku").GetString() == "RQ568-MARKDOWN");
        Assert.Equal(150, markdownArticle.GetProperty("preQty").GetInt32());
        Assert.Equal(150, markdownArticle.GetProperty("postQty").GetInt32());
        Assert.Equal(1500m, markdownArticle.GetProperty("preRevenue").GetDecimal());
        Assert.Equal(1500m, markdownArticle.GetProperty("postRevenue").GetDecimal());
        Assert.Equal(0m, markdownArticle.GetProperty("changePercent").GetDecimal());
        var basis = markdown.GetProperty("meta").GetProperty("basis");
        Assert.Equal("assortment", basis.GetProperty("tab").GetString());
        Assert.Equal("latest_price_event_per_article_including_increases", basis.GetProperty("cohort").GetString());
        Assert.Equal("fixed_30d_pre_post_revenue_and_units_pct", basis.GetProperty("effectMetric").GetString());
        Assert.Equal("latest_price_event_per_article", basis.GetProperty("eventSelection").GetString());
        Assert.Equal("latest_event_per_article", markdown.GetProperty("dataQuality").GetProperty("cohortPolicy").GetString());

        var markup = await GetJsonAsync(
            client,
            $"/api/analytics/vendor-sales-nivelacija?eventDate={rq568Fixture.AnchorDate:yyyy-MM-dd}&vendorId=1&category=Obuca&dataScope=all");
        Assert.True(markup.GetProperty("meta").GetProperty("success").GetBoolean(), markup.GetRawText());
        AssertEndpointArticleMatchesOracle(markup, "RQ568-MARKUP", rq568Fixture.OracleRows[56802], rq568Fixture.AsOfDate);
        Assert.Contains(markup.GetProperty("priceDirectionStats").EnumerateArray(), row =>
            row.GetProperty("segment").GetString() == "Cena ↑"
            && row.GetProperty("hasComparableSalesWindow").GetBoolean());

        var overlap = await GetJsonAsync(
            client,
            $"/api/analytics/vendor-sales-nivelacija?eventDate={rq568Fixture.AnchorDate.AddDays(-20):yyyy-MM-dd}&dataScope=all");
        Assert.True(overlap.GetProperty("meta").GetProperty("success").GetBoolean(), overlap.GetRawText());
        AssertEndpointArticleMatchesOracle(overlap, "RQ568-OVERLAP", rq568Fixture.OracleRows[56803], rq568Fixture.AsOfDate);

        var immature = await GetJsonAsync(
            client,
            $"/api/analytics/vendor-sales-nivelacija?eventDate={rq568Fixture.AsOfDate.AddDays(-10):yyyy-MM-dd}&dataScope=all");
        Assert.True(immature.GetProperty("meta").GetProperty("success").GetBoolean(), immature.GetRawText());
        AssertEndpointArticleMatchesOracle(immature, "RQ568-IMMATURE", rq568Fixture.OracleRows[56805], rq568Fixture.AsOfDate);
        Assert.False(Assert.Single(immature.GetProperty("articleStats").EnumerateArray()).GetProperty("isPostWindowMature").GetBoolean());
        Assert.True(
            immature.GetProperty("meta").GetProperty("operationsIntegrityStatus").GetString() == OperationsAnalyticsIntegrityStates.Verified,
            immature.GetProperty("meta").GetRawText());
        Assert.True(immature.GetProperty("meta").GetProperty("operationsIntegrityContextMatches").GetBoolean());
        Assert.Contains(
            immature.GetProperty("meta").GetProperty("operationsIntegrityEvidenceDimensions").GetProperty("events").EnumerateArray(),
            item => item.GetProperty("eventId").GetInt64() == 56805);

        var importedStore2 = await GetJsonAsync(
            client,
            $"/api/analytics/vendor-sales-nivelacija?eventDate={rq568Fixture.AnchorDate:yyyy-MM-dd}&storeId=2&dataScope=imported");
        Assert.True(importedStore2.GetProperty("meta").GetProperty("success").GetBoolean(), importedStore2.GetRawText());
        AssertEndpointArticleMatchesOracle(
            importedStore2,
            "RQ568-STORE2-IMPORTED",
            rq568Fixture.ScopedOracleRows[56806],
            rq568Fixture.AsOfDate);
        Assert.Equal(2, importedStore2.GetProperty("storeId").GetInt32());
        Assert.Equal("imported", importedStore2.GetProperty("dataScope").GetString());
        Assert.True(importedStore2.GetProperty("scopeApplied").GetBoolean());
        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified,
            importedStore2.GetProperty("meta").GetProperty("operationsIntegrityStatus").GetString());
        Assert.True(importedStore2.GetProperty("meta").GetProperty("operationsIntegrityContextMatches").GetBoolean());
        Assert.Equal(2,
            importedStore2.GetProperty("meta").GetProperty("operationsIntegrityEvidenceDimensions").GetProperty("storeId").GetInt32());
        Assert.Equal("imported",
            importedStore2.GetProperty("meta").GetProperty("operationsIntegrityEvidenceDimensions").GetProperty("dataScope").GetString());

        verdicts.Add(new(
            "/analytics/nivelacije-pre-post",
            "/api/analytics/vendor-sales-nivelacija",
            6,
            0,
            "UNVERIFIED",
            "Canonical SQL, independent raw-fixture oracle, RQ550 150/150 baseline, mature markdown/markup, immature/overlap events, imported store 2 scope and truthful missing-view response are asserted.",
            CanonicalSqlExecuted: true,
            OracleExecuted: true,
            MissingObjectNegativePathExecuted: false));

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
        Assert.Equal(OperationsAnalyticsIntegrityStates.Verified,
            preNivelacija.GetProperty("meta").GetProperty("operationsIntegrityStatus").GetString());
        Assert.True(preNivelacija.GetProperty("meta").GetProperty("operationsIntegrityContextMatches").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null,
            preNivelacija.GetProperty("meta").GetProperty("operationsIntegrityEvidenceDimensions").ValueKind);
        Assert.Equal(1, preNivelacija.GetProperty("totalCandidates").GetInt32());
        Assert.Contains(
            preNivelacija.GetProperty("candidates").EnumerateArray(),
            candidate => candidate.GetProperty("sku").GetString() == "PRE-105");
        var preNivelacijaImported = await GetJsonAsync(
            client,
            "/api/analytics/pre-nivelacija-prioriteti?dataScope=imported&page=1&pageSize=100&focus=all");
        Assert.Equal("imported", preNivelacijaImported.GetProperty("meta").GetProperty("effectiveDataScope").GetString());
        Assert.Equal("pre_nivelacija_product_origin_filter", preNivelacijaImported.GetProperty("meta").GetProperty("dataScopeSource").GetString());
        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified,
            preNivelacijaImported.GetProperty("meta").GetProperty("operationsIntegrityStatus").GetString());
        Assert.True(preNivelacijaImported.GetProperty("meta").GetProperty("operationsIntegrityContextMatches").GetBoolean());
        Assert.Equal("imported",
            preNivelacijaImported.GetProperty("meta").GetProperty("operationsIntegrityEvidenceDimensions").GetProperty("dataScope").GetString());
        Assert.DoesNotContain(
            preNivelacijaImported.GetProperty("candidates").EnumerateArray(),
            candidate => candidate.GetProperty("sku").GetString() == "PRE-105");
        verdicts.Add(new("/analytics/pre-nivelacija-prioriteti", "/api/analytics/pre-nivelacija-prioriteti", 2, 2, "PASS", "candidate count, visible SKU and imported-scope exclusion asserted"));

        await using (var integrityDb = new TrendplusDbContext(
                         new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options))
        {
            var probe = new NivelacijaOperationsIntegrityProbe(integrityDb);
            var checkedAt = DateTime.UtcNow;
            var fromUtc = checkedAt.AddDays(-180);
            var definition = OperationsAnalyticsIntegrityFamilies.DefinitionFor(OperationsAnalyticsIntegrityFamilies.Nivelacija);
            var request = new OperationsAnalyticsIntegrityProbeRequest(
                definition,
                "rq564-probe-context",
                "rq564-probe-generation",
                fromUtc,
                checkedAt,
                "all",
                "rq564-integration-test",
                definition.MaxRows,
                CancellationToken.None);

            var baseline = await probe.ProbeAsync(request);
            Assert.Equal(OperationsAnalyticsIntegrityStates.Verified, baseline.Status);
            var eventEvidence = Assert.Single(
                baseline.EvidenceDimensions!.Value.GetProperty("events").EnumerateArray(),
                row => row.GetProperty("eventId").GetInt64() == 56805);
            Assert.Equal("markdown", eventEvidence.GetProperty("direction").GetString());
            Assert.False(eventEvidence.GetProperty("isMature").GetBoolean());
            Assert.False(eventEvidence.GetProperty("overlapsNextEvent").GetBoolean());
            Assert.Equal(1, eventEvidence.GetProperty("sameDayEventCount").GetInt32());
            Assert.Equal(1, eventEvidence.GetProperty("storeId").GetInt32());
            Assert.Equal("1", eventEvidence.GetProperty("storeIdentity").GetString());
            Assert.True(eventEvidence.GetProperty("canonicalStoreIdentityAvailable").GetBoolean());
            Assert.Equal(1, eventEvidence.GetProperty("canonicalStoreId").GetInt32());
            Assert.Equal("existing", eventEvidence.GetProperty("dataOrigin").GetString());
            Assert.Equal("all", eventEvidence.GetProperty("dataScope").GetString());
            Assert.Equal(eventEvidence.GetProperty("eventDateUtc").GetDateTime().AddDays(-30), eventEvidence.GetProperty("preWindowFromUtc").GetDateTime());
            Assert.Equal(eventEvidence.GetProperty("eventDateUtc").GetDateTime().AddDays(30), eventEvidence.GetProperty("postWindowToUtc").GetDateTime());
            var markupDate = rq568Fixture.AnchorDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var markupProbe = await probe.ProbeAsync(request with { FromUtc = markupDate, ToUtc = markupDate });
            Assert.Equal(OperationsAnalyticsIntegrityStates.Verified, markupProbe.Status);
            var markupEvidence = Assert.Single(
                markupProbe.EvidenceDimensions!.Value.GetProperty("events").EnumerateArray(),
                row => row.GetProperty("eventId").GetInt64() == 56802);
            Assert.Equal("markup", markupEvidence.GetProperty("direction").GetString());
        }

        await ExecuteConnectionSqlAsync(connectionString!, "ALTER VIEW vw_vendor_sales_nivelacija RENAME TO rq564_original_vendor_sales_nivelacija;");
        await ExecuteConnectionSqlAsync(connectionString!, """
            CREATE VIEW vw_vendor_sales_nivelacija AS
            SELECT price_event_id,
                   CASE WHEN price_event_id = 56805 THEN article_id + 100000 ELSE article_id END AS article_id,
                   CASE WHEN price_event_id = 56805 THEN store_id + 1 ELSE store_id END AS store_id,
                   event_date, old_price, new_price,
                   CASE WHEN price_event_id = 56805 THEN 'markup' ELSE price_direction END AS price_direction,
                   post_window_complete, overlaps_next_event, next_event_date,
                   CASE WHEN price_event_id = 56805 THEN same_day_event_count + 1 ELSE same_day_event_count END AS same_day_event_count,
                   pre_qty, pre_revenue, post_qty, post_revenue
            FROM rq564_original_vendor_sales_nivelacija;
            """);
        await using (var driftDb = new TrendplusDbContext(
                         new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options))
        {
            var probe = new NivelacijaOperationsIntegrityProbe(driftDb);
            var checkedAt = DateTime.UtcNow;
            var definition = OperationsAnalyticsIntegrityFamilies.DefinitionFor(OperationsAnalyticsIntegrityFamilies.Nivelacija);
            var drift = await probe.ProbeAsync(new OperationsAnalyticsIntegrityProbeRequest(
                definition,
                "rq564-drift-context",
                "rq564-drift-generation",
                checkedAt.AddDays(-180),
                checkedAt,
                "all",
                "rq564-deliberate-drift",
                definition.MaxRows,
                CancellationToken.None));
            Assert.Equal(OperationsAnalyticsIntegrityStates.DriftDetected, drift.Status);
            Assert.True(drift.BlocksDecisionSignals);
            var changedEvent = Assert.Single(
                drift.EvidenceDimensions!.Value.GetProperty("events").EnumerateArray(),
                row => row.GetProperty("eventId").GetInt64() == 56805);
            Assert.Contains("price_direction", changedEvent.GetProperty("mismatches").EnumerateArray().Select(value => value.GetString()));
            Assert.Contains("event_article_identity", changedEvent.GetProperty("mismatches").EnumerateArray().Select(value => value.GetString()));
            Assert.Contains("event_store_identity", changedEvent.GetProperty("mismatches").EnumerateArray().Select(value => value.GetString()));
            Assert.Contains("same_day_cohort_count", changedEvent.GetProperty("mismatches").EnumerateArray().Select(value => value.GetString()));

            var scopedDrift = await probe.ProbeAsync(new OperationsAnalyticsIntegrityProbeRequest(
                definition,
                "rq564-store-scoped-drift-context",
                "rq564-store-scoped-drift-generation",
                rq568Fixture.AsOfDate.AddDays(-180).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                rq568Fixture.AsOfDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                "existing",
                "rq564-deliberate-store-scoped-cohort-drift",
                definition.MaxRows,
                CancellationToken.None,
                StoreId: 1));
            Assert.Equal(OperationsAnalyticsIntegrityStates.DriftDetected, scopedDrift.Status);
            Assert.True(scopedDrift.BlocksDecisionSignals);
            Assert.Equal(1, scopedDrift.EvidenceDimensions!.Value.GetProperty("storeId").GetInt32());
            Assert.Equal("existing", scopedDrift.EvidenceDimensions.Value.GetProperty("dataScope").GetString());
            var scopedChangedEvent = Assert.Single(
                scopedDrift.EvidenceDimensions.Value.GetProperty("events").EnumerateArray(),
                row => row.GetProperty("eventId").GetInt64() == 56805);
            Assert.Contains("event_article_identity", scopedChangedEvent.GetProperty("mismatches").EnumerateArray().Select(value => value.GetString()));
        }

        await ExecuteConnectionSqlAsync(connectionString!, "DROP VIEW vw_vendor_sales_nivelacija;");
        await ExecuteConnectionSqlAsync(connectionString!, "ALTER VIEW rq564_original_vendor_sales_nivelacija RENAME TO vw_vendor_sales_nivelacija;");

        await ExecuteConnectionSqlAsync(connectionString!, "DROP VIEW IF EXISTS vw_vendor_sales_nivelacija CASCADE;");
        var missingVendorNivelacija = await GetJsonAsync(
            client,
            $"/api/analytics/vendor-sales-nivelacija?eventDate={rq568Fixture.AnchorDate:yyyy-MM-dd}&includeInactive=true&dataScope=all");
        Assert.False(missingVendorNivelacija.GetProperty("meta").GetProperty("success").GetBoolean(), missingVendorNivelacija.GetRawText());
        Assert.Equal("vendor_sales_nivelacija_contract_missing", missingVendorNivelacija.GetProperty("meta").GetProperty("errorCode").GetString());
        Assert.Equal("unavailable", missingVendorNivelacija.GetProperty("dataCoverageStatus").GetString());
        Assert.False(missingVendorNivelacija.GetProperty("recommendationAllowed").GetBoolean());
        var prePostIndex = verdicts.FindIndex(item => item.ApiRoute == "/api/analytics/vendor-sales-nivelacija");
        Assert.True(prePostIndex >= 0);
        verdicts[prePostIndex] = verdicts[prePostIndex] with { MissingObjectNegativePathExecuted = true };

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
            var executedCount = requestCounts.GetValueOrDefault(current.ApiRoute);
            var updated = current with { ExecutedCount = executedCount };
            if (current.ApiRoute == "/api/analytics/vendor-sales-nivelacija")
            {
                updated = updated with
                {
                    EndpointCasesExecuted = executedCount,
                    Verdict = current.CanonicalSqlExecuted == true
                        && current.OracleExecuted == true
                        && current.MissingObjectNegativePathExecuted == true
                        && executedCount == current.ExpectedCount
                        ? "VERIFIED"
                        : "UNVERIFIED"
                };
            }

            verdicts[index] = updated;
        }

        var report = new
        {
            manifestId = "operations-six-screen-certification-2026-10-04",
            expectedRoutes = 6,
            executedRoutes = verdicts.Count(item => item.ExecutedCount > 0),
            skippedRoutes = 6 - verdicts.Count(item => item.ExecutedCount > 0),
            expectedCases = 31,
            executedCases = verdicts.Sum(item => item.ExpectedCount),
            verdict = verdicts.Any(item => item.Verdict is not ("PASS" or "VERIFIED")) ? "UNVERIFIED" : "PASS",
            routes = verdicts
        };
        _output.WriteLine(JsonSerializer.Serialize(report));
        Assert.Equal(6, verdicts.Count);
        Assert.All(verdicts, item => Assert.Equal(item.ExpectedCount, item.ExecutedCount));
        Assert.Equal(6, report.executedRoutes);
        Assert.Equal(0, report.skippedRoutes);
        Assert.Equal(report.expectedCases, report.executedCases);
        Assert.Equal("PASS", report.verdict);
        var prePostVerdict = Assert.Single(verdicts, item => item.ApiRoute == "/api/analytics/vendor-sales-nivelacija");
        Assert.Equal("VERIFIED", prePostVerdict.Verdict);
        Assert.True(prePostVerdict.CanonicalSqlExecuted);
        Assert.True(prePostVerdict.OracleExecuted);
        Assert.True(prePostVerdict.MissingObjectNegativePathExecuted);
        Assert.Equal(6, prePostVerdict.EndpointCasesExecuted);
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

    private static async Task<Rq568FixtureContext> SeedSharedFixtureAsync(string connectionString)
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

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteRepositorySqlAsync(connection, "Database", "Migrations", "012_AddAccessImportSupport.sql");

        var sharedFixture = await File.ReadAllTextAsync(
            FindRepositoryFile("Api.Tests", "Fixtures", "operations-analytics-all-routes-seed.sql"));
        await ExecuteConnectionSqlAsync(connection, sharedFixture);
        var prePostFixture = await File.ReadAllTextAsync(
            FindRepositoryFile("Api.Tests", "Fixtures", "rq568-prepost-adversarial.sql"));
        await ExecuteConnectionSqlAsync(connection, prePostFixture);

        // Run the canonical-view dependencies in DatabaseInitializer order; 019 is a dashboard-only index script and is outside this view contract.
        await ExecuteRepositorySqlAsync(connection, "Database", "Migrations", "017_CreateNightlyAnalyticsMaterializedViews.sql");
        await ExecuteRepositorySqlAsync(connection, "Database", "Migrations", "013_AddVendorSalesNivelacijaViews.sql");
        await ExecuteRepositorySqlAsync(connection, "Database", "Migrations", "014_NormalizeNivelacijaEvents.sql");
        await ExecuteRepositorySqlAsync(connection, "Database", "Analytics", "014_CreateVendorSalesNivelacijaViews.sql");
        await ExecuteRepositorySqlAsync(connection, "Database", "Migrations", "016_AnalyticsNivelacijaEnhancements.sql");

        return await VerifyRq568CanonicalSqlAndOracleAsync(connection);
    }

    private static async Task ExecuteRepositorySqlAsync(NpgsqlConnection connection, params string[] segments)
    {
        var sql = await File.ReadAllTextAsync(FindRepositoryFile(segments));
        var batches = sql.Split("-- SQL_BATCH_BREAK", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var batch in batches)
            await ExecuteConnectionSqlAsync(connection, batch);
    }

    private static async Task ExecuteConnectionSqlAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteConnectionSqlAsync(connection, sql);
    }

    private static async Task ExecuteConnectionSqlAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Rq568FixtureContext> VerifyRq568CanonicalSqlAndOracleAsync(NpgsqlConnection connection)
    {
        const string requiredContractSql = """
            SELECT to_regclass('public.vw_sales_pre_nivelacija') IS NOT NULL
               AND to_regclass('public.vw_sales_post_nivelacija') IS NOT NULL
               AND to_regclass('public.vw_vendor_sales_nivelacija') IS NOT NULL
               AND to_regclass('public.vw_nivelacija_kontrolna_grupa') IS NOT NULL
               AND to_regclass('public.vw_nivelacija_did') IS NOT NULL
               AND EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'vw_vendor_sales_nivelacija'
                      AND column_name = 'post_window_complete'
               );
            """;
        await using (var command = new NpgsqlCommand(requiredContractSql, connection))
            Assert.True((bool)(await command.ExecuteScalarAsync() ?? false), "Canonical Pre/Post views and maturity/DiD dependencies were not installed.");

        var asOf = await ReadDatabaseCurrentDateAsync(connection);
        var fixture = await ReadRq568OracleFixtureAsync(connection);
        var expected = AssortmentNivelacijaOracle.SourceRows(fixture, AssortmentSourceOptions.View(asOf));
        var actual = await ReadRq568CanonicalRowsAsync(connection);
        AssertAssortmentRowsEqual("RQ568 canonical view vs independent raw-fixture oracle", expected, actual);

        var semantics = await ReadRq568ViewSemanticsAsync(connection);
        Assert.Equal("markdown", semantics[56801].PriceDirection);
        Assert.True(semantics[56801].PostWindowComplete);
        Assert.Equal("markup", semantics[56802].PriceDirection);
        Assert.True(semantics[56802].PostWindowComplete);
        Assert.True(semantics[56803].PostWindowComplete);
        Assert.True(semantics[56803].OverlapsNextEvent);
        Assert.Equal(asOf.AddDays(-305), semantics[56803].NextEventDate);
        Assert.False(semantics[56805].PostWindowComplete);

        var scopedExpected = AssortmentNivelacijaOracle.SourceRows(fixture, AssortmentSourceOptions.Scoped(asOf, storeId: 2))
            .ToDictionary(row => row.PriceEventId);
        return new Rq568FixtureContext(
            AnchorDate: asOf.AddDays(-300),
            AsOfDate: asOf,
            OracleRows: expected.ToDictionary(row => row.PriceEventId),
            ScopedOracleRows: scopedExpected);
    }

    private static async Task<DateOnly> ReadDatabaseCurrentDateAsync(NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand("SELECT CURRENT_DATE::timestamp;", connection);
        var value = await command.ExecuteScalarAsync();
        return DateOnly.FromDateTime((DateTime)(value ?? throw new InvalidOperationException("PostgreSQL current date is unavailable.")));
    }

    private static async Task<AssortmentFixture> ReadRq568OracleFixtureAsync(NpgsqlConnection connection)
    {
        var fixture = new AssortmentFixture();
        const string vendorsSql = """
            SELECT DISTINCT vendor."Id", vendor."Naziv"
            FROM "Dobavljaci" vendor
            JOIN "Artikli" article ON article."IDDobavljac" = vendor."Id"
            WHERE article."PLU" LIKE 'RQ568-%'
            ORDER BY vendor."Id";
            """;
        await using (var command = new NpgsqlCommand(vendorsSql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                fixture.Vendors.Add(new AssortmentVendor(reader.GetInt32(0), reader.GetString(1)));
        }

        const string articlesSql = """
            SELECT "Id", "IDDobavljac", COALESCE(NULLIF("Kategorija", ''), 'Nepoznato'), "PLU", COALESCE("Kolicina", 0)::numeric
            FROM "Artikli"
            WHERE "PLU" LIKE 'RQ568-%'
            ORDER BY "Id";
            """;
        await using (var command = new NpgsqlCommand(articlesSql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                fixture.Articles.Add(new AssortmentArticle(reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4)));
        }

        const string eventsSql = """
            SELECT event."Id"::bigint, event."ArtikalId", COALESCE(source."Datum", event."Datum")::date,
                   event."StaraProdajnaCena", event."NovaProdajnaCena", event."IDObjekat",
                   event."TipPromene", event."DobavljacId"
            FROM "DnevnikPromena" event
            JOIN "Artikli" article ON article."Id" = event."ArtikalId"
            LEFT JOIN "DnevnikPromena" source
              ON source."Id" = CASE WHEN event."BrojRacuna" ~ '^[0-9]+$' THEN event."BrojRacuna"::bigint END
            WHERE article."PLU" LIKE 'RQ568-%'
            ORDER BY event."Id";
            """;
        await using (var command = new NpgsqlCommand(eventsSql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                fixture.Events.Add(new AssortmentEvent(reader.GetInt64(0), reader.GetInt32(1), DateOnly.FromDateTime(reader.GetDateTime(2)), reader.GetDecimal(3), reader.GetDecimal(4), reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetInt32(7)));
        }

        const string salesSql = """
            SELECT receipt.id, receipt.broj_racuna, receipt.datum_prodaje::date, line.id_artikal,
                   line.kolicina, line.cena, receipt.id_objekat
            FROM prodaja_zaglavlje receipt
            JOIN prodaja_stavke line ON line.id_prodaja = receipt.id
            JOIN "Artikli" article ON article."Id" = line.id_artikal
            WHERE article."PLU" LIKE 'RQ568-%'
            ORDER BY receipt.id, line.id;
            """;
        await using (var command = new NpgsqlCommand(salesSql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                fixture.Sales.Add(new AssortmentSale(reader.GetInt32(0), reader.GetString(1), DateOnly.FromDateTime(reader.GetDateTime(2)), reader.GetInt32(3), reader.GetInt32(4), reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetInt32(6)));
        }

        Assert.Equal(5, fixture.Articles.Count);
        Assert.Equal(6, fixture.Events.Count);
        Assert.NotEmpty(fixture.Sales);
        return fixture;
    }

    private static async Task<List<AssortmentSourceRow>> ReadRq568CanonicalRowsAsync(NpgsqlConnection connection)
    {
        const string sql = """
            SELECT view.price_event_id, view.event_date, view.vendor_id, view.vendor_name, view.article_id,
                   view.sku, view.category, view.old_price, view.new_price, view.pre_qty, view.pre_revenue,
                   view.post_qty, view.post_revenue, view.coverage_pre30, view.coverage_post30,
                   view.change_qty, view.change_revenue, view.has_qty_baseline, view.qty_baseline_reason,
                   view.change_percent_qty_semantic, view.has_revenue_baseline, view.revenue_baseline_reason,
                   view.change_percent_revenue_semantic
            FROM vw_vendor_sales_nivelacija view
            JOIN "Artikli" article ON article."Id" = view.article_id
            WHERE article."PLU" LIKE 'RQ568-%'
            ORDER BY view.price_event_id;
            """;
        var rows = new List<AssortmentSourceRow>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        int? Int(string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : Convert.ToInt32(reader[column]);
        decimal? Decimal(string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : Convert.ToDecimal(reader[column]);
        string? String(string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(reader.GetOrdinal(column));
        while (await reader.ReadAsync())
        {
            rows.Add(new AssortmentSourceRow(
                PriceEventId: reader.GetInt64(0), EventDate: DateOnly.FromDateTime(reader.GetDateTime(1)),
                VendorId: Int("vendor_id"), VendorName: String("vendor_name"), ArticleId: reader.GetInt32(4),
                Sku: String("sku"), Category: String("category"), OldPrice: Decimal("old_price"), NewPrice: Decimal("new_price"),
                PreQty: Decimal("pre_qty"), PreRevenue: Decimal("pre_revenue"), PostQty: Decimal("post_qty"), PostRevenue: Decimal("post_revenue"),
                CoveragePre30: Decimal("coverage_pre30"), CoveragePost30: Decimal("coverage_post30"), ChangeQty: Decimal("change_qty"),
                ChangeRevenue: Decimal("change_revenue"), HasQtyBaseline: reader.GetBoolean(reader.GetOrdinal("has_qty_baseline")),
                QtyBaselineReason: String("qty_baseline_reason"), ChangePercentQtySemantic: Decimal("change_percent_qty_semantic"),
                HasRevenueBaseline: reader.GetBoolean(reader.GetOrdinal("has_revenue_baseline")), RevenueBaselineReason: String("revenue_baseline_reason"),
                ChangePercentRevenueSemantic: Decimal("change_percent_revenue_semantic")));
        }

        return rows;
    }

    private static async Task<Dictionary<long, Rq568ViewSemantics>> ReadRq568ViewSemanticsAsync(NpgsqlConnection connection)
    {
        const string sql = """
            SELECT price_event_id, price_direction, post_window_complete, overlaps_next_event, next_event_date
            FROM vw_vendor_sales_nivelacija WHERE price_event_id BETWEEN 56801 AND 56806;
            """;
        var rows = new Dictionary<long, Rq568ViewSemantics>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(reader.GetInt64(0), new Rq568ViewSemantics(reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4))));
        Assert.Equal(6, rows.Count);
        return rows;
    }

    private static void AssertAssortmentRowsEqual(string source, IEnumerable<AssortmentSourceRow> expected, IEnumerable<AssortmentSourceRow> actual)
    {
        var expectedById = expected.Select(row => row.Normalized()).ToDictionary(row => row.PriceEventId);
        var actualById = actual.Select(row => row.Normalized()).ToDictionary(row => row.PriceEventId);
        var mismatches = new List<string>();
        foreach (var id in expectedById.Keys.Union(actualById.Keys).Order())
        {
            expectedById.TryGetValue(id, out var expectedRow);
            actualById.TryGetValue(id, out var actualRow);
            if (expectedRow != actualRow)
                mismatches.Add($"{source} event {id}:{Environment.NewLine}  oracle: {expectedRow?.ToString() ?? "<missing>"}{Environment.NewLine}  sql:    {actualRow?.ToString() ?? "<missing>"}");
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    private static void AssertEndpointArticleMatchesOracle(JsonElement response, string sku, AssortmentSourceRow expected, DateOnly asOf)
    {
        var article = Assert.Single(response.GetProperty("articleStats").EnumerateArray(), row => row.GetProperty("sku").GetString() == sku);
        Assert.Equal((int)expected.PreQty.GetValueOrDefault(), article.GetProperty("preQty").GetInt32());
        Assert.Equal(expected.PreRevenue.GetValueOrDefault(), article.GetProperty("preRevenue").GetDecimal());
        Assert.Equal((int)expected.PostQty.GetValueOrDefault(), article.GetProperty("postQty").GetInt32());
        Assert.Equal(expected.PostRevenue.GetValueOrDefault(), article.GetProperty("postRevenue").GetDecimal());
        Assert.Equal(expected.ChangePercentRevenueSemantic, article.GetProperty("semanticChangePercentRevenue").ValueKind == JsonValueKind.Null ? null : article.GetProperty("semanticChangePercentRevenue").GetDecimal());
        Assert.Equal(expected.HasRevenueBaseline, article.GetProperty("hasRevenueBaseline").GetBoolean());
        Assert.Equal(expected.RevenueBaselineReason, article.GetProperty("revenueBaselineReason").GetString());
        Assert.Equal(expected.EventDate.AddDays(30) <= asOf, article.GetProperty("isPostWindowMature").GetBoolean());
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
              (10024, 'RQ561-NEGATIVE-ENTITY', '2026-09-12T09:00:00Z', -1, 'rq561', 'existing', 'utc_instant'),
              (10025, 'RQ561-NEGATIVE-NULL', '2026-09-12T10:00:00Z', -1, 'rq561', 'existing', 'utc_instant');

            INSERT INTO prodaja_stavke
              (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
            SELECT 10024, 10024, a."Id", 1, 125, 75, -1, -1, 'sale_snapshot'
              FROM "Artikli" a WHERE a."PLU" = 'RQ561-NEGATIVE-ENTITY';
            INSERT INTO prodaja_stavke
              (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
            SELECT 10025, 10025, a."Id", 1, 125, NULL, NULL, NULL, 'sale_snapshot'
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
        string Evidence,
        bool? CanonicalSqlExecuted = null,
        bool? OracleExecuted = null,
        bool? MissingObjectNegativePathExecuted = null,
        int? EndpointCasesExecuted = null);

    private sealed record Rq568FixtureContext(
        DateOnly AnchorDate,
        DateOnly AsOfDate,
        IReadOnlyDictionary<long, AssortmentSourceRow> OracleRows,
        IReadOnlyDictionary<long, AssortmentSourceRow> ScopedOracleRows);

    private sealed record Rq568ViewSemantics(
        string PriceDirection,
        bool PostWindowComplete,
        bool OverlapsNextEvent,
        DateOnly? NextEventDate);

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
