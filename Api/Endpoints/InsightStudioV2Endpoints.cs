using Application.Artikli.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Trendplus2.Endpoints;

/// <summary>
/// Insight Studio V2 — Advanced Analytics endpoints
/// Weekly Heatmap, Basket Affinity, Price Sensitivity, Velocity-Margin Matrix,
/// Product Lifecycle, Stock Depletion Forecast, Margin Alerts, Weekly Changelog,
/// Enhanced Supplier Scoring, Enhanced Reorder Plan
/// </summary>
public static class InsightStudioV2Endpoints
{
    public static void MapInsightStudioV2Endpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics/advanced/v2")
            .WithTags("Insight Studio V2");

        // ─── WEEKLY DEMAND HEATMAP ────────────────────────────────────────
        group.MapGet("/weekly-heatmap", async (
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var fromUtc = fromDate.HasValue
                    ? DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow.AddDays(-90);
                var toUtc = toDate.HasValue
                    ? DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow;

                var salesData = await (
                    from pz in db.ProdajaZaglavlja
                    join ps in db.ProdajaStavke on pz.Id equals ps.IdProdaja
                    where pz.DatumProdaje >= fromUtc && pz.DatumProdaje <= toUtc
                    select new { pz.DatumProdaje, ps.Kolicina, ps.Cena }
                ).ToListAsync(ct);

                // Build heatmap: dayOfWeek (0=Mon..6=Sun) × week number
                var dayNames = new[] { "Pon", "Uto", "Sre", "Čet", "Pet", "Sub", "Ned" };
                var byDayAndWeek = salesData
                    .GroupBy(s => new
                    {
                        DayOfWeek = ((int)s.DatumProdaje.DayOfWeek + 6) % 7, // Mon=0
                        WeekStart = s.DatumProdaje.Date.AddDays(-((int)(s.DatumProdaje.DayOfWeek + 6) % 7))
                    })
                    .Select(g => new
                    {
                        day = g.Key.DayOfWeek,
                        dayName = dayNames[g.Key.DayOfWeek],
                        weekStart = g.Key.WeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        revenue = g.Sum(s => s.Kolicina * s.Cena),
                        units = g.Sum(s => s.Kolicina),
                        transactions = g.Count()
                    })
                    .OrderBy(x => x.weekStart)
                    .ThenBy(x => x.day)
                    .ToList();

                // Aggregate by day of week
                var byDay = byDayAndWeek
                    .GroupBy(x => x.day)
                    .Select(g => new
                    {
                        day = g.Key,
                        dayName = dayNames[g.Key],
                        avgRevenue = g.Average(x => (double)x.revenue),
                        avgUnits = g.Average(x => (double)x.units),
                        totalRevenue = g.Sum(x => x.revenue),
                        peakWeek = g.OrderByDescending(x => x.revenue).First().weekStart
                    })
                    .OrderBy(x => x.day)
                    .ToList();

                return Results.Ok(new { cells = byDayAndWeek, byDay });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        }).RequireRateLimiting("db-heavy");

