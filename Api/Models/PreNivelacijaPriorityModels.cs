namespace Api.Models;

public sealed class PreNivelacijaPriorityResponseDto
{
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string FormulaVersion { get; set; } = "pre_nivelacija_v1";
    public string FormulaDescription { get; set; } = string.Empty;
    public PreNivelacijaSummaryDto Summary { get; set; } = new();
    public List<PreNivelacijaSupplierActionDto> SupplierLeaderboard { get; set; } = [];
    public List<PreNivelacijaSkuCandidateDto> Candidates { get; set; } = [];
    public PreNivelacijaQueuesDto Queues { get; set; } = new();
    public List<PreNivelacijaAlertDto> Alerts { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCandidates { get; set; }
}

public sealed class PreNivelacijaSummaryDto
{
    public int SupplierCount { get; set; }
    public int CandidatesCount { get; set; }
    public int HighPriorityCount { get; set; }
    public int TotalStockAtRisk { get; set; }
    public decimal EstimatedAvoidableMarkdownLoss { get; set; }
    public decimal ExpectedHighlightRevenueUplift { get; set; }
    public decimal AveragePreNivelacijaScore { get; set; }
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
    public decimal WeekOverWeekRiskDeltaPct { get; set; }
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
    public string Confidence { get; set; } = "Low";
    public double ReliabilityPct { get; set; }
    public int DecisionScore { get; set; }
    public PreNivelacijaRecommendationDto Recommendation { get; set; } = new();
}

public sealed class PreNivelacijaRecommendationDto
{
    public string Status { get; set; } = "insufficient_data";
    public string Label { get; set; } = "Insufficient data";
    public string Summary { get; set; } = string.Empty;
    public double ConfidencePct { get; set; }
    public double ReliabilityPct { get; set; }
    public string DataQualityStatus { get; set; } = "critical";
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
