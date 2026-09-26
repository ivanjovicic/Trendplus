namespace Application.Analytics;

/// <summary>
/// Shared date contract for date-only Operations analytics requests.
/// Upper bounds are exclusive: [fromUtc, toUtc).
/// </summary>
public static class OperationsDateRange
{
    public static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue) return null;

        var date = value.Value;
        return date.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : date.ToUniversalTime();
    }

    public static (DateTime? PreviousFromUtc, DateTime? PreviousToUtc) BuildComparablePreviousRange(
        DateTime? currentFromUtc,
        DateTime? currentToUtc)
    {
        if (!currentFromUtc.HasValue || !currentToUtc.HasValue || currentFromUtc.Value >= currentToUtc.Value)
        {
            return (null, null);
        }

        var duration = currentToUtc.Value - currentFromUtc.Value;
        var previousFromUtc = currentFromUtc.Value - duration;
        return (previousFromUtc, currentFromUtc.Value);
    }
}
