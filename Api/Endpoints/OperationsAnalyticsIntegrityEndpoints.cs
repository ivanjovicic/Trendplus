using Application.Analytics;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Trendplus2.Endpoints;

public static class OperationsAnalyticsIntegrityEndpoints
{
    public static void MapOperationsAnalyticsIntegrityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analytics/operations-integrity", (
            OperationsAnalyticsIntegrityRegistry registry) =>
        {
            var snapshot = registry.Current;
            return Results.Ok(new
            {
                status = snapshot.Status,
                evidenceId = snapshot.EvidenceId,
                checkedAtUtc = snapshot.CheckedAtUtc,
                lastVerifiedAtUtc = snapshot.LastVerifiedAtUtc,
                trigger = snapshot.Trigger,
                summary = snapshot.Summary,
                reasonCode = snapshot.ReasonCode,
                blocksDecisionSignals = snapshot.BlocksDecisionSignals,
                family = snapshot.Family,
                contextFingerprint = snapshot.ContextFingerprint,
                sourceGeneration = snapshot.SourceGeneration,
                deltas = snapshot.Deltas.Select(delta => new
                {
                    dimension = delta.Dimension,
                    endpointOrLiveRevenue = delta.EndpointOrLiveRevenue,
                    oracleRevenue = delta.OracleRevenue,
                    revenueDelta = delta.RevenueDelta,
                    endpointOrLiveUnits = delta.EndpointOrLiveUnits,
                    oracleUnits = delta.OracleUnits,
                    unitsDelta = delta.UnitsDelta,
                    comparedRows = delta.ComparedRows,
                    comparedRevenue = delta.ComparedRevenue
                }),
                families = registry.CurrentByFamily.Select(familySnapshot => new
                {
                    family = familySnapshot.Family,
                    status = familySnapshot.Status,
                    evidenceId = familySnapshot.EvidenceId,
                    checkedAtUtc = familySnapshot.CheckedAtUtc,
                    lastVerifiedAtUtc = familySnapshot.LastVerifiedAtUtc,
                    contextFingerprint = familySnapshot.ContextFingerprint,
                    sourceGeneration = familySnapshot.SourceGeneration,
                    blocksDecisionSignals = familySnapshot.BlocksDecisionSignals,
                    summary = familySnapshot.Summary
                })
            });
        })
        .WithName("GetOperationsAnalyticsIntegrity")
        .WithTags("Analytics");

        app.MapGet("/api/analytics/operations-integrity/evidence/{evidenceId}", async (
            string evidenceId,
            TrendplusDbContext db,
            CancellationToken ct) =>
        {
            var record = await db.OperationsAnalyticsIntegrityEvidence
                .AsNoTracking()
                .SingleOrDefaultAsync(row => row.EvidenceId == evidenceId, ct);
            if (record is null)
                return Results.NotFound(new { evidenceId });

            return Results.Ok(new
            {
                evidenceId = record.EvidenceId,
                status = record.Status,
                checkedAtUtc = record.CheckedAtUtc,
                lastVerifiedAtUtc = record.LastVerifiedAtUtc,
                trigger = record.Trigger,
                summary = record.Summary,
                reasonCode = record.FailureClassification,
                failureClassification = record.FailureClassification,
                tenantScope = record.TenantScope,
                storeId = record.StoreId,
                dataScope = record.DataScope,
                requestedFromUtc = record.RequestedFromUtc,
                requestedToUtc = record.RequestedToUtc,
                effectiveFromUtc = record.EffectiveFromUtc,
                effectiveToUtc = record.EffectiveToUtc,
                databaseFingerprint = record.DatabaseFingerprint,
                appCommit = record.AppCommit,
                schemaVersion = record.SchemaVersion,
                contractVersion = record.ContractVersion,
                family = record.Family,
                contextFingerprint = record.ContextFingerprint,
                sourceGeneration = record.SourceGeneration,
                fixtureVersion = record.FixtureVersion,
                cacheVersion = record.CacheVersion,
                endpointOrLiveRevenue = record.EndpointOrLiveRevenue,
                oracleRevenue = record.OracleRevenue,
                revenueDelta = record.RevenueDelta,
                endpointOrLiveUnits = record.EndpointOrLiveUnits,
                oracleUnits = record.OracleUnits,
                unitsDelta = record.UnitsDelta,
                deltasJson = record.DeltasJson,
                coverageJson = record.CoverageJson,
                blocksDecisionSignals = record.BlocksDecisionSignals,
                createdAtUtc = record.CreatedAtUtc
            });
        })
        .WithName("GetOperationsAnalyticsIntegrityEvidence")
        .WithTags("Analytics");

        app.MapPost("/api/analytics/operations-integrity/probe", async (
            IOperationsAnalyticsIntegrityService integrityService,
            CancellationToken ct) =>
        {
            var snapshot = await integrityService.RunBoundedProbeAsync(ct);
            return Results.Ok(new
            {
                status = snapshot.Status,
                evidenceId = snapshot.EvidenceId,
                checkedAtUtc = snapshot.CheckedAtUtc,
                blocksDecisionSignals = snapshot.BlocksDecisionSignals,
                summary = snapshot.Summary
            });
        })
        .WithName("RunOperationsAnalyticsIntegrityProbe")
        .WithTags("Analytics");
    }
}
