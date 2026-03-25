-- ==========================================================
-- 014_CreateVendorSalesNivelacijaViews.sql
-- Analytics-native nivelacija views.
--
-- Depends on:
-- - Database/Analytics/013_AddSupplierDecisionCompatibilitySchema.sql
-- - Database/Migrations/017_CreateNightlyAnalyticsMaterializedViews.sql
-- ==========================================================

-- Safety: CREATE OR REPLACE VIEW cannot remove/reorder columns from an existing
-- view (Postgres 42P16). If the column structure changed since the last run we
-- must drop the old views first.  CASCADE removes dependents (vw_nivelacija_did,
-- supplier hub views); those are recreated by 016 and 018 scripts that run after.
DO $$
DECLARE
    _actual  text[];
    _expect  text[] := ARRAY[
        'price_event_id','event_date','article_id','sku','article_name',
        'category','vendor_id','vendor_name','old_price','new_price',
        'pre_qty','pre_revenue','coverage_pre30','valid_days_pre30','is_low_signal'
    ];
BEGIN
    SELECT array_agg(c.column_name::text ORDER BY c.ordinal_position)
      INTO _actual
      FROM information_schema.columns c
     WHERE c.table_schema = current_schema()
       AND c.table_name   = 'vw_sales_pre_nivelacija';

    -- View doesn't exist yet or columns match → nothing to drop.
    IF _actual IS NOT NULL AND _actual IS DISTINCT FROM _expect THEN
        RAISE NOTICE '014: vw_sales_pre_nivelacija column structure changed – dropping cascade';
        DROP VIEW IF EXISTS vw_vendor_sales_nivelacija CASCADE;
        DROP VIEW IF EXISTS vw_sales_post_nivelacija CASCADE;
        DROP VIEW IF EXISTS vw_sales_pre_nivelacija CASCADE;
    END IF;
END$$;

CREATE OR REPLACE VIEW vw_sales_pre_nivelacija AS
WITH nivelacija_events AS (
    SELECT *
    FROM (
        SELECT
            d."Id"::bigint AS price_event_id,
            COALESCE(src."Datum", d."Datum")::date AS event_date,
            a."Id" AS article_id,
            COALESCE(NULLIF(a."PLU", ''), a."Id"::text) AS sku,
            a."Naziv" AS article_name,
            a."Kategorija" AS category,
            COALESCE(d."DobavljacId", a."IDDobavljac") AS vendor_id,
            dob."Naziv" AS vendor_name,
            d."StaraProdajnaCena"::numeric(18,4) AS old_price,
            d."NovaProdajnaCena"::numeric(18,4) AS new_price,
            ROW_NUMBER() OVER (
                PARTITION BY a."Id",
                             COALESCE(src."Datum", d."Datum"),
                             d."StaraProdajnaCena",
                             d."NovaProdajnaCena"
                ORDER BY d."Id" DESC
            ) AS rn
        FROM "DnevnikPromena" d
        JOIN "Artikli" a ON a."Id" = d."ArtikalId"
        LEFT JOIN "Dobavljaci" dob
            ON dob."Id" = COALESCE(d."DobavljacId", a."IDDobavljac")
        LEFT JOIN "DnevnikPromena" src
            ON src."Id" = CASE
                WHEN d."BrojRacuna" ~ '^[0-9]+$'
                THEN d."BrojRacuna"::integer
            END
        WHERE d."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
          AND d."ArtikalId" IS NOT NULL
          AND COALESCE(src."Datum", d."Datum") IS NOT NULL
    ) x
    WHERE rn = 1
),
sales_daily AS (
    SELECT
        ps.id_artikal AS article_id,
        pz.datum_prodaje::date AS day,
        SUM(ps.kolicina)::numeric AS units,
        SUM(ps.kolicina * ps.cena)::numeric(18,2) AS revenue
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz
      ON pz.id = ps.id_prodaja
    GROUP BY ps.id_artikal, pz.datum_prodaje::date
)
SELECT
    e.price_event_id,
    e.event_date,
    e.article_id,
    e.sku,
    e.article_name,
    e.category,
    e.vendor_id,
    e.vendor_name,
    e.old_price,
    e.new_price,
    COALESCE(SUM(s.units), 0) AS pre_qty,
    COALESCE(SUM(s.revenue), 0) AS pre_revenue,
    LEAST(COUNT(DISTINCT s.day) / 30.0, 1) AS coverage_pre30,
    COUNT(DISTINCT s.day) AS valid_days_pre30,
    (
        COUNT(DISTINCT s.day) < 7
        OR COALESCE(SUM(s.units), 0) < 3
        OR COALESCE(SUM(s.revenue), 0) < 100
    ) AS is_low_signal
FROM nivelacija_events e
LEFT JOIN sales_daily s
  ON s.article_id = e.article_id
 AND s.day >= e.event_date - INTERVAL '30 days'
 AND s.day < e.event_date
GROUP BY
    e.price_event_id,
    e.event_date,
    e.article_id,
    e.sku,
    e.article_name,
    e.category,
    e.vendor_id,
    e.vendor_name,
    e.old_price,
    e.new_price;

