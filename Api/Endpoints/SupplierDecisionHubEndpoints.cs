using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;
using System.Globalization;
using System.Text;

namespace Trendplus2.Endpoints;

public static class SupplierDecisionHubEndpoints
{
    private const int DefaultLookbackDays = 180;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private const decimal HighConfidenceThreshold = 60m;
    private static readonly DateTime GlobalAnalyticsFloorDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static void MapSupplierDecisionHubEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics/suppliers/decision-hub")
            .WithTags("Supplier Decision Hub")
            .RequireRateLimiting("analytics");

        group.MapGet("/summary", async (
            IConfiguration configuration,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? category = null,
            string? gender = null,
            int? seasonId = null,
            decimal? minRevenue = null,
            bool onlyHighConfidence = false,
            bool excludeOosBeforeMarkdown = false,
            int? supplierId = null,
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
                    out var filters,
                    out var validationError))
            {
                return Results.ValidationProblem(validationError!);
            }

            var rows = await QuerySupplierRowsAsync(GetAnalyticsConnectionString(configuration), filters!, ct);
            var response = BuildSummaryResponse(rows, filters!);
            return Results.Ok(response);
        });

        group.MapGet("/quadrant", async (
            IConfiguration configuration,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? category = null,
            string? gender = null,
            int? seasonId = null,
            decimal? minRevenue = null,
            bool onlyHighConfidence = false,
            bool excludeOosBeforeMarkdown = false,
            int? supplierId = null,
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
                    out var filters,
                    out var validationError))
            {
                return Results.ValidationProblem(validationError!);
            }

            var rows = await QuerySupplierRowsAsync(GetAnalyticsConnectionString(configuration), filters!, ct);
            var response = new QuadrantResponse(
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
                        x.ConfidenceScore))
                    .ToList());

            return Results.Ok(response);
        });

        group.MapGet("/ranking", async (
            IConfiguration configuration,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? category = null,
            string? gender = null,
            int? seasonId = null,
            decimal? minRevenue = null,
            bool onlyHighConfidence = false,
            bool excludeOosBeforeMarkdown = false,
            int? supplierId = null,
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
                    out var filters,
                    out var validationError))
            {
                return Results.ValidationProblem(validationError!);
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var rows = await QuerySupplierRowsAsync(GetAnalyticsConnectionString(configuration), filters!, ct);
            var ordered = ApplyRankingSort(rows, sortBy, sortDir).ToList();
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
                    x.ConfidenceScore))
                .ToList();

            return Results.Ok(new RankingResponse(page, pageSize, ordered.Count, paged));
        });

        group.MapGet("/{supplierId:int}/details", async (
            int supplierId,
            IConfiguration configuration,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? category = null,
            string? gender = null,
            int? seasonId = null,
            decimal? minRevenue = null,
            bool onlyHighConfidence = false,
            bool excludeOosBeforeMarkdown = false,
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
                    out var filters,
                    out var validationError))
            {
                return Results.ValidationProblem(validationError!);
            }

            var analyticsConnectionString = GetAnalyticsConnectionString(configuration);
            var rows = await QuerySupplierRowsAsync(analyticsConnectionString, filters!, ct);
            var supplier = rows.FirstOrDefault();
            if (supplier is null)
            {
                return Results.NotFound(new { message = $"Supplier {supplierId} not found for the selected filter set." });
            }

            var response = await BuildDetailsResponseAsync(analyticsConnectionString, filters!, supplier, ct);

            return Results.Ok(response);
        });
    }

    private sealed record SupplierDecisionHubFilters(
        DateTime FromDate,
        DateTime ToDate,
        bool HasExplicitDateRange,
        string? Category,
        string? Gender,
        int? SeasonId,
        decimal? MinRevenue,
        bool OnlyHighConfidence,
        bool ExcludeOosBeforeMarkdown,
        int? SupplierId);

    private sealed record SupplierScoreRow(
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
        decimal ConfidenceScore);

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
            ?? (hasExplicitDateRange ? toUtc.AddDays(-DefaultLookbackDays) : GlobalAnalyticsFloorDate);

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
            supplierId);

        return true;
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

    private static SummaryResponse BuildSummaryResponse(
        IReadOnlyList<SupplierScoreRow> rows,
        SupplierDecisionHubFilters filters)
    {
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
                "Zavisnost od sniženja",
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
            insights);
    }

    private static SummarySupplierItem MapSummarySupplier(SupplierScoreRow row) =>
        new(
            row.SupplierId,
            row.SupplierName,
            row.Revenue,
            row.MlSupplierScore,
            row.SupplierQualityIndex,
            row.RecommendationCode,
            row.ConfidenceScore);

    private static IOrderedEnumerable<SupplierScoreRow> ApplyRankingSort(
        IEnumerable<SupplierScoreRow> rows,
        string? sortBy,
        string? sortDir)
    {
        var normalizedSort = string.IsNullOrWhiteSpace(sortBy)
            ? "supplierQualityIndex"
            : sortBy.Trim();
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

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

    private static string FormatPercent(decimal value) =>
        $"{(value * 100m).ToString("0.##", CultureInfo.InvariantCulture)}%";

    private static decimal Round2(decimal value) => decimal.Round(value, 2);
    private static decimal Round4(decimal value) => decimal.Round(value, 4);

    private static async Task<List<SupplierScoreRow>> QuerySupplierRowsAsync(
        string analyticsConnectionString,
        SupplierDecisionHubFilters filters,
        CancellationToken ct)
    {
        if (CanUsePrecomputedSupplierRows(filters))
        {
            var capabilities = await GetPrecomputedQueryCapabilitiesAsync(analyticsConnectionString, ct);
            if (!capabilities.HasDecisionScoreCache)
            {
                // Supplier caches are still building in the background.
                return [];
            }

            var (precomputedSql, precomputedParameters) = BuildPrecomputedSupplierRowsSql(filters, capabilities);
            try
            {
                return await ExecuteSupplierRowsQueryAsync(analyticsConnectionString, precomputedSql, precomputedParameters, ct);
            }
            catch (PostgresException ex) when (IsMissingPrecomputedDependency(ex))
            {
                // Startup recreates supplier decision objects in multiple batches.
                // If a request lands mid-build, prefer an empty payload over a multi-minute live fallback.
                return [];
            }
        }

        var (sql, parameters) = BuildSupplierRowsSql(filters);
        try
        {
            return await ExecuteSupplierRowsQueryAsync(analyticsConnectionString, sql, parameters, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Base supplier decision views are not ready yet.
            // Return empty results until the first 018 batches complete.
            return [];
        }
    }

    private static async Task<List<SupplierScoreRow>> ExecuteSupplierRowsQueryAsync(
        string analyticsConnectionString,
        string sql,
        List<NpgsqlParameter> parameters,
        CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(analyticsConnectionString, ct);

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.ToArray());

        var results = new List<SupplierScoreRow>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SupplierScoreRow(
                GetInt32(reader, "supplier_id"),
                GetString(reader, "supplier_name"),
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
                GetString(reader, "recommendation_code"),
                GetDecimal(reader, "confidence_score")));
        }

        return results;
    }

    private static bool CanUsePrecomputedSupplierRows(SupplierDecisionHubFilters filters) =>
        !filters.HasExplicitDateRange
        && string.IsNullOrWhiteSpace(filters.Category)
        && string.IsNullOrWhiteSpace(filters.Gender)
        && !filters.SeasonId.HasValue
        && !filters.ExcludeOosBeforeMarkdown;

    private sealed record PrecomputedQueryCapabilities(
        bool HasDecisionScoreCache,
        bool HasMarkdownDependencyCache,
        bool HasMlLatestPredictionsView,
        bool DecisionScoreCacheHasMlSupplierScore);

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
            GetBoolean(reader, "has_ml_latest_predictions_view"),
            GetBoolean(reader, "decision_score_cache_has_ml_supplier_score"));
    }

    private static (string Sql, List<NpgsqlParameter> Parameters) BuildPrecomputedSupplierRowsSql(
        SupplierDecisionHubFilters filters,
        PrecomputedQueryCapabilities capabilities)
    {
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
        var mlSupplierScore = capabilities.DecisionScoreCacheHasMlSupplierScore
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
FROM mv_supplier_decision_score_cache ds
{markdownJoin}{mlJoin}
{where}
ORDER BY ds.supplier_quality_index DESC, ds.revenue DESC, ds.supplier_name;
""";

        return (sql, parameters);
    }

    private static (string Sql, List<NpgsqlParameter> Parameters) BuildSupplierRowsSql(SupplierDecisionHubFilters filters)
    {
        var parameters = new List<NpgsqlParameter>();
        var rowWhere = BuildRowFilters(filters, parameters);
        var supplierWhere = BuildSupplierFilters(filters, parameters);
        parameters.Add(new NpgsqlParameter("mlAsOfDate", filters.ToDate));

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
        COALESCE(a."NabavnaCena", 0)::numeric(18,2) AS current_cost,
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
    FROM filtered_suppliers fs
    LEFT JOIN LATERAL (
        SELECT
            p.ml_supplier_score,
            p.top_feature_1,
            p.top_feature_2,
            p.top_feature_3,
            p.explanation_text
        FROM supplier_ml_predictions p
        LEFT JOIN model_version mv
               ON mv.id = p.model_version_id
        WHERE p.supplier_id = fs.supplier_id
          AND p.model_type = 'supplier_ranking_v1'
          AND p.snapshot_date <= @mlAsOfDate
          AND COALESCE(mv.is_active, TRUE)
        ORDER BY p.snapshot_date DESC, p.created_at DESC, p.id DESC
        LIMIT 1
    ) ml ON TRUE
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
        COALESCE(a."NabavnaCena", 0)::numeric(18,2) AS current_cost
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
            "EXPAND" => "Povećati saradnju",
            "EXPAND_SELECTIVELY" => "Povećati selektivno",
            "PRICE_NEGOTIATE" => "Pregovarati o ceni",
            "ASSORTMENT_REDUCE" => "Smanjiti nabavku",
            "OOS_FALSE_NEGATIVE" => "Prvo proveriti zalihe",
            "REVIEW_QUALITY" => "Proveriti kvalitet i povraćaje",
            _ => "Zadržati trenutni nivo"
        };

    private static string RecommendationReason(string recommendationCode) =>
        recommendationCode switch
        {
            "EXPAND" => "Jak sell-through bez sniženja i zdrava marža ukazuju na kvalitetnu saradnju sa dobavljačem.",
            "EXPAND_SELECTIVELY" => "Dobavljač ima najbolje rezultate u užem skupu kategorija, a ne kroz ceo asortiman.",
            "PRICE_NEGOTIATE" => "Tražnja se otvara tek posle sniženja, što sugeriše previsoku ulaznu cenu.",
            "ASSORTMENT_REDUCE" => "Visoka zavisnost od sniženja i stock risk nepotrebno vezuju kapital.",
            "OOS_FALSE_NEGATIVE" => "Slabiji rezultat može biti posledica nedostatka zaliha pre prvog sniženja.",
            "REVIEW_QUALITY" => "Povraćaji ili kvalitet su dovoljno loši da blokiraju bezbedno širenje saradnje.",
            _ => "Signali su mešoviti, pa je najbezbednije zadržati trenutni nivo saradnje."
        };

    private static string GetAnalyticsConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("AnalyticsConnection")
        ?? configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("AnalyticsConnection or DefaultConnection must be configured.");

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
    IReadOnlyList<KeyInsightItem> KeyInsights);

public sealed record SummarySupplierItem(
    int SupplierId,
    string SupplierName,
    decimal Revenue,
    decimal MlSupplierScore,
    decimal SupplierQualityIndex,
    string RecommendationCode,
    decimal ConfidenceScore);

public sealed record KeyInsightItem(
    string Title,
    string Value,
    string Details,
    string Tone);

public sealed record QuadrantResponse(IReadOnlyList<QuadrantItem> Items);

public sealed record QuadrantItem(
    int SupplierId,
    string SupplierName,
    decimal Revenue,
    decimal MarkdownDependency,
    decimal FullPriceSellthrough,
    decimal PreMarkdownMarginPct,
    decimal SupplierQualityIndex,
    string RecommendationCode,
    decimal ConfidenceScore);

public sealed record RankingResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<RankingItem> Items);

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
    decimal ConfidenceScore);

public sealed record SupplierDecisionDetailsResponse(
    SupplierHeaderDto SupplierHeader,
    SupplierKpisDto Kpis,
    IReadOnlyList<CategoryBreakdownItem> CategoryBreakdown,
    IReadOnlyList<ArticleDecisionItem> WinningArticles,
    IReadOnlyList<ArticleDecisionItem> MarkdownDependentArticles,
    IReadOnlyList<ArticleDecisionItem> BlockedByOosArticles,
    IReadOnlyList<RecommendationHistoryItem> RecommendationHistory);

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
