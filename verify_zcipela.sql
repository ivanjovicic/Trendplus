-- =====================================================================
-- Verification: Ž.Cipela (shoe type ID = -2004188974)
-- Date range: last 90 days (today-89 to end of today, UTC)
-- Tables: prodaja_stavke (snake_case), prodaja_zaglavlje (snake_case),
--         "Artikli" (PascalCase), "DnevnikPromena" (PascalCase)
-- =====================================================================

-- Step 1: Basic totals (Ukupan promet, Ukupna kolicina, Ukupan broj artikala)
-- Expected: promet=2,512,740, qty=436, artikli=88
SELECT 
    SUM(ps.kolicina * ps.cena) AS ukupan_promet,
    SUM(ps.kolicina) AS ukupna_kolicina,
    COUNT(DISTINCT ps.id_artikal) AS ukupan_broj_artikala
FROM prodaja_stavke ps
JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
JOIN "Artikli" a ON ps.id_artikal = a."Id"
WHERE a."IDTipObuce" = -2004188974
  AND pz.datum_prodaje >= (CURRENT_DATE - INTERVAL '89 days')
  AND pz.datum_prodaje <= (CURRENT_DATE + INTERVAL '1 day' - INTERVAL '1 microsecond');

-- Step 2: Artikli sa nivelacijom count (expected: 35)
WITH first_niv AS (
    SELECT 
        d."ArtikalId",
        MIN(d."Datum") AS first_niv_date
    FROM "DnevnikPromena" d
    WHERE (d."TipPromene" = 'Nivelacija' OR d."TipPromene" = 'Nivelacija cena')
      AND d."ArtikalId" IS NOT NULL
      AND d."Datum" <= (CURRENT_DATE + INTERVAL '1 day' - INTERVAL '1 microsecond')
    GROUP BY d."ArtikalId"
),
sales AS (
    SELECT 
        ps.id_artikal,
        pz.datum_prodaje,
        ps.kolicina,
        ps.cena,
        ps.kolicina * ps.cena AS prihod
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE a."IDTipObuce" = -2004188974
      AND pz.datum_prodaje >= (CURRENT_DATE - INTERVAL '89 days')
      AND pz.datum_prodaje <= (CURRENT_DATE + INTERVAL '1 day' - INTERVAL '1 microsecond')
)
SELECT COUNT(DISTINCT s.id_artikal) AS artikli_sa_nivelacijom
FROM sales s
JOIN first_niv fn ON s.id_artikal = fn."ArtikalId";

-- Step 3: Pre/post nivelacija split + comparable articles
-- Expected: pre_promet=61,940, pre_qty=6, post_promet=893,890, post_qty=146
WITH first_niv AS (
    SELECT 
        d."ArtikalId",
        MIN(d."Datum") AS first_niv_date
    FROM "DnevnikPromena" d
    WHERE (d."TipPromene" = 'Nivelacija' OR d."TipPromene" = 'Nivelacija cena')
      AND d."ArtikalId" IS NOT NULL
      AND d."Datum" <= (CURRENT_DATE + INTERVAL '1 day' - INTERVAL '1 microsecond')
    GROUP BY d."ArtikalId"
),
sales AS (
    SELECT 
        ps.id_artikal,
        pz.datum_prodaje,
        ps.kolicina,
        ps.cena,
        ps.kolicina * ps.cena AS prihod
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE a."IDTipObuce" = -2004188974
      AND pz.datum_prodaje >= (CURRENT_DATE - INTERVAL '89 days')
      AND pz.datum_prodaje <= (CURRENT_DATE + INTERVAL '1 day' - INTERVAL '1 microsecond')
),
article_split AS (
    SELECT 
        s.id_artikal,
        SUM(CASE WHEN s.datum_prodaje < fn.first_niv_date THEN s.prihod ELSE 0 END) AS pre_revenue,
        SUM(CASE WHEN s.datum_prodaje >= fn.first_niv_date THEN s.prihod ELSE 0 END) AS post_revenue,
        SUM(CASE WHEN s.datum_prodaje < fn.first_niv_date THEN s.kolicina ELSE 0 END) AS pre_qty,
        SUM(CASE WHEN s.datum_prodaje >= fn.first_niv_date THEN s.kolicina ELSE 0 END) AS post_qty
    FROM sales s
    JOIN first_niv fn ON s.id_artikal = fn."ArtikalId"
    GROUP BY s.id_artikal
)
SELECT 
    SUM(pre_revenue) AS pre_nivelacije_promet,
    SUM(pre_qty) AS pre_nivelacije_kolicina,
    SUM(post_revenue) AS posle_nivelacije_promet,
    SUM(post_qty) AS posle_nivelacije_kolicina,
    COUNT(*) FILTER (WHERE pre_revenue > 0 AND post_revenue > 0 AND pre_qty > 0 AND post_qty > 0) AS comparable_articles,
    SUM(pre_revenue) FILTER (WHERE pre_revenue > 0 AND post_revenue > 0 AND pre_qty > 0 AND post_qty > 0) AS comparable_pre_revenue,
    SUM(post_revenue) FILTER (WHERE pre_revenue > 0 AND post_revenue > 0 AND pre_qty > 0 AND post_qty > 0) AS comparable_post_revenue,
    SUM(pre_qty) FILTER (WHERE pre_revenue > 0 AND post_revenue > 0 AND pre_qty > 0 AND post_qty > 0) AS comparable_pre_qty,
    SUM(post_qty) FILTER (WHERE pre_revenue > 0 AND post_revenue > 0 AND pre_qty > 0 AND post_qty > 0) AS comparable_post_qty
