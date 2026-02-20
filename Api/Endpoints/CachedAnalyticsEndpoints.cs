using Application.Analytics.Queries.GetInventoryStatus;
using Application.Analytics.Queries.GetSalesSummary;
using Application.Analytics.Queries.GetTopProducts;
using Application.Artikli.Common.Interfaces;
using Infrastructure.Services.Caching;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Trendplus2.Endpoints;

/// <summary>
/// Analytics endpointi sa hibridnim caching-om.
/// Cache smanjuje opterećenje baze i ubrzava response za 10-100x.
/// </summary>
public static class CachedAnalyticsEndpoints
{
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
                            Date = x.Date.ToString("yyyy-MM-dd"),
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
                            Date = x.Date.ToString("yyyy-MM-dd"),
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
                    var dayNames = new[] { "Nedelja", "Ponedeljak", "Utorak", "Sreda", "Četvrtak", "Petak", "Subota" };
                    var bestDay = prodaje
                        .GroupBy(p => p.DatumProdaje.DayOfWeek)
                        .Select(g => new
                        {
                            dayName = dayNames[(int)g.Key],
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
                    var avgItems = stavke.Any() ? stavke.Average(x => x.ItemCount) : 0.0;
                    var avgValue = stavke.Any() ? stavke.Average(x => x.TotalValue) : 0.0m;
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
                    var dayNames = new[] { "Nedelja", "Ponedeljak", "Utorak", "Sreda", "Četvrtak", "Petak", "Subota" };
                    var grouped = prodaje
                        .GroupBy(p => p.DatumProdaje.DayOfWeek)
                        .Select(g => new
                        {
                            dayOfWeek = ((int)g.Key).ToString(),
                            dayName = dayNames[(int)g.Key],
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
                            .GroupBy(s => artikli.ContainsKey(s.IdArtikal) ? artikli[s.IdArtikal] : "Ostalo")
                            .ToDictionary(
                                g => g.Key,
                                g => g.Sum(x => x.Kolicina * x.Cena)
                            );
                        var row = new Dictionary<string, object>
                        {
                            ["date"] = dateEntry.date.ToString("yyyy-MM-dd")
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
