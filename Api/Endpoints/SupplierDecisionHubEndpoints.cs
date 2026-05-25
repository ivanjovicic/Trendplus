using Api.Config;
using Api.Services;
using Application.Analytics;
using Infrastructure.Services.Caching;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Trendplus2.Dtos;

namespace Trendplus2.Endpoints;

public static class SupplierDecisionHubEndpoints
{
    private const int DefaultLookbackDays = 180;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private const decimal HighConfidenceThreshold = 60m;

    private sealed class SupplierDecisionUnavailableException : Exception
    {
        public SupplierDecisionUnavailableException(string errorCode, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }

        public string ErrorCode { get; }
    }

    public static void MapSupplierDecisionHubEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics/suppliers/decision-hub")
            .WithTags("Supplier Decision Hub")
            .RequireRateLimiting("analytics");

        group.MapGet("/summary", async (
            HttpContext httpContext,
            IConfiguration configuration,
            IAnalyticsCacheService cache,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? category = null,
            string? gender = null,
            int? seasonId = null,
            decimal? minRevenue = null,
            bool onlyHighConfidence = false,
            bool excludeOosBeforeMarkdown = false,
            int? supplierId = null,
            int? storeId = null,
            string dataScope = "all",
            CancellationToken ct = default) =>
        {
            if (!TryCreateFilters(
                    fromDate,
                    toDate,
                    category,
                    gender,
                    seasonId,
                    minRevenue,
                    onlyHighConfidence,
                    excludeOosBeforeMarkdown,
                    supplierId,
                    storeId,
                    dataScope,
                    out var filters,
                    out var validationError))
            {
                return Results.ValidationProblem(validationError!);
            }

            var activeFilters = filters!;
            var analyticsConnectionString = GetAnalyticsConnectionString(configuration);
            var cacheKey = AnalyticsCacheKeys.SupplierDecisionHubSummary(
                activeFilters.FromDate,
                activeFilters.ToDate,
                activeFilters.Category,
                activeFilters.Gender,
                activeFilters.SeasonId,
                activeFilters.MinRevenue,
                activeFilters.OnlyHighConfidence,
                activeFilters.ExcludeOosBeforeMarkdown,
                activeFilters.SupplierId,
                activeFilters.StoreId,
                activeFilters.DataScope);

            try
            {
                var response = await cache.GetOrSetAsync(
                    cacheKey,
                    async () =>
                    {
                        var dataset = await GetSupplierRowsCachedAsync(cache, analyticsConnectionString, activeFilters, ct);
                        return BuildSummaryResponse(dataset, activeFilters);
                    },
                    CacheExpiration.HeavyAnalytics,
                    ct);

                response = response with { Meta = ApplyCorrelationId(response.Meta, ResolveCorrelationId(httpContext)) };
                return Results.Ok(response);
            }
            catch (SupplierDecisionUnavailableException ex)
            {
                var emptyDataset = new SupplierRowsDataset(Array.Empty<SupplierScoreRow>(), 0, 0, DateTime.UtcNow);
                return Results.Ok(new SummaryResponse(
                    activeFilters.FromDate,
                    activeFilters.ToDate,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    [],
                    [],
                    [],
                    BuildDecisionScoreDataNote(activeFilters),
                    BuildScorecardTrustMetadata(emptyDataset, activeFilters),
                    BuildErrorMeta(ex.ErrorCode, ex.Message, ResolveCorrelationId(httpContext))));
            }
        });

        group.MapGet("/quadrant", async (
            HttpContext httpContext,
            IConfiguration configuration,
            IAnalyticsCacheService cache,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? category = null,
            string? gender = null,
            int? seasonId = null,
            decimal? minRevenue = null,
            bool onlyHighConfidence = false,
            bool excludeOosBeforeMarkdown = false,
            int? supplierId = null,
            int? storeId = null,
            string dataScope = "all",
            CancellationToken ct = default) =>
        {
            if (!TryCreateFilters(
                    fromDate,
                    toDate,
                    category,
                    gender,
                    seasonId,
                    minRevenue,
                    onlyHighConfidence,
                    excludeOosBeforeMarkdown,
                    supplierId,
                    storeId,
                    dataScope,
                    out var filters,
                    out var validationError))
            {
                return Results.ValidationProblem(validationError!);
            }

            var activeFilters = filters!;
            var analyticsConnectionString = GetAnalyticsConnectionString(configuration);
            var cacheKey = AnalyticsCacheKeys.SupplierDecisionHubQuadrant(
                activeFilters.FromDate,
                activeFilters.ToDate,
                activeFilters.Category,
                activeFilters.Gender,
                activeFilters.SeasonId,
                activeFilters.MinRevenue,
                activeFilters.OnlyHighConfidence,
                activeFilters.ExcludeOosBeforeMarkdown,
                activeFilters.SupplierId,
                activeFilters.StoreId,
                activeFilters.DataScope);

            try
            {
                var response = await cache.GetOrSetAsync(
                    cacheKey,
                    async () =>
                    {
                        var dataset = await GetSupplierRowsCachedAsync(cache, analyticsConnectionString, activeFilters, ct);
                        var rows = dataset.Rows;
                        var trustMetadata = BuildScorecardTrustMetadata(dataset, activeFilters);
                        return new QuadrantResponse(
                            rows
                                .OrderByDescending(x => x.Revenue)
                                .Select(x => new QuadrantItem(
                                    x.SupplierId,
                                    x.SupplierName,
                                    x.Revenue,
                                    x.MarkdownDependencyScore,
                                    x.FullPriceSellthrough,
                                    x.PreMarkdownMarginPct,
                                    x.SupplierQualityIndex,
                                    x.RecommendationCode,
                                        x.ConfidenceScore,
                                        x.ReliabilityPct,
                                        x.DataQualityStatus,
                                        x.StatusReason,
                                        x.ReasonCodes))
                                .ToList(),
                                    trustMetadata,
                                    BuildResponseMeta(rows, trustMetadata));
                    },
                    CacheExpiration.HeavyAnalytics,
                    ct);

                response = response with { Meta = ApplyCorrelationId(response.Meta, ResolveCorrelationId(httpContext)) };
                return Results.Ok(response);
            }
            catch (SupplierDecisionUnavailableException ex)
            {
                var emptyDataset = new SupplierRowsDataset(Array.Empty<SupplierScoreRow>(), 0, 0, DateTime.UtcNow);
                return Results.Ok(new QuadrantResponse(
                    [],
                    BuildScorecardTrustMetadata(emptyDataset, activeFilters),
                    BuildErrorMeta(ex.ErrorCode, ex.Message, ResolveCorrelationId(httpContext))));
            }
        });

        group.MapGet("/ranking", async (
            HttpContext httpContext,
            IConfiguration configuration,
            IAnalyticsCacheService cache,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? category = null,
            string? gender = null,
            int? seasonId = null,
            decimal? minRevenue = null,
            bool onlyHighConfidence = false,
            bool excludeOosBeforeMarkdown = false,
            int? supplierId = null,
            int? storeId = null,
            string dataScope = "all",
            int page = 1,
            int pageSize = DefaultPageSize,
            string? sortBy = null,
            string? sortDir = null,
            CancellationToken ct = default) =>
        {
            if (!TryCreateFilters(
                    fromDate,
                    toDate,
                    category,
                    gender,
                    seasonId,
                    minRevenue,
                    onlyHighConfidence,
                    excludeOosBeforeMarkdown,
                    supplierId,
                    storeId,
                    dataScope,
                    out var filters,
                    out var validationError))
            {
                return Results.ValidationProblem(validationError!);
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            var normalizedSortBy = NormalizeRankingSortBy(sortBy);
            var normalizedSortDir = NormalizeRankingSortDir(sortDir);

            var activeFilters = filters!;
            var analyticsConnectionString = GetAnalyticsConnectionString(configuration);
            var cacheKey = AnalyticsCacheKeys.SupplierDecisionHubRanking(
                activeFilters.FromDate,
                activeFilters.ToDate,
                activeFilters.Category,
                activeFilters.Gender,
                activeFilters.SeasonId,
                activeFilters.MinRevenue,
                activeFilters.OnlyHighConfidence,
                activeFilters.ExcludeOosBeforeMarkdown,
                activeFilters.SupplierId,
                activeFilters.StoreId,
                activeFilters.DataScope,
                page,
                pageSize,
                normalizedSortBy,
                normalizedSortDir);

            try
            {
                var response = await cache.GetOrSetAsync(
                    cacheKey,
                    async () =>
                    {
                        var dataset = await GetSupplierRowsCachedAsync(cache, analyticsConnectionString, activeFilters, ct);
                        var ordered = ApplyRankingSort(dataset.Rows, normalizedSortBy, normalizedSortDir).ToList();
                        var orderedDataset = dataset with { Rows = ordered };
                        var trustMetadata = BuildScorecardTrustMetadata(orderedDataset, activeFilters);
                        var paged = ordered
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .Select(x => new RankingItem(
                                x.SupplierId,
                                x.SupplierName,
                                x.Revenue,
                                x.Units,
                                x.FullPriceRevenueShare,
                                x.FullPriceSellthrough,
                                x.PreMarkdownMarginPct,
                                x.MarkdownRevenueShare,
                                x.DeadStockRate,
                                x.UnsoldStockValue,
                                x.RepeatWinnerRate,
                                x.MlSupplierScore,
                                x.SupplierQualityIndex,
                                x.RecommendationCode,
                                x.ConfidenceScore,
                                x.ReliabilityPct,
                                x.DataQualityStatus,
                                x.StatusReason,
                                x.ReasonCodes))
                            .ToList();

                        return new RankingResponse(
                            page,
                            pageSize,
                            ordered.Count,
                            paged,
                            BuildDecisionScoreDataNote(activeFilters),
                            trustMetadata,
                            BuildResponseMeta(ordered, trustMetadata));
                    },
                    CacheExpiration.HeavyAnalytics,
                    ct);

                response = response with { Meta = ApplyCorrelationId(response.Meta, ResolveCorrelationId(httpContext)) };
                return Results.Ok(response);
            }
            catch (SupplierDecisionUnavailableException ex)
            {
                var emptyDataset = new SupplierRowsDataset(Array.Empty<SupplierScoreRow>(), 0, 0, DateTime.UtcNow);
                return Results.Ok(new RankingResponse(
                    page,
                    pageSize,
                    0,
                    [],
                    BuildDecisionScoreDataNote(activeFilters),
                    BuildScorecardTrustMetadata(emptyDataset, activeFilters),
                    BuildErrorMeta(ex.ErrorCode, ex.Message, ResolveCorrelationId(httpContext))));
            }
        });

        group.MapGet("/{supplierId:int}/details", async (
            int supplierId,
            HttpContext httpContext,
            IConfiguration configuration,
            IAnalyticsCacheService cache,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? category = null,
            string? gender = null,
            int? seasonId = null,
            decimal? minRevenue = null,
            bool onlyHighConfidence = false,
            bool excludeOosBeforeMarkdown = false,
            int? storeId = null,
            string dataScope = "all",
            CancellationToken ct = default) =>
        {
            if (!TryCreateFilters(
                    fromDate,
                    toDate,
                    category,
                    gender,
                    seasonId,
                    minRevenue,
                    onlyHighConfidence,
                    excludeOosBeforeMarkdown,
                    supplierId,
                    storeId,
                    dataScope,
                    out var filters,
                    out var validationError))
            {
                return Results.ValidationProblem(validationError!);
            }

            var activeFilters = filters!;
            var analyticsConnectionString = GetAnalyticsConnectionString(configuration);
            var cacheKey = AnalyticsCacheKeys.SupplierDecisionHubDetails(
                activeFilters.FromDate,
                activeFilters.ToDate,
                activeFilters.Category,
                activeFilters.Gender,
                activeFilters.SeasonId,
                activeFilters.MinRevenue,
                activeFilters.OnlyHighConfidence,
                activeFilters.ExcludeOosBeforeMarkdown,
                supplierId,
                activeFilters.StoreId,
                activeFilters.DataScope);

            SupplierDecisionHubDetailsCacheEntry response;
            try
            {
                response = await cache.GetOrSetAsync(
                    cacheKey,
                    async () =>
                    {
                        var dataset = await GetSupplierRowsCachedAsync(cache, analyticsConnectionString, activeFilters, ct);
                        var supplier = dataset.Rows.FirstOrDefault();
                        if (supplier is null)
                        {
                            return new SupplierDecisionHubDetailsCacheEntry(false, null);
                        }

                        var details = await BuildDetailsResponseAsync(analyticsConnectionString, activeFilters, supplier, ct);
                        return new SupplierDecisionHubDetailsCacheEntry(true, details);
                    },
                    CacheExpiration.HeavyAnalytics,
                    ct);
            }
            catch (SupplierDecisionUnavailableException ex)
            {
                return Results.Problem(
                    title: "Supplier details trenutno nisu dostupni.",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    extensions: new Dictionary<string, object?>
                    {
                        ["errorCode"] = ex.ErrorCode,
                        ["correlationId"] = ResolveCorrelationId(httpContext)
                    });
            }

            if (!response.Found || response.Response is null)
            {
                return Results.NotFound(new { message = $"Supplier {supplierId} not found for the selected filter set." });
            }

            return Results.Ok(response.Response);
        });

    }

    internal static async Task<IResult> HandleSupplierDecisionReportAsync(
        HttpContext httpContext,
        IConfiguration configuration,
        IAnalyticsCacheService cache,
        AnalyticsCacheAdminService cacheAdmin,
        ILoggerFactory loggerFactory,
        AnalyticsRefreshStatusService refreshStatusService,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? category = null,
        string? gender = null,
        int? seasonId = null,
        decimal? minRevenue = null,
        bool onlyHighConfidence = false,
        bool excludeOosBeforeMarkdown = false,
        int? supplierId = null,
        int? storeId = null,
        string? scope = null,
        string? dataScope = null,
        CancellationToken ct = default)
    {
        var resolvedScope = !string.IsNullOrWhiteSpace(scope)
            ? scope
            : (string.IsNullOrWhiteSpace(dataScope) ? "all" : dataScope);

        if (!TryCreateFilters(
                fromDate,
                toDate,
                category,
                gender,
                seasonId,
                minRevenue,
                onlyHighConfidence,
                excludeOosBeforeMarkdown,
                supplierId,
                storeId,
                resolvedScope,
                out var filters,
                out var validationError))
        {
            return Results.ValidationProblem(validationError!);
        }

        var activeFilters = filters!;
        var correlationId = ResolveCorrelationId(httpContext);
        var analyticsConnectionString = GetAnalyticsConnectionString(configuration);
        var reportCacheVersion = await cacheAdmin.GetReportCacheVersionAsync(ct);
        var reportCacheKey = AnalyticsCacheKeys.SupplierDecisionReport(
            activeFilters.FromDate,
            activeFilters.ToDate,
            activeFilters.Category,
            activeFilters.Gender,
            activeFilters.SeasonId,
            activeFilters.MinRevenue,
            activeFilters.OnlyHighConfidence,
            activeFilters.ExcludeOosBeforeMarkdown,
            activeFilters.SupplierId,
            activeFilters.StoreId,
            activeFilters.DataScope,
            reportCacheVersion);
        var reportCacheKeyHash = AnalyticsCacheKeys.SafeKeyFingerprint(reportCacheKey);
        var cacheLogger = loggerFactory.CreateLogger("SupplierDecisionReportCache");

        try
        {
            var cachedReport = await cache.GetAsync<AnalyticsReportResponseDto>(reportCacheKey, ct);
            AnalyticsReportResponseDto report;
            if (cachedReport is not null)
            {
                cacheLogger.LogInformation(
                    "Supplier decision report cache HIT. ReportType={ReportType} KeyHash={CacheKeyHash} Version={ReportCacheVersion} SupplierId={SupplierId} StoreId={StoreId} DataScope={DataScope}",
                    "supplier-decision",
                    reportCacheKeyHash,
                    reportCacheVersion,
                    activeFilters.SupplierId,
                    activeFilters.StoreId,
                    activeFilters.DataScope);
                report = cachedReport;
            }
            else
            {
                cacheLogger.LogInformation(
                    "Supplier decision report cache MISS. ReportType={ReportType} KeyHash={CacheKeyHash} Version={ReportCacheVersion} SupplierId={SupplierId} StoreId={StoreId} DataScope={DataScope}",
                    "supplier-decision",
                    reportCacheKeyHash,
                    reportCacheVersion,
                    activeFilters.SupplierId,
                    activeFilters.StoreId,
                    activeFilters.DataScope);

                var dataset = await GetSupplierRowsCachedAsync(cache, analyticsConnectionString, activeFilters, ct);
                var summary = BuildSummaryResponse(dataset, activeFilters);
                var details = await BuildSupplierDecisionReportDetailsAsync(analyticsConnectionString, activeFilters, dataset, ct);
                ReportRefreshInfo? refreshInfo = null;

                try
                {
                    var refreshStatus = await refreshStatusService.GetStatusAsync(ct);
                    refreshInfo = ResolveReportRefreshInfo(refreshStatus, "supplier_decision_mvs");
                }
                catch
                {
                    refreshInfo = null;
                }

                report = BuildSupplierDecisionReportResponse(summary, dataset, activeFilters, refreshInfo, details);

                await cache.SetAsync(reportCacheKey, report, CacheExpiration.HeavyAnalytics, ct);
                cacheLogger.LogInformation(
                    "Supplier decision report cache STORE. ReportType={ReportType} KeyHash={CacheKeyHash} Version={ReportCacheVersion} SupplierId={SupplierId} StoreId={StoreId} DataScope={DataScope}",
                    "supplier-decision",
                    reportCacheKeyHash,
                    reportCacheVersion,
                    activeFilters.SupplierId,
                    activeFilters.StoreId,
                    activeFilters.DataScope);
            }

            return Results.Ok(report with
            {
                Meta = ApplyCorrelationId(report.Meta, correlationId)
            });
        }
        catch (SupplierDecisionUnavailableException ex)
        {
            return Results.Ok(BuildSupplierDecisionErrorReportResponse(activeFilters, ex.ErrorCode, ex.Message, correlationId));
        }
    }

