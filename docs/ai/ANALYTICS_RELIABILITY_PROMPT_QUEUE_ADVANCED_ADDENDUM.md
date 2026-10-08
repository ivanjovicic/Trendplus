# Analytics Reliability Prompt Queue - Advanced/V2 Addendum

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none in this addendum
Main RQ queue has no unclaimed READY prompt after RQ320 closure; RQ46 is IN_PROGRESS in the UI/Table/Chart addendum. P-UI-01..P-UI-54 are DONE and the P-UI queue has no READY prompt. `RQ586` and `RQ557` are DONE; `RQ584` is DONE in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; RQ591 frontend trust certification (RQ18) is DONE on main; RQ589 Advanced/V2 backend certification is DONE on main.

Use this queue with `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

Purpose: preserve the original Advanced/V2 acceptance records and their traceability. Backend acceptance for RQ13/RQ16/RQ17/RQ19/RQ20/RQ23/RQ24 is replaced by completed `RQ589`; frontend trust typing RQ18 remains owned by `RQ591` in the main queue.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ13 | OBSOLETE | advanced-date-boundaries | Replaced by completed RQ589 half-open date-boundary contract |
| RQ14 | OBSOLETE | heatmap-transaction-semantics | Replaced by RQ577: unsupported receipt-grain count is unavailable |
| RQ15 | OBSOLETE | basket-affinity-denominator | Replaced by RQ577: basket affinity is gated until receipt grain is proven |
| RQ16 | OBSOLETE | lifecycle-zero-baseline | Replaced by completed RQ589 null-baseline lifecycle contract |
| RQ17 | OBSOLETE | smart-reorder-cost-trust | Replaced by completed RQ589 nullable missing-cost profit contract |
| RQ18 | WAITING | v2-frontend-trust-types | Expose backend cost/margin coverage metadata in TS types |
| RQ19 | OBSOLETE | weekly-changelog-oos-semantics | Replaced by completed RQ589 current-snapshot OOS scope contract |
| RQ20 | OBSOLETE | weekly-changelog-zero-baseline | Replaced by completed RQ589 null comparison-baseline contract |
| RQ21 | OBSOLETE | outcome-not-measured-semantics | Replaced by completed RQ81/RQ86/RQ93 measured-evidence contract |
| RQ22 | OBSOLETE | outcome-realization-denominator | Replaced by completed RQ84 measured-impact sample and subset warnings |
| RQ23 | OBSOLETE | supplier-score-v2-empty-meta | Replaced by completed RQ589 supplier items/meta envelope |
| RQ24 | OBSOLETE | advanced-v2-meta-contract | Replaced by completed RQ589 shared V2 success/empty/freshness metadata |

## Advanced/V2 consolidation map (2026-10-05)

- `RQ589` completed the Advanced/V2 backend acceptance for `RQ13`, `RQ16`, `RQ17`, `RQ19`, `RQ20`, `RQ23` and `RQ24`; original criteria remain traceable below with OBSOLETE replacement status.
- `RQ21` and `RQ22` are OBSOLETE because completed action-outcome owners `RQ81`, `RQ84`, `RQ86` and `RQ93` cover their acceptance, with named regression tests on the individual prompts.
- `RQ14` and `RQ15` remain OBSOLETE only because `RQ577` explicitly suppresses the unsupported receipt-grain measures.
- `RQ591` is the named frontend trust/derived-metric certification owner for `RQ18` (V2 metadata types); its frontend scope is grouped with legacy type/derived-metric work in the main queue.
- `RQ18` remains WAITING in this addendum for traceability; replacement acceptance is satisfied by completed `RQ591` on main. Re-exposure still requires freshness/horizon, oracle/golden and identity/provenance certification.

---

## RQ13 - Advanced/V2 date boundary correctness

Status: OBSOLETE
Replacement: RQ589 DONE - half-open local-date bounds, proved by `V2DateOnlyUpperBound_IncludesTheEntireFinalCalendarDay_AndReportsUnknownFreshness`.
Ready after: RQ01 DONE or explicit reprioritization
Priority: P0
Type: backend/tests
Feature family: advanced-date-boundaries
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ13-<agent>.lock.md`
Commit suggestion: `fix(analytics): normalize advanced date boundaries`

### Why

Advanced/V2 endpoints parse `toDate` as an exact UTC instant and query `<= toDate`. Date-only UI values can exclude the selected day after midnight.

### Scope only

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- Advanced/V2 endpoint tests
- optional date normalization helper

### Do not touch

- core cached analytics endpoints unless a separate audit proves the same bug there
- SQL materialized views
- frontend redesign

### Do

1. Add tests for date-only `fromDate`/`toDate` where sales exist during the `toDate` day.
2. Normalize user date ranges to half-open intervals:
   - `>= fromDate.Date`
   - `< toDate.Date.AddDays(1)`
