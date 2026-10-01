namespace Api.Services;

/// <summary>
/// Gives the existing distinct-sale-day ratio an explicit sample-activity meaning.
/// The source does not expose an authoritative completeness denominator, so data
/// coverage remains unavailable instead of being inferred from sale frequency.
/// </summary>
public static class VendorSalesNivelacijaActivityEvidencePolicy
{
    public const int WindowDays = 30;
    public const string DataCoverageStatus = "unavailable";
    public const string DataCoverageReason =
        "Izvor potvrđuje dane sa prodajom, ali ne i potpunost posmatranog 30-dnevnog prozora.";

    public static decimal? ProjectActivityRatePct(decimal? legacyCoverageRatio, bool observationWindowMature)
    {
        if (legacyCoverageRatio is >= 0m and <= 1m)
        {
            return Math.Round(legacyCoverageRatio.Value * 100m, 2);
        }

        // A mature window with no sale day is a measured zero-activity sample;
        // an immature window has no valid denominator yet.
        return observationWindowMature ? 0m : null;
    }

    public static int? ProjectActiveDays(decimal? activityRatePct)
    {
        if (activityRatePct is < 0m or > 100m)
        {
            return null;
        }

        return activityRatePct.HasValue
            ? (int)Math.Round(activityRatePct.Value / 100m * WindowDays, MidpointRounding.AwayFromZero)
            : null;
    }
}
