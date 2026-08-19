-- ==========================================================
-- 025_inventory_snapshot_foundation_v1.sql
-- Canonical observed inventory snapshot foundation with explicit provenance.
--
-- This bounded slice preserves the distinction between:
-- - observed daily stock from ProductsDim timestamps
-- - reconstructed proxy stock from the existing inventory risk view
-- - mixed evidence when the observed and reconstructed sources disagree
-- - missing evidence when neither source can support the day
-- ==========================================================

DO $$
DECLARE
    _actual text[];
    _expect text[] := ARRAY[
        'article_id',
        'snapshot_date',
        'sku',
        'product_name',
        'observed_at_utc',
        'observed_stock_qty',
        'reconstructed_stock_qty',
        'stock_qty',
        'snapshot_source_status',
        'has_mixed_evidence',
        'source_records'
    ];
BEGIN
    SELECT array_agg(c.column_name::text ORDER BY c.ordinal_position)
      INTO _actual
      FROM information_schema.columns c
     WHERE c.table_schema = 'analytics_intel'
       AND c.table_name = 'vw_inventory_snapshot_foundation_v1';

    IF _actual IS NOT NULL AND _actual IS DISTINCT FROM _expect THEN
        RAISE NOTICE '025: vw_inventory_snapshot_foundation_v1 column structure changed - dropping cache + view';
        DROP MATERIALIZED VIEW IF EXISTS analytics_intel.mv_inventory_snapshot_foundation_v1_cache;
        DROP VIEW IF EXISTS analytics_intel.vw_inventory_snapshot_foundation_v1 CASCADE;
    END IF;
END $$;

CREATE OR REPLACE VIEW analytics_intel.vw_inventory_snapshot_foundation_v1 AS
WITH settings AS (
    SELECT
        CURRENT_DATE::date AS as_of_date,
        120::int AS horizon_days
),
latest_products AS (
    SELECT DISTINCT ON (pd."ProductId")
        pd."ProductId" AS article_id,
        COALESCE(NULLIF(BTRIM(pd."PLU"), ''), pd."ProductId"::text) AS sku,
        COALESCE(NULLIF(BTRIM(pd."ProductName"), ''), 'Unknown product') AS product_name
    FROM "ProductsDim" pd
    ORDER BY pd."ProductId", pd."Timestamp" DESC, pd."ProductKey" DESC
),
observed_daily_stock AS (
    SELECT
        ranked.article_id,
        ranked.snapshot_date,
        ranked.observed_at_utc,
        ranked.sku,
        ranked.product_name,
        ranked.observed_stock_qty,
        ranked.source_records
    FROM (
        SELECT
            pd."ProductId" AS article_id,
            DATE_TRUNC('day', pd."Timestamp")::date AS snapshot_date,
            pd."Timestamp" AS observed_at_utc,
            COALESCE(NULLIF(BTRIM(pd."PLU"), ''), pd."ProductId"::text) AS sku,
            COALESCE(NULLIF(BTRIM(pd."ProductName"), ''), 'Unknown product') AS product_name,
            pd."Kolicina"::numeric(18,4) AS observed_stock_qty,
            COUNT(*) OVER (
                PARTITION BY pd."ProductId", DATE_TRUNC('day', pd."Timestamp")::date
            )::integer AS source_records,
            ROW_NUMBER() OVER (
                PARTITION BY pd."ProductId", DATE_TRUNC('day', pd."Timestamp")::date
                ORDER BY pd."Timestamp" DESC, pd."ProductKey" DESC
            ) AS rn
        FROM "ProductsDim" pd
        JOIN settings s
          ON pd."Timestamp"::date >= s.as_of_date - s.horizon_days
        WHERE pd."Timestamp" IS NOT NULL
    ) ranked
    WHERE ranked.rn = 1
),
reconstructed_stock AS (
    SELECT
        rr.article_id,
        rr.date AS snapshot_date,
        rr.stock_qty AS reconstructed_stock_qty
    FROM analytics_intel.vw_inventory_risk_signals_v1 rr
),
foundation AS (
    SELECT
        COALESCE(o.article_id, r.article_id) AS article_id,
        COALESCE(o.snapshot_date, r.snapshot_date) AS snapshot_date,
        COALESCE(o.sku, lp.sku) AS sku,
        COALESCE(o.product_name, lp.product_name) AS product_name,
        o.observed_at_utc,
        o.observed_stock_qty,
        r.reconstructed_stock_qty,
        CASE
            WHEN o.article_id IS NULL AND r.article_id IS NULL THEN 'missing'
            WHEN o.article_id IS NOT NULL AND o.observed_stock_qty IS NULL AND r.reconstructed_stock_qty IS NOT NULL THEN 'mixed'
            WHEN o.observed_stock_qty IS NOT NULL AND r.reconstructed_stock_qty IS NOT NULL AND o.observed_stock_qty <> r.reconstructed_stock_qty THEN 'mixed'
            WHEN o.observed_stock_qty IS NOT NULL THEN 'observed'
            WHEN r.reconstructed_stock_qty IS NOT NULL THEN 'reconstructed'
            WHEN o.article_id IS NOT NULL THEN 'missing'
            ELSE 'missing'
        END AS snapshot_source_status,
        COALESCE(o.source_records, 0)::integer AS source_records,
        CASE
            WHEN COALESCE(o.source_records, 0) > 1 THEN TRUE
            WHEN o.article_id IS NOT NULL AND o.observed_stock_qty IS NULL AND r.reconstructed_stock_qty IS NOT NULL THEN TRUE
            WHEN o.observed_stock_qty IS NOT NULL AND r.reconstructed_stock_qty IS NOT NULL AND o.observed_stock_qty <> r.reconstructed_stock_qty THEN TRUE
            ELSE FALSE
        END AS has_mixed_evidence,
        ROUND(COALESCE(o.observed_stock_qty, r.reconstructed_stock_qty), 4) AS stock_qty
    FROM observed_daily_stock o
    FULL OUTER JOIN reconstructed_stock r
      ON r.article_id = o.article_id
     AND r.snapshot_date = o.snapshot_date
    LEFT JOIN latest_products lp
      ON lp.article_id = COALESCE(o.article_id, r.article_id)
)
SELECT
    article_id,
    snapshot_date,
    sku,
    product_name,
    observed_at_utc,
    observed_stock_qty,
    reconstructed_stock_qty,
    stock_qty,
    snapshot_source_status,
    has_mixed_evidence,
    source_records
