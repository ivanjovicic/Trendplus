# Analytics Reliability Prompt Queue - Legacy Advanced Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none in this addendum
Main queue READY prompt: `RQ01` in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

Use this queue with `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

Purpose: add reliability prompts for legacy `/api/analytics/advanced/*` and frontend derived analytics. These prompts must stay WAITING until the owner explicitly advances this addendum or reprioritizes after RQ01.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ25 | WAITING | legacy-date-boundaries | Fix date-only toDate exclusion in legacy Advanced analytics |
| RQ26 | WAITING | kpi-period-overlap | Prevent KPI current/previous period boundary overlap |
| RQ27 | WAITING | legacy-margin-fallback | Remove hard-coded 35% margin fallback from score logic |
| RQ28 | WAITING | abc-empty-meta | Add no-data meta to ABC empty/zero-revenue results |
| RQ29 | WAITING | aging-never-sold | Stop treating UpdatedAt as last sale for never-sold products |
| RQ30 | WAITING | daily-zscore-baseline | Exclude target day from daily outlier baseline |
| RQ31 | WAITING | daily-target-no-data | Distinguish missing target day from normal zero-sales day |
| RQ32 | WAITING | category-mixed-denominator | Clarify category velocity stock/sales denominator |
| RQ33 | WAITING | reorder-value-semantics | Clarify reorder value as revenue vs procurement cost |
| RQ34 | WAITING | legacy-frontend-trust-types | Expose legacy backend trust metadata in frontend types |
| RQ35 | WAITING | derived-approx-revenue | Label frontend-derived approximate revenue as estimated |
| RQ36 | WAITING | derived-margin-fake-zero | Stop defaulting missing derived margin to zero |
| RQ37 | WAITING | derived-stock-value-cost | Stop valuing stock with net selling price when cost is missing |
| RQ38 | WAITING | derived-smart-reorder-cost | Prevent derived smart reorder missing-cost profit inflation |

---

## RQ25 - Legacy Advanced date boundary correctness

Status: WAITING
Ready after: RQ01 DONE or explicit reprioritization
Priority: P0
Type: backend/tests
Feature family: legacy-date-boundaries
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ25-<agent>.lock.md`
Commit suggestion: `fix(analytics): normalize legacy advanced date boundaries`

### Why

Legacy Advanced endpoints parse `toDate` as an exact UTC instant and query `<= toDate`. Date-only UI values can exclude the selected day after midnight.

### Scope only

- `Api/Endpoints/InsightStudioEndpoints.cs`
- legacy Advanced endpoint tests
- optional shared date range helper if small and isolated

### Do not touch

- Advanced/V2 endpoints unless doing RQ13 separately
- SQL views
- frontend layout

### Do

1. Add tests for date-only `fromDate`/`toDate` where sales exist during the selected `toDate` day.
2. Normalize to half-open ranges:
   - `>= from.Date`
   - `< to.Date.AddDays(1)`
3. Apply consistently across legacy Advanced endpoints.

### Checks

- `git diff --check`
- targeted legacy Advanced tests

### Acceptance

- Date-only `toDate` includes the whole selected day.

---

## RQ26 - KPI snapshot current/previous period overlap

Status: WAITING
Ready after: RQ01 DONE or explicit reprioritization
Priority: P0
Type: backend/tests
Feature family: kpi-period-overlap
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ26-<agent>.lock.md`
Commit suggestion: `fix(analytics): prevent kpi period overlap`

### Why

KPI snapshot current period includes `from`, and previous period also includes `from`. The boundary instant/day can be double-counted in period-over-period comparisons.

### Scope only

- `Api/Endpoints/InsightStudioEndpoints.cs`
- KPI snapshot tests

### Do not touch

- supplier scorecard
- ABC/reorder endpoints
- UI charts unless labels need tiny update

### Do

1. Add fixture with sale exactly at the boundary.
2. Use non-overlapping half-open periods.
3. Decide whether previous period length should equal current period exactly.
4. Add tests for current/previous revenue and unit change.

### Checks

- `git diff --check`
- targeted KPI snapshot tests

### Acceptance

- A sale cannot be counted in both current and previous periods.

---

## RQ27 - Legacy margin fallback trust contract

Status: WAITING
Ready after: RQ25/RQ26 DONE or explicit unblocking
Priority: P0
Type: backend/tests
Feature family: legacy-margin-fallback
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ27-<agent>.lock.md`
Commit suggestion: `fix(analytics): remove legacy margin fake benchmark`

### Why

Supplier and category intelligence use a hard-coded 35% margin fallback when system margin has no cost evidence. Missing cost should not become a normal benchmark.

### Scope only

- `Api/Endpoints/InsightStudioEndpoints.cs`
- supplier/category tests
- optional response metadata for missing benchmark

### Do not touch

- V2 endpoints
- `AnalyticsMarginPolicy` unless a shared helper is needed
- frontend styling

### Do

1. Add no-cost evidence fixture.
2. Make benchmark nullable or add `benchmarkAvailable=false`/warning metadata.
3. Ensure profitScore/profitLift do not look trustworthy with zero cost coverage.

### Checks

- `git diff --check`
- targeted supplier/category tests

### Acceptance

- Missing margin evidence cannot create a fake 35% benchmark.

---

## RQ28 - ABC empty/no-revenue meta

Status: WAITING
Ready after: RQ25 DONE or explicit unblocking
Priority: P1
Type: backend-contract/tests
Feature family: abc-empty-meta
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ28-<agent>.lock.md`
Commit suggestion: `fix(analytics): mark abc empty results as no data`

### Why

ABC classification returns zero counts when there are no sales or total revenue is zero. This hides no-data/no-revenue.

### Scope only

- `Api/Endpoints/InsightStudioEndpoints.cs`
- ABC tests
- frontend type only if response shape changes

### Do not touch

- ABC threshold formula
- V2 lifecycle

### Do

1. Add tests for no sales and zero revenue.
2. Add `meta.emptyReason`/`dataQualityStatus=insufficient_data` or compatible field.
3. Preserve item/summary shape if needed for UI compatibility.

### Checks

- `git diff --check`
- targeted ABC tests

### Acceptance

- No-data ABC result is explicit.

---

## RQ29 - Aging stock never-sold handling

Status: WAITING
Ready after: RQ25 DONE or explicit unblocking
Priority: P0
Type: backend/tests
Feature family: aging-never-sold
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ29-<agent>.lock.md`
Commit suggestion: `fix(analytics): mark never sold aging stock explicitly`

### Why

Aging stock falls back to article `UpdatedAt` when no last sale exists. Recently edited never-sold products can look active.

### Scope only

- `Api/Endpoints/InsightStudioEndpoints.cs`
- aging stock tests
- frontend type only if adding fields

### Do not touch

- inventory movement algorithm
- V2 depletion endpoint

### Do

1. Add fixture for product with stock and no sales.
2. Add explicit `neverSold`/`lastSaleDate=null`/`agingEvidenceStatus` behavior.
3. Do not use `UpdatedAt` as sale evidence.
4. Decide aging category for never-sold stock based on created/imported date only if such source is reliable.

### Checks

- `git diff --check`
- targeted aging stock tests

### Acceptance

- Never-sold products cannot look recently sold because article metadata changed.

---

## RQ30 - Daily analysis z-score baseline

Status: WAITING
Ready after: RQ25 DONE or explicit unblocking
Priority: P1
Type: backend/tests
Feature family: daily-zscore-baseline
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ30-<agent>.lock.md`
Commit suggestion: `fix(analytics): exclude target day from zscore baseline`

### Why

Daily analysis calculates mean/stddev including the target day being evaluated, which can dampen outlier detection.

### Scope only

- `Api/Endpoints/InsightStudioEndpoints.cs`
- daily analysis tests

### Do not touch

- weekly changelog
- dashboard trend calculations

### Do

1. Add fixture with extreme target-day revenue.
2. Calculate baseline from comparable days excluding target day.
3. Add min-sample warning if baseline is too small.

### Checks

- `git diff --check`
- targeted daily analysis tests

### Acceptance

- Target day does not dilute its own outlier score.

---

## RQ31 - Daily analysis target no-data contract

Status: WAITING
Ready after: RQ30 DONE or explicit unblocking
Priority: P1
Type: backend-contract/tests
Feature family: daily-target-no-data
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ31-<agent>.lock.md`
Commit suggestion: `fix(analytics): mark missing daily target data`

### Why

If target day is missing, daily analysis can return zero revenue, zero units and “Normalan dan”. Missing data must not look like a valid zero-sales day.

### Scope only

- `Api/Endpoints/InsightStudioEndpoints.cs`
- daily analysis tests
- frontend type only if adding meta

### Do not touch

- z-score formula if RQ30 handles it
- import pipeline

### Do

1. Add tests for no target-day sale headers.
2. Add `targetDataStatus`, `emptyReason`, or `dataQualityStatus`.
3. Distinguish true zero-sales day from missing import/no sale header evidence.

### Checks

- `git diff --check`
- targeted daily analysis tests

### Acceptance

- Missing target day is not labeled normal without evidence.

---

## RQ32 - Category intelligence mixed denominator contract

Status: WAITING
Ready after: RQ25/RQ27 DONE or explicit unblocking
Priority: P1
Type: backend-contract/docs/tests
Feature family: category-mixed-denominator
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ32-<agent>.lock.md`
Commit suggestion: `docs(analytics): define category velocity denominator`

### Why

Category intelligence mixes period sales with current all-catalog average stock. This may be acceptable as a current-stock productivity ratio, but the response/label must say so.

### Scope only

- `Api/Endpoints/InsightStudioEndpoints.cs`
- category intelligence tests/docs

### Do not touch

- Product Decision Center
- V2 price sensitivity

### Do

1. Document current denominator.
2. Add test showing historical sales + current stock behavior.
3. Either rename field/tooltip or align stock denominator with the selected context.

### Checks

- `git diff --check`
- targeted category tests if code changes

### Acceptance

- Category velocity cannot be misread as period-only velocity without stock caveat.

---

## RQ33 - Legacy reorder value semantics

Status: WAITING
Ready after: RQ25 DONE or explicit unblocking
Priority: P1
Type: backend-contract/tests
Feature family: reorder-value-semantics
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ33-<agent>.lock.md`
Commit suggestion: `fix(analytics): clarify legacy reorder value semantics`

### Why

Legacy reorder summary uses selling price for `totalReorderValue`. If the UI reads this as procurement/order value, it is wrong.

### Scope only

- `Api/Endpoints/InsightStudioEndpoints.cs`
- `Klijent/clientapp/src/services/insightStudioApi.ts` if field renamed/added
- tests

### Do not touch

- smart reorder V2 cost/profit logic
- inventory core

### Do

1. Decide whether field means potential revenue or procurement cost.
2. Rename/add fields:
   - `potentialRevenueRsd`
   - `estimatedProcurementCostRsd`
   - `costCoveragePct`
3. Add test with price and cost different.

### Checks

- `git diff --check`
- targeted reorder tests

### Acceptance

- Reorder value label matches formula and does not imply cash cost if using selling price.

---

## RQ34 - Legacy frontend trust metadata types

Status: WAITING
Ready after: RQ27/RQ33 DONE or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: legacy-frontend-trust-types
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ34-<agent>.lock.md`
Commit suggestion: `fix(analytics): expose legacy trust metadata types`

### Why

Legacy backend returns margin/cost coverage fields, but frontend TypeScript types omit several of them. UI cannot reliably display trust metadata if types hide it.

### Scope only

- `Klijent/clientapp/src/services/insightStudioApi.ts`
- Insight Studio UI/tests if existing

### Do not touch

- backend formulas
- V2 TypeScript types unless doing RQ18 separately

### Do

1. Compare backend legacy response fields to frontend types.
2. Add missing fields such as `marginDataCoveragePct`, `revenueWithCost`, `marginDataAvailable` where returned.
3. Add small UI/contract tests if available.

### Checks

- `git diff --check`
- TypeScript check/test if configured

### Acceptance

- Frontend types preserve backend trust metadata.

---

## RQ35 - Derived approximate revenue labeling

Status: WAITING
Ready after: RQ34 DONE or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: derived-approx-revenue
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ35-<agent>.lock.md`
Commit suggestion: `fix(analytics): label derived revenue as estimated`

### Why

Derived category intelligence uses velocity × 30 × price but emits `totalRevenue`-shaped fields. Estimated values must not look like booked revenue.

### Scope only

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- Insight Studio UI/types/tests

### Do not touch

- backend category intelligence
- price/demand signal producers

### Do

1. Add tests for derived category intelligence.
2. Add `estimated=true`, `sourceStatus=derived`, or rename fields in derived view model.
3. Ensure UI labels distinguish estimated revenue from actual sales revenue.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Derived approximate revenue cannot be mistaken for booked revenue.

---

## RQ36 - Derived margin fake-zero guardrail

Status: WAITING
Ready after: RQ35 DONE or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: derived-margin-fake-zero
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ36-<agent>.lock.md`
Commit suggestion: `fix(analytics): keep missing derived margin unknown`

### Why

Derived analytics uses `marginPct ?? 0`. Missing margin evidence becomes 0% margin.

### Scope only

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- frontend types/tests

### Do not touch

- backend margin policy
- V2 backend formulas

### Do

1. Add tests for missing margin values.
2. Keep margin nullable or add coverage/warning metadata.
3. Ensure missing margin is not scored as bad margin unless explicitly intended.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Missing margin evidence remains unknown, not fake 0%.

---

## RQ37 - Derived stock value cost vs selling price

Status: WAITING
Ready after: RQ36 DONE or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: derived-stock-value-cost
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ37-<agent>.lock.md`
Commit suggestion: `fix(analytics): avoid selling price as stock cost value`

### Why

Derived aging stock falls back to net selling price when cost is missing. Stock value/capital-at-risk can be overstated.

### Scope only

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- frontend types/tests

### Do not touch

- backend aging stock
- inventory import pipeline

### Do

1. Add test for missing cost but present net price.
2. Do not report cost-based stock value if cost missing.
3. Optionally add separate `retailStockValue` field if useful.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Cost/capital value is not silently replaced by selling value.

---

## RQ38 - Derived smart reorder missing-cost profit

Status: WAITING
Ready after: RQ36 DONE or RQ17 DONE
Priority: P0
Type: frontend-contract/tests
Feature family: derived-smart-reorder-cost
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ38-<agent>.lock.md`
Commit suggestion: `fix(analytics): prevent derived reorder profit inflation`

### Why

Frontend derived smart reorder repeats the missing-cost profit inflation issue: `reorderCost = qty * (cost ?? 0)` and `expectedProfit = expectedRevenue - reorderCost`.

### Scope only

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- frontend types/tests

### Do not touch

- backend V2 smart reorder unless doing RQ17 separately
- inventory signal producers

### Do

1. Add missing-cost fixture with positive recommended qty and price.
2. Mark profit unavailable or unreliable when cost is missing.
3. Ensure summary `expectedProfitFromReorder` excludes/unflags unreliable profit.

### Checks

- `git diff --check`
- frontend tests/typecheck if configured

### Acceptance

- Derived smart reorder cannot inflate profit when cost is missing.
