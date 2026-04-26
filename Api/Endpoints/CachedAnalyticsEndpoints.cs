using Application.Analytics.Queries.GetInventoryStatus;
using Application.Analytics.Queries.GetSalesSummary;
using Application.Analytics.Queries.GetInventoryForecast;
using Application.Analytics.Queries.GetInventoryAlerts;
using Application.Analytics.Queries.GetInventorySizeCurve;
using Application.Analytics.Queries.GetRebalanceSuggestions;
using Application.Analytics.Queries.GetTopProducts;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Infrastructure.Services.Caching;
using MediatR;
using Trendplus2.Dtos;
using Microsoft.EntityFrameworkCore;
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

            var cacheKey = AnalyticsCacheKeys.SalesSummary(fromDate, toDate, storeId, supplierId);

            var result = await cache.GetOrSetAsync(
                cacheKey,
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
                CacheExpiration.Medium,
                ct);

            return Results.Ok(result);
        });

        // ========== TOP PRODUCTS (CACHED) ==========
        group.MapGet("/sales/top-products", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext trendDb,
            IMediator mediator,
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

            var cacheKey = AnalyticsCacheKeys.TopProducts(top, fromDate, toDate, storeId, supplierId);

            var result = await cache.GetOrSetAsync(
                cacheKey,
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

                    var baseQuery = from ps in trendDb.ProdajaStavke.AsNoTracking()
                                    join p in trendDb.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals p.Id
                                    join a in trendDb.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                                    where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                          (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                                          (!storeId.HasValue || p.IDObjekat == storeId.Value) &&
                                          (!supplierId.HasValue || a.IDDobavljac == supplierId.Value)
                                    group new { ps, a } by new { ps.IdArtikal, a.Naziv, a.Velicina, a.Boja } into g
                                    orderby g.Sum(x => x.ps.Kolicina * x.ps.Cena) descending
                                    select new TopProductDto(
                                        g.Key.IdArtikal,
                                        g.Key.Naziv,
                                        g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                                        g.Sum(x => x.ps.Kolicina),
                                        g.Key.Velicina,
                                        g.Key.Boja
                                    );

                    var topRevenue = await baseQuery
                        .OrderByDescending(x => x.TotalRevenue)
                        .Take(top)
                        .ToListAsync(ct);

                    var topUnits = await baseQuery
                        .OrderByDescending(x => x.TotalUnits)
                        .Take(top)
                        .ToListAsync(ct);

                    return new TopProductsResult(topRevenue, topUnits);
                },
                CacheExpiration.Medium,
                ct);

            return Results.Ok(result);
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
                CacheExpiration.Short,
                ct);

            return Results.Ok(result);
        });

        // ========== INVENTORY STATUS (CACHED) ==========
        group.MapGet("/inventory/status", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext trendDb,
            IMediator mediator,
            int lowStockThreshold = 2,
            CancellationToken ct = default) =>
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
                            inventoryData?.OutOfStock ?? 0
                        );
                    }
                },
                CacheExpiration.Short, // Inventory se brzo menja
                ct);

            return Results.Ok(result);
        });

        // ========== INVENTORY BALANCE (CACHED) ==========
        group.MapGet("/inventory/balance", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            int? storeId = null,
            int? supplierId = null,
            CancellationToken ct = default) =>
        {
            var cacheKey = $"analytics:inventory:balance:{storeId}:{supplierId}";

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
                    var estimatedValue = await query.SumAsync(a => (decimal?)( (a.NabavnaCena ?? 0m) * ((a.Kolicina ?? 0) > 0 ? (a.Kolicina ?? 0) : 0) ), ct) ?? 0m;

                    return new InventoryBalanceDto((int)totalSku, (int)totalOnHand, (int)lowStock, (int)outOfStock, Math.Round(estimatedValue, 2));
                },
                CacheExpiration.Short,
                ct);

            return Results.Ok(result);
        });

        // ========== INVENTORY LIST (CACHED) ==========
        group.MapGet("/inventory/list", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            int page = 1,
            int pageSize = 50,
            int? storeId = null,
            int? supplierId = null,
            string? search = null,
            string? sortBy = null,
            CancellationToken ct = default) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 1000);

            var cacheKey = $"analytics:inventory:list:{page}:{pageSize}:{storeId}:{supplierId}:{search}:{sortBy}";

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
                    var items = await query
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(a => new InventoryListItemDto(
                            a.Id,
                            a.PLU,
                            a.Naziv ?? string.Empty,
                            a.Kolicina,
                            a.MinimalnaKolicina,
                            a.NabavnaCena,
                            (a.NabavnaCena ?? 0m) * ((a.Kolicina ?? 0) > 0 ? (a.Kolicina ?? 0) : 0),
                            a.IDObjekat,
                            a.IDDobavljac
                        ))
                        .ToListAsync(ct);

                    return new ArtikliPagedResponse<InventoryListItemDto>(items, total, page, pageSize);
                },
                CacheExpiration.Short,
                ct);

            return Results.Ok(paged);
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
                CacheExpiration.Short,
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
                CacheExpiration.Medium,
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
                CacheExpiration.Short,
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
                CacheExpiration.Short,
                ct);

            return Results.Ok(result);
        });

        // ========== DAILY SALES (CACHED) ==========
        group.MapGet("/sales/daily", async (
            IAnalyticsCacheService cache,
            IAnalyticsDbContext db,
            ITrendplusDbContext trendDb,
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

            var cacheKey = AnalyticsCacheKeys.DailySales(fromDate, toDate, storeId, supplierId);

            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    if (!storeId.HasValue && !supplierId.HasValue)
                    {
                        var aggregatedDaily = await TryGetDailySalesFromAggregatesAsync(trendDb, fromDate, toDate, ct);
                        if (aggregatedDaily is not null && aggregatedDaily.Count > 0)
                        {
                            return aggregatedDaily;
                        }
                    }

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

                            return dailySalesRaw.Select(x => new DailySaleDto
                            {
                                Date = x.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                TotalRevenue = x.TotalRevenue,
                                TransactionCount = x.TransactionCount,
                                TotalUnits = x.TotalUnits
                            }).ToList();
                        }
                    }
                    catch (Exception ex) when (IsMissingRelation(ex))
                    {
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

                    return fallbackRaw.Select(x => new DailySaleDto
                    {
                        Date = x.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        TotalRevenue = x.TotalRevenue,
                        TransactionCount = x.TransactionCount,
                        TotalUnits = x.TotalUnits
                    }).ToList();
                },
                CacheExpiration.Medium,
                ct);

            return Results.Ok(result);
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
                            ItemCount = g.Count(),
                            TotalValue = g.Sum(x => x.Kolicina * x.Cena)
                        }).ToListAsync(ct);

                    if (perTransaction.Count == 0)
                    {
                        return new TransactionStatsDto();
                    }

                    return new TransactionStatsDto
                    {
                        AvgItemsPerTransaction = Math.Round(perTransaction.Average(x => (decimal)x.ItemCount), 2),
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
                CacheExpiration.Short,
                ct);

            return Results.Ok(result);
        });

        group.MapGet("/dashboard/bootstrap", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            IMediator mediator,
            ILogger<Program> logger,
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

                var cacheKey = AnalyticsCacheKeys.DashboardBootstrap(fromDate, toDate, storeId, supplierId, normalizedDataScope);
                var result = await cache.GetOrSetAsync(
                    cacheKey,
                    async () =>
                    {
                        var response = new AnalyticsDashboardBootstrapDto();

                        response.Summary = await TrySectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.SalesSummary(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildSalesSummarySnapshotAsync(db, mediator, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Medium,
                                ct),
                            response.Errors,
                            "Sazetak prodaje nije dostupan.");

                        response.Inventory = await TrySectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.Inventory(2),
                                async () => await BuildInventoryStatusSnapshotAsync(db, mediator, 2, ct),
                                CacheExpiration.Short,
                                ct),
                            response.Errors,
                            "Status zaliha nije dostupan.");

                        response.DailySales = await TryListSectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.DailySales(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildDailySalesSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Medium,
                                ct),
                            response.Errors,
                            "Dnevni trend prodaje nije dostupan.");

                        response.CategoryData = await TryListSectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.CategoryData(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildCategoryDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Medium,
                                ct),
                            response.Errors,
                            "Prodaja po kategorijama nije dostupna.");

                        response.GenderData = await TryListSectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.GenderData(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildGenderDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Medium,
                                ct),
                            response.Errors,
                            "Prodaja po polu nije dostupna.");

                        response.SupplierData = await TryListSectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.SupplierData(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildSupplierDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Medium,
                                ct),
                            response.Errors,
                            "Prodaja po dobavljacima nije dostupna.");

                        response.SupplierOptions = await TryListSectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.SupplierFilters(fromDate, toDate, storeId, normalizedDataScope),
                                async () => await BuildSupplierFilterOptionsAsync(db, fromDate, toDate, storeId, ct, normalizedDataScope),
                                CacheExpiration.Long,
                                ct),
                            response.Errors,
                            "Lista dobavljaca za filter nije dostupna.");

                        response.WeekdayData = await TryListSectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ByWeekday(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildWeekdayDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Medium,
                                ct),
                            response.Errors,
                            "Prodaja po danima nije dostupna.");

                        response.HourData = await TryListSectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ByHour(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildHourDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Medium,
                                ct),
                            response.Errors,
                            "Prodaja po satima nije dostupna.");

                        response.PaymentData = await TryListSectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ByPayment(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildPaymentDataSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Medium,
                                ct),
                            response.Errors,
                            "Prodaja po nacinu placanja nije dostupna.");

                        response.QuickInsights = await TrySectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.QuickInsights(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildQuickInsightsSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Medium,
                                ct),
                            response.Errors,
                            "Brzi uvidi nisu dostupni.");

                        response.TransactionStats = await TrySectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.TransactionStats(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildTransactionStatsSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Medium,
                                ct),
                            response.Errors,
                            "Statistika transakcija nije dostupna.");

                        response.Advanced = await TrySectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.DashboardAdvanced(fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await BuildAdvancedDashboardSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Short,
                                ct),
                            response.Errors,
                            "Napredne metrike nisu dostupne.");

                        response.TopAdvanced = await TrySectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.TopProductsAdvanced(10, fromDate, toDate, storeId, supplierId, normalizedDataScope),
                                async () => await GetTopProductsAdvancedSnapshotAsync(db, 10, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope),
                                CacheExpiration.Short,
                                ct),
                            response.Errors,
                            "Napredna tabela top proizvoda nije dostupna.");

                        response.ValidationCompleteness = await TrySectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ValidationCompleteness,
                                async () => await BuildCompletenessValidationAsync(db, ct),
                                CacheExpiration.Short,
                                ct),
                            response.Errors,
                            "Completeness validacija nije dostupna.");

                        response.ValidationFreshness = await TrySectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ValidationFreshness,
                                async () => await BuildFreshnessValidationAsync(db, ct),
                                CacheExpiration.Short,
                                ct),
                            response.Errors,
                            "Freshness validacija nije dostupna.");

                        response.ValidationLostSales = await TrySectionAsync(
                            async () => await cache.GetOrSetAsync(
                                AnalyticsCacheKeys.ValidationLostSales,
                                async () => await BuildLostSalesValidationAsync(db, ct),
                                CacheExpiration.Short,
                                ct),
                            response.Errors,
                            "Lost-sales validacija nije dostupna.");

                        return response;
                    },
                    CacheExpiration.Short,
                    ct);

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
                    Errors = ["Dashboard bootstrap fallback: request timed out."]
                });
            }
            catch (NpgsqlException ex)
            {
                logger.LogWarning(ex, "Dashboard bootstrap fallback due to database issue.");
                return Results.Ok(new AnalyticsDashboardBootstrapDto
                {
                    Errors = ["Dashboard bootstrap fallback: database temporarily unavailable."]
                });
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "Dashboard bootstrap fallback due to timeout.");
                return Results.Ok(new AnalyticsDashboardBootstrapDto
                {
                    Errors = ["Dashboard bootstrap fallback: request timed out."]
                });
            }
        });

        group.MapGet("/filters/suppliers", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            ILogger<Program> logger,
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
                return Results.Ok(Array.Empty<SupplierFilterOptionDto>());
            }
            catch (NpgsqlException ex)
            {
                logger.LogWarning(ex, "Supplier filters fallback due to database issue.");
                return Results.Ok(Array.Empty<SupplierFilterOptionDto>());
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "Supplier filters fallback due to timeout.");
                return Results.Ok(Array.Empty<SupplierFilterOptionDto>());
            }
        });

        group.MapGet("/filters/stores", async (
            IAnalyticsCacheService cache,
            IAnalyticsDbContext analyticsDb,
            ITrendplusDbContext trendDb,
            ILogger<Program> logger,
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
                return Results.Ok(fallback);
            }
            catch (NpgsqlException ex)
            {
                logger.LogWarning(ex, "Store filters fallback due to database issue.");
                var fallback = await TryBuildStoreFiltersFallbackAsync(trendDb, logger, requestAborted);
                return Results.Ok(fallback);
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex, "Store filters fallback due to timeout.");
                var fallback = await TryBuildStoreFiltersFallbackAsync(trendDb, logger, requestAborted);
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
                CacheExpiration.Short,
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
                        "warning" => "Osvezavanje kasni, proverite import pipeline.",
                        _ => "Podaci su zastareli: osvezite import i agregate."
                    };
                    return new DashboardValidationEndpointDto
                    {
                        Status = status,
                        Message = message,
                        LastImport = lastImport,
                        FreshnessHours = freshnessHours
                    };
                },
                CacheExpiration.Short,
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
                    var (oosSkuCount, lostSalesEstimate) = await GetLostSalesSnapshotAsync(db, ct);
                    var status = lostSalesEstimate <= 0m ? "good" : lostSalesEstimate < 50000m ? "warning" : "critical";
                    var message = status switch
                    {
                        "good" => "Nema znacajnog gubitka prodaje zbog OOS.",
                        "warning" => "Postoji procenjen gubitak prodaje zbog OOS.",
                        _ => "Kritican OOS gubitak: replenishment je prioritet."
                    };
                    return new DashboardValidationEndpointDto
                    {
                        Status = status,
                        Message = message,
                        AffectedSku = oosSkuCount,
                        LostSalesEstimate = lostSalesEstimate
                    };
                },
                CacheExpiration.Short,
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
                        "good" => "Nema negativnih kolicina u prodajnim stavkama.",
                        "warning" => "Pronadjene su negativne kolicine u malom broju stavki.",
                        "critical" => "Negativne kolicine su iznad dozvoljenog praga i zahtevaju proveru.",
                        _ => "Nema podataka za proveru negativnih kolicina."
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
                CacheExpiration.Short,
                ct);

            return Results.Ok(result);
        });

        // ========== CACHE STATUS ENDPOINT ==========
        group.MapGet("/cache/status", (IAnalyticsCacheService cache) =>
        {
            return Results.Ok(new
            {
                redisAvailable = cache.IsRedisAvailable,
                cacheType = cache.IsRedisAvailable ? "Hybrid (In-Memory + Redis)" : "In-Memory only",
                message = cache.IsRedisAvailable 
                    ? "Cache radi u hibridnom modu - podaci su deljeni između instanci" 
                    : "Cache radi samo u In-Memory modu - brz ali nije deljen"
            });
        });

        // ========== CACHE INVALIDATE ENDPOINT (za admin) ==========
        group.MapPost("/cache/invalidate", async (IAnalyticsCacheService cache, CancellationToken ct) =>
        {
            await cache.RemoveByPrefixAsync(AnalyticsCacheKeys.Prefix, ct);
            return Results.Ok(new { success = true, message = "Analytics cache invalidiran" });
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

    private static async Task<(int oosSkuCount, decimal lostSalesEstimate)> GetLostSalesSnapshotAsync(
        ITrendplusDbContext db,
        CancellationToken ct,
        int? storeId = null,
        int? supplierId = null,
        string dataScope = "all")
    {
        var normalizedDataScope = NormalizeDataScope(dataScope);
        var hasSupplierFilter = supplierId.HasValue;

        await using var conn = await OpenTrendplusConnectionAsync(db, ct);
        if (conn is null) return (0, 0m);

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
                    var lostSales = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                    return (oosCount, Math.Round(lostSales, 2));
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
        await using var fallbackCmd = new NpgsqlCommand(fallbackSql, conn);
        fallbackCmd.Parameters.Add(new Npgsql.NpgsqlParameter("storeId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = (object?)storeId ?? DBNull.Value });
        fallbackCmd.Parameters.Add(new Npgsql.NpgsqlParameter("supplierId", NpgsqlTypes.NpgsqlDbType.Integer) { Value = (object?)supplierId ?? DBNull.Value });
        fallbackCmd.Parameters.Add(new Npgsql.NpgsqlParameter("scope", NpgsqlTypes.NpgsqlDbType.Text) { Value = normalizedDataScope });
        await using var fallbackReader = await fallbackCmd.ExecuteReaderAsync(ct);
        if (await fallbackReader.ReadAsync(ct))
        {
            var oosCount = fallbackReader.IsDBNull(0) ? 0 : fallbackReader.GetInt32(0);
            var lostSales = fallbackReader.IsDBNull(1) ? 0m : fallbackReader.GetDecimal(1);
            return (oosCount, Math.Round(lostSales, 2));
        }

        return (0, 0m);
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
                    TrendPct = reader.IsDBNull(8) ? null : Math.Round(reader.GetDecimal(8), 2)
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
        var (oosSkuCount, lostSalesEstimate) = await GetLostSalesSnapshotAsync(db, ct, storeId, supplierId, normalizedDataScope);
        var (avgVelocity, topVelocity, topSku, velocityTrend) = await GetVelocitySnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope);
        var (top20Share, top50Share) = await GetParetoSnapshotAsync(db, fromDate, toDate, storeId, supplierId, ct, normalizedDataScope);

        var completenessStatus = score >= 0.98m ? "good" : score >= 0.90m ? "warning" : "critical";
        var freshnessStatus = freshnessHours <= 6m ? "good" : freshnessHours <= 24m ? "warning" : "critical";
        var oosStatus = lostSalesEstimate <= 0m ? "good" : lostSalesEstimate < 50000m ? "warning" : "critical";
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
                    Subtitle = $"Lost sales estimate: {lostSalesEstimate.ToString("0.##", CultureInfo.InvariantCulture)} RSD"
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
                Description = $"OOS signal: {oosSkuCount} SKU, estimated lost sales {lostSalesEstimate.ToString("0.##", CultureInfo.InvariantCulture)} RSD.",
                Color = oosStatus == "critical" ? "red" : "yellow"
            });
            snapshot.Actions.Add(new DashboardActionDto
            {
                Priority = oosStatus == "critical" ? "P1" : "P2",
                Title = "Replenishment",
                Recommendation = "Prioritize refill for OOS/low-stock SKUs with highest velocity."
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
                Recommendation = "Backfill PLU, name and category for missing SKUs before pricing decisions."
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
                Recommendation = "Run import sync and refresh aggregate summaries."
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
                Recommendation = "Diversify sales concentration by promoting medium-performing SKUs."
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
                Description = "Sve je u redu: kljucne validacije su u zelenoj zoni.",
                Color = "green"
            });
        }

        if (snapshot.Actions.Count == 0)
        {
            snapshot.Actions.Add(new DashboardActionDto
            {
                Priority = "P3",
                Title = "Monitor",
                Recommendation = "Nastavite monitoring metrika i osvezavajte agregate dnevno."
            });
        }

        return snapshot;
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
                inventoryData?.OutOfStock ?? 0
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
                    ItemCount = g.Count(),
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
                    ItemCount = g.Count(),
                    TotalValue = g.Sum(x => x.Kolicina * x.Cena)
                });

        var stats = await perTransactionQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AvgItemsPerTransaction = g.Average(x => (decimal)x.ItemCount),
                AvgTransactionValue = g.Average(x => x.TotalValue),
                TotalTransactions = g.Count()
            })
            .SingleOrDefaultAsync(ct);

        if (stats is null)
            return new TransactionStatsDto();

        return new TransactionStatsDto
        {
            AvgItemsPerTransaction = Math.Round(stats.AvgItemsPerTransaction, 2),
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
            "warning" => "Osvezavanje kasni, proverite import pipeline.",
            _ => "Podaci su zastareli: osvezite import i agregate."
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
        var (oosSkuCount, lostSalesEstimate) = await GetLostSalesSnapshotAsync(db, ct);
        var status = lostSalesEstimate <= 0m ? "good" : lostSalesEstimate < 50000m ? "warning" : "critical";
        var message = status switch
        {
            "good" => "Nema znacajnog gubitka prodaje zbog OOS.",
            "warning" => "Postoji procenjen gubitak prodaje zbog OOS.",
            _ => "Kritican OOS gubitak: replenishment je prioritet."
        };

        return new DashboardValidationEndpointDto
        {
            Status = status,
            Message = message,
            AffectedSku = oosSkuCount,
            LostSalesEstimate = lostSalesEstimate
        };
    }

    private static string NormalizeDataScope(string? dataScope)
    {
        var normalized = (dataScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "all" or "imported" or "existing" ? normalized : "all";
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
}

// DTOs za cache (moraju biti klase za JSON serijalizaciju)
public class DailySaleDto
{
    public string Date { get; set; } = "";
    public decimal TotalRevenue { get; set; }
    public int TransactionCount { get; set; }
    public int TotalUnits { get; set; }
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
    public decimal AvgItemsPerTransaction { get; set; }
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
}

public class DashboardValidationDto
{
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = "";
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
    public List<string> Errors { get; set; } = [];
}

