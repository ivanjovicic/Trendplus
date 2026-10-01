using System.Globalization;

namespace Api.Tests;

// Independent re-statement of the Supplier scorecard formula version deployed by
// Database/Migrations/018_AddSupplierDecisionHubViews.sql (all-time) and
// Database/Migrations/029_AddSupplierDecisionWindowedViews.sql (90d/180d).
// It is computed from raw fixture rows and must never read the SQL views.
// The nivelacija event rows (vw_vendor_sales_nivelacija / vw_nivelacija_did) are
// a fixture-controlled seam; their own windows/aggregates are owned by RQ527.

internal sealed record OracleSupplier(int Id, string Name);

internal sealed record OracleArticle(
    int Id,
    int SupplierId,
    string? Category,
    int Stock,
    int MinStock,
    decimal CostDin,
    decimal CostForeign);

internal sealed record OracleSaleLine(
    int ReceiptId,
    string ReceiptNumber,
    DateOnly Day,
    int ArticleId,
    int Qty,
    decimal Price,
    decimal? LineCost,
    int? SupplierAtSale);

internal sealed record OracleInventoryMove(int ArticleId, DateOnly Day, string Type, int Qty);

internal sealed record OraclePriceEvent(
    long EventId,
    int ArticleId,
    int VendorId,
    DateOnly Day,
    decimal OldPrice,
    decimal NewPrice,
    decimal CoveragePre,
    decimal CoveragePost,
    bool IsLowSignal,
    decimal PostQty,
    decimal PostRevenue);

internal sealed record OracleDidRow(long EventId, decimal DidRevenue, decimal DidQty);

internal sealed class OracleFixture
{
    public List<OracleSupplier> Suppliers { get; } = [];
    public List<OracleArticle> Articles { get; } = [];
    public List<OracleSaleLine> Sales { get; } = [];
    public List<OracleInventoryMove> Moves { get; } = [];
    public List<OraclePriceEvent> Events { get; } = [];
    public List<OracleDidRow> Did { get; } = [];
}

/// <summary>
/// Formula switches. Defaults reproduce the deployed v1 formula. The candidate
/// corrections are the RQ521 input-bug repairs; they are opt-in so tests can
/// assert the known defects until RQ521 lands. Model policy (weights,
/// thresholds, coverage gates, inventory penalty, cost fallback) is not
/// switchable here and changes only through RQ531/owner decision.
/// </summary>
internal sealed record SupplierScorecardOracleOptions(
    DateOnly Anchor,
    int? WindowDays,
    bool DeadStockCountsOnlyPostSignalArticles)
{
    public bool ReturnRateUsesGrossUnits { get; init; }
    public bool SalesInPeriodUsesSaleTimeSupplierOnly { get; init; }
    public bool ExcludeDugKorekcijaFromPreMarkdownSales { get; init; }
    public bool StockProxySubtractsCustomerReturnMoves { get; init; } = true;

    public static SupplierScorecardOracleOptions AllTime(DateOnly anchor) => new(anchor, null, true);

    public static SupplierScorecardOracleOptions Window(DateOnly anchor, int days) => new(anchor, days, false);
}

internal sealed record OracleArticleSignal(
    int SupplierId,
    string SupplierName,
    string Category,
    int ArticleId,
    DateOnly FirstMarkdownDate,
    decimal OldPrice,
    decimal NewPrice,
    decimal PreQty,
    decimal PreRevenue,
    decimal PreMargin,
    decimal PreSellthrough,
    decimal RawStockBeforeMarkdown,
    decimal StockBeforeMarkdown,
    bool StockoutBeforeMarkdown,
    bool HadSalesBeforeMarkdown,
    string SignalQuality,
    bool HasPostSignal,
    bool HasDidSignal,
    bool HasCostSignal,
    decimal PostQty,
    decimal PostRevenue,
    decimal CurrentStock,
    decimal CurrentCost);

internal sealed record OracleDecisionInput(
    int SupplierId,
    string SupplierName,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    decimal Revenue,
    decimal Units,
    decimal FullpriceRevenueShare,
    decimal FullpriceSellthrough,
    decimal PreMarkdownMarginPct,
    decimal MarkdownRevenueShare,
    decimal DeadStockRate,
    decimal UnsoldStockValue,
    decimal PostSignalCoverage,
    decimal DidSignalCoverage,
    decimal CostSignalCoverage,
    decimal SoldUnitsInPeriod,
    decimal ReturnedUnitsInPeriod,
    decimal? ReturnRate,
    decimal CategoryFocusScore,
    decimal RepeatWinnerRate,
    int ArticleCount,
    decimal HighSignalShare,
    decimal MediumSignalShare,
    decimal HadSalesShare,
    bool StockoutBeforeMarkdownFlag,
    decimal SeasonalCategoryShare);

