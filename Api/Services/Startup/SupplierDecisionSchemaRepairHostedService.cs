using Infrastructure.Seed;

namespace Api.Services.Startup;

public sealed class SupplierDecisionSchemaRepairHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SupplierDecisionSchemaRepairHostedService> _logger;

    public SupplierDecisionSchemaRepairHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<SupplierDecisionSchemaRepairHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool?>("StartupTasks:RunSupplierDecisionSchemaRepair") ?? true;
        if (!enabled)
        {
            _logger.LogInformation("Supplier decision schema repair is disabled.");
            return;
        }

        var startDelaySeconds = Math.Max(
            0,
            _configuration.GetValue<int?>("StartupTasks:SupplierDecisionSchemaRepairDelaySeconds") ?? 8);
        var maxRetries = Math.Max(
            1,
            _configuration.GetValue<int?>("StartupTasks:SupplierDecisionSchemaRepairMaxRetries") ?? 5);

        if (startDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(startDelaySeconds), stoppingToken);
        }

        for (var attempt = 1; attempt <= maxRetries && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Supplier decision schema repair starting. Attempt {Attempt}/{MaxRetries}.",
                    attempt,
                    maxRetries);

                await DatabaseInitializer.EnsureAnalyticsSupplierDecisionSchemaAsync(
                    _serviceProvider,
                    _configuration,
                    _logger);

                _logger.LogInformation(
                    "Supplier decision schema repair succeeded. Attempt {Attempt}/{MaxRetries}.",
                    attempt,
                    maxRetries);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Supplier decision schema repair failed. Attempt {Attempt}/{MaxRetries}.",
                    attempt,
                    maxRetries);

                if (attempt >= maxRetries)
                {
                    break;
                }

                var delaySeconds = Math.Min(60, attempt * 10);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }
        }

        _logger.LogWarning("Supplier decision schema repair did not complete successfully.");
    }
}
