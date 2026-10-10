-- Trendplus Access import #23 analytical-fact audit
-- Read-only evidence pack. Run with psql against the local trendplus database.
-- No statement in this file mutates data, schema, cache or materialized views.

BEGIN;
SET TRANSACTION READ ONLY;

-- 1. Import batch and Access operational population.
SELECT "Id", "SourceSystem", "SourceFileName", "QueuedAtUtc", "StartedAtUtc",
       "CompletedAtUtc", "LastHeartbeatUtc", "Status", "ProgressPercent",
       "RowsRead", "RowsAccepted", "RowsWritten", "ProcessedRowCount",
       "RowsInserted", "RowsUpdated", "RowsRejected", "CancellationRequested",
       "RetryCount", "DataOrigin"
FROM "DataImportBatches"
WHERE "Id" = 23;

SELECT 'access_headers' AS population, count(*) AS rows,
       min(p.datum_prodaje) AS min_date, max(p.datum_prodaje) AS max_date
FROM prodaja_zaglavlje p
WHERE p.source_batch_id = 23
UNION ALL
SELECT 'access_lines', count(*), min(pz.datum_prodaje), max(pz.datum_prodaje)
FROM prodaja_stavke ps
JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
WHERE ps.source_batch_id = 23;

-- 2. Access header/line identity and amount parity.
SELECT 'access_headers_without_fact' AS check_name, count(*) AS rows
FROM prodaja_zaglavlje p
LEFT JOIN "SalesFacts" sf ON sf."SaleId" = p.id
WHERE p.source_batch_id = 23 AND sf."Id" IS NULL
UNION ALL
SELECT 'access_lines_without_fact', count(*)
FROM prodaja_stavke ps
JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
LEFT JOIN "SalesLineFacts" slf
  ON slf."SaleId" = ps.id_prodaja
 AND slf."SourceLineId" = ps.id
WHERE ps.source_batch_id = 23 AND slf."Id" IS NULL
UNION ALL
SELECT 'access_line_amount_delta',
       count(*) FILTER (WHERE COALESCE(slf."Qty",0) <> ps.kolicina
                         OR COALESCE(slf."UnitPrice",0) <> ps.cena
                         OR COALESCE(slf."LineTotal",0) <> ps.kolicina * ps.cena)
FROM prodaja_stavke ps
JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
JOIN "SalesLineFacts" slf
  ON slf."SaleId" = ps.id_prodaja
 AND slf."SourceLineId" = ps.id
WHERE ps.source_batch_id = 23;

-- 3. Raw fact versus canonical operational retail population by scope.
SELECT 'raw_salesfacts' AS source, scope, count(*) AS receipts,
       COALESCE(sum(units),0) AS units, COALESCE(sum(amount),0) AS amount
FROM (
    SELECT CASE WHEN sf."DataOrigin" = 'access' THEN 'imported' ELSE 'existing' END AS scope,
           sf."TotalUnits" AS units, sf."TotalAmount" AS amount
    FROM "SalesFacts" sf
) x
GROUP BY scope
UNION ALL
SELECT 'raw_salesfacts', 'all', count(*), COALESCE(sum("TotalUnits"),0), COALESCE(sum("TotalAmount"),0)
FROM "SalesFacts"
UNION ALL
SELECT 'canonical_operational_retail',
       CASE WHEN p.data_origin = 'access' THEN 'imported' ELSE 'existing' END,
       count(DISTINCT p.id), COALESCE(sum(ps.kolicina),0),
       COALESCE(sum(ps.kolicina * ps.cena),0)
FROM prodaja_zaglavlje p
JOIN prodaja_stavke ps ON ps.id_prodaja = p.id
WHERE upper(trim(p.broj_racuna)) NOT IN ('DUG','KOREKCIJA')
GROUP BY 2
UNION ALL
SELECT 'canonical_operational_retail', 'all', count(DISTINCT p.id),
       COALESCE(sum(ps.kolicina),0), COALESCE(sum(ps.kolicina * ps.cena),0)
FROM prodaja_zaglavlje p
JOIN prodaja_stavke ps ON ps.id_prodaja = p.id
WHERE upper(trim(p.broj_racuna)) NOT IN ('DUG','KOREKCIJA');

-- 4. Unmatched fact rows, grouped by business identity/date/store.
SELECT sf."DataOrigin", sf."SaleTimestampUtc"::date AS sale_day, sf."StoreId",
       sf."BrojRacuna", count(*) AS rows, sum(sf."TotalUnits") AS units,
       sum(sf."TotalAmount") AS amount, min(sf."SaleId") AS min_sale_id,
       max(sf."SaleId") AS max_sale_id
FROM "SalesFacts" sf
LEFT JOIN prodaja_zaglavlje p ON p.id = sf."SaleId"
WHERE p.id IS NULL
GROUP BY sf."DataOrigin", sf."SaleTimestampUtc"::date, sf."StoreId", sf."BrojRacuna"
ORDER BY sale_day, sf."BrojRacuna";

SELECT slf."DataOrigin",
       CASE WHEN p.id IS NULL THEN 'no_operational_receipt' ELSE 'operational_fixture' END AS population,
       slf."SaleId", p.broj_racuna, p.datum_prodaje::date AS sale_day,
       count(*) AS rows, sum(slf."Qty") AS units, sum(slf."LineTotal") AS amount
