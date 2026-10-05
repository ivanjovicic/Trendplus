namespace Api.Models;

using Trendplus2.Dtos;

public sealed class PreNivelacijaPriorityResponseDto
{
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string FormulaVersion { get; set; } = "pre_nivelacija_v10";
    public string FormulaDescription { get; set; } = string.Empty;
    public PreNivelacijaModelEvidenceDto ModelEvidence { get; set; } = new();
    public PreNivelacijaSummaryDto Summary { get; set; } = new();
    public List<PreNivelacijaSupplierActionDto> SupplierLeaderboard { get; set; } = [];
    public PreNivelacijaSupplierActionShareProjectionDto SupplierActionShare { get; set; } = new();
    public PreNivelacijaFilterFacetsDto FilterFacets { get; set; } = new();
    public List<PreNivelacijaSkuCandidateDto> Candidates { get; set; } = [];
    public PreNivelacijaQueuesDto Queues { get; set; } = new();
    public List<PreNivelacijaAlertDto> Alerts { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCandidates { get; set; }
    public bool RecommendationAllowed { get; set; }
    public PreNivelacijaEvidenceWindowDto EvidenceWindow { get; set; } = new();
    public AnalyticsResponseMetaDto? Meta { get; set; }
}

public sealed class PreNivelacijaModelEvidenceDto
{
    public string ScoreBasis { get; set; } = "cohort_relative_max_ratio";
    public string ScoreReferencePopulation { get; set; } = "base_candidate_universe_before_dimension_filters";
    public string ScoreNormalization { get; set; } = "stock_and_velocity_divided_by_reference_population_max";
    public string ScenarioBasis { get; set; } = "heuristic_uncalibrated";
    public string ScenarioParameterVersion { get; set; } = "pre_nivelacija_scenario_v2";
    public string ScenarioAssumptions { get; set; } = "highlight demand multiplier 1.15–1.45 over smoothed baseline; markdown discount 8–35%; markdown elasticity 1.8; expected units capped by available stock";
    public string ScenarioDisclaimer { get; set; } = "heuristic_estimate_not_causal_or_calibrated_uplift";
}

public sealed class PreNivelacijaEvidenceWindowDto
{
    public string AnchorBasis { get; set; } = "now";
    public DateTime AnchorDateUtc { get; set; }
    public DateTime? ObservedSourceHorizonUtc { get; set; }
    public string DecisionPopulationPolicy { get; set; } = "trend_plus_1_2_footwear_only";
    public DateTime SalesWindowFromUtc { get; set; }
    public DateTime SalesWindowToUtc { get; set; }
    public DateTime MarkdownWindowFromUtc { get; set; }
    public DateTime MarkdownWindowToUtc { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string ReceiptPopulationPolicy { get; set; } = "certified_retail_excludes_trimmed_case_insensitive_dug_korekcija";
    public string SalesQuantityPolicy { get; set; } = "signed_net_quantity_preserved";
    public string SignedReturnPolicy { get; set; } = "included_in_signed_net_positive_net_remains_actionable";
    public string LastSaleRecencyPolicy { get; set; } = "latest_positive_retail_sale_only";
    public string NonPositiveNetPolicy { get; set; } = "recommendation_unavailable";
    public string PreviousWeekDenominatorPolicy { get; set; } = "unavailable_when_non_positive";
    public int CandidatesWithReturns { get; set; }
    public int CandidatesWithNonPositiveNetSales { get; set; }
    public int CandidatesWithoutSalesInWindow { get; set; }
    public int SuppliersWithUnavailablePreviousWeekDenominator { get; set; }
}

public sealed class PreNivelacijaFilterFacetsDto
{
    public List<PreNivelacijaFilterOptionDto> Suppliers { get; set; } = [];
    public List<PreNivelacijaFilterOptionDto> Seasons { get; set; } = [];
    public List<PreNivelacijaFilterOptionDto> FootwearTypes { get; set; } = [];
    public List<PreNivelacijaFilterOptionDto> Stores { get; set; } = [];
}

public sealed class PreNivelacijaFilterOptionDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class PreNivelacijaSummaryDto
{
    public int SupplierCount { get; set; }
    public int CandidatesCount { get; set; }
    public int HighPriorityCount { get; set; }
    /// <summary>
    /// Counts are calculated over the complete filtered candidate universe, not the requested page.
    /// </summary>
    public int IncreaseFocusCount { get; set; }
    public int MaintainCount { get; set; }
    public int ReviewCount { get; set; }
    public int DoNotTrustCount { get; set; }
    public int InsufficientDataCount { get; set; }
    /// <summary>
    /// Stock units on high-priority candidates only. Null when no high-priority row exists.
    /// </summary>
    public int? TotalStockAtRisk { get; set; }
    public int TotalStockAtRiskCoverageEligible { get; set; }
    public int TotalStockAtRiskCoverageTotal { get; set; }
    /// <summary>
    /// Positive highlight-vs-markdown margin delta for candidates with complete cost/sales evidence.
    /// Null when no eligible row exists. Unit: RSD margin, not revenue.
    /// </summary>
    public decimal? EstimatedAvoidableMarkdownLoss { get; set; }
    public int EstimatedAvoidableMarkdownLossCoverageEligible { get; set; }
    public int EstimatedAvoidableMarkdownLossCoverageTotal { get; set; }
    /// <summary>
    /// Positive revenue uplift for allowed increase_focus ("Pojačaj") recommendations only.
    /// Null when no eligible row exists.
    /// </summary>
    public decimal? ExpectedHighlightRevenueUplift { get; set; }
    public int ExpectedHighlightRevenueUpliftCoverageEligible { get; set; }
    public int ExpectedHighlightRevenueUpliftCoverageTotal { get; set; }
    public decimal AveragePreNivelacijaScore { get; set; }
}

public sealed class PreNivelacijaSupplierActionShareProjectionDto
{
    public string ShareUnit { get; set; } = "percentage_points";
    public string WeekOverWeekRiskDeltaUnit { get; set; } = "percentage_points";
    public string DenominatorPolicy { get; set; } = "leaderboard_action_score_full_population_top_seven_plus_other";
    public string DenominatorLabel { get; set; } = string.Empty;
    public int LeaderboardSupplierCount { get; set; }
    public int VisibleSupplierCount { get; set; }
    public decimal TotalActionScore { get; set; }
    public decimal IncludedActionScore { get; set; }
    public decimal OtherActionScore { get; set; }
    public decimal? OtherSharePct { get; set; }
    public List<PreNivelacijaSupplierActionShareSegmentDto> Segments { get; set; } = [];
}

public sealed class PreNivelacijaSupplierActionShareSegmentDto
{
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = "N/A";
    public decimal ActionSharePct { get; set; }
    public decimal? WeekOverWeekRiskDeltaPct { get; set; }
    public string WeekOverWeekRiskDeltaUnit { get; set; } = "percentage_points";
    public bool IsOther { get; set; }
}

public sealed class PreNivelacijaSupplierActionDto
{
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = "N/A";
    public int HighPrioritySkuCount { get; set; }
    public int CandidateSkuCount { get; set; }
    public int StockUnitsAtRisk { get; set; }
    public decimal EstimatedAvoidableMarkdownLoss { get; set; }
    public decimal ExpectedHighlightRevenueUplift { get; set; }
    public decimal ActionScore { get; set; }
    public decimal? WeekOverWeekRiskDeltaPct { get; set; }
    public string WeekOverWeekEvidenceStatus { get; set; } = "unavailable_non_positive_denominator";
}

public sealed class PreNivelacijaQueuesDto
{
    public List<PreNivelacijaCleanupItemDto> LegacyCleanup { get; set; } = [];
    public int LegacyCleanupTotal { get; set; }
    public List<PreNivelacijaCleanupItemDto> NonFootwearCleanup { get; set; } = [];
    public int NonFootwearCleanupTotal { get; set; }
    public List<PreNivelacijaNewStockQueueItemDto> NewStock { get; set; } = [];
    public int NewStockTotal { get; set; }
    public List<PreNivelacijaQueueItemDto> HighlightNow { get; set; } = [];
    public int HighlightNowTotal { get; set; }
    public List<PreNivelacijaQueueItemDto> Monitor { get; set; } = [];
    public int MonitorTotal { get; set; }
    public List<PreNivelacijaQueueItemDto> LikelyMarkdownSoon { get; set; } = [];
    public int LikelyMarkdownSoonTotal { get; set; }
}

public sealed class PreNivelacijaCleanupItemDto
{
    public int ArtikalId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int? StoreId { get; set; }
    public string StoreName { get; set; } = "N/A";
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = "N/A";
    public int? SeasonId { get; set; }
    public string Season { get; set; } = "N/A";
    public int? FootwearTypeId { get; set; }
    public string FootwearType { get; set; } = "N/A";
    public int StockUnits { get; set; }
    public string[] ReasonCodes { get; set; } = [];
    public bool RecommendationAllowed { get; set; }
}

public sealed class PreNivelacijaNewStockQueueItemDto
{
    public int ArtikalId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int? StoreId { get; set; }
    public int? SupplierId { get; set; }
    public int? SeasonId { get; set; }
    public int? FootwearTypeId { get; set; }
    public string StoreName { get; set; } = "N/A";
    public string SupplierName { get; set; } = "N/A";
    public int StockUnits { get; set; }
    public DateTime FirstReceiptDateUtc { get; set; }
    public int DaysSinceReceipt { get; set; }
    public int? DaysSinceLastSale { get; set; }
    public string SalesHistoryStatus { get; set; } = "unknown";
    public string StockAgeStatus { get; set; } = "new_stock";
    public string ReasonCode { get; set; } = "new_stock";
}

public sealed class PreNivelacijaQueueItemDto
{
    public int ArtikalId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int? StoreId { get; set; }
    public string StoreName { get; set; } = "N/A";
    public string SupplierName { get; set; } = "N/A";
    public decimal PreNivelacijaScore { get; set; }
    public string PriorityBand { get; set; } = "neutral";
    public string Owner { get; set; } = "Nedodeljeno";
    public string Status { get; set; } = "Nedodeljeno";
    public DateTime DueDateUtc { get; set; }
}

public sealed class PreNivelacijaAlertDto
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
    public string Message { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public int? ArtikalId { get; set; }
}

public sealed class PreNivelacijaSkuCandidateDto
{
    public int ArtikalId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int? StoreId { get; set; }
    public string StoreName { get; set; } = "N/A";
    public int? SupplierId { get; set; }
    public int? SeasonId { get; set; }
    public int? FootwearTypeId { get; set; }
    public string SupplierName { get; set; } = "N/A";
    public string Category { get; set; } = "N/A";
    public string FootwearType { get; set; } = "N/A";
    public string Season { get; set; } = "N/A";
    public int StockUnits { get; set; }
    public int Units180 { get; set; }
    public int PositiveUnits180 { get; set; }
    public int NegativeUnits180 { get; set; }
    public decimal Velocity180 { get; set; }
    public int? DaysSinceLastSale { get; set; }
    public string SalesHistoryStatus { get; set; } = "unknown";
    public DateTime? FirstReceiptDateUtc { get; set; }
    public int? DaysSinceReceipt { get; set; }
    public string ReceiptEvidenceStatus { get; set; } = "unknown";
    public string StockAgeStatus { get; set; } = "unknown";
    public int MarkdownEvents { get; set; }
    public decimal AvgMarkdownPct { get; set; }
    public decimal? GrossMarginPctEst { get; set; }
    public decimal? GrossMarginPctSigned { get; set; }
    public bool? BelowCost { get; set; }
    public decimal SeasonRecencyBoost { get; set; }
    public decimal PreNivelacijaScore { get; set; }
    public string PriorityBand { get; set; } = "neutral";
    public PreNivelacijaScoreBreakdownDto ScoreBreakdown { get; set; } = new();
    public PreNivelacijaScenarioDto ScenarioHighlightNow { get; set; } = new();
    public PreNivelacijaScenarioDto ScenarioMarkdownNow { get; set; } = new();
    public decimal MarginDeltaHighlightVsMarkdown { get; set; }
    public decimal RevenueDeltaHighlightVsMarkdown { get; set; }
    public bool HasCompleteEvidence { get; set; }
    public string? EvidenceReason { get; set; }
    public string SalesEvidenceStatus { get; set; } = "no_sales_in_window";
    public string? SalesEvidenceReason { get; set; }
    public string Confidence { get; set; } = "Low";
    public double ReliabilityPct { get; set; }
    public int DecisionScore { get; set; }
    public PreNivelacijaRecommendationDto Recommendation { get; set; } = new();
    public bool RecommendationAllowed => Recommendation.RecommendationAllowed;

