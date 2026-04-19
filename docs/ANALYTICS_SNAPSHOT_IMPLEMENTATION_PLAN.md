# Analytics Cost Snapshot Layer — Implementation Plan

> **Status:** Draft  
> **Target:** Stabilize historical margin analytics for Access-imported sales  
> **Scope:** Phase 1 — Supplier and Shoe-Type analytics only  
> **Constraint:** No mutation of `prodaja_stavke`; no PDV correction; no net margin

---

## 1. Final implementation recommendation

### What to build

A **line-level analytics cost snapshot** — a separate table (`analytics_sale_line_cost_snapshots`) that freezes the resolved cost for every Access-origin sale line item at a point in time, with full provenance tracking. A batch metadata table (`analytics_cost_snapshot_batches`) manages lifecycle, audit, and activation.

The supplier and shoe-type analytics read path gains a new top-priority cost source: **snapshot cost** — inserted between historical sale-line cost (tier 1) and product fallback (tier 2). When a snapshot is active and a row exists, the analytics endpoint uses the snapshotted cost. When no snapshot row exists, it falls back to the current 3-tier resolution.

### What NOT to build

- **No batch backfill into `prodaja_stavke.nabavna_cena`** — operational truth stays untouched.
- **No aggregate-level snapshot tables** — line-level granularity is necessary because aggregation boundaries (supplier, shoe-type, date ranges) change per report. Pre-aggregated snapshots would bloat the surface and still fail to cover ad-hoc filters.
- **No PDV correction** — orthogonal concern, not in scope.
- **No net margin** — system computes gross margin contribution only.
- **No color/insight-studio/runtime-scoring support** — phase 2.
- **No automatic scheduled snapshot generation** — phase 1 is admin-triggered only.

### Why this is the right minimal phase-1 solution

1. **Line-level is unavoidable.** Analytics endpoints group by supplier, shoe-type, date range, and arbitrary filters. Only line-level snapshots survive re-aggregation across all dimensions.
2. **Separate table preserves operational integrity.** `prodaja_stavke` remains the source of truth for POS and sales workflows. The snapshot table is analytics-only, clearly labeled as an estimate, and deletable without data loss.
3. **Feature-flagged read path** means zero risk to production until intentionally enabled.
4. **Batch model enables audit.** Every snapshot has a creation timestamp, source description, and activation state. Stale snapshots can be deactivated; new ones generated; old ones retained for audit.
5. **The existing `MarginAccumulator` + `MarginQualityClassifier` infrastructure** already tracks cost source and coverage — the snapshot layer plugs into this cleanly.

---

## 2. Proposed schema changes

### Table 1: `analytics_cost_snapshot_batches`

**Purpose:** Batch metadata, lifecycle management, audit trail. One row per snapshot generation run. Only one batch can be `active` at a time per scope.

| Column | Type | Nullable | Default | Notes |
|--------|------|----------|---------|-------|
| `id` | `BIGSERIAL` | NOT NULL | auto | PK |
| `scope` | `VARCHAR(50)` | NOT NULL | `'access_origin'` | What data this batch covers. Phase 1: always `'access_origin'`. Future: `'all'`, `'pos_origin'`, etc. |
| `status` | `VARCHAR(20)` | NOT NULL | `'draft'` | `draft` → `generating` → `ready` → `active` / `failed`. Also `deactivated`, `superseded`. |
| `created_at_utc` | `TIMESTAMPTZ` | NOT NULL | `now()` | When batch record was created |
| `generated_at_utc` | `TIMESTAMPTZ` | NULL | — | When generation completed |
| `activated_at_utc` | `TIMESTAMPTZ` | NULL | — | When batch was promoted to active |
| `deactivated_at_utc` | `TIMESTAMPTZ` | NULL | — | When batch was deactivated |
| `created_by` | `VARCHAR(100)` | NOT NULL | `'system'` | Who triggered generation |
| `description` | `TEXT` | NULL | — | Human-readable note |
| `row_count` | `INTEGER` | NOT NULL | `0` | Number of snapshot rows generated |
| `total_revenue_covered` | `NUMERIC(18,2)` | NOT NULL | `0` | Sum of revenue for snapshotted rows |
| `coverage_pct` | `DOUBLE PRECISION` | NOT NULL | `0` | % of Access-origin revenue with a snapshot cost |
| `no_cost_pct` | `DOUBLE PRECISION` | NOT NULL | `0` | % of Access-origin revenue where even fallback found no cost |
| `generation_duration_ms` | `INTEGER` | NULL | — | How long generation took |
| `dry_run` | `BOOLEAN` | NOT NULL | `false` | If true, rows are preview-only and batch cannot be activated |
| `error_message` | `TEXT` | NULL | — | If status=failed |
| `metadata_json` | `JSONB` | NULL | — | Extensible: source file info, config used, etc. |

**PK:** `id`  
**Indexes:**
- `UNIQUE` partial index on `(scope)` `WHERE status = 'active'` — enforces at most one active batch per scope at DB level
- `ix_snapshot_batches_status` on `(status, scope)`

**Phase 1:** Required.

**Why a separate batch table is required:** Lifecycle management (draft → generating → ready → active → deactivated) cannot be modeled as columns on the line-level table. Activation/deactivation is a batch-level operation. Audit needs batch-level timestamps and provenance. The unique partial index on `(scope) WHERE status = 'active'` enforces the single-active-batch invariant at the database level, preventing accidental duplicate activation.

---

### Table 2: `analytics_sale_line_cost_snapshots`

**Purpose:** One row per sale line item that received a resolved cost during snapshot generation. This is the line-level cost freeze.

| Column | Type | Nullable | Default | Notes |
|--------|------|----------|---------|-------|
| `id` | `BIGSERIAL` | NOT NULL | auto | PK |
| `batch_id` | `BIGINT` | NOT NULL | — | FK → `analytics_cost_snapshot_batches.id` |
| `prodaja_stavka_id` | `INTEGER` | NOT NULL | — | FK → `ProdajaStavke.Id` (EF-managed table) |
| `resolved_unit_cost` | `NUMERIC(18,4)` | NOT NULL | — | The frozen cost per unit |
| `cost_source` | `SMALLINT` | NOT NULL | — | Enum: 1=Historical, 2=ProductFallbackRsd, 3=ProductFallbackLegacy |
| `product_cost_rsd_at_snapshot` | `NUMERIC(18,4)` | NULL | — | Value of `Artikli.NabavnaCenaDin` at snapshot time |
| `product_cost_legacy_at_snapshot` | `NUMERIC(18,4)` | NULL | — | Value of `Artikli.NabavnaCena` at snapshot time |
| `artikal_id` | `INTEGER` | NOT NULL | — | Denormalized for fast joins and audit |

**PK:** `id`  
**FK:** `batch_id` → `analytics_cost_snapshot_batches(id)` ON DELETE CASCADE  
**Indexes:**
- `UNIQUE` on `(batch_id, prodaja_stavka_id)` — one cost per line per batch
- `ix_snapshot_lines_stavka` on `(prodaja_stavka_id)` — fast lookup during read path
- `ix_snapshot_lines_batch_source` on `(batch_id, cost_source)` — analytics on snapshot composition

**Phase 1:** Required.

**Why line-level granularity is necessary:** Analytics endpoints apply arbitrary filters (date range, supplier, shoe type, season, size, color) and group results differently per report. An aggregate snapshot (e.g., per-supplier-per-month) would need to be regenerated for every filter combination. Line-level snapshots survive any re-aggregation because the cost is attached to the atomic unit of analysis — the sale line item.

---

### Tables NOT built in phase 1

