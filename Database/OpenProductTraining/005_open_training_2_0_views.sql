-- =============================================================
-- Open Product Training 2.0 views
-- Created: 2026-02-28
-- Idempotent: CREATE OR REPLACE
-- =============================================================

-- -------------------------------------------------------------
-- vw_product_momentum
-- Price-derived momentum + volatility features from product_price_history
-- -------------------------------------------------------------
CREATE OR REPLACE VIEW vw_product_momentum AS
WITH ph AS (
    SELECT
        product_id,
        collected_at,
        price,
        LAG(price) OVER (PARTITION BY product_id ORDER BY collected_at) AS prev_price
    FROM product_price_history
    WHERE collected_at >= NOW() - INTERVAL '180 days'
),
rets AS (
    SELECT
        product_id,
        collected_at,
        price,
        CASE
            WHEN prev_price IS NULL OR prev_price <= 0 THEN NULL
            ELSE (price - prev_price) / prev_price
        END AS pct_change
    FROM ph
),
window_prices AS (
    SELECT DISTINCT ON (product_id)
        product_id,
        price AS last_price,
        collected_at AS last_collected_at
    FROM ph
    ORDER BY product_id, collected_at DESC
),
first_7 AS (
    SELECT DISTINCT ON (product_id)
        product_id,
        price AS first_price_7d,
        collected_at AS first_collected_at_7d
    FROM ph
    WHERE collected_at >= NOW() - INTERVAL '7 days'
    ORDER BY product_id, collected_at ASC
),
last_7 AS (
    SELECT DISTINCT ON (product_id)
        product_id,
        price AS last_price_7d,
        collected_at AS last_collected_at_7d
    FROM ph
    WHERE collected_at >= NOW() - INTERVAL '7 days'
    ORDER BY product_id, collected_at DESC
),
first_30 AS (
    SELECT DISTINCT ON (product_id)
        product_id,
        price AS first_price_30d
    FROM ph
    WHERE collected_at >= NOW() - INTERVAL '30 days'
    ORDER BY product_id, collected_at ASC
),
last_30 AS (
    SELECT DISTINCT ON (product_id)
        product_id,
        price AS last_price_30d
    FROM ph
    WHERE collected_at >= NOW() - INTERVAL '30 days'
    ORDER BY product_id, collected_at DESC
),
first_90 AS (
    SELECT DISTINCT ON (product_id)
        product_id,
        price AS first_price_90d
    FROM ph
    WHERE collected_at >= NOW() - INTERVAL '90 days'
    ORDER BY product_id, collected_at ASC
),
last_90 AS (
    SELECT DISTINCT ON (product_id)
        product_id,
        price AS last_price_90d
    FROM ph
    WHERE collected_at >= NOW() - INTERVAL '90 days'
    ORDER BY product_id, collected_at DESC
),
agg AS (
    SELECT
        product_id,
        COUNT(*) FILTER (WHERE collected_at >= NOW() - INTERVAL '7 days')  AS obs_7d,
        COUNT(*) FILTER (WHERE collected_at >= NOW() - INTERVAL '30 days') AS obs_30d,
        COUNT(*) FILTER (WHERE collected_at >= NOW() - INTERVAL '90 days') AS obs_90d,

        STDDEV_SAMP(pct_change) FILTER (WHERE collected_at >= NOW() - INTERVAL '7 days')  AS volatility_7d,
        STDDEV_SAMP(pct_change) FILTER (WHERE collected_at >= NOW() - INTERVAL '30 days') AS volatility_30d,
        STDDEV_SAMP(pct_change) FILTER (WHERE collected_at >= NOW() - INTERVAL '90 days') AS volatility_90d,

        AVG(ABS(pct_change)) FILTER (WHERE collected_at >= NOW() - INTERVAL '30 days') AS typical_change_rate_30d,

        AVG(CASE WHEN pct_change < 0 THEN 1 ELSE 0 END)::NUMERIC FILTER (WHERE collected_at >= NOW() - INTERVAL '30 days') AS discount_freq_30d,
        AVG(CASE WHEN pct_change < 0 THEN 1 ELSE 0 END)::NUMERIC FILTER (WHERE collected_at >= NOW() - INTERVAL '90 days') AS discount_freq_90d
    FROM rets
    GROUP BY product_id
)
SELECT
    a.product_id,
    wp.last_price,
    wp.last_collected_at,

    a.obs_7d,
    a.obs_30d,
    a.obs_90d,

    a.volatility_7d,
    a.volatility_30d,
    a.volatility_90d,

    CASE WHEN f7.first_price_7d  IS NULL OR f7.first_price_7d  <= 0 OR l7.last_price_7d  IS NULL THEN NULL
         ELSE (l7.last_price_7d  - f7.first_price_7d)  / f7.first_price_7d END AS momentum_7d,
    CASE WHEN f30.first_price_30d IS NULL OR f30.first_price_30d <= 0 OR l30.last_price_30d IS NULL THEN NULL
         ELSE (l30.last_price_30d - f30.first_price_30d) / f30.first_price_30d END AS momentum_30d,
    CASE WHEN f90.first_price_90d IS NULL OR f90.first_price_90d <= 0 OR l90.last_price_90d IS NULL THEN NULL
         ELSE (l90.last_price_90d - f90.first_price_90d) / f90.first_price_90d END AS momentum_90d,

    a.discount_freq_30d,
    a.discount_freq_90d,
    a.typical_change_rate_30d
