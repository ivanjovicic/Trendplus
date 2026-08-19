using System.Globalization;
using System.Text;
using Application.Analytics;
using Domain.Model.Analytics;

namespace Infrastructure.Services.Analytics;

/// <summary>
/// DT07 read-only export/report projection over the DT05 Slice-2 filtered timeline.
/// Reuses filter rows; does not rebuild history from live product catalog state.
/// </summary>
public static class DecisionTimelineExportProjection
{
    public const string PeriodModeIssuedAt = "issued_at";
    public const string SnapshotCoveragePresent = "present";
    public const string SnapshotCoverageAbsent = "absent";
    public const string SnapshotCoverageMixed = "mixed";
    public const string WorkflowStatusSourceSnapshot = "snapshot";
    public const string WorkflowStatusSourceLiveLookup = "live_lookup";
    public const string AbsenceLegacyPartialHistory = "legacy_partial_history";
    public const string AbsenceCreationSnapshot = "creation_snapshot_absent";
    public const string AbsenceResolutionSnapshot = "resolution_snapshot_absent";
    public const string AbsenceEvidenceSnapshot = "evidence_snapshot_absent";

    public static DecisionTimelineExportDto FromFilter(
        DecisionTimelineFilterResponseDto filtered,
        DateTime generatedAtUtc,
        string? dataQualityStatus = null,
        string? freshnessStatus = null)
    {
        ArgumentNullException.ThrowIfNull(filtered);

        var periodFromUtc = NormalizeDateUtc(filtered.Scope.PeriodFromUtc);
        var periodToUtc = NormalizeDateUtc(filtered.Scope.PeriodToUtc);
        var rows = filtered.Timelines.Select(ToExportRow).ToArray();
        var funnel = BuildFunnel(rows);
        var snapshotCoverage = ResolveSnapshotCoverage(rows);
        var resolvedFreshness = freshnessStatus ?? ResolveFreshness(rows);
        var resolvedQuality = dataQualityStatus ?? ResolveDataQuality(filtered, rows.Length);
        var warningCodes = filtered.WarningCodes.ToArray();

        return new DecisionTimelineExportDto(
            Success: true,
            Header: new DecisionTimelineExportHonestyHeaderDto(
                RequestedPeriodFromUtc: periodFromUtc,
                RequestedPeriodToUtc: periodToUtc,
                EffectivePeriodFromUtc: periodFromUtc,
                EffectivePeriodToUtc: periodToUtc,
                PeriodMode: PeriodModeIssuedAt,
                GeneratedAtUtc: generatedAtUtc,
                FreshnessStatus: resolvedFreshness,
                DataQualityStatus: resolvedQuality,
                EmptyReason: filtered.EmptyReason,
                WarningCodes: warningCodes,
                SnapshotCoverage: snapshotCoverage,
                ScopeExplanation: filtered.Scope.ScopeExplanation,
                MatchedActionCount: filtered.MatchedActionCount,
                MatchedEventCount: filtered.MatchedEventCount),
            Funnel: funnel,
            Rows: rows,
            ErrorCode: null,
            ErrorMessage: null);
    }