- **Aggregate helper tables** (e.g., `analytics_supplier_margin_snapshots`): Defer. Line-level snapshots + existing LINQ grouping is sufficient for phase 1 query volume. If latency becomes a problem, pre-aggregated materialized views can be layered on top of the line-level data later.
- **Snapshot diff/audit tables**: Defer. The `metadata_json` column on the batch table can store summary diffs. Per-line diff tracking is over-engineering for phase 1.

---

## 3. Proposed feature flags

### Flag 1: `Analytics:UseSnapshotCost`

| Property | Value |
|----------|-------|
| **Name** | `Analytics:UseSnapshotCost` |
| **Default** | `false` |
| **Environment rollout** | `false` in production initially; `true` in development for testing |
| **Code path** | When `true`, the supplier/shoe-type analytics endpoints check for an active snapshot batch and use snapshot costs in cost resolution. When `false`, the existing 3-tier fallback runs unchanged. |
| **Temporary or permanent** | **Permanent.** This flag should remain as a kill-switch even after full rollout. |
| **Kill-switch behavior** | Setting to `false` instantly reverts all analytics endpoints to legacy 3-tier fallback. No batch deactivation needed. Snapshot data remains in tables untouched for later re-enablement. |

**Configuration location:** `appsettings.json` → `Analytics` section.

```json
{
  "Analytics": {
    "UseSnapshotCost": false
  }
}
```

**Options class:** `AnalyticsSnapshotOptions` in `Infrastructure/Configuration/`.

### Flag 2: `Analytics:SnapshotAdminEnabled`

| Property | Value |
|----------|-------|
| **Name** | `Analytics:SnapshotAdminEnabled` |
| **Default** | `false` |
| **Environment rollout** | `true` in development; `false` in production until ready |
| **Code path** | When `true`, the admin endpoints for batch creation, dry-run, activation, and deactivation are registered. When `false`, the endpoints return 404. |
| **Temporary or permanent** | **Temporary.** Remove once snapshot is proven stable and admin endpoints are always available. |
| **Kill-switch behavior** | Setting to `false` hides admin endpoints. Does not affect read path or existing batches. |

---

## 4. Read-path implementation rules

### Current behavior (no flag / flag = false)

For each sale line in the analytics query:

```
1. cost = ProdajaStavka.NabavnaCena                    → source = Historical
2. if null: cost = Artikli.NabavnaCenaDin               → source = ProductFallbackRsd
3. if null: cost = Artikli.NabavnaCena                  → source = ProductFallbackLegacy
4. if null: cost = NONE                                 → excluded from margin
5. Feed (revenue, quantity, cost, source) into MarginAccumulator
```

No change. This remains the default.

### New behavior (flag = true)

For each sale line in the analytics query:

```
1. cost = ProdajaStavka.NabavnaCena                    → source = Historical
   (If non-null, use it. POS data has real historical cost. Skip snapshot.)

2. if null AND active snapshot exists:
     Look up analytics_sale_line_cost_snapshots WHERE batch_id = activeBatchId
                                                  AND prodaja_stavka_id = line.Id
     if found: cost = snapshot.resolved_unit_cost  → source = SnapshotFallback (new enum value)

3. if null (no snapshot row):
     cost = Artikli.NabavnaCenaDin                 → source = ProductFallbackRsd

4. if null:
     cost = Artikli.NabavnaCena                    → source = ProductFallbackLegacy

5. if null: cost = NONE                            → excluded from margin

6. Feed (revenue, quantity, cost, source) into MarginAccumulator
```

### Decision rules by data scenario

| Scenario | Cost used | Source tag | Notes |
|----------|-----------|------------|-------|
| POS sale with `NabavnaCena` on line | Sale-line cost | `Historical` | Best quality. Snapshot never consulted. |
| Access sale, snapshot row exists | Snapshot cost | `SnapshotFallback` | Frozen estimate. Stable over time. |
| Access sale, no snapshot row, product has `NabavnaCenaDin` | Product RSD cost | `ProductFallbackRsd` | Live fallback. May drift. |
| Access sale, no snapshot row, product has `NabavnaCena` only | Product legacy cost | `ProductFallbackLegacy` | Live fallback. May drift. |
| No cost anywhere | None | `None` | Excluded from margin calculation. |

### Implementation approach

The active batch ID should be resolved **once per request**, not per row. On endpoint entry:

```csharp
long? activeBatchId = null;
if (snapshotOptions.UseSnapshotCost)
{
    activeBatchId = await db.AnalyticsCostSnapshotBatches
        .Where(b => b.Status == "active" && b.Scope == "access_origin")
        .Select(b => (long?)b.Id)
        .FirstOrDefaultAsync();
}
```

Then, if `activeBatchId` has a value, the sale-line query joins to `analytics_sale_line_cost_snapshots` via a left join on `(batch_id, prodaja_stavka_id)`. The existing `ResolveUnitCost` call is extended with an optional `snapshotCost` parameter that takes precedence over product-level fallback when the sale-line's own `NabavnaCena` is null.

### MarginCostSource enum extension

Add:
```csharp
SnapshotFallback = 4  // From analytics_sale_line_cost_snapshots
```

### MarginAccumulator tracking

`SnapshotFallback` revenue is tracked alongside `EstimatedCostRevenue` (it IS an estimate, just a frozen one). A new `SnapshotCostRevenue` accumulator field distinguishes it from live fallback for quality reporting.

---

## 5. Backend implementation tasks

### Task B1: EF Core migration — snapshot schema

| | |
|---|---|
| **Purpose** | Create the two new tables in PostgreSQL |
| **Files** | New: `Infrastructure/Migrations/YYYYMMDDHHMMSS_AddAnalyticsCostSnapshotTables.cs` |
| **Changes** | Standard EF Core migration using `MigrationBuilder.CreateTable`. Creates both tables, indexes, FK, and the unique partial index for single-active-batch. |
| **Dependencies** | None |
| **Risk** | Low — additive schema change, no existing table modifications |

### Task B2: Domain models for snapshot entities

| | |
|---|---|
| **Purpose** | C# entity classes for the two new tables |
| **Files** | New: `Domain/Model/Analytics/AnalyticsCostSnapshotBatch.cs`, `Domain/Model/Analytics/AnalyticsSaleLineCostSnapshot.cs` |
| **Changes** | POCO entities with `[Key]`, `[MaxLength]`, navigation properties. Follow `DataImportBatch` pattern. |
| **Dependencies** | None |
| **Risk** | Low |

### Task B3: DbContext registration

| | |
|---|---|
| **Purpose** | Register new entities in `TrendplusDbContext` |
| **Files** | Modify: `Infrastructure/DbContexts/TrendplusDbContext.cs` |
| **Changes** | Add `DbSet<AnalyticsCostSnapshotBatch>` and `DbSet<AnalyticsSaleLineCostSnapshot>`. Add `OnModelCreating` configuration for table names (`analytics_cost_snapshot_batches`, `analytics_sale_line_cost_snapshots`), column mappings, indexes, FK, and the unique partial index. |
| **Dependencies** | B2 |
| **Risk** | Low |

### Task B4: Feature flag options class + registration

| | |
|---|---|
| **Purpose** | Strongly-typed options for snapshot feature flags |
| **Files** | New: `Infrastructure/Configuration/AnalyticsSnapshotOptions.cs`. Modify: `Api/Program.cs` (or wherever services are registered), `Api/appsettings.json`, `Api/appsettings.Development.json` |
| **Changes** | Options class with `UseSnapshotCost` (bool, default false) and `SnapshotAdminEnabled` (bool, default false). Register via `services.Configure<AnalyticsSnapshotOptions>(config.GetSection("Analytics"))`. Add `Analytics` section to appsettings. |
| **Dependencies** | None |
| **Risk** | Low |

