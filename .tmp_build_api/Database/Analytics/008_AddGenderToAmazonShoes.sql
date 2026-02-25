-- ============================================================
-- Add Gender column to amazon_shoe_products
-- Target DB: Analytics PostgreSQL
-- ============================================================

ALTER TABLE amazon_shoe_products
    ADD COLUMN IF NOT EXISTS "Gender" TEXT;

CREATE INDEX IF NOT EXISTS ix_amazon_shoe_products_gender
    ON amazon_shoe_products ("Gender");

COMMENT ON COLUMN amazon_shoe_products."Gender"
    IS 'Gender segment used during sync: men, women, unisex, or NULL';
