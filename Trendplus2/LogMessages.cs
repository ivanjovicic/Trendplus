using Microsoft.Extensions.Logging;

namespace Trendplus2;

internal static partial class LogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "GET /api/performance - TopCount: {TopCount}, MinDuration: {MinDuration}ms")]
    public static partial void PerformanceRequest(this ILogger logger, int topCount, int minDuration);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch logs")]
    public static partial void LogsFetchFailed(this ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Outbox message {Id} marked for retry")]
    public static partial void OutboxRetry(this ILogger logger, long id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bulk retry: {Count} failed messages marked for retry")]
    public static partial void BulkRetry(this ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} processed messages older than {Days} days")]
    public static partial void PurgeProcessed(this ILogger logger, int count, int days);

    [LoggerMessage(Level = LogLevel.Information, Message = "POST /artikli payload: {Dto}")]
    public static partial void CreateArtikalRequest(this ILogger logger, string dto);

    [LoggerMessage(Level = LogLevel.Information, Message = "Artikal kreiran sa Id {Id}")]
    public static partial void ArtikalCreated(this ILogger logger, int id);

    [LoggerMessage(Level = LogLevel.Information, Message = "GET /artikli/{Id}")]
    public static partial void GetArtikalRequest(this ILogger logger, int id);

    [LoggerMessage(Level = LogLevel.Information, Message = "GET /artikli (lista)")]
    public static partial void GetArtikliRequest(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Received PUT /artikli/{Id} DTO: {Dto}")]
    public static partial void UpdateArtikalRequest(this ILogger logger, int id, string dto);

    [LoggerMessage(Level = LogLevel.Information, Message = "Artikal {Id} uspešno izmenjen")]
    public static partial void ArtikalUpdated(this ILogger logger, int id);

    [LoggerMessage(Level = LogLevel.Warning, Message = "UpdateArtikal failed for Id {Id}")]
    public static partial void UpdateArtikalFailed(this ILogger logger, Exception ex, int id);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error while handling UpdateArtikalCommand")]
    public static partial void UpdateArtikalError(this ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "POST /api/prodaja payload: {Command}")]
    public static partial void ProdajaRequest(this ILogger logger, string command);

    [LoggerMessage(Level = LogLevel.Information, Message = "Prodaja kreirana sa Id {Id}")]
    public static partial void ProdajaCreated(this ILogger logger, int id);

    [LoggerMessage(Level = LogLevel.Information, Message = "Nivelacija cene za ArtikalId {Id}: {Old} -> {New}")]
    public static partial void NivelacijaCene(this ILogger logger, int id, decimal? old, decimal? @new);
}
