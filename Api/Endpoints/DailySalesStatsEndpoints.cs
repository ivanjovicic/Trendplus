using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

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
            IMemoryCache cache,
            CancellationToken ct) =>
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
            var cacheKey = $"daily-sales:{fromUtc:yyyyMMdd}:{toUtc:yyyyMMdd}:{request.StoreId}:{safeTopN}:{normalizedDataScope}";
            if (cache.TryGetValue(cacheKey, out DailySalesTableResponse? cached) && cached is not null)
            {
                return Results.Ok(cached);
            }

            var result = await service.GetDailySalesAsync(
                requestedFromUtc: fromUtc,
                requestedToUtc: toUtc,
                storeId: request.StoreId,
                topN: safeTopN,
                dataScope: normalizedDataScope,
                ct);

            cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
            return Results.Ok(result);
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
