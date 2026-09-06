using Application.Analytics;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class DataQualitySalesWindowTests
{
    [Fact]
    public void Resolve_UsesInclusiveCalendarDaysEndingTodayAsHalfOpenInterval()
    {
        var utcNow = new DateTime(2026, 9, 6, 15, 30, 0, DateTimeKind.Utc);
        var (fromUtc, toExclusiveUtc) = DataQualitySalesWindow.Resolve(30, utcNow);

        Assert.Equal(new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc), fromUtc);
        Assert.Equal(new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc), toExclusiveUtc);
    }

    [Fact]
    public void Resolve_ClampsLookbackToOneDay()
    {
        var utcNow = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        var (fromUtc, toExclusiveUtc) = DataQualitySalesWindow.Resolve(0, utcNow);

        Assert.Equal(utcNow.Date, fromUtc);
        Assert.Equal(utcNow.Date.AddDays(1), toExclusiveUtc);
    }
}
