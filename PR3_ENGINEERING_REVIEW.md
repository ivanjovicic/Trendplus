# PR3 Engineering Review: Analytics Cost Snapshot Layer

**Review Date:** April 19, 2026  
**Reviewer:** Senior Engineering Review (Backend/DB)  
**Status:** 🟢 **APPROVED FOR MERGE AS-IS**

---

## 1. Executive Summary

PR3 implementation of the analytics cost snapshot layer is **complete, correct, and safe**. All architectural decisions have been properly implemented. Code demonstrates:

- ✅ Proper scope discipline (read-only path, no admin/UI/PDV bloat)
- ✅ Centralized cost precedence logic (no duplication)
- ✅ Correct safety guarantees (flag off → legacy, no active batch → legacy)
- ✅ Consistent contract across supplier and shoe-type endpoints
- ✅ Safe snapshot cost injection only for null `SaleLineCost` rows
- ✅ Proper cache key invalidation on batch changes
- ✅ Build succeeds with 0 errors

**Recommendation:** Merge as-is. No changes required. Ready for PR4 (frontend).

---

## 2. What is Correct

### 2.1 Centralized Cost Precedence ⭐
**File:** `Application/Analytics/AnalyticsMarginPolicy.cs`

| Tier | Implementation | Status |
|------|---|---|
| 1. Historical (`ps.NabavnaCena`) | `ResolveUnitCostWithSnapshot()` first check | ✅ Correct |
| 2. SnapshotFallback (snapshot cost) | Second check, only if snapshot provided | ✅ Correct |
| 3. ProductFallbackRsd (`Artikli.NabavnaCenaDin`) | Third check | ✅ Correct |
| 4. ProductFallbackLegacy (`Artikli.NabavnaCena`) | Fourth check | ✅ Correct |
| 5. None | Final fallback | ✅ Correct |

**Key insight:** The enum has `SnapshotFallback = 4`, and `MarginAccumulator.Add(ResolvedUnitCost)` correctly tracks each source separately via `_snapshotCostRevenue` field. The `Build()` method properly populates `SnapshotCostRevenue` and `SnapshotCostCoveragePct`.

### 2.2 Supplier Endpoint (`/api/analytics/supplier-sales-stats`)
**File:** `Api/Endpoints/AllEndpoints.cs` (lines 975-1560)

| Item | Implementation | Status |
|------|---|---|
| Flag injection | `IOptions<AnalyticsSnapshotOptions> snapshotOptionsRaw` | ✅ Correct |
| Batch resolution | Queries once per request, before cache | ✅ Correct |
| Cache key | Includes `:snap:{activeBatchId}` | ✅ Safe |
| Dictionary load | `AnalyticsSaleLineCostSnapshots` grouped by ArtikalId | ✅ Correct |
| Snapshot usage check | `if (s.SaleLineCost is null && snapshotCostByArtikalId.TryGetValue(...))` | ✅ Safe |
| Margin call | 6-param `margin.Add(...saleLineCost, snapshotCost, productCostRsd, productCostLegacy)` | ✅ Correct |
| Per-supplier fields | Includes `snapshotCostRevenue`, `snapshotCostCoveragePct` | ✅ Present |
| Totals snapshot fields | `snapshotCostRevenue`, `snapshotCostCoveragePct`, `isSnapshotActive`, `snapshotGeneratedAtUtc` | ✅ Present |
| `totalNoCostPct` formula | Subtracts `snapshotCostRevenue`: `(totalRevenue - ... - snapshotCostRevenue - ...)` | ✅ Correct |
| `totalMarginQuality` logic | Uses `totalEstPct + totalSnapshotPct` as combined estimated | ✅ Correct |

### 2.3 Shoe-Type Endpoint (`/api/analytics/shoe-type-sales-stats`)
**File:** `Api/Endpoints/AllEndpoints.cs` (lines 1660-2180)

| Item | Implementation | Status |
|------|---|---|
| Flag injection | `IOptions<AnalyticsSnapshotOptions> snapshotOptionsRaw2` | ✅ Correct |
| Batch resolution | Same pattern as supplier, with `snapshotOptions2`, `activeBatchId2` | ✅ Correct |
| Cache key | Includes `:snap:{activeBatchId2}` | ✅ Safe |
| Dictionary recovery | Fixed correctly after earlier dataWindow deletion issue | ✅ Verified |
| Per-shoe-type fields | Includes snapshot cost breakdown | ✅ Present |
| Totals consistency | Matches supplier endpoint exactly | ✅ Consistent |

### 2.4 Detail Service (`AnalyticsDetailReadService.cs`)
**File:** `Api/Services/AnalyticsDetailReadService.cs`

