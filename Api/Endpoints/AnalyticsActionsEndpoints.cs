using Application.Analytics;
using Infrastructure.Services.Analytics;
using Microsoft.AspNetCore.Http;

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

        // GET /api/analytics/actions/{id}
        group.MapGet("/{id:long}", async (
            long id,
            AnalyticsActionItemService svc,
            CancellationToken ct) =>
        {
            var item = await svc.GetByIdAsync(id, includeNotes: true, ct);
            return item is null ? Results.NotFound() : Results.Ok(item);
        })
        .WithName("GetAnalyticsActionById");

        // POST /api/analytics/actions
        // Upserts: returns existing open action if same sourceType+sourceKey already open
        group.MapPost("/", async (
            AnalyticsActionUpsertBody body,
            AnalyticsActionItemService svc,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
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
                ConfidencePct: body.ConfidencePct,
                ReliabilityPct: body.ReliabilityPct,
                DataQualityStatus: normalizedDataQualityStatus,
                ActionUrl: body.ActionUrl,
                MetadataJson: body.MetadataJson
            );

            var result = await svc.UpsertWithResultAsync(request, userId, ct);
            return Results.Ok(result);
        })
        .WithName("UpsertAnalyticsAction");

        // POST /api/analytics/actions/status
        // Batch status probe by sourceType + sourceKeys.
        group.MapPost("/status", async (
            AnalyticsActionSourceStatusBody body,
            AnalyticsActionItemService svc,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.SourceType))
                return Results.BadRequest("sourceType is required");

            if (!AnalyticsActionConstants.IsValidSourceType(body.SourceType))
                return Results.BadRequest($"sourceType must be one of: {string.Join(", ", AnalyticsActionConstants.SourceTypes.AllValues)}");

            if (body.SourceKeys is null || body.SourceKeys.Count == 0)
                return Results.BadRequest("sourceKeys is required");

            if (body.SourceKeys.Count > 1000)
                return Results.BadRequest("sourceKeys must contain at most 1000 items");

            var items = await svc.GetSourceStatusesAsync(body.SourceType, body.SourceKeys, ct);
            return Results.Ok(new { items });
        })
        .WithName("GetAnalyticsActionSourceStatuses");

        // PATCH /api/analytics/actions/{id}/status
        group.MapMethods("/{id:long}/status", ["PATCH"], async (
            long id,
            AnalyticsActionStatusUpdateBody body,
            AnalyticsActionItemService svc,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
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
            return detailed is null ? Results.NotFound() : Results.Ok(detailed);
        })
        .WithName("UpdateAnalyticsActionStatus");
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
    int? ConfidencePct,
    int? ReliabilityPct,
    string? DataQualityStatus,
    string? ActionUrl,
    string? MetadataJson
);

public sealed record AnalyticsActionStatusUpdateBody(
    string Status,
    string? Note
);

public sealed record AnalyticsActionSourceStatusBody(
    string SourceType,
    IReadOnlyList<string> SourceKeys
);
