# Analytics Reliability Prompt Queue

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none (`RQ112` is `IN_PROGRESS`; continue it as the active owner task, then promote the staged `WAITING` chain)
Owner-promoted test pack: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md` (`RQ100`-`RQ105` DONE); `RQ96` DONE; `RQ106` DONE; `RQ97` DONE; `RQ98` DONE. `RQ108` is DONE on current main and `RQ109` is DONE on current main.

Use this queue with `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

Purpose: isolate analytics data-reliability work from SQL formula work. This queue targets false confidence, wrong denominators, hidden fallback states, dataScope drift and board composition errors.

## Queue rules

1. Start only the prompt marked `READY`.
2. Create a local uncommitted lock before work:
   - `.ai/task-locks/<task-id>-<agent>.lock.md`
3. Do not mix reliability contract tests, frontend UX and SQL formula rewrites in one task.
4. When behavior is ambiguous, add tests/docs first; do not guess a product contract.
5. Any fix that can change business output must preserve old behavior in a test fixture or explicitly document the before/after.
6. After finishing one prompt, update this queue with status, changed files, checks and the next READY prompt.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ01 | DONE | decision-board-impact-trust | Prevent wrong expected-impact fallback in board product cards |
| RQ02 | DONE | product-decision-denominators | Define PDC summary top/all-row denominator contract |
| RQ03 | DONE | lost-sales-zero-vs-unknown | Separate unavailable lost-sales evidence from true zero |
| RQ04 | DONE | data-quality-no-data | Prevent no-revenue data-quality windows from looking green |
| RQ05 | DONE | analytics-datascope-consistency | Audit dataScope semantics across analytics modules |
| RQ06 | DONE | data-quality-offender-scope | Fix top-offender revenue impact scope drift |
| RQ07 | DONE | missing-cost-offenders | Add missing-cost offender drilldown contract |
| RQ08 | DONE | supplier-blocked-signal-ranking | Cap/label blocked supplier signals in Decision Board |
| RQ09 | DONE | action-source-empty-state | Decide whether zero analytics actions is healthy empty or insufficient data |
| RQ10 | DONE | inventory-evidence-confidence | Add evidence-based confidence contract for inventory cards |
| RQ11 | DONE | transaction-stat-semantics | Clarify transaction item/line/unit count semantics |
| RQ12 | DONE | pdc-ignored-rows-contract | Make Product Decision Center ignored/top rows explicit |
| RQ13 | DONE | inventory-evidence-wiring | Wire signal confidence onto board inventory cards |
| RQ106 | DONE | decision-pulse-digest | Email + in-app exception digest after QDB06 and RQ96 |
| RQ107 | DONE | scenario-planning-contract | Freeze docs-only scenario vocabulary while runtime stays gated |
| RQ108 | DONE | forecast-materializer-observed-window | Add authoritative forecast materializer and observed pairing foundation |
| RQ109 | DONE | decision-pulse-expansion | Expand Decision Pulse beyond the first Product Decision slice |
| RQ110 | DONE | analytics-screen-data-availability | Prove pilot analytics screens stay non-empty when authoritative seeded data exists |
| RQ111 | DONE | analytics-refresh-cache-parity | Close refresh/cache/materialized-view gaps that can hide existing data |
| RQ112 | IN_PROGRESS | analytics-summary-detail-reconciliation | Reconcile pilot analytics summary values against detail/export on the first proven family |
| RQ113 | WAITING | analytics-generation-provenance-truth | Expose exact freshness/provenance truth for the first pilot family that still looks trusted by inference |
| RQ114 | WAITING | analytics-deterministic-seed-pack | Build a reusable deterministic seed pack and expected-output manifest for pilot analytics proof |
| RQ115 | WAITING | analytics-dashboard-seeded-proof | Isolate dashboard seeded-data proof left open by RQ110 |
| RQ116 | WAITING | decision-pulse-delivery-truth | Prove Pulse queued/sent/disabled states without claiming unverified delivery |
| RQ117 | WAITING | forecast-observed-pair-availability | Prove forecast/observed pairing availability and stale/missing semantics |
| RQ118 | WAITING | data-quality-issues-scope-lineage | Close the residual unscoped Data Quality issues sales window |
| RQ119 | WAITING | analytics-dual-origin-scope-contract | Resolve or explicitly expose PDC/inventory dual-origin scope behavior |
| RQ120 | WAITING | analytics-trust-metadata-ui-propagation | Surface source/denominator/provenance metadata in the first proven pilot UI |

---

## RQ01 - Decision Board product expected-impact correctness

Status: DONE
Priority: P0
Type: backend/tests
Feature family: decision-board-impact-trust
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ01-cursor.lock.md`
Commit suggestion: `fix(analytics): preserve product impact trust in decision board`

### Why

Decision Board currently uses `row.ExpectedImpactRsd ?? row.LostSalesEstimate` for product cards. Product Decision Center already sets expected impact based on recommendation type. The board should not reattach lost-sales estimate to rows where Product Decision Center intentionally left expected impact null.

### Scope only

- `Api/Endpoints/DecisionBoardEndpoints.cs`
- `Api.Tests/DecisionBoardEndpointsTests.cs`
- optional `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`

### Do not touch

- Product Decision Center formula code
- SQL migrations/views
- frontend pages
- action ledger writes

### Do

1. Add tests proving:
   - `REPLENISH`/`BOOST` can show lost-sales expected impact only if PDC supplied it or contract says it is safe.
   - `FIX_DATA` and `INSUFFICIENT_DATA` do not get `LostSalesEstimate` attached as expected impact.
   - `MARKDOWN`/`DO_NOT_ORDER` use slow-stock impact only through `ExpectedImpactRsd`, not lost-sales fallback.
2. Remove or narrow the board-level fallback so board does not override PDC trust semantics.
3. Ensure `impact` section only includes cards with recommendation-aligned expected impact.

### Checks

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "DecisionBoardEndpointsTests"`
- If no build artifacts exist, run `dotnet build Trendplus2.sln --no-restore --configuration Release` and rerun targeted tests.

### Acceptance

- Board no longer upgrades missing expected impact into lost-sales impact for unrelated/blocked recommendations.
- Existing insufficient-data priority cap still passes.
- No Product Decision Center formula change.

### Notes

- 2026-08-04: DONE. Removed board-level `LostSalesEstimate` fallback so product cards trust only `ExpectedImpactRsd` from PDC. Expanded endpoint tests for REPLENISH/BOOST, FIX_DATA/INSUFFICIENT_DATA, and MARKDOWN/DO_NOT_ORDER impact alignment.
- Changed files:
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/DecisionBoardEndpointsTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api/Api.csproj --configuration Release` - pass
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "DecisionBoardEndpointsTests"` - pass (11 tests)
  - `git diff --check` (scoped files) - pass
- Risk:
  - Pre-existing dirty working tree outside RQ01 scope was left untouched.
  - Executive frontend companion fallback remains tracked by RQ72.
- Next:
  - `RQ02 - Product Decision Center summary denominator contract`

---

## RQ02 - Product Decision Center summary denominator contract

Status: DONE
Ready after: RQ01 DONE
Priority: P1
Type: backend/tests/docs
Feature family: product-decision-denominators
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ02-cursor.lock.md`
Commit suggestion: `test(analytics): define pdc summary denominators`

### Why

Product Decision Center count KPIs are based on top-limited returned rows, while money totals are accumulated before top-limiting. The API must clearly state whether each summary field is based on visible rows, all analyzed rows, or ignored rows.

### Scope only

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- Product Decision Center tests
- optional DTO docs/readme update

### Do not touch

- recommendation formulas
- SQL views
- Decision Board ranking

### Do

1. Add tests for top limit behavior with more rows than `top`.
2. Decide if money totals should be:
   - all analyzed rows, with explicit field names/docs, or
   - returned rows only.
3. Avoid changing business totals without before/after notes.

### Checks

- `git diff --check`
- targeted Product Decision Center tests

### Acceptance

- Counts and money totals have explicit denominators.
- `IgnoredRowsCount` meaning is not confused with bad data.

### Notes

- 2026-08-04: DONE. Kept existing numeric split; made denominators explicit with additive fields. Counts stay on returned/top rows; money totals stay on all analyzed rows. `IgnoredRowsCount` is labeled `hidden_by_top_limit`.
- Before/after:
  - BEFORE: same numeric behavior, undocumented mixed denominators.
  - AFTER: unchanged totals/counts; `countDenominatorScope`, `moneyDenominatorScope`, `ignoredRowsMeaning` expose the contract.
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Api.Tests/ProductDecisionCenterSummaryDenominatorTests.cs`
  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "ProductDecisionCenterSummaryDenominatorTests|ProductDecisionCenterBuilderIntegrationTests"` - pass (8 tests)
  - `git diff --check` (scoped) - pass
- Risk:
  - Frontend types do not yet surface the new optional denominator fields; UI can keep showing raw totals until a follow-up labels them.
  - RQ12 can still refine ignored-row UX copy.
- Next:
  - `RQ03 - Lost-sales unavailable vs true zero`

---

## RQ03 - Lost-sales unavailable vs true zero

Status: DONE
Ready after: RQ01 DONE
Priority: P0
Type: backend/tests
Feature family: lost-sales-zero-vs-unknown
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ03-cursor.lock.md`
Commit suggestion: `fix(analytics): separate lost sales unknown from zero`

### Why

Lost-sales validation currently treats `lostSalesEstimate <= 0` as good. Lower-level fallback can return zero when evidence is unavailable. That makes unknown/unavailable look like a clean green zero.

### Scope only

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- validation/lost-sales DTO/tests
- optional `docs/qa/LOST_SALES_VALIDATION_CONTRACT.md`

### Do not touch

- replenishment formula
- Product Decision Center recommendation formulas
- frontend design unless contract requires a tiny compatible field

### Do

1. Add source status for lost-sales evidence:
   - `view`
   - `fallback`
   - `unavailable`
   - `true_zero`
