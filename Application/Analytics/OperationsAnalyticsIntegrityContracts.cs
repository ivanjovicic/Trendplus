namespace Application.Analytics;

public static class OperationsAnalyticsIntegrityStates
{
    public const string Verified = "verified";
    public const string Unverified = "unverified";
    public const string Degraded = "degraded";
    public const string DriftDetected = "drift_detected";

    public static bool BlocksDecisionSignals(string? status)
        => string.Equals(status, DriftDetected, StringComparison.Ordinal);
}

public sealed record OperationsAnalyticsIntegrityProbeDelta(
    string Dimension,
    decimal EndpointOrLiveRevenue,
    decimal OracleRevenue,
    decimal RevenueDelta,
    int EndpointOrLiveUnits,
    int OracleUnits,
    int UnitsDelta);

public sealed record OperationsAnalyticsIntegritySnapshot(
    string Status,
    string EvidenceId,
    DateTime CheckedAtUtc,
    DateTime? LastVerifiedAtUtc,
    string? Trigger,
    string? Summary,
    IReadOnlyList<OperationsAnalyticsIntegrityProbeDelta> Deltas,
    bool BlocksDecisionSignals)
{
    public static OperationsAnalyticsIntegritySnapshot Unverified(string evidenceId, string trigger, string summary)
        => new(
            OperationsAnalyticsIntegrityStates.Unverified,
            evidenceId,
            DateTime.UtcNow,
            null,
            trigger,
            summary,
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false);

    public static OperationsAnalyticsIntegritySnapshot Degraded(string evidenceId, string trigger, string summary)
        => new(
            OperationsAnalyticsIntegrityStates.Degraded,
            evidenceId,
            DateTime.UtcNow,
            null,
            trigger,
            summary,
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            BlocksDecisionSignals: false);
}
