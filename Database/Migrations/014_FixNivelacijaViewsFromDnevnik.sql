-- ==========================================================
-- 014_FixNivelacijaViewsFromDnevnik.sql
-- Replaces pre/post nivelacija views to read directly from
-- DnevnikPromena and uses source Dnevnik IDs for accurate dates.
-- ==========================================================

-- 0) Normalize historical TipPromene values so exact queries work
--    on rows that were imported via generic DnevnikPromena mapping.
UPDATE "DnevnikPromena"
SET "TipPromene" = 'Nivelacija'
WHERE "TipPromene" ILIKE '%nivel%'
  AND "TipPromene" <> 'Nivelacija'
  AND "TipPromene" <> 'Nivelacija cena';

UPDATE "DnevnikPromena"
SET "TipPromene" = 'Ulaz robe'
WHERE ("TipPromene" ILIKE '%ulaz robe%'
    OR "TipPromene" ILIKE '%unos robe%')
  AND "TipPromene" <> 'Ulaz robe';

UPDATE "DnevnikPromena"
SET "TipPromene" = 'Povrat kupca'
WHERE "TipPromene" ILIKE '%povrat%'
  AND "TipPromene" <> 'Povrat kupca';

-- 1) Backfill imported line-level nivelacija dates from source Dnevnik ID.
--    tblNivelacije does not carry date, importer stores source ID in BrojRacuna.
UPDATE "DnevnikPromena" line
SET
    "Datum" = src."Datum",
    "IDObjekat" = COALESCE(line."IDObjekat", src."IDObjekat"),
    "DobavljacId" = COALESCE(line."DobavljacId", src."DobavljacId")
FROM "DnevnikPromena" src
WHERE line."DataOrigin" = 'access'
  AND line."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
  AND line."ArtikalId" IS NOT NULL
  AND line."BrojRacuna" ~ '^-?[0-9]+$'
  AND src."Id" = line."BrojRacuna"::INT
  AND src."Datum" IS NOT NULL;

-- Recreate views from scratch to avoid column-order/name conflicts between
-- previous versions (013) and this upgraded layout (with old_price/new_price).
DROP VIEW IF EXISTS "vw_nivelacija_did";
DROP VIEW IF EXISTS "vw_vendor_sales_nivelacija";
DROP VIEW IF EXISTS "vw_sales_post_nivelacija";
DROP VIEW IF EXISTS "vw_sales_pre_nivelacija";

-- 2) Pre-nivelacija view (30 days before event) from DnevnikPromena.
--    event_date prefers source dnevnik date (BrojRacuna -> source Id).
CREATE OR REPLACE VIEW "vw_sales_pre_nivelacija" AS
SELECT
    d."Id"::BIGINT                                     AS price_event_id,
    COALESCE(src."Datum", d."Datum")                  AS event_date,
    COALESCE(d."DobavljacId", a."IDDobavljac")        AS vendor_id,
    COALESCE(dob."Naziv", 'N/A')                      AS vendor_name,
    COALESCE(NULLIF(a."PLU", ''), a."Id"::TEXT)       AS sku,
    a."Naziv"                                          AS article_name,
    COALESCE(a."Kategorija", 'N/A')                   AS category,
    d."StaraProdajnaCena"                              AS old_price,
    d."NovaProdajnaCena"                               AS new_price,
    COALESCE(pre_stats.pre_qty, 0)::INT               AS pre_qty,
    COALESCE(pre_stats.pre_revenue, 0)::NUMERIC(18,2) AS pre_revenue
FROM "DnevnikPromena" d
JOIN "Artikli" a ON a."Id" = d."ArtikalId"
LEFT JOIN "Dobavljaci" dob ON dob."Id" = COALESCE(d."DobavljacId", a."IDDobavljac")
LEFT JOIN "DnevnikPromena" src
  ON src."Id" = CASE
      WHEN d."BrojRacuna" ~ '^-?[0-9]+$' THEN d."BrojRacuna"::INT
      ELSE NULL
  END
