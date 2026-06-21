using Application.Analytics;
using Domain.Model.Analytics;
using Infrastructure.Services.Analytics;
using Microsoft.AspNetCore.Http;
using Trendplus2.Endpoints;

namespace Api.Endpoints;

public static class AnalyticsActionsEndpoints
{
    public static void MapAnalyticsActionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics/actions")
            .WithTags("Analytics");

        // GET /api/analytics/actions
        group.MapGet("/", async (
            AnalyticsActionItemService svc,
            string? status,
            string? priority,
            string? sourceType,
            string? dataQualityStatus,
            string? search,
            int page = 1,
            int pageSize = 50,
            CancellationToken ct = default) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            if (!string.IsNullOrWhiteSpace(status) && !AnalyticsActionConstants.IsValidStatus(status))
                return Results.BadRequest($"status must be one of: {string.Join(", ", AnalyticsActionConstants.Statuses.AllValues)}");

            if (!string.IsNullOrWhiteSpace(priority) && !AnalyticsActionConstants.IsValidPriority(priority))
                return Results.BadRequest($"priority must be one of: {string.Join(", ", AnalyticsActionConstants.Priorities.AllValues)}");

            if (!string.IsNullOrWhiteSpace(sourceType) && !AnalyticsActionConstants.IsValidSourceType(sourceType))
                return Results.BadRequest($"sourceType must be one of: {string.Join(", ", AnalyticsActionConstants.SourceTypes.AllValues)}");

            var normalizedDataQualityStatus = AnalyticsActionConstants.NormalizeDataQualityStatus(dataQualityStatus);
            if (normalizedDataQualityStatus != null && !AnalyticsActionConstants.IsValidDataQualityStatus(normalizedDataQualityStatus))
                return Results.BadRequest($"dataQualityStatus must be one of: {string.Join(", ", AnalyticsActionConstants.DataQualityStatuses.AllValues)}");

            var (items, totalCount) = await svc.ListAsync(status, priority, sourceType, normalizedDataQualityStatus, search, page, pageSize, ct);

            return Results.Ok(new
            {
                items,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        })
        .WithName("GetAnalyticsActions");

        // GET /api/analytics/actions/counts
        group.MapGet("/counts", async (
            AnalyticsActionItemService svc,
            CancellationToken ct) =>
        {
            var counts = await svc.GetCountsAsync(ct);
            return Results.Ok(counts);
        })
        .WithName("GetAnalyticsActionCounts");

        // GET /api/analytics/actions/outcomes/summary
        group.MapGet("/outcomes/summary", async (
            AnalyticsActionItemService svc,
            DateTime? createdFrom,
            DateTime? createdTo,
            DateTime? resolvedFrom,
            DateTime? resolvedTo,
            DateTime? measuredFrom,
            DateTime? measuredTo,
            string? sourceType,
            string? priority,
            string? dataQualityStatus,
            CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(sourceType) && !AnalyticsActionConstants.IsValidSourceType(sourceType))
                return Results.BadRequest($"sourceType must be one of: {string.Join(", ", AnalyticsActionConstants.SourceTypes.AllValues)}");

            if (!string.IsNullOrWhiteSpace(priority) && !AnalyticsActionConstants.IsValidPriority(priority))
                return Results.BadRequest($"priority must be one of: {string.Join(", ", AnalyticsActionConstants.Priorities.AllValues)}");

            var normalizedDataQualityStatus = AnalyticsActionConstants.NormalizeDataQualityStatus(dataQualityStatus);
            if (normalizedDataQualityStatus != null && !AnalyticsActionConstants.IsValidDataQualityStatus(normalizedDataQualityStatus))
                return Results.BadRequest($"dataQualityStatus must be one of: {string.Join(", ", AnalyticsActionConstants.DataQualityStatuses.AllValues)}");

            if (createdFrom.HasValue && createdTo.HasValue && createdFrom > createdTo)
                return Results.BadRequest("createdFrom must be earlier than or equal to createdTo");

            if (resolvedFrom.HasValue && resolvedTo.HasValue && resolvedFrom > resolvedTo)
                return Results.BadRequest("resolvedFrom must be earlier than or equal to resolvedTo");

            if (measuredFrom.HasValue && measuredTo.HasValue && measuredFrom > measuredTo)
                return Results.BadRequest("measuredFrom must be earlier than or equal to measuredTo");

            var effectiveCreatedFrom = createdFrom;
            var effectiveCreatedTo = createdTo;
            if (!createdFrom.HasValue && !createdTo.HasValue && !resolvedFrom.HasValue && !resolvedTo.HasValue && !measuredFrom.HasValue && !measuredTo.HasValue)
            {
                effectiveCreatedFrom = DateTime.UtcNow.AddDays(-90);
                effectiveCreatedTo = DateTime.UtcNow;
            }

            var summary = await svc.GetOutcomeSummaryAsync(
                new AnalyticsActionOutcomeSummaryQuery(
                    CreatedFrom: effectiveCreatedFrom,
                    CreatedTo: effectiveCreatedTo,
                    ResolvedFrom: resolvedFrom,
                    ResolvedTo: resolvedTo,
                    MeasuredFrom: measuredFrom,
                    MeasuredTo: measuredTo,
                    SourceType: sourceType,
                    Priority: priority,
                    DataQualityStatus: normalizedDataQualityStatus),
                ct);

            return Results.Ok(summary);
        })
        .WithName("GetAnalyticsActionOutcomeSummary");

