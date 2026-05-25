using Domain.Model.Analytics;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infrastructure.Services;

public sealed class AnalyticsRefreshRunRecorder
{
    private const int MaxErrorCodeLength = 120;
    private const int MaxErrorMessageLength = 4000;
    private const int MaxCorrelationIdLength = 120;
    private const int DefaultRetentionMaxRuns = 500;
    private const int DefaultRetentionMaxAgeDays = 30;

    private readonly AnalyticsDbContext _analyticsDb;
    private readonly ILogger<AnalyticsRefreshRunRecorder> _logger;
    private readonly int _retentionMaxRuns;
    private readonly int _retentionMaxAgeDays;

    public AnalyticsRefreshRunRecorder(
        AnalyticsDbContext analyticsDb,
        IConfiguration configuration,
        ILogger<AnalyticsRefreshRunRecorder> logger)
    {
        _analyticsDb = analyticsDb;
        _logger = logger;
        _retentionMaxRuns = Math.Max(
            100,
            configuration.GetValue<int?>("Analytics:RefreshHistory:Retention:MaxRuns")
                ?? DefaultRetentionMaxRuns);
        _retentionMaxAgeDays = Math.Max(
            7,
            configuration.GetValue<int?>("Analytics:RefreshHistory:Retention:MaxAgeDays")
                ?? DefaultRetentionMaxAgeDays);
    }

    public async Task<long?> StartRunAsync(
        string jobKey,
        string jobName,
        string triggeredBy,
        string processMode,
        string workerName,
        string? correlationId,
        CancellationToken ct)
    {
        try
        {
            var run = new AnalyticsRefreshRun
            {
                JobKey = string.IsNullOrWhiteSpace(jobKey) ? "unknown_job" : jobKey.Trim(),
                JobName = string.IsNullOrWhiteSpace(jobName) ? "Unknown job" : jobName.Trim(),
                Status = "running",
                StartedAtUtc = DateTime.UtcNow,
                TriggeredBy = NormalizeTriggeredBy(triggeredBy),
                ProcessMode = NormalizeProcessMode(processMode),
                WorkerName = TrimOrNull(workerName, 128),
                CorrelationId = TrimOrNull(correlationId, MaxCorrelationIdLength),
                CreatedAtUtc = DateTime.UtcNow
            };

            _analyticsDb.AnalyticsRefreshRuns.Add(run);
            await _analyticsDb.SaveChangesAsync(ct);
            await CleanupRetentionAsync(ct);
            return run.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to persist analytics refresh run start. JobKey={JobKey} Worker={WorkerName}",
                jobKey,
                workerName);
            return null;
        }
    }

    public Task MarkSucceededAsync(
        long? runId,
        IReadOnlyCollection<string>? refreshedObjects,
        string? correlationId,
        CancellationToken ct)
    {
        return MarkCompletedAsync(
            runId,
            status: "succeeded",
            refreshedObjects: refreshedObjects,
            failedObjects: null,
            errorCode: null,
            errorMessage: null,
            correlationId: correlationId,
            ct);
    }

    public Task MarkPartialAsync(
        long? runId,
        IReadOnlyCollection<string>? refreshedObjects,
        IReadOnlyCollection<string>? failedObjects,
        string? warningMessage,
        string? correlationId,
        CancellationToken ct)
    {
        return MarkCompletedAsync(
            runId,
            status: "partial",
            refreshedObjects: refreshedObjects,
            failedObjects: failedObjects,
            errorCode: "partial_refresh",
            errorMessage: warningMessage,
            correlationId: correlationId,
            ct);
    }

    public Task MarkFailedAsync(
        long? runId,
        string? errorCode,
        string? errorMessage,
        IReadOnlyCollection<string>? failedObjects,
        string? correlationId,
        CancellationToken ct)
    {
        return MarkCompletedAsync(
            runId,
            status: "failed",
            refreshedObjects: null,
            failedObjects: failedObjects,
            errorCode: errorCode,
            errorMessage: errorMessage,
            correlationId: correlationId,
            ct);
    }

    private async Task MarkCompletedAsync(
        long? runId,
        string status,
        IReadOnlyCollection<string>? refreshedObjects,
        IReadOnlyCollection<string>? failedObjects,
        string? errorCode,
        string? errorMessage,
        string? correlationId,
        CancellationToken ct)
    {
        if (!runId.HasValue)
        {
            return;
        }

        try
        {
            var run = await _analyticsDb.AnalyticsRefreshRuns
                .FirstOrDefaultAsync(x => x.Id == runId.Value, ct);

            if (run is null)
            {
                return;
            }

            var finishedAtUtc = DateTime.UtcNow;
            run.Status = status;
            run.FinishedAtUtc = finishedAtUtc;
            run.DurationSeconds = Math.Max(0d, (finishedAtUtc - run.StartedAtUtc).TotalSeconds);
            run.RefreshedObjectsJson = SerializeObjectList(refreshedObjects);
            run.FailedObjectsJson = SerializeObjectList(failedObjects);
            run.ErrorCode = TrimOrNull(errorCode, MaxErrorCodeLength);
            run.ErrorMessage = TrimOrNull(errorMessage, MaxErrorMessageLength);
            run.CorrelationId = TrimOrNull(correlationId ?? run.CorrelationId, MaxCorrelationIdLength);

            await _analyticsDb.SaveChangesAsync(ct);
            await CleanupRetentionAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to persist analytics refresh run completion. RunId={RunId} Status={Status}",
                runId,
                status);
        }
    }

    private static string? SerializeObjectList(IReadOnlyCollection<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized);
    }

    private static string NormalizeTriggeredBy(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "nightly" => "nightly",
            "manual" => "manual",
            "import" => "import",
            _ => "system"
        };
    }

    private static string NormalizeProcessMode(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "web" => "web",
            "worker" => "worker",
            _ => "unknown"
        };
    }

    private static string? TrimOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        // Keep a readable summary instead of silently truncating raw messages.
        return $"{trimmed[..Math.Max(0, maxLength - 3)]}...";
    }

    private async Task CleanupRetentionAsync(CancellationToken ct)
    {
        try
        {
            var cutoffUtc = DateTime.UtcNow.AddDays(-_retentionMaxAgeDays);
            var keepIds = await _analyticsDb.AnalyticsRefreshRuns
                .AsNoTracking()
                .OrderByDescending(x => x.StartedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(_retentionMaxRuns)
                .Select(x => x.Id)
                .ToListAsync(ct);

            await _analyticsDb.AnalyticsRefreshRuns
                .Where(x => x.StartedAtUtc < cutoffUtc && !keepIds.Contains(x.Id))
                .ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup analytics refresh history retention.");
        }
    }
}