3. Apply consistently across Advanced/V2 endpoints.
4. Document if any endpoint intentionally uses exact timestamp semantics.

### Checks

- `git diff --check`
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- targeted Advanced/V2 tests

### Acceptance

- Date-only `toDate` includes the selected whole day.
- Date behavior is shared across Advanced/V2 endpoints.

---

## RQ14 - Weekly heatmap transaction semantics

Status: OBSOLETE
Ready after: RQ13 DONE or explicit unblocking
Priority: P1
Type: backend/tests
Feature family: heatmap-transaction-semantics
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ14-<agent>.lock.md`
Commit suggestion: `test(analytics): clarify heatmap transaction counts`

Replacement: RQ577. The source receipt grain is unproven, so the heatmap count is explicitly unavailable with `receipt_grain_unavailable`; revenue and units remain available. See `.ai/runs/2026-10-05-RQ577-evidence.md`.

### Why

Weekly heatmap appears to count sale lines as transactions. If UI labels the metric as transactions/receipts, this overstates activity for multi-line receipts.

### Scope only

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- Advanced/V2 heatmap tests
- frontend label only if the contract says “line count”

### Do not touch

- basket affinity
- core sales summary
- unrelated V2 endpoints

### Do

1. Add fixture with one receipt and multiple sale lines.
2. Decide metric name:
   - distinct receipt count, or
   - sale line count.
3. Fix calculation or label accordingly.

### Checks

- `git diff --check`
- targeted heatmap tests

### Acceptance

- Heatmap transaction metric matches its label.

---

## RQ15 - Basket affinity support denominator

Status: OBSOLETE
Ready after: RQ13 DONE or explicit unblocking
Priority: P1
Type: backend/tests
Feature family: basket-affinity-denominator
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ15-<agent>.lock.md`
Commit suggestion: `fix(analytics): correct basket affinity denominator`

Replacement: RQ577. Basket affinity is explicitly unavailable until source lineage proves receipt grain, so no denominator is presented. See `.ai/runs/2026-10-05-RQ577-evidence.md`.

### Why

Basket affinity includes multi-line sales using `COUNT(*) >= 2`, but pairs are built from distinct categories. A basket with two lines from one category can enter the denominator even though it cannot create a pair.

### Scope only

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- basket affinity tests

### Do not touch

- heatmap endpoint
- frontend visual redesign

### Do

1. Add tests for:
   - two lines same category
   - two lines different categories
   - three lines two distinct categories
2. Decide denominator:
   - multi-line receipts, or
   - receipts with at least two distinct categories.
3. Rename `totalMultiItemTransactions` or add `pairEligibleTransactions` if needed.

### Checks

- `git diff --check`
- targeted basket affinity tests

### Acceptance

- Support percentage denominator is explicit and correct.

---

## RQ16 - Lifecycle zero-baseline trend semantics

Status: OBSOLETE
Replacement: RQ589 DONE - nullable trend and `baselineStatus`, proved by `ProductLifecycle_ReportsNoBaselineInsteadOfZeroPercentGrowth`.
Ready after: RQ13 DONE or SQL queue Q70 evidence if relevant
Priority: P1
Type: backend/tests
Feature family: lifecycle-zero-baseline
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ16-<agent>.lock.md`
Commit suggestion: `test(analytics): mark lifecycle zero baseline explicitly`

### Why

Product lifecycle maps first-half zero and second-half positive units to `100%` trend. This is a no-baseline case, not a normal percent growth.

### Scope only

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- lifecycle tests
- optional response metadata field

### Do not touch

- SQL nivelacija views
- supplier decision scoring

### Do

1. Add tests for firstHalf=0/secondHalf>0 and firstHalf=0/secondHalf=0.
2. Add explicit baseline status or reason code if needed.
3. Keep stage classification stable unless product contract says otherwise.

### Checks

- `git diff --check`
- targeted lifecycle tests

### Acceptance

- New/no-baseline products do not look like ordinary +100% growth without explanation.

---

## RQ17 - Smart reorder missing-cost expected profit

Status: OBSOLETE
Replacement: RQ589 DONE - missing cost preserves null profit and unreliable status, proved by `SmartReorder_KeepsProfitUnknownWhenRecommendedUnitsHaveNoCost`.
Ready after: RQ01 DONE or explicit reprioritization
Priority: P0
Type: backend/tests
Feature family: smart-reorder-cost-trust
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ17-<agent>.lock.md`
Commit suggestion: `fix(analytics): prevent missing cost reorder profit inflation`

### Why

Smart reorder sets `reorderCost=0` when unit cost is missing, then calculates `expectedProfit = expectedRevenue - reorderCost`. Missing cost can produce artificially high profit.

### Scope only

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- smart reorder tests
- `Klijent/clientapp/src/services/insightStudioV2Api.ts` only if response fields change

