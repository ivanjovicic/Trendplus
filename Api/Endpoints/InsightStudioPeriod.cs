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
}