internal sealed record OracleSupplierScore(
    OracleDecisionInput Input,
    decimal FullpriceSellthroughRank,
    decimal FullpriceRevenueShareRank,
    decimal PreMarkdownMarginRank,
    decimal MarkdownRevenueShareRank,
    decimal DeadStockRateRank,
    decimal UnsoldStockValueRank,
    decimal RepeatWinnerRateRank,
    decimal ReturnRateRank,
    decimal CategoryFocusRank,
    decimal ArticleCountRank,
    decimal SalesVolumeRank,
    decimal DemandScore,
    decimal MarginScore,
    decimal MarkdownPenalty,
    decimal InventoryPenalty,
    decimal SupplierQualityComponentRaw,
    decimal SupplierQualityComponent,
    decimal ConfidenceRaw,
    decimal ConfidenceScore,
    decimal ScoreRaw,
    decimal SupplierQualityIndex,
    bool ScoreClamped,
    string EvidenceQualityStatus,
    string? ReturnRateMissingEvidenceReason,
    string RecommendationCode);

internal static class SupplierScorecardOracle
{
    private static readonly string[] SeasonalCategoryTokens =
        ["sand", "papuc", "cizm", "gleznj", "boot", "slipper", "season"];

    public static IReadOnlyList<OracleArticleSignal> ArticleSignals(
        OracleFixture fixture,
        SupplierScorecardOracleOptions options)
    {
        var articles = fixture.Articles.ToDictionary(a => a.Id);
        var suppliers = fixture.Suppliers.ToDictionary(s => s.Id);
        var didEvents = fixture.Did.Select(d => d.EventId).ToHashSet();
        var signals = new List<OracleArticleSignal>();

        // First-ever markdown per article: price decreases only, ordered by
        // event date then event id. The window filter is applied afterwards,
        // so an article whose first markdown predates the window is excluded
        // even when it has a later in-window markdown.
        var firstMarkdowns = fixture.Events
            .Where(e => e.NewPrice < e.OldPrice)
            .GroupBy(e => e.ArticleId)
            .Select(g => g.OrderBy(e => e.Day).ThenBy(e => e.EventId).First());

        foreach (var fm in firstMarkdowns)
        {
            if (options.WindowDays is int days
                && (fm.Day < options.Anchor.AddDays(-days) || fm.Day > options.Anchor))
            {
                continue;
            }

            var article = articles[fm.ArticleId];
            var fmd = fm.Day;
            var oldPrice = Round(fm.OldPrice, 2);
            var newPrice = Round(fm.NewPrice, 2);

            var lines = fixture.Sales
                .Where(s => s.ArticleId == fm.ArticleId)
                .Where(s => !options.ExcludeDugKorekcijaFromPreMarkdownSales || !IsDugOrKorekcija(s.ReceiptNumber))
                .ToList();
            var preLines = lines.Where(s => s.Day >= fmd.AddDays(-30) && s.Day < fmd).ToList();
            var preQty = (decimal)preLines.Sum(s => s.Qty);
            var preRevenue = Round(preLines.Sum(s => s.Qty * s.Price), 2);
            var preCost = Round(preLines.Sum(s => s.Qty * UnitCost(s, article)), 2);
            var hadSales = lines.Any(s => s.Day < fmd);
            var soldSince = (decimal)lines.Where(s => s.Day >= fmd).Sum(s => s.Qty);

            var moves = fixture.Moves.Where(m => m.ArticleId == fm.ArticleId && m.Day >= fmd).ToList();
            decimal MoveQty(string type) => moves.Where(m => m.Type == type).Sum(m => m.Qty);

            var rawStock = article.Stock
                + soldSince
                + MoveQty("Prenos izlaz")
                - MoveQty("Ulaz robe")
                - MoveQty("Prenos ulaz")
                - (options.StockProxySubtractsCustomerReturnMoves ? MoveQty("Povrat kupca") : 0m);
            var stockBefore = Math.Max(rawStock, 0m);
            var preSellthrough = preQty + stockBefore <= 0
                ? 0m
                : Round(preQty / (preQty + stockBefore), 4);
            var stockout = stockBefore <= Math.Max(article.MinStock, 1)
                || (!hadSales && stockBefore <= 0);

            var quality = fm.IsLowSignal || fm.CoveragePre < 0.20m || !hadSales
                ? "low"
                : fm.CoveragePre < 0.50m || fm.CoveragePost < 0.50m || rawStock < 0
                    ? "medium"
                    : "high";

            var postEvent = fixture.Events
                .Where(e => e.ArticleId == fm.ArticleId
                    && e.Day == fmd
                    && e.OldPrice == oldPrice
                    && e.NewPrice == newPrice)
                .OrderBy(e => e.EventId)
                .FirstOrDefault();

            var hasCost = article.CostDin > 0 || article.CostForeign > 0;
            var currentCost = article.CostDin > 0
                ? Round(article.CostDin, 2)
                : article.CostForeign > 0 ? Round(article.CostForeign, 2) : 0m;

            signals.Add(new OracleArticleSignal(
                fm.VendorId,
                suppliers[fm.VendorId].Name,
                article.Category ?? "Uncategorized",
                fm.ArticleId,
                fmd,
                oldPrice,
                newPrice,
                preQty,
                preRevenue,
                preRevenue - preCost,
                preSellthrough,
                rawStock,
                stockBefore,
                stockout,
                hadSales,
                quality,
                postEvent is not null,
                postEvent is not null && didEvents.Contains(postEvent.EventId),
                hasCost,
                postEvent?.PostQty ?? 0m,
                Round(postEvent?.PostRevenue ?? 0m, 2),
                article.Stock,
                currentCost));
        }

        return signals;
    }

