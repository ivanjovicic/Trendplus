-- Diagnostic: per-article pre/post breakdown for Ž.Cipela
-- to understand why comparable_articles = 0
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
        fn.first_niv_date,
        SUM(CASE WHEN s.datum_prodaje < fn.first_niv_date THEN s.prihod ELSE 0 END) AS pre_revenue,
        SUM(CASE WHEN s.datum_prodaje >= fn.first_niv_date THEN s.prihod ELSE 0 END) AS post_revenue,
        SUM(CASE WHEN s.datum_prodaje < fn.first_niv_date THEN s.kolicina ELSE 0 END) AS pre_qty,
        SUM(CASE WHEN s.datum_prodaje >= fn.first_niv_date THEN s.kolicina ELSE 0 END) AS post_qty
    FROM sales s
    JOIN first_niv fn ON s.id_artikal = fn."ArtikalId"
    GROUP BY s.id_artikal, fn.first_niv_date
)
SELECT 
    id_artikal,
    first_niv_date,
    pre_revenue,
    post_revenue,
    pre_qty,
    post_qty,
    CASE WHEN pre_revenue > 0 AND post_revenue > 0 AND pre_qty > 0 AND post_qty > 0 THEN 'COMPARABLE' ELSE 'NOT_COMPARABLE' END AS status
FROM article_split
WHERE pre_revenue > 0 OR post_revenue > 0
ORDER BY pre_revenue DESC, post_revenue DESC;

-- Also check: what does the frontend actually display?
-- The endpoint might use a different calculation for the per-row impact
-- Check if the old pre/post coverage was using total (non-comparable) ranges
-- with formula: (preRev + postRev) / totalRev * 100
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
    -- Total pre/post (not just comparable)
    SUM(pre_revenue) AS total_pre_rev,
    SUM(post_revenue) AS total_post_rev,
    SUM(pre_qty) AS total_pre_qty,
    SUM(post_qty) AS total_post_qty,
    -- Total revenue coverage (pre+post / total_rev)
    ROUND((SUM(pre_revenue) + SUM(post_revenue)) / (SELECT SUM(prihod) FROM sales) * 100, 2) AS total_coverage_pct,
    -- Impact from TOTAL pre/post (not comparable)
    CASE WHEN SUM(pre_revenue) > 0 THEN
        ROUND((SUM(post_revenue) - SUM(pre_revenue)) / SUM(pre_revenue) * 100, 2)
    END AS total_revenue_impact_pct,
    CASE WHEN SUM(pre_qty) > 0 THEN
        ROUND((SUM(post_qty) - SUM(pre_qty))::numeric / SUM(pre_qty) * 100, 2)
    END AS total_units_impact_pct
FROM article_split;