    public static DecisionTimelineExportDto Error(
        DateTime requestedPeriodFromUtc,
        DateTime requestedPeriodToUtc,
        DateTime generatedAtUtc,
        string errorCode,
        string errorMessage,
        IReadOnlyList<string>? warningCodes = null)
    {
        var periodFromUtc = NormalizeDateUtc(requestedPeriodFromUtc);
        var periodToUtc = NormalizeDateUtc(requestedPeriodToUtc);
        return new DecisionTimelineExportDto(
            Success: false,
            Header: new DecisionTimelineExportHonestyHeaderDto(
                RequestedPeriodFromUtc: periodFromUtc,
                RequestedPeriodToUtc: periodToUtc,
                EffectivePeriodFromUtc: periodFromUtc,
                EffectivePeriodToUtc: periodToUtc,
                PeriodMode: PeriodModeIssuedAt,
                GeneratedAtUtc: generatedAtUtc,
                FreshnessStatus: null,
                DataQualityStatus: null,
                EmptyReason: null,
                WarningCodes: warningCodes ?? ["export_failed"],
                SnapshotCoverage: SnapshotCoverageAbsent,
                ScopeExplanation: null,
                MatchedActionCount: null,
                MatchedEventCount: null),
            Funnel: null,
            Rows: Array.Empty<DecisionTimelineExportRowDto>(),
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    public static string ToCsv(DecisionTimelineExportDto export)
    {
        ArgumentNullException.ThrowIfNull(export);

        var builder = new StringBuilder();
        AppendHeaderComment(builder, "success", export.Success ? "true" : "false");
        AppendHeaderComment(builder, "requestedPeriodFromUtc", FormatDate(export.Header.RequestedPeriodFromUtc));
        AppendHeaderComment(builder, "requestedPeriodToUtc", FormatDate(export.Header.RequestedPeriodToUtc));
        AppendHeaderComment(builder, "effectivePeriodFromUtc", FormatDate(export.Header.EffectivePeriodFromUtc));
        AppendHeaderComment(builder, "effectivePeriodToUtc", FormatDate(export.Header.EffectivePeriodToUtc));
        AppendHeaderComment(builder, "periodMode", export.Header.PeriodMode);
        AppendHeaderComment(builder, "generatedAtUtc", FormatTimestamp(export.Header.GeneratedAtUtc));
        AppendHeaderComment(builder, "freshnessStatus", export.Header.FreshnessStatus);
        AppendHeaderComment(builder, "dataQualityStatus", export.Header.DataQualityStatus);
        AppendHeaderComment(builder, "emptyReason", export.Header.EmptyReason);
        AppendHeaderComment(builder, "warningCodes", string.Join("|", export.Header.WarningCodes));
        AppendHeaderComment(builder, "snapshotCoverage", export.Header.SnapshotCoverage);
        AppendHeaderComment(builder, "scopeExplanation", export.Header.ScopeExplanation);
        AppendHeaderComment(builder, "errorCode", export.ErrorCode);
        AppendHeaderComment(builder, "errorMessage", export.ErrorMessage);

        if (!export.Success || export.Funnel is null)
        {
            // Failed export must not emit a KPI/rate table of zeros.
            return builder.ToString();
        }

        AppendHeaderComment(builder, "matchedActionCount", export.Header.MatchedActionCount?.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "matchedEventCount", export.Header.MatchedEventCount?.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "issuedCount", export.Funnel.IssuedCount.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "acceptedCount", export.Funnel.AcceptedCount.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "rejectedCount", export.Funnel.RejectedCount.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "ignoredCount", export.Funnel.IgnoredCount.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "executedCount", export.Funnel.ExecutedCount.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "measuredCount", export.Funnel.MeasuredCount.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "notMeasuredCount", export.Funnel.NotMeasuredCount.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "successCount", export.Funnel.SuccessCount.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "neutralCount", export.Funnel.NeutralCount.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "negativeCount", export.Funnel.NegativeCount.ToString(CultureInfo.InvariantCulture));
        AppendHeaderComment(builder, "acceptanceRate", FormatRate(export.Funnel.AcceptanceRate));
        AppendHeaderComment(builder, "executionRate", FormatRate(export.Funnel.ExecutionRate));
        AppendHeaderComment(builder, "measuredRate", FormatRate(export.Funnel.MeasuredRate));
        AppendHeaderComment(builder, "successRate", FormatRate(export.Funnel.SuccessRate));
        AppendHeaderComment(builder, "methodology", "projection-first Slice-2; no replay store; snapshots optional but explicit");

        builder.AppendLine("timelineId,actionId,sourceRecommendationId,correlationId,sourceType,sourceKey,recommendationType,issuedAtUtc,acceptedAtUtc,rejectedAtUtc,executedAtUtc,outcomeMeasuredAtUtc,currentStatus,currentOutcomeStatus,workflowStatusSource,eventTypes,gapReasons,creationSnapshotPresent,resolutionSnapshotPresent,evidenceSnapshotPresent,snapshotAbsenceReason");
        foreach (var row in export.Rows)
        {
            builder.Append(Csv(row.TimelineId)).Append(',');
            builder.Append(row.ActionId.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append(Csv(row.SourceRecommendationId)).Append(',');
            builder.Append(Csv(row.CorrelationId)).Append(',');
            builder.Append(Csv(row.SourceType)).Append(',');
            builder.Append(Csv(row.SourceKey)).Append(',');
            builder.Append(Csv(row.RecommendationType)).Append(',');
            builder.Append(Csv(FormatTimestamp(row.IssuedAtUtc))).Append(',');
            builder.Append(Csv(FormatTimestamp(row.AcceptedAtUtc))).Append(',');
            builder.Append(Csv(FormatTimestamp(row.RejectedAtUtc))).Append(',');
            builder.Append(Csv(FormatTimestamp(row.ExecutedAtUtc))).Append(',');
            builder.Append(Csv(FormatTimestamp(row.OutcomeMeasuredAtUtc))).Append(',');
            builder.Append(Csv(row.CurrentStatus)).Append(',');
            builder.Append(Csv(row.CurrentOutcomeStatus)).Append(',');
            builder.Append(Csv(row.WorkflowStatusSource)).Append(',');
            builder.Append(Csv(string.Join("|", row.Events.Select(item => item.EventType)))).Append(',');
            builder.Append(Csv(string.Join("|", row.Gaps.Select(item => item.GapReason)))).Append(',');
            builder.Append(row.CreationSnapshotPresent ? "true" : "false").Append(',');
            builder.Append(row.ResolutionSnapshotPresent ? "true" : "false").Append(',');
            builder.Append(row.EvidenceSnapshotPresent ? "true" : "false").Append(',');
            builder.AppendLine(Csv(row.SnapshotAbsenceReason));
        }

        return builder.ToString();
    }

    private static DecisionTimelineExportRowDto ToExportRow(DecisionTimelineItemDto timeline)
    {
        var ledger = timeline.LedgerSnapshot;
        var creationPresent = ledger?.CreationSnapshot is not null;
        var resolutionPresent = ledger?.ResolutionSnapshot is not null;
        var evidencePresent = ledger?.EvidenceSnapshot is not null;
        var workflowStatusSource = creationPresent || resolutionPresent || evidencePresent
            ? WorkflowStatusSourceSnapshot
            : WorkflowStatusSourceLiveLookup;

        return new DecisionTimelineExportRowDto(
            TimelineId: timeline.TimelineId,
            ActionId: timeline.ActionId,
            SourceRecommendationId: timeline.SourceRecommendationId,
            CorrelationId: timeline.CorrelationId,
            SourceType: timeline.SourceType,
            SourceKey: timeline.SourceKey,
            RecommendationType: timeline.RecommendationType,
            ProjectionState: timeline.ProjectionState,
            IssuedAtUtc: timeline.IssuedAtUtc,
            AcceptedAtUtc: FindEventTime(timeline.Events, "action_accepted"),
            RejectedAtUtc: FindEventTime(timeline.Events, "action_rejected"),
            ExecutedAtUtc: FindEventTime(timeline.Events, "action_executed"),
            OutcomeMeasuredAtUtc: FindEventTime(timeline.Events, "outcome_measured"),
            CurrentStatus: timeline.CurrentStatus,
            CurrentOutcomeStatus: timeline.CurrentOutcomeStatus,
            WorkflowStatusSource: workflowStatusSource,
            Events: timeline.Events,
            Gaps: timeline.Gaps,
            CreationSnapshotPresent: creationPresent,
            ResolutionSnapshotPresent: resolutionPresent,
            EvidenceSnapshotPresent: evidencePresent,
            SnapshotAbsenceReason: ResolveAbsenceReason(ledger, creationPresent, resolutionPresent, evidencePresent),
            CreationSnapshot: ledger?.CreationSnapshot,
            ResolutionSnapshot: ledger?.ResolutionSnapshot,
            EvidenceSnapshot: ledger?.EvidenceSnapshot);
    }

    private static DecisionTimelineExportFunnelDto BuildFunnel(IReadOnlyList<DecisionTimelineExportRowDto> rows)
    {
        var issuedCount = rows.Count;
        var acceptedCount = rows.Count(row => row.Events.Any(item => EventIs(item, "action_accepted")));
        var rejectedCount = rows.Count(row => row.Events.Any(item => EventIs(item, "action_rejected")));
        var ignoredCount = rows.Count(row =>
            string.Equals(row.CurrentStatus, RecommendationLifecycleSemantics.LifecycleStates.Ignored, StringComparison.OrdinalIgnoreCase)
            || row.Events.Any(item => EventIs(item, "action_ignored")));
        var executedCount = rows.Count(row => row.Events.Any(item => EventIs(item, "action_executed")));
        var measuredCount = rows.Count(row => row.Events.Any(item => EventIs(item, "outcome_measured")));
        var notMeasuredCount = rows.Count(row => row.Events.Any(item => EventIs(item, "outcome_not_measured")));
        var successCount = CountOutcome(rows, AnalyticsActionConstants.OutcomeStatuses.Success);
        var neutralCount = CountOutcome(rows, AnalyticsActionConstants.OutcomeStatuses.Neutral);
        var negativeCount = CountOutcome(rows, AnalyticsActionConstants.OutcomeStatuses.Negative);

        return new DecisionTimelineExportFunnelDto(
            IssuedCount: issuedCount,
            AcceptedCount: acceptedCount,
            RejectedCount: rejectedCount,
            IgnoredCount: ignoredCount,
            ExecutedCount: executedCount,
            MeasuredCount: measuredCount,
            NotMeasuredCount: notMeasuredCount,
            SuccessCount: successCount,
            NeutralCount: neutralCount,
            NegativeCount: negativeCount,
            AcceptanceRate: Rate(acceptedCount, issuedCount),
            ExecutionRate: Rate(executedCount, acceptedCount),
            MeasuredRate: Rate(measuredCount, executedCount),
            SuccessRate: Rate(successCount, measuredCount));
    }

    private static string ResolveSnapshotCoverage(IReadOnlyList<DecisionTimelineExportRowDto> rows)
    {
        if (rows.Count == 0)
        {
            return SnapshotCoverageAbsent;
        }

        var presentCount = rows.Count(row =>
            row.CreationSnapshotPresent && row.ResolutionSnapshotPresent && row.EvidenceSnapshotPresent);
        if (presentCount == rows.Count)
        {
            return SnapshotCoveragePresent;
        }

        var absentCount = rows.Count(row =>
            !row.CreationSnapshotPresent && !row.ResolutionSnapshotPresent && !row.EvidenceSnapshotPresent);
        return absentCount == rows.Count ? SnapshotCoverageAbsent : SnapshotCoverageMixed;
    }

    private static string? ResolveFreshness(IReadOnlyList<DecisionTimelineExportRowDto> rows)
    {
        var values = rows
            .Select(row => row.CreationSnapshot?.InputFreshnessStatus ?? row.EvidenceSnapshot?.InputFreshnessStatus)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        return values.Length == 1 ? values[0] : "mixed";
    }

    private static string ResolveDataQuality(DecisionTimelineFilterResponseDto filtered, int rowCount)
    {
        if (rowCount == 0)
        {
            return "insufficient_data";
        }

        if (filtered.WarningCodes.Contains(AnalyticsActionTimelineFilterProjection.EmptyReasonNoMeasurement, StringComparer.OrdinalIgnoreCase))
        {
            return "warning";
        }

        return "good";
    }

    private static string? ResolveAbsenceReason(
        AnalyticsActionLedgerSnapshot? ledger,
        bool creationPresent,
        bool resolutionPresent,
        bool evidencePresent)
    {
        if (creationPresent && resolutionPresent && evidencePresent)
        {
            return null;
        }

        if (ledger is null)
        {
            return AbsenceLegacyPartialHistory;
        }

        var reasons = new List<string>();
        if (!creationPresent)
        {
            reasons.Add(AbsenceCreationSnapshot);
        }

        if (!resolutionPresent)
        {
            reasons.Add(AbsenceResolutionSnapshot);
        }

        if (!evidencePresent)
        {
            reasons.Add(AbsenceEvidenceSnapshot);
        }

        return reasons.Count == 0 ? null : string.Join(",", reasons);
    }

    private static DateTime? FindEventTime(IReadOnlyList<AnalyticsActionTimelineEventDto> events, string eventType)
        => events.FirstOrDefault(item => EventIs(item, eventType))?.OccurredAtUtc;

    private static bool EventIs(AnalyticsActionTimelineEventDto item, string eventType)
        => string.Equals(item.EventType, eventType, StringComparison.OrdinalIgnoreCase);

    private static int CountOutcome(IReadOnlyList<DecisionTimelineExportRowDto> rows, string outcome)
        => rows.Count(row =>
            row.Events.Any(item => EventIs(item, "outcome_measured"))
            && string.Equals(row.CurrentOutcomeStatus, outcome, StringComparison.OrdinalIgnoreCase));

    private static decimal? Rate(int numerator, int denominator)
        => denominator <= 0 ? null : decimal.Round((decimal)numerator / denominator, 4, MidpointRounding.AwayFromZero);

    private static DateTime NormalizeDateUtc(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        return DateTime.SpecifyKind(utc.Date, DateTimeKind.Utc);
    }

    private static void AppendHeaderComment(StringBuilder builder, string key, string? value)
        => builder.Append('#').Append(' ').Append(key).Append('=').Append(value ?? string.Empty).AppendLine();

    private static string FormatDate(DateTime value)
        => NormalizeDateUtc(value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? FormatTimestamp(DateTime? value)
        => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string FormatRate(decimal? value)
        => value.HasValue ? value.Value.ToString("0.####", CultureInfo.InvariantCulture) : string.Empty;

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}

public sealed record DecisionTimelineExportDto(
    bool Success,
    DecisionTimelineExportHonestyHeaderDto Header,
    DecisionTimelineExportFunnelDto? Funnel,
    IReadOnlyList<DecisionTimelineExportRowDto> Rows,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record DecisionTimelineExportHonestyHeaderDto(
    DateTime RequestedPeriodFromUtc,
    DateTime RequestedPeriodToUtc,
    DateTime EffectivePeriodFromUtc,
    DateTime EffectivePeriodToUtc,
    string PeriodMode,
    DateTime GeneratedAtUtc,
    string? FreshnessStatus,
    string? DataQualityStatus,
    string? EmptyReason,
    IReadOnlyList<string> WarningCodes,
    string SnapshotCoverage,
    string? ScopeExplanation,
    int? MatchedActionCount,
    int? MatchedEventCount);

public sealed record DecisionTimelineExportFunnelDto(
    int IssuedCount,
    int AcceptedCount,
    int RejectedCount,
    int IgnoredCount,
    int ExecutedCount,
    int MeasuredCount,
    int NotMeasuredCount,
    int SuccessCount,
    int NeutralCount,
    int NegativeCount,
    decimal? AcceptanceRate,
    decimal? ExecutionRate,
    decimal? MeasuredRate,
    decimal? SuccessRate);

public sealed record DecisionTimelineExportRowDto(
    string TimelineId,
    long ActionId,
    string SourceRecommendationId,
    string CorrelationId,
    string SourceType,
    string SourceKey,
    string? RecommendationType,
    string ProjectionState,
    DateTime IssuedAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? RejectedAtUtc,
    DateTime? ExecutedAtUtc,
    DateTime? OutcomeMeasuredAtUtc,
    string CurrentStatus,
    string CurrentOutcomeStatus,
    string WorkflowStatusSource,
    IReadOnlyList<AnalyticsActionTimelineEventDto> Events,
    IReadOnlyList<AnalyticsActionTimelineGapDto> Gaps,
    bool CreationSnapshotPresent,
    bool ResolutionSnapshotPresent,
    bool EvidenceSnapshotPresent,
    string? SnapshotAbsenceReason,
    AnalyticsActionCreationSnapshot? CreationSnapshot,
    AnalyticsActionResolutionSnapshot? ResolutionSnapshot,
    AnalyticsActionDecisionEvidenceSnapshot? EvidenceSnapshot);
