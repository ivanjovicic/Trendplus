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
        meta.OperationsIntegrityFamily = snapshot.Family;
        meta.OperationsIntegrityContextFingerprint = snapshot.ContextFingerprint;
        meta.OperationsIntegritySourceGeneration = snapshot.SourceGeneration;

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

    /// <summary>
    /// Adds a family snapshot to response metadata and binds it to the exact bounded
    /// probe context. A snapshot for another period, store, data scope or generation
    /// remains inspectable but cannot be presented as verified for this response.
    /// </summary>
    public static AnalyticsResponseMetaDto ApplyFamilyEvidence(
        AnalyticsResponseMetaDto meta,
        OperationsAnalyticsIntegrityRegistry? registry,
        string family,
        DateTime fromUtc,
        DateTime toUtc,
        string dataScope,
        int? storeId)
    {
        if (registry is null)
            return MarkIndependentlyUnverified(meta, family);

        var snapshot = registry.GetCurrent(family);
        var currentGeneration = registry.GetGeneration(family);
        var expectedFingerprint = OperationsAnalyticsIntegrityContextPolicy.CreateFingerprint(
            family,
            currentGeneration,
            fromUtc,
            toUtc,
            dataScope,
            storeId);
        var hasBoundEvidenceContext =
            !string.Equals(snapshot.Trigger, "bootstrap", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(snapshot.ContextFingerprint);

        meta.OperationsIntegrityStatus = snapshot.Status;
        meta.OperationsIntegrityCheckedAtUtc = hasBoundEvidenceContext ? snapshot.CheckedAtUtc : null;
        meta.OperationsIntegrityEvidenceId = hasBoundEvidenceContext ? snapshot.EvidenceId : null;
        meta.OperationsIntegrityFamily = family;
        meta.OperationsIntegrityContextFingerprint = snapshot.ContextFingerprint;
        meta.OperationsIntegritySourceGeneration = snapshot.SourceGeneration;
        meta.OperationsIntegrityContextMatches =
            hasBoundEvidenceContext
            && string.Equals(snapshot.SourceGeneration, currentGeneration, StringComparison.Ordinal)
            && string.Equals(snapshot.ContextFingerprint, expectedFingerprint, StringComparison.Ordinal);

        return meta;
    }

    /// <summary>
    /// Explicitly represents a family that has no independent runtime probe yet.
    /// It deliberately carries no evidence ID or checked-at timestamp.
    /// </summary>
    public static AnalyticsResponseMetaDto MarkIndependentlyUnverified(
        AnalyticsResponseMetaDto meta,
        string family)
    {
        meta.OperationsIntegrityStatus = OperationsAnalyticsIntegrityStates.Unverified;
        meta.OperationsIntegrityCheckedAtUtc = null;
        meta.OperationsIntegrityEvidenceId = null;
        meta.OperationsIntegrityFamily = family;
        meta.OperationsIntegrityContextFingerprint = null;
        meta.OperationsIntegritySourceGeneration = null;
        meta.OperationsIntegrityContextMatches = false;
        return meta;
    }
}
