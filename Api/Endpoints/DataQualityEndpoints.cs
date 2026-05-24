using Application.Analytics.Queries.GetDataQualityIssues;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using Trendplus2.Dtos;

namespace Trendplus2.Endpoints;

public static class DataQualityEndpoints
{
    public static void MapDataQualityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analytics/data-quality/health", async (
            HttpContext httpContext,
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
                    CorrelationId = ResolveCorrelationId(httpContext),
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
                    EmptyReason = snapshot.TotalRevenue <= 0 ? "no_sales_in_period" : null,
                    IsPartial = false
                }));
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/data-quality/list", async (
            HttpContext httpContext,
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
                CorrelationId = ResolveCorrelationId(httpContext),
                GeneratedAtUtc = DateTime.UtcNow,
                DataQualityStatus = result.Total == 0 ? "insufficient_data" : "warning",
                Message = result.Total == 0 ? "Nema otvorenih data quality problema za izabrani filter." : null,
                EmptyReason = result.Total == 0 ? "no_open_issues" : null
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
            HttpContext httpContext,
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
                    CorrelationId = ResolveCorrelationId(httpContext),
                    GeneratedAtUtc = DateTime.UtcNow,
                    DataQualityStatus = items.Count == 0 ? "insufficient_data" : "warning",
                    Message = items.Count == 0 ? "Nema top offender zapisa za izabrani tip problema." : null,
                    EmptyReason = items.Count == 0 ? "no_top_offenders" : null
                }));
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/data-quality/trend", async (
            HttpContext httpContext,
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
                    CorrelationId = ResolveCorrelationId(httpContext),
                    GeneratedAtUtc = DateTime.UtcNow,
                    DataQualityStatus = points.Count == 0 ? "insufficient_data" : "warning",
                    Message = points.Count == 0 ? "Trend data quality-ja nije dostupan za izabrani opseg." : null,
                    EmptyReason = points.Count == 0 ? "no_trend_points" : null
                }));
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/data-quality/intake-report", async (
            HttpContext httpContext,
            TrendplusDbContext trendDb,
            AnalyticsDbContext analyticsDb,
            AnalyticsDataQualityHealthService healthService,
            AnalyticsRefreshStatusService refreshStatusService,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate,
            [FromQuery] int? storeId,
            [FromQuery] int? supplierId,
            [FromQuery] string? dataScope,
            CancellationToken ct) =>
        {
            var period = ResolveIntakePeriod(fromDate, toDate);
            var lookbackDays = Math.Clamp((int)Math.Ceiling((period.ToUtc.Date - period.FromUtc.Date).TotalDays) + 1, 2, 90);
            var health = await healthService.CaptureAsync(lookbackDays, dataScope, ct);
            var refreshStatus = refreshStatusService.GetStatus();

            IQueryable<Domain.Model.Artikli> articleQuery = trendDb.Artikli.AsNoTracking();
            if (storeId.HasValue)
            {
                articleQuery = articleQuery.Where(x => x.IDObjekat == storeId.Value);
            }

            if (supplierId.HasValue)
            {
                articleQuery = articleQuery.Where(x => x.IDDobavljac == supplierId.Value);
            }

            var totalArticles = await articleQuery.CountAsync(ct);
            var totalSuppliers = await articleQuery
                .Where(x => x.IDDobavljac.HasValue && x.IDDobavljac.Value > 0)
                .Select(x => x.IDDobavljac!.Value)
                .Distinct()
                .CountAsync(ct);
            var totalStores = await analyticsDb.StoresDim.AsNoTracking().CountAsync(ct);

            var salesLinesScoped =
                from line in trendDb.ProdajaStavke.AsNoTracking()
                join header in trendDb.ProdajaZaglavlja.AsNoTracking() on line.IdProdaja equals header.Id
                where header.DatumProdaje >= period.FromUtc && header.DatumProdaje < period.ToExclusiveUtc
                select new { line.Id, line.IdProdaja, line.IdArtikal, line.Cena, header.DatumProdaje, header.IDObjekat };

            if (storeId.HasValue)
            {
                salesLinesScoped = salesLinesScoped.Where(x => x.IDObjekat == storeId.Value);
            }

            if (supplierId.HasValue)
            {
                salesLinesScoped =
                    from line in salesLinesScoped
                    join article in trendDb.Artikli.AsNoTracking() on line.IdArtikal equals article.Id
                    where article.IDDobavljac == supplierId.Value
                    select line;
            }

            var salesLineCount = await salesLinesScoped.CountAsync(ct);
            var salesReceiptCount = await salesLinesScoped.Select(x => x.IdProdaja).Distinct().CountAsync(ct);
            var firstSaleDate = await salesLinesScoped.MinAsync(x => (DateTime?)x.DatumProdaje, ct);
            var lastSaleDate = await salesLinesScoped.MaxAsync(x => (DateTime?)x.DatumProdaje, ct);

            var missingSupplierCount = await articleQuery.CountAsync(x => x.IDDobavljac == null || x.IDDobavljac == 0, ct);
            var missingCostCount = await articleQuery.CountAsync(x => x.NabavnaCena == null || x.NabavnaCena <= 0m, ct);
            var missingCategoryCount = await articleQuery.CountAsync(x => string.IsNullOrWhiteSpace(x.Kategorija), ct);
            var missingSizeCount = await articleQuery.CountAsync(x => string.IsNullOrWhiteSpace(x.Velicina), ct);
            var missingColorCount = await articleQuery.CountAsync(x => string.IsNullOrWhiteSpace(x.Boja), ct);
            var duplicateSkuCount = await articleQuery
                .Where(x => !string.IsNullOrWhiteSpace(x.PLU))
                .GroupBy(x => x.PLU)
                .Where(group => group.Count() > 1)
                .CountAsync(ct);
            var missingSupplierNameCount = await (
                from article in articleQuery
                where article.IDDobavljac.HasValue && article.IDDobavljac.Value > 0
                join supplier in trendDb.Dobavljaci.AsNoTracking() on article.IDDobavljac equals supplier.Id into supplierJoin
                from supplier in supplierJoin.DefaultIfEmpty()
                where supplier == null || string.IsNullOrWhiteSpace(supplier.Naziv)
                select article.Id)
                .CountAsync(ct);

            var saleWithoutArticleCount = supplierId.HasValue
                ? 0
                : await (
                    from line in salesLinesScoped
                    join article in trendDb.Artikli.AsNoTracking() on line.IdArtikal equals article.Id into articleJoin
                    from article in articleJoin.DefaultIfEmpty()
                    where article == null
                    select line.Id)
                    .CountAsync(ct);

            var zeroOrNegativePriceCount = await salesLinesScoped.CountAsync(x => x.Cena <= 0m, ct);
            var soldArticleIds = salesLinesScoped.Select(x => x.IdArtikal).Distinct();
            var insufficientSignalCount = totalArticles == 0
                ? 0
                : await articleQuery.CountAsync(x => !soldArticleIds.Contains(x.Id), ct);

            var latestBatch = await trendDb.DataImportBatches
                .AsNoTracking()
                .OrderByDescending(x => x.CompletedAtUtc)
                .ThenByDescending(x => x.StartedAtUtc)
                .ThenByDescending(x => x.QueuedAtUtc)
                .Select(x => new IntakeBatchSnapshot
                {
                    SourceFileName = x.SourceFileName,
                    SourceFilePath = x.SourceFilePath,
                    CompletedAtUtc = x.CompletedAtUtc,
                    StartedAtUtc = x.StartedAtUtc,
                    QueuedAtUtc = x.QueuedAtUtc,
                    RowsRead = x.RowsRead,
                    RowsAccepted = x.RowsAccepted,
                    RowsWritten = x.RowsWritten,
                    SkippedRowCount = x.SkippedRowCount,
                    TotalErrors = x.TotalErrors,
                    Status = x.Status
                })
                .FirstOrDefaultAsync(ct);

            var ignoredRows = latestBatch is null
                ? 0
                : latestBatch.SkippedRowCount > 0
                    ? latestBatch.SkippedRowCount
                    : Math.Max(0, latestBatch.RowsRead - latestBatch.RowsAccepted);

            var readinessScore = CalculateIntakeScore(
                totalArticles,
                missingSupplierCount,
                missingCostCount,
                missingCategoryCount,
                missingSizeCount,
                missingColorCount,
                missingSupplierNameCount,
                duplicateSkuCount,
                saleWithoutArticleCount,
                zeroOrNegativePriceCount,
                ignoredRows,
                latestBatch?.RowsRead ?? 0,
                health);

            var readiness = ResolveReadiness(readinessScore);
            var blockedRecommendationsCount = missingSupplierCount + missingCostCount + missingSupplierNameCount + saleWithoutArticleCount;
            var latestImportAtUtc = latestBatch?.CompletedAtUtc ?? latestBatch?.StartedAtUtc ?? latestBatch?.QueuedAtUtc;
            var generatedAtUtc = DateTime.UtcNow;
            var lastRefreshAtUtc = refreshStatus.LastSuccessfulRefreshAtUtc ?? health.GeneratedAtUtc;
            var articlesWithoutSupplierPercent = totalArticles <= 0 ? 0d : (double)missingSupplierCount / totalArticles;

            return Results.Ok(new PilotDataQualityIntakeReportDto(
                generatedAtUtc,
                period.FromUtc,
                period.ToUtc,
                string.IsNullOrWhiteSpace(dataScope) ? "all" : dataScope,
                storeId?.ToString(CultureInfo.InvariantCulture),
                supplierId?.ToString(CultureInfo.InvariantCulture),
                latestImportAtUtc,
                lastRefreshAtUtc,
                readinessScore,
                readiness.Code,
                readiness.Label,
                new PilotDataQualityIntakeLoadedDataDto(
                    ArticlesCount: totalArticles,
                    SaleItemsCount: salesLineCount,
                    ReceiptsCount: salesReceiptCount,
                    SuppliersCount: totalSuppliers,
                    StoresCount: storeId.HasValue ? 1 : totalStores,
                    FirstSaleDate: firstSaleDate,
                    LastSaleDate: lastSaleDate),
                new PilotDataQualityIntakeIssuesDto(
                    MissingSupplierCount: missingSupplierCount,
                    MissingCostCount: missingCostCount,
                    MissingCategoryCount: missingCategoryCount,
                    MissingColorCount: missingColorCount,
                    MissingSizeCount: missingSizeCount,
                    SaleWithoutArticleCount: saleWithoutArticleCount,
                    ZeroOrNegativePriceCount: zeroOrNegativePriceCount,
                    DuplicateSkuCount: duplicateSkuCount,
                    MissingSupplierNameCount: missingSupplierNameCount),
                new PilotDataQualityIntakeImpactDto(
                    RevenueWithoutCostPercent: Math.Max(0d, health.MissingCostRevenueSharePct) / 100d,
                    ArticlesWithoutSupplierPercent: articlesWithoutSupplierPercent,
                    RecommendationsBlockedCount: blockedRecommendationsCount,
                    IgnoredRowsCount: ignoredRows,
                    InsufficientSignalCount: insufficientSignalCount),
                new[]
                {
                    "Povezi dobavljace",
                    "Dopuni nabavne cene",
                    "Proveri artikle bez kategorije",
                    "Proveri redove prodaje bez artikla",
                    "Pokreni osvezavanje analitike",
                    "Proveri import mapu",
                },
                new AnalyticsResponseMetaDto
                {
                    Success = true,
                    CorrelationId = ResolveCorrelationId(httpContext),
                    GeneratedAtUtc = generatedAtUtc,
                    LastRefreshAtUtc = lastRefreshAtUtc,
                    DataQualityStatus = readiness.MetaStatus,
                    Message = latestBatch is null ? "Pilot intake izvestaj nema import batch u periodu." : null,
                    EmptyReason = latestBatch is null ? "no_import" : null,
                    IsPartial = false
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

    public sealed record PilotDataQualityIntakeReportDto(
        DateTime GeneratedAtUtc,
        DateTime? PeriodFromUtc,
        DateTime? PeriodToUtc,
        string DataScope,
        string? StoreId,
        string? SupplierId,
        DateTime? LastImportAtUtc,
        DateTime? LastRefreshAtUtc,
        int ReadinessScore,
        string ReadinessStatus,
        string ReadinessLabel,
        PilotDataQualityIntakeLoadedDataDto LoadedData,
        PilotDataQualityIntakeIssuesDto Issues,
        PilotDataQualityIntakeImpactDto Impact,
        IReadOnlyList<string> RecommendedActions,
        AnalyticsResponseMetaDto? Meta = null);

    public sealed record PilotDataQualityIntakeLoadedDataDto(
        int ArticlesCount,
        int SaleItemsCount,
        int ReceiptsCount,
        int SuppliersCount,
        int StoresCount,
        DateTime? FirstSaleDate,
        DateTime? LastSaleDate);

    public sealed record PilotDataQualityIntakeIssuesDto(
        int MissingSupplierCount,
        int MissingCostCount,
        int MissingCategoryCount,
        int? MissingColorCount,
        int? MissingSizeCount,
        int SaleWithoutArticleCount,
        int ZeroOrNegativePriceCount,
        int DuplicateSkuCount,
        int MissingSupplierNameCount);

    public sealed record PilotDataQualityIntakeImpactDto(
        double RevenueWithoutCostPercent,
        double ArticlesWithoutSupplierPercent,
        int RecommendationsBlockedCount,
        int IgnoredRowsCount,
        int InsufficientSignalCount);

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

    private static string ResolveCorrelationId(HttpContext httpContext)
    {
        var responseHeader = httpContext.Response.Headers["X-Correlation-ID"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(responseHeader))
        {
            return responseHeader;
        }

        var requestHeader = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(requestHeader))
        {
            return requestHeader;
        }

        return httpContext.TraceIdentifier;
    }

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

    private static IntakeReadinessDto ResolveReadiness(int readinessScore)
    {
        return readinessScore switch
        {
            >= 90 => new IntakeReadinessDto("excellent", "Spremno za pouzdanu analitiku", "good"),
            >= 70 => new IntakeReadinessDto("good", "Upotrebljivo uz upozorenja", "warning"),
            >= 40 => new IntakeReadinessDto("warning", "Pilot moze, ali preporuke ogranicene", "warning"),
            _ => new IntakeReadinessDto("critical", "Prvo srediti podatke", "critical"),
        };
    }

    private static int CalculateIntakeScore(
        int totalArticles,
        int missingSupplierCount,
        int missingCostCount,
        int missingCategoryCount,
        int missingSizeCount,
        int missingColorCount,
        int missingSupplierNameCount,
        int duplicateSkuCount,
        int saleWithoutArticleCount,
        int zeroOrNegativePriceCount,
        int ignoredRows,
        int rowsRead,
        AnalyticsDataQualityHealthSnapshot health)
    {
        static double Ratio(int numerator, int denominator) => denominator <= 0 ? 0d : (double)numerator / denominator;

        var articleBase = Math.Max(totalArticles, 1);
        var rowBase = Math.Max(rowsRead, 1);

        var penalty = 0d;
        penalty += Ratio(missingSupplierCount, articleBase) * 30d;
        penalty += Ratio(missingCostCount, articleBase) * 24d;
        penalty += Ratio(missingCategoryCount, articleBase) * 10d;
        penalty += Ratio(missingSizeCount, articleBase) * 5d;
        penalty += Ratio(missingColorCount, articleBase) * 5d;
        penalty += Ratio(missingSupplierNameCount, articleBase) * 8d;
        penalty += Ratio(duplicateSkuCount, articleBase) * 8d;
        penalty += Math.Min(15d, Ratio(saleWithoutArticleCount, rowBase) * 15d);
        penalty += Math.Min(10d, Ratio(zeroOrNegativePriceCount, rowBase) * 10d);
        penalty += Math.Min(20d, Ratio(ignoredRows, rowBase) * 20d);
        penalty += Math.Min(20d, Math.Max(0d, health.MissingCostRevenueSharePct) / 100d * 20d);
        penalty += Math.Min(15d, Math.Max(0d, health.UnknownSupplierRevenueSharePct) / 100d * 15d);

        return Math.Clamp((int)Math.Round(100d - penalty), 0, 100);
    }

    private static (DateTime FromUtc, DateTime ToUtc, DateTime ToExclusiveUtc) ResolveIntakePeriod(string? fromDate, string? toDate)
    {
        var today = DateTime.UtcNow.Date;
        var fromUtc = TryParseUtcDate(fromDate) ?? today.AddDays(-29);
        var toUtc = TryParseUtcDate(toDate) ?? today;

        if (toUtc < fromUtc)
        {
            (fromUtc, toUtc) = (toUtc, fromUtc);
        }

        return (fromUtc, toUtc, toUtc.AddDays(1));
    }

    private static DateTime? TryParseUtcDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return null;
        }

        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
    }

    private sealed record DataQualityScoreDto(int Value, string Status, string Summary);
    private sealed record IntakeReadinessDto(string Code, string Label, string MetaStatus);
    private sealed record IntakeBatchSnapshot
    {
        public string? SourceFileName { get; init; }
        public string? SourceFilePath { get; init; }
        public DateTime? CompletedAtUtc { get; init; }
        public DateTime StartedAtUtc { get; init; }
        public DateTime QueuedAtUtc { get; init; }
        public int RowsRead { get; init; }
        public int RowsAccepted { get; init; }
        public int RowsWritten { get; init; }
        public int SkippedRowCount { get; init; }
        public int TotalErrors { get; init; }
        public string Status { get; init; } = string.Empty;
    }
}
