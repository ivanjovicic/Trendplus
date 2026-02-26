using Api.Models;
using Api.Services;
using Infrastructure.DbContexts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Trendplus2.Endpoints;

public static class PreNivelacijaPriorityEndpoints
{
    private sealed class SeasonLite
    {
        public string Naziv { get; init; } = "N/A";
        public DateTime DatumOd { get; init; }
        public DateTime DatumDo { get; init; }
    }

    public static void MapPreNivelacijaPriorityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analytics/pre-nivelacija-prioriteti", async (
            TrendplusDbContext db,
            IPreNivelacijaScoringService scoring,
            int? supplierId = null,
            int? seasonId = null,
            int? footwearTypeId = null,
            int? stockMin = null,
            int? stockMax = null,
            int? noSaleDaysMin = null,
            decimal? minScore = null,
            decimal? marginFloor = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 20);

            var maxSaleDate = await db.ProdajaZaglavlja
                .AsNoTracking()
                .MaxAsync(x => (DateTime?)x.DatumProdaje, ct) ?? DateTime.UtcNow;
            var from180 = maxSaleDate.Date.AddDays(-180);
            var from7 = maxSaleDate.Date.AddDays(-6);
            var fromPrev7 = maxSaleDate.Date.AddDays(-13);
            var toPrev7 = maxSaleDate.Date.AddDays(-7);

            var baseArtikliQuery = db.Artikli
                .AsNoTracking()
                .Where(a => (a.Kolicina ?? 0) > 0);

            if (supplierId.HasValue)
                baseArtikliQuery = baseArtikliQuery.Where(a => a.IDDobavljac == supplierId.Value);
            if (seasonId.HasValue)
                baseArtikliQuery = baseArtikliQuery.Where(a => a.IDSezona == seasonId.Value);
            if (footwearTypeId.HasValue)
                baseArtikliQuery = baseArtikliQuery.Where(a => a.IDTipObuce == footwearTypeId.Value);
            if (stockMin.HasValue)
                baseArtikliQuery = baseArtikliQuery.Where(a => (a.Kolicina ?? 0) >= stockMin.Value);
            if (stockMax.HasValue)
                baseArtikliQuery = baseArtikliQuery.Where(a => (a.Kolicina ?? 0) <= stockMax.Value);
            if (marginFloor.HasValue)
                baseArtikliQuery = baseArtikliQuery.Where(a =>
                    a.ProdajnaCena.HasValue &&
                    a.ProdajnaCena > 0 &&
                    (((a.ProdajnaCena ?? 0) - (a.NabavnaCena ?? 0)) / (a.ProdajnaCena ?? 1m)) * 100m >= marginFloor.Value);

            var artikli = await baseArtikliQuery
                .Select(a => new
                {
                    a.Id,
                    a.Naziv,
                    a.IDDobavljac,
                    a.IDSezona,
                    a.IDTipObuce,
                    StockUnits = a.Kolicina ?? 0,
                    SellingPrice = a.ProdajnaCena ?? 0m,
                    PurchasePrice = a.NabavnaCena ?? 0m,
                    a.Kategorija
                })
                .ToListAsync(ct);

            if (artikli.Count == 0)
            {
                return Results.Ok(new PreNivelacijaPriorityResponseDto
                {
                    FormulaDescription = BuildFormulaDescription(),
                    Page = page,
                    PageSize = pageSize
                });
            }

            var dobavljaciMap = await db.Dobavljaci
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.Naziv ?? "N/A", ct);
            var sezoneMap = await db.Sezone
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => new SeasonLite
                {
                    Naziv = x.Naziv,
                    DatumOd = x.DatumOd,
                    DatumDo = x.DatumDo
                }, ct);
            var tipoviMap = await db.TipoviObuce
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.Naziv, ct);

            var salesAgg = await (
                from s in db.ProdajaStavke.AsNoTracking()
                join h in db.ProdajaZaglavlja.AsNoTracking() on s.IdProdaja equals h.Id
                group new { s, h } by s.IdArtikal
                into g
                select new
                {
                    ArtikalId = g.Key,
                    UnitsTotal = g.Sum(x => x.s.Kolicina),
                    Units180 = g.Where(x => x.h.DatumProdaje >= from180).Sum(x => x.s.Kolicina),
                    Revenue180 = g.Where(x => x.h.DatumProdaje >= from180).Sum(x => x.s.Kolicina * x.s.Cena),
                    Units7 = g.Where(x => x.h.DatumProdaje >= from7).Sum(x => x.s.Kolicina),
                    UnitsPrev7 = g.Where(x => x.h.DatumProdaje >= fromPrev7 && x.h.DatumProdaje <= toPrev7).Sum(x => x.s.Kolicina),
                    LastSaleDate = g.Max(x => (DateTime?)x.h.DatumProdaje)
                }).ToListAsync(ct);

            var salesMap = salesAgg.ToDictionary(x => x.ArtikalId);

            var markdownAgg = await db.DnevnikPromena
                .AsNoTracking()
                .Where(d =>
                    d.ArtikalId.HasValue &&
                    d.StaraProdajnaCena.HasValue &&
                    d.NovaProdajnaCena.HasValue &&
                    d.NovaProdajnaCena < d.StaraProdajnaCena)
                .GroupBy(d => d.ArtikalId!.Value)
                .Select(g => new
                {
                    ArtikalId = g.Key,
                    MarkdownEvents = g.Count(),
                    AvgMarkdownPct = g.Average(x =>
                        x.StaraProdajnaCena!.Value <= 0m
                            ? 0m
                            : ((x.StaraProdajnaCena.Value - x.NovaProdajnaCena!.Value) / x.StaraProdajnaCena.Value) * 100m)
                })
                .ToListAsync(ct);

            var markdownMap = markdownAgg.ToDictionary(x => x.ArtikalId);

            var maxStock = Math.Max(1, artikli.Max(x => x.StockUnits));
            var maxVelocity = Math.Max(0.01m,
                artikli.Select(x =>
                {
                    if (!salesMap.TryGetValue(x.Id, out var s)) return 0m;
                    return s.Units180 / 180m;
                }).DefaultIfEmpty(0m).Max());

            var candidates = new List<PreNivelacijaSkuCandidateDto>(artikli.Count);

            foreach (var art in artikli)
            {
                salesMap.TryGetValue(art.Id, out var s);
                markdownMap.TryGetValue(art.Id, out var m);

                var units180 = s?.Units180 ?? 0;
                var velocity180 = decimal.Round(units180 / 180m, 4);
                var daysSinceLastSale = s?.LastSaleDate.HasValue == true
                    ? (int)Math.Max(0, (maxSaleDate.Date - s.LastSaleDate.Value.Date).TotalDays)
                    : 365;

                if (noSaleDaysMin.HasValue && daysSinceLastSale < noSaleDaysMin.Value)
                    continue;

                var markdownEvents = m?.MarkdownEvents ?? 0;
                var avgMarkdownPct = decimal.Round(m?.AvgMarkdownPct ?? 0m, 2);
                var grossMarginPctEst = art.SellingPrice <= 0m
                    ? 0m
                    : decimal.Round(((art.SellingPrice - art.PurchasePrice) / art.SellingPrice) * 100m, 2);
                var seasonBoost = ResolveSeasonRecencyBoost(art.IDSezona, sezoneMap, maxSaleDate);

                var breakdown = scoring.ComputeScoreBreakdown(
                    art.StockUnits,
                    velocity180,
                    daysSinceLastSale,
                    markdownEvents,
                    avgMarkdownPct,
                    grossMarginPctEst,
                    seasonBoost,
                    maxStock,
                    maxVelocity);

                var score = scoring.ComputePreNivelacijaScore(breakdown);
                if (minScore.HasValue && score < minScore.Value)
                    continue;

                var (highlight, markdown, confidence) = scoring.SimulateScenarios(
                    art.StockUnits,
                    units180,
                    markdownEvents,
                    avgMarkdownPct,
                    art.SellingPrice,
                    art.PurchasePrice,
                    score);

                candidates.Add(new PreNivelacijaSkuCandidateDto
                {
                    ArtikalId = art.Id,
                    Sku = art.Naziv,
                    SupplierId = art.IDDobavljac,
                    SeasonId = art.IDSezona,
                    FootwearTypeId = art.IDTipObuce,
                    SupplierName = ResolveSupplierName(art.IDDobavljac, dobavljaciMap),
                    Category = string.IsNullOrWhiteSpace(art.Kategorija) ? "N/A" : art.Kategorija!,
                    FootwearType = ResolveFootwearType(art.IDTipObuce, tipoviMap),
                    Season = ResolveSeasonName(art.IDSezona, sezoneMap),
                    StockUnits = art.StockUnits,
                    Units180 = units180,
                    Velocity180 = velocity180,
                    DaysSinceLastSale = daysSinceLastSale,
                    MarkdownEvents = markdownEvents,
                    AvgMarkdownPct = avgMarkdownPct,
                    GrossMarginPctEst = grossMarginPctEst,
                    SeasonRecencyBoost = seasonBoost,
                    PreNivelacijaScore = score,
                    PriorityBand = ResolvePriorityBand(score),
                    ScoreBreakdown = breakdown,
                    ScenarioHighlightNow = highlight,
                    ScenarioMarkdownNow = markdown,
                    MarginDeltaHighlightVsMarkdown = decimal.Round(highlight.ExpectedMargin30d - markdown.ExpectedMargin30d, 2),
                    RevenueDeltaHighlightVsMarkdown = decimal.Round(highlight.ExpectedRevenue30d - markdown.ExpectedRevenue30d, 2),
                    Confidence = confidence
                });
            }

            candidates = candidates
                .OrderByDescending(x => x.PreNivelacijaScore)
                .ThenByDescending(x => x.StockUnits)
                .ThenByDescending(x => x.DaysSinceLastSale)
                .ToList();

            var supplierStatsRaw = candidates
                .GroupBy(x => new { x.SupplierId, x.SupplierName })
                .Select(g =>
                {
                    var highPriority = g.Count(x => x.PreNivelacijaScore >= 75m);
                    var stockAtRisk = g.Where(x => x.PreNivelacijaScore >= 55m).Sum(x => x.StockUnits);
                    var avoidableLoss = g.Sum(x =>
                    {
                        var impliedDiscount = decimal.Clamp(
                            x.AvgMarkdownPct <= 0m ? 12m : x.AvgMarkdownPct,
                            8m,
                            35m) / 100m;
                        return x.StockUnits * x.ScenarioHighlightNow.EffectivePrice * impliedDiscount * 0.6m;
                    });
                    var uplift = g.Sum(x => Math.Max(0m, x.RevenueDeltaHighlightVsMarkdown));
                    var s7 = g.Sum(x => salesMap.TryGetValue(x.ArtikalId, out var sa) ? sa.Units7 : 0);
                    var p7 = g.Sum(x => salesMap.TryGetValue(x.ArtikalId, out var sa) ? sa.UnitsPrev7 : 0);
                    var wowRiskDelta = p7 <= 0 ? (s7 <= 0 ? 0m : -100m) : decimal.Round(((p7 - s7) * 100m) / p7, 2);
                    return new
                    {
                        SupplierId = g.Key.SupplierId,
                        SupplierName = g.Key.SupplierName,
                        HighPrioritySkuCount = highPriority,
                        CandidateSkuCount = g.Count(),
                        StockUnitsAtRisk = stockAtRisk,
                        EstimatedAvoidableMarkdownLoss = decimal.Round(avoidableLoss, 2),
                        ExpectedHighlightRevenueUplift = decimal.Round(uplift, 2),
                        WeekOverWeekRiskDeltaPct = wowRiskDelta
                    };
                })
                .ToList();

            var maxHigh = Math.Max(1, supplierStatsRaw.DefaultIfEmpty().Max(x => x?.HighPrioritySkuCount ?? 0));
            var maxRiskStock = Math.Max(1, supplierStatsRaw.DefaultIfEmpty().Max(x => x?.StockUnitsAtRisk ?? 0));
            var maxLoss = Math.Max(1m, supplierStatsRaw.DefaultIfEmpty().Max(x => x?.EstimatedAvoidableMarkdownLoss ?? 0m));
            var maxUplift = Math.Max(1m, supplierStatsRaw.DefaultIfEmpty().Max(x => x?.ExpectedHighlightRevenueUplift ?? 0m));

            var supplierLeaderboard = supplierStatsRaw
                .Select(x =>
                {
                    var score = 0.40m * (x.HighPrioritySkuCount * 100m / maxHigh)
                                + 0.25m * (x.StockUnitsAtRisk * 100m / maxRiskStock)
                                + 0.20m * (x.EstimatedAvoidableMarkdownLoss * 100m / maxLoss)
                                + 0.15m * (x.ExpectedHighlightRevenueUplift * 100m / maxUplift);

                    return new PreNivelacijaSupplierActionDto
                    {
                        SupplierId = x.SupplierId,
                        SupplierName = x.SupplierName,
                        HighPrioritySkuCount = x.HighPrioritySkuCount,
                        CandidateSkuCount = x.CandidateSkuCount,
                        StockUnitsAtRisk = x.StockUnitsAtRisk,
                        EstimatedAvoidableMarkdownLoss = x.EstimatedAvoidableMarkdownLoss,
                        ExpectedHighlightRevenueUplift = x.ExpectedHighlightRevenueUplift,
                        ActionScore = decimal.Round(score, 2),
                        WeekOverWeekRiskDeltaPct = x.WeekOverWeekRiskDeltaPct
                    };
                })
                .OrderByDescending(x => x.ActionScore)
                .ThenByDescending(x => x.HighPrioritySkuCount)
                .Take(25)
                .ToList();

            var now = DateTime.UtcNow.Date;
            var queueHighlight = candidates
                .Where(x => x.PreNivelacijaScore >= 75m)
                .Take(20)
                .Select(x => ToQueueItem(x, now.AddDays(3)))
                .ToList();
            var queueMonitor = candidates
                .Where(x => x.PreNivelacijaScore >= 55m && x.PreNivelacijaScore < 75m)
                .Take(20)
                .Select(x => ToQueueItem(x, now.AddDays(7)))
                .ToList();
            var queueMarkdownSoon = candidates
                .Where(x => x.MarkdownEvents >= 2 || x.DaysSinceLastSale > 120)
                .OrderByDescending(x => x.PreNivelacijaScore)
                .Take(20)
                .Select(x => ToQueueItem(x, now.AddDays(2)))
                .ToList();

            var alerts = BuildAlerts(candidates, supplierLeaderboard);
            var totalCandidates = candidates.Count;
            var paged = candidates.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var summary = new PreNivelacijaSummaryDto
            {
                SupplierCount = supplierLeaderboard.Count,
                CandidatesCount = totalCandidates,
                HighPriorityCount = candidates.Count(x => x.PreNivelacijaScore >= 75m),
                TotalStockAtRisk = candidates.Where(x => x.PreNivelacijaScore >= 55m).Sum(x => x.StockUnits),
                EstimatedAvoidableMarkdownLoss = decimal.Round(candidates.Sum(x =>
                    Math.Max(0m, x.StockUnits * x.ScenarioHighlightNow.EffectivePrice * (x.AvgMarkdownPct <= 0m ? 0.12m : x.AvgMarkdownPct / 100m) * 0.6m)), 2),
                ExpectedHighlightRevenueUplift = decimal.Round(candidates.Sum(x => Math.Max(0m, x.RevenueDeltaHighlightVsMarkdown)), 2),
                AveragePreNivelacijaScore = totalCandidates == 0 ? 0m : decimal.Round(candidates.Average(x => x.PreNivelacijaScore), 2)
            };

            var response = new PreNivelacijaPriorityResponseDto
            {
                FormulaDescription = BuildFormulaDescription(),
                Summary = summary,
                SupplierLeaderboard = supplierLeaderboard,
                Candidates = paged,
                Queues = new PreNivelacijaQueuesDto
                {
                    HighlightNow = queueHighlight,
                    Monitor = queueMonitor,
                    LikelyMarkdownSoon = queueMarkdownSoon
                },
                Alerts = alerts,
                Page = page,
                PageSize = pageSize,
                TotalCandidates = totalCandidates
            };

            return Results.Ok(response);
        })
        .WithName("GetPreNivelacijaPrioriteti")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");
    }

    private static string BuildFormulaDescription()
    {
        return "Pre-Nivelacija Score = 0.30*StockPressure + 0.25*VelocityRisk + 0.20*RecencyRisk + 0.10*MarkdownOpportunity + 0.10*MarginPotential + 0.05*SeasonRecencyBoost";
    }

    private static string ResolvePriorityBand(decimal score)
    {
        if (score >= 75m) return "high";
        if (score >= 55m) return "medium";
        return "low";
    }

    private static string ResolveSupplierName(int? supplierId, IReadOnlyDictionary<int, string> suppliers)
    {
        if (supplierId.HasValue && suppliers.TryGetValue(supplierId.Value, out var name) && !string.IsNullOrWhiteSpace(name))
            return name.Trim();
        return "N/A";
    }

    private static string ResolveSeasonName(int? seasonId, IReadOnlyDictionary<int, SeasonLite> seasons)
    {
        if (seasonId.HasValue && seasons.TryGetValue(seasonId.Value, out var sez) && !string.IsNullOrWhiteSpace(sez.Naziv))
            return sez.Naziv.Trim();
        return "N/A";
    }

    private static string ResolveFootwearType(int? typeId, IReadOnlyDictionary<int, string> tipovi)
    {
        if (typeId.HasValue && tipovi.TryGetValue(typeId.Value, out var t) && !string.IsNullOrWhiteSpace(t))
            return t.Trim();
        return "N/A";
    }

    private static decimal ResolveSeasonRecencyBoost(int? seasonId, IReadOnlyDictionary<int, SeasonLite> seasons, DateTime maxSaleDate)
    {
        if (!seasonId.HasValue || !seasons.TryGetValue(seasonId.Value, out var season))
            return 30m;

        var from = season.DatumOd;
        var to = season.DatumDo;
        var date = maxSaleDate.Date;
        if (date >= from.Date && date <= to.Date) return 100m;

        var minDiff = Math.Min(Math.Abs((date - from.Date).Days), Math.Abs((date - to.Date).Days));
        if (minDiff <= 60) return 60m;
        return 20m;
    }

    private static PreNivelacijaQueueItemDto ToQueueItem(PreNivelacijaSkuCandidateDto sku, DateTime dueDateUtc, string? status = null)
    {
        return new PreNivelacijaQueueItemDto
        {
            ArtikalId = sku.ArtikalId,
            Sku = sku.Sku,
            SupplierName = sku.SupplierName,
            PreNivelacijaScore = sku.PreNivelacijaScore,
            PriorityBand = sku.PriorityBand,
            Owner = "Unassigned",
            Status = status ?? "Unassigned",
            DueDateUtc = dueDateUtc
        };
    }

    private static List<PreNivelacijaAlertDto> BuildAlerts(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates,
        IReadOnlyList<PreNivelacijaSupplierActionDto> suppliers)
    {
        var alerts = new List<PreNivelacijaAlertDto>();

        foreach (var sku in candidates
                     .Where(x => x.DaysSinceLastSale > 120 && x.StockUnits > 8)
                     .Take(8))
        {
            alerts.Add(new PreNivelacijaAlertDto
            {
                Type = "NoSaleStockPressure",
                Severity = "critical",
                Message = $"{sku.Sku} ({sku.SupplierName}) nema prodaju {sku.DaysSinceLastSale} dana uz zalihu {sku.StockUnits}.",
                SupplierName = sku.SupplierName,
                ArtikalId = sku.ArtikalId
            });
        }

        foreach (var sku in candidates
                     .Where(x => x.MarkdownEvents >= 2 && x.Velocity180 < 0.03m)
                     .Take(8))
        {
            alerts.Add(new PreNivelacijaAlertDto
            {
                Type = "RepeatedMarkdownLowSellThrough",
                Severity = "warning",
                Message = $"{sku.Sku} ima ponovljene markdown-e ({sku.MarkdownEvents}) i nizak velocity ({sku.Velocity180:0.000}).",
                SupplierName = sku.SupplierName,
                ArtikalId = sku.ArtikalId
            });
        }

        foreach (var sup in suppliers
                     .Where(x => x.WeekOverWeekRiskDeltaPct > 20m && x.HighPrioritySkuCount >= 3)
                     .Take(5))
        {
            alerts.Add(new PreNivelacijaAlertDto
            {
                Type = "SupplierRiskClusterWoW",
                Severity = "warning",
                Message = $"{sup.SupplierName} ima rast rizika {sup.WeekOverWeekRiskDeltaPct:0.##}% WoW uz {sup.HighPrioritySkuCount} high-priority SKU.",
                SupplierName = sup.SupplierName
            });
        }

        return alerts;
    }
}
