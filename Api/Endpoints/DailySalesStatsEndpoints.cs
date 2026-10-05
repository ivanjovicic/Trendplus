using Api.Models;
using Api.Services;
using Application.Artikli.Common.Interfaces;
using Application.Analytics;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Npgsql;
using Trendplus2.Dtos;

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
            ITrendplusDbContext sourceDb,
            IDailySalesStatsService service,
            IAnalyticsCacheService cache,
            ILogger<Program> logger,
            HttpContext httpContext,
            [FromServices] OperationsAnalyticsIntegrityRegistry integrityRegistry,
            IServiceProvider serviceProvider,
            CancellationToken ct) =>
        {
            try
            {
                var safeTopN = Math.Clamp(request.TopN ?? DefaultTopN, 1, 25);
                var normalizedDataScope = NormalizeDataScope(request.DataScope);
                var requestedFrom = request.FromDate ?? request.From;
                var requestedTo = request.ToDate ?? request.To;
                string? defaultPeriodBasis = null;
                if (!requestedFrom.HasValue && !requestedTo.HasValue)
                {
                    var defaultPeriod = await ObservedSalesHorizonResolver.ResolveDefaultPeriodAsync(
                        sourceDb, request.StoreId, null, normalizedDataScope, ct);
                    if (defaultPeriod is null)
                    {
                        return Results.Ok(new DailySalesTableResponse
                        {
                            StoreId = request.StoreId,
                            TopN = safeTopN,
                            DataScope = normalizedDataScope,
                            Meta = CreateNoPeriodMeta()
                        });
                    }

                    requestedFrom = defaultPeriod.FromUtc;
                    requestedTo = defaultPeriod.ToUtc;
                    defaultPeriodBasis = "source_horizon";
                }
                // Keep the established fromDate/toDate names authoritative while also
                // accepting the shorter aliases used by direct API links.
                var hasExplicitTimestampTo = requestedTo.HasValue
                    && HasTimestampBound(httpContext.Request, request.ToDate.HasValue ? "toDate" : "to");
                var usesHalfOpenTo = hasExplicitTimestampTo || defaultPeriodBasis is not null || !requestedTo.HasValue;
                var normalizedToUtc = hasExplicitTimestampTo
                    ? NormalizeUtcInstant(requestedTo)
                    : NormalizeUtcDate(requestedTo);
                var toUtc = usesHalfOpenTo
                    ? normalizedToUtc ?? DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc)
                    : DateTime.SpecifyKind(normalizedToUtc!.Value.AddDays(1), DateTimeKind.Utc);
                var fromUtc = NormalizeUtcDate(requestedFrom) ?? toUtc.AddDays(-DefaultWindowDays);
                var lastIncludedDateUtc = DateTime.SpecifyKind(toUtc.AddTicks(-1).Date, DateTimeKind.Utc);

                if (fromUtc >= toUtc)
                {
                    return Results.BadRequest(new
                    {
                        message = "Neispravan period: fromDate mora biti manji od ekskluzivnog toDate.",
                        fromDate = fromUtc,
                        toDate = toUtc
                    });
                }

                var totalDays = (int)(lastIncludedDateUtc.Date - fromUtc.Date).TotalDays + 1;
                if (totalDays > MaxRangeDays)
                {
                    return Results.BadRequest(new
                    {
                        message = $"Maksimalni opseg je {MaxRangeDays} dana.",
                        fromDate = fromUtc,
                        toDate = toUtc
                    });
                }

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
                        ct,
                        requestedToIsExclusive: true),
                    CacheExpiration.Long,
                    ct);

                OperationsAnalyticsIntegrityMeta.ApplyFamilyEvidence(
                    result.Meta,
                    integrityRegistry,
                    OperationsAnalyticsIntegrityFamilies.SalesDashboard,
                    fromUtc.Date,
                    toUtc,
                    normalizedDataScope,
                    request.StoreId);
                if (result.Meta.DecisionReadiness is null)
                {
                    AnalyticsResponseMetaFactory.ApplyDecisionReadiness(
                        result.Meta,
                        "signal",
                        reasonCodes: ["daily_sales.actionability_not_assessed"],
                        evidenceReferences: ["daily_sales.rows", "daily_sales.shifts"],
                        repairPath: "Daily Sales kvalitet i opseg podataka");
                }
                result.Meta.DateBoundaryConvention = "half_open_utc";
                await OperationsSourceFreshnessService.ApplyIfRegisteredAsync(
                    serviceProvider,
                    result.Meta,
                    normalizedDataScope,
                    request.StoreId,
                    "signal",
                    ct,
                    defaultPeriodBasis);
                return Results.Ok(result);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                var correlationId = AllEndpoints.ResolveAnalyticsCorrelationId(httpContext);
                logger.LogInformation(
                    "Daily sales request cancelled. CorrelationId={CorrelationId} StoreId={StoreId}",
                    correlationId,
                    request.StoreId);

                return CreateDailySalesStatsProblem(
                    "Greška pri učitavanju dnevne analitike",
                    "Zahtev je otkazan.",
                    StatusCodes.Status503ServiceUnavailable,
                    "daily_sales_stats_cancelled",
                    correlationId);
            }
            catch (TaskCanceledException ex)
            {
                var correlationId = AllEndpoints.ResolveAnalyticsCorrelationId(httpContext);
                logger.LogWarning(
                    ex,
                    "Daily sales request timed out or was cancelled. CorrelationId={CorrelationId} StoreId={StoreId}",
                    correlationId,
                    request.StoreId);

                return CreateDailySalesStatsProblem(
                    "Greška pri učitavanju dnevne analitike",
                    "Zahtev je istekao ili je prekinut.",
                    StatusCodes.Status503ServiceUnavailable,
                    "daily_sales_stats_timeout",
                    correlationId);
            }
            catch (NpgsqlException ex)
            {
                var correlationId = AllEndpoints.ResolveAnalyticsCorrelationId(httpContext);
                logger.LogError(
                    ex,
                    "Daily sales analytics database error. CorrelationId={CorrelationId} StoreId={StoreId}",
                    correlationId,
                    request.StoreId);

                return CreateDailySalesStatsProblem(
                    "Greška pri učitavanju dnevne analitike",
                    "Problem pri povezivanju sa bazom podataka. Molimo pokušajte ponovo kasnije.",
                    StatusCodes.Status503ServiceUnavailable,
                    "daily_sales_stats_database_unavailable",
                    correlationId);
            }
            catch (Exception ex)
            {
                var correlationId = AllEndpoints.ResolveAnalyticsCorrelationId(httpContext);
                logger.LogError(
                    ex,
                    "Daily sales analytics endpoint failed. CorrelationId={CorrelationId} StoreId={StoreId}",
                    correlationId,
                    request.StoreId);

                return CreateDailySalesStatsProblem(
                    "Greška pri učitavanju dnevne analitike",
                    "Dnevna prodaja trenutno nije dostupna. Pokušajte ponovo.",
                    StatusCodes.Status500InternalServerError,
                    "daily_sales_stats_unavailable",
                    correlationId);
            }
        })
        .WithName("GetDailySalesStats")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics")
        .Produces<DailySalesTableResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);
    }

    private static IResult CreateDailySalesStatsProblem(
        string title,
        string detail,
        int statusCode,
        string errorCode,
        string correlationId)
    {
        return Results.Problem(
            title: title,
            detail: $"{detail} Referentni ID: {correlationId}.",
            statusCode: statusCode,
            extensions: new Dictionary<string, object?>
            {
                ["errorCode"] = errorCode,
                ["correlationId"] = correlationId
            });
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

    private static AnalyticsResponseMetaDto CreateNoPeriodMeta()
    {
        var meta = AnalyticsResponseMetaFactory.Empty(
            "source_horizon_unavailable",
            "Nema opaženog poslovnog datuma prodaje za izabrani opseg.",
            "insufficient_data");
        meta.DateBoundaryConvention = "half_open_utc";
        return meta;
    }

    private static DateTime? NormalizeUtcInstant(DateTime? rawDate)
    {
        if (!rawDate.HasValue)
        {
            return null;
        }

        var date = rawDate.Value;
        var utc = date.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : date.ToUniversalTime();
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    private static bool HasTimestampBound(HttpRequest request, string queryName)
    {
        var rawValue = request.Query[queryName].FirstOrDefault();
        return rawValue is not null && (rawValue.Contains('T') || rawValue.Contains(' '));
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
        string? DataScope = null,
        DateTime? From = null,
        DateTime? To = null);
}