FROM foundation;

COMMENT ON VIEW analytics_intel.vw_inventory_snapshot_foundation_v1 IS
'Canonical observed daily inventory snapshot foundation. Observed ProductsDim rows win when available; reconstructed proxy stock is carried separately so downstream analytics can tell observed, reconstructed, missing and mixed evidence apart.';

COMMENT ON COLUMN analytics_intel.vw_inventory_snapshot_foundation_v1.stock_qty IS
'Preferred day-level stock quantity. Observed stock wins when available; reconstructed proxy is kept in reconstructed_stock_qty and provenance is explicit in snapshot_source_status.';

COMMENT ON COLUMN analytics_intel.vw_inventory_snapshot_foundation_v1.observed_stock_qty IS
'Latest observed ProductsDim quantity for the day. NULL means the day has no usable observed stock evidence.';

COMMENT ON COLUMN analytics_intel.vw_inventory_snapshot_foundation_v1.reconstructed_stock_qty IS
'Backwards-built proxy stock from the existing inventory risk view. This is preserved separately from observed stock.';

COMMENT ON COLUMN analytics_intel.vw_inventory_snapshot_foundation_v1.snapshot_source_status IS
'Provenance state for the day: observed, reconstructed, mixed or missing.';

COMMENT ON COLUMN analytics_intel.vw_inventory_snapshot_foundation_v1.has_mixed_evidence IS
'True when the day contains multiple observed rows or when observed and reconstructed evidence disagree.';

CREATE MATERIALIZED VIEW IF NOT EXISTS analytics_intel.mv_inventory_snapshot_foundation_v1_cache AS
SELECT *
FROM analytics_intel.vw_inventory_snapshot_foundation_v1;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_inventory_snapshot_foundation_v1_cache_pk
    ON analytics_intel.mv_inventory_snapshot_foundation_v1_cache (article_id, snapshot_date);

CREATE INDEX IF NOT EXISTS idx_mv_inventory_snapshot_foundation_v1_cache_date
    ON analytics_intel.mv_inventory_snapshot_foundation_v1_cache (snapshot_date DESC);

COMMENT ON MATERIALIZED VIEW analytics_intel.mv_inventory_snapshot_foundation_v1_cache IS
'Materialized cache for analytics_intel.vw_inventory_snapshot_foundation_v1. Unique key supports REFRESH MATERIALIZED VIEW CONCURRENTLY.';
