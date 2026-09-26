namespace Infrastructure.Services;

public static class OperationsAnalyticsIntegrityProbeWindow
{
    public static (DateTime FromUtc, DateTime ToUtc) Resolve(DateTime checkedAtUtc, int probeLookbackDays)
    {
        var lookbackDays = Math.Max(1, probeLookbackDays);
        var fromUtc = DateTime.SpecifyKind(checkedAtUtc.Date.AddDays(-(lookbackDays - 1)), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(checkedAtUtc.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
        return (fromUtc, toUtc);
    }
}