FROM agg a
LEFT JOIN window_prices wp ON wp.product_id = a.product_id
LEFT JOIN first_7  f7  ON f7.product_id  = a.product_id
LEFT JOIN last_7   l7  ON l7.product_id  = a.product_id
LEFT JOIN first_30 f30 ON f30.product_id = a.product_id
LEFT JOIN last_30  l30 ON l30.product_id = a.product_id
LEFT JOIN first_90 f90 ON f90.product_id = a.product_id
LEFT JOIN last_90  l90 ON l90.product_id = a.product_id;

COMMENT ON VIEW vw_product_momentum IS
'Price-derived momentum/volatility features from open_product_training.product_price_history.';

-- -------------------------------------------------------------
-- vw_brand_category_priors_enriched
-- Hierarchical priors: (brand+category) -> category -> brand -> global
-- -------------------------------------------------------------
CREATE OR REPLACE VIEW vw_brand_category_priors_enriched AS
WITH base AS (
    SELECT
        LOWER(TRIM(COALESCE(b.name, ''))) AS brand_key,
        LOWER(TRIM(COALESCE(NULLIF(p.shoe_type, ''), c.name, ''))) AS category_key,
        ROUND(AVG(COALESCE(ll.popularity_prior, 0)), 2) AS popularity_prior_score,
        ROUND(AVG(COALESCE(ll.deal_score, 0)), 2)       AS deal_score,
        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY p.price) AS typical_price,
        COUNT(*)::INT AS sample_size
    FROM product p
    LEFT JOIN brand b ON b.id = p.brand_id
    LEFT JOIN category c ON c.id = p.category_id
    LEFT JOIN vw_product_latest_labels ll ON ll.product_id = p.id
    WHERE p.price IS NOT NULL
      AND COALESCE(NULLIF(p.shoe_type, ''), c.name) IS NOT NULL
      AND TRIM(COALESCE(NULLIF(p.shoe_type, ''), c.name)) <> ''
    GROUP BY 1, 2
),
by_category AS (
    SELECT
        category_key,
        ROUND(AVG(popularity_prior_score), 2) AS popularity_prior_score,
        ROUND(AVG(deal_score), 2)             AS deal_score,
        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY typical_price) AS typical_price,
        SUM(sample_size)::INT AS sample_size
    FROM base
    GROUP BY 1
),
by_brand AS (
    SELECT
        brand_key,
        ROUND(AVG(popularity_prior_score), 2) AS popularity_prior_score,
        ROUND(AVG(deal_score), 2)             AS deal_score,
        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY typical_price) AS typical_price,
        SUM(sample_size)::INT AS sample_size
    FROM base
    GROUP BY 1
),
global AS (
    SELECT
        ROUND(AVG(popularity_prior_score), 2) AS popularity_prior_score,
        ROUND(AVG(deal_score), 2)             AS deal_score,
        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY typical_price) AS typical_price,
        SUM(sample_size)::INT AS sample_size
    FROM base
)
SELECT
    b.brand_key,
    b.category_key,

    b.popularity_prior_score AS brand_category_popularity_prior,
    b.deal_score             AS brand_category_deal_score,
    b.typical_price          AS brand_category_typical_price,
    b.sample_size            AS brand_category_sample_size,

    c.popularity_prior_score AS category_popularity_prior,
    c.deal_score             AS category_deal_score,
    c.typical_price          AS category_typical_price,
    c.sample_size            AS category_sample_size,

    br.popularity_prior_score AS brand_popularity_prior,
    br.deal_score             AS brand_deal_score,
    br.typical_price          AS brand_typical_price,
    br.sample_size            AS brand_sample_size,

    g.popularity_prior_score AS global_popularity_prior,
    g.deal_score             AS global_deal_score,
    g.typical_price          AS global_typical_price,
    g.sample_size            AS global_sample_size,

    CASE
        WHEN b.sample_size >= 10 THEN b.popularity_prior_score
        WHEN c.sample_size >= 10 THEN c.popularity_prior_score
        WHEN br.sample_size >= 10 THEN br.popularity_prior_score
        ELSE g.popularity_prior_score
    END AS resolved_popularity_prior,

    CASE
        WHEN b.sample_size >= 10 THEN b.deal_score
        WHEN c.sample_size >= 10 THEN c.deal_score
        WHEN br.sample_size >= 10 THEN br.deal_score
        ELSE g.deal_score
    END AS resolved_deal_score,

    CASE
        WHEN b.sample_size >= 10 THEN b.typical_price
        WHEN c.sample_size >= 10 THEN c.typical_price
        WHEN br.sample_size >= 10 THEN br.typical_price
        ELSE g.typical_price
    END AS resolved_typical_price,

    CASE
        WHEN b.sample_size >= 10 THEN 'brand_category'
        WHEN c.sample_size >= 10 THEN 'category'
        WHEN br.sample_size >= 10 THEN 'brand'
        ELSE 'global'
    END AS resolved_level