2. Ensure unavailable does not return status `good`.
3. Add tests for unavailable, fallback positive, fallback zero and true view zero.

### Checks

- `git diff --check`
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- targeted validation tests

### Acceptance

- True zero is distinguishable from unknown/unavailable.
- OOS/replenishment trust remains conservative.

### Notes

- 2026-08-04: DONE. Introduced shared `LostSalesSourceStatus` / `LostSalesSnapshot` and `BuildLostSalesValidationFromSnapshot`. Unavailable â†’ `insufficient_data` with null estimate; view zero â†’ `true_zero`/`good`; fallback zero â†’ `warning`.
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Api.Tests/LostSalesValidationSourceStatusTests.cs`
  - `docs/qa/LOST_SALES_VALIDATION_CONTRACT.md`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `Klijent/clientapp/src/types/analytics.ts` (optional `sourceStatus`)
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "LostSalesValidationSourceStatusTests"` - pass (7 tests)
  - `git diff --check` (scoped) - pass
- Risk:
  - UI does not yet surface `sourceStatus` labels; optional TS field is additive only.
  - SQL queue Q80 should reuse this vocabulary (not invent a second model).
- Next:
  - `RQ04 - Data Quality no-revenue/no-data status`

---

## RQ04 - Data Quality no-revenue/no-data status

Status: DONE
Ready after: RQ01 DONE
Priority: P0
Type: backend/tests
Feature family: data-quality-no-data
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ04-cursor.lock.md`
Commit suggestion: `fix(analytics): mark no revenue data quality as insufficient`

### Why

Data Quality health can produce zero percentages when total revenue is zero. That can make a no-data window appear clean rather than insufficient.

### Scope only

- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- `Api/Endpoints/DecisionBoardEndpoints.cs`
- relevant tests

### Do not touch

- supplier-decision SQL
- Product Decision Center formulas
- frontend pages

### Do

1. Add test fixture with zero total revenue and no offenders.
2. Decide expected status: likely `insufficient_data`, not `excellent`/`good`.
3. Add explicit no-data flag/status if needed.
4. Ensure Decision Board does not show data quality health as clean without evidence.

### Checks

- `git diff --check`
- targeted data quality / decision board tests

### Acceptance

- No-revenue windows do not create green health signals.
- Data Quality card distinguishes clean data from no evidence.

### Notes

- 2026-08-04: DONE. Added `HasRevenueEvidence` on the health snapshot. Decision Board evaluation returns `insufficient_data` when there is no revenue evidence, surfaces a blocker card, and emits `no_revenue_evidence`.
- Changed files:
  - `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/AnalyticsDataQualityHealthServiceTests.cs`
  - `Api.Tests/DecisionBoardDataQualityHealthEvaluationTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "AnalyticsDataQualityHealthServiceTests|DecisionBoardDataQualityHealthEvaluationTests"` - pass (11 tests)
  - `git diff --check` (scoped) - pass
- Risk:
  - RQ75 still owns DataQualityPage UI labeling for the same fake-green family.
- Next:
  - `RQ05 - Analytics dataScope consistency audit`

---

## RQ05 - Analytics dataScope consistency audit

Status: DONE
Ready after: RQ01 DONE
Priority: P0
Type: docs/tests
Feature family: analytics-datascope-consistency
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ05-cursor.lock.md`
Commit suggestion: `docs(analytics): audit data scope consistency`

### Why

Different analytics modules apply `dataScope` through article origin, sale header origin, or a mix. This can make imported/existing dashboards inconsistent.

### Scope only

- analytics endpoint/service query builders
- `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`
- focused tests for one or two highest-risk inconsistencies

### Do not touch

- SQL formula weights
- frontend routing
- action write logic

### Do

1. Map each analytics query's dataScope source:
   - article `DataOrigin`
   - sale header `DataOrigin`
   - both
   - not applied
2. Decide canonical rule per metric family.
3. Create follow-up prompts for concrete mismatches.

### Checks

- `git diff --check`
- docs-only unless tests are added

### Acceptance

- `imported`, `existing`, `all` semantics are visible and testable.
- No hidden filter drift remains undocumented.

### Notes

- 2026-08-04: DONE. Docs/tests matrix only; no runtime filter rewrite. Canonical rules proposed (salesâ†’header, quality/inventoryâ†’article). Highest P0 mismatch remains DQ top-offender unscoped `sales_30d`.
- Changed files:
  - `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`
  - `Infrastructure/Services/AnalyticsDataQualityHealthService.cs` (extract `TopOffendersSql` const, no SQL change)
  - `Api.Tests/DataScopeConsistencyContractTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DataScopeConsistencyContractTests"` - pass (2 tests)
  - `git diff --check` (scoped) - pass
- Risk:
  - Dual-origin PDC and inventory forced-all remain documented follow-ups (RQ05-F1/F2), not fixed here.
- Next:
  - `RQ06 - Data Quality top-offender revenue scope correctness`

---

## RQ06 - Data Quality top-offender revenue scope correctness

Status: DONE
Ready after: RQ05 DONE or explicitly unblocked
Priority: P1
Type: backend/tests
Feature family: data-quality-offender-scope
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ06-cursor.lock.md`
Commit suggestion: `fix(analytics): align top offender revenue scope`

### Why

Top offender `sales_30d` currently aggregates sales before applying dataScope at the article quality source. For imported/existing views this can overstate or cross-contaminate revenue impact.

### Scope only

- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- data quality offender tests
- optional runbook doc

### Do not touch

- Decision Board ranking
- Product Decision Center formulas
- supplier-decision SQL

### Do

1. Add tests for imported/existing dataScope where article origin and sale header origin differ.
2. Align offender revenue impact with the canonical dataScope rule from RQ05.
3. Preserve `all` behavior unless intentionally corrected.

### Checks

- `git diff --check`
- targeted data quality tests

### Acceptance

- Top offender revenue impact matches requested scope.
- No silent cross-scope revenue leakage.

### Notes

- 2026-08-04: DONE. `sales_30d` now filters by sale-header `DataOrigin`; article membership stays article-scoped. `all` still includes all headers.
- Changed files:
  - `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
  - `Api.Tests/DataScopeConsistencyContractTests.cs`
  - `Api.Tests/DataQualityPostgresIntegrationTests.cs`
  - `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DataScopeConsistencyContractTests|DataQualityPostgresIntegrationTests"` - pass (9; Postgres cases may no-op when fixture unavailable)
  - `git diff --check` (scoped) - pass
- Risk:
  - `GetDataQualityIssuesHandler` still has unscoped `sales_30d` (out of RQ06 file scope) â†’ follow-up RQ06-F1.
- Next:
  - `RQ07 - Missing-cost offender drilldown`

---

## RQ07 - Missing-cost offender drilldown

Status: DONE
Ready after: RQ04 DONE
Priority: P1
Type: backend/API-contract/tests
Feature family: missing-cost-offenders
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ07-cursor.lock.md`
Commit suggestion: `feat(analytics): add missing cost offender contract`

### Why

Health snapshot tracks missing-cost revenue, but top offenders do not support `missingCost`; unknown issue types silently default to missing supplier. Operators need exact products causing missing-cost risk.

### Scope only

- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- `Api/Endpoints/DataQualityEndpoints.cs`
- data quality tests/docs

### Do not touch

- supplier decision scoring
- frontend redesign
- unrelated issue types

### Do

1. Add or document `missingCost` as an issue type.
2. Stop silently defaulting unknown issue types to missing supplier, or document backward-compatible validation behavior.
3. Add tests that `missingCost` returns products with missing line/article cost evidence.

### Checks

- `git diff --check`
- targeted data quality tests

### Acceptance

- Missing-cost health signal has drilldown.
- Invalid issue type does not silently return wrong offender category.

### Notes

- 2026-08-04: DONE. Top offenders support `missingCost` via article `NabavnaCena` null/â‰¤0 (`is_missing_cost`), independent of supplier CASE. Unknown issue types â†’ API 400 / service `ArgumentOutOfRangeException` (no silent supplier fallback). Issues-list `Normalize` still defaults unknownâ†’missingSupplier (handler not rewritten).
- Changed files:
  - `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
  - `Application/Analytics/Queries/GetDataQualityIssues/GetDataQualityIssuesQuery.cs`
  - `Api/Endpoints/DataQualityEndpoints.cs`
  - `Api.Tests/DataQualityMissingCostOffenderContractTests.cs`
  - `Api.Tests/DataQualityPostgresIntegrationTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DataQualityMissingCostOffenderContractTests|DataScopeConsistencyContractTests|DataQualityPostgresIntegrationTests"` - pass (21)
  - `git diff --check` (scoped) - pass
- Risk:
  - Issues list / frontend tabs still lack missingCost workflow (audit R80 residual); cost evidence is article-level nabavna, not line-level ps.NabavnaCena.
- Next:
  - `RQ08 - Blocked supplier signal ranking in Decision Board`

---

## RQ08 - Blocked supplier signal ranking in Decision Board

Status: DONE
Ready after: RQ01 DONE; SQL queue Q69 evidence available if needed
Priority: P1
Type: backend/tests
Feature family: supplier-blocked-signal-ranking
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ08-cursor.lock.md`
Commit suggestion: `fix(analytics): cap blocked supplier board cards`

### Why

Decision Board adds a blocker when supplier recommendation is not allowed, but still builds supplier cards that can rank high from revenue/confidence. Blocked signal must not look like an actionable supplier decision.

### Scope only

- `Api/Endpoints/DecisionBoardEndpoints.cs`
- `Api.Tests/DecisionBoardEndpointsTests.cs`

### Do not touch

- supplier-decision SQL
- supplier report UI
- action ledger writes

### Do

1. Add tests where `RecommendationAllowed=false` and supplier has high revenue/confidence.
2. Ensure supplier cards are either:
   - capped like insufficient data,
   - explicitly `signal_check`, or
   - only shown under blockers/verification section.
3. Keep blocker card behavior.

### Checks

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "DecisionBoardEndpointsTests"`

