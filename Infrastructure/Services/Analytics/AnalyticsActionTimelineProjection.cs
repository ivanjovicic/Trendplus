using Application.Analytics;
using Domain.Model.Analytics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Infrastructure.Services.Analytics;

public static class AnalyticsActionTimelineProjection
{
    private const string EventRecommendationIssued = "recommendation_issued";
    private const string EventActionAccepted = "action_accepted";
    private const string EventActionRejected = "action_rejected";
    private const string EventActionExecuted = "action_executed";
    private const string EventOutcomeMeasured = "outcome_measured";
    private const string EventOutcomeNotMeasured = "outcome_not_measured";

    private const string StageRecommendation = "recommendation";
    private const string StageWorkflow = "workflow";
    private const string StageOutcome = "outcome";

    private const string GapNoAcceptanceRecord = "no_acceptance_record";
    private const string GapNoExecutionProof = "no_execution_proof";
    private const string GapNoMeasurementEvidence = "no_measurement_evidence";
    private const string GapLegacyPartialHistory = "legacy_partial_history";

    public static AnalyticsActionTimelineProjectionDto Project(AnalyticsActionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var ledgerSnapshot = item.LedgerSnapshot ?? AnalyticsActionItemService.GetLedgerSnapshot(item.MetadataJson);
        var creationSnapshot = ledgerSnapshot?.CreationSnapshot;
        var resolutionSnapshot = ledgerSnapshot?.ResolutionSnapshot;
        string? fallbackRecommendationType = null;
        DateTime? fallbackGeneratedAtUtc = null;
        if (creationSnapshot is null && TryGetPartialCreationMetadata(item.MetadataJson, out _, out var partialRecommendationType, out var partialGeneratedAtUtc))
        {
            fallbackRecommendationType = partialRecommendationType;
            fallbackGeneratedAtUtc = partialGeneratedAtUtc;
        }
        var orderedNotes = (item.Notes ?? Array.Empty<AnalyticsActionNote>())
            .OrderBy(note => note.CreatedAtUtc)
            .ThenBy(note => note.Id)
            .ToArray();

        var issuedAtUtc = creationSnapshot?.GeneratedAtUtc ?? fallbackGeneratedAtUtc ?? item.CreatedAtUtc;
        var sourceRecommendationId = ResolveSourceRecommendationId(
            item,
            creationSnapshot,
            fallbackRecommendationType,
            fallbackGeneratedAtUtc ?? issuedAtUtc,
            out var sourceRecommendationIdDerivation);
        var correlationId = sourceRecommendationId;
        var correlationIdDerivation = sourceRecommendationIdDerivation;

        var events = new List<AnalyticsActionTimelineEventDto>
        {
            new(
                EventRecommendationIssued,
                StageRecommendation,
                issuedAtUtc,
                "issued",
                sourceRecommendationId,
                correlationId,
                correlationIdDerivation,
                EvidenceSource: null,
                EvidenceReference: null,
                MeasurementWindowDays: null)
        };

        var gaps = new List<AnalyticsActionTimelineGapDto>();
        var acceptedNotes = new List<AnalyticsActionNote>();
        var rejectedNotes = new List<AnalyticsActionNote>();
        var executedNotes = new List<AnalyticsActionNote>();

        foreach (var note in orderedNotes)
        {
            if (string.Equals(note.StatusFrom, note.StatusTo, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(note.StatusTo, AnalyticsActionConstants.Statuses.Accepted, StringComparison.OrdinalIgnoreCase))
            {
                acceptedNotes.Add(note);
            }
            else if (string.Equals(note.StatusTo, AnalyticsActionConstants.Statuses.Rejected, StringComparison.OrdinalIgnoreCase))
            {
                rejectedNotes.Add(note);
            }
            else if (string.Equals(note.StatusTo, AnalyticsActionConstants.Statuses.Done, StringComparison.OrdinalIgnoreCase))
            {
                executedNotes.Add(note);
            }
        }

        if (acceptedNotes.Count > 0)
        {
            foreach (var note in acceptedNotes)
            {
                events.Add(CreateWorkflowEvent(EventActionAccepted, note.CreatedAtUtc, note, sourceRecommendationId, correlationId, correlationIdDerivation));
            }
        }
        else
        {
            gaps.Add(new AnalyticsActionTimelineGapDto(StageWorkflow, GapNoAcceptanceRecord, "No acceptance note was captured."));
        }

        if (rejectedNotes.Count > 0)
        {
            foreach (var note in rejectedNotes)
            {
                events.Add(CreateWorkflowEvent(EventActionRejected, note.CreatedAtUtc, note, sourceRecommendationId, correlationId, correlationIdDerivation));
            }
        }

        if (executedNotes.Count > 0)
        {
            foreach (var note in executedNotes)
            {
                events.Add(CreateWorkflowEvent(EventActionExecuted, note.CreatedAtUtc, note, sourceRecommendationId, correlationId, correlationIdDerivation));
            }
        }
        else if (string.Equals(item.Status, AnalyticsActionConstants.Statuses.Done, StringComparison.OrdinalIgnoreCase))
        {
            gaps.Add(new AnalyticsActionTimelineGapDto(StageWorkflow, GapLegacyPartialHistory, "The action is closed as done, but no execution note was captured."));
        }
        else if (acceptedNotes.Count > 0 && !string.Equals(item.Status, AnalyticsActionConstants.Statuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            gaps.Add(new AnalyticsActionTimelineGapDto(StageWorkflow, GapNoExecutionProof, "The action was accepted, but execution proof is missing."));
        }

        var outcomeStatus = NormalizeOutcomeStatus(item.OutcomeStatus);
        if (string.Equals(outcomeStatus, AnalyticsActionConstants.OutcomeStatuses.NotMeasured, StringComparison.OrdinalIgnoreCase))
        {
            events.Add(new AnalyticsActionTimelineEventDto(
                EventOutcomeNotMeasured,
                StageOutcome,
                item.ResolvedAtUtc ?? item.UpdatedAtUtc,
                outcomeStatus,
                sourceRecommendationId,
                correlationId,
                correlationIdDerivation,
                EvidenceSource: resolutionSnapshot?.EvidenceSource,
                EvidenceReference: resolutionSnapshot?.EvidenceReference,
                MeasurementWindowDays: resolutionSnapshot?.MeasuredWindowDays));
            gaps.Add(new AnalyticsActionTimelineGapDto(StageOutcome, GapNoMeasurementEvidence, "The row is explicitly not measured."));
        }
        else if (string.Equals(outcomeStatus, AnalyticsActionConstants.OutcomeStatuses.Success, StringComparison.OrdinalIgnoreCase)
            || string.Equals(outcomeStatus, AnalyticsActionConstants.OutcomeStatuses.Neutral, StringComparison.OrdinalIgnoreCase)
            || string.Equals(outcomeStatus, AnalyticsActionConstants.OutcomeStatuses.Negative, StringComparison.OrdinalIgnoreCase))
        {
            if (item.OutcomeMeasuredAtUtc.HasValue)
            {
                events.Add(new AnalyticsActionTimelineEventDto(
                    EventOutcomeMeasured,
                    StageOutcome,
                    item.OutcomeMeasuredAtUtc.Value,
                    outcomeStatus,
                    sourceRecommendationId,
                    correlationId,
                    correlationIdDerivation,
                    EvidenceSource: resolutionSnapshot?.EvidenceSource,
                    EvidenceReference: resolutionSnapshot?.EvidenceReference,
                    MeasurementWindowDays: resolutionSnapshot?.MeasuredWindowDays));
            }
            else
            {
                gaps.Add(new AnalyticsActionTimelineGapDto(StageOutcome, GapNoMeasurementEvidence, "The outcome status is measured, but the measurement timestamp is missing."));
            }
        }
        else
        {
            gaps.Add(new AnalyticsActionTimelineGapDto(StageOutcome, GapNoMeasurementEvidence, "The row does not have measurable outcome evidence."));
        }

        if (orderedNotes.Length == 0 && (item.Status != AnalyticsActionConstants.Statuses.New || !string.Equals(outcomeStatus, AnalyticsActionConstants.OutcomeStatuses.Pending, StringComparison.OrdinalIgnoreCase)))
        {
            gaps.Add(new AnalyticsActionTimelineGapDto(StageRecommendation, GapLegacyPartialHistory, "The row has no note history, so the timeline is only partially reconstructed."));
        }

        var projectionState = ResolveProjectionState(item.Status, outcomeStatus);

        return new AnalyticsActionTimelineProjectionDto(
            ActionId: item.Id,
            SourceType: item.SourceType,
            SourceKey: item.SourceKey,
            SourceRecommendationId: sourceRecommendationId,
            SourceRecommendationIdDerivation: sourceRecommendationIdDerivation,
            RecommendationType: creationSnapshot?.RecommendationType,
            CorrelationId: correlationId,
            CorrelationIdDerivation: correlationIdDerivation,
            ProjectionState: projectionState,
            IssuedAtUtc: issuedAtUtc,
            CurrentStatus: item.Status,
            CurrentOutcomeStatus: outcomeStatus,
            LedgerSnapshot: ledgerSnapshot,
            Events: events
                .OrderBy(eventItem => eventItem.OccurredAtUtc)
                .ThenBy(eventItem => TimelineStageOrder(eventItem.Stage))
                .ThenBy(eventItem => eventItem.EventType, StringComparer.Ordinal)
                .ToArray(),
            Gaps: gaps
                .GroupBy(gap => (gap.Stage, gap.GapReason, gap.Message))
                .Select(group => group.First())
                .ToArray());
    }

    private static AnalyticsActionTimelineEventDto CreateWorkflowEvent(
        string eventType,
        DateTime occurredAtUtc,
        AnalyticsActionNote note,
        string sourceRecommendationId,
        string correlationId,
        string correlationIdDerivation)
    {
        return new AnalyticsActionTimelineEventDto(
            eventType,
            StageWorkflow,
            occurredAtUtc,
            note.StatusTo,
            sourceRecommendationId,
            correlationId,
            correlationIdDerivation,
            EvidenceSource: null,
            EvidenceReference: null,
            MeasurementWindowDays: null);
    }

    private static string ResolveSourceRecommendationId(
        AnalyticsActionItem item,
        AnalyticsActionCreationSnapshot? creationSnapshot,
        string? fallbackRecommendationType,
        DateTime issuedAtUtc,
        out string derivation)
    {
        var explicitSourceRecommendationId = creationSnapshot?.SourceRecommendationId?.Trim();
        if (!string.IsNullOrWhiteSpace(explicitSourceRecommendationId))
        {
            derivation = "metadata.sourceRecommendationId";
            return explicitSourceRecommendationId!;
        }

        var recommendationType = creationSnapshot?.RecommendationType?.Trim() ?? fallbackRecommendationType?.Trim();
        var issuedStamp = NormalizeUtc(issuedAtUtc).ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture);
        var sourceRecommendationId = recommendationType is null
            ? $"{item.SourceType}:{item.SourceKey}:{issuedStamp}"
            : $"{item.SourceType}:{item.SourceKey}:{recommendationType}:{issuedStamp}";
        derivation = recommendationType is null
            ? "derived.sourceType.sourceKey.issuedAtUtc"
            : "derived.sourceType.sourceKey.recommendationType.issuedAtUtc";
        return sourceRecommendationId;
    }

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static bool TryGetPartialCreationMetadata(
        string? metadataJson,
        out string? sourceRecommendationId,
        out string? recommendationType,
        out DateTime? generatedAtUtc)
    {
        sourceRecommendationId = null;
        recommendationType = null;
        generatedAtUtc = null;

        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return false;
        }

        try
        {
            var parsed = JsonNode.Parse(metadataJson);
            if (parsed is not JsonObject root || root["ledger"] is not JsonObject ledger || ledger["creationSnapshot"] is not JsonObject creationNode)
            {
                return false;
            }

            sourceRecommendationId = creationNode["sourceRecommendationId"]?.GetValue<string>()?.Trim();
            recommendationType = creationNode["recommendationType"]?.GetValue<string>()?.Trim();
            generatedAtUtc = creationNode["generatedAtUtc"]?.GetValue<DateTime?>();
            return !string.IsNullOrWhiteSpace(sourceRecommendationId)
                || !string.IsNullOrWhiteSpace(recommendationType)
                || generatedAtUtc.HasValue;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ResolveProjectionState(string workflowStatus, string? outcomeStatus)
    {
        if (string.Equals(outcomeStatus, AnalyticsActionConstants.OutcomeStatuses.NotMeasured, StringComparison.OrdinalIgnoreCase))
        {
            return AnalyticsActionConstants.OutcomeStatuses.NotMeasured;
        }

        if (string.Equals(workflowStatus, AnalyticsActionConstants.Statuses.Done, StringComparison.OrdinalIgnoreCase))
        {
            return AnalyticsActionConstants.Statuses.Done;
        }

        if (string.Equals(workflowStatus, AnalyticsActionConstants.Statuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            return AnalyticsActionConstants.Statuses.Rejected;
        }

        return AnalyticsActionConstants.OutcomeStatuses.Pending;
    }

    private static string NormalizeOutcomeStatus(string? outcomeStatus)
        => string.IsNullOrWhiteSpace(outcomeStatus) ? AnalyticsActionConstants.OutcomeStatuses.Pending : outcomeStatus.Trim();

    private static int CompareEvents(AnalyticsActionTimelineEventDto left, AnalyticsActionTimelineEventDto right)
    {
        var timeComparison = left.OccurredAtUtc.CompareTo(right.OccurredAtUtc);
        if (timeComparison != 0)
        {
            return timeComparison;
        }

        var stageComparison = TimelineStageOrder(left.Stage).CompareTo(TimelineStageOrder(right.Stage));
        if (stageComparison != 0)
        {
            return stageComparison;
        }

        return StringComparer.Ordinal.Compare(left.EventType, right.EventType);
    }

    private static int TimelineStageOrder(string stage)
        => stage switch
        {
            StageRecommendation => 0,
            StageWorkflow => 1,
            StageOutcome => 2,
            _ => 3,
        };
}

public sealed record AnalyticsActionTimelineProjectionDto(
    long ActionId,
    string SourceType,
    string SourceKey,
    string SourceRecommendationId,
    string SourceRecommendationIdDerivation,
    string? RecommendationType,
    string CorrelationId,
    string CorrelationIdDerivation,
    string ProjectionState,
    DateTime IssuedAtUtc,
    string CurrentStatus,
    string CurrentOutcomeStatus,
    AnalyticsActionLedgerSnapshot? LedgerSnapshot,
    IReadOnlyList<AnalyticsActionTimelineEventDto> Events,
    IReadOnlyList<AnalyticsActionTimelineGapDto> Gaps);

public sealed record AnalyticsActionTimelineEventDto(
    string EventType,
    string Stage,
    DateTime OccurredAtUtc,
    string? Status,
    string SourceRecommendationId,
    string CorrelationId,
    string CorrelationIdDerivation,
    string? EvidenceSource,
    string? EvidenceReference,
    int? MeasurementWindowDays);

public sealed record AnalyticsActionTimelineGapDto(
    string Stage,
    string GapReason,
    string Message);
