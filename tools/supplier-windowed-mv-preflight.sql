-- RQ635 read-only preflight for supplier SQL 029 windowed materialized views.
-- SELECT-only. Run against the analytics PostgreSQL database before a controlled
-- maintenance window. Does not execute DDL, refresh caches or mutate data.
-- Do not paste connection strings or credentials into tickets.

WITH required_prerequisites AS (
    SELECT
        to_regclass('public.vw_supplier_fullprice_signals') IS NOT NULL AS has_fullprice_signals,
        to_regclass('public.vw_supplier_markdown_dependency') IS NOT NULL AS has_markdown_dependency,
        to_regclass('public.vw_supplier_decision_score') IS NOT NULL AS has_decision_score,
        to_regclass('public.mv_supplier_decision_score_cache') IS NOT NULL AS has_all_time_score_cache
),
required_columns AS (
    SELECT unnest(ARRAY[
        'supplier_id',
        'supplier_name',
        'period_from',
        'period_to',
        'revenue',
        'units',
        'fullprice_revenue_share',
        'fullprice_sellthrough',
        'pre_markdown_margin_pct',
        'repeat_winner_rate',
        'markdown_dependency_score',
        'stock_risk_score',
        'return_rate',
        'category_focus_score',
        'supplier_quality_index',
        'recommendation_code',
        'confidence_score',
        'post_signal_coverage'
    ]::text[]) AS column_name
),
mv_90 AS (
    SELECT
        c.relname,
        c.relkind,
        COALESCE(mv.ispopulated, FALSE) AS is_populated,
        (
            SELECT COUNT(*)::integer
            FROM required_columns rc
            LEFT JOIN pg_attribute a
              ON a.attrelid = c.oid
             AND NOT a.attisdropped
             AND a.attnum > 0
             AND a.attname = rc.column_name
            WHERE a.attname IS NULL
        ) AS missing_required_columns
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    LEFT JOIN pg_matviews mv
      ON mv.schemaname = n.nspname
     AND mv.matviewname = c.relname
    WHERE n.nspname = 'public'
      AND c.relname = 'mv_supplier_decision_score_cache_90d'
),
mv_180 AS (
    SELECT
        c.relname,
        c.relkind,
        COALESCE(mv.ispopulated, FALSE) AS is_populated,
        (
            SELECT COUNT(*)::integer
            FROM required_columns rc
            LEFT JOIN pg_attribute a
              ON a.attrelid = c.oid
             AND NOT a.attisdropped
             AND a.attnum > 0
             AND a.attname = rc.column_name
            WHERE a.attname IS NULL
        ) AS missing_required_columns
    FROM pg_class c
    JOIN pg_namespace n ON n.oid = c.relnamespace
    LEFT JOIN pg_matviews mv
      ON mv.schemaname = n.nspname
     AND mv.matviewname = c.relname
    WHERE n.nspname = 'public'
      AND c.relname = 'mv_supplier_decision_score_cache_180d'
),
startup_history AS (
    SELECT
        history."ScriptPath",
        history."ScriptHash",
        history."AppliedAtUtc",
        history."DurationMs"
    FROM "__StartupSqlScriptHistory" AS history
    WHERE history."ScriptPath" = 'Database/Migrations/029_AddSupplierDecisionWindowedViews.sql'
    ORDER BY history."AppliedAtUtc" DESC
    LIMIT 1
),
blocking_locks AS (
    SELECT COUNT(*)::integer AS lock_count
    FROM pg_locks l
    JOIN pg_class c ON c.oid = l.relation
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'public'
      AND c.relname IN (
          'mv_supplier_decision_score_cache_90d',
          'mv_supplier_decision_score_cache_180d',
          'mv_supplier_decision_score_cache',
          'mv_supplier_markdown_dependency_cache'
      )
      AND NOT l.granted
),
windowed_dependent_views AS (
    SELECT
        COUNT(*) FILTER (WHERE to_regclass('public.' || name) IS NOT NULL)::integer AS present_count,
        COUNT(*)::integer AS expected_count
    FROM unnest(ARRAY[
        'vw_supplier_fullprice_signals_90d',
        'vw_supplier_fullprice_signals_180d',
        'vw_supplier_markdown_dependency_90d',
        'vw_supplier_markdown_dependency_180d'
    ]::text[]) AS name
),
classified AS (
    SELECT
        rp.has_fullprice_signals,
        rp.has_markdown_dependency,
        rp.has_decision_score,
        rp.has_all_time_score_cache,
        COALESCE(m90.relname IS NOT NULL, FALSE) AS mv_90_exists,
        COALESCE(m90.is_populated, FALSE) AS mv_90_populated,
        COALESCE(m90.missing_required_columns, 18) AS mv_90_missing_columns,
        COALESCE(m180.relname IS NOT NULL, FALSE) AS mv_180_exists,
        COALESCE(m180.is_populated, FALSE) AS mv_180_populated,
        COALESCE(m180.missing_required_columns, 18) AS mv_180_missing_columns,
        wd.present_count AS windowed_view_count,
        wd.expected_count AS windowed_view_expected,
        COALESCE(bl.lock_count, 0) AS blocking_lock_count,
        sh."ScriptHash" AS startup_sql_029_hash,
        sh."AppliedAtUtc" AS startup_sql_029_applied_at_utc,
        sh."DurationMs" AS startup_sql_029_duration_ms,
        (
            m90.relname IS NOT NULL
            AND m90.is_populated
            AND m90.missing_required_columns = 0
            AND m180.relname IS NOT NULL
            AND m180.is_populated
            AND m180.missing_required_columns = 0
        ) AS windowed_ready
    FROM required_prerequisites rp
    LEFT JOIN mv_90 m90 ON TRUE
    LEFT JOIN mv_180 m180 ON TRUE
    LEFT JOIN blocking_locks bl ON TRUE
    LEFT JOIN startup_history sh ON TRUE
    LEFT JOIN windowed_dependent_views wd ON TRUE
)
SELECT
    CASE
        WHEN NOT (has_fullprice_signals AND has_markdown_dependency AND has_decision_score) THEN 'STOP'
        WHEN blocking_lock_count > 0 THEN 'STOP'
        WHEN windowed_ready THEN 'READY'
        ELSE 'CONTINUE'
    END AS maintenance_verdict,
    CASE
        WHEN NOT has_fullprice_signals THEN 'missing_prerequisite_vw_supplier_fullprice_signals'
        WHEN NOT has_markdown_dependency THEN 'missing_prerequisite_vw_supplier_markdown_dependency'
        WHEN NOT has_decision_score THEN 'missing_prerequisite_vw_supplier_decision_score'
        WHEN blocking_lock_count > 0 THEN 'blocking_lock_on_supplier_cache'
        WHEN windowed_ready THEN 'windowed_materialized_views_ready'
        WHEN NOT mv_90_exists OR NOT mv_180_exists THEN 'missing_windowed_materialized_view'
        WHEN mv_90_missing_columns > 0 OR mv_180_missing_columns > 0 THEN 'windowed_materialized_view_missing_columns'
        WHEN NOT mv_90_populated OR NOT mv_180_populated THEN 'windowed_materialized_view_not_populated'
        ELSE 'safe_to_run_sql_029_maintenance'
    END AS verdict_reason_code,
    has_fullprice_signals,
    has_markdown_dependency,
    has_decision_score,
    has_all_time_score_cache,
    mv_90_exists,
    mv_90_populated,
    mv_90_missing_columns,
    mv_180_exists,
    mv_180_populated,
    mv_180_missing_columns,
    windowed_view_count,
    windowed_view_expected,
    blocking_lock_count,
    startup_sql_029_hash,
    startup_sql_029_applied_at_utc,
    startup_sql_029_duration_ms
FROM classified;
