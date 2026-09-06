-- ==========================================================
-- 018_AddSupplierDecisionHubViews.sql
-- Supplier Decision Hub V2 analytics layer.
--
-- Goal:
-- - keep all supplier-decision analytics in PostgreSQL views
-- - reuse existing nivelacija analytics views instead of duplicating
--   pre/post markdown logic in the API or frontend
-- - expose supplier-level recommendation signals for dashboards
-- ==========================================================

-- Views are recreated via CREATE OR REPLACE (idempotent).
-- Materialized views are created only if they do not exist (IF NOT EXISTS).
-- DO NOT drop materialized views here - that forces expensive recreation on every startup.
-- To force recreation of a materialized view manually, run:
--   DROP MATERIALIZED VIEW IF EXISTS mv_supplier_recommendations_cache CASCADE;
--   DROP MATERIALIZED VIEW IF EXISTS mv_supplier_decision_score_cache CASCADE;
--   DROP MATERIALIZED VIEW IF EXISTS mv_supplier_markdown_dependency_cache CASCADE;

-- ==========================================================
-- 1) Prerequisites and Compatibility Stubs
-- ==========================================================

-- Ensure `vw_vendor_sales_nivelacija` exists as a minimal stub. 
-- This view is defined in 014_CreateVendorSalesNivelacijaViews.sql but might be missing 
-- or slow during first-time schema initialization.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind IN ('v','r')
          AND c.relname = 'vw_vendor_sales_nivelacija'
    ) THEN
        EXECUTE $create$
        CREATE VIEW vw_vendor_sales_nivelacija AS
        SELECT
            NULL::bigint AS price_event_id,
            NULL::date AS event_date,
            NULL::bigint AS vendor_id,
            NULL::text AS vendor_name,
            NULL::bigint AS article_id,
            NULL::text AS sku,
            NULL::text AS article_name,
            NULL::text AS category,
            NULL::numeric AS old_price,
            NULL::numeric AS new_price,
            0::numeric AS pre_qty,
            0::numeric AS post_qty,
            0::numeric AS pre_revenue,
            0::numeric AS post_revenue,
            0::numeric AS coverage_pre30,
            0::numeric AS coverage_post30,
            0::numeric AS change_qty,
            0::numeric AS change_revenue,
            0::numeric AS change_percent_qty,
            0::numeric AS change_percent_revenue,
            FALSE AS is_low_signal
        WHERE FALSE;
        $create$;
    END IF;
END
$$;

-- Ensure `vw_nivelacija_did` exists as a minimal stub when running in an environment
-- where the nivelacija views haven't been created yet. This avoids hard failures
-- during DB bootstrap; the real view from migration 016 will replace this stub.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind IN ('v','r')
          AND c.relname = 'vw_nivelacija_did'
    ) THEN
        EXECUTE $create$
        CREATE VIEW vw_nivelacija_did AS
        SELECT
            NULL::bigint AS price_event_id,
            0::numeric AS did_revenue,
            0::numeric AS did_qty
        WHERE FALSE;
        $create$;
    END IF;
END
$$;

-- SQL_BATCH_BREAK

-- ==========================================================
-- 2) Supplier performance before the first markdown event
-- ==========================================================

