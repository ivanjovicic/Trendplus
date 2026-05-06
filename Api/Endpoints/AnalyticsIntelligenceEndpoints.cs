using Api.Config;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using System.Data;
using System.Globalization;
using System.Text;

namespace Trendplus2.Endpoints;

public static class AnalyticsIntelligenceEndpoints
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private const int DefaultHistoryDays = 1;
    private const int MaxHistoryDays = 30;

    public static void MapAnalyticsIntelligenceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics/intelligence")
            .WithTags("Analytics Intelligence")
            .RequireRateLimiting("analytics");

        group.MapGet("/demand-signals", async (
            IConfiguration configuration,
            DateTime? date = null,
            int historyDays = DefaultHistoryDays,
            int? articleId = null,
            int? storeId = null,
            int? supplierId = null,
            string? category = null,
            decimal? minSalesVelocity = null,
            decimal? minDemandAcceleration = null,
            int page = 1,
            int pageSize = DefaultPageSize,
            string? sortBy = null,
            string? sortDir = null,
            CancellationToken ct = default) =>
        {
            if (!TryNormalizePagination(page, pageSize, out var normalizedPage, out var normalizedPageSize, out var paginationError))
                return Results.ValidationProblem(paginationError!);

            if (!TryNormalizeHistoryDays(historyDays, out var normalizedHistoryDays, out var historyError))
                return Results.ValidationProblem(historyError!);

            if (minSalesVelocity.HasValue && minSalesVelocity.Value < 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["minSalesVelocity"] = ["minSalesVelocity must be zero or positive."]
                });
            }

            var request = new DemandSignalsRequest(
                NormalizeDate(date),
                normalizedHistoryDays,
                articleId,
                storeId,
                supplierId,
                NormalizeText(category),
                minSalesVelocity,
                minDemandAcceleration,
                normalizedPage,
                normalizedPageSize,
                NormalizeText(sortBy),
                NormalizeText(sortDir));

            var response = await QueryDemandSignalsAsync(GetAnalyticsConnectionString(configuration), request, ct);
            return Results.Ok(response);
        })
        .WithName("GetAnalyticsIntelligenceDemandSignals")
        .Produces<IntelligencePageResponse<DemandSignalItem>>(StatusCodes.Status200OK)
        .ProducesValidationProblem();

        group.MapGet("/inventory-risk", async (
            IConfiguration configuration,
            DateTime? date = null,
            int historyDays = DefaultHistoryDays,
            int? articleId = null,
            int? supplierId = null,
            string? category = null,
            decimal? minDeadStockRisk = null,
            bool onlyAtRisk = false,
            int page = 1,
            int pageSize = DefaultPageSize,
            string? sortBy = null,
            string? sortDir = null,
            CancellationToken ct = default) =>
        {
            if (!TryNormalizePagination(page, pageSize, out var normalizedPage, out var normalizedPageSize, out var paginationError))
                return Results.ValidationProblem(paginationError!);

            if (!TryNormalizeHistoryDays(historyDays, out var normalizedHistoryDays, out var historyError))
                return Results.ValidationProblem(historyError!);

            if (minDeadStockRisk.HasValue && (minDeadStockRisk.Value < 0 || minDeadStockRisk.Value > 1))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["minDeadStockRisk"] = ["minDeadStockRisk must be between 0 and 1."]
                });
            }

            var request = new InventoryRiskSignalsRequest(
                NormalizeDate(date),
                normalizedHistoryDays,
                articleId,
                supplierId,
                NormalizeText(category),
                minDeadStockRisk,
                onlyAtRisk,
                normalizedPage,
                normalizedPageSize,
                NormalizeText(sortBy),
                NormalizeText(sortDir));

            var response = await QueryInventoryRiskSignalsAsync(GetAnalyticsConnectionString(configuration), request, ct);
            return Results.Ok(response);
        })
        .WithName("GetAnalyticsIntelligenceInventoryRisk")
        .Produces<IntelligencePageResponse<InventoryRiskSignalItem>>(StatusCodes.Status200OK)
        .ProducesValidationProblem();

        group.MapGet("/price-intelligence", async (
            IConfiguration configuration,
            int? articleId = null,
            int? supplierId = null,
            string? category = null,
            string? brandKey = null,
            decimal? minDiscountDepth = null,
            decimal? minMarginPct = null,
            int page = 1,
            int pageSize = DefaultPageSize,
            string? sortBy = null,
            string? sortDir = null,
            CancellationToken ct = default) =>
        {
            if (!TryNormalizePagination(page, pageSize, out var normalizedPage, out var normalizedPageSize, out var paginationError))
                return Results.ValidationProblem(paginationError!);

            if (minDiscountDepth.HasValue && (minDiscountDepth.Value < 0 || minDiscountDepth.Value > 1))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["minDiscountDepth"] = ["minDiscountDepth must be between 0 and 1."]
                });
            }

            var request = new PriceIntelligenceRequest(
                articleId,
                supplierId,
                NormalizeText(category),
                NormalizeText(brandKey),
                minDiscountDepth,
                minMarginPct,
                normalizedPage,
                normalizedPageSize,
                NormalizeText(sortBy),
                NormalizeText(sortDir));

            var response = await QueryPriceIntelligenceAsync(GetAnalyticsConnectionString(configuration), request, ct);
            return Results.Ok(response);
        })
        .WithName("GetAnalyticsIntelligencePriceSignals")
        .Produces<IntelligencePageResponse<PriceIntelligenceItem>>(StatusCodes.Status200OK)
        .ProducesValidationProblem();

        group.MapGet("/trend-momentum", async (
            IConfiguration configuration,
            int? articleId = null,
            int? supplierId = null,
            string? category = null,
            decimal? minExternalTrendScore = null,
            decimal? minLocalSalesAcceleration = null,
            int page = 1,
            int pageSize = DefaultPageSize,
            string? sortBy = null,
            string? sortDir = null,
            CancellationToken ct = default) =>
        {
            if (!TryNormalizePagination(page, pageSize, out var normalizedPage, out var normalizedPageSize, out var paginationError))
                return Results.ValidationProblem(paginationError!);

            if (minExternalTrendScore.HasValue && minExternalTrendScore.Value < 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["minExternalTrendScore"] = ["minExternalTrendScore must be zero or positive."]
                });
            }

            var request = new TrendMomentumRequest(
                articleId,
                supplierId,
                NormalizeText(category),
                minExternalTrendScore,
                minLocalSalesAcceleration,
                normalizedPage,
                normalizedPageSize,
                NormalizeText(sortBy),
                NormalizeText(sortDir));

            var response = await QueryTrendMomentumAsync(GetAnalyticsConnectionString(configuration), request, ct);
            return Results.Ok(response);
        })
        .WithName("GetAnalyticsIntelligenceTrendMomentum")
        .Produces<IntelligencePageResponse<TrendMomentumItem>>(StatusCodes.Status200OK)
        .ProducesValidationProblem();
    }

    internal static Task<IntelligencePageResponse<DemandSignalItem>> QueryDemandSignalsAsync(
        string analyticsConnectionString,
        DemandSignalsRequest request,
        CancellationToken ct) =>
        ExecutePagedQueryAsync(
            analyticsConnectionString,
            BuildDemandSignalsSql(request, out var parameters),
            parameters,
            request.Page,
            request.PageSize,
            record => new DemandSignalItem(
                GetInt32(record, "article_id"),
                GetString(record, "sku"),
                GetString(record, "product_name"),
                GetString(record, "category"),
                GetNullableInt32(record, "supplier_id"),
                GetString(record, "supplier_name"),
                GetInt32(record, "store_id"),
                GetString(record, "store_name"),
                GetNullableString(record, "store_city"),
                GetDateTime(record, "date"),
                GetDecimal(record, "sales_velocity"),
                GetDecimal(record, "demand_acceleration"),
                GetNullableInt32(record, "days_since_last_sale"),
                GetInt32(record, "launch_age_days"),
                GetInt32(record, "store_coverage"),
                GetInt32(record, "source_rows")),
            "analytics_intel.mv_product_demand_signals_v1_cache",
            "date",
            request.Date,
            ct);

    internal static Task<IntelligencePageResponse<InventoryRiskSignalItem>> QueryInventoryRiskSignalsAsync(
        string analyticsConnectionString,
        InventoryRiskSignalsRequest request,
        CancellationToken ct) =>
        ExecutePagedQueryAsync(
            analyticsConnectionString,
            BuildInventoryRiskSignalsSql(request, out var parameters),
            parameters,
            request.Page,
            request.PageSize,
            record => new InventoryRiskSignalItem(
                GetInt32(record, "article_id"),
                GetString(record, "sku"),
                GetString(record, "product_name"),
                GetString(record, "category"),
                GetNullableInt32(record, "supplier_id"),
                GetString(record, "supplier_name"),
                GetDateTime(record, "date"),
                GetDecimal(record, "stock_qty"),
                GetDecimal(record, "avg_daily_sales_30d"),
                GetNullableDecimal(record, "days_of_cover"),
                GetNullableDecimal(record, "stock_turn"),
                GetInt32(record, "stockout_days"),
                GetInt32(record, "low_stock_days"),
                GetDecimal(record, "dead_stock_risk")),
            "analytics_intel.mv_inventory_risk_signals_v1_cache",
            "date",
            request.Date,
            ct);

    internal static Task<IntelligencePageResponse<PriceIntelligenceItem>> QueryPriceIntelligenceAsync(
        string analyticsConnectionString,
        PriceIntelligenceRequest request,
        CancellationToken ct) =>
        ExecutePagedQueryAsync(
            analyticsConnectionString,
            BuildPriceIntelligenceSql(request, out var parameters),
            parameters,
            request.Page,
            request.PageSize,
            record => new PriceIntelligenceItem(
                GetInt32(record, "article_id"),
                GetString(record, "sku"),
                GetString(record, "product_name"),
                GetString(record, "category"),
                GetString(record, "brand_key"),
                GetNullableInt32(record, "supplier_id"),
                GetString(record, "supplier_name"),
                GetDateTime(record, "price_date"),
                GetDecimal(record, "net_price"),
                GetDecimal(record, "list_price"),
                GetDecimal(record, "cost"),
                GetNullableDecimal(record, "price_index_vs_category"),
                GetNullableDecimal(record, "price_index_vs_brand"),
                GetDecimal(record, "discount_depth"),
                GetNullableDecimal(record, "margin_pct")),
            "analytics_intel.mv_price_intelligence_v1_cache",
            "price_date",
            null,
            ct);

    internal static Task<IntelligencePageResponse<TrendMomentumItem>> QueryTrendMomentumAsync(
        string analyticsConnectionString,
        TrendMomentumRequest request,
        CancellationToken ct) =>
        ExecutePagedQueryAsync(
            analyticsConnectionString,
            BuildTrendMomentumSql(request, out var parameters),
            parameters,
            request.Page,
            request.PageSize,
            record => new TrendMomentumItem(
                GetInt32(record, "article_id"),
                GetString(record, "sku"),
                GetString(record, "product_name"),
                GetString(record, "category"),
                GetNullableInt32(record, "supplier_id"),
                GetString(record, "supplier_name"),
                GetDateTime(record, "signal_date"),
                GetDecimal(record, "external_trend_score"),
                GetDecimal(record, "local_sales_acceleration"),
                GetDecimal(record, "trend_entropy")),
            "analytics_intel.mv_trend_momentum_v1_cache",
            "signal_date",
            null,
            ct);

    private static async Task<IntelligencePageResponse<T>> ExecutePagedQueryAsync<T>(
        string analyticsConnectionString,
        string sql,
        List<NpgsqlParameter> parameters,
        int page,
        int pageSize,
        Func<IDataRecord, T> map,
        string relationName,
        string dateColumn,
        DateTime? requestedAsOfDate,
        CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(analyticsConnectionString, ct);

        try
        {
            await using var command = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = 120
            };
            command.Parameters.AddRange(parameters.ToArray());

            var items = new List<T>();
            var totalCount = 0;
            DateTime? asOfDate = requestedAsOfDate?.Date;

            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    items.Add(map(reader));
                    if (totalCount == 0)
                        totalCount = GetInt32(reader, "total_count");

                    if (!asOfDate.HasValue && !reader.IsDBNull(reader.GetOrdinal("snapshot_date")))
                        asOfDate = GetDateTime(reader, "snapshot_date").Date;
                }
            }

            if (!asOfDate.HasValue)
                asOfDate = await TryGetLatestSnapshotDateAsync(connection, relationName, dateColumn, ct);

            return new IntelligencePageResponse<T>(asOfDate, page, pageSize, totalCount, items);
        }
        catch (PostgresException ex) when (IsIntelligenceRelationUnavailable(ex))
        {
            return new IntelligencePageResponse<T>(requestedAsOfDate?.Date, page, pageSize, 0, Array.Empty<T>());
        }
    }

    private static string BuildDemandSignalsSql(DemandSignalsRequest request, out List<NpgsqlParameter> parameters)
    {
        parameters = CreatePageParameters(request.Page, request.PageSize);
        parameters.Add(new NpgsqlParameter("signalDate", NpgsqlDbType.Date) { Value = (object?)request.Date?.Date ?? DBNull.Value });
        parameters.Add(new NpgsqlParameter("historyDays", NpgsqlDbType.Integer) { Value = request.HistoryDays });

        var where = new StringBuilder("WHERE TRUE");

        AddOptionalIntFilter(where, parameters, "m.article_id", "articleId", request.ArticleId);
        AddOptionalIntFilter(where, parameters, "m.store_id", "storeId", request.StoreId);
        AddOptionalIntFilter(where, parameters, "lp.supplier_id", "supplierId", request.SupplierId);
        AddOptionalTextFilter(where, parameters, "lp.category", "category", request.Category);
        AddOptionalDecimalFloor(where, parameters, "m.sales_velocity", "minSalesVelocity", request.MinSalesVelocity);
        AddOptionalDecimalFloor(where, parameters, "m.demand_acceleration", "minDemandAcceleration", request.MinDemandAcceleration);

        var orderBy = BuildDemandOrderClause(request.SortBy, request.SortDir);

        return $"""
WITH latest_products AS (
    SELECT DISTINCT ON (pd."ProductId")
        pd."ProductId" AS article_id,
        COALESCE(NULLIF(BTRIM(pd."PLU"), ''), pd."ProductId"::text) AS sku,
        COALESCE(NULLIF(BTRIM(pd."ProductName"), ''), 'Unknown product') AS product_name,
        COALESCE(NULLIF(BTRIM(pd."Category"), ''), 'Uncategorized') AS category,
        pd."SupplierId" AS supplier_id
    FROM "ProductsDim" pd
    ORDER BY pd."ProductId", pd."Timestamp" DESC
),
latest_suppliers AS (
    SELECT DISTINCT ON (sd."SupplierId")
        sd."SupplierId" AS supplier_id,
        COALESCE(NULLIF(BTRIM(sd."Naziv"), ''), 'Unknown supplier') AS supplier_name
    FROM "SuppliersDim" sd
    ORDER BY sd."SupplierId", sd."UpdatedAt" DESC
),
latest_stores AS (
    SELECT DISTINCT ON (st."StoreId")
        st."StoreId" AS store_id,
        COALESCE(NULLIF(BTRIM(st."StoreName"), ''), 'Unknown store') AS store_name,
        NULLIF(BTRIM(st."City"), '') AS store_city
    FROM "StoresDim" st
    ORDER BY st."StoreId", st."StoreKey" DESC
),
bounds AS (
    SELECT
        COALESCE(@signalDate::date, (SELECT MAX(date) FROM analytics_intel.mv_product_demand_signals_v1_cache)) AS as_of_date,
        @historyDays::int AS history_days
),
filtered AS (
    SELECT
        m.article_id,
        COALESCE(lp.sku, m.article_id::text) AS sku,
        COALESCE(lp.product_name, 'Unknown product') AS product_name,
        COALESCE(lp.category, 'Uncategorized') AS category,
        lp.supplier_id,
        COALESCE(ls.supplier_name, 'Unknown supplier') AS supplier_name,
        m.store_id,
        COALESCE(lst.store_name, 'Unknown store') AS store_name,
        lst.store_city,
        m.date,
        m.sales_velocity,
        m.demand_acceleration,
        m.days_since_last_sale,
        m.launch_age_days,
        m.store_coverage,
        m.source_rows
    FROM analytics_intel.mv_product_demand_signals_v1_cache m
    JOIN bounds b
      ON b.as_of_date IS NOT NULL
     AND m.date <= b.as_of_date
     AND m.date >= b.as_of_date - (b.history_days - 1)
    LEFT JOIN latest_products lp
      ON lp.article_id = m.article_id
    LEFT JOIN latest_suppliers ls
      ON ls.supplier_id = lp.supplier_id
    LEFT JOIN latest_stores lst
      ON lst.store_id = m.store_id
    {where}
),
ranked AS (
    SELECT
        f.*,
        COUNT(*) OVER() AS total_count,
        MAX(f.date) OVER() AS snapshot_date
    FROM filtered f
)
SELECT
    article_id,
    sku,
    product_name,
    category,
    supplier_id,
    supplier_name,
    store_id,
    store_name,
    store_city,
    date,
    sales_velocity,
    demand_acceleration,
    days_since_last_sale,
    launch_age_days,
    store_coverage,
    source_rows,
    total_count,
    snapshot_date
FROM ranked
ORDER BY {orderBy}
LIMIT @limit OFFSET @offset;
""";
    }

    private static string BuildInventoryRiskSignalsSql(InventoryRiskSignalsRequest request, out List<NpgsqlParameter> parameters)
    {
        parameters = CreatePageParameters(request.Page, request.PageSize);
        parameters.Add(new NpgsqlParameter("signalDate", NpgsqlDbType.Date) { Value = (object?)request.Date?.Date ?? DBNull.Value });
        parameters.Add(new NpgsqlParameter("historyDays", NpgsqlDbType.Integer) { Value = request.HistoryDays });

        var where = new StringBuilder("WHERE TRUE");

        AddOptionalIntFilter(where, parameters, "m.article_id", "articleId", request.ArticleId);
        AddOptionalIntFilter(where, parameters, "lp.supplier_id", "supplierId", request.SupplierId);
        AddOptionalTextFilter(where, parameters, "lp.category", "category", request.Category);
        AddOptionalDecimalFloor(where, parameters, "m.dead_stock_risk", "minDeadStockRisk", request.MinDeadStockRisk);

        if (request.OnlyAtRisk)
            where.AppendLine("  AND (m.dead_stock_risk >= 0.5 OR m.low_stock_days > 0 OR m.stockout_days > 0)");

        var orderBy = BuildInventoryRiskOrderClause(request.SortBy, request.SortDir);

        return $"""
WITH latest_products AS (
    SELECT DISTINCT ON (pd."ProductId")
        pd."ProductId" AS article_id,
        COALESCE(NULLIF(BTRIM(pd."PLU"), ''), pd."ProductId"::text) AS sku,
        COALESCE(NULLIF(BTRIM(pd."ProductName"), ''), 'Unknown product') AS product_name,
        COALESCE(NULLIF(BTRIM(pd."Category"), ''), 'Uncategorized') AS category,
        pd."SupplierId" AS supplier_id
    FROM "ProductsDim" pd
    ORDER BY pd."ProductId", pd."Timestamp" DESC
),
latest_suppliers AS (
    SELECT DISTINCT ON (sd."SupplierId")
        sd."SupplierId" AS supplier_id,
        COALESCE(NULLIF(BTRIM(sd."Naziv"), ''), 'Unknown supplier') AS supplier_name
    FROM "SuppliersDim" sd
    ORDER BY sd."SupplierId", sd."UpdatedAt" DESC
),
bounds AS (
    SELECT
        COALESCE(@signalDate::date, (SELECT MAX(date) FROM analytics_intel.mv_inventory_risk_signals_v1_cache)) AS as_of_date,
        @historyDays::int AS history_days
),
filtered AS (
    SELECT
        m.article_id,
        COALESCE(lp.sku, m.article_id::text) AS sku,
        COALESCE(lp.product_name, 'Unknown product') AS product_name,
        COALESCE(lp.category, 'Uncategorized') AS category,
        lp.supplier_id,
        COALESCE(ls.supplier_name, 'Unknown supplier') AS supplier_name,
        m.date,
        m.stock_qty,
        m.avg_daily_sales_30d,
        m.days_of_cover,
        m.stock_turn,
        m.stockout_days,
        m.low_stock_days,
        m.dead_stock_risk
    FROM analytics_intel.mv_inventory_risk_signals_v1_cache m
    JOIN bounds b
      ON b.as_of_date IS NOT NULL
     AND m.date <= b.as_of_date
     AND m.date >= b.as_of_date - (b.history_days - 1)
    LEFT JOIN latest_products lp
      ON lp.article_id = m.article_id
    LEFT JOIN latest_suppliers ls
      ON ls.supplier_id = lp.supplier_id
    {where}
),
ranked AS (
    SELECT
        f.*,
        COUNT(*) OVER() AS total_count,
        MAX(f.date) OVER() AS snapshot_date
    FROM filtered f
)
SELECT
    article_id,
    sku,
    product_name,
    category,
    supplier_id,
    supplier_name,
    date,
    stock_qty,
    avg_daily_sales_30d,
    days_of_cover,
    stock_turn,
    stockout_days,
    low_stock_days,
    dead_stock_risk,
    total_count,
    snapshot_date
FROM ranked
ORDER BY {orderBy}
LIMIT @limit OFFSET @offset;
""";
    }

    private static string BuildPriceIntelligenceSql(PriceIntelligenceRequest request, out List<NpgsqlParameter> parameters)
    {
        parameters = CreatePageParameters(request.Page, request.PageSize);
        var where = new StringBuilder("WHERE TRUE");

        AddOptionalIntFilter(where, parameters, "m.article_id", "articleId", request.ArticleId);
        AddOptionalIntFilter(where, parameters, "lp.supplier_id", "supplierId", request.SupplierId);
        AddOptionalTextFilter(where, parameters, "m.category", "category", request.Category);
        AddOptionalTextFilter(where, parameters, "m.brand_key", "brandKey", request.BrandKey);
        AddOptionalDecimalFloor(where, parameters, "m.discount_depth", "minDiscountDepth", request.MinDiscountDepth);
        AddOptionalDecimalFloor(where, parameters, "m.margin_pct", "minMarginPct", request.MinMarginPct);

        var orderBy = BuildPriceIntelligenceOrderClause(request.SortBy, request.SortDir);

        return $"""
WITH latest_products AS (
    SELECT DISTINCT ON (pd."ProductId")
        pd."ProductId" AS article_id,
        COALESCE(NULLIF(BTRIM(pd."PLU"), ''), pd."ProductId"::text) AS sku,
        COALESCE(NULLIF(BTRIM(pd."ProductName"), ''), 'Unknown product') AS product_name,
        pd."SupplierId" AS supplier_id
    FROM "ProductsDim" pd
    ORDER BY pd."ProductId", pd."Timestamp" DESC
),
latest_suppliers AS (
    SELECT DISTINCT ON (sd."SupplierId")
        sd."SupplierId" AS supplier_id,
        COALESCE(NULLIF(BTRIM(sd."Naziv"), ''), 'Unknown supplier') AS supplier_name
    FROM "SuppliersDim" sd
    ORDER BY sd."SupplierId", sd."UpdatedAt" DESC
),
filtered AS (
    SELECT
        m.article_id,
        COALESCE(lp.sku, m.article_id::text) AS sku,
        COALESCE(lp.product_name, 'Unknown product') AS product_name,
        m.category,
        m.brand_key,
        lp.supplier_id,
        COALESCE(ls.supplier_name, 'Unknown supplier') AS supplier_name,
        m.price_date,
        m.net_price,
        m.list_price,
        m.cost,
        m.price_index_vs_category,
        m.price_index_vs_brand,
        m.discount_depth,
        m.margin_pct
    FROM analytics_intel.mv_price_intelligence_v1_cache m
    LEFT JOIN latest_products lp
      ON lp.article_id = m.article_id
    LEFT JOIN latest_suppliers ls
      ON ls.supplier_id = lp.supplier_id
    {where}
),
ranked AS (
    SELECT
        f.*,
        COUNT(*) OVER() AS total_count,
        MAX(f.price_date) OVER() AS snapshot_date
    FROM filtered f
)
SELECT
    article_id,
    sku,
    product_name,
    category,
    brand_key,
    supplier_id,
    supplier_name,
    price_date,
    net_price,
    list_price,
    cost,
    price_index_vs_category,
    price_index_vs_brand,
    discount_depth,
    margin_pct,
    total_count,
    snapshot_date
FROM ranked
ORDER BY {orderBy}
LIMIT @limit OFFSET @offset;
""";
    }

    private static string BuildTrendMomentumSql(TrendMomentumRequest request, out List<NpgsqlParameter> parameters)
    {
        parameters = CreatePageParameters(request.Page, request.PageSize);
        var where = new StringBuilder("WHERE TRUE");

        AddOptionalIntFilter(where, parameters, "m.article_id", "articleId", request.ArticleId);
        AddOptionalIntFilter(where, parameters, "lp.supplier_id", "supplierId", request.SupplierId);
        AddOptionalTextFilter(where, parameters, "lp.category", "category", request.Category);
        AddOptionalDecimalFloor(where, parameters, "m.external_trend_score", "minExternalTrendScore", request.MinExternalTrendScore);
        AddOptionalDecimalFloor(where, parameters, "m.local_sales_acceleration", "minLocalSalesAcceleration", request.MinLocalSalesAcceleration);

        var orderBy = BuildTrendMomentumOrderClause(request.SortBy, request.SortDir);

        return $"""
WITH latest_products AS (
    SELECT DISTINCT ON (pd."ProductId")
        pd."ProductId" AS article_id,
        COALESCE(NULLIF(BTRIM(pd."PLU"), ''), pd."ProductId"::text) AS sku,
        COALESCE(NULLIF(BTRIM(pd."ProductName"), ''), 'Unknown product') AS product_name,
        COALESCE(NULLIF(BTRIM(pd."Category"), ''), 'Uncategorized') AS category,
        pd."SupplierId" AS supplier_id
    FROM "ProductsDim" pd
    ORDER BY pd."ProductId", pd."Timestamp" DESC
),
latest_suppliers AS (
    SELECT DISTINCT ON (sd."SupplierId")
        sd."SupplierId" AS supplier_id,
        COALESCE(NULLIF(BTRIM(sd."Naziv"), ''), 'Unknown supplier') AS supplier_name
    FROM "SuppliersDim" sd
    ORDER BY sd."SupplierId", sd."UpdatedAt" DESC
),
filtered AS (
    SELECT
        m.article_id,
        COALESCE(lp.sku, m.article_id::text) AS sku,
        COALESCE(lp.product_name, 'Unknown product') AS product_name,
        COALESCE(lp.category, 'Uncategorized') AS category,
        lp.supplier_id,
        COALESCE(ls.supplier_name, 'Unknown supplier') AS supplier_name,
        m.signal_date,
        m.external_trend_score,
        m.local_sales_acceleration,
        m.trend_entropy
    FROM analytics_intel.mv_trend_momentum_v1_cache m
    LEFT JOIN latest_products lp
      ON lp.article_id = m.article_id
    LEFT JOIN latest_suppliers ls
      ON ls.supplier_id = lp.supplier_id
    {where}
),
ranked AS (
    SELECT
        f.*,
        COUNT(*) OVER() AS total_count,
        MAX(f.signal_date) OVER() AS snapshot_date
    FROM filtered f
)
SELECT
    article_id,
    sku,
    product_name,
    category,
    supplier_id,
    supplier_name,
    signal_date,
    external_trend_score,
    local_sales_acceleration,
    trend_entropy,
    total_count,
    snapshot_date
FROM ranked
ORDER BY {orderBy}
LIMIT @limit OFFSET @offset;
""";
    }

    private static List<NpgsqlParameter> CreatePageParameters(int page, int pageSize) =>
    [
        new("limit", NpgsqlDbType.Integer) { Value = pageSize },
        new("offset", NpgsqlDbType.Integer) { Value = (page - 1) * pageSize }
    ];

    private static void AddOptionalIntFilter(StringBuilder where, List<NpgsqlParameter> parameters, string sqlColumn, string parameterName, int? value)
    {
        if (!value.HasValue)
            return;
        where.AppendLine(string.Format(CultureInfo.InvariantCulture, "  AND {0} = @{1}", sqlColumn, parameterName));
        parameters.Add(new NpgsqlParameter(parameterName, NpgsqlDbType.Integer) { Value = value.Value });
    }

    private static void AddOptionalTextFilter(StringBuilder where, List<NpgsqlParameter> parameters, string sqlColumn, string parameterName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        where.AppendLine(string.Format(CultureInfo.InvariantCulture, "  AND LOWER(COALESCE({0}, '')) = LOWER(@{1})", sqlColumn, parameterName));
        parameters.Add(new NpgsqlParameter(parameterName, NpgsqlDbType.Text) { Value = value });
    }

    private static void AddOptionalDecimalFloor(StringBuilder where, List<NpgsqlParameter> parameters, string sqlColumn, string parameterName, decimal? value)
    {
        if (!value.HasValue)
            return;
        where.AppendLine(string.Format(CultureInfo.InvariantCulture, "  AND COALESCE({0}, 0) >= @{1}", sqlColumn, parameterName));
        parameters.Add(new NpgsqlParameter(parameterName, NpgsqlDbType.Numeric) { Value = value.Value });
    }

    private static string BuildDemandOrderClause(string? sortBy, string? sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy ?? "demandAcceleration") switch
        {
            "productName" => $"product_name {(desc ? "DESC" : "ASC")}, date DESC, article_id ASC, store_id ASC",
            "date" => $"date {(desc ? "DESC" : "ASC")}, sales_velocity DESC, article_id ASC, store_id ASC",
            "salesVelocity" => $"sales_velocity {(desc ? "DESC" : "ASC")}, demand_acceleration DESC, article_id ASC, store_id ASC",
            "daysSinceLastSale" => $"days_since_last_sale {(desc ? "DESC NULLS LAST" : "ASC NULLS LAST")}, sales_velocity DESC, article_id ASC, store_id ASC",
            "launchAgeDays" => $"launch_age_days {(desc ? "DESC" : "ASC")}, sales_velocity DESC, article_id ASC, store_id ASC",
            "storeCoverage" => $"store_coverage {(desc ? "DESC" : "ASC")}, sales_velocity DESC, article_id ASC, store_id ASC",
            "sourceRows" => $"source_rows {(desc ? "DESC" : "ASC")}, sales_velocity DESC, article_id ASC, store_id ASC",
            _ => $"demand_acceleration {(desc ? "DESC" : "ASC")}, sales_velocity DESC, article_id ASC, store_id ASC"
        };
    }

    private static string BuildInventoryRiskOrderClause(string? sortBy, string? sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy ?? "deadStockRisk") switch
        {
            "productName" => $"product_name {(desc ? "DESC" : "ASC")}, date DESC, article_id ASC",
            "date" => $"date {(desc ? "DESC" : "ASC")}, dead_stock_risk DESC, article_id ASC",
            "stockQty" => $"stock_qty {(desc ? "DESC" : "ASC")}, dead_stock_risk DESC, article_id ASC",
            "daysOfCover" => $"days_of_cover {(desc ? "DESC NULLS LAST" : "ASC NULLS LAST")}, dead_stock_risk DESC, article_id ASC",
            "stockTurn" => $"stock_turn {(desc ? "DESC NULLS LAST" : "ASC NULLS LAST")}, dead_stock_risk DESC, article_id ASC",
            "stockoutDays" => $"stockout_days {(desc ? "DESC" : "ASC")}, dead_stock_risk DESC, article_id ASC",
            "lowStockDays" => $"low_stock_days {(desc ? "DESC" : "ASC")}, dead_stock_risk DESC, article_id ASC",
            _ => $"dead_stock_risk {(desc ? "DESC" : "ASC")}, stockout_days DESC, article_id ASC"
        };
    }

    private static string BuildPriceIntelligenceOrderClause(string? sortBy, string? sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy ?? "marginPct") switch
        {
            "productName" => $"product_name {(desc ? "DESC" : "ASC")}, article_id ASC",
            "priceDate" => $"price_date {(desc ? "DESC" : "ASC")}, article_id ASC",
            "netPrice" => $"net_price {(desc ? "DESC" : "ASC")}, article_id ASC",
            "discountDepth" => $"discount_depth {(desc ? "DESC" : "ASC")}, margin_pct DESC NULLS LAST, article_id ASC",
            "priceIndexVsCategory" => $"price_index_vs_category {(desc ? "DESC NULLS LAST" : "ASC NULLS LAST")}, article_id ASC",
            "priceIndexVsBrand" => $"price_index_vs_brand {(desc ? "DESC NULLS LAST" : "ASC NULLS LAST")}, article_id ASC",
            _ => $"margin_pct {(desc ? "DESC NULLS LAST" : "ASC NULLS LAST")}, discount_depth DESC, article_id ASC"
        };
    }

    private static string BuildTrendMomentumOrderClause(string? sortBy, string? sortDir)
    {
        var desc = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
        return (sortBy ?? "externalTrendScore") switch
        {
            "productName" => $"product_name {(desc ? "DESC" : "ASC")}, article_id ASC",
            "signalDate" => $"signal_date {(desc ? "DESC" : "ASC")}, article_id ASC",
            "localSalesAcceleration" => $"local_sales_acceleration {(desc ? "DESC" : "ASC")}, external_trend_score DESC, article_id ASC",
            "trendEntropy" => $"trend_entropy {(desc ? "DESC" : "ASC")}, external_trend_score DESC, article_id ASC",
            _ => $"external_trend_score {(desc ? "DESC" : "ASC")}, local_sales_acceleration DESC, article_id ASC"
        };
    }

    private static bool TryNormalizePagination(
        int page,
        int pageSize,
        out int normalizedPage,
        out int normalizedPageSize,
        out Dictionary<string, string[]>? validationError)
    {
        normalizedPage = page <= 0 ? 1 : page;
        normalizedPageSize = pageSize <= 0 ? DefaultPageSize : pageSize;
        validationError = null;

        if (normalizedPageSize > MaxPageSize)
        {
            validationError = new Dictionary<string, string[]>
            {
                ["pageSize"] = [$"pageSize must be less than or equal to {MaxPageSize}."]
            };
            return false;
        }

        return true;
    }

    private static bool TryNormalizeHistoryDays(
        int historyDays,
        out int normalizedHistoryDays,
        out Dictionary<string, string[]>? validationError)
    {
        normalizedHistoryDays = historyDays <= 0 ? DefaultHistoryDays : historyDays;
        validationError = null;

        if (normalizedHistoryDays > MaxHistoryDays)
        {
            validationError = new Dictionary<string, string[]>
            {
                ["historyDays"] = [$"historyDays must be less than or equal to {MaxHistoryDays}."]
            };
            return false;
        }

        return true;
    }

    private static string GetAnalyticsConnectionString(IConfiguration configuration) =>
        AnalyticsConnectionResolver.Resolve(configuration);

    private static async Task<NpgsqlConnection> OpenConnectionAsync(string analyticsConnectionString, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(analyticsConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static async Task<DateTime?> TryGetLatestSnapshotDateAsync(
        NpgsqlConnection connection,
        string relationName,
        string dateColumn,
        CancellationToken ct)
    {
        try
        {
            var sql = $"SELECT MAX({dateColumn}) FROM {relationName};";
            await using var command = new NpgsqlCommand(sql, connection)
            {
                CommandTimeout = 30
            };

            var value = await command.ExecuteScalarAsync(ct);
            if (value is null || value is DBNull)
                return null;

            var date = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            return date.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
                : date.ToUniversalTime();
        }
        catch (PostgresException ex) when (IsIntelligenceRelationUnavailable(ex))
        {
            return null;
        }
    }

    private static bool IsIntelligenceRelationUnavailable(PostgresException ex) =>
        ex.SqlState is "42P01" or "42703";

    private static DateTime? NormalizeDate(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        var normalized = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();

        return normalized.Date;
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int GetInt32(IDataRecord record, string column) =>
        record.IsDBNull(record.GetOrdinal(column))
            ? 0
            : Convert.ToInt32(record.GetValue(record.GetOrdinal(column)), CultureInfo.InvariantCulture);

    private static int? GetNullableInt32(IDataRecord record, string column) =>
        record.IsDBNull(record.GetOrdinal(column))
            ? null
            : Convert.ToInt32(record.GetValue(record.GetOrdinal(column)), CultureInfo.InvariantCulture);

    private static decimal GetDecimal(IDataRecord record, string column) =>
        record.IsDBNull(record.GetOrdinal(column))
            ? 0m
            : Convert.ToDecimal(record.GetValue(record.GetOrdinal(column)), CultureInfo.InvariantCulture);

    private static decimal? GetNullableDecimal(IDataRecord record, string column) =>
        record.IsDBNull(record.GetOrdinal(column))
            ? null
            : Convert.ToDecimal(record.GetValue(record.GetOrdinal(column)), CultureInfo.InvariantCulture);

    private static string GetString(IDataRecord record, string column) =>
        record.IsDBNull(record.GetOrdinal(column))
            ? string.Empty
            : Convert.ToString(record.GetValue(record.GetOrdinal(column)), CultureInfo.InvariantCulture) ?? string.Empty;

    private static string? GetNullableString(IDataRecord record, string column) =>
        record.IsDBNull(record.GetOrdinal(column))
            ? null
            : Convert.ToString(record.GetValue(record.GetOrdinal(column)), CultureInfo.InvariantCulture);

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

public sealed record IntelligencePageResponse<T>(
    DateTime? AsOfDate,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<T> Items);

public sealed record DemandSignalsRequest(
    DateTime? Date,
    int HistoryDays,
    int? ArticleId,
    int? StoreId,
    int? SupplierId,
    string? Category,
    decimal? MinSalesVelocity,
    decimal? MinDemandAcceleration,
    int Page,
    int PageSize,
    string? SortBy,
    string? SortDir);

public sealed record DemandSignalItem(
    int ArticleId,
    string Sku,
    string ProductName,
    string Category,
    int? SupplierId,
    string SupplierName,
    int StoreId,
    string StoreName,
    string? StoreCity,
    DateTime Date,
    decimal SalesVelocity,
    decimal DemandAcceleration,
    int? DaysSinceLastSale,
    int LaunchAgeDays,
    int StoreCoverage,
    int SourceRows);

public sealed record InventoryRiskSignalsRequest(
    DateTime? Date,
    int HistoryDays,
    int? ArticleId,
    int? SupplierId,
    string? Category,
    decimal? MinDeadStockRisk,
    bool OnlyAtRisk,
    int Page,
    int PageSize,
    string? SortBy,
    string? SortDir);

public sealed record InventoryRiskSignalItem(
    int ArticleId,
    string Sku,
    string ProductName,
    string Category,
    int? SupplierId,
    string SupplierName,
    DateTime Date,
    decimal StockQty,
    decimal AvgDailySales30d,
    decimal? DaysOfCover,
    decimal? StockTurn,
    int StockoutDays,
    int LowStockDays,
    decimal DeadStockRisk);

public sealed record PriceIntelligenceRequest(
    int? ArticleId,
    int? SupplierId,
    string? Category,
    string? BrandKey,
    decimal? MinDiscountDepth,
    decimal? MinMarginPct,
    int Page,
    int PageSize,
    string? SortBy,
    string? SortDir);

public sealed record PriceIntelligenceItem(
    int ArticleId,
    string Sku,
    string ProductName,
    string Category,
    string BrandKey,
    int? SupplierId,
    string SupplierName,
    DateTime PriceDate,
    decimal NetPrice,
    decimal ListPrice,
    decimal Cost,
    decimal? PriceIndexVsCategory,
    decimal? PriceIndexVsBrand,
    decimal DiscountDepth,
    decimal? MarginPct);

public sealed record TrendMomentumRequest(
    int? ArticleId,
    int? SupplierId,
    string? Category,
    decimal? MinExternalTrendScore,
    decimal? MinLocalSalesAcceleration,
    int Page,
    int PageSize,
    string? SortBy,
    string? SortDir);

public sealed record TrendMomentumItem(
    int ArticleId,
    string Sku,
    string ProductName,
    string Category,
    int? SupplierId,
    string SupplierName,
    DateTime SignalDate,
    decimal ExternalTrendScore,
    decimal LocalSalesAcceleration,
    decimal TrendEntropy);
