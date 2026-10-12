using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsIntelligenceFreshnessDateTests
{
    [Fact]
    public void UtcAsOfGate_RejectsYesterdayAfterRolloverAndPreservesCoveredHistory()
    {
        var beforeMidnightUtc = DateOnly.FromDateTime(new DateTime(2026, 10, 12, 23, 59, 0, DateTimeKind.Utc));
        var afterMidnightUtc = DateOnly.FromDateTime(new DateTime(2026, 10, 13, 0, 1, 0, DateTimeKind.Utc));

        Assert.True(AnalyticsIntelligenceEndpoints.IsCacheFreshForRequestedAsOfDate(
            beforeMidnightUtc,
            beforeMidnightUtc));
        Assert.False(AnalyticsIntelligenceEndpoints.IsCacheFreshForRequestedAsOfDate(
            beforeMidnightUtc,
            afterMidnightUtc));
        Assert.True(AnalyticsIntelligenceEndpoints.IsCacheFreshForRequestedAsOfDate(
            beforeMidnightUtc,
            beforeMidnightUtc.AddDays(-1)));
        Assert.False(AnalyticsIntelligenceEndpoints.IsCacheFreshForRequestedAsOfDate(
            null,
            beforeMidnightUtc));
    }
}
