-- ==========================================================
-- 021_DetectMissingDobavljaci.sql
-- Diagnostic migration: detect articles and vendor references
-- that result in COALESCE(..., 'Nepoznato') being used.
-- This migration creates a candidates table with rows that need
-- manual review and mapping to real `Dobavljaci.Id` values.
-- ==========================================================

-- Create table to hold candidate fixes (idempotent)
CREATE TABLE IF NOT EXISTS fix_missing_dobavljaci_candidates (
    id BIGSERIAL PRIMARY KEY,
    artikl_id bigint NULL,
    artikl_naziv text NULL,
    artikl_plu text NULL,
    artikl_iddobavljac bigint NULL,
    referenced_vendor_id bigint NULL,
    referenced_vendor_name text NULL,
    source text NOT NULL,
    created_at timestamptz DEFAULT now()
);

-- Clear previous run (keeps table idempotent but preserves history if needed)
TRUNCATE TABLE fix_missing_dobavljaci_candidates;

-- 1) Articles that have NULL `IDDobavljac`
INSERT INTO fix_missing_dobavljaci_candidates(artikl_id, artikl_naziv, artikl_plu, artikl_iddobavljac, source)
SELECT a."Id", a."Naziv", COALESCE(NULLIF(a."PLU", ''), a."Id"::text), a."IDDobavljac", 'artikli_null_iddobavljac'
FROM "Artikli" a
WHERE a."IDDobavljac" IS NULL;

-- 2) Articles that reference a non-existing Dobavljaci.Id
INSERT INTO fix_missing_dobavljaci_candidates(artikl_id, artikl_naziv, artikl_plu, artikl_iddobavljac, source)
SELECT a."Id", a."Naziv", COALESCE(NULLIF(a."PLU", ''), a."Id"::text), a."IDDobavljac", 'artikli_missing_dobavljac'
FROM "Artikli" a
LEFT JOIN "Dobavljaci" d ON d."Id" = a."IDDobavljac"
WHERE a."IDDobavljac" IS NOT NULL
  AND d."Id" IS NULL;

-- 3) DnevnikPromena entries that reference DobavljacId not present in Dobavljaci
INSERT INTO fix_missing_dobavljaci_candidates(referenced_vendor_id, referenced_vendor_name, source)
SELECT DISTINCT d."DobavljacId"::bigint AS referenced_vendor_id, NULL::text AS referenced_vendor_name, 'dnevnikpromena_missing_dobavljac'
FROM "DnevnikPromena" d
LEFT JOIN "Dobavljaci" dob ON dob."Id" = d."DobavljacId"
WHERE d."DobavljacId" IS NOT NULL
  AND dob."Id" IS NULL;

-- 4) Aggregate sales rows that produce COALESCE(d."Naziv", 'Nepoznato') grouping
INSERT INTO fix_missing_dobavljaci_candidates(referenced_vendor_name, source)
SELECT DISTINCT COALESCE(d."Naziv", 'Nepoznato') AS referenced_vendor_name, 'sales_aggregate_preview'
FROM prodaja_stavke ps
LEFT JOIN "Artikli" a ON a."Id" = ps.id_artikal
LEFT JOIN "Dobavljaci" d ON d."Id" = a."IDDobavljac";

-- Helpful view to inspect current problematic groups (readonly)
CREATE OR REPLACE VIEW vw_fix_missing_dobavljaci_summary AS
SELECT source,
       COUNT(*) AS rows,
       COUNT(DISTINCT artikl_id) FILTER (WHERE artikl_id IS NOT NULL) AS distinct_artikli,
       COUNT(DISTINCT referenced_vendor_id) FILTER (WHERE referenced_vendor_id IS NOT NULL) AS distinct_referenced_vendor_ids
FROM fix_missing_dobavljaci_candidates
GROUP BY source
ORDER BY rows DESC;

-- End of migration
