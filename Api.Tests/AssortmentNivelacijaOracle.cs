using Api.Models;
using Api.Services;
using Application.Analytics;

namespace Api.Tests;

// Independent oracle for the Assortment (vendor-sales-nivelacija) pre/post
// contract. It recomputes windows, maturity and baselines from raw fixture rows
// and aggregates them under the declared endpoint population, so SQL sources
// and the endpoint glue can be compared against one written-down expectation.

internal sealed record AssortmentVendor(int Id, string Name);

internal sealed record AssortmentArticle(int Id, int? VendorId, string Category, string Sku);

internal sealed record AssortmentEvent(
    long Id,
    int ArticleId,
    DateOnly Day,
    decimal OldPrice,
    decimal NewPrice,
    int? StoreId,
    string TipPromene = "Nivelacija",
    int? EventVendorId = null);

internal sealed record AssortmentSale(
    int ReceiptId,
    string ReceiptNumber,
    DateOnly Day,
    int ArticleId,
    int Qty,
    decimal Price,
    int? StoreId);

internal sealed class AssortmentFixture
{
    public List<AssortmentVendor> Vendors { get; } = [];

    public List<AssortmentArticle> Articles { get; } = [];

    public List<AssortmentEvent> Events { get; } = [];

    public List<AssortmentSale> Sales { get; } = [];
}

/// <summary>Source-level options: which SQL source semantics to reproduce.</summary>
/// <param name="AsOf">Post-window maturity anchor used inside SQL.</param>
/// <param name="StoreId">Store filter (scoped source only).</param>
/// <param name="PartitionEventsByStore">Scoped source keeps one event per store; the startup view collapses stores.</param>
internal sealed record AssortmentSourceOptions(DateOnly AsOf, int? StoreId, bool PartitionEventsByStore)
{
    public static AssortmentSourceOptions View(DateOnly asOf) => new(asOf, null, false);

    public static AssortmentSourceOptions Scoped(DateOnly asOf, int? storeId) => new(asOf, storeId, true);
}

/// <summary>One row of vw_vendor_sales_nivelacija / scoped_vendor_sales_nivelacija (columns the endpoint consumes).</summary>
internal sealed record AssortmentSourceRow(
    long PriceEventId,
    DateOnly EventDate,
    int? VendorId,
    string? VendorName,
    int ArticleId,
    string? Sku,
    string? Category,
    decimal? OldPrice,
    decimal? NewPrice,
    decimal? PreQty,
    decimal? PreRevenue,
    decimal? PostQty,
    decimal? PostRevenue,
    decimal? CoveragePre30,
    decimal? CoveragePost30,
    decimal? ChangeQty,
    decimal? ChangeRevenue,
    bool HasQtyBaseline,
    string? QtyBaselineReason,
    decimal? ChangePercentQtySemantic,
    bool HasRevenueBaseline,
    string? RevenueBaselineReason,
    decimal? ChangePercentRevenueSemantic)
{
    /// <summary>Coverage is a repeating numeric fraction; compare at a stable precision.</summary>
    public AssortmentSourceRow Normalized() => this with
    {
        CoveragePre30 = CoveragePre30 is null ? null : Math.Round(CoveragePre30.Value, 6),
        CoveragePost30 = CoveragePost30 is null ? null : Math.Round(CoveragePost30.Value, 6)
    };
}

internal sealed record AssortmentAnalyzedRow(
    long PriceEventId,
    int ArticleId,
    DateOnly EventDate,
    int? VendorId,
    string VendorName,
    decimal PreQty,
    decimal PreRevenue,
    decimal PostQty,
    decimal PostRevenue,
    bool IsPostWindowMature,
    int PostWindowDaysElapsed,
    bool HasComparableSalesWindow,
    string? RevenueBaselineReason,
    decimal? SemanticChangePercentRevenue,
    decimal? PriceChangePercent);

internal sealed record AssortmentTotals(
    decimal PreQty,
    decimal PreRevenue,
    decimal PostQty,
    decimal PostRevenue,
    decimal ChangeQty,
    decimal ChangeRevenue,
    decimal? SemanticChangePercentRevenue,
    int ComparableRows,
    int VendorsCount,
    int ArticlesCount);

internal sealed record AssortmentVendorAggregate(
    int? VendorId,
    string VendorName,
    decimal PreQty,
    decimal PreRevenue,
    decimal PostQty,
    decimal PostRevenue,
    decimal ChangeQty,
    decimal ChangeRevenue,
    decimal? SemanticChangePercentRevenue,
    int ComparableArticles,
    int MatureComparableRows,
    int ImmatureComparableRows,
    int IncreasedPriceArticles,
    int DecreasedPriceArticles,
    string EffectStatus);

