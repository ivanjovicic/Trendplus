-- RQ547 / R1: inventory values that broad startup normalization could rewrite.
-- Evidence only: WARN requires owner review; this script never normalizes source data.
BEGIN TRANSACTION READ ONLY;

WITH suspicious AS (
    SELECT COALESCE(d."TipPromene", '<NULL>') AS tip
    FROM "DnevnikPromena" d
    WHERE (d."TipPromene" ILIKE '%nivel%' OR d."TipPromene" ILIKE '%povrat%')
      AND d."TipPromene" NOT IN ('Nivelacija', 'Nivelacija cena', 'Povrat kupca')
), summary AS (
    SELECT COUNT(*)::bigint AS suspicious_rows,
           COUNT(DISTINCT tip)::bigint AS distinct_values,
           COALESCE(string_agg(DISTINCT tip, ', ' ORDER BY tip), 'none') AS examples
    FROM suspicious
)
SELECT
    'NV-P1-R1'::text AS check_id,
    CASE WHEN suspicious_rows = 0 THEN 'PASS' ELSE 'WARN' END::text AS verdict,
    format('suspicious_rows=%s; distinct_values=%s; values=%s', suspicious_rows, distinct_values, examples) AS observed,
    'no noncanonical values match broad nivel/povrat patterns'::text AS expected,
    'RQ544'::text AS owner,
    'Inventory only; WARN is a review signal, not proof that startup rewrote a row.'::text AS detail
FROM summary;

ROLLBACK;
