namespace Api.Services.DataSources;

public sealed class SourceMappingPreviewRequest
{
    public string Table { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string ExternalKeyColumn { get; set; } = string.Empty;
    public string CursorMode { get; set; } = "id";
    public string? CursorIdColumn { get; set; }
    public string? CursorTimestampColumn { get; set; }
    public int MaxRows { get; set; } = 25;
    public IReadOnlyList<SourceMappingFieldRequest> Fields { get; set; } = [];
}

public sealed class SourceMappingFieldRequest
{
    public string Target { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed record SourceMappingPreviewDto(
    string Source,
    string Table,
    string Entity,
    string SchemaFingerprint,
    string Identity,
    SourceMappingSelectionDto ExternalKey,
    SourceMappingSelectionDto Cursor,
    IReadOnlyList<SourceMappingFieldResultDto> Fields,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Preview,
    int PreviewRowCount,
    int RejectedRowCount,
    IReadOnlyList<string> Warnings);

public sealed record SourceMappingSelectionDto(
    string Status,
    string? Column,
    string? Mode,
    string? Reason,
    IReadOnlyList<string>? SuggestedAliases = null);

public sealed record SourceMappingFieldResultDto(
    string Target,
    string? Source,
    string? ResolvedSource,
    string Status,
    string? Reason,
    IReadOnlyList<string>? SuggestedAliases = null);
