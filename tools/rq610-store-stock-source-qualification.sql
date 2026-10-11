-- RQ610 read-only source qualification: candidate SKU/store stock from operational Artikli
-- Run on *confirmed* local business PostgreSQL only. NO INSERT/UPDATE/DELETE/DDL.
-- Do not infer a store/day history or a verified zero day from current Artikli rows.
-- Preserve IDObjekat NULL as UNKNOWN; SourceBatchId is import lineage, not POS update generation.

SELECT current_database() AS database_name,
       current_schema() AS schema_name,
       current_user AS role_name,
       current_setting('server_version') AS postgres_version,
       pg_is_in_recovery() AS read_replica;

SELECT
  COALESCE(a."DataOrigin", '<NULL>') AS data_origin,
  a."IDObjekat" AS store_id,
  count(*) AS article_rows,
  count(*) FILTER (WHERE a."Kolicina" IS NOT NULL) AS qty_observed_rows,
  count(*) FILTER (WHERE a."Kolicina" = 0) AS measured_zero_rows,
  count(*) FILTER (WHERE a."Kolicina" > 0) AS positive_rows,
  sum(CASE WHEN a."Kolicina" > 0 THEN a."Kolicina" ELSE 0 END) AS positive_on_hand,
  count(*) FILTER (WHERE a."Kolicina" < 0) AS negative_rows,
  count(*) FILTER (WHERE a."IDObjekat" IS NULL) AS unknown_store_rows,
  count(*) FILTER (WHERE a."SourceBatchId" IS NOT NULL) AS batch_lineage_rows,
  count(*) FILTER (WHERE a."SourceBatchId" = 23) AS batch_23_rows,
  count(*) FILTER (WHERE a."SourceRowId" IS NOT NULL AND a."SourceTableKey" IS NOT NULL) AS source_identity_rows,
  min(a."UpdatedAt") AS earliest_operational_updated_at,
  max(a."UpdatedAt") AS latest_operational_updated_at,
  max(a."SourceUpdatedAtUtc") AS latest_source_updated_at
FROM "Artikli" a
GROUP BY COALESCE(a."DataOrigin", '<NULL>'), a."IDObjekat"
ORDER BY data_origin, store_id NULLS LAST;

-- Per-article source identity / quantity alignment with the company-grain ProductsDim.
-- This is a *comparison*, not proof of equivalent time bases or authoritative store grain.
SELECT
  count(*) AS article_rows,
  count(*) FILTER (WHERE a."IDObjekat" IS NOT NULL) AS rows_with_store_id,
  count(*) FILTER (WHERE a."IDObjekat" IS NULL) AS unknown_store_rows,
  count(*) FILTER (WHERE a."DataOrigin" = 'access') AS access_article_rows,
  count(*) FILTER (WHERE a."SourceBatchId" = 23) AS batch_23_article_rows,
  count(*) FILTER (WHERE p."ProductId" IS NULL) AS articles_without_product_dim,
  count(*) FILTER (WHERE p."ProductId" IS NOT NULL AND a."Kolicina" IS DISTINCT FROM p."Kolicina") AS qty_different_from_dim,
  count(*) FILTER (WHERE p."ProductId" IS NOT NULL AND a."DataOrigin" IS DISTINCT FROM p."DataOrigin") AS origin_different_from_dim,
  count(*) FILTER (WHERE p."ProductId" IS NOT NULL AND a."UpdatedAt" IS DISTINCT FROM p."Timestamp") AS timestamp_different_from_dim,
  count(*) FILTER (WHERE a."SourceBatchId" IS NULL AND a."DataOrigin" = 'access') AS access_rows_without_batch_identity
FROM "Artikli" a
LEFT JOIN "ProductsDim" p ON p."ProductId" = a."Id";

-- Same PLU can legitimately represent distinct store rows. Never merge on PLU alone.
SELECT
  count(*) AS multi_store_plu_groups,
  COALESCE(sum(store_count), 0) AS summed_distinct_stores_in_groups,
  COALESCE(sum(article_count), 0) AS summed_article_rows_in_groups
FROM (
  SELECT lower(btrim(a."PLU")) AS sku_key,
         count(DISTINCT a."IDObjekat") AS store_count,
         count(*) AS article_count
  FROM "Artikli" a
  WHERE a."PLU" IS NOT NULL AND btrim(a."PLU") <> '' AND a."IDObjekat" IS NOT NULL
  GROUP BY lower(btrim(a."PLU"))
  HAVING count(DISTINCT a."IDObjekat") > 1
) src;

-- Product dimension has a company-level key. A join multiplying quantities by
-- StoresDim or a transfer document does NOT supply a per-store observed balance.
-- Read-only evidence must be combined with importer field mapping, POS/transfer
-- update semantics, and a disposable two-store PostgreSQL regression fixture.
