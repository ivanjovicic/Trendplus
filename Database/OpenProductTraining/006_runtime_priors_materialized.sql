-- =============================================================
-- Open Product Training: runtime priors materialized view
-- Created: 2026-02-28
-- Depends on: 003_add_ml_export_views.sql
-- =============================================================

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_brand_shoe_runtime_priors AS
SELECT
    brand_key,
    shoe_type_key,
    popularity_prior_score,
    typical_price,
    sample_size
FROM vw_brand_shoe_runtime_priors;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_brand_shoe_runtime_priors_pk
    ON mv_brand_shoe_runtime_priors (brand_key, shoe_type_key);

CREATE INDEX IF NOT EXISTS idx_mv_brand_shoe_runtime_priors_samples
    ON mv_brand_shoe_runtime_priors (sample_size DESC);

-- Initial refresh to keep startup behavior deterministic.
REFRESH MATERIALIZED VIEW mv_brand_shoe_runtime_priors;

