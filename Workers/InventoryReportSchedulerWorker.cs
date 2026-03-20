using Application.Common.Interfaces;
using Infrastructure.Services;
using Infrastructure.Services.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Workers;

public sealed class InventoryReportSchedulerWorker : BackgroundService
{
    private const string WorkerName = "InventoryReportSchedulerWorker";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InventoryReportSchedulerWorker> _logger;
    private readonly WorkerHealthService _healthService;
    private readonly WorkerRuntimeControlService _controlService;

    public InventoryReportSchedulerWorker(
        IServiceProvider serviceProvider,
        ILogger<InventoryReportSchedulerWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _healthService.ReportRunning(WorkerName, "Starting up...");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_controlService.IsEnabled)
            {
                _healthService.ReportStopped(WorkerName, "Pauziran - workers switch je iskljucen.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var scheduleService = scope.ServiceProvider.GetRequiredService<IInventoryReportScheduleService>();
                var deliveryService = scope.ServiceProvider.GetRequiredService<InventoryReportDeliveryService>();
                var schedules = await scheduleService.ListEnabledAsync(stoppingToken);

                var executed = 0;
                foreach (var schedule in schedules.Where(IsDue))
                {
                    var result = await deliveryService.RunAsync(
                        schedule,
                        "inventory-scheduler",
                        "Inventory Scheduler",
                        manualTrigger: false,
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
                _logger.LogError(ex, "Inventory report scheduler iteration failed.");
                _healthService.ReportError(WorkerName, ex);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _healthService.ReportStopped(WorkerName, "Graceful shutdown");
    }

    private static bool IsDue(Application.Inventory.Models.InventoryReportScheduleDefinition schedule)
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

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            if (string.Equals(timeZoneId, "Europe/Belgrade", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
            }

            return TimeZoneInfo.Local;
        }
    }
}