### Task B5: Extend `MarginCostSource` enum + `MarginAccumulator`

| | |
|---|---|
| **Purpose** | Track snapshot-sourced cost separately in margin calculation |
| **Files** | Modify: `Application/Analytics/AnalyticsMarginPolicy.cs` |
| **Changes** | (1) Add `SnapshotFallback = 4` to `MarginCostSource`. (2) Add `_snapshotCostRevenue` field to `MarginAccumulator`. (3) Track it in `Add()` when source is `SnapshotFallback`. (4) Add `SnapshotCostRevenue` and `SnapshotCostCoveragePct` to `MarginSnapshot`. (5) Add `ResolveUnitCostWithSnapshot()` method: if saleLineCost is non-null → Historical; else if snapshotCost is non-null → SnapshotFallback; else fall through to product fallback. (6) Update `MarginQualityClassifier` to treat `SnapshotFallback` as mid-quality (better than live fallback but not historical). |
| **Dependencies** | None |
| **Risk** | Medium — core policy change. Must not break existing paths when snapshot is null. |

### Task B6: Batch generation service

| | |
|---|---|
| **Purpose** | Service that generates snapshot rows for a batch |
| **Files** | New: `Application/Analytics/AnalyticsCostSnapshotService.cs` |
| **Changes** | Methods: `CreateBatchAsync()`, `GenerateBatchAsync(batchId, dryRun)`, `ActivateBatchAsync(batchId)`, `DeactivateBatchAsync(batchId)`, `GetBatchSummaryAsync(batchId)`. Generation logic: query all `ProdajaStavke` joined to `ProdajaZaglavlje` where `DataOrigin = 'access'` and `NabavnaCena IS NULL`, resolve cost via `AnalyticsMarginPolicy.ResolveProductUnitCost()`, bulk-insert into snapshot table. |
| **Dependencies** | B1, B2, B3 |
| **Risk** | Medium — must handle large row counts efficiently (batch insert, no N+1) |

### Task B7: Admin endpoints for snapshot management

| | |
|---|---|
| **Purpose** | HTTP endpoints for creating, generating, activating, deactivating batches |
| **Files** | New: `Api/Endpoints/AnalyticsSnapshotEndpoints.cs` |
| **Changes** | Endpoints: `POST /api/analytics/snapshots/batches` (create), `POST /api/analytics/snapshots/batches/{id}/generate` (generate, with `?dryRun=true`), `POST /api/analytics/snapshots/batches/{id}/activate`, `POST /api/analytics/snapshots/batches/{id}/deactivate`, `GET /api/analytics/snapshots/batches` (list), `GET /api/analytics/snapshots/batches/{id}` (detail with stats). Gated by `SnapshotAdminEnabled` flag. |
| **Dependencies** | B4, B6 |
| **Risk** | Low — isolated endpoints behind flag |

### Task B8: Snapshot-aware supplier analytics endpoint

| | |
|---|---|
| **Purpose** | Modify supplier analytics read path to use snapshot cost when available |
| **Files** | Modify: `Api/Endpoints/AllEndpoints.cs` (~L1215 supplier endpoint) |
| **Changes** | (1) Resolve `activeBatchId` at start of endpoint (one query). (2) If `activeBatchId` has value, left-join `analytics_sale_line_cost_snapshots` on `(batch_id, prodaja_stavka_id)` into the sales query projection. (3) Pass snapshot cost into `MarginAccumulator.Add()` via the new `ResolveUnitCostWithSnapshot()`. (4) Add `snapshotCostRevenue`, `snapshotCostCoveragePct`, `isSnapshotActive` to response DTO. (5) When snapshot is active, update `marginQualityTier` calculation to reflect snapshot stability. |
| **Dependencies** | B5, B6 (active batch must exist to test) |
| **Risk** | Medium — hot path modification. Must be zero-impact when flag is off. |

### Task B9: Snapshot-aware shoe-type analytics endpoint

| | |
|---|---|
| **Purpose** | Same as B8 but for shoe-type analytics |
| **Files** | Modify: `Api/Endpoints/AllEndpoints.cs` (~L1830 shoe-type endpoint) |
| **Changes** | Identical pattern to B8. |
| **Dependencies** | B8 (share the pattern) |
| **Risk** | Medium — same as B8 |

### Task B10: Reconciliation / validation helpers

| | |
|---|---|
| **Purpose** | Admin endpoint or service method to compare snapshot-aware vs legacy output |
| **Files** | Add method to `AnalyticsCostSnapshotService.cs`. Optionally expose via admin endpoint. |
| **Changes** | Method that runs both paths (snapshot-aware and legacy) for a given date range/scope and returns a comparison: margin delta, coverage delta, per-supplier deltas. Output as JSON for review. |
| **Dependencies** | B8, B9 |
| **Risk** | Low — read-only comparison |

---

## 6. Frontend implementation tasks

### Task F1: API interface updates

| | |
|---|---|
| **Purpose** | Add snapshot fields to TypeScript interfaces |
| **Files** | Modify: `Klijent/clientapp/src/services/supplierSalesStatsApi.ts`, `Klijent/clientapp/src/services/shoeTypeSalesStatsApi.ts` |
| **New fields** | `snapshotCostRevenue?: number`, `snapshotCostCoveragePct?: number`, `isSnapshotActive?: boolean` on both stat and totals interfaces |
| **Dependencies** | B8, B9 API contract |
| **Risk** | Low — additive, optional fields |

### Task F2: Margin quality utility update

| | |
|---|---|
| **Purpose** | Update `marginQuality.ts` to handle snapshot tier |
| **Files** | Modify: `Klijent/clientapp/src/utils/marginQuality.ts` |
| **Changes** | (1) New `qualityTierClass()` case for snapshot-aware tiers. (2) New tooltip builder that explains "cost was frozen from a snapshot taken on [date]". (3) `buildMarginDetailNote()` updated to mention snapshot when `isSnapshotActive && snapshotCostCoveragePct > 0`. |
| **Dependencies** | F1 |
| **Risk** | Low |

### Task F3: Supplier page — snapshot awareness

| | |
|---|---|
| **Purpose** | Display snapshot status in supplier analytics |
| **Files** | Modify: `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx` |
| **Changes** | (1) KPI: when snapshot is active, show a small "📌 Zamrznuta cena" badge near the margin quality badge. (2) Table: if `snapshotCostCoveragePct > 0`, show snapshot icon in margin quality column. (3) Detail view: in "Kvalitet podataka" section, add "Snapshot pokrivenost" field showing what % of this supplier's revenue was resolved via snapshot. (4) Recommendation caveat: update to mention snapshot stability when applicable. |
| **Dependencies** | F1, F2 |
| **Risk** | Low |

### Task F4: Shoe-type page — snapshot awareness

| | |
|---|---|
| **Purpose** | Same as F3 for shoe-type page |
| **Files** | Modify: `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx` |
| **Changes** | Identical pattern to F3. |
| **Dependencies** | F1, F2 |
| **Risk** | Low |

### Task F5: Print/export — snapshot source note

| | |
|---|---|
| **Purpose** | Print pages must disclose snapshot usage |
| **Files** | Modify: `Klijent/clientapp/src/pages/AnalyticsPrintPage.tsx` (if applicable) |
| **Changes** | When snapshot is active, add a footer note: "Nabavne cene zamrznute snapshot-om od [datum]. Pokrivenost: X%." |
| **Dependencies** | F3 or F4 |
| **Risk** | Low |

### Task F6: Snapshot admin page (optional phase 1)

| | |
|---|---|
| **Purpose** | Simple admin UI for batch management |
| **Files** | New: `Klijent/clientapp/src/pages/SnapshotAdminPage.tsx` |
| **Changes** | Table of batches with status, row count, coverage. Buttons: Create, Generate (dry-run), Activate, Deactivate. Shows reconciliation diff summary. |
| **Dependencies** | B7 |
| **Risk** | Low — optional. Admin can use API directly via curl/Postman in phase 1. |

