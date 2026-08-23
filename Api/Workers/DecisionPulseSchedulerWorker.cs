using Application.Analytics.DecisionPulse;
using Application.Common.Interfaces;
using Api.Services.Analytics;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Workers;

public sealed class DecisionPulseSchedulerWorker : BackgroundService
{
    private const string WorkerName = "DecisionPulseSchedulerWorker";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DecisionPulseSchedulerWorker> _logger;
    private readonly WorkerHealthService _healthService;
    private readonly WorkerRuntimeControlService _controlService;
    private readonly WorkerRuntimePolicyService _runtimePolicyService;

    public DecisionPulseSchedulerWorker(
        IServiceProvider serviceProvider,
        ILogger<DecisionPulseSchedulerWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService,
        WorkerRuntimePolicyService runtimePolicyService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
        _runtimePolicyService = runtimePolicyService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _healthService.ReportRunning(WorkerName, "Starting up...");
        var pauseCheckInterval = TimeSpan.FromSeconds(10);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_controlService.IsEnabled)
            {
                _healthService.ReportStopped(WorkerName, "Pauziran - workers switch je iskljucen.");
                await Task.Delay(pauseCheckInterval, stoppingToken);
                continue;
            }

            var policy = await _runtimePolicyService.GetPolicyAsync(WorkerName, stoppingToken);
            var manualRunRequested = false;
            if (!policy.CanRunNow)
            {
                _healthService.ReportStopped(WorkerName, policy.PauseReason ?? "Pauziran - worker policy disabled execution.");
                await Task.Delay(pauseCheckInterval, stoppingToken);
                continue;
            }

            if (policy.ManualRunRequested && !string.IsNullOrWhiteSpace(policy.ManualRunToken))
            {
                manualRunRequested = await _runtimePolicyService.TryConsumeManualRunRequestAsync(
                    WorkerName,
                    policy.ManualRunToken,
                    stoppingToken);

                if (!manualRunRequested)
                {
                    await Task.Delay(pauseCheckInterval, stoppingToken);
                    continue;
                }
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var scheduleService = scope.ServiceProvider.GetRequiredService<IDecisionPulseScheduleService>();
                var deliveryService = scope.ServiceProvider.GetRequiredService<DecisionPulseDeliveryService>();
                var schedules = await scheduleService.ListEnabledAsync(stoppingToken);

                var executed = 0;
                foreach (var schedule in schedules.Where(schedule => manualRunRequested || IsDue(schedule)))
                {
                    var result = await deliveryService.RunAsync(
                        schedule,
                        "decision-pulse-scheduler",
                        "Decision Pulse Scheduler",
                        manualTrigger: manualRunRequested,
                        stoppingToken);

                    await scheduleService.MarkRunResultAsync(schedule.Id, result, stoppingToken);
                    executed++;
                }

                _healthService.ReportHealthy(WorkerName, executed == 0 ? "No schedules due." : $"Executed {executed} schedules.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decision Pulse scheduler iteration failed.");
                _healthService.ReportError(WorkerName, ex);
            }

            var delay = manualRunRequested ? pauseCheckInterval : TimeSpan.FromMinutes(1);
            await Task.Delay(delay, stoppingToken);
        }

        _healthService.ReportStopped(WorkerName, "Graceful shutdown");
    }

    private static bool IsDue(DecisionPulseScheduleDefinition schedule)
    {
        var timeZone = ResolveTimeZone(schedule.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        if (!TimeSpan.TryParse(schedule.RunAtLocalTime, out var scheduledTime))
        {
            scheduledTime = TimeSpan.FromHours(8);
        }

        var scheduledToday = nowLocal.Date.Add(scheduledTime);
        if (nowLocal < scheduledToday)
        {
            return false;
        }

        var lastRunLocal = schedule.LastRunAtUtc.HasValue
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(schedule.LastRunAtUtc.Value, DateTimeKind.Utc), timeZone)
            : (DateTime?)null;

        if (string.Equals(schedule.Frequency, "weekly", StringComparison.OrdinalIgnoreCase))
        {
            if (schedule.DayOfWeek.HasValue && (int)nowLocal.DayOfWeek != schedule.DayOfWeek.Value)
            {
                return false;
            }

            return !lastRunLocal.HasValue || lastRunLocal.Value.Date < nowLocal.Date;
        }

        return !lastRunLocal.HasValue || lastRunLocal.Value.Date < nowLocal.Date;
    }

    internal static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Local;
        }

        if (TryResolveTimeZone(timeZoneId, out var resolved))
        {
            return resolved;
        }

        if (string.Equals(timeZoneId, "Europe/Belgrade", StringComparison.OrdinalIgnoreCase)
            && TryResolveTimeZone("Central Europe Standard Time", out var fallback))
        {
            return fallback;
        }

        return TimeZoneInfo.Local;
    }

    private static bool TryResolveTimeZone(string timeZoneId, out TimeZoneInfo resolved)
    {
        try
        {
            resolved = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        resolved = TimeZoneInfo.Local;
        return false;
    }
}
