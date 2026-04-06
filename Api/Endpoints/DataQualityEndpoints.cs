using Application.Analytics.Queries.GetDataQualityIssues;
using Infrastructure.Configuration;
using Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Trendplus2.Endpoints;

public static class DataQualityEndpoints
{
    public static void MapDataQualityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analytics/data-quality/health", async (
            AnalyticsDataQualityHealthService healthService,
            IOptions<AnalyticsDataQualityHealthOptions> options,
            int? lookbackDays,
            string? dataScope,
            CancellationToken ct) =>
        {
            var requestedLookback = lookbackDays ?? options.Value.LookbackDays;
            var snapshot = await healthService.CaptureAsync(requestedLookback, dataScope, ct);

            return Results.Ok(new
            {
                generatedAt = snapshot.GeneratedAtUtc,
                lookbackDays = snapshot.LookbackDays,
                windowFrom = snapshot.WindowFromUtc,
                windowTo = snapshot.WindowToUtc,
                orphanArticleCount = snapshot.OrphanArticleCount,
                totalRevenue = snapshot.TotalRevenue,
                missingCostRevenue = snapshot.MissingCostRevenue,
                missingCostRevenueSharePct = snapshot.MissingCostRevenueSharePct,
                unknownSupplierRevenue = snapshot.UnknownSupplierRevenue,
                unknownSupplierRevenueSharePct = snapshot.UnknownSupplierRevenueSharePct,
                thresholds = new
                {
                    orphanArticleCount = options.Value.WarningOrphanArticleCount,
                    missingCostRevenueSharePct = options.Value.WarningMissingCostRevenueSharePct,
                    unknownSupplierRevenueSharePct = options.Value.WarningUnknownSupplierRevenueSharePct
                }
            });
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/data-quality/list", async (
            [AsParameters] DataQualityListRequest request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation(
                "Data quality issues requested {Type} page {Page} pageSize {PageSize} sortBy {SortBy}",
                request.Type,
                request.Page,
                request.PageSize,
                request.SortBy);

            var result = await mediator.Send(new GetDataQualityIssuesQuery(
                request.Type,
                request.Page,
                request.PageSize,
                request.Q,
                request.SortBy,
                request.SortDir,
                request.DataScope), ct);

            return Results.Ok(result);
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");
    }

    public sealed record DataQualityListRequest(
        string? Type,
        int Page = 1,
        int PageSize = 25,
        string? Q = null,
        string? SortBy = null,
        string? SortDir = null,
        string? DataScope = null);
}