### Acceptance

- Blocked supplier signals cannot appear as ordinary high-confidence decisions.
- UI can still guide operator to verify supplier dataset.

### Notes

- 2026-08-04: DONE. When `RecommendationAllowed=false`, supplier cards are labeled `signal_check` / `insufficient_data`, priority capped â‰¤40, ImpactScore=0, excluded from `urgent` and `impact`; remain in `supplierRisk` for verification. Trust blocker card kept.
- Changed files:
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/DecisionBoardEndpointsTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DecisionBoardEndpointsTests"` - pass (13)
  - `git diff --check` (scoped) - pass
- Risk:
  - Blocked supplier cards still visible in `supplierRisk` (intentional verification path); frontend must respect `insufficient_data` / warning codes.
- Next:
  - `RQ09 - Analytics actions empty-state contract`

---

## RQ09 - Analytics actions empty-state contract

Status: DONE
Ready after: RQ01 DONE
Priority: P2
Type: backend-contract/tests
Feature family: action-source-empty-state
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ09-cursor.lock.md`
Commit suggestion: `fix(analytics): treat empty actions as healthy source state`

### Why

Decision Board marks `analytics-actions` as `insufficient_data` when there are no actions. That may be wrong: zero open actions can be healthy empty state.

### Scope only

- `Api/Endpoints/DecisionBoardEndpoints.cs`
- tests/docs

### Do not touch

- action item service writes
- action outcome calculations

### Do

1. Decide empty action list semantics:
   - healthy empty
   - insufficient only if action service failed
   - warning if expected actions are missing
2. Add tests for empty actions vs service unavailable warnings.

### Checks

- `git diff --check`
- targeted Decision Board tests

### Acceptance

- No-actions is not automatically treated as bad data unless contract says so.

### Notes

- 2026-08-04: DONE. Contract: empty successful load â†’ `good` (no `no_actions` warning); `analytics_actions_unavailable` â†’ `insufficient_data`. "Expected actions missing" not auto-warned (would need cross-source expectation; left as future).
- Changed files:
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/DecisionBoardEndpointsTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DecisionBoardEndpointsTests|DecisionBoardAggregationContractTests"` - pass (23)
  - `git diff --check` (scoped) - pass
- Risk:
  - UI that treated `no_actions` / empty as red DQ may need to switch to source Message; cross-signal "expected actions" still not detected.
- Next:
  - `RQ10 - Inventory evidence confidence contract`

---

## RQ10 - Inventory evidence confidence contract

Status: DONE
Ready after: RQ01 DONE
Priority: P2
Type: docs/backend-contract
Feature family: inventory-evidence-confidence
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ10-cursor.lock.md`
Commit suggestion: `docs(analytics): define inventory evidence confidence`

### Why

Inventory cards derive confidence mostly from workflow status. Evidence quality should ideally account for velocity, stock freshness, movement history and calculation source.

### Scope only

- `Api/Endpoints/DecisionBoardEndpoints.cs`
- inventory DTO/service docs/tests if needed
- optional `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`

### Do not touch

- inventory action algorithm unless a separate prompt is created
- SQL formulas

### Do

1. Document current confidence mapping.
2. Identify missing evidence fields needed for better confidence.
3. Add a follow-up prompt if DTO/service changes are needed.

### Checks

- `git diff --check`
- docs-only unless tiny tests are added

### Acceptance

- Inventory confidence is not presented as stronger than its evidence source.

### Notes

- 2026-08-04: DONE. Documented contract; capped board confidence so workflow status never maps to medium/high; warning `confidence_workflow_status_only`; ConfidenceScore stays null. Follow-up RQ13 for DTO evidence wiring.
- Changed files:
  - `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/DecisionBoardEndpointsTests.cs`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DecisionBoardEndpointsTests"` - pass
  - `git diff --check` (scoped) - pass
- Risk:
  - Approved inventory cards now show `low` instead of `medium` (intentional honesty); evidence-grade confidence still unavailable until RQ13.
- Next:
  - `RQ11 - Transaction item/line/unit semantics`

---

## RQ11 - Transaction item/line/unit semantics

Status: DONE
Ready after: RQ01 DONE
Priority: P2
Type: backend-contract/tests
Feature family: transaction-stat-semantics
Parallel-safe: yes
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ11-cursor.lock.md`
Commit suggestion: `fix(analytics): clarify transaction stats line vs unit semantics`

### Why

`AvgItemsPerTransaction` uses sale-line count, not sum of quantities. If the UI means units/items bought, this is inaccurate; if it means lines, the label should say lines.

### Scope only

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- relevant DTO/test/docs

### Do not touch

- sales summary formulas
- frontend dashboard except label if explicitly required

### Do

1. Add fixture with one transaction, two lines and quantity > 1.
2. Decide whether metric is average lines or average units.
3. Rename/adjust field or add explicit second metric if needed.

### Checks

- `git diff --check`
- targeted cached analytics tests

### Acceptance

- Transaction statistic label matches actual calculation.

### Notes

- 2026-08-05: DONE. Contract: `avgItemsPerTransaction` = sale **lines** per receipt (matches UI *Stavki po transakciji*); added `avgUnitsPerTransaction` for sold units. Fixture test proves divergence when qty > 1.
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
  - `docs/qa/TRANSACTION_STATS_SEMANTICS_CONTRACT.md`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `Klijent/clientapp/src/types/analytics.ts`
  - `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx` (infoTip only)
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "TransactionStats_DistinguishesAverageLinesFromAverageUnits"` - pass
  - `git diff --check` (scoped) - pass
- Risk:
  - Legacy `Program.cs` transaction-stats endpoint still line-count only (documented out of scope).
- Next:
  - `RQ12 - Product Decision Center ignored/top rows contract`

---

## RQ12 - Product Decision Center ignored/top rows contract

Status: DONE
Ready after: RQ02 DONE
Priority: P2
Type: backend-contract/tests
Feature family: pdc-ignored-rows-contract
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ12-cursor.lock.md`
Commit suggestion: `docs(analytics): lock pdc ignored rows contract`

### Why

`IgnoredRowsCount` currently means rows hidden by top limit, not necessarily invalid or ignored for data quality. Operators may misread it as bad data.

### Scope only

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- DTO docs/tests

### Do not touch

- recommendation scoring
- SQL migrations

### Do

1. Test `TotalRows`, `AnalyzedRows`, `IgnoredRowsCount` with top limit.
2. Rename/add metadata if needed to separate hidden-by-limit from ignored-because-invalid.
3. Keep backward compatibility unless explicitly approved.

### Checks

- `git diff --check`
- targeted PDC tests

### Acceptance

- Hidden top-limit rows are not confused with unreliable/invalid data.

### Notes

- 2026-08-05: DONE. Backend contract from RQ02 confirmed and documented; added `PDC_IGNORED_ROWS_CONTRACT.md`, focused contract tests (3-product top=2 fixture), TS denominator types. No numeric behavior change.
- Changed files:
  - `docs/qa/PDC_IGNORED_ROWS_CONTRACT.md`
  - `Api.Tests/ProductDecisionCenterIgnoredRowsContractTests.cs`
  - `Klijent/clientapp/src/types/analytics.ts`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "ProductDecisionCenterIgnoredRows|ProductDecisionCenterSummaryDenominator|ProductDecisionCenterBuilderIntegration"` - pass (11)
  - `git diff --check` (scoped) - pass
- Risk:
  - PDC UI still labels `totalRows` without surfacing `ignoredRowsMeaning`; operators should read contract before comparing to DQ intake â€œignorisani redoviâ€.
- Next:
  - `RQ13 - Wire inventory signal evidence onto Decision Board cards`

---

## RQ13 - Wire inventory signal evidence onto Decision Board cards

Status: DONE
Ready after: RQ10 DONE
Priority: P2
Type: backend/DTO
Feature family: inventory-evidence-wiring
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/RQ13-cursor.lock.md`
Commit suggestion: `feat(analytics): wire inventory signal confidence to decision board`

### Why

RQ10 capped board confidence because `InventoryActionSuggestionDto` lacks evidence fields. Operators still need evidence-grade confidence when inventory signals exist.

### Scope only

- `Api/Dtos/InventoryExperienceDtos.cs` / workflow builder
- `Api/Endpoints/DecisionBoardEndpoints.cs`
- tests for mapping from signal evidence when present

### Do not touch

- inventory SQL formulas rewrite
- frontend redesign

### Do

1. Add optional signal confidence / recommendationAllowed / reasonCodes (or join insights by SKU/store).
2. Map board cards from evidence when present; keep `confidence_workflow_status_only` fallback when absent.
3. Tests for evidence-present vs evidence-absent paths.

### Checks

- `git diff --check`
- targeted Decision Board / inventory tests

### Acceptance

- Board inventory confidence can exceed `low` only when signal evidence is present on the card/DTO.

### Notes

- 2026-08-05: DONE. Extended `InventoryActionSuggestionDto` with optional signal fields; workflow builder computes evidence via `ComputeSuggestionSignalEvidence`; board resolver uses evidence path when `SignalConfidencePct` present, workflow fallback otherwise; blocked recommendations cap at `insufficient_data`.
- Changed files:
  - `Api/Dtos/InventoryExperienceDtos.cs`
  - `Api/Endpoints/InventoryEndpoints.cs`
  - `Api/Endpoints/DecisionBoardEndpoints.cs`
  - `Api.Tests/DecisionBoardEndpointsTests.cs`
  - `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `dotnet test ... --filter "DecisionBoardEndpointsTests"` - pass (27)
- Risk:
  - Approved inventory cards with signal evidence may now show `medium`/`high` (intentional when evidence supports it).
- Next:
  - Queue complete; new reliability work requires a new queue entry.

---

## RQ106 - Decision Pulse exception digest

