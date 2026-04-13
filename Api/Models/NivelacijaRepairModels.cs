namespace Api.Models;

public sealed record NivelacijaRepairRequest(
    bool DryRun = true,
    bool Confirm = false,
    string? SourceFilePath = null,
    int MaxRowsToModify = 10_000);

public sealed class NivelacijaRepairPreflightDto
{
    public string ResolvedSourceFilePath { get; set; } = string.Empty;
    public bool DatabaseReachable { get; set; }
    public int DefaultMaxRowsThreshold { get; set; }
    public Dictionary<string, bool> RequiredObjects { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> AccessTables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Warnings { get; set; } = [];
}

public sealed class NivelacijaRepairIssueDto
{
    public int PriceEventId { get; set; }
    public int ArticleId { get; set; }
    public int? SourceHeaderId { get; set; }
    public int? VendorId { get; set; }
    public DateOnly EventDate { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? NewPrice { get; set; }
    public decimal? CalculatedPreSales { get; set; }
    public decimal? CalculatedPostSales { get; set; }
    public string DetectedIssueType { get; set; } = string.Empty;
    public int? CurrentStoreId { get; set; }
    public int? CurrentVendorId { get; set; }
    public DateOnly? ProposedEventDate { get; set; }
    public int? ProposedStoreId { get; set; }
    public int? ProposedVendorId { get; set; }
    public bool Fixable { get; set; }
}

public sealed class NivelacijaRepairFixDto
{
    public int PriceEventId { get; set; }
    public int ArticleId { get; set; }
    public int? SourceHeaderId { get; set; }
    public DateOnly CurrentEventDate { get; set; }
    public DateOnly TargetEventDate { get; set; }
    public int? CurrentStoreId { get; set; }
    public int? TargetStoreId { get; set; }
    public int? CurrentVendorId { get; set; }
    public int? TargetVendorId { get; set; }
    public List<string> FieldsChanged { get; set; } = [];
}

public sealed class NivelacijaRepairEstimatedImpactDto
{
    public int CandidateRowsScanned { get; set; }
    public int DetectedIssuesCount { get; set; }
    public int ProposedFixesCount { get; set; }
    public int MissingSourceMappings { get; set; }
    public int UpdatedDateRows { get; set; }
    public int UpdatedStoreRows { get; set; }
    public int UpdatedVendorRows { get; set; }
    public int MaxRowsThreshold { get; set; }
    public bool ExceedsThreshold { get; set; }
    public bool CanExecute { get; set; }
}

public sealed class NivelacijaRepairAggregateVerificationDto
{
    public int AccessLineRows { get; set; }
    public int AccessDistinctEvents { get; set; }
    public int ImportedLineRows { get; set; }
    public int ImportedHeaderRows { get; set; }
    public int PreRows { get; set; }
    public int PostRows { get; set; }
    public int VendorRows { get; set; }
    public int VendorDistinctEvents { get; set; }
    public int ImportedDistinctSourceHeaders { get; set; }
    public decimal PreQtySum { get; set; }
    public decimal PreRevenueSum { get; set; }
    public decimal PostQtySum { get; set; }
    public decimal PostRevenueSum { get; set; }
    public decimal VendorPreQtySum { get; set; }
    public decimal VendorPreRevenueSum { get; set; }
    public decimal VendorPostQtySum { get; set; }
    public decimal VendorPostRevenueSum { get; set; }
    public bool AccessLinesMatchVendorRows { get; set; }
    public bool PreQtyMatchesVendorQty { get; set; }
    public bool PreRevenueMatchesVendorRevenue { get; set; }
    public bool PostQtyMatchesVendorQty { get; set; }
    public bool PostRevenueMatchesVendorRevenue { get; set; }
    public bool AccessEventsMatchImportedSourceHeaders { get; set; }
}

public sealed class NivelacijaRepairEdgeCaseVerificationDto
{
    public int ImportedDuplicateGroups { get; set; }
    public int ViewDuplicateGroups { get; set; }
    public int ZeroSalesPeriodRows { get; set; }
    public int InactiveRows { get; set; }
    public int MultipleChangesSameDayRows { get; set; }
    public int AccessMultipleChangesSameDayRows { get; set; }
    public int? OutOfStockEventRows { get; set; }
    public string OutOfStockCheckStatus { get; set; } = "unknown";
}

public sealed class NivelacijaRepairVerificationDto
{
    public NivelacijaRepairAggregateVerificationDto Aggregate { get; set; } = new();
    public NivelacijaRepairEdgeCaseVerificationDto EdgeCases { get; set; } = new();
}

public sealed class NivelacijaRepairPlanDto
{
    public string SourceFilePath { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public List<NivelacijaRepairIssueDto> DetectedIssues { get; set; } = [];
    public List<NivelacijaRepairFixDto> ProposedFixes { get; set; } = [];
    public NivelacijaRepairEstimatedImpactDto EstimatedImpact { get; set; } = new();
    public NivelacijaRepairVerificationDto Verification { get; set; } = new();
}

public sealed class NivelacijaRepairExecutionResultDto
{
    public string SourceFilePath { get; set; } = string.Empty;
    public long AuditId { get; set; }
    public int FixedRows { get; set; }
    public int SkippedRows { get; set; }
    public int RemainingIssuesAfterRepair { get; set; }
    public NivelacijaRepairEstimatedImpactDto EstimatedImpact { get; set; } = new();
    public NivelacijaRepairVerificationDto Verification { get; set; } = new();
}