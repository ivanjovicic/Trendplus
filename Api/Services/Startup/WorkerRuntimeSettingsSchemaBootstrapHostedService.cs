using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Startup;

/// <summary>
/// Ensures WorkerRuntimeSettings schema exists early in runtime, even when full DB initialization
/// is disabled or deferred in the current process mode.
/// </summary>
public sealed class WorkerRuntimeSettingsSchemaBootstrapHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkerRuntimeSettingsSchemaBootstrapHostedService> _logger;

    public WorkerRuntimeSettingsSchemaBootstrapHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkerRuntimeSettingsSchemaBootstrapHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();

            if (!db.Database.IsRelational())
            {
                return;
            }

            var ready = await WorkerRuntimeSettingsSchemaGuard.EnsureSchemaAsync(
                db,
                _logger,
                stoppingToken);

            if (ready)
            {
                _logger.LogInformation("Worker runtime settings schema bootstrap completed.");
                return;
            }

            _logger.LogError("Worker runtime settings schema bootstrap is degraded: schema still missing.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful stop
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker runtime settings schema bootstrap failed.");
        }
    }
}
