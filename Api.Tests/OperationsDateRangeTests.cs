using Application.Analytics;
using Xunit;

namespace Trendplus2.Tests;

public sealed class OperationsDateRangeTests
{
    [Fact]
    public void ComparableRangeUsesHalfOpenWholeDayWindows()
    {
        var currentFrom = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentToExclusive = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);

        var (previousFrom, previousToExclusive) = OperationsDateRange.BuildComparablePreviousRange(
            currentFrom,
            currentToExclusive);

        Assert.Equal(new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc), previousFrom);
        Assert.Equal(currentFrom, previousToExclusive);
        Assert.True(new DateTime(2026, 7, 7, 23, 59, 59, 1, DateTimeKind.Utc) < currentToExclusive);
    }

    [Fact]
    public void EqualBoundsHaveNoComparablePreviousRange()
    {
        var boundary = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);

        var result = OperationsDateRange.BuildComparablePreviousRange(boundary, boundary);

        Assert.Null(result.PreviousFromUtc);
        Assert.Null(result.PreviousToUtc);
    }
}
