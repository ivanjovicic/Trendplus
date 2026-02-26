using Application.Analytics.Queries.GetInventoryStatus;
using Application.Analytics.Queries.GetSalesSummary;
using Application.Analytics.Queries.GetTopProducts;
using Application.Artikli.Common.Interfaces;
using Infrastructure.Services.Caching;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
            .WithTags("Analytics (Cached)");

        // ========== SALES SUMMARY (CACHED) ==========
        group.MapGet("/sales/summary", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext trendDb,
            IMediator mediator,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? storeId = null,
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.SalesSummary(fromDate, toDate);
            
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var aggregatedSummary = await TryGetSalesSummaryFromAggregatesAsync(trendDb, fromDate, toDate, ct);
                    if (aggregatedSummary is not null)
                    {
                        return aggregatedSummary;
                    }

                    try
                    {
                        return await mediator.Send(new GetSalesSummaryQuery(fromDate, toDate, storeId), ct);
                    }
                    catch (Exception ex) when (IsMissingRelation(ex))
                    {
                        var baseQuery = from p in trendDb.ProdajaZaglavlja.AsNoTracking()
                                        join ps in trendDb.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                                        where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                              (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                                              (!storeId.HasValue || p.IDObjekat == storeId.Value)
                                        select new { p.Id, Iznos = ps.Kolicina * ps.Cena, ps.Kolicina };

                        var totalRevenue = await baseQuery.SumAsync(x => (decimal?)x.Iznos, ct) ?? 0m;
                        var totalUnits = await baseQuery.SumAsync(x => (int?)x.Kolicina, ct) ?? 0;
                        var totalTransactions = await baseQuery.Select(x => x.Id).Distinct().CountAsync(ct);
                        var avgBasket = totalTransactions > 0 ? totalRevenue / totalTransactions : 0m;
                        var avgItem = totalUnits > 0 ? totalRevenue / totalUnits : 0m;

                        return new SalesSummaryDto(totalRevenue, totalTransactions, totalUnits, avgBasket, avgItem);
                    }
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.TopProducts(top, fromDate, toDate);
            
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var aggregatedTop = await TryGetTopProductsFromAggregatesAsync(trendDb, top, fromDate, toDate, ct);
                    if (aggregatedTop is not null)
                    {
                        return aggregatedTop;
                    }

                    try
                    {
                        return await mediator.Send(new GetTopProductsQuery(fromDate, toDate, top, storeId), ct);
                    }
                    catch (Exception ex) when (IsMissingRelation(ex))
                    {
                        var aggregated = await (
                            from ps in trendDb.ProdajaStavke.AsNoTracking()
                            join p in trendDb.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals p.Id
                            join a in trendDb.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
                            where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                  (!toDate.HasValue || p.DatumProdaje <= toDate.Value) &&
                                  (!storeId.HasValue || p.IDObjekat == storeId.Value)
                            group new { ps, a } by new { ps.IdArtikal, a.Naziv, a.Velicina, a.Boja } into g
                            select new TopProductDto(
                                g.Key.IdArtikal,
                                g.Key.Naziv,
                                g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                                g.Sum(x => x.ps.Kolicina),
                                g.Key.Velicina,
                                g.Key.Boja)
                        ).ToListAsync(ct);

                        var topByRevenue = aggregated.OrderByDescending(x => x.TotalRevenue).Take(top).ToList();
                        var topByUnits = aggregated.OrderByDescending(x => x.TotalUnits).Take(top).ToList();
                        return new TopProductsResult(topByRevenue, topByUnits);
                    }
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.TopProductsAdvanced(top, fromDate, toDate);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () => await GetTopProductsAdvancedSnapshotAsync(db, top, fromDate, toDate, ct),
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
                        var q = trendDb.Artikli.AsNoTracking();
                        var totalSku = await q.CountAsync(ct);
                        var totalOnHand = await q.SumAsync(x => (int?)x.Kolicina, ct) ?? 0;
                        var outOfStock = await q.CountAsync(x => (x.Kolicina ?? 0) == 0, ct);
                        var lowStock = await q.CountAsync(x => (x.Kolicina ?? 0) > 0 && (x.Kolicina ?? 0) <= lowStockThreshold, ct);

                        return new InventoryStatusDto(totalSku, totalOnHand, lowStock, outOfStock);
                    }
                },
                CacheExpiration.Short, // Inventory se brzo menja
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.DailySales(fromDate, toDate);
            
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var aggregatedDaily = await TryGetDailySalesFromAggregatesAsync(trendDb, fromDate, toDate, ct);
                    if (aggregatedDaily is not null && aggregatedDaily.Count > 0)
                    {
                        return aggregatedDaily;
                    }

                    try
                    {
                        var query = db.SalesFacts.AsNoTracking();

                        if (fromDate.HasValue)
                            query = query.Where(s => s.SaleTimestampUtc >= fromDate.Value);

                        if (toDate.HasValue)
                            query = query.Where(s => s.SaleTimestampUtc <= toDate.Value);

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
                    catch (Exception ex) when (IsMissingRelation(ex))
                    {
                        var fallbackRaw = await (
                                            from p in trendDb.ProdajaZaglavlja.AsNoTracking()
                                            join ps in trendDb.ProdajaStavke.AsNoTracking() on p.Id equals ps.IdProdaja
                                            where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                                  (!toDate.HasValue || p.DatumProdaje <= toDate.Value)
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
                    }
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.CategoryData(fromDate, toDate);
            
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var aggregatedCategory = await TryGetCategoryDataFromAggregatesAsync(db, fromDate, toDate, ct);
                    if (aggregatedCategory is not null && aggregatedCategory.Count > 0)
                    {
                        return aggregatedCategory;
                    }

                    var query = from ps in db.ProdajaStavke
                                join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                                join a in db.Artikli on ps.IdArtikal equals a.Id
                                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value)
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.GenderData(fromDate, toDate);
            
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var aggregatedGender = await TryGetGenderDataFromAggregatesAsync(db, fromDate, toDate, ct);
                    if (aggregatedGender is not null && aggregatedGender.Count > 0)
                    {
                        return aggregatedGender;
                    }

                    var query = from ps in db.ProdajaStavke
                                join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                                join a in db.Artikli on ps.IdArtikal equals a.Id
                                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value)
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.SupplierData(fromDate, toDate);
            
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var aggregatedSupplier = await TryGetSupplierDataFromAggregatesAsync(db, fromDate, toDate, ct);
                    if (aggregatedSupplier is not null && aggregatedSupplier.Count > 0)
                    {
                        return aggregatedSupplier;
                    }

                    var query = from ps in db.ProdajaStavke
                                join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                                join a in db.Artikli on ps.IdArtikal equals a.Id
                                join d in db.Dobavljaci on a.IDDobavljac equals d.Id into dobavljacJoin
                                from d in dobavljacJoin.DefaultIfEmpty()
                                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value)
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);

            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.QuickInsights(fromDate, toDate);
            
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var prodajeQuery = db.ProdajaZaglavlja.AsNoTracking().AsQueryable();
                    
                    if (fromDate.HasValue)
                        prodajeQuery = prodajeQuery.Where(p => p.DatumProdaje >= fromDate.Value);
                    
                    if (toDate.HasValue)
                        prodajeQuery = prodajeQuery.Where(p => p.DatumProdaje <= toDate.Value);

                    var prodaje = await prodajeQuery.ToListAsync(ct);

                    // Best day calculation
                    var bestDay = prodaje
                        .GroupBy(p => p.DatumProdaje.DayOfWeek)
                        .Select(g => new
                        {
                            dayName = SerbianDayNames[(int)g.Key],
                            prodajeIds = g.Select(p => p.Id).ToList()
                        })
                        .ToList();

                    string? bestDayName = null;
                    decimal bestDayRevenue = 0;

                    foreach (var day in bestDay)
                    {
                        var revenue = await db.ProdajaStavke
                            .Where(ps => day.prodajeIds.Contains(ps.IdProdaja))
                            .SumAsync(ps => ps.Kolicina * ps.Cena, ct);
                        
                        if (revenue > bestDayRevenue)
                        {
                            bestDayRevenue = revenue;
                            bestDayName = day.dayName;
                        }
                    }

                    // Top product
                    var prodajeIds = prodaje.Select(p => p.Id).ToList();
                    var topProductData = await db.ProdajaStavke
                        .Where(ps => prodajeIds.Contains(ps.IdProdaja))
                        .GroupBy(ps => ps.IdArtikal)
                        .Select(g => new { artikalId = g.Key, totalRevenue = g.Sum(x => x.Kolicina * x.Cena) })
                        .OrderByDescending(x => x.totalRevenue)
                        .FirstOrDefaultAsync(ct);

                    string? topProductName = null;
                    if (topProductData != null)
                    {
                        var artikal = await db.Artikli.FindAsync(new object[] { topProductData.artikalId }, ct);
                        topProductName = artikal?.Naziv;
                    }

                    // Low stock count
                    var lowStockCount = await db.Artikli
                        .Where(a => a.Kolicina <= a.MinimalnaKolicina || a.Kolicina == 0)
                        .CountAsync(ct);

                    return new QuickInsightsDto
                    {
                        BestDay = bestDayName,
                        BestDayRevenue = bestDayRevenue,
                        TopProduct = topProductName,
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            var cacheKey = AnalyticsCacheKeys.TransactionStats(fromDate, toDate);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var query = db.ProdajaZaglavlja.AsNoTracking().AsQueryable();
                    if (fromDate.HasValue)
                        query = query.Where(p => p.DatumProdaje >= fromDate.Value);
                    if (toDate.HasValue)
                        query = query.Where(p => p.DatumProdaje <= toDate.Value);
                    var prodajeIds = await query.Select(p => p.Id).ToListAsync(ct);
                    if (prodajeIds.Count == 0)
                    {
                        return new
                        {
                            avgItemsPerTransaction = 0.0,
                            avgTransactionValue = 0.0m,
                            totalTransactions = 0
                        };
                    }
                    var stavke = await db.ProdajaStavke
                        .Where(ps => prodajeIds.Contains(ps.IdProdaja))
                        .GroupBy(ps => ps.IdProdaja)
                        .Select(g => new
                        {
                            IdProdaja = g.Key,
                            ItemCount = g.Count(),
                            TotalValue = g.Sum(x => x.Kolicina * x.Cena)
                        })
                        .ToListAsync(ct);
                    var hasStavke = stavke.Count > 0;
                    var avgItems = hasStavke ? stavke.Average(x => x.ItemCount) : 0.0;
                    var avgValue = hasStavke ? stavke.Average(x => x.TotalValue) : 0.0m;
                    return new
                    {
                        avgItemsPerTransaction = avgItems,
                        avgTransactionValue = avgValue,
                        totalTransactions = prodajeIds.Count
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            var cacheKey = AnalyticsCacheKeys.ByPayment(fromDate, toDate);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var query = from p in db.ProdajaZaglavlja
                                where (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                      (!toDate.HasValue || p.DatumProdaje <= toDate.Value)
                                group p by p.NacinPlacanja into g
                                select new
                                {
                                    nacinPlacanja = g.Key ?? "Nepoznato",
                                    transactionCount = g.Count()
                                };
                    var prodajeByPayment = await query.ToListAsync(ct);
                    var result = new List<object>();
                    foreach (var item in prodajeByPayment)
                    {
                        var prodajeIds = await db.ProdajaZaglavlja
                            .Where(p => p.NacinPlacanja == (item.nacinPlacanja == "Nepoznato" ? null : item.nacinPlacanja))
                            .Where(p => (!fromDate.HasValue || p.DatumProdaje >= fromDate.Value) &&
                                        (!toDate.HasValue || p.DatumProdaje <= toDate.Value))
                            .Select(p => p.Id)
                            .ToListAsync(ct);
                        var totalRevenue = await db.ProdajaStavke
                            .Where(ps => prodajeIds.Contains(ps.IdProdaja))
                            .SumAsync(ps => ps.Kolicina * ps.Cena, ct);
                        result.Add(new
                        {
                            item.nacinPlacanja,
                            totalRevenue,
                            item.transactionCount
                        });
                    }
                    return result;
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            var cacheKey = AnalyticsCacheKeys.ByWeekday(fromDate, toDate);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var query = db.ProdajaZaglavlja.AsNoTracking().AsQueryable();
                    if (fromDate.HasValue)
                        query = query.Where(p => p.DatumProdaje >= fromDate.Value);
                    if (toDate.HasValue)
                        query = query.Where(p => p.DatumProdaje <= toDate.Value);
                    var prodaje = await query.ToListAsync(ct);
                    var grouped = prodaje
                        .GroupBy(p => p.DatumProdaje.DayOfWeek)
                        .Select(g => new
                        {
                            dayOfWeek = ((int)g.Key).ToString(CultureInfo.InvariantCulture),
                            dayName = SerbianDayNames[(int)g.Key],
                            transactionCount = g.Count(),
                            prodajeIds = g.Select(p => p.Id).ToList()
                        })
                        .ToList();
                    var result = new List<object>();
                    foreach (var day in grouped)
                    {
                        var totalRevenue = await db.ProdajaStavke
                            .Where(ps => day.prodajeIds.Contains(ps.IdProdaja))
                            .SumAsync(ps => ps.Kolicina * ps.Cena, ct);
                        result.Add(new
                        {
                            day.dayOfWeek,
                            day.dayName,
                            totalRevenue,
                            day.transactionCount
                        });
                    }
                    return result.OrderBy(x => int.Parse(((dynamic)x).dayOfWeek));
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            var cacheKey = AnalyticsCacheKeys.ByHour(fromDate, toDate);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var query = db.ProdajaZaglavlja.AsNoTracking().AsQueryable();
                    if (fromDate.HasValue)
                        query = query.Where(p => p.DatumProdaje >= fromDate.Value);
                    if (toDate.HasValue)
                        query = query.Where(p => p.DatumProdaje <= toDate.Value);
                    var prodaje = await query.ToListAsync(ct);
                    var grouped = prodaje
                        .GroupBy(p => p.DatumProdaje.Hour)
                        .Select(g => new
                        {
                            hour = g.Key,
                            transactionCount = g.Count(),
                            prodajeIds = g.Select(p => p.Id).ToList()
                        })
                        .ToList();
                    var result = new List<object>();
                    foreach (var hour in grouped)
                    {
                        var totalRevenue = await db.ProdajaStavke
                            .Where(ps => hour.prodajeIds.Contains(ps.IdProdaja))
                            .SumAsync(ps => ps.Kolicina * ps.Cena, ct);
                        result.Add(new
                        {
                            hour = hour.hour,
                            totalRevenue,
                            transactionCount = hour.transactionCount
                        });
                    }
                    return result.OrderBy(x => ((dynamic)x).hour);
                },
                CacheExpiration.Medium,
                ct);
            return Results.Ok(result);
        });

        // ========== REORDER SUGGESTIONS (CACHED) ==========
        group.MapGet("/reorder-suggestions", async (
            IAnalyticsCacheService cache,
            ITrendplusDbContext db,
            CancellationToken ct = default) =>
        {
            var cacheKey = AnalyticsCacheKeys.ReorderSuggestions;
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var artikli = await db.Artikli
                        .Where(a => a.Kolicina <= a.MinimalnaKolicina || a.Kolicina == 0)
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
            var cacheKey = AnalyticsCacheKeys.CategoryTrends(fromDate, toDate);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var prodajeQuery = db.ProdajaZaglavlja.AsNoTracking().AsQueryable();
                    if (fromDate.HasValue)
                        prodajeQuery = prodajeQuery.Where(p => p.DatumProdaje >= fromDate.Value);
                    if (toDate.HasValue)
                        prodajeQuery = prodajeQuery.Where(p => p.DatumProdaje <= toDate.Value);
                    var prodaje = await prodajeQuery.ToListAsync(ct);
                    var prodajeIds = prodaje.Select(p => p.Id).ToList();
                    var stavke = await db.ProdajaStavke
                        .Where(ps => prodajeIds.Contains(ps.IdProdaja))
                        .ToListAsync(ct);
                    var artikalIds = stavke.Select(s => s.IdArtikal).Distinct().ToList();
                    var artikli = await db.Artikli
                        .Where(a => artikalIds.Contains(a.Id))
                        .ToDictionaryAsync(a => a.Id, a => a.Kategorija ?? "Ostalo", ct);
                    var grouped = prodaje
                        .GroupBy(p => p.DatumProdaje.Date)
                        .Select(dateGroup => new
                        {
                            date = dateGroup.Key,
                            prodajeIds = dateGroup.Select(p => p.Id).ToList()
                        })
                        .OrderBy(x => x.date)
                        .ToList();
                    var result = new List<Dictionary<string, object>>();
                    foreach (var dateEntry in grouped)
                    {
                        var dateStavke = stavke.Where(s => dateEntry.prodajeIds.Contains(s.IdProdaja)).ToList();
                        var categoryRevenues = dateStavke
                            .GroupBy(s => artikli.TryGetValue(s.IdArtikal, out var kategorija) ? kategorija : "Ostalo")
                            .ToDictionary(
                                g => g.Key,
                                g => g.Sum(x => x.Kolicina * x.Cena)
                            );
                        var row = new Dictionary<string, object>
                        {
                            ["date"] = dateEntry.date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        };
                        foreach (var cat in categoryRevenues)
                        {
                            row[cat.Key] = cat.Value;
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
            CancellationToken ct = default) =>
        {
            if (fromDate.HasValue && fromDate.Value.Kind == DateTimeKind.Unspecified)
                fromDate = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
            if (toDate.HasValue && toDate.Value.Kind == DateTimeKind.Unspecified)
                toDate = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);

            var cacheKey = AnalyticsCacheKeys.DashboardAdvanced(fromDate, toDate);
            var result = await cache.GetOrSetAsync(
                cacheKey,
                async () => await BuildAdvancedDashboardSnapshotAsync(db, fromDate, toDate, ct),
                CacheExpiration.Short,
                ct);

            return Results.Ok(result);
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
            cmd.Parameters.AddWithValue("fromDate", (object?)fromDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("toDate", (object?)toDate?.Date ?? DBNull.Value);

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
            cmd.Parameters.AddWithValue("fromDate", (object?)fromDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("toDate", (object?)toDate?.Date ?? DBNull.Value);

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
            cmd.Parameters.AddWithValue("fromDate", (object?)fromDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("toDate", (object?)toDate?.Date ?? DBNull.Value);

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
            cmd.Parameters.AddWithValue("fromDate", (object?)fromDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("toDate", (object?)toDate?.Date ?? DBNull.Value);

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
                    COALESCE("Pol", 'Neodredjeno') AS pol,
                    COALESCE(SUM("TotalRevenue"), 0) AS total_revenue,
                    COALESCE(SUM("TotalUnits"), 0)::int AS total_units
                FROM "AnalyticsGenderSummary"
                WHERE (@fromDate IS NULL OR "Date" >= @fromDate::date)
                  AND (@toDate IS NULL OR "Date" <= @toDate::date)
                GROUP BY COALESCE("Pol", 'Neodredjeno')
                ORDER BY total_revenue DESC;
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("fromDate", (object?)fromDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("toDate", (object?)toDate?.Date ?? DBNull.Value);

            var list = new List<GenderDataDto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new GenderDataDto
                {
                    Pol = reader.IsDBNull(0) ? "Neodredjeno" : reader.GetString(0),
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
            cmd.Parameters.AddWithValue("fromDate", (object?)fromDate?.Date ?? DBNull.Value);
            cmd.Parameters.AddWithValue("toDate", (object?)toDate?.Date ?? DBNull.Value);

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
        CancellationToken ct)
    {
        await using var conn = await OpenTrendplusConnectionAsync(db, ct);
        if (conn is null) return (0, 0m);

        try
        {
            const string viewSql = """
                SELECT
                    COALESCE(SUM(is_oos), 0)::int AS oos_sku_count,
                    COALESCE(SUM(lost_sales_estimate), 0) AS lost_sales_estimate
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

        const string fallbackSql = """
            WITH recent AS (
              SELECT
                ps."id_artikal" AS article_id,
                AVG(ps."kolicina") AS avg_units_per_sale,
                AVG(ps."cena") AS avg_price
              FROM "prodaja_stavke" ps
              JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
              WHERE p."datum_prodaje" >= NOW() - INTERVAL '30 days'
              GROUP BY ps."id_artikal"
            )
            SELECT
              COUNT(*) FILTER (WHERE COALESCE(a."Kolicina", 0) <= 0)::int AS oos_sku_count,
              COALESCE(SUM(
                CASE WHEN COALESCE(a."Kolicina", 0) <= 0
                     THEN COALESCE(r.avg_units_per_sale, 0) * COALESCE(r.avg_price, 0)
                     ELSE 0
                END
              ), 0) AS lost_sales_estimate
            FROM "Artikli" a
            LEFT JOIN recent r ON r.article_id = a."Id";
            """;
        await using var fallbackCmd = new NpgsqlCommand(fallbackSql, conn);
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
            cmd.Parameters.AddWithValue("fromDate", (object?)fromDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("toDate", (object?)toDate ?? DBNull.Value);
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
        CancellationToken ct)
    {
        await using var conn = await OpenTrendplusConnectionAsync(db, ct);
        if (conn is null) return (0m, 0m, "N/A", null);

        const string velocitySql = """
            WITH base AS (
              SELECT
                COALESCE(a."PLU", a."Id"::text) AS sku,
                DATE(p."datum_prodaje") AS sale_day,
                SUM(ps."kolicina")::decimal AS units_day
              FROM "prodaja_stavke" ps
              JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
              JOIN "Artikli" a ON a."Id" = ps."id_artikal"
              WHERE (@fromDate IS NULL OR p."datum_prodaje" >= @fromDate)
                AND (@toDate IS NULL OR p."datum_prodaje" <= @toDate)
              GROUP BY COALESCE(a."PLU", a."Id"::text), DATE(p."datum_prodaje")
            ),
            agg AS (
              SELECT
                sku,
                SUM(units_day) / GREATEST(COUNT(*), 1) AS velocity
              FROM base
              GROUP BY sku
            )
            SELECT
              COALESCE(AVG(velocity), 0) AS avg_velocity,
              COALESCE(MAX(velocity), 0) AS top_velocity,
              COALESCE((ARRAY_AGG(sku ORDER BY velocity DESC))[1], 'N/A') AS top_sku
            FROM agg;
            """;
        await using var cmd = new NpgsqlCommand(velocitySql, conn);
        cmd.Parameters.AddWithValue("fromDate", (object?)fromDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("toDate", (object?)toDate ?? DBNull.Value);
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
            const string trendSql = """
                SELECT
                  COALESCE(SUM("TotalUnits") FILTER (WHERE "Date" >= CURRENT_DATE - INTERVAL '6 days'), 0) AS last7,
                  COALESCE(SUM("TotalUnits") FILTER (WHERE "Date" BETWEEN CURRENT_DATE - INTERVAL '13 days' AND CURRENT_DATE - INTERVAL '7 days'), 0) AS prev7
                FROM "AnalyticsDailySummary";
                """;
            await using var trendCmd = new NpgsqlCommand(trendSql, conn);
            await using var trendReader = await trendCmd.ExecuteReaderAsync(ct);
            if (await trendReader.ReadAsync(ct))
            {
                var last7 = trendReader.IsDBNull(0) ? 0m : trendReader.GetDecimal(0);
                var prev7 = trendReader.IsDBNull(1) ? 0m : trendReader.GetDecimal(1);
                trendPct = prev7 <= 0m
                    ? (last7 > 0m ? 100m : 0m)
                    : Math.Round(((last7 - prev7) / prev7) * 100m, 2);
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
        CancellationToken ct)
    {
        await using var conn = await OpenTrendplusConnectionAsync(db, ct);
        if (conn is null) return (0m, 0m);

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

        const string fallbackSql = """
            WITH ranked AS (
              SELECT SUM(ps."kolicina" * ps."cena") AS revenue
              FROM "prodaja_stavke" ps
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
        CancellationToken ct)
    {
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

        const string sql = """
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
                  WHEN COUNT(*) FILTER (WHERE a."NabavnaCena" IS NOT NULL) = 0 THEN NULL
                  ELSE SUM((ps."cena" - COALESCE(a."NabavnaCena", ps."cena")) * ps."kolicina")
                END AS margin_impact
              FROM "prodaja_stavke" ps
              JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
              JOIN "Artikli" a ON a."Id" = ps."id_artikal"
              WHERE (@fromDate IS NULL OR p."datum_prodaje" >= @fromDate)
                AND (@toDate IS NULL OR p."datum_prodaje" <= @toDate)
              GROUP BY ps."id_artikal"
            ),
            previous_period AS (
              SELECT
                ps."id_artikal" AS product_id,
                SUM(ps."kolicina")::decimal AS prev_units
              FROM "prodaja_stavke" ps
              JOIN "prodaja_zaglavlje" p ON p."id" = ps."id_prodaja"
              CROSS JOIN period_size s
              WHERE p."datum_prodaje" >= (s.from_date - (s.days_count * INTERVAL '1 day'))
                AND p."datum_prodaje" < s.from_date
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
            cmd.Parameters.AddWithValue("fromDate", (object?)fromDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("toDate", (object?)toDate ?? DBNull.Value);

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
        CancellationToken ct)
    {
        var (score, totalSku, missingSku, lastImport, freshnessHours) = await GetCompletenessAndFreshnessAsync(db, ct);
        var (oosSkuCount, lostSalesEstimate) = await GetLostSalesSnapshotAsync(db, ct);
        var (avgVelocity, topVelocity, topSku, velocityTrend) = await GetVelocitySnapshotAsync(db, fromDate, toDate, ct);
        var (top20Share, top50Share) = await GetParetoSnapshotAsync(db, ct);

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

public class QuickInsightsDto
{
    public string? BestDay { get; set; }
    public decimal BestDayRevenue { get; set; }
    public string? TopProduct { get; set; }
    public int LowStockAlert { get; set; }
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