internal sealed record AssortmentAggregate(
    IReadOnlyList<AssortmentAnalyzedRow> Rows,
    AssortmentTotals Totals,
    IReadOnlyList<AssortmentVendorAggregate> Vendors,
    IReadOnlyList<long> CohortEventIds);

internal static class AssortmentNivelacijaOracle
{
    private const int WindowDays = 30;
    private static readonly string[] NivelacijaTypes = ["Nivelacija", "Nivelacija cena"];

    public static IReadOnlyList<AssortmentSourceRow> SourceRows(AssortmentFixture fixture, AssortmentSourceOptions options)
    {
        var articles = fixture.Articles.ToDictionary(a => a.Id);
        var vendors = fixture.Vendors.ToDictionary(v => v.Id, v => v.Name);

        var events = fixture.Events
            .Where(e => NivelacijaTypes.Contains(e.TipPromene, StringComparer.Ordinal))
            .Where(e => articles.ContainsKey(e.ArticleId))
            .Where(e => NivelacijaEventScopePolicy.AppliesToStore(e.StoreId, options.StoreId))
            .GroupBy(e => options.PartitionEventsByStore
                ? (e.ArticleId, e.Day, e.OldPrice, e.NewPrice, Store: e.StoreId)
                : (e.ArticleId, e.Day, e.OldPrice, e.NewPrice, Store: (int?)0))
            .Select(g => g.MaxBy(e => e.Id)!)
            .ToList();

        var dailySales = fixture.Sales
            .Where(s => !IsExcludedReceipt(s.ReceiptNumber))
            .Where(s => options.StoreId is null || s.StoreId == options.StoreId)
            .GroupBy(s => (s.ArticleId, s.Day))
            .Select(g => (g.Key.ArticleId, g.Key.Day, Units: (decimal)g.Sum(s => s.Qty), Revenue: g.Sum(s => s.Qty * s.Price)))
            .ToList();

        var rows = new List<AssortmentSourceRow>();
        foreach (var e in events)
        {
            var article = articles[e.ArticleId];
            var vendorId = e.EventVendorId ?? article.VendorId;
            var vendorName = vendorId is { } id && vendors.TryGetValue(id, out var name) ? name : null;

            var pre = dailySales.Where(s => s.ArticleId == e.ArticleId && s.Day >= e.Day.AddDays(-WindowDays) && s.Day < e.Day).ToList();
            var post = dailySales.Where(s => s.ArticleId == e.ArticleId && s.Day >= e.Day && s.Day < e.Day.AddDays(WindowDays)).ToList();
            var mature = e.Day.AddDays(WindowDays) <= options.AsOf;

            decimal? preQty = pre.Count == 0 ? null : pre.Sum(s => s.Units);
            decimal? preRevenue = pre.Count == 0 ? null : pre.Sum(s => s.Revenue);
            decimal? postQty = post.Count == 0 ? (mature ? 0m : null) : post.Sum(s => s.Units);
            decimal? postRevenue = post.Count == 0 ? (mature ? 0m : null) : post.Sum(s => s.Revenue);

            rows.Add(new AssortmentSourceRow(
                PriceEventId: e.Id,
                EventDate: e.Day,
                VendorId: vendorId,
                VendorName: vendorName,
                ArticleId: e.ArticleId,
                Sku: article.Sku,
                Category: article.Category,
                OldPrice: e.OldPrice,
                NewPrice: e.NewPrice,
                PreQty: preQty,
                PreRevenue: preRevenue,
                PostQty: postQty,
                PostRevenue: postRevenue,
                CoveragePre30: Coverage(pre.Count),
                CoveragePost30: Coverage(post.Count),
                ChangeQty: postQty - preQty,
                ChangeRevenue: postRevenue - preRevenue,
                HasQtyBaseline: preQty > 0m,
                QtyBaselineReason: BaselineReason(preQty, postQty, "qty"),
                ChangePercentQtySemantic: SemanticPercent(preQty, postQty),
                HasRevenueBaseline: preRevenue > 0m,
                RevenueBaselineReason: BaselineReason(preRevenue, postRevenue, "revenue"),
                ChangePercentRevenueSemantic: SemanticPercent(preRevenue, postRevenue)));
        }

        return rows.OrderBy(r => r.PriceEventId).ToList();
    }

