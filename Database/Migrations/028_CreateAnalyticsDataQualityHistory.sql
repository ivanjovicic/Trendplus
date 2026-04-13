CREATE TABLE IF NOT EXISTS analytics_data_quality_history (
    id BIGSERIAL PRIMARY KEY,
    snapshot_date_utc DATE NOT NULL,
    captured_at_utc TIMESTAMPTZ NOT NULL,
    lookback_days INTEGER NOT NULL,
    orphan_article_count INTEGER NOT NULL DEFAULT 0,
    missing_cost_revenue NUMERIC(18, 2) NOT NULL DEFAULT 0,
    missing_cost_revenue_share_pct DOUBLE PRECISION NOT NULL DEFAULT 0,
    unknown_supplier_revenue NUMERIC(18, 2) NOT NULL DEFAULT 0,
    unknown_supplier_revenue_share_pct DOUBLE PRECISION NOT NULL DEFAULT 0,
    data_scope VARCHAR(20) NOT NULL DEFAULT 'all'
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_analytics_data_quality_history_snapshot
    ON analytics_data_quality_history (snapshot_date_utc, data_scope, lookback_days);

CREATE INDEX IF NOT EXISTS ix_analytics_data_quality_history_scope_date
    ON analytics_data_quality_history (data_scope, snapshot_date_utc DESC);