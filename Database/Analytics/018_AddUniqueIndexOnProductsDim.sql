-- Add unique index on ProductsDim.ProductId to support ON CONFLICT upserts
-- Run on analytics database

-- Create the unique index if it does not already exist.
CREATE UNIQUE INDEX IF NOT EXISTS ux_products_productid
ON analytics."ProductsDim"("ProductId");
