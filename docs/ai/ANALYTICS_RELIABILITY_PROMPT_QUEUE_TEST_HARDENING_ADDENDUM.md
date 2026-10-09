# Analytics Reliability Prompt Queue - Test Hardening Addendum

Date: 2026-08-13
Repo: `ivanjovicic/Trendplus`
Current READY prompt: `RQ600` is READY in the Cross-Surface addendum; `RQ598` is DONE and `RQ597` is DONE.
Status: owner-promoted test-hardening follow-up; `RQ598` was registered from the 2026-10-09 owner-approved execution plan. `RQ96`/`RQ106`/`RQ97`/`RQ98`/`RQ597` are DONE on main.

Purpose: lock the highest-value analytics contracts with focused integration and display tests. This is not a new program. Runtime formula changes are out of scope unless a test reproduces a real contract bug.

Use with:

- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`

## Queue rules

1. `Current READY` is the primary/default pointer; independent test-only prompts may also be READY when their dependencies and paths are clear.
2. `RQ105` is DONE. `RQ597` is DONE on main. `RQ96` and `RQ106` are DONE on main.
3. Do not mix SQL rewrites, premium chrome, or tenant/auth work into these tasks.
4. Prefer extending an existing test class over a new host.
5. If a test fails because the product contract is genuinely ambiguous, stop as `BLOCKED`/`PARTIAL`. Do not invent business truth to make the assertion pass.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| RQ100 | DONE | analytics-critical-decision-contract | PDC + Decision Board recommendation/impact/meta counterexamples |
| RQ101 | DONE | analytics-inventory-null-evidence | Inventory signal/list fake-zero and empty-meta lock-in |
| RQ102 | DONE | analytics-sales-period-empty-scope | Sales summary/daily-sales period, empty, and filter isolation |
| RQ103 | DONE | analytics-action-outcome-learning | Action outcome not-measured and learning-eligibility lock-in |
| RQ104 | DONE | analytics-frontend-backend-truth | Core decision pages display backend fields and hide KPI zeros on error |
| RQ597 | DONE | color-bucket-independent-oracle | Compare every Color API bucket with the independent raw-fact oracle |

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

Status: DONE
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

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: locked empty inventory list/balance meta, null signal evidence vs trusted zero/info/false, and EOF-strict TotalMatchingCount for all four signal families
- Changed files:
  - Api.Tests/InventorySnapshotContractTests.cs
  - Api.Tests/InventoryListEndpointIntegrationTests.cs
  - Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md
  - docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
  - MASTER_ROADMAP.md
  - .ai/runs/2026-08-13-RQ101-evidence.md
- Contract/runtime behavior changed: no; tests and in-memory test-host warning suppression only
- Checks run:
  - `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~InventorySnapshotContractTests|FullyQualifiedName~InventoryListEndpointIntegrationTests|FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests"` - pass (28)
  - first combined run failed on EF `ManyServiceProvidersCreatedWarning` harness noise; retry after factory `ConfigureWarnings` - pass
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
  - `node scripts/check-prompt-queues.mjs` - pass (260 tasks)
  - `node scripts/check-planning-architecture.mjs --self-test` - pass
  - `node scripts/check-planning-architecture.mjs` - pass
  - `node scripts/check-agent-instructions.mjs --self-test` - pass
  - `node scripts/check-agent-instructions.mjs` - pass
  - `git diff --check` - pass
- Checks not run:
  - frontend / npm - out of scope
  - full `dotnet test` solution - not required
- Run log: .ai/runs/2026-08-13-RQ101-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 3244723ecc05e09718088e2d4df59de050b1f634
- Main verification: git rev-parse origin/main -> 8c667c3b52af0af4b0c2bbf271b305d6713cb397; work SHA 3244723ecc05e09718088e2d4df59de050b1f634 is an ancestor
- Missed: none known for the named inventory empty/null/EOF contract
- Follow-up: `RQ102`
- Residual risk: other WebApplicationFactory hosts can still trip the EF provider-count warning if run together without the same suppression
- Prompt defect / scope repair: combined named filter required ignoring `ManyServiceProvidersCreatedWarning` in the two in-scope WAF factories
- Next: `RQ102` - Sales period, empty-success and scope-isolation tests