CREATE OR REPLACE VIEW vw_supplier_fullprice_signals AS
WITH ranked_markdowns AS (
    -- Keep markdown events only and rank them so each article contributes
    -- exactly one "first markdown" anchor row.
    SELECT
        v.price_event_id,
        v.event_date::date AS first_markdown_date,
        v.vendor_id AS supplier_id,
        COALESCE(v.vendor_name, d."Naziv") AS supplier_name,
        COALESCE(v.category, a."Kategorija", 'Uncategorized') AS category,
        v.article_id,
        v.sku,
        v.article_name,
        v.old_price::numeric(18,2) AS old_price,
        v.new_price::numeric(18,2) AS new_price,
        v.coverage_pre30,
        v.coverage_post30,
        v.is_low_signal,
        ROW_NUMBER() OVER (
            PARTITION BY v.article_id
            ORDER BY v.event_date, v.price_event_id
        ) AS rn
    FROM vw_vendor_sales_nivelacija v
    LEFT JOIN "Artikli" a ON a."Id" = v.article_id
    LEFT JOIN "Dobavljaci" d ON d."Id" = COALESCE(v.vendor_id, a."IDDobavljac")
    WHERE v.old_price IS NOT NULL
      AND v.new_price IS NOT NULL
      AND v.new_price < v.old_price
),
first_markdown AS (
    SELECT
        price_event_id,
        first_markdown_date,
        supplier_id,
        supplier_name,
        category,
        article_id,
        sku,
        article_name,
        old_price,
        new_price,
        coverage_pre30,
        coverage_post30,
        is_low_signal
    FROM ranked_markdowns
    WHERE rn = 1
),
sales_profile AS (
    -- Derive pre-markdown quantities, revenue and gross margin directly from
    -- line-level sales so margin stays in SQL and does not leak to the UI.
    SELECT
        fm.article_id,
        COALESCE(SUM(
            CASE
                WHEN pz.datum_prodaje::date >= fm.first_markdown_date - INTERVAL '30 days'
                 AND pz.datum_prodaje::date <  fm.first_markdown_date
                THEN ps.kolicina
                ELSE 0
            END
        ), 0)::numeric AS pre_qty_30d,
        COALESCE(SUM(
            CASE
                WHEN pz.datum_prodaje::date >= fm.first_markdown_date - INTERVAL '30 days'
                 AND pz.datum_prodaje::date <  fm.first_markdown_date
                THEN ps.kolicina * ps.cena
                ELSE 0
            END
        ), 0)::numeric(18,2) AS pre_revenue_30d,
        COALESCE(SUM(
            CASE
                WHEN pz.datum_prodaje::date >= fm.first_markdown_date - INTERVAL '30 days'
                 AND pz.datum_prodaje::date <  fm.first_markdown_date
                THEN ps.kolicina * COALESCE(
                    CASE
                        WHEN ps.nabavna_cena > 0 THEN ps.nabavna_cena
                        WHEN a."NabavnaCenaDin" > 0 THEN a."NabavnaCenaDin"
                        WHEN a."NabavnaCena" > 0 THEN a."NabavnaCena"
                        ELSE NULL
                    END,
                    0
                )
                ELSE 0
            END
        ), 0)::numeric(18,2) AS pre_cost_30d,
        MIN(
            CASE
                WHEN pz.datum_prodaje::date < fm.first_markdown_date
                THEN pz.datum_prodaje::date
                ELSE NULL
            END
        ) AS first_sale_before_markdown_date,
        COALESCE(BOOL_OR(pz.datum_prodaje::date < fm.first_markdown_date), FALSE) AS had_sales_before_markdown_flag,
        COALESCE(SUM(
            CASE
                WHEN pz.datum_prodaje::date >= fm.first_markdown_date
                THEN ps.kolicina
                ELSE 0
            END
        ), 0)::numeric AS sold_since_markdown_qty
    FROM first_markdown fm
    JOIN "Artikli" a ON a."Id" = fm.article_id
    LEFT JOIN prodaja_stavke ps ON ps.id_artikal = fm.article_id
    LEFT JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
    GROUP BY fm.article_id
),
inventory_moves_since_markdown AS (
    -- Reconstruct stock-at-markdown as a proxy:
    -- current stock + sales since markdown + outbound transfers
    -- - inbound receipts - inbound transfers - customer returns.
    -- This keeps the entire stock explanation in SQL and avoids client-side math.
    SELECT
        fm.article_id,
        COALESCE(SUM(
            CASE
                WHEN dp."TipPromene" = 'Ulaz robe'
                THEN COALESCE(dp."Kolicina", 0)
                ELSE 0
            END
        ), 0)::numeric AS receipts_since_markdown_qty,
        COALESCE(SUM(
            CASE
                WHEN dp."TipPromene" = 'Prenos ulaz'
                THEN COALESCE(dp."Kolicina", 0)
                ELSE 0
            END
        ), 0)::numeric AS transfer_in_since_markdown_qty,
        COALESCE(SUM(
            CASE
                WHEN dp."TipPromene" = 'Prenos izlaz'
                THEN COALESCE(dp."Kolicina", 0)
                ELSE 0
            END
        ), 0)::numeric AS transfer_out_since_markdown_qty,
        COALESCE(SUM(
            CASE
                WHEN dp."TipPromene" = 'Povrat kupca'
                THEN COALESCE(dp."Kolicina", 0)
                ELSE 0
            END
        ), 0)::numeric AS customer_return_since_markdown_qty
    FROM first_markdown fm
    LEFT JOIN "DnevnikPromena" dp
           ON dp."ArtikalId" = fm.article_id
          AND dp."Datum"::date >= fm.first_markdown_date
    GROUP BY fm.article_id
),
stock_proxy AS (
    SELECT
        fm.article_id,
        COALESCE(a."Kolicina", 0)::numeric AS current_stock,
        COALESCE(a."MinimalnaKolicina", 0)::numeric AS minimum_stock,
        (
            COALESCE(a."Kolicina", 0)::numeric
            + COALESCE(sp.sold_since_markdown_qty, 0)
            + COALESCE(im.transfer_out_since_markdown_qty, 0)
            - COALESCE(im.receipts_since_markdown_qty, 0)
            - COALESCE(im.transfer_in_since_markdown_qty, 0)
            - COALESCE(im.customer_return_since_markdown_qty, 0)
        )::numeric AS raw_stock_before_markdown
    FROM first_markdown fm
    JOIN "Artikli" a ON a."Id" = fm.article_id
    LEFT JOIN sales_profile sp ON sp.article_id = fm.article_id
    LEFT JOIN inventory_moves_since_markdown im ON im.article_id = fm.article_id
)
SELECT
    fm.supplier_id,
    fm.supplier_name,
    fm.category,
    fm.article_id,
    fm.sku,
    fm.article_name,
    fm.first_markdown_date,
    fm.old_price,
    fm.new_price,
    COALESCE(sp.pre_qty_30d, 0)::numeric AS pre_qty_30d,
    COALESCE(sp.pre_revenue_30d, 0)::numeric(18,2) AS pre_revenue_30d,
    (COALESCE(sp.pre_revenue_30d, 0) - COALESCE(sp.pre_cost_30d, 0))::numeric(18,2) AS pre_margin_30d,
    CASE
        WHEN COALESCE(sp.pre_qty_30d, 0) + GREATEST(COALESCE(st.raw_stock_before_markdown, 0), 0) <= 0
        THEN 0::numeric
        ELSE ROUND(
            COALESCE(sp.pre_qty_30d, 0)
            / NULLIF(COALESCE(sp.pre_qty_30d, 0) + GREATEST(COALESCE(st.raw_stock_before_markdown, 0), 0), 0),
            4
        )
    END AS pre_sellthrough_30d,
    ROUND(COALESCE(sp.pre_qty_30d, 0) / 30.0, 4) AS pre_avg_daily_units,
    CASE
        WHEN sp.first_sale_before_markdown_date IS NULL THEN NULL
        ELSE GREATEST((fm.first_markdown_date - sp.first_sale_before_markdown_date), 0)
    END AS days_to_first_markdown,
    GREATEST(COALESCE(st.raw_stock_before_markdown, 0), 0)::numeric AS stock_before_markdown,
    (
        GREATEST(COALESCE(st.raw_stock_before_markdown, 0), 0)
            <= GREATEST(COALESCE(st.minimum_stock, 0), 1)
        OR (
            COALESCE(sp.had_sales_before_markdown_flag, FALSE) = FALSE
            AND GREATEST(COALESCE(st.raw_stock_before_markdown, 0), 0) <= 0
        )
    ) AS stockout_before_markdown_flag,
    COALESCE(sp.had_sales_before_markdown_flag, FALSE) AS had_sales_before_markdown_flag,
    CASE
        WHEN COALESCE(fm.is_low_signal, FALSE)
          OR COALESCE(fm.coverage_pre30, 0) < 0.20
          OR COALESCE(sp.had_sales_before_markdown_flag, FALSE) = FALSE
        THEN 'low'
        WHEN COALESCE(fm.coverage_pre30, 0) < 0.50
          OR COALESCE(fm.coverage_post30, 0) < 0.50
          OR COALESCE(st.raw_stock_before_markdown, 0) < 0
        THEN 'medium'
        ELSE 'high'
    END AS signal_quality_flag,
    COALESCE(
        NULLIF(
            CONCAT_WS(
                '; ',
                CASE WHEN COALESCE(fm.is_low_signal, FALSE) THEN 'base_nivelacija_view_marked_low_signal' END,
                CASE WHEN COALESCE(fm.coverage_pre30, 0) < 0.20 THEN 'sparse_pre_period_coverage' END,
                CASE WHEN COALESCE(fm.coverage_post30, 0) < 0.20 THEN 'sparse_post_period_coverage' END,
                CASE WHEN COALESCE(sp.had_sales_before_markdown_flag, FALSE) = FALSE THEN 'no_sales_before_first_markdown' END,
                CASE WHEN COALESCE(st.raw_stock_before_markdown, 0) < 0 THEN 'stock_proxy_clamped_to_zero' END,
                CASE
                    WHEN (
                        GREATEST(COALESCE(st.raw_stock_before_markdown, 0), 0)
                            <= GREATEST(COALESCE(st.minimum_stock, 0), 1)
                        OR (
                            COALESCE(sp.had_sales_before_markdown_flag, FALSE) = FALSE
                            AND GREATEST(COALESCE(st.raw_stock_before_markdown, 0), 0) <= 0
                        )
                    )
                    THEN 'stockout_before_markdown'
                END
            ),
            ''
        ),
        'sufficient_pre_markdown_signal'
    ) AS signal_quality_reason