    public static IReadOnlyList<OracleDecisionInput> DecisionInputs(
        OracleFixture fixture,
        SupplierScorecardOracleOptions options)
    {
        var signals = ArticleSignals(fixture, options);
        var inputs = new List<OracleDecisionInput>();

        foreach (var group in signals.GroupBy(s => s.SupplierId).OrderBy(g => g.Key))
        {
            var rows = group.ToList();
            var count = (decimal)rows.Count;

            var revenuePre = rows.Sum(r => r.PreRevenue);
            var revenuePost = rows.Sum(r => r.PostRevenue);
            var qtyPre = rows.Sum(r => r.PreQty);
            var qtyPost = rows.Sum(r => r.PostQty);
            var totalRevenue = revenuePre + revenuePost;

            var deadStockRows = options.DeadStockCountsOnlyPostSignalArticles
                ? rows.Where(r => r.HasPostSignal).ToList()
                : rows;
            var deadStockRate = deadStockRows.Count == 0
                ? 0m
                : Round(deadStockRows.Count(r => r.CurrentStock > 0 && r.PostQty == 0) / (decimal)deadStockRows.Count, 4);

            var categoryRevenue = rows
                .GroupBy(r => r.Category)
                .Select(c => (Category: c.Key, Revenue: c.Sum(r => r.PreRevenue) + c.Sum(r => r.PostRevenue)))
                .ToList();
            var categoryFocus = totalRevenue == 0
                ? 0m
                : categoryRevenue.Max(c => c.Revenue / totalRevenue) * 100;
            var seasonalShare = totalRevenue == 0
                ? 0m
                : categoryRevenue.Where(c => IsSeasonal(c.Category)).Sum(c => c.Revenue) / totalRevenue;

            var stockDenominator = rows.Sum(r => r.PreQty + r.StockBeforeMarkdown);
            var stockoutShare = rows.Count(r => r.StockoutBeforeMarkdown) / count;

            var periodFrom = rows.Min(r => r.FirstMarkdownDate).AddDays(-30);
            var periodTo = rows.Max(r => r.FirstMarkdownDate).AddDays(30);
            var (sold, gross, returned) = SalesInPeriod(fixture, options, group.Key, periodFrom, periodTo);
            var returnDenominator = options.ReturnRateUsesGrossUnits ? gross : sold;

            inputs.Add(new OracleDecisionInput(
                group.Key,
                rows[0].SupplierName,
                periodFrom,
                periodTo,
                Round(totalRevenue, 2),
                qtyPre + qtyPost,
                totalRevenue == 0 ? 0m : revenuePre / totalRevenue,
                stockDenominator == 0 ? 0m : qtyPre / stockDenominator,
                revenuePre == 0 ? 0m : rows.Sum(r => r.PreMargin) / revenuePre,
                totalRevenue == 0 ? 0m : Round(revenuePost / totalRevenue, 4),
                deadStockRate,
                Round(rows.Sum(r => Math.Max(r.CurrentStock, 0) * r.CurrentCost), 2),
                Round(rows.Count(r => r.HasPostSignal) / count, 4),
                Round(rows.Count(r => r.HasDidSignal) / count, 4),
                Round(rows.Count(r => r.HasCostSignal) / count, 4),
                sold,
                returned,
                sold == 0 || returnDenominator == 0 ? null : returned / returnDenominator,
                categoryFocus,
                rows.Count(r => r.PreSellthrough >= 0.45m
                    && r.PreMargin > 0
                    && r.HadSalesBeforeMarkdown
                    && r.SignalQuality != "low") / count,
                rows.Count,
                rows.Count(r => r.SignalQuality == "high") / count,
                rows.Count(r => r.SignalQuality == "medium") / count,
                rows.Count(r => r.HadSalesBeforeMarkdown) / count,
                stockoutShare >= 0.35m,
                seasonalShare));
        }

        return inputs;
    }

