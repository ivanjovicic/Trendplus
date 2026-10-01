-- RQ524: read-only Supplier analytics reconciliation pack.
-- Purpose: turn Supplier hypotheses into explicit PASS / FAIL / EXPLAINED evidence.
-- Safety: SELECT-only. RQ524 is repository-local/fixture-only; any production/replica execution is owned by RQ454/STAB16.
-- psql usage:
--   \set from_utc '2026-09-01T00:00:00Z'
--   \set to_utc   '2026-09-30T23:59:59.999999Z'
--   \set store_id 'NULL'
--
-- Fixture bootstrap (repository-local only):
--   psql ... -f Api.Tests/Fixtures/operations-analytics-all-routes-seed.sql
--   plus the Supplier schema objects required by SUP-005/SUP-006 (see RQ525 local sequence or app startup).
--   Missing optional objects (score cache MV, vendor view, startup history) produce their owned
--   FAIL/EXPLAINED verdicts instead of aborting the pack.
-- Executable proof: Api.Tests/SupplierReconciliationPackTests.cs (Testcontainers PostgreSQL).
--
-- Result columns are intentionally stable:
-- check_id, verdict, observed, expected, owner, detail
--
-- PASS      = observed contract is satisfied.
-- FAIL      = deterministic contract violation; owner identifies the repair lane.
-- EXPLAINED = population exists but is intentionally excluded/different or needs
--             owner-reviewed interpretation before changing product behavior.

BEGIN TRANSACTION READ ONLY;

