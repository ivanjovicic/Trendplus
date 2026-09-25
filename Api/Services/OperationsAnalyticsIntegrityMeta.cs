using Application.Analytics;
using Infrastructure.Services;
using Trendplus2.Dtos;

namespace Api.Services;

public static class OperationsAnalyticsIntegrityMeta
{
    public static bool ShouldBlockDecisionSignals(OperationsAnalyticsIntegrityRegistry? registry)
        => registry is not null && registry.Current.BlocksDecisionSignals;

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

        if (!snapshot.BlocksDecisionSignals)
            return meta;

        if (string.Equals(snapshot.Status, OperationsAnalyticsIntegrityStates.DriftDetected, StringComparison.Ordinal))
        {
            meta.WarningCode = "OPERATIONS_DRIFT_DETECTED";
            meta.WarningMessage = snapshot.Summary ?? "Potvrđeno odstupanje Operacije podataka; preporuke su blokirane.";
            meta.DataQualityStatus = "critical";
        }
        else
        {
            meta.WarningCode ??= "OPERATIONS_INTEGRITY_UNVERIFIED";
            meta.WarningMessage ??= snapshot.Summary ?? "Integritet Operacije podataka nije potvrđen; preporuke su privremeno blokirane.";
            meta.DataQualityStatus ??= "warning";
        }

        meta.RecommendationAllowed = false;
        meta.IsPartial = true;
        return meta;
    }
}
