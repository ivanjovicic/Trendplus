# Tenant Safety Checklist

_Status: pilot / single-tenant. No multi-tenant implementation exists today._  
_Purpose: document future SaaS risks so isolation is designed in — not bolted on._  
_Last reviewed: 2026-06-02_

---

## Current pilot recommendation

| Recommendation | Rationale |
|---|---|
| **One deploy per customer** | Simplest isolation. No code changes required. App config, connection strings and secrets are entirely separate per deployment. |
| **Separate database per customer** | Analytics DB, main Trendplus DB and cache (Redis/memory) are already connection-string driven. Giving each customer their own Postgres instance is the lowest-risk path. |
| **Separate storage per customer** | Import files (`.mdb`), exports and report snapshots should be stored in a customer-specific directory or bucket prefix, not a shared path. |

Until multi-tenant is a deliberate product decision, do not share any infrastructure (DB, cache, storage) between customers.

---

## Future tenant-sensitive areas

The areas below have **no `TenantId` column or cache-key segment today**. If the architecture ever moves to a shared deployment, each area is a data-leak or cross-contamination vector.

### 1. Analytics cache keys

File: `Infrastructure/Services/Caching/IAnalyticsCacheService.cs` — `AnalyticsCacheKeys`

Current key format:
```
analytics:analytics-report:supplier-decision:<hash>
analytics:analytics-report:pilot-intake:<hash>
analytics:observability:logs:...
```

**Risk:** Cache entries from Customer A can be served to Customer B if the same filter combination is requested.  
**Future fix:** Prepend `tenant:{tenantId}:` as the first key segment before any other namespace.

---

### 2. Report snapshots

Report results (supplier decision report, pilot intake report) are cached in `analytics:analytics-report:*` and can be materialised to DB.

**Risk:** A shared report cache or snapshot table with no tenant column exposes one customer's business data to another.  
**Future fix:** `TenantId` column on the snapshot table; cache key must include tenant segment; cache eviction by tenant prefix must be possible.

---

### 3. Action queue — `AnalyticsActionItem.SourceKey`

File: `Domain/Model/Analytics/AnalyticsActionItem.cs`

Current `SourceKey` format examples:
```
inventory:sku:123
dopuna:{artikalId}:{storeId}
supplier:42
```

**Risk:** No tenant dimension. Upsert duplicate-detection (unique on `SourceType + SourceKey`) would collide across tenants for identical SKU/supplier IDs.  
**Future fix:** Add `TenantId` to the unique index and all queries. `SourceKey` format should not need to embed tenant — the column handles it.

---

### 4. Refresh history — `AnalyticsRefreshRun`

File: `Domain/Model/Analytics/AnalyticsRefreshRun.cs`

Columns: `JobKey`, `JobName`, `Status`, `TriggeredBy`, `WorkerName` — no tenant dimension.

**Risk:** Admin API or UI showing refresh history would expose Customer A's job history to Customer B's admin user.  
**Future fix:** `TenantId` column on `AnalyticsRefreshRun`; all queries filtered by tenant; worker trigger must record which tenant triggered the run.

---

### 5. Import files — `AccessImportLog` / `DataImportBatch`

File: `Domain/Model/AccessImportLog.cs`

Logs row-level import activity. `BatchId → DataImportBatch`. No `TenantId`.

**Risk:** Import logs and source `.mdb` file paths visible across tenants. A shared import worker could pick up another tenant's file.  
**Future fix:** `TenantId` on `DataImportBatch` and `AccessImportLog`. Import file paths stored under a tenant-scoped prefix (`/imports/{tenantId}/`). Worker queue job must carry `TenantId`.

---

### 6. Logs and error records — `PerformanceLog`

File: `Domain/Model/PerformanceLog.cs`

Columns: `RequestType`, `RequestName`, `RequestData`, `ResponseData`, `ExceptionMessage` — no tenant dimension.

**Risk:** `ResponseData` and `ExceptionMessage` may contain customer-identifiable data (product names, supplier names, amounts). A shared log viewer would expose this.  
**Future fix:** `TenantId` column; all log queries scoped; consider redacting or hashing PII fields in `RequestData`/`ResponseData` before persisting.

---

### 7. Exports

Excel/PDF exports are currently generated on-the-fly and streamed to the browser. No persistent export record exists today.

**Risk:** If a shared file cache or export queue is ever introduced, export files must be scoped to tenant and user. A presigned URL must not be guessable or reusable by another tenant's user.  
**Future fix:** Any export record table needs `TenantId` + `UserId`. Exported file storage path must include tenant segment. Presigned URL expiry must be short.

---

### 8. Background jobs / worker scheduler

File: `Workers/TrendIngestionWorker.cs`, `Domain/Model/WorkerRuntimeSettings.cs`

Current `WorkerRuntimeSettings` is keyed by `WorkerName`. No tenant dimension.

**Risk:** A shared worker process that reads `WorkerRuntimeSettings` cannot distinguish which customer's dataset to process. A misconfigured job could run against the wrong database or write results to the wrong tenant's tables.  
**Future fix:** `TenantId` on `WorkerRuntimeSettings`; job dispatch must carry `TenantId`; worker must verify tenant context before touching any data. Never share a worker process across tenants without explicit job-level isolation.

---

## Future rule (to apply before any shared-deployment work)

> **Every query, cache key, report, job, log record and file path must be `TenantId`-scoped.**

Checklist before enabling shared deployment:

- [ ] `TenantId` column exists on all tables listed above
- [ ] No query returns rows without a `WHERE TenantId = @tenantId` clause (or EF query filter)
- [ ] Cache key format starts with `tenant:{tenantId}:`
- [ ] Cache eviction by tenant prefix is tested
- [ ] `SourceKey` uniqueness index includes `TenantId`
- [ ] Worker jobs carry and validate `TenantId` before processing
- [ ] Import file storage is under a tenant-scoped path
- [ ] Export files are scoped and not accessible across tenants
- [ ] Log/performance records include `TenantId` and sensitive fields are reviewed for PII
- [ ] Admin APIs that show history/logs/runs are filtered by tenant of the requesting user
- [ ] No `TenantId` is accepted from the client request body — it must come from the authenticated identity claim only
