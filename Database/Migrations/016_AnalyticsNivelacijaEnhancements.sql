-- ==========================================================
-- 016_AnalyticsNivelacijaEnhancements.sql
-- Event-specific control/DiD analytics for price events.
--
-- Depends on:
-- - vw_vendor_sales_nivelacija (Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql)
-- - mv_daily_sales_facts       (017_CreateNightlyAnalyticsMaterializedViews.sql)
-- ==========================================================

-- Potential control articles must have known, positive current stock. Eligibility
-- remains event-relative below: historical events outside an event's exclusion
-- window do not permanently disqualify an article.
CREATE OR REPLACE VIEW vw_nivelacija_kontrolna_grupa AS
SELECT
    a."Id" AS article_id,
    a."Naziv" AS article_name,
    a."Kategorija" AS category,
    a."IDDobavljac" AS vendor_id,
    d."Naziv" AS vendor_name,
    COALESCE(NULLIF(a."PLU", ''), a."Id"::text) AS sku,
    a."Kolicina"::numeric AS stock_qty
FROM "Artikli" a
LEFT JOIN "Dobavljaci" d ON d."Id" = a."IDDobavljac"
WHERE a."Kolicina" > 0;

-- One output row per canonical event. Every control is matched only on its
-- bounded pre-window facts and must be event-free through the treatment window.
CREATE OR REPLACE VIEW vw_nivelacija_did AS
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
        t.post_window_complete,
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
     AND c.article_id <> t.article_id
    WHERE EXISTS (
        SELECT 1
        FROM mv_daily_sales_facts pre
        WHERE pre.article_id = c.article_id
          AND pre.day >= t.event_date - INTERVAL '30 days'
          AND pre.day < t.event_date
          AND pre.units > 0
    )
      AND NOT EXISTS (
        SELECT 1
        FROM vw_vendor_sales_nivelacija excluded_event
        WHERE excluded_event.article_id = c.article_id
          AND excluded_event.event_date >= t.event_date - INTERVAL '60 days'
          AND excluded_event.event_date <= t.event_date + INTERVAL '30 days'
    )
),
control_stats AS (
    SELECT
        cc.price_event_id,
        cc.article_id,
        SUM(f.units) FILTER (
            WHERE f.day >= t.event_date - INTERVAL '30 days'
              AND f.day < t.event_date
        )::numeric AS pre_qty,
        SUM(f.revenue) FILTER (
            WHERE f.day >= t.event_date - INTERVAL '30 days'
              AND f.day < t.event_date
        )::numeric AS pre_revenue,
        COALESCE(SUM(f.units) FILTER (
            WHERE f.day >= t.event_date
              AND f.day < t.event_date + INTERVAL '30 days'
        ), 0)::numeric AS post_qty,
        COALESCE(SUM(f.revenue) FILTER (
            WHERE f.day >= t.event_date
              AND f.day < t.event_date + INTERVAL '30 days'
        ), 0)::numeric AS post_revenue
    FROM control_candidates cc
    JOIN test t ON t.price_event_id = cc.price_event_id
    JOIN mv_daily_sales_facts f
      ON f.article_id = cc.article_id
     AND f.day >= t.event_date - INTERVAL '30 days'
     AND f.day < t.event_date + INTERVAL '30 days'
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
    CASE WHEN t.post_window_complete AND c.article_id IS NOT NULL
        THEN ((t.post_revenue - t.pre_revenue) - (c.post_revenue - c.pre_revenue))::numeric
    END AS did_revenue,
    CASE WHEN t.post_window_complete AND c.article_id IS NOT NULL
        THEN ((t.post_qty - t.pre_qty) - (c.post_qty - c.pre_qty))::numeric
    END AS did_qty
FROM test t
LEFT JOIN ranked_control c
       ON c.price_event_id = t.price_event_id
      AND c.rn = 1;
