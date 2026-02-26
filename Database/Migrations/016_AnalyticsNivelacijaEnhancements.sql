-- 016_AnalyticsNivelacijaEnhancements.sql
-- Enhancements for robust nivelacija effect evaluation
-- - Control group (articles without price change, matched by category/vendor/revenue profile)
-- - Difference-in-Differences (DiD) view
-- - Rolling aggregates (MA7, momentum)
-- - % of articles in red zone (low stock, OOS)
-- - Supporting indexes

-- 1) Control group: articles without recorded price changes
CREATE OR REPLACE VIEW vw_nivelacija_kontrolna_grupa AS
SELECT
    a."Id" AS article_id,
    a."Naziv" AS article_name,
    a."Kategorija" AS category,
    a."IDDobavljac" AS vendor_id,
    d."Naziv" AS vendor_name,
    a."PLU" AS sku,
    MIN(ph."ChangedAt") AS first_price_change,
    COUNT(ph."Id") AS price_change_count
FROM "Artikli" a
LEFT JOIN "price_history" ph ON ph."ArticleId" = a."Id"
LEFT JOIN "Dobavljaci" d ON d."Id" = a."IDDobavljac"
GROUP BY a."Id", a."Naziv", a."Kategorija", a."IDDobavljac", d."Naziv", a."PLU"
HAVING COUNT(ph."Id") = 0;

-- 2) DiD view: test (articles with price change) vs closest control article
CREATE OR REPLACE VIEW vw_nivelacija_did AS
SELECT
    t.price_event_id,
    t.event_date,
    t.vendor_id,
    t.vendor_name,
    t.sku,
    t.article_name,
    t.category,
    t.pre_qty,
    t.pre_revenue,
    t.post_qty,
    t.post_revenue,
    c.article_id AS control_article_id,
    c.article_name AS control_article_name,
    c.category AS control_category,
    c.vendor_id AS control_vendor_id,
    c.vendor_name AS control_vendor_name,
    c.sku AS control_sku,
    c.pre_qty AS control_pre_qty,
    c.pre_revenue AS control_pre_revenue,
    c.post_qty AS control_post_qty,
    c.post_revenue AS control_post_revenue,
    ((t.post_revenue - t.pre_revenue) - (c.post_revenue - c.pre_revenue)) AS did_revenue,
    ((t.post_qty - t.pre_qty) - (c.post_qty - c.pre_qty)) AS did_qty
FROM vw_vendor_sales_nivelacija t
LEFT JOIN LATERAL (
    SELECT
        cg.article_id,
        cg.article_name,
        cg.category,
        cg.vendor_id,
        cg.vendor_name,
        cg.sku,
        COALESCE(pre_stats.pre_qty, 0)::INT AS pre_qty,
        COALESCE(pre_stats.pre_revenue, 0)::NUMERIC(18,2) AS pre_revenue,
        COALESCE(post_stats.post_qty, 0)::INT AS post_qty,
        COALESCE(post_stats.post_revenue, 0)::NUMERIC(18,2) AS post_revenue
    FROM vw_nivelacija_kontrolna_grupa cg
    LEFT JOIN LATERAL (
        SELECT
            SUM(ps."kolicina") AS pre_qty,
            SUM(ps."kolicina" * ps."cena") AS pre_revenue
        FROM "prodaja_stavke" ps
        JOIN "prodaja_zaglavlje" pz ON pz."id" = ps."id_prodaja"
        WHERE ps."id_artikal" = cg.article_id
          AND pz."datum_prodaje" >= t.event_date - INTERVAL '30 days'
          AND pz."datum_prodaje" < t.event_date
    ) pre_stats ON TRUE
    LEFT JOIN LATERAL (
        SELECT
            SUM(ps."kolicina") AS post_qty,
            SUM(ps."kolicina" * ps."cena") AS post_revenue
        FROM "prodaja_stavke" ps
        JOIN "prodaja_zaglavlje" pz ON pz."id" = ps."id_prodaja"
        WHERE ps."id_artikal" = cg.article_id
          AND pz."datum_prodaje" >= t.event_date
          AND pz."datum_prodaje" < t.event_date + INTERVAL '30 days'
    ) post_stats ON TRUE
    WHERE cg.category = t.category
      AND cg.vendor_id = t.vendor_id
    ORDER BY ABS(COALESCE(pre_stats.pre_revenue, 0) - t.pre_revenue) ASC
    LIMIT 1
) c ON TRUE
LIMIT 10000;

