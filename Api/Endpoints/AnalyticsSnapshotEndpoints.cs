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
            if (!IsSnapshotAdminAllowed(httpContext, configuration, options.Value))
                return Results.NotFound();

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
            if (!IsSnapshotAdminAllowed(httpContext, configuration, options.Value))
                return Results.NotFound();

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
            if (!IsSnapshotAdminAllowed(httpContext, configuration, options.Value))
                return Results.NotFound();

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
            if (!IsSnapshotAdminAllowed(httpContext, configuration, options.Value))
                return Results.NotFound();

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
            if (!IsSnapshotAdminAllowed(httpContext, configuration, options.Value))
                return Results.NotFound();

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
            if (!IsSnapshotAdminAllowed(httpContext, configuration, options.Value))
                return Results.NotFound();

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
            if (!IsSnapshotAdminAllowed(httpContext, configuration, options.Value))
                return Results.NotFound();

            var health = await service.GetHealthAsync(ct);
            return Results.Ok(health);
        })
        .WithName("GetSnapshotHealth");
    }

    // ── Auth ─────────────────────────────────────────────────────────────

    private static bool IsSnapshotAdminAllowed(
        HttpContext context, IConfiguration configuration, AnalyticsSnapshotOptions options)
    {
        if (!options.SnapshotAdminEnabled)
            return false;

        return IsAuthorizedAdmin(context, configuration);
    }

    private static bool IsAuthorizedAdmin(HttpContext context, IConfiguration configuration)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole("Admin"))
            return true;

        var configuredKey = configuration["Admin:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
            configuredKey = Environment.GetEnvironmentVariable("ADMIN_API_KEY");
        if (string.IsNullOrWhiteSpace(configuredKey))
            return false;

        var providedKey = context.Request.Headers["X-Admin-Key"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(providedKey)
            && string.Equals(providedKey, configuredKey, StringComparison.Ordinal);
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
        Math.Round(b.CoveragePct, 2), Math.Round(b.NoCostPct, 2),
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
        int? GenerationDurationMs,
        string? ErrorMessage);
}
