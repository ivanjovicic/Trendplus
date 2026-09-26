# Backend CI current-main triage — 2026-09-26

Status: PARTIAL
Owner: Codex
Prompt: BCI10 re-entry
Evidence state: synchronized for the inspected run; acceptance remains open

## Scope

This note records the current-main re-entry after the broad backend suite became red again. It does not reopen the green RQ447 certification gate and does not claim an overall-green workflow.

## Remote workflow truth

- Repository: `ivanjovicic/Trendplus`
- Workflow: `Analytics Tests & Data Integrity` (`analytics-tests.yml`)
- Concurrency group: `analytics-backend-tests-${{ github.ref }}`
- Concurrency policy: `cancel-in-progress: true`
- Run: `36264090648`
- Head SHA: `c95171dff7bc8ad4dccb18dcefa7226df351f53d`
- Event: pull request
- Run result: `failure`
- URL: https://github.com/ivanjovicic/Trendplus/actions/runs/36264090648

The run was not stuck, queued, or waiting for cleanup/bootstrap. Both jobs initialized and completed their teardown steps.

### Job outcomes

| Job | Result | Relevant steps |
|---|---|---|
| RQ447 Supplier/Shoe Type certification (`108465306000`) | success | containers, restore, build, migrations, bootstrap, oracle `4/4`, all-routes `1/1`, OP2 `4/4`, frontend seam and artifact upload all passed |
| Complete backend analytics suite (`108465306251`) | failure | restore, build and migration/bootstrap smoke passed; `Run all backend tests with coverage` failed; coverage summary and artifact upload still completed |

Broad-suite totals from the failed test step: `1475 total / 1394 passed / 43 failed / 38 skipped`.

## Failure families

The failures are not one cleanup/bootstrap failure:

1. SQL Server source-session contract drift: the deterministic supplemental SQL tests fail on mode naming, cursor parameter expectation, missing-alias ordering, and `MaxRows`; the connection test is also environment-sensitive without a SQL Server host.
2. Color cache contract drift: the test still expects `color-sales-stats:v4`, while the current implementation emits `v5`.
3. Supplier negotiation/report warning projection drift: focused tests observe localized/current row labels while older assertions still search for the previous fallback/missing-cost labels.
4. Minimal-API test-host registration/binding: cache authorization tests failed before authorization because `AnalyticsRefreshStatusService` was inferred as a request body in a host that intentionally does not register the full production service graph.
5. Full-suite provider/isolation noise: the remote log also contains duplicate extension/deadlock, missing `PerformanceLogs`/`InventoryMovementFacts`, and in-memory relational-provider errors. These need a separate focused CI/test-host repair; they are not safe to repair by loosening the broad gate.

## Bounded repair completed

`Api/Endpoints/CachedAnalyticsEndpoints.cs` now marks the four `AnalyticsRefreshStatusService` route dependencies explicitly as `[FromServices]`. This preserves the production service contract and makes endpoint metadata construction independent of accidental service registration in the narrow authorization test host.

Focused result after the change:

```text
AnalyticsCacheInvalidateAuthorizationTests: 4 passed, 0 failed, 0 skipped
```

## Decision

BCI10 remains `PARTIAL`. The current-main run is valid evidence of a red broad backend gate, while RQ447 remains green for its dedicated certification acceptance. The remaining SQL/session, report-label, and full-suite database/provider families require focused follow-up ownership; this re-entry does not close BCI10 and does not mark the broad workflow green.

## Next focused work

- Reconcile the SQL Server session helper/tests against one canonical QDB03 contract, including `MaxRows`, deterministic full-scan ordering, safe connection diagnostics and parameter representation.
- Reconcile supplier negotiation warning assertions with the current localized response contract without weakening warning visibility.
- Isolate the remote full-suite provider/bootstrap/order failures using the exact CI image and test-host lifecycle; keep skipped tests visible and failing for certification.
