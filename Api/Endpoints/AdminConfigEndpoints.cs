using Api.Config;
using Api.Services;
using Api.Services.Access;
using Application.Common.Interfaces;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Trendplus2.Endpoints;

namespace Api.Endpoints;

public static class AdminConfigEndpoints
{
    public static void MapAdminConfigEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin")
            .WithName("Admin Config");

        group.MapGet("/pending-batches", GetPendingBatches)
            .WithName("GetPendingBatches")
            .WithSummary("List pending/running/failed import batches")
            .Produces<PendingBatchesResponse>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status401Unauthorized);

        group.MapPost("/requeue-batch/{batchId}", RequeueBatch)
            .WithName("RequeueBatch")
            .WithSummary("Safely requeue a failed/pending batch")
            .Produces<RequeueResponse>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest)
            .Produces<object>(StatusCodes.Status401Unauthorized);

        group.MapPost("/run-stale-recovery", RunStaleRecovery)
            .WithName("RunStaleRecovery")
            .WithSummary("Run stale batch recovery")
            .Produces<DiagnosticsResult>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status401Unauthorized);

        group.MapGet("/health-check", HealthCheck)
            .WithName("AdminHealthCheck")
            .WithSummary("Admin diagnostics")
            .Produces<AdminHealthCheckResponse>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status401Unauthorized);

        group.MapGet("/demo-verification", DemoVerification)
            .WithName("DemoEnvironmentVerification")
            .WithSummary("Check whether the current environment is demo-safe")
            .Produces<DemoEnvironmentVerificationResponse>(StatusCodes.Status200OK);

        group.MapGet("/audit-log", GetAuditLog)
            .WithName("GetAuditLog")
            .WithSummary("Get recent admin action audit trail")
            .Produces<AuditLogResponse>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status401Unauthorized);

        // Worker management endpoints
        group.MapGet("/workers/list", GetWorkersList)
            .WithName("GetWorkersList")
            .WithSummary("Get list of all workers with status and configuration")
            .Produces<WorkersListResponse>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status401Unauthorized);

        group.MapGet("/workers/{workerName}", GetWorkerDetails)
            .WithName("GetWorkerDetails")
            .WithSummary("Get details for a specific worker")
            .Produces<WorkerDetailsDto>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status404NotFound)
            .Produces<object>(StatusCodes.Status401Unauthorized);

        group.MapPost("/workers/{workerName}/resume", ResumeWorker)
            .WithName("ResumeWorker")
            .WithSummary("Resume a manually stopped worker (allows running)")
            .Produces<WorkerActionResponse>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest)
            .Produces<object>(StatusCodes.Status401Unauthorized);

        group.MapPost("/workers/{workerName}/stop", StopWorker)
            .WithName("StopWorker")
            .WithSummary("Manually stop a worker from running")
            .Produces<WorkerActionResponse>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest)
            .Produces<object>(StatusCodes.Status401Unauthorized);

        group.MapPost("/workers/{workerName}/schedule/enable", EnableWorkerSchedule)
            .WithName("EnableWorkerSchedule")
            .WithSummary("Enable scheduled execution for a worker")
            .Produces<WorkerActionResponse>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest)
            .Produces<object>(StatusCodes.Status401Unauthorized);

        group.MapPost("/workers/{workerName}/schedule/disable", DisableWorkerSchedule)
            .WithName("DisableWorkerSchedule")
            .WithSummary("Disable scheduled execution for a worker (manual start still allowed)")
            .Produces<WorkerActionResponse>(StatusCodes.Status200OK)
            .Produces<object>(StatusCodes.Status400BadRequest)
            .Produces<object>(StatusCodes.Status401Unauthorized);
    }

    private static async Task<Ok<PendingBatchesResponse>> GetPendingBatches(
        TrendplusDbContext db,
        [FromQuery] string? status,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);

        IQueryable<Domain.Model.DataImportBatch> query = db.DataImportBatches.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var batches = await query.OrderByDescending(x => x.QueuedAtUtc)
            .Take(take)
            .Select(x => new PendingBatchDto
            {
                Id = x.Id,
                SourceFileName = x.SourceFileName ?? "(unknown)",
                Status = x.Status ?? "pending",
                QueuedAtUtc = x.QueuedAtUtc,
                StartedAtUtc = x.StartedAtUtc,
                CompletedAtUtc = x.CompletedAtUtc,
                LastHeartbeatUtc = x.LastHeartbeatUtc,
                CurrentStep = x.CurrentStep,
                CurrentTable = x.CurrentTable,
                ElapsedSeconds = x.StartedAtUtc != null && (x.CompletedAtUtc == null ? DateTime.UtcNow : x.CompletedAtUtc) > x.StartedAtUtc
                    ? (int)((x.CompletedAtUtc ?? DateTime.UtcNow) - x.StartedAtUtc).TotalSeconds
                    : 0,
                RowsRead = x.RowsRead,
                RowsWritten = x.RowsWritten,
                ProgressPercent = x.ProgressPercent,
                ErrorMessage = x.ErrorMessage,
                HasSourceFile = !string.IsNullOrWhiteSpace(x.SourceFilePath),
                HasStorageKey = !string.IsNullOrWhiteSpace(x.SourceStorageKey),
                CancellationRequested = x.CancellationRequested,
                RetryCount = x.RetryCount
            })
            .ToListAsync(ct);

        return TypedResults.Ok(new PendingBatchesResponse
        {
            Total = batches.Count,
            Batches = batches
        });
    }

    private static async Task<IResult> RequeueBatch(
        long batchId,
        HttpContext context,
        IConfiguration configuration,
        TrendplusDbContext db,
        IAccessImportJobQueue queue,
        CancellationToken ct = default)
    {
        var access = AdminAccessControl.GetDecision(context, configuration);
        if (access is AdminAccessDecision.MissingCredential)
            return Results.Unauthorized();
        if (access is AdminAccessDecision.Forbidden)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var batch = await db.DataImportBatches.FindAsync(new object[] { batchId }, ct);
        if (batch == null)
            return TypedResults.Ok(new RequeueResponse { Success = false, Message = $"Batch {batchId} not found." });

        if (batch.CompletedAtUtc != null && batch.Status == "completed")
            return TypedResults.Ok(new RequeueResponse { Success = false, Message = "Cannot requeue a completed batch." });

        if (batch.CancellationRequested)
            return TypedResults.Ok(new RequeueResponse { Success = false, Message = "Cannot requeue a batch with cancellation requested." });

        if (string.IsNullOrWhiteSpace(batch.SourceFilePath) && string.IsNullOrWhiteSpace(batch.SourceStorageKey))
            return TypedResults.Ok(new RequeueResponse { Success = false, Message = "Batch has no source file or storage key." });

        try
        {
            await queue.EnqueueAsync(batchId, ct);
            return TypedResults.Ok(new RequeueResponse { Success = true, Message = $"Batch {batchId} enqueued for retry.", BatchId = batchId });
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Ok(new RequeueResponse { Success = false, Message = $"Enqueue failed: {ex.Message}" });
        }
    }

    private static async Task<IResult> RunStaleRecovery(
        HttpContext context,
        IConfiguration configuration,
        IAccessImportService importService,
        CancellationToken ct = default)
    {
        var access = AdminAccessControl.GetDecision(context, configuration);
        if (access is AdminAccessDecision.MissingCredential)
            return Results.Unauthorized();
        if (access is AdminAccessDecision.Forbidden)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        try
        {
            await importService.RefreshBatchStatusesAsync(batchId: null, ct);
            return TypedResults.Ok(new DiagnosticsResult { Success = true, Message = "Stale recovery completed.", Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            return TypedResults.Ok(new DiagnosticsResult { Success = false, Message = $"Failed: {ex.GetBaseException().Message}", Timestamp = DateTime.UtcNow });
        }
    }

    private static async Task<Ok<AdminHealthCheckResponse>> HealthCheck(
        WorkerHealthService workerHealth,
        WorkerRuntimeControlService workerControl,
        TrendplusDbContext db,
        CancellationToken ct = default)
    {
        var response = new AdminHealthCheckResponse { Timestamp = DateTime.UtcNow, WorkerGlobalEnabled = workerControl.IsEnabled };
        var schemaStatus = WorkerRuntimeSettingsSchemaGuard.GetStatus();
        response.WorkerRuntimeSettingsSchemaReady = schemaStatus.IsSchemaReady && !schemaStatus.IsSchemaMissing;
        response.WorkerRuntimeSettingsLastEnsureAttemptUtc = schemaStatus.LastEnsureAttemptUtc?.UtcDateTime;
        response.WorkerRuntimeSettingsLastEnsureSuccessUtc = schemaStatus.LastEnsureSuccessUtc?.UtcDateTime;
        response.WorkerRuntimeSettingsSchemaError = schemaStatus.LastEnsureError;

        try
        {
            var canConnect = await db.Database.CanConnectAsync(ct);
            response.DatabaseConnected = canConnect;
            response.DatabaseMessage = canConnect ? "Connected" : "Failed";
        }
        catch (Exception ex)
        {
            response.DatabaseConnected = false;
            response.DatabaseMessage = ex.GetBaseException().Message;
        }

        return TypedResults.Ok(response);
    }

    private static Ok<DemoEnvironmentVerificationResponse> DemoVerification(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return TypedResults.Ok(BuildDemoVerificationResponse(configuration, environment));
    }

    private static DemoEnvironmentVerificationResponse BuildDemoVerificationResponse(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var reasons = new List<string>();
        var warnings = new List<string>();
        var environmentName = environment.EnvironmentName ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(environmentName) &&
            environmentName.Contains("demo", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("environment_name_contains_demo");
        }

        if (configuration.GetValue<bool?>("AnalyticsDemo:Enabled") == true)
        {
            reasons.Add("analytics_demo_flag_enabled");
        }

        EvaluateConnectionString(configuration.GetConnectionString("AnalyticsConnection"), warnings, reasons);
        EvaluateConnectionString(configuration.GetConnectionString("DefaultConnection"), warnings, reasons);

        return new DemoEnvironmentVerificationResponse
        {
            DemoSafe = reasons.Count > 0,
            Reasons = reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Environment = string.IsNullOrWhiteSpace(environmentName) ? "unknown" : environmentName,
            CheckedAtUtc = DateTime.UtcNow
        };
    }

    private static void EvaluateConnectionString(
        string? connectionString,
        List<string> warnings,
        List<string> reasons)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            warnings.Add("connection_string_unavailable_or_unreadable");
            return;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);

            AppendProofReason(builder.Database, "analytics_connection_database_contains_demo", reasons);
            AppendProofReason(builder.Host, "analytics_connection_host_contains_demo", reasons);
            AppendProofReason(builder.ApplicationName, "analytics_connection_application_name_contains_demo", reasons);
        }
        catch
        {
            warnings.Add("connection_string_unavailable_or_unreadable");
        }
    }

    private static void AppendProofReason(
        string? value,
        string reason,
        List<string> reasons)
    {
        if (ContainsMarker(value, "demo"))
        {
            reasons.Add(reason);
        }
    }

    private static bool ContainsMarker(string? value, string marker) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains(marker, StringComparison.OrdinalIgnoreCase);

    private static async Task<Ok<AuditLogResponse>> GetAuditLog(
        TrendplusDbContext db,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 1000);
        var recentLogs = await db.AccessImportLogs
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(take)
            .Select(x => new AuditEntry { Id = x.Id, Timestamp = x.CreatedAtUtc, BatchId = x.BatchId, Severity = x.Severity, Message = x.Message })
            .ToListAsync(ct);

        return TypedResults.Ok(new AuditLogResponse { Entries = recentLogs, Total = recentLogs.Count });
    }

    private static async Task<Ok<WorkersListResponse>> GetWorkersList(
        WorkerRegistryService service,
        CancellationToken ct = default)
    {
        var workers = await service.GetConfigurationAsync(ct);
        return TypedResults.Ok(new WorkersListResponse { Workers = workers.Workers, Total = workers.Total });
    }

    private static async Task<Results<Ok<WorkerConfigurationItemDto>, NotFound>> GetWorkerDetails(
        string workerName,
        WorkerRegistryService service,
        CancellationToken ct = default)
    {
        var workers = await service.GetConfigurationAsync(ct);
        var worker = workers.Workers.FirstOrDefault(
            w => string.Equals(w.WorkerName, workerName, StringComparison.OrdinalIgnoreCase));
        if (worker == null)
            return TypedResults.NotFound();
        return TypedResults.Ok(worker);
    }

    private static async Task<IResult> ResumeWorker(
        string workerName,
        WorkerConfigurationService service,
        HttpContext context,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        var access = AdminAccessControl.GetDecision(context, configuration);
        if (access is AdminAccessDecision.MissingCredential)
            return Results.Unauthorized();
        if (access is AdminAccessDecision.Forbidden)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var userName = context.User.Identity?.Name ?? "system";
        var success = await service.ResumeWorkerAsync(workerName, userName, ct);
        return success
            ? TypedResults.Ok(new WorkerActionResponse { Success = true, Message = $"Worker {workerName} has been resumed" })
            : TypedResults.BadRequest<object>(new { error = "Failed to resume worker" });
    }

    private static async Task<IResult> StopWorker(
        string workerName,
        WorkerConfigurationService service,
        HttpContext context,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        var access = AdminAccessControl.GetDecision(context, configuration);
        if (access is AdminAccessDecision.MissingCredential)
            return Results.Unauthorized();
        if (access is AdminAccessDecision.Forbidden)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var userName = context.User.Identity?.Name ?? "system";
        var success = await service.StopWorkerAsync(workerName, userName, ct);
        return success
            ? TypedResults.Ok(new WorkerActionResponse { Success = true, Message = $"Worker {workerName} has been stopped" })
            : TypedResults.BadRequest<object>(new { error = "Failed to stop worker" });
    }

    private static async Task<IResult> EnableWorkerSchedule(
        string workerName,
        WorkerConfigurationService service,
        HttpContext context,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        var access = AdminAccessControl.GetDecision(context, configuration);
        if (access is AdminAccessDecision.MissingCredential)
            return Results.Unauthorized();
        if (access is AdminAccessDecision.Forbidden)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var userName = context.User.Identity?.Name ?? "system";
        var success = await service.EnableScheduleAsync(workerName, userName, ct);
        return success
            ? TypedResults.Ok(new WorkerActionResponse { Success = true, Message = $"Schedule enabled for worker {workerName}" })
            : TypedResults.BadRequest<object>(new { error = "Failed to enable schedule" });
    }

    private static async Task<IResult> DisableWorkerSchedule(
        string workerName,
        WorkerConfigurationService service,
        HttpContext context,
        IConfiguration configuration,
        CancellationToken ct = default)
    {
        var access = AdminAccessControl.GetDecision(context, configuration);
        if (access is AdminAccessDecision.MissingCredential)
            return Results.Unauthorized();
        if (access is AdminAccessDecision.Forbidden)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var userName = context.User.Identity?.Name ?? "system";
        var success = await service.DisableScheduleAsync(workerName, userName, ct);
        return success
            ? TypedResults.Ok(new WorkerActionResponse { Success = true, Message = $"Schedule disabled for worker {workerName}" })
            : TypedResults.BadRequest<object>(new { error = "Failed to disable schedule" });
    }

}

