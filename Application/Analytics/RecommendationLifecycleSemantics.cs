using Domain.Model.Analytics;

namespace Application.Analytics;

/// <summary>
/// RL04 deterministic recommendation lifecycle + measured-learning eligibility.
/// Lifecycle and outcome evidence stay on separate axes; acceptance alone never implies success.
/// </summary>
public static class RecommendationLifecycleSemantics
{
    public static class LifecycleStates
    {
        public const string Issued = "issued";
        public const string Accepted = "accepted";
        public const string Rejected = "rejected";
        public const string Ignored = "ignored";
        public const string Executed = "executed";

        public static readonly string[] AllValues =
        {
            Issued,
            Accepted,
            Rejected,
            Ignored,
            Executed
        };
    }

    public static class OutcomeEvidenceStates
    {
        public const string Pending = "pending";
        public const string Measured = "measured";
        public const string NotMeasured = "not_measured";

        public static readonly string[] AllValues = { Pending, Measured, NotMeasured };
    }

    public static RecommendationLifecycleCaptureDto ProjectIssuedRecommendation(
        DateTime? asOfUtc = null)
    {
        _ = asOfUtc;
        return new RecommendationLifecycleCaptureDto(
            LifecycleState: LifecycleStates.Issued,
            OutcomeEvidenceState: OutcomeEvidenceStates.Pending,
            OutcomeResult: AnalyticsActionConstants.OutcomeStatuses.Pending,
            LearningEligible: false,
            LearningEligibilityReasonCodes:
            [
                "lifecycle_issued_only",
                "outcome_not_measured",
                "acceptance_is_not_success"
            ],
            CountsTowardIssued: true,
            CountsTowardAccepted: false,
            CountsTowardRejected: false,
            CountsTowardIgnored: false,
            CountsTowardExecuted: false,
            CountsTowardMeasured: false,
            CountsTowardSuccess: false,
            CountsTowardNeutral: false,
            CountsTowardNegative: false,
            CountsTowardNotMeasured: false);
    }

    public static RecommendationLifecycleCaptureDto Project(
        AnalyticsActionItem item,
        DateTime? asOfUtc = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        var nowUtc = NormalizeUtc(asOfUtc ?? DateTime.UtcNow);
        var lifecycleState = ResolveLifecycleState(item, nowUtc);
        var outcomeResult = NormalizeOutcomeStatus(item.OutcomeStatus);
        var evidenceSource = ResolveEvidenceSource(item);
        var hasMeasurementTimestamp = item.OutcomeMeasuredAtUtc.HasValue;
        var outcomeEvidenceState = ResolveOutcomeEvidenceState(
            outcomeResult,
            hasMeasurementTimestamp,
            evidenceSource);

        var reasonCodes = new List<string>();
        var learningEligible = EvaluateLearningEligibility(
            lifecycleState,
            outcomeResult,
            outcomeEvidenceState,
            hasMeasurementTimestamp,
            evidenceSource,
            reasonCodes);

        var countsTowardMeasured = learningEligible;
        var countsTowardSuccess = learningEligible
            && string.Equals(outcomeResult, AnalyticsActionConstants.OutcomeStatuses.Success, StringComparison.OrdinalIgnoreCase);
        var countsTowardNeutral = learningEligible
            && string.Equals(outcomeResult, AnalyticsActionConstants.OutcomeStatuses.Neutral, StringComparison.OrdinalIgnoreCase);
        var countsTowardNegative = learningEligible
            && string.Equals(outcomeResult, AnalyticsActionConstants.OutcomeStatuses.Negative, StringComparison.OrdinalIgnoreCase);
        var countsTowardNotMeasured = string.Equals(lifecycleState, LifecycleStates.Executed, StringComparison.OrdinalIgnoreCase)
            && string.Equals(outcomeEvidenceState, OutcomeEvidenceStates.NotMeasured, StringComparison.OrdinalIgnoreCase);

        return new RecommendationLifecycleCaptureDto(
            LifecycleState: lifecycleState,
            OutcomeEvidenceState: outcomeEvidenceState,
            OutcomeResult: outcomeResult,
            LearningEligible: learningEligible,
            LearningEligibilityReasonCodes: reasonCodes,
            CountsTowardIssued: true,
            CountsTowardAccepted: lifecycleState is LifecycleStates.Accepted or LifecycleStates.Executed,
            CountsTowardRejected: string.Equals(lifecycleState, LifecycleStates.Rejected, StringComparison.OrdinalIgnoreCase),
            CountsTowardIgnored: string.Equals(lifecycleState, LifecycleStates.Ignored, StringComparison.OrdinalIgnoreCase),
            CountsTowardExecuted: string.Equals(lifecycleState, LifecycleStates.Executed, StringComparison.OrdinalIgnoreCase),
            CountsTowardMeasured: countsTowardMeasured,
            CountsTowardSuccess: countsTowardSuccess,
            CountsTowardNeutral: countsTowardNeutral,
            CountsTowardNegative: countsTowardNegative,
            CountsTowardNotMeasured: countsTowardNotMeasured);
    }