---

## RQ102 - Sales period, empty-success and scope-isolation tests

Status: DONE
Ready after: `RQ101` DONE
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

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: locked sales-summary empty-success meta, daily-sales empty-success meta, invalid range as API error, store/supplier isolation, and adjacent-window non-overlap
- Changed files:
  - Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs
  - Api.Tests/DailySalesStatsIntegrationTests.cs
  - Api.Tests/AnalyticsShoeTypeSalesIntegrationTests.cs
  - Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs
  - Api/Endpoints/CachedAnalyticsEndpoints.cs
  - Api/Services/DailySalesStatsService.cs
  - Api/Models/DailySalesStatsDto.cs
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md
  - docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
  - MASTER_ROADMAP.md
  - .ai/runs/2026-08-13-RQ102-evidence.md
- Contract/runtime behavior changed: yes; invalid `fromDate > toDate` on sales summary and top-products now returns 400; daily-sales empty period now includes standard `meta` (`success`, `emptyReason=no_data_in_period`, `insufficient_data`)
- Checks run:
  - `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests|FullyQualifiedName~DailySalesStatsIntegrationTests|FullyQualifiedName~AnalyticsShoeTypeSalesIntegrationTests|FullyQualifiedName~AnalyticsSupplierSalesIntegrationTests"` - pass (52)
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
  - `node scripts/check-prompt-queues.mjs` - pass (260 tasks)
  - `node scripts/check-planning-architecture.mjs --self-test` - pass
  - `node scripts/check-planning-architecture.mjs` - pass
  - `node scripts/check-agent-instructions.mjs --self-test` - pass
  - `node scripts/check-agent-instructions.mjs` - pass
  - `git diff --check` - pass
- Checks not run:
  - frontend / npm - out of scope (`P-UI-20` / `RQ104`)
  - full `dotnet test` solution - not required
- Run log: .ai/runs/2026-08-13-RQ102-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 54b317c1589c634372c02d41205894a58e1cedc7
- Main verification: git rev-parse origin/main -> 0d247493a9a2f2167e1b626ddfdefa27dd1ce6a8; work SHA 54b317c1589c634372c02d41205894a58e1cedc7 is an ancestor
- Missed: shoe/supplier live-DB empty/isolation proofs remain env-gated; sales-summary still uses inclusive `<= toDate` midnight (no RQ13/RQ26 rewrite; adjacent windows were asserted on that contract); previous-period helper stays a local function in AllEndpoints
- Follow-up: `RQ103`
- Residual risk: frontend Daily Sales TypeScript types do not yet declare `meta`; extra JSON field is ignored until `RQ104`/`P-UI-21`
- Prompt defect / scope repair: removed duplicate Purpose paragraph in this addendum header; invalid-range empty-success was a reproduced sales-summary/top-products contract bug
- Next: `RQ103` - Action outcome, not-measured and learning-eligibility tests

---

## RQ103 - Action outcome, not-measured and learning-eligibility tests

Status: DONE
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

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: locked LearningEligible=false for issued/accepted/rejected/ignored even with expected impact; LearningEligible=true only for executed+measured; pending/not_measured do not stamp OutcomeMeasuredAtUtc; gapReason codes stay stable
- Changed files:
  - Api.Tests/RecommendationLifecycleSemanticsTests.cs
  - Api.Tests/AnalyticsActionItemServiceTests.cs
  - Api.Tests/AnalyticsActionsCriticalWorkflowTests.cs
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
  - docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
  - MASTER_ROADMAP.md
  - .ai/runs/2026-08-13-RQ103-evidence.md
