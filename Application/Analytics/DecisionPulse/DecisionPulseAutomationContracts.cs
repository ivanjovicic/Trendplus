namespace Application.Analytics.DecisionPulse;

public sealed record DecisionPulseScheduleDefinition(
    long Id,
    string Name,
    bool IsEnabled,
    string Frequency,
    int? DayOfWeek,
    string RunAtLocalTime,
    string TimeZoneId,
    string RecipientsCsv,
    string? Subject,
    int? StoreId,
    int? SupplierId,
    string? DataScope,
    DateTime? LastRunAtUtc,
    string? LastRunStatus,
    string? LastError,
    string CreatedByUserId,
    string CreatedByUserName,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record DecisionPulseScheduleUpsertRequest(
    string Name,
    bool IsEnabled,
    string Frequency,
    int? DayOfWeek,
    string RunAtLocalTime,
    string TimeZoneId,
    string RecipientsCsv,
    string? Subject,
    int? StoreId,
    int? SupplierId,
    string? DataScope,
    string CreatedByUserId,
    string CreatedByUserName);

public sealed record DecisionPulseScheduleRunResult(
    bool Success,
    string Status,
    string Message,
    DateTime ExecutedAtUtc);

public sealed class DecisionPulseOptions
{
    public const string Section = "DecisionPulse";

    public string[] Recipients { get; set; } = [];
    public int MaxCandidates { get; set; } = 100;
}

public sealed record DecisionPulseItemDto(
    string Id,
    string SourceType,
    string SourceKey,
    string Title,
    string WhySummary,
    IReadOnlyList<string> ReasonCodes,
    string RecommendationStatus,
    string RecommendationLabel,
    string DataQualityStatus,
    string InputFreshnessStatus,
    string DeepLink,
    DateTime? GeneratedAtUtc,
    string TenantScope);

public sealed class DecisionPulseResponseMetaDto
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
    public string? DataQualityStatus { get; set; }
    public bool IsPartial { get; set; }
}

public static class DecisionPulseResponseMetaFactory
{
    public static DecisionPulseResponseMetaDto Success(
        string? dataQualityStatus = null,
        DateTime? lastRefreshAtUtc = null,
        bool isPartial = false,
        string? warningCode = null,
        string? warningMessage = null)
        => new()
        {
            Success = true,
            DataQualityStatus = dataQualityStatus,
            LastRefreshAtUtc = lastRefreshAtUtc,
            IsPartial = isPartial,
            WarningCode = warningCode,
            WarningMessage = warningMessage,
            Message = warningMessage,
            GeneratedAtUtc = DateTime.UtcNow
        };

    public static DecisionPulseResponseMetaDto Empty(
        string emptyReason,
        string message,
        string? dataQualityStatus = "insufficient_data")
        => new()
        {
            Success = true,
            DataQualityStatus = dataQualityStatus,
            EmptyReason = emptyReason,
            Message = message,
            GeneratedAtUtc = DateTime.UtcNow
        };

    public static DecisionPulseResponseMetaDto Error(
        string errorCode,
        string errorMessage,
        string? correlationId,
        string? dataQualityStatus = "insufficient_data",
        string? message = null)
        => new()
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            CorrelationId = correlationId,
            DataQualityStatus = dataQualityStatus,
            Message = message ?? errorMessage,
            GeneratedAtUtc = DateTime.UtcNow
        };
}

public sealed record DecisionPulseResponseDto(
    DateTime GeneratedAtUtc,
    DateTime? PeriodFromUtc,
    DateTime? PeriodToUtc,
    string TenantScope,
    int SuppressedCount,
    IReadOnlyList<DecisionPulseItemDto> Items,
    DecisionPulseResponseMetaDto Meta);

public sealed record DecisionPulseEmailResultDto(
    bool Sent,
    string? FailureCategory,
    string Message,
    int RecipientCount,
    int ItemCount);
