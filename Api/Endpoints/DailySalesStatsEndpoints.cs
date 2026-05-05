using Api.Models;
using Api.Services;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Trendplus2.Endpoints;

public static class DailySalesStatsEndpoints
{
    private const int MaxRangeDays = 365;
    private const int DefaultWindowDays = 30;
    private const int DefaultTopN = 15;

    public static void MapDailySalesStatsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analytics/daily-sales", async (
            [AsParameters] DailySalesStatsRequest request,
            IDailySalesStatsService service,
            IAnalyticsCacheService cache,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                var safeTopN = Math.Clamp(request.TopN ?? DefaultTopN, 1, 25);
                var toUtc = NormalizeUtcDate(request.ToDate) ?? DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                var fromUtc = NormalizeUtcDate(request.FromDate) ?? toUtc.AddDays(-(DefaultWindowDays - 1));

                if (fromUtc > toUtc)
                {
                    return Results.BadRequest(new
                    {
                        message = "Neispravan period: fromDate mora biti manji ili jednak toDate.",
                        fromDate = fromUtc,
                        toDate = toUtc
                    });
                }

                var totalDays = (int)(toUtc.Date - fromUtc.Date).TotalDays + 1;
                if (totalDays > MaxRangeDays)
                {
                    return Results.BadRequest(new
                    {
                        message = $"Maksimalni opseg je {MaxRangeDays} dana.",
                        fromDate = fromUtc,
                        toDate = toUtc
                    });
                }

                var normalizedDataScope = NormalizeDataScope(request.DataScope);
                var cacheKey = AnalyticsCacheKeys.DailySales(
                    fromUtc,
                    toUtc,
                    request.StoreId,
                    null,
                    normalizedDataScope,
                    safeTopN);

                var result = await cache.GetOrSetAsync(
                    cacheKey,
                    () => service.GetDailySalesAsync(
                        requestedFromUtc: fromUtc,
                        requestedToUtc: toUtc,
                        storeId: request.StoreId,
                        topN: safeTopN,
                        dataScope: normalizedDataScope,
                        ct),
                    CacheExpiration.Long,
                    ct);

                return Results.Ok(result);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.Problem(
                    title: "Greška pri učitavanju dnevne analitike",
                    detail: "Zahtev je otkazan.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (TaskCanceledException ex)
            {
                logger.LogWarning(ex, "Daily sales request timed out or was cancelled.");
                return Results.Problem(
                    title: "Greška pri učitavanju dnevne analitike",
                    detail: "Zahtev je istekao ili je prekinut.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (NpgsqlException ex)
            {
                logger.LogError(ex, "Daily sales analytics database error.");
                return Results.Problem(
                    title: "Greška pri učitavanju dnevne analitike",
                    detail: "Problem pri povezivanju sa bazom podataka. Molimo pokušajte ponovo kasnije.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Daily sales analytics endpoint failed.");
                return Results.Problem(
                    title: "Greška pri učitavanju dnevne analitike",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .WithName("GetDailySalesStats")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics")
        .Produces<DailySalesTableResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);
    }

    private static DateTime? NormalizeUtcDate(DateTime? rawDate)
    {
        if (!rawDate.HasValue)
        {
            return null;
        }

        var date = rawDate.Value;
        var utc = date.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : date.ToUniversalTime();
        return DateTime.SpecifyKind(utc.Date, DateTimeKind.Utc);
    }

    private static string NormalizeDataScope(string? rawScope)
    {
        var normalized = (rawScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
    }

    public sealed record DailySalesStatsRequest(
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        int? StoreId = null,
        int? TopN = null,
        string? DataScope = null);
}
