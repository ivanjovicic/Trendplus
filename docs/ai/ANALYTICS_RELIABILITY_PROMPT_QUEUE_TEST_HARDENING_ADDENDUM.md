# Analytics Reliability Prompt Queue - Test Hardening Addendum

Date: 2026-08-13
Repo: `ivanjovicic/Trendplus`
Current READY prompt: `RQ101`
Status: owner-promoted READY pack; `RQ100` DONE

Purpose: lock the highest-value analytics contracts with focused integration and display tests. This is not a new program. Runtime formula changes are out of scope unless a test reproduces a real contract bug.

Purpose: lock the highest-value analytics contracts with focused integration and display tests. This is not a new program. Runtime formula changes are out of scope unless a test reproduces a real contract bug.

Use with:

- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`

## Queue rules

1. Keep later prompts `WAITING` until the current READY prompt is DONE.
2. `RQ101` is the current READY prompt. Promote `RQ102` only after `RQ101` is DONE.
3. Do not mix SQL rewrites, premium chrome, or tenant/auth work into these tasks.
4. Prefer extending an existing test class over a new host.
5. If a test fails because the product contract is genuinely ambiguous, stop as `BLOCKED`/`PARTIAL`. Do not invent business truth to make the assertion pass.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ100 | DONE | analytics-critical-decision-contract | PDC + Decision Board recommendation/impact/meta counterexamples |
| RQ101 | READY | analytics-inventory-null-evidence | Inventory signal/list fake-zero and empty-meta lock-in |
| RQ102 | WAITING | analytics-sales-period-empty-scope | Sales summary/daily-sales period, empty, and filter isolation |
| RQ103 | WAITING | analytics-action-outcome-learning | Action outcome not-measured and learning-eligibility lock-in |
| RQ104 | WAITING | analytics-frontend-backend-truth | Core decision pages display backend fields and hide KPI zeros on error |

---

## RQ100 - Product Decision and Decision Board critical-path contract tests

Status: DONE
Ready after: owner-promoted 2026-08-13; QDB exclusive work is currently clear (`QDB06` still needs migration approval)
Priority: P1
Type: backend-tests/integration
Feature family: analytics-critical-decision-contract
Parallel-safe: yes, tests/docs unless a reproduced contract bug requires a one-file backend fix
Owner: unassigned
Local lock: `.ai/task-locks/RQ100-<agent>.lock.md`
Commit suggestion: `test(analytics): lock decision board and PDC critical contracts`

### Problem

Wrong expected impact or a silent fallback on Product Decision Center / Decision Board can send operators to the wrong action this week. Existing tests cover pieces of the contract, but there is no single focused pack that proves the named failures together: no lost-sales fallback onto blocked recommendations, empty is not error, and backend recommendation fields remain the source of truth.

### Evidence

- `Api/Endpoints/DecisionBoardEndpoints.cs`
- `Api.Tests/DecisionBoardEndpointsTests.cs`
- `Api.Tests/DecisionBoardAggregationContractTests.cs`
- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` `RQ01` / `RQ12` completion notes
- Hardening vocabulary: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md` section 1.2

### Scope

- `Api.Tests/DecisionBoardEndpointsTests.cs`
- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`
- `Api.Tests/DecisionBoardAggregationContractTests.cs` only if the same fallback assertion naturally lives there
- backend endpoint/builder files only if a new test reproduces a real contract bug

### Do Not Touch

- frontend pages
- SQL views/migrations
- action ledger writes
- inventory snapshot handlers
- Premium UI chrome

### Read first

- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md` sections 1.2 and 1.4
- `Api.Tests/DecisionBoardEndpointsTests.cs`
- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`

### Do

1. Add or extend tests that `FIX_DATA` and `INSUFFICIENT_DATA` Decision Board product cards do not receive `LostSalesEstimate` as `expectedImpactRsd`.
2. Add or extend a test that `REPLENISH`/`BOOST` expected impact is present only when the Product Decision Center builder supplied it.
3. Prove a successful empty/no-match PDC or board slice sets `meta.success=true` with an explicit `emptyReason` / insufficient data quality, not a fake healthy zero-impact recommendation.
4. Keep structured `recommendationStatus` as the machine value (`REPLENISH`) and operator label (`Dopuni`) as a separate field. Do not assert that `ScopeExplanation` or UI copy is the source of truth for status.
5. If a reproduced bug requires a runtime fix, keep it in the same owner files and record the before/after in the run log.

### Tests

```powershell
dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~DecisionBoardEndpointsTests|FullyQualifiedName~DecisionBoardAggregationContractTests|FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests"
```

