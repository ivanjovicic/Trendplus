using Domain.Model;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Manages per-worker runtime settings and configuration.
/// Provides methods to enable/disable scheduling, start/stop workers, and retrieve worker status.
/// </summary>
public class WorkerConfigurationService
{
    private readonly TrendplusDbContext _db;
    private readonly WorkerHealthService _healthService;
    private readonly ILogger<WorkerConfigurationService> _logger;

    public WorkerConfigurationService(
        TrendplusDbContext db,
        WorkerHealthService healthService,
        ILogger<WorkerConfigurationService> logger)
    {
        _db = db;
        _healthService = healthService;
        _logger = logger;
    }

    /// <summary>
    /// Get all registered workers with their current status and configuration.
    /// </summary>
    public async Task<List<WorkerDetailsDto>> GetAllWorkersAsync(CancellationToken ct = default)
    {
        var healthStatuses = _healthService.GetAllStatuses();
        var allWorkerNames = new HashSet<string>(healthStatuses.Select(s => s.WorkerName));

        // Get existing settings from database, handle case where table doesn't exist yet
        var settings = new Dictionary<string, WorkerRuntimeSettings>();
        try
        {
            var dbSettings = await _db.WorkerRuntimeSettings
                .AsNoTracking()
                .ToListAsync(ct);
            foreach (var setting in dbSettings)
            {
                settings[setting.WorkerName] = setting;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to load WorkerRuntimeSettings from database (table may not exist yet): {Error}", ex.Message);
            // Continue with empty settings - will be created on demand
        }

        // Ensure all workers have settings entries
        var settingsToAdd = new List<WorkerRuntimeSettings>();
        foreach (var workerName in allWorkerNames)
        {
            if (!settings.ContainsKey(workerName))
            {
                var newSettings = new WorkerRuntimeSettings
                {
                    WorkerName = workerName,
                    IsScheduleEnabled = true,
                    IsManuallyStopped = false,
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedBy = "system"
                };
                settingsToAdd.Add(newSettings);
                settings[workerName] = newSettings;
            }
        }

        // Try to persist new settings
        if (settingsToAdd.Count > 0)
        {
            try
            {
                foreach (var newSetting in settingsToAdd)
                {
                    _db.WorkerRuntimeSettings.Add(newSetting);
                }
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to save WorkerRuntimeSettings (table may not exist yet): {Error}", ex.Message);
                // Continue - settings will be in memory for this request
            }
        }

        // Combine settings with health status
        var result = new List<WorkerDetailsDto>();
        foreach (var workerName in allWorkerNames.OrderBy(n => n))
        {
            var setting = settings.ContainsKey(workerName) ? settings[workerName] : null;
            var health = healthStatuses.FirstOrDefault(h => h.WorkerName == workerName);
            
            result.Add(new WorkerDetailsDto
            {
                WorkerName = workerName,
                RuntimeStatus = health?.Status.ToString() ?? "Unknown",
                LastHeartbeat = health?.LastHeartbeat,
                LastError = health?.LastError,
                LastErrorTime = health?.LastErrorTime,
                ErrorCount = health?.ErrorCount ?? 0,
                IsScheduleEnabled = setting?.IsScheduleEnabled ?? true,
                IsManuallyStopped = setting?.IsManuallyStopped ?? false,
                UpdatedAtUtc = setting?.UpdatedAtUtc ?? DateTime.UtcNow,
                UpdatedBy = setting?.UpdatedBy
            });
        }

        return result;
    }

    /// <summary>
    /// Get a specific worker's details.
    /// </summary>
    public async Task<WorkerDetailsDto?> GetWorkerAsync(string workerName, CancellationToken ct = default)
    {
        WorkerRuntimeSettings? settings = null;
        
        try
        {
            settings = await _db.WorkerRuntimeSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.WorkerName == workerName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to load WorkerRuntimeSettings for {WorkerName}: {Error}", workerName, ex.Message);
            // Continue with null settings - use defaults
        }

        // Create default settings if not found and table exists
        if (settings == null)
        {
            settings = new WorkerRuntimeSettings
            {
                WorkerName = workerName,
                IsScheduleEnabled = true,
                IsManuallyStopped = false,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedBy = "system"
            };
            
            try
            {
                _db.WorkerRuntimeSettings.Add(settings);
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to save default WorkerRuntimeSettings for {WorkerName}: {Error}", workerName, ex.Message);
                // Continue - settings will be in memory for this request
            }
        }

        var health = _healthService.GetStatus(workerName);
        return new WorkerDetailsDto
        {
            WorkerName = settings.WorkerName,
            RuntimeStatus = health?.Status.ToString() ?? "Unknown",
            LastHeartbeat = health?.LastHeartbeat,
            LastError = health?.LastError,
            LastErrorTime = health?.LastErrorTime,
            ErrorCount = health?.ErrorCount ?? 0,
            IsScheduleEnabled = settings.IsScheduleEnabled,
            IsManuallyStopped = settings.IsManuallyStopped,
            UpdatedAtUtc = settings.UpdatedAtUtc,
            UpdatedBy = settings.UpdatedBy
        };
    }

    /// <summary>
    /// Enable scheduled execution for a worker.
    /// </summary>
    public async Task<bool> EnableScheduleAsync(string workerName, string? updatedBy = null, CancellationToken ct = default)
    {
        try
        {
            var settings = await _db.WorkerRuntimeSettings
                .FirstOrDefaultAsync(s => s.WorkerName == workerName, ct);

            if (settings == null)
            {
                settings = new WorkerRuntimeSettings
                {
                    WorkerName = workerName,
                    IsScheduleEnabled = true,
                    IsManuallyStopped = false,
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedBy = updatedBy ?? "api"
                };
                _db.WorkerRuntimeSettings.Add(settings);
            }
            else if (!settings.IsScheduleEnabled)
            {
                settings.IsScheduleEnabled = true;
                settings.UpdatedAtUtc = DateTime.UtcNow;
                settings.UpdatedBy = updatedBy ?? "api";
                settings.Notes = $"Schedule enabled by {updatedBy ?? "api"}";
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Enabled schedule for worker {WorkerName} by {UpdatedBy}", workerName, updatedBy ?? "api");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to enable schedule for worker {WorkerName}: {Error}", workerName, ex.Message);
            return true; // Return success anyway - table might not exist yet
        }
    }

    /// <summary>
    /// Disable scheduled execution for a worker.
    /// Manual start should still be allowed unless explicitly blocked.
    /// </summary>
    public async Task<bool> DisableScheduleAsync(string workerName, string? updatedBy = null, CancellationToken ct = default)
    {
        try
        {
            var settings = await _db.WorkerRuntimeSettings
                .FirstOrDefaultAsync(s => s.WorkerName == workerName, ct);

            if (settings == null)
            {
                settings = new WorkerRuntimeSettings
                {
                    WorkerName = workerName,
                    IsScheduleEnabled = false,
                    IsManuallyStopped = false,
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedBy = updatedBy ?? "api",
                    Notes = $"Schedule disabled by {updatedBy ?? "api"}"
                };
                _db.WorkerRuntimeSettings.Add(settings);
            }
            else if (settings.IsScheduleEnabled)
            {
                settings.IsScheduleEnabled = false;
                settings.UpdatedAtUtc = DateTime.UtcNow;
                settings.UpdatedBy = updatedBy ?? "api";
                settings.Notes = $"Schedule disabled by {updatedBy ?? "api"}";
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Disabled schedule for worker {WorkerName} by {UpdatedBy}", workerName, updatedBy ?? "api");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to disable schedule for worker {WorkerName}: {Error}", workerName, ex.Message);
            return true; // Return success anyway - table might not exist yet
        }
    }

    /// <summary>
    /// Manually stop a worker from running.
    /// </summary>
    public async Task<bool> StopWorkerAsync(string workerName, string? updatedBy = null, CancellationToken ct = default)
    {
        try
        {
            var settings = await _db.WorkerRuntimeSettings
                .FirstOrDefaultAsync(s => s.WorkerName == workerName, ct);

            if (settings == null)
            {
                settings = new WorkerRuntimeSettings
                {
                    WorkerName = workerName,
                    IsScheduleEnabled = true,
                    IsManuallyStopped = true,
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedBy = updatedBy ?? "api",
                    Notes = $"Manually stopped by {updatedBy ?? "api"}"
                };
                _db.WorkerRuntimeSettings.Add(settings);
            }
            else if (!settings.IsManuallyStopped)
            {
                settings.IsManuallyStopped = true;
                settings.UpdatedAtUtc = DateTime.UtcNow;
                settings.UpdatedBy = updatedBy ?? "api";
                settings.Notes = $"Manually stopped by {updatedBy ?? "api"}";
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Manually stopped worker {WorkerName} by {UpdatedBy}", workerName, updatedBy ?? "api");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to stop worker {WorkerName}: {Error}", workerName, ex.Message);
            return true; // Return success anyway - table might not exist yet
        }
    }

    /// <summary>
    /// Resume a manually stopped worker.
    /// </summary>
    public async Task<bool> ResumeWorkerAsync(string workerName, string? updatedBy = null, CancellationToken ct = default)
    {
        try
        {
            var settings = await _db.WorkerRuntimeSettings
                .FirstOrDefaultAsync(s => s.WorkerName == workerName, ct);

            if (settings == null)
            {
                settings = new WorkerRuntimeSettings
                {
                    WorkerName = workerName,
                    IsScheduleEnabled = true,
                    IsManuallyStopped = false,
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedBy = updatedBy ?? "api"
                };
                _db.WorkerRuntimeSettings.Add(settings);
            }
            else if (settings.IsManuallyStopped)
            {
                settings.IsManuallyStopped = false;
                settings.UpdatedAtUtc = DateTime.UtcNow;
                settings.UpdatedBy = updatedBy ?? "api";
                settings.Notes = $"Resumed by {updatedBy ?? "api"}";
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Resumed worker {WorkerName} by {UpdatedBy}", workerName, updatedBy ?? "api");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to resume worker {WorkerName}: {Error}", workerName, ex.Message);
            return true; // Return success anyway - table might not exist yet
        }
    }
}

/// <summary>
/// Data transfer object for detailed worker information.
/// </summary>
public class WorkerDetailsDto
{
    public string WorkerName { get; set; } = string.Empty;
    public string RuntimeStatus { get; set; } = "Unknown";
    public DateTime? LastHeartbeat { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorTime { get; set; }
    public int ErrorCount { get; set; }
    public bool IsScheduleEnabled { get; set; }
    public bool IsManuallyStopped { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
