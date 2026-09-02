-- ==========================================================
-- 029_AddSupplierDecisionWindowedViews.sql
-- Adds date-windowed materialized views for supplier decision
-- scorecard so that 30d / 90d / 180d date ranges return metrics
-- computed for those specific periods instead of all-time data.
--
-- Each windowed view is a copy of the full vw_supplier_decision_score
-- chain but with first_markdown_date filtered to a rolling window.
-- Coverage columns added to an existing dependency view stay append-only so
-- a partial prior run can safely re-apply this script.
-- The NightlyAnalyticsRefreshWorker must be configured to REFRESH
-- these MVs alongside the existing all-time cache.
-- ==========================================================

-- SQL_BATCH_BREAK

-- ----------------------------------------------------------
-- Helper: filtered_signals CTE wrapper per window
-- We create one helper VIEW per window so downstream views
-- can simply reference them without duplicating SQL.
-- ----------------------------------------------------------

CREATE OR REPLACE VIEW vw_supplier_fullprice_signals_90d AS
SELECT *
FROM vw_supplier_fullprice_signals
WHERE first_markdown_date >= (CURRENT_DATE - INTERVAL '90 days')::date
  AND first_markdown_date <= CURRENT_DATE;

COMMENT ON VIEW vw_supplier_fullprice_signals_90d IS
'Supplier fullprice signals limited to the rolling 90-day window ending today.';

-- SQL_BATCH_BREAK

CREATE OR REPLACE VIEW vw_supplier_fullprice_signals_180d AS
SELECT *
FROM vw_supplier_fullprice_signals
WHERE first_markdown_date >= (CURRENT_DATE - INTERVAL '180 days')::date
  AND first_markdown_date <= CURRENT_DATE;

COMMENT ON VIEW vw_supplier_fullprice_signals_180d IS
'Supplier fullprice signals limited to the rolling 180-day window ending today.';

-- SQL_BATCH_BREAK

-- ----------------------------------------------------------
-- 90-day windowed markdown dependency view
-- ----------------------------------------------------------
CREATE OR REPLACE VIEW vw_supplier_markdown_dependency_90d AS
WITH signal_base AS (
    SELECT
        fs.supplier_id,
        fs.supplier_name,
        COALESCE(fs.category, 'Uncategorized') AS category,
        fs.article_id,
        fs.first_markdown_date,
        fs.pre_qty_30d,
        fs.pre_revenue_30d,
        fs.pre_margin_30d,
        fs.pre_sellthrough_30d,
        fs.stock_before_markdown,
        fs.stockout_before_markdown_flag,
        fs.had_sales_before_markdown_flag,
        fs.signal_quality_flag,
        COALESCE(vn.post_qty, 0)::numeric AS post_qty_30d,
        COALESCE(vn.post_revenue, 0)::numeric(18,2) AS post_revenue_30d,
        CASE
            WHEN fs.old_price IS NULL OR fs.old_price = 0 THEN 0::numeric
            ELSE ROUND((fs.old_price - fs.new_price) / fs.old_price, 4)
        END AS price_change_pct,
        COALESCE(nd.did_revenue, 0)::numeric(18,2) AS did_revenue,
        COALESCE(nd.did_qty, 0)::numeric AS did_qty,
        COALESCE(a."Kolicina", 0)::numeric AS current_stock,
        COALESCE(
            CASE
                WHEN a."NabavnaCenaDin" > 0 THEN a."NabavnaCenaDin"
                WHEN a."NabavnaCena" > 0 THEN a."NabavnaCena"
                ELSE NULL
            END,
            0
        )::numeric(18,2) AS current_cost
        , (vn.price_event_id IS NOT NULL) AS has_post_signal
        , (nd.price_event_id IS NOT NULL) AS has_did_signal
        , CASE
            WHEN a."NabavnaCenaDin" > 0 THEN TRUE
            WHEN a."NabavnaCena" > 0 THEN TRUE
            ELSE FALSE
          END AS has_cost_signal
    FROM vw_supplier_fullprice_signals_90d fs
    LEFT JOIN LATERAL (
        SELECT v.price_event_id, v.post_qty, v.post_revenue
        FROM vw_vendor_sales_nivelacija v
        WHERE v.article_id = fs.article_id
          AND v.event_date::date = fs.first_markdown_date
          AND v.old_price = fs.old_price
          AND v.new_price = fs.new_price
        ORDER BY v.price_event_id
        LIMIT 1
    ) vn ON TRUE
    LEFT JOIN vw_nivelacija_did nd ON nd.price_event_id = vn.price_event_id
    LEFT JOIN "Artikli" a ON a."Id" = fs.article_id
),
aggregated AS (
    SELECT
        supplier_id,
        supplier_name,
        category,
        COUNT(*)::int AS articles_count,
        COUNT(*) FILTER (WHERE COALESCE(pre_qty_30d, 0) + COALESCE(post_qty_30d, 0) > 0)::int AS active_articles_count,
        AVG(CASE WHEN has_post_signal THEN 1::numeric ELSE 0::numeric END) AS post_signal_coverage,
        AVG(CASE WHEN has_did_signal THEN 1::numeric ELSE 0::numeric END) AS did_signal_coverage,
        AVG(CASE WHEN has_cost_signal THEN 1::numeric ELSE 0::numeric END) AS cost_signal_coverage,
        SUM(COALESCE(pre_revenue_30d, 0))::numeric(18,2) AS revenue_pre_markdown,
        SUM(COALESCE(post_revenue_30d, 0))::numeric(18,2) AS revenue_post_markdown,
        SUM(COALESCE(pre_qty_30d, 0))::numeric AS qty_pre_markdown,
        SUM(COALESCE(post_qty_30d, 0))::numeric AS qty_post_markdown,
        SUM(COALESCE(post_revenue_30d, 0))
            / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0) AS markdown_revenue_share,
        SUM(COALESCE(post_qty_30d, 0))
            / NULLIF(SUM(COALESCE(pre_qty_30d, 0) + COALESCE(post_qty_30d, 0)), 0) AS markdown_unit_share,
        AVG(COALESCE(price_change_pct, 0))::numeric(18,4) AS avg_price_change_pct,
        AVG(COALESCE(did_revenue, 0))::numeric(18,2) AS avg_did_revenue,
        AVG(COALESCE(did_qty, 0))::numeric(18,4) AS avg_did_qty,
        COALESCE(
            SUM(COALESCE(post_revenue_30d, 0)) FILTER (WHERE COALESCE(stockout_before_markdown_flag, FALSE) = FALSE)
            / NULLIF(
                SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0))
                    FILTER (WHERE COALESCE(stockout_before_markdown_flag, FALSE) = FALSE),
                0
            ),
            SUM(COALESCE(post_revenue_30d, 0))
                / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0)
        ) AS oos_adjusted_markdown_dependency,
        COUNT(*) FILTER (WHERE COALESCE(current_stock, 0) > 0 AND COALESCE(post_qty_30d, 0) = 0)::numeric
            / NULLIF(COUNT(*), 0) AS dead_stock_rate,
        SUM(GREATEST(COALESCE(current_stock, 0), 0) * COALESCE(current_cost, 0))::numeric(18,2) AS unsold_stock_value
    FROM signal_base
    GROUP BY GROUPING SETS (
        (supplier_id, supplier_name, category),
        (supplier_id, supplier_name)
    )
)
SELECT
    supplier_id, supplier_name, category,
    articles_count, active_articles_count,
    ROUND(COALESCE(post_signal_coverage, 0), 4) AS post_signal_coverage,
    ROUND(COALESCE(did_signal_coverage, 0), 4) AS did_signal_coverage,
    ROUND(COALESCE(cost_signal_coverage, 0), 4) AS cost_signal_coverage,
    revenue_pre_markdown, revenue_post_markdown,
    qty_pre_markdown, qty_post_markdown,
    ROUND(COALESCE(markdown_revenue_share, 0), 4) AS markdown_revenue_share,
    ROUND(COALESCE(markdown_unit_share, 0), 4) AS markdown_unit_share,
    ROUND(COALESCE(avg_price_change_pct, 0), 4) AS avg_price_change_pct,
    ROUND(COALESCE(avg_did_revenue, 0), 2) AS avg_did_revenue,
    ROUND(COALESCE(avg_did_qty, 0), 4) AS avg_did_qty,
    ROUND(COALESCE(oos_adjusted_markdown_dependency, 0), 4) AS oos_adjusted_markdown_dependency,
    ROUND(COALESCE(dead_stock_rate, 0), 4) AS dead_stock_rate,
    unsold_stock_value
