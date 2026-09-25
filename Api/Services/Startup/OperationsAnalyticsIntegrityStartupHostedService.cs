using Infrastructure.Configuration;
using Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Api.Services.Startup;

/// <summary>
/// Runs one bounded Operations integrity probe after the web host starts so fresh deployments
/// are not stuck in bootstrap <c>unverified</c> until the worker interval elapses.
/// </summary>
public sealed class OperationsAnalyticsIntegrityStartupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OperationsAnalyticsIntegrityStartupHostedService> _logger;
    private readonly OperationsAnalyticsIntegrityOptions _options;

    public OperationsAnalyticsIntegrityStartupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OperationsAnalyticsIntegrityStartupHostedService> logger,
        IOptions<OperationsAnalyticsIntegrityOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.StartupDelaySeconds)), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var integrityService = scope.ServiceProvider.GetRequiredService<IOperationsAnalyticsIntegrityService>();
            var snapshot = await integrityService.RunBoundedProbeAsync(stoppingToken);
            _logger.LogInformation(
                "Operations integrity startup probe finished with status {Status} (evidence {EvidenceId}).",
                snapshot.Status,
                snapshot.EvidenceId);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Operations integrity startup probe failed; registry remains in prior state.");
        }
    }
}
