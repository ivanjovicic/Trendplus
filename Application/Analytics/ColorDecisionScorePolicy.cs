namespace Application.Analytics;

/// <summary>
/// Backend-owned Color decision score. This is an evidence-strength score, not confidence
/// copied into another field, and it is never exposed when recommendation actionability is blocked.
/// </summary>
public static class ColorDecisionScorePolicy
{
    public const string Unit = "percent";
    public const string Denominator =
        "Red: 55% sigurnost preporuke + 30% pouzdanost + 15% prosečno pokriće troška i pre/post dokaza; zbirni skor je ponderisan prometom kroz akcionalne redove; dostupno samo kada je preporuka dozvoljena";

    public static double? Resolve(
        double? confidencePct,
        double? reliabilityPct,
        double? evidenceCoveragePct,
        bool recommendationAllowed)
    {
        if (!recommendationAllowed
            || !IsBoundedPercentage(confidencePct)
            || !IsBoundedPercentage(reliabilityPct)
            || !IsBoundedPercentage(evidenceCoveragePct))
        {
            return null;
        }

        var score = confidencePct!.Value * 0.55d
            + reliabilityPct!.Value * 0.30d
            + evidenceCoveragePct!.Value * 0.15d;

        return double.IsFinite(score)
            ? Math.Round(Math.Clamp(score, 0d, 100d), 2)
            : null;
    }

    private static bool IsBoundedPercentage(double? value)
        => value.HasValue && double.IsFinite(value.Value) && value.Value is >= 0d and <= 100d;
}
