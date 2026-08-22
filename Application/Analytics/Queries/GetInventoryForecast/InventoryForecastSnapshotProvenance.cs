namespace Application.Analytics.Queries.GetInventoryForecast;

/// <summary>
/// Fail-closed provenance for <c>analytics_inventory_forecast_snapshot</c>.
/// Current main has a read path only; no proven production materializer owns the table.
/// </summary>
public static class InventoryForecastSnapshotProvenance
{
    public const string MissingRelation = "missing_relation";
    public const string OwnerUnknown = "owner_unknown";
    public const string Stale = "stale";
    public const string Trusted = "trusted";

    /// <summary>Reserved until a real writer proves ownership. Never claim a fake owner.</summary>
    public const string UnprovenMaterializerOwner = "none";

    public const string MissingRelationWarning =
        "Forecast snapshot relacija nije dostupna (missing_relation). Nema dokazanog production materializera; surface je unavailable, ne forecasting proizvod.";

    public const string OwnerUnknownWarning =
        "Forecast snapshot je citljiv, ali materializer/owner nije dokazan (owner_unknown). Bounded unproven signal — ne production forecasting. GeneratedAtUtc je vreme odgovora, ne dokazana snapshot svezina. Stale/trusted nisu dostupni dok ne postoji writer sa freshness lineage.";

    public static InventoryForecastProvenanceContract ForMissingRelation() =>
        new(
            ProvenanceStatus: MissingRelation,
            MaterializerOwner: null,
            IsAuthoritativeForecast: false,
            SnapshotFreshnessUtc: null);

    public static InventoryForecastProvenanceContract ForReadableUnprovenOwner() =>
        new(
            ProvenanceStatus: OwnerUnknown,
            MaterializerOwner: UnprovenMaterializerOwner,
            IsAuthoritativeForecast: false,
            SnapshotFreshnessUtc: null);

    public static InventoryForecastProvenanceContract ForTrustedOwner(string materializerOwner, DateTime snapshotFreshnessUtc) =>
        new(
            ProvenanceStatus: Trusted,
            MaterializerOwner: materializerOwner,
            IsAuthoritativeForecast: true,
            SnapshotFreshnessUtc: snapshotFreshnessUtc);

    public static string ComposeWarning(string provenanceStatus, string? detailWarning)
    {
        var baseWarning = provenanceStatus switch
        {
            MissingRelation => MissingRelationWarning,
            OwnerUnknown => OwnerUnknownWarning,
            Stale => "Forecast snapshot postoji, ali sveznost nije poverljiva (stale). Ne tretirati kao production forecasting.",
            Trusted => null,
            _ => OwnerUnknownWarning
        };

        if (string.IsNullOrWhiteSpace(detailWarning))
        {
            return baseWarning ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(baseWarning))
        {
            return detailWarning;
        }

        return $"{baseWarning} {detailWarning}";
    }
}

public sealed record InventoryForecastProvenanceContract(
    string ProvenanceStatus,
    string? MaterializerOwner,
    bool IsAuthoritativeForecast,
    DateTime? SnapshotFreshnessUtc);
