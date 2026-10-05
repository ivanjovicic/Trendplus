using Application.Analytics;
using Api.Services;
using Trendplus2.Tests.Analytics;
using Xunit;

namespace Trendplus2.Tests;

/// <summary>
/// Adversarial metamorphic + boundary proofs for certified retail promet.
/// Expected values are hand-derived from <see cref="CertifiedRetailLineOracle.CreateCanonicalKit"/>;
/// they do not call Analytics endpoints (avoids FE+BE co-failure staying green).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Suite", "AnalyticsAdversarial")]
public sealed class AnalyticsCertifiedRetailMetamorphicTests
{
    private static readonly DateTime DayA = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DayB = DayA.AddDays(1);
    private static readonly DateTime DayC = DayA.AddDays(2);

    private static CertifiedRetailLineOracle.Filters Window(DateTime from, DateTime toExclusive, int? storeId = null, string scope = "all")
        => new(from, toExclusive, storeId, scope);

    [Fact(DisplayName = "Canonical kit: hand expected totals for [2026-09-01, 2026-09-02)")]
    public void CanonicalKit_HandExpected_AllStores_CurrentDay()
    {
        // Formula: sum(qty * price) over half-open window, excluding DUG/KOREKCIJA.
        // Included: 200 + 80 + (-100) + 80 + 240 + 50 = 550; units 2+1-1+1+2+1 = 6.
        var totals = CertifiedRetailLineOracle.QueryTotals(
            CertifiedRetailLineOracle.CreateCanonicalKit(),
            Window(DayA, DayB));

        Assert.Equal(6, totals.LineCount);
        Assert.Equal(6, totals.Units);
        Assert.Equal(550m, totals.Revenue);
    }

    [Fact(DisplayName = "Metamorphic: [A,C) revenue = [A,B) + [B,C)")]
    public void PeriodAdditivity_AdjacentHalfOpenWindows()
    {
        var kit = CertifiedRetailLineOracle.CreateCanonicalKit();
        var ab = CertifiedRetailLineOracle.QueryTotals(kit, Window(DayA, DayB));
        var bc = CertifiedRetailLineOracle.QueryTotals(kit, Window(DayB, DayC));
        var ac = CertifiedRetailLineOracle.QueryTotals(kit, Window(DayA, DayC));

        // R-TOEXCL is sold exactly at DayB: excluded from [A,B), included in [B,C).
        // [B,C) = 9*999 (R-TOEXCL) + 120 (R-NEXT) = 9111.
        Assert.Equal(550m, ab.Revenue);
        Assert.Equal(9111m, bc.Revenue);
        Assert.Equal(ab.Revenue + bc.Revenue, ac.Revenue);
        Assert.Equal(ab.Units + bc.Units, ac.Units);
        Assert.Equal(ab.LineCount + bc.LineCount, ac.LineCount);
    }

    [Fact(DisplayName = "Metamorphic: +100 RSD sale increases promet exactly by 100")]
    public void AddingOneHundredRsdSale_BumpsRevenueByExactlyOneHundred()
    {
        var kit = CertifiedRetailLineOracle.CreateCanonicalKit().ToList();
        var before = CertifiedRetailLineOracle.QueryTotals(kit, Window(DayA, DayB));

        kit.Add(new CertifiedRetailLineOracle.SaleLine(
            DayA.AddHours(18), 1, "R-PLUS-100", "existing", 1, 100m, 40m, 1, 1, "CRNA"));

        var after = CertifiedRetailLineOracle.QueryTotals(kit, Window(DayA, DayB));
        Assert.Equal(before.Revenue + 100m, after.Revenue);
        Assert.Equal(before.Units + 1, after.Units);
        Assert.Equal(before.LineCount + 1, after.LineCount);
    }

    [Fact(DisplayName = "Metamorphic: sale in other store does not change selected store")]
    public void OtherStoreSale_DoesNotChangeSelectedStoreTotals()
    {
        var kit = CertifiedRetailLineOracle.CreateCanonicalKit().ToList();
        var store1Before = CertifiedRetailLineOracle.QueryTotals(kit, Window(DayA, DayB, storeId: 1));

        kit.Add(new CertifiedRetailLineOracle.SaleLine(
            DayA.AddHours(19), 2, "R-OTHER-STORE", "existing", 7, 333m, 10m, 3, 2, "PLAVA"));

        var store1After = CertifiedRetailLineOracle.QueryTotals(kit, Window(DayA, DayB, storeId: 1));
        var store2 = CertifiedRetailLineOracle.QueryTotals(kit, Window(DayA, DayB, storeId: 2));

        Assert.Equal(store1Before.Revenue, store1After.Revenue);
        Assert.Equal(store1Before.Units, store1After.Units);
        Assert.Equal(240m + (7 * 333m), store2.Revenue);
    }

