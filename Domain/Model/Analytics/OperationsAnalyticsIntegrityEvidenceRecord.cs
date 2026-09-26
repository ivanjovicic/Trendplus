namespace Domain.Model.Analytics;

/// <summary>
/// Append-only, redacted evidence for one Operations Analytics integrity check.
/// It stores reconciliation metadata and aggregates, never credentials or sale-line payloads.
/// </summary>
public sealed class OperationsAnalyticsIntegrityEvidenceRecord
{
    public required string EvidenceId { get; set; }
    public required string Status { get; set; }
    public DateTime CheckedAtUtc { get; set; }
    public DateTime? LastVerifiedAtUtc { get; set; }
    public string? Trigger { get; set; }
    public string? Summary { get; set; }
    public string? FailureClassification { get; set; }
    public string? TenantScope { get; set; }
    public int? StoreId { get; set; }
    public string? DataScope { get; set; }
    public DateTime? RequestedFromUtc { get; set; }
    public DateTime? RequestedToUtc { get; set; }
    public DateTime? EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public string? DatabaseFingerprint { get; set; }
    public string? AppCommit { get; set; }
    public string? SchemaVersion { get; set; }
    public string? ContractVersion { get; set; }
    public string? FixtureVersion { get; set; }
    public string? CacheVersion { get; set; }
    public decimal? EndpointOrLiveRevenue { get; set; }
    public decimal? OracleRevenue { get; set; }
    public decimal? RevenueDelta { get; set; }
    public int? EndpointOrLiveUnits { get; set; }
    public int? OracleUnits { get; set; }
    public int? UnitsDelta { get; set; }
    public string? DeltasJson { get; set; }
    public string? CoverageJson { get; set; }
    public bool BlocksDecisionSignals { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
