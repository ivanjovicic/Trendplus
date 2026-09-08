using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class CachedInventoryVelocityTests
{
    [Fact]
    public void CalculateAverageDailySalesUnits_UsesActualThirtyDayWindow()
    {
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(30);

        var result = CachedAnalyticsEndpoints.CalculateAverageDailySalesUnits(10, start, end);

        Assert.Equal(0.3333m, result);
    }

    [Fact]
    public void CalculateAverageDailySalesUnits_UsesActualTenDayWindow()
    {
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(10);

        var result = CachedAnalyticsEndpoints.CalculateAverageDailySalesUnits(10, start, end);

        Assert.Equal(1.0000m, result);
    }

    [Fact]
    public void CalculateAverageDailySalesUnits_RejectsInvalidEvidence()
    {
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Null(CachedAnalyticsEndpoints.CalculateAverageDailySalesUnits(-1, start, start.AddDays(30)));
        Assert.Null(CachedAnalyticsEndpoints.CalculateAverageDailySalesUnits(10, start.AddDays(1), start));
        Assert.Null(CachedAnalyticsEndpoints.CalculateAverageDailySalesUnits(10, start, start));
    }
}