FROM first_markdown fm
LEFT JOIN sales_profile sp ON sp.article_id = fm.article_id
LEFT JOIN stock_proxy st ON st.article_id = fm.article_id;

COMMENT ON VIEW vw_supplier_fullprice_signals IS
'Per-article supplier performance before the first markdown event. Used as the base contract for Supplier Decision Hub.';
COMMENT ON COLUMN vw_supplier_fullprice_signals.pre_margin_30d IS
'Gross margin amount captured in the 30 days before the first markdown.';
COMMENT ON COLUMN vw_supplier_fullprice_signals.pre_sellthrough_30d IS
'Units sold before markdown divided by pre-markdown units plus reconstructed stock before markdown.';
COMMENT ON COLUMN vw_supplier_fullprice_signals.stock_before_markdown IS
'SQL-only proxy for stock immediately before markdown, reconstructed from current stock, sales and inventory moves.';
COMMENT ON COLUMN vw_supplier_fullprice_signals.stockout_before_markdown_flag IS
'True when the article likely lacked enough stock before markdown to fairly measure full-price demand.';
COMMENT ON COLUMN vw_supplier_fullprice_signals.signal_quality_flag IS
'Categorical signal quality: high, medium or low.';
COMMENT ON COLUMN vw_supplier_fullprice_signals.signal_quality_reason IS
'Human-readable explanation of why the pre-markdown signal is strong or weak.';

-- SQL_BATCH_BREAK

