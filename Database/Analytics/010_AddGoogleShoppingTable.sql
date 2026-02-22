-- ============================================================
-- Google Shopping Products table (fetched via SerpAPI)
-- Target DB: Analytics PostgreSQL
-- ============================================================

CREATE TABLE IF NOT EXISTS google_shopping_products (
    "Id"          SERIAL PRIMARY KEY,
    "ProductId"   TEXT,
    "Title"       TEXT,
    "Brand"       TEXT,
    "Price"       NUMERIC(18,4),
    "Currency"    TEXT,
    "Rating"      REAL        NOT NULL DEFAULT 0,
    "ReviewCount" INTEGER     NOT NULL DEFAULT 0,
    "Position"    INTEGER     NOT NULL DEFAULT 0,
    "ImageUrl"    TEXT,
    "ProductUrl"  TEXT,
    "Category"    TEXT,
    "Gender"      TEXT,
    "Domain"      TEXT,
    "TrendScore"  REAL        NOT NULL DEFAULT 0,
    "LastSynced"  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "CreatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_google_shopping_product_id
    ON google_shopping_products ("ProductId")
    WHERE "ProductId" IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_google_shopping_category
    ON google_shopping_products ("Category");

CREATE INDEX IF NOT EXISTS ix_google_shopping_gender
    ON google_shopping_products ("Gender");

CREATE INDEX IF NOT EXISTS ix_google_shopping_trend_score
    ON google_shopping_products ("TrendScore" DESC);

CREATE INDEX IF NOT EXISTS ix_google_shopping_position
    ON google_shopping_products ("Position");

CREATE INDEX IF NOT EXISTS ix_google_shopping_last_synced
    ON google_shopping_products ("LastSynced" DESC);

COMMENT ON TABLE google_shopping_products IS
'Google Shopping listings fetched via SerpAPI, used for market trend analysis.';
