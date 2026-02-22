-- ============================================================
-- eBay Shoe Products table (fetched via eBay Browse API)
-- Target DB: Analytics PostgreSQL
-- ============================================================

CREATE TABLE IF NOT EXISTS ebay_shoe_products (
    "Id"          SERIAL PRIMARY KEY,
    "EbayItemId"  TEXT        NOT NULL,
    "Name"        TEXT,
    "Brand"       TEXT,
    "Condition"   TEXT,
    "Price"       NUMERIC(18,4),
    "Currency"    TEXT,
    "Rating"      REAL        NOT NULL DEFAULT 0,
    "ReviewCount" INTEGER     NOT NULL DEFAULT 0,
    "ImageUrl"    TEXT,
    "ProductUrl"  TEXT,
    "Category"    TEXT,
    "Marketplace" TEXT,
    "LastSynced"  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "CreatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_ebay_shoe_products_item_id UNIQUE ("EbayItemId")
);

CREATE INDEX IF NOT EXISTS ix_ebay_shoe_products_category
    ON ebay_shoe_products ("Category");

CREATE INDEX IF NOT EXISTS ix_ebay_shoe_products_rating
    ON ebay_shoe_products ("Rating" DESC);

CREATE INDEX IF NOT EXISTS ix_ebay_shoe_products_last_synced
    ON ebay_shoe_products ("LastSynced" DESC);

COMMENT ON TABLE ebay_shoe_products IS
'eBay shoe listings fetched via Browse API, used for market trend analysis.';
