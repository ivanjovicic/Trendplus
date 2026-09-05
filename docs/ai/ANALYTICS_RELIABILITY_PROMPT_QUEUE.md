# Analytics Reliability Prompt Queue

Date: 2026-09-05
Repo: `ivanjovicic/Trendplus`
Current READY prompt: RQ139
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
| RQ112 | DONE | analytics-summary-detail-reconciliation | Reconcile pilot analytics summary values against detail/export on the first proven family |
| RQ113 | DONE | analytics-generation-provenance-truth | Expose exact freshness/provenance truth for the first pilot family that still looks trusted by inference |
| RQ114 | DONE | analytics-deterministic-seed-pack | Build a reusable deterministic seed pack and expected-output manifest for pilot analytics proof |
| RQ115 | DONE | analytics-dashboard-seeded-proof | Isolate dashboard seeded-data proof left open by RQ110 |
| RQ116 | DONE | decision-pulse-delivery-truth | Prove Pulse queued/sent/disabled states without claiming unverified delivery |
| RQ117 | DONE | forecast-observed-pair-availability | Prove forecast/observed pairing availability and stale/missing semantics |
| RQ118 | DONE | data-quality-issues-scope-lineage | Close the residual unscoped Data Quality issues sales window |
| RQ119 | DONE | analytics-dual-origin-scope-contract | Resolve or explicitly expose PDC/inventory dual-origin scope behavior |
| RQ120 | DONE | analytics-trust-metadata-ui-propagation | Surface source/denominator/provenance metadata in the first proven pilot UI |
| RQ121 | DONE | analytics-dashboard-row-trust-payload | Expose per-row margin/recommendation trust payload in dashboard top-product tables |
| RQ122 | DONE | supplier-decision-recommendation-trust-payload | Surface backend-owned trust state on supplier summary/quadrant/header recommendations |
| RQ123 | DONE | analytics-report-cache-generation-truth | Prove report-generation freshness/cache-version truth for pilot reports |
| RQ124 | DONE | analytics-dashboard-action-trust-payload | Expose backend-owned trust payload on dashboard legacy/advanced action cards |
| RQ134 | DONE | supplier-summary-aggregation-refresh-parity | Prove supplier summary freshness after successful aggregate refresh |
| RQ135 | DONE | data-quality-trust-propagation-after-snapshot | Refresh trust-bearing analytics caches after data-quality snapshot |
| RQ128 | WAITING | pdc-actionability-deploy-parity | Prove the PDC/Decision Board actionability gate on the exact production deployment |
| RQ129 | DONE | decision-board-non-product-confidence-normalization | Remove non-product fake confidence from blocked and insufficient Decision Board cards |
| RQ132 | WAITING | dashboard-support-signal-explainability | Explain the exact block reason, evidence state and next safe operator step for Dashboard support signals |
| RQ137 | PARTIAL | analytics-period-lineage-parity | Align requested, effective and observed period truth across dashboard, pilot readiness and supplier reports |
| RQ138 | PARTIAL | trend-model-evaluation-contract | Add an authoritative evaluation contract before Trend Models can show numeric scores again |
| RQ139 | READY | analytics-denominator-null-zero-contract | Prove every analytics numerator/denominator and prevent missing values becoming trusted zeroes |
| RQ140 | WAITING | pre-post-nivelacija-causal-comparability | Prove pre/post nivelacija effects are comparable and separate from availability/composition effects |
| RQ141 | WAITING | analytics-lineage-scope-cache-refresh-parity | Map every analytics route to period, scope, source, schema, cache and refresh truth |
| RQ142 | WAITING | forecast-trend-measured-evaluation | Materialize measured forecast/trend evaluation instead of contract-only or heuristic claims |
| RQ143 | WAITING | backend-decision-ranking-ownership | Remove frontend decision/ranking invention and make actionability backend-owned end to end |
| RQ144 | WAITING | data-quality-health-denominator-contract | Make Data Quality health distinguish no evidence, valid zero and unavailable shares |
| RQ145 | WAITING | analytics-surface-parity-and-safe-messaging | Prove table/chart/detail/export/report parity and safe mapping of backend codes |
| RQ146 | WAITING | analytics-schema-runtime-proof | Prove endpoint, EF/SQL, relation/migration, 404 and refresh-failure behavior on current runtime |

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

Status: DONE
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

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: reconciled the supplier decision family so summary metrics, detail sections, and export payload rows all match the same authoritative seeded basis.
- Changed files:
  - `Api.Tests/AnalyticsReportsContractTests.cs`
  - `docs/qa/ANALYTICS_SUPPLIER_SUMMARY_DETAIL_RECONCILIATION_2026-08-24.md`
  - `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
  - `.ai/runs/2026-08-24-RQ112-evidence.md`
- Checks run: `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`; `git diff --check`; `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~AnalyticsReportsContractTests|FullyQualifiedName~SupplierNegotiationPackReportTests|FullyQualifiedName~SupplierDecisionHubContractTests"`
- Checks not run: full solution build/test; live smoke / production proof
- Run log: `.ai/runs/2026-08-24-RQ112-evidence.md`
- Main commit SHA: `42b6b38691d46e44c67ba0e5c36a21427755d09a`
- Main verification: `git merge-base --is-ancestor 42b6b38691d46e44c67ba0e5c36a21427755d09a origin/main -> ancestor=true`
- Missed: no intentional denominator split was needed for the first proven family
- Evidence state: synchronized
- Delivery mode: main delivered
- Follow-up: `RQ113`
- Residual risk: other analytics families still need their own staged reconciliation proofs

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

Status: DONE
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

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: Added an explicit provenance basis contract for the supplier decision hub and supplier sales stats family, surfaced it in the shared trust header / snapshot UI and report payload metadata, and verified the focused backend/frontend contract tests.
- Changed files: `Api/Endpoints/SupplierDecisionHubEndpoints.cs`, `Api/Endpoints/AllEndpoints.cs`, `Api.Tests/SupplierDecisionHubContractTests.cs`, `Api.Tests/SupplierDecisionSchemaSqlTests.cs`, `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader.tsx`, `Klijent/clientapp/src/components/analytics/SupplierDecisionReport.tsx`, `Klijent/clientapp/src/components/supplierDecisionHub/SupplierExplainabilitySnapshot.tsx`, `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`, `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`, `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`, `Klijent/clientapp/src/services/supplierDecisionHubApi.ts`, `Klijent/clientapp/src/services/supplierDecisionReport.ts`, `Klijent/clientapp/src/services/supplierSalesStatsApi.ts`, `Klijent/clientapp/src/pages/supplierSharedState.ts`, `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx`, `Klijent/clientapp/src/pages/__tests__/SupplierConsolidatedPage.spec.tsx`, `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`, `Klijent/clientapp/src/pages/__tests__/analyticsTrustStateProof.spec.tsx`, `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsTrustHeader.spec.tsx`, `Klijent/clientapp/src/components/analytics/__tests__/SupplierExplainabilitySnapshot.spec.tsx`, `Klijent/clientapp/src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx`, `Klijent/clientapp/src/services/__tests__/supplierDecisionHubApi.spec.ts`, `docs/qa/ANALYTICS_GENERATION_PROVENANCE_TRUTH_2026-08-24.md`
- Contract/runtime behavior changed: supplier analytics trust surfaces now expose a backend-led provenance basis instead of leaving refresh/materialized-view generation implicit; the supplier decision hub still carries requested/effective dataset and fallback state, while supplier sales stats now carries a live-query/snapshot provenance basis.
- Checks run: `git diff --check` (pass); `dotnet test .\\Api.Tests\\Api.Tests.csproj --filter "FullyQualifiedName~SupplierDecisionHubContractTests|FullyQualifiedName~SupplierDecisionSchemaSqlTests|FullyQualifiedName~DecisionPulseProjectorTests|FullyQualifiedName~DecisionBoardEndpointsTests"` (pass); `npm ci` in `Klijent/clientapp` (pass); `npm run test:run -- src/components/analytics/__tests__/AnalyticsTrustHeader.spec.tsx src/components/analytics/__tests__/SupplierExplainabilitySnapshot.spec.tsx src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx src/pages/__tests__/SupplierDecisionHubPage.spec.tsx src/pages/__tests__/SupplierConsolidatedPage.spec.tsx src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/analyticsTrustStateProof.spec.tsx src/services/__tests__/supplierDecisionHubApi.spec.ts` (pass after one assertion refinement); `npm run test:run -- src/pages/__tests__/SupplierDecisionHubPage.spec.tsx` (pass)
- Checks not run: full repo build/test suites; not needed after the focused contract and UI proof passed
- Run log: `.ai/runs/2026-08-24-RQ113-evidence.md`
- Evidence state: synchronized
- Delivery mode: main
- Main commit SHA: 25ec243515becb9d1c6bc47561cd08ba6af35cf4
- Main verification: current main contains 25ec243515becb9d1c6bc47561cd08ba6af35cf4
- Missed: none known
- Follow-up: RQ114
- Residual risk: the supplier sales stats provenance basis is intentionally string-based (`live_query` or `live_query/snapshot_cost_batch_<id>`) and may need future owner-doc refinement if that surface gets a stricter materialized-view contract.
- Next: RQ114
- Prompt defect / scope repair: none

---

## RQ114 - Build a reusable deterministic seed pack and expected-output manifest for pilot analytics proof

Status: DONE
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

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: Added a reusable pilot analytics seed pack and expected-output manifest, then switched the product-decision, inventory, and analytics-actions host tests to the shared pack so later prompts can cite one canonical proof basis instead of cloning ad hoc fixtures.
- Changed files: `Api.Tests/PilotAnalyticsSeedPack.cs`, `Api.Tests/PilotAnalyticsSeedPackTests.cs`, `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`, `Api.Tests/InventoryListEndpointIntegrationTests.cs`, `Api.Tests/AnalyticsActionsEndpointsTests.cs`, `docs/qa/ANALYTICS_PILOT_DETERMINISTIC_SEED_PACK_2026-08-24.md`
- Contract/runtime behavior changed: pilot proof fixtures now have one reusable shared seed pack and manifest; product-decision and inventory seeds stay aligned to the shared helper, while inventory keeps a runtime-relative freshness base to preserve the out-of-stock signal path.
- Checks run: `git diff --check` (pass); `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~PilotAnalyticsSeedPackTests|FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests|FullyQualifiedName~InventoryListEndpointIntegrationTests|FullyQualifiedName~AnalyticsActionsEndpointsTests"` (pass, 36 passed)
- Checks not run: full repo suites; not needed after the focused proof and helper tests passed
- Run log: `.ai/runs/2026-08-24-RQ114-evidence.md`
- Evidence state: synchronized
- Delivery mode: main
- Main commit SHA: 53adf409e617aacc69449ecfa1a8939b2307bd7d
- Main verification: current main contains 53adf409e617aacc69449ecfa1a8939b2307bd7d
- Missed: none known
- Follow-up: `RQ115`
- Residual risk: inventory freshness is intentionally runtime-relative so the out-of-stock path stays exercised; later prompts should reuse the pack instead of re-seeding ad hoc timestamps.
- Next: `RQ115`
- Prompt defect / scope repair: none

---

## RQ115 - Isolate the dashboard seeded-data proof left open by RQ110

Status: DONE
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

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: Added a separately citeable seeded dashboard proof, then expanded the reusable pilot analytics seed pack and manifest so the dashboard, product-decision, inventory, and actions families all share one canonical proof basis instead of cloning ad hoc fixtures.
- Changed files: `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`, `Api.Tests/PilotAnalyticsSeedPack.cs`, `Api.Tests/PilotAnalyticsSeedPackTests.cs`, `docs/qa/ANALYTICS_PILOT_DETERMINISTIC_SEED_PACK_2026-08-24.md`, `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`
- Contract/runtime behavior changed: the dashboard now has a deterministic non-empty seeded proof; the pilot proof pack manifest now names the dashboard basis alongside the existing reusable shared families; inventory keeps a runtime-relative freshness base to preserve the out-of-stock signal path.
- Checks run: `git diff --check` (pass); `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~PilotAnalyticsSeedPackTests|FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests.DashboardBootstrap_SeededData_ReturnsNonEmptyExecutiveSnapshot|FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests.SalesSummary_ReturnsExactScopedTotalsAndHealthyMeta|FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests.InventoryBalance_ReturnsExactCountsAndValueForStore"` (pass, 5 passed)
- Checks not run: full repo suites; not needed after the focused proof and helper tests passed
- Run log: `.ai/runs/2026-08-24-RQ115-evidence.md`
- Evidence state: synchronized
- Delivery mode: main
- Main commit SHA: `fb9771406bfca1e98f9a001f379c9a7e21d4e141`
- Main verification: current main contains `fb9771406bfca1e98f9a001f379c9a7e21d4e141`
- Missed: none known
- Follow-up: none
- Residual risk: inventory freshness is intentionally runtime-relative so the out-of-stock path stays exercised; later prompts should reuse the pack instead of re-seeding ad hoc timestamps.
- Next: none
- Prompt defect / scope repair: none

---

## RQ116 - Prove Decision Pulse queued/sent/disabled states without claiming unverified delivery