### Do not touch

- inventory core algorithm
- Product Decision Center recommendations
- supplier decision SQL

### Do

1. Add tests for missing cost with positive recommended quantity and price.
2. Make `reorderCost`/`expectedProfit` nullable or add `costMissing`/`profitReliable=false` metadata.
3. Ensure missing-cost rows do not rank as high-profit opportunities.
4. Preserve reorder urgency if urgency is stock-based, but mark profit as unavailable.

### Checks

- `git diff --check`
- targeted smart reorder tests

### Acceptance

- Missing cost cannot inflate expected profit.
- UI has enough metadata to show profit as unknown.

---

## RQ18 - Advanced/V2 frontend trust metadata types

Status: WAITING
Ready after: RQ17 DONE or explicit unblocking
Priority: P1
Type: frontend-contract/tests
Feature family: v2-frontend-trust-types
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ18-<agent>.lock.md`
Commit suggestion: `fix(analytics): expose v2 trust metadata types`

### Why

Advanced/V2 backend returns cost/margin coverage metadata, but TypeScript types omit some fields. UI cannot reliably surface trust/coverage warnings if types hide them.

### Scope only

- `Klijent/clientapp/src/services/insightStudioV2Api.ts`
- Advanced/V2 UI/tests if existing
- backend only if field names need standardization

### Do not touch

- backend formulas
- core analytics API types

### Do

1. Compare backend V2 response fields with TS interfaces.
2. Add missing trust fields:
   - `marginDataCoveragePct`
   - `marginDataAvailable`
   - `knownCostSkuSharePct`
   - `revenueWithCost`
   - any source/coverage fields added by earlier prompts.
3. Add UI contract tests if present.

### Checks

- `git diff --check`
- `cd Klijent/clientapp && npm test -- --runInBand` or targeted tests if configured
- TypeScript check if configured

### Acceptance

- Frontend contracts preserve backend reliability metadata.

---

## RQ19 - Weekly changelog OOS semantics

Status: OBSOLETE
Replacement: RQ589 DONE - OOS is explicitly the current snapshot because transition history is unavailable.
Ready after: RQ13 DONE or explicit unblocking
Priority: P1
Type: backend/tests
Feature family: weekly-changelog-oos-semantics
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ19-<agent>.lock.md`
Commit suggestion: `fix(analytics): clarify weekly changelog oos count`

### Why

Weekly changelog comment says “new OOS this week” but code counts all current zero-stock articles.

### Scope only

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- weekly changelog tests
- frontend label if the metric remains current OOS

### Do not touch

- inventory alerts
- stock forecast

### Do

1. Decide metric:
   - current OOS count, or
   - newly became OOS this week.
2. Fix label or calculation.
3. Add tests for chronic OOS vs newly OOS if new-OOS is implemented.

### Checks

- `git diff --check`
- targeted weekly changelog tests

### Acceptance

- Weekly changelog does not mislabel current OOS as new weekly OOS.

---

## RQ20 - Weekly changelog zero-baseline percent semantics

Status: OBSOLETE
Replacement: RQ589 DONE - missing prior comparison returns null plus `no_baseline`, proved by `WeeklyChangelog_LeavesPercentChangesUnknownWithoutPriorWeekBaseline`.
Ready after: RQ19 DONE or explicit unblocking
Priority: P1
Type: backend/tests
Feature family: weekly-changelog-zero-baseline
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ20-<agent>.lock.md`
Commit suggestion: `test(analytics): mark weekly zero baseline changes`

### Why

Weekly changelog uses 0/100 percent fallbacks when previous week is zero. This hides no-baseline semantics.

### Scope only

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- weekly changelog tests
- optional response field for baseline status

### Do not touch

- lifecycle endpoint
- SQL views

### Do

1. Add tests for previous=0/current>0 and previous=0/current=0.
2. Add `baselineStatus`, nullable percent, or reason code as appropriate.
3. Do not present no-baseline as ordinary 100% or 0% without explanation.

### Checks

- `git diff --check`
- targeted weekly changelog tests

### Acceptance

- Weekly changes distinguish no baseline from normal percent changes.

---

## RQ21 - Outcome summary notMeasured semantics

Status: OBSOLETE
Ready after: RQ01 DONE or explicit reprioritization
Priority: P0
Type: backend/tests
Feature family: outcome-not-measured-semantics
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ21-<agent>.lock.md`
Replacement: RQ86 requires measured evidence before an outcome counts as measured; RQ81/RQ93 clear measured timestamps and amounts for `not_measured`. `AnalyticsActionItemServiceTests.GetOutcomeSummaryAsync_ReturnsSupportedCohortBuckets_AndKeepsUnknownImpactNull` proves notMeasured is counted separately and excluded from `MeasuredCount`.
Commit suggestion: `fix(analytics): keep not measured out of measured coverage`

