# Supplier windowed SQL 029 maintenance (repository-local guide)

Updated: 2026-10-13  
Owner: analytics reliability (RQ635)  
Production execution authority: STAB16 / provider owner only

This guide describes a **controlled, separately authorized** maintenance run for `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql` against the **analytics** PostgreSQL database. It does not certify that production objects are populated and does not authorize running maintenance from the live free-tier web service during traffic.

## When to use

- `mv_supplier_decision_score_cache_90d` and/or `_180d` are missing, stale, unpopulated or missing required columns.
- Web startup intentionally keeps `DatabaseInitialization:BuildSupplierDecisionWindowedViewsOnStartup=false` so SQL 029 is **not** executed on Render cold start.
- Supplier scorecard 90d/180d must remain fail-closed (`MISSING_OBJECT` / unavailable) until maintenance succeeds.

## Preflight (read-only)

1. Connect with a read-only or maintenance role to the **analytics** database. Prefer a **direct** PostgreSQL session for DDL; poolers can interfere with long `CREATE MATERIALIZED VIEW` work.
2. Run `tools/supplier-windowed-mv-preflight.sql` (SELECT-only).
3. Interpret the single verdict row:
   - `STOP` — fix prerequisites or wait for locks; do **not** start SQL 029.
   - `CONTINUE` — prerequisites satisfied; windowed MVs need repair; proceed with the maintenance window plan below.
   - `READY` — both windowed MVs exist, are populated and expose the required column contract; no SQL 029 maintenance required.

Do not log connection strings, credentials or full environment dumps in tickets.

## Authorized maintenance sequence

1. **Snapshot / backup** the analytics database (provider-native snapshot or agreed backup policy).
2. **Stop competing writers** where possible: do not run concurrent supplier hub repairs, nivelacija contract repairs, or a second copy of SQL 029.
3. **Re-run preflight** immediately before DDL; abort on `STOP`.
4. Execute SQL 029 through the supported pipeline (same file path the application uses: `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql`), in a **worker or maintenance process**, not the production web pooler path.
5. **Post-check (read-only)**:
   ```sql
   SELECT c.relname, c.relkind, c.relispopulated
   FROM pg_class c
   JOIN pg_namespace n ON n.oid = c.relnamespace
   WHERE n.nspname = 'public'
     AND c.relname IN (
       'mv_supplier_decision_score_cache_90d',
       'mv_supplier_decision_score_cache_180d'
     );
   ```
6. Re-run `tools/supplier-windowed-mv-preflight.sql` and require `maintenance_verdict = READY`.
7. Schedule or verify **REFRESH** ownership via `NightlyAnalyticsRefreshWorker` / refresh options for both windowed caches alongside the all-time supplier caches.

## Cancellation and rollback boundaries

- SQL 029 is designed for idempotent re-application on partial installs; it is **not** a blanket `DROP CASCADE` maintenance script.
- If a batch fails mid-run, treat the database as **partial** until SQL 029 completes successfully on a follow-up attempt after reviewing PostgreSQL error output.
- Rolling back a failed transaction does not apply partial DDL from that transaction; re-run preflight before retrying.
- Do not delete `__StartupSqlScriptHistory` rows manually unless following an approved repair playbook; startup code may force re-execution when history and capability state diverge.

## Pooler vs direct connection

- Long-running populated `CREATE MATERIALIZED VIEW` work should use a direct database connection with an explicit timeout policy appropriate to the maintenance window.
- Application poolers (for example Neon pooler endpoints) are appropriate for short API traffic, not for this DDL.

## Production status

Production Render/Neon recovery remains **unproven** until STAB16 records deployed SHA, provider logs and post-maintenance capability checks. Repository-local tests (RQ635) only prove the SQL script and preflight behavior on disposable PostgreSQL fixtures.
