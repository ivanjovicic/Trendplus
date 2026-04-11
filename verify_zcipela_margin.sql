-- Final margin verification with total cost coverage
WITH sales AS (
    SELECT 
        ps.kolicina, ps.cena, 
        ps.kolicina * ps.cena AS prihod,
        ps.nabavna_cena AS slc,
        a."NabavnaCenaDin" AS pcr,
        a."NabavnaCena" AS pcl
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON ps.id_prodaja = pz.id
    JOIN "Artikli" a ON ps.id_artikal = a."Id"
    WHERE a."IDTipObuce" = -2004188974
      AND pz.datum_prodaje >= (CURRENT_DATE - INTERVAL '89 days')
      AND pz.datum_prodaje <= (CURRENT_DATE + INTERVAL '1 day' - INTERVAL '1 microsecond')
),
cr AS (
    SELECT *,
        CASE 
            WHEN slc IS NOT NULL AND slc > 0 THEN slc
            WHEN pcr IS NOT NULL AND pcr > 0 THEN pcr
            WHEN pcl IS NOT NULL AND pcl > 0 THEN pcl
            ELSE 0
        END AS rc
    FROM sales
)
SELECT 
    ROUND(SUM(prihod), 2) AS total_revenue,
    ROUND(SUM(prihod - kolicina * rc), 2) AS margin_contribution,
    ROUND(SUM(prihod - kolicina * rc) / SUM(prihod) * 100, 2) AS margin_pct,
    -- Total coverage (any resolved cost, matching OLD deployed code)
    ROUND(COALESCE(SUM(prihod) FILTER (WHERE rc > 0), 0), 2) AS rev_with_any_cost,
    ROUND(COALESCE(SUM(prihod) FILTER (WHERE rc > 0), 0) / SUM(prihod) * 100, 2) AS total_cost_coverage_pct,
    -- Historical-only coverage (NEW code: only sale_line_cost)
    ROUND(COALESCE(SUM(prihod) FILTER (WHERE slc IS NOT NULL AND slc > 0), 0), 2) AS rev_with_hist_cost,
    ROUND(COALESCE(SUM(prihod) FILTER (WHERE slc IS NOT NULL AND slc > 0), 0) / SUM(prihod) * 100, 2) AS hist_coverage_pct
FROM cr;
