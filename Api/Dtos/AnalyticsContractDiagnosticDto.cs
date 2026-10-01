namespace Trendplus2.Dtos;

/// <summary>Stable explanation for a missing database contract used by an analytics response.</summary>
public sealed class AnalyticsContractDiagnosticDto
{
    public string Relation { get; init; } = string.Empty;
    public string MissingPart { get; init; } = string.Empty;
    public string? Schema { get; init; }
    public string? MissingColumn { get; init; }
    public IReadOnlyList<string>? FoundInSchemas { get; init; }
}
