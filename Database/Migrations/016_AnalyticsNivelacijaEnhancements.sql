-- ==========================================================
-- 016_AnalyticsNivelacijaEnhancements.sql
-- Nivelacija analytics helpers:
-- - Control group (articles without recorded price events)
-- - Difference-in-Differences (DiD) vs best-matching control
--
-- Depends on:
-- - vw_vendor_sales_nivelacija (014_FixNivelacijaViewsFromDnevnik.sql)
-- - mv_daily_sales_facts       (017_CreateNightlyAnalyticsMaterializedViews.sql)
-- ==========================================================

-- 1) Control group: articles without any recorded price event in price_history.
DROP VIEW IF EXISTS vw_nivelacija_kontrolna_grupa CASCADE;
CREATE VIEW vw_nivelacija_kontrolna_grupa AS
SELECT
    a."Id" AS article_id,
    a."Naziv" AS article_name,
    a."Kategorija" AS category,
    a."IDDobavljac" AS vendor_id,
    d."Naziv" AS vendor_name,
    COALESCE(NULLIF(a."PLU", ''), a."Id"::text) AS sku
FROM "Artikli" a
LEFT JOIN "Dobavljaci" d ON d."Id" = a."IDDobavljac"
WHERE NOT EXISTS (
    SELECT 1
    FROM price_history ph
    WHERE ph.article_id = a."Id"
);

-- 2) DiD: test (price-change articles) vs best-matching control (same vendor + category).
DROP VIEW IF EXISTS vw_nivelacija_did CASCADE;
CREATE VIEW vw_nivelacija_did AS
WITH test AS (
    SELECT
        t.price_event_id,
        t.event_date,
        t.vendor_id,
        t.vendor_name,
        t.article_id,
        t.sku,
        t.pre_qty,
        t.post_qty,
        t.pre_revenue,
        t.post_revenue,
        t.change_qty,
        t.change_revenue,
        t.change_percent_qty,
        t.change_percent_revenue,
        t.has_qty_baseline,
        t.qty_baseline_reason,
        t.change_percent_qty_semantic,
        t.has_revenue_baseline,
        t.revenue_baseline_reason,
        t.change_percent_revenue_semantic,
        t.coverage_pre30,
        t.coverage_post30,
        t.is_low_signal,
        a."Kategorija" AS category
    FROM vw_vendor_sales_nivelacija t
    JOIN "Artikli" a ON a."Id" = t.article_id
),
control_candidates AS (
    SELECT
        t.price_event_id,
        c.article_id
    FROM test t
    JOIN vw_nivelacija_kontrolna_grupa c
      ON c.vendor_id = t.vendor_id
     AND c.category = t.category
),
control_stats AS (
    SELECT
        cc.price_event_id,
        cc.article_id,
        SUM(CASE
            WHEN f.day >= t.event_date - INTERVAL '30 days'
             AND f.day <  t.event_date
            THEN f.units ELSE 0 END)::numeric AS pre_qty,
        SUM(CASE
            WHEN f.day >= t.event_date - INTERVAL '30 days'
             AND f.day <  t.event_date
            THEN f.revenue ELSE 0 END)::numeric AS pre_revenue,
        SUM(CASE
            WHEN f.day >= t.event_date
             AND f.day <  t.event_date + INTERVAL '30 days'
            THEN f.units ELSE 0 END)::numeric AS post_qty,
        SUM(CASE
            WHEN f.day >= t.event_date
             AND f.day <  t.event_date + INTERVAL '30 days'
            THEN f.revenue ELSE 0 END)::numeric AS post_revenue
    FROM control_candidates cc
    JOIN test t ON t.price_event_id = cc.price_event_id
    LEFT JOIN mv_daily_sales_facts f
           ON f.article_id = cc.article_id
    GROUP BY cc.price_event_id, cc.article_id
),
ranked_control AS (
    SELECT
        cs.*,
        ROW_NUMBER() OVER (
            PARTITION BY cs.price_event_id
            ORDER BY
                ABS(COALESCE(cs.pre_revenue, 0) - COALESCE(t.pre_revenue, 0)) ASC,
                ABS(COALESCE(cs.pre_qty, 0) - COALESCE(t.pre_qty, 0)) ASC,
                cs.article_id
        ) AS rn
    FROM control_stats cs
    JOIN test t ON t.price_event_id = cs.price_event_id
)
SELECT
    t.price_event_id,
    t.event_date,
    t.vendor_id,
    t.vendor_name,
    t.category,
    t.article_id,
    t.sku,

    t.pre_qty,
    t.post_qty,
    t.pre_revenue,
    t.post_revenue,
    t.change_qty,
    t.change_revenue,
    t.change_percent_qty,
    t.change_percent_revenue,
    t.has_qty_baseline,
    t.qty_baseline_reason,
    t.change_percent_qty_semantic,
    t.has_revenue_baseline,
    t.revenue_baseline_reason,
    t.change_percent_revenue_semantic,
    t.coverage_pre30,
    t.coverage_post30,
    t.is_low_signal,

    c.article_id AS control_article_id,
    c.pre_qty AS control_pre_qty,
    c.post_qty AS control_post_qty,
    c.pre_revenue AS control_pre_revenue,
    c.post_revenue AS control_post_revenue,

    ((t.post_revenue - t.pre_revenue) - (c.post_revenue - c.pre_revenue))::numeric AS did_revenue,
    ((t.post_qty - t.pre_qty) - (c.post_qty - c.pre_qty))::numeric AS did_qty
FROM test t
LEFT JOIN ranked_control c
       ON c.price_event_id = t.price_event_id
      AND c.rn = 1;
