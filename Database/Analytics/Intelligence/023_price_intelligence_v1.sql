-- ==========================================================
-- 023_price_intelligence_v1.sql
-- Price intelligence layer.
--
-- Canonical price mapping in this platform:
-- - net_price  -> ProductsDim.SalePrice (fallback Artikli.ProdajnaCena via sync)
-- - list_price -> ProductsDim.FirstSalePrice
-- - cost       -> ProductsDim.PurchasePriceRsd, then PurchasePrice
--
-- Brand note:
-- - ProductsDim.Brand exists but is not reliably populated by every ingest path.
-- - price_index_vs_brand therefore falls back to supplier-based brand_surrogate
--   when a canonical brand label is missing.
-- ==========================================================

DO $$
DECLARE
    _actual text[];
    _expect text[] := ARRAY[
        'article_id',
        'price_date',
        'category',
        'brand_key',
        'net_price',
        'list_price',
        'cost',
        'price_index_vs_category',
        'price_index_vs_brand',
        'discount_depth',
        'margin_pct'
    ];
BEGIN
    SELECT array_agg(c.column_name::text ORDER BY c.ordinal_position)
      INTO _actual
      FROM information_schema.columns c
     WHERE c.table_schema = 'analytics_intel'
       AND c.table_name = 'vw_price_intelligence_v1';

    IF _actual IS NOT NULL AND _actual IS DISTINCT FROM _expect THEN
        RAISE NOTICE '023: vw_price_intelligence_v1 column structure changed - dropping cache + view';
        DROP MATERIALIZED VIEW IF EXISTS analytics_intel.mv_price_intelligence_v1_cache;
        DROP VIEW IF EXISTS analytics_intel.vw_price_intelligence_v1 CASCADE;
    END IF;
END $$;

CREATE OR REPLACE VIEW analytics_intel.vw_price_intelligence_v1 AS
WITH latest_suppliers AS (
    SELECT DISTINCT ON (sd."SupplierId")
        sd."SupplierId" AS supplier_id,
        NULLIF(BTRIM(sd."Naziv"), '') AS supplier_name
    FROM "SuppliersDim" sd
    ORDER BY sd."SupplierId", sd."UpdatedAt" DESC
),
latest_products AS (
    SELECT DISTINCT ON (pd."ProductId")
        pd."ProductId" AS article_id,
        pd."Timestamp"::date AS price_date,
        COALESCE(NULLIF(BTRIM(pd."Category"), ''), 'Uncategorized') AS category,
        COALESCE(
            NULLIF(BTRIM(pd."Brand"), ''),
            ls.supplier_name,
            CASE
                WHEN pd."SupplierId" IS NOT NULL THEN 'supplier:' || pd."SupplierId"::text
                ELSE 'unknown'
            END
        ) AS brand_key,
        COALESCE(pd."SalePrice", 0)::numeric(18,4) AS net_price,
        COALESCE(pd."FirstSalePrice", pd."SalePrice", 0)::numeric(18,4) AS list_price,
        COALESCE(pd."PurchasePriceRsd", pd."PurchasePrice", 0)::numeric(18,4) AS cost
    FROM "ProductsDim" pd
    LEFT JOIN latest_suppliers ls
      ON ls.supplier_id = pd."SupplierId"
    ORDER BY pd."ProductId", pd."Timestamp" DESC
),
priced_products AS (
    SELECT *
    FROM latest_products
    WHERE net_price > 0
)
SELECT
    pp.article_id,
    pp.price_date,
    pp.category,
    pp.brand_key,
    ROUND(pp.net_price, 4) AS net_price,
    ROUND(pp.list_price, 4) AS list_price,
    ROUND(pp.cost, 4) AS cost,
    ROUND(
        pp.net_price
        / NULLIF(
            AVG(pp.net_price) OVER (
                PARTITION BY pp.category
            ),
            0
        ),
        4
    ) AS price_index_vs_category,
    ROUND(
        pp.net_price
        / NULLIF(
            AVG(pp.net_price) OVER (
                PARTITION BY pp.brand_key
            ),
            0
        ),
        4
    ) AS price_index_vs_brand,
    ROUND(
        CASE
            WHEN pp.list_price <= 0 THEN 0::numeric
            ELSE (pp.list_price - pp.net_price) / NULLIF(pp.list_price, 0)
        END,
        4
    ) AS discount_depth,
    ROUND(
        CASE
            WHEN pp.net_price <= 0 THEN NULL
            ELSE (pp.net_price - pp.cost) / NULLIF(pp.net_price, 0)
        END,
        4
    ) AS margin_pct
FROM priced_products pp;

COMMENT ON VIEW analytics_intel.vw_price_intelligence_v1 IS
'Versioned price intelligence view over the latest product dimension snapshot. Category and brand price indices are computed as relative price-to-peer averages using the platform canonical net/list/cost mapping.';

COMMENT ON COLUMN analytics_intel.vw_price_intelligence_v1.brand_key IS
'Canonical brand when available; otherwise a supplier-based surrogate used to keep brand-relative pricing comparable even when raw brand metadata is sparse.';

COMMENT ON COLUMN analytics_intel.vw_price_intelligence_v1.discount_depth IS
'Relative markdown depth calculated as (list_price - net_price) / list_price.';

COMMENT ON COLUMN analytics_intel.vw_price_intelligence_v1.margin_pct IS
'Gross margin percentage estimated as (net_price - cost) / net_price using ProductsDim purchase cost fields.';

CREATE MATERIALIZED VIEW IF NOT EXISTS analytics_intel.mv_price_intelligence_v1_cache AS
SELECT *
FROM analytics_intel.vw_price_intelligence_v1;

CREATE UNIQUE INDEX IF NOT EXISTS idx_mv_price_intelligence_v1_cache_pk
    ON analytics_intel.mv_price_intelligence_v1_cache (article_id, price_date);

CREATE INDEX IF NOT EXISTS idx_mv_price_intelligence_v1_cache_category
    ON analytics_intel.mv_price_intelligence_v1_cache (category, price_date DESC);

COMMENT ON MATERIALIZED VIEW analytics_intel.mv_price_intelligence_v1_cache IS
'Materialized cache for analytics_intel.vw_price_intelligence_v1. Unique key supports REFRESH MATERIALIZED VIEW CONCURRENTLY.';
