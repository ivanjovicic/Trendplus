-- ============================================================================
-- CLEANUP: Remove duplicate prodaja_stavke rows
-- 
-- Problem: Access re-import inserted identical rows when source rows had no 
-- stable id. Each (id_prodaja, id_artikal, cena) combo should appear N times
-- matching the Access source, but was duplicated to 2N, 3N, etc.
--
-- Strategy: For each group of duplicates sharing (id_prodaja, id_artikal, cena),
-- keep only the row with the smallest id and delete the rest.
-- Special case: if a combo legitimately appears K times in Access (e.g. same
-- article sold twice on one receipt), we need to keep K rows. Here we keep
-- one per distinct id since each Access row gets a unique generated id.
-- Actually the real pattern is: every row was duplicated exactly, so we just
-- keep the MIN(id) for each (id_prodaja, id_artikal, kolicina, cena) group.
-- ============================================================================

-- Step 1: DRY RUN - Count how many rows would be deleted
SELECT 
  'WILL DELETE' AS action,
  COUNT(*) AS rows_to_delete,
  ROUND(SUM(kolicina * cena)::numeric, 2) AS revenue_to_remove
FROM prodaja_stavke
WHERE id NOT IN (
  SELECT MIN(id)
  FROM prodaja_stavke
  GROUP BY id_prodaja, id_artikal, kolicina, cena
);

-- Step 2: Preview some examples
SELECT 
  ps.id AS delete_id,
  ps.id_prodaja,
  ps.id_artikal,
  ps.kolicina,
  ps.cena,
  pz.datum_prodaje::date AS sale_date,
  pz.broj_racuna
FROM prodaja_stavke ps
JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
WHERE ps.id NOT IN (
  SELECT MIN(id)
  FROM prodaja_stavke
  GROUP BY id_prodaja, id_artikal, kolicina, cena
)
ORDER BY pz.datum_prodaje DESC
LIMIT 30;

-- Step 3: EXECUTE DELETE (uncomment when ready)
-- DELETE FROM prodaja_stavke
-- WHERE id NOT IN (
--   SELECT MIN(id)
--   FROM prodaja_stavke
--   GROUP BY id_prodaja, id_artikal, kolicina, cena
-- );

-- Step 4: Verify after cleanup
-- SELECT
--   pz.datum_prodaje::date AS sale_date,
--   SUM(ps.kolicina) AS total_qty,
--   ROUND(SUM(ps.kolicina * ps.cena)::numeric, 2) AS total_revenue
-- FROM prodaja_stavke ps
-- JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
-- WHERE pz.datum_prodaje::date = '2026-03-26'
-- GROUP BY pz.datum_prodaje::date;
-- Expected: qty=40, revenue=232840.00 (half of current 80/465680)
