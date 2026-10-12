-- ==========================================================
-- 024_trend_momentum_v1.sql
-- Trend momentum intelligence layer.
--
-- Signal design notes:
-- - external_trend_score prefers recent TrendHistory and falls back to
--   GlobalTrendScores.FinalGlobalScore; absent external evidence stays NULL
-- - external_trend_source and external_trend_observed_at identify the selected
--   external row without assigning a timezone to the legacy UpdatedAt column
-- - GlobalTrendScores rows updated after the next UTC as-of day are ineligible
-- - local_sales_acceleration uses a 28-day regression slope on daily units sold
-- - trend_entropy measures how evenly gross positive sold units are distributed
--   across days in the trailing 28-day window using normalized Shannon entropy
--   (signed net units remain the basis for local_sales_acceleration)
--
-- Future pgvector integration:
-- - the analytics database already enables pgvector via 020
-- - a future v2 layer can blend ProductImage / EuTrend embeddings to produce
--   semantic trend-alignment signals without changing this v1 contract
-- ==========================================================

DO $$
DECLARE
    _actual text[];
    _expect text[] := ARRAY[
        'article_id',
        'signal_date',
        'external_trend_score',
        'external_trend_source',
        'external_trend_observed_at',
        'local_sales_acceleration',
        'trend_entropy'
    ];
BEGIN
    SELECT array_agg(c.column_name::text ORDER BY c.ordinal_position)
      INTO _actual
      FROM information_schema.columns c
     WHERE c.table_schema = 'analytics_intel'
       AND c.table_name = 'vw_trend_momentum_v1';

    IF _actual IS NOT NULL AND _actual IS DISTINCT FROM _expect THEN
        RAISE NOTICE '024: vw_trend_momentum_v1 column structure changed - dropping cache + view';
        DROP MATERIALIZED VIEW IF EXISTS analytics_intel.mv_trend_momentum_v1_cache;
        DROP VIEW IF EXISTS analytics_intel.vw_trend_momentum_v1 CASCADE;
    END IF;
END $$;

