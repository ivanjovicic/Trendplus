-- ============================================================
-- Amazon Shoe Products table (fetched via SerpAPI)
-- Target DB: Analytics PostgreSQL
-- ============================================================

CREATE TABLE IF NOT EXISTS amazon_shoe_products (
    "Id"            SERIAL PRIMARY KEY,
    "Asin"          TEXT        NOT NULL,
    "Name"          TEXT,
    "Brand"         TEXT,
    "Price"         NUMERIC(18,4),
    "OriginalPrice" NUMERIC(18,4),
    "Currency"      TEXT,
    "Rating"        REAL        NOT NULL DEFAULT 0,
    "ReviewCount"   INTEGER     NOT NULL DEFAULT 0,
    "ImageUrl"      TEXT,
    "ProductUrl"    TEXT,
    "Category"      TEXT,
    "Domain"        TEXT,
    "LastSynced"    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "CreatedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_amazon_shoe_products_asin UNIQUE ("Asin")
);

CREATE INDEX IF NOT EXISTS ix_amazon_shoe_products_category
    ON amazon_shoe_products ("Category");

CREATE INDEX IF NOT EXISTS ix_amazon_shoe_products_rating
    ON amazon_shoe_products ("Rating" DESC);

CREATE INDEX IF NOT EXISTS ix_amazon_shoe_products_last_synced
    ON amazon_shoe_products ("LastSynced" DESC);

COMMENT ON TABLE amazon_shoe_products IS
'Amazon shoe listings fetched via SerpAPI, used for market trend analysis.';