Status: DONE
Ready after: `QDB06` is `DONE` and `RQ96` is `DONE`
Priority: P1
Type: backend/frontend-contract/tests
Feature family: decision-pulse-digest
Parallel-safe: no
Owner: Cursor Auto
Local lock: removed after DONE
Commit suggestion: `feat(analytics): add decision pulse digest`
Promotion note: 2026-08-20 - owner-scheduled after QDB06 and RQ96 both DONE; claimed when no other exclusive READY remained.

### Problem

Operators still have to open analytics screens to learn that a decision, data-quality failure or stale evidence needs action. There is no first-party exception digest that follows an existing decision/metric family with a Why and a deep link.

### Evidence

- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md` ranks exception/digest delivery immediately after source adaptability and observed historical inventory.
- Owner decision 2026-08-18: queue Decision Pulse as WAITING after QDB06 and RQ96; first version is email + in-app feed; no generic DSL or Slack.

### Scope

- in-app Decision Pulse feed plus email for the same events
- events must follow an existing decision or metric family (inventory, product decision, supplier, data quality)
- each item must expose Why, freshness/data-quality, and a deep link to the owning surface
- suppress items whose evidence is stale, empty, or an error-as-zero
- do not add Slack, a generic rule DSL, or a new recommendation scorer

### Read first

- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md`
- current Decision Board / Product Decision / inventory action contracts

### Do

1. Define a bounded event vocabulary owned by existing backend decision/metric families.
2. Persist or project an in-app feed that does not invent recommendations or rates.
3. Send the same events by email without logging row payloads or secrets.
4. Hide or suppress items when evidence is stale, missing, or in error; empty is not an alert.
5. Keep MT dedicated (`n/a_dedicated`); do not use caller headers as tenant authority.

### Tests

- `git diff --check`
- focused backend tests that Pulse items preserve backend status/reason/freshness and do not substitute zero KPIs for errors
- focused UI or contract test that empty/error/stale items are not shown as actionable
- email path does not include secrets or raw customer row payloads

### Acceptance

- An operator can receive a Pulse item with Why + deep link for one existing decision/metric family.
- Stale or failed evidence cannot look like a trusted alert.
- Slack and generic DSL remain out of scope.

### Dependencies

- `QDB06` DONE (owner 2026-08-18)
- `RQ96` DONE so historical inventory evidence can back inventory Pulse items without reconstructed-as-observed confusion
- Do not displace current execution `RQ96`
- Do not start MT02 or shared-SaaS notification routing

### Completion note

- Date: 2026-08-20
- Status: DONE
- Completion: Product Decision exception Pulse with Why + deep link, stale/empty/error suppression, in-app feed and SMTP email path; tenantScope fixed to n/a_dedicated
- Changed files: Application/Analytics/DecisionPulse/DecisionPulseProjector.cs; Application/Analytics/DecisionPulse/DecisionPulseEmailComposer.cs; Api/Services/Analytics/DecisionPulseService.cs; Api/Endpoints/DecisionPulseEndpoints.cs; Api/Program.cs; Api.Tests/DecisionPulseProjectorTests.cs; Klijent/clientapp/src/pages/DecisionPulsePage.tsx; Klijent/clientapp/src/services/decisionPulseApi.ts; Klijent/clientapp/src/pages/__tests__/DecisionPulsePage.spec.tsx; Klijent/clientapp/src/App.tsx; Klijent/clientapp/src/layout/navConfig.ts; docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md; MASTER_ROADMAP.md; .ai/runs/2026-08-20-RQ106-evidence.md
- Contract/runtime behavior changed: yes; new GET/POST `/api/analytics/decision-pulse` and `/analytics/decision-pulse` UI
- Checks run: dotnet test DecisionPulseProjectorTests (4 passed); npm DecisionPulsePage.spec (1 passed); governance validators
- Checks not run: full suites; live SMTP send
- Run log: .ai/runs/2026-08-20-RQ106-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 50236d144d6dd7e668be0601dac9c76c56a3f15e
- Main verification: git rev-parse origin/main -> 50236d144d6dd7e668be0601dac9c76c56a3f15e; work SHA is an ancestor
- Missed: inventory/supplier Pulse families; scheduled worker; durable inbox table
- Follow-up: RQ97 DONE (fail-closed provenance); RQ98 WAITING; SQL Server e2e commercial gate remains owner-routed
- Residual risk: email requires DecisionPulse:Recipients + SMTP enabled; otherwise in-app feed still works
- Prompt defect / scope repair: first slice limited to Product Decision family; RQ96 was already DONE on origin/main by another agent so this run claimed RQ106 instead
- Next: none (RQ Current READY none)

---

## RQ107 - Controlled markdown / replenishment scenario planning contract

Status: DONE
Completed: docs-only precursor promoted on 2026-08-20; runtime scenario work remains gated by trusted forecast materialization plus a measured backtest window
Priority: P2
Type: docs-contract (later runtime)
Feature family: scenario-planning-contract
Parallel-safe: yes, docs/contracts only until later runtime authorization
Owner: unassigned

### Problem

Competitive gap Gate 4 still needs controlled scenario planning (markdown / replenishment what-if). Starting that before trusted forecast materialization and measured backtest would invent scenario outcomes from untrusted forecasts.

### Evidence

- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md` Gate 4
- `docs/qa/FORECAST_BASELINE_BACKTEST_CONTRACT_2026-08-20.md`
- `docs/qa/FORECAST_SNAPSHOT_PROVENANCE_CONTRACT_2026-08-20.md`
- `docs/qa/SCENARIO_PLANNING_CONTRACT_2026-08-20.md`
- `docs/planning/QUEUE_REFILL_2026-08-20.md`

### Owner-gated path

1. Keep `RQ97` and `RQ98` DONE so the queue stays fail-closed on forecast provenance and backtesting.
2. Do not promote runtime scenario work until the forecast writer is proven and the backtest comparison window is available.
3. The docs-only precursor is now complete: freeze only the scenario vocabulary and comparison basis. Do not add simulator logic, optimizer behavior or runtime forecast mutation in that precursor.

### Scope

- docs/contracts only for fixed scenario sets, comparison basis, and no-fake rules;
- no simulator UI, optimizer, or LLM scenarios in this prompt.

### Read first

- RQ98 / RQ97 completion notes
- competitive gap audit Gate 4
- MASTER_ROADMAP.md current READY

### Do

1. Freeze allowed scenario vocabularies (e.g. no-change / fixed markdown / replenishment bands).
2. Require comparison against measured historical behavior, not invented forecast certainty.
3. Keep missing measured windows as unavailable, not zero impact.
4. Do not implement a runtime simulator in this prompt.

### Tests

- missing measured window stays unavailable, not `0` impact;
- docs/queue validators pass when promoted.

### Acceptance

- one citeable scenario-planning contract exists on main;
- RQ Current READY remains single / none as declared.

### Dependencies

- trusted forecast materializer + measured backtest window for runtime follow-up;
- do not promote ahead of higher-priority exclusive RQ work.

---

## RQ108 - Add authoritative forecast materializer and observed pairing foundation

Status: DONE
Ready after: `RQ97` and `RQ98` are `DONE` and an owner authorizes the first runtime forecasting follow-up
Priority: P1
Type: backend/persistence/tests
Feature family: forecast-materializer-observed-window
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ108-<agent>.lock.md`
Commit suggestion: `feat(analytics): materialize forecasts for measured comparison`
Promotion note: 2026-08-20 - owner-promoted from the pilot audit because forecast provenance/backtest contracts are done but no authoritative runtime writer or paired observed window exists yet.

### Problem

`RQ97` and `RQ98` deliberately closed the forecast surface in a fail-closed way, but Trendplus still has no authoritative runtime writer that materializes forecast snapshots and later pairs them to observed evidence. Without that foundation, backtesting, scorecards and scenario planning remain contracts only.

### Evidence

- `RQ97` froze snapshot provenance and made missing materialization explicit instead of inventing trust.
- `RQ98` added a fail-closed baseline/backtest contract, but documented that the paired forecast-vs-observed window is still unavailable.
- `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-20.md` keeps the core pilot conservative and lists inventory/forecast fail-closed paths as a minimum smoke area.
- The 2026-08-20 audit concluded that the product still lacks:
  - a trusted forecast materializer;
  - a paired observed outcome window;
  - measured WAPE/bias/MAE proof on runtime-produced snapshots.

### Scope