- Contract/runtime behavior changed: no; tests only
- Checks run:
  - `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~RecommendationLifecycleSemanticsTests|FullyQualifiedName~AnalyticsActionItemServiceTests|FullyQualifiedName~AnalyticsActionsCriticalWorkflowTests"` - pass (71)
  - first named run failed on upsert asserting stored `pending`/`issued`; retry after matching actual null-outcome + past-due ignored projection - pass
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
  - `node scripts/check-prompt-queues.mjs` - pass (260 tasks)
  - `node scripts/check-planning-architecture.mjs --self-test` - pass
  - `node scripts/check-planning-architecture.mjs` - pass
  - `node scripts/check-agent-instructions.mjs --self-test` - pass
  - `node scripts/check-agent-instructions.mjs` - pass
  - `git diff --check` - pass
- Checks not run:
  - frontend / npm - out of scope (`RQ104` / `P-UI-21`)
  - full `dotnet test` solution - not required
- Run log: .ai/runs/2026-08-13-RQ103-evidence.md
- Delivery mode: direct-main
- Main commit SHA: f4a797d14b0caca5df9ce1456bd22ef33f4ea360
- Main verification: git rev-parse origin/main -> c442401d09183c8ee1b8a1a9d9641fe4355b8485; work SHA f4a797d14b0caca5df9ce1456bd22ef33f4ea360 is an ancestor
- Missed: none known for the named learning-eligibility and not-measured timestamp contract
- Follow-up: `RQ104`
- Residual risk: leftover `OutcomeMeasuredAtUtc` on a `not_measured` row still exists if written outside UpdateOutcomeAsync; Project treats it as not learning-eligible
- Prompt defect / scope repair: none
- Next: `RQ104` - Core decision pages display backend truth

---

## RQ104 - Core decision pages display backend truth

Status: DONE
Ready after: `RQ100` DONE so backend fields are stable; path-safe vs `P-UI-19`
Priority: P2
Type: frontend-tests
Feature family: analytics-frontend-backend-truth
Parallel-safe: yes
Owner: cursor
Local lock: `.ai/task-locks/RQ104-<agent>.lock.md` (removed after DONE)
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

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: locked operator labels on PDC Why/timeline/evidence; Decision Board chips no longer dump workflow ActionType/Status; missing reliability stays Nije dostupno; error paths hide KPI blocks
- Changed files:
  - Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx
  - Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.spec.tsx
  - Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx
  - Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx
  - Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
  - docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md
  - docs/ai/ANALYTICS_TEST_STRATEGY.md
  - MASTER_ROADMAP.md
  - .ai/runs/2026-08-13-RQ104-evidence.md
- Contract/runtime behavior changed: yes; Decision Board filters workflow ActionType/Status codes from warning chips and hides the summary KPI grid on load error
- Checks run:
  - `npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx src/pages/ExecutiveDecisionBoardPage.spec.tsx src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/pages/ProdajaPrePostNivelacijePage.spec.tsx` - pass (37)
  - `npm run check:analytics-guardrails` - pass
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
  - `node scripts/check-prompt-queues.mjs` - pass (260 tasks)
  - `node scripts/check-planning-architecture.mjs --self-test` - pass
  - `node scripts/check-planning-architecture.mjs` - pass
  - `node scripts/check-agent-instructions.mjs --self-test` - pass
  - `node scripts/check-agent-instructions.mjs` - pass
  - `git diff --check` - pass
- Checks not run:
  - `dotnet build` / `dotnet test` - frontend display-contract prompt
  - full `npm run build` - named Vitest + guardrails/typecheck already run
- Run log: .ai/runs/2026-08-13-RQ104-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 94c87dcb87c11add6437de5f541bfeb5db281c2a
- Main verification: git rev-parse origin/main -> a089695d63df6ecf12f21fedd5aac301e75c9873; work SHA 94c87dcb87c11add6437de5f541bfeb5db281c2a is an ancestor
- Missed: none known for the named display-contract and error-without-KPI proofs
- Follow-up: `RQ105`
- Residual risk: other analytics pages can still dump unknown codes via replaceAll("_"," ") if they reuse the old board helper pattern
- Prompt defect / scope repair: none
- Next: `RQ105` - Operational fallback must not look like trusted analytics meta

---

