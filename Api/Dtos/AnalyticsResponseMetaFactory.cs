namespace Trendplus2.Dtos;

/// <summary>
/// Centralized factory for creating AnalyticsResponseMetaDto instances.
/// Ensures consistent field population across all analytics endpoints.
/// </summary>
public static class AnalyticsResponseMetaFactory
{
    public static AnalyticsResponseMetaDto Success(
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

    public static AnalyticsResponseMetaDto Empty(
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

    public static AnalyticsResponseMetaDto Error(
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
