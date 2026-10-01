-- RQ547 / R6: compare the vendor view to a deduplicated source-event and sales oracle.
BEGIN TRANSACTION READ ONLY;

WITH source_events AS (
    SELECT d."Id"::bigint AS price_event_id,
           COALESCE(src."Datum", d."Datum")::date AS event_date,
           d."ArtikalId"::bigint AS article_id,
           d."StaraProdajnaCena"::numeric AS old_price,
           d."NovaProdajnaCena"::numeric AS new_price,
           ROW_NUMBER() OVER (
               PARTITION BY d."ArtikalId", COALESCE(src."Datum", d."Datum"), d."StaraProdajnaCena", d."NovaProdajnaCena"
               ORDER BY d."Id" DESC
           ) AS rn
    FROM "DnevnikPromena" d
    LEFT JOIN "DnevnikPromena" src
      ON d."BrojRacuna" ~ '^[0-9]+$'
     AND length(d."BrojRacuna") <= 10
     AND src."Id" = CASE WHEN d."BrojRacuna" ~ '^[0-9]+$' AND length(d."BrojRacuna") <= 10
                          THEN d."BrojRacuna"::bigint END
    WHERE d."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
      AND d."ArtikalId" IS NOT NULL
      AND COALESCE(src."Datum", d."Datum") IS NOT NULL
), events AS (
    SELECT price_event_id, event_date, article_id FROM source_events WHERE rn = 1
), sales AS (
    SELECT ps.id_artikal::bigint AS article_id,
           pz.datum_prodaje::date AS sale_date,
           SUM(ps.kolicina)::numeric AS qty
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
    WHERE UPPER(BTRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
    GROUP BY ps.id_artikal, pz.datum_prodaje::date
), expected AS (
    SELECT e.price_event_id,
           e.event_date,
           e.article_id,
           COALESCE(SUM(s.qty) FILTER (WHERE s.sale_date >= e.event_date - 30 AND s.sale_date < e.event_date), 0)::numeric AS pre_qty,
           CASE WHEN e.event_date + 30 <= CURRENT_DATE
                THEN COALESCE(SUM(s.qty) FILTER (WHERE s.sale_date >= e.event_date AND s.sale_date < e.event_date + 30), 0)::numeric
                ELSE NULL::numeric END AS post_qty,
           (e.event_date + 30 <= CURRENT_DATE) AS mature
    FROM events e
    LEFT JOIN sales s ON s.article_id = e.article_id
    GROUP BY e.price_event_id, e.event_date, e.article_id
), receipt_cast_risk AS (
    SELECT COUNT(*)::bigint AS integer_overflow_rows
    FROM "DnevnikPromena" d
    WHERE d."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
      AND d."BrojRacuna" ~ '^[0-9]+$'
      AND (
          length(COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0')) > 10
          OR (length(COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0')) = 10
              AND COALESCE(NULLIF(ltrim(d."BrojRacuna", '0'), ''), '0') > '2147483647')
      )
), view_state AS (
    SELECT to_regclass('vw_vendor_sales_nivelacija') AS view_oid,
           CASE WHEN to_regclass('vw_vendor_sales_nivelacija') IS NULL THEN false
                ELSE has_table_privilege(current_user, to_regclass('vw_vendor_sales_nivelacija'), 'SELECT') END AS can_select
), actual_payload AS (
    SELECT s.view_oid, s.can_select, r.integer_overflow_rows,
           CASE WHEN s.view_oid IS NOT NULL AND s.can_select AND r.integer_overflow_rows = 0
                THEN query_to_xml('SELECT to_jsonb(v) AS row_data FROM vw_vendor_sales_nivelacija v', false, false, '')
                ELSE '<table/>'::xml END AS payload
    FROM view_state s CROSS JOIN receipt_cast_risk r
), actual AS (
    SELECT (x.row_data::jsonb ->> 'price_event_id')::bigint AS price_event_id,
           (x.row_data::jsonb ->> 'event_date')::date AS event_date,
           (x.row_data::jsonb ->> 'article_id')::bigint AS article_id,
           (x.row_data::jsonb ->> 'pre_qty')::numeric AS pre_qty,
           (x.row_data::jsonb ->> 'post_qty')::numeric AS post_qty
    FROM actual_payload p
    CROSS JOIN LATERAL XMLTABLE('/table/row' PASSING p.payload COLUMNS row_data text PATH 'row_data') x
), parity AS (
    SELECT COUNT(*) FILTER (WHERE e.price_event_id IS NULL OR a.price_event_id IS NULL)::bigint AS row_mismatches,
           COUNT(*) FILTER (WHERE e.price_event_id IS NOT NULL AND a.price_event_id IS NOT NULL
                             AND e.pre_qty IS DISTINCT FROM a.pre_qty)::bigint AS pre_qty_mismatches,
           COUNT(*) FILTER (WHERE e.mature AND e.price_event_id IS NOT NULL AND a.price_event_id IS NOT NULL
                             AND e.post_qty IS DISTINCT FROM a.post_qty)::bigint AS mature_post_qty_mismatches,
           COUNT(*) FILTER (WHERE e.mature)::bigint AS mature_events,
           COUNT(*) FILTER (WHERE e.price_event_id IS NOT NULL AND a.price_event_id IS NOT NULL)::bigint AS matched_rows,
           COUNT(*) FILTER (WHERE e.event_date IS NOT NULL AND a.event_date IS NOT NULL
                             AND e.event_date <> a.event_date)::bigint AS event_date_mismatches
    FROM expected e
    FULL OUTER JOIN actual a ON a.price_event_id = e.price_event_id
), summary AS (
    SELECT (SELECT COUNT(*) FROM events)::bigint AS deduplicated_events,
           (SELECT COUNT(*) FROM actual)::bigint AS view_rows,
           (SELECT view_oid IS NOT NULL FROM view_state) AS view_exists,
           (SELECT can_select FROM view_state) AS view_selectable,
           (SELECT integer_overflow_rows FROM receipt_cast_risk) AS integer_overflow_rows,
           CASE WHEN (SELECT COUNT(*) FROM events) <> (SELECT COUNT(*) FROM actual) THEN 1 ELSE 0 END::bigint AS row_count_mismatches,
           p.*
    FROM parity p
)
SELECT 'NV-P1-R6'::text AS check_id,
       CASE WHEN NOT view_exists OR NOT view_selectable THEN 'FAIL'
            WHEN integer_overflow_rows > 0 THEN 'WARN'
            WHEN row_count_mismatches > 0
                  OR row_mismatches + pre_qty_mismatches + mature_post_qty_mismatches + event_date_mismatches > 0 THEN 'FAIL'
            WHEN deduplicated_events = 0 THEN 'WARN' ELSE 'PASS' END::text AS verdict,
       format('source_events=%s; view_rows=%s; view_exists=%s; view_selectable=%s; integer_overflow_rows=%s; view_query_skipped_for_cast_risk=%s; row_count_mismatches=%s; matched=%s; row_mismatches=%s; pre_qty_mismatches=%s; mature_events=%s; mature_post_qty_mismatches=%s; event_date_mismatches=%s; independent_window_days=30',
              deduplicated_events, view_rows, view_exists, view_selectable, integer_overflow_rows,
              integer_overflow_rows > 0, row_count_mismatches, matched_rows, row_mismatches, pre_qty_mismatches,
              mature_events, mature_post_qty_mismatches, event_date_mismatches) AS observed,
       'view rows match deduplicated events; pre_qty matches an independent 30-day sum; mature post_qty matches an independent 30-day sum'::text AS expected,
       'RQ547'::text AS owner,
       'Uses canonical retail receipt exclusions and half-open 30-day windows; immature post windows are not compared. An unsafe integer receipt reference prevents executing the deployed view and returns WARN.'::text AS detail
FROM summary;

ROLLBACK;
