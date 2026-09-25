using Application.Analytics;
using Infrastructure.Services;
using Trendplus2.Dtos;

namespace Api.Services;

public static class OperationsAnalyticsIntegrityMeta
{
    public static bool ShouldBlockDecisionSignals(OperationsAnalyticsIntegrityRegistry? registry)
        => registry is not null
           && OperationsAnalyticsIntegrityStates.BlocksDecisionSignals(registry.Current.Status);

    public static AnalyticsResponseMetaDto ApplyIntegrityState(
        AnalyticsResponseMetaDto meta,
        OperationsAnalyticsIntegrityRegistry? registry)
    {
        if (registry is null)
            return meta;

        var snapshot = registry.Current;
        meta.OperationsIntegrityStatus = snapshot.Status;
        meta.OperationsIntegrityCheckedAtUtc = snapshot.CheckedAtUtc;
        meta.OperationsIntegrityEvidenceId = snapshot.EvidenceId;

        if (string.Equals(snapshot.Status, OperationsAnalyticsIntegrityStates.DriftDetected, StringComparison.Ordinal))
        {
            meta.WarningCode = "OPERATIONS_DRIFT_DETECTED";
            meta.WarningMessage = snapshot.Summary ?? "Potvrđeno odstupanje Operacije podataka; preporuke su blokirane.";
            meta.DataQualityStatus = "critical";
            meta.RecommendationAllowed = false;
            meta.IsPartial = true;
            return meta;
        }

        if (string.Equals(snapshot.Status, OperationsAnalyticsIntegrityStates.Unverified, StringComparison.Ordinal)
            || string.Equals(snapshot.Status, OperationsAnalyticsIntegrityStates.Degraded, StringComparison.Ordinal))
        {
            meta.WarningCode ??= "OPERATIONS_INTEGRITY_UNVERIFIED";
            meta.WarningMessage ??= snapshot.Summary ?? "Integritet Operacije podataka još nije potvrđen bounded probom.";
            meta.DataQualityStatus ??= "warning";
            meta.IsPartial = true;
        }

        return meta;
    }
}
