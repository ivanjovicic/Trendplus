using System.Globalization;
using Api.Models;
using Api.Services;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Trendplus2.Dtos;

namespace Trendplus2.Endpoints;

public static class PreNivelacijaPriorityEndpoints
{
    private sealed class SalesLite
    {
        public int Units180 { get; init; }
        public int Units7 { get; init; }
        public int UnitsPrev7 { get; init; }
        public DateTime? LastSaleDateUtc { get; init; }
    }

    private sealed class SeasonLite
    {
        public string Naziv { get; init; } = "N/A";
        public DateTime DatumOd { get; init; }
        public DateTime DatumDo { get; init; }
    }

    private sealed class PreNivelacijaPriorityBaseCacheEntry
    {
        public DateTime GeneratedAtUtc { get; init; }
        public string FormulaVersion { get; init; } = "pre_nivelacija_v2";
        public string FormulaDescription { get; init; } = string.Empty;
        public PreNivelacijaSummaryDto Summary { get; init; } = new();
        public List<PreNivelacijaSupplierActionDto> SupplierLeaderboard { get; init; } = [];
        public List<PreNivelacijaSkuCandidateDto> Candidates { get; init; } = [];
        public PreNivelacijaQueuesDto Queues { get; init; } = new();
        public List<PreNivelacijaAlertDto> Alerts { get; init; } = [];
        public int TotalCandidates { get; init; }
        public bool RecommendationAllowed { get; init; }
        public AnalyticsResponseMetaDto? Meta { get; init; }
    }

    public static void MapPreNivelacijaPriorityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/analytics/pre-nivelacija-prioriteti", async (
            TrendplusDbContext db,
            IPreNivelacijaScoringService scoring,
            IAnalyticsCacheService cache,
            ILoggerFactory loggerFactory,
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
            pageSize = Math.Clamp(pageSize, 1, 100);

