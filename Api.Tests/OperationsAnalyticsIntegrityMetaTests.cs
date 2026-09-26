using Api.Services;
using Application.Analytics;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
    public void ApplyIntegrityState_UnverifiedWarnsWithoutBlockingRecommendations()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        registry.MarkUnverified("cache_clear", "Cache cleared.");

        var meta = OperationsAnalyticsIntegrityMeta.ApplyIntegrityState(
            AnalyticsResponseMetaFactory.Success("good"),
            registry);

        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, meta.OperationsIntegrityStatus);
        Assert.Equal("OPERATIONS_INTEGRITY_UNVERIFIED", meta.WarningCode);
        Assert.Null(meta.RecommendationAllowed);
        Assert.False(OperationsAnalyticsIntegrityMeta.ShouldBlockDecisionSignals(registry));
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

    [Fact]
    public async Task BoundedProbe_PreservesBatchTriggerWhenConnectionIsUnavailable()
    {
        var dbOptions = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase($"integrity-probe-{Guid.NewGuid():N}")
            .Options;
        await using var db = new TrendplusDbContext(dbOptions);
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var service = new OperationsAnalyticsIntegrityService(
            db,
            new DisabledAnalyticsCacheService(),
            registry,
            Options.Create(new OperationsAnalyticsIntegrityOptions()),
            NullLogger<OperationsAnalyticsIntegrityService>.Instance);

        var snapshot = await service.RunBoundedProbeAsync(
            trigger: "access_import:42",
            summary: "Batch 42 probe.");

        Assert.Equal(OperationsAnalyticsIntegrityStates.Degraded, snapshot.Status);
        Assert.Equal("access_import:42", snapshot.Trigger);
        Assert.Equal("Batch 42 probe.", snapshot.Summary);
    }
}