- forecast snapshot persistence/materialization files under the existing inventory forecast owner path
- the observed daily inventory/sales pairing path introduced by `RQ96`
- fail-closed forecast DTO/API surfaces only where needed to expose authoritative pairing state
- focused backend tests for materialization, pairing and unavailable-window behavior
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- one dated `docs/qa/` or durable `.ai/runs/...` evidence note for the runtime follow-up

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md` (`RQ96`-`RQ98`)
- `docs/qa/FORECAST_SNAPSHOT_PROVENANCE_CONTRACT_2026-08-20.md`
- `docs/qa/FORECAST_BASELINE_BACKTEST_CONTRACT_2026-08-20.md`
- current inventory forecast query/handler files
- `RQ96` completion evidence for observed daily snapshot behavior

### Do

1. Add the smallest authoritative forecast snapshot writer/materializer that can persist a forecast snapshot together with its provenance and issue time.
2. Pair persisted forecast snapshots only to observed evidence that satisfies the canonical RQ96 daily snapshot basis; do not reconstruct observed truth from later live views.
3. Keep missing or insufficient observed windows explicit as unavailable, not zero error and not a healthy score.
4. Expose additive runtime fields only where needed so later scorecard work can consume authoritative pairing state.
5. Do not implement scenario simulation, optimizer behavior, or frontend scorecard UX in this prompt.

### Tests

- `git diff --check`
- focused backend tests for:
  - forecast snapshot materialization with provenance preserved
  - observed pairing on a deterministic historical window
  - missing observed window -> unavailable / fail-closed
  - stale or mismatched forecast basis -> unavailable / fail-closed
- nearest focused full forecast test command for the touched area

### Acceptance

- Trendplus can persist an authoritative forecast snapshot and later pair it to the correct observed window.
- Missing observed evidence remains unavailable rather than fake-measured.
- Later measured-scorecard work has a real runtime foundation instead of only contract prose.
- The prompt does not invent scenario outputs or a frontend scorecard.

### Dependencies

- `RQ96` DONE.
- `RQ97` DONE.
- `RQ98` DONE.
- Do not weaken the fail-closed contract from `RQ97`/`RQ98` while adding the writer/pairing foundation.

### Completion note

- Date: 2026-08-22
- Status: DONE
- Completion: added an authoritative inventory forecast snapshot materializer with persisted issue-time/provenance metadata, a fail-closed observed-pairing view foundation, trusted provenance surfacing in the forecast read handler, and focused tests proving upsert plus observed pairing; the implementation is now synchronized on current main
- Changed files: Application/Analytics/Queries/DbDataReaderNullableExtensions.cs; Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs; Application/Analytics/Queries/GetInventoryForecast/InventoryForecastSnapshotProvenance.cs; Application/Analytics/Queries/GetInventoryForecast/InventoryForecastMaterializationContracts.cs; Application/Common/Interfaces/IInventoryForecastSnapshotMaterializerService.cs; Infrastructure/Services/Inventory/InventoryForecastSnapshotMaterializerService.cs; Api.Tests/InventorySnapshotContractTests.cs; Api.Tests/DatabaseInitializerP0IntegrationTests.cs; docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md; MASTER_ROADMAP.md; .ai/runs/2026-08-22-RQ108-evidence.md
- Contract/runtime behavior changed: authoritative forecast snapshot persistence and observed pairing now ship on current main; missing observed evidence still fails closed as unavailable
- Checks run: `git diff --check` - pass; `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Trendplus2.Tests.InventorySnapshotContractTests|FullyQualifiedName~Api.Tests.DatabaseInitializerP0IntegrationTests.ForecastMaterializer_PersistsTrustedSnapshot_AndPairsObservedEvidence"` - pass; `node scripts/check-agent-instructions.mjs --self-test` - pass; `node scripts/check-agent-instructions.mjs` - pass; `node scripts/check-prompt-queues.mjs --self-test` - pass; `node scripts/check-prompt-queues.mjs` - pass; `node scripts/check-planning-architecture.mjs --self-test` - pass; `node scripts/check-planning-architecture.mjs` - pass
- Checks not run: full Release suite - not needed after the targeted materialization/pairing evidence pass; remote workflow re-run - not needed because current main verification is on the pushed delivery SHA
- Run log: `.ai/runs/2026-08-22-RQ108-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `908afeef7a76795280c8e15387454ac33dd2ada4`
- Main verification: `git rev-parse origin/main -> 908afeef7a76795280c8e15387454ac33dd2ada4`
- Missed: none known
- Follow-up: `RQ109` remains WAITING until owner promotion
- Residual risk: pairing still depends on the RQ96 observed daily stock foundation; if that foundation is absent, paired evidence remains unavailable rather than invented
- Next: `RQ109`
- Prompt defect / scope repair: same-owner runtime foundation repair for forecast materialization and observed pairing

---

## RQ109 - Expand Decision Pulse to inventory, supplier and durable delivery

Status: DONE
Ready after: `RQ108` is `DONE` and the first authoritative forecast/observed pairing surface exists
Priority: P1
Type: backend/frontend-delivery/tests
Feature family: decision-pulse-expansion
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ109-<agent>.lock.md`
Commit suggestion: `feat(analytics): expand decision pulse coverage`

### Problem

`RQ106` delivered the first Product Decision Pulse slice, but the audit showed that inventory and supplier families are still missing and there is no scheduler, durable inbox table or live delivery proof. Without a bounded follow-up prompt, Pulse can look more complete than it really is.

### Evidence

- `RQ106` completion note explicitly missed inventory/supplier families, a scheduled worker, a durable inbox table and live SMTP proof.
- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md` ranks exception/digest delivery as a core near-term differentiator.
- The 2026-08-20 audit confirmed that the current Pulse is still a first slice rather than a complete operator-delivery surface.

### Scope

- existing Decision Pulse projector/service/email files
- additive persistence/delivery files needed for a durable inbox or scheduled projection
- inventory/supplier deep-link/status/freshness wiring
- focused backend/frontend tests for suppression, scheduling and family coverage
- no Slack, no generic DSL, no MT/shared-SaaS routing

### Read first

- `RQ106` completion note
- Decision Pulse backend/frontend files landed by `RQ106`
- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md`

### Do

1. Add inventory and supplier Pulse family coverage only when each item can reuse existing backend truth, Why, freshness and deep-link semantics.
2. Add the smallest durable inbox/scheduler path needed to make Pulse delivery repeatable.
3. Prove live send or an equally authoritative delivery path without logging secrets or row payloads.
4. Keep stale/empty/error suppression and `n/a_dedicated` tenant scope rules from `RQ106`.

### Tests

- focused Pulse projector tests for inventory/supplier families
- durable inbox/scheduler tests
- frontend Pulse feed tests only where new family branches are added
- live delivery proof or explicit blocker evidence

### Acceptance

- Decision Pulse covers more than Product Decision without inventing a second recommendation source.
- Delivery is durable/repeatable instead of purely ad hoc.
- Missing SMTP or scheduling proof remains explicit, not implied.

### Dependencies

- `RQ106` DONE.
- `RQ108` DONE first so inventory/forecast Pulse items can rely on authoritative runtime pairing rather than contract-only forecast truth.

### Completion note

- Date: 2026-08-22
- Status: DONE
- Completion: expanded Decision Pulse to inventory and supplier families, added a durable scheduled delivery path, verified the implementation with focused build/test checks, and synchronized the implementation to current main
- Changed files: `Api/Services/Analytics/DecisionPulseService.cs`, `Api/Services/Analytics/DecisionPulseDeliveryService.cs`, `Api/Workers/DecisionPulseSchedulerWorker.cs`, `Api/Endpoints/DecisionPulseEndpoints.cs`, `Api/Program.cs`, `Api/Config/WorkerRuntimeConfig.cs`, `Api/Services/WorkerRegistryService.cs`, `Api.Tests/DecisionPulseProjectorTests.cs`, `Application/Analytics/DecisionPulse/DecisionPulseAutomationContracts.cs`, `Application/Analytics/DecisionPulse/DecisionPulseEmailComposer.cs`, `Application/Analytics/DecisionPulse/DecisionPulseProjector.cs`, `Application/Common/Interfaces/IDecisionPulseScheduleService.cs`, `Infrastructure/Properties/AssemblyInfo.cs`, `Infrastructure/Services/Analytics/DecisionPulseScheduleService.cs`, `Infrastructure/Services/WorkerRegistryCatalog.cs`, `MASTER_ROADMAP.md`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- Contract/runtime behavior changed: Decision Pulse now includes inventory and supplier items, email output shows the source family, and scheduler-backed delivery is available via the new pulse schedule table/worker
- Checks run: `dotnet build Api/Api.csproj --no-restore --configuration Release` (pass), `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "DecisionPulseProjectorTests"` (pass), `git diff --check` (pass)
- Checks not run: live SMTP send, full solution test suite
- Run log: `.ai/runs/2026-08-22-RQ109-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `54a29409efd842da438de99c890f5ecb3054cbc3`
- Main verification: `git merge-base --is-ancestor 54a29409efd842da438de99c890f5ecb3054cbc3 origin/main -> ancestor=true`
- Missed: no live external email proof yet
- Follow-up: none for this prompt; RQ110 remains the next queued analytics reliability prompt
- Residual risk: scheduler delivery still depends on runtime SMTP/configuration
- Next: `RQ110`
- Prompt defect / scope repair: none; the queue prompt was mechanically promotable after confirming the dependency gate

---

## RQ110 - Prove pilot analytics screens stay non-empty when authoritative seeded data exists

Status: DONE
Ready after: `RQ108` is `DONE` and the owner-supplied canonical production data-bearing route/filter matrix exists (`docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md` + `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-22_STAB15.md`)
Priority: P1
Type: docs/tests/backend-contract
Feature family: analytics-screen-data-availability
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ110-<agent>.lock.md`
Commit suggestion: `test(analytics): prove non-empty pilot screen data availability`

### Problem

Trendplus should not show a blank chart, blank table, or misleading empty state on a pilot analytics screen when authoritative data already exists in the database for that screen's requested period/scope. Today this risk is spread across refresh ownership, cache identity, filter lineage, route smoke, and screen-specific endpoint behavior, but there is no single executable proof matrix for the main pilot surfaces.

### Evidence

- User requirement 2026-08-20: maximize analytics data reliability and avoid blank tables/charts when the database already contains data.
- `docs/qa/ANALYTICS_BACKEND_TEST_COVERAGE_PHASE2_2026-07-02.md` already calls out screen cache identity, explicit empty-success metadata, and inventory list coverage, but not one cross-screen authoritative matrix.
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` shows that some families can lag after aggregation/data-quality refresh even when underlying data has already changed.
- `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md` historically captured shell-only route mismatches and route-level failures that can look like "no data" from the operator perspective.
- Current release evidence remains conservative (`docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-20.md`): the pilot is not ready until fresh exact-SHA route/smoke truth exists.
- Owner-supplied route/filter coverage exists in `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-22_STAB15.md` and `docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md`.

### Scope

- one new `docs/qa/` or architecture-style matrix for the main pilot analytics screens:
  - dashboard
  - product decision center
  - executive decision board
  - inventory
  - supplier decision/sales
  - analytics actions
- focused backend contract tests for those screen families only where seeded non-empty proof is missing
- the nearest existing backend test hosts for the named screens
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`

### Read first

- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/qa/ANALYTICS_BACKEND_TEST_COVERAGE_PHASE2_2026-07-02.md`
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md`
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`
- `Api.Tests/DecisionBoardEndpointsTests.cs`
- `Api.Tests/InventoryListEndpointIntegrationTests.cs`
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`

### Do

1. Create a single matrix that names, for each main pilot analytics screen:
   - authoritative source tables/views/materialized views;
   - refresh owner;
   - canonical period/scope filters;
   - allowed successful-empty reasons;
   - one deterministic seeded non-empty fixture/query basis.
2. Add the smallest focused backend proofs that when the authoritative seeded basis exists, the corresponding API does one of only two things:
   - returns non-empty rows/series/cards; or
   - returns an explicit blocked/warning/empty reason that explains why the screen cannot trustfully show data.
3. Do not treat route-shell fallback, stale cache, or unknown refresh state as a successful empty dataset.
4. If a screen family fails the new proof, classify the failure into:
   - source/refresh ownership gap;
   - filter lineage/scope bug;
   - cache identity/invalidation bug;
   - route/render mismatch;
   - test harness gap.
5. Keep this prompt at matrix/proof level. Create or refine the runtime repair prompt from the proven failure family instead of broadening this prompt silently.

### Tests

- `git diff --check`
- focused `dotnet test` commands for the touched screen-family test hosts
- governance validators if queue docs change

### Acceptance

- There is one citeable pilot analytics screen-data availability matrix.
- Each named pilot screen has a deterministic seeded proof that authoritative data does not silently collapse into a blank screen or fake empty success.
- Allowed empty states remain explicit and distinguishable from missing/blocked data.
- Any reproduced runtime gap is classified tightly enough to feed the next owner prompt.

### Dependencies

- `RQ108` DONE first.
- Do not fix broad refresh/cache/runtime behavior inside this prompt unless one smallest same-owner repair is required to make the proof executable and is recorded as such.

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: established the citable pilot screen-data availability matrix, fixed the browser request-timeout contract, and split the dashboard-isolation gap into `RQ115` instead of broadening `RQ110`.
- Changed files:
  - `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
  - `MASTER_ROADMAP.md`
  - `Klijent/clientapp/src/utils/fetchWithTimeout.ts`
  - `Klijent/clientapp/src/utils/__tests__/fetchWithTimeout.spec.ts`
- Contract/runtime behavior changed: dashboard/bootstrap proof is now citable at route/meta/smoke level, and fetch timeout abort behavior now matches the repo contract.
- Checks run: `git diff --check`; `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`; `npm run test:run -- src/utils/__tests__/fetchWithTimeout.spec.ts`; `npm run typecheck`
- Checks not run: backend runtime tests; live SMTP send
- Run log: `.ai/runs/2026-08-22-RQ110-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `0794cfc61250c23d3377b0c8670c830b21d32152`
- Main verification: `git merge-base --is-ancestor 0794cfc61250c23d3377b0c8670c830b21d32152 origin/main -> ancestor=true`
- Missed: dashboard family still lacks a separately named isolated seeded-non-empty backend proof
- Follow-up: `RQ115`
- Residual risk: some surfaces still lean on route/meta/smoke proof rather than a named physical source on every row
- Next: `RQ111`
- Prompt defect / scope repair: split the dashboard gap into `RQ115` rather than broadening `RQ110` further

---

## RQ111 - Close refresh/cache/materialized-view gaps that can hide existing data

Status: DONE
Ready after: `RQ110` is `DONE`
Priority: P1
Type: backend/workers/cache/tests
Feature family: analytics-refresh-cache-parity
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ111-codex.lock.md`
Commit suggestion: `fix(analytics): preserve screen data after refresh and cache churn`

### Problem

Even when authoritative data exists, a pilot analytics screen can still look empty or stale if refresh ownership, materialized-view readiness, or cache invalidation is incomplete. The product must not lose visible screen data behind a stale empty cache entry, a refresh family that was not invalidated, or an unlabelled materialized-view lag.

### Evidence

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` documents remaining follow-up risk for:
  - supplier summary surfaces after aggregation-worker refresh;
  - report-family regeneration/version rotation;
  - dashboard/product/supplier/inventory trust surfaces after data-quality recalculation.
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md` and `docs/roadmaps/BUSINESS_ROADMAP.md` require refresh/freshness truth to stay visible rather than inferred from page render time.
- `RQ110` is intended to classify which pilot screen families still collapse into blank or stale states despite an authoritative seeded basis.

### Scope

- `AnalyticsAggregationWorker`, `NightlyAnalyticsRefreshWorker`, `AnalyticsDataQualityHealthWorker`, and the nearest cache invalidation helpers they use
- screen-family endpoint/meta code only where refresh/materialized-view readiness must be exposed truthfully
- focused worker/cache/endpoint tests
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

### Read first

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
- `docs/roadmaps/BUSINESS_ROADMAP.md`
- the `RQ110` matrix/proof output
- nearest worker/cache tests for the affected family

### Do

1. Use the `RQ110` output to pick the smallest proven refresh/cache/materialized-view failure family.
2. Ensure a successful refresh or worker completion invalidates or refreshes the minimum required screen-family caches so existing data becomes visible without waiting for misleading TTL behavior.
3. If a screen depends on a materialized view that is not current, expose that as explicit freshness/warning state instead of returning a trusted-looking blank result.
4. Preserve successful empty semantics for truly empty datasets; do not turn real empty into fake "data exists" or vice versa.
5. Add focused regression tests for:
   - successful refresh -> screen family no longer serves stale empty data;
   - failed refresh -> cache/data remains clearly stale/blocked, not healthy;
   - materialized-view lag -> visible warning/degraded truth rather than silent blankness.

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: proved the dashboard bootstrap family rebuilds fresh summary values after cache invalidation and a new authoritative sale lands in the active date window.
- Checks run: `node scripts/check-agent-instructions.mjs --self-test`; `node scripts/check-agent-instructions.mjs`; `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`; `git diff --check`; `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~CachedAnalyticsOperationalFallbackTests|FullyQualifiedName~AnalyticsAggregationWorkerTests"`; `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~CachedAnalyticsOperationalFallbackTests.DashboardBootstrap_AfterRefreshInvalidation_RebuildsFreshSummary"`
- Checks not run: full-solution build/test; live refresh smoke; production deployment proof
- Run log: `.ai/runs/2026-08-24-RQ111-evidence.md`
- Changed files:
  - `Api.Tests/CachedAnalyticsOperationalFallbackTests.cs`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
  - `MASTER_ROADMAP.md`
  - `.ai/runs/2026-08-24-RQ111-evidence.md`
- Main commit SHA: `11610dc2e27bbd486eeb27d797bc1a5d0151ab18`
- Main verification: `git merge-base --is-ancestor 11610dc2e27bbd486eeb27d797bc1a5d0151ab18 origin/main -> ancestor=true`
- Missed: no additional miss inside the RQ111 acceptance scope; `RQ112` remains the next queued follow-up
- Evidence state: synchronized
- Delivery mode: main delivered
- Follow-up: `RQ112`
- Residual risk: the refreshed dashboard proof is still focused on the first proven family; other families remain staged behind their own prompts

### Tests

- `git diff --check`
- focused worker/cache invalidation tests
- focused endpoint/meta contract tests for the affected screen family
- governance validators if queue docs change

### Acceptance

- The first proven refresh/cache/materialized-view gap that can hide existing data is closed.
- A named analytics screen family no longer returns a stale empty/trusted-looking blank state after successful refresh when the authoritative source contains data.
- Failed or lagging refresh remains visible as warning/degraded truth.

### Dependencies

- `RQ110` DONE.
- Do not broaden into a repo-wide performance or telemetry program; keep the fix inside the first proven reliability family.

---

## RQ112 - Reconcile pilot analytics summary values against detail/export on the first proven family

Status: IN_PROGRESS
Ready after: `RQ111` is `DONE`
Priority: P1
Type: backend/tests/docs
Feature family: analytics-summary-detail-reconciliation
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ112-<agent>.lock.md`
Commit suggestion: `test(analytics): reconcile pilot summary and detail truth`

### Problem

After `RQ110` and `RQ111`, a pilot analytics screen may be non-empty and freshly refreshed yet still numerically misleading if its summary cards, table rows, chart totals, and export values do not reconcile for the same authoritative seeded basis. Trendplus needs one current-main proof that the first proven family with both summary and detail surfaces is either numerically aligned or explicitly labeled when denominators differ.

### Evidence

- `RQ110` proves whether a screen can stay data-bearing when authoritative seeded data exists, but it does not by itself prove that screen-level summaries reconcile to the underlying detail surface.
- `RQ111` closes the first refresh/cache/materialized-view family that can hide existing data, but it still does not prove that the now-visible numbers match one another.
- Earlier prompt families closed isolated correctness gaps such as mixed denominators (`RQ02`, `RQ12`, `RQ83`) and cross-surface numeric drift (`RQ40`, `RQ55`), but not one current-main pilot-family proof that summary, detail, and export use the same defended semantics.
- Pilot analytics trust depends not only on visible data, but on the operator being able to defend why the headline number matches the underlying drilldown or why it intentionally does not.

### Scope

- the first pilot screen family identified by `RQ110`/`RQ111` that has:
  - a summary/header/KPI surface; and
  - a table/detail and/or export surface
- the nearest query/endpoint/DTO/test files for that single family only
- one dated `docs/qa/` reconciliation note or a scoped extension to `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

### Read first

- the final `RQ110` matrix/proof output
- the final `RQ111` runtime-gap output
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md`
- the nearest summary/detail/export tests for the chosen family
- the chosen family's endpoint/query files

### Do

1. Use the `RQ110` and `RQ111` outputs to choose the smallest current-main pilot family where summary and detail/export can both be proven from the same seeded basis.
2. Add one deterministic reconciliation fixture that names:
   - authoritative source rows or snapshot basis;
   - requested/effective period and scope;
   - expected summary values;
   - expected detail/export values;
   - any allowed intentional denominator or coverage difference.
