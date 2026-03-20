namespace Application.Inventory.Models;

public sealed record InventoryReportScheduleDefinition(
    long Id,
    string Name,
    bool IsEnabled,
    string Frequency,
    int? DayOfWeek,
    string RunAtLocalTime,
    string TimeZoneId,
    string Format,
    string Orientation,
    bool IncludeFiltersAndMetadata,
    string RecipientsCsv,
    string? Subject,
    string? Search,
    int? StoreId,
    int? SupplierId,
    string? SortBy,
    DateTime? LastRunAtUtc,
    string? LastRunStatus,
    string? LastError,
    Guid? LastDocumentId,
    string CreatedByUserId,
    string CreatedByUserName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc
);

public sealed record InventoryReportScheduleUpsertRequest(
    string Name,
    bool IsEnabled,
    string Frequency,
    int? DayOfWeek,
    string RunAtLocalTime,
    string TimeZoneId,
    string Format,
    string Orientation,
    bool IncludeFiltersAndMetadata,
    string RecipientsCsv,
    string? Subject,
    string? Search,
    int? StoreId,
    int? SupplierId,
    string? SortBy,
    string CreatedByUserId,
    string CreatedByUserName
);

public sealed record InventoryReportScheduleRunResult(
    bool Success,
    string Status,
    string Message,
    Guid? DocumentId,
    DateTime ExecutedAtUtc
);

public sealed record InventoryActionDecisionDefinition(
    string SuggestionKey,
    string ActionType,
    string Status,
    string? Note,
    string UpdatedByUserId,
    string UpdatedByUserName,
    DateTime UpdatedAtUtc
);

public sealed record InventoryActionDecisionUpsertRequest(
    string SuggestionKey,
    string ActionType,
    string Status,
    string? Note,
    string UpdatedByUserId,
    string UpdatedByUserName
);

public sealed class EmailAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = [];
}

public sealed class EmailMessage
{
    public List<string> To { get; set; } = [];
    public List<string> Cc { get; set; } = [];
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public List<EmailAttachment> Attachments { get; set; } = [];
}