public class PendingBatchesResponse { public int Total { get; set; } public List<PendingBatchDto> Batches { get; set; } = new(); }
public class PendingBatchDto { public long Id { get; set; } public string SourceFileName { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public DateTime QueuedAtUtc { get; set; } public DateTime StartedAtUtc { get; set; } public DateTime? CompletedAtUtc { get; set; } public DateTime? LastHeartbeatUtc { get; set; } public string? CurrentStep { get; set; } public string? CurrentTable { get; set; } public int ElapsedSeconds { get; set; } public int RowsRead { get; set; } public int RowsWritten { get; set; } public int ProgressPercent { get; set; } public string? ErrorMessage { get; set; } public bool HasSourceFile { get; set; } public bool HasStorageKey { get; set; } public bool CancellationRequested { get; set; } public int RetryCount { get; set; } }
public class RequeueResponse { public bool Success { get; set; } public string Message { get; set; } = string.Empty; public long? BatchId { get; set; } }
public class DiagnosticsResult { public bool Success { get; set; } public string Message { get; set; } = string.Empty; public DateTime Timestamp { get; set; } }
public class AdminHealthCheckResponse
{
    public DateTime Timestamp { get; set; }
    public bool WorkerGlobalEnabled { get; set; }
    public string WorkerHealthState { get; set; } = "operational";
    public DateTime LastWorkerHeartbeat { get; set; }
    public bool DatabaseConnected { get; set; }
    public string DatabaseMessage { get; set; } = string.Empty;
    public bool WorkerRuntimeSettingsSchemaReady { get; set; } = true;
    public DateTime? WorkerRuntimeSettingsLastEnsureAttemptUtc { get; set; }
    public DateTime? WorkerRuntimeSettingsLastEnsureSuccessUtc { get; set; }
    public string? WorkerRuntimeSettingsSchemaError { get; set; }
}
public class DemoEnvironmentVerificationResponse
{
    public bool DemoSafe { get; set; }
    public List<string> Reasons { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string Environment { get; set; } = string.Empty;
    public DateTime CheckedAtUtc { get; set; }
}
public class AuditLogResponse { public List<AuditEntry> Entries { get; set; } = new(); public int Total { get; set; } }
public class AuditEntry { public long Id { get; set; } public DateTime Timestamp { get; set; } public long BatchId { get; set; } public string Severity { get; set; } = string.Empty; public string Message { get; set; } = string.Empty; }
public class WorkersListResponse { public List<WorkerConfigurationItemDto> Workers { get; set; } = new(); public int Total { get; set; } }
public class WorkerActionResponse { public bool Success { get; set; } public string Message { get; set; } = string.Empty; }
