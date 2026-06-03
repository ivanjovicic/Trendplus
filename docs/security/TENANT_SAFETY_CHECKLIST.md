# Tenant Safety Checklist

_Status: pilot / single-tenant. No multi-tenant implementation exists today._  
_Purpose: document future SaaS risks so isolation is designed in, not bolted on._
_Last reviewed: 2026-06-02_

---

## Current pilot recommendation

| Recommendation | Rationale |
|---|---|
| **One deploy per customer** | Simplest isolation. No code changes required. App config, connection strings, and secrets are entirely separate per deployment. |
| **Separate database per customer** | Analytics DB, main Trendplus DB, and cache (Redis or memory) are already connection-string driven. Giving each customer their own Postgres instance is the lowest-risk path. |
| **Separate storage per customer** | Import files (`.mdb`), exports, and report snapshots should be stored in a customer-specific directory or bucket prefix, not a shared path. |

Until multi-tenant is a deliberate product decision, do not share any infrastructure (DB, cache, or storage) between customers.

---

## Tenant-sensitive areas

The areas below have no `TenantId` column or cache-key segment today. If the architecture ever moves to a shared deployment, each area is a cross-contamination or data-leak vector.

### 1. Analytics cache keys

File: `Infrastructure/Services/Caching/IAnalyticsCacheService.cs` - `AnalyticsCacheKeys`

Current key format:

```text
analytics:analytics-report:supplier-decision:<hash>
analytics:analytics-report:pilot-intake:<hash>
analytics:observability:logs:...
```

Risk:
- Cache entries from Customer A can be served to Customer B if the same filter combination is requested.

Future action:
- Prepend `tenant:{tenantId}:` as the first key segment before any other namespace.

### 2. Report snapshots

Report results such as supplier decision reports and pilot intake reports are cached in `analytics:analytics-report:*` and may also be materialised to the database.

Risk:
- A shared report cache or snapshot table with no tenant column exposes one customer's business data to another.

Future action:
- Add `TenantId` to the snapshot table.
- Include the tenant segment in cache keys.
- Make cache eviction by tenant prefix possible.

### 3. Action queue source key

File: `Domain/Model/Analytics/AnalyticsActionItem.cs`

Current `SourceKey` examples:

```text
inventory:sku:123
dopuna:{artikalId}:{storeId}
supplier:42
```

Risk:
- No tenant dimension means duplicate detection on `SourceType + SourceKey` can collide across tenants for identical SKU or supplier IDs.

Future action:
- Add `TenantId` to the unique index and all queries.
- Keep tenant identity in a dedicated column rather than encoding it into `SourceKey`.

### 4. Refresh history

File: `Domain/Model/Analytics/AnalyticsRefreshRun.cs`

Current columns: `JobKey`, `JobName`, `Status`, `TriggeredBy`, `WorkerName`.

Risk:
- An admin view that shows refresh history could expose Customer A's job history to Customer B's admin user.

Future action:
- Add `TenantId` to `AnalyticsRefreshRun`.
- Filter all queries by tenant.
- Record which tenant triggered the run.

### 5. Import files

File: `Domain/Model/AccessImportLog.cs`

Import logs track row-level import activity and point to `DataImportBatch`.

Risk:
- Import logs and source `.mdb` file paths could become visible across tenants.
- A shared import worker could pick up another tenant's file.

Future action:
- Add `TenantId` to `DataImportBatch` and `AccessImportLog`.
- Store import file paths under a tenant-scoped prefix such as `/imports/{tenantId}/`.
- Make sure worker queue jobs carry `TenantId`.

### 6. Logs and error records

File: `Domain/Model/PerformanceLog.cs`

Current columns: `RequestType`, `RequestName`, `RequestData`, `ResponseData`, `ExceptionMessage`.

Risk:
- `ResponseData` and `ExceptionMessage` may contain customer-identifiable data such as product names, supplier names, or amounts.
- A shared log viewer would expose this across tenants.

Future action:
- Add `TenantId`.
- Scope all log queries.
- Redact or hash sensitive fields before persisting if needed.

### 7. Exports

Excel and PDF exports are currently generated on the fly and streamed to the browser. No persistent export record exists today.

Risk:
- If a shared file cache or export queue is introduced later, export files must be scoped to tenant and user.
- A presigned URL must not be guessable or reusable by another tenant's user.

Future action:
- Add `TenantId` and `UserId` to any export record table.
- Include the tenant segment in export storage paths.
- Keep presigned URL expiry short.

### 8. Background jobs and worker scheduler

Files: `Workers/TrendIngestionWorker.cs`, `Domain/Model/WorkerRuntimeSettings.cs`

Current `WorkerRuntimeSettings` is keyed by `WorkerName`. No tenant dimension exists.

Risk:
- A shared worker process cannot distinguish which customer's dataset to process.
- A misconfigured job could run against the wrong database or write results to the wrong tenant's tables.

Future action:
- Add `TenantId` to `WorkerRuntimeSettings`.
- Carry `TenantId` in job dispatch.
- Verify tenant context before touching any data.
- Never share a worker process across tenants without explicit job-level isolation.

---

## Future rule

Before any shared-deployment work, every query, cache key, report, job, log record, and file path must be `TenantId`-scoped.

---

## Tenant safety matrix

| Area | Tenant-safe today? | Risk | Future action |
|---|---|---|---|
| Analytics cache keys | No for shared SaaS; yes only because pilot is isolated per deploy | Cache reuse can leak one customer's analytics to another | Prefix every key with `tenant:{tenantId}:` and test tenant-prefix eviction |
| Report snapshots | No | Shared snapshots or cached report outputs can expose customer data across tenants | Add `TenantId` to snapshot storage and isolate export/cache paths |
| Action queue sourceKey | No | Duplicate detection can collide across tenants for the same SKU or supplier IDs | Add `TenantId` to uniqueness rules and all queries |
| Refresh history | No | Admin views could reveal another tenant's job history | Add `TenantId` to refresh-run records and scope all queries |
| Import files | No | Another tenant's import file or log path could be picked up or exposed | Store files under tenant-scoped paths and add `TenantId` to import records |
| Logs/error records | No | Shared logs may expose customer-specific names, amounts, or exception details | Add `TenantId` and redact sensitive payload fields |
| Exports | No | Export files or links could be guessed, reused, or mixed across tenants | Scope exports by tenant and user, with short-lived URLs |
| Background jobs | No | A shared worker can process the wrong tenant's data or write into the wrong dataset | Carry `TenantId` through scheduling, dispatch, and processing |

---

## Checklist before enabling shared deployment

- [ ] `TenantId` column exists on all tables listed above
- [ ] No query returns rows without a `WHERE TenantId = @tenantId` clause or EF query filter
- [ ] Cache key format starts with `tenant:{tenantId}:`
- [ ] Cache eviction by tenant prefix is tested
- [ ] `SourceKey` uniqueness includes `TenantId`
- [ ] Worker jobs carry and validate `TenantId` before processing
- [ ] Import file storage is under a tenant-scoped path
- [ ] Export files are scoped and not accessible across tenants
- [ ] Log and performance records include `TenantId` and sensitive fields are reviewed for PII
- [ ] Admin APIs that show history, logs, or runs are filtered by tenant of the requesting user
- [ ] No `TenantId` is accepted from the client request body; it must come from the authenticated identity claim only