    public static IReadOnlyList<OracleSupplierScore> Score(
        OracleFixture fixture,
        SupplierScorecardOracleOptions options) =>
        Score(DecisionInputs(fixture, options));

    public static IReadOnlyList<OracleSupplierScore> Score(IReadOnlyList<OracleDecisionInput> inputs)
    {
        var marginP80 = PercentileCont(inputs.Select(i => Math.Max(i.PreMarkdownMarginPct, 0)).ToList(), 0.80);

        var sellthroughRanks = PercentRanks(inputs, i => i.FullpriceSellthrough);
        var revenueShareRanks = PercentRanks(inputs, i => i.FullpriceRevenueShare);
        var marginRanks = PercentRanks(inputs, i => Math.Min(Math.Max(i.PreMarkdownMarginPct, 0), marginP80));
        var markdownRanks = PercentRanks(inputs, i => i.MarkdownRevenueShare);
        var deadStockRanks = PercentRanks(inputs, i => i.DeadStockRate);
        var stockValueRanks = PercentRanks(inputs, i => i.UnsoldStockValue);
        var repeatWinnerRanks = PercentRanks(inputs, i => i.RepeatWinnerRate);
        var returnRanks = PercentRanks(inputs, i => i.ReturnRate ?? 0m);
        var categoryFocusRanks = PercentRanks(inputs, i => i.CategoryFocusScore);
        var articleCountRanks = PercentRanks(inputs, i => i.ArticleCount);
        var salesVolumeRanks = PercentRanks(inputs, i => i.Units);

        var scores = new List<OracleSupplierScore>();
        for (var index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];

            var demand = Round((0.60m * sellthroughRanks[index] + 0.40m * revenueShareRanks[index]) * 100, 2);
            var margin = Round(marginRanks[index] * 100, 2);
            var seasonalMultiplier = input.SeasonalCategoryShare >= 0.60m
                ? 75m
                : input.SeasonalCategoryShare >= 0.30m ? 85m : 100m;
            var markdownPenalty = Round(markdownRanks[index] * seasonalMultiplier, 2);
            var inventoryPenalty = Round((0.50m * deadStockRanks[index] + 0.50m * stockValueRanks[index]) * 100, 2);
            var qualityRaw = (0.50m * repeatWinnerRanks[index]
                - 0.30m * returnRanks[index]
                + 0.20m * categoryFocusRanks[index]) * 100;
            var quality = Round(Math.Min(20, Math.Max(-20, qualityRaw)), 2);

            var confidenceRaw = 0.40m * input.HadSalesShare
                + 0.30m * Math.Min(1, Math.Max(0, input.HighSignalShare + 0.50m * input.MediumSignalShare))
                + 0.30m * (0.50m * articleCountRanks[index] + 0.50m * salesVolumeRanks[index])
                + 0.10m * input.PostSignalCoverage
                + 0.10m * input.DidSignalCoverage
                + 0.10m * input.CostSignalCoverage
                - (input.ReturnRate is null ? 0.15m : 0m);
            var confidence = Round(Math.Min(1, Math.Max(0, confidenceRaw)), 4);

            var scoreRaw = demand + margin - markdownPenalty - inventoryPenalty + quality;
            var score = Round(Math.Min(100, Math.Max(0, scoreRaw)), 2);

            var evidence = input.PostSignalCoverage < 1
                || input.DidSignalCoverage < 1
                || input.CostSignalCoverage < 1
                || input.SoldUnitsInPeriod == 0
                ? "partial"
                : "complete";

            var recommendation = evidence != "complete" ? "REVIEW_QUALITY"
                : (input.ReturnRate ?? 0m) > 0.12m ? "REVIEW_QUALITY"
                : input.StockoutBeforeMarkdownFlag ? "OOS_FALSE_NEGATIVE"
                : score > 80 ? "EXPAND"
                : score >= 60 ? "EXPAND_SELECTIVELY"
                : score >= 40 ? "HOLD"
                : score >= 25 ? "PRICE_NEGOTIATE"
                : "ASSORTMENT_REDUCE";

            scores.Add(new OracleSupplierScore(
                input,
                sellthroughRanks[index],
                revenueShareRanks[index],
                marginRanks[index],
                markdownRanks[index],
                deadStockRanks[index],
                stockValueRanks[index],
                repeatWinnerRanks[index],
                returnRanks[index],
                categoryFocusRanks[index],
                articleCountRanks[index],
                salesVolumeRanks[index],
                demand,
                margin,
                markdownPenalty,
                inventoryPenalty,
                qualityRaw,
                quality,
                confidenceRaw,
                confidence,
                scoreRaw,
                score,
                scoreRaw is < 0 or > 100,
                evidence,
                input.SoldUnitsInPeriod == 0 ? "missing_sales_baseline" : null,
                recommendation));
        }

