-- ==========================================================
-- 022_inventory_risk_signals_v1.sql
-- Inventory risk intelligence layer.
--
-- Assumptions:
-- - canonical observed daily inventory snapshots are published separately in 025
-- - current on-hand comes from the latest ProductsDim.Kolicina
-- - observed SKU/store/day on-hand now lives in analytics_intel.inventory_observed_daily_snapshot (025)
-- - this view remains a reconstructed daily stock proxy, not the canonical observed snapshot
-- - current on-hand used by the proxy comes from the latest ProductsDim.Kolicina
-- - historical stock is reconstructed backwards from:
--     * current on-hand
--     * future sales from SalesLineFacts/SalesFacts
--     * non-sale inventory movements from InventoryMovementFacts
-- - low stock threshold uses GREATEST(MinimalnaKolicina, 5)
-- - dead stock risk uses a 45-day "no sales while stock remains" heuristic
-- ==========================================================

DO $$
DECLARE
    _actual text[];
    _expect text[] := ARRAY[
        'article_id',
        'date',
        'stock_qty',
        'avg_daily_sales_30d',
        'days_of_cover',
        'stock_turn',
        'stockout_days',
        'low_stock_days',
        'dead_stock_risk'
    ];
BEGIN
    SELECT array_agg(c.column_name::text ORDER BY c.ordinal_position)
      INTO _actual
      FROM information_schema.columns c
     WHERE c.table_schema = 'analytics_intel'
       AND c.table_name = 'vw_inventory_risk_signals_v1';

    IF _actual IS NOT NULL AND _actual IS DISTINCT FROM _expect THEN
        RAISE NOTICE '022: vw_inventory_risk_signals_v1 column structure changed - dropping cache + view';
        DROP MATERIALIZED VIEW IF EXISTS analytics_intel.mv_inventory_risk_signals_v1_cache;
        DROP VIEW IF EXISTS analytics_intel.vw_inventory_risk_signals_v1 CASCADE;
    END IF;
END $$;

