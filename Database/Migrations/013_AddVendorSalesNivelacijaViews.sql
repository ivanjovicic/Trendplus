-- ==========================================================
-- 013_AddVendorSalesNivelacijaViews.sql
-- Price history + pre/post nivelacija analytics views
-- ==========================================================

-- 1️⃣ Create price_history table
CREATE TABLE IF NOT EXISTS price_history (
    id BIGSERIAL PRIMARY KEY,
    article_id INTEGER NOT NULL,
    vendor_id INTEGER NULL,
    old_price NUMERIC(18,4),
    new_price NUMERIC(18,4),
    effective_from TIMESTAMPTZ NOT NULL,
    changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    source_dnevnik_id INTEGER UNIQUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_price_history_article
        FOREIGN KEY (article_id)
        REFERENCES "Artikli"("Id")
        ON DELETE CASCADE,
    CONSTRAINT fk_price_history_vendor
        FOREIGN KEY (vendor_id)
        REFERENCES "Dobavljaci"("Id")
        ON DELETE SET NULL
);

-- 2️⃣ Reconcile schema: table may have been created with PascalCase columns (older schema)
DO $reconcile$
DECLARE
    has_snake BOOLEAN;
BEGIN
    -- Check if snake_case column exists
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'price_history' AND column_name = 'article_id'
    ) INTO has_snake;

    IF NOT has_snake THEN
        ALTER TABLE price_history ADD COLUMN IF NOT EXISTS article_id INTEGER;
        ALTER TABLE price_history ADD COLUMN IF NOT EXISTS vendor_id INTEGER;
        ALTER TABLE price_history ADD COLUMN IF NOT EXISTS old_price NUMERIC(18,4);
        ALTER TABLE price_history ADD COLUMN IF NOT EXISTS new_price NUMERIC(18,4);
        ALTER TABLE price_history ADD COLUMN IF NOT EXISTS effective_from TIMESTAMPTZ;
        ALTER TABLE price_history ADD COLUMN IF NOT EXISTS changed_at TIMESTAMPTZ;
        ALTER TABLE price_history ADD COLUMN IF NOT EXISTS source_dnevnik_id INTEGER;
        ALTER TABLE price_history ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

        -- Copy data from PascalCase columns (if they exist)
        BEGIN
            UPDATE price_history SET
                article_id       = "ArticleId",
                vendor_id        = "VendorId",
                old_price        = "OldPrice",
                new_price        = "NewPrice",
                effective_from   = "EffectiveFrom",
                changed_at       = "ChangedAt",
                source_dnevnik_id = "SourceDnevnikId",
                created_at       = "CreatedAt"
            WHERE article_id IS NULL;
        EXCEPTION WHEN undefined_column THEN NULL;
        END;
    END IF;
END
$reconcile$;

-- Create indexes for price_history
CREATE INDEX IF NOT EXISTS idx_price_history_article_date
ON price_history (article_id, effective_from DESC);

CREATE INDEX IF NOT EXISTS idx_price_history_vendor_date
ON price_history (vendor_id, effective_from DESC);

-- 3️⃣ Ensure UNIQUE constraint on source_dnevnik_id (needed for ON CONFLICT)
DO $ensure_unique$
BEGIN
    ALTER TABLE price_history
        ADD CONSTRAINT price_history_source_dnevnik_id_key UNIQUE (source_dnevnik_id);
EXCEPTION WHEN duplicate_table OR duplicate_object THEN
    NULL; -- constraint already exists, nothing to do
END
$ensure_unique$;

-- 3️⃣ Backfill price_history table
INSERT INTO price_history (
        article_id,
        vendor_id,
        old_price,
        new_price,
        effective_from,
        changed_at,
        source_dnevnik_id
)
SELECT
        d."ArtikalId",
        COALESCE(d."DobavljacId", a."IDDobavljac"),
        d."StaraProdajnaCena"::NUMERIC(18,4),
        d."NovaProdajnaCena"::NUMERIC(18,4),
        d."Datum",
        d."Datum",
        d."Id"
FROM "DnevnikPromena" d
LEFT JOIN "Artikli" a ON a."Id" = d."ArtikalId"
WHERE d."ArtikalId" IS NOT NULL
    AND d."Datum" IS NOT NULL
    AND d."TipPromene" IN ('Nivelacija','Nivelacija cena')
ON CONFLICT (source_dnevnik_id) DO NOTHING;

