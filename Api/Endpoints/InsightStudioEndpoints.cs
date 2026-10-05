using Application.Artikli.Common.Interfaces;
using Application.Analytics;
using Api.Endpoints;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Trendplus2.Dtos;

namespace Trendplus2.Endpoints;

/// <summary>
/// Insight Studio â€” Napredna Analitika 2
/// KPI Snapshot, Supplier Scorecard, ABC Classification, Aging Stock,
/// Daily Analysis (Z-score), Category Intelligence, Reorder Plan
/// </summary>
public static class InsightStudioEndpoints
{
    public static void MapInsightStudioEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/analytics/advanced")
            .WithTags("Insight Studio");

        // â”€â”€â”€ KPI COMMAND ROW â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        group.MapGet("/kpi-snapshot", async (
            ITrendplusDbContext db,
            HttpContext httpContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var now = DateTime.UtcNow;
                var (from, toExclusive) = InsightStudioPeriod.ResolveInclusiveUtcRange(
                    fromDate, toDate, now.AddDays(-30), now);
                var span = (toExclusive - from).TotalDays;
                var prevFrom = from.AddDays(-span);

                var currentIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= from && p.DatumProdaje < toExclusive)
                    .Select(p => p.Id).ToListAsync(ct);

                var prevIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= prevFrom && p.DatumProdaje < from)
                    .Select(p => p.Id).ToListAsync(ct);

                var currentRevenue = currentIds.Count == 0 ? 0m :
                    await db.ProdajaStavke.Where(ps => currentIds.Contains(ps.IdProdaja))
                        .SumAsync(ps => ps.Kolicina * ps.Cena, ct);

                var prevRevenue = prevIds.Count == 0 ? 0m :
                    await db.ProdajaStavke.Where(ps => prevIds.Contains(ps.IdProdaja))
                        .SumAsync(ps => ps.Kolicina * ps.Cena, ct);

                var currentUnits = currentIds.Count == 0 ? 0 :
                    await db.ProdajaStavke.Where(ps => currentIds.Contains(ps.IdProdaja))
                        .SumAsync(ps => ps.Kolicina, ct);

                var prevUnits = prevIds.Count == 0 ? 0 :
                    await db.ProdajaStavke.Where(ps => prevIds.Contains(ps.IdProdaja))
                        .SumAsync(ps => ps.Kolicina, ct);

                var withCostData = await (
                    from ps in db.ProdajaStavke
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where currentIds.Contains(ps.IdProdaja)
                    select new
                    {
                        Revenue = ps.Kolicina * ps.Cena,
                        Quantity = ps.Kolicina,
                        SaleLineCost = ps.NabavnaCena,
                        ProductCostRsd = a.NabavnaCenaDin,
                        ProductCostLegacy = a.NabavnaCena
                    }
                ).ToListAsync(ct);

                var margin = new MarginAccumulator();
                foreach (var row in withCostData)
                {
                    margin.Add(
                        row.Revenue,
                        row.Quantity,
                        AnalyticsMarginPolicy.ResolveUnitCost(row.SaleLineCost, row.ProductCostRsd, row.ProductCostLegacy));
                }

                var marginSnapshot = margin.Build(currentRevenue);
                var marginPct = marginSnapshot.MarginPct;

                var oosCount = await db.Artikli.Where(a => a.Kolicina == 0).CountAsync(ct);
                var lowStockCount = await db.Artikli
                    .Where(a => a.Kolicina > 0 && a.Kolicina <= a.MinimalnaKolicina)
                    .CountAsync(ct);

                // Sparkline: daily revenue (up to 30 points)
                var allCurrentStavke = await db.ProdajaStavke
                    .Where(ps => currentIds.Contains(ps.IdProdaja))
                    .Select(ps => new { ps.IdProdaja, ps.Kolicina, ps.Cena })
                    .ToListAsync(ct);

                var dailyGroups = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= from && p.DatumProdaje < toExclusive)
                    .GroupBy(p => p.DatumProdaje.Date)
                    .Select(g => new { Date = g.Key, Ids = g.Select(x => x.Id).ToList() })
                    .ToListAsync(ct);

                var sparkline = dailyGroups
                    .OrderBy(d => d.Date)
                    .Select(d => new
                    {
                        date = d.Date.ToString("MM-dd", CultureInfo.InvariantCulture),
                        revenue = allCurrentStavke.Where(s => d.Ids.Contains(s.IdProdaja)).Sum(s => s.Kolicina * s.Cena)
                    })
                    .ToList();

                var revenueChange = prevRevenue > 0
                    ? (double)((currentRevenue - prevRevenue) / prevRevenue * 100) : 0;
                var unitsChange = prevUnits > 0
                    ? (currentUnits - prevUnits) / (double)prevUnits * 100 : 0;

                return Results.Ok(new
                {
                    revenue = currentRevenue,
                    revenueChange,
                    units = currentUnits,
                    unitsChange,
                    transactions = currentIds.Count,
                    marginPct,
                    marginDataCoveragePct = marginSnapshot.MarginDataCoveragePct,
                    revenueWithCost = marginSnapshot.RevenueWithCost,
                    oosCount,
                    lowStockCount,
                    sparkline
                });
            }
            catch (Exception ex)
            {
                return await CreateSafeErrorResponseAsync(httpContext, ex, "Greška KPI snapshot", ct);
            }
        }).RequireRateLimiting("db-heavy");

        // â”€â”€â”€ SUPPLIER SCORECARD â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        group.MapGet("/supplier-scorecard", async (
            ITrendplusDbContext db,
            HttpContext httpContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var now = DateTime.UtcNow;
                var (from, toExclusive) = InsightStudioPeriod.ResolveInclusiveUtcRange(
                    fromDate, toDate, now.AddDays(-90), now);

                var prodajeIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= from && p.DatumProdaje < toExclusive)
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
                        Units = ps.Kolicina,
                        SaleLineCost = ps.NabavnaCena,
                        ProductCostRsd = a.NabavnaCenaDin,
                        ProductCostLegacy = a.NabavnaCena,
                        Kategorija = a.Kategorija ?? "Ostalo",
                        ArtikalId = a.Id
                    }
                ).ToListAsync(ct);

                var totalRevenue = stavke.Sum(x => x.Revenue);
                var systemMargin = new MarginAccumulator();
                foreach (var row in stavke)
                {
                    systemMargin.Add(
                        row.Revenue,
                        row.Units,
                        AnalyticsMarginPolicy.ResolveUnitCost(row.SaleLineCost, row.ProductCostRsd, row.ProductCostLegacy));
                }

                var systemMarginSnapshot = systemMargin.Build(totalRevenue);
                double? systemMarginPct = systemMarginSnapshot.RevenueWithCost > 0m
                    ? systemMarginSnapshot.MarginPct
                    : null;
                var systemBenchmarkAvailable = systemMarginPct.HasValue;
                var totalCategories = stavke.Select(x => x.Kategorija).Distinct().Count();

                var result = stavke
                    .GroupBy(x => new { x.DobavljacId, x.DobavljacNaziv })
                    .Select(g =>
                    {
                        var rev = g.Sum(x => x.Revenue);
                        var margin = new MarginAccumulator();
                        foreach (var row in g)
                        {
                            margin.Add(
                                row.Revenue,
                                row.Units,
                                AnalyticsMarginPolicy.ResolveUnitCost(row.SaleLineCost, row.ProductCostRsd, row.ProductCostLegacy));
                        }

                        var marginSnapshot = margin.Build(rev);
                        var marginPct = marginSnapshot.MarginPct;
                        var units = g.Sum(x => x.Units);
                        var cats = g.Select(x => x.Kategorija).Distinct().Count();
                        var products = g.Select(x => x.ArtikalId).Distinct().Count();
                        var dependency = totalRevenue > 0 ? (double)(rev / totalRevenue * 100) : 0;

                        var profitScore = Math.Min(100,
                            (systemBenchmarkAvailable && systemMarginPct > 0
                                ? (marginPct / systemMarginPct!.Value) * 50
                                : 0) +
                            (totalRevenue > 0 ? (double)(rev / totalRevenue) * 50 : 0));
                        var diversityScore = totalCategories > 0 ? (double)cats / totalCategories * 100 : 50;
                        var dependencyScore = Math.Max(0, 100 - dependency * 2);
                        var compositeScore = profitScore * 0.35 + diversityScore * 0.25 + dependencyScore * 0.4;
                        var riskLevel = dependency > 30 ? "HIGH" : dependency > 15 ? "MED" : "LOW";

                        return new
                        {
                            dobavljacId = g.Key.DobavljacId,
                            dobavljacNaziv = g.Key.DobavljacNaziv ?? "Nepoznato",
                            totalRevenue = rev,
                            totalUnits = units,
                            marginPct,
                            marginDataCoveragePct = marginSnapshot.MarginDataCoveragePct,
                            revenueWithCost = marginSnapshot.RevenueWithCost,
                            uniqueProducts = products,
                            uniqueCategories = cats,
                            dependencyRatio = dependency,
                            profitScore,
                            diversityScore,
                            dependencyScore,
                            compositeScore,
                            riskLevel,
                            systemBenchmarkAvailable,
                            systemMarginPct
                        };
                    })
                    .OrderByDescending(x => x.totalRevenue)
                    .ToList();

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return await CreateSafeErrorResponseAsync(httpContext, ex, "Greška supplier scorecard", ct);
            }
        }).RequireRateLimiting("db-heavy");

        // â”€â”€â”€ ABC CLASSIFICATION â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        group.MapGet("/abc-classification", async (
            ITrendplusDbContext db,
            HttpContext httpContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var now = DateTime.UtcNow;
                var (from, toExclusive) = InsightStudioPeriod.ResolveInclusiveUtcRange(
                    fromDate, toDate, now.AddDays(-90), now);

                var prodajeIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= from && p.DatumProdaje < toExclusive)
                    .Select(p => p.Id).ToListAsync(ct);

                if (prodajeIds.Count == 0)
                {
                    return Results.Ok(new
                    {
                        items = new List<object>(),
                        summary = new { countA = 0, countB = 0, countC = 0 },
                        meta = AnalyticsResponseMetaFactory.Empty(
                            "no_sales_in_period",
                            "Nema prodaje u traženom periodu za ABC klasifikaciju.")
                    });
                }

                var productSales = await (
                    from ps in db.ProdajaStavke
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where prodajeIds.Contains(ps.IdProdaja)
                    group ps by new { a.Id, a.Naziv, a.Kategorija, a.Pol } into g
                    orderby g.Sum(x => x.Kolicina * x.Cena) descending
                    select new
                    {
                        artikalId = g.Key.Id,
                        naziv = g.Key.Naziv,
                        kategorija = g.Key.Kategorija ?? "Ostalo",
                        pol = g.Key.Pol ?? "Neodređeno",
                        totalRevenue = g.Sum(x => x.Kolicina * x.Cena),
                        totalUnits = g.Sum(x => x.Kolicina)
                    }
                ).ToListAsync(ct);

                var total = productSales.Sum(x => x.totalRevenue);
                if (total == 0)
                {
                    return Results.Ok(new
                    {
                        items = new List<object>(),
                        summary = new { countA = 0, countB = 0, countC = 0 },
                        meta = AnalyticsResponseMetaFactory.Empty(
                            "zero_revenue_in_period",
                            "Prodaja u periodu ima nula prihoda za ABC klasifikaciju.")
                    });
                }

                decimal cumulative = 0;
                var items = productSales.Select(p =>
                {
                    cumulative += p.totalRevenue;
                    var cumPct = (double)(cumulative / total * 100);
                    var revPct = (double)(p.totalRevenue / total * 100);
                    var cls = cumPct <= 70 ? "A" : cumPct <= 90 ? "B" : "C";
                    return new
                    {
                        p.artikalId, p.naziv, p.kategorija, p.pol,
                        p.totalRevenue, p.totalUnits, revPct,
                        cumulativePct = cumPct, abcClass = cls
                    };
                }).ToList();

                var summary = new
                {
                    countA = items.Count(x => x.abcClass == "A"),
                    countB = items.Count(x => x.abcClass == "B"),
                    countC = items.Count(x => x.abcClass == "C"),
                    revenueA = items.Where(x => x.abcClass == "A").Sum(x => x.totalRevenue),
                    revenueB = items.Where(x => x.abcClass == "B").Sum(x => x.totalRevenue),
                    revenueC = items.Where(x => x.abcClass == "C").Sum(x => x.totalRevenue)
                };

                return Results.Ok(new { items, summary });
            }
            catch (Exception ex)
            {
                return await CreateSafeErrorResponseAsync(httpContext, ex, "Greška ABC klasifikacija", ct);
            }
        }).RequireRateLimiting("db-heavy");

        // â”€â”€â”€ AGING STOCK â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        group.MapGet("/aging-stock", async (
            ITrendplusDbContext db,
            HttpContext httpContext,
            CancellationToken ct = default) =>
        {
            try
            {
                var artikli = await (
                    from a in db.Artikli
                    where a.Kolicina > 0
                    select new
                    {
                        a.Id, a.Naziv,
                        a.Kategorija, a.Pol,
                        a.Kolicina, a.NabavnaCenaDin, a.NabavnaCena,
                        a.SourceUpdatedAtUtc, a.UpdatedAt, a.IDDobavljac
                    }
                ).ToListAsync(ct);

                var artikalIds = artikli.Select(a => a.Id).ToList();

                // Last sale date per product using join
                var lastSales = await (
                    from ps in db.ProdajaStavke
                    join p in db.ProdajaZaglavlja on ps.IdProdaja equals p.Id
                    where artikalIds.Contains(ps.IdArtikal)
                    group p.DatumProdaje by ps.IdArtikal into g
                    select new { artikalId = g.Key, lastSale = g.Max() }
                ).ToListAsync(ct);

                var lastSaleDict = lastSales.ToDictionary(x => x.artikalId, x => x.lastSale);

                var dobavljaciIds = artikli
                    .Where(a => a.IDDobavljac.HasValue)
                    .Select(a => a.IDDobavljac!.Value)
                    .Distinct()
                    .ToList();

                var dobavljaciDict = await db.Dobavljaci
                    .Where(d => dobavljaciIds.Contains(d.Id))
                    .ToDictionaryAsync(d => d.Id, d => d.Naziv ?? "Nepoznato", ct);

                var today = DateTime.UtcNow.Date;

                var items = artikli.Select(a =>
                {
                    var hasSale = lastSaleDict.TryGetValue(a.Id, out var lastSale);
                    var neverSold = !hasSale;
                    var agingReferenceDate = hasSale
                        ? lastSale.Date
                        : a.SourceUpdatedAtUtc?.Date ?? a.UpdatedAt.Date;
                    var days = (today - agingReferenceDate).Days;
                    var aging = days < 30 ? "Aktivno" :
                                days < 60 ? "Pazi" :
                                days < 90 ? "Upozorenje" : "Kritično";
                    var resolvedUnitCost = AnalyticsMarginPolicy.ResolveProductUnitCost(a.NabavnaCenaDin, a.NabavnaCena);
                    var stockVal = resolvedUnitCost.HasValue ? a.Kolicina * resolvedUnitCost.Value : (decimal?)null;
                    var dobavNaziv = a.IDDobavljac.HasValue && dobavljaciDict.TryGetValue(a.IDDobavljac.Value, out var dn1)
                        ? dn1 : "Nepoznato";

                    return new
                    {
                        a.Id, a.Naziv,
                        kategorija = a.Kategorija ?? "Ostalo",
                        pol = a.Pol ?? "Neodređeno",
                        kolicina = a.Kolicina ?? 0,
                        stockValue = stockVal,
                        marginDataAvailable = resolvedUnitCost.HasValue,
                        dobavljacNaziv = dobavNaziv,
                        neverSold,
                        agingEvidenceStatus = neverSold ? "never_sold" : "last_sale",
                        lastSaleDate = hasSale
                            ? lastSale.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                            : null,
                        daysWithoutSale = days,
                        agingCategory = aging
                    };
                }).OrderByDescending(x => x.daysWithoutSale).ToList();

                var summary = new
                {
                    totalSKU = items.Count,
                    critical = items.Count(x => x.agingCategory == "Kritično"),
                    warning = items.Count(x => x.agingCategory == "Upozorenje"),
                    watch = items.Count(x => x.agingCategory == "Pazi"),
                    active = items.Count(x => x.agingCategory == "Aktivno"),
                    criticalStockValue = items.Where(x => x.agingCategory == "Kritično")
                        .Sum(x => x.stockValue ?? 0)
                };

                return Results.Ok(new { items, summary });
            }
            catch (Exception ex)
            {
                return await CreateSafeErrorResponseAsync(httpContext, ex, "Greška aging stock", ct);
            }
        }).RequireRateLimiting("db-heavy");

        // â”€â”€â”€ DAILY ANALYSIS (Z-score / outlier detection) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        group.MapGet("/daily-analysis", async (
            ITrendplusDbContext db,
            HttpContext httpContext,
            DateTime? analysisDate = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var targetDate = (analysisDate ?? DateTime.UtcNow.AddDays(-1)).Date;
                var (from, toExclusive) = InsightStudioPeriod.ResolveInclusiveUtcRange(
                    fromDate, toDate, targetDate.AddDays(-60), targetDate);

                var prodajeAll = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= from && p.DatumProdaje < toExclusive)
                    .ToListAsync(ct);

                var allIds = prodajeAll.Select(p => p.Id).ToList();
                var allStavkeRaw = allIds.Count == 0
                    ? new List<(int IdProdaja, int Kolicina, decimal Cena)>()
                    : (await db.ProdajaStavke.Where(ps => allIds.Contains(ps.IdProdaja))
                        .Select(ps => new { ps.IdProdaja, ps.Kolicina, ps.Cena })
                        .ToListAsync(ct))
                        .Select(x => (IdProdaja: x.IdProdaja, Kolicina: x.Kolicina, Cena: x.Cena))
                        .ToList();

                var dailyData = prodajeAll
                    .GroupBy(p => p.DatumProdaje.Date)
                    .Select(g =>
                    {
                        var ids = g.Select(x => x.Id).ToList();
                        var rev = allStavkeRaw.Where(s => ids.Contains(s.IdProdaja)).Sum(s => s.Kolicina * s.Cena);
                        var units = allStavkeRaw.Where(s => ids.Contains(s.IdProdaja)).Sum(s => s.Kolicina);
                        return new { date = g.Key, revenue = rev, units };
                    })
                    .OrderBy(x => x.date)
                    .ToList();

                var targetDay = dailyData.FirstOrDefault(d => d.date == targetDate);
                var hasTargetDayEvidence = prodajeAll.Any(p => p.DatumProdaje.Date == targetDate);
                var baselineDays = dailyData.Where(d => d.date != targetDate).ToList();
                var revenues = baselineDays.Select(x => (double)x.revenue).ToArray();
                var mean = revenues.Length > 0 ? revenues.Average() : 0;
                var stdDev = revenues.Length > 1
                    ? Math.Sqrt(revenues.Sum(r => Math.Pow(r - mean, 2)) / (revenues.Length - 1))
                    : 0;
                var baselineSampleSize = revenues.Length;
                var baselineWarning = baselineSampleSize < 7 ? "insufficient_baseline_sample" : null;
                double? zScore = null;
                if (hasTargetDayEvidence && targetDay != null && stdDev > 0)
                    zScore = ((double)targetDay.revenue - mean) / stdDev;
                else if (hasTargetDayEvidence && targetDay != null)
                    zScore = 0;

                // Top 5 articles for target date
                var targetIds = prodajeAll.Where(p => p.DatumProdaje.Date == targetDate).Select(p => p.Id).ToList();
                var top5 = new List<object>();
                if (targetIds.Count > 0)
                {
                    top5 = await (
                        from ps in db.ProdajaStavke
                        join a in db.Artikli on ps.IdArtikal equals a.Id
                        where targetIds.Contains(ps.IdProdaja)
                        group new { ps, a } by new { a.Id, a.Naziv, a.Kategorija } into g
                        orderby g.Sum(x => x.ps.Kolicina * x.ps.Cena) descending
                        select (object)new
                        {
                            artikalId = g.Key.Id,
                            naziv = g.Key.Naziv,
                            kategorija = g.Key.Kategorija ?? "Ostalo",
                            units = g.Sum(x => x.ps.Kolicina),
                            revenue = g.Sum(x => x.ps.Kolicina * x.ps.Cena)
                        }
                    ).Take(5).ToListAsync(ct);
                }

                var targetDataStatus = hasTargetDayEvidence ? "present" : "missing";
                var outlierLabel = !hasTargetDayEvidence
                    ? "Nema podataka za ciljni dan"
                    : zScore is null
                        ? "Nedovoljna osnova za outlier"
                        : Math.Abs(zScore.Value) > 3 ? "Ekstremni outlier"
                        : Math.Abs(zScore.Value) > 2 ? "Outlier"
                        : "Normalan dan";

                return Results.Ok(new
                {
                    analysisDate = targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    targetDataStatus,
                    targetRevenue = hasTargetDayEvidence ? targetDay?.revenue ?? 0 : (decimal?)null,
                    targetUnits = hasTargetDayEvidence ? targetDay?.units ?? 0 : (int?)null,
                    meanRevenue = (decimal)mean,
                    baselineSampleSize,
                    baselineWarning,
                    zScore,
                    isOutlier = zScore.HasValue && Math.Abs(zScore.Value) > 2,
                    isExtremeOutlier = zScore.HasValue && Math.Abs(zScore.Value) > 3,
                    outlierLabel,
                    dailyData = dailyData.Select(d => new
                    {
                        date = d.date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        revenue = d.revenue,
                        units = d.units,
                        isTarget = d.date == targetDate
                    }),
                    top5Articles = top5
                });
            }
            catch (Exception ex)
            {
                return await CreateSafeErrorResponseAsync(httpContext, ex, "Greška daily analysis", ct);
            }
        }).RequireRateLimiting("db-heavy");

        // â”€â”€â”€ CATEGORY INTELLIGENCE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        group.MapGet("/category-intelligence", async (
            ITrendplusDbContext db,
            HttpContext httpContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var now = DateTime.UtcNow;
                var (from, toExclusive) = InsightStudioPeriod.ResolveInclusiveUtcRange(
                    fromDate, toDate, now.AddDays(-90), now);
                var days = Math.Max(1, (toExclusive - from).TotalDays);

                var prodajeIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= from && p.DatumProdaje < toExclusive)
                    .Select(p => p.Id).ToListAsync(ct);

                var stavke = await (
                    from ps in db.ProdajaStavke
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where prodajeIds.Contains(ps.IdProdaja)
                    select new
                    {
                        Kategorija = a.Kategorija ?? "Ostalo",
                        Pol = a.Pol ?? "Neodređeno",
                        Revenue = ps.Kolicina * ps.Cena,
                        Units = ps.Kolicina,
                        SaleLineCost = ps.NabavnaCena,
                        ProductCostRsd = a.NabavnaCenaDin,
                        ProductCostLegacy = a.NabavnaCena,
                        ArtikalId = a.Id
                    }
                ).ToListAsync(ct);

                var totalRevenue = stavke.Sum(x => x.Revenue);
                var systemMargin = new MarginAccumulator();
                foreach (var row in stavke)
                {
                    systemMargin.Add(
                        row.Revenue,
                        row.Units,
                        AnalyticsMarginPolicy.ResolveUnitCost(row.SaleLineCost, row.ProductCostRsd, row.ProductCostLegacy));
                }

                var systemMarginSnapshot = systemMargin.Build(totalRevenue);
                double? systemMarginPct = systemMarginSnapshot.RevenueWithCost > 0m
                    ? systemMarginSnapshot.MarginPct
                    : null;
                var systemBenchmarkAvailable = systemMarginPct.HasValue;

                var avgStockByKat = await db.Artikli
                    .GroupBy(a => a.Kategorija ?? "Ostalo")
                    .Select(g => new { kat = g.Key, avg = g.Average(a => (double)(a.Kolicina ?? 0)) })
                    .ToDictionaryAsync(x => x.kat, x => x.avg, ct);

                var byCategory = stavke
                    .GroupBy(x => x.Kategorija)
                    .Select(g =>
                    {
                        var rev = g.Sum(x => x.Revenue);
                        var units = g.Sum(x => x.Units);
                        var margin = new MarginAccumulator();
                        foreach (var row in g)
                        {
                            margin.Add(
                                row.Revenue,
                                row.Units,
                                AnalyticsMarginPolicy.ResolveUnitCost(row.SaleLineCost, row.ProductCostRsd, row.ProductCostLegacy));
                        }

                        var marginSnapshot = margin.Build(rev);
                        var marginPct = marginSnapshot.MarginPct;
                        double? profitLift = systemBenchmarkAvailable && systemMarginPct > 0
                            ? (marginPct - systemMarginPct!.Value) / systemMarginPct.Value * 100
                            : null;
                        var revShare = totalRevenue > 0 ? (double)(rev / totalRevenue * 100) : 0;
                        var avgStk = avgStockByKat.TryGetValue(g.Key, out var s) ? s : 1;
                        var velocity = avgStk > 0 ? units / days / Math.Max(avgStk, 0.1) : 0;

                        return new
                        {
                            kategorija = g.Key,
                            totalRevenue = rev,
                            totalUnits = units,
                            marginPct,
                            marginDataCoveragePct = marginSnapshot.MarginDataCoveragePct,
                            revenueWithCost = marginSnapshot.RevenueWithCost,
                            profitLift,
                            revShare,
                            velocity,
                            velocityDenominatorBasis = "period_sales_over_current_catalog_average_stock",
                            uniqueSKU = g.Select(x => x.ArtikalId).Distinct().Count()
                        };
                    })
                    .OrderByDescending(x => x.totalRevenue)
                    .ToList();

                var byGender = stavke
                    .GroupBy(x => x.Pol)
                    .Select(g => new
                    {
                        pol = g.Key,
                        totalRevenue = g.Sum(x => x.Revenue),
                        totalUnits = g.Sum(x => x.Units),
                        revShare = totalRevenue > 0 ? (double)(g.Sum(x => x.Revenue) / totalRevenue * 100) : 0
                    })
                    .OrderByDescending(x => x.totalRevenue)
                    .ToList();

                var categoryDimensionCoverage = CategoricalDimensionCoveragePolicy.ResolveFromRevenueBuckets(
                    byCategory.Select(row => ((decimal)row.totalRevenue, CategoricalDimensionCoveragePolicy.IsUnknownCategory(row.kategorija))));

                return Results.Ok(new
                {
                    byCategory,
                    byGender,
                    velocityDenominatorBasis = "period_sales_over_current_catalog_average_stock",
                    systemBenchmarkAvailable,
                    systemMarginPct,
                    dimensionCoverage = new
                    {
                        category = categoryDimensionCoverage
                    }
                });
            }
            catch (Exception ex)
            {
                return await CreateSafeErrorResponseAsync(httpContext, ex, "Greška category intelligence", ct);
            }
        }).RequireRateLimiting("db-heavy");

        // â”€â”€â”€ REORDER PLAN â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        group.MapGet("/reorder-plan", async (
            ITrendplusDbContext db,
            HttpContext httpContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default) =>
        {
            try
            {
                var now = DateTime.UtcNow;
                var (from, toExclusive) = InsightStudioPeriod.ResolveInclusiveUtcRange(
                    fromDate, toDate, now.AddDays(-30), now);
                var days = Math.Max(1, (toExclusive - from).TotalDays);
                const int leadTimeDays = 14;

                var prodajeIds = await db.ProdajaZaglavlja
                    .Where(p => p.DatumProdaje >= from && p.DatumProdaje < toExclusive)
                    .Select(p => p.Id).ToListAsync(ct);

                var salesByProduct = await (
                    from ps in db.ProdajaStavke
                    join a in db.Artikli on ps.IdArtikal equals a.Id
                    where prodajeIds.Contains(ps.IdProdaja)
                    group ps by new
                    {
                        a.Id, a.Naziv, a.Kategorija, a.Pol,
                        a.IDDobavljac, a.ProdajnaCena, a.MinimalnaKolicina, a.Kolicina,
                        a.NabavnaCenaDin, a.NabavnaCena
                    }
                    into g
                    select new
                    {
                        artikalId = g.Key.Id,
                        naziv = g.Key.Naziv,
                        kategorija = g.Key.Kategorija ?? "Ostalo",
                        pol = g.Key.Pol ?? "Neodređeno",
                        dobavljacId = g.Key.IDDobavljac,
                        prodajnaCena = g.Key.ProdajnaCena,
                        minKolicina = g.Key.MinimalnaKolicina ?? 5,
                        currentStock = g.Key.Kolicina ?? 0,
                        nabavnaCenaDin = g.Key.NabavnaCenaDin,
                        nabavnaCena = g.Key.NabavnaCena,
                        totalSold = g.Sum(x => x.Kolicina),
                        totalRevenue = g.Sum(x => x.Kolicina * x.Cena)
                    }
                ).ToListAsync(ct);

                var dobavljaciDict = await db.Dobavljaci
                    .ToDictionaryAsync(d => d.Id, d => d.Naziv ?? "Nepoznato", ct);

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
                    var dobavNaziv = p.dobavljacId.HasValue && dobavljaciDict.TryGetValue(p.dobavljacId.Value, out var dn2)
                        ? dn2 : "Nepoznato";
                    var unitCost = AnalyticsMarginPolicy.ResolveProductUnitCost(p.nabavnaCenaDin, p.nabavnaCena);
                    // Keep missing selling price unavailable (do not coerce to 0 revenue).
                    decimal? potentialRevenueRsd = !needsReorder
                        ? 0m
                        : p.prodajnaCena.HasValue
                            ? recQty * p.prodajnaCena.Value
                            : null;
                    var estimatedProcurementCostRsd = needsReorder && unitCost.HasValue
                        ? recQty * unitCost.Value
                        : (decimal?)null;

                    return new
                    {
                        p.artikalId, p.naziv, p.kategorija, p.pol,
                        dobavljacNaziv = dobavNaziv,
                        p.currentStock,
                        p.totalSold,
                        avgDailySales = avgDaily,
                        doh,
                        rop,
                        needsReorder,
                        recommendedQty = recQty,
                        urgency,
                        p.prodajnaCena,
                        potentialRevenueRsd,
                        estimatedProcurementCostRsd,
                        costCoveragePct = unitCost.HasValue ? 100m : 0m
                    };
                })
                .OrderBy(x => x.doh)
                .ToList();

                var reorderCandidates = items.Where(x => x.needsReorder).ToList();
                var revenueValues = reorderCandidates.Select(x => x.potentialRevenueRsd).ToList();
                var potentialRevenueRsd = revenueValues.Count == 0
                    ? 0m
                    : revenueValues.Any(x => !x.HasValue)
                        ? (decimal?)null
                        : revenueValues.Sum(x => x!.Value);
                var estimatedProcurementCostRsd = reorderCandidates
                    .Where(x => x.estimatedProcurementCostRsd.HasValue)
                    .Sum(x => x.estimatedProcurementCostRsd!.Value);
                var costCoveredCount = reorderCandidates.Count(x => x.estimatedProcurementCostRsd.HasValue);
                var summary = new
                {
                    criticalCount = items.Count(x => x.urgency == "KRITIČNO"),
                    urgentCount = items.Count(x => x.urgency == "HITNO"),
                    recommendedCount = items.Count(x => x.urgency == "PREPORUČUJE SE"),
                    potentialRevenueRsd,
                    estimatedProcurementCostRsd = costCoveredCount > 0 ? estimatedProcurementCostRsd : (decimal?)null,
                    costCoveragePct = reorderCandidates.Count == 0
                        ? 0m
                        : Math.Round((decimal)costCoveredCount / reorderCandidates.Count * 100m, 2),
                    reorderValueBasis = potentialRevenueRsd is null
                        ? "unavailable"
                        : "potential_revenue_at_selling_price",
                    totalReorderValue = potentialRevenueRsd
                };

                return Results.Ok(new { items, summary });
            }
            catch (Exception ex)
            {
                return await CreateSafeErrorResponseAsync(httpContext, ex, "Greška reorder plan", ct);
            }
        }).RequireRateLimiting("db-heavy");
    }

    private static async Task<IResult> CreateSafeErrorResponseAsync(
        HttpContext httpContext,
        Exception exception,
        string title,
        CancellationToken ct)
    {
        await HandledErrorLogging.PersistHandledExceptionAsync(
            httpContext,
            exception,
            "Insight Studio endpoint failed",
            ct);

        return Results.Problem(
            detail: "Insight Studio trenutno nije dostupan. Pokušajte ponovo ili kontaktirajte podršku.",
            statusCode: StatusCodes.Status500InternalServerError,
            title: title);
    }
}
