-- RQ611/RQ602 read-only coverage gate.
-- Copy exact action/store/source-batch/window rows from the frozen RQ602 manifest;
-- this helper does not change that manifest or calculate uplift.
-- A missing evidence row remains missing/unverified. Batch completion alone never
-- promotes it to verified_empty. Counts are source receipt/line counts, not sales KPIs.

WITH pilot_windows(action_id, store_id, source_batch_id, window_start, window_end_exclusive) AS (
    VALUES
        ('fixture-action-1'::text, 1::integer, 23::bigint, DATE '2026-07-04', DATE '2026-08-01'),
        ('fixture-action-1'::text, 1::integer, 23::bigint, DATE '2026-08-01', DATE '2026-08-29')
),
requested_days AS (
    SELECT w.action_id,
           w.store_id,
           w.source_batch_id,
           w.window_start,
           w.window_end_exclusive,
           generated.day::date AS business_date
    FROM pilot_windows w
    CROSS JOIN LATERAL generate_series(
        w.window_start::timestamp,
        (w.window_end_exclusive - 1)::timestamp,
        interval '1 day') AS generated(day)
),
latest_source_attempt AS (
    SELECT d.*,
           latest."Id" AS effective_source_batch_id,
           latest."Status" AS latest_status
    FROM (SELECT DISTINCT action_id, store_id, source_batch_id FROM requested_days) d
    JOIN "DataImportBatches" requested ON requested."Id" = d.source_batch_id
    LEFT JOIN LATERAL (
        SELECT attempt."Id", attempt."Status"
        FROM "DataImportBatches" attempt
        WHERE attempt."SourceSystem" = requested."SourceSystem"
          AND attempt."SourceStorageProvider" IS NOT DISTINCT FROM requested."SourceStorageProvider"
          AND COALESCE(attempt."SourceStorageKey", attempt."SourceFilePath", attempt."SourceFileName")
              = COALESCE(requested."SourceStorageKey", requested."SourceFilePath", requested."SourceFileName")
        ORDER BY attempt."StartedAtUtc" DESC, attempt."Id" DESC
        LIMIT 1
    ) latest ON TRUE
),
day_evidence AS (
    SELECT d.*,
           CASE
               WHEN latest.effective_source_batch_id IS NULL THEN 'missing'
               WHEN lower(btrim(COALESCE(latest.latest_status, ''))) IN ('pending', 'running') THEN 'partial'
               WHEN lower(btrim(COALESCE(latest.latest_status, ''))) <> 'completed' THEN 'failed'
               WHEN c.coverage_status IS NULL THEN 'missing'
               ELSE c.coverage_status
           END AS coverage_status,
           c.business_timezone,
           c.source_scope_fingerprint,
           c.source_generation,
           c.source_checkpoint_hash,
           c.source_header_table,
           c.source_line_table,
           c.receipt_count,
           c.sale_line_count,
           c.reason_code,
           latest.effective_source_batch_id
    FROM requested_days d
    JOIN latest_source_attempt latest
      ON latest.action_id = d.action_id
     AND latest.store_id = d.store_id
     AND latest.source_batch_id = d.source_batch_id
    LEFT JOIN access_import_store_day_coverage c
      ON c.source_batch_id = latest.effective_source_batch_id
     AND c.store_id = d.store_id
     AND c.business_date = d.business_date
)
SELECT action_id,
       store_id,
       source_batch_id,
       max(effective_source_batch_id) AS effective_source_batch_id,
       window_start,
       window_end_exclusive,
       COUNT(*)::integer AS requested_days,
       COUNT(*) FILTER (WHERE coverage_status = 'observed_sales')::integer AS observed_sales_days,
       COUNT(*) FILTER (WHERE coverage_status = 'verified_empty')::integer AS verified_empty_days,
       COUNT(*) FILTER (WHERE coverage_status = 'partial')::integer AS partial_days,
       COUNT(*) FILTER (WHERE coverage_status = 'failed')::integer AS failed_days,
       COUNT(*) FILTER (WHERE coverage_status IN ('missing', 'unverified'))::integer AS unverified_or_missing_days,
       jsonb_agg(jsonb_build_object(
           'date', business_date,
           'status', coverage_status,
           'receipts', receipt_count,
           'saleLines', sale_line_count,
           'reason', reason_code,
           'timezone', business_timezone,
           'sourceScopeFingerprint', source_scope_fingerprint,
           'generation', source_generation,
           'checkpointHash', source_checkpoint_hash,
           'headerTable', source_header_table,
           'lineTable', source_line_table
       ) ORDER BY business_date) AS days
FROM day_evidence
GROUP BY action_id, store_id, source_batch_id, window_start, window_end_exclusive
ORDER BY action_id, window_start;