        // ─── BASKET AFFINITY ──────────────────────────────────────────────
        group.MapGet("/basket-affinity", async (
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int minSupport = 3,
            CancellationToken ct = default) =>
        {
            try
            {
                var fromUtc = fromDate.HasValue
                    ? DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow.AddDays(-90);
                var to = toDate.HasValue
                    ? DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow;

                // Get multi-item transactions only
                var basketItems = await (
                    from ps in db.ProdajaStavke
                    join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where p.DatumProdaje >= fromUtc && p.DatumProdaje <= to
                    group a by ps.IdProdaja into g
                    where g.Count() >= 2
                    select g.Select(x => x.Kategorija ?? "Ostalo").Distinct().ToList()
                ).ToListAsync(ct);

                if (basketItems.Count == 0)
                    return Results.Ok(new { pairs = new List<object>(), totalMultiItemTransactions = 0 });

                var pairCounts = basketItems
                    .SelectMany(basket => basket
                        .SelectMany((item, i) => basket.Skip(i + 1)
                            .Select(other => string.Compare(item, other, StringComparison.Ordinal) < 0
                                ? (item: item, other: other)
                                : (item: other, other: item))))
                    .GroupBy(pair => pair)
                    .ToDictionary(g => g.Key, g => g.Count());

                var pairs = pairCounts
                    .Where(kv => kv.Value >= minSupport)
                    .OrderByDescending(kv => kv.Value)
                    .Take(20)
                    .Select(kv => new
                    {
                        categoryA = kv.Key.item,
                        categoryB = kv.Key.other,
                        coOccurrences = kv.Value,
                        supportPct = basketItems.Count > 0
                            ? (double)kv.Value / basketItems.Count * 100 : 0
                    })
                    .ToList();

                return Results.Ok(new { pairs, totalMultiItemTransactions = basketItems.Count });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        }).RequireRateLimiting("db-heavy");

        // ─── VELOCITY × MARGIN MATRIX ─────────────────────────────────────
        group.MapGet("/velocity-margin-matrix", async (
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var fromUtc = fromDate.HasValue
                    ? DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow.AddDays(-90);
                var to = toDate.HasValue
                    ? DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow;
                var days = Math.Max(1, (to - fromUtc).TotalDays);

                var productData = await (
                    from ps in db.ProdajaStavke
                    join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where p.DatumProdaje >= fromUtc && p.DatumProdaje <= to
                    group new { ps, a } by new { a.Id, a.Naziv, a.Kategorija, a.Pol, a.NabavnaCena, a.Kolicina } into g
                    select new
                    {
                        artikalId = g.Key.Id,
                        naziv = g.Key.Naziv,
                        kategorija = g.Key.Kategorija ?? "Ostalo",
                        pol = g.Key.Pol ?? "Neodređeno",
                        nabavnaCena = g.Key.NabavnaCena,
                        currentStock = g.Key.Kolicina ?? 0,
                        totalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                        totalCost = g.Key.NabavnaCena.HasValue
                            ? g.Sum(x => x.ps.Kolicina) * g.Key.NabavnaCena.Value : (decimal?)null,
                        totalUnits = g.Sum(x => x.ps.Kolicina)
                    }
                ).ToListAsync(ct);

                var allMargins = productData
                    .Where(p => p.totalCost.HasValue && p.totalRevenue > 0)
                    .Select(p => (double)((p.totalRevenue - p.totalCost!.Value) / p.totalRevenue * 100))
                    .OrderBy(x => x)
                    .ToList();
                var medianMargin = allMargins.Count > 0
                    ? allMargins[allMargins.Count / 2] : 35;

                var allVelocities = productData
                    .Select(p => p.totalUnits / days)
                    .OrderBy(x => x)
                    .ToList();
                var medianVelocity = allVelocities.Count > 0
                    ? allVelocities[allVelocities.Count / 2] : 0.1;

                var items = productData.Select(p =>
                {
                    var marginPct = p.totalCost.HasValue && p.totalRevenue > 0
                        ? (double)((p.totalRevenue - p.totalCost.Value) / p.totalRevenue * 100) : 0;
                    var velocity = p.totalUnits / days;
                    var quadrant = velocity >= medianVelocity
                        ? (marginPct >= medianMargin ? "STAR" : "VOLUME_TRAP")
                        : (marginPct >= medianMargin ? "NICHE_GEM" : "DEAD_WEIGHT");

                    return new
                    {
                        p.artikalId, p.naziv, p.kategorija, p.pol,
                        p.totalRevenue, p.totalUnits,
                        marginPct,
                        velocity,
                        quadrant
                    };
                })
                .OrderByDescending(x => x.totalRevenue)
                .ToList();

                return Results.Ok(new
                {
                    items,
                    medianMargin,
                    medianVelocity,
                    quadrantCounts = new
                    {
                        stars = items.Count(x => x.quadrant == "STAR"),
                        nicheGems = items.Count(x => x.quadrant == "NICHE_GEM"),
                        volumeTraps = items.Count(x => x.quadrant == "VOLUME_TRAP"),
                        deadWeight = items.Count(x => x.quadrant == "DEAD_WEIGHT")
                    }
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        }).RequireRateLimiting("db-heavy");

        // ─── PRODUCT LIFECYCLE STAGE ──────────────────────────────────────
        group.MapGet("/product-lifecycle", async (
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var from = fromDate.HasValue
                    ? DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow.AddDays(-90);
                var to = toDate.HasValue
                    ? DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow;

                // Split period into halves to detect trend
                var mid = from.AddDays((to - from).TotalDays / 2);

                var allIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= from && p.DatumProdaje <= to)
                    .Select(p => new { p.Id, p.DatumProdaje })
                    .ToListAsync(ct);

                var firstHalfIds = allIds.Where(x => x.DatumProdaje < mid).Select(x => x.Id).ToList();
                var secondHalfIds = allIds.Where(x => x.DatumProdaje >= mid).Select(x => x.Id).ToList();

                var allStavke = await (
                    from ps in db.ProdajaStavke
                    where allIds.Select(x => x.Id).Contains(ps.IdProdaja)
                    select new { ps.IdProdaja, ps.IdArtikal, ps.Kolicina, ps.Cena }
                ).ToListAsync(ct);

                var artikli = await db.Artikli
                    .Select(a => new { a.Id, a.Naziv, a.Kategorija, a.Pol, a.Kolicina, a.NabavnaCena, a.ProdajnaCena })
                    .ToDictionaryAsync(a => a.Id, ct);

                var productGroups = allStavke
                    .GroupBy(x => x.IdArtikal)
                    .Select(g =>
                    {
                        var art = artikli.GetValueOrDefault(g.Key);
                        var firstHalfUnits = g.Where(x => firstHalfIds.Contains(x.IdProdaja)).Sum(x => x.Kolicina);
                        var secondHalfUnits = g.Where(x => secondHalfIds.Contains(x.IdProdaja)).Sum(x => x.Kolicina);
                        var totalUnits = g.Sum(x => x.Kolicina);
                        var totalRevenue = g.Sum(x => x.Kolicina * x.Cena);

                        // Determine lifecycle stage
                        var trendPct = firstHalfUnits > 0
                            ? (secondHalfUnits - firstHalfUnits) / (double)firstHalfUnits * 100
                            : (secondHalfUnits > 0 ? 100 : 0);

                        string stage;
                        if (firstHalfUnits == 0 && secondHalfUnits > 0)
                            stage = "LAUNCH";
                        else if (trendPct > 20)
                            stage = "GROWTH";
                        else if (trendPct >= -20)
                            stage = "MATURE";
                        else
                            stage = "DECLINE";

                        return new
                        {
                            artikalId = g.Key,
                            naziv = art?.Naziv ?? "?",
                            kategorija = art?.Kategorija ?? "Ostalo",
                            pol = art?.Pol ?? "Neodređeno",
                            totalUnits,
                            totalRevenue,
                            firstHalfUnits,
                            secondHalfUnits,
                            trendPct,
                            stage,
                            currentStock = art?.Kolicina ?? 0
                        };
                    })
                    .OrderByDescending(x => x.totalRevenue)
                    .ToList();

                var summary = new
                {
                    launch = productGroups.Count(x => x.stage == "LAUNCH"),
                    growth = productGroups.Count(x => x.stage == "GROWTH"),
                    mature = productGroups.Count(x => x.stage == "MATURE"),
                    decline = productGroups.Count(x => x.stage == "DECLINE")
                };

                return Results.Ok(new { items = productGroups.Take(100), summary });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        }).RequireRateLimiting("db-heavy");

        // ─── STOCK DEPLETION FORECAST ─────────────────────────────────────
        group.MapGet("/stock-depletion-forecast", async (
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var from = fromDate.HasValue
                    ? DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow.AddDays(-30);
                var to = toDate.HasValue
                    ? DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow;
                var days = Math.Max(1, (to - from).TotalDays);

                var prodajeIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= from && p.DatumProdaje <= to)
                    .Select(p => p.Id).ToListAsync(ct);

                var salesByProduct = await (
                    from ps in db.ProdajaStavke
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where prodajeIds.Contains(ps.IdProdaja) && a.Kolicina > 0
                    group ps by new { a.Id, a.Naziv, a.Kategorija, a.Kolicina, a.ProdajnaCena, a.NabavnaCena } into g
                    select new
                    {
                        artikalId = g.Key.Id,
                        naziv = g.Key.Naziv,
                        kategorija = g.Key.Kategorija ?? "Ostalo",
                        currentStock = g.Key.Kolicina ?? 0,
                        prodajnaCena = g.Key.ProdajnaCena,
                        nabavnaCena = g.Key.NabavnaCena,
                        totalSold = g.Sum(x => x.Kolicina)
                    }
                ).ToListAsync(ct);

                var forecasts = salesByProduct
                    .Select(p =>
                    {
                        var avgDaily = p.totalSold / days;
                        var daysUntilOOS = avgDaily > 0 ? p.currentStock / avgDaily : 999;
                        var depletionDate = avgDaily > 0
                            ? DateTime.UtcNow.AddDays(daysUntilOOS).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                            : "N/A";
                        var atRiskRevenue = p.prodajnaCena.HasValue
                            ? p.currentStock * p.prodajnaCena.Value : 0;
                        var margin = p.prodajnaCena.HasValue && p.nabavnaCena.HasValue && p.prodajnaCena.Value > 0
                            ? (double)((p.prodajnaCena.Value - p.nabavnaCena.Value) / p.prodajnaCena.Value * 100) : 0;

                        return new
                        {
                            p.artikalId, p.naziv, p.kategorija,
                            p.currentStock,
                            avgDailySales = avgDaily,
                            daysUntilOOS,
                            depletionDate,
                            atRiskRevenue,
                            marginPct = margin,
                            severity = daysUntilOOS < 7 ? "CRITICAL"
                                     : daysUntilOOS < 14 ? "WARNING"
                                     : daysUntilOOS < 30 ? "WATCH" : "OK"
                        };
                    })
                    .Where(x => x.daysUntilOOS < 60)
                    .OrderBy(x => x.daysUntilOOS)
                    .Take(50)
                    .ToList();

                var totalAtRisk = forecasts.Sum(x => x.atRiskRevenue);
                var criticalCount = forecasts.Count(x => x.severity == "CRITICAL");

                return Results.Ok(new { forecasts, totalAtRiskRevenue = totalAtRisk, criticalCount });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        }).RequireRateLimiting("db-heavy");

        // ─── MARGIN PRESSURE ALERTS ──────────────────────────────────────
        group.MapGet("/margin-alerts", async (
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var from = fromDate.HasValue
                    ? DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow.AddDays(-90);
                var to = toDate.HasValue
                    ? DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow;

                var prodajeIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= from && p.DatumProdaje <= to)
                    .Select(p => p.Id).ToListAsync(ct);

                // Products with negative or dangerously low margins
                var products = await (
                    from ps in db.ProdajaStavke
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where prodajeIds.Contains(ps.IdProdaja) && a.NabavnaCena.HasValue
                    group new { ps, a } by new { a.Id, a.Naziv, a.Kategorija, a.NabavnaCena, a.ProdajnaCena, a.PrvaProdajnaCena } into g
                    select new
                    {
                        artikalId = g.Key.Id,
                        naziv = g.Key.Naziv,
                        kategorija = g.Key.Kategorija ?? "Ostalo",
                        nabavnaCena = g.Key.NabavnaCena!.Value,
                        prodajnaCena = g.Key.ProdajnaCena,
                        prvaCena = g.Key.PrvaProdajnaCena,
                        totalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena),
                        totalCost = g.Sum(x => x.ps.Kolicina) * g.Key.NabavnaCena!.Value,
                        totalUnits = g.Sum(x => x.ps.Kolicina)
                    }
                ).ToListAsync(ct);

                var alerts = products
                    .Where(p => p.totalRevenue > 0)
                    .Select(p =>
                    {
                        var marginPct = (double)((p.totalRevenue - p.totalCost) / p.totalRevenue * 100);
                        var priceDropPct = p.prvaCena.HasValue && p.prvaCena.Value > 0 && p.prodajnaCena.HasValue
                            ? (double)((p.prvaCena.Value - p.prodajnaCena.Value) / p.prvaCena.Value * 100) : 0;

                        var alertType = marginPct < 0 ? "NEGATIVE_MARGIN"
                                      : marginPct < 15 ? "LOW_MARGIN"
                                      : priceDropPct > 30 ? "HEAVY_MARKDOWN"
                                      : "OK";

                        return new
                        {
                            p.artikalId, p.naziv, p.kategorija,
                            marginPct,
                            priceDropPct,
                            p.totalRevenue, p.totalUnits,
                            nabavnaCena = p.nabavnaCena,
                            prodajnaCena = p.prodajnaCena ?? 0,
                            alertType,
                            lostMargin = marginPct < 30 ? (30 - marginPct) / 100 * (double)p.totalRevenue : 0
                        };
                    })
                    .Where(x => x.alertType != "OK")
                    .OrderBy(x => x.marginPct)
                    .Take(50)
                    .ToList();

                return Results.Ok(new
                {
                    alerts,
                    summary = new
                    {
                        negativeMarginCount = alerts.Count(x => x.alertType == "NEGATIVE_MARGIN"),
                        lowMarginCount = alerts.Count(x => x.alertType == "LOW_MARGIN"),
                        heavyMarkdownCount = alerts.Count(x => x.alertType == "HEAVY_MARKDOWN"),
                        totalLostMargin = alerts.Sum(x => x.lostMargin)
                    }
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        }).RequireRateLimiting("db-heavy");

        // ─── WEEKLY CHANGELOG ("What Changed This Week") ─────────────────
        group.MapGet("/weekly-changelog", async (
            ITrendplusDbContext db,
            CancellationToken ct = default) =>
        {
            try
            {
                var now = DateTime.UtcNow;
                var weekAgo = now.AddDays(-7);
                var twoWeeksAgo = now.AddDays(-14);

                // This week vs last week
                var thisWeekIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= weekAgo && p.DatumProdaje <= now)
                    .Select(p => p.Id).ToListAsync(ct);
                var lastWeekIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= twoWeeksAgo && p.DatumProdaje < weekAgo)
                    .Select(p => p.Id).ToListAsync(ct);

                var thisWeekRev = thisWeekIds.Count == 0 ? 0m :
                    await db.ProdajaStavke.Where(ps => thisWeekIds.Contains(ps.IdProdaja))
                        .SumAsync(ps => ps.Kolicina * ps.Cena, ct);
                var lastWeekRev = lastWeekIds.Count == 0 ? 0m :
                    await db.ProdajaStavke.Where(ps => lastWeekIds.Contains(ps.IdProdaja))
                        .SumAsync(ps => ps.Kolicina * ps.Cena, ct);
                var thisWeekUnits = thisWeekIds.Count == 0 ? 0 :
                    await db.ProdajaStavke.Where(ps => thisWeekIds.Contains(ps.IdProdaja))
                        .SumAsync(ps => ps.Kolicina, ct);
                var lastWeekUnits = lastWeekIds.Count == 0 ? 0 :
                    await db.ProdajaStavke.Where(ps => lastWeekIds.Contains(ps.IdProdaja))
                        .SumAsync(ps => ps.Kolicina, ct);

                var revChange = lastWeekRev > 0
                    ? (double)((thisWeekRev - lastWeekRev) / lastWeekRev * 100) : 0;
                var unitChange = lastWeekUnits > 0
                    ? (thisWeekUnits - lastWeekUnits) / (double)lastWeekUnits * 100 : 0;

                // Top gainers/losers by category this week vs last
                var thisWeekByCat = await (
                    from ps in db.ProdajaStavke
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where thisWeekIds.Contains(ps.IdProdaja)
                    group ps by a.Kategorija ?? "Ostalo" into g
                    select new { kat = g.Key, rev = g.Sum(x => x.Kolicina * x.Cena) }
                ).ToDictionaryAsync(x => x.kat, x => x.rev, ct);

                var lastWeekByCat = await (
                    from ps in db.ProdajaStavke
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where lastWeekIds.Contains(ps.IdProdaja)
                    group ps by a.Kategorija ?? "Ostalo" into g
                    select new { kat = g.Key, rev = g.Sum(x => x.Kolicina * x.Cena) }
                ).ToDictionaryAsync(x => x.kat, x => x.rev, ct);

                var allCats = thisWeekByCat.Keys.Union(lastWeekByCat.Keys);
                var categoryChanges = allCats.Select(cat =>
                {
                    var tw = thisWeekByCat.GetValueOrDefault(cat, 0);
                    var lw = lastWeekByCat.GetValueOrDefault(cat, 0);
                    var change = lw > 0 ? (double)((tw - lw) / lw * 100) : (tw > 0 ? 100 : 0);
                    return new { kategorija = cat, thisWeekRevenue = tw, lastWeekRevenue = lw, changePct = change };
                })
                .OrderByDescending(x => Math.Abs(x.changePct))
                .ToList();

                // New OOS this week
                var newOosCount = await db.Artikli.Where(a => a.Kolicina == 0).CountAsync(ct);

                // DnevnikPromena (price changes) count this week
                var priceChangesCount = await db.DnevnikPromena
                    .Where(d => d.Datum >= weekAgo && d.Datum <= now && d.TipPromene == "Nivelacija")
                    .CountAsync(ct);

                return Results.Ok(new
                {
                    thisWeekRevenue = thisWeekRev,
                    lastWeekRevenue = lastWeekRev,
                    revenueChangePct = revChange,
                    thisWeekUnits,
                    lastWeekUnits,
                    unitChangePct = unitChange,
                    thisWeekTransactions = thisWeekIds.Count,
                    lastWeekTransactions = lastWeekIds.Count,
                    categoryChanges,
                    oosCount = newOosCount,
                    priceChangesThisWeek = priceChangesCount
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        }).RequireRateLimiting("db-heavy");

        // ─── ENHANCED SUPPLIER SCORING 2.0 ────────────────────────────────
        group.MapGet("/supplier-scoring-v2", async (
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var now = DateTime.UtcNow;
                var fromUtc = fromDate.HasValue
                    ? DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc)
                    : now.AddDays(-90);
                var toUtc = toDate.HasValue
                    ? DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc)
                    : now;
                var days = Math.Max(1, (toUtc - fromUtc).TotalDays);

                var prodajeIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= fromUtc && p.DatumProdaje <= toUtc)
                    .Select(p => p.Id).ToListAsync(ct);

                var stavke = await (
                    from ps in db.ProdajaStavke
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    join d in db.Dobavljaci on a.IDDobavljac equals d.Id into dj
                    from d in dj.DefaultIfEmpty()
                    where prodajeIds.Contains(ps.IdProdaja)
                    select new
                    {
                        DobavljacId = d != null ? d.Id : (int?)null,
                        DobavljacNaziv = d != null ? d.Naziv : "Nepoznato",
                        Revenue = ps.Kolicina * ps.Cena,
                        Cost = a.NabavnaCena.HasValue ? ps.Kolicina * a.NabavnaCena.Value : (decimal?)null,
                        Units = ps.Kolicina,
                        Kategorija = a.Kategorija ?? "Ostalo",
                        ArtikalId = a.Id,
                        StockQty = a.Kolicina ?? 0
                    }
                ).ToListAsync(ct);

                var totalRevenue = stavke.Sum(x => x.Revenue);
                if (totalRevenue == 0)
                    return Results.Ok(new List<object>());

                // Return rate by supplier
                var povracajData = await (
                    from ps in db.PovracajStavke
                    join pz in db.PovracajZaglavlja on ps.IdPovracaj equals pz.Id
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where pz.DatumPovracaja >= fromUtc && pz.DatumPovracaja <= toUtc
                    group ps by a.IDDobavljac into g
                    select new { dobId = g.Key, returnUnits = g.Sum(x => x.Kolicina) }
                ).ToDictionaryAsync(x => x.dobId, x => x.returnUnits, ct);

                var result = stavke
                    .GroupBy(x => new { x.DobavljacId, x.DobavljacNaziv })
                    .Select(g =>
                    {
                        var rev = g.Sum(x => x.Revenue);
                        var wc = g.Where(x => x.Cost.HasValue).ToList();
                        var revWC = wc.Sum(x => x.Revenue);
                        var costSum = wc.Sum(x => x.Cost!.Value);
                        var marginPct = revWC > 0 ? (double)((revWC - costSum) / revWC * 100) : 0;
                        var units = g.Sum(x => x.Units);
                        var cats = g.Select(x => x.Kategorija).Distinct().Count();
                        var products = g.Select(x => x.ArtikalId).Distinct().Count();
                        var dependency = (double)(rev / totalRevenue * 100);
                        var velocity = units / days;
                        var unsoldStock = g.Select(x => new { x.ArtikalId, x.StockQty })
                            .DistinctBy(x => x.ArtikalId).Sum(x => x.StockQty);

                        var returnUnits = g.Key.DobavljacId.HasValue && povracajData.TryGetValue(g.Key.DobavljacId, out var ru) ? ru : 0;
                        var returnRate = units > 0 ? returnUnits / (double)units * 100 : 0;

                        // Scores (0-100)
                        var profitScore = Math.Min(100, marginPct * 2.5);
                        var velocityScore = Math.Min(100, velocity * 20);
                        var diversityScore = Math.Min(100, cats * 15.0);
                        var reliabilityScore = Math.Max(0, 100 - returnRate * 10);
                        var dependencyPenalty = Math.Max(0, dependency - 20) * 2;

                        var composite = profitScore * 0.30
                                      + velocityScore * 0.20
                                      + diversityScore * 0.15
                                      + reliabilityScore * 0.15
                                      - dependencyPenalty * 0.20;
                        composite = Math.Max(0, Math.Min(100, composite));

                        var tier = composite >= 75 ? "GOLD"
                                 : composite >= 50 ? "SILVER"
                                 : composite >= 30 ? "BRONZE" : "AT_RISK";

                        return new
                        {
                            dobavljacId = g.Key.DobavljacId,
                            dobavljacNaziv = g.Key.DobavljacNaziv ?? "Nepoznato",
                            totalRevenue = rev,
                            totalUnits = units,
                            marginPct,
                            uniqueProducts = products,
                            uniqueCategories = cats,
                            dependency,
                            velocity,
                            unsoldStock,
                            returnRate,
                            profitScore,
                            velocityScore,
                            diversityScore,
                            reliabilityScore,
                            compositeScore = composite,
                            tier
                        };
                    })
                    .OrderByDescending(x => x.compositeScore)
                    .ToList();

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        }).RequireRateLimiting("db-heavy");

        // ─── ENHANCED REORDER PLAN WITH SEASONALITY & MARGIN IMPACT ──────
        group.MapGet("/smart-reorder", async (
            ITrendplusDbContext db,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var from = fromDate.HasValue
                    ? DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow.AddDays(-60);
                var to = toDate.HasValue
                    ? DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc)
                    : DateTime.UtcNow;
                var days = Math.Max(1, (to - from).TotalDays);
                const int leadTimeDays = 14;

                var prodajeIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= from && p.DatumProdaje <= to)
                    .Select(p => p.Id).ToListAsync(ct);

                var dobavljaciDict = await db.Dobavljaci
                    .ToDictionaryAsync(d => d.Id, d => d.Naziv ?? "Nepoznato", ct);

                var salesByProduct = await (
                    from ps in db.ProdajaStavke
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where prodajeIds.Contains(ps.IdProdaja)
                    group new { ps, a } by new
                    {
                        a.Id, a.Naziv, a.Kategorija, a.Pol,
                        a.IDDobavljac, a.ProdajnaCena, a.NabavnaCena,
                        a.MinimalnaKolicina, a.Kolicina, a.IDSezona
                    } into g
                    select new
                    {
                        artikalId = g.Key.Id,
                        naziv = g.Key.Naziv,
                        kategorija = g.Key.Kategorija ?? "Ostalo",
                        pol = g.Key.Pol ?? "Neodređeno",
                        dobavljacId = g.Key.IDDobavljac,
                        prodajnaCena = g.Key.ProdajnaCena,
                        nabavnaCena = g.Key.NabavnaCena,
                        minKolicina = g.Key.MinimalnaKolicina ?? 5,
                        currentStock = g.Key.Kolicina ?? 0,
                        sezonaId = g.Key.IDSezona,
                        totalSold = g.Sum(x => x.ps.Kolicina),
                        totalRevenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena)
                    }
                ).ToListAsync(ct);

                var items = salesByProduct.Select(p =>
                {
                    var avgDaily = p.totalSold / days;
                    var doh = avgDaily > 0 ? p.currentStock / avgDaily : 999;
                    var rop = avgDaily * leadTimeDays * 1.5;
                    var needsReorder = p.currentStock <= rop;
                    var recQty = needsReorder
                        ? Math.Max((int)Math.Ceiling(avgDaily * 30) - p.currentStock, 0)
                        : 0;
                    var urgency = doh < 7 ? "KRITIČNO"
                                : doh < 14 ? "HITNO"
                                : doh < 30 ? "PREPORUČUJE SE"
                                : "OK";
                    var margin = p.prodajnaCena.HasValue && p.nabavnaCena.HasValue && p.prodajnaCena.Value > 0
                        ? (double)((p.prodajnaCena.Value - p.nabavnaCena.Value) / p.prodajnaCena.Value * 100) : 0;
                    var reorderCost = p.nabavnaCena.HasValue ? recQty * p.nabavnaCena.Value : 0;
                    var expectedRevenue = p.prodajnaCena.HasValue ? recQty * p.prodajnaCena.Value : 0;
                    var expectedProfit = expectedRevenue - reorderCost;

                    // Reorder probability index: combines velocity + stock urgency + margin
                    var velScore = Math.Min(50, avgDaily * 20);
                    var urgScore = doh < 7 ? 30 : doh < 14 ? 20 : doh < 30 ? 10 : 0;
                    var margScore = Math.Min(20, margin / 5);
                    var reorderProbability = Math.Min(100, velScore + urgScore + margScore);

                    var dobavNaziv = p.dobavljacId.HasValue && dobavljaciDict.TryGetValue(p.dobavljacId.Value, out var dn)
                        ? dn : "Nepoznato";

                    return new
                    {
                        p.artikalId, p.naziv, p.kategorija, p.pol,
                        dobavljacNaziv = dobavNaziv,
                        p.currentStock, p.totalSold,
                        avgDailySales = avgDaily,
                        doh, rop, needsReorder,
                        recommendedQty = recQty,
                        urgency,
                        marginPct = margin,
                        reorderCost,
                        expectedRevenue,
                        expectedProfit,
                        reorderProbability,
                        p.prodajnaCena
                    };
                })
                .OrderByDescending(x => x.reorderProbability)
                .ToList();

                // Group by category for planning windows
                var byCategoryPlan = items
                    .GroupBy(x => x.kategorija)
                    .Select(g => new
                    {
                        kategorija = g.Key,
                        totalItems = g.Count(),
                        criticalCount = g.Count(x => x.urgency == "KRITIČNO"),
                        urgentCount = g.Count(x => x.urgency == "HITNO"),
                        totalReorderCost = g.Sum(x => x.reorderCost),
                        expectedRevenue = g.Sum(x => x.expectedRevenue),
                        avgMargin = g.Where(x => x.marginPct > 0).DefaultIfEmpty().Average(x => x?.marginPct ?? 0)
                    })
                    .OrderByDescending(x => x.criticalCount)
                    .ToList();

                // Group by supplier for supplier planning
                var bySupplierPlan = items
                    .GroupBy(x => x.dobavljacNaziv)
                    .Select(g => new
                    {
                        dobavljac = g.Key,
                        totalItems = g.Count(),
                        criticalCount = g.Count(x => x.urgency == "KRITIČNO"),
                        totalReorderCost = g.Sum(x => x.reorderCost),
                        avgReorderProbability = g.Average(x => x.reorderProbability)
                    })
                    .OrderByDescending(x => x.criticalCount)
                    .ToList();

                var summary = new
                {
                    criticalCount = items.Count(x => x.urgency == "KRITIČNO"),
                    urgentCount = items.Count(x => x.urgency == "HITNO"),
                    recommendedCount = items.Count(x => x.urgency == "PREPORUČUJE SE"),
                    totalReorderCost = items.Sum(x => x.reorderCost),
                    expectedRevenueFromReorder = items.Sum(x => x.expectedRevenue),
                    expectedProfitFromReorder = items.Sum(x => x.expectedProfit)
                };

                return Results.Ok(new { items, byCategoryPlan, bySupplierPlan, summary });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        }).RequireRateLimiting("db-heavy");

        // ─── PRICE SENSITIVITY CLUSTERS ───────────────────────────────────
        group.MapGet("/price-sensitivity", async (
            ITrendplusDbContext db,
            CancellationToken ct = default) =>
        {
            try
            {
                // Group products by price bands and analyze velocity
                var artikli = await (
                    from a in db.Artikli
                    where a.ProdajnaCena.HasValue && a.ProdajnaCena.Value > 0
                    select new
                    {
                        a.Id, a.Naziv, a.Kategorija, a.Pol,
                        a.ProdajnaCena, a.NabavnaCena,
                        a.PrvaProdajnaCena, a.Kolicina
                    }
                ).ToListAsync(ct);

                var last90Days = DateTime.UtcNow.AddDays(-90);
                var prodajeIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= last90Days)
                    .Select(p => p.Id).ToListAsync(ct);

                var salesByProduct = await (
                    from ps in db.ProdajaStavke
                    where prodajeIds.Contains(ps.IdProdaja)
                    group ps by ps.IdArtikal into g
                    select new { artikalId = g.Key, unitsSold = g.Sum(x => x.Kolicina) }
                ).ToDictionaryAsync(x => x.artikalId, x => x.unitsSold, ct);

                // Price bands: 0-3k, 3-6k, 6-10k, 10-15k, 15k+
                string GetPriceBand(decimal price)
                {
                    if (price < 3000) return "0-3k";
                    if (price < 6000) return "3-6k";
                    if (price < 10000) return "6-10k";
                    if (price < 15000) return "10-15k";
                    return "15k+";
                }

                var bands = artikli
                    .GroupBy(a => GetPriceBand(a.ProdajnaCena!.Value))
                    .Select(g =>
                    {
                        var skuCount = g.Count();
                        var totalUnits = g.Sum(a => salesByProduct.GetValueOrDefault(a.Id, 0));
                        var avgVelocity = skuCount > 0 ? totalUnits / 90.0 / skuCount : 0;
                        var avgPrice = g.Average(a => (double)a.ProdajnaCena!.Value);
                        var withCost = g.Where(a => a.NabavnaCena.HasValue).ToList();
                        var avgMargin = withCost.Count > 0
                            ? withCost.Average(a => (double)((a.ProdajnaCena!.Value - a.NabavnaCena!.Value) / a.ProdajnaCena.Value * 100))
                            : 0;
                        var totalStock = g.Sum(a => a.Kolicina ?? 0);

                        // Markdown pressure: how many dropped >20% from original price
                        var markdownCount = g.Count(a =>
                            a.PrvaProdajnaCena.HasValue && a.PrvaProdajnaCena.Value > 0 &&
                            (double)((a.PrvaProdajnaCena.Value - a.ProdajnaCena!.Value) / a.PrvaProdajnaCena.Value * 100) > 20);

                        return new
                        {
                            priceBand = g.Key,
                            skuCount,
                            totalUnits,
                            avgVelocityPerSku = avgVelocity,
                            avgPrice,
                            avgMarginPct = avgMargin,
                            totalStock,
                            markdownCount,
                            elasticity = avgVelocity > 0 ? "HIGH_DEMAND" : "LOW_DEMAND"
                        };
                    })
                    .OrderBy(x => x.avgPrice)
                    .ToList();

                return Results.Ok(new { bands });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        }).RequireRateLimiting("db-heavy");
    }
}
