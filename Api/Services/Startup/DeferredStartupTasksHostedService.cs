using Application.Artikli.Common.Interfaces;
using Api.Services;
using Infrastructure.DbContexts;
using Infrastructure.Seed;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Infrastructure.Logging;

namespace Api.Services.Startup;

public sealed class DeferredStartupTasksHostedService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<DeferredStartupTasksHostedService> _logger;
    private Task? _startupTask;
    private CancellationTokenSource? _startupTaskCts;

    public DeferredStartupTasksHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<DeferredStartupTasksHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            _startupTaskCts = new CancellationTokenSource();
            _startupTask = Task.Run(() => RunDeferredStartupTasksAsync(_startupTaskCts.Token), CancellationToken.None);
        });

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_startupTaskCts is not null)
        {
            try
            {
                _startupTaskCts.Cancel();
            }
            catch
            {
                // Best effort cancellation.
            }
        }

        if (_startupTask is null)
            return;

        try
        {
            await _startupTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down, ignore.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Deferred startup tasks completed with an exception during shutdown.");
        }
        finally
        {
            _startupTaskCts?.Dispose();
            _startupTaskCts = null;
            _startupTask = null;
        }
    }

    public void Dispose()
    {
        _startupTaskCts?.Dispose();
        _startupTaskCts = null;
    }

    private async Task RunDeferredStartupTasksAsync(CancellationToken ct)
    {
        var runDatabaseInitialization = _configuration.GetValue<bool?>("StartupTasks:RunDatabaseInitialization") ?? true;
        var runNeonWarmup = _configuration.GetValue<bool?>("StartupTasks:RunNeonWarmup") ?? true;
        var runStaleBatchRecovery = _configuration.GetValue<bool?>("AccessImport:RunStaleRecoveryOnStartup") ?? true;
        var maxRetries = Math.Max(1, _configuration.GetValue<int?>("StartupTasks:DatabaseInitializationMaxRetries") ?? 5);

        _logger.LogInformation(
            "Deferred startup tasks started. RunDatabaseInitialization: {RunDatabaseInitialization}. RunNeonWarmup: {RunNeonWarmup}. RunStaleBatchRecovery: {RunStaleBatchRecovery}. MaxRetries: {MaxRetries}.",
            runDatabaseInitialization,
            runNeonWarmup,
            runStaleBatchRecovery,
            maxRetries);

        if (runNeonWarmup)
        {
            try
            {
                await using var warmupScope = _serviceProvider.CreateAsyncScope();
                var trendDb = warmupScope.ServiceProvider.GetRequiredService<ITrendplusDbContext>();
                var analyticsDb = warmupScope.ServiceProvider.GetRequiredService<IAnalyticsDbContext>();

                var trendOk = await DbConnectionHelper.TryExecuteSqlProbeAsync((DbContext)trendDb, _logger, ct);
                var analyticsOk = await DbConnectionHelper.TryExecuteSqlProbeAsync((DbContext)analyticsDb, _logger, ct);

                if (trendOk && analyticsOk)
                {
                    _logger.LogInformation("[NeonWarmup] Databases are awake.");
                }
                else
                {
                    _logger.LogWarning("[NeonWarmup] Warmup probe reported failures. trendOk={TrendOk} analyticsOk={AnalyticsOk}", trendOk, analyticsOk);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogInformation("Deferred startup tasks cancelled during Neon warmup.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NeonWarmup] Warmup ping failed.");
            }
        }

        if (runDatabaseInitialization)
        {
            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                if (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Deferred startup tasks cancelled before database initialization attempt {Attempt}.", attempt);
                    return;
                }

                try
                {
                    await using var initScope = _serviceProvider.CreateAsyncScope();
                    var services = initScope.ServiceProvider;
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    var configuration = services.GetRequiredService<IConfiguration>();

                    await DatabaseInitializer.InitializeDatabasesAsync(services, configuration, logger);
                    _logger.LogInformation("Database initialization succeeded on deferred attempt {Attempt}/{MaxRetries}.", attempt, maxRetries);
                    break;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Deferred startup tasks cancelled during database initialization.");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database initialization failed on deferred attempt {Attempt}/{MaxRetries}.", attempt, maxRetries);
                    if (attempt < maxRetries)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(attempt * 5), ct);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            _logger.LogInformation("Deferred startup tasks cancelled while waiting for next DB init retry.");
                            return;
                        }
                    }
                }
            }
        }

        if (runStaleBatchRecovery)
        {
            try
            {
                await using var recoveryScope = _serviceProvider.CreateAsyncScope();
                var accessImportService = recoveryScope.ServiceProvider.GetRequiredService<IAccessImportService>();
                await accessImportService.RefreshBatchStatusesAsync(ct: ct);
                _logger.LogInformation("Access import stale batch recovery completed.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogInformation("Deferred startup tasks cancelled during stale batch recovery.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Access import stale batch recovery failed.");
            }
        }

        _logger.LogInformation("Deferred startup tasks completed.");
    }
}
