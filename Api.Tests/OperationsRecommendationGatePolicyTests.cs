using Application.Analytics;
using Xunit;

namespace Trendplus2.Tests;

[Trait("Category", "Unit")]
public class OperationsRecommendationGatePolicyTests
{
    [Theory(DisplayName = "Supplier unknown revenue gate uses the declared boundary")]
    [InlineData(14.9, false)]
    [InlineData(15.0, false)]
    [InlineData(24.9, false)]
    [InlineData(25.0, true)]
    public void SupplierUnknownRevenueGate_UsesTwentyFivePercentCriticalBoundary(
        double unknownRevenueSharePct,
        bool expectedCritical)
    {
        Assert.Equal(
            expectedCritical,
            OperationsRecommendationGatePolicy.IsSupplierUnknownRevenueCritical(unknownRevenueSharePct));
    }
}
