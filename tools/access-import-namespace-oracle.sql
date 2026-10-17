-- STAB17 read-only source -> header -> line -> fact oracle.
-- Run against the isolated validation database after each synthetic Access import.
-- It deliberately contains no DDL/DML and does not infer a namespace for legacy rows.

WITH access_headers AS (
    SELECT source_namespace, source_table_key, source_row_id, id AS target_sale_id
    FROM prodaja_zaglavlje
    WHERE data_origin = 'access'
), duplicate_headers AS (
    SELECT source_namespace, source_table_key, source_row_id, count(*) AS row_count
    FROM access_headers
    WHERE source_namespace IS NOT NULL AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL
    GROUP BY source_namespace, source_table_key, source_row_id
    HAVING count(*) <> 1
), broken_lines AS (
    SELECT l.id, l.source_namespace, l.source_table_key, l.source_row_id, l.id_prodaja
    FROM prodaja_stavke l
    LEFT JOIN access_headers h ON h.target_sale_id = l.id_prodaja
    WHERE l.source_namespace IS NOT NULL
      AND (h.target_sale_id IS NULL OR h.source_namespace <> l.source_namespace)
), duplicate_lines AS (
    SELECT source_namespace, source_table_key, source_row_id, count(*) AS row_count
    FROM prodaja_stavke
    WHERE source_namespace IS NOT NULL AND source_table_key IS NOT NULL AND source_row_id IS NOT NULL
    GROUP BY source_namespace, source_table_key, source_row_id
    HAVING count(*) <> 1
), fact_drift AS (
    SELECT l.id AS sale_line_id, l.id_prodaja, f."Id" AS fact_id, f."SaleId", f."SourceNamespace"
    FROM prodaja_stavke l
    LEFT JOIN "SalesLineFacts" f
      ON f."SaleId" = l.id_prodaja
     AND f."SourceNamespace" = l.source_namespace
     AND f."SourceTableKey" = l.source_table_key
     AND f."SourceLineId" = l.source_row_id
    WHERE l.source_namespace IS NOT NULL
      AND l.source_row_id IS NOT NULL
      AND f."Id" IS NULL
)
SELECT 'duplicate_headers' AS check_name, count(*)::bigint AS violation_count FROM duplicate_headers
UNION ALL SELECT 'duplicate_lines', count(*)::bigint FROM duplicate_lines
UNION ALL SELECT 'cross_namespace_or_missing_parent', count(*)::bigint FROM broken_lines
UNION ALL SELECT 'missing_sales_line_fact', count(*)::bigint FROM fact_drift
UNION ALL
SELECT 'legacy_unmapped_access_rows', count(*)::bigint
FROM prodaja_stavke
WHERE source_namespace IS NULL
  AND (source_table_key IS NOT NULL OR source_row_id IS NOT NULL)
ORDER BY check_name;

-- The following query is the idempotency oracle: save its output before and
-- after a replay of exactly the same synthetic file. All four counts must match.
SELECT
    source_namespace,
    count(*) FILTER (WHERE data_origin = 'access') AS header_count,
    (SELECT count(*) FROM prodaja_stavke l WHERE l.source_namespace = h.source_namespace) AS line_count,
    (SELECT count(*) FROM "SalesFacts" f WHERE f."SourceNamespace" = h.source_namespace) AS fact_count,
    (SELECT count(*) FROM "SalesLineFacts" f WHERE f."SourceNamespace" = h.source_namespace) AS line_fact_count
FROM prodaja_zaglavlje h
WHERE source_namespace IS NOT NULL
GROUP BY source_namespace
ORDER BY source_namespace;
