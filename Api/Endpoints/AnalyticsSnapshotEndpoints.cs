using System.Security.Claims;
using Api.Services;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Trendplus2.Endpoints;

public static class AnalyticsSnapshotEndpoints
{
    public static void MapAnalyticsSnapshotEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics/snapshots")
            .WithTags("Analytics", "Snapshots")
            .RequireRateLimiting("strict");

        // ── POST /api/analytics/snapshots/batches ──
        group.MapPost("/batches", async (
            CreateBatchRequest? request,
            AnalyticsCostSnapshotService service,
            IOptions<AnalyticsSnapshotOptions> options,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AuthorizeSnapshotAdmin(httpContext, configuration, options.Value);
            if (denial is not null)
                return denial;

            var createdBy = ResolveRequestedBy(httpContext);
            var batch = await service.CreateBatchAsync(
                request?.Description, createdBy, ct);

            return Results.Created($"/api/analytics/snapshots/batches/{batch.Id}", ToBatchDto(batch));
        })
        .WithName("CreateSnapshotBatch");

        // ── POST /api/analytics/snapshots/batches/{id}/generate ──
        group.MapPost("/batches/{id:long}/generate", async (
            long id,
            bool? dryRun,
            AnalyticsCostSnapshotService service,
            IOptions<AnalyticsSnapshotOptions> options,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AuthorizeSnapshotAdmin(httpContext, configuration, options.Value);
            if (denial is not null)
                return denial;

            try
            {
                var batch = await service.GenerateBatchAsync(id, dryRun ?? false, ct);
                return Results.Ok(ToBatchDto(batch));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("GenerateSnapshotBatch");

        // ── POST /api/analytics/snapshots/batches/{id}/activate ──
        group.MapPost("/batches/{id:long}/activate", async (
            long id,
            AnalyticsCostSnapshotService service,
            IOptions<AnalyticsSnapshotOptions> options,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AuthorizeSnapshotAdmin(httpContext, configuration, options.Value);
            if (denial is not null)
                return denial;

            try
            {
                var batch = await service.ActivateBatchAsync(id, ct);
                return Results.Ok(ToBatchDto(batch));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("ActivateSnapshotBatch");

        // ── POST /api/analytics/snapshots/batches/{id}/deactivate ──
        group.MapPost("/batches/{id:long}/deactivate", async (
            long id,
            AnalyticsCostSnapshotService service,
            IOptions<AnalyticsSnapshotOptions> options,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AuthorizeSnapshotAdmin(httpContext, configuration, options.Value);
            if (denial is not null)
                return denial;

            try
            {
                var batch = await service.DeactivateBatchAsync(id, ct);
                return Results.Ok(ToBatchDto(batch));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("DeactivateSnapshotBatch");

        // ── GET /api/analytics/snapshots/batches ──
        group.MapGet("/batches", async (
            string? scope,
            AnalyticsCostSnapshotService service,
            IOptions<AnalyticsSnapshotOptions> options,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AuthorizeSnapshotAdmin(httpContext, configuration, options.Value);
            if (denial is not null)
                return denial;

            var batches = await service.ListBatchesAsync(scope, ct);
            return Results.Ok(batches.Select(ToBatchDto));
        })
        .WithName("ListSnapshotBatches");

        // ── GET /api/analytics/snapshots/batches/{id} ──
        group.MapGet("/batches/{id:long}", async (
            long id,
            AnalyticsCostSnapshotService service,
            IOptions<AnalyticsSnapshotOptions> options,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AuthorizeSnapshotAdmin(httpContext, configuration, options.Value);
            if (denial is not null)
                return denial;

            var detail = await service.GetBatchDetailAsync(id, ct);
            if (detail is null)
                return Results.NotFound(new { message = $"Batch {id} not found." });

            var dto = ToBatchDto(detail.Batch);
            return Results.Ok(new
            {
                dto.Id,
                dto.Scope,
                dto.Status,
                dto.DryRun,
                dto.CreatedAtUtc,
                dto.GeneratedAtUtc,
                dto.ActivatedAtUtc,
                dto.DeactivatedAtUtc,
                dto.CreatedBy,
                dto.Description,
                dto.RowCount,
                dto.TotalRevenueCovered,
                dto.CoveragePct,
                dto.NoCostPct,
                dto.RemainingLiveFallbackPct,
                dto.GenerationDurationMs,
                dto.ErrorMessage,
                CostSourceBreakdown = detail.CostSourceBreakdown,
            });
        })
        .WithName("GetSnapshotBatchDetail");

        // ── GET /api/analytics/snapshots/health ──
        group.MapGet("/health", async (
            AnalyticsCostSnapshotService service,
            IOptions<AnalyticsSnapshotOptions> options,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AuthorizeSnapshotAdmin(httpContext, configuration, options.Value);
            if (denial is not null)
                return denial;

            var health = await service.GetHealthAsync(ct);
            return Results.Ok(health);
        })
        .WithName("GetSnapshotHealth");

        // ── GET /api/analytics/snapshots/reconcile/supplier-sales-stats ──
        group.MapGet("/reconcile/supplier-sales-stats", async (
            long? batchId,
            int? sezonaId,
            DateTime? fromDate,
            DateTime? toDate,
            int? storeId,
            string? dataScope,
            int? top,
            AnalyticsCostSnapshotService service,
            IOptions<AnalyticsSnapshotOptions> options,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AuthorizeSnapshotAdmin(httpContext, configuration, options.Value);
            if (denial is not null)
                return denial;

            try
            {
                var result = await service.CompareSupplierAnalyticsAsync(
                    new AnalyticsCostSnapshotService.SnapshotAnalyticsComparisonRequest(
                        batchId,
                        sezonaId,
                        fromDate,
                        toDate,
                        storeId,
                        dataScope,
                        top),
                    ct);

                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("CompareSupplierSnapshotAnalytics");

        // ── GET /api/analytics/snapshots/reconcile/shoe-type-sales-stats ──
        group.MapGet("/reconcile/shoe-type-sales-stats", async (
            long? batchId,
            int? sezonaId,
            DateTime? fromDate,
            DateTime? toDate,
            int? storeId,
            string? dataScope,
            int? top,
            AnalyticsCostSnapshotService service,
            IOptions<AnalyticsSnapshotOptions> options,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var denial = AuthorizeSnapshotAdmin(httpContext, configuration, options.Value);
            if (denial is not null)
                return denial;

            try
            {
                var result = await service.CompareShoeTypeAnalyticsAsync(
                    new AnalyticsCostSnapshotService.SnapshotAnalyticsComparisonRequest(
                        batchId,
                        sezonaId,
                        fromDate,
                        toDate,
                        storeId,
                        dataScope,
                        top),
                    ct);

                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .WithName("CompareShoeTypeSnapshotAnalytics");
    }

    // ── Auth ─────────────────────────────────────────────────────────────

    private static IResult? AuthorizeSnapshotAdmin(
        HttpContext context, IConfiguration configuration, AnalyticsSnapshotOptions options)
    {
        if (!options.SnapshotAdminEnabled)
            return Results.NotFound();

        var access = AdminAccessControl.GetDecision(context, configuration);
        return access switch
        {
            AdminAccessDecision.MissingCredential => Results.Unauthorized(),
            AdminAccessDecision.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            _ => null
        };
    }

    private static string ResolveRequestedBy(HttpContext context)
    {
        var roleAwareName = context.User.Claims.FirstOrDefault(static claim =>
                claim.Type == ClaimTypes.Name ||
                claim.Type == ClaimTypes.Email ||
                claim.Type == "preferred_username")?.Value;

        if (!string.IsNullOrWhiteSpace(roleAwareName))
            return roleAwareName;

        var headerName = context.Request.Headers["X-Admin-User"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerName))
            return headerName;

        return "admin-key";
    }

    // ── DTO ──────────────────────────────────────────────────────────────

    private static BatchDto ToBatchDto(Domain.Model.Analytics.AnalyticsCostSnapshotBatch b) => new(
        b.Id, b.Scope, b.Status, b.DryRun,
        b.CreatedAtUtc, b.GeneratedAtUtc, b.ActivatedAtUtc, b.DeactivatedAtUtc,
        b.CreatedBy, b.Description, b.RowCount, b.TotalRevenueCovered,
        Math.Round(b.CoveragePct, 2), Math.Round(b.NoCostPct, 2), Math.Max(0d, Math.Round(100d - b.CoveragePct - b.NoCostPct, 2)),
        b.GenerationDurationMs, b.ErrorMessage);

    private sealed record CreateBatchRequest(string? Description);

    private sealed record BatchDto(
        long Id,
        string Scope,
        string Status,
        bool DryRun,
        DateTime CreatedAtUtc,
        DateTime? GeneratedAtUtc,
        DateTime? ActivatedAtUtc,
        DateTime? DeactivatedAtUtc,
        string CreatedBy,
        string? Description,
        int RowCount,
        decimal TotalRevenueCovered,
        double CoveragePct,
        double NoCostPct,
        double RemainingLiveFallbackPct,
        int? GenerationDurationMs,
        string? ErrorMessage);
}