CREATE OR REPLACE VIEW analytics_intel.vw_trend_momentum_v1 AS
WITH settings AS (
    SELECT
        (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date AS as_of_date,
        28::int AS trend_window_days
),
latest_trend_history AS (
    SELECT
        th."LocalProductId" AS article_id,
        th."FinalGlobalScore"::numeric(18,4) AS final_global_score,
        'trend_history'::text AS external_trend_source,
        TO_CHAR(th."Date"::date, 'YYYY-MM-DD') AS external_trend_observed_at,
        ROW_NUMBER() OVER (
            PARTITION BY th."LocalProductId"
            ORDER BY th."Date" DESC, th."CreatedAt" DESC
        ) AS rn
    FROM "TrendHistory" th
    JOIN settings s
      ON th."Date" >= s.as_of_date - s.trend_window_days
     AND th."Date" < ((s.as_of_date + 1)::timestamp AT TIME ZONE 'UTC')
),
latest_global_scores AS (
    SELECT
        gts."LocalProductId" AS article_id,
        gts."FinalGlobalScore"::numeric(18,4) AS final_global_score,
        'global_trend_scores'::text AS external_trend_source,
        TO_CHAR(gts."UpdatedAt", 'YYYY-MM-DD"T"HH24:MI:SS') AS external_trend_observed_at
    FROM "GlobalTrendScores" gts
    JOIN settings s
      ON gts."UpdatedAt" < ((s.as_of_date + 1)::timestamp)
),
daily_sales AS (
    SELECT
        slf."ProductId" AS article_id,
        (sf."SaleTimestampUtc" AT TIME ZONE 'UTC')::date AS sale_date,
        SUM(slf."Qty")::numeric(18,4) AS units_sold,
        SUM(GREATEST(slf."Qty", 0))::numeric(18,4) AS gross_units_sold
    FROM "SalesLineFacts" slf
    JOIN "SalesFacts" sf
      ON sf."SaleId" = slf."SaleId"
    JOIN settings s
      ON (sf."SaleTimestampUtc" AT TIME ZONE 'UTC')::date >= s.as_of_date - s.trend_window_days
     AND sf."SaleTimestampUtc" < ((s.as_of_date + 1)::timestamp AT TIME ZONE 'UTC')
    WHERE UPPER(BTRIM(COALESCE(sf."BrojRacuna", ''))) NOT IN ('DUG', 'KOREKCIJA')
    GROUP BY
        slf."ProductId",
        (sf."SaleTimestampUtc" AT TIME ZONE 'UTC')::date
),
observed_products AS (
    SELECT article_id FROM latest_global_scores
    UNION
    SELECT article_id FROM latest_trend_history
    UNION
    SELECT article_id FROM daily_sales
),
calendar AS (
    SELECT
        gs::date AS signal_date,
        ROW_NUMBER() OVER (ORDER BY gs) - 1 AS day_number
    FROM generate_series(
        (SELECT as_of_date - trend_window_days FROM settings),
        (SELECT as_of_date FROM settings),
        INTERVAL '1 day'
    ) AS gs
),
product_calendar AS (
    SELECT
        op.article_id,
        c.signal_date,
        c.day_number
    FROM observed_products op
    CROSS JOIN calendar c
),
daily_product_sales AS (
    SELECT
        pc.article_id,
        pc.signal_date,
        pc.day_number,
        COALESCE(ds.units_sold, 0)::numeric(18,4) AS units_sold,
        COALESCE(ds.gross_units_sold, 0)::numeric(18,4) AS gross_units_sold
    FROM product_calendar pc
    LEFT JOIN daily_sales ds
      ON ds.article_id = pc.article_id
     AND ds.sale_date = pc.signal_date
),
sales_distribution AS (
    SELECT
        dps.article_id,
        SUM(dps.gross_units_sold)::numeric(18,4) AS gross_units_28d,
        COUNT(*) FILTER (WHERE dps.gross_units_sold > 0)::integer AS active_gross_sales_days_28d,
        REGR_SLOPE(dps.units_sold::double precision, dps.day_number::double precision)::numeric(18,6) AS sales_slope_28d
    FROM daily_product_sales dps
    GROUP BY dps.article_id
),
entropy_components AS (
    SELECT
        dps.article_id,
        CASE
            WHEN dps.gross_units_sold <= 0 THEN 0::numeric
            ELSE dps.gross_units_sold / NULLIF(sd.gross_units_28d, 0)
        END AS gross_sales_share,
        sd.gross_units_28d,
        sd.active_gross_sales_days_28d,
        sd.sales_slope_28d
    FROM daily_product_sales dps
    JOIN sales_distribution sd
      ON sd.article_id = dps.article_id
),
trend_entropy AS (
    SELECT
        ec.article_id,
        ec.sales_slope_28d,
        CASE
            WHEN MAX(ec.gross_units_28d) <= 0 THEN NULL
            WHEN MAX(ec.active_gross_sales_days_28d) <= 1 THEN 0::numeric
            ELSE ROUND(
                (
                    -SUM(
                        CASE
                            WHEN ec.gross_sales_share <= 0 THEN 0::numeric
                            ELSE ec.gross_sales_share * LN(ec.gross_sales_share)
                        END
                    )
                ) / NULLIF(LN(MAX(ec.active_gross_sales_days_28d)::numeric), 0),
                6
            )
        END AS trend_entropy
    FROM entropy_components ec
    GROUP BY
        ec.article_id,
        ec.sales_slope_28d
)
SELECT
    op.article_id,
    (SELECT as_of_date FROM settings) AS signal_date,
    ROUND(COALESCE(lth.final_global_score, lgs.final_global_score), 4) AS external_trend_score,
    COALESCE(lth.external_trend_source, lgs.external_trend_source) AS external_trend_source,
    COALESCE(lth.external_trend_observed_at, lgs.external_trend_observed_at) AS external_trend_observed_at,
    ROUND(COALESCE(te.sales_slope_28d, 0), 6) AS local_sales_acceleration,
    ROUND(te.trend_entropy, 6) AS trend_entropy
FROM observed_products op
LEFT JOIN latest_global_scores lgs
  ON lgs.article_id = op.article_id
LEFT JOIN latest_trend_history lth
  ON lth.article_id = op.article_id
 AND lth.rn = 1
LEFT JOIN trend_entropy te
  ON te.article_id = op.article_id;

COMMENT ON VIEW analytics_intel.vw_trend_momentum_v1 IS
'Versioned trend momentum view combining external trend scores with local demand slope and entropy. Sales use UTC calendar dates and the global all-origin SalesFacts population, excluding trimmed case-insensitive DUG/KOREKCIJA receipts; this is not an Access-only history. Local acceleration is a 28-day regression slope over daily sold units; trend_entropy is normalized Shannon entropy over the same window.';

COMMENT ON COLUMN analytics_intel.vw_trend_momentum_v1.external_trend_score IS
'Latest known external trend score, preferring TrendHistory when a recent row exists and falling back to GlobalTrendScores. NULL means no eligible external row; zero is a measured score.';

COMMENT ON COLUMN analytics_intel.vw_trend_momentum_v1.external_trend_source IS
'Source table for the selected external trend score; NULL means no eligible external row.';

COMMENT ON COLUMN analytics_intel.vw_trend_momentum_v1.external_trend_observed_at IS
'Source date for TrendHistory or the recorded UpdatedAt value for GlobalTrendScores. GlobalTrendScores.UpdatedAt is timezone-naive and is returned as stored.';

COMMENT ON COLUMN analytics_intel.vw_trend_momentum_v1.local_sales_acceleration IS
'Recent sales slope from REGR_SLOPE over the trailing 28-day daily sales curve. Positive values imply strengthening local demand.';

COMMENT ON COLUMN analytics_intel.vw_trend_momentum_v1.trend_entropy IS
'Normalized Shannon entropy of trailing 28-day gross positive sold-unit shares. Signed returns remain in local_sales_acceleration but are not probability mass; NULL means no positive sold-unit evidence, while a single active sales day is a measured zero.';

CREATE MATERIALIZED VIEW IF NOT EXISTS analytics_intel.mv_trend_momentum_v1_cache AS
SELECT *
FROM analytics_intel.vw_trend_momentum_v1;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_trend_momentum_v1_cache_pk
    ON analytics_intel.mv_trend_momentum_v1_cache (article_id, signal_date);

CREATE INDEX IF NOT EXISTS idx_mv_trend_momentum_v1_cache_signal_date
    ON analytics_intel.mv_trend_momentum_v1_cache (signal_date DESC);

COMMENT ON MATERIALIZED VIEW analytics_intel.mv_trend_momentum_v1_cache IS
'Materialized cache for analytics_intel.vw_trend_momentum_v1. Unique key supports REFRESH MATERIALIZED VIEW CONCURRENTLY.';

INSERT INTO analytics_intel.cache_refresh_state (
    cache_name,
    sql_file_path,
    build_state,
    last_refreshed_at_utc,
    metadata_version,
    last_refresh_attempt_at_utc,
    last_refresh_status,
    last_successful_refresh_at_utc,
    last_successful_as_of_date_utc,
    last_successful_generation_id,
    last_successful_refresh_source,
    updated_at_utc
)
VALUES (
    'analytics_intel.mv_trend_momentum_v1_cache',
    'Database/Analytics/Intelligence/024_trend_momentum_v1.sql',
    CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN 'ready' ELSE 'pending' END,
    CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN clock_timestamp() ELSE NULL END,
    1,
    CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN clock_timestamp() ELSE NULL END,
    CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN 'succeeded' ELSE 'pending' END,
    CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN clock_timestamp() ELSE NULL END,
    CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN COALESCE(
        (SELECT MAX(signal_date)::date FROM analytics_intel.mv_trend_momentum_v1_cache),
        (CURRENT_TIMESTAMP AT TIME ZONE 'UTC')::date) ELSE NULL END,
    CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN gen_random_uuid() ELSE NULL END,
    CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN 'sql_bootstrap' ELSE NULL END,
    clock_timestamp()
)
ON CONFLICT (cache_name) DO UPDATE
SET sql_file_path = EXCLUDED.sql_file_path,
    build_state = CASE WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN 'ready' ELSE 'pending' END,
    last_refreshed_at_utc = CASE
        WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN clock_timestamp()
        ELSE analytics_intel.cache_refresh_state.last_refreshed_at_utc
    END,
    metadata_version = 1,
    last_refresh_attempt_at_utc = CASE
        WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN clock_timestamp()
        ELSE analytics_intel.cache_refresh_state.last_refresh_attempt_at_utc
    END,
    last_refresh_status = CASE
        WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN 'succeeded'
        ELSE analytics_intel.cache_refresh_state.last_refresh_status
    END,
    last_successful_refresh_at_utc = CASE
        WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN EXCLUDED.last_successful_refresh_at_utc
        ELSE analytics_intel.cache_refresh_state.last_successful_refresh_at_utc
    END,
    last_successful_as_of_date_utc = CASE
        WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN EXCLUDED.last_successful_as_of_date_utc
        ELSE analytics_intel.cache_refresh_state.last_successful_as_of_date_utc
    END,
    last_successful_generation_id = CASE
        WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN EXCLUDED.last_successful_generation_id
        ELSE analytics_intel.cache_refresh_state.last_successful_generation_id
    END,
    last_successful_refresh_source = CASE
        WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN EXCLUDED.last_successful_refresh_source
        ELSE analytics_intel.cache_refresh_state.last_successful_refresh_source
    END,
    last_refresh_failure_code = CASE
        WHEN to_regclass('"__StartupSqlScriptHistory"') IS NULL THEN NULL
        ELSE analytics_intel.cache_refresh_state.last_refresh_failure_code
    END,
    failure_code = NULL,
    updated_at_utc = clock_timestamp();
