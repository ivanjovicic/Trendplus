# Render startup 503 / supplier decision windowed MV recovery — 2026-10-12

## Evidence and boundary

Render web logs at 16:50 UTC show both `mv_supplier_decision_score_cache_90d` and `_180d` as `MISSING_OBJECT`. The supplier-decision startup repair deletes recorded SQL 029 history and executes `029_AddSupplierDecisionWindowedViews.sql` again. Multiple `/ready` and `/api/*` calls responded 503 while the SQL was running. There is **no final SQL 029 failure/success in the supplied extract**, so it is not proven that SQL 029 is the sole cause of 503; database connectivity, deferred database initialization and readiness must also be checked.

SQL 029 constructs two populated materialized views and may perform costly scans/locking. Supplier scorecard 90d/180d is an optional capability: a missing cache must stay unavailable rather than return stale all-time metrics.

## Mitigation in code

Production `Api/appsettings.Production.json` sets `DatabaseInitialization:BuildSupplierDecisionWindowedViewsOnStartup=false`. Both startup paths — deferred full initializer and the supplier decision schema-repair hosted service — honor the same switch. They log the capability state without executing SQL 029. Other necessary schema/readiness validation is unchanged; **do not disable `StartupReadiness:GateApiTraffic`, `DatabaseInitialization:FailFast`, or required provenance schema checks** to make a deployment green.

The setting defaults to `true` if not configured, keeping local/test/bootstrap behavior unchanged. On a dedicated maintenance or worker process only, an explicit environment override `DatabaseInitialization__BuildSupplierDecisionWindowedViewsOnStartup=true` permits repair. Do not enable this in the live free web instance during traffic.

## Operator validation

1. Deploy a reviewed build and confirm effective `ASPNETCORE_ENVIRONMENT=Production` and no overriding `DatabaseInitialization__BuildSupplierDecisionWindowedViewsOnStartup=true` environment variable. Avoid concurrent DB initialization/repair attempts.
2. Inspect `/health` and `/ready` separately. Check readiness JSON `reason`, `failureCategory` and database probe errors if 503 persists; supplier MV availability is not itself sufficient to explain a global 503.
3. Confirm the logs mention automatic supplier windowed build disabled and contain **no** `Executing startup SQL file Database/Migrations/029_AddSupplierDecisionWindowedViews.sql` from web startup.
4. Confirm other API functionality and the supplier scorecard fail-closed behavior; record the 90d/180d feature as unavailable, not repaired, while objects remain missing.
5. In a separately controlled DB maintenance window, use the supported SQL 029 migration pipeline against the **analytics** database, preferably a dedicated maintenance process and direct PostgreSQL connection rather than the web pooler. Back up/schema-snapshot first, ensure SQL dependencies are present, monitor locks, duration and database utilization; do not run concurrent copies.
6. Read-only post-maintenance check:
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
   Require both materialized views populated and verify required columns through the existing capability inspector. Only then consider the supplier 90d/180d feature ready.

## Exit criteria

- Core web API returns 200 from `/ready` after required DB checks.
- No mandatory background rebuild of SQL 029 occurs on web cold starts.
- The supplier feature does not silently substitute all-time values.
- A later controlled maintenance run creates and validates both MVs, and their refresh ownership is explicit.

This mitigates a credible startup load source; it does not certify production recovery or SQL 029 success from the log excerpt alone.


## Reconciliation on 2026-10-13 (repo proof vs deployment proof)

- PR #133 is **merged** into \`main\` at \`1c9158d3e9a74de96c0b7ece60fb0c88f9a18058\`; targeted schema-source regression and exact-merge backend run \`38301829902\` succeeded (1,998 passed, 42 classified skips). Repository tip at this review was \`6dec862e6449a18a24a26234215bcc9b30705b98\` (evidence-only).
- The setting \`DatabaseInitialization:BuildSupplierDecisionWindowedViewsOnStartup=false\` exists in committed Production configuration. It is **not proven** that Render deployed the merge or used that effective value; environment overrides and startup logs were not available.
- The original log fragment did not show the end of SQL 029 execution or the JSON body of \`/ready\`. Simultaneous 503 and SQL repair indicate contention risk, not a proven singular causal mechanism.
- A read-only attempt to fetch \`/health\` and \`/ready\` from the assistant environment on 2026-10-13 was **not accessible**. Do not record that as HTTP 503/200 or infer service health from this result.
- The October 14 01:00–02:00 UTC scheduled Render dashboard/API maintenance is distinct; the provider notice says deployed services should not be interrupted by it.
- Current repo-only next steps are RQ634 (cold-start regression), RQ635 (SQL 029 maintenance preflight), and RQ636 (test-host worker isolation). **STAB16** exclusively owns provider/deployed evidence and production maintenance authority; required original business data / Access batch #23 is not to be re-imported as part of this incident.

### Confirmed vs not confirmed

| Statement | Classification |
|---|---|
| Missing 90d/180d objects at Oct 12 16:50 UTC | Confirmed in user-provided Render logs |
| SQL 029 was force-executed at 16:50:34 UTC | Confirmed start, outcome not shown |
| /ready and multiple APIs returned 503 in the same interval | Confirmed |
| SQL 029 alone caused all 503s | Unproven |
| PR #133 fixes the intended startup policy in code | Confirmed on merged SHA and CI |
| PR #133 is already running on Render | Unproven |
| Production 90d/180d materialized views are now populated | Unproven |
| Dedicated worker has produced durable refresh evidence | Unproven |

No weakening of readiness or production schema checks is authorized to turn a 503 into an apparent 200.
