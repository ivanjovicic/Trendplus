using Api.Config;
using Npgsql;

namespace Api.Services.Startup;

public sealed class ReadinessWarmupHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly StartupReadinessState _readiness;
    private readonly ILogger<ReadinessWarmupHostedService> _logger;

    public ReadinessWarmupHostedService(
        IConfiguration configuration,
        StartupReadinessState readiness,
        ILogger<ReadinessWarmupHostedService> logger)
    {
        _configuration = configuration;
        _readiness = readiness;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxAttempts = Math.Max(1, _configuration.GetValue<int?>("StartupReadiness:MaxWarmupAttempts") ?? 12);
        var delaySeconds = Math.Max(1, _configuration.GetValue<int?>("StartupReadiness:RetryDelaySeconds") ?? 5);

        _readiness.MarkNotReady("db_warmup");

        for (var attempt = 1; attempt <= maxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            if (await ProbeConfiguredDatabasesAsync(stoppingToken))
            {
                _readiness.MarkReady();
                _logger.LogInformation("Startup readiness warmup succeeded on attempt {Attempt}/{MaxAttempts}.", attempt, maxAttempts);
                return;
            }

            _readiness.MarkNotReady("db_warmup_failed");
            _logger.LogWarning("Startup readiness warmup failed on attempt {Attempt}/{MaxAttempts}.", attempt, maxAttempts);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }

        _readiness.MarkNotReady("db_warmup_exhausted");
    }

    private async Task<bool> ProbeConfiguredDatabasesAsync(CancellationToken stoppingToken)
    {
        var defaultConnection = _configuration.GetConnectionString("DefaultConnection");
        AnalyticsConnectionResolution analyticsConnectionResolution;

        try
        {
            analyticsConnectionResolution = AnalyticsConnectionResolver.ResolveDetailed(
                _configuration,
                onWarning: message => _logger.LogWarning("{Message}", message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Startup readiness warmup cannot resolve analytics DB connection string.");
            return false;
        }

        var analyticsConnection = analyticsConnectionResolution.ConnectionString;

        _logger.LogInformation(
            "Startup readiness analytics DB target resolved. Source={Source} UsedFallback={UsedFallback} Target={Target}",
            analyticsConnectionResolution.Source,
            analyticsConnectionResolution.UsedFallback,
            AnalyticsConnectionResolver.SummarizeConnection(analyticsConnection));

        if (string.IsNullOrWhiteSpace(defaultConnection) || string.IsNullOrWhiteSpace(analyticsConnection))
        {
            _logger.LogWarning("Startup readiness warmup cannot run because one or more DB connection strings are missing.");
            return false;
        }

        return await ProbeDatabaseAsync(defaultConnection, stoppingToken) &&
               await ProbeDatabaseAsync(analyticsConnection, stoppingToken);
    }

    private async Task<bool> ProbeDatabaseAsync(string connectionString, CancellationToken stoppingToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            await using var connection = new NpgsqlConnection(WithProbeTimeout(connectionString));
            await connection.OpenAsync(timeoutCts.Token);

            await using var command = new NpgsqlCommand("SELECT 1;", connection)
            {
                CommandTimeout = 5
            };

            await command.ExecuteScalarAsync(timeoutCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Startup readiness DB probe failed.");
            return false;
        }
    }

    private static string WithProbeTimeout(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = 5,
            CommandTimeout = 5
        };

        return builder.ConnectionString;
    }
}