LEFT JOIN LATERAL (
    SELECT
        SUM(ps."kolicina")               AS pre_qty,
        SUM(ps."kolicina" * ps."cena")   AS pre_revenue
    FROM "prodaja_stavke" ps
    JOIN "prodaja_zaglavlje" pz ON pz."id" = ps."id_prodaja"
    WHERE ps."id_artikal" = d."ArtikalId"
      AND pz."datum_prodaje" >= COALESCE(src."Datum", d."Datum") - INTERVAL '30 days'
      AND pz."datum_prodaje" < COALESCE(src."Datum", d."Datum")
) pre_stats ON TRUE
WHERE d."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
  AND d."ArtikalId" IS NOT NULL
  AND COALESCE(src."Datum", d."Datum") IS NOT NULL;

-- 3) Post-nivelacija view (30 days after event) from DnevnikPromena.
CREATE OR REPLACE VIEW "vw_sales_post_nivelacija" AS
SELECT
    d."Id"::BIGINT                                      AS price_event_id,
    COALESCE(src."Datum", d."Datum")                  AS event_date,
    COALESCE(d."DobavljacId", a."IDDobavljac")        AS vendor_id,
    COALESCE(dob."Naziv", 'N/A')                      AS vendor_name,
    COALESCE(NULLIF(a."PLU", ''), a."Id"::TEXT)       AS sku,
    a."Naziv"                                          AS article_name,
    COALESCE(a."Kategorija", 'N/A')                   AS category,
    d."StaraProdajnaCena"                              AS old_price,
    d."NovaProdajnaCena"                               AS new_price,
    COALESCE(post_stats.post_qty, 0)::INT              AS post_qty,
    COALESCE(post_stats.post_revenue, 0)::NUMERIC(18,2) AS post_revenue
FROM "DnevnikPromena" d
JOIN "Artikli" a ON a."Id" = d."ArtikalId"
LEFT JOIN "Dobavljaci" dob ON dob."Id" = COALESCE(d."DobavljacId", a."IDDobavljac")
LEFT JOIN "DnevnikPromena" src
  ON src."Id" = CASE
      WHEN d."BrojRacuna" ~ '^-?[0-9]+$' THEN d."BrojRacuna"::INT
      ELSE NULL
  END
LEFT JOIN LATERAL (
    SELECT
        SUM(ps."kolicina")               AS post_qty,
        SUM(ps."kolicina" * ps."cena")   AS post_revenue
    FROM "prodaja_stavke" ps
    JOIN "prodaja_zaglavlje" pz ON pz."id" = ps."id_prodaja"
    WHERE ps."id_artikal" = d."ArtikalId"
      AND pz."datum_prodaje" >= COALESCE(src."Datum", d."Datum")
      AND pz."datum_prodaje" < COALESCE(src."Datum", d."Datum") + INTERVAL '30 days'
) post_stats ON TRUE
WHERE d."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
  AND d."ArtikalId" IS NOT NULL
  AND COALESCE(src."Datum", d."Datum") IS NOT NULL;

-- 4) Consolidated vendor/article delta view.
CREATE OR REPLACE VIEW "vw_vendor_sales_nivelacija" AS
SELECT
    pre.price_event_id,
    pre.event_date,
    pre.vendor_id,
    pre.vendor_name,
    pre.sku,
    pre.article_name,
    pre.category,
    pre.old_price,
    pre.new_price,
    pre.pre_qty,
    pre.pre_revenue,
    COALESCE(post.post_qty, 0)::INT                   AS post_qty,
    COALESCE(post.post_revenue, 0)::NUMERIC(18,2)     AS post_revenue,
    (COALESCE(post.post_qty, 0) - pre.pre_qty)::INT   AS change_qty,
    (COALESCE(post.post_revenue, 0) - pre.pre_revenue)::NUMERIC(18,2) AS change_revenue,
    CASE
        WHEN pre.pre_revenue = 0 AND COALESCE(post.post_revenue, 0) > 0 THEN 100::NUMERIC(10,2)
        WHEN pre.pre_revenue = 0 THEN 0::NUMERIC(10,2)
        ELSE ROUND(((COALESCE(post.post_revenue, 0) - pre.pre_revenue) / pre.pre_revenue) * 100, 2)
    END AS change_percent
FROM "vw_sales_pre_nivelacija" pre
LEFT JOIN "vw_sales_post_nivelacija" post
    ON post.price_event_id = pre.price_event_id;