FROM base b
LEFT JOIN by_category c ON c.category_key = b.category_key
LEFT JOIN by_brand br   ON br.brand_key   = b.brand_key
CROSS JOIN global g;

COMMENT ON VIEW vw_brand_category_priors_enriched IS
'Hierarchical priors with fallback: brand+category -> category -> brand -> global.';

-- -------------------------------------------------------------
-- vw_feature_store
-- One row per product with engineered features for ML + runtime
-- -------------------------------------------------------------
CREATE OR REPLACE VIEW vw_feature_store AS
WITH base AS (
    SELECT
        p.id AS product_id,
        d.id AS dataset_id,
        d.name AS dataset_name,
        d.source_type,
        p.external_id,

        b.name AS brand,
        c.name AS category,
        p.shoe_type,
        p.gender,
        p.currency,
        p.price,

        COALESCE(rs.avg_rating, p.avg_rating) AS avg_rating,
        COALESCE(rs.rating_count, p.review_count) AS review_count,

        p.created_at,
        p.updated_at,

        LOWER(TRIM(COALESCE(b.name, ''))) AS brand_key,
        LOWER(TRIM(COALESCE(NULLIF(p.shoe_type, ''), c.name, ''))) AS category_key,

        CASE
            WHEN p.external_id ~ '^[0-9]+$' THEN p.external_id::INT
            ELSE NULL
        END AS local_product_id
    FROM product p
    JOIN dataset d ON d.id = p.dataset_id
    LEFT JOIN brand b ON b.id = p.brand_id
    LEFT JOIN category c ON c.id = p.category_id
    LEFT JOIN product_review_stats rs ON rs.product_id = p.id
),
priors AS (
    SELECT
        brand_key,
        category_key,
        resolved_popularity_prior,
        resolved_deal_score,
        resolved_typical_price,
        resolved_level
    FROM vw_brand_category_priors_enriched
),
mom AS (
    SELECT
        product_id,
        volatility_7d,
        volatility_30d,
        volatility_90d,
        momentum_7d,
        momentum_30d,
        momentum_90d,
        discount_freq_30d,
        discount_freq_90d,
        typical_change_rate_30d
    FROM vw_product_momentum
),
img AS (
    SELECT DISTINCT ON (product_id)
        product_id,
        embedding_model,
        cluster_id,
        created_at AS image_embedding_created_at
    FROM product_feature_vector_image_v2
    ORDER BY product_id, created_at DESC
),
latest_sell AS (
    SELECT DISTINCT ON (product_id, horizon_days)
        product_id,
        horizon_days,
        label_value,
        label_version,
        computed_at
    FROM training_label_sell_probability_rs
    WHERE label_version = 'v1'
    ORDER BY product_id, horizon_days, computed_at DESC, id DESC
),
rs_first_seen AS (
    SELECT
        pd."ProductId" AS product_id,
        MIN(pd."Timestamp") AS first_seen_at
    FROM "ProductsDim" pd
    GROUP BY pd."ProductId"
),
rs_sales AS (
    SELECT
        sl."ProductId" AS product_id,
        sf."SaleTimestampUtc" AS sold_at,
        sl."Qty"::INT AS qty,
        sl."UnitPrice"::NUMERIC AS unit_price
    FROM "SalesLineFacts" sl
    JOIN "SalesFacts" sf ON sf."SaleId" = sl."SaleId"
),
rs_sales_agg AS (
    SELECT
        s.product_id,
        SUM(s.qty) FILTER (WHERE s.sold_at >= NOW() - INTERVAL '30 days') AS sold_qty_30d,
        SUM(s.qty) FILTER (WHERE s.sold_at >= NOW() - INTERVAL '90 days') AS sold_qty_90d,
        MIN(s.sold_at) AS first_sale_at
    FROM rs_sales s
    GROUP BY s.product_id
),
rs_inflows AS (
    SELECT
        imf."ArtikalId" AS product_id,
        SUM(COALESCE(imf."Kolicina", 0)) FILTER (WHERE imf."Datum" >= NOW() - INTERVAL '30 days') AS inflow_qty_30d,
        SUM(COALESCE(imf."Kolicina", 0)) FILTER (WHERE imf."Datum" >= NOW() - INTERVAL '90 days') AS inflow_qty_90d,
        MIN(imf."Datum") AS first_inflow_at
    FROM "InventoryMovementFacts" imf
    WHERE imf."ArtikalId" IS NOT NULL
      AND imf."Kolicina" IS NOT NULL
      AND imf."Kolicina" > 0
      AND imf."TipPromene" = ANY(ARRAY['Ulaz robe','Prenos ulaz','Povrat kupca'])
    GROUP BY imf."ArtikalId"
),
rs_daily AS (
    SELECT
        s.product_id,
        (s.sold_at AT TIME ZONE 'UTC')::DATE AS sale_date,
        SUM(s.qty)::NUMERIC AS qty,
        AVG(NULLIF(s.unit_price, 0))::NUMERIC AS price
    FROM rs_sales s
    GROUP BY s.product_id, (s.sold_at AT TIME ZONE 'UTC')::DATE
),
rs_elasticity AS (
    SELECT
        product_id,
        REGR_SLOPE(LN(qty + 1), LN(price)) FILTER (WHERE price IS NOT NULL AND price > 0) AS price_elasticity
    FROM rs_daily
    WHERE sale_date >= (CURRENT_DATE - 90)
    GROUP BY product_id
),
rs_metrics AS (
    SELECT
        fs.product_id,
        fs.first_seen_at,
        si.first_inflow_at,
        sa.first_sale_at,
        sa.sold_qty_30d,
        sa.sold_qty_90d,
        si.inflow_qty_30d,
        si.inflow_qty_90d,
        CASE
            WHEN fs.first_seen_at IS NULL OR sa.first_sale_at IS NULL THEN NULL
            ELSE EXTRACT(EPOCH FROM (sa.first_sale_at - fs.first_seen_at)) / 86400.0
        END AS days_to_first_sale,
        CASE
            WHEN fs.first_seen_at IS NULL THEN NULL
            ELSE (COALESCE(sa.sold_qty_30d, 0)::NUMERIC / 30.0)
        END AS sell_through_velocity_30d,
        CASE
            WHEN si.inflow_qty_30d IS NULL OR si.inflow_qty_30d <= 0 THEN NULL
            ELSE (COALESCE(sa.sold_qty_30d, 0)::NUMERIC / (si.inflow_qty_30d + 1))
        END AS supply_demand_ratio_30d
    FROM rs_first_seen fs
    LEFT JOIN rs_sales_agg sa ON sa.product_id = fs.product_id
    LEFT JOIN rs_inflows si ON si.product_id = fs.product_id
)
SELECT
    b.product_id,
    b.dataset_id,
    b.dataset_name,
    b.source_type,
    b.external_id,
    b.brand,
    b.category,
    b.shoe_type,
    b.gender,
    b.currency,
    b.price,
    b.avg_rating,
    b.review_count,
    b.created_at,
    b.updated_at,

    -- Review layer (minimal, SQL-only fallback)
    LEAST(1, GREATEST(0, (COALESCE(b.avg_rating, 3.0) - 1.0) / 4.0))::NUMERIC AS sentiment_score,
    CASE
        WHEN b.review_count IS NULL OR b.review_count <= 0 THEN 0
        ELSE (b.review_count::NUMERIC / GREATEST(1, EXTRACT(DAY FROM (NOW() - b.created_at))) ) * 30
    END AS review_velocity_30d_proxy,

    -- Price layer
    m.volatility_7d,
    m.volatility_30d,
    m.volatility_90d,
    m.momentum_7d,
    m.momentum_30d,
    m.momentum_90d,
    m.discount_freq_30d,
    m.discount_freq_90d,
    m.typical_change_rate_30d,

    -- Priors
    p.resolved_popularity_prior AS popularity_prior,
    p.resolved_deal_score       AS deal_score_prior,
    p.resolved_typical_price    AS typical_price_prior,
    p.resolved_level            AS priors_level,

    -- Image layer
    (i.product_id IS NOT NULL) AS has_image_embedding,
    i.embedding_model          AS image_embedding_model,
    i.cluster_id               AS image_cluster_id,
    i.image_embedding_created_at,

    -- RS-specific layer (nullable when product has no local mapping)
    b.local_product_id,
    rm.first_seen_at  AS rs_first_seen_at,
    rm.first_inflow_at AS rs_first_inflow_at,
    rm.first_sale_at  AS rs_first_sale_at,
    rm.sold_qty_30d   AS rs_sold_qty_30d,
    rm.inflow_qty_30d AS rs_inflow_qty_30d,
    rm.sell_through_velocity_30d,
    rm.supply_demand_ratio_30d,
    rm.days_to_first_sale AS median_days_to_sale_proxy,
    re.price_elasticity   AS price_elasticity_90d,

    -- Label (latest)
    ls.label_value AS sell_probability_rs_label
FROM base b
LEFT JOIN priors p
  ON p.brand_key = b.brand_key AND p.category_key = b.category_key
LEFT JOIN mom m
  ON m.product_id = b.product_id
LEFT JOIN img i
  ON i.product_id = b.product_id
LEFT JOIN rs_metrics rm
  ON rm.product_id = b.local_product_id
LEFT JOIN rs_elasticity re
  ON re.product_id = b.local_product_id
LEFT JOIN latest_sell ls
  ON ls.product_id = b.product_id AND ls.horizon_days = 30;

COMMENT ON VIEW vw_feature_store IS
'Feature store view for Open Product Training 2.0 (price/trend/review/image/RS layers).';

-- -------------------------------------------------------------
-- vw_product_training_export (v2)
-- Flat export for Python ML training
-- -------------------------------------------------------------
CREATE OR REPLACE VIEW vw_product_training_export AS
SELECT
    fs.*,
    ps.split AS dataset_split
FROM vw_feature_store fs
LEFT JOIN product_split ps ON ps.product_id = fs.product_id;

COMMENT ON VIEW vw_product_training_export IS
'Flattened export view for ML training (Open Product Training 2.0).';