-- ==========================================================
-- 3) Supplier dependence on markdown sales
-- ==========================================================
CREATE OR REPLACE VIEW vw_supplier_markdown_dependency AS
WITH signal_base AS (
    -- Reattach the first markdown event to its nivelacija and DiD metrics so
    -- supplier aggregates reuse the existing price-effect evaluation logic.
    SELECT
        fs.supplier_id,
        fs.supplier_name,
        COALESCE(fs.category, 'Uncategorized') AS category,
        fs.article_id,
        fs.sku,
        fs.article_name,
        fs.first_markdown_date,
        fs.old_price,
        fs.new_price,
        fs.pre_qty_30d,
        fs.pre_revenue_30d,
        fs.pre_margin_30d,
        fs.pre_sellthrough_30d,
        fs.pre_avg_daily_units,
        fs.days_to_first_markdown,
        fs.stock_before_markdown,
        fs.stockout_before_markdown_flag,
        fs.had_sales_before_markdown_flag,
        fs.signal_quality_flag,
        fs.signal_quality_reason,
        vn.post_qty::numeric AS post_qty_30d,
        vn.post_revenue::numeric(18,2) AS post_revenue_30d,
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
    FROM vw_supplier_fullprice_signals fs
    LEFT JOIN LATERAL (
        SELECT
            v.price_event_id,
            v.post_qty,
            v.post_revenue
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
        COUNT(*) FILTER (
            WHERE COALESCE(pre_qty_30d, 0) + COALESCE(post_qty_30d, 0) > 0
        )::int AS active_articles_count,
        AVG(CASE WHEN has_post_signal THEN 1::numeric ELSE 0::numeric END) AS post_signal_coverage,
        AVG(CASE WHEN has_did_signal THEN 1::numeric ELSE 0::numeric END) AS did_signal_coverage,
        AVG(CASE WHEN has_cost_signal THEN 1::numeric ELSE 0::numeric END) AS cost_signal_coverage,
        SUM(COALESCE(pre_revenue_30d, 0))::numeric(18,2) AS revenue_pre_markdown,
        SUM(COALESCE(post_revenue_30d, 0))::numeric(18,2) AS revenue_post_markdown,
        SUM(COALESCE(pre_qty_30d, 0))::numeric AS qty_pre_markdown,
        SUM(COALESCE(post_qty_30d, 0))::numeric AS qty_post_markdown,
        SUM(COALESCE(post_revenue_30d, 0))
            / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0)
            AS markdown_revenue_share,
        SUM(COALESCE(post_qty_30d, 0))
            / NULLIF(SUM(COALESCE(pre_qty_30d, 0) + COALESCE(post_qty_30d, 0)), 0)
            AS markdown_unit_share,
        AVG(COALESCE(price_change_pct, 0))::numeric(18,4) AS avg_price_change_pct,
        AVG(COALESCE(did_revenue, 0))::numeric(18,2) AS avg_did_revenue,
        AVG(COALESCE(did_qty, 0))::numeric(18,4) AS avg_did_qty,
        COALESCE(
            SUM(COALESCE(post_revenue_30d, 0)) FILTER (
                WHERE COALESCE(stockout_before_markdown_flag, FALSE) = FALSE
            )
            / NULLIF(
                SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)) FILTER (
                    WHERE COALESCE(stockout_before_markdown_flag, FALSE) = FALSE
                ),
                0
            ),
            SUM(COALESCE(post_revenue_30d, 0))
                / NULLIF(SUM(COALESCE(pre_revenue_30d, 0) + COALESCE(post_revenue_30d, 0)), 0)
        ) AS oos_adjusted_markdown_dependency,
        COUNT(*) FILTER (
            WHERE has_post_signal
              AND COALESCE(current_stock, 0) > 0
              AND COALESCE(post_qty_30d, 0) = 0
        )::numeric
        / NULLIF(COUNT(*) FILTER (WHERE has_post_signal), 0) AS dead_stock_rate,
        SUM(GREATEST(COALESCE(current_stock, 0), 0) * COALESCE(current_cost, 0))::numeric(18,2) AS unsold_stock_value
    FROM signal_base
    GROUP BY GROUPING SETS (
        (supplier_id, supplier_name, category),
        (supplier_id, supplier_name)
    )
)
SELECT
    supplier_id,
    supplier_name,
    category,
    articles_count,
    active_articles_count,
    ROUND(COALESCE(post_signal_coverage, 0), 4) AS post_signal_coverage,
    ROUND(COALESCE(did_signal_coverage, 0), 4) AS did_signal_coverage,
    ROUND(COALESCE(cost_signal_coverage, 0), 4) AS cost_signal_coverage,
    revenue_pre_markdown,
    revenue_post_markdown,
    qty_pre_markdown,
    qty_post_markdown,
    ROUND(COALESCE(markdown_revenue_share, 0), 4) AS markdown_revenue_share,
    ROUND(COALESCE(markdown_unit_share, 0), 4) AS markdown_unit_share,
    ROUND(COALESCE(avg_price_change_pct, 0), 4) AS avg_price_change_pct,
    ROUND(COALESCE(avg_did_revenue, 0), 2) AS avg_did_revenue,
    ROUND(COALESCE(avg_did_qty, 0), 4) AS avg_did_qty,
    ROUND(COALESCE(oos_adjusted_markdown_dependency, 0), 4) AS oos_adjusted_markdown_dependency,
    ROUND(COALESCE(dead_stock_rate, 0), 4) AS dead_stock_rate,
    unsold_stock_value
FROM aggregated;

COMMENT ON VIEW vw_supplier_markdown_dependency IS
'Supplier and supplier-category markdown dependence metrics derived from first-markdown article signals.';
COMMENT ON COLUMN vw_supplier_markdown_dependency.markdown_revenue_share IS
'Share of analyzed revenue that was realized after the first markdown window.';
COMMENT ON COLUMN vw_supplier_markdown_dependency.oos_adjusted_markdown_dependency IS
'Markdown revenue share recalculated after excluding likely stockout false negatives.';
COMMENT ON COLUMN vw_supplier_markdown_dependency.dead_stock_rate IS
'Share of analyzed articles that still hold stock but generated no post-markdown units.';
COMMENT ON COLUMN vw_supplier_markdown_dependency.unsold_stock_value IS
'Current stock value tied up in analyzed supplier articles, valued at current unit cost.';

-- SQL_BATCH_BREAK

