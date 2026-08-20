namespace Application.Analytics.Queries.GetForecastBaselineBacktest;

/// <summary>
/// Canonical RQ98 forecast baseline / backtesting vocabulary.
/// Measured scores are forbidden until a trustworthy comparison window exists.
/// </summary>
public static class ForecastBaselineBacktestContract
{
    public const string EvaluationUnavailable = "unavailable";
    public const string EvaluationPartial = "partial";
    public const string EvaluationReady = "ready";

    public const string BaselineNaiveLastPeriod = "naive_last_period";
    public const string BaselineSeasonalNaive = "seasonal_naive";

    public const string MetricWape = "wape";
    public const string MetricBias = "bias";
    public const string MetricMae = "mae";

    public const string CohortSufficientHistory = "sufficient_history";
    public const string CohortSparse = "sparse";
    public const string CohortNewItem = "new_item";
    public const string CohortNoHistory = "no_history";

    public const string ReasonMissingTrustedForecastMaterializer = "missing_trusted_forecast_materializer";
    public const string ReasonInsufficientObservedStockWindow = "insufficient_observed_stock_comparison_window";
    public const string ReasonNoPairedForecastOutcomeSeries = "no_paired_forecast_outcome_series";

    public static readonly IReadOnlyList<int> HorizonDays = [7, 14, 28];

    public static readonly IReadOnlyList<string> AllowedBaselines =
    [
        BaselineNaiveLastPeriod,
        BaselineSeasonalNaive
    ];

    public static readonly IReadOnlyList<string> AllowedMetrics =
    [
        MetricWape,
        MetricBias,
        MetricMae
    ];

    public static readonly IReadOnlyList<string> AllowedCohorts =
    [
        CohortSufficientHistory,
        CohortSparse,
        CohortNewItem,
        CohortNoHistory
    ];
}
