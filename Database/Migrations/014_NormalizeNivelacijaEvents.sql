-- ==========================================================
-- 014_NormalizeNivelacijaEvents.sql
-- Data-only repair for imported nivelacija events.
--
-- View ownership is canonical in:
--   Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql
-- The legacy 014_FixNivelacijaViewsFromDnevnik.sql remains historical
-- evidence and is intentionally not executed by startup.
-- ==========================================================

-- Normalize historical TipPromene values so the canonical analytics views
-- can use one explicit event predicate.
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

-- Imported line-level events store the source Dnevnik ID in BrojRacuna.
-- Backfill only provenance fields; the canonical views remain read-only.
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
  AND src."Id" = line."BrojRacuna"::integer
  AND src."Datum" IS NOT NULL;
