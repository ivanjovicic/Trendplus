using System.Globalization;
using Api.Models;
using Api.Services;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Domain.Model;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Trendplus2.Dtos;

namespace Trendplus2.Endpoints;

public static class PreNivelacijaPriorityEndpoints
{
    internal const int DefaultMinimumNewStockAgeDays = 30;

    private sealed class SalesLite
    {
        public int Units180 { get; init; }
        public int PositiveUnits180 { get; init; }
        public int NegativeUnits180 { get; init; }
        public int Units7 { get; init; }
        public int UnitsPrev7 { get; init; }
        public DateTime? LastPositiveSaleDateUtc { get; init; }
    }

    private sealed record SalesHistoryLite(DateTime FirstPositiveSaleDateUtc, DateTime LastPositiveSaleDateUtc);

    private sealed record MarkdownEventLite(int? StoreId, decimal? OldPrice, decimal? NewPrice);

    private sealed class SeasonLite
    {
        public string Naziv { get; init; } = "N/A";
        public DateTime DatumOd { get; init; }
        public DateTime DatumDo { get; init; }
    }

    private sealed class PreNivelacijaPriorityBaseCacheEntry
    {
        public DateTime GeneratedAtUtc { get; init; }
        public string FormulaVersion { get; init; } = "pre_nivelacija_v10";
        public string FormulaDescription { get; init; } = string.Empty;
        public PreNivelacijaModelEvidenceDto ModelEvidence { get; init; } = new();
        public PreNivelacijaSummaryDto Summary { get; init; } = new();
        public List<PreNivelacijaSupplierActionDto> SupplierLeaderboard { get; init; } = [];
        /// <summary>
        /// Scored candidates for non-facet filters only. Supplier/season/type filters are applied after cache read
        /// so filter facets can use leave-one-out universes.
        /// </summary>
        public List<PreNivelacijaSkuCandidateDto> Candidates { get; init; } = [];
        public List<PreNivelacijaSkuCandidateDto> FacetUniverseCandidates { get; init; } = [];
        public List<PreNivelacijaNewStockQueueItemDto> NewStockCandidates { get; init; } = [];
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
            IAnalyticsDbContext analyticsDb,
            IPreNivelacijaScoringService scoring,
            IAnalyticsCacheService cache,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            HttpContext httpContext,
            int? supplierId = null,
            int? seasonId = null,
            int? footwearTypeId = null,
            int? storeId = null,
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
            var normalizedDataScope = NormalizeDataScope(dataScope);
            var minimumNewStockAgeDays = ResolveMinimumNewStockAgeDays(configuration);
            var integrityRegistry = httpContext.RequestServices.GetService<OperationsAnalyticsIntegrityRegistry>();

            try
            {
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
                requestNowUtc.Date,
                storeId,
                minimumNewStockAgeDays);

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

