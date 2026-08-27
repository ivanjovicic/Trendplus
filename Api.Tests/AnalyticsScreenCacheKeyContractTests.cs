using Infrastructure.Services.Caching;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class AnalyticsScreenCacheKeyContractTests
{
    private static readonly DateTime FromUtc = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ToUtc = new(2026, 6, 30, 23, 59, 0, DateTimeKind.Utc);

    [Fact]
    public void ProductDecisionCenter_EveryBusinessDimensionChangesCacheIdentity()
    {
        var baseline = AnalyticsCacheKeys.ProductDecisionCenter(FromUtc, ToUtc, storeId: 1, supplierId: 2, top: 100, dataScope: "existing");

        var variants = new[]
        {
            AnalyticsCacheKeys.ProductDecisionCenter(FromUtc.AddDays(1), ToUtc, 1, 2, 100, "existing"),
            AnalyticsCacheKeys.ProductDecisionCenter(FromUtc, ToUtc.AddDays(1), 1, 2, 100, "existing"),
            AnalyticsCacheKeys.ProductDecisionCenter(FromUtc, ToUtc, 9, 2, 100, "existing"),
            AnalyticsCacheKeys.ProductDecisionCenter(FromUtc, ToUtc, 1, 9, 100, "existing"),
            AnalyticsCacheKeys.ProductDecisionCenter(FromUtc, ToUtc, 1, 2, 500, "existing"),
            AnalyticsCacheKeys.ProductDecisionCenter(FromUtc, ToUtc, 1, 2, 100, "imported")
        };

        Assert.All(variants, key => Assert.NotEqual(baseline, key));
        Assert.Equal(variants.Length, variants.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void SupplierDecisionHubSummary_NormalizesTextAndScopeWithoutDroppingDimensions()
    {
        var normalized = AnalyticsCacheKeys.SupplierDecisionHubSummary(
            FromUtc,
            ToUtc,
            category: " Patike ",
            gender: " Ženski ",
            seasonId: 3,
            minRevenue: 1000.50m,
            onlyHighConfidence: true,
            excludeOosBeforeMarkdown: true,
            supplierId: 7,
            storeId: 4,
            dataScope: " Existing ");
        var equivalent = AnalyticsCacheKeys.SupplierDecisionHubSummary(
            FromUtc,
            ToUtc,
            category: "patike",
            gender: "ženski",
            seasonId: 3,
            minRevenue: 1000.50m,
            onlyHighConfidence: true,
            excludeOosBeforeMarkdown: true,
            supplierId: 7,
            storeId: 4,
            dataScope: "existing");

        Assert.Equal(normalized, equivalent);
        Assert.Contains("supplier:7", normalized, StringComparison.Ordinal);
        Assert.Contains("store:4", normalized, StringComparison.Ordinal);
        Assert.Contains("scope:existing", normalized, StringComparison.Ordinal);
        Assert.Contains("high-confidence:True", normalized, StringComparison.Ordinal);
        Assert.Contains("exclude-oos:True", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void SupplierDecisionHubRanking_PagingAndSortingCannotShareCacheEntry()
    {
        var pageOne = RankingKey(page: 1, pageSize: 25, sortBy: "revenue", sortDir: "desc");
        var pageTwo = RankingKey(page: 2, pageSize: 25, sortBy: "revenue", sortDir: "desc");
        var largerPage = RankingKey(page: 1, pageSize: 50, sortBy: "revenue", sortDir: "desc");
        var differentSort = RankingKey(page: 1, pageSize: 25, sortBy: "quality", sortDir: "asc");

        Assert.Equal(4, new[] { pageOne, pageTwo, largerPage, differentSort }.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void InventoryStoreComparison_NormalizesStoreOrderDuplicatesAndInvalidIds()
    {
        var first = AnalyticsCacheKeys.InventoryStoreComparison(
            compareStoreIds: [3, 1, 3, -1, 2, 0],
            supplierId: 8,
            search: "Model A",
            dataScope: "existing");
        var equivalent = AnalyticsCacheKeys.InventoryStoreComparison(
            compareStoreIds: [2, 3, 1],
            supplierId: 8,
            search: " model a ",
            dataScope: "EXISTING");

        Assert.Equal(first, equivalent);
        Assert.Contains("stores:1,2,3", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Model A", first, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryInsights_SearchAndSortAreHashedAndIsolated()
    {
        var baseline = AnalyticsCacheKeys.InventoryInsights(1, 2, "Crna čizma 42", "vrednost", "all");
        var sameNormalized = AnalyticsCacheKeys.InventoryInsights(1, 2, " crna čizma 42 ", "VREDNOST", "ALL");
        var differentSearch = AnalyticsCacheKeys.InventoryInsights(1, 2, "Crna čizma 43", "vrednost", "all");
        var differentSort = AnalyticsCacheKeys.InventoryInsights(1, 2, "Crna čizma 42", "kolicina", "all");

        Assert.Equal(baseline, sameNormalized);
        Assert.NotEqual(baseline, differentSearch);
        Assert.NotEqual(baseline, differentSort);
        Assert.DoesNotContain("Crna", baseline, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("čizma", baseline, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportCacheVersion_InvalidatesSupplierAndPilotReports()
    {
        var supplierV1 = SupplierReportKey(reportCacheVersion: 1);
        var supplierV2 = SupplierReportKey(reportCacheVersion: 2);
        var supplierZero = SupplierReportKey(reportCacheVersion: 0);
        var pilotV1 = AnalyticsCacheKeys.PilotIntakeReport(FromUtc, ToUtc, 1, 2, "existing", reportCacheVersion: 1);
        var pilotV2 = AnalyticsCacheKeys.PilotIntakeReport(FromUtc, ToUtc, 1, 2, "existing", reportCacheVersion: 2);
        var pilotNegative = AnalyticsCacheKeys.PilotIntakeReport(FromUtc, ToUtc, 1, 2, "existing", reportCacheVersion: -5);

        Assert.NotEqual(supplierV1, supplierV2);
        Assert.Equal(supplierV1, supplierZero);
        Assert.NotEqual(pilotV1, pilotV2);
        Assert.Equal(pilotV1, pilotNegative);
        Assert.Contains("rv:2", supplierV2, StringComparison.Ordinal);
        Assert.Contains("rv:2", pilotV2, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownDataScopeNormalizesToAllAcrossCriticalScreens()
    {
        Assert.Equal(
            AnalyticsCacheKeys.SalesSummary(FromUtc, ToUtc, 1, 2, "all"),
            AnalyticsCacheKeys.SalesSummary(FromUtc, ToUtc, 1, 2, "unknown"));
        Assert.Equal(
            AnalyticsCacheKeys.ProductDecisionCenter(FromUtc, ToUtc, 1, 2, 100, "all"),
            AnalyticsCacheKeys.ProductDecisionCenter(FromUtc, ToUtc, 1, 2, 100, "unknown"));
        Assert.Equal(
            AnalyticsCacheKeys.InventoryInsights(1, 2, null, null, "all"),
            AnalyticsCacheKeys.InventoryInsights(1, 2, null, null, "unknown"));
        Assert.Equal(
            AnalyticsCacheKeys.ValidationLostSales("all"),
            AnalyticsCacheKeys.ValidationLostSales("unknown"));
    }

    [Fact]
    public void ValidationLostSales_SeparatesImportedAndExistingCacheEntries()
    {
        var all = AnalyticsCacheKeys.ValidationLostSales("all");
        var imported = AnalyticsCacheKeys.ValidationLostSales("imported");
        var existing = AnalyticsCacheKeys.ValidationLostSales("existing");

        Assert.NotEqual(all, imported);
        Assert.NotEqual(all, existing);
        Assert.NotEqual(imported, existing);
        Assert.Contains("scope:all", all, StringComparison.Ordinal);
        Assert.Contains("scope:imported", imported, StringComparison.Ordinal);
        Assert.Contains("scope:existing", existing, StringComparison.Ordinal);
    }

    [Fact]
    public void SafeKeyFingerprint_IsStableShortAndDoesNotExposeRawKey()
    {
        const string key = "analytics:supplier-decision-hub:ranking:secret-filter-value";

        var first = AnalyticsCacheKeys.SafeKeyFingerprint(key);
        var second = AnalyticsCacheKeys.SafeKeyFingerprint($" {key} ");

        Assert.Equal(first, second);
        Assert.Equal(12, first.Length);
        Assert.DoesNotContain("secret", first, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("^[0-9a-f]{12}$", first);
        Assert.Equal("empty", AnalyticsCacheKeys.SafeKeyFingerprint("   "));
    }

    private static string RankingKey(int page, int pageSize, string sortBy, string sortDir) =>
        AnalyticsCacheKeys.SupplierDecisionHubRanking(
            FromUtc,
            ToUtc,
            category: null,
            gender: null,
            seasonId: null,
            minRevenue: null,
            onlyHighConfidence: false,
            excludeOosBeforeMarkdown: false,
            supplierId: null,
            storeId: null,
            dataScope: "all",
            page: page,
            pageSize: pageSize,
            sortBy: sortBy,
            sortDir: sortDir);

    private static string SupplierReportKey(int reportCacheVersion) =>
        AnalyticsCacheKeys.SupplierDecisionReport(
            FromUtc,
            ToUtc,
            category: null,
            gender: null,
            seasonId: null,
            minRevenue: null,
            onlyHighConfidence: false,
            excludeOosBeforeMarkdown: false,
            supplierId: 2,
            storeId: 1,
            dataScope: "existing",
            reportCacheVersion: reportCacheVersion);
}