-- ==========================================================
-- 4) Central supplier decision scorecard
-- ==========================================================
CREATE OR REPLACE VIEW vw_supplier_decision_score AS
WITH supplier_totals AS (
    SELECT
        supplier_id,
        supplier_name,
        revenue_pre_markdown,
        revenue_post_markdown,
        qty_pre_markdown,
        qty_post_markdown,
        markdown_revenue_share,
        avg_did_revenue,
        avg_did_qty,
        dead_stock_rate,
        unsold_stock_value,
        post_signal_coverage,
        did_signal_coverage,
        cost_signal_coverage,
        CASE
            WHEN COALESCE(revenue_pre_markdown, 0) + COALESCE(revenue_post_markdown, 0) = 0
            THEN 0::numeric
            ELSE revenue_pre_markdown
                 / NULLIF(revenue_pre_markdown + revenue_post_markdown, 0)
        END AS fullprice_revenue_share
    FROM vw_supplier_markdown_dependency
    WHERE category IS NULL
),
signal_rollup AS (
    -- Roll up per-article supplier evidence into one supplier window.
    SELECT
        supplier_id,
        supplier_name,
        MIN((first_markdown_date - INTERVAL '30 days')::date) AS period_from,
        MAX((first_markdown_date + INTERVAL '30 days')::date) AS period_to,
        COUNT(*)::int AS article_count,
        COUNT(*) FILTER (WHERE signal_quality_flag = 'high')::numeric
            / NULLIF(COUNT(*), 0) AS high_signal_share,
        COUNT(*) FILTER (WHERE signal_quality_flag = 'medium')::numeric
            / NULLIF(COUNT(*), 0) AS medium_signal_share,
        COUNT(*) FILTER (WHERE had_sales_before_markdown_flag)::numeric
            / NULLIF(COUNT(*), 0) AS had_sales_share,
        COUNT(*) FILTER (WHERE stockout_before_markdown_flag)::numeric
            / NULLIF(COUNT(*), 0) AS stockout_article_share,
        (
            COUNT(*) FILTER (WHERE stockout_before_markdown_flag)::numeric
            / NULLIF(COUNT(*), 0)
        ) >= 0.35 AS stockout_before_markdown_flag,
        SUM(COALESCE(pre_qty_30d, 0))::numeric
            / NULLIF(
                SUM(COALESCE(pre_qty_30d, 0) + GREATEST(COALESCE(stock_before_markdown, 0), 0)),
                0
            ) AS fullprice_sellthrough,
        SUM(COALESCE(pre_margin_30d, 0))::numeric
            / NULLIF(SUM(COALESCE(pre_revenue_30d, 0)), 0) AS pre_markdown_margin_pct,
        COUNT(*) FILTER (
            WHERE COALESCE(pre_sellthrough_30d, 0) >= 0.45
              AND COALESCE(pre_margin_30d, 0) > 0
              AND COALESCE(had_sales_before_markdown_flag, FALSE)
              AND signal_quality_flag <> 'low'
        )::numeric / NULLIF(COUNT(*), 0) AS repeat_winner_rate
    FROM vw_supplier_fullprice_signals
    GROUP BY supplier_id, supplier_name
),
category_focus AS (
    -- Category focus score answers whether the supplier wins everywhere
    -- or mainly in one concentrated category pocket.
    SELECT
        md.supplier_id,
        MAX(
            (COALESCE(md.revenue_pre_markdown, 0) + COALESCE(md.revenue_post_markdown, 0))
            / NULLIF(COALESCE(st.revenue_pre_markdown, 0) + COALESCE(st.revenue_post_markdown, 0), 0)
        ) * 100 AS category_focus_score
    FROM vw_supplier_markdown_dependency md
    JOIN supplier_totals st ON st.supplier_id = md.supplier_id
    WHERE md.category IS NOT NULL
    GROUP BY md.supplier_id
),
seasonal_category_mix AS (
    -- Some footwear categories naturally markdown more often.
    -- Reduce the markdown penalty if most supplier revenue sits in those categories.
    SELECT
        md.supplier_id,
        SUM(
            CASE
                WHEN md.category IS NOT NULL
                 AND (
                    md.category ILIKE '%sand%'
                    OR md.category ILIKE '%papuc%'
                    OR md.category ILIKE '%cizm%'
                    OR md.category ILIKE '%gleznj%'
                    OR md.category ILIKE '%boot%'
                    OR md.category ILIKE '%slipper%'
                    OR md.category ILIKE '%season%'
                 )
                THEN COALESCE(md.revenue_pre_markdown, 0) + COALESCE(md.revenue_post_markdown, 0)
                ELSE 0
            END
        )::numeric
        / NULLIF(
            SUM(COALESCE(md.revenue_pre_markdown, 0) + COALESCE(md.revenue_post_markdown, 0)),
            0
        ) AS seasonal_category_share
    FROM vw_supplier_markdown_dependency md
    WHERE md.category IS NOT NULL
    GROUP BY md.supplier_id
),
sales_in_period AS (
    -- Sold units in the supplier evidence window, used as the denominator
    -- for return rate.
    SELECT
        sr.supplier_id,
        COALESCE(SUM(
            CASE
                WHEN pz.datum_prodaje::date >= sr.period_from
                 AND pz.datum_prodaje::date <= sr.period_to
                THEN ps.kolicina
                ELSE 0
            END
        ), 0)::numeric AS sold_units_in_period
    FROM signal_rollup sr
    JOIN "Artikli" a ON a."IDDobavljac" = sr.supplier_id
    LEFT JOIN prodaja_stavke ps ON ps.id_artikal = a."Id"
    LEFT JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
    GROUP BY sr.supplier_id
),
returns_in_period AS (
    -- Supplier returns indicate quality / fit / assortment problems.
    -- Ignore explicitly rejected return documents.
    SELECT
        sr.supplier_id,
        COALESCE(SUM(
            CASE
                WHEN pz.datum_povracaja::date >= sr.period_from
                 AND pz.datum_povracaja::date <= sr.period_to
                 AND COALESCE(pz.status, '') <> 'Odbijen'
                THEN ps.kolicina
                ELSE 0
            END
        ), 0)::numeric AS returned_units_in_period
    FROM signal_rollup sr
    LEFT JOIN povracaj_zaglavlje pz ON pz.id_dobavljac = sr.supplier_id
    LEFT JOIN povracaj_stavke ps ON ps.id_povracaj = pz.id
    GROUP BY sr.supplier_id
),
decision_inputs AS (
    SELECT
        st.supplier_id,
        st.supplier_name,
        sr.period_from,
        sr.period_to,
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
            ELSE COALESCE(ri.returned_units_in_period, 0)
                 / NULLIF(si.sold_units_in_period, 0)
        END AS return_rate,
        CASE
            WHEN COALESCE(si.sold_units_in_period, 0) = 0 THEN 'missing_sales_baseline'
            ELSE NULL
        END AS return_rate_missing_evidence_reason,
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
    JOIN signal_rollup sr
      ON sr.supplier_id = st.supplier_id
    LEFT JOIN category_focus cf ON cf.supplier_id = st.supplier_id
    LEFT JOIN seasonal_category_mix scm ON scm.supplier_id = st.supplier_id
    LEFT JOIN sales_in_period si ON si.supplier_id = st.supplier_id
    LEFT JOIN returns_in_period ri ON ri.supplier_id = st.supplier_id
),
distribution_bounds AS (
    SELECT
        COALESCE(
            percentile_cont(0.80) WITHIN GROUP (
                ORDER BY GREATEST(COALESCE(pre_markdown_margin_pct, 0), 0)
            ),
            0
        )::numeric AS margin_p80
    FROM decision_inputs
),
normalized_signals AS (
    -- Percentile normalization keeps the score stable across changing supplier cohorts.
    SELECT
        di.supplier_id,
        di.supplier_name,
        di.period_from,
        di.period_to,
        di.revenue,
        di.units,
        di.fullprice_revenue_share,
        di.fullprice_sellthrough,
        di.pre_markdown_margin_pct,
        di.markdown_revenue_share,
        di.dead_stock_rate,
        di.unsold_stock_value,
        di.post_signal_coverage,
        di.did_signal_coverage,
        di.cost_signal_coverage,
        di.return_rate,
        di.return_rate_missing_evidence_reason,
        di.category_focus_score,
        di.repeat_winner_rate,
        di.article_count,
        di.high_signal_share,
        di.medium_signal_share,
        di.had_sales_share,
        di.avg_did_revenue,
        di.avg_did_qty,
        di.stockout_article_share,
        di.stockout_before_markdown_flag,
        di.seasonal_category_share,
        CASE
            WHEN COALESCE(di.post_signal_coverage, 0) < 1
              OR COALESCE(di.did_signal_coverage, 0) < 1
              OR COALESCE(di.cost_signal_coverage, 0) < 1
              OR di.return_rate_missing_evidence_reason IS NOT NULL
            THEN 'partial'
            ELSE 'complete'
        END AS evidence_quality_status,
        CASE
            WHEN COUNT(*) OVER () = 1 THEN 1::numeric
            ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.fullprice_sellthrough, 0)), 0)::numeric
        END AS fullprice_sellthrough_rank,
        CASE
            WHEN COUNT(*) OVER () = 1 THEN 1::numeric
            ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.fullprice_revenue_share, 0)), 0)::numeric
        END AS fullprice_revenue_share_rank,
        CASE
            WHEN COUNT(*) OVER () = 1 THEN 1::numeric
            ELSE COALESCE(
                PERCENT_RANK() OVER (
                    ORDER BY LEAST(
                        GREATEST(COALESCE(di.pre_markdown_margin_pct, 0), 0),
                        db.margin_p80
                    )
                ),
                0
            )::numeric
        END AS pre_markdown_margin_rank,
        CASE
            WHEN COUNT(*) OVER () = 1 THEN 1::numeric
            ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.markdown_revenue_share, 0)), 0)::numeric
        END AS markdown_revenue_share_rank,
        CASE
            WHEN COUNT(*) OVER () = 1 THEN 1::numeric
            ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.dead_stock_rate, 0)), 0)::numeric
        END AS dead_stock_rate_rank,
        CASE
            WHEN COUNT(*) OVER () = 1 THEN 1::numeric
            ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.unsold_stock_value, 0)), 0)::numeric
        END AS unsold_stock_value_rank,
        CASE
            WHEN COUNT(*) OVER () = 1 THEN 1::numeric
            ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.repeat_winner_rate, 0)), 0)::numeric
        END AS repeat_winner_rate_rank,
        CASE
            WHEN COUNT(*) OVER () = 1 THEN 1::numeric
            ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.return_rate, 0)), 0)::numeric
        END AS return_rate_rank,
        CASE
            WHEN COUNT(*) OVER () = 1 THEN 1::numeric
            ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.category_focus_score, 0)), 0)::numeric
        END AS category_focus_rank,
        CASE
            WHEN COUNT(*) OVER () = 1 THEN 1::numeric
            ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.article_count, 0)), 0)::numeric
        END AS article_count_rank,
        CASE
            WHEN COUNT(*) OVER () = 1 THEN 1::numeric
            ELSE COALESCE(PERCENT_RANK() OVER (ORDER BY COALESCE(di.units, 0)), 0)::numeric
        END AS sales_volume_rank
    FROM decision_inputs di
    CROSS JOIN distribution_bounds db
),
score_components AS (
    -- Structured supplier decision model built from demand, margin and penalties.
    SELECT
        ns.*,
        ROUND(
            (0.60 * ns.fullprice_sellthrough_rank + 0.40 * ns.fullprice_revenue_share_rank) * 100,
            2
        ) AS demand_score,
        ROUND(ns.pre_markdown_margin_rank * 100, 2) AS margin_score,
        ROUND(
            ns.markdown_revenue_share_rank
            * CASE
                WHEN ns.seasonal_category_share >= 0.60 THEN 75
                WHEN ns.seasonal_category_share >= 0.30 THEN 85
                ELSE 100
              END,
            2
        ) AS markdown_penalty,
        ROUND(
            (0.50 * ns.dead_stock_rate_rank + 0.50 * ns.unsold_stock_value_rank) * 100,
            2
        ) AS inventory_penalty,
        ROUND(
            LEAST(
                20,
                GREATEST(
                    -20,
                    (
                        0.50 * ns.repeat_winner_rate_rank
                        - 0.30 * ns.return_rate_rank
                        + 0.20 * ns.category_focus_rank
                    ) * 100
                )
            ),
            2
        ) AS supplier_quality_component,
        ROUND(
            LEAST(
                1,
                GREATEST(
                    0,
                    0.40 * ns.had_sales_share
                    + 0.30 * LEAST(1, GREATEST(0, ns.high_signal_share + 0.50 * ns.medium_signal_share))
                    + 0.30 * (
                        0.50 * ns.article_count_rank
                        + 0.50 * ns.sales_volume_rank
                    )
                    + 0.10 * COALESCE(ns.post_signal_coverage, 0)
                    + 0.10 * COALESCE(ns.did_signal_coverage, 0)
                    + 0.10 * COALESCE(ns.cost_signal_coverage, 0)
                    - CASE WHEN ns.return_rate IS NULL THEN 0.15 ELSE 0 END
                )
            ),
            4
        ) AS confidence_score
    FROM normalized_signals ns
),
final_score AS (
    SELECT
        sc.*,
        ROUND(
            LEAST(
                100,
                GREATEST(
                    0,
                    sc.demand_score
                    + sc.margin_score
                    - sc.markdown_penalty
                    - sc.inventory_penalty
                    + sc.supplier_quality_component
                )
            ),
            2
        ) AS supplier_decision_score
    FROM score_components sc
),
recommendation_logic AS (
    SELECT
        fs.*,
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
    supplier_id,
    supplier_name,
    period_from,
    period_to,
    revenue,
    units,
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
    recommendation_code,
    confidence_score
FROM recommendation_logic;

COMMENT ON VIEW vw_supplier_decision_score IS
'Supplier-level decision scorecard for expansion, negotiation, reduction and stockout-exception handling.';
COMMENT ON COLUMN vw_supplier_decision_score.markdown_dependency_score IS
'0-100 normalized markdown penalty where higher values mean the supplier depends more on markdown windows to realize revenue.';
COMMENT ON COLUMN vw_supplier_decision_score.stock_risk_score IS
'0-100 inventory penalty combining normalized dead stock share and normalized unsold stock value.';
COMMENT ON COLUMN vw_supplier_decision_score.category_focus_score IS
'0-100 concentration score representing how much supplier revenue is concentrated in its strongest category.';
COMMENT ON COLUMN vw_supplier_decision_score.repeat_winner_rate IS
'Share of the supplier''s analyzed articles that achieved healthy pre-markdown sell-through and positive margin.';
COMMENT ON COLUMN vw_supplier_decision_score.supplier_quality_index IS
'Final 0-100 supplier decision score built from demand score, margin score, markdown penalty, inventory penalty and supplier quality component.';
COMMENT ON COLUMN vw_supplier_decision_score.confidence_score IS
'0-1 confidence score driven by coverage, signal quality and sample size.';

-- SQL_BATCH_BREAK

-- ==========================================================
-- 5) Presentation-ready recommendations
-- ==========================================================
CREATE OR REPLACE VIEW vw_supplier_recommendations AS
WITH base AS (
    SELECT *
    FROM vw_supplier_decision_score
)
SELECT
    supplier_id,
    supplier_name,
    recommendation_code,
    CASE recommendation_code
        WHEN 'EXPAND' THEN 'Povecati saradnju'
        WHEN 'EXPAND_SELECTIVELY' THEN 'Povecati selektivno'
        WHEN 'PRICE_NEGOTIATE' THEN 'Pregovarati o ceni'
        WHEN 'ASSORTMENT_REDUCE' THEN 'Smanjiti nabavku'
        WHEN 'OOS_FALSE_NEGATIVE' THEN 'Proveriti zalihe pre odluke'
        WHEN 'REVIEW_QUALITY' THEN 'Proveriti kvalitet i povracaje'
        ELSE 'Zadrzati postojeci nivo'
    END AS recommendation_title,
    CASE recommendation_code
        WHEN 'EXPAND' THEN
            CASE
                WHEN fullprice_sellthrough >= 0.60 AND pre_markdown_margin_pct >= 0.25
                THEN 'Visok sell-through bez snizenja i stabilna marza.'
                WHEN fullprice_revenue_share >= 0.60
                THEN 'Veci deo prihoda dolazi pre prvog snizenja.'
                ELSE 'Dobavljac ostvaruje jak rezultat bez veceg oslanjanja na snizenja.'
            END
        WHEN 'EXPAND_SELECTIVELY' THEN 'Dobavljac ima dobar ukupan skor, ali su najbolji rezultati koncentrisani u uzem skupu kategorija.'
        WHEN 'PRICE_NEGOTIATE' THEN 'Prodaja se otvara tek nakon snizenja, pa ulaznu cenu treba pregovarati.'
        WHEN 'ASSORTMENT_REDUCE' THEN 'Visoka zavisnost od snizenja i spor promet vezuju kapital u zalihama.'
        WHEN 'OOS_FALSE_NEGATIVE' THEN 'Nedostatak zaliha pre snizenja verovatno iskrivljuje procenu ovog dobavljaca.'
        WHEN 'REVIEW_QUALITY' THEN 'Povracaji su previsoki u odnosu na prodaju i umanjuju kvalitet saradnje.'
        ELSE 'Signal je mesovit, pa je najbolje zadrzati trenutni nivo saradnje dok se ne prikupi vise podataka.'
    END AS recommendation_reason,
    CASE recommendation_code
        WHEN 'EXPAND' THEN 'supplier_quality_index'
        WHEN 'EXPAND_SELECTIVELY' THEN 'category_focus_score'
        WHEN 'PRICE_NEGOTIATE' THEN 'markdown_dependency_score'
        WHEN 'ASSORTMENT_REDUCE' THEN 'stock_risk_score'
        WHEN 'OOS_FALSE_NEGATIVE' THEN 'confidence_score'
        WHEN 'REVIEW_QUALITY' THEN 'return_rate'
        ELSE 'supplier_quality_index'
    END AS primary_metric,
    CASE recommendation_code
        WHEN 'EXPAND' THEN supplier_quality_index
        WHEN 'EXPAND_SELECTIVELY' THEN category_focus_score
        WHEN 'PRICE_NEGOTIATE' THEN markdown_dependency_score
        WHEN 'ASSORTMENT_REDUCE' THEN stock_risk_score
        WHEN 'OOS_FALSE_NEGATIVE' THEN confidence_score
        WHEN 'REVIEW_QUALITY' THEN return_rate
        ELSE supplier_quality_index
    END AS primary_metric_value,
    CASE recommendation_code
        WHEN 'EXPAND' THEN 'fullprice_sellthrough'
        WHEN 'EXPAND_SELECTIVELY' THEN 'repeat_winner_rate'
        WHEN 'PRICE_NEGOTIATE' THEN 'pre_markdown_margin_pct'
        WHEN 'ASSORTMENT_REDUCE' THEN 'markdown_dependency_score'
        WHEN 'OOS_FALSE_NEGATIVE' THEN 'fullprice_sellthrough'
        WHEN 'REVIEW_QUALITY' THEN 'supplier_quality_index'
        ELSE 'confidence_score'
    END AS secondary_metric,
    CASE recommendation_code
        WHEN 'EXPAND' THEN fullprice_sellthrough
        WHEN 'EXPAND_SELECTIVELY' THEN repeat_winner_rate
        WHEN 'PRICE_NEGOTIATE' THEN pre_markdown_margin_pct
        WHEN 'ASSORTMENT_REDUCE' THEN markdown_dependency_score
        WHEN 'OOS_FALSE_NEGATIVE' THEN fullprice_sellthrough
        WHEN 'REVIEW_QUALITY' THEN supplier_quality_index
        ELSE confidence_score
    END AS secondary_metric_value,
    CASE recommendation_code
        WHEN 'EXPAND' THEN 'medium'
        WHEN 'EXPAND_SELECTIVELY' THEN 'medium'
        WHEN 'PRICE_NEGOTIATE' THEN 'high'
        WHEN 'ASSORTMENT_REDUCE' THEN 'high'
        WHEN 'OOS_FALSE_NEGATIVE' THEN 'medium'
        WHEN 'REVIEW_QUALITY' THEN 'high'
        ELSE 'low'
    END AS urgency,
    confidence_score