## RQ597 - Compare Color API buckets with the independent raw-fact oracle

Status: DONE
Priority: P2
Type: backend-tests/integration
Feature family: color-bucket-independent-oracle
Parallel-safe: yes (test-only; owns a new test file and does not change analytics runtime semantics)
Owner: Analytics Reliability
Ready after: current-main adversarial audit residual confirmed; shared fixture, endpoint and independent oracle exist
Local lock: removed after done close
Commit suggestion: `test(analytics): compare color buckets with raw-fact oracle`

Claimed 2026-10-08 from refreshed `origin/main` `5c570b3a082742c17ebc935bb8f5cd6c2d6c9e22`; at claim time it was the only current RQ READY prompt, with no matching branch, lock or open PR, and its new test-file path was disjoint from BCI16 and RQ482.

### Problem

Color already has aggregate and cross-screen total checks, but no focused integration test compares every returned Color bucket with an independently computed raw-fact bucket. A dimension mapping or grouping error can therefore preserve grand totals while assigning units/revenue to the wrong color.

### Evidence

- `docs/qa/ANALYTICS_TESTS_ADVERSARIAL_AUDIT_2026-10-05.md`, residual 9 and its follow-up list: cross-screen coverage checks totals; bucket-level Color oracle parity remains.
- `Api.Tests/OperationsAnalyticsRawFactOracleIntegrationTests.cs` checks Color oracle bucket sums but does not call the Color endpoint.
- `Api.Tests/AnalyticsCrossScreenRevenueInvariantIntegrationTests.cs` checks Color totals, not bucket-level API-vs-oracle equality.
- `Infrastructure/Services/OperationsAnalyticsRawFactOracle.cs` provides `QueryColorBucketsAsync` with half-open time, store and dataScope filtering plus normalized color identity.
- `Api/Endpoints/AllEndpoints.cs` maps `/api/analytics/color-sales-stats`.
- `Api.Tests/Fixtures/operations-analytics-all-routes-seed.sql` supplies deterministic PostgreSQL rows.

### Scope

- Add `Api.Tests/ColorSalesStatsIndependentOracleIntegrationTests.cs` using the existing disposable PostgreSQL fixture, route registration and shared seed fixture.
- Compare the complete endpoint bucket set against `OperationsAnalyticsRawFactOracle.QueryColorBucketsAsync` by normalized bucket identity and assert each bucket's signed units and revenue. The Color API does not expose per-bucket sale-line count; assert oracle line counts as independent fixture/scope controls rather than inventing or adding a production response field.
- Include representative all-scope, imported/existing, store-filter and unknown-color cases only where the existing fixture supports them; retain half-open boundary and DUG/KOREKCIJA exclusions as independent controls.
- Update this addendum, the parent RQ routing mirror, `MASTER_ROADMAP.md`, and the run evidence when closing the prompt.

### Do not touch

- `Api/Endpoints/AllEndpoints.cs`, Color identity/formula policy, database objects or frontend behavior.
- RQ139/RQ140 status or acceptance; this fills the named Color test residual only.
- Product thresholds, recommendation ownership, or any production database.

### Read first

- `docs/ai/ANALYTICS_TESTS_ADVERSARIAL_AUDIT_2026-10-05.md` items 8-10 and follow-up list.
- `Api.Tests/OperationsAnalyticsRawFactOracleIntegrationTests.cs`.
- `Api.Tests/AnalyticsCrossScreenRevenueInvariantIntegrationTests.cs`.
- `Api.Tests/Fixtures/operations-analytics-all-routes-seed.sql`.
- `Infrastructure/Services/OperationsAnalyticsRawFactOracle.cs` and the Color endpoint response shape.
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md` and `docs/ai/VALIDATION_SELECTOR.md`.

### Do

1. Reuse the existing Testcontainers fixture and seed data; do not create a new test host or hand-maintained expected aggregates when the independent oracle can derive them.
2. Compare the complete normalized bucket sets, not only totals, and assert signed units and revenue for each endpoint bucket. Validate sale-line counts through oracle fixture/scope controls only because the endpoint response does not expose that value.
3. Prove at least one changed filter changes membership as expected and that endpoint and oracle agree for the same scope.
4. Keep an empty/unknown Color bucket distinct from a missing response or failed endpoint.
5. If a mismatch exposes a runtime defect, record the minimal counterexample and route the fix to the owning runtime prompt; do not widen this test-only prompt silently.

### Tests

- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ColorSalesStatsIndependentOracleIntegrationTests"`
- `git diff --check` and the repository queue/governance validators.
- Run with PostgreSQL/Testcontainers enabled when Docker is available; report an unavailable container as not run, never as passing.