        // GET /api/analytics/actions/{id}
        group.MapGet("/{id:long}", async (
            long id,
            AnalyticsActionItemService svc,
            CancellationToken ct) =>
        {
            var item = await svc.GetByIdAsync(id, includeNotes: true, ct);
            if (item is null)
                return Results.NotFound();

            item.LedgerSnapshot = AnalyticsActionItemService.GetLedgerSnapshot(item.MetadataJson);
            return Results.Ok(item);
        })
        .WithName("GetAnalyticsActionById");

        // POST /api/analytics/actions
        // Upserts: returns existing open action if same sourceType+sourceKey already open
        group.MapPost("/", async (
            AnalyticsActionUpsertBody body,
            AnalyticsActionItemService svc,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var access = AdminAccessControl.GetDecision(httpContext, configuration);
            if (access is AdminAccessDecision.MissingCredential)
                return Results.Unauthorized();

            if (access is AdminAccessDecision.Forbidden)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (string.IsNullOrWhiteSpace(body.SourceType))
                return Results.BadRequest("sourceType is required");

            if (!AnalyticsActionConstants.IsValidSourceType(body.SourceType))
                return Results.BadRequest($"sourceType must be one of: {string.Join(", ", AnalyticsActionConstants.SourceTypes.AllValues)}");

            if (string.IsNullOrWhiteSpace(body.SourceKey))
                return Results.BadRequest("sourceKey is required");
            if (string.IsNullOrWhiteSpace(body.Title))
                return Results.BadRequest("title is required");
            if (!AnalyticsActionConstants.IsValidPriority(body.Priority))
                return Results.BadRequest($"priority must be one of: {string.Join(", ", AnalyticsActionConstants.Priorities.AllValues)}");

            // Validate and normalize dataQualityStatus
            var normalizedDataQualityStatus = AnalyticsActionConstants.NormalizeDataQualityStatus(body.DataQualityStatus);
            if (normalizedDataQualityStatus != null && !AnalyticsActionConstants.IsValidDataQualityStatus(normalizedDataQualityStatus))
                return Results.BadRequest($"dataQualityStatus must be one of: {string.Join(", ", AnalyticsActionConstants.DataQualityStatuses.AllValues)}");

            var userId = httpContext.User?.FindFirst("sub")?.Value
                      ?? httpContext.User?.FindFirst("userId")?.Value;

            var request = new AnalyticsActionUpsertRequest(
                SourceType: body.SourceType,
                SourceKey: body.SourceKey,
                SourceId: body.SourceId,
                Title: body.Title,
                Description: body.Description,
                RecommendationStatus: body.RecommendationStatus,
                Priority: body.Priority,
                ImpactEstimateRsd: body.ImpactEstimateRsd,
                DueAtUtc: body.DueAtUtc,
                ExpectedImpactRsd: body.ExpectedImpactRsd,
                ConfidencePct: body.ConfidencePct,
                ReliabilityPct: body.ReliabilityPct,
                DataQualityStatus: normalizedDataQualityStatus,
                ActionUrl: body.ActionUrl,
                SourceRecommendationId: body.SourceRecommendationId,
                RecommendationType: body.RecommendationType,
                ExpectedImpactBasis: body.ExpectedImpactBasis,
                ImpactWindowDays: body.ImpactWindowDays,
                ConfidenceLevel: body.ConfidenceLevel,
                WarningCodes: body.WarningCodes,
                PrimaryDrivers: body.PrimaryDrivers,
                DecisionReason: body.DecisionReason,
                RecommendedAction: body.RecommendedAction,
                GeneratedAtUtc: body.GeneratedAtUtc,
                InputFreshnessStatus: body.InputFreshnessStatus,
                MetadataJson: body.MetadataJson
            );

            var result = await svc.UpsertWithResultAsync(request, userId, ct);
            result.Item.LedgerSnapshot = AnalyticsActionItemService.GetLedgerSnapshot(result.Item.MetadataJson);
            return Results.Ok(result);
        })
        .WithName("UpsertAnalyticsAction");