FROM aggregated;

-- SQL_BATCH_BREAK

-- ----------------------------------------------------------
-- 180-day windowed markdown dependency view (same structure)
-- ----------------------------------------------------------
CREATE OR REPLACE VIEW vw_supplier_markdown_dependency_180d AS
WITH signal_base AS (
    SELECT
        fs.supplier_id,
        fs.supplier_name,
        COALESCE(fs.category, 'Uncategorized') AS category,
        fs.article_id,
        fs.first_markdown_date,
        fs.pre_qty_30d,
        fs.pre_revenue_30d,
        fs.pre_margin_30d,
        fs.pre_sellthrough_30d,
        fs.stock_before_markdown,
        fs.stockout_before_markdown_flag,
        fs.had_sales_before_markdown_flag,
        fs.signal_quality_flag,
        COALESCE(vn.post_qty, 0)::numeric AS post_qty_30d,
        COALESCE(vn.post_revenue, 0)::numeric(18,2) AS post_revenue_30d,
        CASE
            WHEN fs.old_price IS NULL OR fs.old_price = 0 THEN 0::numeric
            ELSE ROUND((fs.old_price - fs.new_price) / fs.old_price, 4)
        END AS price_change_pct,
        COALESCE(nd.did_revenue, 0)::numeric(18,2) AS did_revenue,
        COALESCE(nd.did_qty, 0)::numeric AS did_qty,
        COALESCE(a."Kolicina", 0)::numeric AS current_stock,
        COALESCE(
            CASE
                WHEN a."NabavnaCenaDin" > 0 THEN a."NabavnaCenaDin"
                WHEN a."NabavnaCena" > 0 THEN a."NabavnaCena"
                ELSE NULL
            END,
            0
        )::numeric(18,2) AS current_cost
        , (vn.price_event_id IS NOT NULL) AS has_post_signal
        , (nd.price_event_id IS NOT NULL) AS has_did_signal
        , CASE
            WHEN a."NabavnaCenaDin" > 0 THEN TRUE
            WHEN a."NabavnaCena" > 0 THEN TRUE
            ELSE FALSE
          END AS has_cost_signal
    FROM vw_supplier_fullprice_signals_180d fs
    LEFT JOIN LATERAL (
        SELECT v.price_event_id, v.post_qty, v.post_revenue
        FROM vw_vendor_sales_nivelacija v
        WHERE v.article_id = fs.article_id
          AND v.event_date::date = fs.first_markdown_date
          AND v.old_price = fs.old_price
          AND v.new_price = fs.new_price
        ORDER BY v.price_event_id
        LIMIT 1
    ) vn ON TRUE
    LEFT JOIN vw_nivelacija_did nd ON nd.price_event_id = vn.price_event_id
    LEFT JOIN "Artikli" a ON a."Id" = fs.article_id
),
aggregated AS (
    SELECT
        supplier_id, supplier_name, category,
        COUNT(*)::int AS articles_count,
        COUNT(*) FILTER (WHERE COALESCE(pre_qty_30d, 0) + COALESCE(post_qty_30d, 0) > 0)::int AS active_articles_count,
        AVG(CASE WHEN has_post_signal THEN 1::numeric ELSE 0::numeric END) AS post_signal_coverage,
        AVG(CASE WHEN has_did_signal THEN 1::numeric ELSE 0::numeric END) AS did_signal_coverage,
        AVG(CASE WHEN has_cost_signal THEN 1::numeric ELSE 0::numeric END) AS cost_signal_coverage,
        SUM(COALESCE(pre_revenue_30d, 0))::numeric(18,2) AS revenue_pre_markdown,
        SUM(COALESCE(post_revenue_30d, 0))::numeric(18,2) AS revenue_post_markdown,
        SUM(COALESCE(pre_qty_30d, 0))::numeric AS qty_pre_markdown,
        SUM(COALESCE(post_qty_30d, 0))::numeric AS qty_post_markdown,
        SUM(COALESCE(post_revenue_30d, 0))
            / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0) AS markdown_revenue_share,
        SUM(COALESCE(post_qty_30d, 0))
            / NULLIF(SUM(COALESCE(pre_qty_30d, 0) + COALESCE(post_qty_30d, 0)), 0) AS markdown_unit_share,
        AVG(COALESCE(price_change_pct, 0))::numeric(18,4) AS avg_price_change_pct,
        AVG(COALESCE(did_revenue, 0))::numeric(18,2) AS avg_did_revenue,
        AVG(COALESCE(did_qty, 0))::numeric(18,4) AS avg_did_qty,
        COALESCE(
            SUM(COALESCE(post_revenue_30d, 0)) FILTER (WHERE COALESCE(stockout_before_markdown_flag, FALSE) = FALSE)
            / NULLIF(
                SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0))
                    FILTER (WHERE COALESCE(stockout_before_markdown_flag, FALSE) = FALSE),
                0
            ),
            SUM(COALESCE(post_revenue_30d, 0))
                / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0)
        ) AS oos_adjusted_markdown_dependency,
        COUNT(*) FILTER (WHERE COALESCE(current_stock, 0) > 0 AND COALESCE(post_qty_30d, 0) = 0)::numeric
            / NULLIF(COUNT(*), 0) AS dead_stock_rate,
        SUM(GREATEST(COALESCE(current_stock, 0), 0) * COALESCE(current_cost, 0))::numeric(18,2) AS unsold_stock_value
    FROM signal_base
    GROUP BY GROUPING SETS (
        (supplier_id, supplier_name, category),
        (supplier_id, supplier_name)
    )
)
SELECT
    supplier_id, supplier_name, category,
    articles_count, active_articles_count,
    revenue_pre_markdown, revenue_post_markdown,
    qty_pre_markdown, qty_post_markdown,
    ROUND(COALESCE(markdown_revenue_share, 0), 4) AS markdown_revenue_share,
    ROUND(COALESCE(markdown_unit_share, 0), 4) AS markdown_unit_share,
    ROUND(COALESCE(avg_price_change_pct, 0), 4) AS avg_price_change_pct,
    ROUND(COALESCE(avg_did_revenue, 0), 2) AS avg_did_revenue,
    ROUND(COALESCE(avg_did_qty, 0), 4) AS avg_did_qty,
    ROUND(COALESCE(oos_adjusted_markdown_dependency, 0), 4) AS oos_adjusted_markdown_dependency,
    ROUND(COALESCE(dead_stock_rate, 0), 4) AS dead_stock_rate,
    unsold_stock_value,
    ROUND(COALESCE(post_signal_coverage, 0), 4) AS post_signal_coverage,
    ROUND(COALESCE(did_signal_coverage, 0), 4) AS did_signal_coverage,
    ROUND(COALESCE(cost_signal_coverage, 0), 4) AS cost_signal_coverage
