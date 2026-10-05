namespace Trendplus2.Endpoints;

internal static class InsightStudioPeriod
{
    // Query-bound date-only values are Unspecified; interpret them in the
    // runtime's local zone, while preserving explicit UTC/local instants.
    internal static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(value, TimeZoneInfo.Local)
        };
    }

    internal static DateTime? ToUtc(DateTime? value) =>
        value.HasValue ? ToUtc(value.Value) : null;

    // Date-only query bounds are inclusive calendar dates. Timestamp bounds
    // remain inclusive instants by converting their upper edge to an exclusive
    // tick boundary.
    internal static DateTime ToUtcExclusiveEnd(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified && value.TimeOfDay == TimeSpan.Zero)
            return ToUtc(value.Date.AddDays(1));

        return ToUtc(value).AddTicks(1);
    }
}