### Acceptance

- The three named failure modes have failing-to-passing counterexamples or an explicit proof they were already locked.
- No new frontend scoring is introduced.
- Completion note references `.ai/runs/<date>-RQ100-evidence.md`.

### Dependencies

- Owner promotion to `READY`
- Path-clear vs current exclusive API work; QDB06 remains WAITING on owner migration approval
- `RQ01`/`RQ12` remain historical contracts, not reopened formula work

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: locked PDC + Decision Board counterexamples for lost-sales-off-blocked-impact, REPLENISH/BOOST impact only from PDC, empty success meta, and machine status vs operator label
- Changed files:
  - Api.Tests/DecisionBoardEndpointsTests.cs
  - Api.Tests/DecisionBoardAggregationContractTests.cs
  - Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md
  - docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
  - MASTER_ROADMAP.md
  - .ai/runs/2026-08-13-RQ100-evidence.md
- Contract/runtime behavior changed: no; tests only. Existing board/PDC contracts already matched the named failures.
- Checks run:
  - `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~DecisionBoardEndpointsTests|FullyQualifiedName~DecisionBoardAggregationContractTests|FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests"` - pass (45)
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
  - `node scripts/check-prompt-queues.mjs` - pass (260 tasks)
  - `node scripts/check-planning-architecture.mjs --self-test` - pass
  - `node scripts/check-planning-architecture.mjs` - pass
  - `node scripts/check-agent-instructions.mjs --self-test` - pass
  - `node scripts/check-agent-instructions.mjs` - pass
  - `git diff --check` - pass
- Checks not run:
  - `dotnet build` - test project build already compiled as part of `dotnet test`
  - frontend / npm - out of scope
- Run log: .ai/runs/2026-08-13-RQ100-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 31f338f735da9558c3064a07837a8c9e9cc8a2ab
- Main verification: git rev-parse origin/main -> df1cfd61a9bda335bafb7c448aaae1b8b0e7ddde; work SHA 31f338f735da9558c3064a07837a8c9e9cc8a2ab is an ancestor
- Missed: BOOST expected impact is locked at Decision Board mapping; the PDC in-memory seed still produces REPLENISH + FIX_DATA, not a BOOST row
- Follow-up: `RQ101`
- Residual risk: none known for the named PDC/board impact contract
- Prompt defect / scope repair: none
- Next: `RQ101` - Inventory null-evidence and decision-count contract tests

---

## RQ101 - Inventory null-evidence and decision-count contract tests

Status: READY
Ready after: `RQ100` DONE
Priority: P1
Type: backend-tests/integration
Feature family: analytics-inventory-null-evidence
Parallel-safe: yes, tests unless a reproduced handler bug needs a same-owner fix
Owner: unassigned
Local lock: `.ai/task-locks/RQ101-<agent>.lock.md`
Commit suggestion: `test(inventory): lock null evidence and empty inventory meta`

### Problem

Inventory is where operators decide dopuni / OOS rizik / mrtav lager. A missing forecast, rebalance, alert or size-curve value that becomes `0`/`false` looks like a safe empty warehouse. `RQ64`/`RQ99` started this contract; the remaining gap is a durable counterexample pack across list + signal endpoints, including empty-success meta.

### Evidence