    /// <summary>Cached sales window inputs used to rebuild supplier WoW after facet filtering.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int Units7 { get; set; }

    /// <summary>Cached sales window inputs used to rebuild supplier WoW after facet filtering.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int UnitsPrev7 { get; set; }
}

public sealed class PreNivelacijaRecommendationDto
{
    public string Status { get; set; } = "insufficient_data";
    public string Label { get; set; } = "Nedovoljno podataka";
    public string Summary { get; set; } = string.Empty;
    public double ConfidencePct { get; set; }
    public double ReliabilityPct { get; set; }
    public string DataQualityStatus { get; set; } = "critical";
    public bool RecommendationAllowed { get; set; }
    public IReadOnlyList<string> ReasonCodes { get; set; } = [];
}

public sealed class PreNivelacijaScoreBreakdownDto
{
    public decimal StockPressure { get; set; }
    public decimal VelocityRisk { get; set; }
    public decimal RecencyRisk { get; set; }
    public decimal MarkdownOpportunity { get; set; }
    public decimal MarginPotential { get; set; }
    public decimal SeasonRecencyBoost { get; set; }
}

public sealed class PreNivelacijaScenarioDto
{
    public int ExpectedUnits30d { get; set; }
    public decimal ExpectedRevenue30d { get; set; }
    public decimal ExpectedMargin30d { get; set; }
    public decimal EffectivePrice { get; set; }
}