### Acceptance

- Deterministic PostgreSQL integration coverage compares every Color API bucket identity, signed units and revenue to the independent raw-fact oracle for the selected fixture scopes; oracle sale-line counts remain explicit scope/population controls.
- The assertions would fail if values move between buckets while the overall Color total stays constant.
- No production/runtime behavior, business formula, migration or deployed database is changed.

### Dependencies

None. Existing endpoint, oracle, fixture and database test harness are already present. `RQ139` remains PARTIAL for its broader numeric-state/parity acceptance and is not a prerequisite for this isolated test slice.

### Completion note

- Date: 2026-10-08
- Status: DONE
- Completion: added two Testcontainers-backed integration tests comparing all normalized Color endpoint bucket identities, signed units and revenue against the independent raw-fact oracle across all/imported/existing-store scopes and separate Sep 1/Sep 2 boundary/receipt-exclusion windows.
- Changed files: `Api.Tests/ColorSalesStatsIndependentOracleIntegrationTests.cs`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`, `MASTER_ROADMAP.md`, `.ai/runs/2026-10-08-RQ597-evidence.md`.
- Checks run: `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore` passed (0 errors); Analytics Tests & Data Integrity run `37812228770` passed on `56923cf8feb930e16b4757b8bdb6f2765cdf6768` (full backend 1936 total, 1894 passed, 42 skipped, 0 failed; both RQ597 tests passed); governance validators and CRLF-aware `git diff --check` passed.
- Checks not run: local filtered integration command was not run because the local Docker daemon did not respond and no test connection string was configured; the exact two tests executed successfully in the Actions Testcontainers environment.
- Run log: `.ai/runs/2026-10-08-RQ597-evidence.md`
- Evidence state: pending post-close recovery synchronization.
- Delivery mode: direct-main.
- Main commit SHA: `56923cf8feb930e16b4757b8bdb6f2765cdf6768`.
- Main verification: freshly fetched `origin/main` contains implementation SHA `56923cf8feb930e16b4757b8bdb6f2765cdf6768`.
- Missed: local integration execution only; remote disposable-PostgreSQL proof passed.
- Follow-up: pending post-close recovery scan.
- Residual risk: none known; no production/runtime behavior or API contract changed.
- Post-close routing: pending fresh recovery after this terminal transition reaches `main`.
- Prompt defect / scope repair: removed the impossible per-bucket API sale-line-count assertion because the API does not expose that field; oracle counts remain explicit fixture/scope controls.

---

## RQ598 - Analytics regression gap inventory and targeted test proposals

Status: DONE
Ready after: none
Priority: P1
Type: audit/backend-tests/frontend-tests
Feature family: analytics-regression-gap-inventory
Parallel-safe: yes after path partitioning; RQ598 owns this addendum plus its regression audit/report/evidence, while RQ600 owns the Cross-Surface addendum plus its value matrix
Owner: Analytics Reliability / Codex
Local lock: `.ai/task-locks/RQ598-codex.lock.md` (released on closure)
Commit suggestion: `test(analytics): close verified regression gaps`

### Problem

Current analytics coverage is extensive, but the repository still needs a current source-to-test inventory that distinguishes real blind spots from equivalent existing proof. Missing named tests must not be treated as defects, and new tests must be added only for deterministic gaps that can be proved from current code and independent fixtures/oracles.

### Evidence

- `docs/ai/ANALYTICS_NEXT_EXECUTION_PROMPTS_2026-10-09.md` authorizes this audit-only prompt.
- `RQ597`, `BCI16` and the repository-local slice of `RQ140` are DONE and must be searched before declaring a gap.
- Existing backend suites, raw-fact oracles, contract tests and frontend analytics specs cover overlapping parts of Daily, Supplier, Shoe Type, Color, Supplier Footwear, Inventory, Pre/Post, Pre-Nivelacija, shift and action surfaces.

### Scope

- Map each named shipped metric/route to source, aggregation grain, approved formula/policy, test ID and independent-oracle presence.
- Include export/query consistency and the adversarial cases named by the owner plan.
- Add at most three deterministic independent tests only for VERIFIED_GAP observations that are demonstrably uncovered.
- Production/runtime code changes require a separately registered narrow defect prompt and are not part of RQ598.

### Read first

- `docs/ai/ANALYTICS_NEXT_EXECUTION_PROMPTS_2026-10-09.md`
- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/qa/ANALYTICS_TESTS_ADVERSARIAL_AUDIT_2026-10-05.md`
- RQ597, BCI16 and RQ140 prompts/evidence
- the nearest current backend, PostgreSQL-oracle, contract and frontend tests for every mapped surface

