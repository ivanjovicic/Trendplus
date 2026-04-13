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
            var score = BuildScore(snapshot, options.Value);

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
                score = score.Value,
                scoreStatus = score.Status,
                scoreSummary = score.Summary,
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

            return Results.Ok(result);
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
                items));
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
                points));
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
        IReadOnlyList<DataQualityTopOffenderDto> Items);

    public sealed record DataQualityTrendResponse(
        int Days,
        string DataScope,
        IReadOnlyList<DataQualityTrendPointDto> Points);

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
