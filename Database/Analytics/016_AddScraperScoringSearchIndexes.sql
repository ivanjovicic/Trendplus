-- ==========================================================
-- 016_AddScraperScoringSearchIndexes.sql
--
-- Purpose:
-- - accelerate ILIKE '%...%' filters used by scraper scoring endpoints
-- - accelerate full-text search over items.name + items.brand
-- - reduce per-row latest-image lookup cost
-- - improve runtime scraper/shopify signal queries that filter by market
--
-- Execution notes:
-- - intended to run WITHOUT a wrapping transaction
-- - CREATE INDEX CONCURRENTLY keeps write blocking low on live systems
-- ==========================================================

CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_items_brand_trgm
    ON public.items
    USING gin (brand gin_trgm_ops);

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_items_category_trgm
    ON public.items
    USING gin (category gin_trgm_ops);

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_items_color_trgm
    ON public.items
    USING gin (color gin_trgm_ops);

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_items_name_brand_fts
    ON public.items
    USING gin (to_tsvector('simple', COALESCE(name, '') || ' ' || COALESCE(brand, '')));

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_item_images_item_created_at_desc
    ON public.item_images (item_id, created_at DESC)
    INCLUDE (image_url);

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_item_sources_market_item_id
    ON public.item_sources (market, item_id);

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_brand_name_trgm
    ON public.brand
    USING gin (name gin_trgm_ops);

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_category_name_trgm
    ON public.category
    USING gin (name gin_trgm_ops);

-- SQL_BATCH_BREAK
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_product_dataset_updated_at_price
    ON public.product (dataset_id, updated_at DESC)
    WHERE price IS NOT NULL AND price > 0;