-- prodaja_stavke nema datum_prodaje kolonu; koristi join preko prodaja_zaglavlje.
-- Zato ovde ne pravimo dodatne indekse nad prodaja_* relacijama:
-- - u analytics compatibility schemi to mogu biti VIEW objekti (neindexabilni)
-- - u analytics fact-layeru potrebni indeksi vec postoje na "SalesFacts"/"SalesLineFacts"
-- - u trendplus bazi odgovarajuci indeksi vec pripadaju migration skriptama 013/014
-- Ova skripta treba da bude fokusirana samo na view definicije.

CREATE OR REPLACE VIEW vw_sales_post_nivelacija AS
WITH nivelacija_events AS (
    SELECT * FROM vw_sales_pre_nivelacija
),
sales_daily AS (
    SELECT
        ps.id_artikal AS article_id,
        pz.datum_prodaje::date AS day,
        SUM(ps.kolicina)::numeric AS units,
        SUM(ps.kolicina * ps.cena)::numeric(18,2) AS revenue
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz
      ON pz.id = ps.id_prodaja
    GROUP BY ps.id_artikal, pz.datum_prodaje::date
)
SELECT
    e.price_event_id,
    e.event_date,
    e.vendor_id,
    e.vendor_name,
    e.article_id,
    e.sku,
    e.article_name,
    e.category,
    e.old_price,
    e.new_price,
    COALESCE(SUM(s.units), 0) AS post_qty,
    COALESCE(SUM(s.revenue), 0) AS post_revenue,
    LEAST(COUNT(DISTINCT s.day) / 30.0, 1) AS coverage_post30,
    COUNT(DISTINCT s.day) AS valid_days_post30
FROM nivelacija_events e
LEFT JOIN sales_daily s
  ON s.article_id = e.article_id
 AND s.day >= e.event_date
 AND s.day < e.event_date + INTERVAL '30 days'
GROUP BY
    e.price_event_id,
    e.event_date,
    e.vendor_id,
    e.vendor_name,
    e.article_id,
    e.sku,
    e.article_name,
    e.category,
    e.old_price,
    e.new_price;

-- Safety: check vw_vendor_sales_nivelacija columns before replace.
DO $$
DECLARE
    _actual  text[];
    _expect  text[] := ARRAY[
        'price_event_id','event_date','vendor_id','vendor_name',
        'article_id','sku','article_name','category','old_price','new_price',
        'pre_qty','post_qty','pre_revenue','post_revenue',
        'coverage_pre30','coverage_post30',
        'change_qty','change_revenue','change_percent_qty','change_percent_revenue',
        'is_low_signal'
    ];
BEGIN
    SELECT array_agg(c.column_name::text ORDER BY c.ordinal_position)
      INTO _actual
      FROM information_schema.columns c
     WHERE c.table_schema = current_schema()
       AND c.table_name   = 'vw_vendor_sales_nivelacija';

    IF _actual IS NOT NULL AND _actual IS DISTINCT FROM _expect THEN
        RAISE NOTICE '014: vw_vendor_sales_nivelacija column structure changed – dropping cascade';
        DROP VIEW IF EXISTS vw_vendor_sales_nivelacija CASCADE;
    END IF;
END$$;

CREATE OR REPLACE VIEW vw_vendor_sales_nivelacija AS
SELECT
    pre.price_event_id,
    pre.event_date,
    pre.vendor_id,
    pre.vendor_name,
    pre.article_id,
    pre.sku,
    pre.article_name,
    pre.category,
    pre.old_price,
    pre.new_price,
    pre.pre_qty::numeric AS pre_qty,
    post.post_qty::numeric AS post_qty,
    pre.pre_revenue::numeric(18,2) AS pre_revenue,
    post.post_revenue::numeric(18,2) AS post_revenue,
    pre.coverage_pre30,
    post.coverage_post30,
    (post.post_qty - pre.pre_qty) AS change_qty,
    (post.post_revenue - pre.pre_revenue) AS change_revenue,
    CASE
        WHEN pre.pre_qty = 0 AND post.post_qty > 0 THEN 100
        WHEN pre.pre_qty = 0 THEN 0
        ELSE ROUND(((post.post_qty - pre.pre_qty) / NULLIF(pre.pre_qty, 0)) * 100, 2)
    END AS change_percent_qty,
    CASE
        WHEN pre.pre_revenue = 0 AND post.post_revenue > 0 THEN 100
        WHEN pre.pre_revenue = 0 THEN 0
        ELSE ROUND(((post.post_revenue - pre.pre_revenue) / NULLIF(pre.pre_revenue, 0)) * 100, 2)
    END AS change_percent_revenue,
    (pre.is_low_signal OR post.coverage_post30 < 0.2) AS is_low_signal
FROM vw_sales_pre_nivelacija pre
LEFT JOIN vw_sales_post_nivelacija post
  ON pre.price_event_id = post.price_event_id;

COMMENT ON VIEW vw_vendor_sales_nivelacija IS
'Analytics-native pre/post markdown comparison view built over SalesFacts, ProductsDim and InventoryMovementFacts compatibility views.';
