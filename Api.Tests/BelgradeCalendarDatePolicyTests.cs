using Application.Analytics;
using Xunit;

namespace Api.Tests;

public sealed class BelgradeCalendarDatePolicyTests
{
    [Theory]
    [InlineData(2026, 1, 15, "2026-01-14T23:00:00Z", "2026-01-15T23:00:00Z")]
    [InlineData(2026, 7, 15, "2026-07-14T22:00:00Z", "2026-07-15T22:00:00Z")]
    [InlineData(2026, 3, 29, "2026-03-28T23:00:00Z", "2026-03-29T22:00:00Z")]
    public void CalendarDayUsesBelgradeMidnightsAndExclusiveEnd(
        int year,
        int month,
        int day,
        string expectedStart,
        string expectedEnd)
    {
        var date = new DateOnly(year, month, day);

        Assert.Equal(DateTime.Parse(expectedStart).ToUniversalTime(), BelgradeCalendarDatePolicy.StartOfDateUtc(date));
        Assert.Equal(DateTime.Parse(expectedEnd).ToUniversalTime(), BelgradeCalendarDatePolicy.EndOfDateExclusiveUtc(date));
    }
}
