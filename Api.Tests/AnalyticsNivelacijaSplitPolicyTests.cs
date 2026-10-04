using Application.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsNivelacijaSplitPolicyTests
{
    private sealed record TestRow(int ArtikalId, DateTime DatumProdaje, decimal Prihod, int Kolicina);

    [Fact]
    public void Build_ReturnsLowSignal_WhenComparablePreBaselineIsTooSmall()
    {
        var rows = new[]
        {
            new TestRow(1, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 100m, 1),
            new TestRow(1, new DateTime(2026, 1, 19, 0, 0, 0, DateTimeKind.Utc), 600m, 6),
            new TestRow(2, new DateTime(2026, 1, 22, 0, 0, 0, DateTimeKind.Utc), 400m, 4)
        };

        var snapshot = AnalyticsNivelacijaSplitPolicy.Build(
            rows,
            new Dictionary<int, DateTime>
            {
                [1] = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                [2] = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina);

        Assert.Equal(100m, snapshot.PreRevenue);
        Assert.Equal(1, snapshot.PreQuantity);
        Assert.Equal(1, snapshot.ComparableArticleCount);
        Assert.Null(snapshot.RevenueImpactPct);
        Assert.Null(snapshot.UnitsImpactPct);
        Assert.False(snapshot.HasComparableSignal);
        Assert.Contains("premala", snapshot.SignalNote ?? string.Empty);
    }

    [Fact]
    public void Build_ComputesImpactOnlyFromComparableArticles()
    {
        var rows = new[]
        {
            new TestRow(1, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 500m, 5),
            new TestRow(1, new DateTime(2026, 1, 19, 0, 0, 0, DateTimeKind.Utc), 750m, 6),
            new TestRow(2, new DateTime(2026, 1, 21, 0, 0, 0, DateTimeKind.Utc), 300m, 3)
        };

        var snapshot = AnalyticsNivelacijaSplitPolicy.Build(
            rows,
            new Dictionary<int, DateTime>
            {
                [1] = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                [2] = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina);

        Assert.Equal(500m, snapshot.PreRevenue);
        Assert.Equal(1_050m, snapshot.PostRevenue);
        Assert.Equal(1_550m, snapshot.RevenueWithSplit);
        Assert.Equal(2, snapshot.ArticleCountWithNivelacija);
        Assert.Equal(1, snapshot.ComparableArticleCount);
        Assert.Equal(1_250m, snapshot.ComparableRevenueWithSplit);
        Assert.Equal(500m, snapshot.ComparablePreRevenue);
        Assert.Equal(750m, snapshot.ComparablePostRevenue);
        Assert.Equal(5, snapshot.ComparablePreQuantity);
        Assert.Equal(6, snapshot.ComparablePostQuantity);
        Assert.Equal(50d, snapshot.RevenueImpactPct);
        Assert.Equal(20d, snapshot.UnitsImpactPct);
        Assert.True(snapshot.HasComparableSignal);
        Assert.Null(snapshot.SignalNote);
    }

    [Fact]
    public void Build_DistinguishesEmptyOrMissingEventFromAValidZeroImpact()
    {
        var rows = new[]
        {
            new TestRow(1, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 500m, 5),
            new TestRow(1, new DateTime(2026, 1, 19, 0, 0, 0, DateTimeKind.Utc), 500m, 5)
        };

        var noEvent = AnalyticsNivelacijaSplitPolicy.Build(
            rows,
            new Dictionary<int, DateTime>(),
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina);

        var validZero = AnalyticsNivelacijaSplitPolicy.Build(
            rows,
            new Dictionary<int, DateTime>
            {
                [1] = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina);

        Assert.False(noEvent.HasComparableSignal);
        Assert.Null(noEvent.RevenueImpactPct);
        Assert.True(validZero.HasComparableSignal);
        Assert.Equal(0d, validZero.RevenueImpactPct);
        Assert.Equal(0d, validZero.UnitsImpactPct);
    }

    [Fact]
    public void EvaluateComparableSignal_IgnoresOneSidedRowsInAggregateImpact()
    {
        var signal = AnalyticsNivelacijaSplitPolicy.EvaluateComparableSignal(
            comparablePreRevenue: 500m,
            comparablePostRevenue: 750m,
            comparablePreQuantity: 5,
            comparablePostQuantity: 6,
            comparableArticleCount: 1,
            totalRevenue: 1_550m);

        Assert.Equal(50d, signal.RevenueImpactPct);
        Assert.Equal(20d, signal.UnitsImpactPct);
        Assert.Null(signal.SignalNote);
    }

    [Fact]
    public void EvaluateComparableSignal_BlocksAggregateImpactWhenCohortIsInsufficient()
    {
        var signal = AnalyticsNivelacijaSplitPolicy.EvaluateComparableSignal(
            comparablePreRevenue: 100m,
            comparablePostRevenue: 600m,
            comparablePreQuantity: 1,
            comparablePostQuantity: 6,
            comparableArticleCount: 1,
            totalRevenue: 1_000m);

        Assert.Null(signal.RevenueImpactPct);
        Assert.Null(signal.UnitsImpactPct);
        Assert.Contains("premala", signal.SignalNote ?? string.Empty);
    }

    [Fact]
    public void AggregateComparableSignal_ExcludesOneSidedSupplierActivity()
    {
        var comparableSupplier = AnalyticsNivelacijaSplitPolicy.Build(
            new[]
            {
                new TestRow(1, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 500m, 5),
                new TestRow(1, new DateTime(2026, 1, 19, 0, 0, 0, DateTimeKind.Utc), 750m, 6)
            },
            new Dictionary<int, DateTime>
            {
                [1] = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina);
        var oneSidedSupplier = AnalyticsNivelacijaSplitPolicy.Build(
            new[]
            {
                new TestRow(2, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 300m, 3),
                new TestRow(3, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc), 400m, 4)
            },
            new Dictionary<int, DateTime>
            {
                [2] = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                [3] = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina);

        var signal = AnalyticsNivelacijaSplitPolicy.EvaluateComparableSignal(
            comparableSupplier.ComparablePreRevenue + oneSidedSupplier.ComparablePreRevenue,
            comparableSupplier.ComparablePostRevenue + oneSidedSupplier.ComparablePostRevenue,
            comparableSupplier.ComparablePreQuantity + oneSidedSupplier.ComparablePreQuantity,
            comparableSupplier.ComparablePostQuantity + oneSidedSupplier.ComparablePostQuantity,
            comparableSupplier.ComparableArticleCount + oneSidedSupplier.ComparableArticleCount,
            comparableSupplier.RevenueWithSplit + oneSidedSupplier.RevenueWithSplit);

        Assert.Equal(800m, comparableSupplier.PreRevenue + oneSidedSupplier.PreRevenue);
        Assert.Equal(1_150m, comparableSupplier.PostRevenue + oneSidedSupplier.PostRevenue);
        Assert.Equal(500m, comparableSupplier.ComparablePreRevenue + oneSidedSupplier.ComparablePreRevenue);
        Assert.Equal(750m, comparableSupplier.ComparablePostRevenue + oneSidedSupplier.ComparablePostRevenue);
        Assert.Equal(50d, signal.RevenueImpactPct);
        Assert.Equal(20d, signal.UnitsImpactPct);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(25)]
    public void Build_EqualizesObservedWindowsForFlatDailySales(int eventDayOffset)
    {
        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = Enumerable.Range(0, 30)
            .Select(day => new TestRow(1, periodStart.AddDays(day), 1m, 10))
            .ToArray();

        var snapshot = AnalyticsNivelacijaSplitPolicy.Build(
            rows,
            new Dictionary<int, DateTime> { [1] = periodStart.AddDays(eventDayOffset) },
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina);

        Assert.Equal(eventDayOffset, snapshot.PreRevenue);
        Assert.Equal(30m - eventDayOffset, snapshot.PostRevenue);
        Assert.Equal(0d, snapshot.RevenueImpactPct);
        Assert.Equal(0d, snapshot.UnitsImpactPct);
        Assert.Equal(Math.Min(eventDayOffset, 30 - eventDayOffset), snapshot.ComparablePreQuantity / 10);
        Assert.Equal(snapshot.ComparablePreQuantity, snapshot.ComparablePostQuantity);
        Assert.True(snapshot.HasComparableSignal);
    }

    [Fact]
    public void Build_CapsEqualObservedWindowsAtThirtyDays()
    {
        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = Enumerable.Range(0, 75)
            .Select(day => new TestRow(1, periodStart.AddDays(day), 1m, 1))
            .ToArray();

        var snapshot = AnalyticsNivelacijaSplitPolicy.Build(
            rows,
            new Dictionary<int, DateTime> { [1] = periodStart.AddDays(35) },
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina,
            periodStart,
            periodStart.AddDays(75));

        Assert.Equal(0d, snapshot.RevenueImpactPct);
        Assert.Equal(30, snapshot.ComparablePreQuantity);
        Assert.Equal(30, snapshot.ComparablePostQuantity);
    }

    [Fact]
    public void Build_EventBeforePeriodStartHasNoPreEvidenceAndDoesNotInventAnImpact()
    {
        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = Enumerable.Range(0, 30)
            .Select(day => new TestRow(1, periodStart.AddDays(day), 1m, 10))
            .ToArray();

        var snapshot = AnalyticsNivelacijaSplitPolicy.Build(
            rows,
            new Dictionary<int, DateTime> { [1] = periodStart.AddDays(-1) },
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina);

        Assert.Equal(0m, snapshot.PreRevenue);
        Assert.Equal(30m, snapshot.PostRevenue);
        Assert.Equal(0, snapshot.ComparableArticleCount);
        Assert.Null(snapshot.RevenueImpactPct);
        Assert.Null(snapshot.UnitsImpactPct);
        Assert.False(snapshot.HasComparableSignal);
        Assert.Contains("pre izabranog perioda", snapshot.SignalNote ?? string.Empty);
    }

    [Fact]
    public void Build_StoreScopedAndChainWideEventsSelectTheCorrectFirstEvent()
    {
        var periodStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var rows = Enumerable.Range(0, 30)
            .Select(day => new TestRow(1, periodStart.AddDays(day), 1m, 10))
            .ToArray();
        var events = new[]
        {
            new Domain.Model.DnevnikPromena { Id = 1, ArtikalId = 1, IDObjekat = null, Datum = periodStart.AddDays(20) },
            new Domain.Model.DnevnikPromena { Id = 2, ArtikalId = 1, IDObjekat = 7, Datum = periodStart.AddDays(10) },
            new Domain.Model.DnevnikPromena { Id = 3, ArtikalId = 1, IDObjekat = 8, Datum = periodStart.AddDays(1) }
        }.AsQueryable();

        DateTime? SplitForStore(int storeId)
        {
            var selected = Application.Analytics.NivelacijaEventScopePolicy.ApplyStoreScope(events, storeId)
                .Where(e => e.ArtikalId == 1)
                .Min(e => (DateTime?)e.Datum);
            return selected;
        }

        var storeSevenEvent = SplitForStore(7);
        var storeNineEvent = SplitForStore(9);
        Assert.Equal(periodStart.AddDays(10), storeSevenEvent);
        Assert.Equal(periodStart.AddDays(20), storeNineEvent);

        var storeEventOnly = new[]
        {
            new Domain.Model.DnevnikPromena { Id = 4, ArtikalId = 1, IDObjekat = 7, Datum = periodStart.AddDays(15) }
        }.AsQueryable();
        Assert.Equal(
            periodStart.AddDays(15),
            Application.Analytics.NivelacijaEventScopePolicy.ApplyStoreScope(storeEventOnly, 7).Min(e => e.Datum));
        Assert.Null(Application.Analytics.NivelacijaEventScopePolicy.ApplyStoreScope(storeEventOnly, 9)
            .Select(e => (DateTime?)e.Datum).Min());

        var chainWideSnapshot = AnalyticsNivelacijaSplitPolicy.Build(
            rows,
            new Dictionary<int, DateTime> { [1] = storeNineEvent!.Value },
            row => row.ArtikalId,
            row => row.DatumProdaje,
            row => row.Prihod,
            row => row.Kolicina);
        Assert.Equal(0d, chainWideSnapshot.RevenueImpactPct);
    }
}
