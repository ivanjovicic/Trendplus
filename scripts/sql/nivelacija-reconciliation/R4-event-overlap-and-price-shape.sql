-- RQ547 / R4: summarize event price direction and temporal overlap.
BEGIN TRANSACTION READ ONLY;

WITH events AS (
    SELECT d."Id"::bigint AS event_id,
           d."ArtikalId"::bigint AS article_id,
           COALESCE(src."Datum", d."Datum")::date AS event_date,
           d."StaraProdajnaCena"::numeric AS old_price,
           d."NovaProdajnaCena"::numeric AS new_price
    FROM "DnevnikPromena" d
    LEFT JOIN "DnevnikPromena" src
      ON d."BrojRacuna" ~ '^[0-9]+$'
     AND length(d."BrojRacuna") <= 10
     AND src."Id" = CASE WHEN d."BrojRacuna" ~ '^[0-9]+$' AND length(d."BrojRacuna") <= 10
                          THEN d."BrojRacuna"::bigint END
    WHERE d."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
      AND d."ArtikalId" IS NOT NULL
), sequenced AS (
    SELECT e.*,
           LEAD(event_date) OVER (PARTITION BY article_id ORDER BY event_date, event_id) AS next_event_date,
           COUNT(*) OVER (PARTITION BY article_id, event_date) AS same_day_count
    FROM events e
), summary AS (
    SELECT COUNT(*)::bigint AS event_count,
           COUNT(*) FILTER (WHERE new_price > old_price)::bigint AS markup_events,
           COUNT(*) FILTER (WHERE new_price = old_price)::bigint AS flat_events,
           COUNT(*) FILTER (WHERE same_day_count > 1)::bigint AS same_day_multi_event_rows,
           COUNT(*) FILTER (WHERE next_event_date < event_date + 30)::bigint AS next_event_within_30d
    FROM sequenced
)
SELECT 'NV-P1-R4'::text AS check_id,
       CASE WHEN same_day_multi_event_rows > 0 OR next_event_within_30d > 0 THEN 'WARN'
            WHEN event_count = 0 THEN 'WARN' ELSE 'PASS' END::text AS verdict,
       format('events=%s; markups=%s (%s%%); flat=%s (%s%%); same_day_multi_event_rows=%s; next_event_within_30d=%s',
              event_count, markup_events,
              COALESCE(ROUND(100.0 * markup_events / NULLIF(event_count, 0), 2)::text, 'n/a'),
              flat_events,
              COALESCE(ROUND(100.0 * flat_events / NULLIF(event_count, 0), 2)::text, 'n/a'),
              same_day_multi_event_rows, next_event_within_30d) AS observed,
       'event shape and overlap counts are available; overlapping or same-day events are flagged for review'::text AS expected,
       'RQ547'::text AS owner,
       'Markup/flat shares are descriptive evidence and are not interpreted as a pricing defect.'::text AS detail
FROM summary;

ROLLBACK;
