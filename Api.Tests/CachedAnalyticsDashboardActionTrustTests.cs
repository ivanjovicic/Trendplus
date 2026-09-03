using Trendplus2.Dtos;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

public sealed class CachedAnalyticsDashboardActionTrustTests
{
    [Fact]
    public void BuildDashboardDecisionActions_PreservesActionableBlockedAndLegacyTrustStates()
    {
        var fromDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc);

        var snapshot = new DashboardAdvancedSnapshotDto
        {
            Actions =
            [
                new DashboardActionDto
                {
                    Priority = "P1",
                    Title = "Replenishment",
                    Recommendation = "Prioritize refill for OOS/low-stock SKUs with highest velocity.",
                    StatusReason = "Signal ready.",
                    ConfidencePct = 82,
                    ReliabilityPct = 74,
                    RecommendationAllowed = true,
                    DataQualityStatus = "good"
                },
                new DashboardActionDto
                {
                    Priority = "P1",
                    Title = "Refresh pipeline",
                    Recommendation = "Run import sync and refresh aggregate summaries.",
                    StatusReason = "Freshness validation indicates stale data.",
                    ConfidencePct = 63,
                    ReliabilityPct = 58,
                    RecommendationAllowed = false,
                    DataQualityStatus = "critical"
                },
                new DashboardActionDto
                {
                    Priority = "P3",
                    Title = "Monitor",
                    Recommendation = "Nastavite monitoring metrika i osvežavajte agregate dnevno."
                }
            ]
        };

        var actions = CachedAnalyticsEndpoints.BuildDashboardDecisionActions(
            null,
            snapshot,
            fromDate,
            toDate,
            11,
            22);

        Assert.Equal(3, actions.Count);

        var actionable = actions[0];
        Assert.True(actionable.RecommendationAllowed);
        Assert.Equal("good", actionable.DataQualityStatus);
        Assert.Equal("Signal ready.", actionable.StatusReason);
        Assert.Equal(82, actionable.ConfidencePct);
        Assert.Equal(74, actionable.ReliabilityPct);

        var blocked = actions[1];
        Assert.False(blocked.RecommendationAllowed);
        Assert.Equal("critical", blocked.DataQualityStatus);
        Assert.Equal("Freshness validation indicates stale data.", blocked.StatusReason);
        Assert.Equal(63, blocked.ConfidencePct);
        Assert.Equal(58, blocked.ReliabilityPct);

        var legacy = actions[2];
        Assert.False(legacy.RecommendationAllowed);
        Assert.Equal("insufficient_data", legacy.DataQualityStatus);
        Assert.Equal("Legacy dashboard action bez trust payloada.", legacy.StatusReason);
        Assert.Equal("Nastavite monitoring metrika i osvežavajte agregate dnevno.", legacy.Reason);
    }

    [Fact]
    public void BuildDashboardDecisionActions_DoesNotReenableBlockedProductSignal()
    {
        var fromDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var toDate = new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc);
        var snapshot = new ProductDecisionCenterResponseDto
        {
            Rows =
            [
                new ProductDecisionCenterRowDto
                {
                    ProductId = 1,
                    Sku = "SKU-1",
                    ProductName = "Blokiran artikal",
                    Revenue = 12500m,
                    UnitsSold = 12,
                    VelocityUnitsPerDay = 0.4m,
                    RecommendationStatus = "REPLENISH",
                    RecommendationLabel = "Dopuni",
                    RecommendationReason = "Nedostaje pouzdan trošak.",
                    ConfidencePct = 82,
                    ReliabilityPct = 24,
                    RecommendationAllowed = false,
                    DataQualityStatus = "critical",
                },
                new ProductDecisionCenterRowDto
                {
                    ProductId = 2,
                    Sku = "SKU-2",
                    ProductName = "Blokiran artikal 2",
                    Revenue = 7500m,
                    UnitsSold = 9,
                    VelocityUnitsPerDay = 0.3m,
                    RecommendationStatus = "REPLENISH",
                    RecommendationLabel = "Dopuni",
                    RecommendationReason = "Nedostaje pouzdan trošak.",
                    ConfidencePct = 80,
                    ReliabilityPct = 20,
                    RecommendationAllowed = false,
                    DataQualityStatus = "critical",
                }
            ]
        };

        var actions = CachedAnalyticsEndpoints.BuildDashboardDecisionActions(
            snapshot,
            null,
            fromDate,
            toDate,
            null,
            null);

        var replenish = Assert.Single(actions.Where(action => action.RecommendationStatus == "REPLENISH"));
        Assert.False(replenish.RecommendationAllowed);
        Assert.Equal("critical", replenish.DataQualityStatus);
    }
}
