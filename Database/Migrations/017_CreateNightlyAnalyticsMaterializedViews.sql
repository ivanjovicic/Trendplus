-- ==========================================================
-- 017_CreateNightlyAnalyticsMaterializedViews.sql
-- Nightly ETL base layer for fast analytics:
-- - mv_daily_sales_facts (daily per article)
-- - mv_sales_rolling_7d  (daily + MA7)
-- - mv_sales_momentum    (last7 vs prev7 anchored to last_day)
--
-- Wrapper views keep stable names used by the API / BI:
-- - daily_sales_facts, vw_sales_rolling_7d, vw_sales_momentum
-- ==========================================================

-- 0) Base daily facts (materialized).
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_daily_sales_facts AS
SELECT
    ps.id_artikal AS article_id,
    pz.datum_prodaje::date AS day,
    COALESCE(SUM(ps.kolicina), 0)::bigint AS units,
    COALESCE(SUM(ps.kolicina * ps.cena), 0)::numeric AS revenue
FROM prodaja_stavke ps
JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
GROUP BY ps.id_artikal, pz.datum_prodaje::date;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_daily_sales_facts_pk
    ON mv_daily_sales_facts (article_id, day);

CREATE INDEX IF NOT EXISTS idx_mv_daily_sales_facts_day
    ON mv_daily_sales_facts (day);

-- Optional stable alias: create a VIEW if possible, but don't fail if the name is used by a table/MV.
DO $daily_sales_alias$
BEGIN
    BEGIN
        EXECUTE 'CREATE OR REPLACE VIEW daily_sales_facts AS SELECT * FROM mv_daily_sales_facts;';
    EXCEPTION WHEN others THEN
        RAISE NOTICE 'Skipping daily_sales_facts view alias (%).', SQLERRM;
    END;
END
$daily_sales_alias$;


-- 1) Rolling 7-day moving average (materialized).
-- Keep the output schema compatible with vw_sales_rolling_7d:
-- (article_id, day, units, revenue, ma7_revenue, ma7_units)
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_sales_rolling_7d AS
SELECT
    article_id,
    day,
    units,
    revenue,
    AVG(revenue) OVER (
        PARTITION BY article_id
        ORDER BY day
        ROWS BETWEEN 6 PRECEDING AND CURRENT ROW
    )::numeric AS ma7_revenue,
    AVG(units) OVER (
        PARTITION BY article_id
        ORDER BY day
        ROWS BETWEEN 6 PRECEDING AND CURRENT ROW
    )::numeric AS ma7_units
FROM mv_daily_sales_facts;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_sales_rolling_7d_pk
    ON mv_sales_rolling_7d (article_id, day);

CREATE OR REPLACE VIEW vw_sales_rolling_7d AS
SELECT * FROM mv_sales_rolling_7d;


-- 2) Momentum (last 7 vs previous 7 days) (materialized).
-- Keep the output schema compatible with vw_sales_momentum:
-- (article_id, last_day, last7_units, last7_revenue, prev7_units, prev7_revenue, momentum_revenue)
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_sales_momentum AS
WITH last_days AS (
    SELECT
        article_id,
        MAX(day) AS last_day
    FROM mv_daily_sales_facts
    GROUP BY article_id
),
x AS (
    SELECT
        f.article_id,
        ld.last_day,
        f.day,
        f.units,
        f.revenue
    FROM mv_daily_sales_facts f
    JOIN last_days ld ON ld.article_id = f.article_id
)
SELECT
    article_id,
    last_day,
    SUM(units) FILTER (WHERE day > (last_day - INTERVAL '7 days')) AS last7_units,
    SUM(revenue) FILTER (WHERE day > (last_day - INTERVAL '7 days')) AS last7_revenue,
    SUM(units) FILTER (WHERE day <= (last_day - INTERVAL '7 days') AND day > (last_day - INTERVAL '14 days')) AS prev7_units,
    SUM(revenue) FILTER (WHERE day <= (last_day - INTERVAL '7 days') AND day > (last_day - INTERVAL '14 days')) AS prev7_revenue,
    (
        SUM(revenue) FILTER (WHERE day > (last_day - INTERVAL '7 days'))
        -
        SUM(revenue) FILTER (WHERE day <= (last_day - INTERVAL '7 days') AND day > (last_day - INTERVAL '14 days'))
    ) AS momentum_revenue
FROM x
GROUP BY article_id, last_day;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_sales_momentum_pk
    ON mv_sales_momentum (article_id);

CREATE OR REPLACE VIEW vw_sales_momentum AS
SELECT * FROM mv_sales_momentum;


-- NOTE:
-- - These are materialized views. For fresh data, run:
--     REFRESH MATERIALIZED VIEW CONCURRENTLY mv_daily_sales_facts;
--     REFRESH MATERIALIZED VIEW CONCURRENTLY mv_sales_rolling_7d;
--     REFRESH MATERIALIZED VIEW CONCURRENTLY mv_sales_momentum;
-- - Nightly refresh is implemented via app worker (NightlyAnalyticsRefreshWorker).
