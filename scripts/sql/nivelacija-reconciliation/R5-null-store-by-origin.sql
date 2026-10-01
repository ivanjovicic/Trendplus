-- RQ547 / R5: expose chain-wide/null-store event counts by origin.
BEGIN TRANSACTION READ ONLY;

WITH grouped AS (
    SELECT COALESCE(NULLIF(BTRIM(d."DataOrigin"), ''), '<unknown>') AS data_origin,
           COUNT(*)::bigint AS event_count,
           COUNT(*) FILTER (WHERE d."IDObjekat" IS NULL)::bigint AS null_store_events
    FROM "DnevnikPromena" d
    WHERE d."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
    GROUP BY COALESCE(NULLIF(BTRIM(d."DataOrigin"), ''), '<unknown>')
), summary AS (
    SELECT COUNT(*)::bigint AS origin_groups,
           COALESCE(SUM(event_count), 0)::bigint AS event_count,
           COALESCE(SUM(null_store_events), 0)::bigint AS null_store_events,
           COALESCE(string_agg(format('%s=%s/%s', data_origin, null_store_events, event_count), '; ' ORDER BY data_origin), 'none') AS by_origin
    FROM grouped
)
SELECT 'NV-P1-R5'::text AS check_id,
       CASE WHEN null_store_events > 0 THEN 'WARN'
            WHEN event_count = 0 THEN 'WARN' ELSE 'PASS' END::text AS verdict,
       format('events=%s; null_store=%s; origins=%s; by_origin=%s', event_count, null_store_events, origin_groups, by_origin) AS observed,
       'null IDObjekat population is measured and grouped by DataOrigin'::text AS expected,
       'RQ538'::text AS owner,
       'NULL store scope is reported as evidence; this check does not assign or rewrite a store.'::text AS detail
FROM summary;

ROLLBACK;