FROM aggregated;

-- SQL_BATCH_BREAK

-- ----------------------------------------------------------
-- Shared scoring CTE macro — inlined per window MV
-- (PostgreSQL doesn't support parameterized MVs so we
--  duplicate the scoring formula referencing different base views.)
-- ----------------------------------------------------------

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache_90d AS
WITH supplier_totals AS (
    SELECT
        supplier_id, supplier_name,
        revenue_pre_markdown, revenue_post_markdown,
        qty_pre_markdown, qty_post_markdown,
        markdown_revenue_share, avg_did_revenue, avg_did_qty,
        dead_stock_rate, unsold_stock_value,
        post_signal_coverage,
        did_signal_coverage,
        cost_signal_coverage,
        CASE
            WHEN COALESCE(revenue_pre_markdown, 0) + COALESCE(revenue_post_markdown, 0) = 0 THEN 0::numeric
            ELSE revenue_pre_markdown / NULLIF(revenue_pre_markdown + revenue_post_markdown, 0)
        END AS fullprice_revenue_share
    FROM vw_supplier_markdown_dependency_90d
    WHERE category IS NULL
),
signal_rollup AS (
    SELECT
        supplier_id, supplier_name,
        MIN((first_markdown_date - INTERVAL '30 days')::date) AS period_from,
        MAX((first_markdown_date + INTERVAL '30 days')::date) AS period_to,
        COUNT(*)::int AS article_count,
        COUNT(*) FILTER (WHERE signal_quality_flag = 'high')::numeric / NULLIF(COUNT(*), 0) AS high_signal_share,
        COUNT(*) FILTER (WHERE signal_quality_flag = 'medium')::numeric / NULLIF(COUNT(*), 0) AS medium_signal_share,
        COUNT(*) FILTER (WHERE had_sales_before_markdown_flag)::numeric / NULLIF(COUNT(*), 0) AS had_sales_share,
        COUNT(*) FILTER (WHERE stockout_before_markdown_flag)::numeric / NULLIF(COUNT(*), 0) AS stockout_article_share,
        (COUNT(*) FILTER (WHERE stockout_before_markdown_flag)::numeric / NULLIF(COUNT(*), 0)) >= 0.35 AS stockout_before_markdown_flag,
        SUM(COALESCE(pre_qty_30d, 0))::numeric
            / NULLIF(SUM(COALESCE(pre_qty_30d, 0) + GREATEST(COALESCE(stock_before_markdown, 0), 0)), 0) AS fullprice_sellthrough,
        SUM(COALESCE(pre_margin_30d, 0))::numeric / NULLIF(SUM(COALESCE(pre_revenue_30d, 0)), 0) AS pre_markdown_margin_pct,
        COUNT(*) FILTER (
            WHERE COALESCE(pre_sellthrough_30d, 0) >= 0.45
              AND COALESCE(pre_margin_30d, 0) > 0
              AND COALESCE(had_sales_before_markdown_flag, FALSE)
              AND signal_quality_flag <> 'low'
        )::numeric / NULLIF(COUNT(*), 0) AS repeat_winner_rate
    FROM vw_supplier_fullprice_signals_90d
    GROUP BY supplier_id, supplier_name
),
category_focus AS (
    SELECT
        md.supplier_id,
        MAX((COALESCE(md.revenue_pre_markdown, 0) + COALESCE(md.revenue_post_markdown, 0))
            / NULLIF(COALESCE(st.revenue_pre_markdown, 0) + COALESCE(st.revenue_post_markdown, 0), 0)) * 100 AS category_focus_score
    FROM vw_supplier_markdown_dependency_90d md
    JOIN supplier_totals st ON st.supplier_id = md.supplier_id
    WHERE md.category IS NOT NULL
    GROUP BY md.supplier_id
),
seasonal_category_mix AS (
    SELECT
        md.supplier_id,
        SUM(CASE
            WHEN md.category IS NOT NULL AND (
                md.category ILIKE '%sand%' OR md.category ILIKE '%papuc%' OR md.category ILIKE '%cizm%'
                OR md.category ILIKE '%gleznj%' OR md.category ILIKE '%boot%'
                OR md.category ILIKE '%slipper%' OR md.category ILIKE '%season%'
            ) THEN COALESCE(md.revenue_pre_markdown, 0) + COALESCE(md.revenue_post_markdown, 0)
            ELSE 0
        END)::numeric / NULLIF(SUM(COALESCE(md.revenue_pre_markdown, 0) + COALESCE(md.revenue_post_markdown, 0)), 0) AS seasonal_category_share
    FROM vw_supplier_markdown_dependency_90d md
    WHERE md.category IS NOT NULL
    GROUP BY md.supplier_id
),
sales_in_period AS (
    SELECT sr.supplier_id,
        COALESCE(SUM(CASE WHEN pz.datum_prodaje::date >= sr.period_from AND pz.datum_prodaje::date <= sr.period_to THEN ps.kolicina ELSE 0 END), 0)::numeric AS sold_units_in_period
    FROM signal_rollup sr
    JOIN "Artikli" a ON a."IDDobavljac" = sr.supplier_id
    LEFT JOIN prodaja_stavke ps ON ps.id_artikal = a."Id"
    LEFT JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
    GROUP BY sr.supplier_id
),
returns_in_period AS (
    SELECT sr.supplier_id,
        COALESCE(SUM(CASE WHEN pz.datum_povracaja::date >= sr.period_from AND pz.datum_povracaja::date <= sr.period_to AND COALESCE(pz.status, '') <> 'Odbijen' THEN ps.kolicina ELSE 0 END), 0)::numeric AS returned_units_in_period
    FROM signal_rollup sr
    LEFT JOIN povracaj_zaglavlje pz ON pz.id_dobavljac = sr.supplier_id
    LEFT JOIN povracaj_stavke ps ON ps.id_povracaj = pz.id
    GROUP BY sr.supplier_id
),
decision_inputs AS (
    SELECT
        st.supplier_id, st.supplier_name, sr.period_from, sr.period_to,
        (COALESCE(st.revenue_pre_markdown, 0) + COALESCE(st.revenue_post_markdown, 0))::numeric(18,2) AS revenue,
        (COALESCE(st.qty_pre_markdown, 0) + COALESCE(st.qty_post_markdown, 0))::numeric AS units,
        COALESCE(st.fullprice_revenue_share, 0) AS fullprice_revenue_share,
        COALESCE(sr.fullprice_sellthrough, 0) AS fullprice_sellthrough,
        COALESCE(sr.pre_markdown_margin_pct, 0) AS pre_markdown_margin_pct,
        COALESCE(st.markdown_revenue_share, 0) AS markdown_revenue_share,
        COALESCE(st.dead_stock_rate, 0) AS dead_stock_rate,
        COALESCE(st.unsold_stock_value, 0)::numeric(18,2) AS unsold_stock_value,
        COALESCE(st.post_signal_coverage, 0) AS post_signal_coverage,
        COALESCE(st.did_signal_coverage, 0) AS did_signal_coverage,
        COALESCE(st.cost_signal_coverage, 0) AS cost_signal_coverage,
        CASE
            WHEN COALESCE(si.sold_units_in_period, 0) = 0 THEN NULL
            ELSE COALESCE(ri.returned_units_in_period, 0) / NULLIF(si.sold_units_in_period, 0)
        END AS return_rate,
        CASE
            WHEN COALESCE(si.sold_units_in_period, 0) = 0 THEN 'missing_sales_baseline'
            ELSE NULL
        END AS return_rate_missing_evidence_reason,
        CASE
            WHEN COALESCE(st.post_signal_coverage, 0) < 1
              OR COALESCE(st.did_signal_coverage, 0) < 1
              OR COALESCE(st.cost_signal_coverage, 0) < 1
              OR COALESCE(si.sold_units_in_period, 0) = 0
            THEN 'partial'
            ELSE 'complete'
        END AS evidence_quality_status,
        COALESCE(cf.category_focus_score, 0) AS category_focus_score,
        COALESCE(sr.repeat_winner_rate, 0) AS repeat_winner_rate,
        sr.article_count,
        COALESCE(sr.high_signal_share, 0) AS high_signal_share,
        COALESCE(sr.medium_signal_share, 0) AS medium_signal_share,
        COALESCE(sr.had_sales_share, 0) AS had_sales_share,
        COALESCE(st.avg_did_revenue, 0)::numeric(18,2) AS avg_did_revenue,
        COALESCE(st.avg_did_qty, 0)::numeric(18,4) AS avg_did_qty,
        COALESCE(sr.stockout_article_share, 0) AS stockout_article_share,
        COALESCE(sr.stockout_before_markdown_flag, FALSE) AS stockout_before_markdown_flag,
        COALESCE(scm.seasonal_category_share, 0) AS seasonal_category_share,
        CASE
            WHEN COALESCE(st.post_signal_coverage, 0) < 1
              OR COALESCE(st.did_signal_coverage, 0) < 1
              OR COALESCE(st.cost_signal_coverage, 0) < 1
              OR COALESCE(si.sold_units_in_period, 0) = 0
            THEN 'partial'
            ELSE 'complete'
        END AS evidence_quality_status
    FROM supplier_totals st
    JOIN signal_rollup sr ON sr.supplier_id = st.supplier_id
    LEFT JOIN category_focus cf ON cf.supplier_id = st.supplier_id
    LEFT JOIN seasonal_category_mix scm ON scm.supplier_id = st.supplier_id
    LEFT JOIN sales_in_period si ON si.supplier_id = st.supplier_id
    LEFT JOIN returns_in_period ri ON ri.supplier_id = st.supplier_id
),
distribution_bounds AS (
    SELECT COALESCE(percentile_cont(0.80) WITHIN GROUP (ORDER BY GREATEST(COALESCE(pre_markdown_margin_pct, 0), 0)), 0)::numeric AS margin_p80
    FROM decision_inputs
),
normalized_signals AS (
    SELECT di.*,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.fullprice_sellthrough, 0)), 0)::numeric END AS fullprice_sellthrough_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.fullprice_revenue_share, 0)), 0)::numeric END AS fullprice_revenue_share_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY LEAST(GREATEST(COALESCE(di.pre_markdown_margin_pct, 0), 0), db.margin_p80)), 0)::numeric END AS pre_markdown_margin_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.markdown_revenue_share, 0)), 0)::numeric END AS markdown_revenue_share_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.dead_stock_rate, 0)), 0)::numeric END AS dead_stock_rate_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.unsold_stock_value, 0)), 0)::numeric END AS unsold_stock_value_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.repeat_winner_rate, 0)), 0)::numeric END AS repeat_winner_rate_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.return_rate, 0)), 0)::numeric END AS return_rate_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.category_focus_score, 0)), 0)::numeric END AS category_focus_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.article_count, 0)), 0)::numeric END AS article_count_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.units, 0)), 0)::numeric END AS sales_volume_rank
    FROM decision_inputs di
    CROSS JOIN distribution_bounds db
),
score_components AS (
    SELECT ns.*,
        ROUND((0.60 * ns.fullprice_sellthrough_rank + 0.40 * ns.fullprice_revenue_share_rank) * 100, 2) AS demand_score,
        ROUND(ns.pre_markdown_margin_rank * 100, 2) AS margin_score,
        ROUND(ns.markdown_revenue_share_rank * CASE WHEN ns.seasonal_category_share >= 0.60 THEN 75 WHEN ns.seasonal_category_share >= 0.30 THEN 85 ELSE 100 END, 2) AS markdown_penalty,
        ROUND((0.50 * ns.dead_stock_rate_rank + 0.50 * ns.unsold_stock_value_rank) * 100, 2) AS inventory_penalty,
        ROUND(LEAST(20, GREATEST(-20, (0.50 * ns.repeat_winner_rate_rank - 0.30 * ns.return_rate_rank + 0.20 * ns.category_focus_rank) * 100)), 2) AS supplier_quality_component,
        ROUND(LEAST(1, GREATEST(0, 0.40 * ns.had_sales_share + 0.30 * LEAST(1, GREATEST(0, ns.high_signal_share + 0.50 * ns.medium_signal_share)) + 0.30 * (0.50 * ns.article_count_rank + 0.50 * ns.sales_volume_rank) + 0.10 * COALESCE(ns.post_signal_coverage, 0) + 0.10 * COALESCE(ns.did_signal_coverage, 0) + 0.10 * COALESCE(ns.cost_signal_coverage, 0) - CASE WHEN ns.return_rate IS NULL THEN 0.15 ELSE 0 END)), 4) AS confidence_score
    FROM normalized_signals ns
),
final_score AS (
    SELECT sc.*,
        ROUND(LEAST(100, GREATEST(0, sc.demand_score + sc.margin_score - sc.markdown_penalty - sc.inventory_penalty + sc.supplier_quality_component)), 2) AS supplier_decision_score
    FROM score_components sc
),
recommendation_logic AS (
    SELECT fs.*,
        CASE
            WHEN COALESCE(fs.evidence_quality_status, 'partial') <> 'complete' THEN 'REVIEW_QUALITY'
            WHEN COALESCE(fs.return_rate, 0) > 0.12 THEN 'REVIEW_QUALITY'
            WHEN COALESCE(fs.stockout_before_markdown_flag, FALSE) THEN 'OOS_FALSE_NEGATIVE'
            WHEN fs.supplier_decision_score > 80 THEN 'EXPAND'
            WHEN fs.supplier_decision_score >= 60 THEN 'EXPAND_SELECTIVELY'
            WHEN fs.supplier_decision_score >= 40 THEN 'HOLD'
            WHEN fs.supplier_decision_score >= 25 THEN 'PRICE_NEGOTIATE'
            ELSE 'ASSORTMENT_REDUCE'
        END AS recommendation_code
    FROM final_score fs
)
SELECT
    supplier_id, supplier_name, period_from, period_to, revenue, units,
    ROUND(COALESCE(fullprice_revenue_share, 0), 4) AS fullprice_revenue_share,
    ROUND(COALESCE(fullprice_sellthrough, 0), 4) AS fullprice_sellthrough,
    ROUND(COALESCE(pre_markdown_margin_pct, 0), 4) AS pre_markdown_margin_pct,
    ROUND(COALESCE(markdown_penalty, 0), 2) AS markdown_dependency_score,
    ROUND(COALESCE(inventory_penalty, 0), 2) AS stock_risk_score,
    ROUND(return_rate, 4) AS return_rate,
    ROUND(COALESCE(category_focus_score, 0), 2) AS category_focus_score,
    ROUND(COALESCE(repeat_winner_rate, 0), 4) AS repeat_winner_rate,
    ROUND(COALESCE(post_signal_coverage, 0), 4) AS post_signal_coverage,
    ROUND(COALESCE(did_signal_coverage, 0), 4) AS did_signal_coverage,
    ROUND(COALESCE(cost_signal_coverage, 0), 4) AS cost_signal_coverage,
    evidence_quality_status,
    return_rate_missing_evidence_reason,
    supplier_decision_score AS supplier_quality_index,
    recommendation_code, confidence_score
