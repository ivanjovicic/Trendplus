using Infrastructure.Services;
using Trendplus2.Endpoints;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class DecisionBoardDataQualityHealthEvaluationTests
{
    [Fact]
    public void EvaluateDataQualityHealth_NoRevenue_IsInsufficientData_NotExcellent()
    {
        var health = new AnalyticsDataQualityHealthSnapshot
        {
            GeneratedAtUtc = DateTime.UtcNow,
            TotalRevenue = 0m,
            HasRevenueEvidence = false,
            MissingCostRevenueSharePct = 0d,
            UnknownSupplierRevenueSharePct = 0d,
            OrphanArticleCount = 0
        };

        var result = DecisionBoardEndpoints.EvaluateDataQualityHealth(health);

        Assert.Equal("insufficient_data", result.Status);
        Assert.Equal(0, result.Score);
        Assert.Contains("prometnog dokaza", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("excellent", result.Status);
        Assert.NotEqual("good", result.Status);
    }

    [Fact]
    public void EvaluateDataQualityHealth_NoRevenueWithOrphans_StillInsufficient_NotGreen()
    {
        var health = new AnalyticsDataQualityHealthSnapshot
        {
            GeneratedAtUtc = DateTime.UtcNow,
            TotalRevenue = 0m,
            HasRevenueEvidence = false,
            MissingCostRevenueSharePct = 0d,
            UnknownSupplierRevenueSharePct = 0d,
            OrphanArticleCount = 12
        };

        var result = DecisionBoardEndpoints.EvaluateDataQualityHealth(health);

        Assert.Equal("insufficient_data", result.Status);
        Assert.Contains("Orphan", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateDataQualityHealth_CleanRevenueWindow_CanBeExcellent()
    {
        var health = new AnalyticsDataQualityHealthSnapshot
        {
            GeneratedAtUtc = DateTime.UtcNow,
            TotalRevenue = 100_000m,
            HasRevenueEvidence = true,
            MissingCostRevenueSharePct = 0d,
            UnknownSupplierRevenueSharePct = 0d,
            OrphanArticleCount = 0
        };

        var result = DecisionBoardEndpoints.EvaluateDataQualityHealth(health);

        Assert.Equal("excellent", result.Status);
        Assert.True(result.Score >= 90);
    }

    [Fact]
    public void BuildDecisionBoardResponse_NoRevenueHealth_ShowsBlocker_NotCleanSource()
    {
        var generatedAtUtc = new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc);
        var health = new AnalyticsDataQualityHealthSnapshot
        {
            GeneratedAtUtc = generatedAtUtc,
            TotalRevenue = 0m,
            HasRevenueEvidence = false,
            MissingCostRevenue = 0m,
            MissingCostRevenueSharePct = 0d,
            UnknownSupplierRevenue = 0m,
            UnknownSupplierRevenueSharePct = 0d,
            OrphanArticleCount = 0
        };

        var response = DecisionBoardEndpoints.BuildDecisionBoardResponse(
            generatedAtUtc,
            generatedAtUtc.AddDays(-30),
            generatedAtUtc,
            lastRefreshAtUtc: generatedAtUtc,
            productDecisionCenter: null,
            inventoryInsights: null,
            inventoryWorkflow: null,
            supplierSummary: null,
            actions: [],
            outcomeSummary: null,
            refreshStatus: null,
            dataQualityHealth: health,
            loadWarnings: [],
            dataScope: "all",
            storeId: null,
            supplierId: null);

        var healthSource = Assert.Single(response.SourceStates, state => state.SourceKey == "data-quality-health");
        Assert.Equal("insufficient_data", healthSource.Status);

        var blockers = Assert.Single(response.Sections, section => section.Key == "blockers");
        Assert.Contains(blockers.Cards, card => card.Id == "blocker-health");
        var healthCard = Assert.Single(blockers.Cards, card => card.Id == "blocker-health");
        Assert.Contains(healthCard.WarningCodes, code => code == "no_revenue_evidence");
        Assert.Equal("insufficient_data", healthCard.DataQualityStatus);
    }
}
