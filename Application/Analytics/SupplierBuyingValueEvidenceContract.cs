namespace Application.Analytics;

/// <summary>
/// Source-of-truth availability for Supplier Overview buying-value metrics (RQ530).
/// Proven snapshot metrics may use Inventory balance/insights with store/supplier/dataScope filters.
/// Period sales, supplier-aggregated cover/sell-through, returns, margin trend, PO/lead-time stay unavailable until separately owned.
/// </summary>
public static class SupplierBuyingValueEvidenceContract
{
    public const string ContractVersion = "supplier_buying_value_v1";

    public const string InventoryBalanceRoute = "/api/analytics/cached/inventory/balance";

    public const string InventoryInsightsRoute = "/api/analytics/cached/inventory/insights";

    /// <summary>On-hand units, estimated inventory value, SKU count and aging buckets from Inventory snapshot endpoints.</summary>
    public const bool InventorySnapshotBuyingSignalsAvailable = true;

    /// <summary>No authoritative supplier-scoped days-of-cover aggregate on Supplier overview.</summary>
    public const bool SupplierAggregatedDaysCoverAvailable = false;

    /// <summary>No authoritative supplier-scoped sell-through aggregate on Supplier overview.</summary>
    public const bool SupplierAggregatedSellThroughAvailable = false;

    /// <summary>ReturnFact is not joined to Supplier overview population in this contract.</summary>
    public const bool SupplierGrossReturnRateAvailable = false;

    /// <summary>Equal-period margin trend and sales-based top/bottom articles require periodized Supplier sources.</summary>
    public const bool SupplierPeriodMarginTrendAvailable = false;

    public const bool SupplierSalesTopBottomArticlesAvailable = false;

    /// <summary>Purchase-order and lead-time sources are not proven in repository.</summary>
    public const bool PurchaseOrderLeadTimeAvailable = false;

    public static readonly IReadOnlyList<string> InventorySnapshotMetricKeys =
    [
        "totalOnHand",
        "estimatedInventoryValue",
        "totalSku",
        "agingBucketItemCount",
        "topAgedItems",
    ];

    public static readonly IReadOnlyList<string> UnavailableMetricReasonCodes =
    [
        "supplier_aggregated_days_cover_not_proven",
        "supplier_aggregated_sell_through_not_proven",
        "return_fact_not_joined_to_supplier_overview",
        "period_margin_trend_requires_supplier_sales_source",
        "sales_top_bottom_requires_periodized_supplier_source",
        "purchase_order_lead_time_source_absent",
    ];
}
