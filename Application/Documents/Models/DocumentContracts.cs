namespace Application.Documents.Models;

public sealed class DocumentNamedValue
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public sealed class DocumentColumnDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string? DataType { get; set; }
    public string? FormatHint { get; set; }
}

public sealed class DocumentTablePayload
{
    public string TableKey { get; set; } = string.Empty;
    public string TableTitle { get; set; } = string.Empty;
    public List<DocumentColumnDefinition> Columns { get; set; } = new();
    public List<List<string?>> Rows { get; set; } = new();
    public List<DocumentNamedValue> Filters { get; set; } = new();
    public List<DocumentNamedValue> Metadata { get; set; } = new();
}

public sealed class DocumentGenerationRequest
{
    public string Format { get; set; } = string.Empty;
    public string Orientation { get; set; } = string.Empty;
    public bool IncludeFiltersAndMetadata { get; set; } = true;
    public bool Preview { get; set; }
    public bool ForceAsync { get; set; }
    public string? Locale { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public int? TemplateVersion { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public DocumentTablePayload Table { get; set; } = new();
}

public sealed class DocumentBatchRequest
{
    public List<DocumentGenerationRequest> Items { get; set; } = new();
}

public sealed class DocumentExecutionContext
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public sealed class DocumentGenerateResult
{
    public Guid DocumentId { get; set; }
    public Guid? BatchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsAsync { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public sealed class DocumentBatchResult
{
    public Guid BatchId { get; set; }
    public List<DocumentGenerateResult> Items { get; set; } = new();
}

public sealed class DocumentStatusResult
{
    public Guid DocumentId { get; set; }
    public Guid? BatchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string TableKey { get; set; } = string.Empty;
    public string TableTitle { get; set; } = string.Empty;
    public bool IsAsync { get; set; }
    public int RowCount { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public sealed class DocumentStreamResult : IAsyncDisposable
{
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public Stream Stream { get; set; } = Stream.Null;
    public long? SizeBytes { get; set; }

    public ValueTask DisposeAsync()
    {
        return Stream.DisposeAsync();
    }
}