Status: DONE
Ready after: n/a
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

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: Proved Decision Pulse delivery states locally with deterministic tests for source_error, recipients_missing, smtp_disabled, and successful send; added a contract note so missing SMTP or recipients stay explicit instead of looking delivered.
- Changed files: `Api.Tests/DecisionPulseServiceTests.cs`; `docs/qa/DECISION_PULSE_DELIVERY_STATE_CONTRACT_2026-08-24.md`
- Contract/runtime behavior changed: delivery attempts now have locally provable non-delivery states and a clear success path; no live SMTP proof was claimed.
- Checks run: `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Api.Tests.DecisionPulseServiceTests|FullyQualifiedName~Trendplus2.Tests.InventorySnapshotContractTests|FullyQualifiedName~Api.Tests.DatabaseInitializerP0IntegrationTests.ForecastMaterializer_PersistsTrustedSnapshot_AndPairsObservedEvidence|FullyQualifiedName~Api.Tests.DatabaseInitializerP0IntegrationTests.ForecastMaterializer_StaleAndMismatchedScopesRemainUnpaired"` - pass (21 total, 2 targeted integration checks passed in final rerun); `git diff --check` - pass; `node scripts/check-prompt-queues.mjs` - pass
- Checks not run: live SMTP send; full repo suites
- Run log: `.ai/runs/2026-08-24-RQ116-RQ117-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `f78fcfef96863051fbeec470dafe350597ab31ff`
- Main verification: current main contains `f78fcfef96863051fbeec470dafe350597ab31ff`
- Missed: live SMTP credentialed delivery proof (intentionally out of scope)
- Follow-up: none
- Residual risk: external mail infrastructure remains unproven by design; local receipt/state contract is the durable proof
- Next: `RQ118`
- Prompt defect / scope repair: converted the prompt from gated WAITING to a local proof-and-receipt contract without inventing live delivery evidence

---

## RQ117 - Prove forecast/observed pairing availability and stale/missing semantics

Status: DONE
Ready after: n/a
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

### Completion note

- Date: 2026-08-24
- Status: DONE
- Completion: Proved forecast/observed pairing availability with stale, trusted, and mismatched-scope fixtures; stale provenance is now explicit on the read path and pairings stay fail-closed when scope evidence does not match.
- Changed files: `Api.Tests/DatabaseInitializerP0IntegrationTests.cs`; `Api.Tests/InventorySnapshotContractTests.cs`; `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs`; `Application/Analytics/Queries/GetInventoryForecast/InventoryForecastSnapshotProvenance.cs`; `Infrastructure/Services/Inventory/InventoryForecastSnapshotMaterializerService.cs`; `docs/qa/FORECAST_OBSERVED_PAIRING_CONTRACT_2026-08-24.md`
- Contract/runtime behavior changed: stale forecast provenance is explicit instead of implicit, and observed pairings are now visibly `stale` or `missing_observed_window` rather than borrowing unrelated evidence.
- Checks run: `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~Api.Tests.DatabaseInitializerP0IntegrationTests.ForecastMaterializer_PersistsTrustedSnapshot_AndPairsObservedEvidence|FullyQualifiedName~Api.Tests.DatabaseInitializerP0IntegrationTests.ForecastMaterializer_StaleAndMismatchedScopesRemainUnpaired"` - pass (2/2); `git diff --check` - pass; `node scripts/check-prompt-queues.mjs` - pass
- Checks not run: full repo suites; broader live DB/production verification
- Run log: `.ai/runs/2026-08-24-RQ116-RQ117-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `f78fcfef96863051fbeec470dafe350597ab31ff`
- Main verification: current main contains `f78fcfef96863051fbeec470dafe350597ab31ff`
- Missed: a wider historical comparison matrix beyond the targeted trusted/stale/mismatched fixtures
- Follow-up: none
- Residual risk: the pairing surface remains intentionally fail-closed for any evidence that does not match the exact requested window/scope
- Next: `RQ118`
- Prompt defect / scope repair: tightened the pairing contract so stale provenance is visible and null/absent comparison evidence stays non-actionable

---

## RQ118 - Close the residual unscoped Data Quality issues sales window

Status: DONE
Ready after: owner promotes the P1 dataScope residual
Priority: P1
Type: backend/tests
Feature family: data-quality-issues-scope-lineage
Parallel-safe: no
Owner: root
Local lock: `.ai/task-locks/RQ118-<agent>.lock.md` (removed after DONE)
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

### Completion note

- Date: 2026-08-27
- Status: DONE
- Completion: `GetDataQualityIssuesHandler` now scopes `sales_30d` by sale-header `DataOrigin`, so imported/existing issue lists no longer mix revenue across origins
- Changed files: `Application/Analytics/Queries/GetDataQualityIssues/GetDataQualityIssuesHandler.cs`, `Api.Tests/DataQualityIssuesHandlerTests.cs`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`, `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`, `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`, `docs/qa/ANALYTICS_RELIABILITY_RETROSPECTIVE_AUDIT_2026-08-23.md`, `MASTER_ROADMAP.md`, `.ai/runs/2026-08-27-RQ118-evidence.md`
- Missed: none known
- Checks run: `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~Api.Tests.DataQualityIssuesHandlerTests.Handle_ScopesSales30dByDataScope"` pass; `git diff --check` pass; `node scripts/check-prompt-queues.mjs --self-test` pass; `node scripts/check-prompt-queues.mjs` pass; `node scripts/check-planning-architecture.mjs --self-test` pass; `node scripts/check-planning-architecture.mjs` pass
- Checks not run: broader backend suite - not needed for this narrow handler regression because the focused integration test proved the residual
- Run log: `.ai/runs/2026-08-27-RQ118-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: ae8835e80676aaa0c51f5aae90e7519b8ffef9fe
- Main verification: `git branch --contains ae8835e80676aaa0c51f5aae90e7519b8ffef9fe` -> `* main`
  - Missed: RQ119 dual-origin lane was still waiting at that time and was not pulled into this same prompt; it was later closed by RQ119.
- Follow-up: none
- Residual risk: query load still depends on the same sale-header `DataOrigin` contract being accurate in source data
- Next: none
- Prompt defect / scope repair: none

---

## RQ119 - Resolve or explicitly expose PDC/inventory dual-origin scope behavior

Status: DONE
Ready after: `RQ118` is `DONE` or the owner explicitly reprioritizes the dual-origin lane
Priority: P1
Type: backend/tests/docs
Feature family: analytics-dual-origin-scope-contract
Parallel-safe: no
Owner: unassigned
Local lock: removed after DONE
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

### Notes

- 2026-08-27: DONE. Product Decision Center now exposes explicit dual-origin provenance metadata on the response itself (`RequestedDataScope`, `ScopeAuthority`, `ScopeBreakdown`) and the integration test asserts the requested scope for both imported and existing paths. The family is now explicit about article-origin membership plus sale-header revenue, rather than silently comparing the two as though they were interchangeable.
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
  - `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`
  - `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`
  - `docs/qa/ANALYTICS_RELIABILITY_RETROSPECTIVE_AUDIT_2026-08-23.md`
- Checks:
  - `dotnet test .\\Api.Tests\\Api.Tests.csproj --filter "FullyQualifiedName~Api.Tests.ProductDecisionCenterBuilderIntegrationTests.BuildProductDecisionCenter_DataScopeSeparatesImportedAndExistingProducts"` - pass
  - `git diff --check` - pass
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
  - `node scripts/check-prompt-queues.mjs` - pass
  - `node scripts/check-planning-architecture.mjs --self-test` - pass
  - `node scripts/check-planning-architecture.mjs` - pass
- Run log: `.ai/runs/2026-08-27-RQ119-evidence.md`
- Evidence state: synchronized
- Next: `RQ05-F2 - Inventory + Decision Board apply article dataScope or explicit forced-all meta`

### Dependencies

- `RQ118` DONE or explicit owner reprioritization.

---

## RQ120 - Surface source, denominator, and provenance metadata in the first proven pilot UI

Status: DONE
Ready after: `RQ112` and `RQ113` are `DONE`
Priority: P1
Type: frontend-contract/tests
Feature family: analytics-trust-metadata-ui-propagation
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ120-<agent>.lock.md`
Commit suggestion: `fix(analytics): surface pilot trust metadata`
Promotion note: 2026-08-25 - `RQ112` and `RQ113` are DONE on current main, so this follow-up is promoted to READY.

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

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: surfaced the pilot trust metadata in the first proven UI by forwarding requested/effective scope lineage and the available effective period window into the shared supplier trust header.
- Changed files:
  - `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/analyticsTrustStateProof.spec.tsx`
  - `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
  - `.ai/runs/2026-08-26-RQ120-evidence.md`
- Contract/runtime behavior changed: supplier sales stats now shows the shared trust header with the source label, requested/effective scope lineage, and effective data-window truth derived from the existing API response.
- Checks run: `npm run test:run -- src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/analyticsTrustStateProof.spec.tsx`
- Checks not run: full repo build/test suites
- Run log: `.ai/runs/2026-08-26-RQ120-evidence.md`
- Evidence state: pending
- Delivery mode: direct-main
- Main commit SHA: ead24ae3c531802ca54a58b607f3ef107121abb2
- Main verification: `git merge-base --is-ancestor ead24ae3c531802ca54a58b607f3ef107121abb2 HEAD -> true`
- Missed: none known
- Follow-up: `RQ121`
- Residual risk: the supplier sales stats endpoint still derives scope lineage on the frontend because its backend response does not expose a dedicated requested/effective dataset contract
- Prompt defect / scope repair: none

---

## RQ121 - Expose per-row margin/recommendation trust payload in dashboard top-product tables

Status: DONE
Ready after: `RQ120` is `DONE` or the owner explicitly promotes the dashboard row-trust lane
Priority: P1
Type: backend-frontend-contract/tests
Feature family: analytics-dashboard-row-trust-payload
Parallel-safe: no
Owner: agent-system
Local lock: `.ai/task-locks/RQ121-<agent>.lock.md`
Commit suggestion: `fix(analytics): surface dashboard row trust payload`

### Problem

Dashboard top-product tables still render margin rows with generic fallback copy like `Kvalitet marže nije dostupan`, while the backend DTO/type layer still carries TODOs for row-level margin-quality tier, cost-coverage, and recommendation-quality payload. A row can look financially meaningful without showing whether margin evidence is missing, partial, or intentionally unavailable.

### Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs` still marks `TopProductAdvancedItemDto` with a backend DTO TODO to expose per-row margin quality tier / cost coverage and recommendation quality payload.
- `Klijent/clientapp/src/types/analytics.ts` keeps the matching TODO on `TopProductAdvancedItem`.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx` falls back to `Kvalitet marže nije dostupan` for table rows instead of rendering a proven row-level trust contract.
- Earlier trust work (`RQ18`, `RQ45`) already established that hidden coverage fields make margin output look more trustworthy than the evidence allows.

### Scope

- dashboard top-product backend DTO/query mapping;
- `TopProductAdvancedItem` TypeScript contract and the nearest dashboard row rendering/tests;
- no margin formula rewrite, no ranking-score rewrite, and no Supplier Decision Hub changes.

### Read first

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`;
- `Klijent/clientapp/src/types/analytics.ts`;
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`;
- `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT_ADVANCED_V2.md` and `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`.

### Do

1. Decide the smallest truthful row contract: real margin-quality / cost-coverage fields when available, otherwise an explicit unavailable/insufficient row state.
2. Propagate that contract through DTOs and frontend types without inventing frontend-only scoring.
3. Replace the generic disclaimer with row-level trust text/badges that distinguish missing evidence from true zero or healthy coverage.
4. Add focused regression coverage for good, warning/partial, and unavailable margin rows.

### Tests

- `git diff --check`;
- focused dashboard/backend contract tests for top-product rows;
- focused frontend/Vitest tests for row trust rendering;
- governance validators if queue/docs change.

### Acceptance

- Dashboard top-product rows visibly explain whether margin/recommendation trust is good, partial, or unavailable.
- Unknown coverage is not presented as normal-looking margin confidence.
- The dashboard no longer relies on a generic shared disclaimer when row-level evidence is actually the missing contract.

### Dependencies

- `RQ120` DONE or explicit owner promotion.

---

## RQ134 - Prove supplier summary freshness after successful aggregate refresh

Status: DONE
Ready after: `RQ111` is `DONE` and the owner explicitly promotes the supplier-summary cache-parity lane
Priority: P1
Type: backend/workers/cache/tests
Feature family: supplier-summary-aggregation-refresh-parity
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ134-<agent>.lock.md`
Commit suggestion: `fix(analytics): refresh supplier summary after aggregate worker`

### Problem

Supplier decision summary surfaces can remain on TTL-managed cache after `AnalyticsAggregationWorker` refreshes the aggregate tables they depend on. The worker already clears the dashboard family and dashboard aggregate-backed prefixes, but the supplier-decision-hub family is not part of the same invalidation path. That leaves supplier summary responses able to lag behind successful aggregate refreshes, which makes freshness look stronger or more current than the system can prove.

### Evidence

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` says supplier summary cards that use aggregate tables can lag until TTL expiry and marks supplier-decision-hub as a P1 follow-up after aggregation-worker refresh.
- `Workers/AnalyticsAggregationWorker.cs` currently clears `AnalyticsCachePolicy.DashboardFamily` plus dashboard aggregate-backed prefixes only.
- `Api.Tests/AnalyticsAggregationWorkerTests.cs` only asserts dashboard-prefix invalidation.
- `AnalyticsCachePolicy.CoreFamilies` already includes `SupplierDecisionHubFamily`, so the family is first-class even though the aggregation worker does not currently touch it.

### Scope

- `Workers/AnalyticsAggregationWorker.cs`
- `Api.Tests/AnalyticsAggregationWorkerTests.cs`
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs` only if the shared cache contract needs a new assertion
- `Api.Tests/SupplierDecisionHubContractTests.cs` or `Api/Endpoints/SupplierDecisionHubEndpoints.cs` only if freshness must be surfaced explicitly instead of being cleared
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

### Read first

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `Workers/AnalyticsAggregationWorker.cs`
- `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`
- `Api.Tests/AnalyticsAggregationWorkerTests.cs`
- the nearest supplier summary/cache contract tests

### Do

1. Decide the smallest truthful contract for supplier summary after a successful aggregate refresh: clear the supplier-decision-hub family, or expose an explicit stale/lag state if the family is intentionally TTL-bound.
2. Prove the selected contract with focused tests for success and failure paths, including at least one counterexample that would have left stale supplier summary data visible before the fix.
3. Keep dashboard bootstrap/report freshness behavior out of scope.
4. If the prompt chooses explicit stale/lag state, add the smallest metadata path that tells the operator the summary is stale instead of letting TTL masquerade as freshness.
5. Do not broaden into nightly refresh, report generation, or inventory signal panels.

### Tests

- `git diff --check`
- focused `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsAggregationWorkerTests|FullyQualifiedName~SupplierDecisionHubContractTests"`
- focused frontend test only if supplier summary rendering changes
- governance validators if queue/docs change

### Acceptance

