using Application.Analytics;
using Trendplus2.Dtos;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsDecisionReadinessTests
{
    [Fact]
    public void RecommendationWithDisallowedEvidenceIsBlockedWithoutASecondScore()
    {
        var readiness = AnalyticsDecisionReadinessPolicy.Resolve(
            success: true,
            surfaceRole: "recommendation",
            recommendationAllowed: false,
            dataQualityStatus: "warning",
            isPartial: false,
            reasonCodes: ["missing_cost_evidence"],
            evidenceReferences: ["supplier.costCoverage"]);

        Assert.Equal(AnalyticsDecisionReadinessStates.Blocked, readiness.State);
        Assert.Equal(["missing_cost_evidence"], readiness.ReasonCodes);
        Assert.Null(readiness.RepairPath);
    }

    [Fact]
    public void SignalSurfaceRemainsUsefulButNeverBecomesDecisionReady()
    {
        var readiness = AnalyticsDecisionReadinessPolicy.Resolve(
            success: true,
            surfaceRole: "signal",
            recommendationAllowed: true,
            dataQualityStatus: "good",
            isPartial: false,
            evidenceReferences: ["color.net_sales_signed"]);

        Assert.Equal(AnalyticsDecisionReadinessStates.SignalOnly, readiness.State);
        Assert.Equal("signal", readiness.SurfaceRole);
        Assert.NotEqual(AnalyticsDecisionReadinessStates.DecisionReady, readiness.State);
    }

    [Fact]
    public void MissingEligibilityOrFailedResponseIsUnavailable()
    {
        var missing = AnalyticsDecisionReadinessPolicy.Resolve(
            success: true,
            surfaceRole: "recommendation",
            recommendationAllowed: null,
            dataQualityStatus: "good",
            isPartial: false);
        var failed = AnalyticsDecisionReadinessPolicy.Resolve(
            success: false,
            surfaceRole: "recommendation",
            recommendationAllowed: true,
            dataQualityStatus: "good",
            isPartial: false);

        Assert.Equal(AnalyticsDecisionReadinessStates.Unavailable, missing.State);
        Assert.Equal(AnalyticsDecisionReadinessStates.Unavailable, failed.State);
    }

    [Fact]
    public void MetaFactoryPreservesReadinessAndReasonEvidence()
    {
        var meta = AnalyticsResponseMetaFactory.Success("warning", isPartial: true);
        meta.RecommendationAllowed = false;

        AnalyticsResponseMetaFactory.ApplyDecisionReadiness(
            meta,
            "recommendation",
            reasonCodes: ["freshness_blocks_decision"],
            evidenceReferences: ["refresh.lastSuccessfulRefreshAtUtc"],
            repairPath: "Refresh status");

        Assert.NotNull(meta.DecisionReadiness);
        Assert.Equal(AnalyticsDecisionReadinessStates.Blocked, meta.DecisionReadiness!.State);
        Assert.Equal(["freshness_blocks_decision"], meta.DecisionReadiness.ReasonCodes);
        Assert.Equal("Refresh status", meta.DecisionReadiness.RepairPath);
    }
}