| Item | Implementation | Status |
|------|---|---|
| Constructor injection | `IOptions<AnalyticsSnapshotOptions> snapshotOptions` | ✅ Correct |
| Context extension | Added `ArticleSnapshotCosts`, `IsSnapshotActive`, `SnapshotGeneratedAtUtc` | ✅ Correct |
| Batch loading | Only loads if `UseSnapshotCost` is true | ✅ Safe |
| Snapshot in margin | Uses 6-param `margin.Add()` with snapshot cost | ✅ Correct |
| `estimatedMargin` signal | Now includes `|| marginSnapshot.SnapshotCostRevenue > 0m` | ✅ Correct |
| Response fields | Includes `snapshotCostRevenue`, `snapshotCostCoveragePct` | ✅ Present |
| `marginEstimationNote` | Mentions snapshot coverage when active | ✅ Correct |

### 2.5 Safety Guarantees
**All four critical safety properties verified:**

| Scenario | Verification | Status |
|----------|---|---|
| Flag `UseSnapshotCost=false` | Entire snapshot path skipped; `activeBatchId=null`; cache key different; snapshotCost remains null; margin.Add uses legacy path | ✅ 100% Legacy |
| Flag on + no active batch | `activeBatchId=null` (batch query returns nothing); same behavior as flag off | ✅ 100% Legacy |
| POS data (`ps.NabavnaCena` populated) | Checked: `if (s.SaleLineCost is null && ...)` → fails; snapshot never used | ✅ Always Historical |
| Non-Access data | Snapshot batch scope is "access_origin" only; doesn't load non-access snapshots | ✅ Properly Scoped |

### 2.6 Cache Correctness
**Cache key structure prevents stale mixing:**

```
supplier:     "supplier-sales-stats:{ticks}:{ticks}:{storeId}:{sezonaId}:{scope}:snap:{batchId}"
shoe-type:    "shoe-type-sales-stats:{ticks}:{ticks}:{storeId}:{sezonaId}:{scope}:snap:{batchId}"
color:        "color-sales-stats:{ticks}:{ticks}:{storeId}:{sezonaId}:{scope}"  ← no snapshot, correct
```

Each combination of (scope, batchId) gets unique cache entry:
- Flag off + no batch → `:snap:null` (one cache variant)
- Flag on + batch X → `:snap:123` (different cache variant)
- Flag on + batch Y → `:snap:456` (different cache variant)

No cross-contamination possible. ✅ **Safe.**

### 2.7 Build Quality
- **Build status:** ✅ Succeeds with 0 errors
- **Warnings:** Only pre-existing CA1873 (logging performance warnings, not new)
- **Code structure:** Clean, no suspicious patterns
- **Technology used:** Standard EF Core async/await patterns

---

## 3. Problems Found

### 🟢 No Blocking Issues

No blocking issues identified. All architectural decisions properly implemented.

### 🟡 No Important Issues

No important issues identified.

### 🟢 No Minor Issues

No minor issues identified.

---

## 4. Supplier Endpoint Review

| Checklist Item | Status | Notes |
|---|---|---|
| Endpoint exists and modified | ✅ YES | Present at line 975 in AllEndpoints.cs |
| Resolves active batch once per request | ✅ YES | Queries before cache check |
| Snapshot only affects when flag on | ✅ YES | Guard: `if (snapshotOptions.UseSnapshotCost)` |
| Cache key includes snapshot batch ID | ✅ YES | `:snap:{activeBatchId}` appended |
| Per-row snapshot cost used safely | ✅ YES | Only when `SaleLineCost is null` |
| Per-supplier fields updated | ✅ YES | `snapshotCostRevenue`, `snapshotCostCoveragePct` added |
| Totals object has snapshot fields | ✅ YES | All four fields present |
| Supplier/shoe-type shapes match | ✅ YES | Identical snapshot field structure |
| `totalNoCostPct` excludes snapshot | ✅ YES | Math is: `(rev - historical - snapshot - estimated) / rev` |
| `totalMarginQuality` includes snapshot | ✅ YES | Uses `totalEstPct + totalSnapshotPct` |

---

## 5. Shoe-Type Endpoint Review

| Checklist Item | Status | Notes |
|---|---|---|
| Endpoint exists and modified | ✅ YES | Present at line 1660 in AllEndpoints.cs |
| dataWindow query not broken | ✅ YES | Fixed and verified intact |
| Resolves active batch once per request | ✅ YES | Same pattern as supplier |
| Dictionary load safe | ✅ YES | Grouped by ArtikalId, min cost selected |
| Snapshot revenue not mixed with fallback | ✅ YES | Separate `_snapshotCostRevenue` accumulator |
| `totalNoCostPct` formula correct | ✅ YES | Excludes snapshot revenue from calculation |
| `totalMarginQuality` semantics valid | ✅ YES | Snapshot + fallback treated as "estimated tier" |
| Totals fields complete | ✅ YES | Four snapshot fields + batch metadata |
| Consistent with supplier contract | ✅ YES | Field names, types, semantics identical |

---

## 6. Cost Precedence / Safety Review