-- 4️⃣ Create pre/post nivelacija views (supports both snake_case and PascalCase price_history schema)
DO $create_views$
DECLARE
    has_snake_id BOOLEAN;
BEGIN
    SELECT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'price_history'
          AND column_name = 'id'
    ) INTO has_snake_id;

    IF has_snake_id THEN
        EXECUTE $sql$
        DROP VIEW IF EXISTS vw_vendor_sales_nivelacija CASCADE;
        DROP VIEW IF EXISTS vw_sales_post_nivelacija CASCADE;
        DROP VIEW IF EXISTS vw_sales_pre_nivelacija CASCADE;
        CREATE OR REPLACE VIEW vw_sales_pre_nivelacija AS
        WITH sales_daily AS (
            SELECT
                ps."id_artikal" AS article_id,
                pz."datum_prodaje"::date AS day,
                SUM(ps."kolicina")::NUMERIC AS units,
                SUM(ps."kolicina" * ps."cena")::NUMERIC(18,2) AS revenue
            FROM "prodaja_stavke" ps
            JOIN "prodaja_zaglavlje" pz
                ON pz."id" = ps."id_prodaja"
            GROUP BY ps."id_artikal", pz."datum_prodaje"::date
        )
        SELECT
            ph.id AS price_event_id,
            ph.effective_from::date AS event_date,
            ph.vendor_id,
            COALESCE(d."Naziv",'N/A') AS vendor_name,
            ph.article_id,
            COALESCE(NULLIF(a."PLU",''), ph.article_id::text) AS sku,
            a."Naziv" AS article_name,
            COALESCE(a."Kategorija",'N/A') AS category,
            ph.old_price,
            ph.new_price,

            COALESCE(SUM(s.units),0) AS pre_qty,
            COALESCE(SUM(s.revenue),0) AS pre_revenue,

            LEAST(COUNT(DISTINCT s.day)/30.0,1) AS coverage_pre30,
            COUNT(DISTINCT s.day) AS valid_days_pre30

        FROM price_history ph
        JOIN "Artikli" a ON a."Id" = ph.article_id
        LEFT JOIN "Dobavljaci" d ON d."Id" = ph.vendor_id
        LEFT JOIN sales_daily s
            ON s.article_id = ph.article_id
           AND s.day >= ph.effective_from - INTERVAL '30 days'
           AND s.day <  ph.effective_from

        GROUP BY
            ph.id, ph.effective_from,
            ph.vendor_id, d."Naziv",
            ph.article_id, a."PLU",
            a."Naziv", a."Kategorija",
            ph.old_price, ph.new_price;

        DROP VIEW IF EXISTS vw_sales_post_nivelacija CASCADE;
        CREATE OR REPLACE VIEW vw_sales_post_nivelacija AS
        WITH sales_daily AS (
            SELECT
                ps."id_artikal" AS article_id,
                pz."datum_prodaje"::date AS day,
                SUM(ps."kolicina")::NUMERIC AS units,
                SUM(ps."kolicina" * ps."cena")::NUMERIC(18,2) AS revenue
            FROM "prodaja_stavke" ps
            JOIN "prodaja_zaglavlje" pz
                ON pz."id" = ps."id_prodaja"
            GROUP BY ps."id_artikal", pz."datum_prodaje"::date
        )
        SELECT
            ph.id AS price_event_id,
            ph.effective_from::date AS event_date,
            ph.vendor_id,
            COALESCE(d."Naziv",'N/A') AS vendor_name,
            ph.article_id,
            COALESCE(NULLIF(a."PLU",''), ph.article_id::text) AS sku,
            a."Naziv" AS article_name,
            COALESCE(a."Kategorija",'N/A') AS category,
            ph.old_price,
            ph.new_price,

            COALESCE(SUM(s.units),0) AS post_qty,
            COALESCE(SUM(s.revenue),0) AS post_revenue,

            LEAST(COUNT(DISTINCT s.day)/30.0,1) AS coverage_post30,
            COUNT(DISTINCT s.day) AS valid_days_post30

        FROM price_history ph
        JOIN "Artikli" a ON a."Id" = ph.article_id
        LEFT JOIN "Dobavljaci" d ON d."Id" = ph.vendor_id
        LEFT JOIN sales_daily s
            ON s.article_id = ph.article_id
           AND s.day >= ph.effective_from
           AND s.day <  ph.effective_from + INTERVAL '30 days'

        GROUP BY
            ph.id, ph.effective_from,
            ph.vendor_id, d."Naziv",
            ph.article_id, a."PLU",
            a."Naziv", a."Kategorija",
            ph.old_price, ph.new_price;

        DROP VIEW IF EXISTS vw_vendor_sales_nivelacija CASCADE;
        CREATE OR REPLACE VIEW vw_vendor_sales_nivelacija AS
        SELECT
            pre.price_event_id,
            pre.event_date,
            pre.vendor_id,
            pre.vendor_name,
            pre.article_id,
            pre.sku,
            pre.article_name,
            pre.category,

            pre.pre_qty,
            post.post_qty,
            pre.pre_revenue,
            post.post_revenue,

            pre.coverage_pre30,
            post.coverage_post30,

            (post.post_qty - pre.pre_qty) AS change_qty,
            (post.post_revenue - pre.pre_revenue) AS change_revenue,

            CASE
                WHEN pre.pre_qty = 0 AND post.post_qty > 0 THEN 100
                WHEN pre.pre_qty = 0 THEN 0
                ELSE ROUND(
                    ((post.post_qty - pre.pre_qty)
                     / NULLIF(pre.pre_qty,0)) * 100, 2)
            END AS change_percent_qty,

            CASE
                WHEN pre.pre_revenue = 0 AND post.post_revenue > 0 THEN 100
                WHEN pre.pre_revenue = 0 THEN 0
                ELSE ROUND(
                    ((post.post_revenue - pre.pre_revenue)
                     / NULLIF(pre.pre_revenue,0)) * 100, 2)
            END AS change_percent_revenue

        FROM vw_sales_pre_nivelacija pre
        LEFT JOIN vw_sales_post_nivelacija post
          ON pre.price_event_id = post.price_event_id;
        $sql$;
    ELSE
        EXECUTE $sql$
        DROP VIEW IF EXISTS vw_vendor_sales_nivelacija CASCADE;
        DROP VIEW IF EXISTS vw_sales_post_nivelacija CASCADE;
        DROP VIEW IF EXISTS vw_sales_pre_nivelacija CASCADE;
        CREATE OR REPLACE VIEW vw_sales_pre_nivelacija AS
        WITH sales_daily AS (
            SELECT
                ps."id_artikal" AS article_id,
                pz."datum_prodaje"::date AS day,
                SUM(ps."kolicina")::NUMERIC AS units,
                SUM(ps."kolicina" * ps."cena")::NUMERIC(18,2) AS revenue
            FROM "prodaja_stavke" ps
            JOIN "prodaja_zaglavlje" pz
                ON pz."id" = ps."id_prodaja"
            GROUP BY ps."id_artikal", pz."datum_prodaje"::date
        )
        SELECT
            ph."Id" AS price_event_id,
            ph."EffectiveFrom"::date AS event_date,
            ph."VendorId" AS vendor_id,
            COALESCE(d."Naziv",'N/A') AS vendor_name,
            ph."ArticleId" AS article_id,
            COALESCE(NULLIF(a."PLU",''), ph."ArticleId"::text) AS sku,
            a."Naziv" AS article_name,
            COALESCE(a."Kategorija",'N/A') AS category,
            ph."OldPrice" AS old_price,
            ph."NewPrice" AS new_price,

            COALESCE(SUM(s.units),0) AS pre_qty,
            COALESCE(SUM(s.revenue),0) AS pre_revenue,

            LEAST(COUNT(DISTINCT s.day)/30.0,1) AS coverage_pre30,
            COUNT(DISTINCT s.day) AS valid_days_pre30

        FROM price_history ph
        JOIN "Artikli" a ON a."Id" = ph."ArticleId"
        LEFT JOIN "Dobavljaci" d ON d."Id" = ph."VendorId"
        LEFT JOIN sales_daily s
            ON s.article_id = ph."ArticleId"
           AND s.day >= ph."EffectiveFrom" - INTERVAL '30 days'
           AND s.day <  ph."EffectiveFrom"

        GROUP BY
            ph."Id", ph."EffectiveFrom",
            ph."VendorId", d."Naziv",
            ph."ArticleId", a."PLU",
            a."Naziv", a."Kategorija",
            ph."OldPrice", ph."NewPrice";

        DROP VIEW IF EXISTS vw_sales_post_nivelacija CASCADE;
        CREATE OR REPLACE VIEW vw_sales_post_nivelacija AS
        WITH sales_daily AS (
            SELECT
                ps."id_artikal" AS article_id,
                pz."datum_prodaje"::date AS day,
                SUM(ps."kolicina")::NUMERIC AS units,
                SUM(ps."kolicina" * ps."cena")::NUMERIC(18,2) AS revenue
            FROM "prodaja_stavke" ps
            JOIN "prodaja_zaglavlje" pz
                ON pz."id" = ps."id_prodaja"
            GROUP BY ps."id_artikal", pz."datum_prodaje"::date
        )
        SELECT
            ph."Id" AS price_event_id,
            ph."EffectiveFrom"::date AS event_date,
            ph."VendorId" AS vendor_id,
            COALESCE(d."Naziv",'N/A') AS vendor_name,
            ph."ArticleId" AS article_id,
            COALESCE(NULLIF(a."PLU",''), ph."ArticleId"::text) AS sku,
            a."Naziv" AS article_name,
            COALESCE(a."Kategorija",'N/A') AS category,
            ph."OldPrice" AS old_price,
            ph."NewPrice" AS new_price,

            COALESCE(SUM(s.units),0) AS post_qty,
            COALESCE(SUM(s.revenue),0) AS post_revenue,

            LEAST(COUNT(DISTINCT s.day)/30.0,1) AS coverage_post30,
            COUNT(DISTINCT s.day) AS valid_days_post30

        FROM price_history ph
        JOIN "Artikli" a ON a."Id" = ph."ArticleId"
        LEFT JOIN "Dobavljaci" d ON d."Id" = ph."VendorId"
        LEFT JOIN sales_daily s
            ON s.article_id = ph."ArticleId"
           AND s.day >= ph."EffectiveFrom"
           AND s.day <  ph."EffectiveFrom" + INTERVAL '30 days'

        GROUP BY
            ph."Id", ph."EffectiveFrom",
            ph."VendorId", d."Naziv",
            ph."ArticleId", a."PLU",
            a."Naziv", a."Kategorija",
            ph."OldPrice", ph."NewPrice";

        DROP VIEW IF EXISTS vw_vendor_sales_nivelacija CASCADE;
        CREATE OR REPLACE VIEW vw_vendor_sales_nivelacija AS
        SELECT
            pre.price_event_id,
            pre.event_date,
            pre.vendor_id,
            pre.vendor_name,
            pre.article_id,
            pre.sku,
            pre.article_name,
            pre.category,

            pre.pre_qty,
            post.post_qty,
            pre.pre_revenue,
            post.post_revenue,

            pre.coverage_pre30,
            post.coverage_post30,

            (post.post_qty - pre.pre_qty) AS change_qty,
            (post.post_revenue - pre.pre_revenue) AS change_revenue,

            CASE
                WHEN pre.pre_qty = 0 AND post.post_qty > 0 THEN 100
                WHEN pre.pre_qty = 0 THEN 0
                ELSE ROUND(
                    ((post.post_qty - pre.pre_qty)
                     / NULLIF(pre.pre_qty,0)) * 100, 2)
            END AS change_percent_qty,

            CASE
                WHEN pre.pre_revenue = 0 AND post.post_revenue > 0 THEN 100
                WHEN pre.pre_revenue = 0 THEN 0
                ELSE ROUND(
                    ((post.post_revenue - pre.pre_revenue)
                     / NULLIF(pre.pre_revenue,0)) * 100, 2)
            END AS change_percent_revenue

        FROM vw_sales_pre_nivelacija pre
        LEFT JOIN vw_sales_post_nivelacija post
          ON pre.price_event_id = post.price_event_id;
        $sql$;
    END IF;
END
$create_views$;

-- 7️⃣ Additional indexes
CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_artikal_prodaja
ON "prodaja_stavke" ("id_artikal","id_prodaja");

CREATE INDEX IF NOT EXISTS idx_prodaja_zaglavlje_id_datum
ON "prodaja_zaglavlje" ("id","datum_prodaje");

-- 7️⃣ Additional indexes
CREATE INDEX IF NOT EXISTS idx_prodaja_stavke_artikal_prodaja
ON "prodaja_stavke" ("id_artikal","id_prodaja");

CREATE INDEX IF NOT EXISTS idx_prodaja_zaglavlje_id_datum
ON "prodaja_zaglavlje" ("id","datum_prodaje");
