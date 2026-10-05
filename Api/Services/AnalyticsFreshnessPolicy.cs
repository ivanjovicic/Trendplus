using Api.Config;

namespace Api.Services;

public static class AnalyticsFreshnessPolicy
{
    public static string Resolve(
        DateTime? lastSuccessfulImportAtUtc,
        DateTime? lastFailureAtUtc,
        DateTime nowUtc,
        AnalyticsFreshnessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!lastSuccessfulImportAtUtc.HasValue)
        {
            return "unknown";
        }

        if (lastFailureAtUtc.HasValue && lastFailureAtUtc.Value > lastSuccessfulImportAtUtc.Value)
        {
            return "critical";
        }

        var age = nowUtc - lastSuccessfulImportAtUtc.Value;
        if (age <= TimeSpan.FromHours(options.WarningAfterHours))
        {
            return "fresh";
        }

        if (age <= TimeSpan.FromHours(options.CriticalAfterHours))
        {
            return "stale";
        }

        return "critical";
    }
}