---

## 7. Batch generation implementation plan

### Creating a batch

```
POST /api/analytics/snapshots/batches
Body: { "description": "Initial cost freeze - June 2025", "scope": "access_origin" }
Response: { "batchId": 1, "status": "draft" }
```

Creates a metadata row with `status = 'draft'`. No snapshot rows generated yet.

### Running dry-run mode

```
POST /api/analytics/snapshots/batches/1/generate?dryRun=true
```

1. Sets batch `status = 'generating'`.
2. Queries all `ProdajaStavke ps` joined to `ProdajaZaglavlje pz` where `pz.DataOrigin = 'access'` and `ps.NabavnaCena IS NULL`.
3. For each line, resolves cost via `AnalyticsMarginPolicy.ResolveProductUnitCost(artikal.NabavnaCenaDin, artikal.NabavnaCena)`.
4. Inserts rows into `analytics_sale_line_cost_snapshots` with `batch_id = 1`.
5. Sets batch `dry_run = true`, `status = 'ready'`.
6. Updates batch stats: `row_count`, `total_revenue_covered`, `coverage_pct`, `no_cost_pct`.
7. Returns summary.

Dry-run batches **cannot** be activated. They exist for validation only.

### Validating a batch

```
GET /api/analytics/snapshots/batches/1
```

Returns batch metadata + summary stats. Admin reviews:
- `row_count` — does it match expected Access line count?
- `coverage_pct` — what % of Access revenue now has a frozen cost?
- `no_cost_pct` — what % still has no cost even with fallback?
- Cost source breakdown (how many Historical vs FallbackRsd vs FallbackLegacy rows)

For deeper validation, run the reconciliation endpoint:
```
GET /api/analytics/snapshots/batches/1/reconciliation?supplierId=5
```
Returns per-supplier margin delta between snapshot mode and legacy mode.

### Running real generation (non-dry-run)

```
POST /api/analytics/snapshots/batches
Body: { "description": "Production freeze v1" }

POST /api/analytics/snapshots/batches/2/generate
```

Same as dry-run but `dry_run = false`. Batch moves to `status = 'ready'`.

### Activating a batch

```
POST /api/analytics/snapshots/batches/2/activate
```

1. Check: batch `status` must be `'ready'` and `dry_run = false`.
2. Within a transaction:
   a. Set any currently-active batch for this scope to `status = 'superseded'`, `deactivated_at_utc = now()`.
   b. Set this batch to `status = 'active'`, `activated_at_utc = now()`.
3. The unique partial index `WHERE status = 'active'` on `(scope)` prevents race conditions.

### Deactivating a batch

```
POST /api/analytics/snapshots/batches/2/deactivate
```

1. Sets batch `status = 'deactivated'`, `deactivated_at_utc = now()`.
2. No active batch exists → read path falls back to legacy 3-tier resolution.
3. Snapshot rows are NOT deleted — retained for audit and potential re-activation.

### Preventing duplicate active batches

- **Database level:** Unique partial index `CREATE UNIQUE INDEX ... ON analytics_cost_snapshot_batches (scope) WHERE status = 'active'` — PostgreSQL enforces at most one active row per scope.
- **Application level:** Activation logic supersedes the current active batch in a transaction before setting the new one.
- **Scope:** Phase 1 scope is always `'access_origin'`. One active batch per scope. Multiple scopes are independent.

### What scope means

`scope` identifies what subset of data the batch covers:
- `access_origin` — sale lines where `ProdajaZaglavlje.DataOrigin = 'access'` and `ProdajaStavka.NabavnaCena IS NULL` (the Access import gap)
- Future scopes could include `all` (all sales regardless of origin) or custom subsets

### Batch performance considerations

Expected row count: Access-origin sale lines without historical cost. Based on the forensic analysis (0% historical, 98.25% fallback), this is effectively all Access-imported sale lines. For a medium retail dataset, this could be 50K–500K rows. Generation should use:
- Streaming query (no full materialization)
- Bulk insert via `COPY` or `EF Core SaveChanges` in 5000-row batches
- Single transaction for atomicity
- Progress tracking on batch metadata

---

## 8. PR breakdown

### PR 1: Schema + Models + Flags scaffolding

| | |
|---|---|
| **Purpose** | Lay foundation — tables exist, models exist, flags exist, nothing is wired |
| **Tasks** | B1, B2, B3, B4 |
| **Excluded** | All read-path changes, batch generation, admin endpoints, frontend |
| **Migration impact** | Two new tables created. No existing table changes. |
| **Flag impact** | Flags added to config, both default `false`. |
| **Rollout safety** | Zero runtime impact. Tables are empty. Flags are off. |
| **Validation** | `dotnet build` passes. Migration runs. Tables appear in DB. `SELECT * FROM analytics_cost_snapshot_batches` returns empty. |

### PR 2: MarginAccumulator extension + batch generation

| | |
|---|---|
| **Purpose** | Enable snapshot generation and dry-run |
| **Tasks** | B5, B6, B7 |
| **Excluded** | Read-path changes, frontend |
| **Migration impact** | None — only code changes |
| **Flag impact** | Admin endpoints gated by `SnapshotAdminEnabled` |
| **Rollout safety** | Admin endpoints hidden by default. `MarginCostSource.SnapshotFallback` enum value exists but is never used in read path yet. Existing `Add()` behavior unchanged for current callers. |
| **Validation** | Set `SnapshotAdminEnabled: true` in dev. Create batch via API. Generate dry-run. Verify row count and coverage stats match expected. Generate real batch. Verify `status = 'ready'`. |

### PR 3: Snapshot-aware read path (backend)

| | |
|---|---|
| **Purpose** | Wire snapshot cost into supplier and shoe-type analytics endpoints |
| **Tasks** | B8, B9, B10 |
| **Excluded** | Frontend changes |
| **Migration impact** | None |
| **Flag impact** | `UseSnapshotCost` controls behavior. When `false`, endpoints behave exactly as before. |
| **Rollout safety** | Flag is `false` by default. Can be toggled per environment. If enabled and no batch is active, behavior is identical to legacy (no snapshot rows to find). |
| **Validation** | (1) Flag off → supplier/shoe-type output identical to current. (2) Flag on, no active batch → output identical. (3) Flag on, active batch → output uses snapshot costs. (4) Run reconciliation to verify deltas are expected. (5) Deactivate batch → output reverts to legacy. |

### PR 4: Frontend snapshot awareness

| | |
|---|---|
| **Purpose** | UI discloses snapshot status, updates quality badges, print notes |
| **Tasks** | F1, F2, F3, F4, F5 |
| **Excluded** | Admin page (F6) |
| **Migration impact** | None |
| **Flag impact** | Frontend is reactive — if API returns `isSnapshotActive: false` or field is absent, no snapshot UI appears. |
| **Rollout safety** | Purely cosmetic until backend flag is on and batch is active. |
| **Validation** | (1) Backend flag off → no snapshot badges visible. (2) Backend flag on + active batch → snapshot badges appear. (3) Print shows snapshot note. (4) Detail view shows snapshot coverage. |

### PR 5: Pilot activation + monitoring

| | |
|---|---|
| **Purpose** | Activate snapshot in staging/production, add monitoring |
| **Tasks** | Monitoring tasks (section 11), production batch generation, flag enablement |
| **Excluded** | Admin UI (F6 — optional follow-up) |
| **Migration impact** | None |
| **Flag impact** | `UseSnapshotCost: true` in target environment |
| **Rollout safety** | Reversible by setting flag to `false`. Batch can be deactivated independently. |
| **Validation** | Monitor metrics for 48h. Compare reports to legacy output. Check endpoint latency. Verify no regression in POS-origin data. |