-- 3) Rolling aggregates: 7-day moving average and momentum base
CREATE OR REPLACE VIEW vw_sales_rolling_7d AS
SELECT
    ps."id_artikal" AS article_id,
    pz."datum_prodaje"::date AS day,
    SUM(ps."kolicina") AS units,
    SUM(ps."kolicina" * ps."cena") AS revenue,
    AVG(SUM(ps."kolicina" * ps."cena")) OVER (
        PARTITION BY ps."id_artikal"
        ORDER BY pz."datum_prodaje"::date
        ROWS BETWEEN 6 PRECEDING AND CURRENT ROW
    ) AS ma7_revenue,
    AVG(SUM(ps."kolicina")) OVER (
        PARTITION BY ps."id_artikal"
        ORDER BY pz."datum_prodaje"::date
        ROWS BETWEEN 6 PRECEDING AND CURRENT ROW
    ) AS ma7_units
FROM "prodaja_stavke" ps
JOIN "prodaja_zaglavlje" pz ON pz."id" = ps."id_prodaja"
GROUP BY ps."id_artikal", pz."datum_prodaje"::date;

-- 4) Momentum: last 7 days vs previous 7 days
CREATE OR REPLACE VIEW vw_sales_momentum AS
WITH x AS (
    SELECT
        article_id,
        day,
        units,
        revenue,
        MAX(day) OVER (PARTITION BY article_id) AS last_day
    FROM vw_sales_rolling_7d
)
SELECT
    article_id,
    last_day,
    SUM(CASE WHEN day > last_day - INTERVAL '7 days' THEN units ELSE 0 END) AS last7_units,
    SUM(CASE WHEN day > last_day - INTERVAL '7 days' THEN revenue ELSE 0 END) AS last7_revenue,
    SUM(CASE WHEN day <= last_day - INTERVAL '7 days' AND day > last_day - INTERVAL '14 days' THEN units ELSE 0 END) AS prev7_units,
    SUM(CASE WHEN day <= last_day - INTERVAL '7 days' AND day > last_day - INTERVAL '14 days' THEN revenue ELSE 0 END) AS prev7_revenue,
    (
        SUM(CASE WHEN day > last_day - INTERVAL '7 days' THEN revenue ELSE 0 END)
        - SUM(CASE WHEN day <= last_day - INTERVAL '7 days' AND day > last_day - INTERVAL '14 days' THEN revenue ELSE 0 END)
    ) AS momentum_revenue
FROM x
GROUP BY article_id, last_day;

-- 5) % of articles in red zone (low stock, OOS)
CREATE OR REPLACE VIEW vw_stock_red_zone AS
SELECT
    a."Id" AS article_id,
    a."Naziv" AS article_name,
    a."Kategorija" AS category,
    a."IDDobavljac" AS vendor_id,
    a."PLU" AS sku,
    a."Kolicina" AS stock,
    CASE WHEN a."Kolicina" IS NULL OR a."Kolicina" <= 0 THEN 1 ELSE 0 END AS is_oos,
    CASE WHEN a."Kolicina" <= COALESCE(a."MinimalnaKolicina", 1) THEN 1 ELSE 0 END AS is_low_stock
FROM "Artikli" a;

-- 6) Supporting indexes
CREATE INDEX IF NOT EXISTS IX_prodaja_stavke_id_artikal_datum ON "prodaja_stavke" ("id_artikal", "id_prodaja");
CREATE INDEX IF NOT EXISTS IX_prodaja_zaglavlje_datum ON "prodaja_zaglavlje" ("datum_prodaje");
CREATE INDEX IF NOT EXISTS IX_Artikli_Kolicina ON "Artikli" ("Kolicina");