        // POST /api/analytics/actions/status
        // Batch status probe by sourceType + sourceKey tuples.
        group.MapPost("/status", async (
            AnalyticsActionSourceStatusLookupBody body,
            AnalyticsActionItemService svc,
            CancellationToken ct) =>
        {
            if (body.Items is null || body.Items.Count == 0)
                return Results.BadRequest("items is required");

            if (body.Items.Count > 1000)
                return Results.BadRequest("items must contain at most 1000 entries");

            foreach (var item in body.Items)
            {
                if (string.IsNullOrWhiteSpace(item.SourceType))
                    return Results.BadRequest("items[].sourceType is required");

                if (!AnalyticsActionConstants.IsValidSourceType(item.SourceType))
                    return Results.BadRequest($"sourceType must be one of: {string.Join(", ", AnalyticsActionConstants.SourceTypes.AllValues)}");

                if (string.IsNullOrWhiteSpace(item.SourceKey))
                    return Results.BadRequest("items[].sourceKey is required");
            }

            // exists=true means an open action exists for the exact sourceType+sourceKey tuple.
            // exists=false with status=done/rejected means only closed actions exist and a new action is allowed.
            var items = await svc.GetSourceStatusesAsync(
                body.Items
                    .Select(x => new AnalyticsActionSourceStatusLookupInput(x.SourceType, x.SourceKey))
                    .ToArray(),
                ct);
            return Results.Ok(new { items });
        })
        .WithName("GetAnalyticsActionSourceStatuses");

        // PATCH /api/analytics/actions/{id}/status
        group.MapMethods("/{id:long}/status", ["PATCH"], async (
            long id,
            AnalyticsActionStatusUpdateBody body,
            AnalyticsActionItemService svc,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var access = AdminAccessControl.GetDecision(httpContext, configuration);
            if (access is AdminAccessDecision.MissingCredential)
                return Results.Unauthorized();

            if (access is AdminAccessDecision.Forbidden)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (!AnalyticsActionConstants.IsValidStatus(body.Status))
                return Results.BadRequest($"status must be one of: {string.Join(", ", AnalyticsActionConstants.Statuses.AllValues)}");

            var userId = httpContext.User?.FindFirst("sub")?.Value
                      ?? httpContext.User?.FindFirst("userId")?.Value;
            var userName = httpContext.User?.FindFirst("name")?.Value
                        ?? httpContext.User?.FindFirst("preferred_username")?.Value;

            var updated = await svc.UpdateStatusAsync(id, body.Status, body.Note, userId, userName, ct);
            if (updated is null)
                return Results.NotFound();

            var detailed = await svc.GetByIdAsync(id, includeNotes: true, ct);
            if (detailed is null)
                return Results.NotFound();

            detailed.LedgerSnapshot = AnalyticsActionItemService.GetLedgerSnapshot(detailed.MetadataJson);
            return Results.Ok(detailed);
        })
        .WithName("UpdateAnalyticsActionStatus");