- Supplier summary freshness after aggregate refresh is either immediately cleared/refreshed or explicitly labeled as stale/lagging.
- The worker/cache contract is proven by tests rather than inferred from TTL behavior.
- Dashboard bootstrap and report freshness remain unchanged and out of scope.

### Dependencies

- `RQ111` DONE.
- No live-production proof is required for the queue prompt itself.

### Promotion note

- Date: 2026-09-01
- Status: READY
- Promotion: owner-promoted after the cache invalidation audit identified supplier summary lag after successful aggregate refresh
- Next: implement cache parity proof on the worker/test path

### Completion note

- Date: 2026-09-01
- Status: DONE
- Completion: supplier summary freshness now follows the aggregate refresh invalidation path because `AnalyticsAggregationWorker` clears the supplier-decision-hub family alongside the dashboard family
- Changed files: `Workers/AnalyticsAggregationWorker.cs`; `Api.Tests/AnalyticsAggregationWorkerTests.cs`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-09-01-RQ134-evidence.md`
- Checks run: `git diff --check`; `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`; `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsAggregationWorkerTests|FullyQualifiedName~AnalyticsCacheAdminServiceTests"` (18 passed)
- Checks not run: full solution build; wider frontend regression tests
- Run log: `.ai/runs/2026-09-01-RQ134-evidence.md`
- Delivery mode: local-workspace
- Main commit SHA: uncommitted
- Main verification: not verified; the work remains local in this workspace
- Missed: none
- Follow-up: `RQ128` once `STAB16` is resolved
- Residual risk: other cache paths still use the existing TTL-based contract where the worker does not explicitly clear them

---

## RQ135 - Refresh trust-bearing analytics caches after data-quality snapshot

Status: DONE
Completed after: `RQ134` is `DONE` and the owner explicitly promoted the data-quality trust-propagation lane
Priority: P1
Type: backend/workers/cache/tests
Feature family: data-quality-trust-propagation-after-snapshot
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ135-<agent>.lock.md`
Commit suggestion: `fix(analytics): refresh trust caches after data quality snapshot`

### Problem

`AnalyticsDataQualityHealthWorker` captures a new quality snapshot, saves it durably, and then clears only the `data-quality` and `reports` cache families. The audit still records medium-risk lag on dashboard, product-decision-center, supplier-decision-hub, and inventory trust surfaces when only the quality snapshot changes, which means those operator-facing trust callouts can stay one TTL behind the newest evidence.

### Evidence

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` says `AnalyticsDataQualityHealthWorker` clears `data-quality` and `reports`, while dashboard/product-decision-center/supplier-decision-hub/inventory trust surfaces are not explicitly cleared and may lag until TTL expiry.
- `Workers/AnalyticsDataQualityHealthWorker.cs` currently clears only `AnalyticsCachePolicy.DataQualityFamily` and `AnalyticsCachePolicy.ReportsFamily`.
- `AnalyticsCachePolicy.CoreFamilies` already includes `DashboardFamily`, `ProductDecisionCenterFamily`, `SupplierDecisionHubFamily`, and `InventoryFamily`, so the trust-bearing families are first-class cache targets.
- `AnalyticsCacheAdminServiceTests` already prove that report-family invalidation bumps the report cache version, so this follow-up should preserve that contract if reports remain in the clear set.

### Scope

- `Workers/AnalyticsDataQualityHealthWorker.cs`
- `Api.Tests/AnalyticsDataQualityHealthWorkerTests.cs`
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs` only if the shared cache contract needs a new assertion
- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`

### Read first

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`
- `Workers/AnalyticsDataQualityHealthWorker.cs`
- `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`
- `Infrastructure/Services/AnalyticsDataQualityHistoryService.cs`
- `Api.Tests/AnalyticsDataQualityHealthServiceTests.cs`
- `Api.Tests/AnalyticsAggregationWorkerTests.cs`
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs`

### Do

1. Decide the smallest truthful contract after a successful data-quality snapshot: clear the trust-bearing families that consume the snapshot, or expose an explicit stale/lag state if those families are intentionally TTL-bound.
2. Prove the selected contract with focused tests for a successful snapshot refresh and a failure path that leaves cache state untouched.
3. Keep aggregation-worker, nightly-refresh, and report-template behavior out of scope.
4. Preserve the existing report-version bump behavior owned by the worker if reports remain in the clear set.
5. Do not broaden into dashboard redesign or recommendation-scoring changes.

### Tests

- `git diff --check`
- focused `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsDataQualityHealthWorkerTests|FullyQualifiedName~AnalyticsCacheAdminServiceTests"`
- focused `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsDataQualityHealthServiceTests"` only if the snapshot contract needs a new counterexample
- governance validators if queue/docs change

### Acceptance

- Successful data-quality snapshot refreshes do not leave dashboard, product-decision-center, supplier-decision-hub, or inventory trust surfaces one TTL behind the newest quality evidence.
- Failure paths remain fail-closed and do not clear caches.
- Report freshness semantics stay truthful and unchanged except for the existing data-quality worker behavior.

### Completion note

- Date: 2026-09-01
- Status: DONE
- Completion: `AnalyticsDataQualityHealthWorker` now clears the trust-bearing dashboard, product-decision-center, supplier-decision-hub, inventory, data-quality, and reports cache families after a successful snapshot refresh, so the operator trust surfaces no longer wait for TTL expiry.
- Changed files: `Workers/AnalyticsDataQualityHealthWorker.cs`; `Api.Tests/AnalyticsDataQualityHealthWorkerTests.cs`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`; `.ai/runs/2026-09-01-RQ135-evidence.md`
- Checks run: `git diff --check`; `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsDataQualityHealthWorkerTests|FullyQualifiedName~AnalyticsCacheAdminServiceTests"` (17 passed)
- Checks not run: full solution build; wider frontend regression tests
- Run log: `.ai/runs/2026-09-01-RQ135-evidence.md`
- Delivery mode: local-workspace
- Main commit SHA: uncommitted
- Main verification: not verified; the work remains local in this workspace
- Missed: none
- Follow-up: `RQ128` once `STAB16` is resolved
- Residual risk: other cache paths still use the existing TTL-based contract where the worker does not explicitly clear them

### Dependencies

- `RQ134` DONE.
- No production mutation or worker scheduling change is authorized outside this worker/test path.

### Promotion note

- Date: 2026-09-01
- Status: READY
- Promotion: owner-promoted after the cache invalidation audit identified medium-risk trust lag on dashboard/product/supplier/inventory surfaces after data-quality snapshot refresh
- Next: implement trust-cache parity on the data-quality worker/test path

---

## RQ132 - Explain Dashboard support-signal limits and the next safe operator step

Status: WAITING
Ready after: `STAB16` is DONE and the canonical production API has a healthy runtime/refresh-status proof
Priority: P1
Type: backend-frontend-contract/tests
Feature family: dashboard-support-signal-explainability
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ132-<agent>.lock.md`
Commit suggestion: `fix(analytics): explain dashboard support-signal limits`

### Problem

The Dashboard currently repeats the generic Serbian copy `Prikazani su pomoćni signali. Signal je ograničen zbog kvaliteta ili nedovoljno podataka.` when all displayed actions have `recommendationAllowed=false`. That condition proves only that the shown recommendations are blocked; it does **not** prove that the selected period/store has no source data. The backend often has a specific reason (`missing_cost`, `missing_supplier`, `insufficient_history`, critical/stale/unknown freshness, or a legacy action with unavailable trust payload), but the Dashboard does not turn it into a single operator-facing diagnosis, affected scope/count, and next safe action. A source/API failure must remain an error/partial state, never a support-signal explanation.

### Evidence

- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx` sets `recommendationsBlocked` when every one of up to four prioritized actions has `recommendationAllowed === false`, then renders the same generic explanation both in the cockpit banner and on each blocked card.
- `Api/Endpoints/CachedAnalyticsEndpoints.cs` can build Product Decision action reasons from backend rows, including `FIX_DATA`, `INSUFFICIENT_DATA`, `DataQualityStatus`, `RecommendationReason`, and warning/reason codes, but `DashboardDecisionActionDto` does not expose a bounded, display-ready block-cause contract.
- The existing Product Decision profile already distinguishes `missing_cost`, `missing_supplier`, `insufficient_history`, critical data quality, and stale/unknown input freshness. Those causes must remain backend-owned and must not be recreated by a frontend score heuristic.
- The legacy advanced fallback can carry explicit trust metadata, or it can be an unavailable legacy helper payload. `RQ124` made the distinction representable, but the Dashboard still presents a generic limit sentence instead of explaining it to an operator.
- On 2026-08-31 the public production API returned HTTP 500 even for liveness, runtime-version, refresh-status, and dashboard bootstrap routes. Until `STAB16` restores liveness, the live UI cannot establish whether a visible limit came from absent data, stale refresh, partial/fallback content, or a failed API call.

### Scope

- Dashboard action/trust DTO composition in `Api/Endpoints/CachedAnalyticsEndpoints.cs` and existing analytics response metadata only where the authoritative cause is already known;
- `Klijent/clientapp/src/types/analytics.ts` and `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`;
- focused backend and Dashboard regression tests;
- the nearest Dashboard/Data Quality guidance only if it must describe the new operator-facing states.

Do not change recommendation thresholds, financial calculations, Product Decision scoring, worker scheduling, or the Data Quality issue formulas. Do not make the frontend infer or count business causes from card text.

### Read first

- `Api/Endpoints/CachedAnalyticsEndpoints.cs` (`BuildDashboardDecisionActions`, Product Decision confidence/warning helpers, dashboard bootstrap metadata);
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`;
- `Klijent/clientapp/src/types/analytics.ts`;
- `Api.Tests/CachedAnalyticsDashboardActionTrustTests.cs`;
- `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx`;
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` (`STAB16`);
- `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md`.

### Do

1. Define the smallest additive, backend-owned Dashboard support-signal payload. It must state a normalized state such as `no_qualifying_data`, `data_quality_blocked`, `insufficient_history`, `stale_or_unrefreshed`, `legacy_trust_unavailable`, or `partial_or_failed`; include bounded reason codes, effective filter/period, and a safe next-step target when the backend can prove one.
2. Keep these states visibly distinct:
   - a successful empty requested window;
   - rows with missing master/cost data;
   - insufficient sales/history evidence;
   - stale, unknown, or not-yet-refreshed input;
   - legacy/helper content whose trust is unavailable;
   - API/section failure or partial response.
   Do not label any of the last four as “nema podataka” unless the response explicitly proves an empty source window.
3. Render one concise, deduplicated diagnosis in the Dashboard cockpit. For each state, show what is missing or degraded, the selected period/scope, and the direct safe next step: correct source fields, inspect the affected Data Quality items, restore/await refresh, widen a genuinely empty date range, or contact support with a correlation ID for a failed response.
4. Preserve the existing per-card reason as supporting detail, but do not repeat the generic warning on every card. Keep the Data Quality and worker/refresh links only where they correspond to the backend-owned cause.
5. Add focused tests for: genuinely empty data, missing cost/supplier, insufficient history, stale/unknown refresh, legacy payload without trust data, and an API/partial failure. Verify no case renders fake zero, fake green, or an actionable recommendation.

### Tests

- `git diff --check`;
- focused `CachedAnalyticsDashboardActionTrustTests` plus the smallest bootstrap/meta contract test;
- focused `AnalyticsDashboard.operationalFallback.spec.tsx` or a dedicated Dashboard support-signal presentation test;
- governance validators if queue/docs change.

### Acceptance

- An operator can tell whether there are truly no qualifying records, data is incomplete, history is too short, freshness is degraded, trust is unavailable, or the API failed.
- Every non-error support-signal state has a truthful, scoped next step; failed/partial responses point to recovery/support rather than pretending a data-quality diagnosis.
- The Dashboard uses backend-owned reason/status semantics and does not introduce frontend scoring or data-quality inference.
- The generic support-signal sentence is not duplicated as the only explanation at both cockpit and card level.

### Dependencies

- `STAB16` DONE with current-main runtime, worker/freshness, and production liveness proof.
- `RQ124` is DONE and supplies the legacy-action trust payload foundation.

---

## RQ128 - Prove Product Decision actionability parity on the exact deployed runtime

Status: WAITING
Ready after: `STAB16` is DONE with worker/freshness evidence and read-only reconciliation on the canonical Render runtime
Priority: P0
Type: backend-frontend-contract/live-evidence
Feature family: pdc-actionability-deploy-parity
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ128-<agent>.lock.md`
Commit suggestion: `test(analytics): prove product decision actionability parity in production`

### Problem

The first 2026-08-27 production audit found PDC rows looking actionable under insufficient evidence. A same-day API-only recheck now shows the main fail-closed product repair on the canonical Render runtime, but exact live parity still cannot be claimed until `STAB16` closes worker/freshness, browser, and read-only reconciliation proof on that runtime family.

### Evidence

- A same-day 2026-08-27 API-only recheck returned runtime `commitSha=6ecbfa67a7304c3cbeeb71755a35255e766c8e24`, which is contained in current `main`.
- The same recheck returned 50 visible rows from 12,422 analyzed rows with 12 visible rows already blocked by `recommendationAllowed=false`, showing the product fail-closed path is now live for clearly blocked cases.
- `/api/analytics/refresh-status?dataScope=all` still returned `workersEnabled=false`, process `web`, unknown freshness, an in-memory-cache warning, and zero successful job timestamps, so exact live parity cannot yet be claimed.
- `CachedAnalyticsEndpoints.BuildProductDecisionConfidenceProfile(...)` now centrally clears recommendation allowance, decision confidence, and expected impact for blocked/stale/critical/unknown evidence.
- `DecisionBoardEndpoints` and `ExecutiveDecisionBoardPage.tsx` now fail closed for blocked recommendation payloads so an old numeric diagnostic value cannot look like decision confidence.
- `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md` plus `.ai/runs/2026-08-27-queue-audit-production-followups-evidence.md` record the live observations and the remaining proof gap.

### Scope

- Product Decision Center response/profile, Decision Board aggregate card, and Executive Board presentation parity;
- focused backend/frontend regression tests and exact-deploy live evidence;
- no new recommendation formula, ranking threshold, database migration, or frontend-owned business scoring.

### Read first

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`;
- `Api/Endpoints/DecisionBoardEndpoints.cs`;
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`;
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`;
- `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`;
- `Api.Tests/DecisionBoardEndpointsTests.cs`;
- `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md`;
- `.ai/runs/2026-08-27-queue-audit-production-followups-evidence.md`.