FROM "SalesLineFacts" slf
LEFT JOIN prodaja_zaglavlje p ON p.id = slf."SaleId"
WHERE slf."SourceLineId" IS NULL
GROUP BY slf."DataOrigin", population, slf."SaleId", p.broj_racuna, p.datum_prodaje::date
ORDER BY population, slf."SaleId";

-- 5. RQ604 source-line contract and excluded retail population.
SELECT ps.source_table_key, slf."SourceTableKey", count(*) AS matched_lines
FROM prodaja_stavke ps
JOIN "SalesLineFacts" slf
  ON slf."SaleId" = ps.id_prodaja AND slf."SourceLineId" = ps.id
WHERE ps.source_batch_id = 23
GROUP BY ps.source_table_key, slf."SourceTableKey";

SELECT upper(trim(p.broj_racuna)) AS receipt_marker, count(DISTINCT p.id) AS receipts,
       count(ps.id) AS lines, sum(ps.kolicina) AS units,
       sum(ps.kolicina * ps.cena) AS amount
FROM prodaja_zaglavlje p
JOIN prodaja_stavke ps ON ps.id_prodaja = p.id
WHERE p.source_batch_id = 23
  AND upper(trim(p.broj_racuna)) IN ('DUG','KOREKCIJA')
GROUP BY upper(trim(p.broj_racuna));

-- 6. Dimension orphans and their fact impact.
SELECT pd."ProductId", pd."ProductName", pd."DataOrigin", pd."SupplierId",
       pd."Timestamp"::date AS updated_day, count(slf.*) AS fact_lines,
       COALESCE(sum(slf."Qty"),0) AS units, COALESCE(sum(slf."LineTotal"),0) AS amount
FROM "ProductsDim" pd
LEFT JOIN "Artikli" a ON a."Id" = pd."ProductId"
LEFT JOIN "SalesLineFacts" slf ON slf."ProductId" = pd."ProductId"
WHERE a."Id" IS NULL
GROUP BY pd."ProductId", pd."ProductName", pd."DataOrigin", pd."SupplierId", pd."Timestamp"
ORDER BY pd."ProductId";

SELECT sd."SupplierId", sd."Naziv", sd."DataOrigin", sd."UpdatedAt"::date AS updated_day,
       count(slf.*) AS fact_lines, COALESCE(sum(slf."Qty"),0) AS units,
       COALESCE(sum(slf."LineTotal"),0) AS amount
FROM "SuppliersDim" sd
LEFT JOIN "Dobavljaci" d ON d."Id" = sd."SupplierId"
LEFT JOIN "ProductsDim" pd ON pd."SupplierId" = sd."SupplierId"
LEFT JOIN "SalesLineFacts" slf ON slf."ProductId" = pd."ProductId"
WHERE d."Id" IS NULL
GROUP BY sd."SupplierId", sd."Naziv", sd."DataOrigin", sd."UpdatedAt"
ORDER BY sd."DataOrigin", sd."SupplierId";

-- 7. Aggregate/cache double-count risk inventory.
SELECT 'AnalyticsDailySummary' AS object_name, count(*) AS rows,
       min("Date") AS min_day, max("Date") AS max_day,
       COALESCE(sum("TotalRevenue"),0) AS revenue
FROM "AnalyticsDailySummary"
UNION ALL
SELECT 'daily_sales_facts', count(*), min(day), max(day), COALESCE(sum(revenue),0)
FROM daily_sales_facts
UNION ALL
SELECT 'mv_daily_sales_facts', count(*), min(day), max(day), COALESCE(sum(revenue),0)
FROM mv_daily_sales_facts;

-- 8. Verify current lock state without touching sessions.
SELECT pid, state, xact_start, wait_event_type, wait_event,
       left(query, 250) AS query
FROM pg_stat_activity
WHERE datname = current_database() AND pid <> pg_backend_pid()
ORDER BY pid;

SELECT blocked.pid AS blocked_pid, blocking.pid AS blocking_pid,
       blocked.wait_event_type, blocked.wait_event,
       left(blocked.query, 180) AS blocked_query
FROM pg_stat_activity blocked
JOIN pg_locks bl ON bl.pid = blocked.pid AND NOT bl.granted
JOIN pg_locks kl ON kl.locktype = bl.locktype
  AND kl.database IS NOT DISTINCT FROM bl.database
  AND kl.relation IS NOT DISTINCT FROM bl.relation
  AND kl.page IS NOT DISTINCT FROM bl.page
  AND kl.tuple IS NOT DISTINCT FROM bl.tuple
  AND kl.virtualxid IS NOT DISTINCT FROM bl.virtualxid
  AND kl.transactionid IS NOT DISTINCT FROM bl.transactionid
  AND kl.classid IS NOT DISTINCT FROM bl.classid
  AND kl.objid IS NOT DISTINCT FROM bl.objid
  AND kl.objsubid IS NOT DISTINCT FROM bl.objsubid
  AND kl.granted
JOIN pg_stat_activity blocking ON blocking.pid = kl.pid
WHERE blocked.datname = current_database()
  AND blocking.datname = current_database();

ROLLBACK;
