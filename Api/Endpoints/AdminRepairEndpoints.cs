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
            var access = AdminAccessControl.GetDecision(httpContext, configuration);
            if (access is AdminAccessDecision.MissingCredential)
                return Results.Unauthorized();
            if (access is AdminAccessDecision.Forbidden)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

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
            var access = AdminAccessControl.GetDecision(httpContext, configuration);
            if (access is AdminAccessDecision.MissingCredential)
                return Results.Unauthorized();
            if (access is AdminAccessDecision.Forbidden)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

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
