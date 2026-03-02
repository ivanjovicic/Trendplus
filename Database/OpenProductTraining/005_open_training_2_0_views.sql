-- =============================================================
-- Open Product Training 2.0 views
-- Created: 2026-02-28
-- Idempotent: CREATE OR REPLACE
-- =============================================================

-- Ensure idempotency by dropping views if they exist
DROP VIEW IF EXISTS vw_product_momentum;
DROP VIEW IF EXISTS vw_brand_category_priors_enriched;

-- Recreate vw_product_momentum with improved null handling and comments
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

        AVG(CASE WHEN pct_change < 0 THEN 1 ELSE 0 END) FILTER (WHERE collected_at >= NOW() - INTERVAL '30 days')::NUMERIC AS discount_freq_30d,
        AVG(CASE WHEN pct_change < 0 THEN 1 ELSE 0 END) FILTER (WHERE collected_at >= NOW() - INTERVAL '90 days')::NUMERIC AS discount_freq_90d
    FROM rets
    GROUP BY product_id
)
SELECT
    a.obs_90d,
    a.volatility_7d,
    a.volatility_30d,
    a.volatility_90d,
    -- Calculate momentum metrics with simplified null handling
    COALESCE((l7.last_price_7d - f7.first_price_7d) / NULLIF(f7.first_price_7d, 0), NULL) AS momentum_7d,
    COALESCE((l30.last_price_30d - f30.first_price_30d) / NULLIF(f30.first_price_30d, 0), NULL) AS momentum_30d,
    COALESCE((l90.last_price_90d - f90.first_price_90d) / NULLIF(f90.first_price_90d, 0), NULL) AS momentum_90d,
    a.discount_freq_30d,
    a.discount_freq_90d,
    a.typical_change_rate_30d
FROM agg a
LEFT JOIN window_prices wp ON wp.product_id = a.product_id
LEFT JOIN first_7 f7 ON f7.product_id = a.product_id
LEFT JOIN last_7 l7 ON l7.product_id = a.product_id
LEFT JOIN first_30 f30 ON f30.product_id = a.product_id
LEFT JOIN last_30 l30 ON l30.product_id = a.product_id
LEFT JOIN first_90 f90 ON f90.product_id = a.product_id
LEFT JOIN last_90 l90 ON l90.product_id = a.product_id;

COMMENT ON VIEW vw_product_momentum IS
'Price-derived momentum/volatility features from open_product_training.product_price_history.';

-- Recreate vw_brand_category_priors_enriched with parameterized thresholds and comments
CREATE OR REPLACE VIEW vw_brand_category_priors_enriched AS
WITH base AS (
    SELECT
        LOWER(TRIM(COALESCE(b.name, ''))) AS brand_key,
        LOWER(TRIM(COALESCE(NULLIF(p.shoe_type, ''), c.name, ''))) AS category_key,
        ROUND(AVG(COALESCE(ll.popularity_prior, 0)), 2) AS popularity_prior_score,
        ROUND(AVG(COALESCE(ll.deal_score, 0)), 2) AS deal_score,
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
        ROUND(AVG(deal_score), 2) AS deal_score,
        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY typical_price) AS typical_price,
        SUM(sample_size)::INT AS sample_size
    FROM base
    GROUP BY 1
),
by_brand AS (
    SELECT
        brand_key,
        ROUND(AVG(popularity_prior_score), 2) AS popularity_prior_score,
        ROUND(AVG(deal_score), 2) AS deal_score,
        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY typical_price) AS typical_price,
        SUM(sample_size)::INT AS sample_size
    FROM base
    GROUP BY 1
),
global AS (
    SELECT
        ROUND(AVG(popularity_prior_score), 2) AS popularity_prior_score,
        ROUND(AVG(deal_score), 2) AS deal_score,
        PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY typical_price) AS typical_price,
        SUM(sample_size)::INT AS sample_size
    FROM base
)
SELECT
    b.brand_key,
    b.category_key,
    b.popularity_prior_score AS brand_category_popularity_prior,
    b.deal_score AS brand_category_deal_score,
    b.typical_price AS brand_category_typical_price,
    b.sample_size AS brand_category_sample_size,
    c.popularity_prior_score AS category_popularity_prior,
    c.deal_score AS category_deal_score,
    c.typical_price AS category_typical_price,
    c.sample_size AS category_sample_size,
    br.popularity_prior_score AS brand_popularity_prior,
    br.deal_score AS brand_deal_score,
    br.typical_price AS brand_typical_price,
    br.sample_size AS brand_sample_size,
    g.popularity_prior_score AS global_popularity_prior,
    g.deal_score AS global_deal_score,
    g.typical_price AS global_typical_price,
    g.sample_size AS global_sample_size,
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
    END AS resolved_typical_price
FROM base b
LEFT JOIN by_category c ON b.category_key = c.category_key
LEFT JOIN by_brand br ON b.brand_key = br.brand_key
CROSS JOIN global g;

