namespace Trendplus2.Dtos;

public sealed record InventoryHistoryItemDto(
    int MovementId,
    string TipPromene,
    DateTime Datum,
    int? Kolicina,
    decimal Iznos,
    string? BrojDokumenta,
    string? KorisnikIme,
    string? DataOrigin,
    int? StoreId,
    string? StoreName,
    int? SupplierId,
    string? SupplierName,
    decimal? StaraCena,
    decimal? NovaCena,
    string? Komentar
);

public sealed record InventoryItemDetailDto(
    int Id,
    string? PLU,
    string Naziv,
    int? Kolicina,
    int? MinimalnaKolicina,
    decimal? NabavnaCena,
    decimal? EstimatedValue,
    int? StoreId,
    string? StoreName,
    int? SupplierId,
    string? SupplierName,
    string? Kategorija,
    string? Pol,
    string? Materijal,
    DateTime UpdatedAt,
    DateTime? LastMovementAt,
    int MovementCount30d,
    int DaysSinceMovement,
    string AgingBucket,
    string AgingLabel,
    string AbcClass,
    decimal? StockCoverDays,
    string StockCoverStatus,
    string StockCoverStatusLabel,
    decimal? SellThroughRatio,
    string SellThroughStatus,
    string SellThroughStatusLabel,
    decimal SignalConfidencePct,
    bool RecommendationAllowed,
    string DataQualityStatus,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<InventoryHistoryItemDto> History
);

public sealed record InventoryAgingBucketDto(
    string BucketKey,
    string Label,
    int ItemCount,
    int TotalUnits,
    decimal EstimatedValue
);

public sealed record InventoryAbcBucketDto(
    string BucketKey,
    string Label,
    int ItemCount,
    decimal EstimatedValue,
    decimal ValueSharePct
);

public sealed record InventoryInsightItemDto(
    int Id,
    string? PLU,
    string Naziv,
    string? SupplierName,
    string? StoreName,
    int Quantity,
    int Minimum,
    int ReorderGap,
    decimal EstimatedValue,
    int DaysSinceMovement,
    string AgingBucket,
    string AgingLabel,
    string AbcClass,
    string StockState,
    decimal? StockCoverDays,
    string StockCoverStatus,
    string StockCoverStatusLabel,
    decimal? SellThroughRatio,
    string SellThroughStatus,
    string SellThroughStatusLabel,
    decimal SignalConfidencePct,
    bool RecommendationAllowed,
    string DataQualityStatus,
    IReadOnlyList<string> ReasonCodes
);

public sealed record InventoryInsightsDto(
    int TotalItems,
    decimal TotalEstimatedValue,
    IReadOnlyList<InventoryAgingBucketDto> Aging,
    IReadOnlyList<InventoryAbcBucketDto> Abc,
    IReadOnlyList<InventoryInsightItemDto> TopAgedItems,
    IReadOnlyList<InventoryInsightItemDto> TopCapitalLockedItems,
    AnalyticsResponseMetaDto? Meta = null
);

public sealed class InventoryExportRequestDto
{
    public string Format { get; set; } = "pdf";
    public string Orientation { get; set; } = "landscape";
    public bool IncludeFiltersAndMetadata { get; set; } = true;
    public bool ForceAsync { get; set; }
    public string? Search { get; set; }
    public int? StoreId { get; set; }
    public int? SupplierId { get; set; }
    public string? SortBy { get; set; }
}

public sealed record InventoryStoreComparisonItemDto(
    int StoreId,
    string StoreName,
    int TotalSku,
    int TotalOnHand,
    int LowStockCount,
    int OutOfStockCount,
    int CriticalCount,
    int Stale90PlusCount,
    decimal EstimatedValue,
    decimal AvgUnitsPerSku,
    decimal HealthySharePct
);

public sealed record InventoryStoreComparisonFocusDto(
    string SkuKey,
    string Label,
    int StoreCoverage,
    IReadOnlyList<string> ImpactedStores
);

public sealed record InventoryStoreComparisonDto(
    DateTime GeneratedAtUtc,
    IReadOnlyList<InventoryStoreComparisonItemDto> Stores,
    IReadOnlyList<InventoryStoreComparisonFocusDto> SharedRisks,
    string Summary,
    AnalyticsResponseMetaDto? Meta = null
);

public sealed record InventoryActionSuggestionDto(
    string SuggestionKey,
    string ActionType,
    string Priority,
    string Label,
    string Reason,
    string Status,
    int ArtikalId,
    string? PLU,
    string Naziv,
    string? FromStoreName,
    string? ToStoreName,
    int SuggestedQty,
    decimal EstimatedValue,
    int DaysSinceMovement,
    string? Note,
    DateTime? UpdatedAtUtc,
    decimal? SignalConfidencePct = null,
    bool? RecommendationAllowed = null,
    string? SignalDataQualityStatus = null,
    IReadOnlyList<string>? SignalReasonCodes = null
);

public sealed record InventoryActionWorkflowDto(
    DateTime GeneratedAtUtc,
    int PendingCount,
    int ApprovedCount,
    int DeferredCount,
    int ClosedCount,
    IReadOnlyList<InventoryActionSuggestionDto> Items,
    AnalyticsResponseMetaDto? Meta = null
);

public sealed class InventoryActionDecisionRequestDto
{
    public string ActionType { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? Note { get; set; }
}

public sealed class InventoryReportScheduleDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Frequency { get; set; } = "daily";
    public int? DayOfWeek { get; set; }
    public string RunAtLocalTime { get; set; } = "08:00";
    public string TimeZoneId { get; set; } = "Europe/Belgrade";
    public string Format { get; set; } = "pdf";
    public string Orientation { get; set; } = "landscape";
    public bool IncludeFiltersAndMetadata { get; set; } = true;
    public string RecipientsCsv { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Search { get; set; }
    public int? StoreId { get; set; }
    public int? SupplierId { get; set; }
    public string? SortBy { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
    public string? LastRunStatus { get; set; }
    public string? LastError { get; set; }
    public Guid? LastDocumentId { get; set; }
}

public sealed class InventoryReportScheduleUpsertDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string Frequency { get; set; } = "daily";
    public int? DayOfWeek { get; set; }
    public string RunAtLocalTime { get; set; } = "08:00";
    public string TimeZoneId { get; set; } = "Europe/Belgrade";
    public string Format { get; set; } = "pdf";
    public string Orientation { get; set; } = "landscape";
    public bool IncludeFiltersAndMetadata { get; set; } = true;
    public string RecipientsCsv { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Search { get; set; }
    public int? StoreId { get; set; }
    public int? SupplierId { get; set; }
    public string? SortBy { get; set; }
}

public sealed record InventoryScheduleRunResponseDto(
    bool Success,
    string Status,
    string Message,
    Guid? DocumentId,
    DateTime ExecutedAtUtc
);
