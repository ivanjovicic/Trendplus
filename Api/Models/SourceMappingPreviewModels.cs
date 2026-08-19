using Api.Services.DataSources;

namespace Api.Models;

public sealed class SourceMappingPreviewRequest
{
    public string CanonicalEntity { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public List<string> ExternalKeyColumns { get; set; } = new();
    public SourceReadQuery? Cursor { get; set; }
    public List<SourceMappingFieldRequest> FieldMappings { get; set; } = new();
    public int Take { get; set; } = 10;
}

public sealed class SourceMappingFieldRequest
{
    public string TargetField { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();
}

public sealed class SourceMappingPreviewFieldResult
{
    public string TargetField { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new();
    public string? SourceColumn { get; set; }
    public string Status { get; set; } = "missing";
    public string? ReasonCode { get; set; }
    public string? Message { get; set; }
}

public sealed class SourceMappingPreviewIssue
{
    public string Scope { get; set; } = string.Empty;
    public string? Field { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class SourceMappedValue
{
    public string TargetField { get; set; } = string.Empty;
    public object? Value { get; set; }
}

public sealed class SourceMappingPreviewRow
{
    public int RowIndex { get; set; }
    public List<SourceMappedValue> Values { get; set; } = new();
}

public sealed class SourceMappingPreviewResponse
{
    public string ProfileName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string CanonicalEntity { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public List<string> ExternalKeyColumns { get; set; } = new();
    public SourceReadQuery? Cursor { get; set; }
    public string SchemaFingerprint { get; set; } = string.Empty;
    public int RequestedTake { get; set; }
    public int ReturnedRows { get; set; }
    public bool Truncated { get; set; }
    public List<SourceMappingPreviewFieldResult> FieldMappings { get; set; } = new();
    public List<SourceMappingPreviewIssue> Issues { get; set; } = new();
    public List<SourceMappingPreviewRow> Rows { get; set; } = new();
}