### Do

1. Keep the backend PDC profile as the authoritative actionability gate; do not recreate it in UI code.
2. Prove the four minimum counterexamples: source-blocked, `INSUFFICIENT_DATA`/`FIX_DATA`, critical data quality, and stale/unknown freshness all return `recommendationAllowed=false`, `confidenceScore=null`, and `expectedImpactRsd=null`.
3. Prove the Decision Board removes the blocked row from executable impact ranking and renders it as insufficient/blocked with a visible reason.
4. Prove the PDC and Executive Board UIs preserve the backend block even when a compatibility payload contains an old diagnostic percentage.
5. After `STAB16`, run the same checks on the exact deployed SHA and record returned/analyzed counts separately from visible rows.

### Tests

- focused `AnalyticsProductDecisionConfidenceTests`, `ProductDecisionCenterBuilderIntegrationTests`, and `DecisionBoardEndpointsTests`;
- focused `ExecutiveDecisionBoardPage.spec.ts` and PDC confidence presentation test;
- exact-deploy API/browser smoke after `STAB16`;
- `git diff --check` and governance validators when queue/evidence docs change.

### Acceptance

- Blocked PDC rows cannot carry actionable decision confidence or expected impact through API, Board aggregation, UI, or action payload.
- A numeric diagnostic percentage is never rendered as high/medium/low recommendation confidence when the recommendation is blocked.
- Live evidence ties the PDC/Board result to the exact current-main deployed SHA and records true returned/analyzed/ignored counts.
- Empty, unknown, stale, warning, and critical states remain visibly distinct from a valid zero or healthy recommendation.

### Dependencies

- `STAB16` DONE; it supplies the exact current-main deployment, worker/freshness evidence, and read-only reconciliation path.
- No direct production data mutation or formula change is authorized in this prompt.

---

## RQ129 - Remove non-product fake confidence from blocked and insufficient Decision Board cards

Status: DONE
Ready after: n/a
Priority: P0
Type: backend-contract/tests
Feature family: decision-board-non-product-confidence-normalization
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ129-<agent>.lock.md`
Commit suggestion: `fix(analytics): remove fake confidence from blocked board cards`

### Problem

The live Decision Board still exposes numeric confidence where the contract says operators should see a blocked or insufficient signal. On 2026-08-27, production inventory cards with `recommendationAllowed=false` still carried scores like `55` and `35`, and the outcome summary card returned `confidenceLevel=insufficient_data` together with `confidenceScore=0`. Those values can read like decision confidence instead of blocked evidence or an undersized sample.

### Evidence

- Live `GET /api/analytics/decision-board?dataScope=all` returned inventory cards with `recommendationAllowed=false`, `confidenceLevel=insufficient_data`, warning `inventory_recommendation_blocked`, and still `confidenceScore` values `55` / `35`.
- The same response returned the `actionsOutcome` summary card with `confidenceLevel=insufficient_data` and `confidenceScore=0` because `BuildOutcomeCards(...)` currently maps `outcomeSummary.Meta.MeasuredSampleSize` into `ConfidenceScore`.
- `DecisionBoardEndpoints.ResolveInventoryBoardConfidence(...)` currently preserves `SignalConfidencePct` even when `RecommendationAllowed == false`, and `Api.Tests/DecisionBoardEndpointsTests.cs` locks that behavior with `Assert.Equal(72m, resolved.ConfidenceScore)`.
- `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md` still documents “score preserved” for blocked inventory cards, but `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md` separately says missing/blocked recommendation confidence must stay nullable and that outcome feedback must not become recommendation confidence by itself.

### Scope

- `Api/Endpoints/DecisionBoardEndpoints.cs`;
- targeted backend tests in `Api.Tests/DecisionBoardEndpointsTests.cs` and `Api.Tests/DecisionBoardAggregationContractTests.cs`;
- Decision Board contract docs only where they describe the now-misleading blocked/insufficient confidence semantics;
- optional `ExecutiveDecisionBoardPage` test coverage if a rendering regression needs to be locked.

### Read first

- `Api/Endpoints/DecisionBoardEndpoints.cs`;
- `Api.Tests/DecisionBoardEndpointsTests.cs`;
- `Api.Tests/DecisionBoardAggregationContractTests.cs`;
- `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`;
- `docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md`;
- `.ai/runs/2026-08-27-queue-audit-production-followups-evidence.md`.

### Do

1. Make blocked inventory cards fail closed: when `RecommendationAllowed == false`, keep `confidenceLevel=insufficient_data` but clear `confidenceScore` and `reliabilityPct` instead of preserving the signal score.
2. Separate outcome sample size from decision confidence: a small-sample or incomplete outcome summary may remain visible, but its `confidenceScore` must not be `0` or another numeric value that looks like recommendation confidence.
3. Add focused regression coverage for at least:
   - blocked inventory with signal evidence;
   - workflow-only inventory fallback;
   - outcome summary with `MeasuredSampleSize < 10`;
   - one healthy inventory or outcome counterexample that keeps legitimate confidence behavior unchanged.
4. Update the owning Decision Board/inventory contract docs to match the fixed semantics without redesigning the broader board DTO.

### Tests

- `git diff --check`;
- focused `dotnet test` for `DecisionBoardEndpointsTests` and `DecisionBoardAggregationContractTests`;
- focused frontend board test only if render behavior changes;
- governance validators when queue/docs change.

### Acceptance

- Inventory cards with `recommendationAllowed=false` cannot carry numeric decision confidence or reliability through the Decision Board API.
- Outcome summary cards with insufficient sample do not expose `confidenceScore=0` or another numeric confidence surrogate; sample size stays visible only as sample/coverage context.
- Blocked/insufficient non-product Decision Board cards remain visibly blocked without introducing fake zero, fake confidence, or frontend-owned scoring.

### Dependencies

- `RQ13` is historical DONE and may be refined here only within the same Decision Board confidence family.
- No production mutation, worker configuration, or formula rewrite is authorized in this prompt.

### Completion note

- Date: 2026-08-28
- Status: DONE
- Completion: cleared fake Decision Board confidence on blocked inventory cards by nulling decision confidence/reliability when `recommendationAllowed=false`, separated outcome sample size from `confidenceScore`, and added focused backend regression coverage for blocked inventory and insufficient-sample outcome summaries.
- Changed files: `Api/Endpoints/DecisionBoardEndpoints.cs`; `Api.Tests/DecisionBoardEndpointsTests.cs`; `Api.Tests/DecisionBoardAggregationContractTests.cs`; `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-08-28-RQ129-evidence.md`
- Contract/runtime behavior changed: yes; blocked inventory Decision Board cards now keep `confidenceLevel=insufficient_data` while returning `confidenceScore=null` and `reliabilityPct=null`, and insufficient-sample `actionsOutcome` summaries keep sample context in copy instead of exposing numeric pseudo-confidence
- Checks run: `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DecisionBoardEndpointsTests|FullyQualifiedName~DecisionBoardAggregationContractTests"` (pass); `git diff --check` (pass); `node scripts/check-agent-instructions.mjs --self-test` (pass); `node scripts/check-agent-instructions.mjs` (pass); `node scripts/check-prompt-queues.mjs --self-test` (pass); `node scripts/check-prompt-queues.mjs` (pass); `node scripts/check-planning-architecture.mjs --self-test` (pass); `node scripts/check-planning-architecture.mjs` (pass)
- Checks not run: full solution build/test not run; live production recheck not run in this prompt
- Run log: `.ai/runs/2026-08-28-RQ129-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `08abe2bff58c561f64e3c58ea231d249376c6af9`
- Main verification: `passed - origin/main contains 08abe2bff58c561f64e3c58ea231d249376c6af9`
- Missed: no live redeploy/runtime verification was attempted here because this prompt only corrected the backend contract/tests/docs on current `main`
- Follow-up: no additional RQ prompt is READY; `RQ128` remains `WAITING` on `STAB16`
- Residual risk: the production API will continue showing the old numeric values until the updated backend runtime is deployed on the active Decision Board environment
- Prompt defect / scope repair: historical completion-note blocks for earlier RQ prompts were already adjacent below this section before this claim; they were preserved to avoid a broader queue-structure rewrite inside this same-owner contract fix

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: surfaced backend-owned trust payload on dashboard legacy/advanced action cards by extending the dashboard action DTO, preserving actionable/blocked/legacy trust states in the advanced fallback bridge, and proving the new rendering contract in backend and frontend regression tests.
- Changed files: `Api/Endpoints/CachedAnalyticsEndpoints.cs`; `Api.Tests/CachedAnalyticsDashboardActionTrustTests.cs`; `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`; `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx`; `Klijent/clientapp/src/types/analytics.ts`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-08-26-RQ124-evidence.md`
- Contract/runtime behavior changed: dashboard legacy/advanced action cards now carry explicit trust metadata instead of collapsing to one generic `insufficient_data` fallback, while legacy/unavailable fallback still stays explicit when trust payload is missing
- Checks run: `git diff --check` (pass); `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CachedAnalyticsDashboardActionTrustTests|FullyQualifiedName~CachedAnalyticsOperationalFallbackTests"` (pass); `npm run test:run -- src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx` (pass)
- Checks not run: full solution build/test, live browser smoke, remote main verification
- Run log: `.ai/runs/2026-08-26-RQ124-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: pending
- Main verification: pending
- Missed: the broader cross-surface trust/freshness lanes remain for RQ125-RQ127
- Follow-up: `RQ125` is now READY
- Residual risk: legacy dashboard action cards still rely on the advanced fallback bridge when Product Decision rows are absent, so any future backend schema drift should be caught by the new trust-state regression test before it reaches the UI

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: Added backend-owned row-trust payload to advanced top-product DTOs, surfaced it in the dashboard margin column as explicit trust badges/details, and added backend/frontend contract coverage for good vs insufficient-data rows.
- Changed files: Api/Endpoints/CachedAnalyticsEndpoints.cs; Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs; Klijent/clientapp/src/pages/AnalyticsDashboard.tsx; Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.tableSystem.spec.tsx; Klijent/clientapp/src/types/analytics.ts; docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md; MASTER_ROADMAP.md; .ai/runs/2026-08-26-RQ121-evidence.md
- Contract/runtime behavior changed: yes; dashboard margin rows now show a trust badge and explanatory detail instead of generic fallback copy
- Checks run: npm run test:run -- src/pages/__tests__/AnalyticsDashboard.tableSystem.spec.tsx (pass); dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests.TopProducts_ExposesMarginTrustPayloadForDashboardRows" (pass)
- Checks not run: full solution build; broader suite; direct cached top-products-advanced endpoint against InMemory factory because that route requires relational SQL behavior
- Run log: .ai/runs/2026-08-26-RQ121-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: pending
- Main verification: pending
- Missed: supplier trust payload remains the next lane in RQ122; no formula/ranking rewrite was attempted
- Follow-up: RQ122 READY
- Residual risk: advanced top-products data still depends on the existing SQL path for real runtime data; the new trust payload itself is derived conservatively from margin-impact availability
- Prompt defect / scope repair: the cached advanced top-products route cannot be exercised end-to-end in the InMemory integration factory, so the backend check was shifted to a serialization contract test tied to the actual DTO namespace

---

## RQ122 - Surface backend-owned trust state on supplier summary/quadrant/header recommendations

Status: DONE
Completed after: `RQ112` and `RQ120` are `DONE`, or the owner explicitly promotes the supplier trust-payload lane
Priority: P1
Type: backend-frontend-contract/tests
Feature family: supplier-decision-recommendation-trust-payload
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ122-<agent>.lock.md`
Commit suggestion: `fix(analytics): surface supplier recommendation trust`

### Problem

Supplier Decision Hub SQL/tests already preserve missing-evidence guardrails, but the API/UI still hide part of that trust context on summary cards, quadrant items, and supplier header payloads. Operators can see a recommendation, revenue, and confidence label without the backend-owned reliability/data-quality/status-reason context that proves whether the recommendation is actually decision-safe.

### Evidence

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs` still carries backend DTO TODOs for `SummarySupplierItem`, `QuadrantItem`, `RankingItem`, and `SupplierHeaderDto` to expose recommendation quality payload and margin-quality context.
- `Klijent/clientapp/src/services/supplierDecisionHubApi.ts` omits reliability/data-quality/status-reason fields from `SummarySupplierItem`, `QuadrantItem`, and `SupplierHeaderDto`, even though `RankingItem` already carries part of that vocabulary.
- `Klijent/clientapp/src/components/supplierDecisionHub/SupplierRecommendationRail.tsx` currently shows revenue and confidence copy but no explicit trust/degradation reason.
- `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md` notes that supplier-decision SQL already keeps explicit cost-coverage / missing-evidence flags and conservative `REVIEW_QUALITY` fallback.

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: surfaced backend-owned supplier recommendation trust on summary, quadrant and header contracts; rendered the trust context in the recommendation rail, quadrant tooltip and supplier detail header; and added focused contract/UI tests so the backend-owned reliability, data-quality and status-reason payload no longer disappears between API and UI.
- Changed files: `Api/Endpoints/SupplierDecisionHubEndpoints.cs`; `Api.Tests/SupplierDecisionHubContractTests.cs`; `Klijent/clientapp/src/services/supplierDecisionHubApi.ts`; `Klijent/clientapp/src/services/__tests__/supplierDecisionHubApi.spec.ts`; `Klijent/clientapp/src/components/supplierDecisionHub/SupplierRecommendationRail.tsx`; `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDecisionQuadrant.tsx`; `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDetailDrawer.tsx`; `Klijent/clientapp/src/components/supplierDecisionHub/SupplierRecommendationRail.spec.tsx`; `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDetailDrawer.spec.tsx`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-08-26-RQ122-evidence.md`
- Contract/runtime behavior changed: supplier decision summary items, quadrant items and supplier header payloads now carry backend-owned reliability/data-quality/status-reason/reason-codes fields; the rail, quadrant tooltip and header drawer now render the trust state explicitly instead of implying stronger confidence by omission
- Checks run: `git diff --check` (pass); `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SupplierDecisionHubContractTests"` (pass); `npm run test:run -- src/components/supplierDecisionHub/SupplierRecommendationRail.spec.tsx src/components/supplierDecisionHub/SupplierDetailDrawer.spec.tsx src/services/__tests__/supplierDecisionHubApi.spec.ts` (pass); `npm run typecheck` (pass)
- Checks not run: full solution build/test, live browser smoke, remote main verification
- Run log: `.ai/runs/2026-08-26-RQ122-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `569705f11ba0db22fcb0e13b88c1ca7c3a971878`
- Main verification: `git branch --contains 569705f11ba0db22fcb0e13b88c1ca7c3a971878 -> * main`
- Missed: no dedicated hover regression for the quadrant tooltip itself; the new trust text is covered by component-level rendering and contract tests
- Follow-up: none for this prompt; `RQ123` remains `WAITING`
- Residual risk: older API consumers that do not yet send the new trust fields will need to be upgraded to avoid empty trust lines in the new UI surfaces

### Scope

- Supplier Decision Hub summary/quadrant/header DTOs and frontend contracts;
- the rail/header rendering and nearest contract tests;
- no supplier score weighting rewrite, no SQL score formula rewrite, and no report-template redesign.

### Read first

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`;
- `Klijent/clientapp/src/services/supplierDecisionHubApi.ts`;
- `Klijent/clientapp/src/components/supplierDecisionHub/SupplierRecommendationRail.tsx`;
- `docs/qa/ANALYTICS_SUPPLIER_SUMMARY_DETAIL_RECONCILIATION_2026-08-24.md`;
- `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`.

