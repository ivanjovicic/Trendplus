using Api.Services;
using Application.Analytics;
using Infrastructure.Services;
using Trendplus2.Dtos;
using Xunit;

namespace Api.Tests;

public sealed class OperationsAnalyticsIntegrityMetaTests
{
    [Fact]
    public void ApplyIntegrityState_BlocksRecommendationsWhenDriftDetected()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        registry.Set(new OperationsAnalyticsIntegritySnapshot(
            OperationsAnalyticsIntegrityStates.DriftDetected,
            "evidence-1",
            DateTime.UtcNow,
            null,
            "probe",
            "Drift detected in bounded probe.",
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: true));

        var meta = OperationsAnalyticsIntegrityMeta.ApplyIntegrityState(
            AnalyticsResponseMetaFactory.Success("good"),
            registry);

        Assert.Equal(OperationsAnalyticsIntegrityStates.DriftDetected, meta.OperationsIntegrityStatus);
        Assert.Equal("OPERATIONS_DRIFT_DETECTED", meta.WarningCode);
        Assert.False(meta.RecommendationAllowed);
        Assert.True(meta.IsPartial);
    }

    [Fact]
    public void ShouldBlockDecisionSignals_ReturnsFalseWhenVerified()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        registry.Set(new OperationsAnalyticsIntegritySnapshot(
            OperationsAnalyticsIntegrityStates.Verified,
            "evidence-2",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "probe",
            "Verified.",
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false));

        Assert.False(OperationsAnalyticsIntegrityMeta.ShouldBlockDecisionSignals(registry));
    }
}