- `Api.Tests/InventorySnapshotContractTests.cs`
- `Api.Tests/InventoryListEndpointIntegrationTests.cs`
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs` (`InventoryBalance_*`)
- `Application/Analytics/Queries/GetInventoryForecast/GetInventoryForecastHandler.cs`
- `Application/Analytics/Queries/GetRebalanceSuggestions/GetRebalanceSuggestionsHandler.cs`
- `Application/Analytics/Queries/GetInventoryAlerts/GetInventoryAlertsHandler.cs`
- `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveHandler.cs`

### Scope

- `Api.Tests/InventorySnapshotContractTests.cs`
- `Api.Tests/InventoryListEndpointIntegrationTests.cs`
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
- the four inventory signal handlers only if a new test reproduces a contract bug

### Do Not Touch

- snapshot SQL/materializers (`RQ96`-`RQ98`)
- React inventory panels except as out-of-scope notes
- Decision Board product-impact tests owned by `RQ100`

### Read first

- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md` `RQ64`/`RQ99`
- `Api.Tests/InventorySnapshotContractTests.cs`
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`

### Do

1. Keep or add EOF-strict reader coverage so `TotalMatchingCount` is never read after the last row.
2. Prove an empty inventory balance/list success returns `meta.success=true`, explicit `emptyReason`, and `dataQualityStatus=insufficient_data`, with zeros only as empty counts under that meta.
3. Prove a missing/null signal field does not coerce to trusted `0`/`false`/`info` without a quality/unknown marker.
4. Do not mark `RQ99` DONE from this prompt unless the EOF-strict assertions are actually present and run.

### Tests

```powershell
dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~InventorySnapshotContractTests|FullyQualifiedName~InventoryListEndpointIntegrationTests|FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests"
```

### Acceptance

- Empty inventory and null signal evidence cannot look like a healthy zero warehouse without meta.
- EOF-strict count reads remain locked or are newly locked with a failing-to-passing proof.
- Completion note references `.ai/runs/<date>-RQ101-evidence.md`.

### Dependencies

- `RQ100` preferred predecessor
- Do not promote ahead of `RQ96` if this task starts rewriting snapshot SQL

---

## RQ102 - Sales period, empty-success and scope-isolation tests

Status: WAITING
Ready after: `RQ101` DONE, or owner promotes this first when sales endpoints are already open
Priority: P1
Type: backend-tests/integration
Feature family: analytics-sales-period-empty-scope
Parallel-safe: yes, tests/docs
Owner: unassigned
Local lock: `.ai/task-locks/RQ102-<agent>.lock.md`
Commit suggestion: `test(analytics): lock sales period empty and scope isolation`

### Problem

Sales summary, top products, daily sales and shoe/supplier sales are how operators see what actually sold. The dangerous bugs are period overlap, `toDate` truncation, empty period looking like an error, and store/supplier filters leaking another entity's revenue.

### Evidence

- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
- `Api.Tests/DailySalesStatsIntegrationTests.cs`
- `Api.Tests/AnalyticsShoeTypeSalesIntegrationTests.cs`
- `Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md` section 1.3

### Scope

- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
- `Api.Tests/DailySalesStatsIntegrationTests.cs`
- `Api.Tests/AnalyticsShoeTypeSalesIntegrationTests.cs`
- `Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs`
- sales endpoint files only if a reproduced period/scope bug is found

### Do Not Touch

- recommendation scoring
- inventory snapshot SQL
- frontend Daily Sales chrome (`P-UI-20`)

### Read first

- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_HARDENING_ADDENDUM.md` section 1.3
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
- `Api.Tests/DailySalesStatsIntegrationTests.cs`

### Do

1. Keep the existing empty-period sales-summary proof: `success=true`, `emptyReason`, `dataQualityStatus=insufficient_data`, and no errorCode.
2. Add or extend a daily-sales proof for the same empty-success contract, not only JSON shape / golden snapshot.
3. Keep or add store/supplier isolation so filtered totals cannot include another store or supplier.
4. If current/previous period helpers are in the same files, add one non-overlapping previous-window assertion. Do not start a date-helper rewrite (`RQ13`/`RQ26`) unless the test reproduces overlap.
5. Invalid range (`fromDate > toDate`) must remain client/API error, not an empty-success dataset.

### Tests

```powershell
dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests|FullyQualifiedName~DailySalesStatsIntegrationTests|FullyQualifiedName~AnalyticsShoeTypeSalesIntegrationTests|FullyQualifiedName~AnalyticsSupplierSalesIntegrationTests"
```

### Acceptance

- Empty sales success, invalid range, and at least one scope-isolation case are locked with named assertions.
- Golden snapshots are not the only daily-sales proof.
- Completion note references `.ai/runs/<date>-RQ102-evidence.md`.

### Dependencies

- Owner promotion after `RQ100`/`RQ101` unless sales files are already the open exclusive area

---

## RQ103 - Action outcome, not-measured and learning-eligibility tests

Status: WAITING
Ready after: `RQ102` DONE, or owner promotes this first when action/timeline files are already open
Priority: P1
Type: backend-tests
Feature family: analytics-action-outcome-learning
Parallel-safe: yes, tests unless a reproduced lifecycle bug needs a same-owner fix
Owner: unassigned
Local lock: `.ai/task-locks/RQ103-<agent>.lock.md`
Commit suggestion: `test(actions): lock not-measured and learning eligibility`

### Problem

If acceptance is treated as success, or `not_measured` gets a fake measured timestamp, recommendation learning and the action queue will overstate what worked. Slice-1/Slice-2 timeline work localized gap messages, but the eligibility axis still needs a durable pack: executed + measured evidence required; issued/accepted/rejected are not success.

### Evidence

- `Application/Analytics/RecommendationLifecycleSemantics.cs`
- `Api.Tests/RecommendationLifecycleSemanticsTests.cs`
- `Api.Tests/AnalyticsActionItemServiceTests.cs`
- `Api.Tests/AnalyticsActionsCriticalWorkflowTests.cs`
- `Infrastructure/Services/Analytics/AnalyticsActionTimelineProjection.cs`