    public static string ResolveLifecycleState(AnalyticsActionItem item, DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(item);

        var status = (item.Status ?? string.Empty).Trim();
        if (string.Equals(status, AnalyticsActionConstants.Statuses.Done, StringComparison.OrdinalIgnoreCase))
        {
            return LifecycleStates.Executed;
        }

        if (string.Equals(status, AnalyticsActionConstants.Statuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            return LifecycleStates.Rejected;
        }

        if (string.Equals(status, AnalyticsActionConstants.Statuses.Accepted, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, AnalyticsActionConstants.Statuses.Deferred, StringComparison.OrdinalIgnoreCase))
        {
            return LifecycleStates.Accepted;
        }

        if (string.Equals(status, AnalyticsActionConstants.Statuses.New, StringComparison.OrdinalIgnoreCase)
            && item.DueAtUtc.HasValue
            && NormalizeUtc(item.DueAtUtc.Value) < NormalizeUtc(asOfUtc))
        {
            return LifecycleStates.Ignored;
        }

        return LifecycleStates.Issued;
    }

    public static bool EvaluateLearningEligibility(
        string lifecycleState,
        string outcomeResult,
        string outcomeEvidenceState,
        bool hasMeasurementTimestamp,
        string? evidenceSource,
        ICollection<string>? reasonCodes = null)
    {
        var eligible = true;

        if (!string.Equals(lifecycleState, LifecycleStates.Executed, StringComparison.OrdinalIgnoreCase))
        {
            eligible = false;
            reasonCodes?.Add("execution_required_for_learning");
            if (string.Equals(lifecycleState, LifecycleStates.Accepted, StringComparison.OrdinalIgnoreCase)
                || string.Equals(lifecycleState, LifecycleStates.Issued, StringComparison.OrdinalIgnoreCase)
                || string.Equals(lifecycleState, LifecycleStates.Ignored, StringComparison.OrdinalIgnoreCase)
                || string.Equals(lifecycleState, LifecycleStates.Rejected, StringComparison.OrdinalIgnoreCase))
            {
                reasonCodes?.Add("acceptance_is_not_success");
            }
        }

        if (!string.Equals(outcomeEvidenceState, OutcomeEvidenceStates.Measured, StringComparison.OrdinalIgnoreCase))
        {
            eligible = false;
            reasonCodes?.Add(
                string.Equals(outcomeEvidenceState, OutcomeEvidenceStates.NotMeasured, StringComparison.OrdinalIgnoreCase)
                    ? "outcome_not_measured"
                    : "outcome_pending");
        }

        if (!IsMeasuredOutcomeResult(outcomeResult))
        {
            eligible = false;
            if (reasonCodes is not null && !reasonCodes.Contains("outcome_result_not_measured"))
            {
                reasonCodes.Add("outcome_result_not_measured");
            }
        }

        if (!hasMeasurementTimestamp)
        {
            eligible = false;
            reasonCodes?.Add("missing_outcome_measured_at");
        }

        if (string.IsNullOrWhiteSpace(evidenceSource))
        {
            eligible = false;
            reasonCodes?.Add("missing_evidence_source");
        }

        if (eligible)
        {
            reasonCodes?.Add("measured_learning_eligible");
        }

        return eligible;
    }

    private static string ResolveOutcomeEvidenceState(
        string outcomeResult,
        bool hasMeasurementTimestamp,
        string? evidenceSource)
    {
        if (string.Equals(outcomeResult, AnalyticsActionConstants.OutcomeStatuses.NotMeasured, StringComparison.OrdinalIgnoreCase))
        {
            return OutcomeEvidenceStates.NotMeasured;
        }

        if (IsMeasuredOutcomeResult(outcomeResult)
            && hasMeasurementTimestamp
            && !string.IsNullOrWhiteSpace(evidenceSource))
        {
            return OutcomeEvidenceStates.Measured;
        }

        if (IsMeasuredOutcomeResult(outcomeResult)
            && (!hasMeasurementTimestamp || string.IsNullOrWhiteSpace(evidenceSource)))
        {
            // Claimed result without auditable evidence stays an evidence gap, not measured learning.
            return OutcomeEvidenceStates.NotMeasured;
        }

        return OutcomeEvidenceStates.Pending;
    }

    private static bool IsMeasuredOutcomeResult(string outcomeResult)
        => string.Equals(outcomeResult, AnalyticsActionConstants.OutcomeStatuses.Success, StringComparison.OrdinalIgnoreCase)
            || string.Equals(outcomeResult, AnalyticsActionConstants.OutcomeStatuses.Neutral, StringComparison.OrdinalIgnoreCase)
            || string.Equals(outcomeResult, AnalyticsActionConstants.OutcomeStatuses.Negative, StringComparison.OrdinalIgnoreCase);

    private static string? ResolveEvidenceSource(AnalyticsActionItem item)
    {
        var fromLedger = item.LedgerSnapshot?.ResolutionSnapshot?.EvidenceSource;
        if (!string.IsNullOrWhiteSpace(fromLedger))
        {
            return fromLedger.Trim();
        }

        return TryReadEvidenceSource(item.MetadataJson);
    }

    private static string? TryReadEvidenceSource(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            // Keep Application free of Infrastructure parsers: only probe JSON for evidenceSource.
            using var document = System.Text.Json.JsonDocument.Parse(metadataJson);
            if (!document.RootElement.TryGetProperty("ledger", out var ledger)
                || ledger.ValueKind != System.Text.Json.JsonValueKind.Object
                || !ledger.TryGetProperty("resolutionSnapshot", out var resolution)
                || resolution.ValueKind != System.Text.Json.JsonValueKind.Object
                || !resolution.TryGetProperty("evidenceSource", out var evidenceSourceNode)
                || evidenceSourceNode.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                return null;
            }

            var evidenceSource = evidenceSourceNode.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(evidenceSource) ? null : evidenceSource;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string NormalizeOutcomeStatus(string? outcomeStatus)
        => string.IsNullOrWhiteSpace(outcomeStatus)
            ? AnalyticsActionConstants.OutcomeStatuses.Pending
            : outcomeStatus.Trim();

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