FROM base;

COMMENT ON VIEW vw_supplier_recommendations IS
'Presentation-ready supplier recommendations derived fully in SQL from supplier decision scores.';
COMMENT ON COLUMN vw_supplier_recommendations.recommendation_reason IS
'Human-readable reason string explaining the SQL-generated supplier recommendation.';

-- SQL_BATCH_BREAK

-- ==========================================================
-- 6) Materialized cache for default overview reads
-- ==========================================================
CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_markdown_dependency_cache AS
SELECT *
FROM vw_supplier_markdown_dependency;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_supplier_markdown_dependency_cache_pk
    ON mv_supplier_markdown_dependency_cache (supplier_id, (COALESCE(category, '')));

CREATE INDEX IF NOT EXISTS idx_mv_supplier_markdown_dependency_cache_supplier
    ON mv_supplier_markdown_dependency_cache (supplier_id);

-- SQL_BATCH_BREAK

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_decision_score_cache AS
SELECT *
FROM vw_supplier_decision_score;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_supplier_decision_score_cache_pk
    ON mv_supplier_decision_score_cache (supplier_id);

-- SQL_BATCH_BREAK

CREATE MATERIALIZED VIEW IF NOT EXISTS mv_supplier_recommendations_cache AS
SELECT *
FROM vw_supplier_recommendations;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_supplier_recommendations_cache_pk
    ON mv_supplier_recommendations_cache (supplier_id);

COMMENT ON MATERIALIZED VIEW mv_supplier_markdown_dependency_cache IS
'Materialized cache of supplier markdown dependency totals for fast default Supplier Decision Hub overview reads.';
COMMENT ON MATERIALIZED VIEW mv_supplier_decision_score_cache IS
'Materialized cache of supplier decision totals for fast default Supplier Decision Hub overview reads.';
COMMENT ON MATERIALIZED VIEW mv_supplier_recommendations_cache IS
'Materialized cache of supplier recommendations for fast default Supplier Decision Hub overview reads.';
