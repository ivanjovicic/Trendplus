using Api.Services;
using Application.Analytics;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
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
    public void ApplyFamilyEvidence_BindsEvidenceToExactContextAndGeneration()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var family = OperationsAnalyticsIntegrityFamilies.SalesDashboard;
        var generation = registry.GetGeneration(family);
        var fromUtc = new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
        var fingerprint = OperationsAnalyticsIntegrityContextPolicy.CreateFingerprint(
            family, generation, fromUtc, toUtc, "all", null);
        registry.Set(new OperationsAnalyticsIntegritySnapshot(
            OperationsAnalyticsIntegrityStates.Verified,
            "sales-evidence-verified",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "bounded_probe",
            "Sales buckets reconciled.",
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false)
        {
            Family = family,
            ContextFingerprint = fingerprint,
            SourceGeneration = generation
        });

        var matching = OperationsAnalyticsIntegrityMeta.ApplyFamilyEvidence(
            AnalyticsResponseMetaFactory.Success("good"), registry, family, fromUtc, toUtc, "all", null);
        var changedFilter = OperationsAnalyticsIntegrityMeta.ApplyFamilyEvidence(
            AnalyticsResponseMetaFactory.Success("good"), registry, family, fromUtc.AddDays(1), toUtc, "all", null);

        Assert.Equal("sales-evidence-verified", matching.OperationsIntegrityEvidenceId);
        Assert.Equal(fingerprint, matching.OperationsIntegrityContextFingerprint);
        Assert.True(matching.OperationsIntegrityContextMatches);
        Assert.False(changedFilter.OperationsIntegrityContextMatches);
        Assert.Equal(OperationsAnalyticsIntegrityStates.Verified, changedFilter.OperationsIntegrityStatus);
    }

    [Fact]
    public void ApplyFamilyEvidence_DoesNotExposeTransientInvalidationMarkerAsDurableEvidence()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var family = OperationsAnalyticsIntegrityFamilies.SalesDashboard;
        registry.MarkFamilyUnverified(family, "cache_clear", "A probe is being scheduled.");

        var meta = OperationsAnalyticsIntegrityMeta.ApplyFamilyEvidence(
            AnalyticsResponseMetaFactory.Success("good"),
            registry,
            family,
            new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
            "all",
            null);

        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, meta.OperationsIntegrityStatus);
        Assert.Null(meta.OperationsIntegrityEvidenceId);
        Assert.Null(meta.OperationsIntegrityCheckedAtUtc);
        Assert.Null(meta.OperationsIntegrityContextFingerprint);
        Assert.False(meta.OperationsIntegrityContextMatches);
    }

    [Fact]
    public void NivelacijaEvidence_DistinguishesNullStoreFromNegativeStoreAndStaleContextIsUnverified()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var family = OperationsAnalyticsIntegrityFamilies.Nivelacija;
        var generation = registry.GetGeneration(family);
        var from = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var nullStoreFingerprint = OperationsAnalyticsIntegrityContextPolicy.CreateFingerprint(family, generation, from, to, "all", null);
        var negativeStoreFingerprint = OperationsAnalyticsIntegrityContextPolicy.CreateFingerprint(family, generation, from, to, "all", -1);
        Assert.NotEqual(nullStoreFingerprint, negativeStoreFingerprint);

        registry.Set(new OperationsAnalyticsIntegritySnapshot(
            OperationsAnalyticsIntegrityStates.Verified, "nivelacija-proof", DateTime.UtcNow, DateTime.UtcNow,
            "bounded_probe", "Oracle matched.", Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(), false)
        {
            Family = family,
            ContextFingerprint = nullStoreFingerprint,
            SourceGeneration = generation
        });

        var stale = OperationsAnalyticsIntegrityMeta.ApplyFamilyEvidence(
            AnalyticsResponseMetaFactory.Success(), registry, family, from.AddDays(-1), to, "all", null);
        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, stale.OperationsIntegrityStatus);
        Assert.False(stale.OperationsIntegrityContextMatches);
        Assert.Null(stale.OperationsIntegrityEvidenceId);
        Assert.Null(stale.OperationsIntegrityCheckedAtUtc);
    }

    [Fact]
    public void NivelacijaEvidence_ExactDriftIncludesDimensionsAndStaleProbeCannotStayGreen()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var family = OperationsAnalyticsIntegrityFamilies.Nivelacija;
        var generation = registry.GetGeneration(family);
        var from = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var fingerprint = OperationsAnalyticsIntegrityContextPolicy.CreateFingerprint(family, generation, from, to, "imported", 2);
        var dimensions = JsonDocument.Parse("{\"eventId\":56801,\"direction\":\"markdown\",\"isMature\":true,\"overlapsNextEvent\":false,\"storeId\":2,\"dataScope\":\"imported\"}").RootElement.Clone();
        registry.Set(new OperationsAnalyticsIntegritySnapshot(
            OperationsAnalyticsIntegrityStates.DriftDetected, "nivelacija-drift", DateTime.UtcNow, null,
            "bounded_probe", "Store/cohort event drift.", Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(), true)
        {
            Family = family,
            ContextFingerprint = fingerprint,
            SourceGeneration = generation,
            EvidenceDimensions = dimensions
        });

        var drift = OperationsAnalyticsIntegrityMeta.ApplyFamilyEvidence(
            AnalyticsResponseMetaFactory.Success(), registry, family, from, to, "imported", 2);
        Assert.Equal(OperationsAnalyticsIntegrityStates.DriftDetected, drift.OperationsIntegrityStatus);
        Assert.False(drift.RecommendationAllowed);
        Assert.Equal(56801, drift.OperationsIntegrityEvidenceDimensions!.Value.GetProperty("eventId").GetInt32());

        var staleSnapshot = registry.GetCurrent(family) with { CheckedAtUtc = DateTime.UtcNow.AddHours(-2) };
        registry.Set(staleSnapshot);
        var stale = OperationsAnalyticsIntegrityMeta.ApplyFamilyEvidence(
            AnalyticsResponseMetaFactory.Success(), registry, family, from, to, "imported", 2);
        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, stale.OperationsIntegrityStatus);
        Assert.Null(stale.OperationsIntegrityEvidenceId);
        Assert.Null(stale.OperationsIntegrityEvidenceDimensions);
    }

    [Fact]
    public void MarkIndependentlyUnverified_HasNoEvidenceOrCheckedAt()
    {
        var meta = OperationsAnalyticsIntegrityMeta.MarkIndependentlyUnverified(
            AnalyticsResponseMetaFactory.Success("good"), "nivelacija");

        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, meta.OperationsIntegrityStatus);
        Assert.Equal("nivelacija", meta.OperationsIntegrityFamily);
        Assert.Null(meta.OperationsIntegrityEvidenceId);
        Assert.Null(meta.OperationsIntegrityCheckedAtUtc);
        Assert.False(meta.OperationsIntegrityContextMatches);
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