### Do

1. Expose the smallest additive backend-owned recommendation trust payload on summary, quadrant, and header contracts.
2. Render degraded/partial/review-quality states explicitly instead of leaving the rail/header to imply stronger trust than the backend proved.
3. Preserve existing recommendation codes and confidence semantics; do not invent frontend fallback formulas.
4. Add focused tests for good, degraded/review-quality, and unavailable trust payloads.

### Tests

- `git diff --check`;
- focused Supplier Decision Hub contract tests;
- focused frontend tests for rail/header trust rendering;
- governance validators if queue/docs change.

### Acceptance

- Supplier summary/quadrant/header recommendations can show backend-owned trust state or explicit unavailable semantics.
- A recommendation with partial or review-quality evidence no longer looks like a plain high-confidence action by omission.
- Frontend types stop hiding trust fields that already belong to the backend contract.

### Dependencies

- `RQ112` and `RQ120` DONE, or explicit owner promotion.

---

## RQ123 - Prove report-generation freshness/cache-version truth for pilot reports

Status: DONE
Completed after: `RQ112` is `DONE` or the owner explicitly reprioritizes report freshness truth
Priority: P1
Type: backend/tests/docs
Feature family: analytics-report-cache-generation-truth
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ123-<agent>.lock.md`
Commit suggestion: `fix(analytics): prove report freshness truth`

### Problem

Pilot supplier/data-quality reports now have reconciled numbers and stable URLs, but the current evidence still leaves one trust gap: report generation itself does not prove a report cache-version bump or another freshness guarantee. A report can therefore be numerically correct for some earlier refresh yet still appear freshly generated without a fully tested cache/freshness contract.

### Evidence

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md` lists as the highest-risk finding that report generation does not rotate report cache version on its own and still depends on import/nightly/data-quality/admin invalidation.
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md` still names report cache version bump as part of the supplier-decision/report owner chain.
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs` proves that explicit report-family invalidation bumps the report version, but not that report generation itself truthfully refreshes freshness semantics.
- `docs/qa/ANALYTICS_BACKEND_TEST_COVERAGE_PHASE2_2026-07-02.md` proves stable report URLs and report-cache invalidation exist, but not the exact on-demand generation freshness contract.

### Completion note

- Date: 2026-08-26
- Status: DONE
- Completion: proved the supplier report freshness contract by asserting that report generation and last authoritative refresh are exposed as separate facts, that the report payload metadata carries both timestamps, and that the report cache version contract still cleanly separates cached generation from refresh truth.
- Changed files: `Api.Tests/AnalyticsReportsContractTests.cs`; `Klijent/clientapp/src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-08-26-RQ123-evidence.md`
- Contract/runtime behavior changed: no runtime formula changed; the supplier report contract now explicitly proves generated-vs-refreshed freshness semantics and the report UI test verifies both timestamps are presented separately
- Checks run: `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsReportsContractTests|FullyQualifiedName~AnalyticsCacheAdminServiceTests"` (pass); `npm run test:run -- src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx` (pass)
- Checks not run: full solution build/test, live browser smoke, remote main verification
- Run log: `.ai/runs/2026-08-26-RQ123-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `1e2f5539f6b7884bddb08e3b5272f47d39ac6f10`
- Main verification: `git branch --contains 1e2f5539f6b7884bddb08e3b5272f47d39ac6f10 -> * main`
- Missed: no new runtime report cache invalidation behavior was added; the contract was proven rather than altered
- Follow-up: `RQ124` is now READY
- Residual risk: the runtime still relies on existing cache-version rotation from administrative or refresh-family paths; this task only proved the contract truthfully

### Scope

- one pilot report family (`/analytics/supplier/report` or `/analytics/reports/pilot-intake`) and its cache-version/freshness path;
- the nearest cache/report contract tests plus one QA doc note;
- no broad cache-family redesign beyond the selected report truth contract.

### Read first

- `docs/qa/ANALYTICS_CACHE_INVALIDATION_AUDIT.md`;
- `docs/qa/ANALYTICS_PILOT_SCREEN_DATA_AVAILABILITY_MATRIX.md`;
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs`;
- `Api.Tests/AnalyticsReportsContractTests.cs`;
- the selected report endpoint/cache path.

### Do

1. Decide the truthful contract: either report generation rotates/invalidates the report family when needed, or generation remains read-only but must expose freshness as inherited from the last authoritative refresh.
2. Encode that contract in focused tests so “generated now” cannot be mistaken for “refreshed from source now”.
3. If generation is intentionally read-only, surface/document the exact freshness/version semantics rather than relying on inference.
4. Keep the fix scoped to one report family; split any second report lane into a follow-up.

### Tests

- `git diff --check`;
- focused report/cache contract tests;
- governance validators if queue/docs change.

### Acceptance

- The selected pilot report family has a citeable freshness/cache-version contract.
- Report generation no longer implies a stronger freshness guarantee than the system can prove.
- Operators can tell whether a report is newly rendered, newly refreshed, both, or neither.

### Dependencies

- `RQ112` DONE or explicit owner reprioritization.

---

## RQ124 - Expose backend-owned trust payload on dashboard legacy/advanced action cards

Status: DONE
Completed after: `RQ120` is `DONE` or the owner explicitly promotes the dashboard action-trust lane
Priority: P1
Type: backend-frontend-contract/tests
Feature family: analytics-dashboard-action-trust-payload
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ124-<agent>.lock.md`
Commit suggestion: `fix(analytics): surface dashboard action trust payload`

### Problem

The dashboard still carries a thin legacy/advanced action contract. `DashboardActionDto` exposes only priority/title/recommendation, while the dashboard action UI and bridge layer already reason about confidence, reliability, recommendation gating, data-quality state, and status reason. When Product Decision rows are unavailable, `BuildDashboardDecisionActions(...)` maps legacy advanced actions into generic helper signals with `RecommendationAllowed=false`, `DataQualityStatus="insufficient_data"`, and null confidence/reliability values. That keeps the UI fail-closed, but it also hides whether the action is truly blocked, merely stale, or actually backed by a known validation condition.

### Evidence

- `Klijent/clientapp/src/types/analytics.ts` still carries `TODO(backend-dto): add confidence/reliability/dataQualityStatus/statusReason to dashboard actions`.
- `Api/Endpoints/CachedAnalyticsEndpoints.cs` defines `DashboardActionDto` with only `Priority`, `Title`, and `Recommendation`.
- The same file maps `advancedSnapshot.Actions` into `DashboardDecisionActionDto` by forcing generic fallback trust fields: `RecommendationAllowed = false`, `DataQualityStatus = "insufficient_data"`, `ConfidencePct = null`, `ReliabilityPct = null`, and `StatusReason = action.Recommendation`.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx` renders decision/action cards with explicit trust UI (`confidencePct`, `reliabilityPct`, `dataQualityStatus`, `statusReason`), so the current bridge can make every legacy advanced action look equally blocked even when the originating signal was more specific.

### Scope

- `DashboardActionDto` and the advanced snapshot action builder/bridge in `CachedAnalyticsEndpoints.cs`;
- the dashboard TypeScript contract and nearest dashboard action rendering/tests;
- no ranking-score rewrite, no Product Decision formula rewrite, and no Decision Board contract redesign outside the dashboard action payload.

### Read first

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`;
- `Klijent/clientapp/src/types/analytics.ts`;
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`;
- `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx`;
- `docs/qa/ANALYTICS_DATA_RELIABILITY_AUDIT.md`.

### Do

1. Decide the smallest truthful additive payload for legacy/advanced dashboard actions: real recommendation gate/trust fields when the backend knows them, otherwise an explicit unavailable/legacy fallback state that is distinct from a proven blocked recommendation.
2. Propagate that payload through the backend DTO and frontend type without inventing frontend-only scoring or silently upgrading trust.
3. Replace the generic fallback mapping so helper actions preserve why they are limited: stale validation, missing evidence, workflow-only fallback, or another explicit backend-owned reason.
4. Add focused regression coverage for a healthy/actionable action, an explicitly limited helper signal, and a legacy fallback action with unavailable trust payload.

### Tests

- `git diff --check`;
- focused dashboard backend/contract tests for advanced action mapping;
- focused Vitest coverage for dashboard action trust rendering/fallback behavior;
- governance validators if queue/docs change.

### Acceptance

- Dashboard legacy/advanced action cards no longer collapse every thin payload into the same generic `insufficient_data` helper state.
- The UI can distinguish actionable trust, blocked trust, and unavailable/legacy fallback semantics without inventing local confidence.
- Backend-owned action trust metadata is visible or explicitly unavailable, not silently implied by generic copy.

### Dependencies

- `RQ120` DONE or explicit owner promotion.

---

## RQ136 - Preserve truth in analytics action messages and notifications

Status: WAITING
Priority: P1
Type: backend/contract/frontend/tests
Feature family: analytics-action-notification-truth
Parallel-safe: no, shared action semantics require one owner
Owner: Codex
Commit suggestion: `fix(analytics): align action messages with trust metadata`

### Problem

Analytics messages, notifications and action labels must describe the backend decision state that the data supports. A user must not receive an actionable or success-looking message when the result is empty, stale, degraded, fallback, insufficient or failed.

### Evidence

- Core analytics invariants require a strict distinction between error, empty, warning/degraded and actionable success.
- Backend decision metadata is the source of truth; the frontend must not reconstruct confidence or recommendation status.
- Existing queue work closed several page-level empty/error cases, but cross-surface action and notification wording still needs an explicit parity proof.

### Scope

- the owning backend response/meta contract for action state, reason, confidence/reliability and data quality;
- the shared frontend mapping for action labels, toast/notification text and empty/error states;
- focused backend and frontend tests plus one evidence note.

Do not change recommendation formulas, introduce new notification channels, or add fake defaults for missing fields.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- the shared analytics response/meta contract
- current action/notification mapping and nearest focused tests

### Do

1. Inventory every user-visible message for the selected action surface and map it to a backend state.
2. Define explicit copy for actionable, insufficient, empty, stale/degraded, fallback and error states.
3. Ensure unknown or missing decision metadata blocks actionable copy instead of falling back to zero, success or generic confidence.
4. Keep Serbian text and established formatters intact; do not duplicate business scoring in React.
5. Add counterexample tests for stale, empty, failed and insufficient payloads.

### Tests

- focused backend contract tests for each state;
- focused Vitest tests for message/notification mapping;
- analytics guardrail check;
- typecheck and build when shared frontend code changes;
- `git diff --check` and queue validators.

### Acceptance

- Every user-visible action message has a proven backend state mapping.
- Error and unknown never render as valid zero or success.
- Empty remains distinct from error, and degraded/fallback remains visible.
- Tests cover both actionable and blocked/counterexample states.
- No recommendation formula or worker/infrastructure change is introduced.

### Dependencies

- `STAB16` must provide production liveness/freshness evidence before this is claimed as live pilot proof.
- `RQ128` remains the primary post-STAB actionability parity lane; reuse it rather than duplicating its live scope.
- This prompt is a later focused contract candidate, not current `READY`.

---

## RQ137 - Align requested, effective and observed period truth across analytics surfaces

Status: PARTIAL
Priority: P0
Type: backend/contract/frontend/tests
Feature family: analytics-period-lineage-parity
Parallel-safe: no, shared period semantics must stay under one owner
Owner: Codex
Commit suggestion: `fix(analytics): align period lineage across trust surfaces`

### Problem

Dashboard bootstrap, Pilot Readiness / Pilot Intake, and Supplier Decision report surfaces still expose period truth through different fields and fallback rules. A user can therefore see a requested range in one place, an observed data window in another, and a generated/report period elsewhere without one explicit canonical lineage.

### Evidence

- `.ai/runs/2026-09-03-analytics-followup-audit-evidence.md` recorded that supplier all-history reports needed the observed data period instead of synthetic default bounds and that cross-endpoint period alignment remains a separate contract follow-up.
- `.ai/runs/2026-09-03-pilot-readiness-truthfulness-evidence.md` recorded that dashboard bootstrap, intake, and supplier report endpoints currently expose different periods/denominators, so the UI must not treat them as interchangeable.
- Core analytics invariants require requested/effective period truth, visible fallback state, and no fake “last refreshed” timestamp derived from query generation.

### Scope

- the smallest backend-owned period lineage contract across the selected analytics endpoints and DTOs;
- the frontend trust/report surfaces that render requested, effective, observed, generated and last-successful-refresh facts;
- focused backend/frontend regression tests and one evidence note.

