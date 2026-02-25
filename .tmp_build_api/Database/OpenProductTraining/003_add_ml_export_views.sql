-- =============================================================
-- open_product_training views for ML export and runtime signals
-- =============================================================

-- Latest numeric labels per product (one row per product).
CREATE OR REPLACE VIEW vw_product_latest_labels AS
WITH ranked AS (
    SELECT
        tl.product_id,
        tl.label_type,
        tl.value_numeric,
        tl.created_at,
        tl.id,
        ROW_NUMBER() OVER (
            PARTITION BY tl.product_id, tl.label_type
            ORDER BY tl.created_at DESC, tl.id DESC
        ) AS rn
    FROM training_label tl
)
SELECT
    r.product_id,
    MAX(r.value_numeric) FILTER (WHERE r.label_type = 'popularity_prior') AS popularity_prior,
    MAX(r.value_numeric) FILTER (WHERE r.label_type = 'deal_score') AS deal_score,
    MAX(r.created_at) AS labels_created_at
FROM ranked r
WHERE r.rn = 1
GROUP BY r.product_id;

COMMENT ON VIEW vw_product_latest_labels IS
'Latest popularity_prior and deal_score labels per product.';

-- Flat export view for Python/XGBoost training.
CREATE OR REPLACE VIEW vw_product_training_export AS
SELECT
    p.id AS product_id,
    d.id AS dataset_id,
    d.name AS dataset_name,
    d.source_type,
    p.external_id,
    b.name AS brand,
    c.name AS category,
    p.title,
    p.description,
    p.gender,
    p.shoe_type,
    p.currency,
    p.price,
    COALESCE(rs.avg_rating, p.avg_rating) AS avg_rating,
    COALESCE(rs.rating_count, p.review_count) AS review_count,
    p.main_image_url,
    ps.split AS dataset_split,
    ll.popularity_prior,
    ll.deal_score,
    ll.labels_created_at,
    p.created_at,
    p.updated_at
FROM product p
JOIN dataset d ON d.id = p.dataset_id
LEFT JOIN brand b ON b.id = p.brand_id
LEFT JOIN category c ON c.id = p.category_id
LEFT JOIN product_review_stats rs ON rs.product_id = p.id
LEFT JOIN product_split ps ON ps.product_id = p.id
LEFT JOIN vw_product_latest_labels ll ON ll.product_id = p.id;

COMMENT ON VIEW vw_product_training_export IS
'Flattened product + latest labels view intended for ML export (Python/XGBoost).';

-- Runtime aggregation by (brand, shoe_type) used by live trend scoring.
CREATE OR REPLACE VIEW vw_brand_shoe_runtime_priors AS
SELECT
    LOWER(TRIM(COALESCE(b.name, ''))) AS brand_key,
    LOWER(TRIM(COALESCE(p.shoe_type, ''))) AS shoe_type_key,
    ROUND(AVG(COALESCE(ll.popularity_prior, 0)), 2) AS popularity_prior_score,
    PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY p.price) AS typical_price,
    COUNT(*)::INT AS sample_size
FROM product p
JOIN brand b ON b.id = p.brand_id
LEFT JOIN vw_product_latest_labels ll ON ll.product_id = p.id
WHERE p.price IS NOT NULL
  AND p.shoe_type IS NOT NULL
  AND TRIM(p.shoe_type) <> ''
GROUP BY
    LOWER(TRIM(COALESCE(b.name, ''))),
    LOWER(TRIM(COALESCE(p.shoe_type, '')));

COMMENT ON VIEW vw_brand_shoe_runtime_priors IS
'Aggregated popularity prior and typical price by (brand, shoe_type) for runtime scoring.';