    internal sealed record SupplierDecisionHubFilters(
        DateTime FromDate,
        DateTime ToDate,
        bool HasExplicitDateRange,
        string? Category,
        string? Gender,
        int? SeasonId,
        decimal? MinRevenue,
        bool OnlyHighConfidence,
        bool ExcludeOosBeforeMarkdown,
        int? SupplierId,
        int? StoreId,
        string DataScope);

    internal sealed record ReportRefreshInfo(
        DateTime? LastRefreshAtUtc,
        string? DataFreshnessStatus,
        string? WarningMessage);

    private sealed record SupplierDecisionHubDetailsCacheEntry(
        bool Found,
        SupplierDecisionDetailsResponse? Response);

    internal sealed record SupplierScoreRow(
        int SupplierId,
        string SupplierName,
        DateTime PeriodFrom,
        DateTime PeriodTo,
        decimal Revenue,
        decimal Units,
        decimal FullPriceRevenueShare,
        decimal FullPriceSellthrough,
        decimal MarkdownRevenueShare,
        decimal PreMarkdownMarginPct,
        decimal DeadStockRate,
        decimal UnsoldStockValue,
        decimal RepeatWinnerRate,
        decimal MarkdownDependencyScore,
        decimal StockRiskScore,
        decimal ReturnRate,
        decimal CategoryFocusScore,
        decimal MlSupplierScore,
        string AiExplanation,
        string TopFeature1,
        string TopFeature2,
        string TopFeature3,
        decimal SupplierQualityIndex,
        string RecommendationCode,
        decimal ConfidenceScore,
        bool SupplierNameMissing,
        decimal ReliabilityPct,
        string DataQualityStatus,
        string StatusReason,
        IReadOnlyList<string> ReasonCodes);

    internal sealed record SupplierRowsDataset(
        IReadOnlyList<SupplierScoreRow> Rows,
        int ZeroRevenueRowsExcludedCount,
        int IgnoredRowCount,
        DateTime GeneratedAtUtc);

    private static bool TryCreateFilters(
        DateTime? fromDate,
        DateTime? toDate,
        string? category,
        string? gender,
        int? seasonId,
        decimal? minRevenue,
        bool onlyHighConfidence,
        bool excludeOosBeforeMarkdown,
        int? supplierId,
        int? storeId,
        string? dataScope,
        out SupplierDecisionHubFilters? filters,
        out Dictionary<string, string[]>? validationError)
    {
        filters = null;
        validationError = null;

        var normalizedFrom = NormalizeDate(fromDate);
        var normalizedTo = NormalizeDate(toDate);
        var hasExplicitDateRange = normalizedFrom.HasValue || normalizedTo.HasValue;

        var toUtc = normalizedTo ?? DateTime.UtcNow.Date;
        var fromUtc = normalizedFrom
            ?? toUtc.AddDays(-DefaultLookbackDays);

        if (fromUtc > toUtc)
        {
            validationError = new Dictionary<string, string[]>
            {
                ["fromDate"] = ["fromDate must be earlier than or equal to toDate."]
            };
            return false;
        }

        if (minRevenue.HasValue && minRevenue.Value < 0)
        {
            validationError = new Dictionary<string, string[]>
            {
                ["minRevenue"] = ["minRevenue must be zero or positive."]
            };
            return false;
        }

        filters = new SupplierDecisionHubFilters(
            fromUtc,
            toUtc,
            hasExplicitDateRange,
            string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            string.IsNullOrWhiteSpace(gender) ? null : gender.Trim(),
            seasonId,
            minRevenue,
            onlyHighConfidence,
            excludeOosBeforeMarkdown,
            supplierId,
            storeId,
            NormalizeDataScope(dataScope));

        return true;
    }

    private static string NormalizeDataScope(string? value)
    {
        var normalized = (value ?? "all").Trim().ToLowerInvariant();
        return normalized is "all" or "existing" or "imported" ? normalized : "all";
    }

    private static DateTime? NormalizeDate(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        var normalized = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();

        return normalized.Date;
    }

    internal static SummaryResponse BuildSummaryResponse(
        SupplierRowsDataset dataset,
        SupplierDecisionHubFilters filters)
    {
        var rows = dataset.Rows;
        var from = rows.Count > 0 ? rows.Min(x => x.PeriodFrom) : filters.FromDate;
        var to = rows.Count > 0 ? rows.Max(x => x.PeriodTo) : filters.ToDate;
        var totalRevenue = rows.Sum(x => x.Revenue);
        var totalUnits = rows.Sum(x => x.Units);
        var fullPriceBase = rows.Sum(x => x.Revenue * x.FullPriceRevenueShare);

        var topGrow = rows
            .Where(x => x.RecommendationCode is "EXPAND" or "EXPAND_SELECTIVELY")
            .OrderByDescending(x => x.SupplierQualityIndex)
            .ThenByDescending(x => x.Revenue)
            .Take(5)
            .Select(MapSummarySupplier)
            .ToList();

        if (topGrow.Count == 0)
        {
            topGrow = rows
                .OrderByDescending(x => x.SupplierQualityIndex)
                .ThenByDescending(x => x.Revenue)
                .Take(5)
                .Select(MapSummarySupplier)
                .ToList();
        }

        var topRisk = rows
            .Where(x => x.RecommendationCode is "ASSORTMENT_REDUCE" or "PRICE_NEGOTIATE" or "REVIEW_QUALITY" or "OOS_FALSE_NEGATIVE")
            .OrderByDescending(x => x.StockRiskScore)
            .ThenByDescending(x => x.MarkdownDependencyScore)
            .Take(5)
            .Select(MapSummarySupplier)
            .ToList();

        if (topRisk.Count == 0)
        {
            topRisk = rows
                .OrderByDescending(x => x.MarkdownDependencyScore)
                .ThenByDescending(x => x.StockRiskScore)
                .Take(5)
                .Select(MapSummarySupplier)
                .ToList();
        }

        var bestGrow = topGrow.FirstOrDefault();
        var worstRisk = topRisk.FirstOrDefault();

        var insights = new List<KeyInsightItem>
        {
            new(
                "Kandidat za rast",
                bestGrow?.SupplierName ?? "Nema jasnog kandidata",
                bestGrow is null
                    ? "Trenutni skup filtera ne izdvaja dobavljača za sigurno širenje saradnje."
                    : $"Vodeći kandidat ima indeks kvaliteta {bestGrow.SupplierQualityIndex.ToString("0.##", CultureInfo.InvariantCulture)} i udeo prihoda bez sniženja {FormatPercent(rows.First(x => x.SupplierId == bestGrow.SupplierId).FullPriceRevenueShare)}.",
                bestGrow is null ? "neutral" : "positive"),
            new(
                "Zavisnost od sniÅ¾enja",
                totalRevenue <= 0
                    ? "0%"
                    : FormatPercent(rows.Sum(x => x.MarkdownRevenueShare * x.Revenue) / totalRevenue),
                "Signal je ponderisan prihodom, pa najveći dobavljači najviše utiču na ukupnu sliku.",
                totalRevenue > 0 && rows.Sum(x => x.MarkdownRevenueShare * x.Revenue) / totalRevenue >= 0.5m ? "warning" : "neutral"),
            new(
                "Kapital u riziku",
                rows.Sum(x => x.UnsoldStockValue).ToString("0.##", CultureInfo.InvariantCulture),
                worstRisk is null
                    ? "Nijedan dobavljač se trenutno ne izdvaja kao ekstreman stock-risk problem."
                    : $"Najveći vidljiv rizik trenutno dolazi od dobavljača {worstRisk.SupplierName}.",
                worstRisk is null ? "neutral" : "warning")
        };

        var dataNote = BuildDecisionScoreDataNote(filters);
        var trustMetadata = BuildScorecardTrustMetadata(dataset, filters);

        return new SummaryResponse(
            from,
            to,
            rows.Count,
            totalRevenue <= 0 ? 0 : Round4(rows.Sum(x => x.Revenue * x.FullPriceRevenueShare) / totalRevenue),
            totalUnits <= 0 ? 0 : Round4(rows.Sum(x => x.FullPriceSellthrough * x.Units) / totalUnits),
            totalRevenue <= 0 ? 0 : Round4(rows.Sum(x => x.MarkdownRevenueShare * x.Revenue) / totalRevenue),
            fullPriceBase <= 0 ? 0 : Round4(rows.Sum(x => x.PreMarkdownMarginPct * x.Revenue * x.FullPriceRevenueShare) / fullPriceBase),
            Round2(rows.Sum(x => x.UnsoldStockValue)),
            topGrow,
            topRisk,
            insights,
            dataNote,
            trustMetadata,
            BuildResponseMeta(rows, trustMetadata));
    }

    internal static AnalyticsReportResponseDto BuildSupplierDecisionReportResponse(
        SummaryResponse summary,
        SupplierRowsDataset dataset,
        SupplierDecisionHubFilters filters,
        ReportRefreshInfo? refreshInfo = null,
        SupplierDecisionDetailsResponse? details = null)
    {
        var trust = summary.TrustMetadata;
        var generatedAtUtc = DateTime.UtcNow;
        var reportId = BuildSupplierDecisionReportId(filters);
        var stableQueryUrl = BuildSupplierDecisionStableQueryUrl(filters);
        var period = new AnalyticsReportPeriodDto(
            filters.FromDate,
            filters.ToDate,
            BuildEffectivePeriodLabel(filters, ResolveEffectiveDataset(GetDecisionScoreWindowDays(filters))),
            trust?.RequestedDataset,
            trust?.EffectiveDataset,
            trust?.EffectivePeriodLabel,
            filters.DataScope);
        var methodology = BuildSupplierDecisionMethodology(filters, trust, details is not null);
        var warnings = BuildSupplierDecisionWarnings(summary.Meta, trust, refreshInfo);
        var hasData = dataset.Rows.Count > 0;
        var kpis = hasData
            ? BuildSupplierDecisionReportKpis(summary, dataset)
            : [];
        var actions = BuildSupplierDecisionReportActions(summary, filters, trust, details, hasData);
        var sections = BuildSupplierDecisionReportSections(summary, dataset, trust, refreshInfo, details, actions, methodology, hasData);
        var rows = BuildSupplierDecisionLegacyRows(summary, dataset, filters, trust, refreshInfo, kpis, actions, methodology.Summary, warnings, hasData);
        var payload = BuildSupplierDecisionPayload(reportId, generatedAtUtc, filters, period, trust, refreshInfo, methodology.Summary, rows);
        var meta = BuildSupplierDecisionReportMeta(summary.Meta, trust, refreshInfo);
        var dataQualityStatus = meta.DataQualityStatus ?? trust?.DataCoverageStatus ?? "insufficient_data";

        return new AnalyticsReportResponseDto(
            reportId,
            stableQueryUrl,
            "Trendplus izveštaj dobavljača",
            "supplier_decision",
            generatedAtUtc,
            summary.From,
            summary.To,
            period,
            refreshInfo?.LastRefreshAtUtc ?? trust?.LastRefreshAtUtc,
            refreshInfo?.DataFreshnessStatus,
            dataQualityStatus,
            trust?.RecommendationAllowed ?? false,
            trust?.UsedFallback ?? false,
            trust?.FallbackReason,
            warnings,
            kpis,
            sections,
            actions,
            methodology,
            rows,
            payload,
            ReportTitle: "Trendplus izveštaj dobavljača",
            ReportType: "supplier-decision",
            MethodologySummary: methodology.Summary,
            Meta: meta);
    }

    internal static AnalyticsReportResponseDto BuildSupplierDecisionErrorReportResponse(
        SupplierDecisionHubFilters filters,
        string errorCode,
        string message,
        string correlationId)
    {
        var generatedAtUtc = DateTime.UtcNow;
        var methodology = BuildSupplierDecisionMethodology(filters, null, false);
        var period = new AnalyticsReportPeriodDto(
            filters.FromDate,
            filters.ToDate,
            BuildEffectivePeriodLabel(filters, ResolveRequestedDataset(filters)),
            ResolveRequestedDataset(filters),
            ResolveRequestedDataset(filters),
            BuildEffectivePeriodLabel(filters, ResolveRequestedDataset(filters)),
            filters.DataScope);
        var rows = new List<AnalyticsLegacyReportRowDto>
        {
            new("Status", "Greška", message, errorCode, null),
            new("Metodologija", "Opis", methodology.Summary, null, "Kako čitati ovaj izveštaj: /analytics/data-quality")
        };

        return new AnalyticsReportResponseDto(
            BuildSupplierDecisionReportId(filters),
            BuildSupplierDecisionStableQueryUrl(filters),
            "Trendplus izveštaj dobavljača",
            "supplier_decision",
            generatedAtUtc,
            filters.FromDate,
            filters.ToDate,
            period,
            null,
            null,
            "insufficient_data",
            false,
            false,
            null,
            [],
            [],
            [
                new AnalyticsReportSectionDto(
                    "report-status",
                    "Status reporta",
                    "Supplier decision report trenutno nije dostupan.",
                    [
                        new AnalyticsReportColumnDto("status", "Status"),
                        new AnalyticsReportColumnDto("message", "Poruka"),
                        new AnalyticsReportColumnDto("errorCode", "Kod")
                    ],
                    [
                        new Dictionary<string, object?>
                        {
                            ["status"] = "greška",
                            ["message"] = message,
                            ["errorCode"] = errorCode
                        }
                    ],
                    1,
                    null)
            ],
            [],
            methodology,
            rows,
            new AnalyticsResolvedReportPayloadDto(
                "supplier-decision-report",
                "Trendplus izveštaj dobavljača",
                [],
                [],
                [],
                [],
                "sr-RS",
                "supplier-decision",
                "analytics-table-default",
                1),
            ReportTitle: "Trendplus izveštaj dobavljača",
            ReportType: "supplier-decision",
            MethodologySummary: methodology.Summary,
            Meta: BuildErrorMeta(errorCode, message, correlationId));
    }

    private static string BuildSupplierDecisionReportId(SupplierDecisionHubFilters filters)
    {
        return $"sdr-{filters.FromDate:yyyyMMdd}-{filters.ToDate:yyyyMMdd}-{filters.SupplierId?.ToString(CultureInfo.InvariantCulture) ?? "all"}-{filters.StoreId?.ToString(CultureInfo.InvariantCulture) ?? "all"}-{filters.DataScope}";
    }

