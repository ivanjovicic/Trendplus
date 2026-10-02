namespace Application.Analytics;

/// <summary>
/// Source-of-truth availability for Supplier Assortment size-curve and controlled markdown uplift (RQ532).
/// Negative-path acceptance: when aggregation grain is absent, surfaces must stay unavailable — never inferred from SKU/store share curves or raw pre/post.
/// </summary>
public static class SupplierAssortmentSizeCurveEvidenceContract
{
    public const string ContractVersion = "supplier_assortment_size_curve_v1";

    public const string InventorySizeCurveRoute = "/api/analytics/cached/inventory/size-curve";

    public const string SizeCurveSnapshotRelation = "analytics_size_curve_snapshot";

    public const string ControlledDidView = "vw_nivelacija_did";

    /// <summary>Repository has no authoritative supplier × footwear-type sold/received/on-hand by size contract.</summary>
    public const bool SupplierFootwearSizeCurveAvailable = false;

    /// <summary>Assortment tab may expose descriptive pre/post only; causal DiD uplift requires a separate approved contract.</summary>
    public const bool ControlledMarkdownUpliftAvailable = false;

    public static readonly IReadOnlyList<string> InventorySizeCurveSignalFields =
    [
        "skuId",
        "storeId",
        "sizeCode",
        "actualSizeShare",
        "idealSizeShare",
        "deviationPct",
        "curveConfidence",
    ];

    public static readonly IReadOnlyList<string> MissingSupplierAggregationPropertyNames =
    [
        "SupplierId",
        "FootwearType",
        "SoldQty",
        "ReceivedQty",
        "OnHandQty",
    ];

    public static readonly IReadOnlyList<string> SizeCurveUnavailableReasonCodes =
    [
        "no_repository_owned_size_curve_schema",
        "inventory_size_curve_sku_store_share_only",
        "missing_supplier_footwear_type_grain",
        "missing_sold_received_on_hand_by_size",
    ];

    public static readonly IReadOnlyList<string> ControlledUpliftUnavailableReasonCodes =
    [
        "assortment_descriptive_pre_post_only",
        "did_view_or_population_contract_missing",
        "no_maturity_control_confidence_metadata",
        "raw_pre_post_not_causal_uplift",
    ];
}
