namespace Infrastructure.Configuration;

public sealed class NightlyAnalyticsRefreshOptions
{
    public const string Section = "NightlyAnalyticsRefresh";

    /// <summary>
    /// Allows disabling this worker even when the global workers switch is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Daily run time in UTC (HH:mm).
    /// </summary>
    public string RunAtUtc { get; set; } = "00:10";

    /// <summary>
    /// If true, uses REFRESH MATERIALIZED VIEW CONCURRENTLY (requires unique index on MV).
    /// </summary>
    public bool RefreshConcurrently { get; set; } = true;

    /// <summary>
    /// Run once on startup if the scheduled time for today has already passed and
    /// the job hasn't been attempted today.
    /// </summary>
    public bool CatchUpIfMissed { get; set; } = true;

    /// <summary>
    /// Maximum window (in hours) after RunAtUtc in which a missed run is still allowed to execute.
    /// This avoids heavy refreshes during business hours if the app was down overnight.
    /// </summary>
    public int CatchUpMaxHours { get; set; } = 4;

    /// <summary>
    /// If CatchUpIfMissed is false, the job will only start within this grace period after RunAtUtc.
    /// </summary>
    public int GracePeriodMinutes { get; set; } = 15;

    public int StartupDelaySeconds { get; set; } = 30;
    public int PauseCheckSeconds { get; set; } = 5;
    public int HeartbeatSeconds { get; set; } = 300;

    public int CommandTimeoutSeconds { get; set; } = 1800;

    public List<string> MaterializedViewsToRefresh { get; set; } = new()
    {
        "mv_daily_sales_facts",
        "mv_sales_rolling_7d",
        "mv_sales_momentum",
        "supplier_training_dataset_v1",
        "mv_supplier_markdown_dependency_cache",
        "mv_supplier_decision_score_cache",
        "mv_supplier_decision_score_cache_90d",
        "mv_supplier_decision_score_cache_180d",
        "mv_supplier_recommendations_cache"
    };

    /// <summary>
    /// Intelligence-layer materialized views refreshed after core analytics MVs.
    /// Order matters because downstream dashboards expect demand, inventory,
    /// price and trend caches in that sequence.
    /// </summary>
    public List<string> IntelligenceMaterializedViewsToRefresh { get; set; } = new()
    {
        "analytics_intel.mv_product_demand_signals_v1_cache",
        "analytics_intel.mv_inventory_risk_signals_v1_cache",
        "analytics_intel.mv_price_intelligence_v1_cache",
        "analytics_intel.mv_trend_momentum_v1_cache"
    };

    public List<string> VacuumAnalyzeTargets { get; set; } = new()
    {
        "prodaja_stavke",
        "prodaja_zaglavlje",
        "mv_daily_sales_facts"
    };

    /// <summary>
    /// Optional OpenProductTraining materialized views refreshed in the same nightly window.
    /// Uses OpenProductTraining DB connection string.
    /// </summary>
    public List<string> OpenTrainingMaterializedViewsToRefresh { get; set; } = new()
    {
        "mv_brand_shoe_runtime_priors"
    };

    /// <summary>
    /// If true, queue a supplier ranking training job after a successful nightly refresh.
    /// </summary>
    public bool QueueSupplierRankingTraining { get; set; } = true;
}
