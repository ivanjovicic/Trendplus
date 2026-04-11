-- Verify Rieker supplier-sales-stats numbers
-- Default range: last 90 days (today - 89 to today end)

-- 1. Basic metrics: ukupanPromet, ukupnaKolicina
SELECT
  'Current period' as period,
  SUM(ps."Kolicina" * ps."Cena") as ukupan_promet,
  SUM(ps."Kolicina") as ukupna_kolicina,
  COUNT(DISTINCT a."Id") as broj_artikala
FROM "ProdajaStavke" ps
JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
WHERE a."IDDobavljac" = 1496651500
  AND pz."DatumProdaje" >= (current_date - interval '89 days')
  AND pz."DatumProdaje" <= (current_date + interval '1 day' - interval '1 microsecond');

-- 2. Previous period metrics
SELECT
  'Previous period' as period,
  SUM(ps."Kolicina" * ps."Cena") as prethodni_promet,
  SUM(ps."Kolicina") as prethodni_kolicina
FROM "ProdajaStavke" ps
JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
WHERE a."IDDobavljac" = 1496651500
  AND pz."DatumProdaje" >= (current_date - interval '89 days') - (interval '90 days')
  AND pz."DatumProdaje" < (current_date - interval '89 days');

-- 3. Nivelacija: pre/posle nivelacije
WITH niv AS (
  SELECT "ArtikalId", MIN("Datum") as "PrvaDatum"
  FROM "DnevnikPromena"
  WHERE ("TipPromene" = 'Nivelacija' OR "TipPromene" = 'NivelacijaCena')
    AND "ArtikalId" IS NOT NULL
  GROUP BY "ArtikalId"
),
sales AS (
  SELECT
    a."Id" as artikal_id,
    pz."DatumProdaje",
    ps."Kolicina",
    ps."Kolicina" * ps."Cena" as prihod,
    niv."PrvaDatum" as niv_datum
  FROM "ProdajaStavke" ps
  JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
  JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
  LEFT JOIN niv ON niv."ArtikalId" = a."Id"
  WHERE a."IDDobavljac" = 1496651500
    AND pz."DatumProdaje" >= (current_date - interval '89 days')
    AND pz."DatumProdaje" <= (current_date + interval '1 day' - interval '1 microsecond')
)
SELECT
  'Nivelacija split' as info,
  SUM(CASE WHEN niv_datum IS NOT NULL AND "DatumProdaje" < niv_datum THEN prihod ELSE 0 END) as pre_niv_promet,
  SUM(CASE WHEN niv_datum IS NOT NULL AND "DatumProdaje" < niv_datum THEN "Kolicina" ELSE 0 END) as pre_niv_kolicina,
  SUM(CASE WHEN niv_datum IS NOT NULL AND "DatumProdaje" >= niv_datum THEN prihod ELSE 0 END) as posle_niv_promet,
  SUM(CASE WHEN niv_datum IS NOT NULL AND "DatumProdaje" >= niv_datum THEN "Kolicina" ELSE 0 END) as posle_niv_kolicina,
  COUNT(DISTINCT CASE WHEN niv_datum IS NOT NULL THEN artikal_id END) as artikli_sa_niv,
  SUM(CASE WHEN niv_datum IS NOT NULL THEN prihod ELSE 0 END) as revenue_with_niv_split
FROM sales;

-- 4. Margin calculation
WITH sales AS (
  SELECT
    ps."Kolicina",
    ps."Kolicina" * ps."Cena" as prihod,
    CASE 
      WHEN ps."NabavnaCena" > 0 THEN ps."NabavnaCena"
      WHEN a."NabavnaCenaDin" > 0 THEN a."NabavnaCenaDin"
      WHEN a."NabavnaCena" > 0 THEN a."NabavnaCena"
      ELSE NULL
    END as unit_cost
  FROM "ProdajaStavke" ps
  JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
  JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
  WHERE a."IDDobavljac" = 1496651500
    AND pz."DatumProdaje" >= (current_date - interval '89 days')
    AND pz."DatumProdaje" <= (current_date + interval '1 day' - interval '1 microsecond')
)
SELECT
  'Margin' as info,
  SUM(CASE WHEN unit_cost IS NOT NULL THEN prihod ELSE 0 END) as revenue_with_cost,
  SUM(CASE WHEN unit_cost IS NOT NULL THEN "Kolicina" * unit_cost ELSE 0 END) as total_cost,
  SUM(CASE WHEN unit_cost IS NOT NULL THEN prihod ELSE 0 END) - SUM(CASE WHEN unit_cost IS NOT NULL THEN "Kolicina" * unit_cost ELSE 0 END) as margin_contribution,
  ROUND(
    CASE WHEN SUM(CASE WHEN unit_cost IS NOT NULL THEN prihod ELSE 0 END) > 0 
    THEN (SUM(CASE WHEN unit_cost IS NOT NULL THEN prihod ELSE 0 END) - SUM(CASE WHEN unit_cost IS NOT NULL THEN "Kolicina" * unit_cost ELSE 0 END)) / 
         SUM(CASE WHEN unit_cost IS NOT NULL THEN prihod ELSE 0 END) * 100
    ELSE 0 END, 2
  ) as margin_pct,
  ROUND(
    CASE WHEN SUM(prihod) > 0 
    THEN SUM(CASE WHEN unit_cost IS NOT NULL THEN prihod ELSE 0 END) / SUM(prihod) * 100
    ELSE 0 END, 2
  ) as margin_data_coverage_pct
