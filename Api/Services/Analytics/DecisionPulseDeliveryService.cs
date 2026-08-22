using Application.Analytics.DecisionPulse;
using Microsoft.Extensions.Logging;

namespace Api.Services.Analytics;

public sealed class DecisionPulseDeliveryService
{
    private readonly DecisionPulseService _pulseService;
    private readonly ILogger<DecisionPulseDeliveryService> _logger;

    public DecisionPulseDeliveryService(
        DecisionPulseService pulseService,
        ILogger<DecisionPulseDeliveryService> logger)
    {
        _pulseService = pulseService;
        _logger = logger;
    }

    public async Task<DecisionPulseScheduleRunResult> RunAsync(
        DecisionPulseScheduleDefinition schedule,
        string initiatedByUserId,
        string initiatedByUserName,
        bool manualTrigger,
        CancellationToken ct = default)
    {
        var executedAtUtc = DateTime.UtcNow;
        try
        {
            var feed = await _pulseService.GetFeedAsync(
                fromUtc: null,
                toUtc: null,
                storeId: schedule.StoreId,
                supplierId: schedule.SupplierId,
                dataScope: schedule.DataScope,
                ct);

            if (!feed.Meta.Success)
            {
                return new DecisionPulseScheduleRunResult(
                    false,
                    feed.Meta.ErrorCode ?? feed.Meta.WarningCode ?? "source_error",
                    feed.Meta.ErrorMessage ?? feed.Meta.WarningMessage ?? "Decision Pulse feed nije dostupan.",
                    executedAtUtc);
            }

            var recipients = ParseRecipients(schedule.RecipientsCsv);
            if (recipients.Count == 0)
            {
                return new DecisionPulseScheduleRunResult(
                    false,
                    "recipients_missing",
                    "Schedule nema validne primaoce.",
                    executedAtUtc);
            }

            var result = await _pulseService.SendEmailAsync(feed, recipients, ct);
            if (!result.Sent)
            {
                return new DecisionPulseScheduleRunResult(
                    false,
                    result.FailureCategory ?? "delivery_failed",
                    result.Message,
                    executedAtUtc);
            }

            return new DecisionPulseScheduleRunResult(
                true,
                "emailed",
                $"Poslato na {recipients.Count} primalaca. Trigger={(manualTrigger ? "manual" : "scheduled")}, user={initiatedByUserName} ({initiatedByUserId}).",
                executedAtUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decision Pulse schedule delivery failed for schedule {ScheduleId}", schedule.Id);
            return new DecisionPulseScheduleRunResult(
                false,
                "failed",
                ex.Message,
                executedAtUtc);
        }
    }

    private static List<string> ParseRecipients(string recipientsCsv)
    {
        return recipientsCsv
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static x => x.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
