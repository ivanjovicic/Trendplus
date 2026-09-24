namespace Application.Analytics.Queries.GetInventoryForecast;

/// <summary>
/// Stable contract metadata for inventory forecast list responses (grain, aggregation, evidence scope).
/// </summary>
public static class InventoryForecastListContract
{
    /// <summary>Each item row is one SKU + store + size snapshot.</summary>
    public const string RowGrain = "sku-store-size";

    /// <summary>SKU-level display risk is the max across size rows for the same SKU and store.</summary>
    public const string RiskAggregationPolicy = "max-by-sku-store-across-sizes";

    /// <summary>Forecast is a current materialized snapshot; it does not follow Inventory analysis-period selection.</summary>
    public const string EvidenceScope = "current-snapshot-not-analysis-period";
}
