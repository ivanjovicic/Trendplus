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
    _post_revenue_type text;
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

    -- Legacy definitions expose post_revenue as plain numeric; CREATE OR REPLACE VIEW
    -- cannot change a column type (42P16), so rebuild the post/vendor views.
    SELECT format_type(a.atttypid, a.atttypmod)
      INTO _post_revenue_type
      FROM pg_attribute a
      JOIN pg_class c ON c.oid = a.attrelid
      JOIN pg_namespace n ON n.oid = c.relnamespace
     WHERE n.nspname = current_schema()
       AND c.relname = 'vw_sales_post_nivelacija'
       AND a.attname = 'post_revenue'
       AND NOT a.attisdropped;

    IF _post_revenue_type IS NOT NULL AND _post_revenue_type <> 'numeric(18,2)' THEN
        RAISE NOTICE '014: vw_sales_post_nivelacija post_revenue type % is outdated – dropping cascade', _post_revenue_type;
        DROP VIEW IF EXISTS vw_vendor_sales_nivelacija CASCADE;
        DROP VIEW IF EXISTS vw_sales_post_nivelacija CASCADE;
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
        LEFT JOIN LATERAL (
            SELECT CASE
                WHEN d."BrojRacuna" ~ '^[0-9]+$'
                 AND (
                        length(COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0')) < 19
                     OR (
                            length(COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0')) = 19
                        AND COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0') <= '9223372036854775807'
                     )
                 )
                THEN COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0')::bigint
            END AS source_dnevnik_id
        ) receipt_reference ON TRUE
        LEFT JOIN "DnevnikPromena" src
            ON src."Id"::bigint = receipt_reference.source_dnevnik_id
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
    -- Canonical retail receipt population (SalesReceiptPopulationPolicy).
    WHERE UPPER(TRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
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
    SUM(s.units) AS pre_qty,
    SUM(s.revenue) AS pre_revenue,
    -- Legacy column name retained for compatibility: this is sale-day activity
    -- (distinct sale days / 30), never authoritative data completeness.
    CASE WHEN COUNT(DISTINCT s.day) = 0 THEN NULL
         ELSE LEAST(COUNT(DISTINCT s.day) / 30.0, 1)
    END AS coverage_pre30,
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
    -- Canonical retail receipt population (SalesReceiptPopulationPolicy).
    WHERE UPPER(TRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
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
    CASE
        WHEN e.event_date + INTERVAL '30 days' <= CURRENT_DATE THEN COALESCE(SUM(s.units), 0)
        ELSE SUM(s.units)
    END AS post_qty,
    CASE
        WHEN e.event_date + INTERVAL '30 days' <= CURRENT_DATE THEN COALESCE(SUM(s.revenue), 0)::numeric(18,2)
        ELSE SUM(s.revenue)::numeric(18,2)
    END AS post_revenue,
    -- Legacy column name retained for compatibility: this is sale-day activity
    -- (distinct sale days / 30), never authoritative data completeness.
    CASE WHEN COUNT(DISTINCT s.day) = 0 THEN NULL
         ELSE LEAST(COUNT(DISTINCT s.day) / 30.0, 1)
    END AS coverage_post30,
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
        'is_low_signal','has_qty_baseline','qty_baseline_reason','change_percent_qty_semantic',
        'has_revenue_baseline','revenue_baseline_reason','change_percent_revenue_semantic',
        'price_direction','discount_depth_pct','post_window_complete',
        'overlaps_next_event','next_event_date','same_day_event_count'
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
WITH event_sequence AS (
    SELECT
        price_event_id,
        LEAD(event_date) OVER (
            PARTITION BY article_id
            ORDER BY event_date, price_event_id
        ) AS next_event_date,
        COUNT(*) OVER (PARTITION BY article_id, event_date)::integer AS same_day_event_count
    FROM vw_sales_pre_nivelacija
)
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
        WHEN pre.pre_qty = 0 AND COALESCE(post.post_qty, 0) > 0 THEN NULL
        WHEN pre.pre_qty = 0 THEN 0
        ELSE ROUND(((COALESCE(post.post_qty, 0) - pre.pre_qty) / NULLIF(pre.pre_qty, 0)) * 100, 2)
    END AS change_percent_qty,
    CASE
        WHEN pre.pre_revenue = 0 AND COALESCE(post.post_revenue, 0) > 0 THEN NULL
        WHEN pre.pre_revenue = 0 THEN 0
        ELSE ROUND(((COALESCE(post.post_revenue, 0) - pre.pre_revenue) / NULLIF(pre.pre_revenue, 0)) * 100, 2)
    END AS change_percent_revenue,
    (pre.is_low_signal OR post.coverage_post30 < 0.2) AS is_low_signal,
    COALESCE(pre.pre_qty > 0, FALSE) AS has_qty_baseline,
    CASE
        WHEN pre.pre_qty IS NULL THEN 'missing_pre_qty_window'
        WHEN pre.pre_qty = 0 AND post.post_qty > 0 THEN 'no_pre_qty_baseline_uplift'
        WHEN pre.pre_qty = 0 AND post.post_qty = 0 THEN 'no_pre_qty_baseline_flat'
        ELSE NULL
    END AS qty_baseline_reason,
    CASE
        WHEN pre.pre_qty = 0 THEN NULL
        ELSE ROUND(((post.post_qty - pre.pre_qty) / NULLIF(pre.pre_qty, 0)) * 100, 2)
    END AS change_percent_qty_semantic,
    COALESCE(pre.pre_revenue > 0, FALSE) AS has_revenue_baseline,
    CASE
        WHEN pre.pre_revenue IS NULL THEN 'missing_pre_revenue_window'
        WHEN pre.pre_revenue = 0 AND post.post_revenue > 0 THEN 'no_pre_revenue_baseline_uplift'
        WHEN pre.pre_revenue = 0 AND post.post_revenue = 0 THEN 'no_pre_revenue_baseline_flat'
        ELSE NULL
    END AS revenue_baseline_reason,
    CASE
        WHEN pre.pre_revenue = 0 THEN NULL
        ELSE ROUND(((post.post_revenue - pre.pre_revenue) / NULLIF(pre.pre_revenue, 0)) * 100, 2)
    END AS change_percent_revenue_semantic,
    CASE
        WHEN pre.old_price IS NULL OR pre.new_price IS NULL THEN NULL
        WHEN pre.new_price < pre.old_price THEN 'markdown'
        WHEN pre.new_price > pre.old_price THEN 'markup'
        ELSE 'flat'
    END AS price_direction,
    CASE
        WHEN pre.old_price IS NULL OR pre.new_price IS NULL OR pre.old_price <= 0 THEN NULL
        ELSE ROUND(((pre.old_price - pre.new_price) / pre.old_price) * 100, 2)
    END AS discount_depth_pct,
    (pre.event_date + 30 <= CURRENT_DATE) AS post_window_complete,
    COALESCE(sequence.next_event_date < pre.event_date + 30, FALSE) AS overlaps_next_event,
    sequence.next_event_date,
    sequence.same_day_event_count
FROM vw_sales_pre_nivelacija pre
LEFT JOIN vw_sales_post_nivelacija post
  ON pre.price_event_id = post.price_event_id
JOIN event_sequence sequence ON sequence.price_event_id = pre.price_event_id;

COMMENT ON VIEW vw_vendor_sales_nivelacija IS
'Analytics-native 30-day price-event comparison built from DnevnikPromena, Artikli, Dobavljaci, prodaja_stavke and prodaja_zaglavlje. Post-window totals remain partial until post_window_complete is true; price direction, overlapping event windows and same-day event counts are explicit.';