Do not rewrite recommendation formulas, move worker ownership into the web process, or merge unrelated data-quality scoring changes.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `.ai/runs/2026-09-03-analytics-followup-audit-evidence.md`
- `.ai/runs/2026-09-03-pilot-readiness-truthfulness-evidence.md`
- the current dashboard bootstrap, pilot intake/report, supplier report DTO/page contracts and nearest focused tests

### Do

1. Inventory the current requested/effective/observed/generated/refresh fields for the selected dashboard, readiness/report and supplier-report surfaces.
2. Define one backend-owned lineage vocabulary: requested period, effective calculation period, observed data period when they differ, generated-at, and last successful refresh.
3. Fail closed when the effective or observed range cannot be proven; show unknown/degraded/fallback explicitly instead of synthetic bounds or query-time refresh labels.
4. Keep cards, details, table/export/report and trust headers on the same period contract for each chosen surface.
5. Add counterexample tests for bounded vs all-history, wrong-scope/wrong-period fallback, missing refresh history, and generated-at vs refresh parity.

### Tests

- focused backend contract tests for the selected endpoints/DTOs;
- focused Vitest/report page tests for visible period lineage and fallback copy;
- analytics guardrail check;
- frontend build if shared DTO/page contracts change;
- `git diff --check` and queue validators.

### Acceptance

- Requested, effective and observed period facts are distinguishable and consistent across the changed analytics surfaces.
- No report or trust header presents query generation time as the last successful refresh.
- Unknown/fallback/partial period state remains visible and user-readable.
- Export/report/detail surfaces do not drift from the visible page period contract.

### Completion note

- Date: 2026-09-04
- Status: PARTIAL
- Completion: dashboard bootstrap, pilot readiness/intake, and supplier decision report surfaces now share explicit requested/effective/observed period lineage fields, keep generated-at separate from last successful refresh, and render the observed-period explanation only when the backend proves it differs from the effective calculation window
- Changed files: `Api/Dtos/AnalyticsResponseMetaDto.cs`; `Api/Dtos/AnalyticsReportResponseDto.cs`; `Api/Endpoints/CachedAnalyticsEndpoints.cs`; `Api/Endpoints/DataQualityEndpoints.cs`; `Api/Endpoints/SupplierDecisionHubEndpoints.cs`; `Api.Tests/AnalyticsSalesReadinessRegressionTests.cs`; `Api.Tests/AnalyticsReportsContractTests.cs`; `Klijent/clientapp/src/types/analytics.ts`; `Klijent/clientapp/src/utils/analyticsPeriodLineage.ts`; `Klijent/clientapp/src/utils/__tests__/analyticsPeriodLineage.spec.ts`; `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`; `Klijent/clientapp/src/pages/PilotReadinessPage.tsx`; `Klijent/clientapp/src/pages/__tests__/PilotReadinessPage.edgeCases.spec.ts`; `Klijent/clientapp/src/components/analytics/SupplierDecisionReport.tsx`; `Klijent/clientapp/src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx`; `Klijent/clientapp/src/services/supplierDecisionReport.ts`; `Klijent/clientapp/src/services/__tests__/supplierDecisionReport.spec.ts`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `.ai/runs/2026-09-04-RQ137-evidence.md`
- Contract/runtime behavior changed: yes - the selected analytics trust/report surfaces now expose backend-owned period lineage and no longer substitute query-generation time for refresh truth
- Checks run: `git diff --check`; `node scripts/check-agent-instructions.mjs --self-test`; `node scripts/check-agent-instructions.mjs`; `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`; `npm run test -- --run src/utils/__tests__/analyticsPeriodLineage.spec.ts src/pages/__tests__/PilotReadinessPage.edgeCases.spec.ts src/components/analytics/__tests__/SupplierDecisionReport.spec.tsx src/services/__tests__/supplierDecisionReport.spec.ts`; `npm run check:analytics-guardrails`; `npm run build`; `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsSalesReadinessRegressionTests|FullyQualifiedName~AnalyticsReportsContractTests"`
- Checks not run: full solution `dotnet build`; full solution `dotnet test`; browser/live console smoke; production/live freshness verification from `STAB16`
- Run log: `.ai/runs/2026-09-04-RQ137-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 29a5943ad606c67721e931d73fd5906b49c9ade3
- Main verification: passed - origin/main contains 29a5943ad606c67721e931d73fd5906b49c9ade3
- Missed: delivery to `main` and live-runtime proof remain out of scope for this local queue execution
- Follow-up: `STAB16` for live freshness proof, then `RQ128` for broader post-stabilization actionability parity
- Residual risk: other analytics surfaces outside the selected dashboard/readiness/supplier-report path still rely on their own existing period contracts and were not revalidated here
- Next: none
- Prompt defect / scope repair: because the queue had no current `READY` prompt, `RQ137` was locally promoted as the smallest same-owner owner-bounded period-lineage repair candidate and is now returned to non-runnable `PARTIAL` truth after local proof

### Dependencies

- `STAB16` remains the live-runtime/deploy proof owner; do not duplicate production deploy verification here.
- This prompt remains `PARTIAL` and non-runnable; live freshness/deployment proof stays with `STAB16`.

---

## RQ138 - Add an authoritative Trend Models evaluation contract before numeric claims return

Status: PARTIAL
Priority: P1
Type: backend/contract/frontend/tests
Feature family: trend-model-evaluation-contract
Parallel-safe: no, score semantics must remain backend-owned
Owner: Codex
Commit suggestion: `feat(analytics): add trend model evaluation contract`

### Problem

The dashboard Trend Models panel is now fail-closed, but there is still no authoritative endpoint or DTO that defines what a model score means, which period/sample it covers, or whether it is current enough to trust. Numeric model accuracy must not return until that contract exists.

### Evidence

- `.ai/runs/2026-09-03-trend-model-truthfulness-evidence.md` proved that the prior Trend Models values were hardcoded placeholders with no backend endpoint, period, sample, or evaluation result.
- `RQ108`, `RQ117`, and the forecast/backtest chain already established foundation work for forecast materialization and observed pairing, but they do not yet expose a user-facing model evaluation contract on the dashboard.
- Core analytics invariants require backend ownership for score/confidence/recommendation semantics and explicit freshness/limitations before display.

### Scope

- the smallest backend-owned registry/evaluation DTO and endpoint, if an authoritative evaluation source now exists;
- the dashboard Trend Models UI mapping and tooltip/copy for available vs unavailable evaluation;
- focused backend/frontend tests and one evidence note.

Do not invent scores from frontend heuristics, backfill fake history, or mix scenario-planning/runtime forecast work beyond the chosen evaluation contract.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `.ai/runs/2026-09-03-trend-model-truthfulness-evidence.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` sections for `RQ108` and `RQ117`
- the current Trend Models component and nearest focused tests

### Do

1. Confirm whether an authoritative backend evaluation source exists; if not, stop with a bounded docs/evidence update rather than recreating placeholder scores.
2. When a source exists, define explicit fields for metric name, score/unit, evaluated sample/window, freshness, last evaluated at, and limitation/warning state.
3. Keep unavailable evaluation fail-closed: no numeric accuracy, no percent delta, no fake confidence.
4. Map the dashboard panel to the backend contract and keep explanatory copy user-readable in Serbian.
5. Add tests for available evaluation, unavailable evaluation, stale evaluation and malformed/missing score payloads.

### Tests

- focused backend tests for the evaluation DTO/endpoint if one is added;
- focused Vitest for the Trend Models component mapping;
- analytics guardrail check;
- frontend build and changed backend project build if the contract changes;
- `git diff --check` and queue validators.

### Acceptance

- Trend Models show numeric evaluation only from an authoritative backend contract.
- Period/sample/freshness/limitations are visible whenever a numeric score is shown.
- Missing, stale or malformed evaluation remains explicitly unavailable instead of falling back to placeholder numbers.

### Completion note

- Date: 2026-09-04
- Status: PARTIAL
- Completion: Trend Models now consume the backend forecast backtest contract instead of hardcoded placeholders; the contract exposes freshness, last-evaluated, baseline label, and backend-owned metric metadata, while the dashboard keeps stale, unavailable, and malformed evaluation fail-closed and shows numerics only for `ready` + authoritative + non-stale payloads
- Changed files: `Application/Analytics/Queries/GetForecastBaselineBacktest/ForecastBaselineBacktestContract.cs`; `Application/Analytics/Queries/GetForecastBaselineBacktest/GetForecastBaselineBacktestQuery.cs`; `Application/Analytics/Queries/GetForecastBaselineBacktest/GetForecastBaselineBacktestHandler.cs`; `Api.Tests/ForecastBaselineBacktestContractTests.cs`; `Klijent/clientapp/src/types/analytics.ts`; `Klijent/clientapp/src/services/analyticsApi.ts`; `Klijent/clientapp/src/components/dashboard/TrendModelList.tsx`; `Klijent/clientapp/src/components/dashboard/TrendModelList.spec.tsx`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `.ai/runs/2026-09-04-RQ138-evidence.md`
- Contract/runtime behavior changed: yes - Trend Models no longer present static descriptive placeholders as the only source of truth; they now render backend-owned evaluation state and fail closed when trust conditions are not met
- Checks run: `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~ForecastBaselineBacktestContractTests"`; `dotnet build .\Api\Api.csproj`; `npm run test -- --run src/components/dashboard/TrendModelList.spec.tsx`; `npm run check:analytics-guardrails`; `npm run build`; `git diff --check`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs`
- Checks not run: full solution `dotnet test`; browser/live console smoke; production/live model-evaluation proof
- Run log: `.ai/runs/2026-09-04-RQ138-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 29a5943ad606c67721e931d73fd5906b49c9ade3
- Main verification: passed - origin/main contains 29a5943ad606c67721e931d73fd5906b49c9ade3
- Missed: no authoritative measured `ready` payload is materialized from production data yet; the backend contract still truthfully defaults to unavailable until that runtime source exists
- Follow-up: a later evaluation-materialization prompt can reuse this contract instead of inventing new dashboard semantics
- Residual risk: the workspace contains unrelated in-flight analytics changes; this prompt proves only the trend-model evaluation contract path listed above
- Next: none
- Prompt defect / scope repair: because the queue had no current `READY` prompt, `RQ138` was locally promoted as the smallest same-owner trend-evaluation contract follow-up and is now returned to non-runnable `PARTIAL` truth after local proof

### Dependencies

- Reuse the forecast/backtest foundation from `RQ108` and `RQ117`; do not duplicate that lower-layer provenance work.
- This prompt remains `PARTIAL` and non-runnable until a real measured evaluation source is available.

---

## RQ139 - Prove analytics denominator, null and zero semantics across every decision surface

Status: READY
Priority: P0
Type: backend/contract/frontend/tests
Feature family: analytics-denominator-null-zero-contract
Parallel-safe: no, this is the shared numeric trust contract
Owner: Codex
Commit suggestion: `fix(analytics): fail closed on missing numeric evidence`

### Problem

Several analytics paths still use a numeric zero as a compatibility/default value when the underlying signal is missing. This can turn missing cost, missing velocity, missing margin, missing supplier/stock coverage or a missing denominator into a valid-looking KPI, score, forecast, ranking value or action. The contract must distinguish a real zero from unknown, not applicable, insufficient evidence and calculation failure.

### Evidence

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts:155-191` derives approximate units/revenue and averages with `?? 0` and `Math.max(..., 1)`; `:217-260`, `:280-310`, `:325-350` and `:377-418` repeat this for price, aging, depletion and reorder outputs.
- `Application/Analytics/Services/TrendScoringService.cs:216-217` returns momentum `1.0` when either score is missing, and `:265-271` returns index `0.0` for an empty/positive-score-free input.
- `Application/Analytics/Services/TrendScoringService.cs:307-315` treats missing social input as zero and `:347-362` returns recommended order quantity zero for non-positive velocity without distinguishing true zero demand from unavailable/invalid velocity.
- `Api/Services/PreNivelacijaScoringService.cs:63-79`, `:198-205` and `:223-240` use zero/midpoint fallbacks for zero normalization spans and unknown confidence; `:208-214` clamps scenario units to at least one, so no evidence can create a positive scenario.
- `Api/Endpoints/AllEndpoints.cs:3338-3346`, `:3401-3409` and `:3510-3517` coalesce pre/post quantity, revenue, coverage and change percent to zero before the response is built.
- Existing RQ work fixed selected Daily Sales, supplier and shoe-type cases, but `RQ137` explicitly records that other analytics surfaces were not revalidated and `RQ138` records that measured evaluation data is still unavailable.

### Scope

- A canonical numeric-state contract for currency, quantity, ratios/percentages, rates, scores, confidence/reliability and dates.
- Backend DTO/meta fields that preserve `unknown`, `missing`, `insufficient`, `not_applicable`, `error` and `valid_zero` without overloading numeric zero.
- Frontend shared mapping/formatting for all analytics pages, cards, tables, charts, details, action lists, exports and reports.
- The affected sales, trend, forecast, inventory, supplier, data-quality and pre/post nivelacija calculations; do not silently limit the repair to one page.

