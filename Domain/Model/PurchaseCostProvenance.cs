namespace Domain.Model;

/// <summary>
/// Evidence labels for a sale-line purchase cost. These labels describe provenance,
/// not whether the numeric value is currently usable for a margin calculation.
/// </summary>
public static class PurchaseCostProvenance
{
    public const string SourceSaleLine = "source_sale_line";
    public const string ConfirmedCalculation = "confirmed_calculation";
    public const string MasterRsdBackfill = "master_nabavnacena_din_backfill";
    public const string MasterLegacyBackfill = "master_nabavnacena_backfill";
    public const string Unknown = "unknown_provenance";

    public static bool IsConfirmed(string? value)
        => string.Equals(value, SourceSaleLine, StringComparison.Ordinal)
           || string.Equals(value, ConfirmedCalculation, StringComparison.Ordinal);

    public static bool IsEstimated(string? value)
        => string.Equals(value, MasterRsdBackfill, StringComparison.Ordinal)
           || string.Equals(value, MasterLegacyBackfill, StringComparison.Ordinal);

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Unknown : value.Trim();
}