                    // Supplier/season/footwear/store filters are applied after cache so facets stay leave-one-out
                    // and all-store scoring remains the explicit aggregation of the same store-grain rows.

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
                            StoreId = a.IDObjekat,
                            StockUnits = a.Kolicina ?? 0,
                            a.Kategorija,
                            SellingPrice = a.ProdajnaCena ?? a.PrvaProdajnaCena,
                            PurchasePrice = a.NabavnaCenaDin ?? a.NabavnaCena
                        })
                        .ToListAsync(ct);

                    if (artikli.Count == 0)
                    {
                        return BuildEmptyBaseEntry(nowUtc, minimumNewStockAgeDays: minimumNewStockAgeDays);
                    }

                    Dictionary<int, string> storeNames;
                    try
                    {
                        var storeIds = artikli
                            .Where(x => x.StoreId.HasValue)
                            .Select(x => x.StoreId!.Value)
                            .Distinct()
                            .ToArray();
                        storeNames = await analyticsDb.StoresDim
                            .AsNoTracking()
                            .Where(x => storeIds.Contains(x.StoreId))
                            .ToDictionaryAsync(
                                x => x.StoreId,
                                x => string.IsNullOrWhiteSpace(x.StoreName) ? EntityIdentity.FallbackLabel(EntityKind.Store, x.StoreId) : x.StoreName.Trim(),
                                ct);
                    }
                    catch (Exception ex)
                    {
                        loggerFactory.CreateLogger("PreNivelacijaPriorityEndpoints")
                            .LogWarning(ex, "Pre-nivelacija store-name lookup unavailable; using stable store IDs.");
                        storeNames = new Dictionary<int, string>();
                    }

                    var artikalIds = artikli.Select(x => x.Id).ToArray();

                    Dictionary<(int ArtikalId, int? StoreId), SalesLite> salesByArtikal;
                    try
                    {
                        var sales = await (
                            from ps in db.ProdajaStavke.AsNoTracking()
                            join p in db.ProdajaZaglavlja
                                .AsNoTracking()
                                .Where(SalesReceiptPopulationPolicy.IncludedHeaderPredicate)
                                on ps.IdProdaja equals p.Id
                            where artikalIds.Contains(ps.IdArtikal)
                                && p.DatumProdaje >= from180Utc
                                && p.DatumProdaje <= nowUtc
                                && (normalizedDataScope == "all"
                                    || (normalizedDataScope == "imported" && p.DataOrigin == "access")
                                    || (normalizedDataScope == "existing" && (p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")))
                            group new { ps, p } by new { ArtikalId = ps.IdArtikal, StoreId = p.IDObjekat } into g
                            select new
                            {
                                ArtikalId = g.Key.ArtikalId,
                                StoreId = g.Key.StoreId,
                                Units180 = g.Sum(x => x.ps.Kolicina),
                                PositiveUnits180 = g.Where(x => x.ps.Kolicina > 0).Sum(x => x.ps.Kolicina),
                                NegativeUnits180 = g.Where(x => x.ps.Kolicina < 0).Sum(x => x.ps.Kolicina),
                                LastPositiveSale = g
                                    .Where(x => x.ps.Kolicina > 0)
                                    .Select(x => (DateTime?)x.p.DatumProdaje)
                                    .Max(),
                                Units7 = g.Where(x => x.p.DatumProdaje >= last7FromUtc).Sum(x => x.ps.Kolicina),
                                UnitsPrev7 = g.Where(x => x.p.DatumProdaje >= prev7FromUtc && x.p.DatumProdaje < last7FromUtc).Sum(x => x.ps.Kolicina),
                            })
                            .ToListAsync(ct);

                        salesByArtikal = sales.ToDictionary(
                            x => (x.ArtikalId, x.StoreId),
                            x => new SalesLite
                            {
                                Units180 = x.Units180,
                                PositiveUnits180 = x.PositiveUnits180,
                                NegativeUnits180 = x.NegativeUnits180,
                                Units7 = x.Units7,
                                UnitsPrev7 = x.UnitsPrev7,
                                LastPositiveSaleDateUtc = x.LastPositiveSale
                            });
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        loggerFactory.CreateLogger("PreNivelacijaPriorityEndpoints")
                            .LogWarning(ex, "Pre-nivelacija sales evidence query failed.");
                        throw new PreNivelacijaQueryFailedException(
                            salesQueryFailed: true,
                            markdownQueryFailed: false,
                            ex);
                    }

                    Dictionary<int, MarkdownEventLite[]> markdownByArtikal;
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
                                Event = new MarkdownEventLite(dp.IDObjekat, dp.StaraProdajnaCena, dp.NovaProdajnaCena)
                            })
                            .ToListAsync(ct);

                        markdownByArtikal = markdownRows
                            .GroupBy(x => x.ArtikalId)
                            .ToDictionary(
                                g => g.Key,
                                g => g.Select(x => x.Event).ToArray());
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        loggerFactory.CreateLogger("PreNivelacijaPriorityEndpoints")
                            .LogWarning(ex, "Pre-nivelacija markdown evidence query failed.");
                        throw new PreNivelacijaQueryFailedException(
                            salesQueryFailed: false,
                            markdownQueryFailed: true,
                            ex);
                    }

                    Dictionary<(int ArtikalId, int? StoreId), DateTime> firstReceiptByArtikal;
                    try
                    {
                        var receipts = await db.DnevnikPromena
                            .AsNoTracking()
                            .Where(dp => dp.ArtikalId.HasValue
                                && artikalIds.Contains(dp.ArtikalId.Value)
                                && dp.TipPromene == TipPromeneConstants.UlazRobe
                                && dp.Datum <= nowUtc
                                && (normalizedDataScope == "all"
                                    || (normalizedDataScope == "imported" && dp.DataOrigin == "access")
                                    || (normalizedDataScope == "existing" && (dp.DataOrigin == "existing" || dp.DataOrigin == null || dp.DataOrigin == ""))))
                            .GroupBy(dp => new { ArtikalId = dp.ArtikalId!.Value, StoreId = dp.IDObjekat })
                            .Select(group => new
                            {
                                group.Key.ArtikalId,
                                group.Key.StoreId,
                                FirstReceiptDateUtc = group.Min(dp => dp.Datum)
                            })
                            .ToListAsync(ct);

                        firstReceiptByArtikal = receipts.ToDictionary(
                            receipt => (receipt.ArtikalId, receipt.StoreId),
                            receipt => receipt.FirstReceiptDateUtc);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        loggerFactory.CreateLogger("PreNivelacijaPriorityEndpoints")
                            .LogWarning(ex, "Pre-nivelacija receipt evidence query failed.");
                        throw new PreNivelacijaQueryFailedException(
                            salesQueryFailed: false,
                            markdownQueryFailed: false,
                            ex,
                            receiptQueryFailed: true);
                    }

                    Dictionary<(int ArtikalId, int? StoreId), SalesHistoryLite> salesHistoryByArtikal;
                    try
                    {
                        var salesHistory = await (
                            from ps in db.ProdajaStavke.AsNoTracking()
                            join p in db.ProdajaZaglavlja.AsNoTracking()
                                    .Where(SalesReceiptPopulationPolicy.IncludedHeaderPredicate)
                                on ps.IdProdaja equals p.Id
                            where artikalIds.Contains(ps.IdArtikal)
                                && ps.Kolicina > 0
                                && p.DatumProdaje <= nowUtc
                                && (normalizedDataScope == "all"
                                    || (normalizedDataScope == "imported" && p.DataOrigin == "access")
                                    || (normalizedDataScope == "existing" && (p.DataOrigin == "existing" || p.DataOrigin == null || p.DataOrigin == "")))
                            group p by new { ArtikalId = ps.IdArtikal, StoreId = p.IDObjekat } into historyGroup
                            select new
                            {
                                historyGroup.Key.ArtikalId,
                                historyGroup.Key.StoreId,
                                FirstPositiveSaleDateUtc = historyGroup.Min(header => header.DatumProdaje),
                                LastPositiveSaleDateUtc = historyGroup.Max(header => header.DatumProdaje)
                            })
                            .ToListAsync(ct);

                        salesHistoryByArtikal = salesHistory.ToDictionary(
                            history => (history.ArtikalId, history.StoreId),
                            history => new SalesHistoryLite(history.FirstPositiveSaleDateUtc, history.LastPositiveSaleDateUtc));
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        loggerFactory.CreateLogger("PreNivelacijaPriorityEndpoints")
                            .LogWarning(ex, "Pre-nivelacija historical sales evidence query failed.");
                        throw new PreNivelacijaQueryFailedException(
                            salesQueryFailed: true,
                            markdownQueryFailed: false,
                            ex);
                    }

                    var newStockCandidates = new List<PreNivelacijaNewStockQueueItemDto>();
                    var actionableArticles = new List<(int Id, string? PLU, int? SupplierId, int? SeasonId, int? FootwearTypeId,
                        int? StoreId, int StockUnits, string? Kategorija, decimal? SellingPrice, decimal? PurchasePrice)>();
                    foreach (var article in artikli)
                    {
                        var salesKey = (article.Id, article.StoreId);
                        salesHistoryByArtikal.TryGetValue(salesKey, out var salesHistory);
                        firstReceiptByArtikal.TryGetValue(salesKey, out var firstReceipt);
                        DateTime? firstReceiptDate = firstReceipt != default
                            ? firstReceipt
                            : salesHistory?.FirstPositiveSaleDateUtc;

                        if (firstReceiptDate.HasValue)
                        {
                            var daysSinceReceipt = Math.Max(0, (todayUtc - firstReceiptDate.Value.Date).Days);
                            if (daysSinceReceipt < minimumNewStockAgeDays)
                            {
                                var sku = !string.IsNullOrWhiteSpace(article.PLU)
                                    ? article.PLU.Trim()
                                    : article.Id.ToString(CultureInfo.InvariantCulture);
                                var lastSaleDate = salesHistory?.LastPositiveSaleDateUtc;
                                newStockCandidates.Add(new PreNivelacijaNewStockQueueItemDto
                                {
                                    ArtikalId = article.Id,
                                    Sku = sku,
                                    StoreId = article.StoreId,
                                    SupplierId = article.SupplierId,
                                    SeasonId = article.SeasonId,
                                    FootwearTypeId = article.FootwearTypeId,
                                    StoreName = ResolveStoreName(article.StoreId, storeNames),
                                    SupplierName = ResolveSupplierName(article.SupplierId, suppliers),
                                    StockUnits = article.StockUnits,
                                    FirstReceiptDateUtc = DateTime.SpecifyKind(firstReceiptDate.Value, DateTimeKind.Utc),
                                    DaysSinceReceipt = daysSinceReceipt,
                                    DaysSinceLastSale = lastSaleDate.HasValue
                                        ? Math.Max(0, (todayUtc - lastSaleDate.Value.Date).Days)
                                        : null,
                                    SalesHistoryStatus = ResolveSalesHistoryStatus(lastSaleDate, from180Utc),
                                    StockAgeStatus = "new_stock",
                                    ReasonCode = "new_stock"
                                });
                                continue;
                            }
                        }

                        actionableArticles.Add((
                            article.Id,
                            article.PLU,
                            article.SupplierId,
                            article.SeasonId,
                            article.FootwearTypeId,
                            article.StoreId,
                            article.StockUnits,
                            article.Kategorija,
                            article.SellingPrice,
                            article.PurchasePrice));
                    }

                    var maxStock = Math.Max(1, actionableArticles.Select(article => article.StockUnits).DefaultIfEmpty(1).Max());
                    var maxVelocity = actionableArticles
                        .Select(x => salesByArtikal.TryGetValue((x.Id, x.StoreId), out var salesLite) ? (decimal)salesLite.Units180 / 180m : 0m)
                        .DefaultIfEmpty(0m)
                        .Max();

                    var allCandidates = new List<PreNivelacijaSkuCandidateDto>(actionableArticles.Count);

                    foreach (var a in actionableArticles)
                    {
                        var sku = !string.IsNullOrWhiteSpace(a.PLU) ? a.PLU.Trim() : a.Id.ToString(CultureInfo.InvariantCulture);
                        var supplierName = ResolveSupplierName(a.SupplierId, suppliers);
                        var seasonName = ResolveSeasonName(a.SeasonId, seasons);
                        var footwearType = ResolveFootwearType(a.FootwearTypeId, footwearTypes);

                        var salesKey = (a.Id, a.StoreId);
                        var salesLite = salesByArtikal.GetValueOrDefault(salesKey);
                        salesHistoryByArtikal.TryGetValue(salesKey, out var salesHistory);
                        var units180 = salesLite?.Units180 ?? 0;
                        var velocity180 = decimal.Round(units180 / 180m, 4);
                        var lastSaleDate = salesHistory?.LastPositiveSaleDateUtc;
                        int? daysSinceLastSale = lastSaleDate.HasValue
                            ? Math.Max(0, (nowUtc.Date - lastSaleDate.Value.Date).Days)
                            : null;
                        var salesHistoryStatus = ResolveSalesHistoryStatus(lastSaleDate, from180Utc);
                        firstReceiptByArtikal.TryGetValue(salesKey, out var firstReceipt);
                        DateTime? firstReceiptDate = firstReceipt != default
                            ? firstReceipt
                            : salesHistory?.FirstPositiveSaleDateUtc;
                        var receiptEvidenceStatus = firstReceipt != default
                            ? "received"
                            : salesHistory is not null ? "first_sale_fallback" : "unknown";
                        var daysSinceReceipt = firstReceiptDate.HasValue
                            ? Math.Max(0, (todayUtc - firstReceiptDate.Value.Date).Days)
                            : (int?)null;
                        var stockAgeStatus = daysSinceReceipt.HasValue
                            ? "established"
                            : "unknown";

                        var noSaleDaysForFilter = daysSinceLastSale
                            ?? (salesHistoryStatus == "never_sold" ? daysSinceReceipt : null);
                        if (noSaleDaysMin.HasValue
                            && (!noSaleDaysForFilter.HasValue || noSaleDaysForFilter.Value < noSaleDaysMin.Value))
                            continue;

                        var scopedMarkdownEvents = markdownByArtikal
                            .GetValueOrDefault(a.Id, [])
                            .Where(item => NivelacijaEventScopePolicy.AppliesToStore(item.StoreId, a.StoreId))
                            .ToArray();
                        var markdownEvents = scopedMarkdownEvents.Length;
                        var avgMarkdownPct = decimal.Round(
                            scopedMarkdownEvents
                                .Where(item => item.OldPrice.HasValue
                                    && item.NewPrice.HasValue
                                    && item.OldPrice.Value > 0m
                                    && item.NewPrice.Value < item.OldPrice.Value)
                                .Select(item => ((item.OldPrice!.Value - item.NewPrice!.Value) / item.OldPrice!.Value) * 100m)
                                .DefaultIfEmpty(0m)
                                .Average(),
                            2);

                        var marginEvidence = ResolveMarginEvidence(a.SellingPrice, a.PurchasePrice);
                        var salesEvidence = ResolveSalesEvidence(
                            salesLite is not null,
                            salesLite?.Units180 ?? 0,
                            salesLite?.NegativeUnits180 ?? 0);
                        var sellingPrice = marginEvidence.SellingPriceForScenarios;
                        var purchasePrice = marginEvidence.PurchasePriceForScenarios;
                        var hasCompleteEvidence = marginEvidence.HasCompleteEvidence
                            && salesEvidence.IsComplete
                            && firstReceiptDate.HasValue;
                        var grossMarginPct = marginEvidence.GrossMarginPctEst ?? 0m;

                        if (marginFloor.HasValue)
                        {
                            if (!hasCompleteEvidence || grossMarginPct < marginFloor.Value)
                                continue;
                        }

                        var seasonRecencyBoost = ResolveSeasonRecencyBoost(a.SeasonId, seasons, todayUtc);
                        var recencyDaysForScore = daysSinceLastSale ?? daysSinceReceipt ?? 0;
                        var breakdown = scoring.ComputeScoreBreakdown(
                            a.StockUnits,
                            velocity180,
                            recencyDaysForScore,
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

                        var evidenceReasons = new[]
                            {
                                marginEvidence.EvidenceReason,
                                salesEvidence.Reason,
                                firstReceiptDate.HasValue ? null : "receipt_date_unknown",
                                marginEvidence.BelowCost == true ? "below_cost" : null
                            }
                            .Where(reason => !string.IsNullOrWhiteSpace(reason))
                            .ToArray();

                        allCandidates.Add(new PreNivelacijaSkuCandidateDto
                        {
                            ArtikalId = a.Id,
                            Sku = sku,
                            StoreId = a.StoreId,
                            StoreName = ResolveStoreName(a.StoreId, storeNames),
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
                            SalesHistoryStatus = salesHistoryStatus,
                            FirstReceiptDateUtc = firstReceiptDate.HasValue
                                ? DateTime.SpecifyKind(firstReceiptDate.Value, DateTimeKind.Utc)
                                : null,
                            DaysSinceReceipt = daysSinceReceipt,
                            ReceiptEvidenceStatus = receiptEvidenceStatus,
                            StockAgeStatus = stockAgeStatus,
                            MarkdownEvents = markdownEvents,
                            AvgMarkdownPct = decimal.Round(avgMarkdownPct, 2),
                            GrossMarginPctEst = marginEvidence.GrossMarginPctEst,
                            GrossMarginPctSigned = marginEvidence.GrossMarginPctEst,
                            BelowCost = marginEvidence.BelowCost,
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
                        return BuildEmptyBaseEntry(
                            nowUtc,
                            newStockCandidates: newStockCandidates,
                            minimumNewStockAgeDays: minimumNewStockAgeDays);
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
                            candidate.DaysSinceLastSale ?? candidate.DaysSinceReceipt ?? 0,
                            candidate.PriorityBand,
                            candidate.Confidence,
                            candidate.Units180,
                            candidate.StockUnits,
                            HasCompleteEvidence: candidate.HasCompleteEvidence,
                            SalesEvidenceStatus: candidate.SalesEvidenceStatus));

                        candidate.DecisionScore = recommendation.DecisionScore;
                        candidate.ReliabilityPct = recommendation.ReliabilityPct;
                        candidate.Recommendation = recommendation.Recommendation;
                        if (candidate.BelowCost == true)
                        {
                            candidate.Recommendation.ReasonCodes = candidate.Recommendation.ReasonCodes
                                .Append("below_cost")
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToArray();
                        }
                        if (candidate.ReceiptEvidenceStatus == "unknown")
                        {
                            candidate.Recommendation.ReasonCodes = candidate.Recommendation.ReasonCodes
                                .Append("receipt_date_unknown")
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToArray();
                        }
                        if (!candidate.StoreId.HasValue)
                        {
                            candidate.Recommendation = new PreNivelacijaRecommendationDto
                            {
                                Status = "do_not_trust",
                                Label = "Nedostaje objekat",
                                Summary = "Akcija je blokirana jer kandidat nema pouzdan identitet prodavnice.",
                                ConfidencePct = 0,
                                ReliabilityPct = 0,
                                DataQualityStatus = "critical",
                                RecommendationAllowed = false,
                                ReasonCodes = candidate.Recommendation.ReasonCodes
                                    .Append("missing_store_identity")
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToArray()
                            };
                            candidate.DecisionScore = 0;
                            candidate.ReliabilityPct = 0;
                        }
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
                        FormulaVersion = "pre_nivelacija_v10",
                        FormulaDescription = BuildFormulaDescription(minimumNewStockAgeDays),
                        ModelEvidence = BuildModelEvidence(),
                        Summary = new PreNivelacijaSummaryDto(),
                        SupplierLeaderboard = [],
                        Candidates = allCandidates,
                        FacetUniverseCandidates = allCandidates,
                        NewStockCandidates = newStockCandidates,
                        Queues = new PreNivelacijaQueuesDto(),
                        Alerts = [],
                        EvidenceWindow = BuildEvidenceWindow(from180Utc, nowUtc, allCandidates, []),
                        TotalCandidates = allCandidates.Count,
                        RecommendationAllowed = allCandidates.Count > 0 && allCandidates.All(x => x.Recommendation.RecommendationAllowed)
                    };
                },
                CacheExpiration.HeavyAnalytics,
                ct);

            var response = BuildResponse(
                MaterializeFacetFilteredEntry(baseEntry, supplierId, seasonId, footwearTypeId, storeId),
                page,
                pageSize,
                focus,
                normalizedDataScope,
                integrityRegistry,
                BuildFilterFacets(
                    baseEntry.FacetUniverseCandidates.Count > 0
                        ? baseEntry.FacetUniverseCandidates
                        : baseEntry.Candidates,
                    supplierId,
                    seasonId,
                    footwearTypeId,
                    storeId));

                return Results.Ok(response);
            }
            catch (PreNivelacijaQueryFailedException ex)
            {
                var unavailable = BuildEmptyBaseEntry(
                    DateTime.UtcNow,
                    BuildQueryFailureMeta(ex.SalesQueryFailed, ex.MarkdownQueryFailed, ex.ReceiptQueryFailed),
                    minimumNewStockAgeDays: minimumNewStockAgeDays);
                return Results.Ok(BuildResponse(
                    unavailable,
                    page,
                    pageSize,
                    focus,
                    normalizedDataScope,
                    integrityRegistry,
                    new PreNivelacijaFilterFacetsDto()));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
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
                        null),
                    minimumNewStockAgeDays: minimumNewStockAgeDays);
                return Results.Ok(BuildResponse(
                    unavailable,
                    page,
                    pageSize,
                    focus,
                    normalizedDataScope,
                    integrityRegistry,
                    new PreNivelacijaFilterFacetsDto()));
            }
        })
        .WithName("GetPreNivelacijaPrioriteti")
        .WithTags("Analytics")
        .RequireRateLimiting("analytics");
    }

    private static string BuildFormulaDescription(int minimumNewStockAgeDays)
    {
        return $"Skor pre-nivelacije = 0,30*pritisak_zalihe + 0,25*rizik_brzine_prodaje + 0,20*rizik_svežine + 0,10*prilika_za_sniženje + 0,10*potencijal_marže + 0,05*sezonski_signal; pritisak zalihe i rizik brzine su relativni prema maksimumu osnovne referentne kohorte, nisu percentile ni apsolutni pragovi. Preporuka = 0,50*skor + 0,20*razlika_scenarija + 0,15*rizik_zastarelosti + 0,15*pouzdanost. Scenario pretpostavke: highlight povećava zaglađenu baznu tražnju za 15–45% (množilac 1,15–1,45); markdown je 8–35%, sa elastičnošću tražnje 1,8; očekivane jedinice su ograničene raspoloživom zalihom. Scenario brojevi su heuristička, nekalibrisana procena bez kauzalne garancije. Prozor prodaje i nivelacija: poslednjih 180 dana u UTC; DUG/KOREKCIJA računi su isključeni, povrati ostaju u potpisanom netu, a recency koristi poslednju pozitivnu prodaju. Roba primljena pre manje od {minimumNewStockAgeDays} dana je izdvojena iz prioriteta; starost se računa iz prvog prijema, uz prvu pozitivnu prodaju kao rezervni dokaz. Marža ispod nabavne ostaje negativna u scenarijima.";
    }

    private static PreNivelacijaModelEvidenceDto BuildModelEvidence()
    {
        return new PreNivelacijaModelEvidenceDto();
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

    internal static int ResolveMinimumNewStockAgeDays(IConfiguration configuration)
    {
        var configured = configuration["Analytics:PreNivelacija:MinimumNewStockAgeDays"];
        return int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days)
            ? Math.Clamp(days, 0, 3650)
            : DefaultMinimumNewStockAgeDays;
    }

    internal static AnalyticsResponseMetaDto BuildQueryFailureMeta(
        bool salesQueryFailed,
        bool markdownQueryFailed,
        bool receiptQueryFailed = false)
    {
        if (salesQueryFailed)
        {
            return AnalyticsResponseMetaFactory.ApplyDecisionReadiness(
                AnalyticsResponseMetaFactory.Error(
                "pre_nivelacija_sales_unavailable",
                "Prodaja za pre-nivelacija skor trenutno nije dostupna.",
                null),
                "recommendation",
                reasonCodes: ["pre_nivelacija_sales_unavailable"],
                evidenceReferences: ["pre_nivelacija.salesEvidence"],
                repairPath: "Sales data quality");
        }

        if (markdownQueryFailed)
        {
            return AnalyticsResponseMetaFactory.ApplyDecisionReadiness(
                AnalyticsResponseMetaFactory.Error(
                "pre_nivelacija_markdown_unavailable",
                "Dnevnik nivelacija za pre-nivelacija skor trenutno nije dostupan.",
                null),
                "recommendation",
                reasonCodes: ["pre_nivelacija_markdown_unavailable"],
                evidenceReferences: ["pre_nivelacija.markdownEvidence"],
                repairPath: "Nivelacija evidence");
        }

        if (receiptQueryFailed)
        {
            return AnalyticsResponseMetaFactory.ApplyDecisionReadiness(
                AnalyticsResponseMetaFactory.Error(
                    "pre_nivelacija_receipt_unavailable",
                    "Podatak o prvom prijemu robe za pre-nivelacija skor trenutno nije dostupan.",
                    null),
                "recommendation",
                reasonCodes: ["pre_nivelacija_receipt_unavailable"],
                evidenceReferences: ["pre_nivelacija.receiptEvidence"],
                repairPath: "Receipt data quality");
        }

        return AnalyticsResponseMetaFactory.ApplyDecisionReadiness(
            AnalyticsResponseMetaFactory.Success(),
            "recommendation",
            reasonCodes: ["pre_nivelacija.evidence"],
            evidenceReferences: ["pre_nivelacija.salesEvidence", "pre_nivelacija.marginEvidence"]);
    }

    internal readonly record struct PreNivelacijaMarginEvidence(
        bool HasCompleteEvidence,
        string? EvidenceReason,
        decimal? GrossMarginPctEst,
        decimal SellingPriceForScenarios,
        decimal PurchasePriceForScenarios,
        bool? BelowCost);

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

        if (signedUnits180 < 0)
        {
            return negativeUnits180 < 0
                ? new PreNivelacijaSalesEvidence(false, "non_positive_net_with_returns", "signed_sales_non_positive")
                : new PreNivelacijaSalesEvidence(false, "negative_net_sales", "negative_net_sales");
        }

        if (signedUnits180 == 0)
        {
            return negativeUnits180 < 0
                ? new PreNivelacijaSalesEvidence(false, "non_positive_net_with_returns", "signed_sales_non_positive")
                : new PreNivelacijaSalesEvidence(false, "zero_net_sales", "zero_net_sales");
        }

        return new PreNivelacijaSalesEvidence(
            true,
            "positive_net_sales",
            negativeUnits180 < 0 ? "signed_sales_return" : null);
    }

    internal static string ResolveSalesHistoryStatus(DateTime? lastPositiveSaleDateUtc, DateTime windowStartUtc)
    {
        if (!lastPositiveSaleDateUtc.HasValue)
            return "never_sold";

        return lastPositiveSaleDateUtc.Value < windowStartUtc
            ? "no_sale_in_window"
            : "sold";
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
            ReceiptPopulationPolicy = "certified_retail_excludes_trimmed_case_insensitive_dug_korekcija",
            SignedReturnPolicy = "included_in_signed_net_positive_net_remains_actionable",
            LastSaleRecencyPolicy = "latest_positive_retail_sale_only",
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
            return new PreNivelacijaMarginEvidence(false, "missing_price_baseline", null, 0m, 0m, null);
        }

        if (!hasSellingPrice)
        {
            return new PreNivelacijaMarginEvidence(false, "missing_selling_price", null, 0m, purchasePrice!.Value, null);
        }

        if (!hasReliableCost)
        {
            var reason = !purchasePrice.HasValue
                ? "missing_purchase_cost"
                : "non_positive_purchase_cost";
            return new PreNivelacijaMarginEvidence(false, reason, null, sellingPrice!.Value, 0m, null);
        }

        var sell = sellingPrice!.Value;
        var cost = purchasePrice!.Value;
        var grossMarginPct = decimal.Round(((sell - cost) / sell) * 100m, 2);

        return new PreNivelacijaMarginEvidence(true, null, grossMarginPct, sell, cost, cost > sell);
    }

    private static PreNivelacijaPriorityBaseCacheEntry BuildEmptyBaseEntry(
        DateTime nowUtc,
        AnalyticsResponseMetaDto? meta = null,
        List<PreNivelacijaNewStockQueueItemDto>? newStockCandidates = null,
        int minimumNewStockAgeDays = DefaultMinimumNewStockAgeDays)
    {
        return new PreNivelacijaPriorityBaseCacheEntry
        {
            GeneratedAtUtc = nowUtc,
            FormulaVersion = "pre_nivelacija_v10",
            FormulaDescription = BuildFormulaDescription(minimumNewStockAgeDays),
            ModelEvidence = BuildModelEvidence(),
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
            NewStockCandidates = newStockCandidates ?? [],
            Queues = BuildQueues([], nowUtc, newStockCandidates),
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
        string dataScope = "all",
        OperationsAnalyticsIntegrityRegistry? integrityRegistry = null,
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
            ModelEvidence = baseEntry.ModelEvidence,
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
        response.Meta.RequestedDataScope = dataScope;
        response.Meta.EffectiveDataScope = response.Meta.Success ? dataScope : null;
        response.Meta.DataScopeSource = "pre_nivelacija_product_origin_filter";
        response.Meta.RequestedPeriodFromUtc = baseEntry.EvidenceWindow.SalesWindowFromUtc;
        response.Meta.RequestedPeriodToUtc = baseEntry.EvidenceWindow.SalesWindowToUtc;
        response.Meta.EffectivePeriodFromUtc = baseEntry.EvidenceWindow.SalesWindowFromUtc;
        response.Meta.EffectivePeriodToUtc = baseEntry.EvidenceWindow.SalesWindowToUtc;
        response.Meta.ObservedPeriodFromUtc = baseEntry.EvidenceWindow.SalesWindowFromUtc;
        response.Meta.ObservedPeriodToUtc = baseEntry.EvidenceWindow.SalesWindowToUtc;
        AnalyticsResponseMetaFactory.ApplyDecisionReadiness(
            response.Meta,
            "recommendation",
            reasonCodes: ["pre_nivelacija.evidence"],
            evidenceReferences: ["pre_nivelacija.salesEvidence", "pre_nivelacija.marginEvidence", "pre_nivelacija.recommendation"],
            repairPath: "Data Quality ili Nivelacija evidence");
        if (integrityRegistry is null)
        {
            OperationsAnalyticsIntegrityMeta.MarkIndependentlyUnverified(
                response.Meta,
                OperationsAnalyticsIntegrityFamilies.Nivelacija);
        }
        else
        {
            OperationsAnalyticsIntegrityMeta.ApplyFamilyEvidence(
                response.Meta,
                integrityRegistry,
                OperationsAnalyticsIntegrityFamilies.Nivelacija,
                baseEntry.EvidenceWindow.SalesWindowFromUtc,
                baseEntry.EvidenceWindow.SalesWindowToUtc,
                dataScope,
                storeId: null);
        }

        return response;
    }

    private static PreNivelacijaPriorityBaseCacheEntry MaterializeFacetFilteredEntry(
        PreNivelacijaPriorityBaseCacheEntry universeEntry,
        int? supplierId,
        int? seasonId,
        int? footwearTypeId,
        int? storeId = null)
    {
        var universe = universeEntry.FacetUniverseCandidates.Count > 0
            ? universeEntry.FacetUniverseCandidates
            : universeEntry.Candidates;
        var filtered = ApplyDimensionFilters(universe, supplierId, seasonId, footwearTypeId, storeId);
        var filteredNewStock = ApplyNewStockDimensionFilters(
            universeEntry.NewStockCandidates,
            supplierId,
            seasonId,
            footwearTypeId,
            storeId);
        var leaderboard = BuildSupplierLeaderboard(filtered);
        var summary = BuildSummary(filtered, leaderboard);
        var queues = BuildQueues(filtered, universeEntry.GeneratedAtUtc, filteredNewStock);
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
            ModelEvidence = universeEntry.ModelEvidence,
            Summary = summary,
            SupplierLeaderboard = leaderboard,
            Candidates = filtered,
            FacetUniverseCandidates = universe.ToList(),
            NewStockCandidates = filteredNewStock,
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
        int? footwearTypeId,
        int? storeId = null)
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

        if (storeId.HasValue)
        {
            query = query.Where(candidate => candidate.StoreId == storeId.Value);
        }

        return query.ToList();
    }

    private static List<PreNivelacijaNewStockQueueItemDto> ApplyNewStockDimensionFilters(
        IReadOnlyList<PreNivelacijaNewStockQueueItemDto> candidates,
        int? supplierId,
        int? seasonId,
        int? footwearTypeId,
        int? storeId)
    {
        IEnumerable<PreNivelacijaNewStockQueueItemDto> query = candidates;
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        if (seasonId.HasValue) query = query.Where(x => x.SeasonId == seasonId.Value);
        if (footwearTypeId.HasValue) query = query.Where(x => x.FootwearTypeId == footwearTypeId.Value);
        if (storeId.HasValue) query = query.Where(x => x.StoreId == storeId.Value);
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
        DateTime nowUtc,
        IReadOnlyList<PreNivelacijaNewStockQueueItemDto>? newStockCandidates = null)
    {
        var highlightNow = candidates
            .Where(x => IsHighPriorityCandidate(x) && x.Recommendation.RecommendationAllowed)
            .ToList();
        var monitor = candidates
            .Where(x => x.PriorityBand == "medium" && x.Recommendation.RecommendationAllowed)
            .ToList();
        var likelyMarkdownSoon = candidates
            .Where(x => x.Recommendation.RecommendationAllowed
                && (x.DaysSinceLastSale >= 60 || x.MarkdownEvents >= 2 || x.AvgMarkdownPct >= 25m))
            .OrderByDescending(x => x.DaysSinceLastSale)
            .ThenByDescending(x => x.StockUnits)
            .ToList();

        return new PreNivelacijaQueuesDto
        {
            NewStockTotal = newStockCandidates?.Count ?? 0,
            NewStock = newStockCandidates?.Take(30).ToList() ?? [],
            HighlightNowTotal = highlightNow.Count,
            HighlightNow = highlightNow
                .Take(30)
                .Select(x => ToQueueItem(x, nowUtc.AddDays(2)))
                .ToList(),
            MonitorTotal = monitor.Count,
            Monitor = monitor
                .Take(30)
                .Select(x => ToQueueItem(x, nowUtc.AddDays(7)))
                .ToList(),
            LikelyMarkdownSoonTotal = likelyMarkdownSoon.Count,
            LikelyMarkdownSoon = likelyMarkdownSoon
                .Take(30)
                .Select(x => ToQueueItem(x, nowUtc.AddDays(3)))
                .ToList()
        };
    }

    internal static PreNivelacijaFilterFacetsDto BuildFilterFacets(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> universe,
        int? selectedSupplierId,
        int? selectedSeasonId,
        int? selectedFootwearTypeId,
        int? selectedStoreId = null)
    {
        var supplierUniverse = ApplyDimensionFilters(universe, null, selectedSeasonId, selectedFootwearTypeId);
        var seasonUniverse = ApplyDimensionFilters(universe, selectedSupplierId, null, selectedFootwearTypeId);
        var footwearUniverse = ApplyDimensionFilters(universe, selectedSupplierId, selectedSeasonId, null);
        var storeUniverse = ApplyDimensionFilters(universe, selectedSupplierId, selectedSeasonId, selectedFootwearTypeId);

        var suppliers = AggregateSupplierFacet(supplierUniverse);
        var seasons = AggregateSeasonFacet(seasonUniverse);
        var footwearTypes = AggregateFootwearFacet(footwearUniverse);
        var stores = AggregateStoreFacet(storeUniverse);

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
        EnsureSelectedFacetOption(
            stores,
            selectedStoreId,
            universe,
            candidate => candidate.StoreId,
            candidate => candidate.StoreName);

        return new PreNivelacijaFilterFacetsDto
        {
            Suppliers = suppliers,
            Seasons = seasons,
            FootwearTypes = footwearTypes,
            Stores = stores
        };
    }

    private static List<PreNivelacijaFilterOptionDto> AggregateStoreFacet(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates)
    {
        return candidates
            .Where(candidate => candidate.StoreId.HasValue)
            .GroupBy(candidate => candidate.StoreId!.Value)
            .Select(group => new PreNivelacijaFilterOptionDto
            {
                Id = group.Key,
                Label = group.Select(item => string.IsNullOrWhiteSpace(item.StoreName) ? EntityIdentity.FallbackLabel(EntityKind.Store, group.Key) : item.StoreName.Trim()).First(),
                Count = group.Count()
            })
            .OrderBy(option => option.Label, StringComparer.Create(new System.Globalization.CultureInfo("sr-Latn-RS"), ignoreCase: true))
            .ToList();
    }

    private static List<PreNivelacijaFilterOptionDto> AggregateSupplierFacet(
        IReadOnlyList<PreNivelacijaSkuCandidateDto> candidates)
    {
        return candidates
            .Where(candidate => candidate.SupplierId.HasValue
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
            if (!candidate.SeasonId.HasValue
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
            if (!candidate.FootwearTypeId.HasValue
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

    private static string ResolveStoreName(int? storeId, IReadOnlyDictionary<int, string> storeNames)
    {
        return storeId.HasValue && storeNames.TryGetValue(storeId.Value, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : storeId.HasValue ? EntityIdentity.FallbackLabel(EntityKind.Store, storeId) : "N/A";
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
            StoreId = sku.StoreId,
            StoreName = sku.StoreName,
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

internal sealed class PreNivelacijaQueryFailedException : Exception
{
    public PreNivelacijaQueryFailedException(
        bool salesQueryFailed,
        bool markdownQueryFailed,
        Exception innerException,
        bool receiptQueryFailed = false)
        : base("Pre-nivelacija query failed.", innerException)
    {
        SalesQueryFailed = salesQueryFailed;
        MarkdownQueryFailed = markdownQueryFailed;
        ReceiptQueryFailed = receiptQueryFailed;
    }

    public bool SalesQueryFailed { get; }

    public bool MarkdownQueryFailed { get; }

    public bool ReceiptQueryFailed { get; }
}