    private static string BuildSupplierDecisionStableQueryUrl(SupplierDecisionHubFilters filters)
    {
        var query = new List<string>
        {
            $"fromDate={Uri.EscapeDataString(filters.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}",
            $"toDate={Uri.EscapeDataString(filters.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}",
            $"scope={Uri.EscapeDataString(filters.DataScope)}"
        };

        if (filters.SupplierId.HasValue)
        {
            query.Add($"supplierId={filters.SupplierId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (filters.StoreId.HasValue)
        {
            query.Add($"storeId={filters.StoreId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        return $"/analytics/supplier/report?{string.Join("&", query)}";
    }

    private static async Task<SupplierDecisionDetailsResponse?> BuildSupplierDecisionReportDetailsAsync(
        string analyticsConnectionString,
        SupplierDecisionHubFilters filters,
        SupplierRowsDataset dataset,
        CancellationToken ct)
    {
        if (!filters.SupplierId.HasValue)
        {
            return null;
        }

        var supplier = dataset.Rows.FirstOrDefault();
        return supplier is null
            ? null
            : await BuildDetailsResponseAsync(analyticsConnectionString, filters, supplier, ct);
    }

    private static ReportRefreshInfo? ResolveReportRefreshInfo(
        AnalyticsRefreshStatusDto refreshStatus,
        string preferredJobKey)
    {
        var job = refreshStatus.Jobs.FirstOrDefault(x => string.Equals(x.Key, preferredJobKey, StringComparison.OrdinalIgnoreCase));
        var freshnessStatus = job?.DataFreshnessStatus ?? refreshStatus.DataFreshnessStatus;
        var warningMessage = freshnessStatus is "stale" or "critical"
            ? job?.StatusReason ?? refreshStatus.WorkerWarning ?? "Analytics refresh može biti zastareo."
            : null;

        return new ReportRefreshInfo(
            job?.LastSuccessfulRefreshAtUtc ?? refreshStatus.LastSuccessfulRefreshAtUtc,
            freshnessStatus,
            warningMessage);
    }

    private static AnalyticsReportMethodologyDto BuildSupplierDecisionMethodology(
        SupplierDecisionHubFilters filters,
        ScorecardTrustMetadata? trust,
        bool includesArticleDetails)
    {
        var notes = new List<string>
        {
            "Preporuka kombinuje promet, maržni doprinos, zavisnost od sniženja, rizik zaliha i pouzdanost signala.",
            "Frontend prikazuje backend signal i ne uvodi lokalne threshold-e za recommendation status.",
            BuildDecisionScoreDataNote(filters) ?? "Za traženi period koristi se kanonski supplier decision dataset bez tihog proširenja opsega."
        };

        if (trust?.UsedFallback == true && !string.IsNullOrWhiteSpace(trust.FallbackReason))
        {
            notes.Add(trust.FallbackReason!);
        }

        if (includesArticleDetails)
        {
            notes.Add("Sekcija artikala sa rizikom koristi supplier details upit za izabranog dobavljača.");
        }

        return new AnalyticsReportMethodologyDto(
            "Preporuka kombinuje promet, maržni doprinos, zavisnost od sniženja, rizik zaliha i pouzdanost signala.",
            notes,
            ["/analytics/data-quality", "/admin/configuration?panel=workers"]);
    }

    private static IReadOnlyList<AnalyticsReportKpiDto> BuildSupplierDecisionReportKpis(
        SummaryResponse summary,
        SupplierRowsDataset dataset)
    {
        var totalRevenue = dataset.Rows.Sum(x => x.Revenue);
        var totalUnits = dataset.Rows.Sum(x => x.Units);
        var marginContribution = dataset.Rows.Sum(x => x.Revenue * x.PreMarkdownMarginPct * x.FullPriceRevenueShare);
        var avgConfidence = dataset.Rows.Count == 0 ? 0m : Round2(dataset.Rows.Average(x => x.ConfidenceScore));
        var avgReliability = dataset.Rows.Count == 0 ? 0m : Round2(dataset.Rows.Average(x => x.ReliabilityPct));

        return new List<AnalyticsReportKpiDto>
        {
            new("revenue", "Prihod", Round2(totalRevenue), "RSD", totalRevenue > 0 ? "neutral" : "warning", "Ukupan prihod za traženi filter skup."),
            new("marginContribution", "Maržni doprinos", Round2(marginContribution), "RSD", marginContribution >= 0 ? "positive" : "warning", "Procena doprinosa na osnovu full-price prihoda i pre-markdown marže."),
            new("units", "Prodate jedinice", Round2(totalUnits), "kom", totalUnits > 0 ? "neutral" : "warning", null),
            new("supplierCount", "Broj dobavljača", summary.SupplierCount, null, summary.SupplierCount >= 3 ? "positive" : "warning", null),
            new("capitalAtRisk", "Kapital u riziku", summary.CapitalAtRisk, "RSD", summary.CapitalAtRisk > 0 ? "warning" : "positive", "Vrednost neprodate robe koja trenutno nosi najveći stock-risk signal."),
            new("avgConfidence", "Pouzdanost signala", avgConfidence, "%", avgConfidence >= 70 ? "positive" : "warning", null),
            new("avgReliability", "Pouzdanost preporuke", avgReliability, "%", avgReliability >= 70 ? "positive" : "warning", null)
        };
    }

    private static IReadOnlyList<AnalyticsReportActionDto> BuildSupplierDecisionReportActions(
        SummaryResponse summary,
        SupplierDecisionHubFilters filters,
        ScorecardTrustMetadata? trust,
        SupplierDecisionDetailsResponse? details,
        bool hasData)
    {
        var actions = new List<AnalyticsReportActionDto>();

        if (!hasData || trust is { RecommendationAllowed: false })
        {
            actions.Add(new AnalyticsReportActionDto(
                "Proveri kvalitet podataka",
                "Pre finalne preporuke proveri razlog ograničenja i stanje data quality signala.",
                "/analytics/data-quality",
                "high"));
        }

        if (trust?.UsedFallback == true)
        {
            actions.Add(new AnalyticsReportActionDto(
                "Proveri status osvežavanja",
                "Report koristi pomoćni dataset; proveri da li su worker refresh-evi ažurni.",
                "/admin/configuration?panel=workers",
                "high"));
        }

        var topGrow = summary.TopGrowSuppliers.FirstOrDefault();
        if (topGrow is not null)
        {
            actions.Add(new AnalyticsReportActionDto(
                $"Pregledaj rast za {topGrow.SupplierName}",
                "Otvori trajni supplier report za vodećeg kandidata za rast.",
                BuildSupplierDecisionStableQueryUrl(filters with { SupplierId = topGrow.SupplierId }),
                "medium"));
        }

        var topRisk = summary.TopRiskSuppliers.FirstOrDefault();
        if (topRisk is not null)
        {
            actions.Add(new AnalyticsReportActionDto(
                $"Smanji rizik za {topRisk.SupplierName}",
                "Fokusiraj se na markdown dependency i stock-risk signal za najrizičnijeg dobavljača.",
                BuildSupplierDecisionStableQueryUrl(filters with { SupplierId = topRisk.SupplierId }),
                "medium"));
        }

        if (details is { BlockedByOosArticles.Count: > 0 })
        {
            actions.Add(new AnalyticsReportActionDto(
                "Istraži OOS false negative artikle",
                "Pregledaj artikle koji nose OOS false negative signal za izabranog dobavljača.",
                BuildSupplierDecisionStableQueryUrl(filters),
                "medium"));
        }

        return actions
            .GroupBy(action => action.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static IReadOnlyList<AnalyticsReportSectionDto> BuildSupplierDecisionReportSections(
        SummaryResponse summary,
        SupplierRowsDataset dataset,
        ScorecardTrustMetadata? trust,
        ReportRefreshInfo? refreshInfo,
        SupplierDecisionDetailsResponse? details,
        IReadOnlyList<AnalyticsReportActionDto> actions,
        AnalyticsReportMethodologyDto methodology,
        bool hasData)
    {
        var sections = new List<AnalyticsReportSectionDto>();

        if (!hasData)
        {
            sections.Add(new AnalyticsReportSectionDto(
                "report-status",
                "Status reporta",
                "Nedovoljno podataka za supplier report u traženom periodu.",
                [
                    new AnalyticsReportColumnDto("status", "Status"),
                    new AnalyticsReportColumnDto("message", "Poruka"),
                    new AnalyticsReportColumnDto("requestedDataset", "Requested dataset"),
                    new AnalyticsReportColumnDto("effectiveDataset", "Effective dataset")
                ],
                [
                    new Dictionary<string, object?>
                    {
                        ["status"] = "insufficient_data",
                        ["message"] = summary.Meta?.Message ?? "Nema dovoljno podataka za Supplier scorecard u izabranom periodu.",
                        ["requestedDataset"] = trust?.RequestedDataset,
                        ["effectiveDataset"] = trust?.EffectiveDataset
                    }
                ],
                1,
                null));
        }
        else
        {
            sections.Add(new AnalyticsReportSectionDto(
                "executive-summary",
                "Izvršni sažetak",
                "Najvažniji signal i kontekst za traženi period.",
                [
                    new AnalyticsReportColumnDto("metric", "Metrika"),
                    new AnalyticsReportColumnDto("value", "Vrednost"),
                    new AnalyticsReportColumnDto("details", "Detalji"),
                    new AnalyticsReportColumnDto("tone", "Ton")
                ],
                summary.KeyInsights.Select(insight => new Dictionary<string, object?>
                {
                    ["metric"] = insight.Title,
                    ["value"] = insight.Value,
                    ["details"] = insight.Details,
                    ["tone"] = insight.Tone
                }).ToList(),
                summary.KeyInsights.Count,
                summary.KeyInsights.Count == 0 ? "Nema dostupnih signala za sažetak." : null));

            var selectedSupplier = dataset.Rows.FirstOrDefault();
            var supplierRows = (details is not null && selectedSupplier is not null)
                ? new List<SummarySupplierItem>
                {
                    new(
                        selectedSupplier.SupplierId,
                        selectedSupplier.SupplierName,
                        selectedSupplier.Revenue,
                        selectedSupplier.MlSupplierScore,
                        selectedSupplier.SupplierQualityIndex,
                        selectedSupplier.RecommendationCode,
                        selectedSupplier.ConfidenceScore,
                        selectedSupplier.ReliabilityPct,
                        selectedSupplier.DataQualityStatus,
                        selectedSupplier.StatusReason,
                        selectedSupplier.ReasonCodes)
                }
                : summary.TopGrowSuppliers;

            sections.Add(new AnalyticsReportSectionDto(
                details is not null ? "selected-supplier" : "top-suppliers",
                details is not null ? "Izabrani dobavljač" : "Top dobavljači",
                details is not null ? "Finalni signal za dobavljača traženog kroz URL parametre." : "Dobavljači sa najboljim kvalitetom signala u traženom periodu.",
                [
                    new AnalyticsReportColumnDto("supplierName", "Dobavljač"),
                    new AnalyticsReportColumnDto("recommendation", "Preporuka"),
                    new AnalyticsReportColumnDto("revenue", "Prihod", "currency"),
                    new AnalyticsReportColumnDto("confidencePct", "Pouzdanost signala", "percent"),
                    new AnalyticsReportColumnDto("reliabilityPct", "Pouzdanost preporuke", "percent"),
                    new AnalyticsReportColumnDto("dataQualityStatus", "Kvalitet podataka"),
                    new AnalyticsReportColumnDto("reason", "Zašto")
                ],
                supplierRows.Select(supplier => new Dictionary<string, object?>
                {
                    ["supplierName"] = supplier.SupplierName,
                    ["recommendation"] = supplier.RecommendationCode,
                    ["revenue"] = Round2(supplier.Revenue),
                    ["confidencePct"] = Round2(supplier.ConfidenceScore),
                    ["reliabilityPct"] = Round2(supplier.ReliabilityPct),
                    ["dataQualityStatus"] = supplier.DataQualityStatus,
                    ["reason"] = supplier.StatusReason,
                    ["reasonCodes"] = supplier.ReasonCodes
                }).ToList(),
                supplierRows.Count,
                supplierRows.Count == 0 ? "Nema kandidata za rast u traženom periodu." : null));

            var riskRows = details is not null
                ? details.MarkdownDependentArticles
                    .Take(4)
                    .Concat(details.BlockedByOosArticles.Take(4))
                    .Select(article => new Dictionary<string, object?>
                    {
                        ["articleName"] = article.ArticleName,
                        ["sku"] = article.Sku,
                        ["category"] = article.Category,
                        ["issue"] = article.StockoutBeforeMarkdownFlag ? "OOS false negative" : "Markdown dependency",
                        ["preRevenue30d"] = Round2(article.PreRevenue30d),
                        ["postRevenue30d"] = Round2(article.PostRevenue30d),
                        ["signalQuality"] = article.SignalQualityFlag,
                        ["signalReason"] = article.SignalQualityReason
                    })
                    .ToList()
                : summary.TopRiskSuppliers
                    .Select(supplier => new Dictionary<string, object?>
                    {
                        ["supplierName"] = supplier.SupplierName,
                        ["recommendation"] = supplier.RecommendationCode,
                        ["revenue"] = Round2(supplier.Revenue),
                        ["confidencePct"] = Round2(supplier.ConfidenceScore),
                        ["reliabilityPct"] = Round2(supplier.ReliabilityPct),
                        ["reason"] = supplier.StatusReason
                    })
                    .ToList();

            sections.Add(new AnalyticsReportSectionDto(
                details is not null ? "risk-items" : "risk-suppliers",
                details is not null ? "Artikli sa rizikom" : "Dobavljači u riziku",
                details is not null ? "Najizraženiji artikli sa markdown ili OOS problemom." : "Dobavljači sa najvećim stock-risk ili markdown dependency signalom.",
                details is not null
                    ? [
                        new AnalyticsReportColumnDto("articleName", "Artikal"),
                        new AnalyticsReportColumnDto("sku", "SKU"),
                        new AnalyticsReportColumnDto("category", "Kategorija"),
                        new AnalyticsReportColumnDto("issue", "Problem"),
                        new AnalyticsReportColumnDto("preRevenue30d", "Prihod pre markdowna", "currency"),
                        new AnalyticsReportColumnDto("postRevenue30d", "Prihod posle markdowna", "currency"),
                        new AnalyticsReportColumnDto("signalQuality", "Kvalitet signala"),
                        new AnalyticsReportColumnDto("signalReason", "Napomena")
                    ]
                    : [
                        new AnalyticsReportColumnDto("supplierName", "Dobavljač"),
                        new AnalyticsReportColumnDto("recommendation", "Preporuka"),
                        new AnalyticsReportColumnDto("revenue", "Prihod", "currency"),
                        new AnalyticsReportColumnDto("confidencePct", "Pouzdanost signala", "percent"),
                        new AnalyticsReportColumnDto("reliabilityPct", "Pouzdanost preporuke", "percent"),
                        new AnalyticsReportColumnDto("reason", "Zašto")
                    ],
                riskRows,
                riskRows.Count,
                riskRows.Count == 0 ? "Nema identifikovanih stavki sa rizikom za traženi opseg." : null));
        }

        sections.Add(new AnalyticsReportSectionDto(
            "data-quality",
            "Kvalitet podataka",
            "Trust metadata, fallback status i pokrivenost dataseta.",
            [
                new AnalyticsReportColumnDto("metric", "Metrika"),
                new AnalyticsReportColumnDto("value", "Vrednost"),
                new AnalyticsReportColumnDto("note", "Napomena")
            ],
            new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["metric"] = "Requested dataset",
                    ["value"] = trust?.RequestedDataset ?? "n/a",
                    ["note"] = trust?.EffectivePeriodLabel
                },
                new()
                {
                    ["metric"] = "Effective dataset",
                    ["value"] = trust?.EffectiveDataset ?? "n/a",
                    ["note"] = trust?.FallbackReason
                },
                new()
                {
                    ["metric"] = "Coverage status",
                    ["value"] = trust?.DataCoverageStatus ?? summary.Meta?.DataQualityStatus ?? "insufficient_data",
                    ["note"] = refreshInfo?.DataFreshnessStatus
                },
                new()
                {
                    ["metric"] = "Rows",
                    ["value"] = trust?.RowCount ?? dataset.Rows.Count,
                    ["note"] = $"Ignored: {trust?.IgnoredRowCount ?? dataset.IgnoredRowCount}; zero revenue excluded: {trust?.ZeroRevenueRowsExcludedCount ?? dataset.ZeroRevenueRowsExcludedCount}"
                },
                new()
                {
                    ["metric"] = "Recommendation allowed",
                    ["value"] = trust?.RecommendationAllowed ?? false,
                    ["note"] = trust?.UsedFallback == true ? "Pomoćni signal" : "Finalna preporuka dozvoljena"
                }
            },
            5,
            null));

        sections.Add(new AnalyticsReportSectionDto(
            "recommended-actions",
            "Preporučene akcije",
            "Sledeći koraci za operativni tim.",
            [
                new AnalyticsReportColumnDto("priority", "Prioritet"),
                new AnalyticsReportColumnDto("title", "Akcija"),
                new AnalyticsReportColumnDto("description", "Opis"),
                new AnalyticsReportColumnDto("href", "Link")
            ],
            actions.Select(action => new Dictionary<string, object?>
            {
                ["priority"] = action.Priority,
                ["title"] = action.Title,
                ["description"] = action.Description,
                ["href"] = action.Href
            }).ToList(),
            actions.Count,
            actions.Count == 0 ? "Nema preporučenih akcija za traženi opseg." : null));

        sections.Add(new AnalyticsReportSectionDto(
            "methodology",
            "Metodologija",
            "Kako čitati KPI-jeve, warning stanja i pomoćne signale.",
            [
                new AnalyticsReportColumnDto("topic", "Tema"),
                new AnalyticsReportColumnDto("details", "Objašnjenje")
            ],
            methodology.Notes.Select((note, index) => new Dictionary<string, object?>
            {
                ["topic"] = index == 0 ? "Sažetak" : $"Napomena {index}",
                ["details"] = note
            }).ToList(),
            methodology.Notes.Count,
            null));

        return sections;
    }

    private static List<AnalyticsLegacyReportRowDto> BuildSupplierDecisionLegacyRows(
        SummaryResponse summary,
        SupplierRowsDataset dataset,
        SupplierDecisionHubFilters filters,
        ScorecardTrustMetadata? trust,
        ReportRefreshInfo? refreshInfo,
        IReadOnlyList<AnalyticsReportKpiDto> kpis,
        IReadOnlyList<AnalyticsReportActionDto> actions,
        string methodologySummary,
        IReadOnlyList<string> warnings,
        bool hasData)
    {
        var rows = new List<AnalyticsLegacyReportRowDto>
        {
            new("Header", "Naziv izveštaja", "Trendplus izveštaj dobavljača"),
            new("Header", "Period", $"{summary.From:yyyy-MM-dd} - {summary.To:yyyy-MM-dd}", trust?.EffectivePeriodLabel, null),
            new("Header", "Kvalitet podataka", summary.Meta?.DataQualityStatus ?? trust?.DataCoverageStatus ?? "insufficient_data", refreshInfo?.DataFreshnessStatus, trust?.FallbackReason),
            new("Header", "Preporuka dozvoljena", trust?.RecommendationAllowed == true ? "Da" : "Ne", trust?.EffectiveDataset, trust?.UsedFallback == true ? "Pomoćni signal" : null)
        };

        if (hasData)
        {
            foreach (var kpi in kpis)
            {
                rows.Add(new AnalyticsLegacyReportRowDto("KPI", kpi.Label, FormatReportValue(kpi.Value), kpi.Unit, kpi.Note));
            }

            foreach (var supplier in summary.TopGrowSuppliers.Take(5))
            {
                rows.Add(new AnalyticsLegacyReportRowDto(
                    "Top dobavljači",
                    supplier.SupplierName,
                    supplier.Revenue.ToString("0.##", CultureInfo.InvariantCulture),
                    $"Signal: {supplier.RecommendationCode}",
                    supplier.StatusReason));
            }

            foreach (var supplier in summary.TopRiskSuppliers.Take(5))
            {
                rows.Add(new AnalyticsLegacyReportRowDto(
                    "Rizik",
                    supplier.SupplierName,
                    supplier.Revenue.ToString("0.##", CultureInfo.InvariantCulture),
                    $"Signal: {supplier.RecommendationCode}",
                    supplier.StatusReason));
            }
        }
        else
        {
            rows.Add(new AnalyticsLegacyReportRowDto(
                "Status",
                "Nedovoljno podataka",
                summary.Meta?.Message ?? "Nema dovoljno podataka za supplier report u izabranom periodu.",
                filters.DataScope,
                trust?.FallbackReason));
        }

        foreach (var warning in warnings)
        {
            rows.Add(new AnalyticsLegacyReportRowDto("Upozorenja", "Upozorenje", warning));
        }

        foreach (var action in actions)
        {
            rows.Add(new AnalyticsLegacyReportRowDto("Preporučene akcije", action.Title, action.Description, action.Priority, action.Href));
        }

        rows.Add(new AnalyticsLegacyReportRowDto("Metodologija", "Opis", methodologySummary, null, "Kako čitati ovaj izveštaj: /analytics/data-quality"));
        return rows;
    }

    private static AnalyticsResolvedReportPayloadDto BuildSupplierDecisionPayload(
        string reportId,
        DateTime generatedAtUtc,
        SupplierDecisionHubFilters filters,
        AnalyticsReportPeriodDto period,
        ScorecardTrustMetadata? trust,
        ReportRefreshInfo? refreshInfo,
        string methodologySummary,
        IReadOnlyList<AnalyticsLegacyReportRowDto> rows)
    {
        var filterValues = new List<AnalyticsReportNamedValueDto>
        {
            new("period", "Period", $"{period.FromUtc:yyyy-MM-dd} - {period.ToUtc:yyyy-MM-dd}"),
            new("dataScope", "Opseg podataka", filters.DataScope),
            new("supplier", "Dobavljač", filters.SupplierId?.ToString(CultureInfo.InvariantCulture) ?? "all"),
            new("store", "Objekat", filters.StoreId?.ToString(CultureInfo.InvariantCulture) ?? "all")
        };

        return new AnalyticsResolvedReportPayloadDto(
            "supplier-decision-report",
            "Trendplus izveštaj dobavljača",
            new List<AnalyticsReportPayloadColumnDto>
            {
                new("section", "Sekcija", "text"),
                new("item", "Stavka", "text"),
                new("value", "Vrednost", "text"),
                new("secondary", "Kontekst", "text"),
                new("note", "Napomena", "text")
            },
            rows.Select(row => new AnalyticsReportPayloadRowDto(row.Section, row.Item, row.Value, row.Secondary, row.Note)).ToList(),
            filterValues,
            new List<AnalyticsReportNamedValueDto>
            {
                new("reportId", "Report ID", reportId),
                new("generatedAtUtc", "Generisano", generatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
                new("lastRefreshAtUtc", "Poslednje osveženje", (refreshInfo?.LastRefreshAtUtc ?? trust?.LastRefreshAtUtc)?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty),
                new("dataFreshnessStatus", "Svežina podataka", refreshInfo?.DataFreshnessStatus ?? string.Empty),
                new("dataQualityStatus", "Kvalitet podataka", trust?.DataCoverageStatus ?? "insufficient_data"),
                new("usedFallback", "Korišćen fallback", (trust?.UsedFallback ?? false).ToString()),
                new("methodology", "Metodologija", methodologySummary)
            },
            "sr-RS",
            "supplier-decision",
            "analytics-table-default",
            1);
    }

    private static AnalyticsResponseMetaDto BuildSupplierDecisionReportMeta(
        AnalyticsResponseMetaDto? meta,
        ScorecardTrustMetadata? trust,
        ReportRefreshInfo? refreshInfo)
    {
        var resolved = CloneMeta(meta ?? BuildResponseMeta(Array.Empty<SupplierScoreRow>(), trust));
        resolved.LastRefreshAtUtc = refreshInfo?.LastRefreshAtUtc ?? resolved.LastRefreshAtUtc ?? trust?.LastRefreshAtUtc;

        if (refreshInfo is { DataFreshnessStatus: "stale" or "critical" } && string.IsNullOrWhiteSpace(resolved.WarningCode))
        {
            resolved.IsPartial = true;
            resolved.WarningCode = "STALE_REFRESH";
            resolved.WarningMessage = refreshInfo.WarningMessage ?? "Analytics refresh može biti zastareo.";
            resolved.Message ??= resolved.WarningMessage;
            if (string.IsNullOrWhiteSpace(resolved.DataQualityStatus) || string.Equals(resolved.DataQualityStatus, "good", StringComparison.OrdinalIgnoreCase))
            {
                resolved.DataQualityStatus = "warning";
            }
        }

        return resolved;
    }

    private static AnalyticsResponseMetaDto CloneMeta(AnalyticsResponseMetaDto source)
    {
        return new AnalyticsResponseMetaDto
        {
            Success = source.Success,
            Message = source.Message,
            ErrorCode = source.ErrorCode,
            ErrorMessage = source.ErrorMessage,
            WarningCode = source.WarningCode,
            WarningMessage = source.WarningMessage,
            DataQualityStatus = source.DataQualityStatus,
            EmptyReason = source.EmptyReason,
            IsPartial = source.IsPartial,
            GeneratedAtUtc = source.GeneratedAtUtc,
            LastRefreshAtUtc = source.LastRefreshAtUtc,
            CorrelationId = source.CorrelationId
        };
    }

    private static string FormatReportValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            decimal decimalValue => decimalValue.ToString("0.##", CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("0.##", CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("0.##", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static IReadOnlyList<string> BuildSupplierDecisionWarnings(
        AnalyticsResponseMetaDto? meta,
        ScorecardTrustMetadata? trust,
        ReportRefreshInfo? refreshInfo = null)
    {
        var warnings = new List<string>();

        if (trust?.UsedFallback == true)
        {
            var fallbackLabel = string.IsNullOrWhiteSpace(trust.EffectivePeriodLabel) ? "pomoćni dataset" : trust.EffectivePeriodLabel;
            warnings.Add($"Korišćen je pomoćni dataset: {fallbackLabel}.");
        }

        if (trust is { RecommendationAllowed: false })
        {
            warnings.Add("Kvalitet podataka ograničava pouzdanost reporta.");
        }

        if (!string.IsNullOrWhiteSpace(meta?.WarningMessage))
        {
            warnings.Add(meta.WarningMessage!);
        }

        if (!string.IsNullOrWhiteSpace(refreshInfo?.WarningMessage))
        {
            warnings.Add(refreshInfo!.WarningMessage!);
        }

        return warnings
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static AnalyticsResponseMetaDto BuildResponseMeta(
        IReadOnlyCollection<SupplierScoreRow> rows,
        ScorecardTrustMetadata? trustMetadata = null)
    {
        var fallbackWarningMessage = trustMetadata?.UsedFallback == true
            ? $"Za izabrani period nema dovoljno podataka. Koriscen je dataset {trustMetadata.EffectivePeriodLabel} kao pomocni signal."
            : null;

        if (rows.Count == 0)
        {
            return new AnalyticsResponseMetaDto
            {
                Success = true,
                EmptyReason = "no_data_in_period",
                Message = "Nema dovoljno podataka za Supplier scorecard u izabranom periodu.",
                DataQualityStatus = trustMetadata?.DataCoverageStatus ?? "insufficient_data",
                IsPartial = trustMetadata?.UsedFallback == true,
                WarningCode = trustMetadata?.UsedFallback == true ? "FALLBACK_DATASET_USED" : null,
                WarningMessage = fallbackWarningMessage,
                LastRefreshAtUtc = trustMetadata?.LastRefreshAtUtc,
                GeneratedAtUtc = DateTime.UtcNow
            };
        }

        var recommendationGated = trustMetadata is { RecommendationAllowed: false };
        var warningCode = trustMetadata?.UsedFallback == true
            ? "FALLBACK_DATASET_USED"
            : (recommendationGated ? "RECOMMENDATION_GATED" : null);
        var warningMessage = trustMetadata?.UsedFallback == true
            ? fallbackWarningMessage
            : (recommendationGated ? "Preporuka je onemogucena zbog nedovoljne pouzdanosti podataka." : null);

        return new AnalyticsResponseMetaDto
        {
            Success = true,
            DataQualityStatus = trustMetadata?.DataCoverageStatus ?? "good",
            IsPartial = trustMetadata?.UsedFallback == true || recommendationGated,
            WarningCode = warningCode,
            WarningMessage = warningMessage,
            LastRefreshAtUtc = trustMetadata?.LastRefreshAtUtc,
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static AnalyticsResponseMetaDto BuildErrorMeta(string errorCode, string message, string correlationId)
    {
        return new AnalyticsResponseMetaDto
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
            Message = message,
            CorrelationId = correlationId,
            DataQualityStatus = "insufficient_data",
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static AnalyticsResponseMetaDto ApplyCorrelationId(AnalyticsResponseMetaDto? meta, string correlationId)
    {
        var resolved = meta is null
            ? new AnalyticsResponseMetaDto
            {
                Success = true,
                GeneratedAtUtc = DateTime.UtcNow
            }
            : CloneMeta(meta);

        resolved.CorrelationId = correlationId;
        return resolved;
    }

    private static SummarySupplierItem MapSummarySupplier(SupplierScoreRow row) =>
        new(
            row.SupplierId,
            row.SupplierName,
            row.Revenue,
            row.MlSupplierScore,
            row.SupplierQualityIndex,
            row.RecommendationCode,
            row.ConfidenceScore,
            row.ReliabilityPct,
            row.DataQualityStatus,
            row.StatusReason,
            row.ReasonCodes);

    private static IOrderedEnumerable<SupplierScoreRow> ApplyRankingSort(
        IEnumerable<SupplierScoreRow> rows,
        string? sortBy,
        string? sortDir)
    {
        var normalizedSort = NormalizeRankingSortBy(sortBy);
        var desc = NormalizeRankingSortDir(sortDir) == "desc";

        Func<SupplierScoreRow, object> selector = normalizedSort switch
        {
            "supplierName" => x => x.SupplierName,
            "revenue" => x => x.Revenue,
            "units" => x => x.Units,
            "fullPriceRevenueShare" => x => x.FullPriceRevenueShare,
            "fullPriceSellthrough" => x => x.FullPriceSellthrough,
            "preMarkdownMarginPct" => x => x.PreMarkdownMarginPct,
            "markdownRevenueShare" => x => x.MarkdownRevenueShare,
            "deadStockRate" => x => x.DeadStockRate,
            "unsoldStockValue" => x => x.UnsoldStockValue,
            "repeatWinnerRate" => x => x.RepeatWinnerRate,
            "mlSupplierScore" => x => x.MlSupplierScore,
            "confidenceScore" => x => x.ConfidenceScore,
            _ => x => x.SupplierQualityIndex
        };

        return desc
            ? rows.OrderByDescending(selector).ThenBy(x => x.SupplierName)
            : rows.OrderBy(selector).ThenBy(x => x.SupplierName);
    }

    private static string NormalizeRankingSortBy(string? sortBy)
    {
        var normalizedSort = string.IsNullOrWhiteSpace(sortBy)
            ? "supplierQualityIndex"
            : sortBy.Trim();

        return normalizedSort switch
        {
            "supplierName" => "supplierName",
            "revenue" => "revenue",
            "units" => "units",
            "fullPriceRevenueShare" => "fullPriceRevenueShare",
            "fullPriceSellthrough" => "fullPriceSellthrough",
            "preMarkdownMarginPct" => "preMarkdownMarginPct",
            "markdownRevenueShare" => "markdownRevenueShare",
            "deadStockRate" => "deadStockRate",
            "unsoldStockValue" => "unsoldStockValue",
            "repeatWinnerRate" => "repeatWinnerRate",
            "mlSupplierScore" => "mlSupplierScore",
            "confidenceScore" => "confidenceScore",
            "supplierQualityIndex" => "supplierQualityIndex",
            _ => "supplierQualityIndex"
        };
    }

    private static string NormalizeRankingSortDir(string? sortDir) =>
        string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";

    private static Task<SupplierRowsDataset> GetSupplierRowsCachedAsync(
        IAnalyticsCacheService cache,
        string analyticsConnectionString,
        SupplierDecisionHubFilters filters,
        CancellationToken ct)
    {
        var cacheKey = AnalyticsCacheKeys.SupplierDecisionHubDataset(
            filters.FromDate,
            filters.ToDate,
            filters.Category,
            filters.Gender,
            filters.SeasonId,
            filters.MinRevenue,
            filters.OnlyHighConfidence,
            filters.ExcludeOosBeforeMarkdown,
            filters.SupplierId,
            filters.StoreId,
            filters.DataScope);

        return cache.GetOrSetAsync(
            cacheKey,
            () => QuerySupplierRowsAsync(analyticsConnectionString, filters, ct),
            CacheExpiration.HeavyAnalytics,
            ct);
    }

    private static string FormatPercent(decimal value) =>
        $"{(value * 100m).ToString("0.##", CultureInfo.InvariantCulture)}%";

    private static decimal Round2(decimal value) => decimal.Round(value, 2);
    private static decimal Round4(decimal value) => decimal.Round(value, 4);

    private static string NormalizeSupplierName(int supplierId, string supplierName)
    {
        if (!string.IsNullOrWhiteSpace(supplierName))
        {
            return supplierName.Trim();
        }

        return supplierId > 0
            ? $"DobavljaÄ #{supplierId.ToString(CultureInfo.InvariantCulture)}"
            : "Nepoznat dobavljaÄ";
    }

    private static async Task<SupplierRowsDataset> QuerySupplierRowsAsync(
        string analyticsConnectionString,
        SupplierDecisionHubFilters filters,
        CancellationToken ct)
    {
        if (CanUsePrecomputedSupplierRows(filters))
        {
            var capabilities = await GetPrecomputedQueryCapabilitiesAsync(analyticsConnectionString, ct);
            if (!capabilities.HasDecisionScoreCache)
            {
                throw new SupplierDecisionUnavailableException(
                    "MISSING_TABLE",
                    "Supplier decision cache nije dostupan. Pokusajte ponovo nakon osvezavanja analitike.");
            }

            var (precomputedSql, precomputedParameters) = BuildPrecomputedSupplierRowsSql(filters, capabilities);
            try
            {
                var rawRows = await ExecuteSupplierRowsQueryAsync(analyticsConnectionString, precomputedSql, precomputedParameters, ct);
                var (rows, zeroRevenueRowsExcludedCount) = FilterRevenueRows(rawRows);

                // Do NOT fall back to a wider window when an explicit date range was given
                // (30d / 90d / 180d period presets all set HasExplicitDateRange = true).
                // Silently returning 180d data for a 30d request is misleading â€” show empty
                // results instead so the user knows there are no metrics for that period.
                if (rows.Count == 0 && !filters.HasExplicitDateRange)
                {
                    // Only reached when no date range is specified at all AND the windowed MV
                    // is somehow empty. Fall back to all-time cache as a last resort.
                    var (sqlAll, pAll) = BuildPrecomputedSupplierRowsSql(filters, capabilities, windowOverride: 0, applyDateRangeFilter: false);
                    rawRows = await ExecuteSupplierRowsQueryAsync(analyticsConnectionString, sqlAll, pAll, ct);
                    (rows, zeroRevenueRowsExcludedCount) = FilterRevenueRows(rawRows);
                }

                return new SupplierRowsDataset(
                    Rows: rows,
                    ZeroRevenueRowsExcludedCount: zeroRevenueRowsExcludedCount,
                    IgnoredRowCount: zeroRevenueRowsExcludedCount,
                    GeneratedAtUtc: DateTime.UtcNow);
            }
            catch (PostgresException ex) when (IsMissingPrecomputedDependency(ex))
            {
                throw new SupplierDecisionUnavailableException(
                    ex.SqlState == "42P01" ? "MISSING_TABLE" : "SQL_ERROR",
                    "Supplier decision podaci trenutno nisu spremni. Pokusajte ponovo uskoro.",
                    ex);
            }
        }

        var mlCapabilities = await GetSupplierMlQueryCapabilitiesAsync(analyticsConnectionString, ct);
        var (sql, parameters) = BuildSupplierRowsSql(filters, mlCapabilities);
        try
        {
            var rawRows = await ExecuteSupplierRowsQueryAsync(analyticsConnectionString, sql, parameters, ct);
            var (rows, zeroRevenueRowsExcludedCount) = FilterRevenueRows(rawRows);
            return new SupplierRowsDataset(
                Rows: rows,
                ZeroRevenueRowsExcludedCount: zeroRevenueRowsExcludedCount,
                IgnoredRowCount: zeroRevenueRowsExcludedCount,
                GeneratedAtUtc: DateTime.UtcNow);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            throw new SupplierDecisionUnavailableException(
                "MISSING_TABLE",
                "Supplier decision podaci trenutno nisu spremni. Pokusajte ponovo uskoro.",
                ex);
        }
        catch (NpgsqlException ex) when (ex.InnerException is TimeoutException)
        {
            Infrastructure.Logging.SqlCommandLoggingHelper.LogSqlExecution(
                dbSource: "analytics",
                commandKind: "ExecuteReader",
                sql: sql,
                parameters: null,
                durationMs: -1,
                succeeded: false,
                rowsAffected: null,
                exception: ex,
                requestId: Application.Logging.RequestLogContext.Current.RequestId,
                traceId: Application.Logging.RequestLogContext.Current.TraceId);

            throw new SupplierDecisionUnavailableException(
                "SQL_TIMEOUT",
                "Supplier decision podaci trenutno nisu dostupni zbog isteka vremena.",
                ex);
        }
    }

    private static (List<SupplierScoreRow> Rows, int ZeroRevenueRowsExcludedCount) FilterRevenueRows(List<SupplierScoreRow> rows)
    {
        if (rows.Count == 0)
        {
            return (rows, 0);
        }

        var excluded = rows.Count(x => x.Revenue <= 0);
        if (excluded == 0)
        {
            return (rows, 0);
        }

        return (rows.Where(x => x.Revenue > 0).ToList(), excluded);
    }

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

    private static async Task<List<SupplierScoreRow>> ExecuteSupplierRowsQueryAsync(
        string analyticsConnectionString,
        string sql,
        List<NpgsqlParameter> parameters,
        CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(analyticsConnectionString, ct);

        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 25; // hard limit: return empty/throw fast rather than hanging
        command.Parameters.AddRange(parameters.ToArray());

        var results = new List<SupplierScoreRow>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                var supplierId = GetInt32(reader, "supplier_id");
                var sourceSupplierName = GetString(reader, "supplier_name");
                var supplierNameMissing = string.IsNullOrWhiteSpace(sourceSupplierName);
                var supplierName = NormalizeSupplierName(supplierId, sourceSupplierName);
                var recommendationCode = GetString(reader, "recommendation_code");
                var confidenceScore = GetDecimal(reader, "confidence_score");
                var recommendationSignal = BuildRecommendationSignal(recommendationCode, confidenceScore);

                results.Add(new SupplierScoreRow(
                    supplierId,
                    supplierName,
                    GetDateTime(reader, "period_from"),
                    GetDateTime(reader, "period_to"),
                    GetDecimal(reader, "revenue"),
                    GetDecimal(reader, "units"),
                    GetDecimal(reader, "fullprice_revenue_share"),
                    GetDecimal(reader, "fullprice_sellthrough"),
                    GetDecimal(reader, "markdown_revenue_share"),
                    GetDecimal(reader, "pre_markdown_margin_pct"),
                    GetDecimal(reader, "dead_stock_rate"),
                    GetDecimal(reader, "unsold_stock_value"),
                    GetDecimal(reader, "repeat_winner_rate"),
                    GetDecimal(reader, "markdown_dependency_score"),
                    GetDecimal(reader, "stock_risk_score"),
                    GetDecimal(reader, "return_rate"),
                    GetDecimal(reader, "category_focus_score"),
                    GetDecimal(reader, "ml_supplier_score"),
                    GetString(reader, "ai_explanation"),
                    GetString(reader, "top_feature_1"),
                    GetString(reader, "top_feature_2"),
                    GetString(reader, "top_feature_3"),
                    GetDecimal(reader, "supplier_quality_index"),
                        recommendationCode,
                        confidenceScore,
                        supplierNameMissing,
                        recommendationSignal.ReliabilityPct,
                        recommendationSignal.DataQualityStatus,
                        recommendationSignal.StatusReason,
                        recommendationSignal.ReasonCodes));
            }

            sw.Stop();
            try
            {
                Infrastructure.Logging.SqlCommandLoggingHelper.LogSqlExecution(
                    dbSource: "analytics",
                    commandKind: "ExecuteReader",
                    sql: sql,
                    parameters: command.Parameters,
                    durationMs: sw.ElapsedMilliseconds,
                    succeeded: true,
                    rowsAffected: null,
                    exception: null,
                    requestId: Application.Logging.RequestLogContext.Current.RequestId,
                    traceId: Application.Logging.RequestLogContext.Current.TraceId);
            }
            catch { }
        }
        catch (Exception ex)
        {
            sw.Stop();
            try
            {
                Infrastructure.Logging.SqlCommandLoggingHelper.LogSqlExecution(
                    dbSource: "analytics",
                    commandKind: "ExecuteReader",
                    sql: sql,
                    parameters: command.Parameters,
                    durationMs: sw.ElapsedMilliseconds,
                    succeeded: false,
                    rowsAffected: null,
                    exception: ex,
                    requestId: Application.Logging.RequestLogContext.Current.RequestId,
                    traceId: Application.Logging.RequestLogContext.Current.TraceId);
            }
            catch { }

            throw;
        }

        return results;
    }

    private sealed record RecommendationSignal(
        decimal ReliabilityPct,
        string DataQualityStatus,
        string StatusReason,
        IReadOnlyList<string> ReasonCodes);

    private static RecommendationSignal BuildRecommendationSignal(string recommendationCode, decimal confidenceScore)
    {
        var normalizedCode = (recommendationCode ?? string.Empty).Trim().ToUpperInvariant();
        var reliabilityPct = Math.Clamp(confidenceScore, 0m, 100m);

        var dataQualityStatus = reliabilityPct switch
        {
            >= 70m => "good",
            >= 45m => "warning",
            > 0m => "critical",
            _ => "insufficient_data"
        };

        var statusReason = normalizedCode switch
        {
            "EXPAND" => "Jak signal rasta uz stabilan kvalitet i marginu.",
            "EXPAND_SELECTIVELY" => "Pozitivan signal uz preporuku za selektivno Å¡irenje.",
            "HOLD" => "Stabilan uÄinak; zadrÅ¾ati trenutni nivo fokusa.",
            "PRICE_NEGOTIATE" => "Signal ukazuje na pritisak margine; potreban pregovor o ceni.",
            "ASSORTMENT_REDUCE" => "PoviÅ¡en stock-risk i niÅ¾a isplativost; razmotriti suÅ¾avanje asortimana.",
            "OOS_FALSE_NEGATIVE" => "Signal je meÅ¡ovit zbog OOS efekata; potrebno ruÄno tumaÄenje.",
            "REVIEW_QUALITY" => "Kvalitet signala zahteva dodatnu proveru pre odluke.",
            _ => "Nedovoljno podataka za pouzdanu preporuku."
        };

        var reasonCodes = normalizedCode switch
        {
            "EXPAND" => new[] { "strong_growth_signal" },
            "EXPAND_SELECTIVELY" => new[] { "selective_growth_signal" },
            "HOLD" => new[] { "stable_performance" },
            "PRICE_NEGOTIATE" => new[] { "margin_pressure" },
            "ASSORTMENT_REDUCE" => new[] { "assortment_risk" },
            "OOS_FALSE_NEGATIVE" => new[] { "oos_false_negative" },
            "REVIEW_QUALITY" => new[] { "review_required" },
            _ => new[] { "insufficient_signal" }
        };

        if (reliabilityPct <= 0m)
        {
            return new RecommendationSignal(
                0m,
                "insufficient_data",
                "Nedovoljno podataka za pouzdanu preporuku.",
                new[] { "insufficient_signal" });
        }

        return new RecommendationSignal(
            reliabilityPct,
            dataQualityStatus,
            statusReason,
            reasonCodes);
    }

    private static bool CanUsePrecomputedSupplierRows(SupplierDecisionHubFilters filters) =>
        CanUsePrecomputedDateRange(filters)
        && string.IsNullOrWhiteSpace(filters.Category)
        && string.IsNullOrWhiteSpace(filters.Gender)
        && !filters.SeasonId.HasValue
        && !filters.ExcludeOosBeforeMarkdown
        && !filters.StoreId.HasValue
        && string.Equals(filters.DataScope, "all", StringComparison.OrdinalIgnoreCase);

    private static bool CanUsePrecomputedDateRange(SupplierDecisionHubFilters filters)
    {
        // The precomputed MV already filters by period_from/period_to overlap, so any explicit
        // date range can be served from the cache.  The 180-day threshold was causing short
        // ranges (e.g. last-30-days) to fall through to the live query, which times out on
        // the analytics DB under normal load.
        return true;
    }

    private sealed record PrecomputedQueryCapabilities(
        bool HasDecisionScoreCache,
        bool HasMarkdownDependencyCache,
        bool HasMlLatestPredictionsView,
        bool DecisionScoreCacheHasMlSupplierScore);

    private sealed record SupplierMlQueryCapabilities(
        bool HasSupplierMlPredictionsTable,
        bool HasSupplierMlPredictionRequiredColumns,
        bool HasSupplierMlPredictionModelVersionId,
        bool HasModelVersionTable)
    {
        public bool CanUseSupplierMlPredictions =>
            HasSupplierMlPredictionsTable && HasSupplierMlPredictionRequiredColumns;

        public bool CanFilterActiveModelVersion =>
            CanUseSupplierMlPredictions
            && HasSupplierMlPredictionModelVersionId
            && HasModelVersionTable;
    }

    private static bool IsMissingPrecomputedDependency(PostgresException ex) =>
        ex.SqlState is "42P01" or "42703";

    private static async Task<PrecomputedQueryCapabilities> GetPrecomputedQueryCapabilitiesAsync(
        string analyticsConnectionString,
        CancellationToken ct)
    {
        const string sql = """
SELECT
    to_regclass('public.mv_supplier_decision_score_cache') IS NOT NULL AS has_decision_score_cache,
    to_regclass('public.mv_supplier_markdown_dependency_cache') IS NOT NULL AS has_markdown_dependency_cache,
    to_regclass('public.vw_supplier_ml_latest_predictions') IS NOT NULL AS has_ml_latest_predictions_view,
    to_regclass('public.supplier_ml_predictions') IS NOT NULL AS has_supplier_ml_predictions_table,
    to_regclass('public.model_version') IS NOT NULL AS has_model_version_table,
    EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'mv_supplier_decision_score_cache'
          AND column_name = 'ml_supplier_score'
    ) AS decision_score_cache_has_ml_supplier_score;
""";

        await using var connection = await OpenConnectionAsync(analyticsConnectionString, ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return new PrecomputedQueryCapabilities(false, false, false, false);
        }

        return new PrecomputedQueryCapabilities(
            GetBoolean(reader, "has_decision_score_cache"),
            GetBoolean(reader, "has_markdown_dependency_cache"),
            GetBoolean(reader, "has_ml_latest_predictions_view")
                && GetBoolean(reader, "has_supplier_ml_predictions_table")
                && GetBoolean(reader, "has_model_version_table"),
            GetBoolean(reader, "decision_score_cache_has_ml_supplier_score"));
    }

    private static async Task<SupplierMlQueryCapabilities> GetSupplierMlQueryCapabilitiesAsync(
        string analyticsConnectionString,
        CancellationToken ct)
    {
        const string sql = """
SELECT
    to_regclass('public.supplier_ml_predictions') IS NOT NULL AS has_supplier_ml_predictions_table,
    (
        SELECT COUNT(DISTINCT column_name)
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'supplier_ml_predictions'
          AND column_name = ANY(ARRAY[
              'id',
              'supplier_id',
              'snapshot_date',
              'model_type',
              'ml_supplier_score',
              'top_feature_1',
              'top_feature_2',
              'top_feature_3',
              'explanation_text',
              'created_at'
          ])
    ) = 10 AS has_supplier_ml_prediction_required_columns,
    EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'supplier_ml_predictions'
          AND column_name = 'model_version_id'
    ) AS has_supplier_ml_prediction_model_version_id,
    to_regclass('public.model_version') IS NOT NULL AS has_model_version_table;
""";

        await using var connection = await OpenConnectionAsync(analyticsConnectionString, ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return new SupplierMlQueryCapabilities(false, false, false, false);
        }

        return new SupplierMlQueryCapabilities(
            GetBoolean(reader, "has_supplier_ml_predictions_table"),
            GetBoolean(reader, "has_supplier_ml_prediction_required_columns"),
            GetBoolean(reader, "has_supplier_ml_prediction_model_version_id"),
            GetBoolean(reader, "has_model_version_table"));
    }

    /// Returns the number of lookback days (90 or 180) if a windowed MV should be used,
    /// or 0 if the all-time cache is most appropriate.
    private static int GetDecisionScoreWindowDays(SupplierDecisionHubFilters filters)
    {
        if (!filters.HasExplicitDateRange)
            return 0;

        var days = GetRequestedRangeDays(filters);
        if (days <= 90) return 90;
        if (days <= 180) return 180;
        return 0;
    }

    private static int GetRequestedRangeDays(SupplierDecisionHubFilters filters)
    {
        if (!filters.HasExplicitDateRange)
        {
            return DefaultLookbackDays;
        }

        var days = (int)Math.Floor((filters.ToDate - filters.FromDate).TotalDays) + 1;
        return Math.Max(1, days);
    }

    private static string ResolveRequestedDataset(SupplierDecisionHubFilters filters)
    {
        if (!filters.HasExplicitDateRange)
        {
            return "all_time";
        }

        var days = GetRequestedRangeDays(filters);
        if (days <= 30) return "30d";
        if (days <= 90) return "90d";
        if (days <= 180) return "180d";
        return "custom_range";
    }

    private static string ResolveEffectiveDataset(int windowDays) => windowDays switch
    {
        90 => "90d",
        180 => "180d",
        _ => "all_time"
    };

    private static string BuildEffectivePeriodLabel(SupplierDecisionHubFilters filters, string dataset)
    {
        if (dataset == "custom_range")
        {
            return $"{filters.FromDate:dd.MM.yyyy} - {filters.ToDate:dd.MM.yyyy}";
        }

        return dataset switch
        {
            "30d" => "Poslednjih 30 dana",
            "90d" => "Poslednjih 90 dana",
            "180d" => "Poslednjih 180 dana",
            _ => "Neograniceno"
        };
    }

    private static string SelectDecisionScoreMv(int windowDays) => windowDays switch
    {
        90 => "mv_supplier_decision_score_cache_90d",
        180 => "mv_supplier_decision_score_cache_180d",
        _ => "mv_supplier_decision_score_cache"
    };

    private static string? BuildDecisionScoreDataNote(SupplierDecisionHubFilters filters)
    {
        var requestedDataset = ResolveRequestedDataset(filters);
        var effectiveDataset = ResolveEffectiveDataset(GetDecisionScoreWindowDays(filters));
        var usedFallback = !string.Equals(requestedDataset, effectiveDataset, StringComparison.OrdinalIgnoreCase);

        if (usedFallback)
        {
            return requestedDataset == "30d"
                ? "Trazeni period je 30d, ali ne postoji posebna 30d materialized view. Za helper signal koristi se 90d dataset, uz striktan filter opsega i bez tihog fallback-a za finalnu preporuku."
                : $"Trazeni period se oslanja na dataset {BuildEffectivePeriodLabel(filters, effectiveDataset)} kao pomocni signal, uz striktan filter opsega i bez tihog fallback-a.";
        }

        return requestedDataset switch
        {
            "30d" => "Metrike su izracunate za trazeni period od 30 dana, uz striktan opseg bez tihog fallback-a.",
            "90d" => "Metrike su izracunate na osnovu nivelacija iz poslednjih 90 dana.",
            "180d" => "Metrike su izracunate na osnovu nivelacija iz poslednjih 180 dana.",
            _ => filters.HasExplicitDateRange
                ? "Metrike su izracunate za odabrani period preko 180 dana, uz all-history cache kao izvor i striktan filter bez tihog fallback-a."
                : null
        };
    }

    private static ScorecardTrustMetadata BuildScorecardTrustMetadata(
        SupplierRowsDataset dataset,
        SupplierDecisionHubFilters filters)
    {
        var rows = dataset.Rows;
        var hasData = rows.Count > 0;
        var requestedDataset = ResolveRequestedDataset(filters);
        var windowDays = GetDecisionScoreWindowDays(filters);
        var effectiveDataset = ResolveEffectiveDataset(windowDays);
        var usedFallback = !string.Equals(requestedDataset, effectiveDataset, StringComparison.OrdinalIgnoreCase);
        var effectiveFrom = hasData ? rows.Min(x => x.PeriodFrom) : filters.FromDate;
        var effectiveTo = hasData ? rows.Max(x => x.PeriodTo) : filters.ToDate;
        var missingSupplierNameCount = rows.Count(x => x.SupplierNameMissing);
        var hasLowSampleSize = rows.Count > 0 && rows.Count < 3;
        var zeroRevenueRowsExcludedCount = dataset.ZeroRevenueRowsExcludedCount;
        var ignoredRowCount = dataset.IgnoredRowCount;
        var lastRefreshAtUtc = dataset.GeneratedAtUtc;
        var dataCoverageStatus = !hasData
            ? "insufficient_data"
            : (missingSupplierNameCount > 0
                ? "critical"
                : (usedFallback || hasLowSampleSize ? "warning" : "good"));
        var recommendationAllowed = hasData
            && !usedFallback
            && !hasLowSampleSize
            && missingSupplierNameCount == 0
            && (dataCoverageStatus == "good" || dataCoverageStatus == "warning");
        var coverage = windowDays switch
        {
            90 => "window_90d",
            180 => "window_180d",
            _ => "all_history"
        };
        var fallbackReasonCode = (string?)null;
        var fallbackReason = (string?)null;
        if (usedFallback)
        {
            if (requestedDataset == "30d" && effectiveDataset == "90d")
            {
                fallbackReasonCode = "no_mv_30d";
                fallbackReason = "Trazeni 30d nema zaseban scorecard dataset; koristi se 90d kao pomocni signal (bez tihog fallback-a za finalnu preporuku).";
            }
            else if (requestedDataset == "custom_range" && effectiveDataset == "all_time")
            {
                fallbackReasonCode = "range_uses_all_time";
                fallbackReason = "Odabrani period je siri od 180 dana; koristi se all-time cache kao helper dataset uz striktan filter opsega (bez tihog fallback-a).";
            }
            else
            {
                fallbackReasonCode = "fallback_dataset_used";
                fallbackReason = "Trazeni dataset nije dostupan; prikazan je siri helper dataset uz striktan filter opsega (bez tihog fallback-a).";
            }
        }

        return new ScorecardTrustMetadata(
            filters.FromDate,
            filters.ToDate,
            effectiveFrom,
            effectiveTo,
            requestedDataset,
            effectiveDataset,
            BuildEffectivePeriodLabel(filters, effectiveDataset),
            dataCoverageStatus,
            usedFallback,
            fallbackReason,
            fallbackReasonCode,
            lastRefreshAtUtc,
            rows.Count,
            ignoredRowCount,
            zeroRevenueRowsExcludedCount,
            missingSupplierNameCount,
            hasData,
            filters.HasExplicitDateRange,
            recommendationAllowed,
            true,
            windowDays,
            filters.DataScope,
            coverage,
            BuildDecisionScoreDataNote(filters));
    }

    private static (string Sql, List<NpgsqlParameter> Parameters) BuildPrecomputedSupplierRowsSql(
        SupplierDecisionHubFilters filters,
        PrecomputedQueryCapabilities capabilities,
        int? windowOverride = null,
        bool applyDateRangeFilter = true)
    {
        var windowDays = windowOverride ?? GetDecisionScoreWindowDays(filters);
        var mvName = SelectDecisionScoreMv(windowDays);
        var parameters = new List<NpgsqlParameter>();
        var where = new StringBuilder("WHERE 1 = 1");
        var markdownSelect = capabilities.HasMarkdownDependencyCache
            ? """
    COALESCE(md.markdown_revenue_share, 0) AS markdown_revenue_share,
    COALESCE(md.dead_stock_rate, 0) AS dead_stock_rate,
    COALESCE(md.unsold_stock_value, 0)::numeric(18,2) AS unsold_stock_value,
"""
            : """
    0::numeric AS markdown_revenue_share,
    0::numeric AS dead_stock_rate,
    0::numeric(18,2) AS unsold_stock_value,
""";
        var markdownJoin = capabilities.HasMarkdownDependencyCache
            ? """
LEFT JOIN mv_supplier_markdown_dependency_cache md
       ON md.supplier_id = ds.supplier_id
      AND md.category IS NULL
"""
            : string.Empty;
        // ml_supplier_score column only exists on the all-time MV, not on windowed MVs.
        var mlSupplierScore = capabilities.DecisionScoreCacheHasMlSupplierScore && windowDays == 0
            ? "COALESCE(ds.ml_supplier_score, ds.supplier_quality_index)"
            : "ds.supplier_quality_index";
        var mlSelect = capabilities.HasMlLatestPredictionsView
            ? """
    COALESCE(NULLIF(ml.explanation_text, ''), '') AS ai_explanation,
    COALESCE(NULLIF(ml.top_feature_1, ''), '') AS top_feature_1,
    COALESCE(NULLIF(ml.top_feature_2, ''), '') AS top_feature_2,
    COALESCE(NULLIF(ml.top_feature_3, ''), '') AS top_feature_3,
"""
            : """
    '' AS ai_explanation,
    '' AS top_feature_1,
    '' AS top_feature_2,
    '' AS top_feature_3,
""";
        var mlJoin = capabilities.HasMlLatestPredictionsView
            ? """
LEFT JOIN vw_supplier_ml_latest_predictions ml
       ON ml.supplier_id = ds.supplier_id
"""
            : string.Empty;

        if (filters.SupplierId.HasValue)
        {
            where.Append(" AND ds.supplier_id = @supplierId");
            parameters.Add(new NpgsqlParameter("supplierId", filters.SupplierId.Value));
        }

        if (applyDateRangeFilter && filters.HasExplicitDateRange)
        {
            // Always enforce explicit period overlap to avoid silently widening requested ranges.
            // applyDateRangeFilter=false is reserved for explicit fallback queries.
            where.Append(" AND ds.period_to >= @fromDate AND ds.period_from <= @toDate");
            parameters.Add(new NpgsqlParameter("fromDate", filters.FromDate));
            parameters.Add(new NpgsqlParameter("toDate", filters.ToDate));
        }

        if (filters.MinRevenue.HasValue)
        {
            where.Append(" AND ds.revenue >= @minRevenue");
            parameters.Add(new NpgsqlParameter("minRevenue", filters.MinRevenue.Value));
        }

        if (filters.OnlyHighConfidence)
        {
            where.Append(" AND ds.confidence_score * 100 >= @confidenceThreshold");
            parameters.Add(new NpgsqlParameter("confidenceThreshold", HighConfidenceThreshold));
        }

        var sql = $"""
SELECT
    ds.supplier_id,
    ds.supplier_name,
    ds.period_from,
    ds.period_to,
    ds.revenue,
    ds.units,
    ds.fullprice_revenue_share,
    ds.fullprice_sellthrough,
{markdownSelect}
    ds.pre_markdown_margin_pct,
    ds.repeat_winner_rate,
    ds.markdown_dependency_score,
    ds.stock_risk_score,
    ds.return_rate,
    ds.category_focus_score,
    {mlSupplierScore} AS ml_supplier_score,
{mlSelect}
    ds.supplier_quality_index,
    ds.recommendation_code,
    ROUND(ds.confidence_score * 100, 2) AS confidence_score
FROM {mvName} ds
{markdownJoin}{mlJoin}
{where}
ORDER BY ds.supplier_quality_index DESC, ds.revenue DESC, ds.supplier_name;
""";

        return (sql, parameters);
    }

    private static (string Sql, List<NpgsqlParameter> Parameters) BuildSupplierRowsSql(
        SupplierDecisionHubFilters filters,
        SupplierMlQueryCapabilities mlCapabilities)
    {
        var parameters = new List<NpgsqlParameter>();
        var rowWhere = BuildRowFilters(filters, parameters);
        var supplierWhere = BuildSupplierFilters(filters, parameters);
        var currentCostSql = AnalyticsMarginPolicy.BuildPositiveCostSql(@"a.""NabavnaCenaDin""", @"a.""NabavnaCena""");
        parameters.Add(new NpgsqlParameter("mlAsOfDate", filters.ToDate));
        var modelVersionJoin = mlCapabilities.CanFilterActiveModelVersion
            ? """
        LEFT JOIN model_version mv
               ON mv.id = p.model_version_id
"""
            : string.Empty;
        var modelVersionWhere = mlCapabilities.CanFilterActiveModelVersion
            ? "          AND COALESCE(mv.is_active, TRUE)"
            : string.Empty;
        var mlSelect = mlCapabilities.CanUseSupplierMlPredictions
            ? $"""
        ROUND(COALESCE(ml.ml_supplier_score, fs.supplier_quality_index), 2) AS ml_supplier_score,
        COALESCE(NULLIF(ml.explanation_text, ''), '') AS ai_explanation,
        COALESCE(NULLIF(ml.top_feature_1, ''), '') AS top_feature_1,
        COALESCE(NULLIF(ml.top_feature_2, ''), '') AS top_feature_2,
        COALESCE(NULLIF(ml.top_feature_3, ''), '') AS top_feature_3,
        ROUND(
            LEAST(
                100,
                GREATEST(
                    0,
                    0.60 * COALESCE(ml.ml_supplier_score, fs.supplier_quality_index)
                    + 0.40 * fs.supplier_quality_index
                )
            ),
            2
        ) AS blended_supplier_quality_index
"""
            : """
        ROUND(fs.supplier_quality_index, 2) AS ml_supplier_score,
        '' AS ai_explanation,
        '' AS top_feature_1,
        '' AS top_feature_2,
        '' AS top_feature_3,
        fs.supplier_quality_index AS blended_supplier_quality_index
""";
        var mlJoin = mlCapabilities.CanUseSupplierMlPredictions
            ? $"""
    LEFT JOIN LATERAL (
        SELECT
            p.ml_supplier_score,
            p.top_feature_1,
            p.top_feature_2,
            p.top_feature_3,
            p.explanation_text
        FROM supplier_ml_predictions p
{modelVersionJoin}        WHERE p.supplier_id = fs.supplier_id
          AND p.model_type = 'supplier_ranking_v1'
          AND p.snapshot_date <= @mlAsOfDate
{modelVersionWhere}
        ORDER BY p.snapshot_date DESC, p.created_at DESC, p.id DESC
        LIMIT 1
    ) ml ON TRUE
"""
            : string.Empty;

        var sql = $"""
WITH filtered_signals AS (
    SELECT
        fs.supplier_id,
        fs.supplier_name,
        COALESCE(fs.category, 'Uncategorized') AS category,
        fs.article_id,
        fs.sku,
        fs.article_name,
        fs.first_markdown_date,
        fs.old_price,
        fs.new_price,
        fs.pre_qty_30d,
        fs.pre_revenue_30d,
        fs.pre_margin_30d,
        fs.pre_sellthrough_30d,
        fs.pre_avg_daily_units,
        fs.days_to_first_markdown,
        fs.stock_before_markdown,
        fs.stockout_before_markdown_flag,
        fs.had_sales_before_markdown_flag,
        fs.signal_quality_flag,
        fs.signal_quality_reason,
        COALESCE(vn.post_qty, 0)::numeric AS post_qty_30d,
        COALESCE(vn.post_revenue, 0)::numeric(18,2) AS post_revenue_30d,
        COALESCE(nd.did_revenue, 0)::numeric(18,2) AS did_revenue,
        COALESCE(nd.did_qty, 0)::numeric AS did_qty,
        COALESCE(a."Kolicina", 0)::numeric AS current_stock,
        COALESCE({currentCostSql}, 0)::numeric(18,2) AS current_cost,
        a."Pol" AS gender,
        a."IDSezona" AS season_id
    FROM vw_supplier_fullprice_signals fs
    LEFT JOIN LATERAL (
        SELECT
            v.price_event_id,
            v.post_qty,
            v.post_revenue
        FROM vw_vendor_sales_nivelacija v
        WHERE v.article_id = fs.article_id
          AND v.event_date::date = fs.first_markdown_date
          AND v.old_price = fs.old_price
          AND v.new_price = fs.new_price
        ORDER BY v.price_event_id
        LIMIT 1
    ) vn ON TRUE
    LEFT JOIN vw_nivelacija_did nd ON nd.price_event_id = vn.price_event_id
    LEFT JOIN "Artikli" a ON a."Id" = fs.article_id
    {rowWhere}
),
filtered_articles AS (
    SELECT DISTINCT supplier_id, article_id
    FROM filtered_signals
),
category_rollup AS (
    SELECT
        supplier_id,
        supplier_name,
        category,
        SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0))::numeric(18,2) AS revenue,
        SUM(COALESCE(pre_qty_30d, 0) + COALESCE(post_qty_30d, 0))::numeric AS units
    FROM filtered_signals
    GROUP BY supplier_id, supplier_name, category
),
supplier_base AS (
    SELECT
        supplier_id,
        supplier_name,
        MIN((first_markdown_date - INTERVAL '30 days')::date) AS period_from,
        MAX((first_markdown_date + INTERVAL '30 days')::date) AS period_to,
        COUNT(*)::int AS article_count,
        SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0))::numeric(18,2) AS revenue,
        SUM(COALESCE(pre_qty_30d, 0) + COALESCE(post_qty_30d, 0))::numeric AS units,
        SUM(COALESCE(pre_revenue_30d, 0))::numeric(18,2) AS revenue_pre_markdown,
        SUM(COALESCE(post_revenue_30d, 0))::numeric(18,2) AS revenue_post_markdown,
        SUM(COALESCE(pre_qty_30d, 0))::numeric AS qty_pre_markdown,
        SUM(COALESCE(post_qty_30d, 0))::numeric AS qty_post_markdown,
        SUM(COALESCE(pre_revenue_30d, 0))
            / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0) AS fullprice_revenue_share,
        SUM(COALESCE(post_revenue_30d, 0))
            / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0) AS markdown_revenue_share,
        SUM(COALESCE(pre_qty_30d, 0))
            / NULLIF(SUM(COALESCE(pre_qty_30d, 0) + GREATEST(COALESCE(stock_before_markdown, 0), 0)), 0) AS fullprice_sellthrough,
        SUM(COALESCE(pre_margin_30d, 0))
            / NULLIF(SUM(COALESCE(pre_revenue_30d, 0)), 0) AS pre_markdown_margin_pct,
        COALESCE(
            SUM(COALESCE(post_revenue_30d, 0)) FILTER (WHERE COALESCE(stockout_before_markdown_flag, FALSE) = FALSE)
            / NULLIF(
                SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)) FILTER (WHERE COALESCE(stockout_before_markdown_flag, FALSE) = FALSE),
                0
            ),
            SUM(COALESCE(post_revenue_30d, 0))
            / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0)
        ) AS oos_adjusted_markdown_dependency,
        COUNT(*) FILTER (
            WHERE COALESCE(current_stock, 0) > 0
              AND COALESCE(post_qty_30d, 0) = 0
        )::numeric / NULLIF(COUNT(*), 0) AS dead_stock_rate,
        SUM(GREATEST(COALESCE(current_stock, 0), 0) * COALESCE(current_cost, 0))::numeric(18,2) AS unsold_stock_value,
        AVG(COALESCE(did_revenue, 0))::numeric(18,2) AS avg_did_revenue,
        AVG(COALESCE(did_qty, 0))::numeric(18,4) AS avg_did_qty,
        COUNT(*) FILTER (WHERE signal_quality_flag = 'high')::numeric / NULLIF(COUNT(*), 0) AS high_signal_share,
        COUNT(*) FILTER (WHERE had_sales_before_markdown_flag)::numeric / NULLIF(COUNT(*), 0) AS had_sales_share,
        COUNT(*) FILTER (WHERE stockout_before_markdown_flag)::numeric / NULLIF(COUNT(*), 0) AS stockout_article_share,
        COUNT(*) FILTER (
            WHERE COALESCE(pre_sellthrough_30d, 0) >= 0.45
              AND COALESCE(pre_margin_30d, 0) > 0
              AND COALESCE(had_sales_before_markdown_flag, FALSE)
              AND signal_quality_flag <> 'low'
        )::numeric / NULLIF(COUNT(*), 0) AS repeat_winner_rate
    FROM filtered_signals
    GROUP BY supplier_id, supplier_name
),
category_focus AS (
    SELECT
        c.supplier_id,
        MAX(c.revenue / NULLIF(b.revenue, 0)) * 100 AS category_focus_score
    FROM category_rollup c
    JOIN supplier_base b ON b.supplier_id = c.supplier_id
    GROUP BY c.supplier_id
),
period_sales AS (
    SELECT
        b.supplier_id,
        COALESCE(SUM(
            CASE
                WHEN pz.datum_prodaje::date >= b.period_from
                 AND pz.datum_prodaje::date <= b.period_to
                THEN ps.kolicina
                ELSE 0
            END
        ), 0)::numeric AS sold_units_in_period
    FROM supplier_base b
    JOIN filtered_articles fa ON fa.supplier_id = b.supplier_id
    LEFT JOIN prodaja_stavke ps ON ps.id_artikal = fa.article_id
    LEFT JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
    GROUP BY b.supplier_id
),
period_returns AS (
    SELECT
        b.supplier_id,
        COALESCE(SUM(
            CASE
                WHEN pz.datum_povracaja::date >= b.period_from
                 AND pz.datum_povracaja::date <= b.period_to
                 AND COALESCE(pz.status, '') <> 'Odbijen'
                THEN ps.kolicina
                ELSE 0
            END
        ), 0)::numeric AS returned_units_in_period
    FROM supplier_base b
    JOIN filtered_articles fa ON fa.supplier_id = b.supplier_id
    LEFT JOIN povracaj_stavke ps ON ps.id_artikal = fa.article_id
    LEFT JOIN povracaj_zaglavlje pz
           ON pz.id = ps.id_povracaj
          AND pz.id_dobavljac = b.supplier_id
    GROUP BY b.supplier_id
),
supplier_scored AS (
    SELECT
        b.supplier_id,
        b.supplier_name,
        b.period_from,
        b.period_to,
        b.revenue,
        b.units,
        ROUND(COALESCE(b.fullprice_revenue_share, 0), 4) AS fullprice_revenue_share,
        ROUND(COALESCE(b.fullprice_sellthrough, 0), 4) AS fullprice_sellthrough,
        ROUND(COALESCE(b.markdown_revenue_share, 0), 4) AS markdown_revenue_share,
        ROUND(COALESCE(b.pre_markdown_margin_pct, 0), 4) AS pre_markdown_margin_pct,
        ROUND(COALESCE(b.dead_stock_rate, 0), 4) AS dead_stock_rate,
        b.unsold_stock_value,
        ROUND(COALESCE(b.repeat_winner_rate, 0), 4) AS repeat_winner_rate,
        ROUND(COALESCE(b.oos_adjusted_markdown_dependency, 0) * 100, 2) AS markdown_dependency_score,
        ROUND(
            LEAST(
                100,
                GREATEST(
                    0,
                    55 * COALESCE(b.dead_stock_rate, 0)
                    + 25 * GREATEST(1 - COALESCE(b.fullprice_sellthrough, 0), 0)
                    + 20 * COALESCE(
                        LEAST(COALESCE(b.unsold_stock_value, 0) / NULLIF(COALESCE(b.revenue, 0), 0), 1),
                        CASE WHEN COALESCE(b.unsold_stock_value, 0) > 0 THEN 1 ELSE 0 END
                    )
                )
            ),
            2
        ) AS stock_risk_score,
        ROUND(
            COALESCE(r.returned_units_in_period, 0)
            / NULLIF(COALESCE(s.sold_units_in_period, 0), 0),
            4
        ) AS return_rate,
        ROUND(COALESCE(cf.category_focus_score, 0), 2) AS category_focus_score,
        ROUND(
            LEAST(
                100,
                GREATEST(
                    0,
                    LEAST(COALESCE(b.article_count, 0), 10) * 4
                    + 35 * COALESCE(b.high_signal_share, 0)
                    + 25 * COALESCE(b.had_sales_share, 0)
                )
            ),
            2
        ) AS confidence_score,
        COALESCE(b.avg_did_revenue, 0)::numeric(18,2) AS avg_did_revenue,
        COALESCE(b.avg_did_qty, 0)::numeric(18,4) AS avg_did_qty,
        COALESCE(b.stockout_article_share, 0) AS stockout_article_share
    FROM supplier_base b
    LEFT JOIN category_focus cf ON cf.supplier_id = b.supplier_id
    LEFT JOIN period_sales s ON s.supplier_id = b.supplier_id
    LEFT JOIN period_returns r ON r.supplier_id = b.supplier_id
),
supplier_rows AS (
    SELECT
        ss.*,
        ROUND(
            LEAST(
                100,
                GREATEST(
                    0,
                    0.25 * (ss.fullprice_revenue_share * 100)
                    + 0.15 * (ss.fullprice_sellthrough * 100)
                    + 0.15 * (LEAST(GREATEST(ss.pre_markdown_margin_pct, 0), 1) * 100)
                    + 0.15 * (ss.repeat_winner_rate * 100)
                    + 0.10 * ss.category_focus_score
                    + 0.20 * ss.confidence_score
                    - 0.20 * ss.markdown_dependency_score
                    - 0.10 * ss.stock_risk_score
                    - 0.10 * (LEAST(GREATEST(COALESCE(ss.return_rate, 0), 0), 1) * 100)
                )
            ),
            2
        ) AS supplier_quality_index
    FROM supplier_scored ss
),
filtered_suppliers AS (
    SELECT
        sr.*,
        CASE
            WHEN sr.return_rate >= 0.12 THEN 'REVIEW_QUALITY'
            WHEN sr.stockout_article_share >= 0.35
             AND sr.fullprice_sellthrough < 0.45
             AND sr.markdown_dependency_score >= 40
            THEN 'OOS_FALSE_NEGATIVE'
            WHEN sr.confidence_score < 45 THEN 'HOLD'
            WHEN sr.confidence_score >= 60
             AND sr.fullprice_revenue_share >= 0.55
             AND sr.fullprice_sellthrough >= 0.45
             AND sr.pre_markdown_margin_pct >= 0.25
             AND sr.markdown_dependency_score <= 35
             AND sr.stock_risk_score < 35
             AND sr.return_rate <= 0.08
            THEN 'EXPAND'
            WHEN sr.confidence_score >= 55
             AND sr.category_focus_score >= 60
             AND sr.repeat_winner_rate >= 0.45
             AND sr.fullprice_revenue_share >= 0.45
             AND sr.markdown_dependency_score < 50
             AND sr.stock_risk_score < 50
            THEN 'EXPAND_SELECTIVELY'
            WHEN sr.confidence_score >= 50
             AND sr.fullprice_revenue_share < 0.45
             AND sr.avg_did_revenue > 0
             AND sr.markdown_dependency_score >= 50
             AND sr.pre_markdown_margin_pct < 0.28
            THEN 'PRICE_NEGOTIATE'
            WHEN sr.confidence_score >= 50
             AND sr.markdown_dependency_score >= 60
             AND sr.stock_risk_score >= 55
             AND sr.repeat_winner_rate < 0.30
            THEN 'ASSORTMENT_REDUCE'
            ELSE 'HOLD'
        END AS recommendation_code
    FROM supplier_rows sr
    {supplierWhere}
),
supplier_rows_with_ml AS (
    SELECT
        fs.*,
{mlSelect}
    FROM filtered_suppliers fs
{mlJoin}
),
final_suppliers AS (
    SELECT
        sr.*,
        CASE
            WHEN sr.return_rate >= 0.12 THEN 'REVIEW_QUALITY'
            WHEN sr.stockout_article_share >= 0.35
             AND sr.fullprice_sellthrough < 0.45
             AND sr.markdown_dependency_score >= 40
            THEN 'OOS_FALSE_NEGATIVE'
            WHEN sr.blended_supplier_quality_index > 80 THEN 'EXPAND'
            WHEN sr.blended_supplier_quality_index >= 60 THEN 'EXPAND_SELECTIVELY'
            WHEN sr.blended_supplier_quality_index >= 40 THEN 'HOLD'
            WHEN sr.blended_supplier_quality_index >= 25 THEN 'PRICE_NEGOTIATE'
            ELSE 'ASSORTMENT_REDUCE'
        END AS blended_recommendation_code
    FROM supplier_rows_with_ml sr
)
SELECT
    supplier_id,
    supplier_name,
    period_from,
    period_to,
    revenue,
    units,
    fullprice_revenue_share,
    fullprice_sellthrough,
    markdown_revenue_share,
    pre_markdown_margin_pct,
    dead_stock_rate,
    unsold_stock_value,
    repeat_winner_rate,
    markdown_dependency_score,
    stock_risk_score,
    COALESCE(return_rate, 0) AS return_rate,
    category_focus_score,
    ml_supplier_score,
    ai_explanation,
    top_feature_1,
    top_feature_2,
    top_feature_3,
    blended_supplier_quality_index AS supplier_quality_index,
    blended_recommendation_code AS recommendation_code,
    confidence_score
FROM final_suppliers;
""";

        return (sql, parameters);
    }

    private static string BuildRowFilters(
        SupplierDecisionHubFilters filters,
        List<NpgsqlParameter> parameters)
    {
        var where = new StringBuilder("WHERE 1 = 1");

        where.Append(" AND fs.first_markdown_date >= @fromDate");
        parameters.Add(new NpgsqlParameter("fromDate", filters.FromDate));

        where.Append(" AND fs.first_markdown_date <= @toDate");
        parameters.Add(new NpgsqlParameter("toDate", filters.ToDate));

        if (filters.StoreId.HasValue)
        {
            where.Append(" AND a.\"IDObjekat\" = @storeId");
            parameters.Add(new NpgsqlParameter("storeId", filters.StoreId.Value));
        }

        if (string.Equals(filters.DataScope, "imported", StringComparison.OrdinalIgnoreCase))
        {
            where.Append(" AND a.\"DataOrigin\" = 'access'");
        }
        else if (string.Equals(filters.DataScope, "existing", StringComparison.OrdinalIgnoreCase))
        {
            where.Append(" AND (a.\"DataOrigin\" = 'existing' OR a.\"DataOrigin\" IS NULL OR a.\"DataOrigin\" = '')");
        }

        if (!string.IsNullOrWhiteSpace(filters.Category))
        {
            where.Append(" AND COALESCE(fs.category, 'Uncategorized') ILIKE @category");
            parameters.Add(new NpgsqlParameter("category", filters.Category));
        }

        if (!string.IsNullOrWhiteSpace(filters.Gender))
        {
            where.Append(" AND COALESCE(a.\"Pol\", '') ILIKE @gender");
            parameters.Add(new NpgsqlParameter("gender", filters.Gender));
        }

        if (filters.SeasonId.HasValue)
        {
            where.Append(" AND a.\"IDSezona\" = @seasonId");
            parameters.Add(new NpgsqlParameter("seasonId", filters.SeasonId.Value));
        }

        if (filters.SupplierId.HasValue)
        {
            where.Append(" AND fs.supplier_id = @supplierId");
            parameters.Add(new NpgsqlParameter("supplierId", filters.SupplierId.Value));
        }

        if (filters.ExcludeOosBeforeMarkdown)
        {
            where.Append(" AND COALESCE(fs.stockout_before_markdown_flag, FALSE) = FALSE");
        }

        return where.ToString();
    }

    private static string BuildSupplierFilters(
        SupplierDecisionHubFilters filters,
        List<NpgsqlParameter> parameters)
    {
        var where = new StringBuilder("WHERE 1 = 1");

        if (filters.MinRevenue.HasValue)
        {
            where.Append(" AND sr.revenue >= @minRevenue");
            parameters.Add(new NpgsqlParameter("minRevenue", filters.MinRevenue.Value));
        }

        if (filters.OnlyHighConfidence)
        {
            where.Append(" AND sr.confidence_score >= @confidenceThreshold");
            parameters.Add(new NpgsqlParameter("confidenceThreshold", HighConfidenceThreshold));
        }

        return where.ToString();
    }

    private static async Task<SupplierDecisionDetailsResponse> BuildDetailsResponseAsync(
        string analyticsConnectionString,
        SupplierDecisionHubFilters filters,
        SupplierScoreRow supplier,
        CancellationToken ct)
    {
        var categoryBreakdown = await QueryCategoryBreakdownAsync(analyticsConnectionString, filters, ct);
        var winningArticles = await QueryArticleDecisionsAsync(analyticsConnectionString, filters, "winning", ct);
        var markdownDependentArticles = await QueryArticleDecisionsAsync(analyticsConnectionString, filters, "markdown", ct);
        var blockedByOosArticles = await QueryArticleDecisionsAsync(analyticsConnectionString, filters, "oos", ct);
        var recommendationHistory = await QueryRecommendationHistoryAsync(analyticsConnectionString, filters, ct);

        return new SupplierDecisionDetailsResponse(
            new SupplierHeaderDto(
                supplier.SupplierId,
                supplier.SupplierName,
                supplier.PeriodFrom,
                supplier.PeriodTo,
                supplier.MlSupplierScore,
                supplier.AiExplanation,
                supplier.TopFeature1,
                supplier.TopFeature2,
                supplier.TopFeature3,
                supplier.SupplierQualityIndex,
                supplier.RecommendationCode,
                supplier.ConfidenceScore),
            new SupplierKpisDto(
                supplier.Revenue,
                supplier.Units,
                supplier.FullPriceRevenueShare,
                supplier.FullPriceSellthrough,
                supplier.MarkdownRevenueShare,
                supplier.PreMarkdownMarginPct,
                supplier.DeadStockRate,
                supplier.UnsoldStockValue,
                supplier.RepeatWinnerRate,
                supplier.UnsoldStockValue),
            categoryBreakdown,
            winningArticles,
            markdownDependentArticles,
            blockedByOosArticles,
            recommendationHistory);
    }

    private static async Task<List<CategoryBreakdownItem>> QueryCategoryBreakdownAsync(
        string analyticsConnectionString,
        SupplierDecisionHubFilters filters,
        CancellationToken ct)
    {
        var (sql, parameters) = BuildCategoryBreakdownSql(filters);
        await using var connection = await OpenConnectionAsync(analyticsConnectionString, ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        var items = new List<CategoryBreakdownItem>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new CategoryBreakdownItem(
                GetString(reader, "category"),
                GetDecimal(reader, "revenue"),
                GetDecimal(reader, "units"),
                GetDecimal(reader, "fullprice_revenue_share"),
                GetDecimal(reader, "fullprice_sellthrough"),
                GetDecimal(reader, "markdown_revenue_share"),
                GetDecimal(reader, "dead_stock_rate"),
                GetDecimal(reader, "unsold_stock_value"),
                GetDecimal(reader, "repeat_winner_rate")));
        }

        return items;
    }

    private static async Task<List<ArticleDecisionItem>> QueryArticleDecisionsAsync(
        string analyticsConnectionString,
        SupplierDecisionHubFilters filters,
        string mode,
        CancellationToken ct)
    {
        var (sql, parameters) = BuildArticleDecisionSql(filters, mode);
        await using var connection = await OpenConnectionAsync(analyticsConnectionString, ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        var items = new List<ArticleDecisionItem>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new ArticleDecisionItem(
                GetInt32(reader, "article_id"),
                GetString(reader, "sku"),
                GetString(reader, "article_name"),
                GetString(reader, "category"),
                GetDateTime(reader, "first_markdown_date"),
                GetDecimal(reader, "pre_revenue_30d"),
                GetDecimal(reader, "post_revenue_30d"),
                GetDecimal(reader, "pre_sellthrough_30d"),
                GetDecimal(reader, "pre_margin_30d"),
                GetDecimal(reader, "markdown_revenue_share"),
                GetDecimal(reader, "stock_before_markdown"),
                GetBoolean(reader, "stockout_before_markdown_flag"),
                GetString(reader, "signal_quality_flag"),
                GetString(reader, "signal_quality_reason")));
        }

        return items;
    }

    private static async Task<List<RecommendationHistoryItem>> QueryRecommendationHistoryAsync(
        string analyticsConnectionString,
        SupplierDecisionHubFilters filters,
        CancellationToken ct)
    {
        var (sql, parameters) = BuildRecommendationHistorySql(filters);
        await using var connection = await OpenConnectionAsync(analyticsConnectionString, ct);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        var items = new List<RecommendationHistoryItem>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var code = GetString(reader, "recommendation_code");
            items.Add(new RecommendationHistoryItem(
                GetDateTime(reader, "period_start"),
                GetDecimal(reader, "revenue"),
                GetDecimal(reader, "fullprice_revenue_share"),
                GetDecimal(reader, "markdown_revenue_share"),
                GetDecimal(reader, "fullprice_sellthrough"),
                GetDecimal(reader, "pre_markdown_margin_pct"),
                code,
                RecommendationTitle(code),
                RecommendationReason(code)));
        }

        return items;
    }

    private static (string Sql, List<NpgsqlParameter> Parameters) BuildCategoryBreakdownSql(SupplierDecisionHubFilters filters)
    {
        var parameters = new List<NpgsqlParameter>();
        var rowWhere = BuildRowFilters(filters, parameters);
        var currentCostSql = AnalyticsMarginPolicy.BuildPositiveCostSql(@"a.""NabavnaCenaDin""", @"a.""NabavnaCena""");

        var sql = $"""
WITH filtered_signals AS (
    SELECT
        fs.supplier_id,
        COALESCE(fs.category, 'Uncategorized') AS category,
        fs.pre_qty_30d,
        fs.pre_revenue_30d,
        fs.pre_margin_30d,
        fs.pre_sellthrough_30d,
        fs.stock_before_markdown,
        fs.stockout_before_markdown_flag,
        fs.had_sales_before_markdown_flag,
        fs.signal_quality_flag,
        COALESCE(vn.post_qty, 0)::numeric AS post_qty_30d,
        COALESCE(vn.post_revenue, 0)::numeric(18,2) AS post_revenue_30d,
        COALESCE(a."Kolicina", 0)::numeric AS current_stock,
        COALESCE({currentCostSql}, 0)::numeric(18,2) AS current_cost
    FROM vw_supplier_fullprice_signals fs
    LEFT JOIN LATERAL (
        SELECT v.post_qty, v.post_revenue
        FROM vw_vendor_sales_nivelacija v
        WHERE v.article_id = fs.article_id
          AND v.event_date::date = fs.first_markdown_date
          AND v.old_price = fs.old_price
          AND v.new_price = fs.new_price
        ORDER BY v.price_event_id
        LIMIT 1
    ) vn ON TRUE
    LEFT JOIN "Artikli" a ON a."Id" = fs.article_id
    {rowWhere}
)
SELECT
    category,
    SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0))::numeric(18,2) AS revenue,
    SUM(COALESCE(pre_qty_30d, 0) + COALESCE(post_qty_30d, 0))::numeric AS units,
    ROUND(
        SUM(COALESCE(pre_revenue_30d, 0))
        / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0),
        4
    ) AS fullprice_revenue_share,
    ROUND(
        SUM(COALESCE(pre_qty_30d, 0))
        / NULLIF(SUM(COALESCE(pre_qty_30d, 0) + GREATEST(COALESCE(stock_before_markdown, 0), 0)), 0),
        4
    ) AS fullprice_sellthrough,
    ROUND(
        SUM(COALESCE(post_revenue_30d, 0))
        / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0),
        4
    ) AS markdown_revenue_share,
    ROUND(
        COUNT(*) FILTER (WHERE COALESCE(current_stock, 0) > 0 AND COALESCE(post_qty_30d, 0) = 0)::numeric
        / NULLIF(COUNT(*), 0),
        4
    ) AS dead_stock_rate,
    SUM(GREATEST(COALESCE(current_stock, 0), 0) * COALESCE(current_cost, 0))::numeric(18,2) AS unsold_stock_value,
    ROUND(
        COUNT(*) FILTER (
            WHERE COALESCE(pre_sellthrough_30d, 0) >= 0.45
              AND COALESCE(pre_margin_30d, 0) > 0
              AND COALESCE(had_sales_before_markdown_flag, FALSE)
              AND signal_quality_flag <> 'low'
        )::numeric / NULLIF(COUNT(*), 0),
        4
    ) AS repeat_winner_rate
FROM filtered_signals
GROUP BY category
ORDER BY revenue DESC, category;
""";

        return (sql, parameters);
    }

    private static (string Sql, List<NpgsqlParameter> Parameters) BuildArticleDecisionSql(
        SupplierDecisionHubFilters filters,
        string mode)
    {
        var parameters = new List<NpgsqlParameter>();
        var rowWhere = BuildRowFilters(filters, parameters);

        var modeClause = mode switch
        {
            "winning" => """
WHERE COALESCE(stockout_before_markdown_flag, FALSE) = FALSE
  AND COALESCE(pre_sellthrough_30d, 0) >= 0.45
  AND COALESCE(pre_margin_30d, 0) > 0
ORDER BY pre_revenue_30d DESC, pre_sellthrough_30d DESC
LIMIT 10
""",
            "markdown" => """
WHERE COALESCE(post_revenue_30d, 0) > 0
ORDER BY markdown_revenue_share DESC, post_revenue_30d DESC
LIMIT 10
""",
            _ => """
WHERE COALESCE(stockout_before_markdown_flag, FALSE) = TRUE
ORDER BY stock_before_markdown ASC, pre_revenue_30d ASC
LIMIT 10
"""
        };

        var sql = $"""
WITH filtered_signals AS (
    SELECT
        fs.article_id,
        fs.sku,
        fs.article_name,
        COALESCE(fs.category, 'Uncategorized') AS category,
        fs.first_markdown_date,
        fs.pre_revenue_30d,
        fs.pre_margin_30d,
        fs.pre_sellthrough_30d,
        fs.stock_before_markdown,
        fs.stockout_before_markdown_flag,
        fs.had_sales_before_markdown_flag,
        fs.signal_quality_flag,
        fs.signal_quality_reason,
        COALESCE(vn.post_revenue, 0)::numeric(18,2) AS post_revenue_30d
    FROM vw_supplier_fullprice_signals fs
    LEFT JOIN LATERAL (
        SELECT v.post_revenue
        FROM vw_vendor_sales_nivelacija v
        WHERE v.article_id = fs.article_id
          AND v.event_date::date = fs.first_markdown_date
          AND v.old_price = fs.old_price
          AND v.new_price = fs.new_price
        ORDER BY v.price_event_id
        LIMIT 1
    ) vn ON TRUE
    LEFT JOIN "Artikli" a ON a."Id" = fs.article_id
    {rowWhere}
)
SELECT
    article_id,
    sku,
    article_name,
    category,
    first_markdown_date,
    COALESCE(pre_revenue_30d, 0)::numeric(18,2) AS pre_revenue_30d,
    COALESCE(post_revenue_30d, 0)::numeric(18,2) AS post_revenue_30d,
    COALESCE(pre_sellthrough_30d, 0) AS pre_sellthrough_30d,
    COALESCE(pre_margin_30d, 0)::numeric(18,2) AS pre_margin_30d,
    COALESCE(markdown_revenue_share, 0) AS markdown_revenue_share,
    COALESCE(stock_before_markdown, 0) AS stock_before_markdown,
    COALESCE(stockout_before_markdown_flag, FALSE) AS stockout_before_markdown_flag,
    COALESCE(signal_quality_flag, 'low') AS signal_quality_flag,
    COALESCE(signal_quality_reason, 'no_reason') AS signal_quality_reason
FROM filtered_signals
{modeClause}
""";

        return (sql, parameters);
    }

    private static (string Sql, List<NpgsqlParameter> Parameters) BuildRecommendationHistorySql(SupplierDecisionHubFilters filters)
    {
        var parameters = new List<NpgsqlParameter>();
        var rowWhere = BuildRowFilters(filters, parameters);

        var sql = $"""
WITH filtered_signals AS (
    SELECT
        fs.first_markdown_date,
        fs.pre_qty_30d,
        fs.pre_revenue_30d,
        fs.pre_margin_30d,
        fs.pre_sellthrough_30d,
        fs.stock_before_markdown,
        fs.stockout_before_markdown_flag,
        COALESCE(vn.post_revenue, 0)::numeric(18,2) AS post_revenue_30d
    FROM vw_supplier_fullprice_signals fs
    LEFT JOIN LATERAL (
        SELECT v.post_revenue
        FROM vw_vendor_sales_nivelacija v
        WHERE v.article_id = fs.article_id
          AND v.event_date::date = fs.first_markdown_date
          AND v.old_price = fs.old_price
          AND v.new_price = fs.new_price
        ORDER BY v.price_event_id
        LIMIT 1
    ) vn ON TRUE
    LEFT JOIN "Artikli" a ON a."Id" = fs.article_id
    {rowWhere}
),
monthly AS (
    SELECT
        date_trunc('month', first_markdown_date)::date AS period_start,
        SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0))::numeric(18,2) AS revenue,
        SUM(COALESCE(pre_revenue_30d, 0))
            / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0) AS fullprice_revenue_share,
        SUM(COALESCE(post_revenue_30d, 0))
            / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0) AS markdown_revenue_share,
        SUM(COALESCE(pre_qty_30d, 0))
            / NULLIF(SUM(COALESCE(pre_qty_30d, 0) + GREATEST(COALESCE(stock_before_markdown, 0), 0)), 0) AS fullprice_sellthrough,
        SUM(COALESCE(pre_margin_30d, 0))
            / NULLIF(SUM(COALESCE(pre_revenue_30d, 0)), 0) AS pre_markdown_margin_pct,
        COUNT(*) FILTER (WHERE COALESCE(stockout_before_markdown_flag, FALSE))::numeric / NULLIF(COUNT(*), 0) AS stockout_share
    FROM filtered_signals
    GROUP BY date_trunc('month', first_markdown_date)::date
)
SELECT
    period_start,
    revenue,
    ROUND(COALESCE(fullprice_revenue_share, 0), 4) AS fullprice_revenue_share,
    ROUND(COALESCE(markdown_revenue_share, 0), 4) AS markdown_revenue_share,
    ROUND(COALESCE(fullprice_sellthrough, 0), 4) AS fullprice_sellthrough,
    ROUND(COALESCE(pre_markdown_margin_pct, 0), 4) AS pre_markdown_margin_pct,
    CASE
        WHEN COALESCE(stockout_share, 0) >= 0.35 AND COALESCE(fullprice_sellthrough, 0) < 0.45 THEN 'OOS_FALSE_NEGATIVE'
        WHEN COALESCE(fullprice_revenue_share, 0) >= 0.55
         AND COALESCE(fullprice_sellthrough, 0) >= 0.45
         AND COALESCE(pre_markdown_margin_pct, 0) >= 0.25
         AND COALESCE(markdown_revenue_share, 0) <= 0.35
        THEN 'EXPAND'
        WHEN COALESCE(fullprice_revenue_share, 0) < 0.45
         AND COALESCE(markdown_revenue_share, 0) >= 0.50
        THEN 'PRICE_NEGOTIATE'
        WHEN COALESCE(markdown_revenue_share, 0) >= 0.60 THEN 'ASSORTMENT_REDUCE'
        ELSE 'HOLD'
    END AS recommendation_code
FROM monthly
ORDER BY period_start DESC
LIMIT 6;
""";

        return (sql, parameters);
    }

    private static string RecommendationTitle(string recommendationCode) =>
        recommendationCode switch
        {
            "EXPAND" => "PoveÄ‡ati saradnju",
            "EXPAND_SELECTIVELY" => "PoveÄ‡ati selektivno",
            "PRICE_NEGOTIATE" => "Pregovarati o ceni",
            "ASSORTMENT_REDUCE" => "Smanjiti nabavku",
            "OOS_FALSE_NEGATIVE" => "Prvo proveriti zalihe",
            "REVIEW_QUALITY" => "Proveriti kvalitet i povraÄ‡aje",
            _ => "ZadrÅ¾ati trenutni nivo"
        };

    private static string RecommendationReason(string recommendationCode) =>
        recommendationCode switch
        {
            "EXPAND" => "Jak sell-through bez sniÅ¾enja i zdrava marÅ¾a ukazuju na kvalitetnu saradnju sa dobavljaÄem.",
            "EXPAND_SELECTIVELY" => "DobavljaÄ ima najbolje rezultate u uÅ¾em skupu kategorija, a ne kroz ceo asortiman.",
            "PRICE_NEGOTIATE" => "TraÅ¾nja se otvara tek posle sniÅ¾enja, Å¡to sugeriÅ¡e previsoku ulaznu cenu.",
            "ASSORTMENT_REDUCE" => "Visoka zavisnost od sniÅ¾enja i stock risk nepotrebno vezuju kapital.",
            "OOS_FALSE_NEGATIVE" => "Slabiji rezultat moÅ¾e biti posledica nedostatka zaliha pre prvog sniÅ¾enja.",
            "REVIEW_QUALITY" => "PovraÄ‡aji ili kvalitet su dovoljno loÅ¡i da blokiraju bezbedno Å¡irenje saradnje.",
            _ => "Signali su meÅ¡oviti, pa je najbezbednije zadrÅ¾ati trenutni nivo saradnje."
        };

    private static string GetAnalyticsConnectionString(IConfiguration configuration) =>
        AnalyticsConnectionResolver.Resolve(configuration);

    private static async Task<NpgsqlConnection> OpenConnectionAsync(string analyticsConnectionString, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(analyticsConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static int GetInt32(IDataRecord record, string column) =>
        record.IsDBNull(record.GetOrdinal(column)) ? 0 : Convert.ToInt32(record.GetValue(record.GetOrdinal(column)), CultureInfo.InvariantCulture);

    private static decimal GetDecimal(IDataRecord record, string column) =>
        record.IsDBNull(record.GetOrdinal(column)) ? 0m : Convert.ToDecimal(record.GetValue(record.GetOrdinal(column)), CultureInfo.InvariantCulture);

    private static string GetString(IDataRecord record, string column) =>
        record.IsDBNull(record.GetOrdinal(column)) ? string.Empty : Convert.ToString(record.GetValue(record.GetOrdinal(column)), CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool GetBoolean(IDataRecord record, string column) =>
        !record.IsDBNull(record.GetOrdinal(column)) && Convert.ToBoolean(record.GetValue(record.GetOrdinal(column)), CultureInfo.InvariantCulture);

    private static DateTime GetDateTime(IDataRecord record, string column)
    {
        if (record.IsDBNull(record.GetOrdinal(column)))
            return DateTime.UtcNow;

        var value = Convert.ToDateTime(record.GetValue(record.GetOrdinal(column)), CultureInfo.InvariantCulture);
        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
    }
}

public sealed record SummaryResponse(
    DateTime From,
    DateTime To,
    int SupplierCount,
    decimal FullPriceRevenueShare,
    decimal FullPriceSellthrough,
    decimal MarkdownRevenueShare,
    decimal PreMarkdownMarginPct,
    decimal CapitalAtRisk,
    IReadOnlyList<SummarySupplierItem> TopGrowSuppliers,
    IReadOnlyList<SummarySupplierItem> TopRiskSuppliers,
    IReadOnlyList<KeyInsightItem> KeyInsights,
    string? DataNote = null,
    ScorecardTrustMetadata? TrustMetadata = null,
    AnalyticsResponseMetaDto? Meta = null);

public sealed record ScorecardTrustMetadata(
    DateTime RequestedFrom,
    DateTime RequestedTo,
    DateTime EffectiveFrom,
    DateTime EffectiveTo,
    string RequestedDataset,
    string EffectiveDataset,
    string EffectivePeriodLabel,
    string DataCoverageStatus,
    bool UsedFallback,
    string? FallbackReason,
    string? FallbackReasonCode,
    DateTime? LastRefreshAtUtc,
    int RowCount,
    int IgnoredRowCount,
    int ZeroRevenueRowsExcludedCount,
    int MissingSupplierNameCount,
    bool HasData,
    bool HasExplicitDateRange,
    bool RecommendationAllowed,
    bool NoSilentFallback,
    int WindowDays,
    string DataScope,
    string Coverage,
    string? DataNote)
{
    [JsonPropertyName("requestedPeriodFrom")]
    public DateTime RequestedPeriodFrom => RequestedFrom;

    [JsonPropertyName("requestedPeriodTo")]
    public DateTime RequestedPeriodTo => RequestedTo;
}

// TODO(backend-dto): extend Supplier Decision Hub recommendation DTOs with
// ReliabilityPct, DataQualityStatus, StatusReason and ReasonCodes so the UI can
// stop showing "pouzdanost nije dostupna" fallbacks for supplier recommendations.
public sealed record SummarySupplierItem(
    int SupplierId,
    string SupplierName,
    decimal Revenue,
    decimal MlSupplierScore,
    decimal SupplierQualityIndex,
    string RecommendationCode,
    decimal ConfidenceScore,
    decimal ReliabilityPct,
    string DataQualityStatus,
    string StatusReason,
    IReadOnlyList<string> ReasonCodes);

public sealed record KeyInsightItem(
    string Title,
    string Value,
    string Details,
    string Tone);

public sealed record QuadrantResponse(
    IReadOnlyList<QuadrantItem> Items,
    ScorecardTrustMetadata? TrustMetadata = null,
    AnalyticsResponseMetaDto? Meta = null);

// TODO(backend-dto): include recommendation quality payload on quadrant items too.
public sealed record QuadrantItem(
    int SupplierId,
    string SupplierName,
    decimal Revenue,
    decimal MarkdownDependency,
    decimal FullPriceSellthrough,
    decimal PreMarkdownMarginPct,
    decimal SupplierQualityIndex,
    string RecommendationCode,
    decimal ConfidenceScore,
    decimal ReliabilityPct,
    string DataQualityStatus,
    string StatusReason,
    IReadOnlyList<string> ReasonCodes);

public sealed record RankingResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<RankingItem> Items,
    string? DataNote = null,
    ScorecardTrustMetadata? TrustMetadata = null,
    AnalyticsResponseMetaDto? Meta = null);

// TODO(backend-dto): include recommendation quality payload and margin quality context on ranking rows.
public sealed record RankingItem(
    int SupplierId,
    string SupplierName,
    decimal Revenue,
    decimal Units,
    decimal FullPriceRevenueShare,
    decimal FullPriceSellthrough,
    decimal PreMarkdownMarginPct,
    decimal MarkdownRevenueShare,
    decimal DeadStockRate,
    decimal UnsoldStockValue,
    decimal RepeatWinnerRate,
    decimal MlSupplierScore,
    decimal SupplierQualityIndex,
    string RecommendationCode,
    decimal ConfidenceScore,
    decimal ReliabilityPct,
    string DataQualityStatus,
    string StatusReason,
    IReadOnlyList<string> ReasonCodes);

public sealed record SupplierDecisionDetailsResponse(
    SupplierHeaderDto SupplierHeader,
    SupplierKpisDto Kpis,
    IReadOnlyList<CategoryBreakdownItem> CategoryBreakdown,
    IReadOnlyList<ArticleDecisionItem> WinningArticles,
    IReadOnlyList<ArticleDecisionItem> MarkdownDependentArticles,
    IReadOnlyList<ArticleDecisionItem> BlockedByOosArticles,
    IReadOnlyList<RecommendationHistoryItem> RecommendationHistory);

public sealed record SupplierDecisionReportResponse(
    string ReportId,
    string StableQueryUrl,
    string ReportTitle,
    string ReportType,
    DateTime GeneratedAtUtc,
    DateTime PeriodFrom,
    DateTime PeriodTo,
    SupplierDecisionReportPeriodContract Period,
    DateTime? LastRefreshAtUtc,
    string DataQualityStatus,
    bool RecommendationAllowed,
    bool UsedFallback,
    IReadOnlyList<string> Warnings,
    string Methodology,
    IReadOnlyList<SupplierDecisionReportRowContract> Rows,
    IReadOnlyList<SupplierDecisionReportSectionContract> Sections,
    SupplierDecisionReportPayloadContract Payload,
    AnalyticsResponseMetaDto? Meta = null);

public sealed record SupplierDecisionReportPeriodContract(
    DateTime FromUtc,
    DateTime ToUtc,
    string Label);

public sealed record SupplierDecisionReportRowContract(
    string Section,
    string Item,
    string Value,
    string? Secondary,
    string? Note);

public sealed record SupplierDecisionReportSectionContract(
    string Key,
    int RowCount);

public sealed record SupplierDecisionReportPayloadContract(
    string TableKey,
    string TableTitle,
    string DocumentType,
    string TemplateName,
    string Locale,
    IReadOnlyList<SupplierDecisionReportPayloadColumnContract> Columns,
    IReadOnlyList<SupplierDecisionReportRowContract> Rows,
    IReadOnlyList<SupplierDecisionReportPayloadNamedValueContract> Filters,
    IReadOnlyList<SupplierDecisionReportPayloadNamedValueContract> Metadata);

public sealed record SupplierDecisionReportPayloadColumnContract(
    string Key,
    string Header,
    string DataType);

public sealed record SupplierDecisionReportPayloadNamedValueContract(
    string Key,
    string Label,
    string Value);

// TODO(backend-dto): expose recommendation quality payload on supplier details header.
public sealed record SupplierHeaderDto(
    int SupplierId,
    string SupplierName,
    DateTime PeriodFrom,
    DateTime PeriodTo,
    decimal MlSupplierScore,
    string AiExplanation,
    string TopFeature1,
    string TopFeature2,
    string TopFeature3,
    decimal SupplierQualityIndex,
    string RecommendationCode,
    decimal ConfidenceScore);

public sealed record SupplierKpisDto(
    decimal Revenue,
    decimal Units,
    decimal FullPriceRevenueShare,
    decimal FullPriceSellthrough,
    decimal MarkdownRevenueShare,
    decimal PreMarkdownMarginPct,
    decimal DeadStockRate,
    decimal UnsoldStockValue,
    decimal RepeatWinnerRate,
    decimal CapitalAtRisk);

public sealed record CategoryBreakdownItem(
    string Category,
    decimal Revenue,
    decimal Units,
    decimal FullPriceRevenueShare,
    decimal FullPriceSellthrough,
    decimal MarkdownRevenueShare,
    decimal DeadStockRate,
    decimal UnsoldStockValue,
    decimal RepeatWinnerRate);

public sealed record ArticleDecisionItem(
    int ArticleId,
    string Sku,
    string ArticleName,
    string Category,
    DateTime FirstMarkdownDate,
    decimal PreRevenue30d,
    decimal PostRevenue30d,
    decimal PreSellthrough30d,
    decimal PreMargin30d,
    decimal MarkdownRevenueShare,
    decimal StockBeforeMarkdown,
    bool StockoutBeforeMarkdownFlag,
    string SignalQualityFlag,
    string SignalQualityReason);

public sealed record RecommendationHistoryItem(
    DateTime PeriodStart,
    decimal Revenue,
    decimal FullPriceRevenueShare,
    decimal MarkdownRevenueShare,
    decimal FullPriceSellthrough,
    decimal PreMarkdownMarginPct,
    string RecommendationCode,
    string RecommendationTitle,
    string RecommendationReason);
