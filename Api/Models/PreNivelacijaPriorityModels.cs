namespace Api.Models;

using Trendplus2.Dtos;

public sealed class PreNivelacijaPriorityResponseDto
{
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string FormulaVersion { get; set; } = "pre_nivelacija_v1";
    public string FormulaDescription { get; set; } = string.Empty;
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

public sealed class PreNivelacijaEvidenceWindowDto
{
    public DateTime SalesWindowFromUtc { get; set; }
    public DateTime SalesWindowToUtc { get; set; }
    public DateTime MarkdownWindowFromUtc { get; set; }
    public DateTime MarkdownWindowToUtc { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string SalesQuantityPolicy { get; set; } = "signed_net_quantity_preserved";
    public string NonPositiveNetPolicy { get; set; } = "recommendation_unavailable";
    public string PreviousWeekDenominatorPolicy { get; set; } = "unavailable_when_non_positive";
    public int CandidatesWithReturns { get; set; }
    public int CandidatesWithNonPositiveNetSales { get; set; }
    public int CandidatesWithoutSalesInWindow { get; set; }
    public int SuppliersWithUnavailablePreviousWeekDenominator { get; set; }
}

public sealed class PreNivelacijaFilterFacetsDto
{
    public List<PreNivelacijaFilterOptionDto> Seasons { get; set; } = [];
    public List<PreNivelacijaFilterOptionDto> FootwearTypes { get; set; } = [];
}

public sealed class PreNivelacijaFilterOptionDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
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
    public List<PreNivelacijaQueueItemDto> HighlightNow { get; set; } = [];
    public List<PreNivelacijaQueueItemDto> Monitor { get; set; } = [];
    public List<PreNivelacijaQueueItemDto> LikelyMarkdownSoon { get; set; } = [];
}

public sealed class PreNivelacijaQueueItemDto
{
    public int ArtikalId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string SupplierName { get; set; } = "N/A";
    public decimal PreNivelacijaScore { get; set; }
    public string PriorityBand { get; set; } = "neutral";
    public string Owner { get; set; } = "Unassigned";
    public string Status { get; set; } = "Unassigned";
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
    public int DaysSinceLastSale { get; set; }
    public int MarkdownEvents { get; set; }
    public decimal AvgMarkdownPct { get; set; }
    public decimal GrossMarginPctEst { get; set; }
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
}

public sealed class PreNivelacijaRecommendationDto
{
    public string Status { get; set; } = "insufficient_data";
    public string Label { get; set; } = "Insufficient data";
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