    [Fact(DisplayName = "Metamorphic: full return of a 100 RSD unit nets that unit's revenue to zero")]
    public void FullReturn_NetsMatchingUnitRevenueToZero()
    {
        // Pair: +1 @ 100 and -1 @ 100 in the same window → net revenue 0 for the pair.
        var lines = new[]
        {
            new CertifiedRetailLineOracle.SaleLine(DayA.AddHours(9), 1, "SALE", "existing", 1, 100m, 50m, 1, 1, "CRNA"),
            new CertifiedRetailLineOracle.SaleLine(DayA.AddHours(10), 1, "RETURN", "existing", -1, 100m, 50m, 1, 1, "CRNA"),
        };

        var totals = CertifiedRetailLineOracle.QueryTotals(lines, Window(DayA, DayB));
        Assert.Equal(0, totals.Units);
        Assert.Equal(0m, totals.Revenue);
        Assert.Equal(2, totals.LineCount);
    }

    [Fact(DisplayName = "Boundary: soldAt == from inclusive; soldAt == toExclusive exclusive")]
    public void HalfOpenBoundary_IncludesFrom_ExcludesToExclusive()
    {
        var kit = CertifiedRetailLineOracle.CreateCanonicalKit();
        var totals = CertifiedRetailLineOracle.QueryTotals(kit, Window(DayA, DayB, storeId: 1));

        // Store 1 without store2 (240): 550 - 240 = 310; includes R-FROM (50), excludes R-TOEXCL (999*9).
        Assert.Equal(310m, totals.Revenue);
        Assert.DoesNotContain(
            CertifiedRetailLineOracle.Filter(kit, Window(DayA, DayB)),
            line => line.ReceiptNumber == "R-TOEXCL");
        Assert.Contains(
            CertifiedRetailLineOracle.Filter(kit, Window(DayA, DayB)),
            line => line.ReceiptNumber == "R-FROM");
    }

    [Fact(DisplayName = "DUG/KOREKCIJA (trimmed, mixed case) are not retail promet")]
    public void DugAndKorekcija_AreExcludedFromCertifiedRetail()
    {
        Assert.True(SalesReceiptPopulationPolicy.IsExcluded("  dUg  "));
        Assert.True(SalesReceiptPopulationPolicy.IsExcluded(" KoReKcIjA "));

        var kit = CertifiedRetailLineOracle.CreateCanonicalKit();
        var matched = CertifiedRetailLineOracle.Filter(kit, Window(DayA, DayB)).ToArray();
        Assert.DoesNotContain(matched, line => SalesReceiptPopulationPolicy.IsExcluded(line.ReceiptNumber));
        // Excluded documents would have added 5*200 + 3*150 = 1450 if wrongly included.
        Assert.Equal(550m, CertifiedRetailLineOracle.QueryTotals(kit, Window(DayA, DayB)).Revenue);
    }

    [Fact(DisplayName = "dataScope imported vs existing isolates header origin")]
    public void DataScope_IsolatesImportedVersusExistingHeaders()
    {
        var kit = CertifiedRetailLineOracle.CreateCanonicalKit();
        var imported = CertifiedRetailLineOracle.QueryTotals(kit, Window(DayA, DayB, scope: "imported"));
        var existing = CertifiedRetailLineOracle.QueryTotals(kit, Window(DayA, DayB, scope: "existing"));

        Assert.Equal(240m, imported.Revenue); // only store2 access row
        Assert.Equal(310m, existing.Revenue); // all-store kit minus imported
        Assert.Equal(550m, imported.Revenue + existing.Revenue);
    }

    [Fact(DisplayName = "Cross-dimension buckets on same filtered population sum to the same promet")]
    public void SupplierShoeTypeColorBuckets_SumToSameRevenue()
    {
        var kit = CertifiedRetailLineOracle.CreateCanonicalKit();
        var filters = Window(DayA, DayB);
        var totals = CertifiedRetailLineOracle.QueryTotals(kit, filters);

        var suppliers = CertifiedRetailLineOracle.QueryBuckets(
            kit, filters, line => line.SupplierIdAtSale?.ToString() ?? "unknown");
        var shoeTypes = CertifiedRetailLineOracle.QueryBuckets(
            kit, filters, line => line.ShoeTypeIdAtSale?.ToString() ?? "unknown");
        var colors = CertifiedRetailLineOracle.QueryBuckets(
            kit, filters, line => string.IsNullOrWhiteSpace(line.ColorKey) ? "unknown" : line.ColorKey!);

        Assert.Equal(totals.Revenue, suppliers.Sum(b => b.Revenue));
        Assert.Equal(totals.Revenue, shoeTypes.Sum(b => b.Revenue));
        Assert.Equal(totals.Revenue, colors.Sum(b => b.Revenue));
        Assert.Equal(totals.Units, suppliers.Sum(b => b.Units));
        Assert.Equal(totals.Units, shoeTypes.Sum(b => b.Units));
        Assert.Equal(totals.Units, colors.Sum(b => b.Units));
    }

