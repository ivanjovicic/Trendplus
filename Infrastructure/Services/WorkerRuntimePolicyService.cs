using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Evaluates and updates per-worker runtime policy:
/// schedule state, manual stop, and one-shot manual run requests.
/// </summary>
public sealed class WorkerRuntimePolicyService
{
    private const string ManualRunMarkerPrefix = "__manual_run_requested_at_utc=";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkerRuntimePolicyService> _logger;

    public WorkerRuntimePolicyService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkerRuntimePolicyService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<WorkerRuntimePolicySnapshot> GetPolicyAsync(
        string workerName,
        CancellationToken ct = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            if (!await WorkerRuntimeSettingsSchemaGuard.EnsureSchemaAsync(db, _logger, ct))
            {
                return CreateAllowFallback(workerName);
            }

            var settings = await GetOrCreateSettingsAsync(db, workerName, updatedBy: "system", ct);

            var manualRunToken = TryExtractManualRunToken(settings.Notes);
            var manualRunRequested = !string.IsNullOrWhiteSpace(manualRunToken);
            var canRunNow = !settings.IsManuallyStopped && (settings.IsScheduleEnabled || manualRunRequested);

            var pauseReason = settings.IsManuallyStopped
                ? "Pauziran - ručno zaustavljen."
                : settings.IsScheduleEnabled
                    ? null
                    : "Pauziran - raspored je onemogućen.";

            return new WorkerRuntimePolicySnapshot(
                WorkerName: settings.WorkerName,
                IsScheduleEnabled: settings.IsScheduleEnabled,
                IsManuallyStopped: settings.IsManuallyStopped,
                ManualRunRequested: manualRunRequested,
                ManualRunToken: manualRunToken,
                CanRunNow: canRunNow,
                PauseReason: pauseReason);
        }
        catch (Exception ex) when (WorkerRuntimeSettingsSchemaGuard.IsMissingRelationException(ex))
        {
            WorkerRuntimeSettingsSchemaGuard.ReportMissingSchema(
                _logger,
                ex,
                $"GetPolicyAsync:{workerName}");
            return CreateAllowFallback(workerName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate worker runtime policy for {WorkerName}. Falling back to allow.", workerName);
            return CreateAllowFallback(workerName);
        }
    }

    public async Task<string> RequestManualRunAsync(
        string workerName,
        string? updatedBy = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            if (!await WorkerRuntimeSettingsSchemaGuard.EnsureSchemaAsync(db, _logger, ct))
            {
                return DateTime.UtcNow.ToString("O");
            }

            var settings = await GetOrCreateSettingsAsync(db, workerName, updatedBy ?? "api", ct);

            var token = DateTime.UtcNow.ToString("O");
            settings.IsManuallyStopped = false;
            settings.UpdatedAtUtc = DateTime.UtcNow;
            settings.UpdatedBy = updatedBy ?? "api";
            settings.Notes = SetManualRunToken(settings.Notes, token);

            await db.SaveChangesAsync(ct);
            return token;
        }
        catch (Exception ex) when (WorkerRuntimeSettingsSchemaGuard.IsMissingRelationException(ex))
        {
            WorkerRuntimeSettingsSchemaGuard.ReportMissingSchema(
                _logger,
                ex,
                $"RequestManualRunAsync:{workerName}");
            return DateTime.UtcNow.ToString("O");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to request manual worker run for {WorkerName}.", workerName);
            return DateTime.UtcNow.ToString("O");
        }
    }

    public async Task<bool> TryConsumeManualRunRequestAsync(
        string workerName,
        string manualRunToken,
        CancellationToken ct = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            if (!await WorkerRuntimeSettingsSchemaGuard.EnsureSchemaAsync(db, _logger, ct))
            {
                return false;
            }

            var settings = await db.WorkerRuntimeSettings
                .FirstOrDefaultAsync(
                    s => s.WorkerName == workerName,
                    ct);

            if (settings is null)
                return false;

            var currentToken = TryExtractManualRunToken(settings.Notes);
            if (!string.Equals(currentToken, manualRunToken, StringComparison.Ordinal))
                return false;

            settings.Notes = ClearManualRunToken(settings.Notes);
            settings.UpdatedAtUtc = DateTime.UtcNow;
            settings.UpdatedBy = "worker";
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex) when (WorkerRuntimeSettingsSchemaGuard.IsMissingRelationException(ex))
        {
            WorkerRuntimeSettingsSchemaGuard.ReportMissingSchema(
                _logger,
                ex,
                $"TryConsumeManualRunRequestAsync:{workerName}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to consume manual worker run request for {WorkerName}.", workerName);
            return false;
        }
    }

    private static async Task<WorkerRuntimeSettings> GetOrCreateSettingsAsync(
        TrendplusDbContext db,
        string workerName,
        string updatedBy,
        CancellationToken ct)
    {
        var settings = await db.WorkerRuntimeSettings
            .FirstOrDefaultAsync(
                s => s.WorkerName == workerName,
                ct);

        if (settings is not null)
            return settings;

        settings = new WorkerRuntimeSettings
        {
            WorkerName = workerName,
            IsScheduleEnabled = true,
            IsManuallyStopped = false,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };

        db.WorkerRuntimeSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }

    private static string? TryExtractManualRunToken(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;

        var lines = notes
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (!line.StartsWith(ManualRunMarkerPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var token = line[ManualRunMarkerPrefix.Length..].Trim();
            if (!string.IsNullOrWhiteSpace(token))
                return token;
        }

        return null;
    }

    private static string SetManualRunToken(string? notes, string token)
    {
        var cleaned = ClearManualRunToken(notes);
        var marker = $"{ManualRunMarkerPrefix}{token}";
        return string.IsNullOrWhiteSpace(cleaned)
            ? marker
            : $"{cleaned}\n{marker}";
    }

    private static string? ClearManualRunToken(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;

        var lines = notes
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith(ManualRunMarkerPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return lines.Length == 0
            ? null
            : string.Join(Environment.NewLine, lines);
    }

    private static WorkerRuntimePolicySnapshot CreateAllowFallback(string workerName)
    {
        return new WorkerRuntimePolicySnapshot(
            WorkerName: workerName,
            IsScheduleEnabled: true,
            IsManuallyStopped: false,
            ManualRunRequested: false,
            ManualRunToken: null,
            CanRunNow: true,
            PauseReason: null);
    }
}

public sealed record WorkerRuntimePolicySnapshot(
    string WorkerName,
    bool IsScheduleEnabled,
    bool IsManuallyStopped,
    bool ManualRunRequested,
    string? ManualRunToken,
    bool CanRunNow,
    string? PauseReason);
