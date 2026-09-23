using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Deterministic")]
public sealed class OperationsAnalyticsProofPackTests
{
    [Fact]
    public void OperationsManifestCoversAllEightMenuFamiliesAndCanonicalRedirects()
    {
        var routes = PilotAnalyticsSeedPack.OperationsRoutes;

        Assert.Equal(PilotAnalyticsSeedPack.OperationsFixtureId, "operations-analytics-v1");
        Assert.Equal(8, routes.Count);
        Assert.Equal(8, routes.Select(route => route.Family).Distinct(StringComparer.Ordinal).Count());
        Assert.All(routes, route =>
        {
            Assert.StartsWith("/analytics/", route.MenuPath, StringComparison.Ordinal);
            Assert.StartsWith("/analytics/", route.CanonicalPath, StringComparison.Ordinal);
            Assert.StartsWith("/api/analytics/", route.EndpointFamily, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(route.ExistingOwner));
        });

        var supplierSales = Assert.Single(routes, route => route.Family == "supplier-sales");
        var supplierFootwear = Assert.Single(routes, route => route.Family == "supplier-footwear");
        Assert.Equal("/analytics/supplier", supplierSales.CanonicalPath);
        Assert.Equal(supplierSales.CanonicalPath, supplierFootwear.CanonicalPath);
    }

    [Fact]
    public void OperationsFixtureContainsRequiredSignedScopeAndDataQualitySignals()
    {
        var fixture = PilotAnalyticsSeedPack.OperationsFixture;
        var rows = fixture.SalesRows;

        Assert.Equal("operations-analytics-v1", fixture.FixtureId);
        Assert.Contains(rows, row => row.Quantity > 0m);
        Assert.Contains(rows, row => row.IsReturn && row.Quantity < 0m && row.Revenue < 0m);
        Assert.Contains(rows, row => row.CostRsd is null);
        Assert.Contains(rows, row => row.Supplier == "Nepoznato" && row.ShoeType == "Nepoznato" && row.Color == "Nepoznato");
        Assert.True(rows.Select(row => row.StoreId).Distinct().Count() > 1);
        Assert.Contains(rows, row => row.Shift == "off-shift");
        Assert.Contains(fixture.PriceEvents, priceEvent => priceEvent.PrePrice != priceEvent.PostPrice);
        Assert.Contains(fixture.InventoryRows, row => row.ExpectedState == "oos_risk" && row.RecommendationAllowed);
        Assert.Contains(fixture.InventoryRows, row => row.ExpectedState == "insufficient_data" && !row.RecommendationAllowed);
    }

    [Fact]
    public void OperationsFixtureReconcilesSignedTotalsAcrossAllExistingAndImportedScopes()
    {
        var fixture = PilotAnalyticsSeedPack.OperationsFixture;

        AssertScope(fixture.SalesRows, "all", fixture.Expected.AllQuantity, fixture.Expected.AllRevenue);
        AssertScope(fixture.SalesRows, "existing", fixture.Expected.ExistingQuantity, fixture.Expected.ExistingRevenue);
        AssertScope(fixture.SalesRows, "imported", fixture.Expected.ImportedQuantity, fixture.Expected.ImportedRevenue);

        var byShift = fixture.SalesRows
            .GroupBy(row => row.Shift, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (Quantity: group.Sum(row => row.Quantity), Revenue: group.Sum(row => row.Revenue)), StringComparer.Ordinal);

        Assert.Equal((fixture.Expected.MorningQuantity, fixture.Expected.MorningRevenue), byShift["morning"]);
        Assert.Equal((fixture.Expected.AfternoonQuantity, fixture.Expected.AfternoonRevenue), byShift["afternoon"]);
        Assert.Equal((fixture.Expected.OffShiftQuantity, fixture.Expected.OffShiftRevenue), byShift["off-shift"]);

        var unknownRevenue = fixture.SalesRows
            .Where(row => row.Supplier == "Nepoznato" || row.ShoeType == "Nepoznato" || row.Color == "Nepoznato")
            .Sum(row => row.Revenue);
        Assert.Equal(fixture.Expected.UnknownBucketRevenue, unknownRevenue);
    }

