using Application.Analytics.Queries.GetInventoryStatus;
using Application.Analytics.Queries.GetSalesSummary;
using Application.Analytics.Queries.GetInventoryForecast;
using Application.Analytics.Queries.GetForecastBaselineBacktest;
using Application.Analytics.Queries.GetInventoryAlerts;
using Application.Analytics.Queries.GetInventorySizeCurve;
using Application.Analytics.Queries.GetRebalanceSuggestions;
using Application.Analytics.Queries.GetTopProducts;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Infrastructure.Services.Caching;
using Infrastructure.Services.Analytics;
using MediatR;
using Domain.Model;
using Domain.Model.Analytics;
using Trendplus2.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Diagnostics;
using System.Globalization;

namespace Trendplus2.Endpoints;

/// <summary>
/// Analytics endpointi sa hibridnim caching-om.
/// Cache smanjuje opterećenje baze i ubrzava response za 10-100x.
/// </summary>
public static class CachedAnalyticsEndpoints
{
    private const int MovementStatsBatchSize = 5_000;
    private static readonly TimeSpan DashboardSectionTtl = CacheExpiration.Medium;
    private static readonly TimeSpan DashboardFastSectionTtl = CacheExpiration.Short;
    private static readonly TimeSpan DashboardReferenceSectionTtl = CacheExpiration.Long;

    private static readonly string[] SerbianDayNames =
    {
        "Nedelja",
        "Ponedeljak",
        "Utorak",
        "Sreda",
        "Četvrtak",
        "Petak",
        "Subota"
    };

    public static void MapCachedAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics/cached")
            .WithTags("Analytics (Cached)")
            .RequireRateLimiting("analytics");

        // ========== SALES SUMMARY (CACHED) ==========
        group.MapGet("/sales/summary", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext trendDb,
            IMediator mediator,
            ILoggerFactory loggerFactory,
            HttpContext httpContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            {
                return Results.BadRequest(new
                {
                    message = "Neispravan period: fromDate mora biti manji ili jednak toDate.",
                    fromDate = fromDate.Value,
                    toDate = toDate.Value
                });
            }

            var cacheKey = AnalyticsCacheKeys.SalesSummary(fromDate, toDate, storeId, supplierId);

            try
            {
                var cacheResult = await GetOrSetWithPolicyAsync(
                    cache,
                    cacheKey,
                    AnalyticsCachePolicy.DashboardFamily,
                    AnalyticsCachePolicy.DashboardBootstrap,
                    async () =>
                    {
                        if (!storeId.HasValue && !supplierId.HasValue)
                        {
                            var aggregated = await TryGetSalesSummaryFromAggregatesAsync(trendDb, fromDate, toDate, ct);
                            if (aggregated is not null)
                            {
                                return aggregated;
                            }
                        }

                        var baseQuery = from p in trendDb.ProdajaZaglavlja.AsNoTracking()
                                        join ps in trendDb.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                                        join a in trendDb.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                                              (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                                              (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                                        group ps by p.Id into g
                                        select new
                                        {
                                            TotalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                                            TotalUnits = g.Sum(x => x.Kolicina),
                                            TransactionCount = g.Key
                                        };

                        var aggregatedResult = await baseQuery.ToListAsync(ct);

                        var totalRevenue = aggregatedResult.Sum(x => x.TotalRevenue);
                        var totalUnits = aggregatedResult.Sum(x => x.TotalUnits);
                        var totalTransactions = aggregatedResult.Count;
                        var avgBasket = totalTransactions > 0 ? totalRevenue / totalTransactions : 0m;
                        var avgItem = totalUnits > 0 ? totalRevenue / totalUnits : 0m;

                        return new SalesSummaryDto(totalRevenue, totalTransactions, totalUnits, avgBasket, avgItem);
                    },
                    ct,
                    loggerFactory: loggerFactory,
                    routeName: "sales.summary");
                var result = cacheResult.Value;

                var correlationId = ResolveCorrelationId(httpContext);
                var meta = result.TotalTransactions == 0
                    ? AnalyticsResponseMetaFactory.Empty("no_data_in_period", "Nema prodaje za izabrani period.")
                    : AnalyticsResponseMetaFactory.Success();
                ApplyStaleCacheWarning(meta, cacheResult.Metadata, AnalyticsCachePolicy.DashboardBootstrap);
                meta.CorrelationId = correlationId;

                return Results.Ok(new
                {
                    result.TotalRevenue,
                    result.TotalTransactions,
                    result.TotalUnits,
                    result.AvgBasketValue,
                    result.AvgItemPrice,
                    Meta = meta
                });
            }
            catch (PostgresException ex) when (ex.SqlState is "42P01" or "42703")
            {
                var meta = AnalyticsResponseMetaFactory.Error(
                    "missing_table",
                    "Prodajni sažetak trenutno nije dostupan zbog nedostajuće analitičke relacije.",
                    ResolveCorrelationId(httpContext));
                return Results.Ok(new
                {
                    TotalRevenue = 0m,
                    TotalTransactions = 0,
                    TotalUnits = 0,
                    AvgBasketValue = 0m,
                    AvgItemPrice = 0m,
                    Meta = meta
                });
            }
            catch (TimeoutException)
            {
                var meta = AnalyticsResponseMetaFactory.Error(
                    "sql_timeout",
                    "Prodajni sažetak trenutno nije dostupan zbog isteka vremena.",
                    ResolveCorrelationId(httpContext));
                return Results.Ok(new
                {
                    TotalRevenue = 0m,
                    TotalTransactions = 0,
                    TotalUnits = 0,
                    AvgBasketValue = 0m,
                    AvgItemPrice = 0m,
                    Meta = meta
                });
            }
            catch (NpgsqlException)
            {
                var meta = AnalyticsResponseMetaFactory.Error(
                    "analytics_db_unavailable",
                    "Prodajni sažetak trenutno nije dostupan zbog greške baze.",
                    ResolveCorrelationId(httpContext));
                return Results.Ok(new
                {
                    TotalRevenue = 0m,
                    TotalTransactions = 0,
                    TotalUnits = 0,
                    AvgBasketValue = 0m,
                    AvgItemPrice = 0m,
                    Meta = meta
                });
            }
        });

        // ========== TOP PRODUCTS (CACHED) ==========
        group.MapGet("/sales/top-products", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext trendDb,
            IMediator mediator,
            ILoggerFactory loggerFactory,
            HttpContext httpContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int top = 20,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            {
                return Results.BadRequest(new
                {
                    message = "Neispravan period: fromDate mora biti manji ili jednak toDate.",
                    fromDate = fromDate.Value,
                    toDate = toDate.Value
                });
            }

            var cacheKey = AnalyticsCacheKeys.TopProducts(top, fromDate, toDate, storeId, supplierId);

            try
            {
                var cacheResult = await GetOrSetWithPolicyAsync(
                    cache,
                    cacheKey,
                    AnalyticsCachePolicy.DashboardFamily,
                    AnalyticsCachePolicy.DashboardBootstrap,
                    async () =>
                    {
                        if (!storeId.HasValue && !supplierId.HasValue)
                        {
                            var aggregated = await TryGetTopProductsFromAggregatesAsync(trendDb, top, fromDate, toDate, ct);
                            if (aggregated is not null)
                            {
                                return aggregated;
                            }
                        }

                        var aggregatedRows = await (
                            from ps in trendDb.ProdajaStavke.AsNoTracking()
                            join p in trendDb.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals p.Id
                            join a in trendDb.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                            where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                  (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                                  (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                                  (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                            group new { ps, a } by new { ps.IdArtikal, a.Naziv, a.Velicina, a.Boja } into g
                            select new
                            {
                                g.Key.IdArtikal,
                                g.Key.Naziv,
                                TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                                TotalUnits = g.Sum(x => x.ps.Kolicina),
                                g.Key.Velicina,
                                g.Key.Boja
                            })
                            .ToListAsync(ct);

                        // Rank independently in-memory so InMemory and relational hosts share one contract.
                        // Avoid re-ordering a DTO-projected IQueryable (EF cannot translate that composition).
                        var topRevenue = aggregatedRows
                            .OrderByDescending(x => x.TotalRevenue)
                            .Take(top)
                            .Select(x => new TopProductDto(
                                x.IdArtikal,
                                x.Naziv ?? string.Empty,
                                x.TotalRevenue,
                                x.TotalUnits,
                                x.Velicina,
                                x.Boja))
                            .ToList();

                        var topUnits = aggregatedRows
                            .OrderByDescending(x => x.TotalUnits)
                            .Take(top)
                            .Select(x => new TopProductDto(
                                x.IdArtikal,
                                x.Naziv ?? string.Empty,
                                x.TotalRevenue,
                                x.TotalUnits,
                                x.Velicina,
                                x.Boja))
                            .ToList();

                        return new TopProductsResult(topRevenue, topUnits);
                    },
                    ct,
                    loggerFactory: loggerFactory,
                    routeName: "sales.top-products");
                var result = cacheResult.Value;

                var correlationId = ResolveCorrelationId(httpContext);
                var isEmpty = result.ByRevenue.Count == 0 && result.ByUnits.Count == 0;
                var meta = isEmpty
                    ? AnalyticsResponseMetaFactory.Empty("no_data_in_period", "Nema prodaje za izabrani period.")
                    : AnalyticsResponseMetaFactory.Success();
                ApplyStaleCacheWarning(meta, cacheResult.Metadata, AnalyticsCachePolicy.DashboardBootstrap);
                meta.CorrelationId = correlationId;

                return Results.Ok(new
                {
                    result.ByRevenue,
                    result.ByUnits,
                    Meta = meta
                });
            }
            catch (PostgresException ex) when (ex.SqlState is "42P01" or "42703")
            {
                var meta = AnalyticsResponseMetaFactory.Error(
                    "missing_table",
                    "Top proizvodi trenutno nisu dostupni zbog nedostajuce analiticke relacije.",
                    ResolveCorrelationId(httpContext));
                return Results.Ok(new
                {
                    ByRevenue = Array.Empty<TopProductDto>(),
                    ByUnits = Array.Empty<TopProductDto>(),
                    Meta = meta
                });
            }
            catch (TimeoutException)
            {
                var meta = AnalyticsResponseMetaFactory.Error(
                    "sql_timeout",
                    "Top proizvodi trenutno nisu dostupni zbog isteka vremena.",
                    ResolveCorrelationId(httpContext));
                return Results.Ok(new
                {
                    ByRevenue = Array.Empty<TopProductDto>(),
                    ByUnits = Array.Empty<TopProductDto>(),
                    Meta = meta
                });
            }
            catch (NpgsqlException)
            {
                var meta = AnalyticsResponseMetaFactory.Error(
                    "analytics_db_unavailable",
                    "Top proizvodi trenutno nisu dostupni zbog greske baze.",
                    ResolveCorrelationId(httpContext));
                return Results.Ok(new
                {
                    ByRevenue = Array.Empty<TopProductDto>(),
                    ByUnits = Array.Empty<TopProductDto>(),
                    Meta = meta
                });
            }
        });

        // ========== TOP PRODUCTS ADVANCED (CACHED) ==========
        group.MapGet("/sales/top-products-advanced", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int top = 10,
            int? storeId = null,
            int? supplierId = null,
            string dataScope = "all",
            CancellationToken ct = default) =>
        {
            var normalizedDataScope = NormalizeDataScope(dataScope);
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.TopProductsAdvanced(top, fromDate, toDate, storeId, supplierId, normalizedDataScope);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () => await GetTopProductsAdvancedSnapshotAsync(db, top, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                DashboardFastSectionTtl,
                ct);

            return Results.Ok(result);
        });

        // ========== INVENTORY STATUS (CACHED) ==========
        group.MapGet("/inventory/status", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext trendDb,
            IMediator mediator,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            int lowStockThreshold = 2,
            CancellationToken ct = default) =>
        {
            var correlationId = ResolveCorrelationId(httpContext);
            var logger = loggerFactory.CreateLogger("CachedAnalyticsEndpoints.InventoryStatus");
            try
            {
                var cacheKey = AnalyticsCacheKeys.Inventory(lowStockThreshold);

                var result = await cache.GetOrSetAsync(
                    cacheKey,
                    async () =>
                    {
                        try
                        {
                            return await mediator.Send(new GetInventoryStatusQuery(lowStockThreshold), ct);
                        }
                        catch (Exception ex) when (IsMissingRelation(ex))
                        {
                            var inventoryData = await trendDb.Artikli.AsNoTracking()
                                .GroupBy(a => 1)
                                .Select(g => new
                                {
                                    TotalSku = g.Count(),
                                    TotalOnHand = g.Sum(x => (int?)x.Kolicina) ?? 0,
                                    OutOfStock = g.Count(x => (x.Kolicina ?? 0) == 0),
                                    LowStock = g.Count(x => (x.Kolicina ?? 0) > 0 && (x.Kolicina ?? 0) <= lowStockThreshold)
                                })
                                .SingleOrDefaultAsync(ct);

                            return new InventoryStatusDto(
                                inventoryData?.TotalSku ?? 0,
                                inventoryData?.TotalOnHand ?? 0,
                                inventoryData?.LowStock ?? 0,
                                inventoryData?.OutOfStock ?? 0,
                                UsedOperationalFallback: true
                            );
                        }
                    },
                    AnalyticsCachePolicy.Inventory.Ttl,
                    ct);

                var meta = result.TotalSkuCount == 0
                    ? AnalyticsResponseMetaFactory.Empty("no_inventory_data", "Nema podataka o zalihama.")
                    : result.UsedOperationalFallback
                        ? AnalyticsResponseMetaFactory.Warning(
                            "inventory_status_operational_fallback",
                            "Status zaliha je učitan iz operativne tabele Artikli jer analytics relacija nije dostupna.",
                            "warning")
                        : AnalyticsResponseMetaFactory.Success();
                meta.CorrelationId = correlationId;

                return Results.Ok(new
                {
                    result.TotalSkuCount,
                    result.TotalOnHand,
                    result.LowStockCount,
                    result.OutOfStockCount,
                    result.UsedOperationalFallback,
                    Meta = meta
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error loading cached inventory status.");
                return Results.Ok(new
                {
                    TotalSkuCount = 0, TotalOnHand = 0, LowStockCount = 0, OutOfStockCount = 0,
                    Meta = AnalyticsResponseMetaFactory.Error("inventory_status_error", "Status zaliha trenutno nije dostupan.", correlationId)
                });
            }
        });

        // ========== INVENTORY BALANCE (CACHED) ==========
        group.MapGet("/inventory/balance", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            var logger = loggerFactory.CreateLogger("CachedAnalyticsEndpoints");
            var correlationId = ResolveCorrelationId(httpContext);
            var cacheKey = $"analytics:inventory:balance:{storeId}:{supplierId}";
            try
            {
                var result = await cache.GetOrSetAsync(
                    cacheKey,
                    async () =>
                    {
                        var query = db.Artikli.AsNoTracking().AsQueryable();

                        if (storeId.HasValue)
                            query = query.Where(a => a.IDObjekat == storeId.Value);
                        if (supplierId.HasValue)
                            query = query.Where(a => a.IDDobavljac == supplierId.Value);

                        var totalSku = await query.CountAsync(ct);
                        var totalOnHand = await query.SumAsync(a => (int?)((a.Kolicina ?? 0) > 0 ? (a.Kolicina ?? 0) : 0), ct) ?? 0;
                        var lowStock = await query.CountAsync(a => (a.Kolicina ?? 0) > 0 && (a.Kolicina ?? 0) <= (a.MinimalnaKolicina ?? 0), ct);
                        var outOfStock = await query.CountAsync(a => (a.Kolicina ?? 0) <= 0, ct);
                        var estimatedValue = await query.SumAsync(a => (decimal?)((a.NabavnaCena ?? 0m) * ((a.Kolicina ?? 0) > 0 ? (a.Kolicina ?? 0) : 0)), ct) ?? 0m;
                        var meta = totalSku == 0
                            ? AnalyticsResponseMetaFactory.Empty("no_inventory_data", "Nema podataka o zalihama.")
                            : AnalyticsResponseMetaFactory.Success();
                        meta.CorrelationId = correlationId;

                        return new InventoryBalanceDto((int)totalSku, (int)totalOnHand, (int)lowStock, (int)outOfStock, Math.Round(estimatedValue, 2), meta);
                    },
                    AnalyticsCachePolicy.Inventory.Ttl,
                    ct);

                var insightsMeta = result.Meta ?? AnalyticsResponseMetaFactory.Success();
                insightsMeta.CorrelationId = correlationId;
                return Results.Ok(result with { Meta = insightsMeta });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
            catch (NpgsqlException ex)
            {
                logger.LogWarning(ex, "Cached inventory balance query failed due to database issue.");
                return Results.Ok(new InventoryBalanceDto(
                    0, 0, 0, 0, 0m,
                    AnalyticsResponseMetaFactory.Error("inventory_cached_balance_db_error", "Zalihe trenutno nisu dostupne.", correlationId)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error loading cached inventory balance.");
                return Results.Ok(new InventoryBalanceDto(
                    0, 0, 0, 0, 0m,
                    AnalyticsResponseMetaFactory.Error("inventory_cached_balance_error", "Neocekivana greska pri ucitavanju zaliha.", correlationId)));
            }
        });

        // ========== INVENTORY LIST (CACHED) ==========
        group.MapGet("/inventory/list", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            int page = 1,
            int pageSize = 50,
            int? storeId = null,
            int? supplierId = null,
            string? dataScope = null,
            string? search = null,
            string? sortBy = null,
            CancellationToken ct = default) =>
        {
            var logger = loggerFactory.CreateLogger("CachedAnalyticsEndpoints");
            var correlationId = ResolveCorrelationId(httpContext);
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 1000);
            var normalizedDataScope = NormalizeDataScope(dataScope);

            var cacheKey = $"analytics:inventory:list:{page}:{pageSize}:{storeId}:{supplierId}:{normalizedDataScope}:{search}:{sortBy}";
            try
            {
                var paged = await cache.GetOrSetAsync(
                    cacheKey,
                    async () =>
                    {
                        var query = db.Artikli.AsNoTracking().AsQueryable();

                        if (storeId.HasValue)
                            query = query.Where(a => a.IDObjekat == storeId.Value);
                        if (supplierId.HasValue)
                            query = query.Where(a => a.IDDobavljac == supplierId.Value);
                        if (!string.IsNullOrWhiteSpace(search))
                            query = query.Where(a => (a.Naziv ?? "").Contains(search) || (a.PLU ?? "").Contains(search));

                        query = sortBy?.ToLowerInvariant() switch
                        {
                            "kolicina" => query.OrderByDescending(a => a.Kolicina),
                            "naziv" => query.OrderBy(a => a.Naziv),
                            "vrednost" => query.OrderByDescending(a => (a.NabavnaCena ?? 0m) * ((a.Kolicina ?? 0) > 0 ? (a.Kolicina ?? 0) : 0)).ThenBy(a => a.Naziv),
                            "azuriranje" => query.OrderByDescending(a => a.UpdatedAt).ThenBy(a => a.Naziv),
                            _ => query.OrderByDescending(a => (a.Kolicina ?? 0))
                        };

                        var total = await query.CountAsync(ct);
                        var rawItems = await query
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .Select(a => new
                            {
                                a.Id,
                                a.PLU,
                                Naziv = a.Naziv ?? string.Empty,
                                a.Kolicina,
                                a.MinimalnaKolicina,
                                a.NabavnaCena,
                                a.IDObjekat,
                                a.IDDobavljac
                            })
                            .ToListAsync(ct);

                        var articleIds = rawItems.Select(item => item.Id).ToArray();
                        var salesFromDate = DateTime.UtcNow.AddDays(-30);
                        var soldUnitsByArticle = await (
                            from pz in db.ProdajaZaglavlja.AsNoTracking()
                            join ps in db.ProdajaStavke.AsNoTracking() on pz.Id equals ps.IdProdaja
                            where articleIds.Contains(ps.IdArtikal)
                                  && pz.DatumProdaje >= salesFromDate
                                  && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
                            group ps by ps.IdArtikal
                            into g
                            select new
                            {
                                ProductId = g.Key,
                                UnitsSold = g.Sum(x => x.Kolicina)
                            })
                            .ToDictionaryAsync(x => x.ProductId, x => x.UnitsSold, ct);

                        var movementWindowStatsByArticle = await LoadInventorySignalWindowStatsFromJournalAsync(
                            db,
                            articleIds,
                            storeId,
                            salesFromDate,
                            DateTime.UtcNow,
                            normalizedDataScope,
                            ct);

                        var items = new List<InventoryListItemDto>(rawItems.Count);
                        foreach (var item in rawItems)
                        {
                            var quantity = item.Kolicina ?? 0;
                            var soldUnits30d = soldUnitsByArticle.TryGetValue(item.Id, out var units) ? units : 0;
                            var movementWindowStats = movementWindowStatsByArticle.TryGetValue(item.Id, out var stats)
                                ? stats
                                : new InventorySignalWindowStats(0, 0);
                            var openingStockUnits = Math.Max(quantity - movementWindowStats.NetMovementUnits, 0);
                            var hasReliableSellThroughInputs = openingStockUnits > 0 || movementWindowStats.InboundUnits > 0;
                            var avgDailySalesUnits = Math.Round(soldUnits30d / 30m, 4, MidpointRounding.AwayFromZero);
                            var hasSufficientData = soldUnits30d > 0 || quantity > 0 || hasReliableSellThroughInputs;
                            var signalDataQuality = soldUnits30d > 0 && hasReliableSellThroughInputs
                                ? "good"
                                : hasSufficientData
                                    ? "warning"
                                    : "insufficient_data";

                            var signal = InventorySignalCalculator.Calculate(
                                currentOnHandUnits: quantity,
                                avgDailySalesUnits: avgDailySalesUnits,
                                soldUnits: soldUnits30d,
                                openingStockUnits: openingStockUnits,
                                inboundUnits: movementWindowStats.InboundUnits,
                                dataQualityStatus: signalDataQuality,
                                hasSufficientData: hasSufficientData);

                            var reasonCodes = signal.ReasonCodes.ToList();
                            if (signal.StockCoverStatus == InventorySignalCalculator.StockCoverOutOfStockRisk)
                            {
                                reasonCodes.Add("replenish_needed");
                            }

                            if (signal.StockCoverStatus is InventorySignalCalculator.StockCoverSlowStock or InventorySignalCalculator.StockCoverNoVelocity)
                            {
                                reasonCodes.Add("slow_stock");
                            }

                            items.Add(new InventoryListItemDto(
                                item.Id,
                                item.PLU,
                                item.Naziv,
                                item.Kolicina,
                                item.MinimalnaKolicina,
                                item.NabavnaCena,
                                (item.NabavnaCena ?? 0m) * (quantity > 0 ? quantity : 0),
                                item.IDObjekat,
                                item.IDDobavljac,
                                signal.StockCoverDays,
                                signal.StockCoverStatus,
                                signal.StockCoverStatusLabel,
                                signal.SellThroughRatio,
                                signal.SellThroughStatus,
                                signal.SellThroughStatusLabel,
                                signal.SignalConfidencePct,
                                signal.RecommendationAllowed,
                                reasonCodes,
                                signalDataQuality));
                        }

                        var meta = total == 0
                            ? AnalyticsResponseMetaFactory.Empty("no_inventory_items", "Nema artikala koji odgovaraju filterima.")
                            : AnalyticsResponseMetaFactory.Success();
                        meta.CorrelationId = correlationId;

                        return new ArtikliPagedResponse<InventoryListItemDto>(items, total, page, pageSize, meta);
                    },
                    AnalyticsCachePolicy.Inventory.Ttl,
                    ct);

                var listMeta = paged.Meta ?? AnalyticsResponseMetaFactory.Success();
                listMeta.CorrelationId = correlationId;
                return Results.Ok(paged with { Meta = listMeta });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
            catch (NpgsqlException ex)
            {
                logger.LogWarning(ex, "Cached inventory list query failed due to database issue.");
                return Results.Ok(new ArtikliPagedResponse<InventoryListItemDto>(
                    [],
                    0,
                    page,
                    pageSize,
                    AnalyticsResponseMetaFactory.Error("inventory_cached_list_db_error", "Lista artikala trenutno nije dostupna.", correlationId)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error loading cached inventory list.");
                return Results.Ok(new ArtikliPagedResponse<InventoryListItemDto>(
                    [],
                    0,
                    page,
                    pageSize,
                    AnalyticsResponseMetaFactory.Error("inventory_cached_list_error", "Neocekivana greska pri ucitavanju liste artikala.", correlationId)));
            }
        });

        // ========== INVENTORY INSIGHTS (CACHED) ==========
        group.MapGet("/inventory/insights", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            IAnalyticsDbContext analyticsDb,
            HttpContext httpContext,
            ILoggerFactory loggerFactory,
            int? storeId = null,
            int? supplierId = null,
            string? search = null,
            string? sortBy = null,
            string? dataScope = null,
            CancellationToken ct = default) =>
        {
            var correlationId = ResolveCorrelationId(httpContext);
            var logger = loggerFactory.CreateLogger("CachedAnalyticsEndpoints.InventoryInsights");
            try
            {
                var cacheKey = AnalyticsCacheKeys.InventoryInsights(storeId, supplierId, search, sortBy, dataScope);

                var result = await cache.GetOrSetAsync(
                    cacheKey,
                    async () => await InventoryEndpoints.GetInventoryInsightsAsync(cache, db, analyticsDb, storeId, supplierId, search, sortBy, ct, dataScope),
                    AnalyticsCachePolicy.Inventory.Ttl,
                    ct);

                var balanceMeta = result.Meta ?? AnalyticsResponseMetaFactory.Success();
                balanceMeta.CorrelationId = correlationId;
                return Results.Ok(result with { Meta = balanceMeta });
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
            catch (NpgsqlException ex)
            {
                logger.LogWarning(ex, "Cached inventory insights query failed due to database issue.");
                return Results.Ok(new InventoryInsightsDto(
                    0, 0m, [], [], [], [],
                    AnalyticsResponseMetaFactory.Error("inventory_cached_insights_db_error", "Inventory uvidi trenutno nisu dostupni.", correlationId)));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error loading cached inventory insights.");
                return Results.Ok(new InventoryInsightsDto(
                    0, 0m, [], [], [], [],
                    AnalyticsResponseMetaFactory.Error("inventory_cached_insights_error", "Neocekivana greska pri ucitavanju inventory uvida.", correlationId)));
            }
        });

        // ========== INVENTORY STORE COMPARISON (CACHED) ==========
        group.MapGet("/inventory/store-comparison", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            IAnalyticsDbContext analyticsDb,
            HttpContext httpContext,
            int[]? compareStoreIds,
            int? supplierId,
            string? search,
            string? dataScope,
            CancellationToken ct) =>
        {
            var normalizedCompareStoreIds = (compareStoreIds ?? [])
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
            var effectiveCompareStoreIds = normalizedCompareStoreIds.Length == 0 ? null : normalizedCompareStoreIds;
            var cacheKey = AnalyticsCacheKeys.InventoryStoreComparison(normalizedCompareStoreIds, supplierId, search, dataScope);

            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () => await InventoryEndpoints.GetInventoryStoreComparisonAsync(cache, db, analyticsDb, effectiveCompareStoreIds, supplierId, search, ct, dataScope),
                AnalyticsCachePolicy.Inventory.Ttl,
                ct);

            var storeCmpMeta = result.Meta ?? AnalyticsResponseMetaFactory.Success();
            storeCmpMeta.CorrelationId = ResolveCorrelationId(httpContext);
            return Results.Ok(result with { Meta = storeCmpMeta });
        });

        // ========== INVENTORY FORECAST (CACHED) ==========
        group.MapGet("/inventory/forecast", async (
            IAnalyticsCacheService cache,
            IMediator mediator,
            int? storeId = null,
            int? supplierId = null,
            int? skuId = null,
            string? sizeCode = null,
            int top = 200,
            CancellationToken ct = default) =>
        {
            top = Math.Clamp(top, 1, 500);
            var cacheKey = AnalyticsCacheKeys.InventoryForecast(storeId, supplierId, skuId, sizeCode, top);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () => await mediator.Send(new GetInventoryForecastQuery(storeId, supplierId, skuId, sizeCode, top), ct),
                AnalyticsCachePolicy.Inventory.Ttl,
                ct);

            return Results.Ok(result);
        });

        // ========== FORECAST BASELINE / BACKTEST CONTRACT (CACHED, FAIL-CLOSED) ==========
        group.MapGet("/inventory/forecast/backtest", async (
            IAnalyticsCacheService cache,
            IMediator mediator,
            int? storeId = null,
            int? supplierId = null,
            int horizonDays = 14,
            CancellationToken ct = default) =>
        {
            var cacheKey = AnalyticsCacheKeys.InventoryForecastBacktest(storeId, supplierId, horizonDays);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () => await mediator.Send(new GetForecastBaselineBacktestQuery(storeId, supplierId, horizonDays), ct),
                AnalyticsCachePolicy.Inventory.Ttl,
                ct);

            return Results.Ok(result);
        });

        // ========== INVENTORY SIZE CURVE (CACHED) ==========
        group.MapGet("/inventory/size-curve", async (
            IAnalyticsCacheService cache,
            IMediator mediator,
            int? storeId = null,
            int? supplierId = null,
            int? skuId = null,
            int top = 200,
            CancellationToken ct = default) =>
        {
            top = Math.Clamp(top, 1, 500);
            var cacheKey = AnalyticsCacheKeys.InventorySizeCurve(storeId, supplierId, skuId, top);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () => await mediator.Send(new GetInventorySizeCurveQuery(storeId, supplierId, skuId, top), ct),
                AnalyticsCachePolicy.Inventory.Ttl,
                ct);

            return Results.Ok(result);
        });

        // ========== REBALANCE SUGGESTIONS (CACHED) ==========
        group.MapGet("/inventory/rebalance-suggestions", async (
            IAnalyticsCacheService cache,
            IMediator mediator,
            int? fromStoreId = null,
            int? toStoreId = null,
            int? supplierId = null,
            string? urgency = null,
            int top = 100,
            CancellationToken ct = default) =>
        {
            top = Math.Clamp(top, 1, 500);
            var cacheKey = AnalyticsCacheKeys.RebalanceSuggestions(fromStoreId, toStoreId, supplierId, urgency, top);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () => await mediator.Send(new GetRebalanceSuggestionsQuery(fromStoreId, toStoreId, supplierId, urgency, top), ct),
                AnalyticsCachePolicy.Inventory.Ttl,
                ct);

            return Results.Ok(result);
        });