        // PATCH /api/analytics/actions/{id}/outcome
        group.MapMethods("/{id:long}/outcome", ["PATCH"], async (
            long id,
            AnalyticsActionOutcomeUpdateBody body,
            AnalyticsActionItemService svc,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var access = AdminAccessControl.GetDecision(httpContext, configuration);
            if (access is AdminAccessDecision.MissingCredential)
                return Results.Unauthorized();

            if (access is AdminAccessDecision.Forbidden)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (!AnalyticsActionConstants.IsValidOutcomeStatus(body.OutcomeStatus))
                return Results.BadRequest($"outcomeStatus must be one of: {string.Join(", ", AnalyticsActionConstants.OutcomeStatuses.AllValues)}");

            if (!string.IsNullOrWhiteSpace(body.OutcomeNotes) && body.OutcomeNotes.Trim().Length > 4000)
                return Results.BadRequest("outcomeNotes must be 4000 characters or fewer");

            var userId = httpContext.User?.FindFirst("sub")?.Value
                      ?? httpContext.User?.FindFirst("userId")?.Value;
            var userName = httpContext.User?.FindFirst("name")?.Value
                        ?? httpContext.User?.FindFirst("preferred_username")?.Value;

            var updated = await svc.UpdateOutcomeAsync(
                id,
                new AnalyticsActionOutcomeUpdateRequest(
                    OutcomeStatus: body.OutcomeStatus,
                    MeasuredImpactRsd: body.MeasuredImpactRsd,
                    OutcomeMeasuredAtUtc: body.OutcomeMeasuredAtUtc,
                    OutcomeNotes: body.OutcomeNotes,
                    MeasuredWindowDays: body.MeasuredWindowDays,
                    EvidenceSource: body.EvidenceSource,
                    EvidenceReference: body.EvidenceReference,
                    ResolutionNote: body.ResolutionNote),
                userId,
                userName,
                ct);

            if (updated is null)
                return Results.NotFound();

            var detailed = await svc.GetByIdAsync(id, includeNotes: true, ct);
            if (detailed is null)
                return Results.NotFound();

            detailed.LedgerSnapshot = AnalyticsActionItemService.GetLedgerSnapshot(detailed.MetadataJson);
            return Results.Ok(detailed);
        })
        .WithName("UpdateAnalyticsActionOutcome");
    }
}

// ── Request bodies ────────────────────────────────────────────────────────────

public sealed record AnalyticsActionUpsertBody(
    string SourceType,
    string SourceKey,
    int? SourceId,
    string Title,
    string? Description,
    string? RecommendationStatus,
    string Priority,
    decimal? ImpactEstimateRsd,
    DateTime? DueAtUtc,
    decimal? ExpectedImpactRsd,
    int? ConfidencePct,
    int? ReliabilityPct,
    string? DataQualityStatus,
    string? ActionUrl,
    string? SourceRecommendationId,
    string? RecommendationType,
    string? ExpectedImpactBasis,
    int? ImpactWindowDays,
    string? ConfidenceLevel,
    IReadOnlyList<string>? WarningCodes,
    IReadOnlyList<string>? PrimaryDrivers,
    string? DecisionReason,
    string? RecommendedAction,
    DateTime? GeneratedAtUtc,
    string? InputFreshnessStatus,
    string? MetadataJson
);

public sealed record AnalyticsActionStatusUpdateBody(
    string Status,
    string? Note
);

public sealed record AnalyticsActionOutcomeUpdateBody(
    string OutcomeStatus,
    decimal? MeasuredImpactRsd,
    DateTime? OutcomeMeasuredAtUtc,
    string? OutcomeNotes,
    int? MeasuredWindowDays,
    string? EvidenceSource,
    string? EvidenceReference,
    string? ResolutionNote
);

public sealed record AnalyticsActionSourceStatusLookupBody(
    IReadOnlyList<AnalyticsActionSourceStatusLookupItemBody> Items
);

public sealed record AnalyticsActionSourceStatusLookupItemBody(
    string SourceType,
    string SourceKey
);
