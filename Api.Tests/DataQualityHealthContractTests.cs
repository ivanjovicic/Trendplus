using Application.Analytics;
using Infrastructure.Services;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class DataQualityHealthContractTests
{
    [Fact]
    public void ExplicitPeriodUsesHalfOpenUtcBounds()
    {
        var window = DataQualityEndpoints.ResolveHealthWindow(
            new DateTime(2026, 7, 7, 18, 30, 0, DateTimeKind.Local),
            new DateTime(2026, 8, 6, 4, 0, 0, DateTimeKind.Local),
            lookbackDays: 30);

        Assert.Equal(new DateTime(2026, 7, 7, 0, 0, 0, DateTimeKind.Utc), window.FromUtc);
        Assert.Equal(new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc), window.ToExclusiveUtc);
    }

    [Fact]
    public void ExplicitPeriodRequiresBothBoundsAndPositiveRange()
    {
        Assert.Throws<ArgumentException>(() => DataQualityEndpoints.ResolveHealthWindow(
            new DateTime(2026, 7, 7),
            null,
            lookbackDays: 30));

        Assert.Throws<ArgumentException>(() => DataQualityEndpoints.ResolveHealthWindow(
            new DateTime(2026, 8, 6),
            new DateTime(2026, 7, 7),
            lookbackDays: 30));
    }

    [Fact]
    public void OverallStatusDoesNotReportExcellentWhenMasterDataIsIncomplete()
    {
        var snapshot = new AnalyticsDataQualityHealthSnapshot
        {
            HasRevenueEvidence = true,
            MissingCategoryArticleCount = 12
        };

        Assert.Equal("warning", DataQualityEndpoints.ResolveOverallDataQualityStatus("excellent", snapshot));
        Assert.Equal("critical", DataQualityEndpoints.ResolveOverallDataQualityStatus("critical", snapshot));
    }
}
