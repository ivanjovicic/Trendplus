namespace Application.Analytics;

public static class BelgradeCalendarDatePolicy
{
    private static readonly Lazy<TimeZoneInfo> BelgradeTimeZone = new(ResolveTimeZone);

    public static DateTime StartOfDateUtc(DateOnly date) => ToUtc(date);

    public static DateTime EndOfDateExclusiveUtc(DateOnly date) => ToUtc(date.AddDays(1));

    private static DateTime ToUtc(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localStart, BelgradeTimeZone.Value);
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Belgrade");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
        }
    }
}