---

## 9. Rollout plan

### Phase R1: Dark launch (PR 1–2 merged)

| | |
|---|---|
| **What is enabled** | Tables exist. Models exist. Admin endpoints exist (behind `SnapshotAdminEnabled: true` in dev only). |
| **What is still off** | `UseSnapshotCost: false`. No read-path changes deployed. |
| **Who validates** | Developer |
| **Metrics to watch** | Migration success. Batch creation/generation works in dev. |
| **Success criteria** | Can create and generate a dry-run batch in development. Row count matches expected Access line count. |
| **Rollback** | Drop tables via down migration. Remove config. |

### Phase R2: Dry-run in staging (PR 2 deployed to staging)

| | |
|---|---|
| **What is enabled** | `SnapshotAdminEnabled: true` in staging. Generate a dry-run batch against staging data. |
| **What is still off** | `UseSnapshotCost: false`. Endpoints return legacy data. |
| **Who validates** | Developer + product owner |
| **Metrics to watch** | Row count, coverage %, no-cost %, generation duration. |
| **Success criteria** | Coverage ≥ 95% of Access-origin revenue. No-cost ≤ 3%. Generation completes in < 5 minutes. |
| **Rollback** | Delete dry-run batch rows. No impact on anything. |

### Phase R3: Real batch + read path in staging (PR 3 deployed)

| | |
|---|---|
| **What is enabled** | `UseSnapshotCost: true` in staging. Generate and activate a real batch. |
| **What is still off** | Production unchanged. |
| **Who validates** | Developer + product owner reviewing supplier and shoe-type reports |
| **Metrics to watch** | Margin contribution deltas (snapshot vs legacy). Endpoint latency. MarginQuality tier distribution changes. |
| **Success criteria** | Supplier report margins are stable across page refreshes. Margin deltas vs legacy are < 5% for most suppliers (unless Artikli costs genuinely changed since last Access import). Endpoint latency increase < 50ms. |
| **Rollback** | Set `UseSnapshotCost: false`. Deactivate batch. |

### Phase R4: Frontend + partial production (PR 4 deployed, production dry-run)

| | |
|---|---|
| **What is enabled** | Frontend snapshot UI deployed. Production: generate dry-run batch, review stats. |
| **What is still off** | Production `UseSnapshotCost: false`. |
| **Who validates** | Product owner reviews dry-run stats. Developer reviews reconciliation output. |
| **Metrics to watch** | Dry-run coverage, no-cost %, generation duration in production. |
| **Success criteria** | Production dry-run stats align with staging. Product owner approves coverage metrics. |
| **Rollback** | N/A — dry-run only. |

### Phase R5: Production go-live (PR 5)

| | |
|---|---|
| **What is enabled** | Generate real batch in production. Activate. Set `UseSnapshotCost: true`. |
| **What is still off** | Nothing — full feature active. |
| **Who validates** | Product owner monitors reports for 48h. Developer monitors latency and error logs. |
| **Metrics to watch** | Endpoint latency, margin deltas from legacy, snapshot lookup miss rate, error rate, user feedback. |
| **Success criteria** | Reports are stable. No latency regression > 100ms. Product owner confirms margins "make sense." |
| **Rollback criteria** | Endpoint errors spike. Margins are wildly wrong. Latency > 500ms. Product owner rejects data. |

### Phase R6: Broader adoption or stop

| | |
|---|---|
| **What is enabled** | Evaluate extending to color analytics, InsightStudio, RuntimeScoring. |
| **Decision point** | If phase 1 is stable for 2 weeks, plan phase 2 PRs. If not, fix or roll back. |

---

## 10. Rollback plan

### Step 1: Instantly disable snapshot reads

```json
// appsettings.json or environment variable
{ "Analytics": { "UseSnapshotCost": false } }
```

If runtime config reload is supported, this takes effect on next request. Otherwise, app restart required.

**Effect:** All analytics endpoints immediately revert to the legacy 3-tier fallback path. No code deployment needed.

### Step 2: Deactivate the active batch

```
POST /api/analytics/snapshots/batches/{id}/deactivate
```

Or directly in the database:
```sql
UPDATE analytics_cost_snapshot_batches
SET status = 'deactivated', deactivated_at_utc = NOW()
WHERE status = 'active' AND scope = 'access_origin';
```

**Effect:** Even if the flag is later re-enabled, no active batch exists, so snapshot lookup returns nothing and legacy fallback runs.

### Step 3: Verify rollback

1. Call supplier analytics endpoint. Verify response does not contain `isSnapshotActive: true`.
2. Call shoe-type analytics endpoint. Same check.
3. Compare output to pre-snapshot baseline (if saved from reconciliation).
4. Check logs for `SnapshotFallback` cost source — should be zero occurrences.

### Step 4: Preserve audit data

Do **NOT** drop tables or delete rows unless explicitly decided. The snapshot rows and batch metadata are harmless when inactive and serve as audit trail.

### Full nuclear rollback (if needed)

```sql
-- Only if you want to completely remove the feature
DROP TABLE IF EXISTS analytics_sale_line_cost_snapshots;
DROP TABLE IF EXISTS analytics_cost_snapshot_batches;
```

Then remove the `Analytics` config section and the `SnapshotFallback` enum value (code-level revert via git).

---

## 11. Monitoring and observability tasks

### Task M1: Structured logging in batch generation

| | |
|---|---|
| **What** | Log batch lifecycle events: created, generation started/completed/failed, activated, deactivated |
| **Where** | `AnalyticsCostSnapshotService.cs` |
| **Format** | Serilog structured logs with `BatchId`, `Scope`, `RowCount`, `CoveragePct`, `DurationMs` properties |
| **Example** | `Log.Information("Snapshot batch {BatchId} generated: {RowCount} rows, {CoveragePct:F1}% coverage in {DurationMs}ms", ...)` |

### Task M2: Read-path cost source counters

| | |
|---|---|
| **What** | Per-request counter of how many rows used each cost source (Historical, SnapshotFallback, ProductFallbackRsd, ProductFallbackLegacy, None) |
| **Where** | Supplier and shoe-type endpoints, included in response metadata |
| **Format** | `costSourceBreakdown: { historical: 0, snapshot: 4521, fallbackRsd: 89, fallbackLegacy: 12, none: 34 }` |

### Task M3: Endpoint latency monitoring

| | |
|---|---|
| **What** | Track latency impact of snapshot join |
| **Where** | Existing `PerformanceLogging` infrastructure |
| **Metric** | p50/p95/p99 latency for supplier and shoe-type endpoints, segmented by `UseSnapshotCost` flag value |

### Task M4: Snapshot batch health summary

| | |
|---|---|
| **What** | Admin endpoint returning: active batch ID, age, row count, coverage. Alertable if active batch is > 30 days old. |
| **Where** | `AnalyticsSnapshotEndpoints.cs` — `GET /api/analytics/snapshots/health` |
| **Alert** | Log warning if no active batch and flag is on. Log warning if active batch is stale (> configurable age). |

### Task M5: Reconciliation report

| | |
|---|---|
| **What** | On-demand comparison of snapshot-aware vs legacy margin output |
| **Where** | `AnalyticsCostSnapshotService.cs` + admin endpoint |
| **Output** | JSON with per-supplier and per-shoe-type margin deltas, total delta, and coverage change |
| **Metrics** | `snapshot_rows_generated`, `snapshot_coverage_pct`, `remaining_live_fallback_pct`, `no_cost_pct`, `margin_delta_vs_legacy` |

### Key metrics summary

