using Application.Analytics;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class OperationsAnalyticsIntegrityFamilyTests
{
    [Fact]
    public void EnrolledFamilies_HaveExplicitOwnersAndBounds()
    {
        Assert.Equal(6, OperationsAnalyticsIntegrityFamilies.Enrolled.Count);
        Assert.All(
            OperationsAnalyticsIntegrityFamilies.Enrolled,
            definition =>
            {
                Assert.False(string.IsNullOrWhiteSpace(definition.Family));
                Assert.False(string.IsNullOrWhiteSpace(definition.Owner));
                Assert.InRange(definition.MaxRows, 1, 10000);
            });
        Assert.Equal(180, OperationsAnalyticsIntegrityFamilies.DefinitionFor(OperationsAnalyticsIntegrityFamilies.Nivelacija).MaxWindowDays);
        Assert.All(
            OperationsAnalyticsIntegrityFamilies.Enrolled.Where(definition => definition.Family != OperationsAnalyticsIntegrityFamilies.Nivelacija),
            definition => Assert.InRange(definition.MaxWindowDays, 1, 31));
    }

    [Fact]
    public void ResolveAffected_MapsNivelacijaAliasesToDedicatedFamily()
    {
        var resolved = OperationsAnalyticsIntegrityFamilies.ResolveAffected("vendor-sales-nivelacija");
        Assert.Equal([OperationsAnalyticsIntegrityFamilies.Nivelacija], resolved);
    }

    [Fact]
    public void FamilyInvalidation_RotatesOnlyTheAffectedGeneration()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var supplierGeneration = registry.GetGeneration(OperationsAnalyticsIntegrityFamilies.SupplierShoeType);
        var inventoryGeneration = registry.GetGeneration(OperationsAnalyticsIntegrityFamilies.Inventory);

        registry.MarkUnverified(
            "cache_clear",
            "Supplier cache cleared.",
            OperationsAnalyticsIntegrityFamilies.ResolveAffected("supplier-sales"));

        Assert.NotEqual(supplierGeneration, registry.GetGeneration(OperationsAnalyticsIntegrityFamilies.SupplierShoeType));
        Assert.Equal(inventoryGeneration, registry.GetGeneration(OperationsAnalyticsIntegrityFamilies.Inventory));
        Assert.Equal(
            OperationsAnalyticsIntegrityStates.Unverified,
            registry.GetCurrent(OperationsAnalyticsIntegrityFamilies.SupplierShoeType).Status);
        Assert.Equal(
            OperationsAnalyticsIntegrityStates.Unverified,
            registry.GetCurrent(OperationsAnalyticsIntegrityFamilies.Inventory).Status);
    }

    [Fact]
    public void AccessImportInvalidation_RotatesInventoryEvidenceGeneration()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var before = registry.GetGeneration(OperationsAnalyticsIntegrityFamilies.Inventory);

        registry.MarkUnverified(
            "access_import",
            "Inventory source changed.",
            OperationsAnalyticsIntegrityFamilies.ResolveAffected("access_import"));

        Assert.NotEqual(before, registry.GetGeneration(OperationsAnalyticsIntegrityFamilies.Inventory));
        Assert.Equal(
            OperationsAnalyticsIntegrityStates.Unverified,
            registry.GetCurrent(OperationsAnalyticsIntegrityFamilies.Inventory).Status);
    }

    [Fact]
    public void NivelacijaAndAccessImportInvalidation_RotateOnlyTheSharedFamilyGeneration()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var family = OperationsAnalyticsIntegrityFamilies.Nivelacija;
        var before = registry.GetGeneration(family);
        var supplierBefore = registry.GetGeneration(OperationsAnalyticsIntegrityFamilies.SupplierShoeType);

        registry.MarkUnverified("nivelacija_repair", "Repair began.");
        Assert.NotEqual(before, registry.GetGeneration(family));
        Assert.Equal(supplierBefore, registry.GetGeneration(OperationsAnalyticsIntegrityFamilies.SupplierShoeType));

        before = registry.GetGeneration(family);
        registry.MarkUnverified("access_import", "Source generation changed.");
        Assert.NotEqual(before, registry.GetGeneration(family));
        Assert.Equal(
            OperationsAnalyticsIntegrityStates.Unverified,
            registry.GetCurrent(family).Status);

        before = registry.GetGeneration(family);
        registry.MarkUnverified("cache_clear", "Analytics cache/source generation changed.");
        Assert.NotEqual(before, registry.GetGeneration(family));
        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, registry.GetCurrent(family).Status);
    }

    [Fact]
    public void CompletedProbeFromOldGenerationCannotRestoreGreenAfterMutation()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var family = OperationsAnalyticsIntegrityFamilies.Nivelacija;
        var oldGeneration = registry.GetGeneration(family);
        registry.MarkFamilyUnverified(family, "nivelacija_write", "Write is in progress.");

        var stale = new OperationsAnalyticsIntegritySnapshot(
            OperationsAnalyticsIntegrityStates.Verified,
            "old-probe",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "bounded_probe",
            "Old probe completed late.",
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            false)
        {
            Family = family,
            SourceGeneration = oldGeneration
        };

        registry.Set(stale);
        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, registry.GetCurrent(family).Status);
        Assert.NotEqual(oldGeneration, registry.GetGeneration(family));
    }

    [Fact]
    public void ProbeStartedDuringNivelacijaWriteCannotPublishGreenBeforeCommit()
    {
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var family = OperationsAnalyticsIntegrityFamilies.Nivelacija;
        registry.BeginFamilyMutation(family, "nivelacija_write", "First write in progress.");
        var pending = registry.BeginFamilyMutation(family, "nivelacija_write", "Second write in progress.");
        var probeDuringWrite = new OperationsAnalyticsIntegritySnapshot(
            OperationsAnalyticsIntegrityStates.Verified,
            "probe-during-write",
            DateTime.UtcNow,
            DateTime.UtcNow,
            "bounded_probe",
            "Probe observed source rows before commit.",
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            false)
        {
            Family = family,
            SourceGeneration = pending.SourceGeneration
        };

        Assert.False(registry.Set(probeDuringWrite));
        Assert.Equal(OperationsAnalyticsIntegrityStates.Unverified, registry.GetCurrent(family).Status);

        var firstCompleted = registry.CompleteFamilyMutation(family, "nivelacija_write", "First write committed; second write remains.");
        var probeBetweenWrites = probeDuringWrite with
        {
            EvidenceId = "probe-between-writes",
            SourceGeneration = firstCompleted.SourceGeneration
        };
        Assert.False(registry.Set(probeBetweenWrites));

        var completed = registry.CompleteFamilyMutation(family, "nivelacija_write", "Both writes committed; probe required.");
        var postCommitProbe = probeDuringWrite with
        {
            EvidenceId = "post-commit-probe",
            SourceGeneration = completed.SourceGeneration
        };
        Assert.True(registry.Set(postCommitProbe));
        Assert.Equal(OperationsAnalyticsIntegrityStates.Verified, registry.GetCurrent(family).Status);
    }

    [Fact]
    public async Task ProbeWithoutDatabase_PublishesExplicitNonVerifiedStateForEveryEnrolledFamily()
    {
        var dbOptions = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseInMemoryDatabase($"integrity-family-{Guid.NewGuid():N}")
            .Options;
        await using var db = new TrendplusDbContext(dbOptions);
        var registry = new OperationsAnalyticsIntegrityRegistry();
        var service = new OperationsAnalyticsIntegrityService(
            db,
            new DisabledAnalyticsCacheService(),
            registry,
            Options.Create(new OperationsAnalyticsIntegrityOptions()),
            NullLogger<OperationsAnalyticsIntegrityService>.Instance);

        await service.RunBoundedProbeAsync(trigger: "family-test");

        Assert.Equal(OperationsAnalyticsIntegrityFamilies.Enrolled.Count, registry.CurrentByFamily.Count);
        Assert.All(
            registry.CurrentByFamily,
            snapshot =>
            {
                Assert.Equal(OperationsAnalyticsIntegrityStates.Degraded, snapshot.Status);
                Assert.False(string.IsNullOrWhiteSpace(snapshot.EvidenceId));
                Assert.False(string.IsNullOrWhiteSpace(snapshot.ContextFingerprint));
                Assert.False(string.IsNullOrWhiteSpace(snapshot.SourceGeneration));
            });
    }

    [Fact]
    public void ProbeResult_DistinguishesDriftFromDependencyDegradation()
    {
        var delta = new OperationsAnalyticsIntegrityProbeDelta("inventory_balance", 12m, 10m, 2m, 3, 2, 1);
        var drift = OperationsAnalyticsIntegrityProbeResult.DriftDetected("Mismatch.", [delta]);
        var degraded = OperationsAnalyticsIntegrityProbeResult.Degraded("Dependency unavailable.");

        Assert.Equal(OperationsAnalyticsIntegrityStates.DriftDetected, drift.Status);
        Assert.True(drift.BlocksDecisionSignals);
        Assert.Equal(OperationsAnalyticsIntegrityStates.Degraded, degraded.Status);
        Assert.False(degraded.BlocksDecisionSignals);
    }
}
