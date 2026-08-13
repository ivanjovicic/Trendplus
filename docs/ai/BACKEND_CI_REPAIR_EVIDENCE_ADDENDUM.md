# Trendplus Backend CI Repair - Evidence Follow-up Addendum

Created: 2026-08-10
Repo: `ivanjovicic/Trendplus`
Current READY prompt: `BCI09`
Owner program: `BCI`
Parent queue: `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`

Purpose: close evidence that the original BCI prompts explicitly required but their completion notes did not fully observe. This addendum does not reopen already-implemented CI/build changes and must not be used to bypass the real backend assertion repairs owned by `RQ89` and `RQ90`.

## Status summary

| Task | Status | Purpose |
|---|---|---|
| BCI08 | DONE | Isolate the current full-suite CI-only integration failures that do not reproduce in focused local runs |
| BCI05 | PARTIAL | Green GHA on `aed38ff` after BCI08; current main is red at build because test cache stubs omit `GetFootprintSnapshot()` |
| BCI09 | READY | Add `GetFootprintSnapshot()` to the five `IAnalyticsCacheService` test stubs so `Api.Tests` compiles again |
| BCI06 | WAITING | Verify the BCI03 mixed-solution/JavaScript SDK model in Windows/Visual Studio or document a proven support boundary |

---

## BCI05 - Close full backend suite and GitHub Actions evidence

Status: PARTIAL
Ready after: `RQ89`/`RQ90` DONE; re-entry after `RQ91`/`RQ92`/`RQ93` DONE; re-entry after `RQ94` DONE; re-entry after `RQ95` DONE; re-entry after `BCI08` DONE; re-entry after `BCI09`
Priority: P0
Type: CI/evidence/tests
Feature family: backend-ci-final-evidence
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/BCI05-codex.lock.md`
Commit suggestion: `test(ci): close backend suite evidence`

### Problem

`BCI01` correctly remains PARTIAL because restore/build are fixed but the backend test step is still red. `BCI02` implemented diagnostic-cascade hardening, but its completion note explicitly says live GitHub Actions annotation shape was not verified after the YAML change. Focused root-cause work must not be mistaken for a green full backend gate.

### Evidence

- BCI04 recorded `741 passed / 40 failed` after bootstrap repair.
- `STAB09`, `RQ77`, and `RQ78` are already DONE.
- `RQ89` and `RQ90` own the remaining explicitly triaged list-contract failures and must be closed first.
- BCI02 evidence log says live post-push GitHub Actions annotation-shape verification was not run.
- BCI01 acceptance requires exact GitHub Actions run ID, test totals and a successful backend test step before DONE.

### Scope

- `.github/workflows/analytics-tests.yml` only if evidence reveals a CI-reporting defect
- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md` status/evidence note only after proof exists
- `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md`
- dated `docs/qa/` evidence report
- test artifacts/log inspection

Do not change application runtime behavior in this prompt. If a new product/test root cause appears, stop and create/reuse a focused repair prompt.

### Read first

