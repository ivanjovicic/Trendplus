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
        Assert.Equal(5, OperationsAnalyticsIntegrityFamilies.Enrolled.Count);
        Assert.All(
            OperationsAnalyticsIntegrityFamilies.Enrolled,
            definition =>
            {
                Assert.False(string.IsNullOrWhiteSpace(definition.Family));
                Assert.False(string.IsNullOrWhiteSpace(definition.Owner));
                Assert.InRange(definition.MaxRows, 1, 10000);
                Assert.InRange(definition.MaxWindowDays, 1, 31);
            });
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
