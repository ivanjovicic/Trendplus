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
), broken_product_fk AS (
    SELECT l.id
    FROM prodaja_stavke l
    LEFT JOIN "Artikli" a ON a."Id" = l.id_artikal
    WHERE l.source_namespace IS NOT NULL AND a."Id" IS NULL
), broken_return_parent AS (
    SELECT l.id
    FROM povracaj_stavke l
    LEFT JOIN povracaj_zaglavlje h ON h.id = l.id_povracaj
    WHERE l.source_namespace IS NOT NULL
      AND (h.id IS NULL OR h.source_namespace IS DISTINCT FROM l.source_namespace)
), broken_return_product_fk AS (
    SELECT l.id
    FROM povracaj_stavke l
    LEFT JOIN "Artikli" a ON a."Id" = l.id_artikal
    WHERE l.source_namespace IS NOT NULL AND a."Id" IS NULL
), broken_master_fk AS (
    SELECT a."Id"
    FROM "Artikli" a
    LEFT JOIN "Dobavljaci" d ON d."Id" = a."IDDobavljac"
    LEFT JOIN "TipoviObuce" t ON t."Id" = a."IDTipObuce"
    LEFT JOIN "Sezone" s ON s."Id" = a."IDSezona"
    WHERE a."DataOrigin" = 'access'
      AND ((a."IDDobavljac" IS NOT NULL AND d."Id" IS NULL)
        OR (a."IDTipObuce" IS NOT NULL AND t."Id" IS NULL)
        OR (a."IDSezona" IS NOT NULL AND s."Id" IS NULL))
), fact_amount_drift AS (
    SELECT h.source_namespace
    FROM prodaja_zaglavlje h
    LEFT JOIN prodaja_stavke l ON l.id_prodaja = h.id
    LEFT JOIN "SalesFacts" f
      ON f."SaleId" = h.id
     AND f."SourceNamespace" = h.source_namespace
    WHERE h.source_namespace IS NOT NULL
    GROUP BY h.source_namespace, h.id, f."TotalAmount", f."TotalUnits", f."TotalLines", f."SaleId"
    HAVING f."SaleId" IS NULL
        OR f."TotalAmount" IS DISTINCT FROM COALESCE(SUM(l.kolicina * l.cena), 0)
        OR f."TotalUnits" IS DISTINCT FROM COALESCE(SUM(l.kolicina), 0)::integer
        OR f."TotalLines" IS DISTINCT FROM COUNT(l.id)::integer
)
SELECT 'duplicate_headers' AS check_name, count(*)::bigint AS violation_count FROM duplicate_headers
UNION ALL SELECT 'duplicate_lines', count(*)::bigint FROM duplicate_lines
UNION ALL SELECT 'cross_namespace_or_missing_parent', count(*)::bigint FROM broken_lines
UNION ALL SELECT 'missing_sales_line_fact', count(*)::bigint FROM fact_drift
UNION ALL SELECT 'missing_product_fk', count(*)::bigint FROM broken_product_fk
UNION ALL SELECT 'cross_namespace_or_missing_return_parent', count(*)::bigint FROM broken_return_parent
UNION ALL SELECT 'missing_return_product_fk', count(*)::bigint FROM broken_return_product_fk
UNION ALL SELECT 'missing_master_fk', count(*)::bigint FROM broken_master_fk
UNION ALL SELECT 'sales_fact_amount_or_quantity_drift', count(*)::bigint FROM fact_amount_drift
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

-- Provenance coverage is a separate read-only result set for baseline dry-runs.
-- It never infers identity from names, dates, receipt numbers or amounts.
SELECT 'all' AS population, count(*)::bigint AS row_count FROM prodaja_zaglavlje
UNION ALL
SELECT 'imported', count(*)::bigint FROM prodaja_zaglavlje
 WHERE data_origin = 'access' AND source_namespace IS NOT NULL
UNION ALL
SELECT 'existing', count(*)::bigint FROM prodaja_zaglavlje
 WHERE lower(coalesce(data_origin, '')) <> 'access'
UNION ALL
SELECT 'unknown_provenance', count(*)::bigint FROM prodaja_zaglavlje
 WHERE source_namespace IS NULL OR source_table_key IS NULL OR source_row_id IS NULL
UNION ALL
SELECT 'unmatched', count(*)::bigint
FROM prodaja_stavke l
LEFT JOIN prodaja_zaglavlje h
  ON h.id = l.id_prodaja
 AND h.source_namespace IS NOT DISTINCT FROM l.source_namespace
WHERE l.source_namespace IS NOT NULL AND h.id IS NULL
ORDER BY population;
