using System.Globalization;
using Api.Models;
using Api.Services;
using Application.Analytics;
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
        public int PositiveUnits180 { get; init; }
        public int NegativeUnits180 { get; init; }
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
        public string FormulaVersion { get; init; } = "pre_nivelacija_v5";
        public string FormulaDescription { get; init; } = string.Empty;
        public PreNivelacijaSummaryDto Summary { get; init; } = new();
        public List<PreNivelacijaSupplierActionDto> SupplierLeaderboard { get; init; } = [];
        /// <summary>
        /// Scored candidates for non-facet filters only. Supplier/season/type filters are applied after cache read
        /// so filter facets can use leave-one-out universes.
        /// </summary>
        public List<PreNivelacijaSkuCandidateDto> Candidates { get; init; } = [];
        public List<PreNivelacijaSkuCandidateDto> FacetUniverseCandidates { get; init; } = [];
        public PreNivelacijaQueuesDto Queues { get; init; } = new();
        public List<PreNivelacijaAlertDto> Alerts { get; init; } = [];
        public PreNivelacijaEvidenceWindowDto EvidenceWindow { get; init; } = new();
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
            string dataScope = "all",
            int page = 1,
            int pageSize = 20,
            string? focus = null,
            CancellationToken ct = default) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            try
            {
            var normalizedDataScope = NormalizeDataScope(dataScope);
            var requestNowUtc = DateTime.UtcNow;

            var cacheKey = AnalyticsCacheKeys.PreNivelacijaPriorityBase(
                supplierId: null,
                seasonId: null,
                footwearTypeId: null,
                stockMin,
                stockMax,
                noSaleDaysMin,
                minScore,
                marginFloor,
                normalizedDataScope,
                requestNowUtc.Date);

            var baseEntry = await cache.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    var nowUtc = DateTime.UtcNow;
                    var todayUtc = nowUtc.Date;
                    var from180Utc = nowUtc.AddDays(-180);
                    var last7FromUtc = nowUtc.AddDays(-7);
                    var prev7FromUtc = nowUtc.AddDays(-14);

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

                    if (normalizedDataScope == "imported")
                    {
                        artikliQuery = artikliQuery.Where(a => a.DataOrigin == "access");
                    }
                    else if (normalizedDataScope == "existing")
                    {
                        artikliQuery = artikliQuery.Where(a => a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "");
                    }

                    // Supplier/season/footwear filters are applied after cache so facets stay leave-one-out.

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
                            where artikalIds.Contains(ps.IdArtikal)
                                && p.DatumProdaje >= from180Utc
                                && p.DatumProdaje <= nowUtc
                                && (normalizedDataScope == "all"
                                    || (normalizedDataScope == "imported" && p.DataOrigin == "access")
                                    || (normalizedDataScope == "existing" && (p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")))
                            group new { ps, p } by ps.IdArtikal into g
                            select new
                            {
                                ArtikalId = g.Key,
                                Units180 = g.Sum(x => x.ps.Kolicina),
                                PositiveUnits180 = g.Where(x => x.ps.Kolicina > 0).Sum(x => x.ps.Kolicina),
                                NegativeUnits180 = g.Where(x => x.ps.Kolicina < 0).Sum(x => x.ps.Kolicina),
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
                                PositiveUnits180 = x.PositiveUnits180,
                                NegativeUnits180 = x.NegativeUnits180,
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
                        // Keep the relational query simple and perform the small
                        // markdown-per-article reduction in memory. PostgreSQL
                        // providers do not translate the nested DefaultIfEmpty /
                        // Average expression consistently across supported hosts.
                        var markdownRows = await db.DnevnikPromena
                            .AsNoTracking()
                            .Where(dp => dp.ArtikalId.HasValue
                                         && artikalIds.Contains(dp.ArtikalId.Value)
                                         && (normalizedDataScope == "all"
                                             || (normalizedDataScope == "imported" && dp.DataOrigin == "access")
                                             || (normalizedDataScope == "existing" && (dp.DataOrigin == "existing" || dp.DataOrigin == null || dp.DataOrigin == "")))
                                         && dp.Datum >= from180Utc
                                         && dp.Datum <= nowUtc
                                         && (dp.TipPromene == "Nivelacija" || dp.TipPromene == "Nivelacija cena"))
                            .Select(dp => new
                            {
                                ArtikalId = dp.ArtikalId!.Value,
                                dp.StaraProdajnaCena,
                                dp.NovaProdajnaCena
                            })
                            .ToListAsync(ct);

                        markdownByArtikal = markdownRows
                            .GroupBy(x => x.ArtikalId)
                            .ToDictionary(
                                g => g.Key,
                                g =>
                                (
                                    MarkdownEvents: g.Count(),
                                    AvgMarkdownPct: decimal.Round(
                                        g
                                            .Where(x => x.StaraProdajnaCena.HasValue
                                                        && x.NovaProdajnaCena.HasValue
                                                        && x.StaraProdajnaCena.Value > 0m
                                                        && x.NovaProdajnaCena.Value < x.StaraProdajnaCena.Value)
                                            .Select(x => ((x.StaraProdajnaCena!.Value - x.NovaProdajnaCena!.Value) / x.StaraProdajnaCena!.Value) * 100m)
                                            .DefaultIfEmpty(0m)
                                            .Average(),
                                        2)));
                    }
                    catch (Exception ex)
                    {
                        loggerFactory.CreateLogger("PreNivelacijaPriorityEndpoints")
                            .LogWarning(ex, "Pre-nivelacija markdown evidence query failed.");
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
                        var velocity180 = decimal.Round(units180 / 180m, 4);
                        var lastSaleDate = salesLite?.LastSaleDateUtc;
                        var daysSinceLastSale = lastSaleDate.HasValue
                            ? Math.Max(0, (nowUtc.Date - lastSaleDate.Value.Date).Days)
                            : 999;

                        if (noSaleDaysMin.HasValue && daysSinceLastSale < noSaleDaysMin.Value)
                            continue;

                        var markdownEvents = markdownByArtikal.TryGetValue(a.Id, out var markdownLite) ? markdownLite.MarkdownEvents : 0;
                        var avgMarkdownPct = markdownByArtikal.TryGetValue(a.Id, out markdownLite) ? markdownLite.AvgMarkdownPct : 0m;

                        var marginEvidence = ResolveMarginEvidence(a.SellingPrice, a.PurchasePrice);
                        var salesEvidence = ResolveSalesEvidence(
                            salesLite is not null,
                            salesLite?.Units180 ?? 0,
                            salesLite?.NegativeUnits180 ?? 0);
                        var sellingPrice = marginEvidence.SellingPriceForScenarios;
                        var purchasePrice = marginEvidence.PurchasePriceForScenarios;
                        var hasCompleteEvidence = marginEvidence.HasCompleteEvidence && salesEvidence.IsComplete;
                        var grossMarginPct = marginEvidence.GrossMarginPctEst ?? 0m;

                        if (marginFloor.HasValue)
                        {
                            if (!hasCompleteEvidence || grossMarginPct < marginFloor.Value)
                                continue;
                        }

                        var seasonRecencyBoost = ResolveSeasonRecencyBoost(a.SeasonId, seasons, todayUtc);
                        var breakdown = scoring.ComputeScoreBreakdown(
                            a.StockUnits,
                            velocity180,
                            daysSinceLastSale,
                            markdownEvents,
                            avgMarkdownPct,
                            hasCompleteEvidence ? grossMarginPct : 0m,
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
                            preNivelacijaScore,
                            hasReliableCost: marginEvidence.HasCompleteEvidence);

                        var evidenceReasons = new[] { marginEvidence.EvidenceReason, salesEvidence.Reason }
                            .Where(reason => !string.IsNullOrWhiteSpace(reason))
                            .ToArray();

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
                            PositiveUnits180 = salesLite?.PositiveUnits180 ?? 0,
                            NegativeUnits180 = salesLite?.NegativeUnits180 ?? 0,
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
                            MarginDeltaHighlightVsMarkdown = hasCompleteEvidence
                                ? decimal.Round(highlight.ExpectedMargin30d - markdown.ExpectedMargin30d, 2)
                                : 0m,
                            RevenueDeltaHighlightVsMarkdown = decimal.Round(highlight.ExpectedRevenue30d - markdown.ExpectedRevenue30d, 2),
                            HasCompleteEvidence = hasCompleteEvidence,
                            EvidenceReason = evidenceReasons.Length == 0 ? null : string.Join(",", evidenceReasons),
                            SalesEvidenceStatus = salesEvidence.Status,
                            SalesEvidenceReason = salesEvidence.Reason,
                            Confidence = hasCompleteEvidence ? confidence : "Low",
                            Units7 = salesLite?.Units7 ?? 0,
                            UnitsPrev7 = salesLite?.UnitsPrev7 ?? 0
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
                            HasCompleteEvidence: candidate.HasCompleteEvidence,
                            SalesEvidenceStatus: candidate.SalesEvidenceStatus));

                        candidate.DecisionScore = recommendation.DecisionScore;
                        candidate.ReliabilityPct = recommendation.ReliabilityPct;
                        candidate.Recommendation = recommendation.Recommendation;
                    }

                    allCandidates = allCandidates
                        .OrderByDescending(x => x.PreNivelacijaScore)
                        .ThenByDescending(x => x.DecisionScore)
                        .ThenByDescending(x => x.StockUnits)
                        .ToList();

                    // Cache stores the facet universe (non-facet filters only). Dimension filters and
                    // summary/leaderboard projection happen after cache read.
                    return new PreNivelacijaPriorityBaseCacheEntry
                    {
                        GeneratedAtUtc = nowUtc,
                        FormulaVersion = "pre_nivelacija_v5",
                        FormulaDescription = BuildFormulaDescription(),
                        Summary = new PreNivelacijaSummaryDto(),
                        SupplierLeaderboard = [],
                        Candidates = allCandidates,
                        FacetUniverseCandidates = allCandidates,
                        Queues = new PreNivelacijaQueuesDto(),
                        Alerts = [],
                        EvidenceWindow = BuildEvidenceWindow(from180Utc, nowUtc, allCandidates, []),
                        TotalCandidates = allCandidates.Count,
                        RecommendationAllowed = allCandidates.All(x => x.Recommendation.RecommendationAllowed)
                    };
                },
                CacheExpiration.HeavyAnalytics,
                ct);

            var response = BuildResponse(
                MaterializeFacetFilteredEntry(baseEntry, supplierId, seasonId, footwearTypeId),
                page,
                pageSize,
                focus,
                BuildFilterFacets(
                    baseEntry.FacetUniverseCandidates.Count > 0
                        ? baseEntry.FacetUniverseCandidates
                        : baseEntry.Candidates,
                    supplierId,
                    seasonId,
                    footwearTypeId));

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
                return Results.Ok(BuildResponse(
                    unavailable,
                    page,
                    pageSize,
                    focus,
                    new PreNivelacijaFilterFacetsDto()));
            }
        })
        .WithName("GetPreNivelacijaPrioriteti")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");
    }

    private static string BuildFormulaDescription()
    {
        return "Skor pre-nivelacije = 0,30*pritisak_zalihe + 0,25*rizik_brzine_prodaje + 0,20*rizik_svežine + 0,10*prilika_za_sniženje + 0,10*potencijal_marže + 0,05*sezonski_signal; preporuka = 0,50*skor + 0,20*razlika_scenarija + 0,15*rizik_zastarelosti + 0,15*pouzdanost. Prozor prodaje i nivelacija: poslednjih 180 dana u UTC; količina je potpisana neto vrednost.";
    }

    internal static bool IsHighPriorityCandidate(PreNivelacijaSkuCandidateDto candidate)
    {
        return string.Equals(candidate.PriorityBand, "high", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Revenue-uplift KPI includes only allowed increase_focus ("Pojačaj") rows with a positive revenue delta.
    /// Blocked recommendations stay out so the KPI matches table gating.
    /// </summary>
    internal static bool IsRevenueUpliftEligible(PreNivelacijaSkuCandidateDto candidate)
    {
        return string.Equals(candidate.Recommendation.Status, "increase_focus", StringComparison.OrdinalIgnoreCase)
            && candidate.Recommendation.RecommendationAllowed
            && candidate.RevenueDeltaHighlightVsMarkdown > 0m;
    }

    /// <summary>
    /// Avoidable markdown-loss KPI includes only complete cost/sales evidence with a positive margin delta.
    /// Missing-cost rows must not inflate this margin KPI.
    /// </summary>
    internal static bool IsAvoidableMarginLossEligible(PreNivelacijaSkuCandidateDto candidate)
    {
        return candidate.HasCompleteEvidence
            && candidate.MarginDeltaHighlightVsMarkdown > 0m;
    }

    internal static PreNivelacijaSummaryDto BuildSummary(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates,
        IReadOnlyList<PreNivelacijaSupplierActionDto> supplierLeaderboard)
    {
        var highPriority = candidates.Where(IsHighPriorityCandidate).ToList();
        var revenueUpliftEligible = candidates.Where(IsRevenueUpliftEligible).ToList();
        var avoidableLossEligible = candidates.Where(IsAvoidableMarginLossEligible).ToList();
        var totalCandidates = candidates.Count;

        return new PreNivelacijaSummaryDto
        {
            SupplierCount = supplierLeaderboard.Count,
            CandidatesCount = totalCandidates,
            HighPriorityCount = highPriority.Count,
            IncreaseFocusCount = candidates.Count(x => string.Equals(x.Recommendation.Status, "increase_focus", StringComparison.OrdinalIgnoreCase)),
            MaintainCount = candidates.Count(x => string.Equals(x.Recommendation.Status, "maintain", StringComparison.OrdinalIgnoreCase)),
            ReviewCount = candidates.Count(x => string.Equals(x.Recommendation.Status, "review", StringComparison.OrdinalIgnoreCase)),
            DoNotTrustCount = candidates.Count(x => string.Equals(x.Recommendation.Status, "do_not_trust", StringComparison.OrdinalIgnoreCase)),
            InsufficientDataCount = candidates.Count(x => string.Equals(x.Recommendation.Status, "insufficient_data", StringComparison.OrdinalIgnoreCase)),
            TotalStockAtRisk = highPriority.Count == 0 ? null : highPriority.Sum(x => x.StockUnits),
            TotalStockAtRiskCoverageEligible = highPriority.Count,
            TotalStockAtRiskCoverageTotal = totalCandidates,
            EstimatedAvoidableMarkdownLoss = avoidableLossEligible.Count == 0
                ? null
                : decimal.Round(avoidableLossEligible.Sum(x => x.MarginDeltaHighlightVsMarkdown), 2),
            EstimatedAvoidableMarkdownLossCoverageEligible = avoidableLossEligible.Count,
            EstimatedAvoidableMarkdownLossCoverageTotal = totalCandidates,
            ExpectedHighlightRevenueUplift = revenueUpliftEligible.Count == 0
                ? null
                : decimal.Round(revenueUpliftEligible.Sum(x => x.RevenueDeltaHighlightVsMarkdown), 2),
            ExpectedHighlightRevenueUpliftCoverageEligible = revenueUpliftEligible.Count,
            ExpectedHighlightRevenueUpliftCoverageTotal = totalCandidates,
            AveragePreNivelacijaScore = totalCandidates == 0 ? 0m : decimal.Round(candidates.Average(x => x.PreNivelacijaScore), 2)
        };
    }

    internal static string NormalizeDataScope(string? rawScope)
    {
        var normalized = (rawScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
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

    internal readonly record struct PreNivelacijaMarginEvidence(
        bool HasCompleteEvidence,
        string? EvidenceReason,
        decimal? GrossMarginPctEst,
        decimal SellingPriceForScenarios,
        decimal PurchasePriceForScenarios);

    internal readonly record struct PreNivelacijaSalesEvidence(
        bool IsComplete,
        string Status,
        string? Reason);

    internal static PreNivelacijaSalesEvidence ResolveSalesEvidence(
        bool hasSalesRows,
        int signedUnits180,
        int negativeUnits180)
    {
        if (!hasSalesRows)
        {
            return new PreNivelacijaSalesEvidence(false, "no_sales_in_window", "no_sales_in_window");
        }

        if (negativeUnits180 < 0)
        {
            return signedUnits180 <= 0
                ? new PreNivelacijaSalesEvidence(false, "non_positive_net_with_returns", "signed_sales_non_positive")
                : new PreNivelacijaSalesEvidence(false, "signed_adjustment", "signed_sales_adjustment");
        }

        if (signedUnits180 < 0)
        {
            return new PreNivelacijaSalesEvidence(false, "negative_net_sales", "negative_net_sales");
        }

        if (signedUnits180 == 0)
        {
            return new PreNivelacijaSalesEvidence(false, "zero_net_sales", "zero_net_sales");
        }

        return new PreNivelacijaSalesEvidence(true, "positive_net_sales", null);
    }

    internal static decimal? CalculateWeekOverWeekRiskDelta(int last7Units, int previous7Units)
    {
        if (previous7Units <= 0)
        {
            return null;
        }

        return decimal.Round(((previous7Units - last7Units) / (decimal)previous7Units) * 100m, 2);
    }

    private static PreNivelacijaEvidenceWindowDto BuildEvidenceWindow(
        DateTime salesWindowFromUtc,
        DateTime salesWindowToUtc,
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates,
        IReadOnlyList<PreNivelacijaSupplierActionDto> suppliers)
    {
        return new PreNivelacijaEvidenceWindowDto
        {
            SalesWindowFromUtc = salesWindowFromUtc,
            SalesWindowToUtc = salesWindowToUtc,
            MarkdownWindowFromUtc = salesWindowFromUtc,
            MarkdownWindowToUtc = salesWindowToUtc,
            CandidatesWithReturns = candidates.Count(x => x.NegativeUnits180 < 0),
            CandidatesWithNonPositiveNetSales = candidates.Count(x => x.Units180 <= 0),
            CandidatesWithoutSalesInWindow = candidates.Count(x => string.Equals(x.SalesEvidenceStatus, "no_sales_in_window", StringComparison.OrdinalIgnoreCase)),
            SuppliersWithUnavailablePreviousWeekDenominator = suppliers.Count(x => !string.Equals(x.WeekOverWeekEvidenceStatus, "measured", StringComparison.OrdinalIgnoreCase))
        };
    }

    /// <summary>
    /// Aligns pre-nivelacija completeness with <see cref="AnalyticsMarginPolicy.IsReliableCost"/>:
    /// null/zero/negative purchase cost is missing evidence, never a 100% margin signal.
    /// </summary>
    internal static PreNivelacijaMarginEvidence ResolveMarginEvidence(decimal? sellingPrice, decimal? purchasePrice)
    {
        var hasSellingPrice = sellingPrice.HasValue && sellingPrice.Value > 0m;
        var hasReliableCost = AnalyticsMarginPolicy.IsReliableCost(purchasePrice);

        if (!hasSellingPrice && !hasReliableCost)
        {
            return new PreNivelacijaMarginEvidence(false, "missing_price_baseline", null, 0m, 0m);
        }

        if (!hasSellingPrice)
        {
            return new PreNivelacijaMarginEvidence(false, "missing_selling_price", null, 0m, purchasePrice!.Value);
        }

        if (!hasReliableCost)
        {
            var reason = !purchasePrice.HasValue
                ? "missing_purchase_cost"
                : "non_positive_purchase_cost";
            return new PreNivelacijaMarginEvidence(false, reason, null, sellingPrice!.Value, 0m);
        }

        var sell = sellingPrice!.Value;
        var cost = purchasePrice!.Value;
        var grossMarginPct = decimal.Round(
            Math.Clamp(((sell - cost) / sell) * 100m, 0m, 100m),
            2);

        return new PreNivelacijaMarginEvidence(true, null, grossMarginPct, sell, cost);
    }

    private static PreNivelacijaPriorityBaseCacheEntry BuildEmptyBaseEntry(
        DateTime nowUtc,
        AnalyticsResponseMetaDto? meta = null)
    {
        return new PreNivelacijaPriorityBaseCacheEntry
        {
            GeneratedAtUtc = nowUtc,
            FormulaVersion = "pre_nivelacija_v5",
            FormulaDescription = BuildFormulaDescription(),
            Summary = new PreNivelacijaSummaryDto
            {
                SupplierCount = 0,
                CandidatesCount = 0,
                HighPriorityCount = 0,
                TotalStockAtRisk = null,
                TotalStockAtRiskCoverageEligible = 0,
                TotalStockAtRiskCoverageTotal = 0,
                EstimatedAvoidableMarkdownLoss = null,
                EstimatedAvoidableMarkdownLossCoverageEligible = 0,
                EstimatedAvoidableMarkdownLossCoverageTotal = 0,
                ExpectedHighlightRevenueUplift = null,
                ExpectedHighlightRevenueUpliftCoverageEligible = 0,
                ExpectedHighlightRevenueUpliftCoverageTotal = 0,
                AveragePreNivelacijaScore = 0
            },
            SupplierLeaderboard = [],
            Candidates = [],
            FacetUniverseCandidates = [],
            Queues = new PreNivelacijaQueuesDto(),
            Alerts = [],
            EvidenceWindow = BuildEvidenceWindow(nowUtc.AddDays(-180), nowUtc, [], []),
            TotalCandidates = 0,
            Meta = meta ?? AnalyticsResponseMetaFactory.Empty(
                "no_pre_nivelacija_candidates",
                "Nema kandidata za pre-nivelaciju.")
        };
    }

    internal const string PreNivelacijaPercentagePointsUnit = "percentage_points";
    internal const string SupplierActionShareDenominatorPolicy = "leaderboard_action_score_full_population_top_seven_plus_other";

    internal static PreNivelacijaSupplierActionShareProjectionDto BuildSupplierActionShareProjection(
        IReadOnlyList<PreNivelacijaSupplierActionDto> leaderboard,
        int visibleSupplierLimit = 7)
    {
        var scoredSuppliers = leaderboard
            .Where(x => x.ActionScore > 0m)
            .OrderByDescending(x => x.ActionScore)
            .ToList();

        if (scoredSuppliers.Count == 0)
        {
            return new PreNivelacijaSupplierActionShareProjectionDto
            {
                ShareUnit = PreNivelacijaPercentagePointsUnit,
                WeekOverWeekRiskDeltaUnit = PreNivelacijaPercentagePointsUnit,
                DenominatorPolicy = SupplierActionShareDenominatorPolicy,
                DenominatorLabel = "Nema pozitivnog action score-a u leaderboard-u; udeo u akciji nije dostupan.",
            };
        }

        var totalActionScore = scoredSuppliers.Sum(x => x.ActionScore);
        if (totalActionScore <= 0m)
        {
            return new PreNivelacijaSupplierActionShareProjectionDto
            {
                ShareUnit = PreNivelacijaPercentagePointsUnit,
                WeekOverWeekRiskDeltaUnit = PreNivelacijaPercentagePointsUnit,
                DenominatorPolicy = SupplierActionShareDenominatorPolicy,
                DenominatorLabel = "Ukupan action score je nula; udeo u akciji nije dostupan.",
                LeaderboardSupplierCount = scoredSuppliers.Count,
            };
        }

        var visibleSuppliers = scoredSuppliers.Take(visibleSupplierLimit).ToList();
        var includedActionScore = visibleSuppliers.Sum(x => x.ActionScore);
        var otherActionScore = decimal.Round(totalActionScore - includedActionScore, 2);

        var segments = visibleSuppliers
            .Select(supplier => new PreNivelacijaSupplierActionShareSegmentDto
            {
                SupplierId = supplier.SupplierId,
                SupplierName = supplier.SupplierName,
                ActionSharePct = decimal.Round((supplier.ActionScore / totalActionScore) * 100m, 2),
                WeekOverWeekRiskDeltaPct = supplier.WeekOverWeekRiskDeltaPct,
                WeekOverWeekRiskDeltaUnit = PreNivelacijaPercentagePointsUnit,
                IsOther = false,
            })
            .ToList();

        decimal? otherSharePct = null;
        if (otherActionScore > 0m)
        {
            otherSharePct = decimal.Round((otherActionScore / totalActionScore) * 100m, 2);
            segments.Add(new PreNivelacijaSupplierActionShareSegmentDto
            {
                SupplierName = "Ostali",
                ActionSharePct = otherSharePct.Value,
                WeekOverWeekRiskDeltaUnit = PreNivelacijaPercentagePointsUnit,
                IsOther = true,
            });
        }

        return new PreNivelacijaSupplierActionShareProjectionDto
        {
            ShareUnit = PreNivelacijaPercentagePointsUnit,
            WeekOverWeekRiskDeltaUnit = PreNivelacijaPercentagePointsUnit,
            DenominatorPolicy = SupplierActionShareDenominatorPolicy,
            DenominatorLabel = "Udeo u akciji u odnosu na ukupan action score svih dobavljača u leaderboard-u; prikaz top 7 plus Ostali kada postoji preostali udeo.",
            LeaderboardSupplierCount = scoredSuppliers.Count,
            VisibleSupplierCount = visibleSuppliers.Count,
            TotalActionScore = decimal.Round(totalActionScore, 2),
            IncludedActionScore = decimal.Round(includedActionScore, 2),
            OtherActionScore = otherActionScore,
            OtherSharePct = otherSharePct,
            Segments = segments,
        };
    }

    internal static IReadOnlyList<PreNivelacijaSkuCandidateDto> FilterCandidatesByFocus(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates,
        string? focus)
    {
        var normalized = (focus ?? "all").Trim();
        if (normalized.Length == 0 || string.Equals(normalized, "all", StringComparison.OrdinalIgnoreCase))
        {
            return candidates;
        }

        return normalized switch
        {
            "increaseFocus" => candidates
                .Where(x => string.Equals(x.Recommendation.Status, "increase_focus", StringComparison.OrdinalIgnoreCase))
                .ToList(),
            "maintain" => candidates
                .Where(x => string.Equals(x.Recommendation.Status, "maintain", StringComparison.OrdinalIgnoreCase))
                .ToList(),
            "review" => candidates
                .Where(x => string.Equals(x.Recommendation.Status, "review", StringComparison.OrdinalIgnoreCase))
                .ToList(),
            "doNotTrust" => candidates
                .Where(x => string.Equals(x.Recommendation.Status, "do_not_trust", StringComparison.OrdinalIgnoreCase))
                .ToList(),
            "insufficientData" => candidates
                .Where(x => string.Equals(x.Recommendation.Status, "insufficient_data", StringComparison.OrdinalIgnoreCase))
                .ToList(),
            "highPriority" => candidates.Where(IsHighPriorityCandidate).ToList(),
            _ => candidates
        };
    }

    private static PreNivelacijaPriorityResponseDto BuildResponse(
        PreNivelacijaPriorityBaseCacheEntry baseEntry,
        int page,
        int pageSize,
        string? focus = null,
        PreNivelacijaFilterFacetsDto? filterFacets = null)
    {
        var filteredCandidates = FilterCandidatesByFocus(baseEntry.Candidates, focus);
        var totalFilteredCandidates = filteredCandidates.Count;
        var pagedCandidates = filteredCandidates
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
            SupplierActionShare = BuildSupplierActionShareProjection(baseEntry.SupplierLeaderboard),
            FilterFacets = filterFacets ?? BuildFilterFacets(baseEntry.Candidates, null, null, null),
            Candidates = pagedCandidates,
            Queues = baseEntry.Queues,
            Alerts = baseEntry.Alerts,
            Page = page,
            PageSize = pageSize,
            TotalCandidates = totalFilteredCandidates,
            RecommendationAllowed = baseEntry.RecommendationAllowed,
            EvidenceWindow = baseEntry.EvidenceWindow,
            Meta = baseEntry.Meta ?? AnalyticsResponseMetaFactory.Success()
        };

        response.Meta!.RecommendationAllowed = response.RecommendationAllowed;
        response.Meta.RequestedPeriodFromUtc = baseEntry.EvidenceWindow.SalesWindowFromUtc;
        response.Meta.RequestedPeriodToUtc = baseEntry.EvidenceWindow.SalesWindowToUtc;
        response.Meta.EffectivePeriodFromUtc = baseEntry.EvidenceWindow.SalesWindowFromUtc;
        response.Meta.EffectivePeriodToUtc = baseEntry.EvidenceWindow.SalesWindowToUtc;
        response.Meta.ObservedPeriodFromUtc = baseEntry.EvidenceWindow.SalesWindowFromUtc;
        response.Meta.ObservedPeriodToUtc = baseEntry.EvidenceWindow.SalesWindowToUtc;
        return response;
    }

    private static PreNivelacijaPriorityBaseCacheEntry MaterializeFacetFilteredEntry(
        PreNivelacijaPriorityBaseCacheEntry universeEntry,
        int? supplierId,
        int? seasonId,
        int? footwearTypeId)
    {
        var universe = universeEntry.FacetUniverseCandidates.Count > 0
            ? universeEntry.FacetUniverseCandidates
            : universeEntry.Candidates;
        var filtered = ApplyDimensionFilters(universe, supplierId, seasonId, footwearTypeId);
        var leaderboard = BuildSupplierLeaderboard(filtered);
        var summary = BuildSummary(filtered, leaderboard);
        var queues = BuildQueues(filtered, universeEntry.GeneratedAtUtc);
        var alerts = BuildAlerts(filtered, leaderboard);
        var evidenceWindow = BuildEvidenceWindow(
            universeEntry.EvidenceWindow.SalesWindowFromUtc,
            universeEntry.EvidenceWindow.SalesWindowToUtc,
            filtered,
            leaderboard);

        return new PreNivelacijaPriorityBaseCacheEntry
        {
            GeneratedAtUtc = universeEntry.GeneratedAtUtc,
            FormulaVersion = universeEntry.FormulaVersion,
            FormulaDescription = universeEntry.FormulaDescription,
            Summary = summary,
            SupplierLeaderboard = leaderboard,
            Candidates = filtered,
            FacetUniverseCandidates = universe.ToList(),
            Queues = queues,
            Alerts = alerts,
            EvidenceWindow = evidenceWindow,
            TotalCandidates = filtered.Count,
            RecommendationAllowed = filtered.Count == 0
                ? universeEntry.RecommendationAllowed
                : filtered.All(x => x.Recommendation.RecommendationAllowed),
            Meta = filtered.Count == 0 && universe.Count > 0
                ? AnalyticsResponseMetaFactory.Empty(
                    "no_pre_nivelacija_candidates_for_filters",
                    "Nema kandidata za izabrane filtere pre-nivelacije.")
                : universeEntry.Meta
        };
    }

    internal static List<PreNivelacijaSkuCandidateDto> ApplyDimensionFilters(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates,
        int? supplierId,
        int? seasonId,
        int? footwearTypeId)
    {
        IEnumerable<PreNivelacijaSkuCandidateDto> query = candidates;
        if (supplierId.HasValue)
        {
            query = query.Where(candidate => candidate.SupplierId == supplierId.Value);
        }

        if (seasonId.HasValue)
        {
            query = query.Where(candidate => candidate.SeasonId == seasonId.Value);
        }

        if (footwearTypeId.HasValue)
        {
            query = query.Where(candidate => candidate.FootwearTypeId == footwearTypeId.Value);
        }

        return query.ToList();
    }

    internal static List<PreNivelacijaSupplierActionDto> BuildSupplierLeaderboard(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates)
    {
        return candidates
            .GroupBy(x => new { x.SupplierId, x.SupplierName })
            .Select(g =>
            {
                var highCount = g.Count(IsHighPriorityCandidate);
                var candidateCount = g.Count();
                var stockAtRisk = g.Where(IsHighPriorityCandidate).Sum(x => x.StockUnits);
                var avoidableLoss = g.Where(IsAvoidableMarginLossEligible).Sum(x => x.MarginDeltaHighlightVsMarkdown);
                var expectedUplift = g.Where(IsRevenueUpliftEligible).Sum(x => x.RevenueDeltaHighlightVsMarkdown);
                var last7 = g.Sum(x => x.Units7);
                var prev7 = g.Sum(x => x.UnitsPrev7);
                var wowRiskDelta = CalculateWeekOverWeekRiskDelta(last7, prev7);

                var actionScore = decimal.Round(
                    (highCount * 10m)
                    + (avoidableLoss / 1000m)
                    + (expectedUplift / 5000m)
                    + (Math.Max(0m, wowRiskDelta ?? 0m) / 10m),
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
                    WeekOverWeekRiskDeltaPct = wowRiskDelta,
                    WeekOverWeekEvidenceStatus = prev7 > 0
                        ? "measured"
                        : "unavailable_non_positive_denominator"
                };
            })
            .OrderByDescending(x => x.ActionScore)
            .ToList();
    }

    internal static PreNivelacijaQueuesDto BuildQueues(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates,
        DateTime nowUtc)
    {
        return new PreNivelacijaQueuesDto
        {
            HighlightNow = candidates
                .Where(x => IsHighPriorityCandidate(x) && x.Recommendation.RecommendationAllowed)
                .Take(30)
                .Select(x => ToQueueItem(x, nowUtc.AddDays(2)))
                .ToList(),
            Monitor = candidates
                .Where(x => x.PriorityBand == "medium" && x.Recommendation.RecommendationAllowed)
                .Take(30)
                .Select(x => ToQueueItem(x, nowUtc.AddDays(7)))
                .ToList(),
            LikelyMarkdownSoon = candidates
                .Where(x => x.Recommendation.RecommendationAllowed
                    && (x.DaysSinceLastSale >= 60 || x.MarkdownEvents >= 2 || x.AvgMarkdownPct >= 25m))
                .OrderByDescending(x => x.DaysSinceLastSale)
                .ThenByDescending(x => x.StockUnits)
                .Take(30)
                .Select(x => ToQueueItem(x, nowUtc.AddDays(3)))
                .ToList()
        };
    }

    internal static PreNivelacijaFilterFacetsDto BuildFilterFacets(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> universe,
        int? selectedSupplierId,
        int? selectedSeasonId,
        int? selectedFootwearTypeId)
    {
        var supplierUniverse = ApplyDimensionFilters(universe, null, selectedSeasonId, selectedFootwearTypeId);
        var seasonUniverse = ApplyDimensionFilters(universe, selectedSupplierId, null, selectedFootwearTypeId);
        var footwearUniverse = ApplyDimensionFilters(universe, selectedSupplierId, selectedSeasonId, null);

        var suppliers = AggregateSupplierFacet(supplierUniverse);
        var seasons = AggregateSeasonFacet(seasonUniverse);
        var footwearTypes = AggregateFootwearFacet(footwearUniverse);

        EnsureSelectedFacetOption(
            suppliers,
            selectedSupplierId,
            universe,
            candidate => candidate.SupplierId,
            candidate => candidate.SupplierName);
        EnsureSelectedFacetOption(
            seasons,
            selectedSeasonId,
            universe,
            candidate => candidate.SeasonId,
            candidate => candidate.Season);
        EnsureSelectedFacetOption(
            footwearTypes,
            selectedFootwearTypeId,
            universe,
            candidate => candidate.FootwearTypeId,
            candidate => candidate.FootwearType);

        return new PreNivelacijaFilterFacetsDto
        {
            Suppliers = suppliers,
            Seasons = seasons,
            FootwearTypes = footwearTypes
        };
    }

    private static List<PreNivelacijaFilterOptionDto> AggregateSupplierFacet(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates)
    {
        return candidates
            .Where(candidate => candidate.SupplierId is > 0
                && !string.IsNullOrWhiteSpace(candidate.SupplierName)
                && !string.Equals(candidate.SupplierName, "N/A", StringComparison.OrdinalIgnoreCase))
            .GroupBy(candidate => candidate.SupplierId!.Value)
            .Select(group => new PreNivelacijaFilterOptionDto
            {
                Id = group.Key,
                Label = group.Select(item => item.SupplierName.Trim()).First(label => label.Length > 0),
                Count = group.Count()
            })
            .OrderBy(option => option.Label, StringComparer.Create(new System.Globalization.CultureInfo("sr-Latn-RS"), ignoreCase: true))
            .ToList();
    }

    private static List<PreNivelacijaFilterOptionDto> AggregateSeasonFacet(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates)
    {
        var seasons = new Dictionary<int, (string Label, int Count)>();
        foreach (var candidate in candidates)
        {
            if (candidate.SeasonId is not > 0
                || string.IsNullOrWhiteSpace(candidate.Season)
                || string.Equals(candidate.Season, "N/A", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (seasons.TryGetValue(candidate.SeasonId.Value, out var existing))
            {
                seasons[candidate.SeasonId.Value] = (existing.Label, existing.Count + 1);
            }
            else
            {
                seasons[candidate.SeasonId.Value] = (candidate.Season.Trim(), 1);
            }
        }

        return seasons
            .Select(pair => new PreNivelacijaFilterOptionDto
            {
                Id = pair.Key,
                Label = pair.Value.Label,
                Count = pair.Value.Count
            })
            .OrderBy(option => option.Label, StringComparer.Create(new System.Globalization.CultureInfo("sr-Latn-RS"), ignoreCase: true))
            .ToList();
    }

    private static List<PreNivelacijaFilterOptionDto> AggregateFootwearFacet(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates)
    {
        var footwearTypes = new Dictionary<int, (string Label, int Count)>();
        foreach (var candidate in candidates)
        {
            if (candidate.FootwearTypeId is not > 0
                || string.IsNullOrWhiteSpace(candidate.FootwearType)
                || string.Equals(candidate.FootwearType, "N/A", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (footwearTypes.TryGetValue(candidate.FootwearTypeId.Value, out var existing))
            {
                footwearTypes[candidate.FootwearTypeId.Value] = (existing.Label, existing.Count + 1);
            }
            else
            {
                footwearTypes[candidate.FootwearTypeId.Value] = (candidate.FootwearType.Trim(), 1);
            }
        }

        return footwearTypes
            .Select(pair => new PreNivelacijaFilterOptionDto
            {
                Id = pair.Key,
                Label = pair.Value.Label,
                Count = pair.Value.Count
            })
            .OrderBy(option => option.Label, StringComparer.Create(new System.Globalization.CultureInfo("sr-Latn-RS"), ignoreCase: true))
            .ToList();
    }

    private static void EnsureSelectedFacetOption(
        List<PreNivelacijaFilterOptionDto> options,
        int? selectedId,
        IReadOnlyList<PreNivelacijaSkuCandidateDto> universe,
        Func<PreNivelacijaSkuCandidateDto, int?> idSelector,
        Func<PreNivelacijaSkuCandidateDto, string> labelSelector)
    {
        if (!selectedId.HasValue || options.Any(option => option.Id == selectedId.Value))
        {
            return;
        }

        var label = universe
            .Where(candidate => idSelector(candidate) == selectedId.Value)
            .Select(labelSelector)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase));

        options.Insert(0, new PreNivelacijaFilterOptionDto
        {
            Id = selectedId.Value,
            Label = string.IsNullOrWhiteSpace(label) ? $"Nepoznata vrednost ({selectedId.Value})" : label.Trim(),
            Count = 0
        });
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
