# PostgreSQL Storage Limit Runbook

This runbook covers the failure pattern where the application database provider rejects writes because the project or database storage limit has been reached. It is an operational incident, separate from API routing or application compilation.

## Observed Failure

On 2026-08-31 the runtime log contained:

- `Npgsql.PostgresException` with SQLSTATE `53100`;
- `could not extend file because project size limit (512 MB) has been exceeded`;
- a secondary failure from `DbErrorStore.SaveAsync` while trying to persist the original diagnostic.

The secondary error is expected when the same full database is used for error persistence. It must not replace the original incident diagnosis.

## Triage Order

1. Capture the correlation ID, UTC time, failing endpoint and the original SQLSTATE. Do not copy connection strings or customer row payloads into tickets or repository files.
2. In the provider console, confirm the affected project, branch/database and current storage limit. Check whether another branch or database in the same project is consuming the quota.
3. If read-only SQL access is available, measure usage before changing data:

```sql
SELECT current_database() AS database_name,
       pg_size_pretty(pg_database_size(current_database())) AS database_size;

SELECT schemaname,
       relname,
       pg_size_pretty(pg_total_relation_size(relid)) AS total_size
FROM pg_catalog.pg_statio_user_tables
ORDER BY pg_total_relation_size(relid) DESC
LIMIT 20;
```

4. Pause or reduce write-heavy imports, refreshes and retries until capacity is restored. Do not repeatedly retry a `53100` write; retries consume time without creating space.
5. Prefer the provider-supported capacity increase or an approved retention/archive operation. Do not delete `ErrorRecords`, documents, snapshots or customer data without backup, retention and owner approval.
6. After capacity is restored, verify `/health`, `/ready`, `/api/runtime/version` and the affected endpoint. Then confirm that a new error can be persisted and that the original failed operation has an explicit retry/recovery state.

## Application Boundary

`DbErrorStore.SaveAsync` is a best-effort diagnostic path and must not turn an existing request failure into a second unhandled 500. A successful API response must never be inferred from the absence of a persisted error record. The routing fix for duplicate `mapping-preview` endpoint names is independent of SQLSTATE `53100` and does not resolve provider capacity.

## Evidence To Close

Record the following in the incident ticket or deployment evidence:

- provider project/database and observed limit, without credentials;
- before/after storage measurement;
- approved cleanup, archive or capacity action;
- health/readiness and affected endpoint results after recovery;
- whether the original operation was replayed safely and whether diagnostic persistence recovered.

If provider access or owner approval is unavailable, mark the incident operationally blocked. Do not mark the API healthy based only on a local build.
