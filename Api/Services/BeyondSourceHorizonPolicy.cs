namespace Api.Services;

public static class BeyondSourceHorizonPolicy
{
    public const string ReasonCode = "beyond_source_horizon";

    public static bool IsBeyond(DateTime? requestedToExclusiveUtc, DateTime? observedHorizonUtc)
        => requestedToExclusiveUtc.HasValue
            && observedHorizonUtc.HasValue
            && requestedToExclusiveUtc.Value.Date > observedHorizonUtc.Value.Date.AddDays(1);
}
