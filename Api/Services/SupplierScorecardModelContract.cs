namespace Api.Services;

/// <summary>
/// Stable, machine-readable description of the scorecard formula currently
/// deployed by the 018/029 score caches. This contract documents policy; it
/// does not provide a second scoring implementation.
/// </summary>
public static class SupplierScorecardModelContract
{
    public const string FormulaVersion = "supplier-scorecard-v1";
    public const string ExplainabilityVersion = "supplier-scorecard-explainability-v1";

    public static SupplierScorecardModelMetadata Current { get; } = new(
        FormulaVersion,
        ExplainabilityVersion,
        "0-100",
        "all suppliers returned by the active scorecard dataset after filters; ranks are relative to this population",
        "signed pre-markdown revenue: revenue * fullpriceRevenueShare; negative values remain negative",
        "current article stock and current positive article cost snapshot; historical sales inputs remain period-bound",
        [
            new("demand.fullprice_sellthrough", "Full-price sell-through rank", "positive", 0.60m, "rank[0,1] * 100", "fullprice_sellthrough", null, null),
            new("demand.fullprice_revenue_share", "Full-price revenue share rank", "positive", 0.40m, "rank[0,1] * 100", "fullprice_revenue_share", null, null),
            new("margin.pre_markdown", "Pre-markdown margin rank", "positive", 1.00m, "clamp(pre_markdown_margin_pct, 0, margin_p80) -> rank[0,1] * 100", "pre_markdown_margin_pct", 0m, null),
            new("penalty.markdown_dependency", "Markdown dependency rank", "penalty", -1.00m, "rank[0,1] * seasonal multiplier (75/85/100)", "markdown_revenue_share", 0m, 100m),
            new("penalty.dead_stock", "Dead-stock rate rank", "penalty", -0.50m, "rank[0,1] * 100", "dead_stock_rate", 0m, 100m),
            new("penalty.unsold_stock_value", "Unsold current-stock value rank", "penalty", -0.50m, "rank[0,1] * 100", "unsold_stock_value", 0m, null),
            new("quality.repeat_winner", "Repeat-winner rank", "quality", 0.50m, "rank[0,1] * 100", "repeat_winner_rate", 0m, 100m),
            new("quality.return_rate", "Return-rate rank", "quality", -0.30m, "rank[0,1] * 100; missing baseline uses neutral 0.5", "return_rate", 0m, 100m),
            new("quality.category_focus", "Category-focus rank", "quality", 0.20m, "rank[0,1] * 100", "category_focus_score", 0m, 100m),
            new("confidence", "Evidence confidence", null, null, "weighted evidence inputs clamped to [0,1]", "coverage_and_signal_inputs", 0m, 1m),
            new("score.final", "Displayed supplier score", "output", null, "clamp(demand + margin - markdown - inventory + quality, 0, 100)", "supplier_quality_index", 0m, 100m)
        ],
        [
            new("coverage_gate", "Incomplete signal coverage", "post/did/cost coverage < 1 => REVIEW_QUALITY"),
            new("return_rate_gate", "High return rate", "return rate > 0.12 => REVIEW_QUALITY"),
            new("stockout_gate", "Stockout before markdown", "stockout flag => OOS_FALSE_NEGATIVE"),
            new("score_expand", "Expand cutoff", "score > 80 => EXPAND"),
            new("score_expand_selectively", "Expand selectively cutoff", "score >= 60 => EXPAND_SELECTIVELY"),
            new("score_hold", "Hold cutoff", "score >= 40 => HOLD"),
            new("score_price_negotiate", "Price negotiate cutoff", "score >= 25 => PRICE_NEGOTIATE"),
            new("single_population_rank", "Single-row rank guard", "one supplier => rank 1"),
            new("missing_return_rank", "Missing return-rate rank", "missing/only-known rate => neutral rank 0.5"),
            new("margin_p80_clamp", "Margin percentile clamp", "pre-markdown margin is capped at population percentile_cont(0.80)")
        ],
        [
            "owner_decision_pending: coverage below 100% blocks recommendation",
            "owner_decision_pending: confidence weights currently sum to 130% before the [0,1] clamp",
            "owner_decision_pending: inventory penalty uses absolute current-stock value and exposes supplier-size effects",
            "owner_decision_pending: score ranks are population-relative and can move when the filtered population changes",
            "owner_decision_pending: customer-return stock moves and negative sale lines may both affect the current stock proxy"
        ],
        [
            "Current stock is a snapshot input; it does not prove that a supplier caused dead stock or sell-through.",
            "Ranks are relative to the returned supplier population; adding or removing suppliers can change an existing score.",
            "The scorecard is a signal for review, not a causal estimate or profit forecast.",
            "A missing or unavailable denominator remains unavailable; it is never a trusted zero."
        ]);
}

public sealed record SupplierScorecardModelMetadata(
    string FormulaVersion,
    string ExplainabilityVersion,
    string ScoreScale,
    string RankPopulation,
    string SignedMarginContributionDenominator,
    string StockInputBasis,
    IReadOnlyList<SupplierScorecardComponentMetadata> Components,
    IReadOnlyList<SupplierScorecardGateMetadata> Gates,
    IReadOnlyList<string> OwnerDecisionPending,
    IReadOnlyList<string> Limitations);

public sealed record SupplierScorecardComponentMetadata(
    string Key,
    string Label,
    string? Role,
    decimal? Weight,
    string Transform,
    string SourceField,
    decimal? RangeMin,
    decimal? RangeMax);

public sealed record SupplierScorecardGateMetadata(
    string Key,
    string Label,
    string Rule);

public sealed record SupplierScorecardContribution(
    string Key,
    string Label,
    decimal Value,
    decimal? Rank,
    decimal? Weight,
    string Sign,
    string SourceField);

public sealed record SupplierScorecardRowExplanation(
    string FormulaVersion,
    string SourceBasis,
    string RankPopulation,
    int RankPopulationCount,
    decimal? MarginP80,
    decimal SignedMarginContributionDenominator,
    string MarginContributionDenominatorBasis,
    string StockInputBasis,
    decimal ScoreRaw,
    decimal DisplayedScore,
    bool ScoreClamped,
    IReadOnlyList<SupplierScorecardContribution> Contributions,
    IReadOnlyList<string> GateCodes,
    IReadOnlyList<string> Limitations);