| Metric | Source | Alert threshold |
|--------|--------|-----------------|
| Snapshot rows generated | Batch metadata | < expected count |
| Snapshot coverage % of Access revenue | Batch metadata | < 90% |
| Remaining live fallback % after snapshot | Endpoint response | > 10% (when snapshot active) |
| No-cost % | Endpoint response | > 5% |
| Margin delta (snapshot vs legacy) | Reconciliation | > 15% per supplier |
| Endpoint latency impact | PerformanceLogs | p95 increase > 100ms |

---

## 12. Testing tasks

### Test T1: Migration round-trip

| | |
|---|---|
| **Proves** | Tables can be created and dropped cleanly |
| **Fixtures** | Empty database |
| **Edge cases** | Run migration up, then down, then up again. Verify idempotency. |

### Test T2: Schema integrity — unique partial index

| | |
|---|---|
| **Proves** | Only one active batch per scope is possible |
| **Fixtures** | Two batch records for scope `access_origin` |
| **Test** | Activate batch 1. Try to activate batch 2 without deactivating batch 1 → must fail with unique constraint violation (if bypassing application logic). Application logic must handle this by superseding. |

### Test T3: Batch generation — happy path

| | |
|---|---|
| **Proves** | Snapshot rows are generated with correct costs |
| **Fixtures** | 5 Artikli records with known `NabavnaCenaDin`/`NabavnaCena`. 10 ProdajaStavke with `NabavnaCena = null`, `DataOrigin = 'access'`. 2 ProdajaStavke with `NabavnaCena` set (POS origin). |
| **Test** | Generate batch. Verify: 10 snapshot rows (not 12 — POS lines excluded because they have historical cost, but actually scope is all Access lines without sale-line cost). Verify each row's `resolved_unit_cost` matches expected fallback. Verify `cost_source` is correctly tagged. |
| **Edge cases** | Artikal with both `NabavnaCenaDin` and `NabavnaCena` → prefer RSD. Artikal with only `NabavnaCena` → use legacy. Artikal with neither → no snapshot row (or row with source=None? Decision: skip row entirely — no snapshot if no cost exists). |

### Test T4: Batch generation — no-cost articles

| | |
|---|---|
| **Proves** | Lines with no cost on the product are correctly counted but not snapshotted |
| **Fixtures** | Artikal with `NabavnaCenaDin = null`, `NabavnaCena = null`. ProdajaStavka referencing it. |
| **Test** | Generate batch. Verify line is NOT in snapshot table. Verify batch `no_cost_pct` is correctly calculated. |

### Test T5: Provenance — snapshot records correct source values

| | |
|---|---|
| **Proves** | `product_cost_rsd_at_snapshot` and `product_cost_legacy_at_snapshot` capture current Artikli values |
| **Fixtures** | Artikal with `NabavnaCenaDin = 500`, `NabavnaCena = 5`. |
| **Test** | Generate batch. Verify snapshot row has `product_cost_rsd_at_snapshot = 500`, `product_cost_legacy_at_snapshot = 5`. Change Artikal to `NabavnaCenaDin = 600`. Generate new batch. New row has `600`. Old batch row still has `500`. |

### Test T6: Read precedence — flag off

| | |
|---|---|
| **Proves** | When flag is off, snapshot is never consulted |
| **Fixtures** | Active batch with snapshot rows. `UseSnapshotCost = false`. |
| **Test** | Call supplier endpoint. Verify response uses live fallback costs. `isSnapshotActive` is false or absent. |

### Test T7: Read precedence — flag on, no active batch

| | |
|---|---|
| **Proves** | When flag is on but no batch is active, behavior is identical to legacy |
| **Fixtures** | No active batch. `UseSnapshotCost = true`. |
| **Test** | Call supplier endpoint. Verify output matches legacy exactly. |

### Test T8: Read precedence — flag on, active batch

| | |
|---|---|
| **Proves** | When flag is on and batch is active, snapshot cost is used for Access lines |
| **Fixtures** | Active batch. Sale lines with snapshot rows. `UseSnapshotCost = true`. |
| **Test** | Call supplier endpoint. Verify margin uses snapshot cost, not current Artikli cost. Change Artikli cost → margin stays same (snapshot is frozen). |

### Test T9: Read precedence — POS data unaffected

| | |
|---|---|
| **Proves** | POS-origin lines still use their own `NabavnaCena`, not snapshot |
| **Fixtures** | POS sale line with `NabavnaCena = 300`. Snapshot batch active. |
| **Test** | Verify POS line uses `300`, not snapshot value. |

### Test T10: Rollback — flag toggle

| | |
|---|---|
| **Proves** | Toggling flag immediately reverts behavior |
| **Fixtures** | Active batch. Flag on. |
| **Test** | Call endpoint → snapshot mode. Set flag off. Call endpoint → legacy mode. Set flag on → snapshot mode again. |

### Test T11: Rollback — batch deactivation

| | |
|---|---|
| **Proves** | Deactivating batch reverts to legacy even with flag on |
| **Fixtures** | Active batch. Flag on. |
| **Test** | Call endpoint → snapshot mode. Deactivate batch. Call endpoint → legacy mode (flag is on but no active batch). |

### Test T12: Supplier and shoe-type regression

| | |
|---|---|
| **Proves** | Existing endpoint behavior is preserved when feature is off |
| **Fixtures** | Known dataset with pre-computed expected margins. |
| **Test** | Flag off. Call supplier endpoint. Verify all margin fields match expected. Same for shoe-type. |

### Test T13: Dry-run batch cannot be activated

| | |
|---|---|
| **Proves** | Guard logic works |
| **Fixtures** | Dry-run batch in `ready` state. |
| **Test** | Call activate → must return 400 or appropriate error. |

---

## 13. Risks and mitigations

### R1: Semantic misunderstanding — users think snapshot = truth

| | |
|---|---|
| **Risk** | Users see stable margins and assume they are historically accurate sale-time costs. |
| **Mitigation** | UI must always label snapshot costs as "Zamrznuta procena" (frozen estimate), never "Istorijska cena". Quality tier for snapshot must be distinct from "confirmed." Tooltip must include snapshot date. |

### R2: False certainty — coverage looks high but costs are stale

| | |
|---|---|
| **Risk** | A snapshot generated 6 months ago has 98% coverage, but product costs have changed significantly since then. |
| **Mitigation** | Track batch age. Alert if active batch is > 30 days old. Reconciliation report shows delta between snapshot and current fallback. Admin can re-generate at any time. |

### R3: Stale batches — forgotten active batch

| | |
|---|---|
| **Risk** | Batch is activated and never regenerated as product catalog changes. |
| **Mitigation** | Batch health endpoint returns age. Future phase: nightly worker warns if batch is stale. Batch metadata includes `generated_at_utc` for easy age calculation. |

### R4: Performance — join overhead on hot analytics path

| | |
|---|---|
| **Risk** | Left-joining 200K snapshot rows adds latency to every analytics request. |
| **Mitigation** | Index on `(batch_id, prodaja_stavka_id)` makes point lookups fast. Single `activeBatchId` lookup is cached per request. For truly large datasets, consider materialized views in phase 2. Benchmark in staging before production activation. Expected impact: < 50ms based on indexed join. |

### R5: Dual-path divergence — snapshot and legacy give different numbers

