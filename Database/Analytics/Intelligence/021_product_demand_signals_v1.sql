-- ==========================================================
-- 021_product_demand_signals_v1.sql
-- Product demand intelligence layer.
--
-- Signal design notes:
-- - horizon: last 180 calendar days
-- - sales_velocity: rolling 7-day unit sum at article/store/day grain
-- - demand_acceleration: current 7-day velocity vs previous 7-day velocity
--   using LAG(rolling_7d, 7) over a gap-free daily calendar
-- - days_since_last_sale: date distance to the latest selling day at or before
--   the signal date
-- - launch_age_days: first selling day, with ProductsDim.Timestamp fallback
-- - store_coverage: count of distinct stores that sold the product in the
--   trailing 30 days ending on the signal date
-- ==========================================================

DO $$
DECLARE
    _actual text[];
    _expect text[] := ARRAY[
        'article_id',
        'store_id',
        'date',
        'sales_velocity',
        'demand_acceleration',
        'days_since_last_sale',
        'launch_age_days',
        'store_coverage',
        'source_rows'
    ];
BEGIN
    SELECT array_agg(c.column_name::text ORDER BY c.ordinal_position)
      INTO _actual
      FROM information_schema.columns c
     WHERE c.table_schema = 'analytics_intel'
       AND c.table_name = 'vw_product_demand_signals_v1';

    IF _actual IS NOT NULL AND _actual IS DISTINCT FROM _expect THEN
        RAISE NOTICE '021: vw_product_demand_signals_v1 column structure changed - dropping cache + view';
        DROP MATERIALIZED VIEW IF EXISTS analytics_intel.mv_product_demand_signals_v1_cache;
        DROP VIEW IF EXISTS analytics_intel.vw_product_demand_signals_v1 CASCADE;
    END IF;
END $$;

