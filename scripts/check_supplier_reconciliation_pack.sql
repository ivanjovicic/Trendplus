-- RQ524: read-only Supplier analytics reconciliation pack.
-- Purpose: turn Supplier hypotheses into explicit PASS / FAIL / EXPLAINED evidence.
-- Safety: SELECT-only. RQ524 is repository-local/fixture-only; any production/replica execution is owned by RQ454/STAB16.
-- psql usage:
--   \set from_utc '2026-01-01T00:00:00Z'
--   \set to_utc   '2026-03-31T23:59:59Z'
--   \set store_id 'NULL'
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
        CASE WHEN COUNT(*) FILTER (WHERE resolved_cost IS NULL) = 0 THEN 'PASS' ELSE 'EXPLAINED' END,
        ROUND(
            100.0 * COUNT(*) FILTER (WHERE resolved_cost IS NOT NULL) / NULLIF(COUNT(*),0), 2
        )::text || '%',
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