### Why

Action outcome summary treats any outcome status except pending as measured. `notMeasured` should likely not count as measured evidence.

### Scope only

- `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`
- `Api.Tests/AnalyticsActionItemServiceTests.cs`

### Do not touch

- Decision Board display unless tests prove it needs a tiny adjustment
- action write APIs

### Do

1. Add fixture with closed actions that are `success`, `negative`, `neutral`, `notMeasured`, and `pending`.
2. Decide whether `notMeasured` belongs in measured sample size.
3. Ensure outcome coverage and measured sample size reflect the chosen contract.

### Checks

- `git diff --check`
- targeted action outcome tests

### Acceptance

- `notMeasured` cannot inflate measured coverage if it means “not measured”.

---

## RQ22 - Outcome realization denominator contract

Status: OBSOLETE
Ready after: RQ21 DONE
Priority: P1
Type: backend/tests/docs
Feature family: outcome-realization-denominator
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ22-<agent>.lock.md`
Replacement: RQ84 separates measured outcomes from the measured-impact sample and requires subset visibility/warnings. `AnalyticsActionItemServiceTests.GetOutcomeSummaryAsync_AddsCoverageAndMissingImpactWarnings_WhenClosedCoverageIsLow` proves the bounded denominator and warns when the impact sample is incomplete.
Commit suggestion: `test(analytics): define realization denominator`

### Why

Realization ratio is calculated on the subset with measured impact and expected impact. Missing measured impact produces a warning, but the ratio may still look valid for a biased subset.

### Scope only

- `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`
- action outcome tests/docs

### Do not touch

- action creation/upsert behavior
- frontend charts unless needed for warning display

### Do

1. Add fixtures where some measured actions lack measured impact.
2. Decide denominator contract:
   - ratio for measured-impact subset only, with explicit subset sample size, or
   - ratio unavailable unless coverage threshold is met.
3. Add warning/metadata for biased subset if needed.

### Checks

- `git diff --check`
- targeted outcome summary tests

### Acceptance

- Realization ratio cannot appear stronger than its impact sample coverage.

---

## RQ23 - Supplier scoring V2 no-data meta

Status: OBSOLETE
Replacement: RQ589 DONE - supplier items envelope carries explicit freshness metadata, proved by `SupplierScoringV2_UsesItemsEnvelopeWithUnknownFreshnessMeta`.
Ready after: RQ13 DONE or explicit unblocking
Priority: P1
Type: backend-contract/tests
Feature family: supplier-score-v2-empty-meta
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ23-<agent>.lock.md`
Commit suggestion: `fix(analytics): add supplier score empty meta`

### Why

Supplier scoring V2 returns a bare empty list when total revenue is zero. No-data should not be indistinguishable from a valid empty list.

### Scope only

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- frontend service type if response shape changes
- tests

### Do not touch

- supplier scoring formula weights
- supplier decision hub SQL

### Do

1. Add no-revenue test case.
2. Add meta/emptyReason wrapper or compatible field.
3. Preserve current list consumers if backward compatibility is required.

### Checks

- `git diff --check`
- targeted supplier scoring V2 tests

### Acceptance

- No-revenue supplier scoring is explicit `insufficient_data`/emptyReason, not silent empty success.

---

## RQ24 - Advanced/V2 reliability meta contract

Status: OBSOLETE
Replacement: RQ589 DONE - all successful V2 responses carry success/empty/freshness metadata; RQ21 is replaced by completed action-outcome owners RQ81/RQ84/RQ86/RQ93.
Priority: P1
Type: backend-contract/docs
Feature family: advanced-v2-meta-contract
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ24-<agent>.lock.md`
Commit suggestion: `docs(analytics): define advanced v2 reliability meta`

### Why

Advanced/V2 endpoints return mostly bare anonymous objects/arrays without consistent `dataQualityStatus`, `warningCode`, `emptyReason`, `sourceStatus`, freshness or coverage metadata. This makes reliability hard to enforce.

### Scope only

- `Api/Endpoints/InsightStudioV2Endpoints.cs`
- `Klijent/clientapp/src/services/insightStudioV2Api.ts`
- optional `docs/qa/ADVANCED_V2_ANALYTICS_META_CONTRACT.md`

### Do not touch

- individual formulas in the same task
- SQL materialized views
- core cached analytics API unless a shared DTO is deliberately introduced

### Do

1. Inventory all Advanced/V2 responses.
2. Define a wrapper/meta convention that can represent:
   - success
   - no data
   - partial/degraded data
   - missing cost coverage
   - zero baseline
   - fallback/helper data
3. Split implementation into smaller endpoint-specific prompts.

### Checks

- `git diff --check`
- docs-only unless adding type definitions

### Acceptance

- Advanced/V2 has a reliability metadata strategy before more formulas are trusted in UI.
