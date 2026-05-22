using Application.Analytics.Queries.GetDataQualityIssues;
using Infrastructure.Configuration;
using Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Trendplus2.Dtos;

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
            var score = BuildScore(snapshot, options.Value);

            return Results.Ok(new DataQualityHealthResponse(
                snapshot.GeneratedAtUtc,
                snapshot.LookbackDays,
                snapshot.WindowFromUtc,
                snapshot.WindowToUtc,
                snapshot.OrphanArticleCount,
                snapshot.TotalRevenue,
                snapshot.MissingCostRevenue,
                snapshot.MissingCostRevenueSharePct,
                snapshot.UnknownSupplierRevenue,
                snapshot.UnknownSupplierRevenueSharePct,
                score.Value,
                score.Status,
                score.Summary,
                new DataQualityHealthThresholds(
                    options.Value.WarningOrphanArticleCount,
                    options.Value.WarningMissingCostRevenueSharePct,
                    options.Value.WarningUnknownSupplierRevenueSharePct),
                new AnalyticsResponseMetaDto
                {
                    Success = true,
                    GeneratedAtUtc = DateTime.UtcNow,
                    LastRefreshAtUtc = snapshot.GeneratedAtUtc,
                    DataQualityStatus = score.Status switch
                    {
                        "critical" => "critical",
                        "warning" => "warning",
                        "good" or "excellent" => "good",
                        _ => "insufficient_data"
                    },
                    Message = snapshot.TotalRevenue <= 0 ? "Nema dovoljno podataka za score data quality-ja u ovom prozoru." : null,
                    IsPartial = false
                }));
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/data-quality/list", async (
            [AsParameters] DataQualityListRequest request,
            IMediator mediator,
            IOptions<AnalyticsDataQualityHealthOptions> options,
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
                request.DataScope,
                options.Value.MinSalesForNoisyIssuesRsd), ct);

            var meta = new AnalyticsResponseMetaDto
            {
                Success = true,
                GeneratedAtUtc = DateTime.UtcNow,
                DataQualityStatus = result.Total == 0 ? "insufficient_data" : "warning",
                Message = result.Total == 0 ? "Nema otvorenih data quality problema za izabrani filter." : null
            };

            return Results.Ok(new DataQualityIssueListResponse(
                result.Page,
                result.PageSize,
                result.Total,
                result.Items,
                meta));
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/data-quality/top-offenders", async (
            AnalyticsDataQualityHealthService healthService,
            IOptions<AnalyticsDataQualityHealthOptions> options,
            string? issueType,
            int? limit,
            string? dataScope,
            CancellationToken ct) =>
        {
            var normalizedIssueType = DataQualityIssueTypes.Normalize(issueType);
            var resolvedLimit = Math.Clamp(limit ?? options.Value.TopOffenderLimit, 1, 100);

            var items = await healthService.GetTopOffendersAsync(
                normalizedIssueType,
                resolvedLimit,
                options.Value.MinSalesForNoisyIssuesRsd,
                dataScope,
                ct);

            return Results.Ok(new DataQualityTopOffendersResponse(
                normalizedIssueType,
                resolvedLimit,
                items.Count,
                items,
                new AnalyticsResponseMetaDto
                {
                    Success = true,
                    GeneratedAtUtc = DateTime.UtcNow,
                    DataQualityStatus = items.Count == 0 ? "insufficient_data" : "warning",
                    Message = items.Count == 0 ? "Nema top offender zapisa za izabrani tip problema." : null
                }));
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/data-quality/trend", async (
            AnalyticsDataQualityHistoryService historyService,
            int? days,
            string? dataScope,
            CancellationToken ct) =>
        {
            var resolvedDays = Math.Clamp(days ?? 7, 2, 90);
            var points = await historyService.GetTrendAsync(resolvedDays, dataScope, ct);

            return Results.Ok(new DataQualityTrendResponse(
                resolvedDays,
                string.IsNullOrWhiteSpace(dataScope) ? "all" : dataScope,
                points,
                new AnalyticsResponseMetaDto
                {
                    Success = true,
                    GeneratedAtUtc = DateTime.UtcNow,
                    DataQualityStatus = points.Count == 0 ? "insufficient_data" : "warning",
                    Message = points.Count == 0 ? "Trend data quality-ja nije dostupan za izabrani opseg." : null
                }));
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

    public sealed record DataQualityTopOffendersResponse(
        string IssueType,
        int Limit,
        int Count,
        IReadOnlyList<DataQualityTopOffenderDto> Items,
        AnalyticsResponseMetaDto? Meta = null);

    public sealed record DataQualityIssueListResponse(
        int Page,
        int PageSize,
        int Total,
        IReadOnlyList<DataQualityIssueItemDto> Items,
        AnalyticsResponseMetaDto? Meta = null);

    public sealed record DataQualityTrendResponse(
        int Days,
        string DataScope,
        IReadOnlyList<DataQualityTrendPointDto> Points,
        AnalyticsResponseMetaDto? Meta = null);

    public sealed record DataQualityHealthThresholds(
        int OrphanArticleCount,
        double MissingCostRevenueSharePct,
        double UnknownSupplierRevenueSharePct);

    public sealed record DataQualityHealthResponse(
        DateTime GeneratedAt,
        int LookbackDays,
        DateTime WindowFrom,
        DateTime WindowTo,
        int OrphanArticleCount,
        decimal TotalRevenue,
        decimal MissingCostRevenue,
        double MissingCostRevenueSharePct,
        decimal UnknownSupplierRevenue,
        double UnknownSupplierRevenueSharePct,
        int Score,
        string ScoreStatus,
        string ScoreSummary,
        DataQualityHealthThresholds Thresholds,
        AnalyticsResponseMetaDto? Meta = null);

    private static DataQualityScoreDto BuildScore(
        AnalyticsDataQualityHealthSnapshot snapshot,
        AnalyticsDataQualityHealthOptions options)
    {
        static double Clamp01(double value) => Math.Max(0d, Math.Min(1d, value));

        var warningPenalty = Clamp01(options.ScorePenaltyAtWarning);
        var criticalMultiplier = Math.Max(1.25d, options.ScoreCriticalMultiplier);
        var missingCostWeight = Math.Max(0d, options.ScoreMissingCostWeight);
        var unknownSupplierWeight = Math.Max(0d, options.ScoreUnknownSupplierWeight);
        var orphanWeight = Math.Max(0d, options.ScoreOrphanWeight);
        var totalWeight = Math.Max(0.0001d, missingCostWeight + unknownSupplierWeight + orphanWeight);

        var missingCostPenalty = CalculatePenalty(
            snapshot.MissingCostRevenueSharePct,
            options.WarningMissingCostRevenueSharePct,
            warningPenalty,
            criticalMultiplier);
        var unknownSupplierPenalty = CalculatePenalty(
            snapshot.UnknownSupplierRevenueSharePct,
            options.WarningUnknownSupplierRevenueSharePct,
            warningPenalty,
            criticalMultiplier);
        var orphanPenalty = CalculatePenalty(
            snapshot.OrphanArticleCount,
            options.WarningOrphanArticleCount,
            warningPenalty,
            criticalMultiplier);

        var weightedPenalty =
            missingCostPenalty * missingCostWeight +
            unknownSupplierPenalty * unknownSupplierWeight +
            orphanPenalty * orphanWeight;

        var value = (int)Math.Round(100d * (1d - Clamp01(weightedPenalty / totalWeight)));

        var status = value switch
        {
            >= 90 => "excellent",
            >= 75 => "good",
            >= 50 => "warning",
            _ => "critical"
        };

        var dominantRisk = new[]
        {
            (Label: "missing nabavna cena", Score: missingCostPenalty * missingCostWeight),
            (Label: "unknown supplier promet", Score: unknownSupplierPenalty * unknownSupplierWeight),
            (Label: "orphan artikli", Score: orphanPenalty * orphanWeight),
        }
        .OrderByDescending(item => item.Score)
        .First().Label;

        var summary = value switch
        {
            >= 90 => "Analytics signal je pouzdan za odluke.",
            >= 75 => $"Vecina KPI-jeva je pouzdana. Najveci rizik: {dominantRisk}.",
            >= 50 => $"Postoje vidljivi problemi. Najveci rizik: {dominantRisk}.",
            _ => $"Podaci traze hitnu korekciju. Najveci rizik: {dominantRisk}."
        };

        return new DataQualityScoreDto(value, status, summary);
    }

    private static double CalculatePenalty(double metricValue, double warningThreshold, double warningPenalty, double criticalMultiplier)
    {
        if (warningThreshold <= 0d)
        {
            return 0d;
        }

        var normalized = Math.Max(0d, metricValue) / warningThreshold;
        if (normalized <= 1d)
        {
            return normalized * warningPenalty;
        }

        var overflowSpan = Math.Max(0.25d, criticalMultiplier - 1d);
        var overflowProgress = Math.Min((normalized - 1d) / overflowSpan, 1d);
        return warningPenalty + overflowProgress * (1d - warningPenalty);
    }

    private sealed record DataQualityScoreDto(int Value, string Status, string Summary);
}