### Do

1. Reconcile fresh `origin/main`, all current RQ statuses and active ownership before auditing.
2. Build a source-to-test matrix for Daily, Supplier, Shoe Type, Color, Supplier Footwear, Inventory, Pre/Post, Pre-Nivelacija, shift and selected action screens.
3. Check unknown ID/display name, previous-only categories, negative/zero margins, missing costs, returns/adjustments, date/store filters, empty/missing/error/stale states, applied/draft filters, top-N scope, rounding and denominators.
4. Classify every observation as VERIFIED_GAP, COVERED or NEEDS_AUTHORITY with exact file/line evidence and risk.
5. Add no more than three deterministic independent tests for VERIFIED_GAP cases. If no reproducible gaps remain, close without speculative follow-ups.

### Tests

- Run the smallest focused tests selected by any VERIFIED_GAP additions.
- Run the live queue/planning governance validators and `git diff --check`.
- Record current-main CI state when a relevant run is available; never treat queued/skipped work as passing.

### Acceptance

- A durable source-to-test matrix covers all named surfaces and adversarial cases.
- Every observation is classified with exact repository evidence and no duplicate test proposal.
- At most three demonstrated gaps receive deterministic independent tests; production behavior remains unchanged.
- Canonical owner status/evidence and post-close routing are synchronized from fresh post-delivery `origin/main`.

### Completion note

- Date: 2026-10-09
- Status: DONE
- Completion: source-to-test inventory completed; two deterministic frontend export/query identity gaps closed; no runtime or formula change
- Changed files: `docs/qa/ANALYTICS_REGRESSION_GAP_INVENTORY_2026-10-09.md`; `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx`; `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.spec.tsx`; `.ai/runs/2026-10-09-RQ598-evidence.md`
- Checks run: focused Daily/Shoe Type specs 29/29; `npm run typecheck`; queue/planning validators; final `git diff --check`
- Checks not run: full frontend/backend suites (narrow audit scope); production certification/read-only validation (STAB16 authority)
- Run log: `.ai/runs/2026-10-09-RQ598-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `4f765418177ba8042093ab83a867f276ab97c3da`
- Main verification: freshly fetched `origin/main` resolves to `4f765418177ba8042093ab83a867f276ab97c3da` and contains the implementation commit
- Missed: production freshness, certification and business sign-off remain open
- Follow-up: RQ600 remains READY for the independent action-eligibility/value-contract audit
- Residual risk: remote CI and production/business validation remain open; no repository-local regression was found beyond the two closed test gaps
- Post-close routing: RQ600 remains READY after the full 13-file RQ/SQL dependency cascade; no additional promotion required
- Prompt defect / scope repair: none; RQ599 promotion condition was not met

### Dependencies

None. STAB16 retains production certification/freshness ownership and does not block this repository-local audit.