### Scope

- `Api.Tests/RecommendationLifecycleSemanticsTests.cs`
- `Api.Tests/AnalyticsActionItemServiceTests.cs`
- `Api.Tests/AnalyticsActionsCriticalWorkflowTests.cs`
- lifecycle/projection files only if a reproduced eligibility bug is found

### Do Not Touch

- frontend Analytics Actions chrome except as a follow-up note for `RQ104`
- SQL
- Decision Board product impact (`RQ100`)

### Read first

- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/Analytics/RECOMMENDATION_MEASUREMENT_STATISTICS_CONTRACT.md` if the test names measurement denominators
- `Application/Analytics/RecommendationLifecycleSemantics.cs`
- `Api.Tests/RecommendationLifecycleSemanticsTests.cs`

### Do

1. Prove `LearningEligible=false` for issued, accepted, rejected and ignored states even when an expected impact exists.
2. Prove `LearningEligible=true` only for executed + measured evidence, not for `not_measured`.
3. Prove `not_measured` / pending outcome does not populate `OutcomeMeasuredAtUtc` as a fake now-timestamp.
4. Keep `gapReason` codes stable (`no_acceptance_record`, `no_execution_proof`, `no_measurement_evidence`). Message language may be Serbian; do not assert English prose.
5. Do not start `RL06` runtime statistics projection from this prompt.

### Tests

```powershell
dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~RecommendationLifecycleSemanticsTests|FullyQualifiedName~AnalyticsActionItemServiceTests|FullyQualifiedName~AnalyticsActionsCriticalWorkflowTests"
```

### Acceptance

- Acceptance-is-not-success and not-measured-is-not-measured are locked.
- Learning eligibility requires executed + measured evidence.
- Completion note references `.ai/runs/<date>-RQ103-evidence.md`.

### Dependencies

- Do not displace `RL06` contract/runtime work; this prompt only hardens tests around already-landed semantics

---

## RQ104 - Core decision pages display backend truth

Status: WAITING
Ready after: `RQ100` DONE so backend fields are stable; path-safe vs `P-UI-19`
Priority: P2
Type: frontend-tests
Feature family: analytics-frontend-backend-truth
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/RQ104-<agent>.lock.md`
Commit suggestion: `test(ui): lock backend-owned decision fields on core pages`

### Problem

Frontend pages still drift into local scoring or raw backend codes. The operator-facing risk is a page that shows KPI zeros on error, invents Visoko/Srednje/Nisko, or hides the reason for a recommendation. This prompt locks display contracts; it does not move business truth into the client.

### Evidence

- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx`

### Scope

- the spec files listed above
- the four pages only if a spec reproduces a display-contract bug (raw code, fake Nisko, KPI zeros on error)

### Do Not Touch

- backend recommendation formulas
- Premium chrome migrations owned by `P-UI-19`/`P-UI-20`
- new local reliability bands

### Read first

- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/ai/FRONTEND_UX_STANDARDS.md` if present for ErrorState/EmptyState
- `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md` only if a route smoke assertion is required
- the existing specs listed in Evidence

### Do

1. PDC: keep assertions that Why/timeline/evidence show operator labels, not raw `REPLENISH` / `recommendation_issued` as the primary copy, while request payloads may still send machine codes.
2. Decision Board: warning chips do not dump workflow `ActionType`/`Status` as data-quality warnings.
3. Pre-nivelacija and Prodaja pre/post: missing reliability is `Nije dostupno`, never fake `Nisko`; available reliability is a percent from backend, not a local Visoko/Srednje/Nisko label.
4. Error path: at least one core decision page spec proves `AnalyticsErrorState` / role=alert and the absence of the main KPI block.
5. Do not convert lazy routes to eager imports to make a test pass.

### Tests

```powershell
cd Klijent/clientapp
npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx src/pages/ExecutiveDecisionBoardPage.spec.tsx src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/pages/ProdajaPrePostNivelacijePage.spec.tsx
npm run check:analytics-guardrails
```

### Acceptance

- Core decision pages have display-contract tests for backend-owned status/reason/reliability and error-without-KPI-zeros.
- No new frontend scoring threshold is introduced.
- Completion note references `.ai/runs/<date>-RQ104-evidence.md`.

### Dependencies

- `RQ100` preferred so backend field names stay stable
- Path-safe vs `P-UI-19`; do not rewrite TrustHeader/ControlBar while that prompt is `READY`
