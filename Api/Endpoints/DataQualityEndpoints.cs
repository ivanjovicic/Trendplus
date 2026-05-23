using Application.Analytics.Queries.GetDataQualityIssues;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
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
            var totalSuppliers = await trendDb.Dobavljaci.AsNoTracking().CountAsync(ct);
            var totalStores = await analyticsDb.StoresDim.AsNoTracking().CountAsync(ct);

            var salesHeaderQuery = trendDb.ProdajaZaglavlja.AsNoTracking()
                .Where(x => x.DatumProdaje >= period.FromUtc && x.DatumProdaje < period.ToExclusiveUtc);

            if (storeId.HasValue)
            {
                salesHeaderQuery = salesHeaderQuery.Where(x => x.IDObjekat == storeId.Value);
            }

            var salesReceiptCount = await salesHeaderQuery.CountAsync(ct);
            var salesLineCount = await (
                from line in trendDb.ProdajaStavke.AsNoTracking()
                join header in trendDb.ProdajaZaglavlja.AsNoTracking() on line.IdProdaja equals header.Id
                where header.DatumProdaje >= period.FromUtc && header.DatumProdaje < period.ToExclusiveUtc
                select line)
                .CountAsync(ct);

            var missingSupplierCount = await articleQuery.CountAsync(x => x.IDDobavljac == null || x.IDDobavljac == 0, ct);
            var missingShoeTypeCount = await articleQuery.CountAsync(x => x.IDTipObuce == null || x.IDTipObuce == 0, ct);
            var missingCostCount = await articleQuery.CountAsync(x => x.NabavnaCena == null || x.NabavnaCena <= 0m, ct);
            var missingCategoryCount = await articleQuery.CountAsync(x => string.IsNullOrWhiteSpace(x.Kategorija), ct);
            var missingSizeCount = await articleQuery.CountAsync(x => string.IsNullOrWhiteSpace(x.Velicina), ct);
            var missingColorCount = await articleQuery.CountAsync(x => string.IsNullOrWhiteSpace(x.Boja), ct);
            var invalidNameCount = await articleQuery.CountAsync(x => string.IsNullOrWhiteSpace(x.Naziv) || x.Naziv.Trim().Length < 3, ct);
            var duplicateSkuCount = await articleQuery
                .Where(x => !string.IsNullOrWhiteSpace(x.PLU))
                .GroupBy(x => x.PLU)
                .Where(group => group.Count() > 1)
                .CountAsync(ct);

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
                missingShoeTypeCount,
                missingCostCount,
                missingCategoryCount,
                missingSizeCount,
                missingColorCount,
                invalidNameCount,
                duplicateSkuCount,
                ignoredRows,
                latestBatch?.RowsRead ?? 0,
                health);

            var readiness = ResolveReadiness(readinessScore);
            var revenueAtRisk = health.MissingCostRevenue + health.UnknownSupplierRevenue;
            var blockedRecommendationsCount = missingSupplierCount + missingCostCount + invalidNameCount + duplicateSkuCount;
            var latestImportAtUtc = latestBatch?.CompletedAtUtc ?? latestBatch?.StartedAtUtc ?? latestBatch?.QueuedAtUtc;
            var generatedAtUtc = DateTime.UtcNow;

            return Results.Ok(new PilotDataQualityIntakeReportDto(
                generatedAtUtc,
                period.FromUtc,
                period.ToUtc,
                string.IsNullOrWhiteSpace(dataScope) ? "all" : dataScope,
                storeId,
                supplierId,
                latestImportAtUtc,
                readiness.Code,
                readiness.Label,
                readinessScore,
                BuildIntakeSummary(totalArticles, totalSuppliers, totalStores, salesReceiptCount, salesLineCount, latestBatch, readinessScore, readiness.Label),
                new PilotDataQualityIntakeLoadedDataDto(
                    totalArticles,
                    totalSuppliers,
                    totalStores,
                    salesReceiptCount,
                    salesLineCount,
                    latestBatch?.SourceFileName,
                    latestBatch?.SourceFilePath,
                    latestBatch?.RowsRead ?? 0,
                    latestBatch?.RowsAccepted ?? 0,
                    latestBatch?.RowsWritten ?? 0,
                    ignoredRows,
                    latestBatch?.TotalErrors ?? 0),
                new PilotDataQualityIntakeIssuesDto(
                    missingSupplierCount,
                    missingShoeTypeCount,
                    missingCostCount,
                    missingCategoryCount,
                    missingSizeCount,
                    missingColorCount,
                    invalidNameCount,
                    duplicateSkuCount,
                    blockedRecommendationsCount,
                    new[]
                    {
                        new PilotDataQualityIntakeIssueItemDto("missingSupplier", "Nedostaje dobavljac", "critical", missingSupplierCount, "Blokira supplier signale i cini deo preporuka neupotrebljivim."),
                        new PilotDataQualityIntakeIssueItemDto("missingShoeType", "Nedostaje tip obuce", "warning", missingShoeTypeCount, "Smanjuje segmentaciju i filtriranje u dashboardima."),
                        new PilotDataQualityIntakeIssueItemDto("missingCost", "Nedostaje nabavna cena", "critical", missingCostCount, "Onemogucava pouzdanu marzu i profitne signale."),
                        new PilotDataQualityIntakeIssueItemDto("missingCategory", "Nedostaje kategorija", "warning", missingCategoryCount, "Slabi klasifikaciju i reporting kroz kategorije."),
                        new PilotDataQualityIntakeIssueItemDto("missingSize", "Nedostaje velicina", "warning", missingSizeCount, "Smanjuje kvalitet preporuka po veličini."),
                        new PilotDataQualityIntakeIssueItemDto("missingColor", "Nedostaje boja", "warning", missingColorCount, "Smanjuje detaljnost kataloga."),
                        new PilotDataQualityIntakeIssueItemDto("invalidName", "Neispravan naziv", "warning", invalidNameCount, "Kvari pretragu i usability u vizualizacijama."),
                        new PilotDataQualityIntakeIssueItemDto("duplicateSku", "Duplirani PLU", "critical", duplicateSkuCount, "Moze izazvati sudare identiteta i pogresne agregacije."),
                    }),
                new PilotDataQualityIntakeImpactDto(
                    revenueAtRisk,
                    Math.Max(0d, Math.Min(100d, readinessScore)),
                    health.OrphanArticleCount,
                    health.MissingCostRevenueSharePct,
                    health.UnknownSupplierRevenueSharePct,
                    new[]
                    {
                        new PilotDataQualityIntakeImpactItemDto("revenue-at-risk", "Promet u riziku", $"{revenueAtRisk:N2} RSD", "Promet bez nabavne cene i nepoznatog dobavljaca je najosetljiviji signal."),
                        new PilotDataQualityIntakeImpactItemDto("orphan-articles", "Orphan artikli", health.OrphanArticleCount.ToString("N0"), "Artikli bez veza ka normalizovanim dimenzijama slabe preporuke i filtere."),
                        new PilotDataQualityIntakeImpactItemDto("reliability", "Pouzdanost", $"{readinessScore:N1}%", "Sto je score visi, to je pilot spremniji za prikaz glavnih dashboarda."),
                        new PilotDataQualityIntakeImpactItemDto("ignored-rows", "Ignorisani redovi", ignoredRows.ToString("N0"), "Redovi koje import nije prihvatio treba proveriti pre handoff-a."),
                    }),
                new PilotDataQualityIntakeActionsDto(
                    new[]
                    {
                        new PilotDataQualityIntakeActionItemDto("P1", "Popuniti dobavljace i nabavne cene", "Critical podaci blokiraju signal za preporuke i marzu.", "Korigovati master podatke i ponoviti import."),
                        new PilotDataQualityIntakeActionItemDto("P2", "Ocistiti PLU, nazive i duplikate", "Neuredni identifikatori razbijaju matching i filtriranje.", "Deduplicirati artikle i standardizovati nazive."),
                        new PilotDataQualityIntakeActionItemDto("P2", "Potvrditi poslednji import batch", latestBatch is null ? "Nema potvrđenog batch-a za onboarding." : $"Poslednji import: {latestBatch.SourceFileName ?? latestBatch.SourceFilePath ?? "(unknown)"}.", "Proveriti poslednji upload i greske u batch logu."),
                        new PilotDataQualityIntakeActionItemDto("P3", "Pokazati pilot kupcu samo spremne segmente", "Report omogucava selektivno otvaranje dashboarda kada je data quality nizak.", "Koristiti readiness score kao gate za onboarding."),
                    }),
                new AnalyticsResponseMetaDto
                {
                    Success = true,
                    CorrelationId = ResolveCorrelationId(httpContext),
                    GeneratedAtUtc = generatedAtUtc,
                    LastRefreshAtUtc = latestImportAtUtc ?? health.GeneratedAtUtc,
                    DataQualityStatus = readiness.MetaStatus,
                    Message = latestBatch is null ? "Pilot intake report nema import batch u periodu." : null,
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
        int? StoreId,
        int? SupplierId,
        DateTime? LastImportAtUtc,
        string ReadinessStatus,
        string ReadinessLabel,
        int ReadinessScore,
        string Summary,
        PilotDataQualityIntakeLoadedDataDto LoadedData,
        PilotDataQualityIntakeIssuesDto Issues,
        PilotDataQualityIntakeImpactDto Impact,
        PilotDataQualityIntakeActionsDto RecommendedActions,
        AnalyticsResponseMetaDto? Meta = null);

    public sealed record PilotDataQualityIntakeLoadedDataDto(
        int ArticleCount,
        int SupplierCount,
        int StoreCount,
        int SalesReceiptCount,
        int SalesLineCount,
        string? LastImportSourceFile,
        string? LastImportSourcePath,
        int RowsRead,
        int RowsAccepted,
        int RowsWritten,
        int IgnoredRows,
        int TotalErrors);

    public sealed record PilotDataQualityIntakeIssueItemDto(
        string Key,
        string Label,
        string Severity,
        int Count,
        string Impact);

    public sealed record PilotDataQualityIntakeIssuesDto(
        int MissingSupplierCount,
        int MissingShoeTypeCount,
        int MissingCostCount,
        int MissingCategoryCount,
        int MissingSizeCount,
        int MissingColorCount,
        int InvalidNameCount,
        int DuplicateSkuCount,
        int BlockedRecommendationsCount,
        IReadOnlyList<PilotDataQualityIntakeIssueItemDto> Items);

    public sealed record PilotDataQualityIntakeImpactItemDto(
        string Key,
        string Label,
        string Value,
        string Description);

    public sealed record PilotDataQualityIntakeImpactDto(
        decimal RevenueAtRiskRsd,
        double ReliabilityPct,
        int OrphanArticleCount,
        double MissingCostRevenueSharePct,
        double UnknownSupplierRevenueSharePct,
        IReadOnlyList<PilotDataQualityIntakeImpactItemDto> Items);

    public sealed record PilotDataQualityIntakeActionItemDto(
        string Priority,
        string Title,
        string Reason,
        string NextStep);

    public sealed record PilotDataQualityIntakeActionsDto(
        IReadOnlyList<PilotDataQualityIntakeActionItemDto> Items);

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
            >= 85 => new IntakeReadinessDto("Ready", "Spremno za pilot", "good"),
            >= 70 => new IntakeReadinessDto("UsableWithWarnings", "Upotrebljivo uz upozorenja", "warning"),
            >= 50 => new IntakeReadinessDto("PilotLimited", "Pilot ogranicen", "warning"),
            _ => new IntakeReadinessDto("FixDataFirst", "Prvo popravi podatke", "critical"),
        };
    }

    private static int CalculateIntakeScore(
        int totalArticles,
        int missingSupplierCount,
        int missingShoeTypeCount,
        int missingCostCount,
        int missingCategoryCount,
        int missingSizeCount,
        int missingColorCount,
        int invalidNameCount,
        int duplicateSkuCount,
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
        penalty += Ratio(missingShoeTypeCount, articleBase) * 10d;
        penalty += Ratio(missingSizeCount, articleBase) * 5d;
        penalty += Ratio(missingColorCount, articleBase) * 5d;
        penalty += Ratio(invalidNameCount, articleBase) * 6d;
        penalty += Ratio(duplicateSkuCount, articleBase) * 8d;
        penalty += Math.Min(20d, Ratio(ignoredRows, rowBase) * 20d);
        penalty += Math.Min(20d, Math.Max(0d, health.MissingCostRevenueSharePct) / 100d * 20d);
        penalty += Math.Min(15d, Math.Max(0d, health.UnknownSupplierRevenueSharePct) / 100d * 15d);

        return Math.Clamp((int)Math.Round(100d - penalty), 0, 100);
    }

    private static string BuildIntakeSummary(
        int totalArticles,
        int totalSuppliers,
        int totalStores,
        int salesReceiptCount,
        int salesLineCount,
        IntakeBatchSnapshot? latestBatch,
        int readinessScore,
        string readinessLabel)
    {
        var importLabel = latestBatch is null
            ? "nema ucitanog import batch-a"
            : $"poslednji import {latestBatch.SourceFileName ?? latestBatch.SourceFilePath ?? "(unknown)"}";

        return $"{readinessLabel} ({readinessScore}/100). Artikli: {totalArticles:N0}, dobavljaci: {totalSuppliers:N0}, objekti: {totalStores:N0}, racuni: {salesReceiptCount:N0}, stavke: {salesLineCount:N0}. {importLabel}.";
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