            try
            {

            var cacheKey = AnalyticsCacheKeys.PreNivelacijaPriorityBase(
                supplierId,
                seasonId,
                footwearTypeId,
                stockMin,
                stockMax,
                noSaleDaysMin,
                minScore,
                marginFloor);

            var baseEntry = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var nowUtc = DateTime.UtcNow;
                    var todayUtc = nowUtc.Date;
                    var from180Utc = todayUtc.AddDays(-180);
                    var last7FromUtc = todayUtc.AddDays(-6);
                    var prev7FromUtc = todayUtc.AddDays(-13);

                    var suppliers = await db.Dobavljaci
                        .AsNoTracking()
                        .ToDictionaryAsync(x => x.Id, x => string.IsNullOrWhiteSpace(x.Naziv) ? "N/A" : x.Naziv.Trim(), ct);

                    var seasons = await db.Sezone
                        .AsNoTracking()
                        .ToDictionaryAsync(x => x.Id, x => new SeasonLite
                        {
                            Naziv = x.Naziv,
                            DatumOd = x.DatumOd,
                            DatumDo = x.DatumDo
                        }, ct);

                    var footwearTypes = await db.TipoviObuce
                        .AsNoTracking()
                        .ToDictionaryAsync(x => x.Id, x => x.Naziv, ct);

                    var artikliQuery = db.Artikli
                        .AsNoTracking()
                        .Where(a => (a.Kolicina ?? 0) > 0);

                    if (supplierId.HasValue)
                    {
                        artikliQuery = artikliQuery.Where(a => a.IDDobavljac == supplierId.Value);
                    }

                    if (seasonId.HasValue)
                    {
                        artikliQuery = artikliQuery.Where(a => a.IDSezona == seasonId.Value);
                    }

                    if (footwearTypeId.HasValue)
                    {
                        artikliQuery = artikliQuery.Where(a => a.IDTipObuce == footwearTypeId.Value);
                    }

                    if (stockMin.HasValue)
                    {
                        artikliQuery = artikliQuery.Where(a => (a.Kolicina ?? 0) >= stockMin.Value);
                    }

                    if (stockMax.HasValue)
                    {
                        artikliQuery = artikliQuery.Where(a => (a.Kolicina ?? 0) <= stockMax.Value);
                    }

                    var artikli = await artikliQuery
                        .Select(a => new
                        {
                            a.Id,
                            a.PLU,
                            SupplierId = a.IDDobavljac,
                            SeasonId = a.IDSezona,
                            FootwearTypeId = a.IDTipObuce,
                            StockUnits = a.Kolicina ?? 0,
                            a.Kategorija,
                            SellingPrice = a.ProdajnaCena ?? a.PrvaProdajnaCena,
                            PurchasePrice = a.NabavnaCenaDin ?? a.NabavnaCena
                        })
                        .ToListAsync(ct);

                    if (artikli.Count == 0)
                    {
                        return BuildEmptyBaseEntry(nowUtc);
                    }

                    var artikalIds = artikli.Select(x => x.Id).ToArray();

                    Dictionary<int, SalesLite> salesByArtikal;
                    var salesQueryFailed = false;
                    try
                    {
                        var sales = await (
                            from ps in db.ProdajaStavke.AsNoTracking()
                            join p in db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals p.Id
                            where artikalIds.Contains(ps.IdArtikal) && p.DatumProdaje >= from180Utc
                            group new { ps, p } by ps.IdArtikal into g
                            select new
                            {
                                ArtikalId = g.Key,
                                Units180 = g.Sum(x => x.ps.Kolicina),
                                LastSale = g.Max(x => (DateTime?)x.p.DatumProdaje),
                                Units7 = g.Where(x => x.p.DatumProdaje >= last7FromUtc).Sum(x => x.ps.Kolicina),
                                UnitsPrev7 = g.Where(x => x.p.DatumProdaje >= prev7FromUtc && x.p.DatumProdaje < last7FromUtc).Sum(x => x.ps.Kolicina),
                            })
                            .ToListAsync(ct);

                        salesByArtikal = sales.ToDictionary(
                            x => x.ArtikalId,
                            x => new SalesLite
                            {
                                Units180 = x.Units180,
                                Units7 = x.Units7,
                                UnitsPrev7 = x.UnitsPrev7,
                                LastSaleDateUtc = x.LastSale
                            });
                    }
                    catch
                    {
                        salesQueryFailed = true;
                        salesByArtikal = new Dictionary<int, SalesLite>();
                    }

                    Dictionary<int, (int MarkdownEvents, decimal AvgMarkdownPct)> markdownByArtikal;
                    var markdownQueryFailed = false;
                    try
                    {
                        var markdown = await db.DnevnikPromena
                            .AsNoTracking()
                            .Where(dp => dp.ArtikalId.HasValue
                                         && artikalIds.Contains(dp.ArtikalId.Value)
                                         && (dp.TipPromene == "Nivelacija" || dp.TipPromene == "Nivelacija cena"))
                            .GroupBy(dp => dp.ArtikalId!.Value)
                            .Select(g => new
                            {
                                ArtikalId = g.Key,
                                MarkdownEvents = g.Count(),
                                AvgMarkdownPct = g
                                    .Where(dp => dp.StaraProdajnaCena.HasValue
                                                 && dp.NovaProdajnaCena.HasValue
                                                 && dp.StaraProdajnaCena.Value > 0m
                                                 && dp.NovaProdajnaCena.Value < dp.StaraProdajnaCena.Value)
                                    .Select(dp => ((dp.StaraProdajnaCena!.Value - dp.NovaProdajnaCena!.Value) / dp.StaraProdajnaCena!.Value) * 100m)
                                    .DefaultIfEmpty(0m)
                                    .Average()
                            })
                            .ToListAsync(ct);

                        markdownByArtikal = markdown.ToDictionary(x => x.ArtikalId, x => (x.MarkdownEvents, decimal.Round(x.AvgMarkdownPct, 2)));
                    }
                    catch
                    {
                        markdownQueryFailed = true;
                        markdownByArtikal = new Dictionary<int, (int MarkdownEvents, decimal AvgMarkdownPct)>();
                    }

                    if (salesQueryFailed || markdownQueryFailed)
                    {
                        return BuildEmptyBaseEntry(nowUtc, BuildQueryFailureMeta(salesQueryFailed, markdownQueryFailed));
                    }

                    var maxStock = Math.Max(1, artikli.Max(x => x.StockUnits));
                    var maxVelocity = artikli
                        .Select(x => salesByArtikal.TryGetValue(x.Id, out var salesLite) ? (decimal)salesLite.Units180 / 180m : 0m)
                        .DefaultIfEmpty(0m)
                        .Max();

                    var allCandidates = new List<PreNivelacijaSkuCandidateDto>(artikli.Count);

                    foreach (var a in artikli)
                    {
                        var sku = !string.IsNullOrWhiteSpace(a.PLU) ? a.PLU.Trim() : a.Id.ToString(CultureInfo.InvariantCulture);
                        var supplierName = ResolveSupplierName(a.SupplierId, suppliers);
                        var seasonName = ResolveSeasonName(a.SeasonId, seasons);
                        var footwearType = ResolveFootwearType(a.FootwearTypeId, footwearTypes);

                        var units180 = salesByArtikal.TryGetValue(a.Id, out var salesLite) ? salesLite.Units180 : 0;
                        var velocity180 = units180 <= 0 ? 0m : decimal.Round(units180 / 180m, 4);
                        var lastSaleDate = salesLite?.LastSaleDateUtc;
                        var daysSinceLastSale = lastSaleDate.HasValue
                            ? Math.Max(0, (todayUtc - lastSaleDate.Value.Date).Days)
                            : 999;

                        if (noSaleDaysMin.HasValue && daysSinceLastSale < noSaleDaysMin.Value)
                            continue;

                        var markdownEvents = markdownByArtikal.TryGetValue(a.Id, out var markdownLite) ? markdownLite.MarkdownEvents : 0;
                        var avgMarkdownPct = markdownByArtikal.TryGetValue(a.Id, out markdownLite) ? markdownLite.AvgMarkdownPct : 0m;

                        var sellingPrice = a.SellingPrice ?? 0m;
                        var purchasePrice = a.PurchasePrice ?? 0m;
                        var hasCompleteEvidence = a.SellingPrice.HasValue
                            && a.SellingPrice.Value > 0m
                            && a.PurchasePrice.HasValue
                            && a.PurchasePrice.Value >= 0m;
                        var grossMarginPct = hasCompleteEvidence && sellingPrice > 0m
                            ? decimal.Round(Math.Clamp(((sellingPrice - purchasePrice) / sellingPrice) * 100m, 0m, 100m), 2)
                            : 0m;

                        if (marginFloor.HasValue && grossMarginPct < marginFloor.Value)
                            continue;

                        var seasonRecencyBoost = ResolveSeasonRecencyBoost(a.SeasonId, seasons, todayUtc);
                        var breakdown = scoring.ComputeScoreBreakdown(
                            a.StockUnits,
                            velocity180,
                            daysSinceLastSale,
                            markdownEvents,
                            avgMarkdownPct,
                            grossMarginPct,
                            seasonRecencyBoost,
                            maxStock,
                            maxVelocity);

                        var preNivelacijaScore = scoring.ComputePreNivelacijaScore(breakdown);
                        if (minScore.HasValue && preNivelacijaScore < minScore.Value)
                            continue;

                        var (highlight, markdown, confidence) = scoring.SimulateScenarios(
                            a.StockUnits,
                            units180,
                            markdownEvents,
                            avgMarkdownPct,
                            sellingPrice,
                            purchasePrice,
                            preNivelacijaScore);

                        allCandidates.Add(new PreNivelacijaSkuCandidateDto
                        {
                            ArtikalId = a.Id,
                            Sku = sku,
                            SupplierId = a.SupplierId,
                            SeasonId = a.SeasonId,
                            FootwearTypeId = a.FootwearTypeId,
                            SupplierName = supplierName,
                            Category = string.IsNullOrWhiteSpace(a.Kategorija) ? "N/A" : a.Kategorija.Trim(),
                            FootwearType = footwearType,
                            Season = seasonName,
                            StockUnits = a.StockUnits,
                            Units180 = units180,
                            Velocity180 = velocity180,
                            DaysSinceLastSale = daysSinceLastSale,
                            MarkdownEvents = markdownEvents,
                            AvgMarkdownPct = decimal.Round(avgMarkdownPct, 2),
                            GrossMarginPctEst = grossMarginPct,
                            SeasonRecencyBoost = seasonRecencyBoost,
                            PreNivelacijaScore = preNivelacijaScore,
                            PriorityBand = ResolvePriorityBand(preNivelacijaScore),
                            ScoreBreakdown = breakdown,
                            ScenarioHighlightNow = highlight,
                            ScenarioMarkdownNow = markdown,
                            MarginDeltaHighlightVsMarkdown = decimal.Round(highlight.ExpectedMargin30d - markdown.ExpectedMargin30d, 2),
                            RevenueDeltaHighlightVsMarkdown = decimal.Round(highlight.ExpectedRevenue30d - markdown.ExpectedRevenue30d, 2),
                            HasCompleteEvidence = hasCompleteEvidence,
                            EvidenceReason = hasCompleteEvidence ? null : "missing_price_baseline",
                            Confidence = confidence
                        });
                    }

                    if (allCandidates.Count == 0)
                    {
                        return BuildEmptyBaseEntry(nowUtc);
                    }

                    var minRevenueDelta = allCandidates.Min(x => x.RevenueDeltaHighlightVsMarkdown);
                    var maxRevenueDelta = allCandidates.Max(x => x.RevenueDeltaHighlightVsMarkdown);

                    foreach (var candidate in allCandidates)
                    {
                        var recommendation = scoring.EvaluateRecommendation(new IPreNivelacijaScoringService.RecommendationInput(
                            candidate.PreNivelacijaScore,
                            candidate.RevenueDeltaHighlightVsMarkdown,
                            minRevenueDelta,
                            maxRevenueDelta,
                            candidate.DaysSinceLastSale,
                            candidate.PriorityBand,
                            candidate.Confidence,
                            candidate.Units180,
                            candidate.StockUnits,
                            HasCompleteEvidence: candidate.HasCompleteEvidence));

                        candidate.DecisionScore = recommendation.DecisionScore;
                        candidate.ReliabilityPct = recommendation.ReliabilityPct;
                        candidate.Recommendation = recommendation.Recommendation;
                    }

                    allCandidates = allCandidates
                        .OrderByDescending(x => x.PreNivelacijaScore)
                        .ThenByDescending(x => x.DecisionScore)
                        .ThenByDescending(x => x.StockUnits)
                        .ToList();

                    var totalCandidates = allCandidates.Count;
                    var pagedCandidates = allCandidates
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    var supplierLeaderboard = allCandidates
                        .GroupBy(x => new { x.SupplierId, x.SupplierName })
                        .Select(g =>
                        {
                            var highCount = g.Count(x => x.PriorityBand == "high");
                            var candidateCount = g.Count();
                            var stockAtRisk = g.Where(x => x.PriorityBand == "high").Sum(x => x.StockUnits);
                            var avoidableLoss = g.Where(x => x.MarginDeltaHighlightVsMarkdown > 0m).Sum(x => x.MarginDeltaHighlightVsMarkdown);
                            var expectedUplift = g.Where(x => x.RevenueDeltaHighlightVsMarkdown > 0m).Sum(x => x.RevenueDeltaHighlightVsMarkdown);

                            var last7 = g.Sum(x => salesByArtikal.TryGetValue(x.ArtikalId, out var salesLite) ? salesLite.Units7 : 0);
                            var prev7 = g.Sum(x => salesByArtikal.TryGetValue(x.ArtikalId, out var salesLite) ? salesLite.UnitsPrev7 : 0);
                            var wowRiskDelta = prev7 <= 0
                                ? 0m
                                : decimal.Round(((prev7 - last7) / (decimal)prev7) * 100m, 2);

                            var actionScore = decimal.Round(
                                (highCount * 10m)
                                + (avoidableLoss / 1000m)
                                + (expectedUplift / 5000m)
                                + (Math.Max(0m, wowRiskDelta) / 10m),
                                2);

                            return new PreNivelacijaSupplierActionDto
                            {
                                SupplierId = g.Key.SupplierId,
                                SupplierName = g.Key.SupplierName,
                                HighPrioritySkuCount = highCount,
                                CandidateSkuCount = candidateCount,
                                StockUnitsAtRisk = stockAtRisk,
                                EstimatedAvoidableMarkdownLoss = decimal.Round(avoidableLoss, 2),
                                ExpectedHighlightRevenueUplift = decimal.Round(expectedUplift, 2),
                                ActionScore = actionScore,
                                WeekOverWeekRiskDeltaPct = wowRiskDelta
                            };
                        })
                        .OrderByDescending(x => x.ActionScore)
                        .ToList();

                    var highPriority = allCandidates.Where(x => x.PriorityBand == "high").ToList();
                    var summary = new PreNivelacijaSummaryDto
                    {
                        SupplierCount = supplierLeaderboard.Count,
                        CandidatesCount = totalCandidates,
                        HighPriorityCount = highPriority.Count,
                        TotalStockAtRisk = highPriority.Sum(x => x.StockUnits),
                        EstimatedAvoidableMarkdownLoss = decimal.Round(allCandidates.Where(x => x.MarginDeltaHighlightVsMarkdown > 0m).Sum(x => x.MarginDeltaHighlightVsMarkdown), 2),
                        ExpectedHighlightRevenueUplift = decimal.Round(allCandidates.Where(x => x.RevenueDeltaHighlightVsMarkdown > 0m).Sum(x => x.RevenueDeltaHighlightVsMarkdown), 2),
                        AveragePreNivelacijaScore = totalCandidates == 0 ? 0m : decimal.Round(allCandidates.Average(x => x.PreNivelacijaScore), 2)
                    };

                    var queues = new PreNivelacijaQueuesDto
                    {
                        HighlightNow = allCandidates
                            .Where(x => x.PriorityBand == "high" && x.Recommendation.RecommendationAllowed)
                            .Take(30)
                            .Select(x => ToQueueItem(x, nowUtc.AddDays(2)))
                            .ToList(),
                        Monitor = allCandidates
                            .Where(x => x.PriorityBand == "medium" && x.Recommendation.RecommendationAllowed)
                            .Take(30)
                            .Select(x => ToQueueItem(x, nowUtc.AddDays(7)))
                            .ToList(),
                        LikelyMarkdownSoon = allCandidates
                            .Where(x => x.Recommendation.RecommendationAllowed
                                && (x.DaysSinceLastSale >= 60 || x.MarkdownEvents >= 2 || x.AvgMarkdownPct >= 25m))
                            .OrderByDescending(x => x.DaysSinceLastSale)
                            .ThenByDescending(x => x.StockUnits)
                            .Take(30)
                            .Select(x => ToQueueItem(x, nowUtc.AddDays(3)))
                            .ToList()
                    };

                    var alerts = BuildAlerts(allCandidates, supplierLeaderboard);

                    return new PreNivelacijaPriorityBaseCacheEntry
                    {
                        GeneratedAtUtc = nowUtc,
                        FormulaVersion = "pre_nivelacija_v2",
                        FormulaDescription = BuildFormulaDescription(),
                        Summary = summary,
                        SupplierLeaderboard = supplierLeaderboard,
                        Candidates = allCandidates,
                        Queues = queues,
                        Alerts = alerts,
                        TotalCandidates = totalCandidates,
                        RecommendationAllowed = allCandidates.All(x => x.Recommendation.RecommendationAllowed)
                    };
                },
                CacheExpiration.HeavyAnalytics,
                ct);

            var response = BuildResponse(baseEntry, page, pageSize);

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("PreNivelacijaPriorityEndpoints")
                    .LogWarning(ex, "Pre-nivelacija analytics unavailable; returning explicit error metadata.");

                var unavailable = BuildEmptyBaseEntry(
                    DateTime.UtcNow,
                    AnalyticsResponseMetaFactory.Error(
                        "pre_nivelacija_unavailable",
                        "Pre-nivelacija podaci trenutno nisu dostupni.",
                        null));
                return Results.Ok(BuildResponse(unavailable, page, pageSize));
            }
        })
        .WithName("GetPreNivelacijaPrioriteti")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");
    }

    private static string BuildFormulaDescription()
    {
        return "Pre-Nivelacija Score = 0.30*StockPressure + 0.25*VelocityRisk + 0.20*RecencyRisk + 0.10*MarkdownOpportunity + 0.10*MarginPotential + 0.05*SeasonRecencyBoost; Recommendation = 0.50*Score + 0.20*ScenarioDelta + 0.15*StaleRisk + 0.15*Reliability";
    }

    internal static AnalyticsResponseMetaDto BuildQueryFailureMeta(bool salesQueryFailed, bool markdownQueryFailed)
    {
        if (salesQueryFailed)
        {
            return AnalyticsResponseMetaFactory.Error(
                "pre_nivelacija_sales_unavailable",
                "Prodaja za pre-nivelacija skor trenutno nije dostupna.",
                null);
        }

        if (markdownQueryFailed)
        {
            return AnalyticsResponseMetaFactory.Error(
                "pre_nivelacija_markdown_unavailable",
                "Dnevnik nivelacija za pre-nivelacija skor trenutno nije dostupan.",
                null);
        }

        return AnalyticsResponseMetaFactory.Success();
    }

    private static PreNivelacijaPriorityBaseCacheEntry BuildEmptyBaseEntry(
        DateTime nowUtc,
        AnalyticsResponseMetaDto? meta = null)
    {
        return new PreNivelacijaPriorityBaseCacheEntry
        {
            GeneratedAtUtc = nowUtc,
            FormulaVersion = "pre_nivelacija_v2",
            FormulaDescription = BuildFormulaDescription(),
            Summary = new PreNivelacijaSummaryDto
            {
                SupplierCount = 0,
                CandidatesCount = 0,
                HighPriorityCount = 0,
                TotalStockAtRisk = 0,
                EstimatedAvoidableMarkdownLoss = 0,
                ExpectedHighlightRevenueUplift = 0,
                AveragePreNivelacijaScore = 0
            },
            SupplierLeaderboard = [],
            Candidates = [],
            Queues = new PreNivelacijaQueuesDto(),
            Alerts = [],
            TotalCandidates = 0,
            Meta = meta ?? AnalyticsResponseMetaFactory.Empty(
                "no_pre_nivelacija_candidates",
                "Nema kandidata za pre-nivelaciju.")
        };
    }

    private static PreNivelacijaPriorityResponseDto BuildResponse(
        PreNivelacijaPriorityBaseCacheEntry baseEntry,
        int page,
        int pageSize)
    {
        var pagedCandidates = baseEntry.Candidates
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var response = new PreNivelacijaPriorityResponseDto
        {
            GeneratedAtUtc = baseEntry.GeneratedAtUtc,
            FormulaVersion = baseEntry.FormulaVersion,
            FormulaDescription = baseEntry.FormulaDescription,
            Summary = baseEntry.Summary,
            SupplierLeaderboard = baseEntry.SupplierLeaderboard,
            Candidates = pagedCandidates,
            Queues = baseEntry.Queues,
            Alerts = baseEntry.Alerts,
            Page = page,
            PageSize = pageSize,
            TotalCandidates = baseEntry.TotalCandidates,
            RecommendationAllowed = baseEntry.RecommendationAllowed,
            Meta = baseEntry.Meta ?? AnalyticsResponseMetaFactory.Success()
        };

        response.Meta!.RecommendationAllowed = response.RecommendationAllowed;
        return response;
    }

    private static string ResolvePriorityBand(decimal score)
    {
        if (score >= 75m) return "high";
        if (score >= 55m) return "medium";
        return "low";
    }

    private static string ResolveSupplierName(int? supplierId, Dictionary<int, string> suppliers)
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

    private static string ResolveFootwearType(int? typeId, Dictionary<int, string> tipovi)
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