CREATE OR REPLACE VIEW analytics_intel.vw_inventory_risk_signals_v1 AS
WITH settings AS (
    SELECT
        (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date AS as_of_date,
        120::int AS horizon_days,
        30::int AS sales_window_days,
        45::int AS dead_stock_window_days,
        5::numeric AS minimum_low_stock_threshold
),
latest_product_dim AS (
    SELECT DISTINCT ON (pd."ProductId")
        pd."ProductId" AS article_id,
        COALESCE(pd."Kolicina", 0)::numeric(18,4) AS current_stock_qty,
        GREATEST(
            COALESCE(pd."MinimalnaKolicina", 0)::numeric,
            (SELECT minimum_low_stock_threshold FROM settings)
        )::numeric(18,4) AS low_stock_threshold,
        COALESCE(pd."PurchasePriceRsd", pd."PurchasePrice", 0)::numeric(18,4) AS unit_cost
    FROM "ProductsDim" pd
    ORDER BY pd."ProductId", pd."Timestamp" DESC
),
daily_sales AS (
    SELECT
        slf."ProductId" AS article_id,
        (sf."SaleTimestampUtc" AT TIME ZONE 'UTC')::date AS sale_date,
        SUM(slf."Qty")::numeric(18,4) AS sales_qty
    FROM "SalesLineFacts" slf
    JOIN "SalesFacts" sf
      ON sf."SaleId" = slf."SaleId"
    JOIN settings s
      ON (sf."SaleTimestampUtc" AT TIME ZONE 'UTC')::date >= s.as_of_date - s.horizon_days
    WHERE UPPER(BTRIM(COALESCE(sf."BrojRacuna", ''))) NOT IN ('DUG', 'KOREKCIJA')
    GROUP BY
        slf."ProductId",
        (sf."SaleTimestampUtc" AT TIME ZONE 'UTC')::date
),
daily_non_sale_movements AS (
    SELECT
        imf."ArtikalId" AS article_id,
        DATE_TRUNC('day', imf."Datum")::date AS movement_date,
        SUM(
            CASE
                WHEN imf."TipPromene" = 'Ulaz robe' THEN ABS(COALESCE(imf."Kolicina", 0))
                ELSE 0
            END
        )::numeric(18,4) AS receipts_qty,
        SUM(
            CASE
                WHEN imf."TipPromene" = 'Prenos ulaz' THEN ABS(COALESCE(imf."Kolicina", 0))
                ELSE 0
            END
        )::numeric(18,4) AS transfer_in_qty,
        SUM(
            CASE
                WHEN imf."TipPromene" = 'Prenos izlaz' THEN ABS(COALESCE(imf."Kolicina", 0))
                ELSE 0
            END
        )::numeric(18,4) AS transfer_out_qty,
        SUM(
            CASE
                WHEN imf."TipPromene" = 'Povrat kupca' THEN ABS(COALESCE(imf."Kolicina", 0))
                ELSE 0
            END
        )::numeric(18,4) AS customer_return_qty
    FROM "InventoryMovementFacts" imf
    JOIN settings s
      ON imf."Datum"::date >= s.as_of_date - s.horizon_days
    WHERE imf."ArtikalId" IS NOT NULL
      AND imf."TipPromene" IN ('Ulaz robe', 'Prenos ulaz', 'Prenos izlaz', 'Povrat kupca')
    GROUP BY
        imf."ArtikalId",
        DATE_TRUNC('day', imf."Datum")::date
),
observed_products AS (
    SELECT article_id FROM latest_product_dim
    UNION
    SELECT article_id FROM daily_sales
    UNION
    SELECT article_id FROM daily_non_sale_movements
),
calendar AS (
    SELECT generate_series(
        (SELECT as_of_date - horizon_days FROM settings),
        (SELECT as_of_date FROM settings),
        INTERVAL '1 day'
    )::date AS signal_date
),
product_calendar AS (
    SELECT
        op.article_id,
        c.signal_date
    FROM observed_products op
    CROSS JOIN calendar c
),
daily_stock_inputs AS (
    SELECT
        pc.article_id,
        pc.signal_date,
        COALESCE(lpd.current_stock_qty, 0)::numeric(18,4) AS current_stock_qty,
        (lpd.article_id IS NOT NULL) AS has_current_stock_baseline,
        COALESCE(lpd.low_stock_threshold, (SELECT minimum_low_stock_threshold FROM settings))::numeric(18,4) AS low_stock_threshold,
        COALESCE(lpd.unit_cost, 0)::numeric(18,4) AS unit_cost,
        COALESCE(ds.sales_qty, 0)::numeric(18,4) AS sales_qty,
        COALESCE(dnm.receipts_qty, 0)::numeric(18,4) AS receipts_qty,
        COALESCE(dnm.transfer_in_qty, 0)::numeric(18,4) AS transfer_in_qty,
        COALESCE(dnm.transfer_out_qty, 0)::numeric(18,4) AS transfer_out_qty,
        COALESCE(dnm.customer_return_qty, 0)::numeric(18,4) AS customer_return_qty
    FROM product_calendar pc
    LEFT JOIN latest_product_dim lpd
      ON lpd.article_id = pc.article_id
    LEFT JOIN daily_sales ds
      ON ds.article_id = pc.article_id
     AND ds.sale_date = pc.signal_date
    LEFT JOIN daily_non_sale_movements dnm
      ON dnm.article_id = pc.article_id
     AND dnm.movement_date = pc.signal_date
),
stock_proxy AS (
    SELECT
        dsi.article_id,
        dsi.signal_date,
        dsi.low_stock_threshold,
        dsi.unit_cost,
        dsi.sales_qty,
        CASE
            WHEN NOT dsi.has_current_stock_baseline THEN NULL::numeric(18,4)
            ELSE GREATEST(
                dsi.current_stock_qty
                + COALESCE(SUM(dsi.sales_qty) OVER (
                    PARTITION BY dsi.article_id
                    ORDER BY dsi.signal_date DESC
                    ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                ), 0)
                + COALESCE(SUM(dsi.transfer_out_qty) OVER (
                    PARTITION BY dsi.article_id
                    ORDER BY dsi.signal_date DESC
                    ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                ), 0)
                - COALESCE(SUM(dsi.receipts_qty) OVER (
                    PARTITION BY dsi.article_id
                    ORDER BY dsi.signal_date DESC
                    ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                ), 0)
                - COALESCE(SUM(dsi.transfer_in_qty) OVER (
                    PARTITION BY dsi.article_id
                    ORDER BY dsi.signal_date DESC
                    ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                ), 0)
                - COALESCE(SUM(dsi.customer_return_qty) OVER (
                    PARTITION BY dsi.article_id
                    ORDER BY dsi.signal_date DESC
                    ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                ), 0),
                0
            )::numeric(18,4)
        END AS stock_qty
    FROM daily_stock_inputs dsi
),
risk_rollup AS (
    SELECT
        sp.article_id,
        sp.signal_date,
        sp.stock_qty,
        sp.low_stock_threshold,
        sp.unit_cost,
        sp.sales_qty,
        AVG(sp.sales_qty) OVER (
            PARTITION BY sp.article_id
            ORDER BY sp.signal_date
            ROWS BETWEEN 29 PRECEDING AND CURRENT ROW
        )::numeric(18,6) AS avg_daily_sales_30d,
        SUM(sp.sales_qty * sp.unit_cost) OVER (
            PARTITION BY sp.article_id
            ORDER BY sp.signal_date
            ROWS BETWEEN 29 PRECEDING AND CURRENT ROW
        )::numeric(18,4) AS sales_cost_30d,
        AVG(sp.stock_qty * sp.unit_cost) OVER (
            PARTITION BY sp.article_id
            ORDER BY sp.signal_date
            ROWS BETWEEN 29 PRECEDING AND CURRENT ROW
        )::numeric(18,4) AS avg_inventory_value_30d,
        CASE
            WHEN COUNT(sp.stock_qty) OVER (
                PARTITION BY sp.article_id
                ORDER BY sp.signal_date
                ROWS BETWEEN 29 PRECEDING AND CURRENT ROW
            ) = 0 THEN NULL::integer
            ELSE COUNT(*) FILTER (WHERE sp.stock_qty = 0) OVER (
                PARTITION BY sp.article_id
                ORDER BY sp.signal_date
                ROWS BETWEEN 29 PRECEDING AND CURRENT ROW
            )::integer
        END AS stockout_days_30d,
        CASE
            WHEN COUNT(sp.stock_qty) OVER (
                PARTITION BY sp.article_id
                ORDER BY sp.signal_date
                ROWS BETWEEN 29 PRECEDING AND CURRENT ROW
            ) = 0 THEN NULL::integer
            ELSE COUNT(*) FILTER (
                WHERE sp.stock_qty > 0
                  AND sp.stock_qty <= sp.low_stock_threshold
            ) OVER (
                PARTITION BY sp.article_id
                ORDER BY sp.signal_date
                ROWS BETWEEN 29 PRECEDING AND CURRENT ROW
            )::integer
        END AS low_stock_days_30d,
        SUM(sp.sales_qty) OVER (
            PARTITION BY sp.article_id
            ORDER BY sp.signal_date
            ROWS BETWEEN 44 PRECEDING AND CURRENT ROW
        )::numeric(18,4) AS sales_units_45d
    FROM stock_proxy sp
)
SELECT
    rr.article_id,
    rr.signal_date AS date,
    ROUND(rr.stock_qty, 4) AS stock_qty,
    ROUND(COALESCE(rr.avg_daily_sales_30d, 0), 6) AS avg_daily_sales_30d,
    ROUND(
        CASE
            WHEN COALESCE(rr.avg_daily_sales_30d, 0) <= 0 THEN NULL
            ELSE rr.stock_qty / NULLIF(rr.avg_daily_sales_30d, 0)
        END,
        4
    ) AS days_of_cover,
    ROUND(
        CASE
            WHEN COALESCE(rr.avg_inventory_value_30d, 0) <= 0 THEN NULL
            ELSE rr.sales_cost_30d / NULLIF(rr.avg_inventory_value_30d, 0)
        END,
        4
    ) AS stock_turn,
    rr.stockout_days_30d AS stockout_days,
    rr.low_stock_days_30d AS low_stock_days,
    ROUND(
        CASE
            WHEN rr.stock_qty IS NULL THEN NULL::numeric
            WHEN rr.stock_qty <= 0 THEN 0::numeric
            WHEN COALESCE(rr.sales_units_45d, 0) = 0 THEN 1::numeric
            WHEN COALESCE(rr.avg_daily_sales_30d, 0) = 0 THEN 0.8::numeric
            WHEN rr.stock_qty > rr.low_stock_threshold * 2 THEN 0.5::numeric
            ELSE 0::numeric
        END,
        4
    ) AS dead_stock_risk
FROM risk_rollup rr;

COMMENT ON VIEW analytics_intel.vw_inventory_risk_signals_v1 IS
'Versioned inventory risk signal view built from a reconstructed daily stock proxy using UTC sales dates and the global all-origin SalesFacts population, excluding trimmed case-insensitive DUG/KOREKCIJA receipts. It is not an Access-only history. Canonical observed stock is analytics_intel.inventory_observed_daily_snapshot / vw_inventory_daily_stock_v1. This view must not be treated as observed warehouse history.';

COMMENT ON COLUMN analytics_intel.vw_inventory_risk_signals_v1.stock_qty IS
'Estimated on-hand quantity for the signal date. This is a reconstructed proxy (provenance reconstructed), not an observed daily snapshot; NULL means no current product-dimension stock baseline exists.';

COMMENT ON COLUMN analytics_intel.vw_inventory_risk_signals_v1.avg_daily_sales_30d IS
'Average daily sold units over the trailing 30-day window. This drives both days_of_cover and dead-stock heuristics.';

COMMENT ON COLUMN analytics_intel.vw_inventory_risk_signals_v1.days_of_cover IS
'Estimated stock_qty divided by trailing average daily sales. NULL means there is no recent sales demand or no product-dimension stock baseline to anchor the calculation.';

COMMENT ON COLUMN analytics_intel.vw_inventory_risk_signals_v1.stock_turn IS
'Trailing 30-day sales cost divided by trailing average inventory value. Cost is sourced from ProductsDim PurchasePriceRsd/PurchasePrice fallback; NULL is preserved when there is no inventory baseline.';

COMMENT ON COLUMN analytics_intel.vw_inventory_risk_signals_v1.stockout_days IS
'Trailing stockout-day count. NULL means no product-dimension stock baseline exists in the measurement window.';

COMMENT ON COLUMN analytics_intel.vw_inventory_risk_signals_v1.low_stock_days IS
'Trailing low-stock-day count. NULL means no product-dimension stock baseline exists in the measurement window.';

COMMENT ON COLUMN analytics_intel.vw_inventory_risk_signals_v1.dead_stock_risk IS
'0-1 heuristic risk score. NULL means there is no product-dimension stock baseline. The default threshold assumes dead stock means stock remains while no units sold during the last 45 days.';

CREATE MATERIALIZED VIEW IF NOT EXISTS analytics_intel.mv_inventory_risk_signals_v1_cache AS
SELECT *
FROM analytics_intel.vw_inventory_risk_signals_v1;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_inventory_risk_signals_v1_cache_pk
    ON analytics_intel.mv_inventory_risk_signals_v1_cache (article_id, date);

CREATE INDEX IF NOT EXISTS idx_mv_inventory_risk_signals_v1_cache_date
    ON analytics_intel.mv_inventory_risk_signals_v1_cache (date DESC);

COMMENT ON MATERIALIZED VIEW analytics_intel.mv_inventory_risk_signals_v1_cache IS
'Materialized cache for analytics_intel.vw_inventory_risk_signals_v1. Unique key supports REFRESH MATERIALIZED VIEW CONCURRENTLY.';

INSERT INTO analytics_intel.cache_refresh_state (
    cache_name,
    sql_file_path,
    build_state,
    last_refreshed_at_utc,
    updated_at_utc
)
VALUES (
    'analytics_intel.mv_inventory_risk_signals_v1_cache',
    'Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql',
    CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN 'ready' ELSE 'pending' END,
    CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN clock_timestamp() ELSE NULL END,
    clock_timestamp()
)
ON CONFLICT (cache_name) DO UPDATE
SET sql_file_path = EXCLUDED.sql_file_path,
    build_state = CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN 'ready' ELSE 'pending' END,
    last_refreshed_at_utc = CASE
        WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN clock_timestamp()
        ELSE analytics_intel.cache_refresh_state.last_refreshed_at_utc
    END,
    failure_code = NULL,
    updated_at_utc = clock_timestamp();