    [Fact(DisplayName = "Previous-only entity does not appear in current window buckets")]
    public void PreviousOnlyEntity_AbsentFromCurrentWindow()
    {
        var kit = CertifiedRetailLineOracle.CreateCanonicalKit();
        var current = CertifiedRetailLineOracle.QueryBuckets(
            kit, Window(DayA, DayB), line => line.ShoeTypeIdAtSale?.ToString() ?? "unknown");
        var previous = CertifiedRetailLineOracle.QueryBuckets(
            kit, Window(DayA.AddDays(-1), DayA), line => line.ShoeTypeIdAtSale?.ToString() ?? "unknown");

        Assert.DoesNotContain(current, bucket => bucket.Key == "5");
        Assert.Contains(previous, bucket => bucket.Key == "5" && bucket.Revenue == 180m);
    }

    [Fact(DisplayName = "Null cost is not treated as zero cost margin (MarginAccumulator)")]
    public void MissingCost_IsNotFakeFullMargin()
    {
        var accumulator = new MarginAccumulator();
        // Same shape as the weak legacy unit test that claimed null cost => margin == revenue.
        accumulator.Add(revenue: 100m, quantity: 1m, unitCost: null);
        var snapshot = accumulator.Build(totalRevenue: 100m);

        Assert.Equal(0m, snapshot.RevenueWithCost);
        Assert.Equal(0m, snapshot.MarginContribution);
        Assert.Equal(0d, snapshot.MarginPct);
        Assert.Equal(0d, snapshot.MarginDataCoveragePct);
        Assert.Equal(100m, AnalyticsMarginPolicy.ResolveNoCostRevenue(100m, snapshot.RevenueWithCost));
    }

    [Fact(DisplayName = "Zero vs null quantity/revenue: measured zero stays zero; empty window stays empty")]
    public void NullVersusZero_EmptyWindowDistinctFromMeasuredZero()
    {
        var empty = CertifiedRetailLineOracle.QueryTotals(
            Array.Empty<CertifiedRetailLineOracle.SaleLine>(),
            Window(DayA, DayB));
        Assert.Equal(0, empty.LineCount);
        Assert.Equal(0, empty.Units);
        Assert.Equal(0m, empty.Revenue);

        var measuredZero = CertifiedRetailLineOracle.QueryTotals(
            [
                new CertifiedRetailLineOracle.SaleLine(DayA.AddHours(1), 1, "Z", "existing", 1, 100m, 50m),
                new CertifiedRetailLineOracle.SaleLine(DayA.AddHours(2), 1, "Z-RET", "existing", -1, 100m, 50m),
            ],
            Window(DayA, DayB));
        Assert.Equal(2, measuredZero.LineCount);
        Assert.Equal(0, measuredZero.Units);
        Assert.Equal(0m, measuredZero.Revenue);
    }

    [Fact(DisplayName = "Negative margin line remains in promet; cost coverage still tracks reliable cost")]
    public void NegativeMarginLine_StaysInRevenue_AndCostIsTracked()
    {
        var kit = CertifiedRetailLineOracle.CreateCanonicalKit();
        var negMargin = Assert.Single(
            CertifiedRetailLineOracle.Filter(kit, Window(DayA, DayB)),
            line => line.ReceiptNumber == "R-103");
        Assert.Equal(80m, negMargin.Quantity * negMargin.UnitPrice);
        Assert.True(negMargin.UnitCost > negMargin.UnitPrice);

        var accumulator = new MarginAccumulator();
        accumulator.Add(negMargin.Quantity * negMargin.UnitPrice, negMargin.Quantity, negMargin.UnitCost);
        var snapshot = accumulator.Build(totalRevenue: 80m);
        Assert.Equal(80m, snapshot.RevenueWithCost);
        Assert.Equal(-20m, snapshot.MarginContribution);
        Assert.Equal(-25d, snapshot.MarginPct);
    }

    [Fact(DisplayName = "SupplierSharePolicy: ratio unavailable for non-positive revenue (not a fake 0%)")]
    public void SharePolicy_NonPositiveRevenue_IsUnavailableNotZeroPercent()
    {
        var evidence = SupplierSharePolicy.Resolve(revenue: -50m, denominator: 500m);
        Assert.False(evidence.IsAvailable);
        Assert.Null(evidence.SharePct);
        Assert.Equal(SupplierSharePolicy.NonPositiveSupplierState, evidence.State);
    }

    [Fact(DisplayName = "Extreme decimals: tiny and huge lines stay exact under decimal arithmetic")]
    public void ExtremeDecimals_RemainExact()
    {
        var lines = new[]
        {
            new CertifiedRetailLineOracle.SaleLine(DayA, 1, "TINY", "existing", 1, 0.01m, 0.01m),
            new CertifiedRetailLineOracle.SaleLine(DayA.AddHours(1), 1, "HUGE", "existing", 1, 999_999_999.99m, 1m),
        };
        var totals = CertifiedRetailLineOracle.QueryTotals(lines, Window(DayA, DayB));
        Assert.Equal(1_000_000_000.00m, totals.Revenue);
    }
}
