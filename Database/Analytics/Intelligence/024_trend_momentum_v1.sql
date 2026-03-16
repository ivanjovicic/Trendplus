-- ==========================================================
-- 024_trend_momentum_v1.sql
-- Trend momentum intelligence layer.
--
-- Signal design notes:
-- - external_trend_score prefers the latest TrendHistory score and falls back
--   to GlobalTrendScores.FinalGlobalScore
-- - local_sales_acceleration uses a 28-day regression slope on daily units sold
-- - trend_entropy measures how evenly demand is distributed across days in the
--   trailing 28-day window using normalized Shannon entropy
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
        CURRENT_DATE::date AS as_of_date,
        28::int AS trend_window_days
),
latest_trend_history AS (
    SELECT
        th."LocalProductId" AS article_id,
        th."FinalGlobalScore"::numeric(18,4) AS final_global_score,
        ROW_NUMBER() OVER (
            PARTITION BY th."LocalProductId"
            ORDER BY th."Date" DESC, th."CreatedAt" DESC
        ) AS rn
    FROM "TrendHistory" th
    JOIN settings s
      ON th."Date" >= s.as_of_date - s.trend_window_days
),
latest_global_scores AS (
    SELECT
        gts."LocalProductId" AS article_id,
        gts."FinalGlobalScore"::numeric(18,4) AS final_global_score
    FROM "GlobalTrendScores" gts
),
daily_sales AS (
    SELECT
        slf."ProductId" AS article_id,
        DATE_TRUNC('day', sf."SaleTimestampUtc")::date AS sale_date,
        SUM(slf."Qty")::numeric(18,4) AS units_sold
    FROM "SalesLineFacts" slf
    JOIN "SalesFacts" sf
      ON sf."Id" = slf."SaleId"
    JOIN settings s
      ON sf."SaleTimestampUtc"::date >= s.as_of_date - s.trend_window_days
    GROUP BY
        slf."ProductId",
        DATE_TRUNC('day', sf."SaleTimestampUtc")::date
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
        COALESCE(ds.units_sold, 0)::numeric(18,4) AS units_sold
    FROM product_calendar pc
    LEFT JOIN daily_sales ds
      ON ds.article_id = pc.article_id
     AND ds.sale_date = pc.signal_date
),
sales_distribution AS (
    SELECT
        dps.article_id,
        SUM(dps.units_sold)::numeric(18,4) AS total_units_28d,
        COUNT(*) FILTER (WHERE dps.units_sold > 0)::integer AS active_days_28d,
        REGR_SLOPE(dps.units_sold::double precision, dps.day_number::double precision)::numeric(18,6) AS sales_slope_28d
    FROM daily_product_sales dps
    GROUP BY dps.article_id
),
entropy_components AS (
    SELECT
        dps.article_id,
        CASE
            WHEN sd.total_units_28d <= 0 THEN 0::numeric
            WHEN dps.units_sold <= 0 THEN 0::numeric
            ELSE (dps.units_sold / NULLIF(sd.total_units_28d, 0))
        END AS sales_share,
        sd.active_days_28d,
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
            WHEN MAX(ec.active_days_28d) <= 1 THEN 0::numeric
            ELSE ROUND(
                (
                    -SUM(
                        CASE
                            WHEN ec.sales_share <= 0 THEN 0::numeric
                            ELSE ec.sales_share * LN(ec.sales_share)
                        END
                    )
                ) / NULLIF(LN(MAX(ec.active_days_28d)::numeric), 0),
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
    ROUND(
        COALESCE(lth.final_global_score, lgs.final_global_score, 0),
        4
    ) AS external_trend_score,
    ROUND(COALESCE(te.sales_slope_28d, 0), 6) AS local_sales_acceleration,
    ROUND(COALESCE(te.trend_entropy, 0), 6) AS trend_entropy
FROM observed_products op
LEFT JOIN latest_global_scores lgs
  ON lgs.article_id = op.article_id
LEFT JOIN latest_trend_history lth
  ON lth.article_id = op.article_id
 AND lth.rn = 1
LEFT JOIN trend_entropy te
  ON te.article_id = op.article_id;

COMMENT ON VIEW analytics_intel.vw_trend_momentum_v1 IS
'Versioned trend momentum view combining external trend scores with local demand slope and entropy. Local acceleration is a 28-day regression slope over daily sold units; trend_entropy is normalized Shannon entropy over the same window.';

COMMENT ON COLUMN analytics_intel.vw_trend_momentum_v1.external_trend_score IS
'Latest external trend signal for the product, preferring TrendHistory when recent history exists and falling back to GlobalTrendScores.';

COMMENT ON COLUMN analytics_intel.vw_trend_momentum_v1.local_sales_acceleration IS
'Recent sales slope from REGR_SLOPE over the trailing 28-day daily sales curve. Positive values imply strengthening local demand.';

COMMENT ON COLUMN analytics_intel.vw_trend_momentum_v1.trend_entropy IS
'Normalized Shannon entropy of trailing 28-day daily sales shares. Higher values mean demand is broadly distributed; lower values indicate spiky or event-driven demand.';

CREATE MATERIALIZED VIEW IF NOT EXISTS analytics_intel.mv_trend_momentum_v1_cache AS
SELECT *
FROM analytics_intel.vw_trend_momentum_v1;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_trend_momentum_v1_cache_pk
    ON analytics_intel.mv_trend_momentum_v1_cache (article_id, signal_date);

CREATE INDEX IF NOT EXISTS idx_mv_trend_momentum_v1_cache_signal_date
    ON analytics_intel.mv_trend_momentum_v1_cache (signal_date DESC);

COMMENT ON MATERIALIZED VIEW analytics_intel.mv_trend_momentum_v1_cache IS
'Materialized cache for analytics_intel.vw_trend_momentum_v1. Unique key supports REFRESH MATERIALIZED VIEW CONCURRENTLY.';