3. Make summary, detail, chart, and export semantics align for that family, or add additive metadata that explains the intentional difference without silently changing business meaning.
4. Do not let dropped rows, hidden unknown buckets, stale cached totals, or unit conversions create a trusted-looking headline number that the underlying surface cannot defend.
5. Keep the fix inside one family; if a second family shows the same failure, record it as follow-up evidence rather than broadening this prompt silently.

### Tests

- `git diff --check`
- focused backend tests for the chosen summary/detail family
- focused export/detail parity tests if an export surface is touched
- governance validators if queue docs change

### Acceptance

- One current-main pilot family has a deterministic reconciliation proof from authoritative seeded basis to summary and detail/export output.
- Summary values no longer overstate, understate, or silently redefine the same dataset relative to the underlying surface.
- Any intentional denominator or coverage split is explicit in contract metadata or proof documentation.

### Dependencies

- `RQ110` DONE.
- `RQ111` DONE.
- Do not broaden into multi-family audit work; prove the first family completely.

---

## RQ113 - Expose exact freshness/provenance truth for the first pilot family that still looks trusted by inference

Status: WAITING
Ready after: `RQ112` is `DONE`
Priority: P1
Type: backend/frontend-contract/tests
Feature family: analytics-generation-provenance-truth
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ113-<agent>.lock.md`
Commit suggestion: `fix(analytics): expose pilot generation provenance truth`

### Problem

Even when a pilot analytics family is non-empty and numerically reconciled, it can still be weakly evidenced if the operator cannot tell which refresh/materialized-view generation produced it, whether it came from a fallback path, or whether the visible period/scope is requested truth or effective fallback. Trendplus should not require an operator to infer freshness or provenance from page render time, cache age, or a single borrowed timestamp.

### Evidence

- `RQ111` is intended to close the first refresh/cache/materialized-view gap, but its acceptance is about preventing hidden stale empty states, not standardizing family-level provenance truth.
- `RQ61` and `RQ105` already fixed surface-specific freshness/fallback honesty issues, yet they do not provide one current-main contract that ties visible pilot numbers to a named generation/provenance basis.
- The `RQ110` matrix makes source tables/views and refresh owners explicit, which creates the evidence foundation for a stricter provenance contract.
- Pilot release truth still depends on being able to explain not only what number is shown, but which owned refresh/basis generated it and whether fallback or degradation was involved.

### Scope

- the first pilot family from `RQ110`/`RQ111`/`RQ112` whose visible trust still depends on inferred freshness or provenance
- nearest endpoint/meta DTO files and only the minimum frontend mapping needed to surface truthful additive metadata
- focused endpoint/meta contract tests and small UI assertions only when a visible trust state changes
- one dated `docs/qa/` provenance note if a current owner doc does not already capture the new contract
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

### Read first

- final outputs from `RQ110`, `RQ111`, and `RQ112`
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md`
- the chosen family's endpoint/meta/frontend trust files
- nearest freshness/fallback tests already covering that family

### Do

1. Choose the smallest pilot family whose current-main trust still depends on inferred freshness, inferred fallback, or inferred effective period/scope.
2. Add only the minimum additive contract fields needed to expose:
   - requested period/scope;
   - effective period/scope when fallback occurs;
   - refresh/materialized-view generation or equivalent provenance basis;
   - fallback/degraded/operational path state.
3. Ensure the surface does not borrow freshness or provenance from an unrelated panel or fallback branch.
4. Keep unknown or unavailable provenance explicit as unknown/unavailable; do not coerce it into fresh, healthy, or measured.
5. If a visible UI label changes, keep the wording aligned to backend truth rather than inventing new scoring language on the frontend.

### Tests

- `git diff --check`
- focused endpoint/meta contract tests for the chosen family
- focused UI trust-state tests only if visible copy or branching changes
- governance validators if queue docs change

### Acceptance

- One pilot analytics family can expose why its data is trusted using explicit requested/effective period, provenance, and fallback truth instead of inference.
- Unknown or degraded provenance no longer looks fresh or fully authoritative.
- The frontend does not invent provenance semantics that the backend contract does not own.

### Dependencies

- `RQ110` DONE.
- `RQ111` DONE.
- `RQ112` DONE.
- Keep the scope to one family and one provenance contract.

---

## RQ114 - Build a reusable deterministic seed pack and expected-output manifest for pilot analytics proof

Status: WAITING
Ready after: `RQ113` is `DONE`
Priority: P1
Type: tests/docs
Feature family: analytics-deterministic-seed-pack
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ114-<agent>.lock.md`
Commit suggestion: `test(analytics): add deterministic pilot proof seed pack`

### Problem

Trendplus now has growing current-main proof needs for pilot analytics, but many focused checks still rely on one-off seeded fixtures or prompt-local reasoning. Without a reusable deterministic seed pack and expected-output manifest, future reliability prompts can pass locally while proving different implicit datasets, which weakens both repeatability and evidence quality.

### Evidence

- `RQ110` introduces a named screen-data availability matrix, which is a strong start, but it does not by itself create a reusable authoritative seed pack for future prompts.
- `RQ112` and `RQ113` depend on deterministic seeded bases and expected outputs; without a shared pack, later prompts can drift in what they consider the authoritative proof dataset.
- Existing analytics tests already contain seeded cases across dashboard, product decision, decision board, inventory, supplier, and actions, but they are spread across hosts and are not yet documented as one reusable pilot proof basis.
- Pilot-readiness claims are stronger when repeated prompts can cite the same known seed set, expected rows, expected warnings, and allowed empty/degraded states.

### Scope

- test fixtures/builders/seed helpers already used by the pilot analytics test hosts
- one new `docs/qa/` manifest that names the canonical seed pack, its authoritative basis, and expected outputs by screen family
- minimal test-host changes needed so later prompts can reuse the same seed pack instead of cloning ad hoc datasets
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

### Read first

- final outputs from `RQ110`, `RQ112`, and `RQ113`
- the pilot screen-family test hosts named by `RQ110`
- the nearest existing seed helpers/builders for those hosts
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`

### Do

1. Create one reusable deterministic seed pack for pilot analytics proof that can back at least the main families already exercised by `RQ110` through `RQ113`.
2. Document, for each included family:
   - authoritative source rows/snapshots;
   - requested/effective filters;
   - expected non-empty outputs;
   - allowed explicit empty/degraded/warning outcomes.
3. Reuse existing test helpers where possible; do not create a second parallel seed system without a clear owner reason.
4. Add only the smallest test-host hooks needed so later reliability prompts can consume the same pack with stable names and stable expected-output references.
5. Keep the seed pack deterministic and current-main-friendly; do not introduce runtime-only or environment-specific proof requirements.

### Tests

- `git diff --check`
- focused tests for any touched seed helpers or hosts
- governance validators if queue/docs metadata changes

### Acceptance

- Trendplus has one reusable deterministic pilot analytics seed pack and expected-output manifest that later prompts can cite directly.
- Future reliability prompts no longer need to reinvent the authoritative seeded basis for the same pilot families.
- The proof basis stays compatible with explicit empty/degraded semantics instead of forcing every family to look non-empty.

### Dependencies

- `RQ110` DONE.
- `RQ112` DONE.
- `RQ113` DONE.
- Keep this prompt at reusable proof-harness scope; do not broaden into general integration-test refactoring.

---

## RQ115 - Isolate the dashboard seeded-data proof left open by RQ110

Status: WAITING
Ready after: `RQ110` is `DONE`
Priority: P1
Type: docs/tests/backend-contract
Feature family: analytics-dashboard-seeded-proof
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ115-<agent>.lock.md`
Commit suggestion: `test(analytics): prove dashboard seeded data path`

### Problem

The RQ110 review explicitly found that the dashboard family has no separately named isolated seeded-non-empty backend proof. The pilot matrix currently relies on route/meta/smoke evidence for that row, so a dashboard blank state could still be confused with a valid empty dataset.

### Evidence

- `.ai/runs/2026-08-22-RQ110-evidence.md` records the dashboard gap as the primary missed item.
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md` identifies the dashboard as the least isolated proof surface.
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` records dashboard refresh/cache risk.

### Scope

- the dashboard endpoint/query and its nearest backend test host;
- the dashboard row in `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`;
- one deterministic seeded fixture or source-basis note;
- this queue and a dated evidence note.

### Read first

- the final RQ110 matrix and completion note;
- dashboard endpoint/service files and nearest focused tests;
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`.

### Do

1. Name the authoritative dashboard source, requested/effective period and scope, refresh owner, and cache identity.
2. Add the smallest deterministic seeded proof that returns non-empty dashboard data when the source basis exists.
3. If the source cannot be trusted or is unavailable, return an explicit empty/warning/provenance reason; never use a blank route shell or zero-filled fallback as proof.
4. Classify any failure as source, filter, cache, route/render, or test-harness gap and create a narrower follow-up if runtime repair is needed.

### Tests

- `git diff --check`;
- focused dashboard backend contract/integration test;
- prompt and planning validators when queue/docs change.

### Acceptance

- Dashboard has a separately citeable seeded non-empty proof or an explicit blocked/degraded contract.
- A missing dashboard row cannot be reported as healthy empty data.
- The matrix names the physical source or honestly records why it cannot be named.

### Dependencies

- `RQ110` DONE.
- Do not broaden into the full refresh/cache repair owned by `RQ111`.

---

## RQ116 - Prove Decision Pulse queued/sent/disabled states without claiming unverified delivery

