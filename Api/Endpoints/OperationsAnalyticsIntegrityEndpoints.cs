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
                blocksDecisionSignals = snapshot.BlocksDecisionSignals,
                deltas = snapshot.Deltas.Select(delta => new
                {
                    dimension = delta.Dimension,
                    endpointOrLiveRevenue = delta.EndpointOrLiveRevenue,
                    oracleRevenue = delta.OracleRevenue,
                    revenueDelta = delta.RevenueDelta,
                    endpointOrLiveUnits = delta.EndpointOrLiveUnits,
                    oracleUnits = delta.OracleUnits,
                    unitsDelta = delta.UnitsDelta
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
