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
            var correlationId = ResolveCorrelationId(httpContext);
            try
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
                        CorrelationId = correlationId,
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
            }
            catch (Exception)
            {
                return Results.Ok(new DataQualityHealthResponse(
                    DateTime.UtcNow, 0,
                    DateTime.UtcNow, DateTime.UtcNow,
                    0, 0m, 0m, 0d, 0m, 0d,
                    0, "error", "Data quality health nije dostupan.",
                    new DataQualityHealthThresholds(0, 0d, 0d),
                    AnalyticsResponseMetaFactory.Error(
                        "data_quality_health_error",
                        "Data quality health nije dostupan zbog greske.",
                        correlationId)));
            }
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
            var correlationId = ResolveCorrelationId(httpContext);
            try
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
                    CorrelationId = correlationId,
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
            }
            catch (Exception)
            {
                return Results.Ok(new DataQualityIssueListResponse(
                    request.Page, request.PageSize, 0, [],
                    AnalyticsResponseMetaFactory.Error(
                        "data_quality_list_error",
                        "Lista data quality problema trenutno nije dostupna.",
                        correlationId)));
            }
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
            var correlationId = ResolveCorrelationId(httpContext);
            try
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
                        CorrelationId = correlationId,
                        GeneratedAtUtc = DateTime.UtcNow,
                        DataQualityStatus = items.Count == 0 ? "insufficient_data" : "warning",
                        Message = items.Count == 0 ? "Nema top offender zapisa za izabrani tip problema." : null,
                        EmptyReason = items.Count == 0 ? "no_top_offenders" : null
                    }));
            }
            catch (Exception)
            {
                return Results.Ok(new DataQualityTopOffendersResponse(
                    DataQualityIssueTypes.Normalize(issueType),
                    Math.Clamp(limit ?? 10, 1, 100),
                    0, [],
                    AnalyticsResponseMetaFactory.Error(
                        "data_quality_top_offenders_error",
                        "Top offenders trenutno nisu dostupni.",
                        correlationId)));
            }
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
            var correlationId = ResolveCorrelationId(httpContext);
            var resolvedDays = Math.Clamp(days ?? 7, 2, 90);
            try
            {
                var points = await historyService.GetTrendAsync(resolvedDays, dataScope, ct);

                return Results.Ok(new DataQualityTrendResponse(
                    resolvedDays,
                    string.IsNullOrWhiteSpace(dataScope) ? "all" : dataScope,
                    points,
                    new AnalyticsResponseMetaDto
                    {
                        Success = true,
                        CorrelationId = correlationId,
                        GeneratedAtUtc = DateTime.UtcNow,
                        DataQualityStatus = points.Count == 0 ? "insufficient_data" : "warning",
                        Message = points.Count == 0 ? "Trend data quality-ja nije dostupan za izabrani opseg." : null,
                        EmptyReason = points.Count == 0 ? "no_trend_points" : null
                    }));
            }
            catch (Exception)
            {
                return Results.Ok(new DataQualityTrendResponse(
                    resolvedDays,
                    string.IsNullOrWhiteSpace(dataScope) ? "all" : dataScope,
                    [],
                    AnalyticsResponseMetaFactory.Error(
                        "data_quality_trend_error",
                        "Trend data quality-ja trenutno nije dostupan.",
                        correlationId)));
            }
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
            var correlationId = ResolveCorrelationId(httpContext);
            try
            {
                var report = await BuildPilotDataQualityIntakeReportAsync(
                    trendDb,
                    analyticsDb,
                    healthService,
                    refreshStatusService,
                    fromDate,
                    toDate,
                    storeId,
                    supplierId,
                    dataScope,
                    correlationId,
                    ct);

                return Results.Ok(report);
            }
            catch (Exception)
            {
                var generatedAtUtcErr = DateTime.UtcNow;
                var periodErr = ResolveIntakePeriod(fromDate, toDate);
                return Results.Ok(new PilotDataQualityIntakeReportDto(
                    generatedAtUtcErr, periodErr.FromUtc, periodErr.ToUtc,
                    string.IsNullOrWhiteSpace(dataScope) ? "all" : dataScope,
                    storeId?.ToString(CultureInfo.InvariantCulture),
                    supplierId?.ToString(CultureInfo.InvariantCulture),
                    null, null, 0, "error", "Greska",
                    new PilotDataQualityIntakeLoadedDataDto(0, 0, 0, 0, 0, null, null),
                    new PilotDataQualityIntakeIssuesDto(0, 0, 0, null, null, 0, 0, 0, 0),
                    new PilotDataQualityIntakeImpactDto(0d, 0d, 0, 0, 0),
                    [],
                    AnalyticsResponseMetaFactory.Error(
                        "intake_report_error",
                        "Pilot intake izvestaj trenutno nije dostupan.",
                        correlationId)));
            }
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");

        app.MapGet("/api/analytics/reports/pilot-intake", async (
            HttpContext httpContext,
            TrendplusDbContext trendDb,
            AnalyticsDbContext analyticsDb,
            AnalyticsDataQualityHealthService healthService,
            AnalyticsRefreshStatusService refreshStatusService,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate,
            [FromQuery] int? storeId,
            [FromQuery] int? supplierId,
            [FromQuery] string? scope,
            [FromQuery] string? dataScope,
            CancellationToken ct) =>
        {
            var correlationId = ResolveCorrelationId(httpContext);
            var resolvedScope = !string.IsNullOrWhiteSpace(scope)
                ? scope
                : dataScope;

            try
            {
                var intake = await BuildPilotDataQualityIntakeReportAsync(
                    trendDb,
                    analyticsDb,
                    healthService,
                    refreshStatusService,
                    fromDate,
                    toDate,
                    storeId,
                    supplierId,
                    resolvedScope,
                    correlationId,
                    ct);

                var period = ResolveIntakePeriod(fromDate, toDate);
                var reportId = BuildPilotIntakeReportId(period, storeId, supplierId, resolvedScope);
                var stableQueryUrl = BuildPilotIntakeStableQueryUrl(period, storeId, supplierId, resolvedScope);
                var methodology = "Readiness score vrednuje potpunost master podataka, integritet prodaje i uticaj na preporuke.";
                var recommendationAllowed = intake.ReadinessScore >= 70;
                var warnings = BuildPilotIntakeWarnings(intake, recommendationAllowed);

                var rows = BuildPilotIntakeRows(intake, methodology);
                var sections = rows
                    .GroupBy(x => x.Section)
                    .Select(group => new ReportSectionSummaryDto(group.Key, group.Key, group.Count()))
                    .ToList();

                var payload = new ReportResolvedPayloadDto(
                    "pilot-data-quality-intake",
                    "Trendplus pilot izvestaj kvaliteta podataka",
                    new List<ReportPayloadColumnDto>
                    {
                        new("section", "Sekcija", "text"),
                        new("item", "Stavka", "text"),
                        new("value", "Vrednost", "text"),
                        new("secondary", "Kontekst", "text"),
                        new("note", "Napomena", "text")
                    },
                    rows.Select(row => new ReportPayloadRowDto(row.Section, row.Item, row.Value, row.Secondary, row.Note)).ToList(),
                    new List<ReportPayloadFilterDto>
                    {
                        new("period", "Period", $"{period.FromUtc:yyyy-MM-dd} - {period.ToUtc:yyyy-MM-dd}"),
                        new("dataScope", "Scope", string.IsNullOrWhiteSpace(resolvedScope) ? "all" : resolvedScope),
                        new("storeId", "Objekat", storeId?.ToString(CultureInfo.InvariantCulture) ?? "all"),
                        new("supplierId", "Dobavljac", supplierId?.ToString(CultureInfo.InvariantCulture) ?? "all"),
                    },
                    new List<ReportPayloadFilterDto>
                    {
                        new("reportId", "Report ID", reportId),
                        new("generatedAtUtc", "Generisano", intake.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                        new("lastRefreshAtUtc", "Poslednje osvezenje", intake.LastRefreshAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                        new("dataQualityStatus", "Kvalitet podataka", intake.Meta?.DataQualityStatus ?? "insufficient_data"),
                        new("methodology", "Metodologija", methodology)
                    },
                    "sr-RS",
                    "pilot-data-quality-intake",
                    "analytics-table-default",
                    1);

                return Results.Ok(new PilotIntakeReportResponse(
                    reportId,
                    stableQueryUrl,
                    "Trendplus pilot izveštaj kvaliteta podataka",
                    "pilot-intake",
                    intake.GeneratedAtUtc,
                    period.FromUtc,
                    period.ToUtc,
                    new ReportPeriodDto(period.FromUtc, period.ToUtc, "Pilot intake"),
                    intake.LastRefreshAtUtc,
                    intake.Meta?.DataQualityStatus ?? "insufficient_data",
                    recommendationAllowed,
                    false,
                    warnings,
                    methodology,
                    rows,
                    sections,
                    payload,
                    intake.Meta));
            }
            catch (Exception)
            {
                return Results.Ok(new PilotIntakeReportResponse(
                    "pilot-intake-error",
                    "/analytics/data-quality",
                    "Trendplus pilot izveštaj kvaliteta podataka",
                    "pilot-intake",
                    DateTime.UtcNow,
                    DateTime.UtcNow.Date.AddDays(-29),
                    DateTime.UtcNow.Date,
                    new ReportPeriodDto(DateTime.UtcNow.Date.AddDays(-29), DateTime.UtcNow.Date, "Pilot intake"),
                    null,
                    "insufficient_data",
                    false,
                    false,
                    [],
                    "Readiness score vrednuje potpunost master podataka, integritet prodaje i uticaj na preporuke.",
                    [],
                    [],
                    new ReportResolvedPayloadDto(
                        "pilot-data-quality-intake",
                        "Trendplus pilot izvestaj kvaliteta podataka",
                        [],
                        [],
                        [],
                        [],
                        "sr-RS",
                        "pilot-data-quality-intake",
                        "analytics-table-default",
                        1),
                    AnalyticsResponseMetaFactory.Error(
                        "pilot_intake_report_error",
                        "Pilot intake report trenutno nije dostupan.",
                        correlationId)));
            }
        })
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");
    }

    private static IReadOnlyList<string> BuildPilotIntakeWarnings(
        PilotDataQualityIntakeReportDto report,
        bool recommendationAllowed)
    {
        var warnings = new List<string>();

        if (!recommendationAllowed)
        {
            warnings.Add("Kvalitet podataka ograničava pouzdanost reporta.");
        }

        if (report.Impact.RevenueWithoutCostPercent >= 0.10d)
        {
            warnings.Add("Značajan udeo prihoda je bez nabavne cene.");
        }

        if (!string.IsNullOrWhiteSpace(report.Meta?.WarningMessage))
        {
            warnings.Add(report.Meta.WarningMessage!);
        }

        return warnings
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static async Task<PilotDataQualityIntakeReportDto> BuildPilotDataQualityIntakeReportAsync(
        TrendplusDbContext trendDb,
        AnalyticsDbContext analyticsDb,
        AnalyticsDataQualityHealthService healthService,
        AnalyticsRefreshStatusService refreshStatusService,
        string? fromDate,
        string? toDate,
        int? storeId,
        int? supplierId,
        string? dataScope,
        string correlationId,
        CancellationToken ct)
    {
        var period = ResolveIntakePeriod(fromDate, toDate);
        var lookbackDays = Math.Clamp((int)Math.Ceiling((period.ToUtc.Date - period.FromUtc.Date).TotalDays) + 1, 2, 90);
        var health = await healthService.CaptureAsync(lookbackDays, dataScope, ct);
        var refreshStatus = await refreshStatusService.GetStatusAsync(ct);

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

        return new PilotDataQualityIntakeReportDto(
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
                CorrelationId = correlationId,
                GeneratedAtUtc = generatedAtUtc,
                LastRefreshAtUtc = lastRefreshAtUtc,
                DataQualityStatus = readiness.MetaStatus,
                Message = latestBatch is null ? "Pilot intake izvestaj nema import batch u periodu." : null,
                EmptyReason = latestBatch is null ? "no_import" : null,
                IsPartial = false
            });
    }

    private static string BuildPilotIntakeReportId((DateTime FromUtc, DateTime ToUtc, DateTime ToExclusiveUtc) period, int? storeId, int? supplierId, string? dataScope)
    {
        return $"pir-{period.FromUtc:yyyyMMdd}-{period.ToUtc:yyyyMMdd}-{storeId?.ToString(CultureInfo.InvariantCulture) ?? "all"}-{supplierId?.ToString(CultureInfo.InvariantCulture) ?? "all"}-{(string.IsNullOrWhiteSpace(dataScope) ? "all" : dataScope)}";
    }

    private static string BuildPilotIntakeStableQueryUrl((DateTime FromUtc, DateTime ToUtc, DateTime ToExclusiveUtc) period, int? storeId, int? supplierId, string? dataScope)
    {
        var query = new List<string>
        {
            $"fromDate={Uri.EscapeDataString(period.FromUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}",
            $"toDate={Uri.EscapeDataString(period.ToUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}",
            $"scope={Uri.EscapeDataString(string.IsNullOrWhiteSpace(dataScope) ? "all" : dataScope)}"
        };

        if (storeId.HasValue)
        {
            query.Add($"storeId={storeId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (supplierId.HasValue)
        {
            query.Add($"supplierId={supplierId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        return $"/analytics/data-quality?view=intake&{string.Join("&", query)}";
    }

    private static List<ReportRowDto> BuildPilotIntakeRows(PilotDataQualityIntakeReportDto report, string methodology)
    {
        var rows = new List<ReportRowDto>
        {
            new("Header", "Naziv izvestaja", "Trendplus pilot izvestaj kvaliteta podataka"),
            new("Header", "Period", $"{report.PeriodFromUtc:yyyy-MM-dd} - {report.PeriodToUtc:yyyy-MM-dd}"),
            new("Header", "Scope", report.DataScope),
            new("Header", "Kvalitet podataka", report.Meta?.DataQualityStatus ?? "insufficient_data"),
            new("KPI", "Readiness score", report.ReadinessScore.ToString(CultureInfo.InvariantCulture), report.ReadinessLabel),
            new("Ucitano", "Artikli", report.LoadedData.ArticlesCount.ToString(CultureInfo.InvariantCulture)),
            new("Ucitano", "Stavke prodaje", report.LoadedData.SaleItemsCount.ToString(CultureInfo.InvariantCulture)),
            new("Ucitano", "Racuni", report.LoadedData.ReceiptsCount.ToString(CultureInfo.InvariantCulture)),
            new("Problemi", "Bez dobavljaca", report.Issues.MissingSupplierCount.ToString(CultureInfo.InvariantCulture)),
            new("Problemi", "Bez nabavne cene", report.Issues.MissingCostCount.ToString(CultureInfo.InvariantCulture)),
            new("Problemi", "Prodaja bez artikla", report.Issues.SaleWithoutArticleCount.ToString(CultureInfo.InvariantCulture)),
            new("Uticaj", "Prihod bez cene", report.Impact.RevenueWithoutCostPercent.ToString("0.####", CultureInfo.InvariantCulture)),
            new("Uticaj", "Blokirane preporuke", report.Impact.RecommendationsBlockedCount.ToString(CultureInfo.InvariantCulture)),
            new("Metodologija", "Opis", methodology),
        };

        foreach (var action in report.RecommendedActions)
        {
            rows.Add(new ReportRowDto("Preporucene akcije", "Akcija", action));
        }

        return rows;
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

    public sealed record PilotIntakeReportResponse(
        string ReportId,
        string StableQueryUrl,
        string ReportTitle,
        string ReportType,
        DateTime GeneratedAtUtc,
        DateTime PeriodFrom,
        DateTime PeriodTo,
        ReportPeriodDto Period,
        DateTime? LastRefreshAtUtc,
        string DataQualityStatus,
        bool RecommendationAllowed,
        bool UsedFallback,
        IReadOnlyList<string> Warnings,
        string Methodology,
        IReadOnlyList<ReportRowDto> Rows,
        IReadOnlyList<ReportSectionSummaryDto> Sections,
        ReportResolvedPayloadDto Payload,
        AnalyticsResponseMetaDto? Meta = null);

    public sealed record ReportPeriodDto(
        DateTime FromUtc,
        DateTime ToUtc,
        string Label);

    public sealed record ReportRowDto(
        string Section,
        string Item,
        string Value,
        string? Secondary = null,
        string? Note = null);

    public sealed record ReportSectionSummaryDto(
        string Key,
        string Title,
        int RowCount);

    public sealed record ReportPayloadColumnDto(
        string Key,
        string Header,
        string DataType = "text");

    public sealed record ReportPayloadRowDto(
        string Section,
        string Item,
        string Value,
        string? Secondary = null,
        string? Note = null);

    public sealed record ReportPayloadFilterDto(
        string Key,
        string Label,
        string Value);

    public sealed record ReportResolvedPayloadDto(
        string TableKey,
        string TableTitle,
        IReadOnlyList<ReportPayloadColumnDto> Columns,
        IReadOnlyList<ReportPayloadRowDto> Rows,
        IReadOnlyList<ReportPayloadFilterDto> Filters,
        IReadOnlyList<ReportPayloadFilterDto> Metadata,
        string Locale,
        string DocumentType,
        string TemplateName,
        int TemplateVersion);

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