FROM sales;

-- 5. Date range check
SELECT 
  current_date - interval '89 days' as from_date,
  current_date as to_date;

-- 6. PoP calculations check
WITH current_p AS (
  SELECT
    SUM(ps."Kolicina" * ps."Cena") as promet,
    SUM(ps."Kolicina") as kolicina
  FROM "ProdajaStavke" ps
  JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
  JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
  WHERE a."IDDobavljac" = 1496651500
    AND pz."DatumProdaje" >= (current_date - interval '89 days')
    AND pz."DatumProdaje" <= (current_date + interval '1 day' - interval '1 microsecond')
),
prev_p AS (
  SELECT
    SUM(ps."Kolicina" * ps."Cena") as promet,
    SUM(ps."Kolicina") as kolicina
  FROM "ProdajaStavke" ps
  JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
  JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
  WHERE a."IDDobavljac" = 1496651500
    AND pz."DatumProdaje" >= (current_date - interval '89 days') - (interval '90 days')
    AND pz."DatumProdaje" < (current_date - interval '89 days')
)
SELECT
  c.promet as current_promet,
  p.promet as prev_promet,
  c.kolicina as current_kolicina,
  p.kolicina as prev_kolicina,
  ROUND((c.promet - p.promet) / NULLIF(p.promet, 0) * 100, 2) as pop_promet_pct,
  ROUND((c.kolicina - p.kolicina)::numeric / NULLIF(p.kolicina, 0) * 100, 2) as pop_kolicina_pct
FROM current_p c, prev_p p;

-- 7. Pre/post nivelacija derived metrics
WITH niv AS (
  SELECT "ArtikalId", MIN("Datum") as "PrvaDatum"
  FROM "DnevnikPromena"
  WHERE ("TipPromene" = 'Nivelacija' OR "TipPromene" = 'NivelacijaCena')
    AND "ArtikalId" IS NOT NULL
  GROUP BY "ArtikalId"
),
sales AS (
  SELECT
    a."Id" as artikal_id,
    pz."DatumProdaje",
    ps."Kolicina",
    ps."Kolicina" * ps."Cena" as prihod,
    niv."PrvaDatum" as niv_datum
  FROM "ProdajaStavke" ps
  JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
  JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
  LEFT JOIN niv ON niv."ArtikalId" = a."Id"
  WHERE a."IDDobavljac" = 1496651500
    AND pz."DatumProdaje" >= (current_date - interval '89 days')
    AND pz."DatumProdaje" <= (current_date + interval '1 day' - interval '1 microsecond')
),
agg AS (
  SELECT
    SUM(prihod) as total_rev,
    SUM(CASE WHEN niv_datum IS NOT NULL AND "DatumProdaje" < niv_datum THEN prihod ELSE 0 END) as pre_niv,
    SUM(CASE WHEN niv_datum IS NOT NULL AND "DatumProdaje" >= niv_datum THEN prihod ELSE 0 END) as post_niv,
    SUM(CASE WHEN niv_datum IS NOT NULL AND "DatumProdaje" < niv_datum THEN "Kolicina" ELSE 0 END) as pre_qty,
    SUM(CASE WHEN niv_datum IS NOT NULL AND "DatumProdaje" >= niv_datum THEN "Kolicina" ELSE 0 END) as post_qty,
    SUM(CASE WHEN niv_datum IS NOT NULL THEN prihod ELSE 0 END) as rev_with_split
  FROM sales
)
SELECT
  ROUND(rev_with_split / NULLIF(total_rev, 0) * 100, 2) as pre_post_coverage_pct,
  ROUND((post_niv - pre_niv) / NULLIF(pre_niv, 0) * 100, 2) as niv_impact_revenue_pct,
  ROUND((post_qty - pre_qty)::numeric / NULLIF(pre_qty, 0) * 100, 2) as niv_impact_qty_pct
FROM agg;
