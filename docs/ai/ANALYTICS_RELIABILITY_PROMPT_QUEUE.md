# Analytics Reliability Prompt Queue

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none (`RQ97` DONE; `RQ98` WAITING)
Owner-promoted test pack: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md` (`RQ100`-`RQ105` DONE); `RQ96` DONE; `RQ106` DONE; `RQ97` DONE.

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

- 2026-08-04: DONE. Introduced shared `LostSalesSourceStatus` / `LostSalesSnapshot` and `BuildLostSalesValidationFromSnapshot`. Unavailable → `insufficient_data` with null estimate; view zero → `true_zero`/`good`; fallback zero → `warning`.
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

- 2026-08-04: DONE. Docs/tests matrix only; no runtime filter rewrite. Canonical rules proposed (sales→header, quality/inventory→article). Highest P0 mismatch remains DQ top-offender unscoped `sales_30d`.
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
  - `GetDataQualityIssuesHandler` still has unscoped `sales_30d` (out of RQ06 file scope) → follow-up RQ06-F1.
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

- 2026-08-04: DONE. Top offenders support `missingCost` via article `NabavnaCena` null/≤0 (`is_missing_cost`), independent of supplier CASE. Unknown issue types → API 400 / service `ArgumentOutOfRangeException` (no silent supplier fallback). Issues-list `Normalize` still defaults unknown→missingSupplier (handler not rewritten).
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

- 2026-08-04: DONE. When `RecommendationAllowed=false`, supplier cards are labeled `signal_check` / `insufficient_data`, priority capped ≤40, ImpactScore=0, excluded from `urgent` and `impact`; remain in `supplierRisk` for verification. Trust blocker card kept.
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

- 2026-08-04: DONE. Contract: empty successful load → `good` (no `no_actions` warning); `analytics_actions_unavailable` → `insufficient_data`. "Expected actions missing" not auto-warned (would need cross-source expectation; left as future).
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
  - PDC UI still labels `totalRows` without surfacing `ignoredRowsMeaning`; operators should read contract before comparing to DQ intake “ignorisani redovi”.
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