    /// <summary>
    /// Declared endpoint population: endpoint event dedup, maturity against
    /// <paramref name="asOf"/>, latest event per article, changed price only,
    /// rows with sales evidence; totals and vendor sums use mature comparable rows.
    /// </summary>
    public static AssortmentAggregate Aggregate(IEnumerable<AssortmentSourceRow> sourceRows, DateOnly asOf)
    {
        var asOfUtc = asOf.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var deduplicated = sourceRows
            .GroupBy(r => (r.EventDate, r.VendorId, r.ArticleId, r.OldPrice, r.NewPrice))
            .Select(g => g.MaxBy(r => r.PriceEventId)!)
            .ToList();

        var dtos = deduplicated.Select(r => ToDto(r, asOfUtc)).ToList();
        var cohort = VendorSalesNivelacijaCohortPolicy.SelectLatestEventPerArticle(dtos);
        var analyzed = cohort
            .Where(x => !(x.OldPrice.HasValue && x.NewPrice.HasValue && x.OldPrice.Value == x.NewPrice.Value))
            .Where(x => x.HasSalesWindow)
            .OrderBy(x => x.ArticleId)
            .ToList();

        var matureComparable = analyzed.Where(x => x.HasComparableSalesWindow && x.IsPostWindowMature).ToList();
        var preRevenue = matureComparable.Sum(x => x.PreRevenue);
        var postRevenue = matureComparable.Sum(x => x.PostRevenue);
        var preQty = (decimal)matureComparable.Sum(x => x.PreQty);
        var postQty = (decimal)matureComparable.Sum(x => x.PostQty);

        var totals = new AssortmentTotals(
            PreQty: preQty,
            PreRevenue: preRevenue,
            PostQty: postQty,
            PostRevenue: postRevenue,
            ChangeQty: postQty - preQty,
            ChangeRevenue: postRevenue - preRevenue,
            SemanticChangePercentRevenue: VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeCohortChangePercent(matureComparable.Count, preRevenue, postRevenue),
            ComparableRows: matureComparable.Count,
            VendorsCount: matureComparable.Select(x => SupplierUnknownBucketPolicy.Resolve(x.VendorId, x.VendorName).SupplierId).Distinct().Count(),
            ArticlesCount: matureComparable.Select(x => x.Sku).Distinct(StringComparer.Ordinal).Count());

        var vendors = analyzed
            .GroupBy(x => SupplierUnknownBucketPolicy.Resolve(x.VendorId, x.VendorName))
            .Select(g =>
            {
                var comparable = g.Where(x => x.HasComparableSalesWindow).ToList();
                var mature = comparable.Where(x => x.IsPostWindowMature).ToList();
                var vPre = mature.Sum(x => x.PreRevenue);
                var vPost = mature.Sum(x => x.PostRevenue);
                var vPreQty = (decimal)mature.Sum(x => x.PreQty);
                var vPostQty = (decimal)mature.Sum(x => x.PostQty);
                var semantic = VendorSalesNivelacijaPriceChangeEffectPolicy.ComputeCohortChangePercent(mature.Count, vPre, vPost);
                var comparableArticles = comparable.Select(x => x.Sku).Distinct(StringComparer.Ordinal).Count();

                var effect = VendorSalesNivelacijaPriceChangeEffectPolicy.Evaluate(
                    new VendorSalesNivelacijaPriceChangeEffectPolicy.VendorAggregateInput(
                        IsUnknownVendor: g.Key.IsUnknown,
                        PreRevenue: vPre,
                        PostRevenue: vPost,
                        PreQty: (int)vPreQty,
                        PostQty: (int)vPostQty,
                        ComparableArticleCount: comparableArticles,
                        MatureComparableArticleCount: mature.Count,
                        ImmatureComparableArticleCount: comparable.Count - mature.Count,
                        SemanticChangePercentRevenue: semantic.HasValue ? (double)semantic.Value : null,
                        MarginPct: 0d,
                        MarginCoveragePct: null,
                        SplitCoveragePct: null));

                return new AssortmentVendorAggregate(
                    VendorId: g.Key.SupplierId,
                    VendorName: g.Key.SupplierName,
                    PreQty: vPreQty,
                    PreRevenue: vPre,
                    PostQty: vPostQty,
                    PostRevenue: vPost,
                    ChangeQty: vPostQty - vPreQty,
                    ChangeRevenue: vPost - vPre,
                    SemanticChangePercentRevenue: semantic,
                    ComparableArticles: comparableArticles,
                    MatureComparableRows: mature.Count,
                    ImmatureComparableRows: comparable.Count - mature.Count,
                    IncreasedPriceArticles: mature.Count(x => x.PriceChangePercent > 0m),
                    DecreasedPriceArticles: mature.Count(x => x.PriceChangePercent < 0m),
                    EffectStatus: effect.Status);
            })
            .OrderBy(v => v.VendorId ?? int.MaxValue)
            .ToList();

        var rows = analyzed
            .Select(x => new AssortmentAnalyzedRow(
                x.PriceEventId,
                x.ArticleId,
                DateOnly.FromDateTime(x.EventDate),
                x.VendorId,
                x.VendorName,
                x.PreQty,
                x.PreRevenue,
                x.PostQty,
                x.PostRevenue,
                x.IsPostWindowMature,
                x.PostWindowDaysElapsed,
                x.HasComparableSalesWindow,
                x.RevenueBaselineReason,
                x.SemanticChangePercentRevenue,
                x.PriceChangePercent))
            .ToList();

        return new AssortmentAggregate(rows, totals, vendors, cohort.Select(x => x.PriceEventId).OrderBy(id => id).ToList());
    }

