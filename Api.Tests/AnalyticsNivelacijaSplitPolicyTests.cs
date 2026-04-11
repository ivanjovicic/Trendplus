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
            new TestRow(1, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc), 600m, 6),
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
        Assert.Contains("premala", snapshot.SignalNote ?? string.Empty);
    }

    [Fact]
    public void Build_ComputesImpactOnlyFromComparableArticles()
    {
        var rows = new[]
        {
            new TestRow(1, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), 500m, 5),
            new TestRow(1, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc), 750m, 6),
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
        Assert.Equal(50d, snapshot.RevenueImpactPct);
        Assert.Equal(20d, snapshot.UnitsImpactPct);
        Assert.Null(snapshot.SignalNote);
    }
}
