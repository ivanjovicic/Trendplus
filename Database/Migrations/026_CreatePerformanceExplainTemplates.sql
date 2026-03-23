-- Stores ready-to-run EXPLAIN templates used for diagnostics.
-- This does not execute plans; it only stores SQL templates.

CREATE TABLE IF NOT EXISTS "__PerformanceExplainTemplates" (
    "TemplateKey" character varying(128) PRIMARY KEY,
    "Title" character varying(256) NOT NULL,
    "TargetDatabase" character varying(32) NOT NULL,
    "Description" text NOT NULL,
    "SqlTemplate" text NOT NULL,
    "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW()
);

INSERT INTO "__PerformanceExplainTemplates" (
    "TemplateKey",
    "Title",
    "TargetDatabase",
    "Description",
    "SqlTemplate",
    "UpdatedAtUtc"
)
VALUES
(
    'analytics_top_products',
    'Top Products aggregation',
    'analytics',
    'Use after replacing :fromDate, :toDate and :storeId (optional).',
    'EXPLAIN (ANALYZE, BUFFERS)
SELECT slf."ProductId", COALESCE(SUM(slf."LineTotal"), 0.0) AS "TotalRevenue", COALESCE(SUM(slf."Qty"), 0)::int AS "TotalSold"
FROM "SalesFacts" sf
JOIN "SalesLineFacts" slf ON sf."SaleId" = slf."SaleId"
WHERE sf."SaleTimestampUtc" >= :fromDate
  AND sf."SaleTimestampUtc" <= :toDate
  AND (:storeId IS NULL OR sf."StoreId" = :storeId)
GROUP BY slf."ProductId";',
    NOW()
),
(
    'analytics_sales_summary',
    'Sales Summary aggregates',
    'analytics',
    'Use with :fromDate, :toDate and :storeId (optional).',
    'EXPLAIN (ANALYZE, BUFFERS)
SELECT
    COALESCE(SUM(sf."TotalAmount"), 0.0) AS total_revenue,
    COUNT(*)::int AS total_transactions,
    COALESCE(SUM(sf."TotalUnits"), 0)::int AS total_units
FROM "SalesFacts" sf
WHERE sf."SaleTimestampUtc" >= :fromDate
  AND sf."SaleTimestampUtc" <= :toDate
  AND (:storeId IS NULL OR sf."StoreId" = :storeId);',
    NOW()
),
(
    'trendplus_data_quality_issues',
    'Data Quality Issues',
    'trendplus',
    'Use with :salesFromUtc, :issueType, :queryPattern, :pageSize and :offset.',
    'EXPLAIN (ANALYZE, BUFFERS)
WITH sales_30d AS (
    SELECT
        ps.id_artikal AS artikal_id,
        COALESCE(SUM(ps.kolicina * ps.cena), 0) AS sales_30d
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje p ON p.id = ps.id_prodaja
    WHERE p.datum_prodaje >= :salesFromUtc
    GROUP BY ps.id_artikal
),
quality_source AS (
    SELECT
        a."PLU" AS sku,
        a."Id" AS product_id,
        NULLIF(BTRIM(a."Naziv"), '''') AS product_name,
        a."IDDobavljac" AS supplier_id,
        NULLIF(BTRIM(d."Naziv"), '''') AS supplier_name,
        a."IDTipObuce" AS shoe_type_id,
        NULLIF(BTRIM(t."Naziv"), '''') AS shoe_type_name,
        CASE
            WHEN a."IDDobavljac" IS NULL OR d."Id" IS NULL THEN ''missingSupplier''
            WHEN a."IDTipObuce" IS NULL OR t."Id" IS NULL THEN ''missingShoeType''
            WHEN NULLIF(BTRIM(a."Naziv"), '''') IS NULL
                 OR (a."IDDobavljac" IS NOT NULL AND NULLIF(BTRIM(d."Naziv"), '''') IS NULL)
                 OR (a."IDTipObuce" IS NOT NULL AND NULLIF(BTRIM(t."Naziv"), '''') IS NULL)
                THEN ''invalidName''
            ELSE ''ok''
        END AS issue_type,
        COALESCE(s.sales_30d, 0) AS sales_30d,
        COALESCE(a."Kolicina", 0) AS stock,
        a."UpdatedAt" AS last_updated
    FROM "Artikli" a
    LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
    LEFT JOIN "TipoviObuce" t ON a."IDTipObuce" = t."Id"
    LEFT JOIN sales_30d s ON s.artikal_id = a."Id"
)
SELECT
    sku,
    product_id,
    product_name,
    supplier_id,
    supplier_name,
    shoe_type_id,
    shoe_type_name,
    issue_type,
    sales_30d,
    stock,
    last_updated,
    COUNT(*) OVER() AS total_count
FROM quality_source
WHERE issue_type = :issueType
  AND (
      :queryPattern = ''''
      OR COALESCE(sku, '''') ILIKE :queryPattern
      OR COALESCE(product_name, '''') ILIKE :queryPattern
      OR COALESCE(supplier_name, '''') ILIKE :queryPattern
      OR COALESCE(shoe_type_name, '''') ILIKE :queryPattern
  )
ORDER BY sales_30d DESC, last_updated DESC, product_id ASC
LIMIT :pageSize
OFFSET :offset;',
    NOW()
),
(
    'analytics_inventory_alert_snapshot',
    'Inventory Alert snapshot',
    'analytics',
    'Use with :storeId, :supplierId, :severity and :top.',
    'EXPLAIN (ANALYZE, BUFFERS)
SELECT
    COALESCE(alert_type, ''unknown'') AS alert_type,
    sku_id,
    store_id,
    size_code,
    COALESCE(severity, ''info'') AS severity,
    COALESCE(title, ''Alert'') AS title,
    COALESCE(message, '''') AS message,
    CAST(COALESCE(confidence_score, 0) AS numeric(18,4)) AS confidence_score
FROM analytics_inventory_alert_snapshot
WHERE (:storeId IS NULL OR store_id = :storeId)
  AND (:supplierId IS NULL OR supplier_id = :supplierId)
  AND (:severity IS NULL OR severity = :severity)
ORDER BY
    CASE COALESCE(severity, ''info'')
        WHEN ''critical'' THEN 0
        WHEN ''warning'' THEN 1
        ELSE 2
    END,
    confidence_score DESC
LIMIT :top;',
    NOW()
),
(
    'analytics_rebalance_snapshot',
    'Rebalance Suggestion snapshot',
    'analytics',
    'Use with :fromStoreId, :toStoreId, :supplierId, :urgency and :top.',
    'EXPLAIN (ANALYZE, BUFFERS)
SELECT
    from_store_id,
    to_store_id,
    sku_id,
    COALESCE(size_code, ''UNKNOWN'') AS size_code,
    COALESCE(recommended_qty, 0) AS recommended_qty,
    COALESCE(urgency, ''normal'') AS urgency,
    CAST(COALESCE(confidence, 0) AS numeric(18,4)) AS confidence,
    COALESCE(reason, ''snapshot'') AS reason,
    CAST(COALESCE(expected_saved_sales, 0) AS numeric(18,2)) AS expected_saved_sales,
    CAST(COALESCE(expected_capital_release, 0) AS numeric(18,2)) AS expected_capital_release
FROM analytics_rebalance_suggestion_snapshot
WHERE (:fromStoreId IS NULL OR from_store_id = :fromStoreId)
  AND (:toStoreId IS NULL OR to_store_id = :toStoreId)
  AND (:supplierId IS NULL OR supplier_id = :supplierId)
  AND (:urgency IS NULL OR urgency = :urgency)
ORDER BY confidence DESC, expected_saved_sales DESC, recommended_qty DESC
LIMIT :top;',
    NOW()
)
ON CONFLICT ("TemplateKey") DO UPDATE
SET
    "Title" = EXCLUDED."Title",
    "TargetDatabase" = EXCLUDED."TargetDatabase",
    "Description" = EXCLUDED."Description",
    "SqlTemplate" = EXCLUDED."SqlTemplate",
    "UpdatedAtUtc" = NOW();