CREATE OR REPLACE VIEW analytics_intel.vw_product_demand_signals_v1 AS
WITH settings AS (
    SELECT
        CURRENT_DATE::date AS as_of_date,
        180::int AS horizon_days,
        7::int AS velocity_window_days,
        30::int AS coverage_window_days
),
latest_product_dim AS (
    SELECT DISTINCT ON (pd."ProductId")
        pd."ProductId" AS article_id,
        pd."Timestamp"::date AS product_seen_date
    FROM "ProductsDim" pd
    ORDER BY pd."ProductId", pd."Timestamp" DESC
),
daily_sales AS (
    SELECT
        slf."ProductId" AS article_id,
        sf."StoreId" AS store_id,
        DATE_TRUNC('day', sf."SaleTimestampUtc")::date AS sale_date,
        SUM(slf."Qty")::numeric(18,4) AS units_sold,
        COUNT(*)::integer AS source_rows
    FROM "SalesLineFacts" slf
    JOIN "SalesFacts" sf
      ON sf."Id" = slf."SaleId"
    JOIN settings s
      ON sf."SaleTimestampUtc"::date >= s.as_of_date - (s.horizon_days + s.coverage_window_days)
    GROUP BY
        slf."ProductId",
        sf."StoreId",
        DATE_TRUNC('day', sf."SaleTimestampUtc")::date
),
observed_pairs AS (
    SELECT DISTINCT
        ds.article_id,
        ds.store_id
    FROM daily_sales ds
),
calendar AS (
    SELECT generate_series(
        (SELECT as_of_date - horizon_days FROM settings),
        (SELECT as_of_date FROM settings),
        INTERVAL '1 day'
    )::date AS signal_date
),
product_store_calendar AS (
    SELECT
        op.article_id,
        op.store_id,
        c.signal_date
    FROM observed_pairs op
    CROSS JOIN calendar c
),
daily_store_signals AS (
    SELECT
        psc.article_id,
        psc.store_id,
        psc.signal_date,
        COALESCE(ds.units_sold, 0)::numeric(18,4) AS units_sold,
        COALESCE(ds.source_rows, 0) AS source_rows
    FROM product_store_calendar psc
    LEFT JOIN daily_sales ds
      ON ds.article_id = psc.article_id
     AND ds.store_id = psc.store_id
     AND ds.sale_date = psc.signal_date
),
rolling_signals AS (
    SELECT
        dss.article_id,
        dss.store_id,
        dss.signal_date,
        dss.units_sold,
        dss.source_rows,
        SUM(dss.units_sold) OVER (
            PARTITION BY dss.article_id, dss.store_id
            ORDER BY dss.signal_date
            ROWS BETWEEN 6 PRECEDING AND CURRENT ROW
        )::numeric(18,4) AS rolling_units_7d,
        SUM(dss.source_rows) OVER (
            PARTITION BY dss.article_id, dss.store_id
            ORDER BY dss.signal_date
            ROWS BETWEEN 13 PRECEDING AND CURRENT ROW
        )::integer AS source_rows_14d,
        MAX(
            CASE
                WHEN dss.units_sold > 0 THEN dss.signal_date
                ELSE NULL
            END
        ) OVER (
            PARTITION BY dss.article_id, dss.store_id
            ORDER BY dss.signal_date
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS last_sale_date
    FROM daily_store_signals dss
),
demand_signals AS (
    SELECT
        rs.article_id,
        rs.store_id,
        rs.signal_date,
        rs.rolling_units_7d,
        LAG(rs.rolling_units_7d, 7) OVER (
            PARTITION BY rs.article_id, rs.store_id
            ORDER BY rs.signal_date
        )::numeric(18,4) AS prior_rolling_units_7d,
        rs.last_sale_date,
        rs.source_rows_14d
    FROM rolling_signals rs
),
product_launch_dates AS (
    SELECT
        ds.article_id,
        COALESCE(
            MIN(sales.sale_date),
            MAX(lpd.product_seen_date),
            (SELECT as_of_date FROM settings)
        ) AS launch_date
    FROM (
        SELECT DISTINCT article_id
        FROM observed_pairs
    ) ds
    LEFT JOIN daily_sales sales
      ON sales.article_id = ds.article_id
    LEFT JOIN latest_product_dim lpd
      ON lpd.article_id = ds.article_id
    GROUP BY ds.article_id
),
product_day_coverage AS (
    SELECT
        pd.article_id,
        pd.signal_date,
        COUNT(DISTINCT ds.store_id) FILTER (
            WHERE ds.sale_date > pd.signal_date - INTERVAL '30 days'
              AND ds.sale_date <= pd.signal_date
        )::integer AS store_coverage
    FROM (
        SELECT DISTINCT
            psc.article_id,
            psc.signal_date
        FROM product_store_calendar psc
    ) pd
    LEFT JOIN daily_sales ds
      ON ds.article_id = pd.article_id
    GROUP BY
        pd.article_id,
        pd.signal_date
)
SELECT
    ds.article_id,
    ds.store_id,
    ds.signal_date AS date,
    ROUND(COALESCE(ds.rolling_units_7d, 0), 4) AS sales_velocity,
    ROUND(
        CASE
            WHEN COALESCE(ds.prior_rolling_units_7d, 0) = 0
             AND COALESCE(ds.rolling_units_7d, 0) > 0
            THEN 1::numeric
            WHEN COALESCE(ds.prior_rolling_units_7d, 0) = 0
            THEN 0::numeric
            ELSE (
                COALESCE(ds.rolling_units_7d, 0) - COALESCE(ds.prior_rolling_units_7d, 0)
            ) / NULLIF(ds.prior_rolling_units_7d, 0)
        END,
        4
    ) AS demand_acceleration,
    CASE
        WHEN ds.last_sale_date IS NULL THEN NULL
        ELSE (ds.signal_date - ds.last_sale_date)
    END AS days_since_last_sale,
    GREATEST(ds.signal_date - pld.launch_date, 0) AS launch_age_days,
    COALESCE(pdc.store_coverage, 0) AS store_coverage,
    COALESCE(ds.source_rows_14d, 0) AS source_rows
FROM demand_signals ds
JOIN product_launch_dates pld
  ON pld.article_id = ds.article_id
LEFT JOIN product_day_coverage pdc
  ON pdc.article_id = ds.article_id
 AND pdc.signal_date = ds.signal_date;

COMMENT ON VIEW analytics_intel.vw_product_demand_signals_v1 IS
'Versioned product demand signal view at article/store/day grain. Uses a 7-day rolling velocity window, a lagged 7-day comparison for acceleration, first-sale launch proxying, and 30-day store coverage.';

COMMENT ON COLUMN analytics_intel.vw_product_demand_signals_v1.sales_velocity IS
'Rolling 7-day sum of sold units. Window size is intentionally fixed in SQL for deterministic dashboard behavior.';

COMMENT ON COLUMN analytics_intel.vw_product_demand_signals_v1.demand_acceleration IS
'Relative change between the current 7-day velocity and the prior 7-day velocity. 1.0 means velocity doubled; 0 means flat; negative values indicate deceleration.';

COMMENT ON COLUMN analytics_intel.vw_product_demand_signals_v1.days_since_last_sale IS
'Calendar days between the signal date and the latest selling day at or before that signal date.';

COMMENT ON COLUMN analytics_intel.vw_product_demand_signals_v1.launch_age_days IS
'Age proxy in days since first observed sale, with latest ProductsDim.Timestamp fallback when explicit launch metadata is unavailable.';

COMMENT ON COLUMN analytics_intel.vw_product_demand_signals_v1.store_coverage IS
'Distinct store count with at least one sale for the product in the trailing 30-day window ending on the signal date.';

CREATE MATERIALIZED VIEW IF NOT EXISTS analytics_intel.mv_product_demand_signals_v1_cache AS
SELECT *
FROM analytics_intel.vw_product_demand_signals_v1;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_product_demand_signals_v1_cache_pk
    ON analytics_intel.mv_product_demand_signals_v1_cache (article_id, store_id, date);

CREATE INDEX IF NOT EXISTS idx_mv_product_demand_signals_v1_cache_date
    ON analytics_intel.mv_product_demand_signals_v1_cache (date DESC);

COMMENT ON MATERIALIZED VIEW analytics_intel.mv_product_demand_signals_v1_cache IS
'Materialized cache for analytics_intel.vw_product_demand_signals_v1. Unique key supports REFRESH MATERIALIZED VIEW CONCURRENTLY.';
