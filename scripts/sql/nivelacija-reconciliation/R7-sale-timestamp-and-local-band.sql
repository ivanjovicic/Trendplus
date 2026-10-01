-- RQ547 / R7: report the source timestamp type and sale count in the Belgrade 22:00–02:00 band.
BEGIN TRANSACTION READ ONLY;

WITH relation AS (
    SELECT to_regclass('prodaja_zaglavlje') AS relation_oid
), column_info AS (
    SELECT r.relation_oid,
           a.atttypid,
           format_type(a.atttypid, a.atttypmod) AS column_type
    FROM relation r
    LEFT JOIN pg_attribute a
      ON a.attrelid = r.relation_oid AND a.attname = 'datum_prodaje' AND a.attnum > 0 AND NOT a.attisdropped
), scan AS (
    SELECT c.*,
           CASE
             WHEN c.relation_oid IS NOT NULL AND c.atttypid = 'timestamp with time zone'::regtype THEN
               query_to_xml($q$SELECT COUNT(*)::bigint AS band_sales
                              FROM prodaja_zaglavlje
                             WHERE (datum_prodaje AT TIME ZONE 'Europe/Belgrade')::time >= TIME '22:00'
                                OR (datum_prodaje AT TIME ZONE 'Europe/Belgrade')::time < TIME '02:00'$q$, false, false, '')
             WHEN c.relation_oid IS NOT NULL AND c.atttypid = 'timestamp without time zone'::regtype THEN
               query_to_xml($q$SELECT COUNT(*)::bigint AS band_sales
                              FROM prodaja_zaglavlje
                             WHERE datum_prodaje::time >= TIME '22:00'
                                OR datum_prodaje::time < TIME '02:00'$q$, false, false, '')
             ELSE '<table/>'::xml
           END AS payload
    FROM column_info c
), result AS (
    SELECT s.relation_oid,
           s.column_type,
           s.atttypid IN ('timestamp with time zone'::regtype, 'timestamp without time zone'::regtype) AS supported_type,
           x.band_sales
    FROM scan s
    LEFT JOIN LATERAL XMLTABLE('/table/row' PASSING s.payload COLUMNS band_sales bigint PATH 'band_sales') x ON true
)
SELECT 'NV-P1-R7'::text AS check_id,
       CASE WHEN relation_oid IS NULL OR column_type IS NULL OR NOT supported_type OR band_sales IS NULL THEN 'FAIL'
            WHEN band_sales > 0 OR column_type = 'timestamp without time zone' THEN 'WARN'
            ELSE 'PASS' END::text AS verdict,
       format('column_type=%s; sales_22_00_to_02_00_local=%s; timezone_basis=%s',
              COALESCE(column_type, 'missing'), COALESCE(band_sales::text, 'unavailable'),
              CASE WHEN column_type = 'timestamp with time zone' THEN 'Europe/Belgrade conversion'
                   WHEN column_type = 'timestamp without time zone' THEN 'stored local wall time; source zone unknown'
                   ELSE 'unknown' END) AS observed,
       'timestamp type and local late-night sales count are known; ambiguous wall-time basis is flagged'::text AS expected,
       'RQ547'::text AS owner,
       'Counts evidence only; it does not reinterpret or update sale timestamps.'::text AS detail
FROM result;

ROLLBACK;