- `MASTER_ROADMAP.md`
- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`
- `docs/qa/BACKEND_CI_FAILURE_TRIAGE_2026-08-06.md`
- final notes for `RQ89` and `RQ90`
- `.github/workflows/analytics-tests.yml`

### Do

1. Confirm RQ89 and RQ90 are DONE with their focused tests green.
2. Run the exact complete backend test command from BCI01 locally/equivalent when available; record totals without filtering or exclusions.
3. Push/inspect a GitHub Actions run for the resulting main commit.
4. Record restore, build and test step outcomes plus exact run/job IDs.
5. Verify a green test run requires and publishes Cobertura coverage.
6. Verify TRX/coverage artifacts exist and upload successfully.
7. Verify BCI02 diagnostic behavior from real workflow evidence:
   - a test failure remains the primary red cause when naturally available;
   - missing coverage/artifacts after an upstream failure do not create misleading secondary root failures.
8. If the full suite is green, update BCI01 to DONE with exact evidence.
9. If any test fails, keep BCI01 PARTIAL and create/reuse one prompt for each new root-cause family; do not weaken assertions or the workflow.

### Tests

- full `Api.Tests/Api.Tests.csproj` Release suite, no filters
- PostgreSQL-backed CI integration population actually executes
- successful run produces coverage
- successful run uploads test/coverage artifacts
- no `continue-on-error`, test exclusions or silent skips introduced
- workflow summary identifies the real primary failure on a red run when such evidence exists

### Acceptance

- One recorded GitHub Actions run has restore=success, build=success, test=success.
- Exact test totals and run/job IDs are recorded.
- Coverage and artifacts are present on the successful run.
- BCI02 live diagnostic behavior is observed or explicitly remains PARTIAL with a concrete evidence gap.
- BCI01 is changed to DONE only if all of its original acceptance conditions are now proven.

### Notes

- Date: 2026-08-10 (initial pass)
- Evidence report: `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-10.md`
- Local Docker suite (`CI=true`): 809 total / 801 passed / 8 failed
- Latest main GHA run `31378849007` job `93424204254`: restore=success, build=success, test=failure; coverage-summary=success; artifact upload=success (`9059086207`)
- BCI02 red-run diagnostic behavior observed; green-run annotation proof still unavailable
- `BCI01` remains PARTIAL
- Created follow-up assertion prompts: `RQ91` (READY), `RQ92`, `RQ93`
- Next: `RQ91` then `RQ92` then `RQ93`, then re-enter this prompt for green suite/GHA closure

### Notes (re-entry after RQ94)

- Date: 2026-08-11
- Evidence report: `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-11.md`
- Local Docker suite (`CI=true`): 809 total / 802 passed / 7 failed
- Durable remaining failure: mojibake expected resolution note in `UpdateOutcomeAsync_MergesResolutionSnapshot_WithoutOverwritingCreationSnapshot`
- Six `WorkerRuntimePolicyServiceTests` failures were Testcontainers Docker-pipe timeouts and did not reproduce on focused re-check
- Created follow-up: `RQ95` READY
- GHA on committed HEAD `9e53f2cc` still red; green GHA requires commit/push after RQ95
- `BCI01` remains PARTIAL
- Next: `RQ95`, then re-enter this prompt

### Notes (re-entry after RQ95)

- Date: 2026-08-11
- `RQ95` DONE: mojibake assert fixed in `AnalyticsActionItemServiceTests.cs`; focused outcome tests 5/5 pass
- `BCI05` promoted READY for full suite + GHA green proof
- GHA on committed HEAD `9e53f2cc` still red; green GHA requires commit/push of repair worktree
- `BCI01` remains PARTIAL until green GHA evidence
- Next: re-enter `BCI05` full suite with Docker

### Notes (re-entry after RQ95 — suite run)

- Date: 2026-08-11
- Evidence report: `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-11_RQ95_REENTRY.md`
- Local Docker suite (`CI=true`): 809 total / 809 passed / 0 failed
- RQ95 fix confirmed in full suite; no new failure families
- WorkerRuntime flake did not reproduce
- GHA on committed HEAD `9e53f2cc` still red; green GHA requires commit/push of repair worktree
- `BCI01` remains PARTIAL until green GHA evidence on pushed commit
- Next: commit/push repairs, then record green GHA run IDs

### Notes (current execution)

- Date: 2026-08-11
- Owner switched to `Codex` for the final `BCI05` execution pass on the current `main` worktree
- Local lock: `.ai/task-locks/BCI05-codex.lock.md`
- Immediate plan: re-run targeted sanity checks, then execute the full local backend suite, and only then commit/push for green GitHub Actions evidence

### Notes (current execution result)

- Date: 2026-08-11
- Evidence report: `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-11_CODEX_REENTRY.md`
- Local exact BCI05 sequence: restore=success, build=success, test=success
- Local suite totals: 809 total / 809 passed / 0 failed
- Coverage pipeline gap closed locally: `Api.Tests/coverage.runsettings` now emits Cobertura-only output and produced `TestResults/75b4a260-31d7-43b6-b31f-b4a2540166a7/coverage.cobertura.xml`
- Focused corroboration on the same worktree: `AnalyticsActionItemServiceTests` 36/36, `AnalyticsActionsPage.spec.tsx` 14/14, `npm run build` success, `npm run check:analytics-guardrails` success
- GHA proof is still pending commit/push from this worktree
- Next: commit/push current worktree, capture green `analytics-tests` run/job IDs, then close `BCI05` and `BCI01` with remote evidence

### Notes (current execution result - 2026-08-12)

- Date: 2026-08-12
- Evidence report: `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-12.md`
- Latest relevant GitHub Actions proof is still red:
  - workflow run `31575771867`
  - workflow job `94047422144`
  - head SHA `9c5fb2c6a2254f364ad2247a133413709860bd69`
  - restore=success, build=success, test=failure, coverage-summary=success, artifact upload=success
- TRX totals from the uploaded artifact:
  - `829 total / 825 passed / 4 failed`
- Failing tests in the latest run:
  - `Api.Tests.DemoEnvironmentVerificationEndpointTests.DemoVerification_ReturnsUnsafe_WhenNoProofInputsArePresent`
  - `Api.Tests.InventoryListEndpointIntegrationTests.InventoryList_UncachedRouteMatchesSeededRowCountAndEmptyMeta`
  - `Api.Tests.AccessImportRunEndpointTests.PostRun_WhenStoragePreparationTimesOut_ReturnsGatewayTimeout`
  - `Api.Tests.AccessImportRunEndpointTests.PostRun_ReturnsAccepted_AndInvokesImportServiceOnce`
- Focused local repro on current `HEAD` (`e2ebd1d0311617901587184c798f08e0335a5f60`) does not reproduce the failing family:
  - targeted access-import + demo verification tests pass `3/3`
  - `InventoryListEndpointIntegrationTests` pass `7/7`
- Backend/workflow equivalence check:
  - `git diff --name-only 9c5fb2c6..HEAD -- Api Api.Tests Application Domain Infrastructure .github/workflows/analytics-tests.yml` -> no differences
- Interpretation:
  - the current blocker is no longer "commit/push missing";
  - the latest backend-equivalent main proof is a real full-suite CI failure;
  - the failure family now looks like shared host/database isolation or order dependence inside the full suite, not a focused deterministic assertion regression
- Decision:
  - keep `BCI01` PARTIAL
  - mark `BCI05` PARTIAL
  - promote `BCI08` READY as the next focused repair prompt

### Notes (re-entry after BCI08 — 2026-08-13)

- Date: 2026-08-13
- Evidence report: `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-13.md`
- Green GHA on BCI08 commit `aed38ff133068388db9175e8e09ddd427d37337e`:
  - run `31598948469` / job `94121251668`
  - restore=success, build=success, test=success, coverage-summary=success, artifact upload=success (`9142326802`)
  - exact TRX totals unknown without authenticated artifact download
- Current main is backend-equivalent to `2fbea01fe15ef38eb00e5c0219ae3675976c6848` and is red:
  - run `31622706051` / job `94201257231`
  - restore=success, build=failure, test=skipped
  - five `IAnalyticsCacheService` test stubs missing `GetFootprintSnapshot()`
- Local `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` reproduces the same five `CS0535` errors
- BCI02 green-run coverage/artifact proof now exists on `31598948469`; the later red run still attributes the primary failure to build rather than coverage/artifacts
- `BCI01` remains PARTIAL
- Next: `BCI09`, then re-enter this prompt

### Dependencies

- `RQ89` DONE
- `RQ90` DONE
- no known unassigned BCI04 root-cause family remains before the 2026-08-12 re-entry
- re-entry after `RQ91`/`RQ92`/`RQ93` DONE for final green proof
- additional re-entry after `RQ94` DONE
- additional re-entry after `RQ95` DONE

---

## BCI08 - Stabilize full-suite CI integration isolation for access-import, demo verification and inventory routes

Status: DONE
Ready after: `BCI05` PARTIAL evidence on 2026-08-12
Priority: P0
Type: backend/tests/ci
Feature family: backend-ci-full-suite-isolation
Parallel-safe: no
Owner: cursor
Local lock: removed
Commit suggestion: `test(ci): stabilize backend full-suite integration isolation`

### Problem

`BCI05` can no longer close on "just push and capture green GHA." The latest backend-equivalent GitHub Actions run on `main` still fails the full suite, but the failing tests pass when executed in focused local runs on the current backend-equivalent `HEAD`. That makes this a full-suite CI isolation/order-dependence problem until proven otherwise.

### Evidence

- Latest relevant GitHub Actions run: `31575771867`
- Latest relevant job: `94047422144`
- Head SHA: `9c5fb2c6a2254f364ad2247a133413709860bd69`
- Workflow outcomes: restore=success, build=success, test=failure, coverage-summary=success, artifact-upload=success
- Uploaded TRX totals: `829 total / 825 passed / 4 failed`
- Failing tests:
  - `DemoEnvironmentVerificationEndpointTests.DemoVerification_ReturnsUnsafe_WhenNoProofInputsArePresent`
  - `InventoryListEndpointIntegrationTests.InventoryList_UncachedRouteMatchesSeededRowCountAndEmptyMeta`
  - `AccessImportRunEndpointTests.PostRun_WhenStoragePreparationTimesOut_ReturnsGatewayTimeout`
  - `AccessImportRunEndpointTests.PostRun_ReturnsAccepted_AndInvokesImportServiceOnce`
- First failure signatures from TRX/logs:
  - demo verification: expected warning `connection_string_unavailable_or_unreadable`, actual warnings collection empty
  - inventory uncached route: expected `200 OK`, actual `500 InternalServerError`
  - access import timeout path: expected `504 GatewayTimeout`, actual `503 ServiceUnavailable`
  - access import accepted path: expected `202 Accepted`, actual `503 ServiceUnavailable`
- Local focused evidence on `e2ebd1d0311617901587184c798f08e0335a5f60`:
  - targeted access-import + demo tests: pass `3/3`
  - full `InventoryListEndpointIntegrationTests`: pass `7/7`
- Backend/workflow diff from the failing GHA commit to current `HEAD` is empty:
  - `git diff --name-only 9c5fb2c6..HEAD -- Api Api.Tests Application Domain Infrastructure .github/workflows/analytics-tests.yml`
- The GHA job log also shows repeated missing-table noise (`PerformanceLogs`, `InventoryMovementFacts`) around the failing integration paths, which points at suite-order/shared-host state rather than a single new product contract.

### Scope

- `Api.Tests/AccessImportRunEndpointTests.cs`
- `Api.Tests/DemoEnvironmentVerificationEndpointTests.cs`
- `Api.Tests/InventoryListEndpointIntegrationTests.cs`
- any shared backend test-host helpers they actually use
- `Api/Program.cs`, `Api/Endpoints/AdminConfigEndpoints.cs`, `Api/Endpoints/AccessImportEndpoints.cs`, `Api/Services/Startup/*` only if the root cause is proven to be runtime/shared-startup state rather than test setup
- `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md`
- one dated `docs/qa/` evidence note for the focused repair

Do not weaken the GitHub Actions workflow, skip tests, add retries to hide failures, or broaden this prompt into unrelated analytics correctness work.

### Read first

- `MASTER_ROADMAP.md`
- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`
- `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md`
- `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-12.md`
- `docs/qa/BACKEND_CI_FAILURE_TRIAGE_2026-08-06.md`
- `Api.Tests/AccessImportRunEndpointTests.cs`
- `Api.Tests/DemoEnvironmentVerificationEndpointTests.cs`
- `Api.Tests/InventoryListEndpointIntegrationTests.cs`

### Do

1. Reproduce the four failing tests in the narrowest focused groups and in the full suite on the current backend-equivalent `HEAD`.
2. Prove whether the failure class is:
   - shared database/test isolation;
   - shared host/service-registration leakage;
   - order dependence across integration hosts;
   - startup readiness / connection-string state pollution;
   - or a real runtime regression that only the focused filters missed.
3. Inspect the shared host/test setup used by the three failing classes before changing runtime code.
4. If the root cause is test-host or suite-order isolation, make the smallest deterministic fix there first.
5. Change production runtime code only if the exact same source-of-truth defect is proven outside the test harness.
6. Re-run:
   - the exact four-test filter;
   - each affected class;
   - the full `Api.Tests/Api.Tests.csproj` suite or the closest local equivalent available;
   - and then inspect the next GitHub Actions run for the resulting commit.
7. Return to `BCI05` only after the focused family is closed or truthfully reduced to a smaller residual set.

### Tests

- exact four-test filter from the latest red GHA artifact
- `AccessImportRunEndpointTests`
- `DemoEnvironmentVerificationEndpointTests`
- `InventoryListEndpointIntegrationTests`
- full `Api.Tests/Api.Tests.csproj` Release suite
- GitHub Actions `analytics-tests.yml` run on the resulting commit

### Acceptance

- The four failing tests are either fixed or reduced to a smaller, newly evidenced root-cause family.
- The fix is deterministic and does not rely on retries, sleep inflation or skipped assertions.
- Full-suite local evidence and a fresh GitHub Actions run are both recorded.
- `BCI05` is re-entered only after this isolation family is resolved or narrowed with new proof.

### Dependencies

- `BCI05` PARTIAL evidence from 2026-08-12
- latest GHA artifact/log proof from run `31575771867`
- no newer backend/workflow commit than `9c5fb2c6` before the next agent starts runtime work

### Notes

- Date: 2026-08-12
- Evidence report: `docs/qa/BACKEND_CI_FULL_SUITE_ISOLATION_EVIDENCE_2026-08-12.md`
- Root cause: CI env leakage into test hosts (connection strings, analytics DB stub gap, Linux mdb-tools runtime gate)
- Focused BCI08 family with CI env: **14/14 pass**; exact four-test filter: **4/4 pass**
- Full suite with CI env: **826/829**; remaining 3 failures are Testcontainers Docker-pipe timeout in `AccessImportExecutionStrategyTests` (environment flake, not BCI08 family)
- Next: commit/push, re-enter `BCI05` for green GHA proof

---

## BCI09 - Implement GetFootprintSnapshot on IAnalyticsCacheService test stubs

Status: READY
Ready after: `BCI05` PARTIAL evidence on 2026-08-13
Priority: P0
Type: tests/ci
Feature family: backend-ci-cache-footprint-stubs
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/BCI09-<agent>.lock.md`
Commit suggestion: `test(ci): implement cache footprint snapshot on test stubs`

### Problem

PERF13 added `IAnalyticsCacheService.GetFootprintSnapshot()` and production implementations, but five `Api.Tests` cache stubs were not updated. Current `main` therefore fails the GitHub Actions **build** step, so the test step never runs. BCI08's green suite on `aed38ff` is no longer the current-main proof.

### Evidence

- Green GHA after BCI08: run `31598948469` / job `94121251668` on `aed38ff133068388db9175e8e09ddd427d37337e`
- Red current-main GHA: run `31622706051` / job `94201257231` on `2fbea01fe15ef38eb00e5c0219ae3675976c6848`
- Local `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` reproduces `CS0535` on the same five stubs
- Dated report: `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-13.md`
- Production implementations already exist in `DisabledAnalyticsCacheService`, `InMemoryCacheService`, and `HybridCacheService`

### Scope

- `Api.Tests/AnalyticsReportsContractTests.cs` (`StubAnalyticsCacheService`)
- `Api.Tests/CachedAnalyticsFailureContractTests.cs` (`ThrowingAnalyticsCacheService`)
- `Api.Tests/AnalyticsCacheInvalidateAuthorizationTests.cs` (`RecordingAnalyticsCacheService`)
- `Api.Tests/AnalyticsCacheAdminServiceTests.cs` (`RecordingAnalyticsCacheService`)
- `Api.Tests/AnalyticsAggregationWorkerTests.cs` (`RecordingAnalyticsCacheService`)
- `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md`
- one dated `docs/qa/` evidence note

Do not change production cache classes, `CachedAnalyticsEndpoints.cs`, workflow YAML, or PERF measurement scripts. Do not invent cache-hit counts in stubs.

### Read first

- `MASTER_ROADMAP.md`
- `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md`
- `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-13.md`
- `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`
- `Infrastructure/Services/Caching/DisabledAnalyticsCacheService.cs`
- the five test stub classes listed in Scope

### Do

1. Add `GetFootprintSnapshot()` to each of the five test stubs.
2. Return a non-throwing empty/disabled snapshot, matching `DisabledAnalyticsCacheService` (`new("disabled", false, false, 0)` or equivalent), unless a focused test proves a throw is required.
3. Do not change throw/recording behavior of existing Get/Set/Remove methods.
4. Prove `dotnet build Api.Tests/Api.Tests.csproj --configuration Release`.
5. Re-run the five affected test classes, or the nearest focused filters.
6. Return to `BCI05` after a green local build; do not mark `BCI01` DONE from this prompt.

### Tests

- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AnalyticsReportsContractTests|FullyQualifiedName~CachedAnalyticsFailureContractTests|FullyQualifiedName~AnalyticsCacheInvalidateAuthorizationTests|FullyQualifiedName~AnalyticsCacheAdminServiceTests|FullyQualifiedName~AnalyticsAggregationWorkerTests"`
- no production cache files in the diff

### Acceptance

- `Api.Tests` Release build succeeds.
- All five stubs implement `GetFootprintSnapshot()` without weakening existing assertions.
- Production cache/runtime behavior is unchanged.
- `BCI05` is re-entered for green GHA proof; `BCI01` stays PARTIAL until that later run.

### Dependencies

- `BCI05` PARTIAL evidence from 2026-08-13
- interface method already present on `IAnalyticsCacheService` from `2fbea01`

---

## BCI06 - Verify Windows and Visual Studio mixed-solution compatibility

Status: WAITING
Ready after: `BCI01` DONE, or earlier only when a Windows/Visual Studio-capable environment is explicitly available without delaying P0 backend assertion repair
Priority: P2
Type: build-system/evidence
Feature family: mixed-solution-windows-compatibility
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/BCI06-<agent>.lock.md`
Commit suggestion: `test(build): verify visual studio solution compatibility`

### Problem

BCI03 selected an explicit backend `.slnf` and changed both JavaScript SDK pins to an available package version. Its own prompt explicitly required `PARTIAL` if Windows/Visual Studio compatibility could not be verified. The completion evidence says Visual Studio IDE load was not tested, so the implementation is useful but one original acceptance proof remains open.

### Evidence

- `Trendplus2.Backend.slnf` restore/build was proven.
- `dotnet restore Trendplus2.sln --force` was proven after the SDK pin change.
- `Trendplus.POS.Ui` npm build was proven.
- BCI03 completion note explicitly says Visual Studio IDE load of the mixed solution was not opened in that session.
- The original BCI03 test matrix included clean Windows/Visual Studio restore when mixed-solution support is claimed.

### Scope

- `Trendplus2.sln`
- `Trendplus2.Backend.slnf`
- `Klijent/Klijent.esproj`
- `Trendplus.POS.Ui/Trendplus.POS.Ui.esproj`
- `docs/ci/SOLUTION_AND_FRONTEND_BUILD_CONTRACT.md`
- `.github/workflows/analytics-quality-gates.yml` only if a lightweight automated compatibility check is justified
- this evidence addendum / dated QA evidence

Do not change application business logic, npm dependencies or backend test expectations.

### Read first

- BCI03 in `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`
- `.ai/runs/2026-08-06-BCI03-evidence.md` if present
- `docs/ci/SOLUTION_AND_FRONTEND_BUILD_CONTRACT.md`
- both `.esproj` wrappers and `Trendplus2.sln`

### Do

1. Use a clean Windows environment with the documented supported .NET/Visual Studio workloads.
2. Open/restore `Trendplus2.sln` in Visual Studio or use an equivalent documented VS/MSBuild command that actually exercises the JavaScript SDK project system.
3. Verify both `.esproj` projects load without an unavailable-SDK error.
4. Verify the backend solution filter remains the canonical non-IDE Linux path.
5. Run the primary React and POS build commands required by the documented contract where the environment supports them.
6. If Visual Studio requires a workload not currently documented, document it explicitly rather than silently declaring the solution broken.
7. If the mixed solution genuinely fails with the available pin, make the smallest build-system correction and prove both Windows IDE and Linux backend paths.
8. If no Windows/VS-capable environment is available, leave BCI06 WAITING/BLOCKED; do not manufacture proof.

### Tests

- clean Windows/Visual Studio solution load/restore
- both JavaScript SDK wrappers load
- backend `.slnf` restore/build remains green
- primary frontend build remains independently gated
- POS build remains green if it is still a supported project

### Acceptance

- The supported Windows/Visual Studio behavior is observed and documented, not inferred solely from NuGet availability or `dotnet restore`.
- Required Visual Studio workloads are documented.
- Linux backend CI remains isolated from frontend project-system availability.
- If mixed-solution Visual Studio support is not actually supported, the documentation says so explicitly and stops claiming it.

### Notes (current execution)

- Date: 2026-08-11
- Owner switched to `Codex` for the final `BCI05` execution pass on the current `main` worktree
- Local lock: `.ai/task-locks/BCI05-codex.lock.md`
- Immediate plan: re-run targeted sanity checks, then execute the full local backend suite, and only then commit/push for green GitHub Actions evidence

### Notes (current execution result)

- Date: 2026-08-11
- Evidence report: `docs/qa/BACKEND_CI_FULL_SUITE_EVIDENCE_2026-08-11_CODEX_REENTRY.md`
- Local exact BCI05 sequence: restore=success, build=success, test=success
- Local suite totals: 809 total / 809 passed / 0 failed
- Coverage pipeline gap closed locally: `Api.Tests/coverage.runsettings` now emits Cobertura-only output and produced `TestResults/75b4a260-31d7-43b6-b31f-b4a2540166a7/coverage.cobertura.xml`
- Focused corroboration on the same worktree: `AnalyticsActionItemServiceTests` 36/36, `AnalyticsActionsPage.spec.tsx` 14/14, `npm run build` success, `npm run check:analytics-guardrails` success
- GHA proof is still pending commit/push from this worktree
- Next: commit/push current worktree, capture green `analytics-tests` run/job IDs, then close `BCI05` and `BCI01` with remote evidence

### Dependencies

- BCI03 implementation already present
- Windows/Visual Studio-capable verification environment
