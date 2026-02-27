-- ==========================================================
-- 013_AddVendorSalesNivelacijaViews.sql
-- Price history + pre/post nivelacija analytics views
-- ==========================================================

-- 1) Canonical price history table (source for nivelacija date)
CREATE TABLE IF NOT EXISTS "price_history" (
    "Id" BIGSERIAL PRIMARY KEY,
    "ArticleId" INTEGER NOT NULL,
    "VendorId" INTEGER NULL,
    "OldPrice" NUMERIC(18,2) NULL,
    "NewPrice" NUMERIC(18,2) NULL,
    "EffectiveFrom" TIMESTAMP WITH TIME ZONE NULL,
    "ChangedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "SourceDnevnikId" INTEGER NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_price_history_SourceDnevnikId"
    ON "price_history" ("SourceDnevnikId");

CREATE INDEX IF NOT EXISTS "IX_price_history_ArticleId_ChangedAt"
    ON "price_history" ("ArticleId", "ChangedAt" DESC);

CREATE INDEX IF NOT EXISTS "IX_price_history_VendorId_ChangedAt"
    ON "price_history" ("VendorId", "ChangedAt" DESC);

-- 2) Backfill from DnevnikPromena (if available)
INSERT INTO "price_history" (
    "ArticleId",
    "VendorId",
    "OldPrice",
    "NewPrice",
    "EffectiveFrom",
    "ChangedAt",
    "SourceDnevnikId"
)
SELECT
    d."ArtikalId",
    COALESCE(d."DobavljacId", a."IDDobavljac"),
    d."StaraProdajnaCena",
    d."NovaProdajnaCena",
    d."Datum",
    d."Datum",
    d."Id"
FROM "DnevnikPromena" d
LEFT JOIN "Artikli" a ON a."Id" = d."ArtikalId"
WHERE d."ArtikalId" IS NOT NULL
  AND d."Datum" IS NOT NULL
  AND d."TipPromene" ILIKE '%nivel%'
ON CONFLICT ("SourceDnevnikId") DO NOTHING;

-- 3) Helpful indexes for report queries
CREATE INDEX IF NOT EXISTS "IX_prodaja_stavke_id_artikal"
    ON "prodaja_stavke" ("id_artikal");

CREATE INDEX IF NOT EXISTS "IX_prodaja_zaglavlje_datum_prodaje"
    ON "prodaja_zaglavlje" ("datum_prodaje");

CREATE INDEX IF NOT EXISTS "IX_DnevnikPromena_TipPromene_Datum"
    ON "DnevnikPromena" ("TipPromene", "Datum");

-- Drop views before recreating so column-list changes from later migrations
-- don't cause "cannot drop columns from view" on subsequent startups.
-- Dependency order: drop downstream analytics views first.
DROP VIEW IF EXISTS "vw_nivelacija_did";
DROP VIEW IF EXISTS "vw_vendor_sales_nivelacija";
DROP VIEW IF EXISTS "vw_sales_post_nivelacija";
DROP VIEW IF EXISTS "vw_sales_pre_nivelacija";

-- 4) Pre-nivelacija view (30 days before event)
CREATE OR REPLACE VIEW "vw_sales_pre_nivelacija" AS
SELECT
    ph."Id" AS price_event_id,
    COALESCE(ph."EffectiveFrom", ph."ChangedAt") AS event_date,
    COALESCE(ph."VendorId", a."IDDobavljac") AS vendor_id,
    COALESCE(d."Naziv", 'N/A') AS vendor_name,
    COALESCE(NULLIF(a."PLU", ''), a."Id"::TEXT) AS sku,
    a."Naziv" AS article_name,
    COALESCE(a."Kategorija", 'N/A') AS category,
    COALESCE(pre_stats.pre_qty, 0)::INT AS pre_qty,
    COALESCE(pre_stats.pre_revenue, 0)::NUMERIC(18,2) AS pre_revenue
