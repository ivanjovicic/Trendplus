-- ==========================================================
-- 014_NormalizeNivelacijaEvents.sql
-- Data-only repair for imported nivelacija events.
--
-- View ownership is canonical in:
--   Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql
-- The legacy 014_FixNivelacijaViewsFromDnevnik.sql remains historical
-- evidence and is intentionally not executed by startup.
-- ==========================================================

-- Normalize only known, semantically unambiguous aliases. Similar words such
-- as supplier returns, storno and re-nivelacija stay unchanged for review.
DO $$
DECLARE
    _mapping record;
    _unmapped record;
    _changed_rows bigint;
    _unmapped_distinct_count bigint;
BEGIN
    FOR _mapping IN
        SELECT *
        FROM (VALUES
            ('nivelacija', 'Nivelacija'),
            ('nivelacija cena', 'Nivelacija cena'),
            ('ulaz robe', 'Ulaz robe'),
            ('unos robe', 'Ulaz robe'),
            ('povrat kupca', 'Povrat kupca')
        ) AS mappings(source_value, canonical_value)
    LOOP
        UPDATE "DnevnikPromena" d
           SET "TipPromene" = _mapping.canonical_value
         WHERE lower(btrim(d."TipPromene")) = _mapping.source_value
           AND d."TipPromene" IS DISTINCT FROM _mapping.canonical_value;
        GET DIAGNOSTICS _changed_rows = ROW_COUNT;
        RAISE NOTICE 'Startup normalization mapping: source=% canonical=% changed_rows=%',
            _mapping.source_value, _mapping.canonical_value, _changed_rows;
    END LOOP;

    SELECT COUNT(*)::bigint
      INTO _unmapped_distinct_count
      FROM (
          SELECT d."TipPromene"
            FROM "DnevnikPromena" d
           WHERE d."TipPromene" IS NOT NULL
             AND (
                    position('nivel' IN lower(d."TipPromene")) > 0
                 OR position('storno' IN lower(d."TipPromene")) > 0
                 OR position('ponist' IN lower(d."TipPromene")) > 0
                 OR position('poništ' IN lower(d."TipPromene")) > 0
                 OR position('povrat' IN lower(d."TipPromene")) > 0
                 OR position('povra' IN lower(d."TipPromene")) > 0
                 OR position('ulaz robe' IN lower(d."TipPromene")) > 0
                 OR position('unos robe' IN lower(d."TipPromene")) > 0
             )
             AND NOT EXISTS (
                 SELECT 1
                   FROM (VALUES
                       ('nivelacija'),
                       ('nivelacija cena'),
                       ('ulaz robe'),
                       ('unos robe'),
                       ('povrat kupca')
                   ) AS mappings(source_value)
                  WHERE lower(btrim(d."TipPromene")) = mappings.source_value
             )
           GROUP BY d."TipPromene"
      ) AS unmapped_types;

    FOR _unmapped IN
        SELECT d."TipPromene" AS source_value, COUNT(*)::bigint AS row_count
          FROM "DnevnikPromena" d
         WHERE d."TipPromene" IS NOT NULL
           AND (
                  position('nivel' IN lower(d."TipPromene")) > 0
               OR position('storno' IN lower(d."TipPromene")) > 0
               OR position('ponist' IN lower(d."TipPromene")) > 0
               OR position('poništ' IN lower(d."TipPromene")) > 0
               OR position('povrat' IN lower(d."TipPromene")) > 0
               OR position('povra' IN lower(d."TipPromene")) > 0
               OR position('ulaz robe' IN lower(d."TipPromene")) > 0
               OR position('unos robe' IN lower(d."TipPromene")) > 0
           )
           AND NOT EXISTS (
               SELECT 1
                 FROM (VALUES
                     ('nivelacija'),
                     ('nivelacija cena'),
                     ('ulaz robe'),
                     ('unos robe'),
                     ('povrat kupca')
                 ) AS mappings(source_value)
                WHERE lower(btrim(d."TipPromene")) = mappings.source_value
           )
         GROUP BY d."TipPromene"
         ORDER BY COUNT(*) DESC, d."TipPromene"
         LIMIT 20
    LOOP
        RAISE NOTICE 'Startup normalization unmapped: source=% rows=%',
            left(_unmapped.source_value, 120), _unmapped.row_count;
    END LOOP;

    IF _unmapped_distinct_count > 20 THEN
        RAISE NOTICE 'Startup normalization unmapped: omitted_distinct_types=%',
            _unmapped_distinct_count - 20;
    END IF;
END
$$;

-- Imported line-level events store the source Dnevnik ID in BrojRacuna.
-- Resolve the reference as a bounded bigint; oversized digit strings stay
-- unmatched instead of aborting the complete startup update.
WITH receipt_references AS (
    SELECT
        line."Id" AS line_id,
        CASE
            WHEN line."BrojRacuna" ~ '^[0-9]+$'
             AND (
                    length(COALESCE(NULLIF(ltrim(line."BrojRacuna", '0'), ''), '0')) < 19
                 OR (
                        length(COALESCE(NULLIF(ltrim(line."BrojRacuna", '0'), ''), '0')) = 19
                    AND COALESCE(NULLIF(ltrim(line."BrojRacuna", '0'), ''), '0') <= '9223372036854775807'
                 )
             )
            THEN COALESCE(NULLIF(ltrim(line."BrojRacuna", '0'), ''), '0')::bigint
        END AS source_id
    FROM "DnevnikPromena" line
    WHERE line."DataOrigin" = 'access'
      AND line."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
      AND line."ArtikalId" IS NOT NULL
)
UPDATE "DnevnikPromena" line
SET
    "Datum" = src."Datum",
    "IDObjekat" = COALESCE(line."IDObjekat", src."IDObjekat"),
    "DobavljacId" = COALESCE(line."DobavljacId", src."DobavljacId")
FROM receipt_references ref
JOIN "DnevnikPromena" src ON src."Id"::bigint = ref.source_id
WHERE line."Id" = ref.line_id
  AND src."Datum" IS NOT NULL;
