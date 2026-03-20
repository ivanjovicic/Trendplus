namespace Domain.Model.Documents;

public class DocumentRecord
{
    public Guid Id { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? TemplateId { get; set; }
    public int TemplateVersion { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = DocumentTemplateTypes.AnalyticsTableReport;
    public string TableKey { get; set; } = string.Empty;
    public string TableTitle { get; set; } = string.Empty;
    public string Format { get; set; } = DocumentFormats.Csv;
    public string Orientation { get; set; } = DocumentOrientations.Landscape;
    public string Status { get; set; } = DocumentStatuses.Requested;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByUserName { get; set; } = string.Empty;
    public string RequestedByRoles { get; set; } = string.Empty;
    public string? Locale { get; set; }
    public bool IncludeFiltersAndMetadata { get; set; } = true;
    public bool IsPreview { get; set; }
    public bool IsAsync { get; set; }
    public int RowCount { get; set; }
    public string? FiltersJson { get; set; }
    public string? MetadataJson { get; set; }
    public string RequestJson { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public string? FileName { get; set; }
    public string? StoragePath { get; set; }
    public string? FileUrl { get; set; }
    public long? SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
