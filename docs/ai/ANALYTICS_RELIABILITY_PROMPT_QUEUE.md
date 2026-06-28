# Analytics Reliability Prompt Queue

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: RQ01

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
| RQ01 | READY | decision-board-impact-trust | Prevent wrong expected-impact fallback in board product cards |
| RQ02 | WAITING | product-decision-denominators | Define PDC summary top/all-row denominator contract |
| RQ03 | WAITING | lost-sales-zero-vs-unknown | Separate unavailable lost-sales evidence from true zero |
| RQ04 | WAITING | data-quality-no-data | Prevent no-revenue data-quality windows from looking green |
| RQ05 | WAITING | analytics-datascope-consistency | Audit dataScope semantics across analytics modules |
| RQ06 | WAITING | data-quality-offender-scope | Fix top-offender revenue impact scope drift |
| RQ07 | WAITING | missing-cost-offenders | Add missing-cost offender drilldown contract |
| RQ08 | WAITING | supplier-blocked-signal-ranking | Cap/label blocked supplier signals in Decision Board |
| RQ09 | WAITING | action-source-empty-state | Decide whether zero analytics actions is healthy empty or insufficient data |
| RQ10 | WAITING | inventory-evidence-confidence | Add evidence-based confidence contract for inventory cards |
| RQ11 | WAITING | transaction-stat-semantics | Clarify transaction item/line/unit count semantics |
| RQ12 | WAITING | pdc-ignored-rows-contract | Make Product Decision Center ignored/top rows explicit |

---

## RQ01 - Decision Board product expected-impact correctness

Status: READY
Priority: P0
Type: backend/tests
Feature family: decision-board-impact-trust
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ01-<agent>.lock.md`
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

---

## RQ02 - Product Decision Center summary denominator contract

Status: WAITING
Ready after: RQ01 DONE
Priority: P1
Type: backend/tests/docs
Feature family: product-decision-denominators
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ02-<agent>.lock.md`
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

---

## RQ03 - Lost-sales unavailable vs true zero

Status: WAITING
Ready after: RQ01 DONE
Priority: P0
Type: backend/tests
Feature family: lost-sales-zero-vs-unknown
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ03-<agent>.lock.md`
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

---

## RQ04 - Data Quality no-revenue/no-data status

Status: WAITING
Ready after: RQ01 DONE
Priority: P0
Type: backend/tests
Feature family: data-quality-no-data
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ04-<agent>.lock.md`
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

---

## RQ05 - Analytics dataScope consistency audit

Status: WAITING
Ready after: RQ01 DONE
Priority: P0
Type: docs/tests
Feature family: analytics-datascope-consistency
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ05-<agent>.lock.md`
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

---

## RQ06 - Data Quality top-offender revenue scope correctness

Status: WAITING
Ready after: RQ05 DONE or explicitly unblocked
Priority: P1
Type: backend/tests
Feature family: data-quality-offender-scope
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ06-<agent>.lock.md`
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

---

## RQ07 - Missing-cost offender drilldown

Status: WAITING
Ready after: RQ04 DONE
Priority: P1
Type: backend/API-contract/tests
Feature family: missing-cost-offenders
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ07-<agent>.lock.md`
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

---

## RQ08 - Blocked supplier signal ranking in Decision Board

Status: WAITING
Ready after: RQ01 DONE; SQL queue Q69 evidence available if needed
Priority: P1
Type: backend/tests
Feature family: supplier-blocked-signal-ranking
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ08-<agent>.lock.md`
Commit suggestion: `test(analytics): cap blocked supplier board cards`

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

---

## RQ09 - Analytics actions empty-state contract

Status: WAITING
Ready after: RQ01 DONE
Priority: P2
Type: backend-contract/tests
Feature family: action-source-empty-state
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ09-<agent>.lock.md`
Commit suggestion: `test(analytics): define actions empty source state`

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

---

## RQ10 - Inventory evidence confidence contract

Status: WAITING
Ready after: RQ01 DONE
Priority: P2
Type: docs/backend-contract
Feature family: inventory-evidence-confidence
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ10-<agent>.lock.md`
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

---

## RQ11 - Transaction item/line/unit semantics

Status: WAITING
Ready after: RQ01 DONE
Priority: P2
Type: backend-contract/tests
Feature family: transaction-stat-semantics
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/RQ11-<agent>.lock.md`
Commit suggestion: `test(analytics): clarify transaction stats item semantics`

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

---

## RQ12 - Product Decision Center ignored/top rows contract

Status: WAITING
Ready after: RQ02 DONE
Priority: P2
Type: backend-contract/tests
Feature family: pdc-ignored-rows-contract
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ12-<agent>.lock.md`
Commit suggestion: `test(analytics): define pdc ignored rows contract`

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