| | |
|---|---|
| **Risk** | When snapshot is active, margins differ from what users saw before. This is by design (that's the point of the snapshot), but sudden changes can confuse users. |
| **Mitigation** | Reconciliation report quantifies deltas before activation. Staged rollout with product owner review. UI clearly marks when snapshot mode is active. |

### R6: Rollout mistakes — flag enabled before batch exists

| | |
|---|---|
| **Risk** | Operator enables `UseSnapshotCost` but forgets to create/activate a batch. |
| **Mitigation** | When flag is on and no active batch exists, behavior is identical to legacy. No crash, no incorrect data. Health endpoint warns "Flag is on but no active batch." |

### R7: Bulk insert performance during generation

| | |
|---|---|
| **Risk** | Generating 500K snapshot rows in a single transaction might be slow or cause lock contention. |
| **Mitigation** | Use batched inserts (5000 rows per `SaveChanges`). Generation runs against a read-only snapshot of Artikli; no write locks on sales tables. Execute during low-traffic hours. |

### R8: Maintenance burden — new enum value in MarginCostSource

| | |
|---|---|
| **Risk** | Adding `SnapshotFallback` requires updating every `switch` or pattern match on `MarginCostSource`. |
| **Mitigation** | Audit all usages of the enum before merging PR 2. The enum currently has 4 values (None, Historical, ProductFallbackRsd, ProductFallbackLegacy). Grep for all references and update. |

---

## 14. Minimal ticket backlog

### P0 — Must-have for feature delivery

| # | Title | Owner | Dependency | Acceptance Criteria |
|---|-------|-------|------------|-------------------|
| P0-1 | EF Core migration: snapshot tables | Backend | None | Tables created, indexes present, unique partial index enforced, down migration drops both tables |
| P0-2 | Domain models: AnalyticsCostSnapshotBatch + AnalyticsSaleLineCostSnapshot | Backend | None | Entities compile, map to correct tables, have navigation properties |
| P0-3 | DbContext: register snapshot entities | Backend | P0-2 | DbSets exist, OnModelCreating configures table names + indexes |
| P0-4 | Feature flag: AnalyticsSnapshotOptions | Backend/Infra | None | Options class registered, both flags default false, configurable in appsettings |
| P0-5 | Extend MarginCostSource + MarginAccumulator for snapshot | Backend | None | `SnapshotFallback` enum value exists, `SnapshotCostRevenue` tracked, `ResolveUnitCostWithSnapshot()` works, existing callers unchanged |
| P0-6 | Batch generation service | Backend | P0-1, P0-2, P0-3 | Can create batch, generate rows, update stats. Dry-run works. Handles 100K+ rows. No-cost lines skipped. |
| P0-7 | Admin endpoints for batch CRUD | Backend | P0-4, P0-6 | Create, generate, activate, deactivate, list, detail endpoints work. Gated by SnapshotAdminEnabled. |
| P0-8 | Snapshot-aware supplier endpoint | Backend | P0-5, P0-6 | When flag on + batch active, uses snapshot cost. When off, identical to legacy. Adds snapshot fields to response. |
| P0-9 | Snapshot-aware shoe-type endpoint | Backend | P0-8 | Same as P0-8 for shoe-type. |
| P0-10 | Frontend: API interface updates | Frontend | P0-8 API contract | New optional fields added to TS interfaces |
| P0-11 | Frontend: supplier page snapshot awareness | Frontend | P0-10 | Snapshot badge, coverage display, detail note, print note |
| P0-12 | Frontend: shoe-type page snapshot awareness | Frontend | P0-10 | Same as P0-11 |

### P1 — Important for safe rollout

| # | Title | Owner | Dependency | Acceptance Criteria |
|---|-------|-------|------------|-------------------|
| P1-1 | Reconciliation comparison endpoint | Backend | P0-8, P0-9 | Returns per-supplier and per-shoe-type margin deltas, snapshot vs legacy |
| P1-2 | Structured logging in batch lifecycle | Backend | P0-6 | All lifecycle events logged with structured properties |
| P1-3 | Batch health endpoint | Backend | P0-7 | Returns active batch age, warns if stale or missing |
| P1-4 | Read-path cost source breakdown in response | Backend | P0-8 | Response includes `costSourceBreakdown` object |
| P1-5 | Frontend: print/export snapshot note | Frontend | P0-11 | Print footer shows snapshot date and coverage when active |
| P1-6 | Unit tests: batch generation | QA/Backend | P0-6 | T3, T4, T5 pass |
| P1-7 | Unit tests: read precedence | QA/Backend | P0-8 | T6, T7, T8, T9, T10, T11 pass |
| P1-8 | Regression tests: supplier + shoe-type | QA/Backend | P0-8, P0-9 | T12 passes |

### P2 — Nice-to-have / phase 2

| # | Title | Owner | Dependency | Acceptance Criteria |
|---|-------|-------|------------|-------------------|
| P2-1 | Snapshot admin page (UI) | Frontend | P0-7 | Batch list, create/generate/activate buttons work |
| P2-2 | Stale batch warning worker | Backend | P0-7 | Background check logs warning if active batch > 30 days |
| P2-3 | Color analytics snapshot support | Backend/Frontend | P0-8 | ColorSalesStatsPage uses snapshot path |
| P2-4 | Latency monitoring dashboard | Infra | P0-8 | PerformanceLogs segmented by snapshot flag |
| P2-5 | Migration test: up/down round-trip | QA | P0-1 | T1 passes |

---

## 15. Expected files to change

### Backend — existing files to modify

| File | Why | Phase 1 critical? |
|------|-----|--------------------|
| `Application/Analytics/AnalyticsMarginPolicy.cs` | Add `SnapshotFallback` to enum, extend `MarginAccumulator`, add `ResolveUnitCostWithSnapshot()`, update `MarginQualityClassifier` | Yes |
| `Api/Endpoints/AllEndpoints.cs` | Supplier (~L1215) and shoe-type (~L1830) endpoints: add snapshot join, use new resolution, add response fields | Yes |
| `Infrastructure/DbContexts/TrendplusDbContext.cs` | Register new DbSets, configure table mappings and indexes | Yes |
| `Api/appsettings.json` | Add `Analytics` feature flag section | Yes |
| `Api/appsettings.Development.json` | Add `Analytics` section with dev defaults | Yes |
| `Api/Program.cs` (or DI registration file) | Register `AnalyticsSnapshotOptions`, register `AnalyticsCostSnapshotService` | Yes |

### Backend — new files to add

| File | Why | Phase 1 critical? |
|------|-----|--------------------|
| `Domain/Model/Analytics/AnalyticsCostSnapshotBatch.cs` | Batch entity | Yes |
| `Domain/Model/Analytics/AnalyticsSaleLineCostSnapshot.cs` | Snapshot line entity | Yes |
| `Infrastructure/Configuration/AnalyticsSnapshotOptions.cs` | Feature flag options class | Yes |
| `Infrastructure/Migrations/YYYYMMDDHHMMSS_AddAnalyticsCostSnapshotTables.cs` | EF Core migration | Yes |
| `Application/Analytics/AnalyticsCostSnapshotService.cs` | Batch generation, activation, reconciliation | Yes |
| `Api/Endpoints/AnalyticsSnapshotEndpoints.cs` | Admin HTTP endpoints | Yes |

### Frontend — existing files to modify

| File | Why | Phase 1 critical? |
|------|-----|--------------------|
| `Klijent/clientapp/src/services/supplierSalesStatsApi.ts` | Add snapshot fields to TS interfaces | Yes |
| `Klijent/clientapp/src/services/shoeTypeSalesStatsApi.ts` | Add snapshot fields to TS interfaces | Yes |
| `Klijent/clientapp/src/utils/marginQuality.ts` | Handle snapshot quality tier, tooltip, badge | Yes |
| `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx` | Snapshot badge, detail note, coverage display | Yes |
| `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx` | Same as above | Yes |
| `Klijent/clientapp/src/pages/SupplierSalesStatsPage.css` | Snapshot badge styling | Yes |
| `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.css` | Same | Yes |
| `Klijent/clientapp/src/pages/AnalyticsPrintPage.tsx` | Snapshot footer note | Optional (P1) |

### Frontend — new files to add

| File | Why | Phase 1 critical? |
|------|-----|--------------------|
| `Klijent/clientapp/src/pages/SnapshotAdminPage.tsx` | Admin UI for batch management | No (P2) |

### Migrations / config files

| File | Why | Phase 1 critical? |
|------|-----|--------------------|
| `Infrastructure/Migrations/YYYYMMDDHHMMSS_AddAnalyticsCostSnapshotTables.cs` | Primary EF migration | Yes |
| `Database/Migrations/029_AddAnalyticsCostSnapshotTables.sql` | Optional parallel SQL migration for manual deployment | Optional |

---

## 16. Open implementation questions

| # | Question | Blocks |
|---|----------|--------|
| Q1 | **Which database does the snapshot table go in?** The system has `DefaultConnection` (trendplus) and `AnalyticsConnection` (analytics). Snapshot tables reference `ProdajaStavke` via FK, which is in `trendplus`. Options: (a) put in `trendplus` alongside sales data — simpler FK integrity; (b) put in `analytics` — cleaner separation but cross-database FK not possible. **Recommendation:** Put in `trendplus` — FK integrity matters more than logical separation. | PR 1 |
| Q2 | **Should snapshot generation include lines that already have `ProdajaStavka.NabavnaCena` (POS-origin)?** The plan says no — only Access-origin lines without historical cost. But some POS lines might also have null `NabavnaCena` (edge case). **Recommendation:** Snapshot all lines where `NabavnaCena IS NULL` regardless of origin, but tag with `DataOrigin` in batch scope. | PR 2 |
| Q3 | **Batch re-generation: overwrite or new batch?** When admin wants to re-snapshot, should they create a new batch or regenerate rows in the existing one? **Recommendation:** Always create a new batch. Old batches are retained for audit. Supersede on activation. | PR 2 |
| Q4 | **What is the maximum expected line count?** This determines whether bulk insert strategy needs `COPY` command or if EF `SaveChanges` in batches is sufficient. Need to `SELECT COUNT(*) FROM "ProdajaStavke" ps JOIN "ProdajaZaglavlje" pz ON ps."IdProdaja" = pz."Id" WHERE pz."DataOrigin" = 'access' AND ps."NabavnaCena" IS NULL` on production data. | PR 2 performance design |
| Q5 | **Should endpoint return `snapshotDate` to frontend?** If yes, frontend can show "Cene zamrznute: 15. jun 2025". **Recommendation:** Yes — return `snapshotGeneratedAtUtc` from the active batch. | PR 3, F3, F4 |

---

## 17. Final go/no-go recommendation

**Proceed now with minimal snapshot layer.**

Justification:

1. **The problem is real and proven.** The forensic analysis showed 0% historical cost coverage on Access-imported sales. Every `Artikli.NabavnaCenaDin` change retroactively alters historical analytics. The temporal drift audit confirmed reproducibility failure.

2. **The solution is minimal.** Two tables, one service, one feature flag, two endpoint modifications, and reactive frontend changes. No operational data mutation. No new workers. No new external dependencies.

3. **Risk is managed.** Feature flag + batch activation model means the feature can be deployed dark, tested in staging, activated in production, and rolled back in under a minute without data loss.

4. **The existing infrastructure supports it.** `MarginAccumulator` already tracks cost sources. `MarginQualityClassifier` already grades data quality. The admin endpoint pattern (`AdminRepairEndpoints`) exists. The batch metadata pattern (`DataImportBatch`) exists. The configuration pattern (`IOptions<T>`) is established.

5. **The cost of NOT doing this** is continued analytics instability — every product cost update silently rewrites historical margin reports, and users have no way to detect it.

Build it.

---

## 18. Optional coding kickoff prompt — PR 1 only

```
You are implementing PR 1 of the analytics cost snapshot layer for Trendplus2.

SCOPE: Schema + domain models + feature flag scaffolding ONLY.
NO read path changes. NO batch generation logic. NO admin endpoints. NO frontend.

TASK 1: Create domain model `Domain/Model/Analytics/AnalyticsCostSnapshotBatch.cs`
- Namespace: Domain.Model.Analytics
- Sealed class with [Key] on Id (long)
- Properties: Id, Scope (string, default "access_origin"), Status (string, default "draft"),
  CreatedAtUtc, GeneratedAtUtc?, ActivatedAtUtc?, DeactivatedAtUtc?, CreatedBy (string, default "system"),
  Description?, RowCount (int), TotalRevenueCovered (decimal), CoveragePct (double), NoCostPct (double),
  GenerationDurationMs? (int?), DryRun (bool), ErrorMessage?, MetadataJson?
- [MaxLength] on Status(20), Scope(50), CreatedBy(100)
- Navigation: ICollection<AnalyticsSaleLineCostSnapshot> Snapshots

TASK 2: Create domain model `Domain/Model/Analytics/AnalyticsSaleLineCostSnapshot.cs`
- Namespace: Domain.Model.Analytics
- Sealed class with [Key] on Id (long)
- Properties: Id, BatchId (long), ProdajaStavkaId (int), ResolvedUnitCost (decimal),
  CostSource (short), ProductCostRsdAtSnapshot (decimal?), ProductCostLegacyAtSnapshot (decimal?), ArtikalId (int)
- Navigation: AnalyticsCostSnapshotBatch Batch

TASK 3: Register in TrendplusDbContext (Infrastructure/DbContexts/TrendplusDbContext.cs)
- Add DbSet<AnalyticsCostSnapshotBatch> AnalyticsCostSnapshotBatches
- Add DbSet<AnalyticsSaleLineCostSnapshot> AnalyticsSaleLineCostSnapshots
- In OnModelCreating: map to table names "analytics_cost_snapshot_batches" and
  "analytics_sale_line_cost_snapshots" with snake_case column names
- Configure unique partial index on batches (scope) WHERE status = 'active'
  via HasIndex + HasFilter
- Configure unique index on snapshots (batch_id, prodaja_stavka_id)
- Configure FK: snapshot.BatchId → batch.Id with cascade delete
- Configure additional indexes per schema spec

TASK 4: Create feature flag options class `Infrastructure/Configuration/AnalyticsSnapshotOptions.cs`
- Follow existing pattern from AnalyticsDataQualityHealthOptions.cs
- Properties: UseSnapshotCost (bool, default false), SnapshotAdminEnabled (bool, default false)

TASK 5: Register options in DI (find the service registration — likely Api/Program.cs)
- services.Configure<AnalyticsSnapshotOptions>(config.GetSection("Analytics"))

TASK 6: Add config sections to Api/appsettings.json and Api/appsettings.Development.json
- "Analytics": { "UseSnapshotCost": false, "SnapshotAdminEnabled": false }
- In Development.json: "SnapshotAdminEnabled": true

TASK 7: Create EF Core migration
- Run: dotnet ef migrations add AddAnalyticsCostSnapshotTables -p Infrastructure -s Api
- OR create manually following timestamp convention YYYYMMDDHHMMSS

CONVENTIONS:
- Follow DataImportBatch.cs as the model pattern
- Follow AnalyticsDataQualityHealthOptions.cs as the options pattern
- Table names: snake_case (analytics_cost_snapshot_batches)
- Column names: snake_case via .HasColumnName() or UseSnakeCase convention
- PostgreSQL types: BIGSERIAL, TIMESTAMPTZ, NUMERIC(18,2/4), VARCHAR, TEXT, JSONB, BOOLEAN, etc.
- Build must pass: dotnet build Api/Api.csproj

DO NOT: modify AllEndpoints.cs, AnalyticsMarginPolicy.cs, or any frontend files.
```