    private static VendorSalesNivelacijaArticleStatDto ToDto(AssortmentSourceRow r, DateTime asOfUtc)
    {
        var eventUtc = r.EventDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var comparable = r.PreQty.HasValue && r.PostQty.HasValue && r.PreRevenue.HasValue && r.PostRevenue.HasValue
            && r.HasQtyBaseline && r.HasRevenueBaseline;
        var mature = VendorSalesNivelacijaPriceChangeEffectPolicy.IsPostWindowMature(eventUtc, asOfUtc);
        var postQty = r.PostQty ?? (mature ? 0m : null);
        var postRevenue = r.PostRevenue ?? (mature ? 0m : null);
        var priceChanged = r.OldPrice.HasValue && r.NewPrice.HasValue && r.OldPrice.Value != r.NewPrice.Value;

        return new VendorSalesNivelacijaArticleStatDto
        {
            PriceEventId = r.PriceEventId,
            EventDate = eventUtc,
            VendorId = r.VendorId,
            VendorName = r.VendorName ?? "Nepoznato",
            ArticleId = r.ArticleId,
            Sku = r.Sku ?? r.ArticleId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            OldPrice = r.OldPrice,
            NewPrice = r.NewPrice,
            PreQty = r.PreQty.HasValue ? (int)r.PreQty.Value : 0,
            PreRevenue = r.PreRevenue ?? 0m,
            PostQty = postQty.HasValue ? (int)postQty.Value : 0,
            PostRevenue = postRevenue ?? 0m,
            HasSalesWindow = r.PreQty.HasValue || postQty.HasValue || r.PreRevenue.HasValue || postRevenue.HasValue,
            PriceChanged = priceChanged,
            PriceChangePercent = priceChanged && r.OldPrice!.Value != 0m
                ? Math.Round(((r.NewPrice!.Value - r.OldPrice.Value) / r.OldPrice.Value) * 100m, 2)
                : null,
            HasComparableSalesWindow = comparable,
            HasRevenueBaseline = r.HasRevenueBaseline,
            RevenueBaselineReason = r.RevenueBaselineReason,
            SemanticChangePercentRevenue = r.ChangePercentRevenueSemantic,
            IsPostWindowMature = mature,
            PostWindowDaysElapsed = VendorSalesNivelacijaPriceChangeEffectPolicy.ComputePostWindowDaysElapsed(eventUtc, asOfUtc)
        };
    }

    private static bool IsExcludedReceipt(string receiptNumber)
    {
        var normalized = receiptNumber.Trim().ToUpperInvariant();
        return normalized is "DUG" or "KOREKCIJA";
    }

    private static decimal? Coverage(int days) => days == 0 ? null : Math.Min(days / 30m, 1m);

    private static decimal? SemanticPercent(decimal? pre, decimal? post) =>
        pre is null || post is null || pre == 0m
            ? null
            : Math.Round((post.Value - pre.Value) / pre.Value * 100m, 2, MidpointRounding.AwayFromZero);

    private static string? BaselineReason(decimal? pre, decimal? post, string metric)
    {
        if (pre is null)
        {
            return $"missing_pre_{metric}_window";
        }

        if (pre == 0m && post > 0m)
        {
            return $"no_pre_{metric}_baseline_uplift";
        }

        return pre == 0m && post == 0m ? $"no_pre_{metric}_baseline_flat" : null;
    }
}
