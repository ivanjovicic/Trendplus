using Application.Analytics;
using Infrastructure.Services;
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
