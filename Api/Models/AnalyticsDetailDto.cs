namespace Api.Models;

public sealed class AnalyticsDetailFieldDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Value { get; init; }
    public string? DataType { get; init; }
    public bool Highlight { get; init; }
}

public sealed class AnalyticsDetailRecommendationDto
{
    public string Status { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public double? ConfidencePct { get; init; }
    public double? ReliabilityPct { get; init; }
    public string DataQualityStatus { get; init; } = string.Empty;
    public bool RecommendationAllowed { get; init; }
    public IReadOnlyList<string> ReasonCodes { get; init; } = Array.Empty<string>();
}

public sealed class AnalyticsDetailProvenanceDto
{
    public DateTime? RequestedFromUtc { get; init; }
    public DateTime? RequestedToUtc { get; init; }
    public DateTime? EffectiveFromUtc { get; init; }
    public DateTime? EffectiveToUtc { get; init; }
    public string? Season { get; init; }
    public int? StoreId { get; init; }
    public string DataScope { get; init; } = "all";
    public DateTime GeneratedAtUtc { get; init; }
    public string Freshness { get; init; } = "fresh";
    public string DataQualityStatus { get; init; } = string.Empty;
    public bool SnapshotActive { get; init; }
    public DateTime? SnapshotGeneratedAtUtc { get; init; }
    public bool FallbackApplied { get; init; }
    public bool RecommendationAllowed { get; init; }
}

public sealed class AnalyticsDetailResponseDto
{
    public string Table { get; init; } = string.Empty;
    public string RecordId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public IReadOnlyList<AnalyticsDetailFieldDto> Fields { get; init; } = Array.Empty<AnalyticsDetailFieldDto>();
    public IReadOnlyList<AnalyticsDetailFieldDto> Metadata { get; init; } = Array.Empty<AnalyticsDetailFieldDto>();
    public AnalyticsDetailRecommendationDto? Recommendation { get; init; }
    public AnalyticsDetailProvenanceDto? Provenance { get; init; }
}
