-- RQ547 / R3: check source-event history coverage and DiD self-controls without assuming volume.
BEGIN TRANSACTION READ ONLY;

WITH events AS (
    SELECT d."Id"::bigint AS source_dnevnik_id, d."ArtikalId"::bigint AS article_id
    FROM "DnevnikPromena" d
    WHERE d."TipPromene" IN ('Nivelacija', 'Nivelacija cena')
      AND d."ArtikalId" IS NOT NULL
), history_state AS (
    SELECT to_regclass('price_history') IS NOT NULL AS relation_exists,
           CASE WHEN to_regclass('price_history') IS NULL THEN '<table/>'::xml
                ELSE query_to_xml('SELECT source_dnevnik_id FROM price_history', false, false, '')
           END AS payload
), history_ids AS (
    SELECT x.source_dnevnik_id
    FROM history_state h
    CROSS JOIN LATERAL XMLTABLE(
        '/table/row' PASSING h.payload
        COLUMNS source_dnevnik_id bigint PATH 'source_dnevnik_id'
    ) AS x
), did_state AS (
    SELECT to_regclass('vw_nivelacija_did') IS NOT NULL AS relation_exists,
           CASE WHEN to_regclass('vw_nivelacija_did') IS NULL THEN '<table/>'::xml
                ELSE query_to_xml('SELECT article_id, control_article_id FROM vw_nivelacija_did', false, false, '')
           END AS payload
), did_rows AS (
    SELECT x.article_id, x.control_article_id
    FROM did_state d
    CROSS JOIN LATERAL XMLTABLE(
        '/table/row' PASSING d.payload
        COLUMNS article_id bigint PATH 'article_id',
                control_article_id bigint PATH 'control_article_id'
    ) AS x
), summary AS (
    SELECT (SELECT COUNT(*) FROM events)::bigint AS event_count,
           (SELECT relation_exists FROM history_state) AS history_exists,
           (SELECT relation_exists FROM did_state) AS did_exists,
           (SELECT COUNT(*) FROM events e LEFT JOIN history_ids h USING (source_dnevnik_id)
             WHERE h.source_dnevnik_id IS NULL)::bigint AS missing_history,
           (SELECT COUNT(*) FROM did_rows WHERE article_id = control_article_id)::bigint AS self_controls
)
SELECT
    'NV-P1-R3'::text AS check_id,
    CASE WHEN NOT history_exists OR NOT did_exists OR missing_history > 0 OR self_controls > 0 THEN 'FAIL'
         WHEN event_count = 0 THEN 'WARN'
         ELSE 'PASS' END::text AS verdict,
    format('events=%s; price_history_missing=%s; did_self_controls=%s; history_relation=%s; did_relation=%s',
           event_count, missing_history, self_controls, history_exists, did_exists) AS observed,
    'history covers every canonical source event and no DiD control is its own test article'::text AS expected,
    'RQ542'::text AS owner,
    'Missing optional relations are reported as FAIL instead of aborting this check.'::text AS detail
FROM summary;

ROLLBACK;
