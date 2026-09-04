using System;

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
    public DateTime? RequestedPeriodFromUtc { get; set; }
    public DateTime? RequestedPeriodToUtc { get; set; }
    public DateTime? EffectivePeriodFromUtc { get; set; }
    public DateTime? EffectivePeriodToUtc { get; set; }
    public DateTime? ObservedPeriodFromUtc { get; set; }
    public DateTime? ObservedPeriodToUtc { get; set; }
    public string? DataQualityStatus { get; set; }
    public bool? RecommendationAllowed { get; set; }
    public bool IsPartial { get; set; }
}