        return scores;
    }

    public static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static (decimal Sold, decimal Gross, decimal Returned) SalesInPeriod(
        OracleFixture fixture,
        SupplierScorecardOracleOptions options,
        int supplierId,
        DateOnly from,
        DateOnly to)
    {
        var currentArticles = fixture.Articles
            .Where(a => a.SupplierId == supplierId)
            .Select(a => a.Id)
            .ToHashSet();

        var lines = fixture.Sales
            .Where(s => s.SupplierAtSale == supplierId)
            .Where(s => options.SalesInPeriodUsesSaleTimeSupplierOnly || currentArticles.Contains(s.ArticleId))
            .Where(s => !IsDugOrKorekcija(s.ReceiptNumber))
            .Where(s => s.Day >= from && s.Day <= to)
            .ToList();

        return (
            lines.Sum(s => s.Qty),
            lines.Where(s => s.Qty > 0).Sum(s => s.Qty),
            lines.Where(s => s.Qty < 0).Sum(s => -s.Qty));
    }

    private static decimal UnitCost(OracleSaleLine line, OracleArticle article) =>
        line.LineCost > 0 ? line.LineCost.Value
        : article.CostDin > 0 ? article.CostDin
        : article.CostForeign > 0 ? article.CostForeign
        : 0m;

    private static bool IsDugOrKorekcija(string receiptNumber) =>
        receiptNumber.Trim().ToUpperInvariant() is "DUG" or "KOREKCIJA";

    private static bool IsSeasonal(string category) =>
        SeasonalCategoryTokens.Any(token => category.Contains(token, StringComparison.OrdinalIgnoreCase));

    // PostgreSQL PERCENT_RANK() is float8 and the scorecard casts it to numeric,
    // which keeps 15 significant digits; the oracle mirrors that cast.
    private static decimal[] PercentRanks<T>(IReadOnlyList<T> rows, Func<T, decimal> value)
    {
        var values = rows.Select(value).ToArray();
        if (values.Length == 1)
        {
            return [1m];
        }

        return values
            .Select(v => FromFloat8(values.Count(other => other < v) / (double)(values.Length - 1)))
            .ToArray();
    }

    private static decimal PercentileCont(IReadOnlyList<decimal> values, double fraction)
    {
        if (values.Count == 0)
        {
            return 0m;
        }

        var sorted = values.Select(v => (double)v).OrderBy(v => v).ToArray();
        var position = fraction * (sorted.Length - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        var result = lower == upper
            ? sorted[lower]
            : sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        return FromFloat8(result);
    }

    private static decimal FromFloat8(double value) =>
        decimal.Parse(value.ToString("G15", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
}
