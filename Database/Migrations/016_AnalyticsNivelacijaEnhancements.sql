-- 016_AnalyticsNivelacijaEnhancements.sql
-- Unapređenja za robustnu evaluaciju efekta nivelacije
-- - Kontrolna grupa (artikli bez promene cene, upareni po kategoriji/dobavljaču/ceni)
-- - Difference-in-Differences (DiD) view
-- - Rolling agregati (7d moving avg, momentum)
-- - % artikala u crvenoj zoni (low stock, OOS)
-- - Indeksi za ubrzanje

-- 1) Kontrolna grupa: artikli bez promene cene u istom periodu
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

-- 2) DiD view: test (artikli sa promenom cene) vs kontrola
CREATE OR REPLACE VIEW vw_nivelacija_did AS
SELECT
    t.price_event_id,
    t.event_date,
    t.vendor_id,
    t.vendor_name,
    t.sku,
    t.article_name,
    t.category,
    t.pre_qty, t.pre_revenue, t.post_qty, t.post_revenue,
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
    -- DiD formula
    ( (t.post_revenue - t.pre_revenue) - (c.post_revenue - c.pre_revenue) ) AS did_revenue,
    ( (t.post_qty - t.pre_qty) - (c.post_qty - c.pre_qty) ) AS did_qty
FROM vw_vendor_sales_nivelacija t
JOIN (
    -- Za svaki test artikal, pronađi kontrolu iz iste kategorije i cenovnog razreda
    SELECT
        pre.category,
        pre.vendor_id,
        pre.sku,
        pre.pre_qty,
        pre.pre_revenue,
        post.post_qty,
        post.post_revenue,
        pre.sku AS control_sku,
        pre.article_name,
        pre.category AS control_category,
        pre.vendor_id AS control_vendor_id,
        d."Naziv" AS control_vendor_name,
        pre.sku AS control_sku,
        pre.article_name AS control_article_name,
        pre.article_id
    FROM vw_sales_pre_nivelacija pre
    LEFT JOIN vw_sales_post_nivelacija post ON post.price_event_id = pre.price_event_id
    LEFT JOIN "Dobavljaci" d ON d."Id" = pre.vendor_id
    WHERE pre.price_event_id IS NULL -- artikli bez promene cene
) c
    ON t.category = c.category AND t.vendor_id = c.vendor_id
    AND ABS(t.pre_revenue - c.pre_revenue) < 0.2 * GREATEST(t.pre_revenue, 1)
LIMIT 10000;

-- 3) Rolling agregati: 7d moving average, momentum
CREATE OR REPLACE VIEW vw_sales_rolling_7d AS
SELECT
    ps."id_artikal" AS article_id,
    pz."datum_prodaje"::date AS day,
    SUM(ps."kolicina") AS units,
    SUM(ps."kolicina" * ps."cena") AS revenue,
    AVG(SUM(ps."kolicina" * ps."cena")) OVER (PARTITION BY ps."id_artikal" ORDER BY pz."datum_prodaje"::date ROWS BETWEEN 6 PRECEDING AND CURRENT ROW) AS ma7_revenue,
    AVG(SUM(ps."kolicina")) OVER (PARTITION BY ps."id_artikal" ORDER BY pz."datum_prodaje"::date ROWS BETWEEN 6 PRECEDING AND CURRENT ROW) AS ma7_units
FROM "prodaja_stavke" ps
JOIN "prodaja_zaglavlje" pz ON pz."id" = ps."id_prodaja"
GROUP BY ps."id_artikal", pz."datum_prodaje"::date;

-- 4) Momentum: poslednjih 7 dana vs prethodnih 7 dana
CREATE OR REPLACE VIEW vw_sales_momentum AS
SELECT
    article_id,
    MAX(day) AS last_day,
    SUM(CASE WHEN day > MAX(day) - INTERVAL '7 days' THEN units ELSE 0 END) AS last7_units,
    SUM(CASE WHEN day > MAX(day) - INTERVAL '7 days' THEN revenue ELSE 0 END) AS last7_revenue,
    SUM(CASE WHEN day BETWEEN MAX(day) - INTERVAL '14 days' AND MAX(day) - INTERVAL '7 days' THEN units ELSE 0 END) AS prev7_units,
    SUM(CASE WHEN day BETWEEN MAX(day) - INTERVAL '14 days' AND MAX(day) - INTERVAL '7 days' THEN revenue ELSE 0 END) AS prev7_revenue,
    (SUM(CASE WHEN day > MAX(day) - INTERVAL '7 days' THEN revenue ELSE 0 END) - SUM(CASE WHEN day BETWEEN MAX(day) - INTERVAL '14 days' AND MAX(day) - INTERVAL '7 days' THEN revenue ELSE 0 END)) AS momentum_revenue
FROM vw_sales_rolling_7d
GROUP BY article_id;

-- 5) % artikala u crvenoj zoni (low stock, OOS)
CREATE OR REPLACE VIEW vw_stock_red_zone AS
SELECT
    a."Id" AS article_id,
    a."Naziv" AS article_name,
    a."Kategorija" AS category,
    a."IDDobavljac" AS vendor_id,
    a."PLU" AS sku,
    a."StanjeZaliha" AS stock,
    CASE WHEN a."StanjeZaliha" IS NULL OR a."StanjeZaliha" <= 0 THEN 1 ELSE 0 END AS is_oos,
    CASE WHEN a."StanjeZaliha" <= COALESCE(a."MinimalnaZaliha", 1) THEN 1 ELSE 0 END AS is_low_stock
FROM "Artikli" a;

-- 6) Indeksi za ubrzanje
CREATE INDEX IF NOT EXISTS IX_prodaja_stavke_id_artikal_datum ON "prodaja_stavke" ("id_artikal", "id_prodaja");
CREATE INDEX IF NOT EXISTS IX_prodaja_zaglavlje_datum ON "prodaja_zaglavlje" ("datum_prodaje");
CREATE INDEX IF NOT EXISTS IX_Artikli_StanjeZaliha ON "Artikli" ("StanjeZaliha");