    [Fact]
    public void OperationsFixtureReconcilesPrePostDenominatorAndRevenueChange()
    {
        var priceEvent = Assert.Single(PilotAnalyticsSeedPack.OperationsFixture.PriceEvents);

        Assert.Equal(100m, priceEvent.PrePrice);
        Assert.Equal(110m, priceEvent.PostPrice);
        Assert.Equal(2m, priceEvent.PreQuantity);
        Assert.Equal(200m, priceEvent.PreRevenue);
        Assert.Equal(3m, priceEvent.PostQuantity);
        Assert.Equal(270m, priceEvent.PostRevenue);
        Assert.Equal(1, priceEvent.ComparableArticleCount);
        Assert.Equal(35m, Math.Round((priceEvent.PostRevenue - priceEvent.PreRevenue) / priceEvent.PreRevenue * 100m, 2));

        var expected = PilotAnalyticsSeedPack.OperationsFixture.Expected;
        Assert.Equal(expected.ComparablePreQuantity, priceEvent.PreQuantity);
        Assert.Equal(expected.ComparablePreRevenue, priceEvent.PreRevenue);
        Assert.Equal(expected.ComparablePostQuantity, priceEvent.PostQuantity);
        Assert.Equal(expected.ComparablePostRevenue, priceEvent.PostRevenue);
        Assert.Equal(expected.ComparableArticleCount, priceEvent.ComparableArticleCount);
        Assert.Equal(expected.ComparableRevenueChangePercent, Math.Round((priceEvent.PostRevenue - priceEvent.PreRevenue) / priceEvent.PreRevenue * 100m, 2));
    }

    [Fact]
    public void OperationsFixtureReconcilesDimensionDailyAndInventoryPopulations()
    {
        var fixture = PilotAnalyticsSeedPack.OperationsFixture;

        AssertDimensionTotals(fixture.SalesRows, row => row.Supplier, fixture.Expected.SupplierRevenue);
        AssertDimensionTotals(fixture.SalesRows, row => row.ShoeType, fixture.Expected.ShoeTypeRevenue);
        AssertDimensionTotals(fixture.SalesRows, row => row.Color, fixture.Expected.ColorRevenue);

        var dailyTotals = fixture.SalesRows
            .GroupBy(row => row.OccurredAtUtc.ToString("yyyy-MM-dd"), StringComparer.Ordinal)
            .Select(group => new PilotOperationsDailyExpected(group.Key, group.Sum(row => row.Quantity), group.Sum(row => row.Revenue)))
            .OrderBy(day => day.Date, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(fixture.Expected.DailyTotals, dailyTotals);

        Assert.Equal(fixture.Expected.PreNivelacijaCandidateCount, fixture.PriceEvents.Count);
        Assert.Equal(fixture.Expected.InventoryPopulationCount, fixture.InventoryRows.Count);
        Assert.Equal(fixture.Expected.InventoryAlertCount, fixture.InventoryRows.Count(row => row.ExpectedState == "oos_risk"));
        Assert.Equal(fixture.Expected.InventoryRecommendationAllowedCount, fixture.InventoryRows.Count(row => row.RecommendationAllowed));
    }

    [Fact]
    public void OperationsFixtureKeepsUnknownDimensionsAndInventoryStatesExplicit()
    {
        var fixture = PilotAnalyticsSeedPack.OperationsFixture;
        var unknown = fixture.SalesRows.Where(row => row.Supplier == "Nepoznato").ToArray();

        Assert.Single(unknown);
        Assert.Equal("Nepoznato", unknown[0].ShoeType);
        Assert.Equal("Nepoznato", unknown[0].Color);
        Assert.Equal(fixture.Expected.InventoryRiskSku, Assert.Single(fixture.InventoryRows, row => row.ExpectedState == "oos_risk").Sku);
        Assert.Equal(fixture.Expected.InventoryBlockedSku, Assert.Single(fixture.InventoryRows, row => row.ExpectedState == "insufficient_data").Sku);
    }

    private static void AssertScope(
        IReadOnlyList<PilotOperationsSalesFact> rows,
        string scope,
        decimal expectedQuantity,
        decimal expectedRevenue)
    {
        var scopedRows = scope switch
        {
            "existing" => rows.Where(row => row.DataOrigin == "existing"),
            "imported" => rows.Where(row => row.DataOrigin == "imported"),
            _ => rows
        };

        Assert.Equal(expectedQuantity, scopedRows.Sum(row => row.Quantity));
        Assert.Equal(expectedRevenue, scopedRows.Sum(row => row.Revenue));
    }

    private static void AssertDimensionTotals(
        IReadOnlyList<PilotOperationsSalesFact> rows,
        Func<PilotOperationsSalesFact, string> keySelector,
        IReadOnlyList<PilotOperationsDimensionExpected> expected)
    {
        var actual = rows
            .GroupBy(keySelector, StringComparer.Ordinal)
            .Select(group => new PilotOperationsDimensionExpected(
                group.Key,
                group.Sum(row => row.Quantity),
                group.Sum(row => row.Revenue),
                Math.Round(group.Sum(row => row.Revenue) / rows.Sum(row => row.Revenue) * 100m, 6)))
            .OrderBy(row => row.Key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.OrderBy(row => row.Key, StringComparer.Ordinal), actual);
        Assert.Equal(100m, Math.Round(actual.Sum(row => row.RevenueSharePercent), 6));
    }
}
