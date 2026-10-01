using System.Globalization;
using Trendplus2.Dtos;

namespace Api.Services;

/// <summary>
/// Declares how each Supplier tab counts. The codes describe the current code
/// paths (supplier-sales-stats, Supplier Decision Hub, vendor-sales-nivelacija);
/// change them together with the path they describe.
/// </summary>
public static class SupplierTabBasisPolicy
{
    public const string Version = "supplier_tab_basis_v1";

    public const string OverviewTab = "overview";
    public const string ScorecardTab = "scorecard";
    public const string AssortmentTab = "assortment";

    public const string SaleTimeSupplier = "sale_time_supplier";
    public const string MarkdownEventSupplierWithSaleTimeReturns = "markdown_event_supplier_with_sale_time_returns";
    public const string MarkdownEventSupplier = "markdown_event_supplier";

    public const string SaleLineThenSnapshotThenCurrentCost = "sale_line_then_snapshot_then_current_article_cost";
    public const string SaleLineThenCurrentCost = "sale_line_then_current_article_cost";
    public const string CurrentArticleCost = "current_article_cost";

    public const string RetailReceiptsExcludingDugKorekcija = "retail_receipts_excluding_dug_korekcija";

    public const string AllSalesInPeriod = "all_sales_in_period";
    public const string FirstMarkdownPerArticle = "first_markdown_per_article";
    public const string LatestPriceEventPerArticle = "latest_price_event_per_article_including_increases";

    public const string SaleDateInPeriod = "sale_date_in_period";
    public const string AllTimeMarkdownsWithPrePostWindows = "all_time_markdowns_with_30d_pre_post";
    public const string RollingWindowMarkdownsWithPrePostWindows = "first_markdown_in_rolling_window_with_30d_pre_post";
    public const string PriceEventDateInPeriodWithPrePostWindows = "price_event_date_in_period_with_30d_pre_post";

    public const string ReceiptStoreWithChainWideMarkdowns = "receipt_store_with_chain_wide_markdowns";
    public const string StoreFilterLimitsSalesEvidence = "store_filter_limits_sales_evidence";
    public const string StoreFilterAppliesToEventsAndSales = "store_filter_applies_to_events_and_sales";

    public const string SingleUnknownBucket = SupplierUnknownBucketPolicy.Policy;
    public const string UnresolvedSupplierNotCollapsed = "unresolved_supplier_not_collapsed";

    /// <summary>
    /// Supplier endpoints default periods and date filters on UTC calendar days;
    /// there is no business-timezone contract for these surfaces yet.
    /// </summary>
    public const string Timezone = "UTC";

    public static SupplierTabBasisDto Overview(DateTime? nowUtc = null) => new()
    {
        Tab = OverviewTab,
        Version = Version,
        SupplierAttribution = SaleTimeSupplier,
        CostBasis = SaleLineThenSnapshotThenCurrentCost,
        ReceiptPopulation = RetailReceiptsExcludingDugKorekcija,
        Cohort = AllSalesInPeriod,
        PeriodSemantics = SaleDateInPeriod,
        StoreScope = ReceiptStoreWithChainWideMarkdowns,
        UnknownSupplierPolicy = SingleUnknownBucket,
        AsOfDate = AsOfDate(nowUtc),
        Timezone = Timezone
    };

    public static SupplierTabBasisDto Scorecard(string? effectiveDataset, DateTime? nowUtc = null) => new()
    {
        Tab = ScorecardTab,
        Version = Version,
        SupplierAttribution = MarkdownEventSupplierWithSaleTimeReturns,
        CostBasis = SaleLineThenCurrentCost,
        ReceiptPopulation = RetailReceiptsExcludingDugKorekcija,
        Cohort = FirstMarkdownPerArticle,
        PeriodSemantics = string.Equals(effectiveDataset, "all_time", StringComparison.OrdinalIgnoreCase)
            ? AllTimeMarkdownsWithPrePostWindows
            : RollingWindowMarkdownsWithPrePostWindows,
        StoreScope = StoreFilterLimitsSalesEvidence,
        UnknownSupplierPolicy = UnresolvedSupplierNotCollapsed,
        AsOfDate = AsOfDate(nowUtc),
        Timezone = Timezone
    };

    public static SupplierTabBasisDto Assortment(DateTime? nowUtc = null) => new()
    {
        Tab = AssortmentTab,
        Version = Version,
        SupplierAttribution = MarkdownEventSupplier,
        CostBasis = CurrentArticleCost,
        ReceiptPopulation = RetailReceiptsExcludingDugKorekcija,
        Cohort = LatestPriceEventPerArticle,
        PeriodSemantics = PriceEventDateInPeriodWithPrePostWindows,
        StoreScope = StoreFilterAppliesToEventsAndSales,
        UnknownSupplierPolicy = SingleUnknownBucket,
        AsOfDate = AsOfDate(nowUtc),
        Timezone = Timezone
    };

    private static string AsOfDate(DateTime? nowUtc) =>
        (nowUtc ?? DateTime.UtcNow).ToUniversalTime().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