FROM article_split;

-- Step 4: ComparableRevenueCoveragePct and RevenueImpactPct and UnitsImpactPct
-- Expected: coverage=38.04%, revenue_impact=1343.15%, units_impact=2333.33%
WITH first_niv AS (
    SELECT 
        d."ArtikalId",
        MIN(d."Datum") AS first_niv_date
    FROM "DnevnikPromena" d
    WHERE (d."TipPromene" = 'Nivelacija' OR d."TipPromene" = 'Nivelacija cena')
      AND d."ArtikalId" IS NOT NULL
      AND d."Datum" <= (CURRENT_DATE + INTERVAL '1 day' - INTERVAL '1 microsecond')
    GROUP BY d."ArtikalId"
),
sales AS (
    SELECT 
        ps.id_artikal,
        pz.datum_prodaje,
        ps.kolicina,
        ps.cena,
        ps.kolicina * ps.cena AS prihod
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE a."IDTipObuce" = -2004188974
      AND pz.datum_prodaje >= (CURRENT_DATE - INTERVAL '89 days')
      AND pz.datum_prodaje <= (CURRENT_DATE + INTERVAL '1 day' - INTERVAL '1 microsecond')
),
article_split AS (
    SELECT 
        s.id_artikal,
        SUM(CASE WHEN s.datum_prodaje < fn.first_niv_date THEN s.prihod ELSE 0 END) AS pre_revenue,
        SUM(CASE WHEN s.datum_prodaje >= fn.first_niv_date THEN s.prihod ELSE 0 END) AS post_revenue,
        SUM(CASE WHEN s.datum_prodaje < fn.first_niv_date THEN s.kolicina ELSE 0 END) AS pre_qty,
        SUM(CASE WHEN s.datum_prodaje >= fn.first_niv_date THEN s.kolicina ELSE 0 END) AS post_qty
    FROM sales s
    JOIN first_niv fn ON s.id_artikal = fn."ArtikalId"
    GROUP BY s.id_artikal
),
totals AS (
    SELECT SUM(prihod) AS total_revenue FROM sales
),
comparable AS (
    SELECT 
        SUM(pre_revenue) AS comp_pre_rev,
        SUM(post_revenue) AS comp_post_rev,
        SUM(pre_qty) AS comp_pre_qty,
        SUM(post_qty) AS comp_post_qty
    FROM article_split
    WHERE pre_revenue > 0 AND post_revenue > 0 AND pre_qty > 0 AND post_qty > 0
)
SELECT 
    ROUND((comp_pre_rev + comp_post_rev) / t.total_revenue * 100, 2) AS comparable_coverage_pct,
    ROUND((comp_post_rev - comp_pre_rev) / comp_pre_rev * 100, 2) AS revenue_impact_pct,
    ROUND((comp_post_qty - comp_pre_qty)::numeric / comp_pre_qty * 100, 2) AS units_impact_pct
FROM comparable c, totals t;

-- Step 5: Margin calculation
-- Expected: margin_contribution=1,191,299, margin%=47.41%, historical_coverage=100%
WITH sales AS (
    SELECT 
        ps.id_artikal,
        ps.kolicina,
        ps.cena,
        ps.kolicina * ps.cena AS prihod,
        ps.nabavna_cena AS sale_line_cost,
        a."NabavnaCenaDin" AS product_cost_rsd,
        a."NabavnaCena" AS product_cost_legacy
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE a."IDTipObuce" = -2004188974
      AND pz.datum_prodaje >= (CURRENT_DATE - INTERVAL '89 days')
      AND pz.datum_prodaje <= (CURRENT_DATE + INTERVAL '1 day' - INTERVAL '1 microsecond')
),
cost_resolved AS (
    SELECT 
        *,
        CASE 
            WHEN sale_line_cost IS NOT NULL AND sale_line_cost > 0 THEN sale_line_cost
            WHEN product_cost_rsd IS NOT NULL AND product_cost_rsd > 0 THEN product_cost_rsd
            WHEN product_cost_legacy IS NOT NULL AND product_cost_legacy > 0 THEN product_cost_legacy
            ELSE 0
        END AS resolved_cost,
        CASE 
            WHEN sale_line_cost IS NOT NULL AND sale_line_cost > 0 THEN 'Historical'
            WHEN product_cost_rsd IS NOT NULL AND product_cost_rsd > 0 THEN 'ProductFallbackRsd'
            WHEN product_cost_legacy IS NOT NULL AND product_cost_legacy > 0 THEN 'ProductFallbackLegacy'
            ELSE 'None'
        END AS cost_source
    FROM sales
)
SELECT 
    cost_source,
    COUNT(*) AS rows_count,
    SUM(prihod) AS revenue,
    SUM(prihod - kolicina * resolved_cost) AS margin
FROM cost_resolved
GROUP BY GROUPING SETS ((), (cost_source))
ORDER BY cost_source NULLS FIRST;