Do not edit the raw vendor nivelacija SQL/reader branch owned by `Q83`; consume its additive contract after that SQL prompt lands. This keeps the independently runnable numeric-state work disjoint from the SQL owner path.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` sections `RQ02`, `RQ03`, `RQ04`, `RQ10`, `RQ137` and `RQ138`
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- `Application/Analytics/Services/TrendScoringService.cs`
- `Api/Services/PreNivelacijaScoringService.cs`
- nearest backend/frontend tests for each changed calculation

### Do

1. Build a formula inventory with metric name, unit, numerator, denominator, source fields, valid-zero rule, missing rule, minimum evidence and owner. Include every `?? 0`, `?? 100`, `|| 0`, `Math.max(..., 1)`, epsilon and synthetic sentinel found on the mapped surfaces.
2. Replace only semantics-proven fallbacks. A missing denominator must produce null/unknown/insufficient metadata, never a share of zero or 100. A true measured zero must remain zero. NaN and Infinity must be rejected before serialization/rendering.
3. Remove frontend reconstruction of trusted revenue, margin, forecast, risk, score and recommendation values where a backend contract exists. If a legacy fallback must remain, label it degraded and keep it out of recommendation/actionable ranking.
4. Preserve empty-success separately from backend error; keep stale, fallback, partial and insufficient states visible and user-readable.
5. Keep backward compatibility only through additive nullable/meta fields or an explicitly versioned contract. Do not hide a changed business meaning behind the old numeric field.

### Tests

- Backend unit/contract tests for empty result, null input, a genuine valid zero, missing denominator, NaN, Infinity, negative/invalid input and zero normalization span.
- Frontend tests for the same cases through card, table, chart, detail, action, export and report adapters.
- Tests proving no unavailable value becomes a score, confidence, reliability, forecast, revenue share, reorder quantity or recommendation.
- Tests for stale and unknown freshness, partial/fallback response, wrong period and wrong scope where the numeric state is displayed.
- `npm run test -- --run <changed analytics specs>`; the nearest targeted `dotnet test`; analytics guardrail; `git diff --check` and queue validators.

### Acceptance

- Every audited metric has an explicit numerator/denominator and state contract.
- `null`, unknown, missing, insufficient, NaN and Infinity cannot render as a trusted zero or produce an allowed recommendation.
- A valid zero remains visible as zero and is not confused with no evidence.
- Backend values and states are identical across card, table, chart, details, export and report for the same query.
- Focused regression tests fail against the pre-fix behavior for all required counterexamples.

### Dependencies

- `RQ137` and `RQ138` remain partial/non-runnable; their existing fields may be reused, but completing them is not a prerequisite for this bounded numeric-state work.
- `Q83` is independently promoted for the raw vendor nivelacija SQL path; `RQ139` must not edit those SQL/reader files in parallel.
- `STAB16` remains the owner of production worker/live refresh access. This prompt may use deterministic fixtures and current runtime contracts but must not claim live proof without that evidence.
- Later prompts `RQ140`-`RQ146` must reuse this numeric-state vocabulary rather than creating local exceptions.

---

## RQ140 - Prove pre/post nivelacija effects are comparable and not availability artifacts

Status: WAITING
Priority: P0
Type: backend/SQL/contract/frontend/tests
Feature family: pre-post-nivelacija-causal-comparability
Parallel-safe: no, pre/post semantics are shared by sales and decision screens
Owner: Codex
Commit suggestion: `fix(analytics): harden pre-post nivelacija comparability`

### Problem

Pre/post nivelacija screens expose revenue, units, margin and impact signals, but a delta after a price change is not evidence of a price effect when the article set, stock availability, observation window, cost coverage or event timing differs. Current SQL compatibility branches also coalesce missing coverage/change fields to zero and may fall back from revenue change to quantity change. This can present an unproven effect as a measured recommendation input.

### Evidence

- `Api/Endpoints/AllEndpoints.cs:3227-3232` selects `change_percent_revenue` when available but falls back to `change_percent_qty`; `:3338-3346` and `:3401-3409` coalesce coverage and change fields to zero.
- `Api/Endpoints/AllEndpoints.cs:2240-2285`, `:2821-2861` build split, cost and margin snapshots for shoe type/color families, while `:2373-2422` and `:2949-2995` pass split coverage and impact into recommendation inputs. This is a high-risk boundary because coverage and recommendation are coupled.
- `Api/Endpoints/AllEndpoints.cs:2485-2489` and `:3050-3054` return null for prior-period changes when the denominator is not positive, but the same semantic distinction is not proven for every pre/post SQL response path.
- `Api/Services/PreNivelacijaScoringService.cs:106-152` uses smoothed scenario units and minimum-one-unit clamping without an observed comparable cohort or availability adjustment.

### Scope

- `/analytics` sales/trend surfaces, `/analytics/products`, `/analytics/supplier`, `/analytics/inventory`, `/analytics/actions`, `/analytics/decision-board`, `/analytics/data-quality`, `/analytics/reports`, and all vendor/color/shoe-type/pre/post nivelacija screens that consume the split.
- Backend/SQL view contract for event date, pre/post windows, comparable article cohort, stock/availability, revenue, quantity, cost/margin coverage and control/test evidence.
- Frontend explanation, recommendation gate and export/report parity for the same split payload.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- the installed `analytics-nivelacija` skill instructions
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` sections for `RQ107`, `RQ112`, `RQ119`, `RQ137` and `RQ139`
- `Api/Endpoints/AllEndpoints.cs` vendor/color/shoe-type nivelacija handlers
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`
- `Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql`
- `Api.Tests/AnalyticsResponseMetaContractTests.cs` and nearest nivelacija tests

### Do

1. Define pre-window, event boundary, post-window, timezone and effective/observed period semantics as half-open dates; prove the same article/cohort and scope are used in both periods.
2. Separate price effect from stock availability, OOS, assortment/composition, seasonality and traffic effects. If a control group or comparable cohort is unavailable, return an unproven/insufficient state and suppress recommendation.
3. Keep revenue deltas, quantity deltas, price metrics, margin/profit metrics and coverage as separate fields with units and denominators. Never substitute quantity percentage for revenue percentage without an explicit backend status and user-facing explanation.
4. Remove SQL/reader coalescing that hides missing coverage or change evidence. Preserve a true zero only when the source proves the measured value is zero.
5. Ensure recommendation status, score, confidence/reliability and `recommendationAllowed` are computed by the backend from the same validated split and reused unchanged by every frontend consumer.

### Tests

- Deterministic fixtures for: no event, event with no pre sales, event with no post sales, true zero delta, missing denominator, partial article cohort, stockout in post window, different scope, wrong period, duplicate event rows and control/test mismatch.
- SQL/view and endpoint tests proving revenue and quantity denominators are not interchangeable and missing coverage is not zero.
- Backend tests for margin with historical cost, estimated cost and no cost; recommendation suppression when comparability or coverage is insufficient.
- Frontend tests for visible explanation and no action when `recommendationAllowed=false`, including export/table/chart parity.
- `dotnet test` nearest nivelacija filters, focused frontend specs, analytics guardrail and `git diff --check`.

### Acceptance

- Every pre/post number states its window, cohort/scope, denominator, coverage and whether the effect is measured or only descriptive.
- Price, availability and composition effects are not silently conflated.
- Missing/partial/insufficient comparability never becomes zero effect, positive effect, confidence or an allowed action.
- The same backend split payload drives page, table, chart, detail, export and report without frontend recomputation.

### Dependencies

- `RQ139` numeric-state contract is the required semantic baseline.
- `Q83` is the separate SQL owner for raw nivelacija nullability/baseline behavior; reuse its result instead of duplicating SQL formula work here.
- Reuse existing `RQ107` scenario vocabulary and `RQ112` reconciliation work; do not create a second pre/post formula owner.
- Production event/refresh proof remains subject to `STAB16`; local deterministic evidence is not live deployment proof.

---

## RQ141 - Map full analytics lineage, scope, cache and refresh parity

Status: WAITING
Priority: P0
Type: audit/backend/contract/frontend/tests
Feature family: analytics-lineage-scope-cache-refresh-parity
Parallel-safe: no, this is the cross-screen source-of-truth map
Owner: Codex
Commit suggestion: `fix(analytics): align full lineage and refresh provenance`

### Problem

The existing period-lineage repair covers selected dashboard/readiness/report paths only. The remaining analytics pages can still disagree about requested/effective/observed period, data scope, generated time, successful refresh, cache version or fallback source. A query timestamp must not be shown as data freshness, and a cache hit or fallback must not look like a fresh authoritative result.

### Evidence

- `RQ137` completion note explicitly records residual risk for analytics surfaces outside the selected dashboard/readiness/supplier-report path.
- `Api/Endpoints/AllEndpoints.cs:4058-4103` redirects legacy analytics routes to cached routes, creating a compatibility/cache boundary that needs route-by-route proof.
- `Api/Endpoints/AllEndpoints.cs:3187-3211` keys vendor nivelacija cache by request parameters and applies response metadata on cache hits; the full set of cache inputs, invalidation and last-successful-refresh behavior is not proven for all families.
- `Infrastructure/Seed/DatabaseInitializer.cs:458-478` explicitly keeps heavy analytics refresh out of startup and assigns it to `NightlyAnalyticsRefreshWorker`, while `:2102-2107` logs migration failure and continues. These paths require visible degraded/runtime truth rather than optimistic freshness.

### Scope

Produce and implement a matrix for every listed route and all sales, trend, forecast and pre/post nivelacija screens. Each row must map React page/component, API client, endpoint, DTO/response, backend service, SQL/EF query, table/view/migration, cache key/invalidation, refresh owner/source, existing tests and these facts:

- requested period;
- effective calculation period;
- observed data period;
- data scope;
- generated-at;
- last successful refresh;
- freshness status;
- data-quality status;
- empty/partial/error state;
- recommendation allowed;
- limitation/reason.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` sections `RQ111`, `RQ113`, `RQ120`, `RQ123`, `RQ135`, `RQ137` and `RQ139`
- `Api/Dtos/AnalyticsResponseMetaDto.cs`
- `Api/Services/AnalyticsRefreshStatusService.cs`
- `Infrastructure/Services/AnalyticsRefreshRunRecorder.cs`
- `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`
- all route clients/pages named in Scope

### Do

1. Build the matrix before changing behavior and record every unresolved source-of-truth or schema gap.
2. Standardize backend lineage fields and ensure every cache hit, fallback, partial result and stale result carries its real source and status.
3. Keep generated-at separate from last successful refresh. If refresh history is missing, return unknown, not the current query time.
4. Validate all period and scope parameters at the endpoint boundary and include normalized values in cache identity. Wrong period/scope must not reuse a trusted-looking cache entry.
5. Ensure empty success, endpoint 404, missing table/migration, refresh failure and true server error have distinct API and user-facing states.
6. Reconcile one seeded data fixture across page/card/table/chart/detail/export/report and document any intentional aggregation conversion.

### Tests

- Matrix completeness check covering all required routes and all sales/trend/forecast/nivelacija families.
- Endpoint/client tests for wrong period, wrong scope, cache-key collision, stale and unknown freshness, fallback/partial response, failed refresh, endpoint 404 and missing relation/migration.
- Tests proving generated-at is not displayed as last successful refresh and empty success is not server error.
- Frontend route tests for dark/light/soft-gray theme and user-readable messages without raw backend codes.
- `npm run check:analytics-guardrails`, focused frontend/backend tests, `dotnet build`/test for changed backend contracts, `npm run build`, `git diff --check` and planning/queue validators.

### Acceptance

- A complete, current matrix exists for every requested screen and each field is either confirmed by code/test or explicitly marked unproven.
- Period, scope, provenance, freshness and quality cannot drift between cached and uncached responses.
- Last successful refresh is sourced from refresh history, never from request generation time.
- 404, missing schema, refresh failure, partial/fallback, empty and stale states are distinguishable and visible.

### Dependencies

- `RQ137`, `RQ139` and existing cache/refresh prompts are prerequisites for vocabulary and compatibility.
- `STAB16` owns provider/live worker access; this prompt must label live proof as pending when unavailable.

---

## RQ142 - Materialize measured forecast and trend evaluation with safe chart states

Status: WAITING
Priority: P1
Type: backend/SQL/contract/frontend/tests
Feature family: forecast-trend-measured-evaluation
Parallel-safe: no, evaluation semantics must remain backend-owned
Owner: Codex
Commit suggestion: `feat(analytics): materialize measured forecast evaluation`

### Problem

`RQ138` added a fail-closed Trend Models contract, but its completion note states that no measured `ready` evaluation source is materialized from production data. Forecast/trend screens therefore still lack proven actual-vs-forecast pairing, horizon, cut-off, baseline and error metrics. A score, confidence or reliability claim without these facts is not evidence.

### Evidence

- `RQ138` completion note: the contract is present, but numeric values remain unavailable until a real measured evaluation source exists.
- `RQ108` and `RQ117` provide forecast materialization/observed-pairing foundations but do not by themselves prove the user-facing evaluation sample and metrics.
- `Application/Analytics/Services/TrendScoringService.cs:245-271` computes a normalized index from positive scores and returns zero for no usable scores without a sample/quality state.
- Frontend chart coverage includes `TrendModelList`, dashboard analytics charts, supplier/shoe-type charts and `SupplierSalesStatsPage`’s positive-size gate; all require explicit handling of initial width/height `0` or `-1`.

### Scope