FROM recommendation_logic;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_supplier_decision_score_cache_90d_pk
    ON mv_supplier_decision_score_cache_90d (supplier_id);

COMMENT ON MATERIALIZED VIEW mv_supplier_decision_score_cache_90d IS
'Supplier decision scorecard computed over the rolling 90-day window. Refreshed nightly.';

-- SQL_BATCH_BREAK

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache_180d AS
WITH supplier_totals AS (
    SELECT
        supplier_id, supplier_name,
        revenue_pre_markdown, revenue_post_markdown,
        qty_pre_markdown, qty_post_markdown,
        markdown_revenue_share, avg_did_revenue, avg_did_qty,
        dead_stock_rate, unsold_stock_value,
        CASE
            WHEN COALESCE(revenue_pre_markdown, 0) + COALESCE(revenue_post_markdown, 0) = 0 THEN 0::numeric
            ELSE revenue_pre_markdown / NULLIF(revenue_pre_markdown + revenue_post_markdown, 0)
        END AS fullprice_revenue_share
    FROM vw_supplier_markdown_dependency_180d
    WHERE category IS NULL
),
signal_rollup AS (
    SELECT
        supplier_id, supplier_name,
        MIN((first_markdown_date - INTERVAL '30 days')::date) AS period_from,
        MAX((first_markdown_date + INTERVAL '30 days')::date) AS period_to,
        COUNT(*)::int AS article_count,
        COUNT(*) FILTER (WHERE signal_quality_flag = 'high')::numeric / NULLIF(COUNT(*), 0) AS high_signal_share,
        COUNT(*) FILTER (WHERE signal_quality_flag = 'medium')::numeric / NULLIF(COUNT(*), 0) AS medium_signal_share,
        COUNT(*) FILTER (WHERE had_sales_before_markdown_flag)::numeric / NULLIF(COUNT(*), 0) AS had_sales_share,
        COUNT(*) FILTER (WHERE stockout_before_markdown_flag)::numeric / NULLIF(COUNT(*), 0) AS stockout_article_share,
        (COUNT(*) FILTER (WHERE stockout_before_markdown_flag)::numeric / NULLIF(COUNT(*), 0)) >= 0.35 AS stockout_before_markdown_flag,
        SUM(COALESCE(pre_qty_30d, 0))::numeric
            / NULLIF(SUM(COALESCE(pre_qty_30d, 0) + GREATEST(COALESCE(stock_before_markdown, 0), 0)), 0) AS fullprice_sellthrough,
        SUM(COALESCE(pre_margin_30d, 0))::numeric / NULLIF(SUM(COALESCE(pre_revenue_30d, 0)), 0) AS pre_markdown_margin_pct,
        COUNT(*) FILTER (
            WHERE COALESCE(pre_sellthrough_30d, 0) >= 0.45
              AND COALESCE(pre_margin_30d, 0) > 0
              AND COALESCE(had_sales_before_markdown_flag, FALSE)
              AND signal_quality_flag <> 'low'
        )::numeric / NULLIF(COUNT(*), 0) AS repeat_winner_rate
    FROM vw_supplier_fullprice_signals_180d
    GROUP BY supplier_id, supplier_name
),
category_focus AS (
    SELECT
        md.supplier_id,
        MAX((COALESCE(md.revenue_pre_markdown, 0) + COALESCE(md.revenue_post_markdown, 0))
            / NULLIF(COALESCE(st.revenue_pre_markdown, 0) + COALESCE(st.revenue_post_markdown, 0), 0)) * 100 AS category_focus_score
    FROM vw_supplier_markdown_dependency_180d md
    JOIN supplier_totals st ON st.supplier_id = md.supplier_id
    WHERE md.category IS NOT NULL
    GROUP BY md.supplier_id
),
seasonal_category_mix AS (
    SELECT
        md.supplier_id,
        SUM(CASE
            WHEN md.category IS NOT NULL AND (
                md.category ILIKE '%sand%' OR md.category ILIKE '%papuc%' OR md.category ILIKE '%cizm%'
                OR md.category ILIKE '%gleznj%' OR md.category ILIKE '%boot%'
                OR md.category ILIKE '%slipper%' OR md.category ILIKE '%season%'
            ) THEN COALESCE(md.revenue_pre_markdown, 0) + COALESCE(md.revenue_post_markdown, 0)
            ELSE 0
        END)::numeric / NULLIF(SUM(COALESCE(md.revenue_pre_markdown, 0) + COALESCE(md.revenue_post_markdown, 0)), 0) AS seasonal_category_share
    FROM vw_supplier_markdown_dependency_180d md
    WHERE md.category IS NOT NULL
    GROUP BY md.supplier_id
),
sales_in_period AS (
    SELECT sr.supplier_id,
        COALESCE(SUM(CASE WHEN pz.datum_prodaje::date >= sr.period_from AND pz.datum_prodaje::date <= sr.period_to THEN ps.kolicina ELSE 0 END), 0)::numeric AS sold_units_in_period
    FROM signal_rollup sr
    JOIN "Artikli" a ON a."IDDobavljac" = sr.supplier_id
    LEFT JOIN prodaja_stavke ps ON ps.id_artikal = a."Id"
    LEFT JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
    GROUP BY sr.supplier_id
),
returns_in_period AS (
    SELECT sr.supplier_id,
        COALESCE(SUM(CASE WHEN pz.datum_povracaja::date >= sr.period_from AND pz.datum_povracaja::date <= sr.period_to AND COALESCE(pz.status, '') <> 'Odbijen' THEN ps.kolicina ELSE 0 END), 0)::numeric AS returned_units_in_period
    FROM signal_rollup sr
    LEFT JOIN povracaj_zaglavlje pz ON pz.id_dobavljac = sr.supplier_id
    LEFT JOIN povracaj_stavke ps ON ps.id_povracaj = pz.id
    GROUP BY sr.supplier_id
),
decision_inputs AS (
    SELECT
        st.supplier_id, st.supplier_name, sr.period_from, sr.period_to,
        (COALESCE(st.revenue_pre_markdown, 0) + COALESCE(st.revenue_post_markdown, 0))::numeric(18,2) AS revenue,
        (COALESCE(st.qty_pre_markdown, 0) + COALESCE(st.qty_post_markdown, 0))::numeric AS units,
        COALESCE(st.fullprice_revenue_share, 0) AS fullprice_revenue_share,
        COALESCE(sr.fullprice_sellthrough, 0) AS fullprice_sellthrough,
        COALESCE(sr.pre_markdown_margin_pct, 0) AS pre_markdown_margin_pct,
        COALESCE(st.markdown_revenue_share, 0) AS markdown_revenue_share,
        COALESCE(st.dead_stock_rate, 0) AS dead_stock_rate,
        COALESCE(st.unsold_stock_value, 0)::numeric(18,2) AS unsold_stock_value,
        COALESCE(st.post_signal_coverage, 0) AS post_signal_coverage,
        COALESCE(st.did_signal_coverage, 0) AS did_signal_coverage,
        COALESCE(st.cost_signal_coverage, 0) AS cost_signal_coverage,
        CASE
            WHEN COALESCE(si.sold_units_in_period, 0) = 0 THEN NULL
            ELSE COALESCE(ri.returned_units_in_period, 0) / NULLIF(si.sold_units_in_period, 0)
        END AS return_rate,
        CASE
            WHEN COALESCE(si.sold_units_in_period, 0) = 0 THEN 'missing_sales_baseline'
            ELSE NULL
        END AS return_rate_missing_evidence_reason,
        CASE
            WHEN COALESCE(st.post_signal_coverage, 0) < 1
              OR COALESCE(st.did_signal_coverage, 0) < 1
              OR COALESCE(st.cost_signal_coverage, 0) < 1
              OR COALESCE(si.sold_units_in_period, 0) = 0
            THEN 'partial'
            ELSE 'complete'
        END AS evidence_quality_status,
        COALESCE(cf.category_focus_score, 0) AS category_focus_score,
        COALESCE(sr.repeat_winner_rate, 0) AS repeat_winner_rate,
        sr.article_count,
        COALESCE(sr.high_signal_share, 0) AS high_signal_share,
        COALESCE(sr.medium_signal_share, 0) AS medium_signal_share,
        COALESCE(sr.had_sales_share, 0) AS had_sales_share,
        COALESCE(st.avg_did_revenue, 0)::numeric(18,2) AS avg_did_revenue,
        COALESCE(st.avg_did_qty, 0)::numeric(18,4) AS avg_did_qty,
        COALESCE(sr.stockout_article_share, 0) AS stockout_article_share,
        COALESCE(sr.stockout_before_markdown_flag, FALSE) AS stockout_before_markdown_flag,
        COALESCE(scm.seasonal_category_share, 0) AS seasonal_category_share
    FROM supplier_totals st
    JOIN signal_rollup sr ON sr.supplier_id = st.supplier_id
    LEFT JOIN category_focus cf ON cf.supplier_id = st.supplier_id
    LEFT JOIN seasonal_category_mix scm ON scm.supplier_id = st.supplier_id
    LEFT JOIN sales_in_period si ON si.supplier_id = st.supplier_id
    LEFT JOIN returns_in_period ri ON ri.supplier_id = st.supplier_id
),
distribution_bounds AS (
    SELECT COALESCE(percentile_cont(0.80) WITHIN GROUP (ORDER BY GREATEST(COALESCE(pre_markdown_margin_pct, 0), 0)), 0)::numeric AS margin_p80
    FROM decision_inputs
),
normalized_signals AS (
    SELECT di.*,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.fullprice_sellthrough, 0)), 0)::numeric END AS fullprice_sellthrough_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.fullprice_revenue_share, 0)), 0)::numeric END AS fullprice_revenue_share_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY LEAST(GREATEST(COALESCE(di.pre_markdown_margin_pct, 0), 0), db.margin_p80)), 0)::numeric END AS pre_markdown_margin_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.markdown_revenue_share, 0)), 0)::numeric END AS markdown_revenue_share_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.dead_stock_rate, 0)), 0)::numeric END AS dead_stock_rate_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.unsold_stock_value, 0)), 0)::numeric END AS unsold_stock_value_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.repeat_winner_rate, 0)), 0)::numeric END AS repeat_winner_rate_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.return_rate, 0)), 0)::numeric END AS return_rate_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.category_focus_score, 0)), 0)::numeric END AS category_focus_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.article_count, 0)), 0)::numeric END AS article_count_rank,
        CASE WHEN COUNT(*) OVER () = 1 THEN 1::numeric ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.units, 0)), 0)::numeric END AS sales_volume_rank
    FROM decision_inputs di
    CROSS JOIN distribution_bounds db
),
score_components AS (
    SELECT ns.*,
        ROUND((0.60 * ns.fullprice_sellthrough_rank + 0.40 * ns.fullprice_revenue_share_rank) * 100, 2) AS demand_score,
        ROUND(ns.pre_markdown_margin_rank * 100, 2) AS margin_score,
        ROUND(ns.markdown_revenue_share_rank * CASE WHEN ns.seasonal_category_share >= 0.60 THEN 75 WHEN ns.seasonal_category_share >= 0.30 THEN 85 ELSE 100 END, 2) AS markdown_penalty,
        ROUND((0.50 * ns.dead_stock_rate_rank + 0.50 * ns.unsold_stock_value_rank) * 100, 2) AS inventory_penalty,
        ROUND(LEAST(20, GREATEST(-20, (0.50 * ns.repeat_winner_rate_rank - 0.30 * ns.return_rate_rank + 0.20 * ns.category_focus_rank) * 100)), 2) AS supplier_quality_component,
        ROUND(LEAST(1, GREATEST(0, 0.40 * ns.had_sales_share + 0.30 * LEAST(1, GREATEST(0, ns.high_signal_share + 0.50 * ns.medium_signal_share)) + 0.30 * (0.50 * ns.article_count_rank + 0.50 * ns.sales_volume_rank) + 0.10 * COALESCE(ns.post_signal_coverage, 0) + 0.10 * COALESCE(ns.did_signal_coverage, 0) + 0.10 * COALESCE(ns.cost_signal_coverage, 0) - CASE WHEN ns.return_rate IS NULL THEN 0.15 ELSE 0 END)), 4) AS confidence_score
    FROM normalized_signals ns
),
final_score AS (
    SELECT sc.*,
        ROUND(LEAST(100, GREATEST(0, sc.demand_score + sc.margin_score - sc.markdown_penalty - sc.inventory_penalty + sc.supplier_quality_component)), 2) AS supplier_decision_score
    FROM score_components sc
),
recommendation_logic AS (
    SELECT fs.*,
        CASE
            WHEN COALESCE(fs.evidence_quality_status, 'partial') <> 'complete' THEN 'REVIEW_QUALITY'
            WHEN COALESCE(fs.return_rate, 0) > 0.12 THEN 'REVIEW_QUALITY'
            WHEN COALESCE(fs.stockout_before_markdown_flag, FALSE) THEN 'OOS_FALSE_NEGATIVE'
            WHEN fs.supplier_decision_score > 80 THEN 'EXPAND'
            WHEN fs.supplier_decision_score >= 60 THEN 'EXPAND_SELECTIVELY'
            WHEN fs.supplier_decision_score >= 40 THEN 'HOLD'
            WHEN fs.supplier_decision_score >= 25 THEN 'PRICE_NEGOTIATE'
            ELSE 'ASSORTMENT_REDUCE'
        END AS recommendation_code
    FROM final_score fs
)
SELECT
    supplier_id, supplier_name, period_from, period_to, revenue, units,
    ROUND(COALESCE(fullprice_revenue_share, 0), 4) AS fullprice_revenue_share,
    ROUND(COALESCE(fullprice_sellthrough, 0), 4) AS fullprice_sellthrough,
    ROUND(COALESCE(pre_markdown_margin_pct, 0), 4) AS pre_markdown_margin_pct,
    ROUND(COALESCE(markdown_penalty, 0), 2) AS markdown_dependency_score,
    ROUND(COALESCE(inventory_penalty, 0), 2) AS stock_risk_score,
    ROUND(return_rate, 4) AS return_rate,
    ROUND(COALESCE(category_focus_score, 0), 2) AS category_focus_score,
    ROUND(COALESCE(repeat_winner_rate, 0), 4) AS repeat_winner_rate,
    ROUND(COALESCE(post_signal_coverage, 0), 4) AS post_signal_coverage,
    ROUND(COALESCE(did_signal_coverage, 0), 4) AS did_signal_coverage,
    ROUND(COALESCE(cost_signal_coverage, 0), 4) AS cost_signal_coverage,
    evidence_quality_status,
    return_rate_missing_evidence_reason,
    supplier_decision_score AS supplier_quality_index,
    recommendation_code, confidence_score
FROM recommendation_logic;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_supplier_decision_score_cache_180d_pk
    ON mv_supplier_decision_score_cache_180d (supplier_id);

COMMENT ON MATERIALIZED VIEW mv_supplier_decision_score_cache_180d IS
'Supplier decision scorecard computed over the rolling 180-day window. Refreshed nightly.';