FROM "price_history" ph
JOIN "Artikli" a ON a."Id" = ph."ArticleId"
LEFT JOIN "Dobavljaci" d ON d."Id" = COALESCE(ph."VendorId", a."IDDobavljac")
LEFT JOIN LATERAL (
    SELECT
        SUM(ps."kolicina") AS pre_qty,
        SUM(ps."kolicina" * ps."cena") AS pre_revenue
    FROM "prodaja_stavke" ps
    JOIN "prodaja_zaglavlje" pz ON pz."id" = ps."id_prodaja"
    WHERE ps."id_artikal" = ph."ArticleId"
      AND pz."datum_prodaje" >= COALESCE(ph."EffectiveFrom", ph."ChangedAt") - INTERVAL '30 days'
      AND pz."datum_prodaje" < COALESCE(ph."EffectiveFrom", ph."ChangedAt")
) pre_stats ON TRUE;

-- 5) Post-nivelacija view (30 days after event)
CREATE OR REPLACE VIEW "vw_sales_post_nivelacija" AS
SELECT
    ph."Id" AS price_event_id,
    COALESCE(ph."EffectiveFrom", ph."ChangedAt") AS event_date,
    COALESCE(ph."VendorId", a."IDDobavljac") AS vendor_id,
    COALESCE(d."Naziv", 'N/A') AS vendor_name,
    COALESCE(NULLIF(a."PLU", ''), a."Id"::TEXT) AS sku,
    a."Naziv" AS article_name,
    COALESCE(a."Kategorija", 'N/A') AS category,
    COALESCE(post_stats.post_qty, 0)::INT AS post_qty,
    COALESCE(post_stats.post_revenue, 0)::NUMERIC(18,2) AS post_revenue
FROM "price_history" ph
JOIN "Artikli" a ON a."Id" = ph."ArticleId"
LEFT JOIN "Dobavljaci" d ON d."Id" = COALESCE(ph."VendorId", a."IDDobavljac")
LEFT JOIN LATERAL (
    SELECT
        SUM(ps."kolicina") AS post_qty,
        SUM(ps."kolicina" * ps."cena") AS post_revenue
    FROM "prodaja_stavke" ps
    JOIN "prodaja_zaglavlje" pz ON pz."id" = ps."id_prodaja"
    WHERE ps."id_artikal" = ph."ArticleId"
      AND pz."datum_prodaje" >= COALESCE(ph."EffectiveFrom", ph."ChangedAt")
      AND pz."datum_prodaje" < COALESCE(ph."EffectiveFrom", ph."ChangedAt") + INTERVAL '30 days'
) post_stats ON TRUE;

-- 6) Consolidated vendor/article delta view
CREATE OR REPLACE VIEW "vw_vendor_sales_nivelacija" AS
SELECT
    pre.price_event_id,
    pre.event_date,
    pre.vendor_id,
    pre.vendor_name,
    pre.sku,
    pre.article_name,
    pre.category,
    pre.pre_qty,
    pre.pre_revenue,
    COALESCE(post.post_qty, 0)::INT AS post_qty,
    COALESCE(post.post_revenue, 0)::NUMERIC(18,2) AS post_revenue,
    (COALESCE(post.post_qty, 0) - pre.pre_qty)::INT AS change_qty,
    (COALESCE(post.post_revenue, 0) - pre.pre_revenue)::NUMERIC(18,2) AS change_revenue,
    CASE
        WHEN pre.pre_revenue = 0 AND COALESCE(post.post_revenue, 0) > 0 THEN 100::NUMERIC(10,2)
        WHEN pre.pre_revenue = 0 THEN 0::NUMERIC(10,2)
        ELSE ROUND(((COALESCE(post.post_revenue, 0) - pre.pre_revenue) / pre.pre_revenue) * 100, 2)
    END AS change_percent
FROM "vw_sales_pre_nivelacija" pre
LEFT JOIN "vw_sales_post_nivelacija" post
    ON post.price_event_id = pre.price_event_id;