WITH
params AS (
    SELECT
        :'from_utc'::timestamptz AS from_utc,
        :'to_utc'::timestamptz AS to_utc,
        NULLIF(:'store_id', 'NULL')::integer AS store_id
),
sales AS (
    SELECT
        pz.id AS sale_id,
        pz.broj_racuna AS receipt_no,
        pz.datum_prodaje AS sold_at,
        pz.id_objekat AS store_id,
        ps.id_artikal AS article_id,
        ps.kolicina AS quantity,
        ps.cena AS unit_price,
        ps.nabavna_cena AS line_cost,
        ps.supplier_id_at_sale,
        a."IDDobavljac" AS supplier_id,
        a."NabavnaCenaDin" AS product_cost_rsd,
        a."NabavnaCena" AS product_cost_legacy
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
    JOIN "Artikli" a ON a."Id" = ps.id_artikal
    CROSS JOIN params p
    WHERE pz.datum_prodaje >= p.from_utc
      AND pz.datum_prodaje <= p.to_utc
      AND (p.store_id IS NULL OR pz.id_objekat = p.store_id)
),
classified AS (
    SELECT
        s.*,
        UPPER(BTRIM(COALESCE(s.receipt_no, ''))) IN ('DUG', 'KOREKCIJA') AS excluded_receipt,
        COALESCE(
            NULLIF(CASE WHEN s.line_cost > 0 THEN s.line_cost END, 0),
            NULLIF(CASE WHEN s.product_cost_rsd > 0 THEN s.product_cost_rsd END, 0),
            NULLIF(CASE WHEN s.product_cost_legacy > 0 THEN s.product_cost_legacy END, 0)
        ) AS resolved_cost
    FROM sales s
),
retail AS (
    SELECT * FROM classified WHERE NOT excluded_receipt
),
previous_retail AS (
    -- Equally long window immediately before from_utc; `retail` is already limited to the selected window.
    SELECT a."IDDobavljac" AS supplier_id
    FROM prodaja_stavke ps
    JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja
    JOIN "Artikli" a ON a."Id" = ps.id_artikal
    CROSS JOIN params p
    WHERE pz.datum_prodaje >= p.from_utc - (p.to_utc - p.from_utc)
      AND pz.datum_prodaje < p.from_utc
      AND (p.store_id IS NULL OR pz.id_objekat = p.store_id)
      AND UPPER(BTRIM(COALESCE(pz.broj_racuna, ''))) NOT IN ('DUG', 'KOREKCIJA')
),
current_suppliers AS (
    SELECT DISTINCT supplier_id
    FROM retail
    WHERE supplier_id IS NOT NULL
),
previous_only_suppliers AS (
    SELECT DISTINCT p.supplier_id
    FROM previous_retail p
    WHERE p.supplier_id IS NOT NULL
      AND NOT EXISTS (
          SELECT 1
          FROM current_suppliers c
          WHERE c.supplier_id = p.supplier_id
      )
),
attribution_drift AS (
    SELECT COUNT(*) AS drift_rows
    FROM retail r
    WHERE r.supplier_id_at_sale IS NOT NULL
      AND r.supplier_id IS NOT NULL
      AND r.supplier_id_at_sale <> r.supplier_id
),
overview_revenue AS (
    SELECT
        COALESCE(SUM(r.quantity * r.unit_price), 0) AS retail_revenue
    FROM retail r
),
-- Optional objects are read through to_regclass + query_to_xml so a missing object yields
-- its owned FAIL/EXPLAINED verdict instead of aborting the whole pack with 42P01.
scorecard_rows AS (
    SELECT CASE
        WHEN to_regclass('public.mv_supplier_decision_score_cache') IS NULL THEN 0
        ELSE (xpath('/row/n/text()', query_to_xml(
            'SELECT COUNT(*) AS n FROM public.mv_supplier_decision_score_cache',
            false, true, '')))[1]::text::bigint
    END AS row_count
),
vendor_assortment_xml AS (
    SELECT CASE
        WHEN to_regclass('public.vw_vendor_sales_nivelacija') IS NULL THEN NULL
        ELSE query_to_xml(
            'SELECT
                 COUNT(*) AS row_count,
                 COUNT(*) FILTER (WHERE pre_revenue IS NOT NULL AND post_revenue IS NOT NULL) AS comparable_total,
                 COUNT(*) FILTER (
                     WHERE pre_revenue IS NOT NULL AND post_revenue IS NOT NULL
                       AND (change_revenue IS NULL
                            OR ABS(change_revenue - (post_revenue - pre_revenue)) > 0.01)
                 ) AS comparable_violations,
                 COUNT(*) FILTER (
                     WHERE COALESCE(has_revenue_baseline, false) = false
                        OR COALESCE(revenue_baseline_reason, '''') <> ''''
                 ) AS baseline_flagged_rows,
                 COUNT(*) FILTER (
                     WHERE coverage_post30 IS NULL AND event_date + 30 > CURRENT_DATE
                 ) AS immature_no_post,
                 COUNT(*) FILTER (
                     WHERE coverage_post30 IS NULL AND event_date + 30 <= CURRENT_DATE
                 ) AS mature_no_post,
                 COUNT(*) FILTER (
                     WHERE coverage_post30 IS NULL
                       AND ((event_date + 30 > CURRENT_DATE AND post_revenue IS NOT NULL)
                            OR (event_date + 30 <= CURRENT_DATE AND post_revenue IS DISTINCT FROM 0))
                 ) AS no_post_violations
             FROM public.vw_vendor_sales_nivelacija',
            false, true, '')
    END AS x
),
vendor_assortment AS (
    SELECT
        COALESCE((xpath('/row/row_count/text()', x))[1]::text::bigint, 0) AS row_count,
        COALESCE((xpath('/row/comparable_total/text()', x))[1]::text::bigint, 0) AS comparable_total,
        COALESCE((xpath('/row/comparable_violations/text()', x))[1]::text::bigint, 0) AS comparable_violations,
        COALESCE((xpath('/row/baseline_flagged_rows/text()', x))[1]::text::bigint, 0) AS baseline_flagged_rows,
        COALESCE((xpath('/row/immature_no_post/text()', x))[1]::text::bigint, 0) AS immature_no_post,
        COALESCE((xpath('/row/mature_no_post/text()', x))[1]::text::bigint, 0) AS mature_no_post,
        COALESCE((xpath('/row/no_post_violations/text()', x))[1]::text::bigint, 0) AS no_post_violations
    FROM vendor_assortment_xml
),
nivelacija_store_grain AS (
    SELECT
        COUNT(*) AS event_rows,
        COUNT(*) FILTER (WHERE "IDObjekat" IS NULL) AS missing_store_rows
    FROM "DnevnikPromena"
    WHERE COALESCE("TipPromene", '') ILIKE '%nivelacija%'
),
startup_history_xml AS (
    -- ScriptPath identifiers must match DatabaseInitializer history ids exactly.
    SELECT CASE
        WHEN to_regclass('public."__StartupSqlScriptHistory"') IS NULL THEN NULL
        ELSE query_to_xml(
            'SELECT
                 COUNT(*) FILTER (
                     WHERE "ScriptPath" = ''Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql''
                 ) AS canonical_view_scripts,
                 COUNT(*) FILTER (
                     WHERE "ScriptPath" = ''Database/Migrations/018_AddSupplierDecisionHubViews.sql#full-build''
                 ) AS scorecard_refresh_scripts
             FROM public."__StartupSqlScriptHistory"',
            false, true, '')
    END AS x
),
startup_history AS (
    SELECT
        x IS NOT NULL AS history_table_exists,
        COALESCE((xpath('/row/canonical_view_scripts/text()', x))[1]::text::bigint, 0) AS canonical_view_scripts,
        COALESCE((xpath('/row/scorecard_refresh_scripts/text()', x))[1]::text::bigint, 0) AS scorecard_refresh_scripts
    FROM startup_history_xml
),
mv AS (
    SELECT
        c.relname AS object_name,
        c.relkind,
        COALESCE(m.ispopulated, false) AS is_populated
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    LEFT JOIN pg_matviews m
      ON m.schemaname = n.nspname AND m.matviewname = c.relname
    WHERE n.nspname = 'public'
      AND c.relname IN ('mv_supplier_decision_score_cache', 'mv_supplier_decision_score_cache_v2')
),
vendor_view AS (
    SELECT EXISTS (
        SELECT 1
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'vw_vendor_sales_nivelacija'
          AND c.relkind IN ('v','m')
    ) AS exists_now
),
checks AS (
    SELECT
        'SUP-001'::text AS check_id,
        CASE WHEN COUNT(*) FILTER (WHERE supplier_id IS NULL) = 0 THEN 'PASS' ELSE 'EXPLAINED' END AS verdict,
        (COUNT(*) FILTER (WHERE supplier_id IS NULL))::text AS observed,
        '0 preferred; unknown bucket must remain explicit'::text AS expected,
        'RQ522'::text AS owner,
        'Unknown-supplier retail sale lines in selected period.'::text AS detail
    FROM retail

    UNION ALL
    SELECT
        'SUP-002',
        CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'EXPLAINED' END,
        COUNT(*)::text,
        '0 in certified retail turnover',
        'RQ521',
        'DUG/KOREKCIJA lines are an auditable excluded population, not certified turnover.'
    FROM classified WHERE excluded_receipt

    UNION ALL
    SELECT
        'SUP-003',
        CASE
            WHEN COUNT(*) = 0 THEN 'EXPLAINED'
            WHEN COUNT(*) FILTER (WHERE resolved_cost IS NULL) = 0 THEN 'PASS'
            ELSE 'EXPLAINED'
        END,
        COALESCE(
            ROUND(
                100.0 * COUNT(*) FILTER (WHERE resolved_cost IS NOT NULL) / NULLIF(COUNT(*),0), 2
            )::text || '%',
            'n/a (0 retail lines)'
        ),
        '100% preferred; missing cost must reduce coverage, never become zero cost',
        'RQ521',
        'Cost coverage using line -> NabavnaCenaDin -> legacy fallback.'
    FROM retail

    UNION ALL
    SELECT
        'SUP-004',
        CASE WHEN COUNT(*) FILTER (
            WHERE product_cost_rsd > 0 AND product_cost_legacy > 0
              AND (product_cost_rsd / NULLIF(product_cost_legacy,0)) NOT BETWEEN 50 AND 200
        ) = 0 THEN 'PASS' ELSE 'EXPLAINED' END,
        COUNT(*) FILTER (
            WHERE product_cost_rsd > 0 AND product_cost_legacy > 0
              AND (product_cost_rsd / NULLIF(product_cost_legacy,0)) NOT BETWEEN 50 AND 200
        )::text,
        '0 suspicious dual-cost ratios',
        'RQ521',
        'Unit sanity only; this does not assume a currency conversion rate.'
    FROM retail

    UNION ALL
    SELECT
        'SUP-005',
        CASE
            WHEN COUNT(*) = 0 THEN 'FAIL'
            WHEN BOOL_AND(relkind = 'm' AND is_populated) THEN 'PASS'
            ELSE 'FAIL'
        END,
        COALESCE(
            string_agg(
                object_name || ':' || relkind::text || ':populated=' || is_populated::text,
                ', '
            ),
            'missing'
        ),
        'required score cache exists as populated materialized view',
        'RQ518',
        'Catalog-level MV health/capability proof; never use information_schema for MV identity.'
    FROM mv

    UNION ALL
    SELECT
        'SUP-006',
        CASE WHEN exists_now THEN 'PASS' ELSE 'FAIL' END,
        exists_now::text,
        'true',
        'RQ519',
        'Supplier nivelacija view exists after startup/schema sequence.'
    FROM vendor_view

    UNION ALL
    SELECT
        'SUP-007',
        CASE WHEN COUNT(*) FILTER (WHERE quantity < 0) = 0 THEN 'PASS' ELSE 'EXPLAINED' END,
        COUNT(*) FILTER (WHERE quantity < 0)::text,
        'signed returns allowed and must remain in net retail sales',
        'RQ521',
        'Negative retail lines are evidence, not rows to silently drop.'
    FROM retail

    UNION ALL
    SELECT
        'SUP-008',
        CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'EXPLAINED' END,
        COUNT(*)::text,
        '0 previous-only suppliers in selected current window',
        'RQ522',
        'Suppliers with retail evidence in the equally long previous window but none inside the selected window remain explicit, not silently dropped.'
    FROM previous_only_suppliers

    UNION ALL
    SELECT
        'SUP-009',
        CASE WHEN drift_rows = 0 THEN 'PASS' ELSE 'EXPLAINED' END,
        drift_rows::text,
        '0 sale-time/current-master supplier mismatches',
        'RQ521',
        'Sale-time attribution drift versus current master supplier must stay visible.'
    FROM attribution_drift

    UNION ALL
    SELECT
        'SUP-010',
        CASE
            WHEN NOT history_table_exists THEN 'EXPLAINED'
            WHEN canonical_view_scripts > 0 THEN 'PASS'
            ELSE 'EXPLAINED'
        END,
        canonical_view_scripts::text,
        '>=1 canonical vendor-sales startup history row when history table exists',
        'RQ519',
        'Startup SQL history for canonical nivelacija view ownership.'
    FROM startup_history

    UNION ALL
    SELECT
        'SUP-011',
        CASE
            WHEN event_rows = 0 THEN 'EXPLAINED'
            WHEN missing_store_rows = 0 THEN 'PASS'
            ELSE 'FAIL'
        END,
        missing_store_rows::text || ' missing of ' || event_rows::text,
        '0 missing store ids on nivelacija journal rows',
        'RQ522',
        'Nivelacija events must declare store grain when journal rows exist.'
    FROM nivelacija_store_grain

    UNION ALL
    SELECT
        'SUP-012',
        CASE
            WHEN comparable_total = 0 THEN 'EXPLAINED'
            WHEN comparable_violations = 0 THEN 'PASS'
            ELSE 'FAIL'
        END,
        (comparable_total - comparable_violations)::text || ' of ' || comparable_total::text
            || ' comparable; ' || (row_count - comparable_total)::text || ' not comparable',
        'Change equals Post minus Pre for every row with known pre and post revenue',
        'RQ527',
        'Assortment comparable totals on vw_vendor_sales_nivelacija; rows with unknown pre or post revenue (missing pre window, immature without post sales) are not comparable and are counted separately.'
    FROM vendor_assortment

    UNION ALL
    SELECT
        'SUP-013',
        CASE
            WHEN row_count = 0 THEN 'EXPLAINED'
            WHEN retail_revenue = 0 THEN 'EXPLAINED'
            ELSE 'EXPLAINED'
        END,
        'overview_revenue=' || retail_revenue::text || '; scorecard_rows=' || row_count::text,
        'explained delta required when overview and scorecard bases differ',
        'RQ528',
        'Overview retail turnover and scorecard cache rows use different bases until RQ528 parity contract exists.'
    FROM overview_revenue, scorecard_rows

    UNION ALL
    SELECT
        'SUP-014',
        CASE
            WHEN row_count = 0 THEN 'EXPLAINED'
            WHEN baseline_flagged_rows = 0 THEN 'PASS'
            ELSE 'EXPLAINED'
        END,
        baseline_flagged_rows::text || ' of ' || row_count::text,
        '0 immature/no-post/baseline-flagged assortment rows unless fixture intentionally seeds them',
        'RQ520',
        'Assortment maturity and no-post/zero-baseline states stay explicit.'
    FROM vendor_assortment

    UNION ALL
    SELECT
        'SUP-015',
        CASE
            WHEN NOT history_table_exists THEN 'EXPLAINED'
            WHEN scorecard_refresh_scripts > 0 THEN 'PASS'
            ELSE 'EXPLAINED'
        END,
        scorecard_refresh_scripts::text,
        '>=1 recorded scorecard refresh script when startup history exists',
        'RQ518',
        'Scorecard MV refresh/history evidence from startup SQL history.'
    FROM startup_history

    UNION ALL
    SELECT
        'SUP-016',
        CASE
            WHEN no_post_violations > 0 THEN 'FAIL'
            WHEN immature_no_post + mature_no_post = 0 THEN 'EXPLAINED'
            ELSE 'PASS'
        END,
        'immature_no_post=' || immature_no_post::text
            || '; mature_no_post=' || mature_no_post::text
            || '; violations=' || no_post_violations::text,
        'immature rows without post sales keep post revenue unknown (NULL); mature rows without post sales show explicit 0',
        'RQ520',
        'Post-window maturity on vw_vendor_sales_nivelacija: an open window must never read as a fake zero.'
    FROM vendor_assortment
)
SELECT check_id, verdict, observed, expected, owner, detail
FROM checks
ORDER BY check_id;

-- Additional catalog evidence: exact Supplier MV columns.
SELECT
    c.relname AS object_name,
    a.attname AS column_name,
    format_type(a.atttypid, a.atttypmod) AS data_type
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
JOIN pg_attribute a ON a.attrelid = c.oid
WHERE n.nspname = 'public'
  AND c.relname IN ('mv_supplier_decision_score_cache', 'mv_supplier_decision_score_cache_v2')
  AND c.relkind = 'm'
  AND a.attnum > 0
  AND NOT a.attisdropped
ORDER BY c.relname, a.attnum;

ROLLBACK;
