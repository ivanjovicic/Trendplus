-- ==========================================================
-- 025_observed_inventory_daily_snapshot_v1.sql
-- Canonical observed SKU/store/day inventory snapshot foundation.
--
-- Rules:
-- - this table stores observed on-hand only; never reconstructed proxy rows
-- - a missing day has no row; on_hand_qty = 0 is true observed zero
-- - ProductsDim.Kolicina is article-level and not store-grained; capture uses store_id = 0
-- - reconstructed history stays on analytics_intel.vw_inventory_risk_signals_v1
-- - capture does not backfill past dates
-- ==========================================================

CREATE TABLE IF NOT EXISTS analytics_intel.inventory_observed_daily_snapshot (
    article_id integer NOT NULL,
    store_id integer NOT NULL DEFAULT 0,
    snapshot_date date NOT NULL,
    on_hand_qty numeric(18, 4) NOT NULL,
    captured_at_utc timestamptz NOT NULL DEFAULT now(),
    source_system text NOT NULL,
    CONSTRAINT inventory_observed_daily_snapshot_pk
        PRIMARY KEY (article_id, store_id, snapshot_date),
    CONSTRAINT inventory_observed_daily_snapshot_source_chk
        CHECK (char_length(btrim(source_system)) > 0)
);

COMMENT ON TABLE analytics_intel.inventory_observed_daily_snapshot IS
'Durable observed SKU/store/day on-hand. Absence of a row means unobserved, not zero. Reconstructed stock must not be written here.';

COMMENT ON COLUMN analytics_intel.inventory_observed_daily_snapshot.store_id IS
'Store grain when the source provides it. 0 means unspecified/company-level observation (current ProductsDim path).';

COMMENT ON COLUMN analytics_intel.inventory_observed_daily_snapshot.on_hand_qty IS
'Observed on-hand quantity. 0 is true empty stock. NULL is not stored; omit the row instead.';

CREATE INDEX IF NOT EXISTS idx_inventory_observed_daily_snapshot_date
    ON analytics_intel.inventory_observed_daily_snapshot (snapshot_date DESC);

CREATE OR REPLACE FUNCTION analytics_intel.capture_observed_inventory_daily(
    p_as_of date DEFAULT CURRENT_DATE
)
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE
    _row_count integer;
BEGIN
    INSERT INTO analytics_intel.inventory_observed_daily_snapshot (
        article_id,
        store_id,
        snapshot_date,
        on_hand_qty,
        captured_at_utc,
        source_system
    )
    SELECT DISTINCT ON (pd."ProductId")
        pd."ProductId",
        0,
        p_as_of,
        pd."Kolicina"::numeric(18, 4),
        now(),
        'products_dim_current'
    FROM "ProductsDim" pd
    WHERE pd."Kolicina" IS NOT NULL
    ORDER BY pd."ProductId", pd."Timestamp" DESC
    ON CONFLICT (article_id, store_id, snapshot_date)
    DO UPDATE SET
        on_hand_qty = EXCLUDED.on_hand_qty,
        captured_at_utc = EXCLUDED.captured_at_utc,
        source_system = EXCLUDED.source_system;

    GET DIAGNOSTICS _row_count = ROW_COUNT;
    RETURN _row_count;
END;
$$;

COMMENT ON FUNCTION analytics_intel.capture_observed_inventory_daily(date) IS
'Upserts observed on-hand for one calendar day from the latest ProductsDim row per article. Skips NULL Kolicina. Does not fabricate historical days.';

DO $$
DECLARE
    _actual text[];
    _expect text[] := ARRAY[
        'article_id',
        'store_id',
        'date',
        'observed_qty',
        'reconstructed_qty',
        'stock_qty',
        'provenance',
        'captured_at_utc',
        'source_system'
    ];
BEGIN
    SELECT array_agg(c.column_name::text ORDER BY c.ordinal_position)
      INTO _actual
      FROM information_schema.columns c
     WHERE c.table_schema = 'analytics_intel'
       AND c.table_name = 'vw_inventory_daily_stock_v1';

    IF _actual IS NOT NULL AND _actual IS DISTINCT FROM _expect THEN
        RAISE NOTICE '025: vw_inventory_daily_stock_v1 column structure changed - dropping view';
        DROP VIEW IF EXISTS analytics_intel.vw_inventory_daily_stock_v1 CASCADE;
    END IF;
END $$;

CREATE OR REPLACE VIEW analytics_intel.vw_inventory_daily_stock_v1 AS
WITH observed AS (
    SELECT
        s.article_id,
        s.store_id,
        s.snapshot_date AS date,
        s.on_hand_qty AS observed_qty,
        s.captured_at_utc,
        s.source_system
    FROM analytics_intel.inventory_observed_daily_snapshot s
),
reconstructed AS (
    SELECT
        r.article_id,
        0::integer AS store_id,
        r.date,
        r.stock_qty AS reconstructed_qty
    FROM analytics_intel.vw_inventory_risk_signals_v1 r
)
SELECT
    COALESCE(o.article_id, r.article_id) AS article_id,
    COALESCE(o.store_id, r.store_id) AS store_id,
    COALESCE(o.date, r.date) AS date,
    o.observed_qty,
    r.reconstructed_qty,
    CASE
        WHEN o.observed_qty IS NOT NULL THEN o.observed_qty
        ELSE r.reconstructed_qty
    END AS stock_qty,
    CASE
        WHEN o.observed_qty IS NOT NULL
         AND r.reconstructed_qty IS NOT NULL
         AND o.observed_qty IS DISTINCT FROM r.reconstructed_qty THEN 'mixed'
        WHEN o.observed_qty IS NOT NULL THEN 'observed'
        WHEN r.reconstructed_qty IS NOT NULL THEN 'reconstructed'
        ELSE 'missing'
    END AS provenance,
    o.captured_at_utc,
    o.source_system
FROM observed o
FULL OUTER JOIN reconstructed r
  ON r.article_id = o.article_id
 AND r.store_id = o.store_id
 AND r.date = o.date;

COMMENT ON VIEW analytics_intel.vw_inventory_daily_stock_v1 IS
'Canonical daily stock evidence with explicit provenance. observed_qty is snapshot truth; reconstructed_qty is the 022 proxy; stock_qty follows observed first. missing is a provenance label, not a zero quantity.';

COMMENT ON COLUMN analytics_intel.vw_inventory_daily_stock_v1.observed_qty IS
'Observed on-hand from inventory_observed_daily_snapshot. NULL means that grain was not observed.';

COMMENT ON COLUMN analytics_intel.vw_inventory_daily_stock_v1.reconstructed_qty IS
'Reconstructed stock proxy from vw_inventory_risk_signals_v1. Not equivalent to observed stock. 0 here is proxy-estimated, not proven empty.';

COMMENT ON COLUMN analytics_intel.vw_inventory_daily_stock_v1.stock_qty IS
'Convenience quantity: observed_qty when present, otherwise reconstructed_qty. Always read with provenance. NULL only when both sides are missing.';

COMMENT ON COLUMN analytics_intel.vw_inventory_daily_stock_v1.provenance IS
'observed | reconstructed | mixed | missing. mixed means both sides exist and quantities differ.';
