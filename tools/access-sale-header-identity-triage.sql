-- Access sale-header 23505: READ-ONLY identity and referential diagnostics.
-- Run against an AUTHORIZED COPY or with SELECT-only credentials.
-- Requires existing prodaja_zaglavlje / prodaja_stavke and lineage columns.
-- There is deliberately NO INSERT, UPDATE, DELETE, DDL, setval, sequence advance or backfill.
-- Source identifiers and row keys returned by the optional conflict sample below are
-- operational metadata. Do not paste raw query output into public GitHub issues.
-- The original PostgresException.ConstraintName + batch/step + failed input source key
-- is REQUIRED to attribute an individual failure; these queries alone cannot identify
-- which attempted source row triggered the rollback.

BEGIN TRANSACTION READ ONLY;

-- 1. Discover the ACTUAL primary/unique index definitions (migration assumptions
-- are not proof that this deployed DB matches). Confirm PK and lineage index exist.
SELECT
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = ANY(current_schemas(false))
  AND tablename = 'prodaja_zaglavlje'
  AND (indexdef ILIKE '%UNIQUE%' OR indexname ILIKE '%pkey%')
ORDER BY schemaname, indexname;

-- 2. Source/target provenance overview. Missing lineage is not permission to match
-- on receipt/date/price or to convert an existing/POS record to access.
SELECT
    COALESCE(data_origin, '<NULL>') AS data_origin,
    COALESCE(source_table_key, '<NULL>') AS source_table_key,
    COUNT(*) AS headers,
    COUNT(*) FILTER (WHERE source_row_id IS NULL) AS missing_source_row_id,
    COUNT(*) FILTER (WHERE source_table_key IS NULL) AS missing_source_table,
    COUNT(*) FILTER (WHERE source_batch_id IS NULL) AS missing_batch_id,
    COUNT(DISTINCT source_row_id) FILTER (WHERE source_row_id IS NOT NULL) AS distinct_source_row_ids,
    MIN(source_batch_id) AS earliest_recorded_batch_id,
    MAX(source_batch_id) AS latest_recorded_batch_id
FROM prodaja_zaglavlje
GROUP BY data_origin, source_table_key
ORDER BY headers DESC, data_origin, source_table_key;

-- 3. Check whether more than one target header claims the same legacy source
-- identity. A healthy enforced unique index should return no rows, but an old
-- unmigrated schema or NULL source key may produce collisions.
SELECT
    data_origin,
    source_table_key,
    source_row_id,
    COUNT(*) AS mapped_headers,
    MIN(id) AS smallest_target_id,
    MAX(id) AS largest_target_id
FROM prodaja_zaglavlje
WHERE data_origin = 'access'
  AND source_table_key IS NOT NULL
  AND source_row_id IS NOT NULL
GROUP BY data_origin, source_table_key, source_row_id
HAVING COUNT(*) > 1
ORDER BY mapped_headers DESC, source_table_key, source_row_id
LIMIT 100;

-- 4. Potential numeric-ID collision evidence: the *source ID* of one mapped
-- Access header equals the *target ID* of another header. This establishes why
-- using access_id as target PK is UNSAFE; it does NOT identify a failed import row.
SELECT
    a.source_table_key,
    a.source_row_id,
    a.id AS mapped_target_id,
    t.id AS numeric_collision_target_id,
    t.data_origin AS other_target_origin,
    t.source_table_key AS other_source_table_key,
    t.source_row_id AS other_source_row_id
FROM prodaja_zaglavlje AS a
JOIN prodaja_zaglavlje AS t
  ON t.id = a.source_row_id
 AND t.id <> a.id
WHERE a.data_origin = 'access'
  AND a.source_row_id BETWEEN 1 AND 2147483647
ORDER BY a.source_table_key, a.source_row_id
LIMIT 100;

-- 5. Legacy imported sale-header/line population and broken header FKs.
SELECT
    COUNT(*) AS headers_total,
    COUNT(*) FILTER (WHERE data_origin = 'access') AS access_headers,
    COUNT(*) FILTER (WHERE data_origin = 'access' AND (source_row_id IS NULL OR source_table_key IS NULL)) AS access_headers_without_full_lineage
FROM prodaja_zaglavlje;

SELECT
    COUNT(*) AS lines_total,
    COUNT(*) FILTER (WHERE h.id IS NULL) AS lines_without_header,
    COUNT(*) FILTER (WHERE h.data_origin = 'access') AS lines_attached_to_access_headers,
    COUNT(*) FILTER (WHERE h.data_origin = 'access' AND (s.source_row_id IS NULL OR s.source_table_key IS NULL)) AS access_parent_lines_without_full_lineage,
    COUNT(*) FILTER (WHERE h.data_origin = 'existing') AS lines_attached_to_existing_headers
FROM prodaja_stavke AS s
LEFT JOIN prodaja_zaglavlje AS h ON h.id = s.id_prodaja;

-- 6. The legacy table+row pair has NO stable source-database namespace today.
-- The same pair may mean different receipts in separate Access databases.
-- Only an operator-authoritative source namespace can resolve that ambiguity.
-- This script deliberately does not choose source ownership or reparent children.

COMMIT;
