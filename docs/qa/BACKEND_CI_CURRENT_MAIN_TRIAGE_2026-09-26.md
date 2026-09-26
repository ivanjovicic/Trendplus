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

## Current-main recheck — run `36269623614`

- Head SHA: `47c0a5c0d066effcb524b266661aac8810486fb0`
- Event: push to `main`
- Concurrency group: `analytics-backend-tests-refs/heads/main`
- Run result: `failure`; both jobs completed teardown normally, so the run was not stuck, cancelled, or waiting for cleanup/bootstrap.
- RQ447 certification job `108480955813`: `success`; oracle `4/4`, all-routes `1/1`, OP2 `4/4`, frontend seam and artifact upload all passed.
- Complete backend analytics suite job `108480955962`: `failure` only in the all-tests step; restore, build and migration/bootstrap smoke passed.
- Broad totals: `1475 total / 1410 passed / 27 failed / 38 skipped`.

The 27 failures are a smaller but still mixed set: five SQL Server supplemental contract failures, three supplier-report warning-label assertions, one color cache-key version assertion, two outbox/data-source/Access integration groups, and several full-suite provider/isolation or database-schema failures. They are not one cleanup/bootstrap fault and are not safe to hide with a filter.

The wide CI job now explicitly sets `Analytics__AllowLoopbackInProduction=true`, matching the already-certified isolated RQ447 job. This is a test-environment declaration only; the production resolver remains fail-closed. It removes misleading loopback-resolution noise from the broad job but does not claim the 27 assertion/provider failures are repaired.

RQ448 was re-evaluated after this run and remains `WAITING`: the local/browser session inventory has no authenticated tab, API/deployment access is unavailable, and mocked frontend tests would not satisfy raw-facts → API → rendered screen → detail → CSV/XLSX acceptance.

## Certification recheck — run `36270291395`

- Head SHA: `62c7616db950a8f7781cb5c7695e4fbea8358406`
- Concurrency group: `analytics-backend-tests-refs/heads/main`
- Run result: `failure`; both jobs completed normally and the failure was not caused by queue, cancellation or cleanup.
- RQ447 certification job `108482830923`: `success`; oracle `4/4`, all-routes `1/1`, OP2 `4/4`, frontend seam and artifact upload all passed.
- Complete backend analytics suite job `108482831006`: restore, build and lifecycle smoke passed; all-tests step failed with `1475 total / 1398 passed / 39 failed / 38 skipped`.

The first actionable broad-suite schema failures are concrete: `PerformanceLogs` and `analytics_refresh_runs` are missing from the service database, and startup logs also show concurrent duplicate-extension activity. The previous smoke test uses its own Testcontainers database and therefore does not bootstrap the CI service database used by the full suite. The workflow is being repaired to use the canonical pgvector service plus explicit EF migrations and the same startup SQL bootstrap already proven by RQ447. The broad gate remains open until that exact SHA produces a fresh result.

The OP2 failure from the preceding run was a transient `503 db_warmup` response. The bounded retry repair in `SupplierShoeTypeRq447SeamIntegrationTests` was validated by this run and the full RQ447 certification is now green.

## Next focused work

- Reconcile the SQL Server session helper/tests against one canonical QDB03 contract, including `MaxRows`, deterministic full-scan ordering, safe connection diagnostics and parameter representation.
- Reconcile supplier negotiation warning assertions with the current localized response contract without weakening warning visibility.
- Isolate the remote full-suite provider/bootstrap/order failures using the exact CI image and test-host lifecycle; keep skipped tests visible and failing for certification.

## Current-main recheck — run `36270728235`

- Head SHA: `067cd68b6d88ee6e5e24f6e9e2046f675a3ed85e`
- Event: push to `main`
- Concurrency group: `analytics-backend-tests-refs/heads/main`
- Run result: `failure`; both jobs completed normally and teardown completed. The earlier `898202b0` run was cancelled only because this newer same-ref push superseded it under the configured `cancel-in-progress: true` policy.
- RQ447 certification job `108484268364`: `success`; migrations, startup SQL bootstrap, oracle `4/4`, all-routes `1/1`, OP2 `4/4`, frontend seam and artifact upload all passed. Artifact: `rq447-certification-067cd68b6d88ee6e5e24f6e9e2046f675a3ed85e`.
- Complete backend analytics suite job `108484268134`: setup, pgvector service, both EF migration contexts, startup SQL bootstrap and lifecycle smoke all passed; only the full test step failed.
- Broad totals: `1477 total / 1398 passed / 41 failed / 38 skipped`.

The shared CI database bootstrap repair is therefore validated: the previous concrete `PerformanceLogs` / `analytics_refresh_runs` schema failures are no longer the setup blocker. The remaining 41 failures are mixed existing contract/provider/isolation families (supplier warning projections, worker catalog, Access/import and data-source integration, PostgreSQL-backed scope/aggregation, SQL Server supplemental contracts, demo endpoint host setup, cache-key version and pgvector/provider assumptions). They are not safe to hide with a filter or to relabel as RQ448 evidence.

RQ448 remains `WAITING`, not claimed: RQ447's live CI certificate is green, but this workspace still has no authenticated browser tab/session and no deployment/API access. Its required raw-facts → API → rendered Supplier/Shoe Type screen → detail → CSV/XLSX reconciliation cannot be honestly completed from mocked or static tests.

## Remaining-today-commits audit — local repair set

The remaining 2026-09-26 commit set was re-read against the Operations accuracy, BCI10, RQ440/RQ442/RQ446/RQ448/RQ450/RQ456/RQ457/RQ458/RQ459 and STAB prompt requirements. RQ447 certification and its CI workflow were not reopened; the audit found three deterministic contract drifts outside that gate:

- Daily Sales golden data still expected off-shift units inside the first measured shift. The current RQ383/RQ430 contract intentionally keeps daily totals inclusive while shift columns remain measured-window-only. The golden was corrected from `7` to `5` for `2026-01-01`; the focused service suite is green.
- The startup Operations integrity probe was registered in `Api/Program.cs` but missing from the central worker catalog. The catalog now includes it as a non-controllable web startup service, restoring the single-source-of-truth invariant.
- Supplier Decision warning assertions still used the old English/mixed labels (`Korišćen fallback dataset`, `Visok missing cost`, `Data Quality ekran`) while the current localized API contract emits `Korišćen pomoćni skup podataka`, `Nedostaje nabavna cena`, and `ekran Kvalitet podataka`. Tests now assert the current contract without weakening warning presence.

Focused local result: Worker registry + supplier negotiation/report contracts `50 passed, 0 failed, 0 skipped`; Daily Sales service contracts `11 passed, 0 failed, 0 skipped`. The Daily Sales integration subset was not used as a local gate because this VM has no PostgreSQL host; RQ447's remote pgvector certificate remains the authoritative integration evidence.
