using System.Security.Claims;
using Api.Models;
using Api.Services;

namespace Trendplus2.Endpoints;

public static class AdminRepairEndpoints
{
    public static void MapAdminRepairEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/repair")
            .WithTags("Admin", "Nivelacija Repair")
            .RequireRateLimiting("strict");

        group.MapGet("/nivelacije/preflight", async (
            INivelacijaRepairService repairService,
            HttpContext httpContext,
            IConfiguration configuration,
            [AsParameters] PreflightQuery query,
            CancellationToken ct) =>
        {
            if (!IsAuthorizedAdmin(httpContext, configuration))
                return Results.Unauthorized();

            var result = await repairService.RunPreflightAsync(query.SourceFilePath, ct);
            return Results.Ok(result);
        })
        .WithName("PreflightNivelacijaRepair");

        group.MapPost("/nivelacije", async (
            NivelacijaRepairRequest request,
            INivelacijaRepairService repairService,
            HttpContext httpContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!IsAuthorizedAdmin(httpContext, configuration))
                return Results.Unauthorized();

            var requestedBy = ResolveRequestedBy(httpContext);

            try
            {
                if (request.DryRun)
                {
                    var plan = await repairService.GenerateRepairPlanAsync(request.SourceFilePath, request.MaxRowsToModify, ct);
                    var auditId = await repairService.WriteDryRunAuditAsync(requestedBy, plan, ct);

                    return Results.Ok(new
                    {
                        detectedIssues = plan.DetectedIssues,
                        proposedFixes = plan.ProposedFixes,
                        estimatedImpact = plan.EstimatedImpact,
                        verification = plan.Verification,
                        auditId,
                        sourceFilePath = plan.SourceFilePath,
                    });
                }

                if (!request.Confirm)
                {
                    return Results.BadRequest(new
                    {
                        message = "Live nivelacija repair requires confirm=true.",
                    });
                }

                var result = await repairService.ExecuteRepairAsync(request.SourceFilePath, requestedBy, request.MaxRowsToModify, ct);
                return Results.Ok(new
                {
                    fixedRows = result.FixedRows,
                    skippedRows = result.SkippedRows,
                    auditId = result.AuditId,
                    remainingIssuesAfterRepair = result.RemainingIssuesAfterRepair,
                    estimatedImpact = result.EstimatedImpact,
                    verification = result.Verification,
                    sourceFilePath = result.SourceFilePath,
                });
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message, sourceFilePath = ex.FileName });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        })
        .WithName("RepairNivelacije");
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

    private sealed record PreflightQuery(string? SourceFilePath);
}