        // ========== INVENTORY ALERTS (CACHED) ==========
        group.MapGet("/inventory/alerts", async (
            IAnalyticsCacheService cache,
            IMediator mediator,
            int? storeId = null,
            int? supplierId = null,
            string? severity = null,
            int top = 100,
            CancellationToken ct = default) =>
        {
            top = Math.Clamp(top, 1, 500);
            var cacheKey = AnalyticsCacheKeys.InventoryAlerts(storeId, supplierId, severity, top);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () => await mediator.Send(new GetInventoryAlertsQuery(storeId, supplierId, severity, top), ct),
                AnalyticsCachePolicy.Inventory.Ttl,
                ct);

            return Results.Ok(result);
        });

        // ========== DAILY SALES (CACHED) ==========
        group.MapGet("/sales/daily", async (
            IAnalyticsCacheService cache,
            IAnalyticsDbContext db,
            ITrendplusDbContext trendDb,
            HttpContext httpContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.DailySales(fromDate, toDate, storeId, supplierId) + ":meta-v1";
            var snapshot = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    if (!storeId.HasValue && !supplierId.HasValue)
                    {
                        var aggregatedDaily = await TryGetDailySalesFromAggregatesAsync(trendDb, fromDate, toDate, ct);
                        if (aggregatedDaily is not null && aggregatedDaily.Count > 0)
                        {
                            return new DailySalesCachedSnapshot { Items = aggregatedDaily };
                        }
                    }

                    var usedOperationalFallback = false;
                    try
                    {
                        if (!supplierId.HasValue)
                        {
                            var query = db.SalesFacts.AsNoTracking();

                            if (fromDate.HasValue)
                                query = query.Where(s => s.SaleTimestampUtc >= fromDate.Value);

                            if (toDate.HasValue)
                                query = query.Where(s => s.SaleTimestampUtc <= toDate.Value);

                            if (storeId.HasValue)
                                query = query.Where(s => s.StoreId == storeId.Value);

                            var dailySalesRaw = await query
                                .GroupBy(s => s.SaleTimestampUtc.Date)
                                .Select(g => new
                                {
                                    Date = g.Key,
                                    TotalRevenue = g.Sum(s => s.TotalAmount),
                                    TransactionCount = g.Count(),
                                    TotalUnits = g.Sum(s => s.TotalUnits)
                                })
                                .OrderBy(x => x.Date)
                                .ToListAsync(ct);

                            return new DailySalesCachedSnapshot
                            {
                                Items = dailySalesRaw.Select(x => new DailySaleDto
                                {
                                    Date = x.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                    TotalRevenue = x.TotalRevenue,
                                    TransactionCount = x.TransactionCount,
                                    TotalUnits = x.TotalUnits
                                }).ToList()
                            };
                        }
                    }
                    catch (Exception ex) when (IsMissingRelation(ex))
                    {
                        usedOperationalFallback = true;
                    }

                    var fallbackRaw = await (
                                        from p in trendDb.ProdajaZaglavlja.AsNoTracking()
                                        join ps in trendDb.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                                        join a in trendDb.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                                              (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                                              (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                                        group new { p, ps } by p.DatumProdaje.Date into g
                                        select new
                                        {
                                            Date = g.Key,
                                            TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                                            TransactionCount = g.Select(x => x.p.Id).Distinct().Count(),
                                            TotalUnits = g.Sum(x => x.ps.Kolicina)
                                        })
                        .OrderBy(x => x.Date)
                        .ToListAsync(ct);

                    return new DailySalesCachedSnapshot
                    {
                        UsedOperationalFallback = usedOperationalFallback,
                        Items = fallbackRaw.Select(x => new DailySaleDto
                        {
                            Date = x.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            TotalRevenue = x.TotalRevenue,
                            TransactionCount = x.TransactionCount,
                            TotalUnits = x.TotalUnits
                        }).ToList()
                    };
                },
                CacheExpiration.Medium,
                ct);

            var meta = ResolveCachedDailySalesMeta(snapshot.UsedOperationalFallback, snapshot.Items.Count);
            meta.CorrelationId = ResolveCorrelationId(httpContext);
            return Results.Ok(new { items = snapshot.Items, meta });
        });

        // ========== CATEGORY DATA (CACHED) ==========
        group.MapGet("/sales/by-category", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.CategoryData(fromDate, toDate, storeId, supplierId);

            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    if (!storeId.HasValue && !supplierId.HasValue)
                    {
                        var aggregatedCategory = await TryGetCategoryDataFromAggregatesAsync(db, fromDate, toDate, ct);
                        if (aggregatedCategory is not null && aggregatedCategory.Count > 0)
                        {
                            return aggregatedCategory;
                        }
                    }

                    var query = from ps in db.ProdajaStavke
                                join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                                join a in db.Artikli on ps.IdArtikal equals a.Id
                                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                                      (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                                group ps by new { a.Kategorija, a.Pol } into g
                                select new CategoryDataDto
                                {
                                    Kategorija = g.Key.Kategorija ?? "Ostalo",
                                    Pol = g.Key.Pol ?? "Neodređeno",
                                    TotalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                                    TotalUnits = g.Sum(x => x.Kolicina),
                                    TransactionCount = g.Select(x => x.IdProdaja).Distinct().Count()
                                };

                    return await query.OrderByDescending(x => x.TotalRevenue).ToListAsync(ct);
                },
                CacheExpiration.Medium,
                ct);

            return Results.Ok(result);
        });

        // ========== GENDER DATA (CACHED) ==========
        group.MapGet("/sales/by-gender", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.GenderData(fromDate, toDate, storeId, supplierId);

            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    if (!storeId.HasValue && !supplierId.HasValue)
                    {
                        var aggregatedGender = await TryGetGenderDataFromAggregatesAsync(db, fromDate, toDate, ct);
                        if (aggregatedGender is not null && aggregatedGender.Count > 0)
                        {
                            return aggregatedGender;
                        }
                    }

                    var query = from ps in db.ProdajaStavke
                                join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                                join a in db.Artikli on ps.IdArtikal equals a.Id
                                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                                      (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                                group ps by a.Pol into g
                                select new GenderDataDto
                                {
                                    Pol = g.Key ?? "Neodređeno",
                                    TotalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                                    TotalUnits = g.Sum(x => x.Kolicina)
                                };

                    return await query.OrderByDescending(x => x.TotalRevenue).ToListAsync(ct);
                },
                CacheExpiration.Medium,
                ct);

            return Results.Ok(result);
        });

        // ========== SUPPLIER DATA (CACHED) ==========
        group.MapGet("/sales/by-supplier", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.SupplierData(fromDate, toDate, storeId, supplierId);

            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    if (!storeId.HasValue && !supplierId.HasValue)
                    {
                        var aggregatedSupplier = await TryGetSupplierDataFromAggregatesAsync(db, fromDate, toDate, ct);
                        if (aggregatedSupplier is not null && aggregatedSupplier.Count > 0)
                        {
                            return aggregatedSupplier;
                        }
                    }

                    var query = from ps in db.ProdajaStavke
                                join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                                join a in db.Artikli on ps.IdArtikal equals a.Id
                                join d in db.Dobavljaci on a.IDDobavljac equals d.Id into dobavljacJoin
                                from d in dobavljacJoin.DefaultIfEmpty()
                                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                                      (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                                group ps by new { DobavljacId = d != null ? d.Id : (int?)null, DobavljacNaziv = d != null ? d.Naziv : "Nepoznato" } into g
                                select new SupplierDataDto
                                {
                                    DobavljacId = g.Key.DobavljacId,
                                    DobavljacNaziv = g.Key.DobavljacNaziv ?? "Nepoznato",
                                    TotalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                                    TotalUnits = g.Sum(x => x.Kolicina),
                                    TransactionCount = g.Select(x => x.IdProdaja).Distinct().Count()
                                };

                    return await query.OrderByDescending(x => x.TotalRevenue).ToListAsync(ct);
                },
                CacheExpiration.Medium,
                ct);

            return Results.Ok(result);
        });

        // ========== QUICK INSIGHTS (CACHED) ==========
        group.MapGet("/quick-insights", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.QuickInsights(fromDate, toDate, storeId, supplierId);

            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var salesLines = from p in db.ProdajaZaglavlja.AsNoTracking()
                                     join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                                     join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                                     where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                           (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                                           (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                                           (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                                     select new
                                     {
                                         p.Id,
                                         p.DatumProdaje,
                                         ps.IdArtikal,
                                         ps.Kolicina,
                                         ps.Cena,
                                         ProductName = a.Naziv
                                     };

                    var bestDay = await salesLines
                        .GroupBy(x => x.DatumProdaje.DayOfWeek)
                        .Select(g => new
                        {
                            DayOfWeek = (int)g.Key,
                            TotalRevenue = g.Sum(x => x.Kolicina * x.Cena)
                        })
                        .OrderByDescending(x => x.TotalRevenue)
                        .ThenBy(x => x.DayOfWeek)
                        .FirstOrDefaultAsync(ct);

                    var topProduct = await salesLines
                        .GroupBy(x => new { x.IdArtikal, x.ProductName })
                        .Select(g => new
                        {
                            ProductName = g.Key.ProductName,
                            TotalRevenue = g.Sum(x => x.Kolicina * x.Cena)
                        })
                        .OrderByDescending(x => x.TotalRevenue)
                        .ThenBy(x => x.ProductName)
                        .FirstOrDefaultAsync(ct);

                    var lowStockQuery = db.Artikli.AsNoTracking()
                        .Where(a => a.Kolicina <= a.MinimalnaKolicina || a.Kolicina == 0);

                    if (storeId.HasValue)
                        lowStockQuery = lowStockQuery.Where(a => a.IDObjekat == storeId.Value);

                    if (supplierId.HasValue)
                        lowStockQuery = lowStockQuery.Where(a => a.IDDobavljac == supplierId.Value);

                    var lowStockCount = await lowStockQuery.CountAsync(ct);

                    return new QuickInsightsDto
                    {
                        BestDay = bestDay is null ? null : SerbianDayNames[bestDay.DayOfWeek],
                        BestDayRevenue = bestDay?.TotalRevenue ?? 0,
                        TopProduct = topProduct?.ProductName,
                        LowStockAlert = lowStockCount
                    };
                },
                CacheExpiration.Medium,
                ct);

            return Results.Ok(result);
        });

        // ========== TRANSACTION STATS (CACHED) ==========
        group.MapGet("/sales/transaction-stats", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            var cacheKey = AnalyticsCacheKeys.TransactionStats(fromDate, toDate, storeId, supplierId);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var perTransaction = await (
                        from p in db.ProdajaZaglavlja.AsNoTracking()
                        join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                        join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                              (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                              (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                        group ps by p.Id into g
                        select new
                        {
                            LineCount = g.Count(),
                            UnitCount = g.Sum(x => x.Kolicina),
                            TotalValue = g.Sum(x => x.Kolicina * x.Cena)
                        }).ToListAsync(ct);

                    if (perTransaction.Count == 0)
                    {
                        return new TransactionStatsDto();
                    }

                    return new TransactionStatsDto
                    {
                        AvgItemsPerTransaction = Math.Round(perTransaction.Average(x => (decimal)x.LineCount), 2),
                        AvgUnitsPerTransaction = Math.Round(perTransaction.Average(x => (decimal)x.UnitCount), 2),
                        AvgTransactionValue = Math.Round(perTransaction.Average(x => x.TotalValue), 2),
                        TotalTransactions = perTransaction.Count
                    };
                },
                CacheExpiration.Medium,
                ct);
            return Results.Ok(result);
        });

        // ========== SALES BY PAYMENT (CACHED) ==========
        group.MapGet("/sales/by-payment", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            var cacheKey = AnalyticsCacheKeys.ByPayment(fromDate, toDate, storeId, supplierId);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    return await (
                        from p in db.ProdajaZaglavlja.AsNoTracking()
                        join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                        join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                              (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                              (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                        group new { p, ps } by p.NacinPlacanja into g
                        orderby g.Sum(x => x.ps.Kolicina * x.ps.Cena) descending
                        select new PaymentDataDto
                        {
                            NacinPlacanja = g.Key ?? "Nepoznato",
                            TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                            TransactionCount = g.Select(x => x.p.Id).Distinct().Count()
                        }).ToListAsync(ct);
                },
                CacheExpiration.Medium,
                ct);
            return Results.Ok(result);
        });

        // ========== SALES BY WEEKDAY (CACHED) ==========
        group.MapGet("/sales/by-weekday", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            var cacheKey = AnalyticsCacheKeys.ByWeekday(fromDate, toDate, storeId, supplierId);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var rows = await (
                        from p in db.ProdajaZaglavlja.AsNoTracking()
                        join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                        join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                              (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                              (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                        group new { p, ps } by p.DatumProdaje.DayOfWeek into g
                        orderby g.Key
                        select new WeekdayDataDto
                        {
                            DayOfWeek = (int)g.Key,
                            DayName = SerbianDayNames[(int)g.Key],
                            TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                            TransactionCount = g.Select(x => x.p.Id).Distinct().Count()
                        }).ToListAsync(ct);

                    return rows;
                },
                CacheExpiration.Medium,
                ct);
            return Results.Ok(result);
        });

        // ========== SALES BY HOUR (CACHED) ==========
        group.MapGet("/sales/by-hour", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            var cacheKey = AnalyticsCacheKeys.ByHour(fromDate, toDate, storeId, supplierId);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    return await (
                        from p in db.ProdajaZaglavlja.AsNoTracking()
                        join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                        join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                              (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                              (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                        group new { p, ps } by p.DatumProdaje.Hour into g
                        orderby g.Key
                        select new HourDataDto
                        {
                            Hour = g.Key,
                            TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                            TransactionCount = g.Select(x => x.p.Id).Distinct().Count()
                        }).ToListAsync(ct);
                },
                CacheExpiration.Medium,
                ct);
            return Results.Ok(result);
        });

        // ========== REORDER SUGGESTIONS (CACHED) ==========
        group.MapGet("/reorder-suggestions", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            var cacheKey = AnalyticsCacheKeys.ReorderSuggestions(supplierId);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var artikli = await db.Artikli
                        .Where(a => a.Kolicina <= a.MinimalnaKolicina || a.Kolicina == 0)
                        .Where(a => !supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                        .OrderBy(a => a.Kolicina)
                        .Select(a => new
                        {
                            a.Id,
                            a.Naziv,
                            a.Kolicina,
                            a.MinimalnaKolicina,
                            a.Kategorija,
                            a.NabavnaCena
                        })
                        .ToListAsync(ct);
                    return artikli;
                },
                CacheExpiration.Short,
                ct);
            return Results.Ok(result);
        });

        // ========== PRODUCT DECISION CENTER (CACHED) ==========
        group.MapGet("/products/decision-center", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            ILogger<Program> logger,
            ILoggerFactory loggerFactory,
            HttpContext httpContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            int top = 500,
            string dataScope = "all",
            CancellationToken ct = default) =>
        {
            var normalizedDataScope = NormalizeDataScope(dataScope);
            top = Math.Clamp(top, 50, 2000);

            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            try
            {
                var cacheKey = AnalyticsCacheKeys.ProductDecisionCenter(fromDate, toDate, storeId, supplierId, top, normalizedDataScope);
                var cacheResult = await GetOrSetWithPolicyAsync(
                    cache,
                    cacheKey,
                    AnalyticsCachePolicy.ProductDecisionCenterFamily,
                    AnalyticsCachePolicy.ProductDecisionCenter,
                    async () => await BuildProductDecisionCenterAsync(db, fromDate, toDate, storeId, supplierId, top, normalizedDataScope, ct),
                    ct,
                    loggerFactory: loggerFactory,
                    routeName: "products.decision-center");
                var result = cacheResult.Value;

                result.Meta ??= BuildSuccessMeta();
                ApplyStaleCacheWarning(result.Meta, cacheResult.Metadata, AnalyticsCachePolicy.ProductDecisionCenter);
                result.Meta.CorrelationId = ResolveCorrelationId(httpContext);

                return Results.Ok(result);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogWarning(ex, "Product decision center fallback due to timeout.");
                return Results.Ok(new ProductDecisionCenterResponseDto
                {
                    Meta = BuildErrorMeta(
                        "ANALYTICS_TIMEOUT",
                        "Product Decision Center podaci trenutno nisu dostupni zbog isteka vremena.",
                        ResolveCorrelationId(httpContext)),
                });
            }
            catch (NpgsqlException ex)
            {
                logger.LogWarning(ex, "Product decision center fallback due to database issue.");
                return Results.Ok(new ProductDecisionCenterResponseDto
                {
                    Meta = BuildErrorMeta(
                        "ANALYTICS_DB_UNAVAILABLE",
                        "Product Decision Center podaci trenutno nisu dostupni zbog greske baze.",
                        ResolveCorrelationId(httpContext)),
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Product decision center fallback due to unexpected issue.");
                return Results.Ok(new ProductDecisionCenterResponseDto
                {
                    Meta = BuildErrorMeta(
                        "ANALYTICS_UNEXPECTED_ERROR",
                        "Product Decision Center podaci trenutno nisu dostupni.",
                        ResolveCorrelationId(httpContext)),
                });
            }
        });

        // ========== PRODUCT DECISION TIMELINE FILTER (DT05, read-only) ==========
        group.MapGet("/products/decision-center/timeline", async (
            IAnalyticsDbContext analyticsDb,
            ILogger<Program> logger,
            HttpContext httpContext,
            string? sourceType = null,
            string? sourceKey = null,
            int? productId = null,
            string? recommendationType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var response = await BuildProductDecisionTimelineFilterAsync(
                    analyticsDb,
                    sourceType,
                    sourceKey,
                    productId,
                    recommendationType,
                    fromDate,
                    toDate,
                    ct);
                response.Meta ??= BuildSuccessMeta();
                response.Meta.CorrelationId = ResolveCorrelationId(httpContext);
                return Results.Ok(response);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogWarning(ex, "Product decision timeline filter fallback due to timeout.");
                return Results.Ok(new ProductDecisionTimelineFilterResponseDto
                {
                    Meta = BuildErrorMeta(
                        "ANALYTICS_TIMEOUT",
                        "Decision Timeline podaci trenutno nisu dostupni zbog isteka vremena.",
                        ResolveCorrelationId(httpContext)),
                    EmptyReason = AnalyticsActionTimelineFilterProjection.EmptyReasonNoEvents
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Product decision timeline filter fallback due to unexpected issue.");
                return Results.Ok(new ProductDecisionTimelineFilterResponseDto
                {
                    Meta = BuildErrorMeta(
                        "ANALYTICS_UNEXPECTED_ERROR",
                        "Decision Timeline podaci trenutno nisu dostupni.",
                        ResolveCorrelationId(httpContext)),
                    EmptyReason = AnalyticsActionTimelineFilterProjection.EmptyReasonNoEvents
                });
            }
        });

        // ========== PRODUCT DECISION TIMELINE EXPORT (DT07, read-only over Slice-2) ==========
        group.MapGet("/products/decision-center/timeline/export", async (
            IAnalyticsDbContext analyticsDb,
            ILogger<Program> logger,
            HttpContext httpContext,
            string? sourceType = null,
            string? sourceKey = null,
            int? productId = null,
            string? recommendationType = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? format = null,
            CancellationToken ct = default) =>
        {
            var nowUtc = DateTime.UtcNow.Date;
            var periodToUtc = toDate?.Date ?? nowUtc;
            var periodFromUtc = fromDate?.Date ?? periodToUtc.AddDays(-29);
            if (periodFromUtc > periodToUtc)
            {
                (periodFromUtc, periodToUtc) = (periodToUtc, periodFromUtc);
            }

            var generatedAtUtc = DateTime.UtcNow;
            var correlationId = ResolveCorrelationId(httpContext);
            try
            {
                var filtered = await BuildProductDecisionTimelineFilterAsync(
                    analyticsDb,
                    sourceType,
                    sourceKey,
                    productId,
                    recommendationType,
                    fromDate,
                    toDate,
                    ct);

                DecisionTimelineExportDto export;
                if (filtered.Meta is { Success: false } || filtered.Scope is null)
                {
                    export = DecisionTimelineExportProjection.Error(
                        periodFromUtc,
                        periodToUtc,
                        generatedAtUtc,
                        filtered.Meta?.ErrorCode ?? "ANALYTICS_UNEXPECTED_ERROR",
                        filtered.Meta?.ErrorMessage ?? "Decision Timeline export trenutno nije dostupan.",
                        filtered.WarningCodes);
                }
                else
                {
                    export = DecisionTimelineExportProjection.FromFilter(
                        new DecisionTimelineFilterResponseDto(
                            Scope: filtered.Scope,
                            EmptyReason: filtered.EmptyReason,
                            Timelines: filtered.Timelines,
                            MatchedActionCount: filtered.MatchedActionCount,
                            MatchedEventCount: filtered.MatchedEventCount,
                            WarningCodes: filtered.WarningCodes),
                        generatedAtUtc,
                        filtered.Meta?.DataQualityStatus);
                }

                return FormatDecisionTimelineExport(export, format, correlationId);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogWarning(ex, "Product decision timeline export fallback due to timeout.");
                var export = DecisionTimelineExportProjection.Error(
                    periodFromUtc,
                    periodToUtc,
                    generatedAtUtc,
                    "ANALYTICS_TIMEOUT",
                    "Decision Timeline export trenutno nije dostupan zbog isteka vremena.");
                return FormatDecisionTimelineExport(export, format, correlationId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Product decision timeline export fallback due to unexpected issue.");
                var export = DecisionTimelineExportProjection.Error(
                    periodFromUtc,
                    periodToUtc,
                    generatedAtUtc,
                    "ANALYTICS_UNEXPECTED_ERROR",
                    "Decision Timeline export trenutno nije dostupan.");
                return FormatDecisionTimelineExport(export, format, correlationId);
            }
        });

        // ========== CATEGORY TRENDS (CACHED) ==========
        group.MapGet("/sales/category-trends", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            var cacheKey = AnalyticsCacheKeys.CategoryTrends(fromDate, toDate, storeId, supplierId);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var compactRows = await (
                        from p in db.ProdajaZaglavlja.AsNoTracking()
                        join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                        join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                              (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                              (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                        group ps by new { Date = p.DatumProdaje.Date, Category = a.Kategorija ?? "Ostalo" } into g
                        orderby g.Key.Date, g.Key.Category
                        select new
                        {
                            g.Key.Date,
                            g.Key.Category,
                            TotalRevenue = g.Sum(x => x.Kolicina * x.Cena)
                        }).ToListAsync(ct);

                    var result = new List<Dictionary<string, object>>();
                    foreach (var dateEntry in compactRows.GroupBy(x => x.Date).OrderBy(x => x.Key))
                    {
                        var row = new Dictionary<string, object>
                        {
                            ["date"] = dateEntry.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        };
                        foreach (var cat in dateEntry)
                        {
                            row[cat.Category] = cat.TotalRevenue;
                        }
                        result.Add(row);
                    }
                    return result;
                },
                CacheExpiration.Medium,
                ct);
            return Results.Ok(result);
        });

        // ========== ADVANCED DASHBOARD SNAPSHOT (CACHED) ==========
        group.MapGet("/dashboard/advanced", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            string dataScope = "all",
            CancellationToken ct = default) =>
        {
            var normalizedDataScope = NormalizeDataScope(dataScope);

            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.DashboardAdvanced(fromDate, toDate, storeId, supplierId, normalizedDataScope);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () => await BuildAdvancedDashboardSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                DashboardFastSectionTtl,
                ct);

            return Results.Ok(result);
        });

        group.MapGet("/dashboard/bootstrap", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            IMediator mediator,
            ILogger<Program> logger,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            HttpContext httpContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            int? supplierId = null,
            string dataScope = "all",
            CancellationToken ct = default) =>
        {
            var normalizedDataScope = NormalizeDataScope(dataScope);
            var requestAborted = ct;

            try
            {
                if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                    fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

                if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                    toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

                var profilingEnabled = configuration.GetValue<bool>("AnalyticsBootstrapSectionTiming:Enabled");
                var profileSections = profilingEnabled
                    || (httpContext.Request.Query.TryGetValue("profileSections", out var profileSectionsQuery)
                        && bool.TryParse(profileSectionsQuery.ToString(), out var parsedProfileSections)
                        && parsedProfileSections);
                var profileSample = httpContext.Request.Query.TryGetValue("profileSample", out var profileSampleQuery)
                    ? profileSampleQuery.ToString()
                    : "n/a";

                var cacheKey = AnalyticsCacheKeys.DashboardBootstrap(fromDate, toDate, storeId, supplierId, normalizedDataScope);
                var cacheResult = await GetOrSetWithPolicyAsync(
                    cache,
                    cacheKey,
                    AnalyticsCachePolicy.DashboardFamily,
                    AnalyticsCachePolicy.DashboardBootstrap,
                    async () =>
                    {
                        var response = new AnalyticsDashboardBootstrapDto();

                        async Task<T?> MeasureSectionAsync<T>(
                            string sectionId,
                            string priority,
                            Func<Task<T>> factory,
                            string fallbackMessage) where T : class
                        {
                            Stopwatch? stopwatch = null;
                            if (profileSections)
                            {
                                stopwatch = Stopwatch.StartNew();
                            }

                            T? value = default;
                            try
                            {
                                value = await TrySectionAsync(factory, response.Errors, fallbackMessage);
                                return value;
                            }
                            finally
                            {
                                if (stopwatch is not null)
                                {
                                    stopwatch.Stop();
                                    logger.LogInformation(
                                        "dashboard.bootstrap.section sample={Sample} section={Section} priority={Priority} elapsedMs={ElapsedMs:F2} success={Success} errors={Errors}",
                                        profileSample,
                                        sectionId,
                                        priority,
                                        stopwatch.Elapsed.TotalMilliseconds,
                                        value is not null,
                                        response.Errors.Count);
                                }
                            }
                        }

                        response.Summary = await MeasureSectionAsync(
                            "Summary",
                            "P0",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.SalesSummary(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildSalesSummarySnapshotAsync(db, mediator, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardSectionTtl,
                                ct),
                            "Sažetak prodaje nije dostupan.");

                        response.Inventory = await MeasureSectionAsync(
                            "Inventory",
                            "P0",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.Inventory(2),
                                async () => await BuildInventoryStatusSnapshotAsync(db, mediator, 2, ct),
                                DashboardFastSectionTtl,
                                ct),
                            "Status zaliha nije dostupan.");

                        response.DailySales = await MeasureSectionAsync(
                            "DailySales",
                            "P0",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.DailySales(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildDailySalesSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardSectionTtl,
                                ct),
                            "Dnevni trend prodaje nije dostupan.") ?? [];

                        response.CategoryData = await MeasureSectionAsync(
                            "CategoryData",
                            "P1",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.CategoryData(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildCategoryDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardSectionTtl,
                                ct),
                            "Prodaja po kategorijama nije dostupna.") ?? [];

                        response.GenderData = await MeasureSectionAsync(
                            "GenderData",
                            "P1",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.GenderData(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildGenderDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardSectionTtl,
                                ct),
                            "Prodaja po polu nije dostupna.") ?? [];

                        response.SupplierData = await MeasureSectionAsync(
                            "SupplierData",
                            "P1",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.SupplierData(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildSupplierDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardSectionTtl,
                                ct),
                            "Prodaja po dobavljačima nije dostupna.") ?? [];

                        response.SupplierOptions = await MeasureSectionAsync(
                            "SupplierOptions",
                            "P2",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.SupplierFilters(fromDate, toDate, storeId, normalizedDataScope),
                                async () => await BuildSupplierFilterOptionsAsync(db, fromDate, toDate, storeId, ct, normalizedDataScope),
                                DashboardReferenceSectionTtl,
                                ct),
                            "Lista dobavljača za filter nije dostupna.") ?? [];

                        response.WeekdayData = await MeasureSectionAsync(
                            "WeekdayData",
                            "P1",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ByWeekday(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildWeekdayDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardSectionTtl,
                                ct),
                            "Prodaja po danima nije dostupna.") ?? [];

                        response.HourData = await MeasureSectionAsync(
                            "HourData",
                            "P1",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ByHour(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildHourDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardSectionTtl,
                                ct),
                            "Prodaja po satima nije dostupna.") ?? [];

                        response.PaymentData = await MeasureSectionAsync(
                            "PaymentData",
                            "P1",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ByPayment(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildPaymentDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardSectionTtl,
                                ct),
                            "Prodaja po nacinu placanja nije dostupna.") ?? [];

                        response.QuickInsights = await MeasureSectionAsync(
                            "QuickInsights",
                            "P1",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.QuickInsights(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildQuickInsightsSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardSectionTtl,
                                ct),
                            "Brzi uvidi nisu dostupni.");

                        response.TransactionStats = await MeasureSectionAsync(
                            "TransactionStats",
                            "P1",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.TransactionStats(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildTransactionStatsSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardSectionTtl,
                                ct),
                            "Statistika transakcija nije dostupna.");

                        response.Advanced = await MeasureSectionAsync(
                            "Advanced",
                            "P0",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.DashboardAdvanced(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildAdvancedDashboardSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardFastSectionTtl,
                                ct),
                            "Napredne metrike nisu dostupne.");

                        var productDecisionSnapshot = await MeasureSectionAsync(
                            "ProductDecisionCenter",
                            "P0",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ProductDecisionCenter(fromDate, toDate, storeId, supplierId, 300, normalizedDataScope),
                                async () => await BuildProductDecisionCenterAsync(db, fromDate, toDate, storeId, supplierId, 300, normalizedDataScope, ct),
                                DashboardFastSectionTtl,
                                ct),
                            "Product Decision Center nije dostupan.");

                        var decisionActionsStopwatch = profileSections ? Stopwatch.StartNew() : null;
                        response.DecisionActions = BuildDashboardDecisionActions(
                            productDecisionSnapshot,
                            response.Advanced,
                            fromDate,
                            toDate,
                            storeId,
                            supplierId);
                        if (decisionActionsStopwatch is not null)
                        {
                            decisionActionsStopwatch.Stop();
                            logger.LogInformation(
                                "dashboard.bootstrap.section sample={Sample} section={Section} priority={Priority} elapsedMs={ElapsedMs:F2} success={Success} errors={Errors}",
                                profileSample,
                                "DecisionActions",
                                "P2",
                                decisionActionsStopwatch.Elapsed.TotalMilliseconds,
                                response.DecisionActions is not null,
                                response.Errors.Count);
                        }

                        var executiveStopwatch = profileSections ? Stopwatch.StartNew() : null;
                        response.Executive = BuildExecutiveDashboardSnapshot(
                            productDecisionSnapshot,
                            response.Summary,
                            response.ValidationFreshness,
                            fromDate,
                            toDate,
                            storeId,
                            supplierId);
                        if (executiveStopwatch is not null)
                        {
                            executiveStopwatch.Stop();
                            logger.LogInformation(
                                "dashboard.bootstrap.section sample={Sample} section={Section} priority={Priority} elapsedMs={ElapsedMs:F2} success={Success} errors={Errors}",
                                profileSample,
                                "Executive",
                                "P2",
                                executiveStopwatch.Elapsed.TotalMilliseconds,
                                response.Executive is not null,
                                response.Errors.Count);
                        }

                        response.TopAdvanced = await MeasureSectionAsync(
                            "TopAdvanced",
                            "P1",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.TopProductsAdvanced(10, fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await GetTopProductsAdvancedSnapshotAsync(db, 10, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                DashboardFastSectionTtl,
                                ct),
                            "Napredna tabela top proizvoda nije dostupna.");

                        response.ValidationCompleteness = await MeasureSectionAsync(
                            "ValidationCompleteness",
                            "P2",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ValidationCompleteness,
                                async () => await BuildCompletenessValidationAsync(db, ct),
                                DashboardFastSectionTtl,
                                ct),
                            "Completeness validacija nije dostupna.");

                        response.ValidationFreshness = await MeasureSectionAsync(
                            "ValidationFreshness",
                            "P2",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ValidationFreshness,
                                async () => await BuildFreshnessValidationAsync(db, ct),
                                DashboardFastSectionTtl,
                                ct),
                            "Freshness validacija nije dostupna.");

                        response.ValidationLostSales = await MeasureSectionAsync(
                            "ValidationLostSales",
                            "P2",
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ValidationLostSales,
                                async () => await BuildLostSalesValidationAsync(db, ct),
                                DashboardFastSectionTtl,
                                ct),
                            "Lost-sales validacija nije dostupna.");

                        var inventoryFallback = response.Inventory?.UsedOperationalFallback == true;
                        var hasSectionErrors = response.Errors.Count > 0;
                        response.Meta = BuildSuccessMeta(
                            dataQualityStatus: inventoryFallback
                                ? "warning"
                                : ResolveDashboardDataQualityStatus(response),
                            isPartial: hasSectionErrors || inventoryFallback,
                            warningCode: inventoryFallback
                                ? "inventory_status_operational_fallback"
                                : hasSectionErrors ? "ANALYTICS_PARTIAL_DATA" : null,
                            warningMessage: inventoryFallback
                                ? "Status zaliha je učitan iz operativne tabele Artikli jer analytics relacija nije dostupna."
                                : hasSectionErrors ? "Deo dashboard sekcija nije trenutno dostupan." : null,
                            message: inventoryFallback
                                ? "Status zaliha je učitan iz operativne tabele Artikli jer analytics relacija nije dostupna."
                                : hasSectionErrors ? "Deo dashboard sekcija nije trenutno dostupan." : null,
                            lastRefreshAtUtc: response.Advanced?.GeneratedAtUtc ?? response.ValidationFreshness?.LastImport);

                        return response;
                    },
                    ct,
                    loggerFactory: loggerFactory,
                    routeName: "dashboard.bootstrap");
                var result = cacheResult.Value;

                result.Meta ??= BuildSuccessMeta();
                ApplyStaleCacheWarning(result.Meta, cacheResult.Metadata, AnalyticsCachePolicy.DashboardBootstrap);
                result.Meta.CorrelationId = ResolveCorrelationId(httpContext);

                return Results.Ok(result);
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                logger.LogWarning("Request cancelled while loading analytics dashboard bootstrap.");
                return Results.StatusCode(499);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogWarning(ex, "Dashboard bootstrap fallback due to timeout.");
                return Results.Ok(new AnalyticsDashboardBootstrapDto
                {
                    Errors = ["Dashboard bootstrap fallback: request timed out."],
                    Meta = BuildErrorMeta(
                        "ANALYTICS_TIMEOUT",
                        "Dashboard podaci trenutno nisu dostupni zbog isteka vremena.",
                        ResolveCorrelationId(httpContext))
                });
            }
            catch (NpgsqlException ex)
            {
                logger.LogWarning(ex, "Dashboard bootstrap fallback due to database issue.");
                return Results.Ok(new AnalyticsDashboardBootstrapDto
                {
                    Errors = ["Dashboard bootstrap fallback: database temporarily unavailable."],
                    Meta = BuildErrorMeta(
                        "ANALYTICS_DB_UNAVAILABLE",
                        "Dashboard podaci trenutno nisu dostupni zbog greske baze.",
                        ResolveCorrelationId(httpContext))
                });
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "Dashboard bootstrap fallback due to timeout.");
                return Results.Ok(new AnalyticsDashboardBootstrapDto
                {
                    Errors = ["Dashboard bootstrap fallback: request timed out."],
                    Meta = BuildErrorMeta(
                        "ANALYTICS_TIMEOUT",
                        "Dashboard podaci trenutno nisu dostupni zbog isteka vremena.",
                        ResolveCorrelationId(httpContext))
                });
            }
        });

        group.MapGet("/filters/suppliers", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            ILogger<Program> logger,
            HttpContext httpContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            string dataScope = "all",
            CancellationToken ct = default) =>
        {
            var normalizedDataScope = NormalizeDataScope(dataScope);
            var requestAborted = ct;

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
                ct = timeoutCts.Token;

                if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                    fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

                if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                    toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

                var result = await cache.GetOrSetAsync(
                    AnalyticsCacheKeys.SupplierFilters(fromDate, toDate, storeId, normalizedDataScope),
                    async () => await BuildSupplierFilterOptionsAsync(db, fromDate, toDate, storeId, ct, normalizedDataScope),
                    CacheExpiration.Long,
                    ct);

                return Results.Ok(result);
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                logger.LogWarning("Request cancelled while loading supplier filters.");
                return Results.StatusCode(499);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogWarning(ex, "Supplier filters fallback due to timeout.");
                SetFilterFallbackHeaders(httpContext, "supplier_filters_timeout", "Filteri dobavljača trenutno koriste pomoćni signal.");
                return Results.Ok(Array.Empty<SupplierFilterOptionDto>());
            }
            catch (NpgsqlException ex)
            {
                logger.LogWarning(ex, "Supplier filters fallback due to database issue.");
                SetFilterFallbackHeaders(httpContext, "supplier_filters_db_unavailable", "Filteri dobavljača trenutno koriste pomoćni signal.");
                return Results.Ok(Array.Empty<SupplierFilterOptionDto>());
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "Supplier filters fallback due to timeout.");
                SetFilterFallbackHeaders(httpContext, "supplier_filters_timeout", "Filteri dobavljača trenutno koriste pomoćni signal.");
                return Results.Ok(Array.Empty<SupplierFilterOptionDto>());
            }
        });

        group.MapGet("/filters/stores", async (
            IAnalyticsCacheService cache,
            IAnalyticsDbContext analyticsDb,
            ITrendplusDbContext trendDb,
            ILogger<Program> logger,
            HttpContext httpContext,
            CancellationToken ct = default) =>
        {
            var requestAborted = ct;
            var totalStopwatch = Stopwatch.StartNew();
            var cacheKey = AnalyticsCacheKeys.Stores;

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
                ct = timeoutCts.Token;

                var cached = await cache.GetAsync<List<StoreFilterOptionDto>>(cacheKey, ct);
                if (cached is not null)
                {
                    totalStopwatch.Stop();
                    logger.LogInformation(
                        "Store filters cache hit in {ElapsedMs}ms. StoreCount={StoreCount}",
                        totalStopwatch.ElapsedMilliseconds,
                        cached.Count);

                    return Results.Ok(cached);
                }

                var dbStopwatch = Stopwatch.StartNew();
                var result = await analyticsDb.StoresDim
                    .AsNoTracking()
                    .OrderBy(x => x.StoreName)
                    .Select(x => new StoreFilterOptionDto
                    {
                        StoreId = x.StoreId,
                        StoreName = x.StoreName,
                        City = x.City,
                        Region = x.Region
                    })
                    .ToListAsync(ct);

                dbStopwatch.Stop();
                await cache.SetAsync(cacheKey, result, CacheExpiration.VeryLong, ct);

                totalStopwatch.Stop();
                logger.LogInformation(
                    "Store filters cache miss computed in {ElapsedMs}ms. DbMs={DbMs} StoreCount={StoreCount} TtlMinutes={TtlMinutes}",
                    totalStopwatch.ElapsedMilliseconds,
                    dbStopwatch.ElapsedMilliseconds,
                    result.Count,
                    CacheExpiration.VeryLong.TotalMinutes);

                return Results.Ok(result);
            }
            catch (OperationCanceledException) when (requestAborted.IsCancellationRequested)
            {
                logger.LogWarning("Request cancelled while loading store filters.");
                return Results.StatusCode(499);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogWarning(ex, "Store filters fallback due to timeout.");
                var fallback = await TryBuildStoreFiltersFallbackAsync(trendDb, logger, requestAborted);
                SetFilterFallbackHeaders(httpContext, "store_filters_timeout", "Filteri prodavnica trenutno koriste pomoćni signal.");
                return Results.Ok(fallback);
            }
            catch (NpgsqlException ex)
            {
                logger.LogWarning(ex, "Store filters fallback due to database issue.");
                var fallback = await TryBuildStoreFiltersFallbackAsync(trendDb, logger, requestAborted);
                SetFilterFallbackHeaders(httpContext, "store_filters_db_unavailable", "Filteri prodavnica trenutno koriste pomoćni signal.");
                return Results.Ok(fallback);
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "Store filters fallback due to timeout.");
                var fallback = await TryBuildStoreFiltersFallbackAsync(trendDb, logger, requestAborted);
                SetFilterFallbackHeaders(httpContext, "store_filters_timeout", "Filteri prodavnica trenutno koriste pomoćni signal.");
                return Results.Ok(fallback);
            }
        });

        // ========== VALIDATION: COMPLETENESS (CACHED) ==========
        group.MapGet("/validation/completeness", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            CancellationToken ct = default) =>
        {
            var result = await cache.GetOrSetAsync(
                AnalyticsCacheKeys.ValidationCompleteness,
                async () =>
                {
                    var (score, totalSku, missingSku, _, _) = await GetCompletenessAndFreshnessAsync(db, ct);
                    var status = score >= 0.98m ? "good" : score >= 0.90m ? "warning" : "critical";
                    var message = status switch
                    {
                        "good" => "Completeness je stabilan.",
                        "warning" => "Nedostaju bitna polja za deo artikala.",
                        _ => "Nizak completeness: validacija podataka je prioritet."
                    };
                    return new DashboardValidationEndpointDto
                    {
                        Status = status,
                        Message = message,
                        Score = score,
                        TotalSku = totalSku,
                        AffectedSku = missingSku
                    };
                },
                AnalyticsCachePolicy.DataQuality.Ttl,
                ct);

            return Results.Ok(result);
        });

        // ========== VALIDATION: FRESHNESS (CACHED) ==========
        group.MapGet("/validation/freshness", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            CancellationToken ct = default) =>
        {
            var result = await cache.GetOrSetAsync(
                AnalyticsCacheKeys.ValidationFreshness,
                async () =>
                {
                    var (_, _, _, lastImport, freshnessHours) = await GetCompletenessAndFreshnessAsync(db, ct);
                    var status = freshnessHours <= 6m ? "good" : freshnessHours <= 24m ? "warning" : "critical";
                    var message = status switch
                    {
                        "good" => "Podaci su svezi.",
                        "warning" => "Osvežavanje kasni, proverite import pipeline.",
                        _ => "Podaci su zastareli: osvežite import i agregate."
                    };
                    return new DashboardValidationEndpointDto
                    {
                        Status = status,
                        Message = message,
                        LastImport = lastImport,
                        FreshnessHours = freshnessHours
                    };
                },
                AnalyticsCachePolicy.DataQuality.Ttl,
                ct);

            return Results.Ok(result);
        });

        // ========== VALIDATION: LOST SALES (CACHED) ==========
        group.MapGet("/validation/lost-sales", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            CancellationToken ct = default) =>
        {
            var result = await cache.GetOrSetAsync(
                AnalyticsCacheKeys.ValidationLostSales,
                async () =>
                {
                    var snapshot = await GetLostSalesSnapshotAsync(db, ct);
                    return BuildLostSalesValidationFromSnapshot(snapshot);
                },
                AnalyticsCachePolicy.DataQuality.Ttl,
                ct);

            return Results.Ok(result);
        });

        // ========== VALIDATION: NEGATIVE QUANTITY (CACHED) ==========
        group.MapGet("/validation/negative-qty", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var result = await cache.GetOrSetAsync(
                AnalyticsCacheKeys.ValidationNegativeQty(fromDate, toDate),
                async () =>
                {
                    var (negativeQtyCount, totalRows) = await GetNegativeQuantityValidationAsync(db, fromDate, toDate, ct);
                    var rate = totalRows <= 0 ? 0m : Math.Round(((decimal)negativeQtyCount / totalRows) * 100m, 4);
                    var status = totalRows == 0
                        ? "info"
                        : negativeQtyCount == 0
                            ? "good"
                            : rate <= 0.1m ? "warning" : "critical";
                    var message = status switch
                    {
                        "good" => "Nema negativnih količina u prodajnim stavkama.",
                        "warning" => "Pronađene su negativne količine u malom broju stavki.",
                        "critical" => "Negativne količine su iznad dozvoljenog praga i zahtevaju proveru.",
                        _ => "Nema podataka za proveru negativnih količina."
                    };
                    return new DashboardValidationEndpointDto
                    {
                        Status = status,
                        Message = message,
                        NegativeQtyCount = negativeQtyCount,
                        TotalRows = totalRows,
                        Score = totalRows <= 0 ? null : Math.Round(1m - ((decimal)negativeQtyCount / totalRows), 6)
                    };
                },
                AnalyticsCachePolicy.DataQuality.Ttl,
                ct);

            return Results.Ok(result);
        });

        // ========== CACHE STATUS ENDPOINTS (LEGACY + CANONICAL ALIAS) ==========
        group.MapGet("/cache/status", HandleCacheStatusAsync);

        app.MapGet("/api/analytics/cache/status", HandleCacheStatusAsync)
            .WithTags("Analytics (Cached)")
            .RequireRateLimiting("analytics");

        // ========== CACHE INVALIDATE ENDPOINT (za admin) ==========
        group.MapPost("/cache/invalidate", async (
            HttpContext context,
            IConfiguration configuration,
            AnalyticsCacheAdminService cacheAdmin,
            string? family,
            CancellationToken ct) =>
        {
            var access = GetAdminAccessDecision(context, configuration);
            if (access is AdminAccessDecision.MissingCredential)
            {
                return Results.Unauthorized();
            }

            if (access is AdminAccessDecision.Forbidden)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var state = await cacheAdmin.ClearAsync(family, ct);
            return Results.Ok(new
            {
                success = true,
                message = "Analytics cache i report cache su očišćeni.",
                lastClearAtUtc = state.LastClearAtUtc,
                lastClearFamily = state.LastClearFamily,
                lastAnalyticsCacheClearAtUtc = state.LastAnalyticsCacheClearAtUtc,
                lastReportCacheClearAtUtc = state.LastReportCacheClearAtUtc,
                reportCacheVersion = state.ReportCacheVersion,
                isShared = state.IsShared,
                warning = state.Warning,
                storage = state.Storage
            });
        });

    }

    private enum AdminAccessDecision
    {
        MissingCredential,
        Forbidden,
        Authorized
    }

    private static AdminAccessDecision GetAdminAccessDecision(HttpContext context, IConfiguration configuration)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return context.User.IsInRole("Admin")
                ? AdminAccessDecision.Authorized
                : AdminAccessDecision.Forbidden;
        }

        var providedKey = context.Request.Headers["X-Admin-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return AdminAccessDecision.MissingCredential;
        }

        var configuredKey = configuration["Admin:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            configuredKey = Environment.GetEnvironmentVariable("ADMIN_API_KEY");
        }

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return AdminAccessDecision.Forbidden;
        }

        return string.Equals(providedKey, configuredKey, StringComparison.Ordinal)
            ? AdminAccessDecision.Authorized
            : AdminAccessDecision.Forbidden;
    }

    private static async Task<IResult> HandleCacheStatusAsync(
        IAnalyticsCacheService cache,
        AnalyticsCacheAdminService cacheAdmin,
        IWebHostEnvironment env)
    {
        var clearState = await cacheAdmin.GetStateAsync();
        var (cacheMode, isDistributed) = cacheAdmin.ResolveCacheMode();
        var footprint = cache.GetFootprintSnapshot();
        var isShared = clearState.IsShared;
        var warning = clearState.Warning;
        if (env.IsProduction() && string.Equals(cacheMode, "in-memory", StringComparison.OrdinalIgnoreCase))
        {
            warning = "Analytics cache je in-memory. U multi-instance okruženju podaci mogu biti nekonzistentni između instanci.";
        }

        var message = isShared
            ? "Cache radi u deljenom modu; clear state i invalidacija su vidljivi između instanci."
            : "Cache nije distribuiran; može biti nekonzistentan između instanci.";

        return Results.Ok(new
        {
            provider = cache.GetType().Name.Replace("CacheService", string.Empty),
            redisAvailable = cache.IsRedisAvailable,
            redisEnabled = cache.IsRedisEnabled,
            isShared,
            isDistributed,
            cacheMode,
            environment = env.EnvironmentName,
            cacheType = isShared ? "Hybrid (In-Memory + Redis)" : "In-Memory only",
            trackedKeyCount = footprint.TrackedKeyCount,
            message,
            warning,
            lastClearAtUtc = clearState.LastClearAtUtc,
            lastClearFamily = clearState.LastClearFamily,
            lastAnalyticsCacheClearAtUtc = clearState.LastAnalyticsCacheClearAtUtc,
            lastReportCacheClearAtUtc = clearState.LastReportCacheClearAtUtc,
            reportCacheVersion = clearState.ReportCacheVersion,
            clearStateStorage = clearState.Storage
        });
    }

    private static async Task<NpgsqlConnection?> OpenTrendplusConnectionAsync(ITrendplusDbContext db, CancellationToken ct)
    {
        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    private static void AddNullableDateParameter(NpgsqlCommand command, string name, DateTime? value)
    {
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlTypes.NpgsqlDbType.Date)
        {
            Value = value.HasValue ? value.Value.Date : DBNull.Value
        });
    }

    private static async Task<CacheReadResult<T>> GetOrSetWithPolicyAsync<T>(
        IAnalyticsCacheService cache,
        string cacheKey,
        string family,
        AnalyticsCachePolicyEntry policy,
        Func<Task<T>> factory,
        CancellationToken ct,
        ILoggerFactory? loggerFactory = null,
        string? routeName = null) where T : class
    {
        var sw = Stopwatch.StartNew();
        var metadataKey = AnalyticsCacheKeys.Metadata(cacheKey);
        var provider = ResolveCacheProvider(cache);
        var cacheLogger = loggerFactory?.CreateLogger("AnalyticsCachePolicy");
        var normalizedRouteName = string.IsNullOrWhiteSpace(routeName) ? family : routeName.Trim();

        var cached = await cache.GetAsync<T>(cacheKey, ct);
        if (cached is not null)
        {
            var metadata = await cache.GetAsync<AnalyticsCacheEntryMetadata>(metadataKey, ct)
                ?? new AnalyticsCacheEntryMetadata
                {
                    CreatedAtUtc = DateTime.UtcNow,
                    Family = family,
                    Provider = provider
                };
            sw.Stop();
            cacheLogger?.LogInformation(
                "Cache HIT route={Route} family={Family} provider={Provider} key={KeyHash} ageSec={AgeSec} ttlSec={TtlSec} elapsedMs={ElapsedMs}",
                normalizedRouteName,
                family,
                provider,
                AnalyticsCacheKeys.SafeKeyFingerprint(cacheKey),
                Math.Max(0, (DateTime.UtcNow - metadata.CreatedAtUtc).TotalSeconds),
                policy.Ttl.TotalSeconds,
                sw.ElapsedMilliseconds);

            return new CacheReadResult<T>(cached, true, metadata);
        }

        var value = await factory();
        var entryMetadata = new AnalyticsCacheEntryMetadata
        {
            CreatedAtUtc = DateTime.UtcNow,
            Family = family,
            Provider = provider
        };
        await cache.SetAsync(cacheKey, value, policy.Ttl, ct);
        await cache.SetAsync(metadataKey, entryMetadata, policy.Ttl, ct);
        sw.Stop();
        cacheLogger?.LogInformation(
            "Cache MISS route={Route} family={Family} provider={Provider} key={KeyHash} ttlSec={TtlSec} elapsedMs={ElapsedMs}",
            normalizedRouteName,
            family,
            provider,
            AnalyticsCacheKeys.SafeKeyFingerprint(cacheKey),
            policy.Ttl.TotalSeconds,
            sw.ElapsedMilliseconds);

        return new CacheReadResult<T>(value, false, entryMetadata);
    }

    private static void ApplyStaleCacheWarning(
        AnalyticsResponseMetaDto meta,
        AnalyticsCacheEntryMetadata metadata,
        AnalyticsCachePolicyEntry policy)
    {
        meta.LastRefreshAtUtc = metadata.CreatedAtUtc;

        var age = DateTime.UtcNow - metadata.CreatedAtUtc;
        if (age <= policy.StaleAfter)
        {
            return;
        }

        var staleWarning = AnalyticsResponseMetaFactory.StaleCacheWarning(
            "Prikazani su keširani podaci. Pokrenite osvežavanje ako su potrebni najnoviji rezultati.");
        meta.IsPartial = true;
        meta.WarningCode = staleWarning.WarningCode;
        meta.WarningMessage = staleWarning.WarningMessage;
        meta.Message = staleWarning.Message;
        if (string.IsNullOrWhiteSpace(meta.DataQualityStatus))
        {
            meta.DataQualityStatus = staleWarning.DataQualityStatus;
        }
    }

    private static string ResolveCacheProvider(IAnalyticsCacheService cache)
    {
        if (cache is DisabledAnalyticsCacheService)
        {
            return "disabled";
        }

        return cache.IsRedisEnabled && cache.IsRedisAvailable ? "redis" : "memory";
    }

    private static async Task<SalesSummaryDto?> TryGetSalesSummaryFromAggregatesAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        try
        {
            await using var conn = await OpenTrendplusConnectionAsync(db, ct);
            if (conn is null) return null;

            const string sql = """
                SELECT
                    COUNT(*)::int AS days_count,
                    COALESCE(SUM("TotalRevenue"), 0) AS total_revenue,
                    COALESCE(SUM("TotalTransactions"), 0)::int AS total_transactions,
                    COALESCE(SUM("TotalUnits"), 0)::int AS total_units
                FROM "AnalyticsDailySummary"
                WHERE (@fromDate IS NULL OR "Date" >= @fromDate::date)
                  AND (@toDate IS NULL OR "Date" <= @toDate::date);
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            AddNullableDateParameter(cmd, "fromDate", fromDate);
            AddNullableDateParameter(cmd, "toDate", toDate);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;

            var daysCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            if (daysCount == 0) return null;

            var totalRevenue = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
            var totalTransactions = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
            var totalUnits = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            var avgBasket = totalTransactions > 0 ? totalRevenue / totalTransactions : 0m;
            var avgItem = totalUnits > 0 ? totalRevenue / totalUnits : 0m;

            return new SalesSummaryDto(totalRevenue, totalTransactions, totalUnits, avgBasket, avgItem);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            return null;
        }
    }

    private static async Task<TopProductsResult?> TryGetTopProductsFromAggregatesAsync(
        ITrendplusDbContext db,
        int top,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        try
        {
            await using var conn = await OpenTrendplusConnectionAsync(db, ct);
            if (conn is null) return null;

            const string sql = """
                SELECT
                    "ProductId",
                    COALESCE("ProductName", 'Nepoznato') AS product_name,
                    COALESCE(SUM("TotalRevenue"), 0) AS total_revenue,
                    COALESCE(SUM("TotalUnits"), 0)::int AS total_units
                FROM "AnalyticsTopProducts"
                WHERE (@fromDate IS NULL OR "Date" >= @fromDate::date)
                  AND (@toDate IS NULL OR "Date" <= @toDate::date)
                GROUP BY "ProductId", COALESCE("ProductName", 'Nepoznato');
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            AddNullableDateParameter(cmd, "fromDate", fromDate);
            AddNullableDateParameter(cmd, "toDate", toDate);

            var all = new List<TopProductDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                all.Add(new TopProductDto(
                    ProductId: reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    ProductName: reader.IsDBNull(1) ? "Nepoznato" : reader.GetString(1),
                    TotalRevenue: reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
                    TotalUnits: reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    Velicina: null,
                    Boja: null));
            }

            if (all.Count == 0) return null;

            var byRevenue = all.OrderByDescending(x => x.TotalRevenue).Take(top).ToList();
            var byUnits = all.OrderByDescending(x => x.TotalUnits).Take(top).ToList();
            return new TopProductsResult(byRevenue, byUnits);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            return null;
        }
    }

    private static async Task<List<DailySaleDto>?> TryGetDailySalesFromAggregatesAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        try
        {
            await using var conn = await OpenTrendplusConnectionAsync(db, ct);
            if (conn is null) return null;

            const string sql = """
                SELECT
                    "Date",
                    COALESCE("TotalRevenue", 0) AS total_revenue,
                    COALESCE("TotalTransactions", 0)::int AS total_transactions,
                    COALESCE("TotalUnits", 0)::int AS total_units
                FROM "AnalyticsDailySummary"
                WHERE (@fromDate IS NULL OR "Date" >= @fromDate::date)
                  AND (@toDate IS NULL OR "Date" <= @toDate::date)
                ORDER BY "Date";
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            AddNullableDateParameter(cmd, "fromDate", fromDate);
            AddNullableDateParameter(cmd, "toDate", toDate);

            var list = new List<DailySaleDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var date = reader.IsDBNull(0) ? DateTime.UtcNow.Date : reader.GetDateTime(0);
                list.Add(new DailySaleDto
                {
                    Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    TotalRevenue = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                    TransactionCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    TotalUnits = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                });
            }

            return list;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            return null;
        }
    }

    private static async Task<List<CategoryDataDto>?> TryGetCategoryDataFromAggregatesAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        try
        {
            await using var conn = await OpenTrendplusConnectionAsync(db, ct);
            if (conn is null) return null;

            const string sql = """
                SELECT
                    COALESCE("Kategorija", 'Ostalo') AS kategorija,
                    COALESCE(SUM("TotalRevenue"), 0) AS total_revenue,
                    COALESCE(SUM("TotalUnits"), 0)::int AS total_units,
                    COALESCE(SUM("TransactionCount"), 0)::int AS transaction_count
                FROM "AnalyticsCategorySummary"
                WHERE (@fromDate IS NULL OR "Date" >= @fromDate::date)
                  AND (@toDate IS NULL OR "Date" <= @toDate::date)
                GROUP BY COALESCE("Kategorija", 'Ostalo')
                ORDER BY total_revenue DESC;
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            AddNullableDateParameter(cmd, "fromDate", fromDate);
            AddNullableDateParameter(cmd, "toDate", toDate);

            var list = new List<CategoryDataDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new CategoryDataDto
                {
                    Kategorija = reader.IsDBNull(0) ? "Ostalo" : reader.GetString(0),
                    Pol = "Ukupno",
                    TotalRevenue = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                    TotalUnits = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    TransactionCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                });
            }

            return list;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            return null;
        }
    }

    private static async Task<List<GenderDataDto>?> TryGetGenderDataFromAggregatesAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        try
        {
            await using var conn = await OpenTrendplusConnectionAsync(db, ct);
            if (conn is null) return null;

            const string sql = """
                SELECT
                    COALESCE("Pol", 'Neodređeno') AS pol,
                    COALESCE(SUM("TotalRevenue"), 0) AS total_revenue,
                    COALESCE(SUM("TotalUnits"), 0)::int AS total_units
                FROM "AnalyticsGenderSummary"
                WHERE (@fromDate IS NULL OR "Date" >= @fromDate::date)
                  AND (@toDate IS NULL OR "Date" <= @toDate::date)
                GROUP BY COALESCE("Pol", 'Neodređeno')
                ORDER BY total_revenue DESC;
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            AddNullableDateParameter(cmd, "fromDate", fromDate);
            AddNullableDateParameter(cmd, "toDate", toDate);

            var list = new List<GenderDataDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new GenderDataDto
                {
                    Pol = reader.IsDBNull(0) ? "Neodređeno" : reader.GetString(0),
                    TotalRevenue = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                    TotalUnits = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
                });
            }

            return list;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            return null;
        }
    }

    private static async Task<List<SupplierDataDto>?> TryGetSupplierDataFromAggregatesAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        try
        {
            await using var conn = await OpenTrendplusConnectionAsync(db, ct);
            if (conn is null) return null;

            const string sql = """
                SELECT
                    "DobavljacId",
                    COALESCE("DobavljacNaziv", 'Nepoznato') AS dobavljac_naziv,
                    COALESCE(SUM("TotalRevenue"), 0) AS total_revenue,
                    COALESCE(SUM("TotalUnits"), 0)::int AS total_units,
                    COALESCE(SUM("TransactionCount"), 0)::int AS transaction_count
                FROM "AnalyticsSupplierSummary"
                WHERE (@fromDate IS NULL OR "Date" >= @fromDate::date)
                  AND (@toDate IS NULL OR "Date" <= @toDate::date)
                GROUP BY "DobavljacId", COALESCE("DobavljacNaziv", 'Nepoznato')
                ORDER BY total_revenue DESC;
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            AddNullableDateParameter(cmd, "fromDate", fromDate);
            AddNullableDateParameter(cmd, "toDate", toDate);

            var list = new List<SupplierDataDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new SupplierDataDto
                {
                    DobavljacId = reader.IsDBNull(0) ? null : reader.GetInt32(0),
                    DobavljacNaziv = reader.IsDBNull(1) ? "Nepoznato" : reader.GetString(1),
                    TotalRevenue = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
                    TotalUnits = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    TransactionCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                });
            }

            return list;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            return null;
        }
    }

    private static async Task<(decimal score, int totalSku, int missingSku, DateTime? lastImport, decimal freshnessHours)> GetCompletenessAndFreshnessAsync(
        ITrendplusDbContext db,
        CancellationToken ct)
    {
        await using var conn = await OpenTrendplusConnectionAsync(db, ct);
        if (conn is null) return (0m, 0, 0, null, 999m);

        try
        {
            const string healthSql = """
                SELECT completeness_score, last_import
                FROM vw_analytics_data_health
                LIMIT 1;
                """;
            await using var cmd = new NpgsqlCommand(healthSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var score = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
                var lastImport = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
                var freshnessHours = lastImport.HasValue
                    ? Math.Max(0m, Math.Round((decimal)(DateTime.UtcNow - lastImport.Value.ToUniversalTime()).TotalHours, 2))
                    : 999m;

                return (score, 0, 0, lastImport, freshnessHours);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            // fallback below
        }

        const string fallbackSql = """
            SELECT
                COUNT(*)::int AS total_sku,
                COUNT(*) FILTER (
                  WHERE "Naziv" IS NULL OR "PLU" IS NULL OR "Kategorija" IS NULL
                )::int AS missing_sku,
                MAX("UpdatedAt") AS last_import
            FROM "Artikli";
            """;
        await using (var cmd = new NpgsqlCommand(fallbackSql, conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                var total = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                var missing = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                var lastImport = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                var score = total == 0 ? 0m : Math.Round(1m - ((decimal)missing / total), 4);
                var freshnessHours = lastImport.HasValue
                    ? Math.Max(0m, Math.Round((decimal)(DateTime.UtcNow - lastImport.Value.ToUniversalTime()).TotalHours, 2))
                    : 999m;
                return (score, total, missing, lastImport, freshnessHours);
            }
        }

        return (0m, 0, 0, null, 999m);
    }

    private static async Task<LostSalesSnapshot> GetLostSalesSnapshotAsync(
        ITrendplusDbContext db,
        CancellationToken ct,
        int? storeId = null,
        int? supplierId = null,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var hasSupplierFilter = supplierId.HasValue;

        await using var conn = await OpenTrendplusConnectionAsync(db, ct);
        if (conn is null)
        {
            return LostSalesSnapshot.Unavailable();
        }

        if (normalizedDataScope == "all" && !storeId.HasValue && !supplierId.HasValue)
        {
            try
            {
                const string viewSql = """
                    SELECT
                        COALESCE(SUM(is_oos), 0)::int AS oos_sku_count,
                        COALESCE(SUM(lost_sales_estimate), 0)::numeric(18,2) AS lost_sales_estimate
                    FROM vw_analytics_oos_lost_sales;
                    """;
                await using var cmd = new NpgsqlCommand(viewSql, conn);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    var oosCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    var lostSales = reader.IsDBNull(1) ? 0m : Math.Round(reader.GetDecimal(1), 2);
                    return lostSales <= 0m
                        ? LostSalesSnapshot.TrueZero(oosCount)
                        : LostSalesSnapshot.FromView(oosCount, lostSales);
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
            {
                // fallback below
            }
        }

        var recentSupplierJoin = hasSupplierFilter
            ? "JOIN \"Artikli\" a2 ON a2.\"Id\" = ps.\"id_artikal\""
            : string.Empty;
        var recentSupplierPredicate = hasSupplierFilter
            ? "AND a2.\"IDDobavljac\" = @supplierId"
            : string.Empty;

        var fallbackSql = $"""
            WITH recent AS (
              SELECT
                ps."id_artikal" AS article_id,
                AVG(ps."kolicina")::numeric(18,2) AS avg_units_per_sale,
                AVG(ps."cena")::numeric(18,2) AS avg_price
              FROM "prodaja_stavke" ps
              JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
              {recentSupplierJoin}
              WHERE p."datum_prodaje" >= NOW() - INTERVAL '30 days'
                AND (@storeId IS NULL OR p."id_objekat" = @storeId)
                {recentSupplierPredicate}
                AND (@scope <> 'imported' OR p."DataOrigin" = 'access')
                AND (@scope <> 'existing' OR p."DataOrigin" = 'existing' OR p."DataOrigin" IS NULL OR p."DataOrigin" = '')
              GROUP BY ps."id_artikal"
            )
            SELECT
              COUNT(*) FILTER (WHERE COALESCE(a."Kolicina", 0) <= 0)::int AS oos_sku_count,
              COALESCE(SUM(
                  CASE WHEN COALESCE(a."Kolicina", 0) <= 0
                           THEN COALESCE(r.avg_units_per_sale, 0) * COALESCE(r.avg_price, 0)
                           ELSE 0
                  END
              ), 0)::numeric(18,2) AS lost_sales_estimate
            FROM "Artikli" a
            LEFT JOIN recent r ON r.article_id = a."Id"
            WHERE (@storeId IS NULL OR a."IDObjekat" = @storeId)
              AND (@supplierId IS NULL OR a."IDDobavljac" = @supplierId)
              AND (@scope <> 'imported' OR a."DataOrigin" = 'access')
              AND (@scope <> 'existing' OR a."DataOrigin" = 'existing' OR a."DataOrigin" IS NULL OR a."DataOrigin" = '');
            """;
        try
        {
            await using var fallbackCmd = new NpgsqlCommand(fallbackSql, conn);
            fallbackCmd.Parameters.Add(new Npgsql.NpgsqlParameter("storeId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = (object?)storeId ?? DBNull.Value });
            fallbackCmd.Parameters.Add(new Npgsql.NpgsqlParameter("supplierId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = (object?)supplierId ?? DBNull.Value });
            fallbackCmd.Parameters.Add(new Npgsql.NpgsqlParameter("scope", NpgsqlTypes.NpgsqlDbType.Text) { Value = normalizedDataScope });
            await using var fallbackReader = await fallbackCmd.ExecuteReaderAsync(ct);
            if (await fallbackReader.ReadAsync(ct))
            {
                var oosCount = fallbackReader.IsDBNull(0) ? 0 : fallbackReader.GetInt32(0);
                var lostSales = fallbackReader.IsDBNull(1) ? 0m : Math.Round(fallbackReader.GetDecimal(1), 2);
                return LostSalesSnapshot.FromFallback(oosCount, lostSales);
            }
        }
        catch (PostgresException)
        {
            return LostSalesSnapshot.Unavailable();
        }

        return LostSalesSnapshot.Unavailable();
    }

    private static async Task<(int negativeQtyCount, int totalRows)> GetNegativeQuantityValidationAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        await using var conn = await OpenTrendplusConnectionAsync(db, ct);
        if (conn is null) return (0, 0);

        const string sql = """
            SELECT
              COUNT(*) FILTER (WHERE ps."kolicina" < 0)::int AS negative_qty_count,
              COUNT(*)::int AS total_rows
            FROM "prodaja_stavke" ps
            JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
            WHERE (@fromDate IS NULL OR p."datum_prodaje" >= @fromDate)
              AND (@toDate IS NULL OR p."datum_prodaje" <= @toDate);
            """;
        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("fromDate", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)fromDate ?? DBNull.Value });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("toDate", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)toDate ?? DBNull.Value });
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var negativeQtyCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                var totalRows = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                return (negativeQtyCount, totalRows);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            return (0, 0);
        }

        return (0, 0);
    }

    private static async Task<(decimal avgVelocity, decimal topVelocity, string topSku, decimal? trendPct)> GetVelocitySnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var hasSupplierFilter = supplierId.HasValue;
        var supplierJoin = hasSupplierFilter
            ? "JOIN \"Artikli\" a_filter ON a_filter.\"Id\" = ps.\"id_artikal\""
            : string.Empty;
        var supplierPredicate = hasSupplierFilter
            ? "AND a_filter.\"IDDobavljac\" = @supplierId"
            : string.Empty;

        await using var conn = await OpenTrendplusConnectionAsync(db, ct);
        if (conn is null) return (0m, 0m, "N/A", null);

        var velocitySql = $"""
            WITH base AS (
              SELECT
                ps."id_artikal" AS article_id,
                DATE(p."datum_prodaje") AS sale_day,
                SUM(ps."kolicina")::decimal AS units_day
              FROM "prodaja_stavke" ps
              JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
              {supplierJoin}
              WHERE (@fromDate IS NULL OR p."datum_prodaje" >= @fromDate)
                AND (@toDate IS NULL OR p."datum_prodaje" <= @toDate)
                AND (@storeId IS NULL OR p."id_objekat" = @storeId)
                {supplierPredicate}
                AND (@scope <> 'imported' OR p."DataOrigin" = 'access')
                AND (@scope <> 'existing' OR p."DataOrigin" = 'existing' OR p."DataOrigin" IS NULL OR p."DataOrigin" = '')
              GROUP BY ps."id_artikal", DATE(p."datum_prodaje")
            ),
            agg AS (
              SELECT
                article_id,
                SUM(units_day) / GREATEST(COUNT(*), 1) AS velocity
              FROM base
              GROUP BY article_id
            )
            SELECT
              COALESCE(AVG(agg.velocity), 0) AS avg_velocity,
              COALESCE(MAX(agg.velocity), 0) AS top_velocity,
              COALESCE((ARRAY_AGG(COALESCE(a."PLU", agg.article_id::text) ORDER BY agg.velocity DESC, agg.article_id))[1], 'N/A') AS top_sku
            FROM agg
            LEFT JOIN "Artikli" a ON a."Id" = agg.article_id;
            """;
        await using var cmd = new NpgsqlCommand(velocitySql, conn);
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("fromDate", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)fromDate ?? DBNull.Value });
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("toDate", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)toDate ?? DBNull.Value });
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("storeId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = (object?)storeId ?? DBNull.Value });
        if (hasSupplierFilter)
        {
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("supplierId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = supplierId!.Value });
        }
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("scope", NpgsqlTypes.NpgsqlDbType.Text) { Value = normalizedDataScope });
        decimal avgVelocity = 0m;
        decimal topVelocity = 0m;
        string topSku = "N/A";
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                avgVelocity = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
                topVelocity = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                topSku = reader.IsDBNull(2) ? "N/A" : reader.GetString(2);
            }
        }

        decimal? trendPct = null;
        try
        {
            var trendWindowFrom = fromDate?.Date;
            var trendWindowTo = toDate?.Date;

            if (!trendWindowFrom.HasValue || !trendWindowTo.HasValue || trendWindowFrom.Value > trendWindowTo.Value)
            {
                trendWindowTo = DateTime.UtcNow.Date;
                trendWindowFrom = trendWindowTo.Value.AddDays(-13);
            }

            var totalDays = Math.Max(1, (trendWindowTo.Value - trendWindowFrom.Value).Days + 1);
            var currentWindowDays = Math.Max(1, totalDays / 2);
            var lastFrom = trendWindowTo.Value.AddDays(-(currentWindowDays - 1));
            var prevFrom = trendWindowFrom.Value;
            var prevTo = lastFrom.AddDays(-1);

            var trendSql = $"""
                WITH line_daily AS (
                  SELECT
                    DATE(p."datum_prodaje") AS sale_day,
                    SUM(ps."kolicina")::decimal AS total_units
                  FROM "prodaja_stavke" ps
                  JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
                  {supplierJoin}
                  WHERE (@fromDate IS NULL OR p."datum_prodaje" >= @fromDate)
                    AND (@toDate IS NULL OR p."datum_prodaje" <= @toDate)
                    AND (@storeId IS NULL OR p."id_objekat" = @storeId)
                    {supplierPredicate}
                    AND (@scope <> 'imported' OR p."DataOrigin" = 'access')
                    AND (@scope <> 'existing' OR p."DataOrigin" = 'existing' OR p."DataOrigin" IS NULL OR p."DataOrigin" = '')
                  GROUP BY DATE(p."datum_prodaje")
                )
                SELECT
                  COALESCE(SUM(total_units) FILTER (WHERE sale_day >= @lastFrom::date AND sale_day <= @lastTo::date), 0) AS current_units,
                  COALESCE(SUM(total_units) FILTER (WHERE sale_day >= @prevFrom::date AND sale_day <= @prevTo::date), 0) AS previous_units
                FROM line_daily;
                """;
            await using var trendCmd = new NpgsqlCommand(trendSql, conn);
            trendCmd.Parameters.Add(new Npgsql.NpgsqlParameter("fromDate", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)fromDate ?? DBNull.Value });
            trendCmd.Parameters.Add(new Npgsql.NpgsqlParameter("toDate", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)toDate ?? DBNull.Value });
            trendCmd.Parameters.Add(new Npgsql.NpgsqlParameter("storeId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = (object?)storeId ?? DBNull.Value });
            if (hasSupplierFilter)
            {
                trendCmd.Parameters.Add(new Npgsql.NpgsqlParameter("supplierId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = supplierId!.Value });
            }
            trendCmd.Parameters.Add(new Npgsql.NpgsqlParameter("scope", NpgsqlTypes.NpgsqlDbType.Text) { Value = normalizedDataScope });
            trendCmd.Parameters.Add(new Npgsql.NpgsqlParameter("lastFrom", NpgsqlTypes.NpgsqlDbType.Date) { Value = lastFrom });
            trendCmd.Parameters.Add(new Npgsql.NpgsqlParameter("lastTo", NpgsqlTypes.NpgsqlDbType.Date) { Value = trendWindowTo.Value });
            trendCmd.Parameters.Add(new Npgsql.NpgsqlParameter("prevFrom", NpgsqlTypes.NpgsqlDbType.Date) { Value = prevFrom });
            trendCmd.Parameters.Add(new Npgsql.NpgsqlParameter("prevTo", NpgsqlTypes.NpgsqlDbType.Date) { Value = prevTo });
            await using var trendReader = await trendCmd.ExecuteReaderAsync(ct);
            if (await trendReader.ReadAsync(ct))
            {
                var currentUnits = trendReader.IsDBNull(0) ? 0m : trendReader.GetDecimal(0);
                var previousUnits = trendReader.IsDBNull(1) ? 0m : trendReader.GetDecimal(1);
                trendPct = previousUnits <= 0m
                    ? (currentUnits > 0m ? 100m : 0m)
                    : Math.Round(((currentUnits - previousUnits) / previousUnits) * 100m, 2);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            trendPct = null;
        }

        return (Math.Round(avgVelocity, 2), Math.Round(topVelocity, 2), topSku, trendPct);
    }

    private static async Task<(decimal top20Share, decimal top50Share)> GetParetoSnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var hasSupplierFilter = supplierId.HasValue;

        await using var conn = await OpenTrendplusConnectionAsync(db, ct);
        if (conn is null) return (0m, 0m);

        if (normalizedDataScope == "all" && !fromDate.HasValue && !toDate.HasValue && !storeId.HasValue && !supplierId.HasValue)
        {
            try
            {
                const string paretoSql = """
                    WITH ranked AS (
                      SELECT
                        revenue,
                        cumulative_share,
                        ROW_NUMBER() OVER (ORDER BY revenue DESC, article_id) AS rn
                      FROM vw_analytics_pareto
                    )
                    SELECT
                      COALESCE(MAX(cumulative_share) FILTER (WHERE rn <= 20), 0),
                      COALESCE(MAX(cumulative_share) FILTER (WHERE rn <= 50), 0)
                    FROM ranked;
                    """;
                await using var cmd = new NpgsqlCommand(paretoSql, conn);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    var top20 = reader.IsDBNull(0) ? 0m : reader.GetDecimal(0);
                    var top50 = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                    return (Math.Round(top20, 4), Math.Round(top50, 4));
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
            {
                // fallback below
            }
        }

        var supplierJoin = hasSupplierFilter
            ? "JOIN \"Artikli\" a ON a.\"Id\" = ps.\"id_artikal\""
            : string.Empty;
        var supplierPredicate = hasSupplierFilter
            ? "AND a.\"IDDobavljac\" = @supplierId"
            : string.Empty;

        var fallbackSql = $"""
            WITH ranked AS (
              SELECT SUM(ps."kolicina" * ps."cena") AS revenue
              FROM "prodaja_stavke" ps
              JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
              {supplierJoin}
              WHERE (@fromDate IS NULL OR p."datum_prodaje" >= @fromDate)
                AND (@toDate IS NULL OR p."datum_prodaje" <= @toDate)
                AND (@storeId IS NULL OR p."id_objekat" = @storeId)
                {supplierPredicate}
                AND (@scope <> 'imported' OR p."DataOrigin" = 'access')
                AND (@scope <> 'existing' OR p."DataOrigin" = 'existing' OR p."DataOrigin" IS NULL OR p."DataOrigin" = '')
              GROUP BY ps."id_artikal"
            ),
            ordered AS (
              SELECT
                revenue,
                ROW_NUMBER() OVER (ORDER BY revenue DESC) AS rn,
                CASE
                  WHEN SUM(revenue) OVER () = 0 THEN 0
                  ELSE SUM(revenue) OVER (ORDER BY revenue DESC ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)
                       / SUM(revenue) OVER ()
                END AS cumulative_share
              FROM ranked
            )
            SELECT
              COALESCE(MAX(cumulative_share) FILTER (WHERE rn <= 20), 0),
              COALESCE(MAX(cumulative_share) FILTER (WHERE rn <= 50), 0)
            FROM ordered;
            """;
        await using var fallbackCmd = new NpgsqlCommand(fallbackSql, conn);
        fallbackCmd.Parameters.Add(new Npgsql.NpgsqlParameter("fromDate", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)fromDate ?? DBNull.Value });
        fallbackCmd.Parameters.Add(new Npgsql.NpgsqlParameter("toDate", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)toDate ?? DBNull.Value });
        fallbackCmd.Parameters.Add(new Npgsql.NpgsqlParameter("storeId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = (object?)storeId ?? DBNull.Value });
        if (hasSupplierFilter)
        {
            fallbackCmd.Parameters.Add(new Npgsql.NpgsqlParameter("supplierId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = supplierId!.Value });
        }
        fallbackCmd.Parameters.Add(new Npgsql.NpgsqlParameter("scope", NpgsqlTypes.NpgsqlDbType.Text) { Value = normalizedDataScope });
        await using var fallbackReader = await fallbackCmd.ExecuteReaderAsync(ct);
        if (await fallbackReader.ReadAsync(ct))
        {
            var top20 = fallbackReader.IsDBNull(0) ? 0m : fallbackReader.GetDecimal(0);
            var top50 = fallbackReader.IsDBNull(1) ? 0m : fallbackReader.GetDecimal(1);
            return (Math.Round(top20, 4), Math.Round(top50, 4));
        }

        return (0m, 0m);
    }

    private static async Task<TopProductsAdvancedResultDto> GetTopProductsAdvancedSnapshotAsync(
        ITrendplusDbContext db,
        int top,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var hasSupplierFilter = supplierId.HasValue;
        var previousSupplierJoin = hasSupplierFilter
            ? "JOIN \"Artikli\" a_prev ON a_prev.\"Id\" = ps.\"id_artikal\""
            : string.Empty;
        var previousSupplierPredicate = hasSupplierFilter
            ? "AND a_prev.\"IDDobavljac\" = @supplierId"
            : string.Empty;

        await using var conn = await OpenTrendplusConnectionAsync(db, ct);
        if (conn is null)
        {
            return new TopProductsAdvancedResultDto
            {
                MarginAvailable = false,
                MarginMessage = "Konekcija ka bazi nije dostupna."
            };
        }

        var safeTop = Math.Max(1, Math.Min(top, 100));
        var resolvedUnitCostSql = AnalyticsMarginPolicy.BuildPositiveCostSql(@"ps.""nabavna_cena""", @"a.""NabavnaCenaDin""", @"a.""NabavnaCena""");

        var sql = $"""
            WITH period_meta AS (
              SELECT
                COALESCE(@fromDate::date, (SELECT MIN(DATE(p."datum_prodaje")) FROM "prodaja_zaglavlje" p), CURRENT_DATE) AS from_date,
                COALESCE(@toDate::date, (SELECT MAX(DATE(p."datum_prodaje")) FROM "prodaja_zaglavlje" p), CURRENT_DATE) AS to_date
            ),
            period_size AS (
              SELECT
                from_date,
                to_date,
                GREATEST((to_date - from_date + 1), 1)::int AS days_count
              FROM period_meta
            ),
            current_period AS (
              SELECT
                ps."id_artikal" AS product_id,
                SUM(ps."kolicina" * ps."cena") AS revenue,
                SUM(ps."kolicina")::int AS units,
                GREATEST(COUNT(DISTINCT DATE(p."datum_prodaje")), 1)::int AS active_days,
                CASE
                  WHEN COUNT(*) FILTER (WHERE {resolvedUnitCostSql} IS NOT NULL) = 0 THEN NULL
                  ELSE SUM((ps."cena" - {resolvedUnitCostSql}) * ps."kolicina")
                END AS margin_impact
              FROM "prodaja_stavke" ps
              JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
              JOIN "Artikli" a ON a."Id" = ps."id_artikal"
              WHERE (@fromDate IS NULL OR p."datum_prodaje" >= @fromDate)
                AND (@toDate IS NULL OR p."datum_prodaje" <= @toDate)
                AND (@storeId IS NULL OR p."id_objekat" = @storeId)
                AND (@supplierId IS NULL OR a."IDDobavljac" = @supplierId)
                AND (@scope <> 'imported' OR p."DataOrigin" = 'access')
                AND (@scope <> 'existing' OR p."DataOrigin" = 'existing' OR p."DataOrigin" IS NULL OR p."DataOrigin" = '')
              GROUP BY ps."id_artikal"
            ),
            previous_period AS (
              SELECT
                ps."id_artikal" AS product_id,
                SUM(ps."kolicina")::decimal AS prev_units
              FROM "prodaja_stavke" ps
              JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
              {previousSupplierJoin}
              CROSS JOIN period_size s
              WHERE p."datum_prodaje" >= (s.from_date - (s.days_count * INTERVAL '1 day'))
                AND p."datum_prodaje" < s.from_date
                AND (@storeId IS NULL OR p."id_objekat" = @storeId)
                {previousSupplierPredicate}
                AND (@scope <> 'imported' OR p."DataOrigin" = 'access')
                AND (@scope <> 'existing' OR p."DataOrigin" = 'existing' OR p."DataOrigin" IS NULL OR p."DataOrigin" = '')
              GROUP BY ps."id_artikal"
            )
            SELECT
              cp.product_id,
              COALESCE(a."PLU", cp.product_id::text) AS sku,
              COALESCE(a."Naziv", 'Nepoznato') AS product_name,
              COALESCE(cp.revenue, 0) AS revenue,
              COALESCE(cp.units, 0)::int AS units,
              ROUND(COALESCE(cp.units, 0)::decimal / GREATEST(cp.active_days, 1), 2) AS velocity_units_per_day,
              cp.margin_impact,
              CASE
                WHEN COALESCE(a."Kolicina", 0) <= 0 THEN 'critical'
                WHEN COALESCE(a."Kolicina", 0) <= GREATEST(COALESCE(a."MinimalnaKolicina", 1), 1) THEN 'warning'
                ELSE 'good'
              END AS stock_status,
              CASE
                WHEN COALESCE(pp.prev_units, 0) = 0 AND COALESCE(cp.units, 0) > 0 THEN 100
                WHEN COALESCE(pp.prev_units, 0) = 0 THEN 0
                ELSE ROUND(((COALESCE(cp.units, 0)::decimal - pp.prev_units) / pp.prev_units) * 100, 2)
              END AS trend_pct
            FROM current_period cp
            JOIN "Artikli" a ON a."Id" = cp.product_id
            LEFT JOIN previous_period pp ON pp.product_id = cp.product_id;
            """;

        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("fromDate", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)fromDate ?? DBNull.Value });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("toDate", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)toDate ?? DBNull.Value });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("storeId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = (object?)storeId ?? DBNull.Value });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("supplierId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = (object?)supplierId ?? DBNull.Value });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("scope", NpgsqlTypes.NpgsqlDbType.Text) { Value = normalizedDataScope });

            var all = new List<TopProductAdvancedItemDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var hasMarginImpact = !reader.IsDBNull(6);
                var marginQualityTier = hasMarginImpact ? "good" : "insufficient_data";
                var marginQualityLabel = hasMarginImpact
                    ? "Margin signal dostupan"
                    : "Nedovoljno podataka";
                var marginQualityShortLabel = hasMarginImpact
                    ? "Dostupno"
                    : "Nedostaje dokaz";
                var marginQualityTooltip = hasMarginImpact
                    ? "Margin impact je izračunat iz dostupne nabavne cene."
                    : "Nabavna cena nije dostupna, pa margin signal nije potvrđen.";
                var dataQualityStatus = hasMarginImpact ? "good" : "insufficient_data";
                var statusReason = hasMarginImpact
                    ? "Margin signal je potvrđen na osnovu dostupne nabavne cene."
                    : "Nabavna cena nije dostupna za ovaj artikal.";

                all.Add(new TopProductAdvancedItemDto
                {
                    ProductId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    Sku = reader.IsDBNull(1) ? "N/A" : reader.GetString(1),
                    ProductName = reader.IsDBNull(2) ? "Nepoznato" : reader.GetString(2),
                    Revenue = reader.IsDBNull(3) ? 0m : Math.Round(reader.GetDecimal(3), 2),
                    Units = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    VelocityUnitsPerDay = reader.IsDBNull(5) ? 0m : Math.Round(reader.GetDecimal(5), 2),
                    MarginImpact = reader.IsDBNull(6) ? null : Math.Round(reader.GetDecimal(6), 2),
                    StockStatus = reader.IsDBNull(7) ? "neutral" : reader.GetString(7),
                    TrendPct = reader.IsDBNull(8) ? null : Math.Round(reader.GetDecimal(8), 2),
                    MarginQualityLabel = marginQualityLabel,
                    MarginQualityTier = marginQualityTier,
                    MarginQualityShortLabel = marginQualityShortLabel,
                    MarginQualityTooltip = marginQualityTooltip,
                    DataQualityStatus = dataQualityStatus,
                    StatusReason = statusReason,
                    ReasonCodes = hasMarginImpact ? ["margin_available"] : ["missing_cost"]
                });
            }

            var marginAvailable = all.Any(x => x.MarginImpact.HasValue);
            return new TopProductsAdvancedResultDto
            {
                ByRevenue = all.OrderByDescending(x => x.Revenue).Take(safeTop).ToList(),
                ByUnits = all.OrderByDescending(x => x.Units).Take(safeTop).ToList(),
                ByVelocity = all.OrderByDescending(x => x.VelocityUnitsPerDay).Take(safeTop).ToList(),
                ByMarginImpact = marginAvailable
                    ? all.Where(x => x.MarginImpact.HasValue).OrderByDescending(x => x.MarginImpact).Take(safeTop).ToList()
                    : [],
                MarginAvailable = marginAvailable,
                MarginMessage = marginAvailable ? null : "Nabavna cena nije dostupna za margin impact izracun."
            };
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            return new TopProductsAdvancedResultDto
            {
                MarginAvailable = false,
                MarginMessage = "Nedostaju tabele ili kolone za napredne top proizvode."
            };
        }
    }

    private static async Task<DashboardAdvancedSnapshotDto> BuildAdvancedDashboardSnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var (score, totalSku, missingSku, lastImport, freshnessHours) = await GetCompletenessAndFreshnessAsync(db, ct);
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var lostSalesSnapshot = await GetLostSalesSnapshotAsync(db, ct, storeId, supplierId, normalizedDataScope);
        var (avgVelocity, topVelocity, topSku, velocityTrend) = await GetVelocitySnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope);
        var (top20Share, top50Share) = await GetParetoSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope);

        var completenessStatus = score >= 0.98m ? "good" : score >= 0.90m ? "warning" : "critical";
        var freshnessStatus = freshnessHours <= 6m ? "good" : freshnessHours <= 24m ? "warning" : "critical";
        var oosValidation = BuildLostSalesValidationFromSnapshot(lostSalesSnapshot);
        var oosStatus = oosValidation.Status;
        var oosSkuCount = lostSalesSnapshot.OosSkuCount;
        var lostSalesEstimateDisplay = lostSalesSnapshot.LostSalesEstimate?.ToString("0.##", CultureInfo.InvariantCulture) ?? "n/a";
        var velocityStatus = avgVelocity > 0m ? "good" : "warning";
        var paretoStatus = top20Share > 0.80m ? "warning" : "good";

        var snapshot = new DashboardAdvancedSnapshotDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Cards =
            [
                new DashboardMetricCardDto
                {
                    Key = "velocity",
                    Label = "Velocity",
                    Value = avgVelocity,
                    Unit = "units/day",
                    TrendPct = velocityTrend,
                    Status = velocityStatus,
                    Subtitle = $"Top SKU: {topSku} ({topVelocity.ToString("0.##", CultureInfo.InvariantCulture)})"
                },
                new DashboardMetricCardDto
                {
                    Key = "oos",
                    Label = "OOS",
                    Value = oosSkuCount,
                    Unit = "SKU",
                    TrendPct = null,
                    Status = oosStatus,
                    Subtitle = $"Lost sales estimate: {lostSalesEstimateDisplay} RSD"
                },
                new DashboardMetricCardDto
                {
                    Key = "pareto",
                    Label = "Pareto",
                    Value = Math.Round(top20Share * 100m, 2),
                    Unit = "%",
                    TrendPct = null,
                    Status = paretoStatus,
                    Subtitle = $"Top 50 share: {Math.Round(top50Share * 100m, 2).ToString("0.##", CultureInfo.InvariantCulture)}%"
                },
                new DashboardMetricCardDto
                {
                    Key = "data_health",
                    Label = "Data Health",
                    Value = freshnessHours,
                    Unit = "hours old",
                    TrendPct = null,
                    Status = freshnessStatus,
                    Subtitle = lastImport.HasValue
                        ? $"Last import: {lastImport.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC"
                        : "Last import: N/A"
                },
                new DashboardMetricCardDto
                {
                    Key = "completeness",
                    Label = "Completeness",
                    Value = Math.Round(score * 100m, 2),
                    Unit = "%",
                    TrendPct = null,
                    Status = completenessStatus,
                    Subtitle = totalSku > 0 ? $"Missing: {missingSku}/{totalSku}" : "Missing: N/A"
                }
            ]
        };

        if (oosStatus != "good")
        {
            snapshot.Insights.Add(new DashboardInsightDto
            {
                Badge = "OOS",
                Description = $"OOS signal: {oosSkuCount} SKU, estimated lost sales {lostSalesEstimateDisplay} RSD.",
                Color = oosStatus == "critical" ? "red" : "yellow"
            });
            snapshot.Actions.Add(new DashboardActionDto
            {
                Priority = oosStatus == "critical" ? "P1" : "P2",
                Title = "Replenishment",
                Recommendation = "Prioritize refill for OOS/low-stock SKUs with highest velocity.",
                RecommendationAllowed = true,
                DataQualityStatus = oosStatus,
                StatusReason = "Lost sales estimate indicates stock-out pressure."
            });
            snapshot.Validations.Add(new DashboardValidationDto
            {
                Severity = oosStatus == "critical" ? "error" : "warning",
                Message = "Lost sales estimate indicates stock-out pressure."
            });
        }

        if (completenessStatus != "good")
        {
            snapshot.Insights.Add(new DashboardInsightDto
            {
                Badge = "Data",
                Description = $"Completeness is {Math.Round(score * 100m, 2).ToString("0.##", CultureInfo.InvariantCulture)}%, missing core fields for {missingSku} SKU.",
                Color = completenessStatus == "critical" ? "red" : "yellow"
            });
            snapshot.Actions.Add(new DashboardActionDto
            {
                Priority = "P1",
                Title = "Data quality fix",
                Recommendation = "Backfill PLU, name and category for missing SKUs before pricing decisions.",
                RecommendationAllowed = true,
                DataQualityStatus = completenessStatus,
                StatusReason = "Completeness validation is below target."
            });
            snapshot.Validations.Add(new DashboardValidationDto
            {
                Severity = completenessStatus == "critical" ? "error" : "warning",
                Message = "Completeness validation is below target."
            });
        }

        if (freshnessStatus != "good")
        {
            snapshot.Insights.Add(new DashboardInsightDto
            {
                Badge = "Freshness",
                Description = $"Data is {freshnessHours.ToString("0.##", CultureInfo.InvariantCulture)}h old.",
                Color = freshnessStatus == "critical" ? "red" : "yellow"
            });
            snapshot.Actions.Add(new DashboardActionDto
            {
                Priority = freshnessStatus == "critical" ? "P1" : "P2",
                Title = "Refresh pipeline",
                Recommendation = "Run import sync and refresh aggregate summaries.",
                RecommendationAllowed = true,
                DataQualityStatus = freshnessStatus,
                StatusReason = "Freshness validation indicates stale data."
            });
            snapshot.Validations.Add(new DashboardValidationDto
            {
                Severity = freshnessStatus == "critical" ? "error" : "warning",
                Message = "Freshness validation indicates stale data."
            });
        }

        if (paretoStatus == "warning")
        {
            snapshot.Insights.Add(new DashboardInsightDto
            {
                Badge = "Pareto",
                Description = $"Top 20 SKUs contribute {Math.Round(top20Share * 100m, 2).ToString("0.##", CultureInfo.InvariantCulture)}% of revenue.",
                Color = "yellow"
            });
            snapshot.Actions.Add(new DashboardActionDto
            {
                Priority = "P3",
                Title = "Portfolio balance",
                Recommendation = "Diversify sales concentration by promoting medium-performing SKUs.",
                RecommendationAllowed = false,
                DataQualityStatus = paretoStatus,
                StatusReason = "Pareto concentration is elevated."
            });
            snapshot.Validations.Add(new DashboardValidationDto
            {
                Severity = "info",
                Message = "Pareto concentration is elevated."
            });
        }

        if (snapshot.Insights.Count == 0)
        {
            snapshot.Insights.Add(new DashboardInsightDto
            {
                Badge = "OK",
                Description = "Sve je u redu: ključne validacije su u zelenoj zoni.",
                Color = "green"
            });
        }

        if (snapshot.Actions.Count == 0)
        {
            snapshot.Actions.Add(new DashboardActionDto
            {
                Priority = "P3",
                Title = "Monitor",
                Recommendation = "Nastavite monitoring metrika i osvežavajte agregate dnevno."
            });
        }

        return snapshot;
    }

    internal static List<DashboardDecisionActionDto> BuildDashboardDecisionActions(
        ProductDecisionCenterResponseDto? productDecisionSnapshot,
        DashboardAdvancedSnapshotDto? advancedSnapshot,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId)
    {
        var actions = new List<DashboardDecisionActionDto>();

        if (productDecisionSnapshot?.Rows is { Count: > 0 })
        {
            var rows = productDecisionSnapshot.Rows;

            TryAddDecisionActionFromRows(
                actions,
                rows,
                recommendationStatus: "REPLENISH",
                minRows: 2,
                minConfidence: 55,
                priority: "P1",
                title: "Dopuni artikle sa visokim velocity i niskom zalihom",
                impactTemplate: "Smanjenje procenjene izgubljene prodaje oko {0} RSD.",
                sourceType: "inventory",
                actionPath: "/analytics/inventory",
                actionTypeKey: "replenish",
                fromDate: fromDate,
                toDate: toDate,
                storeId: storeId,
                supplierId: supplierId);

            TryAddDecisionActionFromRows(
                actions,
                rows,
                recommendationStatus: "FIX_DATA",
                minRows: 1,
                minConfidence: 0,
                priority: "P1",
                title: "Proveri artikle bez dobavljača, nabavne cene ili kategorije",
                impactTemplate: "Bez ispravke podataka preporuke ostaju nepouzdane za {0} RSD prometa.",
                sourceType: "data_quality",
                actionPath: "/analytics/data-quality",
                actionTypeKey: "fix_data",
                fromDate: fromDate,
                toDate: toDate,
                storeId: storeId,
                supplierId: supplierId);

            TryAddDecisionActionFromRows(
                actions,
                rows,
                recommendationStatus: "MARKDOWN",
                minRows: 2,
                minConfidence: 55,
                priority: "P1",
                title: "Snizi artikle sa starom zalihom i slabom prodajom",
                impactTemplate: "Ubrzanje obrta na sporoj zalihi vrednoj oko {0} RSD.",
                sourceType: "nivelacija",
                actionPath: "/analytics/pre-nivelacija-prioriteti",
                actionTypeKey: "markdown",
                fromDate: fromDate,
                toDate: toDate,
                storeId: storeId,
                supplierId: supplierId);

            TryAddDecisionActionFromRows(
                actions,
                rows,
                recommendationStatus: "BOOST",
                minRows: 1,
                minConfidence: 60,
                priority: "P2",
                title: "Pojačaj artikle sa rastom i zdravom maržom",
                impactTemplate: "Potencijal dodatnog rasta kroz artikle sa prometom oko {0} RSD.",
                sourceType: "product",
                actionPath: "/analytics/products",
                actionTypeKey: "boost",
                fromDate: fromDate,
                toDate: toDate,
                storeId: storeId,
                supplierId: supplierId);

            TryAddDecisionActionFromRows(
                actions,
                rows,
                recommendationStatus: "DO_NOT_ORDER",
                minRows: 2,
                minConfidence: 55,
                priority: "P2",
                title: "Zaustavi porudžbine artikala sa padom i viškom zalihe",
                impactTemplate: "Smanjenje vezanog kapitala na artiklima vrednim oko {0} RSD.",
                sourceType: "product",
                actionPath: "/analytics/products",
                actionTypeKey: "do_not_order",
                fromDate: fromDate,
                toDate: toDate,
                storeId: storeId,
                supplierId: supplierId);

            TryAddDecisionActionFromRows(
                actions,
                rows,
                recommendationStatus: "INSUFFICIENT_DATA",
                minRows: 3,
                minConfidence: 0,
                priority: "P3",
                title: "Proveri artikle sa velikim padom ili nedovoljno signala",
                impactTemplate: "Potrebna ručna provera za grupu artikala sa prometom oko {0} RSD.",
                sourceType: "product",
                actionPath: "/analytics/products",
                actionTypeKey: "insufficient_data",
                fromDate: fromDate,
                toDate: toDate,
                storeId: storeId,
                supplierId: supplierId);
        }

        if (actions.Count == 0 && advancedSnapshot?.Actions is { Count: > 0 })
        {
            foreach (var action in advancedSnapshot.Actions.Take(4))
            {
                var mappedLink = MapLegacyAdvancedActionLink(action.Title, fromDate, toDate, storeId, supplierId);
                var sourceType = ResolveDashboardSourceTypeFromActionUrl(mappedLink);
                var actionTypeKey = BuildDashboardActionTypeKey(action.Title);
                var sourceKey = BuildDashboardActionSourceKey(sourceType, actionTypeKey, fromDate, toDate, storeId, supplierId);
                actions.Add(new DashboardDecisionActionDto
                {
                    ActionKey = sourceKey,
                    SourceType = sourceType,
                    Priority = string.IsNullOrWhiteSpace(action.Priority) ? "P3" : action.Priority,
                    Title = TranslateLegacyActionTitle(action.Title),
                    Description = action.Recommendation,
                    Reason = action.Recommendation,
                    StatusReason = ResolveDashboardLegacyActionStatusReason(action),
                    RecommendationStatus = null,
                    ExpectedImpact = null,
                    ImpactEstimateRsd = null,
                    ConfidencePct = action.ConfidencePct,
                    ReliabilityPct = action.ReliabilityPct,
                    RecommendationAllowed = action.RecommendationAllowed ?? false,
                    DataQualityStatus = string.IsNullOrWhiteSpace(action.DataQualityStatus)
                        ? "insufficient_data"
                        : action.DataQualityStatus,
                    ActionUrl = mappedLink,
                    Metadata = new Dictionary<string, object?>
                    {
                        ["actionType"] = actionTypeKey,
                        ["periodFrom"] = FormatDashboardActionDate(fromDate),
                        ["periodTo"] = FormatDashboardActionDate(toDate),
                        ["storeId"] = storeId?.ToString(CultureInfo.InvariantCulture) ?? "all",
                        ["supplierId"] = supplierId?.ToString(CultureInfo.InvariantCulture) ?? "all",
                        ["legacyAction"] = true
                    },
                    Link = mappedLink,
                    LinkLabel = "Otvori povezani ekran"
                });
            }
        }

        return actions
            .Select(action =>
            {
                action.RecommendationAllowed = action.RecommendationStatus switch
                {
                    "INSUFFICIENT_DATA" => false,
                    "FIX_DATA" => false,
                    null => action.RecommendationAllowed,
                    _ => true
                };
                return action;
            })
            .OrderBy(a => DecisionPriorityRank(a.Priority))
            .ThenByDescending(a => a.ConfidencePct ?? 0)
            .Take(8)
            .ToList();
    }

    private static ExecutiveDashboardSnapshotDto BuildExecutiveDashboardSnapshot(
        ProductDecisionCenterResponseDto? productDecisionSnapshot,
        SalesSummaryDto? salesSummary,
        DashboardValidationEndpointDto? validationFreshness,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId)
    {
        var snapshot = new ExecutiveDashboardSnapshotDto();

        var rows = productDecisionSnapshot?.Rows ?? [];
        if (rows.Count == 0)
        {
            snapshot.TotalMarginContributionRsd = 0m;
            snapshot.InventoryDangerValueRsd = 0m;
            snapshot.DataQualitySummary = new ExecutiveDataQualitySummaryDto
            {
                MissingSupplierCount = 0,
                MissingCostCount = 0,
                InsufficientSignalCount = 0,
                IgnoredRowsCount = productDecisionSnapshot?.IgnoredRowsCount ?? 0,
                ZeroRevenueRowsCount = 0,
                FreshnessStatus = validationFreshness?.Status ?? "unknown"
            };
            return snapshot;
        }

        snapshot.TotalMarginContributionRsd = rows.Sum(x => x.MarginContribution);
        snapshot.InventoryDangerValueRsd = productDecisionSnapshot?.Summary?.SlowStockCapital
            ?? rows.Sum(x => x.SlowStockCapital);

        // Kvalitet podataka: brojimo eksplicitno "rupe" (dobavljač, nabavna cena) i redove koji nemaju signal.
        snapshot.DataQualitySummary = new ExecutiveDataQualitySummaryDto
        {
            MissingSupplierCount = rows.Count(x => !x.SupplierId.HasValue || string.IsNullOrWhiteSpace(x.SupplierName)),
            MissingCostCount = rows.Count(x => x.MarginCoveragePct <= 1m),
            InsufficientSignalCount = rows.Count(x =>
                x.RecommendationStatus == "INSUFFICIENT_DATA" || x.RecommendationStatus == "FIX_DATA"),
            IgnoredRowsCount = productDecisionSnapshot?.IgnoredRowsCount ?? 0,
            ZeroRevenueRowsCount = rows.Count(x => x.Revenue <= 0m),
            FreshnessStatus = validationFreshness?.Status ?? "unknown"
        };

        snapshot.TopSuppliers = rows
            .GroupBy(x => new { x.SupplierId, SupplierName = (x.SupplierName ?? string.Empty).Trim() })
            .Select(group =>
            {
                var supplierIdValue = group.Key.SupplierId;
                var supplierNameValue = string.IsNullOrWhiteSpace(group.Key.SupplierName) ? "Nepoznat dobavljač" : group.Key.SupplierName;
                var linkSupplierId = supplierIdValue;

                var link = linkSupplierId.HasValue
                    ? BuildDashboardActionLink("/analytics/supplier", fromDate, toDate, storeId, linkSupplierId.Value)
                    : BuildDashboardActionLink("/analytics/data-quality", fromDate, toDate, storeId, supplierId);

                return new ExecutiveTopSupplierDto
                {
                    SupplierId = supplierIdValue,
                    SupplierName = supplierNameValue,
                    Revenue = group.Sum(x => x.Revenue),
                    MarginContribution = group.Sum(x => x.MarginContribution),
                    Link = link
                };
            })
            .OrderByDescending(x => x.MarginContribution)
            .Take(5)
            .ToList();

        snapshot.TopMarginProducts = rows
            .OrderByDescending(x => x.MarginContribution)
            .Take(5)
            .Select(row => new ExecutiveTopMarginItemDto
            {
                Key = row.ProductId.ToString(CultureInfo.InvariantCulture),
                Label = string.IsNullOrWhiteSpace(row.Sku)
                    ? row.ProductName
                    : $"{row.Sku} - {row.ProductName}",
                ItemType = "product",
                ProductId = row.ProductId,
                SupplierId = row.SupplierId,
                SupplierName = row.SupplierName,
                Revenue = row.Revenue,
                MarginContribution = row.MarginContribution,
                MarginPct = row.MarginPct,
                DataQualityStatus = row.DataQualityStatus,
                ConfidencePct = row.ConfidencePct,
                Link = BuildExecutiveProductLink(row.ProductId, fromDate, toDate, storeId, supplierId)
            })
            .ToList();

        snapshot.TopMarginCategories = rows
            .GroupBy(x =>
            {
                var category = (x.Category ?? x.TipObuce ?? string.Empty).Trim();
                return string.IsNullOrWhiteSpace(category) ? "Nepoznata kategorija" : category;
            }, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ExecutiveTopMarginItemDto
            {
                Key = group.Key,
                Label = group.Key,
                ItemType = "category",
                ProductId = null,
                SupplierId = null,
                SupplierName = null,
                Revenue = group.Sum(x => x.Revenue),
                MarginContribution = group.Sum(x => x.MarginContribution),
                MarginPct = null,
                DataQualityStatus = ResolveWorstDataQualityStatus(group.Select(x => x.DataQualityStatus)),
                ConfidencePct = (int)Math.Round(group.Average(x => (double)Math.Clamp(x.ConfidencePct, 0, 100))),
                Link = BuildDashboardActionLink("/analytics/products", fromDate, toDate, storeId, supplierId)
            })
            .OrderByDescending(x => x.MarginContribution)
            .Take(5)
            .ToList();

        snapshot.NegativeSignals = BuildExecutiveNegativeSignals(rows, fromDate, toDate, storeId, supplierId);

        return snapshot;
    }

    private static string BuildExecutiveProductLink(
        int productId,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId)
    {
        var basePath = $"/analitika/top-products/{productId}";
        var query = new List<string>();
        if (fromDate.HasValue) query.Add($"fromDate={Uri.EscapeDataString(fromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}");
        if (toDate.HasValue) query.Add($"toDate={Uri.EscapeDataString(toDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}");
        if (storeId.HasValue) query.Add($"storeId={storeId.Value}");
        if (supplierId.HasValue) query.Add($"supplierId={supplierId.Value}");
        return query.Count == 0 ? basePath : $"{basePath}?{string.Join("&", query)}";
    }

    private static List<ExecutiveNegativeSignalDto> BuildExecutiveNegativeSignals(
        List<ProductDecisionCenterRowDto> rows,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId)
    {
        var signals = new List<ExecutiveNegativeSignalDto>();

        // Kandidati za snizenje / spora zaliha (kapital vezan)
        foreach (var row in rows
            .Where(x => x.RecommendationStatus == "MARKDOWN")
            .OrderByDescending(x => x.SlowStockCapital)
            .ThenByDescending(x => x.Revenue)
            .Take(2))
        {
            signals.Add(new ExecutiveNegativeSignalDto
            {
                SignalType = "markdown",
                Priority = "P1",
                Title = $"Snizi: {row.Sku} {row.ProductName}",
                Description = string.IsNullOrWhiteSpace(row.RecommendationReason)
                    ? "Spor obrt i kapital vezan u zalihama."
                    : row.RecommendationReason,
                ImpactEstimateRsd = row.SlowStockCapital > 0 ? row.SlowStockCapital : null,
                ConfidencePct = row.ConfidencePct,
                DataQualityStatus = row.DataQualityStatus,
                RecommendationStatus = row.RecommendationStatus,
                RecommendationReason = row.RecommendationReason,
                ProductId = row.ProductId,
                Sku = row.Sku,
                ProductName = row.ProductName,
                SupplierName = row.SupplierName,
                Link = BuildDashboardActionLink("/analytics/pre-nivelacija-prioriteti", fromDate, toDate, storeId, supplierId)
            });
        }

        // Dead stock / 90+ dana bez prodaje (ili eksplicitno nema prodaje u periodu)
        foreach (var row in rows
            .Where(x => x.CurrentStock > 0 && (x.UnitsSold <= 0 || (x.DaysSinceLastSale.HasValue && x.DaysSinceLastSale.Value >= 90)))
            .OrderByDescending(x => x.SlowStockCapital)
            .ThenByDescending(x => x.Revenue)
            .Take(1))
        {
            signals.Add(new ExecutiveNegativeSignalDto
            {
                SignalType = "dead_stock",
                Priority = "P2",
                Title = $"Mrtva zaliha: {row.Sku} {row.ProductName}",
                Description = row.DaysSinceLastSale.HasValue
                    ? $"Nema prodaje {row.DaysSinceLastSale.Value} dana, a zaliha je {row.CurrentStock} kom."
                    : "Nema prodaje u periodu, a zaliha je i dalje prisutna.",
                ImpactEstimateRsd = row.SlowStockCapital > 0 ? row.SlowStockCapital : null,
                ConfidencePct = row.ConfidencePct,
                DataQualityStatus = row.DataQualityStatus,
                RecommendationStatus = row.RecommendationStatus,
                RecommendationReason = row.RecommendationReason,
                ProductId = row.ProductId,
                Sku = row.Sku,
                ProductName = row.ProductName,
                SupplierName = row.SupplierName,
                Link = BuildDashboardActionLink("/analytics/inventory", fromDate, toDate, storeId, supplierId)
            });
        }

        // Lost sales / OOS rizik (replenish + lostSalesEstimate)
        foreach (var row in rows
            .Where(x => x.RecommendationStatus == "REPLENISH" && x.LostSalesEstimate > 0m)
            .OrderByDescending(x => x.LostSalesEstimate)
            .ThenByDescending(x => x.VelocityUnitsPerDay)
            .Take(1))
        {
            signals.Add(new ExecutiveNegativeSignalDto
            {
                SignalType = "lost_sales",
                Priority = "P1",
                Title = $"Rizik rasprodaje: {row.Sku} {row.ProductName}",
                Description = string.IsNullOrWhiteSpace(row.RecommendationReason)
                    ? "Visok velocity i manjak zalihe; procenjena izgubljena prodaja raste."
                    : row.RecommendationReason,
                ImpactEstimateRsd = row.LostSalesEstimate > 0 ? row.LostSalesEstimate : null,
                ConfidencePct = row.ConfidencePct,
                DataQualityStatus = row.DataQualityStatus,
                RecommendationStatus = row.RecommendationStatus,
                RecommendationReason = row.RecommendationReason,
                ProductId = row.ProductId,
                Sku = row.Sku,
                ProductName = row.ProductName,
                SupplierName = row.SupplierName,
                Link = BuildDashboardActionLink("/analytics/inventory", fromDate, toDate, storeId, supplierId)
            });
        }

        // Veliki prihod ali niska marza (signal za pregovor / portfolio)
        foreach (var row in rows
            .Where(x => x.Revenue > 0m && x.MarginPct.HasValue && x.MarginPct.Value <= 5m && x.DataQualityStatus != "critical")
            .OrderByDescending(x => x.Revenue)
            .ThenByDescending(x => x.MarginPct)
            .Take(1))
        {
            signals.Add(new ExecutiveNegativeSignalDto
            {
                SignalType = "low_margin_high_revenue",
                Priority = "P2",
                Title = $"Niska marza: {row.Sku} {row.ProductName}",
                Description = $"Visok prihod ({row.Revenue.ToString("0.##", CultureInfo.InvariantCulture)} RSD) uz nisku marzu. Pregledaj cenu / nabavku.",
                ImpactEstimateRsd = null,
                ConfidencePct = row.ConfidencePct,
                DataQualityStatus = row.DataQualityStatus,
                RecommendationStatus = row.RecommendationStatus,
                RecommendationReason = row.RecommendationReason,
                ProductId = row.ProductId,
                Sku = row.Sku,
                ProductName = row.ProductName,
                SupplierName = row.SupplierName,
                Link = BuildDashboardActionLink("/analytics/products", fromDate, toDate, storeId, supplierId)
            });
        }

        // Fix data (ako postoji) - direktno vodi na data-quality
        foreach (var row in rows
            .Where(x => x.RecommendationStatus == "FIX_DATA")
            .OrderByDescending(x => x.Revenue)
            .Take(1))
        {
            signals.Add(new ExecutiveNegativeSignalDto
            {
                SignalType = "fix_data",
                Priority = "P1",
                Title = $"Proveri podatke: {row.Sku} {row.ProductName}",
                Description = string.IsNullOrWhiteSpace(row.RecommendationReason)
                    ? "Nedostaju ključni atributi (dobavljač, nabavna cena, kategorija) - preporuke nisu pouzdane."
                    : row.RecommendationReason,
                ImpactEstimateRsd = row.Revenue > 0 ? row.Revenue : null,
                ConfidencePct = row.ConfidencePct,
                DataQualityStatus = row.DataQualityStatus,
                RecommendationStatus = row.RecommendationStatus,
                RecommendationReason = row.RecommendationReason,
                ProductId = row.ProductId,
                Sku = row.Sku,
                ProductName = row.ProductName,
                SupplierName = row.SupplierName,
                Link = BuildDashboardActionLink("/analytics/data-quality", fromDate, toDate, storeId, supplierId)
            });
        }

        return signals
            .OrderBy(x => DecisionPriorityRank(x.Priority))
            .ThenByDescending(x => x.ImpactEstimateRsd ?? 0m)
            .Take(5)
            .ToList();
    }

    private static string ResolveWorstDataQualityStatus(IEnumerable<string> statuses)
    {
        // Statuses are already normalized to: good|warning|critical|insufficient_data|unknown.
        var worstRank = 0;
        string worst = "unknown";
        foreach (var status in statuses)
        {
            var normalized = (status ?? string.Empty).Trim();
            var rank = normalized switch
            {
                "critical" => 4,
                "warning" => 3,
                "good" => 2,
                "insufficient_data" => 1,
                _ => 0
            };

            if (rank > worstRank)
            {
                worstRank = rank;
                worst = string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
            }
        }

        return worst;
    }

    private static void TryAddDecisionActionFromRows(
        List<DashboardDecisionActionDto> actions,
        List<ProductDecisionCenterRowDto> rows,
        string recommendationStatus,
        int minRows,
        int minConfidence,
        string priority,
        string title,
        string impactTemplate,
        string sourceType,
        string actionPath,
        string actionTypeKey,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId)
    {
        var candidates = rows
            .Where(x => x.RecommendationStatus == recommendationStatus)
            .ToList();

        if (candidates.Count < minRows)
            return;

        var reliable = minConfidence <= 0
            ? candidates
            : candidates.Where(x => x.ConfidencePct >= minConfidence).ToList();

        if (reliable.Count == 0)
            return;

        var exemplar = reliable
            .OrderByDescending(x => x.ConfidencePct)
            .ThenByDescending(x => x.Revenue)
            .First();

        var avgConfidence = reliable.Count > 0
            ? (int)Math.Round(reliable.Average(x => x.ConfidencePct), MidpointRounding.AwayFromZero)
            : 0;
        var avgReliability = reliable.Count > 0
            ? (int?)Math.Round(reliable.Average(x => x.ReliabilityPct), MidpointRounding.AwayFromZero)
            : null;

        var impactedRevenue = reliable.Sum(x => x.Revenue);
        var impactEstimateRsd = Math.Round(impactedRevenue, 2);
        var reason = $"{reliable.Count} artikala imaju signal '{exemplar.RecommendationLabel}'. Primer: {exemplar.ProductName} ({exemplar.RecommendationReason})";
        var impact = string.Format(CultureInfo.InvariantCulture, impactTemplate, Math.Round(impactedRevenue, 0).ToString("0", CultureInfo.InvariantCulture));
        var actionUrl = BuildDashboardActionLink(actionPath, fromDate, toDate, storeId, supplierId);
        var sourceKey = BuildDashboardActionSourceKey(sourceType, actionTypeKey, fromDate, toDate, storeId, supplierId);

        actions.Add(new DashboardDecisionActionDto
        {
            ActionKey = sourceKey,
            SourceType = sourceType,
            Priority = priority,
            Title = title,
            Description = exemplar.RecommendationReason,
            Reason = reason,
            StatusReason = exemplar.RecommendationReason,
            RecommendationStatus = recommendationStatus,
            ExpectedImpact = impact,
            ImpactEstimateRsd = impactEstimateRsd,
            ConfidencePct = avgConfidence,
            ReliabilityPct = avgReliability,
            RecommendationAllowed = recommendationStatus is not "INSUFFICIENT_DATA" and not "FIX_DATA",
            DataQualityStatus = string.IsNullOrWhiteSpace(exemplar.DataQualityStatus) ? "insufficient_data" : exemplar.DataQualityStatus,
            ActionUrl = actionUrl,
            Metadata = new Dictionary<string, object?>
            {
                ["actionType"] = actionTypeKey,
                ["recommendationStatus"] = recommendationStatus,
                ["periodFrom"] = FormatDashboardActionDate(fromDate),
                ["periodTo"] = FormatDashboardActionDate(toDate),
                ["storeId"] = storeId?.ToString(CultureInfo.InvariantCulture) ?? "all",
                ["supplierId"] = supplierId?.ToString(CultureInfo.InvariantCulture) ?? "all",
                ["candidateCount"] = reliable.Count,
                ["exemplarProductId"] = exemplar.ProductId,
                ["exemplarSku"] = exemplar.Sku
            },
            Link = actionUrl,
            LinkLabel = "Otvori detalj"
        });
    }

    private static string FormatDashboardActionDate(DateTime? value)
        => value.HasValue
            ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "all";

    private static string BuildDashboardActionSourceKey(
        string sourceType,
        string actionType,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId)
    {
        var normalizedType = string.IsNullOrWhiteSpace(sourceType) ? "dashboard" : sourceType.Trim().ToLowerInvariant();
        var normalizedAction = string.IsNullOrWhiteSpace(actionType) ? "signal" : actionType.Trim().ToLowerInvariant();
        var fromPart = FormatDashboardActionDate(fromDate);
        var toPart = FormatDashboardActionDate(toDate);
        var storePart = storeId?.ToString(CultureInfo.InvariantCulture) ?? "all";
        var supplierPart = supplierId?.ToString(CultureInfo.InvariantCulture) ?? "all";
        return $"{normalizedType}:{normalizedAction}:{fromPart}:{toPart}:{storePart}:{supplierPart}";
    }

    private static string BuildDashboardActionTypeKey(string? title)
    {
        var normalized = (title ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(normalized))
            return "signal";

        var sanitized = string.Concat(normalized.Where(ch =>
            (ch >= 'a' && ch <= 'z')
            || (ch >= '0' && ch <= '9')
            || ch == '_'));

        return string.IsNullOrWhiteSpace(sanitized) ? "signal" : sanitized;
    }

    private static string ResolveDashboardSourceTypeFromActionUrl(string? actionUrl)
    {
        var normalized = (actionUrl ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("/analytics/inventory", StringComparison.Ordinal)) return "inventory";
        if (normalized.Contains("/analytics/products", StringComparison.Ordinal)) return "product";
        if (normalized.Contains("/analytics/supplier", StringComparison.Ordinal)) return "supplier";
        if (normalized.Contains("/analytics/data-quality", StringComparison.Ordinal)) return "data_quality";
        if (normalized.Contains("/analytics/pre-nivelacija-prioriteti", StringComparison.Ordinal)) return "nivelacija";
        return "dashboard";
    }

    private static int DecisionPriorityRank(string priority) => priority switch
    {
        "P1" => 1,
        "P2" => 2,
        _ => 3
    };

    private static string BuildDashboardActionLink(
        string basePath,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId)
    {
        var query = new List<string>();
        if (fromDate.HasValue) query.Add($"fromDate={Uri.EscapeDataString(fromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}");
        if (toDate.HasValue) query.Add($"toDate={Uri.EscapeDataString(toDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}");
        if (storeId.HasValue) query.Add($"storeId={storeId.Value}");
        if (supplierId.HasValue) query.Add($"supplierId={supplierId.Value}");

        return query.Count == 0 ? basePath : $"{basePath}?{string.Join("&", query)}";
    }

    private static string MapLegacyAdvancedActionLink(
        string title,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId)
    {
        var normalized = (title ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("replenishment")) return BuildDashboardActionLink("/analytics/inventory", fromDate, toDate, storeId, supplierId);
        if (normalized.Contains("data")) return BuildDashboardActionLink("/analytics/data-quality", fromDate, toDate, storeId, supplierId);
        if (normalized.Contains("portfolio")) return BuildDashboardActionLink("/analytics/products", fromDate, toDate, storeId, supplierId);
        if (normalized.Contains("refresh")) return BuildDashboardActionLink("/analytics/data-quality", fromDate, toDate, storeId, supplierId);
        return BuildDashboardActionLink("/analytics", fromDate, toDate, storeId, supplierId);
    }

    private static string ResolveDashboardLegacyActionStatusReason(DashboardActionDto action)
    {
        if (!string.IsNullOrWhiteSpace(action.StatusReason))
            return action.StatusReason;

        var hasTrustPayload =
            action.RecommendationAllowed.HasValue ||
            action.ConfidencePct.HasValue ||
            action.ReliabilityPct.HasValue ||
            !string.IsNullOrWhiteSpace(action.DataQualityStatus);

        if (hasTrustPayload && !string.IsNullOrWhiteSpace(action.Recommendation))
            return action.Recommendation;

        return "Legacy dashboard action bez trust payloada.";
    }

    private static string TranslateLegacyActionTitle(string title)
    {
        return (title ?? string.Empty).Trim() switch
        {
            "Replenishment" => "Dopuni kritične artikle",
            "Data quality fix" => "Proveri kvalitet podataka",
            "Portfolio balance" => "Balansiraj portfolio artikala",
            "Refresh pipeline" => "Osveži pipeline podataka",
            "Monitor" => "Nastavi praćenje ključnih signala",
            var value when !string.IsNullOrWhiteSpace(value) => value,
            _ => "Operativna akcija"
        };
    }

    private static async Task<T?> TrySectionAsync<T>(
        Func<Task<T>> factory,
        List<string> errors,
        string fallbackMessage) where T : class
    {
        try
        {
            return await factory();
        }
        catch (Exception ex)
        {
            errors.Add(GetErrorMessage(ex, fallbackMessage));
            return null;
        }
    }

    private static async Task<List<T>> TryListSectionAsync<T>(
        Func<Task<List<T>>> factory,
        List<string> errors,
        string fallbackMessage)
    {
        try
        {
            return await factory();
        }
        catch (Exception ex)
        {
            errors.Add(GetErrorMessage(ex, fallbackMessage));
            return [];
        }
    }

    private static async Task<SalesSummaryDto> BuildSalesSummarySnapshotAsync(
        ITrendplusDbContext trendDb,
        IMediator mediator,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        if (normalizedDataScope == "all" && !storeId.HasValue && !supplierId.HasValue)
        {
            var aggregated = await TryGetSalesSummaryFromAggregatesAsync(trendDb, fromDate, toDate, ct);
            if (aggregated is not null)
            {
                return aggregated;
            }
        }

        var totals = supplierId.HasValue
            ? await (
                from p in trendDb.ProdajaZaglavlja.AsNoTracking()
                join ps in trendDb.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                join a in trendDb.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      a.IDDobavljac == supplierId.Value &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group new { p, ps } by 1 into g
                select new
                {
                    TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                    TotalUnits = g.Sum(x => x.ps.Kolicina),
                    TotalTransactions = g.Select(x => x.p.Id).Distinct().Count()
                })
                .SingleOrDefaultAsync(ct)
            : await (
                from p in trendDb.ProdajaZaglavlja.AsNoTracking()
                join ps in trendDb.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group new { p, ps } by 1 into g
                select new
                {
                    TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                    TotalUnits = g.Sum(x => x.ps.Kolicina),
                    TotalTransactions = g.Select(x => x.p.Id).Distinct().Count()
                })
                .SingleOrDefaultAsync(ct);

        var totalRevenue = totals?.TotalRevenue ?? 0m;
        var totalUnits = totals?.TotalUnits ?? 0;
        var totalTransactions = totals?.TotalTransactions ?? 0;
        var avgBasket = totalTransactions > 0 ? totalRevenue / totalTransactions : 0m;
        var avgItem = totalUnits > 0 ? totalRevenue / totalUnits : 0m;

        return new SalesSummaryDto(totalRevenue, totalTransactions, totalUnits, avgBasket, avgItem);
    }

    private static async Task<InventoryStatusDto> BuildInventoryStatusSnapshotAsync(
        ITrendplusDbContext trendDb,
        IMediator mediator,
        int lowStockThreshold,
        CancellationToken ct)
    {
        try
        {
            return await mediator.Send(new GetInventoryStatusQuery(lowStockThreshold), ct);
        }
        catch (Exception ex) when (IsMissingRelation(ex))
        {
            var inventoryData = await trendDb.Artikli.AsNoTracking()
                .GroupBy(a => 1)
                .Select(g => new
                {
                    TotalSku = g.Count(),
                    TotalOnHand = g.Sum(x => (int?)x.Kolicina) ?? 0,
                    OutOfStock = g.Count(x => (x.Kolicina ?? 0) == 0),
                    LowStock = g.Count(x => (x.Kolicina ?? 0) > 0 && (x.Kolicina ?? 0) <= lowStockThreshold)
                })
                .SingleOrDefaultAsync(ct);

            return new InventoryStatusDto(
                inventoryData?.TotalSku ?? 0,
                inventoryData?.TotalOnHand ?? 0,
                inventoryData?.LowStock ?? 0,
                inventoryData?.OutOfStock ?? 0,
                UsedOperationalFallback: true
            );
        }
    }

    private static async Task<List<DailySaleDto>> BuildDailySalesSnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        if (normalizedDataScope == "all" && !storeId.HasValue && !supplierId.HasValue)
        {
            var aggregatedDaily = await TryGetDailySalesFromAggregatesAsync(db, fromDate, toDate, ct);
            if (aggregatedDaily is not null && aggregatedDaily.Count > 0)
            {
                return aggregatedDaily;
            }
        }

        var fallbackRaw = supplierId.HasValue
            ? await (
                from p in db.ProdajaZaglavlja.AsNoTracking()
                join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      a.IDDobavljac == supplierId.Value &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group ps by p.DatumProdaje.Date into g
                orderby g.Key
                select new
                {
                    Date = g.Key,
                    TotalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                    TransactionCount = g.Select(x => x.IdProdaja).Distinct().Count(),
                    TotalUnits = g.Sum(x => x.Kolicina)
                }).ToListAsync(ct)
            : await (
                from p in db.ProdajaZaglavlja.AsNoTracking()
                join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group ps by p.DatumProdaje.Date into g
                orderby g.Key
                select new
                {
                    Date = g.Key,
                    TotalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                    TransactionCount = g.Select(x => x.IdProdaja).Distinct().Count(),
                    TotalUnits = g.Sum(x => x.Kolicina)
                }).ToListAsync(ct);

        return fallbackRaw.Select(x => new DailySaleDto
        {
            Date = x.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TotalRevenue = x.TotalRevenue,
            TransactionCount = x.TransactionCount,
            TotalUnits = x.TotalUnits
        }).ToList();
    }

    private static async Task<List<CategoryDataDto>> BuildCategoryDataSnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        if (normalizedDataScope == "all" && !storeId.HasValue && !supplierId.HasValue)
        {
            var aggregatedCategory = await TryGetCategoryDataFromAggregatesAsync(db, fromDate, toDate, ct);
            if (aggregatedCategory is not null && aggregatedCategory.Count > 0)
            {
                return aggregatedCategory;
            }
        }

        var query = from ps in db.ProdajaStavke.AsNoTracking()
                    join p in db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals p.Id
                    join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                    where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                          (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                          (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                          (!supplierId.HasValue || a.IDDobavljac == supplierId.Value) &&
                          (!importedOnly || p.DataOrigin == "access") &&
                          (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                    group ps by new { a.Kategorija, a.Pol } into g
                    select new CategoryDataDto
                    {
                        Kategorija = g.Key.Kategorija ?? "Ostalo",
                        Pol = g.Key.Pol ?? "Neodređeno",
                        TotalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                        TotalUnits = g.Sum(x => x.Kolicina),
                        TransactionCount = g.Select(x => x.IdProdaja).Distinct().Count()
                    };

        return await query.OrderByDescending(x => x.TotalRevenue).ToListAsync(ct);
    }

    private static async Task<List<GenderDataDto>> BuildGenderDataSnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        if (normalizedDataScope == "all" && !storeId.HasValue && !supplierId.HasValue)
        {
            var aggregatedGender = await TryGetGenderDataFromAggregatesAsync(db, fromDate, toDate, ct);
            if (aggregatedGender is not null && aggregatedGender.Count > 0)
            {
                return aggregatedGender;
            }
        }

        var query = from ps in db.ProdajaStavke.AsNoTracking()
                    join p in db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals p.Id
                    join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                    where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                          (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                          (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                          (!supplierId.HasValue || a.IDDobavljac == supplierId.Value) &&
                          (!importedOnly || p.DataOrigin == "access") &&
                          (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                    group ps by a.Pol into g
                    select new GenderDataDto
                    {
                        Pol = g.Key ?? "Neodređeno",
                        TotalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                        TotalUnits = g.Sum(x => x.Kolicina)
                    };

        return await query.OrderByDescending(x => x.TotalRevenue).ToListAsync(ct);
    }

    private static async Task<List<SupplierDataDto>> BuildSupplierDataSnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        if (normalizedDataScope == "all" && !storeId.HasValue && !supplierId.HasValue)
        {
            var aggregatedSupplier = await TryGetSupplierDataFromAggregatesAsync(db, fromDate, toDate, ct);
            if (aggregatedSupplier is not null && aggregatedSupplier.Count > 0)
            {
                return aggregatedSupplier;
            }
        }

        var query = from ps in db.ProdajaStavke.AsNoTracking()
                    join p in db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals p.Id
                    join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                    join d in db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into dobavljacJoin
                    from d in dobavljacJoin.DefaultIfEmpty()
                    where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                          (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                          (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                          (!supplierId.HasValue || a.IDDobavljac == supplierId.Value) &&
                          (!importedOnly || p.DataOrigin == "access") &&
                          (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                    group ps by new { DobavljacId = d != null ? d.Id : (int?)null, DobavljacNaziv = d != null ? d.Naziv : "Nepoznato" } into g
                    select new SupplierDataDto
                    {
                        DobavljacId = g.Key.DobavljacId,
                        DobavljacNaziv = g.Key.DobavljacNaziv ?? "Nepoznato",
                        TotalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                        TotalUnits = g.Sum(x => x.Kolicina),
                        TransactionCount = g.Select(x => x.IdProdaja).Distinct().Count()
                    };

        return await query.OrderByDescending(x => x.TotalRevenue).ToListAsync(ct);
    }

    private static async Task<List<SupplierFilterOptionDto>> BuildSupplierFilterOptionsAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        if (normalizedDataScope == "all" && !storeId.HasValue)
        {
            var aggregatedSupplier = await TryGetSupplierDataFromAggregatesAsync(db, fromDate, toDate, ct);
            if (aggregatedSupplier is not null && aggregatedSupplier.Count > 0)
            {
                return aggregatedSupplier
                    .Where(x => x.DobavljacId.HasValue)
                    .Select(x => new SupplierFilterOptionDto
                    {
                        SupplierId = x.DobavljacId!.Value,
                        SupplierName = x.DobavljacNaziv
                    })
                    .DistinctBy(x => x.SupplierId)
                    .OrderBy(x => x.SupplierName)
                    .ToList();
            }
        }

        return await (
            from p in db.ProdajaZaglavlja.AsNoTracking()
            join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
            join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            join d in db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into dobavljacJoin
            from d in dobavljacJoin.DefaultIfEmpty()
            where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                  (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                  (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                  (!importedOnly || p.DataOrigin == "access") &&
                  (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "") &&
                  d != null
            group d by new { d.Id, d.Naziv } into g
            orderby g.Key.Naziv
            select new SupplierFilterOptionDto
            {
                SupplierId = g.Key.Id,
                SupplierName = g.Key.Naziv
            }).ToListAsync(ct);
    }

    private static async Task<QuickInsightsDto> BuildQuickInsightsSnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        var bestDayQuery = supplierId.HasValue
            ? from p in db.ProdajaZaglavlja.AsNoTracking()
              join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
              join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
              where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                    (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                    (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                    a.IDDobavljac == supplierId.Value &&
                    (!importedOnly || p.DataOrigin == "access") &&
                    (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
              select new { p.DatumProdaje, ps.Kolicina, ps.Cena }
            : from p in db.ProdajaZaglavlja.AsNoTracking()
              join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
              where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                    (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                    (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                    (!importedOnly || p.DataOrigin == "access") &&
                    (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
              select new { p.DatumProdaje, ps.Kolicina, ps.Cena };

        var bestDay = await bestDayQuery
            .GroupBy(x => x.DatumProdaje.DayOfWeek)
            .Select(g => new
            {
                DayOfWeek = (int)g.Key,
                TotalRevenue = g.Sum(x => x.Kolicina * x.Cena)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ThenBy(x => x.DayOfWeek)
            .FirstOrDefaultAsync(ct);

        var topProductQuery = supplierId.HasValue
            ? from p in db.ProdajaZaglavlja.AsNoTracking()
              join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
              join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
              where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                    (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                    (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                    a.IDDobavljac == supplierId.Value &&
                    (!importedOnly || p.DataOrigin == "access") &&
                    (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
              select new { ps.IdArtikal, ProductName = a.Naziv, ps.Kolicina, ps.Cena }
            : from p in db.ProdajaZaglavlja.AsNoTracking()
              join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
              join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
              where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                    (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                    (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                    (!importedOnly || p.DataOrigin == "access") &&
                    (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
              select new { ps.IdArtikal, ProductName = a.Naziv, ps.Kolicina, ps.Cena };

        var topProduct = await topProductQuery
            .GroupBy(x => new { x.IdArtikal, x.ProductName })
            .Select(g => new
            {
                ProductName = g.Key.ProductName,
                TotalRevenue = g.Sum(x => x.Kolicina * x.Cena)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ThenBy(x => x.ProductName)
            .FirstOrDefaultAsync(ct);

        var lowStockQuery = db.Artikli.AsNoTracking()
            .Where(a => a.Kolicina <= a.MinimalnaKolicina || a.Kolicina == 0);

        if (storeId.HasValue)
            lowStockQuery = lowStockQuery.Where(a => a.IDObjekat == storeId.Value);

        if (supplierId.HasValue)
            lowStockQuery = lowStockQuery.Where(a => a.IDDobavljac == supplierId.Value);

        var lowStockCount = await lowStockQuery.CountAsync(ct);

        return new QuickInsightsDto
        {
            BestDay = bestDay is null ? null : SerbianDayNames[bestDay.DayOfWeek],
            BestDayRevenue = bestDay?.TotalRevenue ?? 0,
            TopProduct = topProduct?.ProductName,
            LowStockAlert = lowStockCount
        };
    }

    private static async Task<TransactionStatsDto> BuildTransactionStatsSnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        var perTransactionQuery = supplierId.HasValue
            ? (
                from p in db.ProdajaZaglavlja.AsNoTracking()
                join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      a.IDDobavljac == supplierId.Value &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group ps by p.Id into g
                select new
                {
                    LineCount = g.Count(),
                    UnitCount = g.Sum(x => x.Kolicina),
                    TotalValue = g.Sum(x => x.Kolicina * x.Cena)
                })
            : (
                from p in db.ProdajaZaglavlja.AsNoTracking()
                join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group ps by p.Id into g
                select new
                {
                    LineCount = g.Count(),
                    UnitCount = g.Sum(x => x.Kolicina),
                    TotalValue = g.Sum(x => x.Kolicina * x.Cena)
                });

        var stats = await perTransactionQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AvgItemsPerTransaction = g.Average(x => (decimal)x.LineCount),
                AvgUnitsPerTransaction = g.Average(x => (decimal)x.UnitCount),
                AvgTransactionValue = g.Average(x => x.TotalValue),
                TotalTransactions = g.Count()
            })
            .SingleOrDefaultAsync(ct);

        if (stats is null)
            return new TransactionStatsDto();

        return new TransactionStatsDto
        {
            AvgItemsPerTransaction = Math.Round(stats.AvgItemsPerTransaction, 2),
            AvgUnitsPerTransaction = Math.Round(stats.AvgUnitsPerTransaction, 2),
            AvgTransactionValue = Math.Round(stats.AvgTransactionValue, 2),
            TotalTransactions = stats.TotalTransactions
        };
    }

    private static async Task<IReadOnlyList<StoreFilterOptionDto>> TryBuildStoreFiltersFallbackAsync(
        ITrendplusDbContext trendDb,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var storeIds = await trendDb.ProdajaZaglavlja
                .AsNoTracking()
                .Where(x => x.IDObjekat.HasValue)
                .Select(x => x.IDObjekat!.Value)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(ct);

            return storeIds
                .Select(id => new StoreFilterOptionDto
                {
                    StoreId = id,
                    StoreName = $"Objekat {id}",
                    City = null,
                    Region = null
                })
                .ToArray();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Array.Empty<StoreFilterOptionDto>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Store filters fallback query from Trend DB failed.");
            return Array.Empty<StoreFilterOptionDto>();
        }
    }

    private static void SetFilterFallbackHeaders(
        HttpContext httpContext,
        string warningCode,
        string warningMessage)
    {
        httpContext.Response.Headers["X-Analytics-Fallback"] = "true";
        httpContext.Response.Headers["X-Analytics-Fallback-Code"] = warningCode;
        httpContext.Response.Headers["X-Analytics-Fallback-Reason"] = warningMessage;
    }

    private static async Task<List<PaymentDataDto>> BuildPaymentDataSnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        return supplierId.HasValue
            ? await (
                from p in db.ProdajaZaglavlja.AsNoTracking()
                join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      a.IDDobavljac == supplierId.Value &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group new { p, ps } by p.NacinPlacanja into g
                orderby g.Sum(x => x.ps.Kolicina * x.ps.Cena) descending
                select new PaymentDataDto
                {
                    NacinPlacanja = g.Key ?? "Nepoznato",
                    TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                    TransactionCount = g.Select(x => x.p.Id).Distinct().Count()
                }).ToListAsync(ct)
            : await (
                from p in db.ProdajaZaglavlja.AsNoTracking()
                join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group new { p, ps } by p.NacinPlacanja into g
                orderby g.Sum(x => x.ps.Kolicina * x.ps.Cena) descending
                select new PaymentDataDto
                {
                    NacinPlacanja = g.Key ?? "Nepoznato",
                    TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                    TransactionCount = g.Select(x => x.p.Id).Distinct().Count()
                }).ToListAsync(ct);
    }

    private static async Task<List<WeekdayDataDto>> BuildWeekdayDataSnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        return supplierId.HasValue
            ? await (
                from p in db.ProdajaZaglavlja.AsNoTracking()
                join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      a.IDDobavljac == supplierId.Value &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group new { p, ps } by p.DatumProdaje.DayOfWeek into g
                orderby g.Key
                select new WeekdayDataDto
                {
                    DayOfWeek = (int)g.Key,
                    DayName = SerbianDayNames[(int)g.Key],
                    TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                    TransactionCount = g.Select(x => x.p.Id).Distinct().Count()
                }).ToListAsync(ct)
            : await (
                from p in db.ProdajaZaglavlja.AsNoTracking()
                join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group new { p, ps } by p.DatumProdaje.DayOfWeek into g
                orderby g.Key
                select new WeekdayDataDto
                {
                    DayOfWeek = (int)g.Key,
                    DayName = SerbianDayNames[(int)g.Key],
                    TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                    TransactionCount = g.Select(x => x.p.Id).Distinct().Count()
                }).ToListAsync(ct);
    }

    private static async Task<List<HourDataDto>> BuildHourDataSnapshotAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        CancellationToken ct,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        return supplierId.HasValue
            ? await (
                from p in db.ProdajaZaglavlja.AsNoTracking()
                join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      a.IDDobavljac == supplierId.Value &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group new { p, ps } by p.DatumProdaje.Hour into g
                orderby g.Key
                select new HourDataDto
                {
                    Hour = g.Key,
                    TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                    TransactionCount = g.Select(x => x.p.Id).Distinct().Count()
                }).ToListAsync(ct)
            : await (
                from p in db.ProdajaZaglavlja.AsNoTracking()
                join ps in db.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                      (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                      (!importedOnly || p.DataOrigin == "access") &&
                      (!existingOnly || p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")
                group new { p, ps } by p.DatumProdaje.Hour into g
                orderby g.Key
                select new HourDataDto
                {
                    Hour = g.Key,
                    TotalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                    TransactionCount = g.Select(x => x.p.Id).Distinct().Count()
                }).ToListAsync(ct);
    }

    private static async Task<DashboardValidationEndpointDto> BuildCompletenessValidationAsync(
        ITrendplusDbContext db,
        CancellationToken ct)
    {
        var (score, totalSku, missingSku, _, _) = await GetCompletenessAndFreshnessAsync(db, ct);
        var status = score >= 0.98m ? "good" : score >= 0.90m ? "warning" : "critical";
        var message = status switch
        {
            "good" => "Completeness je stabilan.",
            "warning" => "Nedostaju bitna polja za deo artikala.",
            _ => "Nizak completeness: validacija podataka je prioritet."
        };

        return new DashboardValidationEndpointDto
        {
            Status = status,
            Message = message,
            Score = score,
            TotalSku = totalSku,
            AffectedSku = missingSku
        };
    }

    private static async Task<DashboardValidationEndpointDto> BuildFreshnessValidationAsync(
        ITrendplusDbContext db,
        CancellationToken ct)
    {
        var (_, _, _, lastImport, freshnessHours) = await GetCompletenessAndFreshnessAsync(db, ct);
        var status = freshnessHours <= 6m ? "good" : freshnessHours <= 24m ? "warning" : "critical";
        var message = status switch
        {
            "good" => "Podaci su svezi.",
            "warning" => "Osvežavanje kasni, proverite import pipeline.",
            _ => "Podaci su zastareli: osvežite import i agregate."
        };

        return new DashboardValidationEndpointDto
        {
            Status = status,
            Message = message,
            LastImport = lastImport,
            FreshnessHours = freshnessHours
        };
    }

    private static async Task<DashboardValidationEndpointDto> BuildLostSalesValidationAsync(
        ITrendplusDbContext db,
        CancellationToken ct)
    {
        var snapshot = await GetLostSalesSnapshotAsync(db, ct);
        return BuildLostSalesValidationFromSnapshot(snapshot);
    }

    /// <summary>
    /// Maps lost-sales evidence source to validation status.
    /// Unavailable must never look like a clean green zero.
    /// </summary>
    internal static DashboardValidationEndpointDto BuildLostSalesValidationFromSnapshot(LostSalesSnapshot snapshot)
    {
        var estimate = snapshot.LostSalesEstimate;
        string status;
        string message;

        switch (snapshot.SourceStatus)
        {
            case LostSalesSourceStatus.Unavailable:
                status = "insufficient_data";
                message = "Procena izgubljene prodaje nije dostupna; OOS signal se ne sme tretirati kao nula.";
                estimate = null;
                break;
            case LostSalesSourceStatus.TrueZero:
                status = "good";
                message = "Nema znacajnog gubitka prodaje zbog OOS.";
                estimate = 0m;
                break;
            case LostSalesSourceStatus.Fallback when (estimate ?? 0m) <= 0m:
                status = "warning";
                message = "Fallback procena ne pokazuje gubitak, ali pouzdanost je smanjena jer view nije korišćen.";
                estimate = 0m;
                break;
            default:
            {
                var value = estimate ?? 0m;
                status = value < 50_000m ? "warning" : "critical";
                message = status == "warning"
                    ? (snapshot.SourceStatus == LostSalesSourceStatus.Fallback
                        ? "Postoji procenjen gubitak prodaje zbog OOS (fallback izvor)."
                        : "Postoji procenjen gubitak prodaje zbog OOS.")
                    : (snapshot.SourceStatus == LostSalesSourceStatus.Fallback
                        ? "Kritican OOS gubitak (fallback izvor): replenishment je prioritet."
                        : "Kritican OOS gubitak: replenishment je prioritet.");
                estimate = value;
                break;
            }
        }

        return new DashboardValidationEndpointDto
        {
            Status = status,
            Message = message,
            AffectedSku = snapshot.SourceStatus == LostSalesSourceStatus.Unavailable ? null : snapshot.OosSkuCount,
            LostSalesEstimate = estimate,
            SourceStatus = snapshot.SourceStatus
        };
    }

    private sealed class ProductDecisionArticleSnapshot
    {
        public int ProductId { get; init; }
        public string Sku { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public int? SupplierId { get; init; }
        public string? SupplierName { get; init; }
        public string? Category { get; init; }
        public string? ShoeTypeName { get; init; }
        public string? Color { get; init; }
        public string? Size { get; init; }
        public int CurrentStock { get; init; }
        public int MinStock { get; init; }
        public decimal? UnitCost { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    private sealed class ProductDecisionSalesAggregate
    {
        public int ProductId { get; init; }
        public decimal Revenue { get; init; }
        public int UnitsSold { get; init; }
        public decimal MarginContribution { get; init; }
        public decimal CostCoveredRevenue { get; init; }
    }

    internal static async Task<ProductDecisionTimelineFilterResponseDto> BuildProductDecisionTimelineFilterAsync(
        IAnalyticsDbContext analyticsDb,
        string? sourceType,
        string? sourceKey,
        int? productId,
        string? recommendationType,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow.Date;
        var periodToUtc = (toDate?.Date ?? nowUtc);
        var periodFromUtc = fromDate?.Date ?? periodToUtc.AddDays(-29);
        if (periodFromUtc > periodToUtc)
        {
            (periodFromUtc, periodToUtc) = (periodToUtc, periodFromUtc);
        }

        var normalizedSourceType = string.IsNullOrWhiteSpace(sourceType)
            ? (productId.HasValue ? "product" : null)
            : sourceType.Trim();

        IQueryable<AnalyticsActionItem> query = analyticsDb.AnalyticsActionItems
            .AsNoTracking()
            .Include(item => item.Notes);

        if (!string.IsNullOrWhiteSpace(normalizedSourceType))
        {
            query = query.Where(item => item.SourceType == normalizedSourceType);
        }

        if (!string.IsNullOrWhiteSpace(sourceKey))
        {
            var normalizedSourceKey = sourceKey.Trim();
            query = query.Where(item => item.SourceKey == normalizedSourceKey);
        }
        else if (productId.HasValue)
        {
            var exactKey = $"product:{productId.Value}";
            var prefix = exactKey + ":";
            query = query.Where(item =>
                item.SourceId == productId.Value
                || item.SourceKey == exactKey
                || item.SourceKey.StartsWith(prefix));
        }

        // Family and exact period precision are applied by the read-only filter helper.
        // Keep SQL candidate window intentionally wider than the requested period.
        var candidateFrom = periodFromUtc.AddDays(-14);
        var candidateToExclusive = periodToUtc.AddDays(15);
        query = query.Where(item =>
            item.CreatedAtUtc >= candidateFrom
            && item.CreatedAtUtc < candidateToExclusive);

        var items = await query
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Take(200)
            .ToListAsync(ct);

        var filtered = AnalyticsActionTimelineFilterProjection.Filter(
            items,
            new DecisionTimelineFilterQuery(
                SourceType: normalizedSourceType,
                SourceKey: string.IsNullOrWhiteSpace(sourceKey) ? null : sourceKey.Trim(),
                ProductId: productId,
                RecommendationType: string.IsNullOrWhiteSpace(recommendationType) ? null : recommendationType.Trim(),
                PeriodFromUtc: periodFromUtc,
                PeriodToUtc: periodToUtc));

        var dataQuality = filtered.Timelines.Count == 0
            ? "insufficient_data"
            : filtered.WarningCodes.Contains(AnalyticsActionTimelineFilterProjection.EmptyReasonNoMeasurement)
                ? "warning"
                : "good";

        return new ProductDecisionTimelineFilterResponseDto
        {
            Scope = filtered.Scope,
            EmptyReason = filtered.EmptyReason,
            Timelines = filtered.Timelines.ToList(),
            MatchedActionCount = filtered.MatchedActionCount,
            MatchedEventCount = filtered.MatchedEventCount,
            WarningCodes = filtered.WarningCodes.ToList(),
            Meta = filtered.EmptyReason is null
                ? BuildSuccessMeta(dataQualityStatus: dataQuality, lastRefreshAtUtc: DateTime.UtcNow)
                : BuildSuccessMeta(
                    dataQualityStatus: "insufficient_data",
                    message: filtered.EmptyReason switch
                    {
                        AnalyticsActionTimelineFilterProjection.EmptyReasonOutsidePeriod
                            => "Nema timeline događaja u izabranom periodu.",
                        _ => "Nema timeline događaja za izabrani entitet/porodicu."
                    },
                    lastRefreshAtUtc: DateTime.UtcNow)
        };
    }

    private static IResult FormatDecisionTimelineExport(
        DecisionTimelineExportDto export,
        string? format,
        string? correlationId)
    {
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) && export.Success)
        {
            var csv = DecisionTimelineExportProjection.ToCsv(export);
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(csv),
                "text/csv; charset=utf-8",
                "decision-timeline-export.csv");
        }

        var meta = export.Success
            ? BuildSuccessMeta(
                dataQualityStatus: export.Header.DataQualityStatus ?? "insufficient_data",
                lastRefreshAtUtc: export.Header.GeneratedAtUtc)
            : BuildErrorMeta(
                export.ErrorCode ?? "ANALYTICS_UNEXPECTED_ERROR",
                export.ErrorMessage ?? "Decision Timeline export trenutno nije dostupan.",
                correlationId);
        meta.CorrelationId = correlationId;
        meta.GeneratedAtUtc = export.Header.GeneratedAtUtc;

        return Results.Ok(new ProductDecisionTimelineExportResponseDto
        {
            Success = export.Success,
            Header = export.Header,
            Funnel = export.Funnel,
            Rows = export.Rows.ToList(),
            ErrorCode = export.ErrorCode,
            ErrorMessage = export.ErrorMessage,
            Meta = meta
        });
    }

    internal static async Task<ProductDecisionCenterResponseDto> BuildProductDecisionCenterAsync(
        ITrendplusDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int? storeId,
        int? supplierId,
        int top,
        string dataScope,
        CancellationToken ct)
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var importedOnly = normalizedDataScope == "imported";
        var existingOnly = normalizedDataScope == "existing";

        var nowUtc = DateTime.UtcNow;
        var periodToExclusiveUtc = (toDate?.Date ?? nowUtc.Date).AddDays(1);
        var periodFromUtc = fromDate?.Date ?? periodToExclusiveUtc.AddDays(-30);
        if (periodFromUtc >= periodToExclusiveUtc)
        {
            periodFromUtc = periodToExclusiveUtc.AddDays(-1);
        }

        var periodDays = Math.Max(1, (int)Math.Ceiling((periodToExclusiveUtc - periodFromUtc).TotalDays));
        var previousFromUtc = periodFromUtc.AddDays(-periodDays);
        var previousToExclusiveUtc = periodFromUtc;

        var articles = await (
            from a in db.Artikli.AsNoTracking()
            join d in db.Dobavljaci.AsNoTracking() on a.IDDobavljac equals d.Id into supplierJoin
            from d in supplierJoin.DefaultIfEmpty()
            join t in db.TipoviObuce.AsNoTracking() on a.IDTipObuce equals t.Id into shoeTypeJoin
            from t in shoeTypeJoin.DefaultIfEmpty()
            where (!storeId.HasValue || a.IDObjekat == storeId.Value)
                  && (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                  && (!importedOnly || a.DataOrigin == "access")
                  && (!existingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
            select new ProductDecisionArticleSnapshot
            {
                ProductId = a.Id,
                Sku = a.PLU ?? string.Empty,
                ProductName = a.Naziv ?? string.Empty,
                SupplierId = a.IDDobavljac,
                SupplierName = d != null ? d.Naziv : null,
                Category = a.Kategorija,
                ShoeTypeName = t != null ? t.Naziv : null,
                Color = a.Boja,
                Size = a.Velicina,
                CurrentStock = a.Kolicina ?? 0,
                MinStock = a.MinimalnaKolicina ?? 0,
                UnitCost = a.NabavnaCena,
                UpdatedAtUtc = a.UpdatedAt
            })
            .ToListAsync(ct);

        if (articles.Count == 0)
        {
            return new ProductDecisionCenterResponseDto
            {
                GeneratedAtUtc = nowUtc,
                PeriodFromUtc = periodFromUtc,
                PeriodToUtc = periodToExclusiveUtc.AddDays(-1),
                RequestedDataScope = normalizedDataScope,
                ScopeAuthority = "both",
                ScopeBreakdown = "article_origin=Artikli.DataOrigin;sale_origin=ProdajaZaglavlje.DataOrigin",
                Summary = BuildProductDecisionCenterSummary([], analyzedLostSalesEstimate: 0m, analyzedSlowStockCapital: 0m),
                TotalRows = 0,
                AnalyzedRows = 0,
                IgnoredRowsCount = 0,
                IgnoredRowsMeaning = ProductDecisionDenominatorScope.HiddenByTopLimit,
                Rows = [],
                Meta = new AnalyticsResponseMetaDto
                {
                    Success = true,
                    DataQualityStatus = "insufficient_data",
                    EmptyReason = "no_rows_for_period",
                    Message = "Nema podataka za izabrani period i filtere.",
                    GeneratedAtUtc = nowUtc
                }
            };
        }

        var articleIds = articles.Select(x => x.ProductId).ToHashSet();

        var currentSales = await (
            from pz in db.ProdajaZaglavlja.AsNoTracking()
            join ps in db.ProdajaStavke.AsNoTracking() on pz.Id equals ps.IdProdaja
            join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            where articleIds.Contains(ps.IdArtikal)
                  && pz.DatumProdaje >= periodFromUtc
                  && pz.DatumProdaje < periodToExclusiveUtc
                  && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
                  && (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                  && (!importedOnly || pz.DataOrigin == "access")
                  && (!existingOnly || pz.DataOrigin == "existing" || pz.DataOrigin == null || pz.DataOrigin == "")
            group new { ps, a } by ps.IdArtikal
            into g
            select new ProductDecisionSalesAggregate
            {
                ProductId = g.Key,
                Revenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                UnitsSold = g.Sum(x => x.ps.Kolicina),
                MarginContribution = g.Sum(x =>
                    (x.ps.NabavnaCena ?? x.a.NabavnaCena).HasValue
                        ? (x.ps.Cena - (x.ps.NabavnaCena ?? x.a.NabavnaCena)!.Value) * x.ps.Kolicina
                        : 0m),
                CostCoveredRevenue = g.Sum(x =>
                    (x.ps.NabavnaCena ?? x.a.NabavnaCena).HasValue
                        ? x.ps.Kolicina * x.ps.Cena
                        : 0m)
            })
            .ToListAsync(ct);

        var previousRevenue = await (
            from pz in db.ProdajaZaglavlja.AsNoTracking()
            join ps in db.ProdajaStavke.AsNoTracking() on pz.Id equals ps.IdProdaja
            join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            where articleIds.Contains(ps.IdArtikal)
                  && pz.DatumProdaje >= previousFromUtc
                  && pz.DatumProdaje < previousToExclusiveUtc
                  && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
                  && (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                  && (!importedOnly || pz.DataOrigin == "access")
                  && (!existingOnly || pz.DataOrigin == "existing" || pz.DataOrigin == null || pz.DataOrigin == "")
            group ps by ps.IdArtikal
            into g
            select new
            {
                ProductId = g.Key,
                Revenue = g.Sum(x => x.Kolicina * x.Cena)
            })
            .ToListAsync(ct);

        var lastSales = await (
            from pz in db.ProdajaZaglavlja.AsNoTracking()
            join ps in db.ProdajaStavke.AsNoTracking() on pz.Id equals ps.IdProdaja
            join a in db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            where articleIds.Contains(ps.IdArtikal)
                  && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
                  && (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                  && (!importedOnly || pz.DataOrigin == "access")
                  && (!existingOnly || pz.DataOrigin == "existing" || pz.DataOrigin == null || pz.DataOrigin == "")
            group pz by ps.IdArtikal
            into g
            select new
            {
                ProductId = g.Key,
                LastSaleAtUtc = g.Max(x => x.DatumProdaje)
            })
            .ToListAsync(ct);

        var currentByProduct = currentSales.ToDictionary(x => x.ProductId);
        var previousByProduct = previousRevenue.ToDictionary(x => x.ProductId, x => x.Revenue);
        var lastSaleByProduct = lastSales.ToDictionary(x => x.ProductId, x => x.LastSaleAtUtc);
        var movementWindowStatsByArticle = await LoadInventorySignalWindowStatsFromJournalAsync(
            db,
            articles.Select(x => x.ProductId).ToArray(),
            storeId,
            periodFromUtc,
            periodToExclusiveUtc,
            normalizedDataScope,
            ct);

        var rows = new List<ProductDecisionCenterRowDto>(articles.Count);
        var totalLostSalesEstimate = 0m;
        var totalSlowStockCapital = 0m;

        foreach (var article in articles)
        {
            currentByProduct.TryGetValue(article.ProductId, out var sales);
            previousByProduct.TryGetValue(article.ProductId, out var previousRevenueValue);
            lastSaleByProduct.TryGetValue(article.ProductId, out var lastSaleAtUtc);

            var revenue = sales?.Revenue ?? 0m;
            var unitsSold = sales?.UnitsSold ?? 0;
            var velocityUnitsPerDay = periodDays > 0 ? (decimal)unitsSold / periodDays : 0m;
            var marginContribution = sales?.MarginContribution ?? 0m;
            var marginPct = revenue > 0m ? (marginContribution / revenue) * 100m : (decimal?)null;
            var marginCoveragePct = revenue > 0m
                ? ((sales?.CostCoveredRevenue ?? 0m) / revenue) * 100m
                : 0m;
            var marginQualityLabel = marginCoveragePct >= 85m
                ? "Visok kvalitet"
                : marginCoveragePct >= 60m
                    ? "Srednji kvalitet"
                    : "Nizak kvalitet";

            var stockGap = Math.Max(0, article.MinStock - article.CurrentStock);
            var daysSinceLastSale = lastSaleAtUtc > default(DateTime)
                ? (int?)Math.Max(0, (int)Math.Floor((nowUtc - DateTime.SpecifyKind(lastSaleAtUtc, DateTimeKind.Utc)).TotalDays))
                : null;

            decimal? trendPct = null;
            if (previousRevenueValue > 0m)
            {
                trendPct = ((revenue - previousRevenueValue) / previousRevenueValue) * 100m;
            }
            else if (revenue > 0m)
            {
                trendPct = 100m;
            }

            var avgUnitPrice = unitsSold > 0 ? revenue / unitsSold : 0m;
            var lostSalesEstimate = stockGap > 0 && velocityUnitsPerDay > 0m && avgUnitPrice > 0m
                ? Math.Round(stockGap * avgUnitPrice, 2)
                : 0m;

            var missingSupplier = !article.SupplierId.HasValue || string.IsNullOrWhiteSpace(article.SupplierName);
            var missingCost = !article.UnitCost.HasValue;
            var missingCategory = string.IsNullOrWhiteSpace(article.Category) && string.IsNullOrWhiteSpace(article.ShoeTypeName);
            var missingVariantData = string.IsNullOrWhiteSpace(article.Color) || string.IsNullOrWhiteSpace(article.Size);

            var dataQualityStatus = missingSupplier || missingCost || missingCategory
                ? "critical"
                : (marginCoveragePct < 60m || missingVariantData ? "warning" : "good");

            var reasoning = ProductDecisionReasoningHelper.Evaluate(new ProductDecisionReasoningHelper.Input(
                MissingSupplier: missingSupplier,
                MissingCost: missingCost,
                MissingCategory: missingCategory,
                MissingVariantData: missingVariantData,
                Revenue: revenue,
                UnitsSold: unitsSold,
                VelocityUnitsPerDay: velocityUnitsPerDay,
                MarginPct: marginPct,
                MarginCoveragePct: marginCoveragePct,
                TrendPct: trendPct,
                StockGap: stockGap,
                CurrentStock: article.CurrentStock,
                MinStock: article.MinStock,
                DaysSinceLastSale: daysSinceLastSale));

            var recommendationStatus = reasoning.RecommendationStatus;

            var confidencePct = ResolveRecommendationConfidence(
                recommendationStatus,
                revenue,
                unitsSold,
                marginCoveragePct,
                trendPct,
                daysSinceLastSale);

            var reliabilityPct = ResolveRecommendationReliability(
                recommendationStatus,
                revenue,
                unitsSold,
                marginCoveragePct,
                trendPct,
                daysSinceLastSale,
                dataQualityStatus);

            var reasonCodes = reasoning.ReasonCodes;

            var recommendationReason = BuildRecommendationReason(
                recommendationStatus,
                revenue,
                unitsSold,
                velocityUnitsPerDay,
                marginPct,
                trendPct,
                stockGap,
                article.CurrentStock,
                article.MinStock,
                daysSinceLastSale,
                dataQualityStatus);

            var recommendationLabel = RecommendationLabel(recommendationStatus);
            var recommendedAction = RecommendedAction(recommendationStatus);
            var slowStockCapital = velocityUnitsPerDay < 0.15m && article.CurrentStock > article.MinStock * 2
                ? Math.Round((article.UnitCost ?? 0m) * article.CurrentStock, 2)
                : 0m;

            var movementWindowStats = movementWindowStatsByArticle.TryGetValue(article.ProductId, out var stats)
                ? stats
                : new InventorySignalWindowStats(0, 0);
            var openingStockUnits = Math.Max(article.CurrentStock - movementWindowStats.NetMovementUnits, 0);
            var hasReliableSellThroughInputs = openingStockUnits > 0 || movementWindowStats.InboundUnits > 0;
            var hasSufficientSignalData = unitsSold > 0 || article.CurrentStock > 0 || hasReliableSellThroughInputs;
            var signalDataQuality = unitsSold > 0 && hasReliableSellThroughInputs
                ? "good"
                : hasSufficientSignalData
                    ? "warning"
                    : "insufficient_data";

            var signal = InventorySignalCalculator.Calculate(
                currentOnHandUnits: article.CurrentStock,
                avgDailySalesUnits: velocityUnitsPerDay,
                soldUnits: unitsSold,
                openingStockUnits: openingStockUnits,
                inboundUnits: movementWindowStats.InboundUnits,
                dataQualityStatus: signalDataQuality,
                hasSufficientData: hasSufficientSignalData);

            var combinedReasonCodes = reasonCodes
                .Concat(signal.ReasonCodes)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            totalLostSalesEstimate += lostSalesEstimate;
            totalSlowStockCapital += slowStockCapital;

            var row = new ProductDecisionCenterRowDto
            {
                ProductId = article.ProductId,
                Sku = article.Sku,
                ProductName = article.ProductName,
                SupplierId = article.SupplierId,
                SupplierName = article.SupplierName,
                Category = article.Category,
                TipObuce = article.ShoeTypeName,
                Color = article.Color,
                Size = article.Size,
                Revenue = Math.Round(revenue, 2),
                UnitsSold = unitsSold,
                VelocityUnitsPerDay = Math.Round(velocityUnitsPerDay, 3),
                MarginContribution = Math.Round(marginContribution, 2),
                MarginPct = marginPct.HasValue ? Math.Round(marginPct.Value, 2) : null,
                MarginQualityLabel = marginQualityLabel,
                MarginCoveragePct = Math.Round(marginCoveragePct, 2),
                CurrentStock = article.CurrentStock,
                MinStock = article.MinStock,
                StockGap = stockGap,
                DaysSinceLastSale = daysSinceLastSale,
                TrendPct = trendPct.HasValue ? Math.Round(trendPct.Value, 2) : null,
                LostSalesEstimate = lostSalesEstimate,
                SlowStockCapital = slowStockCapital,
                StockCoverDays = signal.StockCoverDays,
                StockCoverStatus = signal.StockCoverStatus,
                StockCoverStatusLabel = signal.StockCoverStatusLabel,
                SellThroughRatio = signal.SellThroughRatio,
                SellThroughStatus = signal.SellThroughStatus,
                SellThroughStatusLabel = signal.SellThroughStatusLabel,
                SignalConfidencePct = signal.SignalConfidencePct,
                RecommendationAllowed = signal.RecommendationAllowed,
                DataQualityStatus = dataQualityStatus,
                ConfidencePct = confidencePct,
                ReliabilityPct = reliabilityPct,
                RecommendationStatus = recommendationStatus,
                RecommendationLabel = recommendationLabel,
                RecommendationReason = recommendationReason,
                ReasonCodes = combinedReasonCodes,
                RecommendedAction = recommendedAction
            };

            var confidenceProfile = BuildProductDecisionConfidenceProfile(row, periodFromUtc, periodToExclusiveUtc.AddDays(-1));
            row.RecommendationId = confidenceProfile.RecommendationId;
            row.SourceType = confidenceProfile.SourceType;
            row.SourceKey = confidenceProfile.SourceKey;
            row.RecommendationType = confidenceProfile.RecommendationType;
            row.ConfidenceLevel = confidenceProfile.ConfidenceLevel;
            row.ConfidenceScore = confidenceProfile.ConfidenceScore;
            row.PrimaryDrivers = confidenceProfile.PrimaryDrivers.ToList();
            row.WarningCodes = confidenceProfile.WarningCodes.ToList();
            row.ConfidenceBreakdown = confidenceProfile.ConfidenceBreakdown.ToList();
            row.AlternativeRecommendations = confidenceProfile.AlternativeRecommendations.ToList();
            row.ExpectedImpactRsd = confidenceProfile.ExpectedImpactRsd;
            row.ImpactWindowDays = confidenceProfile.ImpactWindowDays;
            row.RiskIfIgnored = confidenceProfile.RiskIfIgnored;
            row.ExplainabilityText = confidenceProfile.ExplainabilityText;
            row.InputFreshnessStatus = confidenceProfile.InputFreshnessStatus;
            row.EvidenceChain = confidenceProfile.EvidenceChain.ToList();
            row.WhyPanel = confidenceProfile.WhyPanel;
            ApplyIssuedRecommendationLifecycle(row);
            ApplyDecisionEvidenceSnapshotPreview(row, periodFromUtc, periodToExclusiveUtc.AddDays(-1));

            rows.Add(row);
        }

        var sortedRows = rows
            .OrderByDescending(x => RecommendationPriority(x.RecommendationStatus))
            .ThenByDescending(x => x.ConfidencePct)
            .ThenByDescending(x => x.Revenue)
            .Take(top)
            .ToList();

        var rowWindow = BuildProductDecisionCenterRowWindow(rows.Count, sortedRows.Count);
        return new ProductDecisionCenterResponseDto
        {
            GeneratedAtUtc = nowUtc,
            PeriodFromUtc = periodFromUtc,
            PeriodToUtc = periodToExclusiveUtc.AddDays(-1),
            RequestedDataScope = normalizedDataScope,
            ScopeAuthority = "both",
            ScopeBreakdown = "article_origin=Artikli.DataOrigin;sale_origin=ProdajaZaglavlje.DataOrigin",
            TotalRows = rowWindow.TotalRows,
            AnalyzedRows = rowWindow.AnalyzedRows,
            IgnoredRowsCount = rowWindow.IgnoredRowsCount,
            IgnoredRowsMeaning = rowWindow.IgnoredRowsMeaning,
            Summary = BuildProductDecisionCenterSummary(
                sortedRows,
                analyzedLostSalesEstimate: totalLostSalesEstimate,
                analyzedSlowStockCapital: totalSlowStockCapital),
            Rows = sortedRows,
            Meta = sortedRows.Count == 0
                ? BuildSuccessMeta(
                    dataQualityStatus: "insufficient_data",
                    message: "Nema dovoljno podataka za preporuke u ovom periodu.",
                    lastRefreshAtUtc: nowUtc,
                    emptyReason: "no_rows_for_period")
                : BuildSuccessMeta(
                    dataQualityStatus: ResolveDataQualityFromRows(sortedRows),
                    lastRefreshAtUtc: nowUtc)
        };
    }

    /// <summary>
    /// PDC summary contract:
    /// count KPIs use returned/top rows; money totals use all analyzed rows.
    /// Numeric behavior is unchanged; scopes make the denominator explicit.
    /// </summary>
    internal static ProductDecisionCenterSummaryDto BuildProductDecisionCenterSummary(
        IReadOnlyList<ProductDecisionCenterRowDto> returnedRows,
        decimal analyzedLostSalesEstimate,
        decimal analyzedSlowStockCapital) =>
        new()
        {
            ReplenishCount = returnedRows.Count(x => x.RecommendationStatus == "REPLENISH"),
            MarkdownCount = returnedRows.Count(x => x.RecommendationStatus == "MARKDOWN"),
            HighPotentialCount = returnedRows.Count(x => x.RecommendationStatus == "BOOST"),
            BadDataCount = returnedRows.Count(x => x.RecommendationStatus == "FIX_DATA"),
            LostSalesEstimate = Math.Round(analyzedLostSalesEstimate, 2),
            SlowStockCapital = Math.Round(analyzedSlowStockCapital, 2),
            CountDenominatorScope = ProductDecisionDenominatorScope.ReturnedRows,
            MoneyDenominatorScope = ProductDecisionDenominatorScope.AnalyzedRows
        };

    /// <summary>
    /// <c>IgnoredRowsCount</c> means rows hidden by the top limit, not invalid/bad-data rows.
    /// </summary>
    internal static ProductDecisionCenterRowWindow BuildProductDecisionCenterRowWindow(
        int analyzedRowCount,
        int returnedRowCount) =>
        new(
            TotalRows: returnedRowCount,
            AnalyzedRows: analyzedRowCount,
            IgnoredRowsCount: Math.Max(0, analyzedRowCount - returnedRowCount),
            IgnoredRowsMeaning: ProductDecisionDenominatorScope.HiddenByTopLimit);

    private static AnalyticsResponseMetaDto ResolveCachedDailySalesMeta(bool usedOperationalFallback, int itemCount)
    {
        if (usedOperationalFallback)
        {
            return AnalyticsResponseMetaFactory.Warning(
                "daily_sales_operational_fallback",
                "Dnevna prodaja je učitana iz operativnih tabela jer analytics relacija nije dostupna.",
                "warning");
        }

        if (itemCount == 0)
        {
            return AnalyticsResponseMetaFactory.Empty("no_data_in_period", "Nema prodaje za izabrani period.");
        }

        return AnalyticsResponseMetaFactory.Success();
    }

    private static AnalyticsResponseMetaDto BuildSuccessMeta(
        string? dataQualityStatus = null,
        bool isPartial = false,
        string? warningCode = null,
        string? message = null,
        DateTime? lastRefreshAtUtc = null,
        string? correlationId = null,
        string? warningMessage = null,
        string? emptyReason = null)
    {
        var resolvedMessage = message ?? warningMessage;
        return new AnalyticsResponseMetaDto
        {
            Success = true,
            WarningCode = warningCode,
            WarningMessage = warningMessage,
            Message = resolvedMessage,
            CorrelationId = correlationId,
            GeneratedAtUtc = DateTime.UtcNow,
            LastRefreshAtUtc = lastRefreshAtUtc,
            DataQualityStatus = dataQualityStatus,
            IsPartial = isPartial,
            EmptyReason = emptyReason
        };
    }

    private static AnalyticsResponseMetaDto BuildErrorMeta(string errorCode, string message, string? correlationId = null)
    {
        return new AnalyticsResponseMetaDto
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
            CorrelationId = correlationId,
            Message = message,
            GeneratedAtUtc = DateTime.UtcNow,
            DataQualityStatus = "insufficient_data",
            IsPartial = false
        };
    }

    private static string ResolveDataQualityFromRows(IReadOnlyList<ProductDecisionCenterRowDto> rows)
    {
        if (rows.Count == 0)
        {
            return "insufficient_data";
        }

        if (rows.Any(x => string.Equals(x.DataQualityStatus, "critical", StringComparison.OrdinalIgnoreCase)))
        {
            return "critical";
        }

        if (rows.Any(x => string.Equals(x.DataQualityStatus, "warning", StringComparison.OrdinalIgnoreCase)))
        {
            return "warning";
        }

        return "good";
    }

    private static string ResolveDashboardDataQualityStatus(AnalyticsDashboardBootstrapDto response)
    {
        var status = response.ValidationFreshness?.Status?.Trim().ToLowerInvariant();
        if (status is "critical")
        {
            return "critical";
        }

        if (status is "warning")
        {
            return "warning";
        }

        if (status is "good" or "info")
        {
            return "good";
        }

        return response.Errors.Count > 0 ? "warning" : "insufficient_data";
    }

    private static int ResolveRecommendationConfidence(
        string recommendationStatus,
        decimal revenue,
        int unitsSold,
        decimal marginCoveragePct,
        decimal? trendPct,
        int? daysSinceLastSale)
    {
        var confidence = 35m;

        if (unitsSold >= 20) confidence += 20m;
        else if (unitsSold >= 8) confidence += 10m;

        if (revenue >= 100_000m) confidence += 10m;
        if (marginCoveragePct >= 80m) confidence += 15m;
        else if (marginCoveragePct < 50m) confidence -= 20m;

        if (trendPct.HasValue) confidence += 10m;
        if (daysSinceLastSale.HasValue && daysSinceLastSale.Value > 90) confidence -= 15m;

        if (recommendationStatus is "FIX_DATA" or "INSUFFICIENT_DATA")
        {
            confidence = Math.Min(confidence, 35m);
        }

        confidence = Math.Clamp(confidence, 5m, 99m);
        return (int)Math.Round(confidence, MidpointRounding.AwayFromZero);
    }

    private static int ResolveRecommendationReliability(
        string recommendationStatus,
        decimal revenue,
        int unitsSold,
        decimal marginCoveragePct,
        decimal? trendPct,
        int? daysSinceLastSale,
        string dataQualityStatus)
    {
        var reliability = 30m;

        if (unitsSold >= 20) reliability += 25m;
        else if (unitsSold >= 8) reliability += 12m;

        if (revenue >= 100_000m) reliability += 10m;

        if (marginCoveragePct >= 85m) reliability += 20m;
        else if (marginCoveragePct >= 60m) reliability += 10m;
        else reliability -= 20m;

        reliability += trendPct.HasValue ? 10m : -5m;

        if (!daysSinceLastSale.HasValue) reliability -= 10m;
        else if (daysSinceLastSale.Value > 90) reliability -= 10m;

        if (dataQualityStatus == "critical") reliability = Math.Min(reliability, 35m);
        else if (dataQualityStatus == "warning") reliability = Math.Min(reliability, 70m);

        if (recommendationStatus is "FIX_DATA" or "INSUFFICIENT_DATA")
            reliability = Math.Min(reliability, 45m);

        reliability = Math.Clamp(reliability, 5m, 99m);
        return (int)Math.Round(reliability, MidpointRounding.AwayFromZero);
    }

    internal static ProductDecisionConfidenceProfile BuildProductDecisionConfidenceProfile(
        ProductDecisionCenterRowDto row,
        DateTime periodFromUtc,
        DateTime periodToUtc)
    {
        var recommendationStatus = NormalizeRecommendationStatus(row.RecommendationStatus);
        var sourceType = "product";
        var sourceKey = $"product:{row.ProductId}";
        var recommendationId = $"{sourceKey}:{recommendationStatus}:{periodFromUtc:yyyyMMdd}:{periodToUtc:yyyyMMdd}";
        var confidenceScore = row.ConfidencePct > 0 && !IsProductDecisionInsufficientData(row)
            ? row.ConfidencePct
            : (int?)null;
        var warningCodes = BuildProductDecisionWarningCodes(row);
        var confidenceLevel = ResolveProductDecisionConfidenceLevel(row, confidenceScore, warningCodes);
        var primaryDrivers = BuildProductDecisionPrimaryDrivers(row, warningCodes);
        var expectedImpactRsd = ResolveProductDecisionExpectedImpact(row);
        var impactWindowDays = ResolveProductDecisionImpactWindowDays(recommendationStatus);
        var riskIfIgnored = BuildProductDecisionRiskIfIgnored(recommendationStatus);
        var explainabilityText = string.IsNullOrWhiteSpace(row.RecommendationReason)
            ? BuildRecommendationReason(
                recommendationStatus,
                row.Revenue,
                row.UnitsSold,
                row.VelocityUnitsPerDay,
                row.MarginPct,
                row.TrendPct,
                row.StockGap,
                row.CurrentStock,
                row.MinStock,
                row.DaysSinceLastSale,
                row.DataQualityStatus)
            : row.RecommendationReason;
        var inputFreshnessStatus = ResolveProductDecisionInputFreshnessStatus(row, confidenceLevel);
        var confidenceBreakdown = BuildProductDecisionConfidenceBreakdown(
            row,
            confidenceLevel,
            confidenceScore,
            warningCodes,
            inputFreshnessStatus);
        var alternativeRecommendations = BuildProductDecisionAlternativeRecommendations(
            row,
            confidenceLevel,
            confidenceScore,
            warningCodes,
            inputFreshnessStatus);
        var evidenceChain = BuildProductDecisionEvidenceChain(
            row,
            confidenceLevel,
            confidenceScore,
            warningCodes,
            expectedImpactRsd,
            inputFreshnessStatus,
            explainabilityText);
        var decisionTree = BuildProductDecisionDecisionTree(
            row,
            confidenceLevel,
            warningCodes,
            inputFreshnessStatus,
            alternativeRecommendations,
            explainabilityText);
        var whyPanel = BuildProductDecisionWhyPanel(
            row,
            confidenceLevel,
            confidenceScore,
            primaryDrivers,
            warningCodes,
            expectedImpactRsd,
            impactWindowDays,
            riskIfIgnored,
            explainabilityText,
            inputFreshnessStatus,
            confidenceBreakdown,
            alternativeRecommendations,
            evidenceChain,
            decisionTree);

        return new ProductDecisionConfidenceProfile(
            RecommendationId: recommendationId,
            SourceType: sourceType,
            SourceKey: sourceKey,
            RecommendationType: recommendationStatus,
            ConfidenceLevel: confidenceLevel,
            ConfidenceScore: confidenceScore,
            PrimaryDrivers: primaryDrivers,
            WarningCodes: warningCodes,
            ConfidenceBreakdown: confidenceBreakdown,
            AlternativeRecommendations: alternativeRecommendations,
            ExpectedImpactRsd: expectedImpactRsd,
            ImpactWindowDays: impactWindowDays,
            RiskIfIgnored: riskIfIgnored,
            ExplainabilityText: explainabilityText,
            InputFreshnessStatus: inputFreshnessStatus,
            EvidenceChain: evidenceChain,
            WhyPanel: whyPanel);
    }

    private static ProductDecisionWhyPanelDto BuildProductDecisionWhyPanel(
        ProductDecisionCenterRowDto row,
        string confidenceLevel,
        int? confidenceScore,
        IReadOnlyList<string> primaryDrivers,
        IReadOnlyCollection<string> warningCodes,
        decimal? expectedImpactRsd,
        int? impactWindowDays,
        string riskIfIgnored,
        string explainabilityText,
        string inputFreshnessStatus,
        IReadOnlyList<ProductDecisionEvidenceNodeDto> confidenceBreakdown,
        IReadOnlyList<ProductDecisionAlternativeRecommendationDto> alternativeRecommendations,
        IReadOnlyList<ProductDecisionEvidenceNodeDto> evidenceChain,
        IReadOnlyList<ProductDecisionDecisionTreeNodeDto> decisionTree)
    {
        var summarySource = string.IsNullOrWhiteSpace(row.RecommendationReason)
            ? "backend_composed"
            : "recommendation_reason";

        return new ProductDecisionWhyPanelDto
        {
            RecommendationStatus = row.RecommendationStatus,
            RecommendationLabel = RecommendationLabel(row.RecommendationStatus),
            RecommendationReason = row.RecommendationReason,
            RecommendedAction = RecommendedAction(row.RecommendationStatus),
            ExplainabilityText = explainabilityText,
            SummarySource = summarySource,
            SummaryFallbackUsed = summarySource != "recommendation_reason",
            SummaryFallbackReason = summarySource != "recommendation_reason" ? "recommendation_reason_missing" : null,
            ReasonCodes = [.. row.ReasonCodes],
            PrimaryDrivers = [.. primaryDrivers],
            WarningCodes = [.. warningCodes],
            ConfidenceLevel = confidenceLevel,
            ConfidenceScore = confidenceScore,
            ConfidencePct = row.ConfidencePct,
            ReliabilityPct = row.ReliabilityPct,
            DataQualityStatus = row.DataQualityStatus,
            InputFreshnessStatus = inputFreshnessStatus,
            RecommendationAllowed = row.RecommendationAllowed,
            ExpectedImpactRsd = expectedImpactRsd,
            ImpactWindowDays = impactWindowDays,
            RiskIfIgnored = riskIfIgnored,
            ConfidenceBreakdown = [.. confidenceBreakdown],
            AlternativeRecommendations = [.. alternativeRecommendations],
            EvidenceChain = [.. evidenceChain],
            DecisionTree = [.. decisionTree],
            LifecycleState = RecommendationLifecycleSemantics.LifecycleStates.Issued,
            OutcomeEvidenceState = RecommendationLifecycleSemantics.OutcomeEvidenceStates.Pending,
            LearningEligible = false,
            LearningEligibilityReasonCodes =
            [
                "lifecycle_issued_only",
                "outcome_not_measured",
                "acceptance_is_not_success"
            ]
        };
    }

    private static void ApplyIssuedRecommendationLifecycle(ProductDecisionCenterRowDto row)
    {
        var lifecycle = RecommendationLifecycleSemantics.ProjectIssuedRecommendation();
        row.RecommendationLifecycle = lifecycle;
        row.LifecycleState = lifecycle.LifecycleState;
        row.OutcomeEvidenceState = lifecycle.OutcomeEvidenceState;
        row.LearningEligible = lifecycle.LearningEligible;
        row.LearningEligibilityReasonCodes = lifecycle.LearningEligibilityReasonCodes.ToList();

        row.WhyPanel.LifecycleState = lifecycle.LifecycleState;
        row.WhyPanel.OutcomeEvidenceState = lifecycle.OutcomeEvidenceState;
        row.WhyPanel.LearningEligible = lifecycle.LearningEligible;
        row.WhyPanel.LearningEligibilityReasonCodes = lifecycle.LearningEligibilityReasonCodes.ToList();
    }

    internal static void ApplyDecisionEvidenceSnapshotPreview(
        ProductDecisionCenterRowDto row,
        DateTime periodFromUtc,
        DateTime periodToUtc)
    {
        row.EvidenceSnapshotStatus = "absent";
        row.EvidenceSnapshotPreview = new ProductDecisionEvidenceSnapshotPreviewDto
        {
            SchemaVersion = 1,
            RecommendationId = row.RecommendationId,
            RecommendationType = row.RecommendationType,
            PeriodFromUtc = periodFromUtc.ToString("yyyy-MM-dd"),
            PeriodToUtc = periodToUtc.ToString("yyyy-MM-dd"),
            DataQualityStatus = row.DataQualityStatus,
            ConfidenceLevel = row.ConfidenceLevel,
            ConfidenceScore = row.ConfidenceScore,
            ConfidencePct = row.ConfidencePct,
            ReliabilityPct = row.ReliabilityPct,
            InputFreshnessStatus = row.InputFreshnessStatus,
            ExplainabilityText = row.ExplainabilityText,
            ReasonCodes = [.. row.ReasonCodes],
            WarningCodes = [.. row.WarningCodes],
            PrimaryDrivers = [.. row.PrimaryDrivers],
            EvidenceChain = [.. row.EvidenceChain],
            ConfidenceBreakdown = [.. row.ConfidenceBreakdown]
        };
    }

    private static IReadOnlyList<ProductDecisionDecisionTreeNodeDto> BuildProductDecisionDecisionTree(
        ProductDecisionCenterRowDto row,
        string confidenceLevel,
        IReadOnlyCollection<string> warningCodes,
        string inputFreshnessStatus,
        IReadOnlyList<ProductDecisionAlternativeRecommendationDto> alternativeRecommendations,
        string explainabilityText)
    {
        var decisionTree = new List<ProductDecisionDecisionTreeNodeDto>();
        var selectedRecommendationLabel = string.IsNullOrWhiteSpace(row.RecommendationLabel)
            ? RecommendationLabel(row.RecommendationStatus)
            : row.RecommendationLabel;
        var selectedAction = RecommendedAction(row.RecommendationStatus);
        var dataQualityBlocksDecision = IsProductDecisionInsufficientData(row) || row.DataQualityStatus is "critical" or "insufficient_data";
        var freshnessBlocksDecision = inputFreshnessStatus is "critical";
        var gateDetail = dataQualityBlocksDecision
            ? "Kritični ili nedovoljni ulazi preusmeravaju odluku na sigurne grane."
            : "Ulazi su dovoljno stabilni da se nastavi determinističkom granom.";
        var freshnessDetail = freshnessBlocksDecision
            ? "Ulaz je prestar za samouverenu granu."
            : "Ulaz je dovoljno svež za nastavak grane.";

        void AddNode(
            string category,
            string code,
            string label,
            string valueText,
            IReadOnlyList<string> sourceFields,
            bool isSelected,
            string? detail = null)
        {
            decisionTree.Add(new ProductDecisionDecisionTreeNodeDto
            {
                Category = category,
                Code = code,
                Label = label,
                ValueText = valueText,
                SourceFields = [.. sourceFields],
                IsSelected = isSelected,
                Detail = detail
            });
        }

        AddNode(
            "decision",
            "selected_recommendation",
            "Odabrana preporuka",
            selectedRecommendationLabel,
            ["RecommendationStatus", "RecommendationLabel", "RecommendationReason"],
            true,
            string.IsNullOrWhiteSpace(explainabilityText) ? null : explainabilityText);

        AddNode(
            "gate",
            "data_quality_gate",
            "Kvalitet podataka",
            dataQualityBlocksDecision ? "Blokira granu" : "Prolazi dalje",
            ["DataQualityStatus", "WarningCodes", "ReasonCodes"],
            !dataQualityBlocksDecision,
            gateDetail);

        AddNode(
            "gate",
            "freshness_gate",
            "Svežina ulaza",
            DescribeProductDecisionFreshnessStatus(inputFreshnessStatus),
            ["InputFreshnessStatus", "DataQualityStatus"],
            !freshnessBlocksDecision,
            freshnessDetail);

        AddNode(
            "branch",
            "selected_branch",
            selectedRecommendationLabel,
            selectedAction,
            ["RecommendationStatus", "RecommendedAction", "ReasonCodes", "PrimaryDrivers"],
            true,
            BuildProductDecisionBranchDetail(row, confidenceLevel, warningCodes));

        foreach (var alternative in alternativeRecommendations.Take(2))
        {
            AddNode(
                "branch",
                $"alternative_{alternative.Rank}",
                alternative.RecommendationLabel,
                alternative.RecommendedAction,
                ["AlternativeRecommendations", "ReasonCodes"],
                false,
                alternative.WhyLowerRanked);
        }

        return decisionTree;
    }

    private static string BuildProductDecisionBranchDetail(
        ProductDecisionCenterRowDto row,
        string confidenceLevel,
        IReadOnlyCollection<string> warningCodes)
    {
        return row.RecommendationStatus switch
        {
            "REPLENISH" => "Signal prodaje i zalihe otvaraju granu dopune.",
            "BOOST" => "Signal prodaje i prostora za rast otvaraju granu pojačavanja.",
            "MARKDOWN" => "Slabiji obrt i pritisak signala otvaraju granu sniženja.",
            "DO_NOT_ORDER" => "Signal blokira novu narudžbinu dok se ne oporavi.",
            "FIX_DATA" => "Nedostajući ili kritični ulazi preusmeravaju tok na ispravku podataka.",
            "WATCH" => "Signal je dovoljno jasan da se nastavi praćenje bez hitne intervencije.",
            "INSUFFICIENT_DATA" => "Nedovoljno signala ostavlja odluku u sigurnom režimu.",
            _ when warningCodes.Contains("expected_impact_denominator_missing") => "Ograničen ulazni signal vodi ka konzervativnoj grani.",
            _ when confidenceLevel == "high" => "Visoka sigurnost zadržava izabranu granu.",
            _ => "Deterministička grana vodi do izabrane preporuke."
        };
    }

    private static IReadOnlyList<ProductDecisionEvidenceNodeDto> BuildProductDecisionEvidenceChain(
        ProductDecisionCenterRowDto row,
        string confidenceLevel,
        int? confidenceScore,
        IReadOnlyCollection<string> warningCodes,
        decimal? expectedImpactRsd,
        string inputFreshnessStatus,
        string explainabilityText)
    {
        var evidence = new List<ProductDecisionEvidenceNodeDto>();
        var impactWindowText = row.ImpactWindowDays.HasValue
            ? $"{row.ImpactWindowDays.Value} dana"
            : "nije dostupno";

        void AddNode(
            string category,
            string code,
            string label,
            string valueText,
            IReadOnlyList<string> sourceFields,
            bool isMissing = false,
            string? detail = null)
        {
            evidence.Add(new ProductDecisionEvidenceNodeDto
            {
                Category = category,
                Code = code,
                Label = label,
                ValueText = valueText,
                SourceFields = [.. sourceFields],
                IsMissing = isMissing,
                Detail = detail
            });
        }

        AddNode(
            "decision",
            "selected_recommendation",
            "Odabrana preporuka",
            string.IsNullOrWhiteSpace(row.RecommendationLabel) ? row.RecommendationStatus : row.RecommendationLabel,
            ["RecommendationStatus", "RecommendationLabel", "RecommendationReason"],
            detail: string.IsNullOrWhiteSpace(explainabilityText) ? null : explainabilityText);

        AddNode(
            "evidence",
            "sales_signal",
            "Signal prodaje",
            $"{FormatProductDecisionNumber(row.VelocityUnitsPerDay, 2)} kom/dan · {row.UnitsSold} kom",
            ["VelocityUnitsPerDay", "UnitsSold", "Revenue"],
            detail: $"Prihod {FormatProductDecisionAmount(row.Revenue)}");

        AddNode(
            "evidence",
            "stock_signal",
            "Signal zalihe",
            $"{row.CurrentStock} kom · min {row.MinStock} · gap {row.StockGap}",
            ["CurrentStock", "MinStock", "StockGap", "StockCoverDays", "StockCoverStatus"],
            detail: $"Pokrivenost: {(string.IsNullOrWhiteSpace(row.StockCoverStatusLabel) ? row.StockCoverStatus : row.StockCoverStatusLabel)}");

        AddNode(
            "evidence",
            "margin_signal",
            "Signal marže",
            row.MarginPct.HasValue
                ? $"{FormatProductDecisionNumber(row.MarginPct.Value, 1)}% · doprinos {FormatProductDecisionAmount(row.MarginContribution)}"
                : "Nedostaje marža",
            ["MarginPct", "MarginContribution", "MarginCoveragePct"],
            isMissing: !row.MarginPct.HasValue,
            detail: $"Pokrivenost nabavnom cenom {FormatProductDecisionNumber(row.MarginCoveragePct, 1)}%");

        AddNode(
            "evidence",
            "trend_signal",
            "Signal trenda",
            row.TrendPct.HasValue ? $"{FormatProductDecisionNumber(row.TrendPct.Value, 1)}%" : "Nedostaje trend",
            ["TrendPct", "DaysSinceLastSale"],
            isMissing: !row.TrendPct.HasValue,
            detail: row.DaysSinceLastSale.HasValue ? $"Dani od poslednje prodaje {row.DaysSinceLastSale.Value}" : "Dani od poslednje prodaje nisu dostupni");

        AddNode(
            "confidence",
            "confidence_signal",
            "Pouzdanost",
            confidenceLevel == "insufficient_data"
                ? "Nedovoljno podataka"
                : $"{DescribeProductDecisionConfidenceLevel(confidenceLevel)} · {(confidenceScore?.ToString(CultureInfo.InvariantCulture) ?? FormatProductDecisionNumber(row.ConfidencePct, 0))}%",
            ["ConfidenceLevel", "ConfidenceScore", "ConfidencePct", "ReliabilityPct"],
            isMissing: confidenceLevel == "insufficient_data",
            detail: $"Pouzdanost signala {FormatProductDecisionNumber(row.ReliabilityPct, 0)}%");

        AddNode(
            "confidence",
            "freshness_signal",
            "Svežina ulaza",
            DescribeProductDecisionFreshnessStatus(inputFreshnessStatus),
            ["InputFreshnessStatus", "DataQualityStatus"],
            detail: $"Kvalitet podataka {DescribeProductDecisionDataQualityStatus(row.DataQualityStatus)}");

        AddNode(
            "constraint",
            "actionability",
            "Akcija je dozvoljena",
            row.RecommendationAllowed ? "Da" : "Ne",
            ["RecommendationAllowed"],
            isMissing: false,
            detail: row.RecommendationAllowed ? "Preporuka može da se izvrši." : "Preporuka je blokirana.");

        foreach (var warningCode in warningCodes)
        {
            AddNode(
                "constraint",
                $"warning:{warningCode}",
                DescribeProductDecisionWarningCode(warningCode),
                "Upozorenje",
                ["WarningCodes", "ReasonCodes"],
                detail: warningCode);
        }

        AddNode(
            "impact",
            "expected_impact",
            "Očekivani uticaj",
            expectedImpactRsd.HasValue
                ? $"{FormatProductDecisionAmount(expectedImpactRsd.Value)} u {impactWindowText}"
                : "Nije dostupno",
            ["ExpectedImpactRsd", "ImpactWindowDays", "RiskIfIgnored"],
            isMissing: !expectedImpactRsd.HasValue,
            detail: string.IsNullOrWhiteSpace(row.RiskIfIgnored) ? null : row.RiskIfIgnored);

        return evidence;
    }

    private static IReadOnlyList<ProductDecisionEvidenceNodeDto> BuildProductDecisionConfidenceBreakdown(
        ProductDecisionCenterRowDto row,
        string confidenceLevel,
        int? confidenceScore,
        IReadOnlyCollection<string> warningCodes,
        string inputFreshnessStatus)
    {
        var breakdown = new List<ProductDecisionEvidenceNodeDto>();
        var confidenceScoreText = confidenceScore.HasValue
            ? confidenceScore.Value.ToString(CultureInfo.InvariantCulture)
            : FormatProductDecisionNumber(row.ConfidencePct, 0);

        void AddNode(
            string category,
            string code,
            string label,
            string valueText,
            IReadOnlyList<string> sourceFields,
            bool isMissing = false,
            string? detail = null)
        {
            breakdown.Add(new ProductDecisionEvidenceNodeDto
            {
                Category = category,
                Code = code,
                Label = label,
                ValueText = valueText,
                SourceFields = [.. sourceFields],
                IsMissing = isMissing,
                Detail = detail
            });
        }

        AddNode(
            "confidence",
            "confidence_score",
            "Ocena pouzdanosti",
            confidenceLevel == "insufficient_data"
                ? "Nedovoljno podataka"
                : $"{DescribeProductDecisionConfidenceLevel(confidenceLevel)} · {confidenceScoreText}%",
            ["ConfidenceLevel", "ConfidenceScore", "ConfidencePct"],
            isMissing: confidenceLevel == "insufficient_data",
            detail: "Ocena kombinuje snagu signala i dostupnost ulaza.");

        AddNode(
            "confidence",
            "evidence_coverage",
            "Pokrivenost signala",
            ResolveProductDecisionConfidenceCoverageText(row, confidenceLevel, confidenceScore, warningCodes),
            ["UnitsSold", "VelocityUnitsPerDay", "MarginPct", "TrendPct", "DaysSinceLastSale", "WarningCodes", "ReasonCodes"],
            detail: ResolveProductDecisionConfidenceCoverageDetail(row, confidenceLevel, warningCodes));

        AddNode(
            "confidence",
            "reliability_signal",
            "Pouzdanost signala",
            $"{FormatProductDecisionNumber(row.ReliabilityPct, 0)}%",
            ["ReliabilityPct", "SignalConfidencePct"],
            detail: row.SignalConfidencePct > 0m
                ? $"SignalConfidence {FormatProductDecisionNumber(row.SignalConfidencePct, 0)}%"
                : "Nema dodatnog signala pouzdanosti.");

        AddNode(
            "confidence",
            "freshness_signal",
            "Svežina ulaza",
            DescribeProductDecisionFreshnessStatus(inputFreshnessStatus),
            ["InputFreshnessStatus", "DataQualityStatus"],
            detail: $"Kvalitet podataka {DescribeProductDecisionDataQualityStatus(row.DataQualityStatus)}");

        AddNode(
            "confidence",
            "data_quality_signal",
            "Kvalitet podataka",
            DescribeProductDecisionDataQualityStatus(row.DataQualityStatus),
            ["DataQualityStatus", "WarningCodes"],
            isMissing: string.Equals(row.DataQualityStatus, "insufficient_data", StringComparison.OrdinalIgnoreCase),
            detail: ResolveProductDecisionDataQualityDetail(row, warningCodes));

        return breakdown;
    }

    private static IReadOnlyList<ProductDecisionAlternativeRecommendationDto> BuildProductDecisionAlternativeRecommendations(
        ProductDecisionCenterRowDto row,
        string selectedConfidenceLevel,
        int? selectedConfidenceScore,
        IReadOnlyCollection<string> warningCodes,
        string inputFreshnessStatus)
    {
        var candidates = new List<ProductDecisionAlternativeRecommendationCandidate>();

        void AddCandidate(string status)
        {
            if (string.Equals(status, row.RecommendationStatus, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var score = ResolveProductDecisionAlternativeScore(status, row, selectedConfidenceLevel, selectedConfidenceScore, warningCodes, inputFreshnessStatus);
            if (score <= 0)
            {
                return;
            }

            candidates.Add(new ProductDecisionAlternativeRecommendationCandidate(
                status,
                score,
                BuildProductDecisionAlternativeWhyLowerRanked(status, row.RecommendationStatus, row, selectedConfidenceLevel, selectedConfidenceScore, warningCodes, inputFreshnessStatus)));
        }

        AddCandidate("FIX_DATA");
        AddCandidate("REPLENISH");
        AddCandidate("BOOST");
        AddCandidate("MARKDOWN");
        AddCandidate("DO_NOT_ORDER");
        AddCandidate("WATCH");

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => RecommendationPriority(candidate.RecommendationStatus))
            .Take(2)
            .Select((candidate, index) =>
            {
                var recommendationStatus = NormalizeRecommendationStatus(candidate.RecommendationStatus);
                var confidenceScore = candidate.Score;
                var confidenceLevel = ResolveAlternativeConfidenceLevel(confidenceScore);
                var reasonCodes = BuildProductDecisionAlternativeReasonCodes(recommendationStatus, row, warningCodes);

                return new ProductDecisionAlternativeRecommendationDto
                {
                    Rank = index + 1,
                    RecommendationStatus = recommendationStatus,
                    RecommendationLabel = RecommendationLabel(recommendationStatus),
                    RecommendedAction = RecommendedAction(recommendationStatus),
                    Reason = BuildRecommendationReason(
                        recommendationStatus,
                        row.Revenue,
                        row.UnitsSold,
                        row.VelocityUnitsPerDay,
                        row.MarginPct,
                        row.TrendPct,
                        row.StockGap,
                        row.CurrentStock,
                        row.MinStock,
                        row.DaysSinceLastSale,
                        row.DataQualityStatus),
                    ReasonCodes = [.. reasonCodes],
                    ConfidenceLevel = confidenceLevel,
                    ConfidenceScore = confidenceScore,
                    ReliabilityPct = ResolveAlternativeReliabilityPct(confidenceScore, row),
                    DataQualityStatus = string.IsNullOrWhiteSpace(row.DataQualityStatus) ? "insufficient_data" : row.DataQualityStatus,
                    WhyLowerRanked = candidate.WhyLowerRanked
                };
            })
            .ToList();
    }

    private static int ResolveProductDecisionAlternativeScore(
        string recommendationStatus,
        ProductDecisionCenterRowDto row,
        string selectedConfidenceLevel,
        int? selectedConfidenceScore,
        IReadOnlyCollection<string> warningCodes,
        string inputFreshnessStatus)
    {
        var normalizedStatus = NormalizeRecommendationStatus(recommendationStatus);
        var selectedScore = selectedConfidenceScore ?? 0;
        var dataQuality = (row.DataQualityStatus ?? string.Empty).Trim().ToLowerInvariant();
        var lowConfidencePenalty = selectedScore >= 80 ? 18 : selectedScore >= 60 ? 10 : selectedScore >= 40 ? 5 : 0;

        var score = normalizedStatus switch
        {
            "FIX_DATA" => ResolveFixDataAlternativeScore(row, warningCodes),
            "REPLENISH" => ResolveReplenishAlternativeScore(row, dataQuality, lowConfidencePenalty),
            "BOOST" => ResolveBoostAlternativeScore(row, dataQuality, lowConfidencePenalty),
            "MARKDOWN" => ResolveMarkdownAlternativeScore(row, dataQuality, lowConfidencePenalty),
            "DO_NOT_ORDER" => ResolveDoNotOrderAlternativeScore(row, dataQuality, lowConfidencePenalty),
            "WATCH" => ResolveWatchAlternativeScore(row, selectedConfidenceLevel, selectedScore, warningCodes, inputFreshnessStatus),
            _ => 0
        };

        if (normalizedStatus == row.RecommendationStatus)
        {
            return 0;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static int ResolveFixDataAlternativeScore(ProductDecisionCenterRowDto row, IReadOnlyCollection<string> warningCodes)
    {
        var score = 0;
        if (warningCodes.Contains("missing_cost", StringComparer.OrdinalIgnoreCase)) score += 55;
        if (warningCodes.Contains("missing_supplier", StringComparer.OrdinalIgnoreCase)) score += 55;
        if (warningCodes.Contains("data_quality_critical", StringComparer.OrdinalIgnoreCase)) score += 30;
        if (warningCodes.Contains("insufficient_data", StringComparer.OrdinalIgnoreCase)) score += 20;
        if (row.ReasonCodes.Contains("missing_cost", StringComparer.OrdinalIgnoreCase)) score += 10;
        if (row.ReasonCodes.Contains("missing_supplier", StringComparer.OrdinalIgnoreCase)) score += 10;
        if (string.Equals(row.DataQualityStatus, "critical", StringComparison.OrdinalIgnoreCase)) score += 10;
        return score;
    }

    private static int ResolveReplenishAlternativeScore(ProductDecisionCenterRowDto row, string dataQuality, int lowConfidencePenalty)
    {
        var score = 15;
        score += (int)Math.Round(Math.Clamp(row.StockGap, 0, 20) * 4m, MidpointRounding.AwayFromZero);
        score += (int)Math.Round(Math.Clamp(row.VelocityUnitsPerDay, 0m, 3m) * 12m, MidpointRounding.AwayFromZero);
        if (row.LostSalesEstimate > 0m) score += 10;
        if (row.CurrentStock <= row.MinStock) score += 12;
        if (row.CurrentStock <= 0) score += 8;
        if (dataQuality == "warning") score -= 4;
        if (dataQuality == "critical" || dataQuality == "insufficient_data") score -= 18;
        return score - lowConfidencePenalty / 2;
    }

    private static int ResolveBoostAlternativeScore(ProductDecisionCenterRowDto row, string dataQuality, int lowConfidencePenalty)
    {
        var score = 18;
        if (row.TrendPct.HasValue && row.TrendPct.Value > 0m) score += (int)Math.Round(Math.Min(row.TrendPct.Value, 20m), MidpointRounding.AwayFromZero);
        if (row.MarginPct.HasValue) score += (int)Math.Round(Math.Min(row.MarginPct.Value / 2m, 20m), MidpointRounding.AwayFromZero);
        score += (int)Math.Round(Math.Clamp(row.VelocityUnitsPerDay, 0m, 3m) * 10m, MidpointRounding.AwayFromZero);
        if (row.StockGap > 0) score += 8;
        if (row.Revenue > 0m) score += 4;
        if (dataQuality == "warning") score -= 3;
        if (dataQuality == "critical" || dataQuality == "insufficient_data") score -= 16;
        return score - lowConfidencePenalty / 3;
    }

    private static int ResolveMarkdownAlternativeScore(ProductDecisionCenterRowDto row, string dataQuality, int lowConfidencePenalty)
    {
        var score = 18;
        if (row.DaysSinceLastSale.HasValue && row.DaysSinceLastSale.Value >= 45) score += 24;
        if (row.VelocityUnitsPerDay < 0.25m) score += 22;
        if ((row.TrendPct ?? 0m) < -5m) score += 16;
        if ((row.MarginPct ?? 0m) < 12m) score += 10;
        if (row.CurrentStock > row.MinStock) score += 10;
        if (row.SlowStockCapital > 0m) score += 10;
        if (dataQuality == "warning") score += 2;
        if (dataQuality == "critical" || dataQuality == "insufficient_data") score -= 10;
        return score - lowConfidencePenalty / 4;
    }

    private static int ResolveDoNotOrderAlternativeScore(ProductDecisionCenterRowDto row, string dataQuality, int lowConfidencePenalty)
    {
        var score = 14;
        if (row.CurrentStock > row.MinStock * 3) score += 28;
        if (row.VelocityUnitsPerDay < 0.25m) score += 18;
        if ((row.TrendPct ?? 0m) < 0m) score += 10;
        if (row.DaysSinceLastSale.HasValue && row.DaysSinceLastSale.Value >= 45) score += 10;
        if (dataQuality == "warning") score += 2;
        if (dataQuality == "critical" || dataQuality == "insufficient_data") score -= 12;
        return score - lowConfidencePenalty / 5;
    }

    private static int ResolveWatchAlternativeScore(
        ProductDecisionCenterRowDto row,
        string selectedConfidenceLevel,
        int selectedConfidenceScore,
        IReadOnlyCollection<string> warningCodes,
        string inputFreshnessStatus)
    {
        var score = 26;
        if (selectedConfidenceLevel is "low" or "insufficient_data") score += 18;
        else if (selectedConfidenceLevel == "medium") score += 10;
        else if (selectedConfidenceScore >= 80) score += 2;

        if (warningCodes.Count > 0) score += 8;
        if (string.Equals(inputFreshnessStatus, "stale", StringComparison.OrdinalIgnoreCase)) score += 4;
        if (string.Equals(inputFreshnessStatus, "critical", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (row.RecommendationStatus == "FIX_DATA") score += 4;
        if (row.RecommendationStatus == "REPLENISH" || row.RecommendationStatus == "BOOST") score += 2;
        return score;
    }

    private static string BuildProductDecisionAlternativeWhyLowerRanked(
        string candidateStatus,
        string selectedStatus,
        ProductDecisionCenterRowDto row,
        string selectedConfidenceLevel,
        int? selectedConfidenceScore,
        IReadOnlyCollection<string> warningCodes,
        string inputFreshnessStatus)
    {
        var selectedLabel = RecommendationLabel(selectedStatus);

        return selectedStatus switch
        {
            "FIX_DATA" => candidateStatus switch
            {
                "WATCH" => "Kritični problemi podataka i dalje imaju prednost nad čekanjem.",
                "REPLENISH" => "Dopuna bi ostala zasnovana na nepouzdanim ulazima.",
                "BOOST" => "Rast potražnje nije pouzdano potkrepljen dok je kvalitet podataka kritičan.",
                "MARKDOWN" => "Sprečavanje greške u podacima ima veću hitnost od cenovne korekcije.",
                "DO_NOT_ORDER" => "Zaustavljanje nove narudžbine ne rešava uzrok loših ulaza.",
                _ => $"Odabrana preporuka {selectedLabel} je i dalje dominantna nad ovom alternativom."
            },
            "REPLENISH" => candidateStatus switch
            {
                "BOOST" => "Dopuna ima neposredniji signal od širenja potražnje, jer je stock gap već vidljiv.",
                "WATCH" => "Čekanje bi odložilo odgovor na postojeći manjak zalihe.",
                "DO_NOT_ORDER" => "Holding je slabiji odgovor od dopune dok je stock gap otvoren.",
                "MARKDOWN" => "Sniženje ne rešava trenutni rizik rasprodaje.",
                _ => $"Odabrana preporuka {selectedLabel} ima neposredniji signal od ove alternative."
            },
            "BOOST" => candidateStatus switch
            {
                "REPLENISH" => "Dopuna ostaje bliža trenutnom stock gap-u od širenja potražnje.",
                "WATCH" => "Pasivno praćenje ne koristi dovoljno jak trend i maržu.",
                "MARKDOWN" => "Sniženje bi slabilo signal koji traži rast, a ne rasprodaju.",
                _ => $"Odabrana preporuka {selectedLabel} ima snažniju kombinaciju trenda i marže."
            },
            "MARKDOWN" => candidateStatus switch
            {
                "DO_NOT_ORDER" => "Potpuno zaustavljanje narudžbine je rigidnije od ciljane korekcije cene.",
                "WATCH" => "Čekanje bi zadržalo kapital zarobljen u sporoj robi.",
                "REPLENISH" => "Dopuna bi pojačala pritisak na već spor signal zalihe.",
                _ => $"Odabrana preporuka {selectedLabel} bolje odgovara sporoj prodaji i starijoj zalihi."
            },
            "DO_NOT_ORDER" => candidateStatus switch
            {
                "MARKDOWN" => "Cenovna korekcija je aktivniji odgovor na spor obrt od pukog čekanja.",
                "WATCH" => "Čekanje ne ublažava rizik dodatnog lagera dovoljno brzo.",
                "REPLENISH" => "Dopuna je slabija od zadržavanja postojećeg lagera.",
                _ => $"Odabrana preporuka {selectedLabel} je bezbednija za postojeći lager."
            },
            "WATCH" => candidateStatus switch
            {
                "REPLENISH" => "Signal već ima dovoljno snage za konkretnu akciju, pa praćenje kasni za njim.",
                "BOOST" => "Aktivan potez bolje koristi postojeći signal od pukog praćenja.",
                "MARKDOWN" => "Praćenje ne rešava spor obrt ako je signal već vidljiv.",
                "DO_NOT_ORDER" => "Praćenje je slabije od jasne odluke o zalihama.",
                "FIX_DATA" => "Ako je problem u podacima, bolje je ispraviti ih nego samo pratiti.",
                _ => $"Odabrana preporuka {selectedLabel} je i dalje najbezbedniji odgovor."
            },
            _ => $"Odabrana preporuka {selectedLabel} je i dalje dominantna nad ovom alternativom."
        };
    }

    private static IReadOnlyList<string> BuildProductDecisionAlternativeReasonCodes(
        string recommendationStatus,
        ProductDecisionCenterRowDto row,
        IReadOnlyCollection<string> warningCodes)
    {
        var codes = new List<string>();

        void Add(string code)
        {
            if (!codes.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                codes.Add(code);
            }
        }

        switch (recommendationStatus)
        {
            case "FIX_DATA":
                foreach (var code in warningCodes.Where(code =>
                             string.Equals(code, "missing_cost", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(code, "missing_supplier", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(code, "data_quality_critical", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(code, "insufficient_data", StringComparison.OrdinalIgnoreCase)))
                {
                    Add(code);
                }

                foreach (var code in row.ReasonCodes.Where(code =>
                             string.Equals(code, "missing_cost", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(code, "missing_supplier", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(code, "data_quality_blocker", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(code, "insufficient_history", StringComparison.OrdinalIgnoreCase)))
                {
                    Add(code);
                }
                break;
            case "REPLENISH":
                if (row.StockGap > 0 || row.CurrentStock < row.MinStock) Add("low_stock");
                if (row.VelocityUnitsPerDay >= 0.8m) Add("high_velocity");
                if (row.ReasonCodes.Contains("replenish_needed", StringComparer.OrdinalIgnoreCase)) Add("replenish_needed");
                break;
            case "BOOST":
                if (row.VelocityUnitsPerDay >= 0.8m) Add("high_velocity");
                if (row.TrendPct.HasValue && row.TrendPct.Value >= 0m) Add("low_stock");
                if (row.MarginPct.HasValue && row.MarginPct.Value >= 10m) Add("high_velocity");
                if (row.StockGap > 0) Add("low_stock");
                break;
            case "MARKDOWN":
                if (row.DaysSinceLastSale.HasValue && row.DaysSinceLastSale.Value >= 45) Add("stale_stock");
                if (row.CurrentStock > row.MinStock) Add("high_stock_risk");
                if ((row.MarginPct ?? 0m) < 10m) Add("poor_margin");
                break;
            case "DO_NOT_ORDER":
                if (row.CurrentStock > row.MinStock * 3) Add("high_stock_risk");
                if (row.DaysSinceLastSale.HasValue && row.DaysSinceLastSale.Value >= 45) Add("stale_stock");
                if ((row.MarginPct ?? 0m) < 10m) Add("poor_margin");
                break;
            case "WATCH":
                if (warningCodes.Contains("insufficient_data", StringComparer.OrdinalIgnoreCase)) Add("insufficient_history");
                if (row.ReasonCodes.Contains("insufficient_history", StringComparer.OrdinalIgnoreCase)) Add("insufficient_history");
                if (row.ReasonCodes.Contains("low_stock", StringComparer.OrdinalIgnoreCase)) Add("low_stock");
                if (row.ReasonCodes.Contains("stale_stock", StringComparer.OrdinalIgnoreCase)) Add("stale_stock");
                break;
        }

        if (codes.Count == 0)
        {
            codes.AddRange(row.ReasonCodes.Take(2));
        }

        return codes;
    }

    private static string ResolveAlternativeConfidenceLevel(int confidenceScore) =>
        confidenceScore >= 80 ? "high"
            : confidenceScore >= 55 ? "medium"
            : confidenceScore >= 30 ? "low"
            : "insufficient_data";

    private static int ResolveAlternativeReliabilityPct(int confidenceScore, ProductDecisionCenterRowDto row)
    {
        var reliability = confidenceScore;
        if (string.Equals(row.DataQualityStatus, "critical", StringComparison.OrdinalIgnoreCase))
        {
            reliability -= 20;
        }
        else if (string.Equals(row.DataQualityStatus, "warning", StringComparison.OrdinalIgnoreCase))
        {
            reliability -= 10;
        }

        return Math.Clamp(reliability, 5, 99);
    }

    private sealed record ProductDecisionAlternativeRecommendationCandidate(
        string RecommendationStatus,
        int Score,
        string WhyLowerRanked);

    private static string FormatProductDecisionNumber(decimal value, int decimals)
        => value.ToString(decimals > 0 ? $"0.{new string('#', decimals)}" : "0", CultureInfo.InvariantCulture);

    private static string FormatProductDecisionAmount(decimal value)
        => $"{value.ToString("0.##", CultureInfo.InvariantCulture)} RSD";

    private static string DescribeProductDecisionWarningCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "missing_cost" => "Nedostaje nabavna cena",
            "missing_supplier" => "Nedostaje dobavljač",
            "insufficient_history" => "Nedovoljno istorije",
            "expected_impact_denominator_missing" => "Nedostaje ulaz za procenu uticaja",
            "data_quality_critical" => "Kvalitet podataka je kritičan",
            "insufficient_data" => "Nedovoljno podataka",
            "data_quality_blocker" => "Blokada kvaliteta podataka",
            _ => code
        };
    }

    private static string DescribeProductDecisionConfidenceLevel(string confidenceLevel)
    {
        var normalized = (confidenceLevel ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "high" => "Visoka sigurnost",
            "medium" => "Srednja sigurnost",
            "low" => "Niska sigurnost",
            _ => "Nedovoljno podataka"
        };
    }

    private static string DescribeProductDecisionFreshnessStatus(string freshnessStatus)
    {
        var normalized = (freshnessStatus ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "fresh" => "Sveže",
            "stale" => "Zastarelo",
            "critical" => "Kritično",
            _ => "Nije poznato"
        };
    }

    private static string DescribeProductDecisionDataQualityStatus(string dataQualityStatus)
    {
        var normalized = (dataQualityStatus ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "good" => "dobar",
            "warning" => "upozorenje",
            "critical" => "kritičan",
            "insufficient_data" => "nedovoljno podataka",
            _ => dataQualityStatus
        };
    }

    private static string NormalizeRecommendationStatus(string? recommendationStatus) =>
        string.IsNullOrWhiteSpace(recommendationStatus)
            ? "INSUFFICIENT_DATA"
            : recommendationStatus.Trim().ToUpperInvariant();

    private static bool IsProductDecisionInsufficientData(ProductDecisionCenterRowDto row) =>
        string.Equals(row.RecommendationStatus, "INSUFFICIENT_DATA", StringComparison.OrdinalIgnoreCase)
        || string.Equals(row.RecommendationStatus, "FIX_DATA", StringComparison.OrdinalIgnoreCase)
        || string.Equals(row.DataQualityStatus, "critical", StringComparison.OrdinalIgnoreCase);

    private static string ResolveProductDecisionConfidenceLevel(
        ProductDecisionCenterRowDto row,
        int? confidenceScore,
        IReadOnlyCollection<string> warningCodes)
    {
        if (string.Equals(row.RecommendationStatus, "INSUFFICIENT_DATA", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.RecommendationStatus, "FIX_DATA", StringComparison.OrdinalIgnoreCase))
        {
            return "insufficient_data";
        }

        if (string.Equals(row.DataQualityStatus, "critical", StringComparison.OrdinalIgnoreCase))
        {
            return confidenceScore.HasValue && confidenceScore.Value >= 60 ? "low" : "insufficient_data";
        }

        if (warningCodes.Contains("missing_cost", StringComparer.OrdinalIgnoreCase)
            || warningCodes.Contains("missing_supplier", StringComparer.OrdinalIgnoreCase))
        {
            return confidenceScore.HasValue && confidenceScore.Value >= 60 ? "low" : "insufficient_data";
        }

        if (!confidenceScore.HasValue)
        {
            return "insufficient_data";
        }

        if (confidenceScore.Value >= 80 && HasStrongProductDecisionEvidence(row))
        {
            return "high";
        }

        if (confidenceScore.Value >= 60)
        {
            return "medium";
        }

        return "low";
    }

    private static bool HasStrongProductDecisionEvidence(ProductDecisionCenterRowDto row) =>
        row.UnitsSold >= 20
        && row.MarginCoveragePct >= 80m
        && row.TrendPct.HasValue
        && row.VelocityUnitsPerDay > 0.5m;

    private static string ResolveProductDecisionConfidenceCoverageText(
        ProductDecisionCenterRowDto row,
        string confidenceLevel,
        int? confidenceScore,
        IReadOnlyCollection<string> warningCodes)
    {
        if (string.Equals(confidenceLevel, "insufficient_data", StringComparison.OrdinalIgnoreCase))
        {
            return "Nedovoljna";
        }

        if (string.Equals(row.DataQualityStatus, "critical", StringComparison.OrdinalIgnoreCase))
        {
            return "Ograničena";
        }

        if (warningCodes.Contains("missing_cost", StringComparer.OrdinalIgnoreCase)
            || warningCodes.Contains("missing_supplier", StringComparer.OrdinalIgnoreCase)
            || warningCodes.Contains("insufficient_history", StringComparer.OrdinalIgnoreCase))
        {
            return "Delimična";
        }

        if (confidenceScore.HasValue && confidenceScore.Value >= 80 && HasStrongProductDecisionEvidence(row))
        {
            return "Široka";
        }

        if (confidenceScore.HasValue && confidenceScore.Value >= 60)
        {
            return "Dovoljna";
        }

        return "Ograničena";
    }

    private static string ResolveProductDecisionConfidenceCoverageDetail(
        ProductDecisionCenterRowDto row,
        string confidenceLevel,
        IReadOnlyCollection<string> warningCodes)
    {
        if (string.Equals(confidenceLevel, "insufficient_data", StringComparison.OrdinalIgnoreCase))
        {
            return "Obavezni signali nisu kompletni.";
        }

        if (HasStrongProductDecisionEvidence(row))
        {
            return "Više nezavisnih signala je prisutno: prodaja, marža, zaliha i trend.";
        }

        if (warningCodes.Contains("missing_cost", StringComparer.OrdinalIgnoreCase)
            || warningCodes.Contains("missing_supplier", StringComparer.OrdinalIgnoreCase)
            || warningCodes.Contains("insufficient_history", StringComparer.OrdinalIgnoreCase))
        {
            return "Upozorenja i nedostajući signali smanjuju pokrivenost.";
        }

        return "Kombinacija signala je ograničena.";
    }

    private static string ResolveProductDecisionDataQualityDetail(
        ProductDecisionCenterRowDto row,
        IReadOnlyCollection<string> warningCodes)
    {
        if (warningCodes.Contains("data_quality_critical", StringComparer.OrdinalIgnoreCase))
        {
            return "Kritični kvalitet podataka spušta pouzdanost.";
        }

        if (warningCodes.Contains("insufficient_data", StringComparer.OrdinalIgnoreCase))
        {
            return "Nedovoljno signala za stabilnu preporuku.";
        }

        return (row.DataQualityStatus ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "good" => "Podaci su konzistentni.",
            "warning" => "Podaci nose upozorenja, ali preporuka ostaje upotrebljiva.",
            "critical" => "Kvalitet podataka je kritičan.",
            "insufficient_data" => "Kvalitet podataka nije dovoljan.",
            _ => "Kvalitet podataka nije eksplicitno klasifikovan."
        };
    }

    private static IReadOnlyList<string> BuildProductDecisionWarningCodes(ProductDecisionCenterRowDto row)
    {
        var warnings = new List<string>();

        void AddWarning(string code)
        {
            if (!warnings.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add(code);
            }
        }

        if (row.ReasonCodes.Any(code =>
                string.Equals(code, "missing_cost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code, "missing_supplier", StringComparison.OrdinalIgnoreCase)
                || string.Equals(code, "insufficient_history", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var code in row.ReasonCodes.Where(code =>
                         string.Equals(code, "missing_cost", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(code, "missing_supplier", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(code, "insufficient_history", StringComparison.OrdinalIgnoreCase)))
            {
                AddWarning(code);
            }
        }

        if (string.Equals(row.DataQualityStatus, "critical", StringComparison.OrdinalIgnoreCase))
        {
            AddWarning("data_quality_critical");
        }

        if (IsProductDecisionInsufficientData(row))
        {
            AddWarning("insufficient_data");
        }

        if (ResolveProductDecisionExpectedImpact(row) is null)
        {
            AddWarning("expected_impact_denominator_missing");
        }

        return warnings;
    }

    private static IReadOnlyList<string> BuildProductDecisionPrimaryDrivers(
        ProductDecisionCenterRowDto row,
        IReadOnlyCollection<string> warningCodes)
    {
        var drivers = new List<string>();

        void AddDriver(string code)
        {
            if (!drivers.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                drivers.Add(code);
            }
        }

        if (row.VelocityUnitsPerDay > 0.5m || row.UnitsSold >= 20)
        {
            AddDriver("sales_velocity");
        }

        if (row.MarginPct.HasValue || row.MarginContribution > 0m)
        {
            AddDriver("margin");
        }

        if (row.StockGap > 0
            || row.CurrentStock <= row.MinStock
            || string.Equals(row.StockCoverStatus, "low_cover", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.StockCoverStatus, "out_of_stock_risk", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.StockCoverStatus, "slow_stock", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.StockCoverStatus, "no_velocity", StringComparison.OrdinalIgnoreCase))
        {
            AddDriver("stock_risk");
        }

        if (row.TrendPct.HasValue)
        {
            AddDriver("trend");
        }

        if (warningCodes.Contains("missing_cost", StringComparer.OrdinalIgnoreCase))
        {
            AddDriver("missing_cost");
        }

        if (warningCodes.Contains("insufficient_history", StringComparer.OrdinalIgnoreCase)
            || row.UnitsSold < 8
            || !row.DaysSinceLastSale.HasValue)
        {
            AddDriver("sparse_sales");
        }

        return drivers;
    }

    private static decimal? ResolveProductDecisionExpectedImpact(ProductDecisionCenterRowDto row)
    {
        if (string.Equals(row.RecommendationStatus, "REPLENISH", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.RecommendationStatus, "BOOST", StringComparison.OrdinalIgnoreCase))
        {
            return row.LostSalesEstimate > 0m ? row.LostSalesEstimate : null;
        }

        if (string.Equals(row.RecommendationStatus, "MARKDOWN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.RecommendationStatus, "DO_NOT_ORDER", StringComparison.OrdinalIgnoreCase))
        {
            return row.SlowStockCapital > 0m ? row.SlowStockCapital : null;
        }

        return null;
    }

    private static int? ResolveProductDecisionImpactWindowDays(string recommendationStatus) =>
        recommendationStatus switch
        {
            "REPLENISH" => 14,
            "BOOST" => 14,
            "MARKDOWN" => 30,
            "DO_NOT_ORDER" => 30,
            "FIX_DATA" => 7,
            _ => null
        };

    private static string BuildProductDecisionRiskIfIgnored(string recommendationStatus) =>
        recommendationStatus switch
        {
            "REPLENISH" => "Rizik je izgubljena prodaja i pad dostupnosti na polici.",
            "BOOST" => "Rizik je da se dobar signal ne iskoristi za dodatni prihod.",
            "MARKDOWN" => "Rizik je da kapital ostane zarobljen u sporoj robi.",
            "DO_NOT_ORDER" => "Rizik je dodatni lager i sporiji obrt zalihe.",
            "FIX_DATA" => "Rizik je da sve naredne odluke ostanu zasnovane na nepouzdanim podacima.",
            _ => "Rizik je da se odluka odloži bez dovoljno jakog signala."
        };

    private static string ResolveProductDecisionInputFreshnessStatus(
        ProductDecisionCenterRowDto row,
        string confidenceLevel)
    {
        if (string.Equals(confidenceLevel, "insufficient_data", StringComparison.OrdinalIgnoreCase))
        {
            return "critical";
        }

        if (row.DaysSinceLastSale is null)
        {
            return "unknown";
        }

        if (row.DaysSinceLastSale.Value > 60)
        {
            return "stale";
        }

        return "fresh";
    }

    private static string BuildRecommendationReason(
        string recommendationStatus,
        decimal revenue,
        int unitsSold,
        decimal velocityUnitsPerDay,
        decimal? marginPct,
        decimal? trendPct,
        int stockGap,
        int currentStock,
        int minStock,
        int? daysSinceLastSale,
        string dataQualityStatus)
    {
        var trendText = trendPct.HasValue ? $"{trendPct.Value:0.0}%" : "N/A";
        var marginText = marginPct.HasValue ? $"{marginPct.Value:0.0}%" : "N/A";
        var staleText = daysSinceLastSale.HasValue ? $"{daysSinceLastSale.Value} dana" : "N/A";

        return recommendationStatus switch
        {
            "BOOST" => $"Trend {trendText}, marža {marginText}, velocity {velocityUnitsPerDay:0.00}/dan i gap zalihe {stockGap}.",
            "REPLENISH" => $"Brza rotacija ({velocityUnitsPerDay:0.00}/dan) uz manjak zalihe ({currentStock}/{minStock}).",
            "MARKDOWN" => $"Spora prodaja ({velocityUnitsPerDay:0.00}/dan), trend {trendText} i starost bez prodaje {staleText}.",
            "DO_NOT_ORDER" => $"Visoka zaliha ({currentStock}), slab trend {trendText} i marža {marginText}.",
            "FIX_DATA" => $"Kritični problemi kvaliteta podataka ({dataQualityStatus}) blokiraju pouzdanu preporuku.",
            "INSUFFICIENT_DATA" => $"Nedovoljno signala: promet {revenue:0.##} RSD, komadi {unitsSold}, poslednja prodaja {staleText}.",
            _ => $"Stabilan signal bez hitne akcije. Trend {trendText}, marža {marginText}, velocity {velocityUnitsPerDay:0.00}/dan."
        };
    }

    private static int RecommendationPriority(string status) => status switch
    {
        "FIX_DATA" => 7,
        "BOOST" => 6,
        "REPLENISH" => 5,
        "MARKDOWN" => 4,
        "DO_NOT_ORDER" => 3,
        "WATCH" => 2,
        _ => 1
    };

    private static string RecommendationLabel(string status) => status switch
    {
        "BOOST" => "Pojačaj",
        "REPLENISH" => "Dopuni",
        "WATCH" => "Prati",
        "MARKDOWN" => "Snizi cenu",
        "DO_NOT_ORDER" => "Ne naručuj",
        "FIX_DATA" => "Proveri podatke",
        _ => "Nedovoljno podataka"
    };

    private static string RecommendedAction(string status) => status switch
    {
        "BOOST" => "Povećaj vidljivost artikla i planiraj brzu dopunu.",
        "REPLENISH" => "Aktiviraj dopunu prema minimalnoj zalihi.",
        "WATCH" => "Nastavi praćenje bez hitne intervencije.",
        "MARKDOWN" => "Pokreni ciljano sniženje i testiraj elastičnost cene.",
        "DO_NOT_ORDER" => "Zaustavi novu narudžbinu dok se signal ne oporavi.",
        "FIX_DATA" => "Ispravi dobavljača, nabavnu cenu ili kategoriju pre odluke.",
        _ => "Sačekaj dodatne podatke pre poslovne odluke."
    };

    private static string InputFreshnessLabel(string status) => status switch
    {
        "fresh" => "Sveže",
        "stale" => "Zastarelo",
        "critical" => "Kritično",
        _ => "Nepoznato"
    };

    private static string NormalizeDataScope(string? dataScope)
    {
        var normalized = (dataScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "all" or "imported" or "existing" ? normalized : "all";
    }

    private static async Task<Dictionary<int, InventorySignalWindowStats>> LoadInventorySignalWindowStatsFromJournalAsync(
        ITrendplusDbContext db,
        IReadOnlyCollection<int> articleIds,
        int? storeId,
        DateTime fromUtc,
        DateTime toExclusiveUtc,
        string? dataScope,
        CancellationToken ct)
    {
        var stats = new Dictionary<int, InventorySignalWindowStats>();
        if (articleIds.Count == 0)
        {
            return stats;
        }

        var normalizedDataScope = NormalizeDataScope(dataScope);

        foreach (var batch in articleIds.Chunk(MovementStatsBatchSize))
        {
            var movementQuery = db.DnevnikPromena
                .AsNoTracking()
                .Where(x => x.ArtikalId.HasValue
                    && batch.Contains(x.ArtikalId.Value)
                    && x.Datum >= fromUtc
                    && x.Datum < toExclusiveUtc
                    && (!storeId.HasValue || x.IDObjekat == storeId.Value));

            movementQuery = normalizedDataScope switch
            {
                "imported" => movementQuery.Where(x => x.DataOrigin == "access"),
                "existing" => movementQuery.Where(x => x.DataOrigin == "existing" || x.DataOrigin == null || x.DataOrigin == ""),
                _ => movementQuery
            };

            var movementRows = await movementQuery
                .Select(x => new
                {
                    ArtikalId = x.ArtikalId!.Value,
                    Quantity = x.Kolicina ?? 0,
                    x.TipPromene,
                })
                .ToListAsync(ct);

            foreach (var movement in movementRows)
            {
                stats.TryGetValue(movement.ArtikalId, out var current);
                var netMovement = current.NetMovementUnits + movement.Quantity;
                var inboundUnits = current.InboundUnits;

                if (!string.IsNullOrWhiteSpace(movement.TipPromene)
                    && TipPromeneConstants.UlazTypes.Contains(movement.TipPromene, StringComparer.OrdinalIgnoreCase))
                {
                    inboundUnits += Math.Max(movement.Quantity, 0);
                }

                stats[movement.ArtikalId] = new InventorySignalWindowStats(netMovement, inboundUnits);
            }
        }

        return stats;
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

    private static string GetErrorMessage(Exception ex, string fallbackMessage)
    {
        if (ex is OperationCanceledException or TaskCanceledException or TimeoutException)
        {
            return fallbackMessage;
        }

        var message = ex.Message?.Trim();
        return string.IsNullOrWhiteSpace(message) ? fallbackMessage : $"{fallbackMessage} ({message})";
    }

    private static bool IsMissingRelation(Exception ex) =>
        ex is PostgresException pg && pg.SqlState == "42P01"
        || ex.InnerException is PostgresException innerPg && innerPg.SqlState == "42P01";

    internal sealed record ProductDecisionConfidenceProfile(
        string RecommendationId,
        string SourceType,
        string SourceKey,
        string RecommendationType,
        string ConfidenceLevel,
        int? ConfidenceScore,
        IReadOnlyList<string> PrimaryDrivers,
        IReadOnlyList<string> WarningCodes,
        decimal? ExpectedImpactRsd,
        int? ImpactWindowDays,
        string RiskIfIgnored,
        string ExplainabilityText,
        string InputFreshnessStatus,
        IReadOnlyList<ProductDecisionEvidenceNodeDto> ConfidenceBreakdown,
        IReadOnlyList<ProductDecisionAlternativeRecommendationDto> AlternativeRecommendations,
        IReadOnlyList<ProductDecisionEvidenceNodeDto> EvidenceChain,
        ProductDecisionWhyPanelDto WhyPanel);

    private sealed record CacheReadResult<T>(T Value, bool CacheHit, AnalyticsCacheEntryMetadata Metadata) where T : class;
    private readonly record struct InventorySignalWindowStats(int NetMovementUnits, int InboundUnits);
}

// DTOs za cache (moraju biti klase za JSON serijalizaciju)
public class DailySaleDto
{
    public string Date { get; set; } = "";
    public decimal TotalRevenue { get; set; }
    public int TransactionCount { get; set; }
    public int TotalUnits { get; set; }
}

public class DailySalesCachedSnapshot
{
    public List<DailySaleDto> Items { get; set; } = [];
    public bool UsedOperationalFallback { get; set; }
}

public class CategoryDataDto
{
    public string Kategorija { get; set; } = "";
    public string Pol { get; set; } = "";
    public decimal TotalRevenue { get; set; }
    public int TotalUnits { get; set; }
    public int TransactionCount { get; set; }
}

public class GenderDataDto
{
    public string Pol { get; set; } = "";
    public decimal TotalRevenue { get; set; }
    public int TotalUnits { get; set; }
}

public class SupplierDataDto
{
    public int? DobavljacId { get; set; }
    public string DobavljacNaziv { get; set; } = "";
    public decimal TotalRevenue { get; set; }
    public int TotalUnits { get; set; }
    public int TransactionCount { get; set; }
}

public class PaymentDataDto
{
    public string NacinPlacanja { get; set; } = "";
    public decimal TotalRevenue { get; set; }
    public int TransactionCount { get; set; }
}

public class WeekdayDataDto
{
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = "";
    public decimal TotalRevenue { get; set; }
    public int TransactionCount { get; set; }
}

public class HourDataDto
{
    public int Hour { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TransactionCount { get; set; }
}

public class QuickInsightsDto
{
    public string? BestDay { get; set; }
    public decimal BestDayRevenue { get; set; }
    public string? TopProduct { get; set; }
    public int LowStockAlert { get; set; }
}

public class TransactionStatsDto
{
    /// <summary>Average sale lines (prodajne stavke) per receipt. Not sold units.</summary>
    public decimal AvgItemsPerTransaction { get; set; }

    /// <summary>Average sold units (sum of line quantities) per receipt.</summary>
    public decimal AvgUnitsPerTransaction { get; set; }

    public decimal AvgTransactionValue { get; set; }
    public int TotalTransactions { get; set; }
}

public class StoreFilterOptionDto
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = "";
    public string? City { get; set; }
    public string? Region { get; set; }
}

public class SupplierFilterOptionDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
}

public class TopProductAdvancedItemDto
{
    public int ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal Revenue { get; set; }
    public int Units { get; set; }
    public decimal VelocityUnitsPerDay { get; set; }
    public decimal? MarginImpact { get; set; }
    public string StockStatus { get; set; } = "neutral";
    public decimal? TrendPct { get; set; }
    public string? MarginQualityLabel { get; set; }
    public string? MarginQualityTier { get; set; }
    public string? MarginQualityShortLabel { get; set; }
    public string? MarginQualityTooltip { get; set; }
    public string? DataQualityStatus { get; set; }
    public string? StatusReason { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
}

public class TopProductsAdvancedResultDto
{
    public List<TopProductAdvancedItemDto> ByRevenue { get; set; } = [];
    public List<TopProductAdvancedItemDto> ByUnits { get; set; } = [];
    public List<TopProductAdvancedItemDto> ByVelocity { get; set; } = [];
    public List<TopProductAdvancedItemDto> ByMarginImpact { get; set; } = [];
    public bool MarginAvailable { get; set; }
    public string? MarginMessage { get; set; }
}

public static class ProductDecisionDenominatorScope
{
    public const string ReturnedRows = "returned_rows";
    public const string AnalyzedRows = "analyzed_rows";
    public const string HiddenByTopLimit = "hidden_by_top_limit";
}

public sealed record ProductDecisionCenterRowWindow(
    int TotalRows,
    int AnalyzedRows,
    int IgnoredRowsCount,
    string IgnoredRowsMeaning);

public class ProductDecisionCenterSummaryDto
{
    public int ReplenishCount { get; set; }
    public int MarkdownCount { get; set; }
    public int HighPotentialCount { get; set; }
    public int BadDataCount { get; set; }
    public decimal LostSalesEstimate { get; set; }
    public decimal SlowStockCapital { get; set; }
    /// <summary>Denominator for count KPIs. Current contract: <see cref="ProductDecisionDenominatorScope.ReturnedRows"/>.</summary>
    public string CountDenominatorScope { get; set; } = ProductDecisionDenominatorScope.ReturnedRows;
    /// <summary>Denominator for money totals. Current contract: <see cref="ProductDecisionDenominatorScope.AnalyzedRows"/>.</summary>
    public string MoneyDenominatorScope { get; set; } = ProductDecisionDenominatorScope.AnalyzedRows;
}

public class ProductDecisionCenterRowDto
{
    public int ProductId { get; set; }
    public string RecommendationId { get; set; } = string.Empty;
    public string SourceType { get; set; } = "product";
    public string SourceKey { get; set; } = string.Empty;
    public string RecommendationType { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? Category { get; set; }
    public string? TipObuce { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public decimal Revenue { get; set; }
    public int UnitsSold { get; set; }
    public decimal VelocityUnitsPerDay { get; set; }
    public decimal MarginContribution { get; set; }
    public decimal? MarginPct { get; set; }
    public string MarginQualityLabel { get; set; } = string.Empty;
    public decimal MarginCoveragePct { get; set; }
    public int CurrentStock { get; set; }
    public int MinStock { get; set; }
    public int StockGap { get; set; }
    public int? DaysSinceLastSale { get; set; }
    public decimal? TrendPct { get; set; }
    public decimal LostSalesEstimate { get; set; }
    public decimal SlowStockCapital { get; set; }
    public decimal? StockCoverDays { get; set; }
    public string StockCoverStatus { get; set; } = InventorySignalCalculator.StockCoverInsufficientData;
    public string StockCoverStatusLabel { get; set; } = InventorySignalCalculator.StockCoverStatusLabel(InventorySignalCalculator.StockCoverInsufficientData);
    public decimal? SellThroughRatio { get; set; }
    public string SellThroughStatus { get; set; } = InventorySignalCalculator.SellThroughInsufficientData;
    public string SellThroughStatusLabel { get; set; } = InventorySignalCalculator.SellThroughStatusLabel(InventorySignalCalculator.SellThroughInsufficientData);
    public decimal SignalConfidencePct { get; set; }
    public bool RecommendationAllowed { get; set; }
    public string DataQualityStatus { get; set; } = "warning";
    public string ConfidenceLevel { get; set; } = "insufficient_data";
    public int? ConfidenceScore { get; set; }
    public int ConfidencePct { get; set; }
    public int ReliabilityPct { get; set; }
    public string RecommendationStatus { get; set; } = "INSUFFICIENT_DATA";
    public string RecommendationLabel { get; set; } = "Nedovoljno podataka";
    public string RecommendationReason { get; set; } = string.Empty;
    public List<string> ReasonCodes { get; set; } = [];
    public List<string> WarningCodes { get; set; } = [];
    public List<string> PrimaryDrivers { get; set; } = [];
    public decimal? ExpectedImpactRsd { get; set; }
    public int? ImpactWindowDays { get; set; }
    public string RiskIfIgnored { get; set; } = string.Empty;
    public string ExplainabilityText { get; set; } = string.Empty;
    public string InputFreshnessStatus { get; set; } = "unknown";
    public List<ProductDecisionEvidenceNodeDto> ConfidenceBreakdown { get; set; } = [];
    public List<ProductDecisionAlternativeRecommendationDto> AlternativeRecommendations { get; set; } = [];
    public List<ProductDecisionEvidenceNodeDto> EvidenceChain { get; set; } = [];
    public ProductDecisionWhyPanelDto WhyPanel { get; set; } = new();
    public string RecommendedAction { get; set; } = string.Empty;
    /// <summary>RL04 lifecycle tip state for this issued recommendation instance.</summary>
    public string LifecycleState { get; set; } = RecommendationLifecycleSemantics.LifecycleStates.Issued;
    /// <summary>RL04 outcome evidence axis: pending | measured | not_measured.</summary>
    public string OutcomeEvidenceState { get; set; } = RecommendationLifecycleSemantics.OutcomeEvidenceStates.Pending;
    /// <summary>True only when measured evidence may feed later learning statistics.</summary>
    public bool LearningEligible { get; set; }
    public List<string> LearningEligibilityReasonCodes { get; set; } = [];
    public RecommendationLifecycleCaptureDto RecommendationLifecycle { get; set; }
        = RecommendationLifecycleSemantics.ProjectIssuedRecommendation();
    /// <summary>DEX10: live recommendations are absent until acted on and frozen into the action ledger.</summary>
    public string EvidenceSnapshotStatus { get; set; } = "absent";
    public ProductDecisionEvidenceSnapshotPreviewDto? EvidenceSnapshotPreview { get; set; }
}

public class ProductDecisionEvidenceSnapshotPreviewDto
{
    public int SchemaVersion { get; set; } = 1;
    public string RecommendationId { get; set; } = string.Empty;
    public string RecommendationType { get; set; } = string.Empty;
    public string? PeriodFromUtc { get; set; }
    public string? PeriodToUtc { get; set; }
    public string DataQualityStatus { get; set; } = "insufficient_data";
    public string ConfidenceLevel { get; set; } = "insufficient_data";
    public int? ConfidenceScore { get; set; }
    public int ConfidencePct { get; set; }
    public int ReliabilityPct { get; set; }
    public string InputFreshnessStatus { get; set; } = "unknown";
    public string ExplainabilityText { get; set; } = string.Empty;
    public List<string> ReasonCodes { get; set; } = [];
    public List<string> WarningCodes { get; set; } = [];
    public List<string> PrimaryDrivers { get; set; } = [];
    public List<ProductDecisionEvidenceNodeDto> EvidenceChain { get; set; } = [];
    public List<ProductDecisionEvidenceNodeDto> ConfidenceBreakdown { get; set; } = [];
}

public class ProductDecisionEvidenceNodeDto
{
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ValueText { get; set; } = string.Empty;
    public List<string> SourceFields { get; set; } = [];
    public bool IsMissing { get; set; }
    public string? Detail { get; set; }
}

public class ProductDecisionAlternativeRecommendationDto
{
    public int Rank { get; set; }
    public string RecommendationStatus { get; set; } = "WATCH";
    public string RecommendationLabel { get; set; } = "Prati";
    public string RecommendedAction { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<string> ReasonCodes { get; set; } = [];
    public string ConfidenceLevel { get; set; } = "low";
    public int ConfidenceScore { get; set; }
    public int ReliabilityPct { get; set; }
    public string DataQualityStatus { get; set; } = "insufficient_data";
    public string WhyLowerRanked { get; set; } = string.Empty;
}

public class ProductDecisionWhyPanelDto
{
    public string RecommendationStatus { get; set; } = string.Empty;
    public string RecommendationLabel { get; set; } = string.Empty;
    public string RecommendationReason { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public string ExplainabilityText { get; set; } = string.Empty;
    public string SummarySource { get; set; } = "missing";
    public bool SummaryFallbackUsed { get; set; }
    public string? SummaryFallbackReason { get; set; }
    public List<string> ReasonCodes { get; set; } = [];
    public List<string> PrimaryDrivers { get; set; } = [];
    public List<string> WarningCodes { get; set; } = [];
    public string ConfidenceLevel { get; set; } = "insufficient_data";
    public int? ConfidenceScore { get; set; }
    public int ConfidencePct { get; set; }
    public int ReliabilityPct { get; set; }
    public string DataQualityStatus { get; set; } = "warning";
    public string InputFreshnessStatus { get; set; } = "unknown";
    public bool RecommendationAllowed { get; set; }
    public decimal? ExpectedImpactRsd { get; set; }
    public int? ImpactWindowDays { get; set; }
    public string RiskIfIgnored { get; set; } = string.Empty;
    public List<ProductDecisionEvidenceNodeDto> ConfidenceBreakdown { get; set; } = [];
    public List<ProductDecisionAlternativeRecommendationDto> AlternativeRecommendations { get; set; } = [];
    public List<ProductDecisionEvidenceNodeDto> EvidenceChain { get; set; } = [];
    public List<ProductDecisionDecisionTreeNodeDto> DecisionTree { get; set; } = [];
    public string LifecycleState { get; set; } = RecommendationLifecycleSemantics.LifecycleStates.Issued;
    public string OutcomeEvidenceState { get; set; } = RecommendationLifecycleSemantics.OutcomeEvidenceStates.Pending;
    public bool LearningEligible { get; set; }
    public List<string> LearningEligibilityReasonCodes { get; set; } = [];
}

public class ProductDecisionDecisionTreeNodeDto
{
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ValueText { get; set; } = string.Empty;
    public List<string> SourceFields { get; set; } = [];
    public bool IsSelected { get; set; }
    public string? Detail { get; set; }
}

public class ProductDecisionCenterResponseDto
{
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime PeriodFromUtc { get; set; }
    public DateTime PeriodToUtc { get; set; }
    public string RequestedDataScope { get; set; } = "all";
    public string ScopeAuthority { get; set; } = "both";
    public string ScopeBreakdown { get; set; } = "article_origin=Artikli.DataOrigin;sale_origin=ProdajaZaglavlje.DataOrigin";
    /// <summary>Returned/top row count (same as <see cref="Rows"/>.Count).</summary>
    public int TotalRows { get; set; }
    /// <summary>All analyzed product rows before top limiting.</summary>
    public int AnalyzedRows { get; set; }
    /// <summary>Rows hidden by top limit. Not a bad-data count.</summary>
    public int IgnoredRowsCount { get; set; }
    /// <summary>Semantic meaning of <see cref="IgnoredRowsCount"/>. Current contract: <see cref="ProductDecisionDenominatorScope.HiddenByTopLimit"/>.</summary>
    public string IgnoredRowsMeaning { get; set; } = ProductDecisionDenominatorScope.HiddenByTopLimit;
    public ProductDecisionCenterSummaryDto Summary { get; set; } = new();
    public List<ProductDecisionCenterRowDto> Rows { get; set; } = [];
    public AnalyticsResponseMetaDto Meta { get; set; } = new()
    {
        Success = true,
        GeneratedAtUtc = DateTime.UtcNow
    };
}

public class ProductDecisionTimelineFilterResponseDto
{
    public DecisionTimelineFilterScopeDto? Scope { get; set; }
    public string? EmptyReason { get; set; }
    public List<DecisionTimelineItemDto> Timelines { get; set; } = [];
    public int MatchedActionCount { get; set; }
    public int MatchedEventCount { get; set; }
    public List<string> WarningCodes { get; set; } = [];
    public AnalyticsResponseMetaDto Meta { get; set; } = new()
    {
        Success = true,
        GeneratedAtUtc = DateTime.UtcNow
    };
}

public class ProductDecisionTimelineExportResponseDto
{
    public bool Success { get; set; }
    public DecisionTimelineExportHonestyHeaderDto Header { get; set; } = null!;
    public DecisionTimelineExportFunnelDto? Funnel { get; set; }
    public List<DecisionTimelineExportRowDto> Rows { get; set; } = [];
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public AnalyticsResponseMetaDto Meta { get; set; } = new()
    {
        Success = true,
        GeneratedAtUtc = DateTime.UtcNow
    };
}

public class DashboardMetricCardDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal Value { get; set; }
    public string Unit { get; set; } = "";
    public decimal? TrendPct { get; set; }
    public string Status { get; set; } = "neutral";
    public string Subtitle { get; set; } = "";
}

public class DashboardInsightDto
{
    public string Badge { get; set; } = "";
    public string Description { get; set; } = "";
    public string Color { get; set; } = "blue";
}

public class DashboardActionDto
{
    public string Priority { get; set; } = "P3";
    public string Title { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public string? StatusReason { get; set; }
    public int? ConfidencePct { get; set; }
    public int? ReliabilityPct { get; set; }
    public bool? RecommendationAllowed { get; set; }
    public string? DataQualityStatus { get; set; }
}

public class DashboardDecisionActionDto
{
    public string ActionKey { get; set; } = "";
    public string SourceType { get; set; } = "dashboard";
    public string Priority { get; set; } = "P3";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Reason { get; set; } = "";
    public string? StatusReason { get; set; }
    public string? RecommendationStatus { get; set; }
    public string? ExpectedImpact { get; set; }
    public decimal? ImpactEstimateRsd { get; set; }
    public int? ConfidencePct { get; set; }
    public int? ReliabilityPct { get; set; }
    public bool RecommendationAllowed { get; set; } = true;
    public string DataQualityStatus { get; set; } = "insufficient_data";
    public string ActionUrl { get; set; } = "/analytics";
    public Dictionary<string, object?> Metadata { get; set; } = new();
    public string Link { get; set; } = "/analytics";
    public string? LinkLabel { get; set; }
}

public class ExecutiveTopSupplierDto
{
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal MarginContribution { get; set; }
    public string Link { get; set; } = "/analytics/supplier";
}

public class ExecutiveTopMarginItemDto
{
    public string Key { get; set; } = string.Empty; // productId or category name
    public string Label { get; set; } = string.Empty;
    public string ItemType { get; set; } = "product"; // product|category
    public int? ProductId { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal Revenue { get; set; }
    public decimal MarginContribution { get; set; }
    public decimal? MarginPct { get; set; }
    public string DataQualityStatus { get; set; } = "insufficient_data";
    public int? ConfidencePct { get; set; }
    public string Link { get; set; } = "/analytics";
}

public class ExecutiveNegativeSignalDto
{
    public string SignalType { get; set; } = "unknown";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "P3";
    public decimal? ImpactEstimateRsd { get; set; }
    public int? ConfidencePct { get; set; }
    public string DataQualityStatus { get; set; } = "insufficient_data";
    public string? RecommendationStatus { get; set; }
    public string? RecommendationReason { get; set; }
    public int? ProductId { get; set; }
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public string? SupplierName { get; set; }
    public string Link { get; set; } = "/analytics";
}

public class ExecutiveDataQualitySummaryDto
{
    public int MissingSupplierCount { get; set; }
    public int MissingCostCount { get; set; }
    public int InsufficientSignalCount { get; set; }
    public int IgnoredRowsCount { get; set; }
    public int ZeroRevenueRowsCount { get; set; }
    public string FreshnessStatus { get; set; } = "unknown";
}

public class ExecutiveDashboardSnapshotDto
{
    public decimal TotalMarginContributionRsd { get; set; }
    public decimal InventoryDangerValueRsd { get; set; }
    public List<ExecutiveTopSupplierDto> TopSuppliers { get; set; } = [];
    public List<ExecutiveTopMarginItemDto> TopMarginProducts { get; set; } = [];
    public List<ExecutiveTopMarginItemDto> TopMarginCategories { get; set; } = [];
    public List<ExecutiveNegativeSignalDto> NegativeSignals { get; set; } = [];
    public ExecutiveDataQualitySummaryDto DataQualitySummary { get; set; } = new();
}

public class DashboardValidationDto
{
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = "";
}

public static class LostSalesSourceStatus
{
    public const string View = "view";
    public const string Fallback = "fallback";
    public const string Unavailable = "unavailable";
    public const string TrueZero = "true_zero";
}

public sealed record LostSalesSnapshot(
    int OosSkuCount,
    decimal? LostSalesEstimate,
    string SourceStatus)
{
    public static LostSalesSnapshot Unavailable() =>
        new(0, null, LostSalesSourceStatus.Unavailable);

    public static LostSalesSnapshot TrueZero(int oosSkuCount) =>
        new(oosSkuCount, 0m, LostSalesSourceStatus.TrueZero);

    public static LostSalesSnapshot FromView(int oosSkuCount, decimal lostSalesEstimate) =>
        new(oosSkuCount, lostSalesEstimate, LostSalesSourceStatus.View);

    public static LostSalesSnapshot FromFallback(int oosSkuCount, decimal lostSalesEstimate) =>
        new(oosSkuCount, lostSalesEstimate, LostSalesSourceStatus.Fallback);
}

public class DashboardValidationEndpointDto
{
    public string Status { get; set; } = "info";
    public string Message { get; set; } = "";
    public decimal? Score { get; set; }
    public int? TotalSku { get; set; }
    public int? AffectedSku { get; set; }
    public DateTime? LastImport { get; set; }
    public decimal? FreshnessHours { get; set; }
    public decimal? LostSalesEstimate { get; set; }
    /// <summary>Lost-sales evidence source: view | fallback | unavailable | true_zero.</summary>
    public string? SourceStatus { get; set; }
    public int? NegativeQtyCount { get; set; }
    public int? TotalRows { get; set; }
}

public class DashboardAdvancedSnapshotDto
{
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public List<DashboardMetricCardDto> Cards { get; set; } = [];
    public List<DashboardInsightDto> Insights { get; set; } = [];
    public List<DashboardActionDto> Actions { get; set; } = [];
    public List<DashboardValidationDto> Validations { get; set; } = [];
}

public class AnalyticsDashboardBootstrapDto
{
    public SalesSummaryDto? Summary { get; set; }
    public InventoryStatusDto? Inventory { get; set; }
    public List<DailySaleDto> DailySales { get; set; } = [];
    public List<CategoryDataDto> CategoryData { get; set; } = [];
    public List<GenderDataDto> GenderData { get; set; } = [];
    public List<SupplierDataDto> SupplierData { get; set; } = [];
    public List<SupplierFilterOptionDto> SupplierOptions { get; set; } = [];
    public List<PaymentDataDto> PaymentData { get; set; } = [];
    public List<WeekdayDataDto> WeekdayData { get; set; } = [];
    public List<HourDataDto> HourData { get; set; } = [];
    public QuickInsightsDto? QuickInsights { get; set; }
    public TransactionStatsDto? TransactionStats { get; set; }
    public DashboardAdvancedSnapshotDto? Advanced { get; set; }
    public TopProductsAdvancedResultDto? TopAdvanced { get; set; }
    public DashboardValidationEndpointDto? ValidationCompleteness { get; set; }
    public DashboardValidationEndpointDto? ValidationFreshness { get; set; }
    public DashboardValidationEndpointDto? ValidationLostSales { get; set; }
    public List<DashboardDecisionActionDto> DecisionActions { get; set; } = [];
    public ExecutiveDashboardSnapshotDto? Executive { get; set; }
    public List<string> Errors { get; set; } = [];
    public AnalyticsResponseMetaDto Meta { get; set; } = new()
    {
        Success = true,
        GeneratedAtUtc = DateTime.UtcNow
    };
}