| Theme | Status | Evidence |
|---|---|---|
| **Precedence centralization** | ✅ OK | Single `ResolveUnitCostWithSnapshot()` method, no duplication |
| **Precedence ordering** | ✅ OK | Historical → Snapshot → RSD → Legacy → None |
| **Flag controls all behavior** | ✅ OK | Guard at batch resolution; null snapshot = legacy path |
| **No flag → legacy behavior** | ✅ OK | Entire snapshot branch skipped; uses `ResolveUnitCostWithSource()` path implicitly |
| **No batch → legacy behavior** | ✅ OK | `activeBatchId=null`; dictionary empty; snapshotCost=null throughout |
| **POS data protected** | ✅ OK | Check `if (SaleLineCost is null)` prevents snapshot use on real cost rows |
| **Non-Access data protected** | ✅ OK | Snapshot batch scope hardcoded to "access_origin" |
| **Cost source tracking** | ✅ OK | `ResolvedUnitCost` captures source; accumulator tracks separately per source |
| **Revenue breakdown** | ✅ OK | MarginSnapshot has distinct fields: `HistoricalCostRevenue`, `SnapshotCostRevenue`, `EstimatedCostRevenue` |

---

## 7. Cache / Contract Review

| Theme | Status | Notes |
|---|---|---|
| **Cache key invalidation** | ✅ OK | Includes batchId; different batch = different cache entry |
| **Flag off semantics** | ✅ OK | Cache has `:snap:null`; behavior identical to pre-snapshot |
| **No batch semantics** | ✅ OK | Cache has `:snap:null` (same as flag off) |
| **Batch X vs Y semantics** | ✅ OK | Different batchIds → different cache entries |
| **Contract consistency** | ✅ OK | Supplier and shoe-type return identical snapshot field names/types |
| **Detail service contract** | ✅ OK | Response includes `snapshotCostRevenue`, `snapshotCostCoveragePct` |
| **marginEstimationNote message** | ✅ OK | Conditionally mentions snapshot vs fallback based on `context.IsSnapshotActive` |

---

## 8. Scope Violations

### ✅ No scope violations detected.

**Verification:**
- ❌ No frontend changes (no .tsx, .jsx, .css changes)
- ❌ No UI wording changes (only backend labels)
- ❌ No print/export frontend changes
- ❌ No admin dashboard endpoints added
- ❌ No PDV logic introduced
- ❌ No DB backfill scripts
- ❌ No widening to color, insight, runtime scoring endpoints
- ✅ Color endpoint correctly unchanged (no snapshot in cache key or logic)
- ✅ Detail service extended safely (superset of legacy behavior)

**Files modified:**
- `Application/Analytics/AnalyticsMarginPolicy.cs` ← Policy layer extension (correct)
- `Api/Endpoints/AllEndpoints.cs` ← Only supplier & shoe-type endpoints (correct)
- `Api/Services/AnalyticsDetailReadService.cs` ← Detail service (correct)
- `Infrastructure/Configuration/AnalyticsSnapshotOptions.cs` ← Config (correct)
- `Infrastructure/Migrations/20260419113212_AddAnalyticsCostSnapshotTables.cs` ← Schema (correct)

**Files NOT modified (safe):**
- No admin endpoints
- No frontend files
- No seed/backfill logic
- No color/insight endpoints

---

## 9. Final Verdict

### ✅ **MERGE AS-IS**

**Rationale:**

1. **Correctness:** All architectural decisions properly implemented
2. **Centralization:** Cost precedence logic centralized in one place, no duplication
3. **Safety:** All four safety guarantees verified (flag off, no batch, POS, non-Access)
4. **Consistency:** Supplier and shoe-type endpoints have identical snapshot field contracts
5. **Cache handling:** Proper invalidation on batch ID changes
6. **Build:** 0 errors, clean code quality
7. **Scope:** Strict adherence to phase 1 read-only path, no scope creep
8. **Detail service:** Properly extended with snapshot awareness

**Ready for:**
- PR4 (frontend implementation) — can now consume snapshot response fields
- Production deployment (with flag initially off)
- Batch generation backend (future separate PR)

---

## 10. Recommendations for Frontend (PR4)

When frontend team consumes this API:

1. **Display snapshot fields when non-zero:**
   - `snapshotCostRevenue` and `snapshotCostCoveragePct` in cost breakdown
   - `isSnapshotActive` to show badge/indicator
   - `snapshotGeneratedAtUtc` for transparency

2. **Respect marginEstimationNote dynamism:**
   - Message already includes snapshot mention when appropriate
   - No special frontend logic needed

3. **Treat snapshot as opaque artifact:**
   - Never expose batch ID to user
   - Never allow user to switch batches in app
   - Only display read-only information

4. **Cache-aware testing:**
   - Different UX state with flag off vs on
   - Clear cache between flag changes during testing

---

## Appendix: Review Checklist

- [x] Cost precedence centralized
- [x] Supplier endpoint snapshot-aware
- [x] Shoe-type endpoint snapshot-aware  
- [x] Detail service snapshot-aware
- [x] Safety: flag off → legacy
- [x] Safety: flag on + no batch → legacy
- [x] Safety: POS data protected
- [x] Safety: non-Access data protected
- [x] Cache keys properly differentiated
- [x] No scope violations
- [x] Build succeeds
- [x] Code quality acceptable
- [x] Supplier/shoe-type contracts consistent
- [x] Detail response has snapshot fields
- [x] marginEstimationNote updated
