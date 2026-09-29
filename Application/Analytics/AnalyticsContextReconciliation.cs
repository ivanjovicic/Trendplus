namespace Application.Analytics;

/// <summary>
/// One read-only comparison between two consumers of an analytics metric.
/// Values are compared only after the context identity proves comparability.
/// </summary>
public sealed record AnalyticsReconciliationCase(
    string ExpectedEndpoint,
    string ActualEndpoint,
    string MetricKey,
    AnalyticsContextDescriptor? ExpectedContext,
    AnalyticsContextDescriptor? ActualContext,
    decimal? ExpectedValue,
    decimal? ActualValue);

public sealed record AnalyticsReconciliationResult(
    string ExpectedEndpoint,
    string ActualEndpoint,
    string MetricKey,
    string? ContextFingerprint,
    decimal? ExpectedValue,
    decimal? ActualValue,
    decimal? Delta,
    string Classification,
    string Explanation);

public static class AnalyticsContextReconciliation
{
    public const string Reconciled = "reconciled";
    public const string UnexplainedDelta = "unexplained_delta";
    public const string NonComparableContext = "non_comparable_context";
    public const string UnavailableContext = "unavailable_context";

    public static IReadOnlyList<AnalyticsReconciliationResult> Compare(
        IEnumerable<AnalyticsReconciliationCase> cases)
        => cases.Select(Compare).ToArray();

    public static AnalyticsReconciliationResult Compare(AnalyticsReconciliationCase comparison)
    {
        var expectedFingerprint = comparison.ExpectedContext?.Fingerprint;
        var actualFingerprint = comparison.ActualContext?.Fingerprint;

        if (string.IsNullOrWhiteSpace(expectedFingerprint) || string.IsNullOrWhiteSpace(actualFingerprint))
        {
            return CreateResult(
                comparison,
                contextFingerprint: null,
                delta: null,
                classification: UnavailableContext,
                explanation: "Context fingerprint is missing; numeric reconciliation is not evaluated.");
        }

        if (!string.Equals(expectedFingerprint, actualFingerprint, StringComparison.Ordinal))
        {
            return CreateResult(
                comparison,
                contextFingerprint: null,
                delta: null,
                classification: NonComparableContext,
                explanation: "Context fingerprints differ; the declared populations or source generations are not comparable.");
        }

        decimal? delta = comparison.ActualValue.HasValue && comparison.ExpectedValue.HasValue
            ? comparison.ActualValue.Value - comparison.ExpectedValue.Value
            : null;
        var valuesMatch = delta.HasValue && delta.Value == 0m;

        return CreateResult(
            comparison,
            contextFingerprint: expectedFingerprint,
            delta,
            classification: valuesMatch ? Reconciled : UnexplainedDelta,
            explanation: valuesMatch
                ? "Same context fingerprint and exact deterministic value match."
                : "Same context fingerprint but expected and actual values do not reconcile exactly.");
    }

    private static AnalyticsReconciliationResult CreateResult(
        AnalyticsReconciliationCase comparison,
        string? contextFingerprint,
        decimal? delta,
        string classification,
        string explanation)
        => new(
            comparison.ExpectedEndpoint,
            comparison.ActualEndpoint,
            comparison.MetricKey,
            contextFingerprint,
            comparison.ExpectedValue,
            comparison.ActualValue,
            delta,
            classification,
            explanation);
}
