namespace Api.Models;

public sealed class AnalyticsDetailFieldDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Value { get; init; }
    public string? DataType { get; init; }
    public bool Highlight { get; init; }
}

public sealed class AnalyticsDetailResponseDto
{
    public string Table { get; init; } = string.Empty;
    public string RecordId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public IReadOnlyList<AnalyticsDetailFieldDto> Fields { get; init; } = Array.Empty<AnalyticsDetailFieldDto>();
    public IReadOnlyList<AnalyticsDetailFieldDto> Metadata { get; init; } = Array.Empty<AnalyticsDetailFieldDto>();
}
