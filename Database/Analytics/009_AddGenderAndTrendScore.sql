-- ============================================================
-- Add Gender column to ebay_shoe_products
-- Add TrendScore column to both amazon and ebay tables
-- Target DB: Analytics PostgreSQL
-- ============================================================

-- eBay: Gender column
ALTER TABLE ebay_shoe_products
    ADD COLUMN IF NOT EXISTS "Gender" TEXT;

-- eBay: TrendScore column
ALTER TABLE ebay_shoe_products
    ADD COLUMN IF NOT EXISTS "TrendScore" REAL NOT NULL DEFAULT 0;

-- Amazon: TrendScore column
ALTER TABLE amazon_shoe_products
    ADD COLUMN IF NOT EXISTS "TrendScore" REAL NOT NULL DEFAULT 0;

-- Indexes
CREATE INDEX IF NOT EXISTS ix_ebay_shoe_products_gender
    ON ebay_shoe_products ("Gender");

CREATE INDEX IF NOT EXISTS ix_ebay_shoe_products_trend_score
    ON ebay_shoe_products ("TrendScore" DESC);

CREATE INDEX IF NOT EXISTS ix_amazon_shoe_products_trend_score
    ON amazon_shoe_products ("TrendScore" DESC);

COMMENT ON COLUMN ebay_shoe_products."Gender"
    IS 'Gender segment: men, women, unisex, or NULL';

COMMENT ON COLUMN ebay_shoe_products."TrendScore"
    IS 'Weighted trend score: rating * log10(reviews+2) * priceFactor';

COMMENT ON COLUMN amazon_shoe_products."TrendScore"
    IS 'Weighted trend score: rating * log10(reviews+2) * priceFactor';
