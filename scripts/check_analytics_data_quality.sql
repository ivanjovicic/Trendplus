-- Periodic/manual analytics data-quality checks.
-- Replace lookback_days if needed.

WITH params AS (
    SELECT
        90::integer AS lookback_days,
        (CURRENT_DATE - INTERVAL '89 day')::timestamptz AS from_utc,
        (CURRENT_DATE + INTERVAL '1 day' - INTERVAL '1 microsecond')::timestamptz AS to_utc
),
orphan_articles AS (
    SELECT COUNT(*) AS orphan_article_count
    FROM "Artikli" a
    LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
    WHERE a."IDDobavljac" IS NOT NULL
      AND d."Id" IS NULL
),
sales_window AS (
    SELECT
        ROUND(SUM(ps."Kolicina" * ps."Cena"), 2) AS total_revenue,
        ROUND(SUM(CASE WHEN COALESCE(ps."NabavnaCena", a."NabavnaCena") IS NULL THEN ps."Kolicina" * ps."Cena" ELSE 0 END), 2) AS missing_cost_revenue,
        ROUND(SUM(CASE WHEN a."IDDobavljac" IS NULL OR d."Id" IS NULL THEN ps."Kolicina" * ps."Cena" ELSE 0 END), 2) AS unknown_supplier_revenue
    FROM "ProdajaStavke" ps
    JOIN "ProdajaZaglavlja" pz ON ps."IdProdaja" = pz."Id"
    JOIN "Artikli" a ON ps."IdArtikal" = a."Id"
    LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
    CROSS JOIN params p
    WHERE pz."DatumProdaje" >= p.from_utc
      AND pz."DatumProdaje" <= p.to_utc
)
SELECT
    p.lookback_days,
    p.from_utc,
    p.to_utc,
    oa.orphan_article_count,
    sw.total_revenue,
    sw.missing_cost_revenue,
    ROUND(CASE WHEN sw.total_revenue > 0 THEN (sw.missing_cost_revenue / sw.total_revenue) * 100 ELSE 0 END, 2) AS missing_cost_revenue_share_pct,
    sw.unknown_supplier_revenue,
    ROUND(CASE WHEN sw.total_revenue > 0 THEN (sw.unknown_supplier_revenue / sw.total_revenue) * 100 ELSE 0 END, 2) AS unknown_supplier_revenue_share_pct
FROM params p
CROSS JOIN orphan_articles oa
CROSS JOIN sales_window sw;
