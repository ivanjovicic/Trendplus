namespace Application.Analytics.Queries.GetInventoryForecast;

public sealed record InventoryForecastSnapshotMaterializationRequest(
    int SkuId,
    int StoreId,
    int SupplierId,
    string SizeCode,
    DateTime ForecastBasisDateUtc,
    DateTime IssuedAtUtc,
    string MaterializerOwner,
    string ProvenanceStatus,
    DateTime? SnapshotFreshnessUtc,
    decimal? Forecast7d,
    decimal? Forecast14d,
    decimal? Forecast28d,
    decimal? ProbabilityOfOOSIn7d,
    decimal? OverstockRisk,
    decimal? ConfidenceScore,
    string Explanation);

public sealed record InventoryForecastSnapshotMaterializationResult(
    long ForecastSnapshotId,
    DateTime IssuedAtUtc);

public sealed record InventoryForecastObservedPairQuery(
    int? StoreId = null,
    int? SupplierId = null,
    int? SkuId = null,
    string? SizeCode = null,
    int? HorizonDays = null,
    int Top = 200);

public sealed record InventoryForecastObservedPairDto(
    long ForecastSnapshotId,
    int SkuId,
    int StoreId,
    int SupplierId,
    string SizeCode,
    DateTime ForecastBasisDate,
    DateTime IssuedAtUtc,
    int HorizonDays,
    DateTime ObservedDate,
    decimal? ForecastValue,
    decimal? ObservedQty,
    decimal? ReconstructedQty,
    decimal? StockQty,
    string? ObservedProvenance,
    string PairingStatus,
    string MaterializerOwner,
    string ProvenanceStatus,
    DateTime? SnapshotFreshnessUtc,
    string Explanation);