Status: WAITING
Ready after: `RQ109` is `DONE` and the owner authorizes delivery-proof work
Priority: P1
Type: backend/tests/docs
Feature family: decision-pulse-delivery-truth
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ116-<agent>.lock.md`
Commit suggestion: `test(analytics): prove decision pulse delivery states`

### Problem

RQ109 added scheduled Pulse generation and a delivery path, but its evidence explicitly missed live SMTP proof and did not prove a durable receipt for each attempt. Operators must be able to distinguish queued, delivered, disabled, and failed delivery without treating missing SMTP configuration as success.

### Evidence

- `.ai/runs/2026-08-22-RQ109-evidence.md` records that live SMTP send was not exercised.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` records that RQ106/RQ109 still lack external delivery proof.
- Existing delivery/config services define the runtime boundary; this prompt must not invent production credentials or recipients.

### Scope

- Decision Pulse delivery/schedule services, config, and nearest tests;
- an additive delivery-attempt/receipt contract or durable in-app state if the existing owner supports it;
- queue/docs evidence only.

### Read first

- RQ109 completion note and current Pulse service/worker tests;
- `Api/Services/Analytics/DecisionPulseDeliveryService.cs`;
- `Application/Analytics/DecisionPulse/DecisionPulseAutomationContracts.cs`;
- the current SMTP/runtime configuration contract.

### Do

1. Define explicit states such as `queued`, `sent`, `disabled`, `failed`, and `not_attempted` with safe reason codes.
2. Ensure disabled/missing SMTP or recipients cannot be reported as delivered.
3. Add deterministic tests for successful composition, disabled configuration, recipient absence, and delivery failure.
4. If external SMTP cannot be exercised, record that as an external gate and prove the local receipt/state contract instead of fabricating a live-send result.

### Tests

- `git diff --check`;
- focused Decision Pulse delivery/scheduler tests;
- governance validators if queue/docs metadata changes.

### Acceptance

- Every Pulse attempt has an honest local delivery state and reason.
- No evidence claims live SMTP delivery without an actual configured send.
- Existing empty/error suppression and tenant scope remain unchanged.

### Dependencies

- `RQ109` DONE.
- No production SMTP, recipient, or secret changes are authorized by this prompt.

---

## RQ117 - Prove forecast/observed pairing availability and stale/missing semantics

Status: WAITING
Ready after: `RQ108` and `RQ96` are `DONE`
Priority: P1
Type: backend/tests/docs
Feature family: forecast-observed-pair-availability
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ117-<agent>.lock.md`
Commit suggestion: `test(analytics): prove forecast observed pairing availability`

### Problem

RQ108 delivered the forecast materializer and fail-closed observed pairing foundation, but its residual risk states that pairing remains dependent on the RQ96 observed daily stock foundation. A forecast comparison must be explicitly unavailable when no observed window exists, rather than silently becoming zero, trusted, or complete.

### Evidence

- `.ai/runs/2026-08-22-RQ108-evidence.md` records the observed-pair dependency as the remaining risk.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md` defines RQ96 provenance and missing-history semantics.
- Current forecast materialization code already exposes provenance fields that can be tested without inventing observations.

### Scope

- inventory forecast read/materializer/pairing contracts and nearest tests;
- one dated pairing availability contract note;
- queue and run evidence.

### Read first

- RQ96, RQ97, RQ98, and RQ108 completion evidence;
- forecast materialization and observed-pairing source files;
- `Api.Tests/InventorySnapshotContractTests.cs` and related integration coverage.

### Do

1. Add deterministic fixtures for trusted paired data, missing observed data, stale observed data, and mismatched store/period scope.
2. Return explicit `trusted`, `stale`, `missing_relation`, or `unavailable` semantics with null comparison values when evidence is absent.
3. Prove forecast issue time, observed date, tenant/store scope, and provenance cannot be borrowed from unrelated rows.
4. Keep this prompt at pairing availability; do not add forecasting formulas or fabricate historical stock.

### Tests

- `git diff --check`;
- focused forecast materializer/pairing tests;
- governance validators if queue/docs metadata changes.

### Acceptance

- Paired comparisons are trusted only when both forecast and observed evidence match the requested scope/window.
- Missing, stale, and mismatched observations remain explicit and non-actionable.
- No synthetic zero or inferred freshness is used to complete a comparison.

### Dependencies

- `RQ96`, `RQ97`, and `RQ108` DONE.

---

## RQ118 - Close the residual unscoped Data Quality issues sales window

Status: WAITING
Ready after: owner promotes the P1 dataScope residual
Priority: P1
Type: backend/tests
Feature family: data-quality-issues-scope-lineage
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ118-<agent>.lock.md`
Commit suggestion: `fix(analytics): align data quality issues scope`

### Problem

RQ05/RQ06 fixed the top-offender query path, but the audit still names `GetDataQualityIssuesHandler` as using an unscoped `sales_30d` CTE. That can leak sales from another origin into a scoped Data Quality issue list and make a warning amount look more authoritative than its source.

### Evidence

- `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md` marks the residual as `RQ06-F1`.
- `.ai/runs/2026-08-22-large-commit-review-evidence.md` confirms earlier work did not re-audit this handler.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` RQ06 completion notes leave this exact residual open.

### Scope

- `GetDataQualityIssuesHandler` and its nearest SQL/query tests;
- the dataScope consistency audit and this queue;
- no PDC, inventory, or supplier formula changes.

### Read first

- `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`;
- RQ05/RQ06 completion notes;
- the handler and `DataScopeConsistencyContractTests`.

### Do

1. Reproduce imported/existing/all cases where article and sale-header origins differ.
2. Apply the canonical sale-header scope to the sales window, or document an explicit forced-all contract in response metadata.
3. Add true-zero, missing-scope, and cross-origin regression cases.
4. Preserve existing all-scope behavior unless a before/after contract note proves it was wrong.

### Tests

- `git diff --check`;
- focused Data Quality issues and dataScope tests;
- governance validators if queue/docs change.

### Acceptance

- Scoped Data Quality issue revenue cannot include an unrelated sale origin silently.
- Missing/unknown scope is explicit, not treated as all or zero.

### Dependencies

- RQ05/RQ06 DONE; owner promotion required because this is a residual follow-up, not a new current READY task.

---

## RQ119 - Resolve or explicitly expose PDC/inventory dual-origin scope behavior

Status: WAITING
Ready after: `RQ118` is `DONE` or the owner explicitly reprioritizes the dual-origin lane
Priority: P1
Type: backend/tests/docs
Feature family: analytics-dual-origin-scope-contract
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ119-<agent>.lock.md`
Commit suggestion: `docs(analytics): freeze dual origin scope contract`

### Problem

The RQ05 audit found high-risk dual-origin or forced-all behavior in Product Decision Center, inventory insights, and Decision Board inventory cards. Without an explicit contract, users can compare scoped sales with unscoped inventory and draw a false replenishment or supplier conclusion.

### Evidence

- `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md` tracks `RQ05-F1` and `RQ05-F2`.
- `docs/qa/ANALYTICS_SQL_FILTER_CONSISTENCY_AUDIT.md` repeats the same unresolved scope split.
- RQ05 completion explicitly states these follow-ups were documented, not fixed.

### Scope

- one smallest proven PDC or inventory/Decision Board scope family;
- contract tests and an additive scope/provenance note;
- no SQL formula rewrite or frontend redesign.

### Read first

- both dataScope audits and RQ05 completion note;
- the selected builder/endpoint and nearest contract tests;
- `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`.

### Do

1. Choose one family and state whether article origin, sale-header origin, both, or forced-all is authoritative.
2. Add mismatch fixtures and expose requested/effective scope in metadata when the two origins cannot be aligned safely.
3. Keep recommendation/action eligibility conservative when scope evidence is mixed or unavailable.
4. Create a separate follow-up for any second family instead of broadening this task.

### Tests

- `git diff --check`;
- focused scope-lineage tests for the selected family;
- governance validators if queue/docs change.

### Acceptance

- One high-risk dual-origin family has a tested, citeable scope contract.
- Mixed-scope values are labelled/degraded rather than silently compared as like-for-like.

### Dependencies

- `RQ118` DONE or explicit owner reprioritization.

---

## RQ120 - Surface source, denominator, and provenance metadata in the first proven pilot UI

Status: WAITING
Ready after: `RQ112` and `RQ113` are `DONE`
Priority: P1
Type: frontend-contract/tests
Feature family: analytics-trust-metadata-ui-propagation
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ120-<agent>.lock.md`
Commit suggestion: `fix(analytics): surface pilot trust metadata`

### Problem

Several earlier contracts added additive backend trust metadata, while earlier evidence noted that UI labels can still be absent. A numerically correct pilot result is not fully defensible if the operator cannot see its denominator, source status, effective scope, or generation/provenance state.

### Evidence

- RQ02/RQ12 introduced denominator metadata; RQ03 introduced `sourceStatus` vocabulary.
- RQ104 proves selected core pages do not invent reliability, but does not cover every pilot family or every new provenance field.
- RQ112/RQ113 are intended to select the first reconciled/provenance-backed family for this UI slice.

### Scope

- one pilot family selected by RQ112/RQ113;
- its TypeScript API type, trust header/metadata mapping, and nearest UI contract tests;
- no frontend formula or local confidence scoring.

### Read first

- final RQ112/RQ113 contracts;
- selected family backend DTO and TypeScript service/type definitions;
- RQ104 evidence and the shared analytics trust UI patterns.

### Do

1. Map backend source status, denominator scope, requested/effective period/scope, freshness, and provenance fields without renaming their meaning.
2. Render unknown/unavailable as explicit trust states; never coerce them to zero, green, fresh, or measured.
3. Add one success, true-zero, unknown/fallback, and error/empty display test for the selected family.
4. Keep machine reason codes behind the established operator mapping.

### Tests

- `git diff --check`;
- focused Vitest contract tests and analytics guardrails;
- governance validators if queue/docs change.

### Acceptance

- The first proven pilot family visibly explains the backend-owned data trust metadata.
- UI output preserves denominator, source/fallback, freshness, and effective-scope semantics.
- No local scoring or fake-zero fallback is introduced.

### Dependencies

- `RQ112` and `RQ113` DONE.
- If frontend dependencies are unavailable, record the environment failure and do not change backend semantics to satisfy the harness.
