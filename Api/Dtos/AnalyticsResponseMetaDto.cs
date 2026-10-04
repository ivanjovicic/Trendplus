using System;
using System.Collections.Generic;
using Application.Analytics;

namespace Trendplus2.Dtos;

public class AnalyticsResponseMetaDto
{
    public bool Success { get; set; }
    public string? WarningCode { get; set; }
    public string? WarningMessage { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? EmptyReason { get; set; }
    public string? CorrelationId { get; set; }
    public string? Message { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastRefreshAtUtc { get; set; }
    public DateTime? CacheCreatedAtUtc { get; set; }
    public DateTime? RequestedPeriodFromUtc { get; set; }
    public DateTime? RequestedPeriodToUtc { get; set; }
    public DateTime? EffectivePeriodFromUtc { get; set; }
    public DateTime? EffectivePeriodToUtc { get; set; }
    public string? RequestedDataScope { get; set; }
    public string? EffectiveDataScope { get; set; }
    public string? DataScopeSource { get; set; }
    public string? ProvenanceBasis { get; set; }
    public string? AttributionBasis { get; set; }
    public double? AttributionCoveragePct { get; set; }
    public DateTime? ObservedPeriodFromUtc { get; set; }
    public DateTime? ObservedPeriodToUtc { get; set; }
    public string? DataQualityStatus { get; set; }
    public bool? RecommendationAllowed { get; set; }
    public bool IsPartial { get; set; }
    /// <summary>
    /// Optional provenance by stable metric key. Existing clients may omit this field.
    /// </summary>
    public IReadOnlyDictionary<string, AnalyticsMetricProvenanceDto>? MetricProvenance { get; set; }
    /// <summary>
    /// Additive machine-comparable context identity for cross-surface metrics.
    /// A null fingerprint or unavailable state must never be treated as a match.
    /// </summary>
    public AnalyticsContextDescriptor? Context { get; set; }
    public string? OperationsIntegrityStatus { get; set; }
    public DateTime? OperationsIntegrityCheckedAtUtc { get; set; }
    public string? OperationsIntegrityEvidenceId { get; set; }
    public string? OperationsIntegrityFamily { get; set; }
    public string? OperationsIntegrityContextFingerprint { get; set; }
    public string? OperationsIntegritySourceGeneration { get; set; }
    public bool? OperationsIntegrityContextMatches { get; set; }
    /// <summary>
    /// Backend-owned categorical readiness for the declared decision surface.
    /// This is additive and preserves existing response shapes.
    /// </summary>
    public AnalyticsDecisionReadinessDto? DecisionReadiness { get; set; }
    /// <summary>
    /// Optional per-tab counting basis for Supplier surfaces. Existing clients may omit this field.
    /// </summary>
    public SupplierTabBasisDto? Basis { get; set; }
    /// <summary>Optional database contract diagnosis for analytics availability failures.</summary>
    public AnalyticsContractDiagnosticDto? ContractDiagnostic { get; set; }
    /// <summary>Stable identifier for a read-only readiness check; not a correlation ID.</summary>
    public string? ReadinessId { get; set; }
    /// <summary>Safe next step for restoring an unavailable analytics contract.</summary>
    public string? RecoveryInstruction { get; set; }
}
