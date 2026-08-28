namespace Api.Models;

public sealed class DataSourceMappingPreviewRequest
{
    public string CanonicalEntity { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public List<string> ExternalKeyColumns { get; set; } = [];
    public DataSourceCursorSelection Cursor { get; set; } = new();
    public List<DataSourceFieldMappingSelection> ColumnMappings { get; set; } = [];
    public int SampleSize { get; set; } = 10;
}

public sealed class DataSourceCursorSelection
{
    public string Mode { get; set; } = "none";
    public string? IdColumn { get; set; }
    public string? TimestampColumn { get; set; }
    public string? TieBreakerColumn { get; set; }
}

public sealed class DataSourceFieldMappingSelection
{
    public string TargetField { get; set; } = string.Empty;
    public string SourceColumn { get; set; } = string.Empty;
    public List<string> Transforms { get; set; } = [];
}

public sealed class DataSourceMappingPreviewResponse
{
    public string ProfileName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string CanonicalEntity { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public bool CanPreview { get; set; }
    public bool CanSync { get; set; }
    public int SampleSize { get; set; }
    public int PreviewedRows { get; set; }
    public int RowCount { get; set; }
    public string RowCountMode { get; set; } = "unknown";
    public string SchemaFingerprint { get; set; } = string.Empty;
    public List<DataSourcePreviewColumn> Columns { get; set; } = [];
    public List<string> ExternalKeyColumns { get; set; } = [];
    public DataSourcePreviewCursor Cursor { get; set; } = new();
    public List<DataSourcePreviewFieldMapping> FieldMappings { get; set; } = [];
    public List<DataSourcePreviewRow> PreviewRows { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class DataSourcePreviewColumn
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string SourceType { get; set; } = "unknown";
    public bool? IsNullable { get; set; }
    public int Ordinal { get; set; }
}

public sealed class DataSourcePreviewCursor
{
    public string Mode { get; set; } = "none";
    public string? IdColumn { get; set; }
    public string? TimestampColumn { get; set; }
    public string? TieBreakerColumn { get; set; }
    public string Status { get; set; } = "valid";
    public List<string> ValidationErrors { get; set; } = [];
}

public sealed class DataSourcePreviewFieldMapping
{
    public string TargetField { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? SourceColumn { get; set; }
    public string Status { get; set; } = "missing";
    public List<string> Transforms { get; set; } = [];
    public List<string> ValidationErrors { get; set; } = [];
}

public sealed class DataSourcePreviewRow
{
    public int RowIndex { get; set; }
    public string? ExternalKey { get; set; }
    public string Status { get; set; } = "accepted";
    public List<string> RejectionReasons { get; set; } = [];
    public Dictionary<string, object?> SourceSnapshot { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<DataSourcePreviewFieldValue> Fields { get; set; } = [];
}

public sealed class DataSourcePreviewFieldValue
{
    public string TargetField { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    public string? SourceColumn { get; set; }
    public object? RawValue { get; set; }
    public object? ParsedValue { get; set; }
    public string Status { get; set; } = "accepted";
    public List<string> RejectionReasons { get; set; } = [];
}
