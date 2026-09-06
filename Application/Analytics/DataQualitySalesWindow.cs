namespace Application.Analytics;

/// <summary>
/// Shared Data Quality sales interval: calendar days ending today as
/// <c>[fromUtc, toExclusiveUtc)</c> in UTC. Used by health, top offenders and issues.
/// </summary>
public static class DataQualitySalesWindow
{
    public const int DefaultLookbackDays = 30;

    public static (DateTime FromUtc, DateTime ToExclusiveUtc) Resolve(int lookbackDays, DateTime? utcNow = null)
    {
        var todayUtc = (utcNow ?? DateTime.UtcNow).Date;
        var safeLookbackDays = Math.Max(1, lookbackDays);
        var fromUtc = todayUtc.AddDays(-(safeLookbackDays - 1));
        var toExclusiveUtc = todayUtc.AddDays(1);
        return (fromUtc, toExclusiveUtc);
    }
}
