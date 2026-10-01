-- RQ547 / R9: capture caller, search path, relation resolution, privileges and view columns.
BEGIN TRANSACTION READ ONLY;

WITH relation_state AS (
    SELECT current_user::text AS caller,
           current_schemas(true)::text AS effective_schemas,
           to_regclass('vw_vendor_sales_nivelacija') AS view_oid
), columns AS (
    SELECT string_agg(a.attname, ', ' ORDER BY a.attnum) AS column_list,
           COUNT(*) FILTER (WHERE a.attname IN ('price_event_id', 'event_date', 'article_id', 'pre_qty', 'post_qty'))::integer AS required_columns
    FROM relation_state r
    LEFT JOIN pg_attribute a
      ON a.attrelid = r.view_oid AND a.attnum > 0 AND NOT a.attisdropped
), access AS (
    SELECT r.caller, r.effective_schemas, r.view_oid,
           CASE WHEN r.view_oid IS NULL THEN false ELSE has_table_privilege(r.caller, r.view_oid, 'SELECT') END AS can_select,
           c.column_list, c.required_columns
    FROM relation_state r CROSS JOIN columns c
)
SELECT 'NV-P1-R9'::text AS check_id,
       CASE WHEN view_oid IS NULL OR NOT can_select OR required_columns < 5 THEN 'FAIL' ELSE 'PASS' END::text AS verdict,
       format('current_user=%s; current_schemas=%s; to_regclass=%s; select_privilege=%s; required_columns=%s/5; columns=%s',
              caller, effective_schemas, COALESCE(view_oid::text, 'missing'), can_select,
              required_columns, COALESCE(column_list, 'none')) AS observed,
       'view resolves for the caller, SELECT is available, and required semantic columns are present'::text AS expected,
       'RQ545'::text AS owner,
       'Catalog-only diagnostic. A missing view or privilege is returned as FAIL rather than aborting the pack.'::text AS detail
FROM access;

ROLLBACK;