- Authoritative forecast/trend evaluation materializer and DTO/endpoint.
- Actual/forecast pair identity, cutoff, horizon, baseline, sample, missing pairs, WAPE/MAE/bias or explicitly selected metrics, units and denominator rules.
- Trend Models/dashboard/sales/inventory forecast consumers plus chart/table/export/report parity and zero-dimension safety.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ108`, `RQ117`, `RQ138` and their evidence notes
- current forecast backtest query/handler/contract
- current `TrendModelList` and forecast/chart components

### Do

1. Prove the actual and forecast rows belong to the same entity, scope, cutoff and observed period; exclude leakage from future actuals.
2. Define each metric’s numerator/denominator, zero-demand behavior, missing-pair treatment, minimum sample and rounding. Do not emit measured accuracy for insufficient or stale samples.
3. Make baseline, horizon, last evaluated time, freshness, data quality and limitations mandatory when a numeric result is available.
4. Keep unavailable/stale/partial evaluation fail-closed; no frontend score or percent reconstruction.
5. Make every chart render a stable empty/blocked state while width or height is `0`, negative, NaN or not yet measured; never pass invalid dimensions to the chart library.

### Tests

- Pairing fixtures for perfect forecast, valid zero demand, no actual, no forecast, missing denominator, all-zero actuals, partial horizon, stale evaluation, leakage/wrong cutoff and wrong scope.
- Metric tests for WAPE/MAE/bias (or the chosen authoritative set), NaN/Infinity and minimum sample.
- Frontend tests for unavailable/stale/partial states, chart dimensions `0` and `-1`, table/chart/export parity and dark/light/soft-gray themes.
- Focused backend/frontend tests, analytics guardrail, changed project builds and `git diff --check`.

### Acceptance

- Numeric forecast/trend evaluation appears only from a measured backend source with explicit sample, period, baseline, horizon, freshness and limitations.
- Missing, zero-denominator, stale, partial or wrong-scope evaluation is visibly unavailable, not zero accuracy.
- Charts never receive invalid initial dimensions and do not generate console warnings/errors in the tested states.

### Dependencies

- `RQ138` contract, `RQ108` materializer foundation and `RQ117` observed-pair semantics are prerequisites.
- `RQ139` supplies the shared missing/zero/finite-number contract.

---

## RQ143 - Remove frontend decision and ranking invention from analytics surfaces

Status: WAITING
Priority: P0
Type: backend/contract/frontend/tests
Feature family: backend-decision-ranking-ownership
Parallel-safe: no, actionability has one source of truth
Owner: Codex
Commit suggestion: `fix(analytics): keep ranking and recommendation backend-owned`

### Problem

Backend ownership of recommendation status, score and confidence is not enough if pages still derive local thresholds, confidence tones, priority scores, urgency, reorder probability or ranking fallbacks. The same item can then be actionable in one surface and blocked in another, especially when impact/confidence is null or data quality is insufficient.

### Evidence

- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx:782-786`, `:821-825` use `?? 0` for expected impact and locally compute/sort priority and impact values.
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx:778-784` derives data-quality status from measured sample size and warning-code counts in the page, which can diverge from backend status.
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts:380-425` computes reorder need, urgency, reorder probability and expected profit on the frontend from fallback-filled inputs.
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`, `ShoeTypeSalesStatsPage.tsx`, `ColorSalesStatsPage.tsx` and `DailySalesStatsPage.tsx` contain local quality/coverage thresholds that must be classified as presentation-only or moved to backend-owned status fields.
- `Api/Services/PreNivelacijaScoringService.cs:155-193` and `:234-280` show backend decision semantics already exist for one family, making frontend reimplementation especially risky.

### Scope

- Decision Board, Product Decision, supplier, inventory, actions, pre/nivelacija and all cards/tables/details/exports/reports with recommendation or ranking.
- Backend DTOs for status, decision score, expected impact, confidence/reliability, reason codes, data quality and `recommendationAllowed`.
- Frontend adapters and display-only sorting/filtering that must not change business decisions.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ01`, `RQ08`, `RQ10`, `RQ12`, `RQ13`, `RQ121`, `RQ122`, `RQ124`, `RQ129`, `RQ139`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- backend decision/recommendation DTOs and nearest tests

### Do

1. Inventory every frontend threshold, score, fallback, sort key and action visibility condition. Classify it as harmless presentation formatting or business logic.
2. Move business logic to the backend or consume an existing backend field. The frontend may sort by a backend-provided rank for presentation but may not invent rank from null-as-zero impact/confidence.
3. Enforce `recommendationAllowed=false` as a hard no-action rule across buttons, links, bulk actions, exports and reports.
4. Do not display confidence/reliability without valid backend basis; do not infer quality from sample length or warning count when backend already owns it.
5. Map reason/warning codes to safe Serbian user-facing copy, preserving raw codes only in an explicitly technical/audit channel.

### Tests

- Backend/frontend parity fixtures for allowed, blocked, insufficient, stale, partial, fallback, null impact, null confidence and true zero impact.
- Tests proving no action appears when `recommendationAllowed=false` and no ranking promotion occurs from missing values.
- Tests proving local threshold changes do not alter backend decision status.
- Table/card/detail/export/report parity and safe unknown-code mapping tests.
- Focused backend/frontend tests, analytics guardrail, frontend build and `git diff --check`.

### Acceptance

- Business decision, score, confidence/reliability, quality status, reason and actionability have one backend owner.
- Frontend never converts missing impact/confidence into zero for a trusted ranking or recommendation.
- Blocked recommendations expose explanation and limitation, but no executable action.
- All changed surfaces use the same backend payload and user-readable reason mapping.

### Dependencies

- `RQ139` is required for null/zero semantics.
- Reuse completed `RQ121`, `RQ122`, `RQ124` and `RQ129`; do not duplicate their contracts.

---

## RQ144 - Make Data Quality health distinguish no evidence from a valid zero

Status: WAITING
Priority: P1
Type: backend/contract/frontend/tests
Feature family: data-quality-health-denominator-contract
Parallel-safe: no, health status gates trust everywhere
Owner: Codex
Commit suggestion: `fix(analytics): preserve data-quality denominator truth`

### Problem

Data Quality health uses revenue shares as decision signals. When the sales denominator is zero or unavailable, a share of zero is not evidence that quality is healthy. The page currently applies thresholds through `?? 0`, while the backend snapshot exposes non-null share fields that cannot tell no revenue from a measured zero share.

### Evidence

- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs:145-171` sets `HasRevenueEvidence`, but `MissingCostRevenueSharePct` and `UnknownSupplierRevenueSharePct` become `0d` when `totalRevenue <= 0`.
- `Klijent/clientapp/src/pages/DataQualityPage.tsx:655-662` evaluates missing-cost and unknown-supplier health thresholds through `?? 0`, which can make unavailable health look green.
- `Klijent/clientapp/src/pages/DataQualityPage.tsx:401`, `:625` defaults issue totals to zero; this must remain distinct from a successful empty query versus unavailable issue data.
- Existing RQ04/RQ118/RQ135 work improved selected health/scope paths, but this specific backend nullable denominator contract is not proven across all consumers.

### Scope

- Data Quality health DTO/service/page, issue list and trend chart.
- Dashboard, Decision Board and supplier/product surfaces that consume health status.
- Period/scope, freshness, refresh and empty/error metadata for the health snapshot.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ04`, `RQ05`, `RQ07`, `RQ118`, `RQ135`, `RQ139` and their evidence
- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- `Klijent/clientapp/src/pages/DataQualityPage.tsx`
- `Api.Tests/AnalyticsResponseMetaContractTests.cs` and Data Quality tests

### Do

1. Make share fields nullable or stateful and define: valid zero with positive denominator, unknown/no denominator, insufficient evidence, stale, partial and error.
2. Keep `HasRevenueEvidence` and denominator facts backend-owned; frontend must not infer health from null-coalesced values or local thresholds.
3. Distinguish successful empty issue list (`total=0`) from failed/unavailable issue query; show a user-readable explanation for both.
4. Carry exact health period, scope, generated-at, last successful refresh, freshness and data-quality status into every consumer.

### Tests

- Backend/frontend tests for empty sales window, null share, valid zero share with positive denominator, nonzero share, missing health payload, stale/unknown freshness and partial response.
- Issue-list tests for successful empty, filtered empty, endpoint error and missing relation.
- Dashboard/Decision Board tests proving no green/healthy recommendation from missing denominator.
- Focused tests, analytics guardrail, changed builds and `git diff --check`.

### Acceptance

- No-revenue/no-denominator health is not rendered as a measured zero or green state.
- A real zero share with a positive denominator remains zero and can be healthy.
- Health status and explanation are identical across Data Quality, dashboard, board, export and report consumers.

### Dependencies

- `RQ139` numeric-state vocabulary and `RQ118` scope contract are prerequisites.
- Reuse `RQ135` cache invalidation/freshness work.

---

## RQ145 - Prove analytics card/table/chart/detail/export/report parity and safe messaging

Status: WAITING
Priority: P1
Type: frontend/backend/contract/tests
Feature family: analytics-surface-parity-and-safe-messaging
Parallel-safe: no, parity requires one fixture and one semantic adapter
Owner: Codex
Commit suggestion: `test(analytics): prove cross-surface metric parity`

### Problem

Even when an endpoint is correct, analytics trust fails if cards, tables, charts, details, exports and reports use different values, fallback rules, rounding, period labels or warning text. Unknown backend codes can also leak into user-facing action/measurement messages.

### Evidence

- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts` creates separate derived structures for category, price, aging, depletion and reorder views, so parity cannot be assumed from one API response.
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx` contains `OUTCOME_SUMMARY_WARNING_LABELS[code] ?? code`, an explicit raw-code fallback risk.
- `RQ112`, `RQ120`, `RQ123`, `RQ136` and `RQ137` closed selected parity/provenance paths but do not establish one fixture-based parity proof for every required route/family.
- Chart components include both fixed heights and responsive containers; initial zero/negative measurement states need a common safe adapter rather than per-page behavior.

### Scope

- All routes listed by the user and all sales, trend, forecast and pre/post nivelacija surfaces.
- Shared formatters, metric adapters, warning/reason mappings, export/report builders and chart state wrappers.
- One deterministic fixture manifest with exact expected values/states for card/table/chart/detail/export/report.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ112`, `RQ120`, `RQ123`, `RQ136`, `RQ139`, `RQ141` and their evidence
- `Klijent/clientapp/src/utils/analyticsFormatters.ts`
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
- existing analytics export/report/chart tests

### Do

1. Select representative fixtures for valid zero, null/unknown, stale, partial/fallback, empty, error and valid nonzero results.
2. Assert that all surfaces consume the same backend metric/state and only apply documented presentation formatting; no surface may recreate a decision or denominator.
3. Centralize safe Serbian labels for unknown warning/reason/status codes. Never fall back to the raw code in a user-facing label, tooltip, export or report.
4. Add a common chart guard for width/height `0`, `-1`, NaN and Infinity, preserving an accessible empty/preparing state and avoiding console warnings.
5. Verify dark, light and soft-gray themes using semantic tokens; do not fix parity by hardcoding a new unrelated theme.

### Tests

- Fixture-based exact parity tests for card/table/chart/detail/export/report values, units, rounding, period, scope, freshness, quality, fallback and recommendation status.
- Unknown-code mapping tests and no-raw-code assertions for page, export and report text.
- Chart tests for dimensions `0` and `-1`, empty data, NaN/Infinity point values and responsive initial render.
- Dark/light/soft-gray visual or DOM-state tests, focused frontend suite, analytics guardrail and `npm run build`.

### Acceptance

- One fixture produces semantically identical values and states everywhere it is shown.
- Exports/reports cannot silently restore a value hidden or blocked on the page.
- User-facing messages contain clear Serbian explanation, not internal backend codes.
- No chart warning/error is introduced by initial invalid dimensions or invalid metric values.

### Dependencies

- `RQ139`, `RQ141` and the completed parity/provenance prompts are prerequisites.
- This prompt consumes backend truth; it must not add frontend business formulas to repair a mismatch.

---

## RQ146 - Prove analytics endpoint, schema, migration and refresh-failure behavior

Status: WAITING
Priority: P1
Type: backend/integration/EF/SQL/tests
Feature family: analytics-schema-runtime-proof
Parallel-safe: no, runtime schema is an owner boundary
Owner: Codex
Commit suggestion: `test(analytics): prove schema and refresh failure states`

### Problem

Analytics code references EF entities, raw SQL relations, views and startup repair scripts across multiple databases. A missing table/view/migration, 404 route, failed refresh or partially applied schema can currently be reported as an empty or fallback result unless each path has a tested error contract. The user must never trust an empty dataset caused by a schema/runtime failure.

### Evidence

- `Api/Endpoints/AllEndpoints.cs:3227-3232` probes relation columns and selects a compatibility expression; `:3338-3346`, `:3401-3409` then coalesce missing fields, making schema/column drift a numeric-trust boundary.
- `Infrastructure/Seed/DatabaseInitializer.cs:2102-2107` catches analytics migration failures and continues, while `:150-168` performs supplier/nivelacija schema repair. This requires explicit readiness/failure propagation to analytics responses.
- `Api/Endpoints/AllEndpoints.cs:4058-4103` maintains legacy redirect routes, so endpoint 404/redirect parity must be tested rather than inferred.
- Existing `AnalyticsDbInfrastructureTests` and response-meta tests cover selected contracts, not a complete endpoint-to-relation/migration proof for all requested analytics families.

### Scope

- Endpoint inventory and 404/redirect behavior for all required analytics routes.
- EF/SQL query, relation/view/table, migration and startup repair mapping for sales, trend, forecast, inventory, supplier, Data Quality and nivelacija.
- Refresh worker/recorder, cache invalidation and API meta behavior for successful, failed, partial and skipped refreshes.

### Read first

- `AGENTS.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `RQ111`, `RQ113`, `RQ135`, `RQ141` and their evidence
- `Infrastructure/Seed/DatabaseInitializer.cs`
- `Infrastructure/Services/AnalyticsRefreshRunRecorder.cs`
- `Api/Services/AnalyticsRefreshStatusService.cs`
- analytics migrations/views and nearest infrastructure tests

### Do

1. Generate an endpoint-to-service-to-query-to-relation/migration inventory and mark every edge confirmed or unproven.
2. Add deterministic integration/contract tests for missing table/view, missing column, unapplied migration, endpoint 404, failed refresh, skipped worker and stale cache.
3. Ensure missing schema and failed refresh return an explicit degraded/error/readiness state with safe user copy; never return successful empty data without an empty reason.
4. Verify migration listing and current model/view compatibility for the analytics context. Do not perform destructive production schema repair in this prompt.
5. Prove cache invalidation/versioning after successful and failed refresh; a failed refresh must not advance the last-successful-refresh timestamp.

### Tests

- Endpoint route/redirect tests including 404 and wrong method/path.
- EF/SQL integration tests for missing table/view/column and migration mismatch, with safe classification and no fake zero rows.
- Refresh recorder/status tests for success, failure, retry, skipped/unregistered worker and partial family refresh.
- Cache tests proving failed refresh does not publish fresh metadata and successful refresh invalidates all dependent families.
- `dotnet ef migrations list` for the affected context, focused `dotnet test`, backend build, analytics guardrail if contract changes, `git diff --check` and queue validators.

### Acceptance

- Every requested analytics endpoint has a confirmed route, query and schema/migration owner or an explicit blocker.
- Missing schema, 404 and refresh failure are visible as failures/degraded states, never as trusted empty/zero analytics.
- Last successful refresh changes only after a confirmed successful refresh and cache metadata agrees with it.
- The proof is reproducible on current main without destructive database operations.

### Dependencies

- `RQ141` lineage matrix and `RQ139` numeric-state contract are prerequisites.
- `STAB16` remains the owner of provider/live worker registration and production refresh proof; local integration tests cannot replace that evidence.
