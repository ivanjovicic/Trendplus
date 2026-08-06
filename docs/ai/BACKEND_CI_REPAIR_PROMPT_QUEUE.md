# Trendplus Backend CI Repair Prompt Queue

Created: 2026-08-05
Repo: `ivanjovicic/Trendplus`
Purpose: restore truthful execution of the backend analytics test suite and separate real test failures from workflow/bootstrap failures.
Current READY prompt: `none`

## Current diagnosis

The bootstrap blocker is fixed. The backend tests now start and fail on real assertions / runtime contract drift.

The first real failures are triaged in `docs/qa/BACKEND_CI_FAILURE_TRIAGE_2026-08-06.md`.

The last three inspected runs of `Analytics Tests & Data Integrity` failed before build and test execution:

| Commit | Workflow run | Result | First failing step |
| --- | ---: | --- | --- |
| `c5e6ce689115e94719c28b0ec6b0c0fd4d9cb08f` | `30928945654` | failed | `Restore dependencies` |
| `2ab36cb376d236cdde98fbb4d31e5316bf9cdd4d` | `30983785992` | failed | `Restore dependencies` |
| `a1b9231a6910ab2209b5e7d79db0f2bd42cf8a04` | `30992652247` | failed | `Restore dependencies` |

The repeated root error is:

```text
Klijent/Klijent.esproj: Could not resolve SDK "Microsoft.VisualStudio.JavaScript.Sdk".
Unable to find package Microsoft.VisualStudio.JavaScript.Sdk with version (= 1.0.3864779).
Nearest version found on nuget.org: 1.0.3982316.
```

Both frontend project wrappers pin the unavailable version:

- `Klijent/Klijent.esproj`
- `Trendplus.POS.Ui/Trendplus.POS.Ui.esproj`

The backend workflow runs:

```text
dotnet restore Trendplus2.sln
dotnet build Trendplus2.sln --no-restore --configuration Release
```

`Trendplus2.sln` contains the backend projects and both `.esproj` projects. Because solution restore evaluates the unavailable JavaScript SDK, it exits with code 1 even though the backend `.csproj` projects restore successfully.

`Api.Tests/Api.Tests.csproj` references `Api/Api.csproj`, and `Api/Api.csproj` transitively references `Application`, `Domain`, `Infrastructure` and `Workers`. Therefore the backend test project is already a valid root for restoring and building the backend dependency graph without evaluating unrelated frontend `.esproj` wrappers.

Three additional observations must not be confused with the root cause:

1. The PostgreSQL service becomes healthy. Its default `pg_isready` probe produces noisy `role "root" does not exist` log entries, but this does not fail the job.
2. `Publish coverage summary` fails only because no tests ran and no Cobertura file exists.
3. `Upload test results and coverage` fails only because no `TestResults` directory exists. These secondary failures obscure the first actionable error.

## Queue ownership and routing

- This queue owns GitHub Actions backend restore/build/test execution and CI reporting behavior.
- Analytics recommendation/formula correctness remains in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md` and its source queues.
- Deployment, authorization and broader release work remains in `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`.
- While the backend workflow is red before tests execute, `BCI01` takes precedence over claims that backend stabilization checks are passing.
- Do not mark any backend test task `DONE` until a GitHub Actions run reaches the test step and records its real result.

## Queue rules

- Follow `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.
- One prompt per commit/session.
- Use exact statuses: `READY`, `WAITING`, `IN_PROGRESS`, `BLOCKED`, `PARTIAL`, `DONE`, `OBSOLETE`.
- Do not mix JavaScript SDK upgrades with the minimal backend-workflow unblock.
- Do not hide a genuine test failure by weakening the test command or excluding tests.
- Do not treat local success as GitHub Actions proof.

---

## BCI01 - Restore and build the backend project graph instead of the mixed solution

Status: PARTIAL
Priority: P0
Type: CI/workflow
Feature family: backend-ci-bootstrap
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/BCI01-codex.lock.md`
Commit suggestion: `fix(ci): restore backend tests without frontend sdk`

### Why

The workflow called `Complete backend analytics suite` is blocked by unrelated `.esproj` SDK resolution. No backend assembly is built and no test is executed, so the red check currently provides no information about backend correctness.

### Evidence already found

- Workflow file: `.github/workflows/analytics-tests.yml`.
- Current restore command: `dotnet restore Api.Tests/Api.Tests.csproj`.
- Current build command: `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release`.
- `Trendplus2.sln` contains `Klijent/Klijent.esproj` and `Trendplus.POS.Ui/Trendplus.POS.Ui.esproj`.
- Both `.esproj` files pin `Microsoft.VisualStudio.JavaScript.Sdk/1.0.3864779`.
- NuGet on the Linux runner cannot resolve that exact package version.
- `Api.Tests/Api.Tests.csproj` references `Api/Api.csproj`.
- `Api/Api.csproj` references all backend projects needed by the tests.
- Runs `30928945654`, `30983785992` and `30992652247` all fail in restore with the same root cause.
- Risk class: confirmed deterministic CI bootstrap failure.

### Fixed contract

- A backend-only workflow must restore, build and test the backend project graph without requiring frontend JavaScript SDK projects.
- The test command and test population must not be reduced.
- PostgreSQL-backed integration tests must still run with `CI=true` and the configured PostgreSQL service.
- Frontend and whole-solution build compatibility remain separate responsibilities.

### Scope only

- `.github/workflows/analytics-tests.yml`
- `docs/ci/ANALYTICS_CI_GATES.md` only if its documented commands become stale
- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`

### Do not touch

- `Klijent/Klijent.esproj`
- `Trendplus.POS.Ui/Trendplus.POS.Ui.esproj`
- `Trendplus2.sln`
- backend production code
- backend test assertions
- frontend workflows
- package versions

### Read first

- `.github/copilot-instructions.md`
- `AGENTS.md`
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `.github/workflows/analytics-tests.yml`
- `Api.Tests/Api.Tests.csproj`
- `Api/Api.csproj`
- `Trendplus2.sln`

### Do

1. Confirm from project references that `Api.Tests/Api.Tests.csproj` transitively covers `Api`, `Application`, `Domain`, `Infrastructure` and `Workers`.
2. Change backend workflow restore to:

   ```text
   dotnet restore Api.Tests/Api.Tests.csproj
   ```

3. Change backend workflow build to:

   ```text
   dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release
   ```

4. Keep the existing full `dotnet test Api.Tests/Api.Tests.csproj` command, coverage collector, PostgreSQL service and `CI=true` behavior.
5. Do not add `--no-dependencies`, test filters, exclusions, `continue-on-error`, or another mechanism that weakens the backend gate.
6. Run the exact restore/build/test commands locally or in an equivalent Linux environment when available.
7. Push the workflow change and inspect the new GitHub Actions run.
8. Mark `DONE` only if the workflow reaches `Run all backend tests with coverage` and that step completes successfully.
9. If restore/build succeeds but real test assertions fail, mark this task `PARTIAL`, record the failing test names/log evidence, and make `BCI04` READY. Do not expand BCI01 into application fixes.

### Test matrix

- backend restore succeeds on Linux without resolving either `.esproj`;
- backend Release build succeeds;
- all `Api.Tests` unit tests execute;
- all CI-enabled PostgreSQL integration tests execute rather than skip silently;
- coverage file is created when tests complete;
- a deliberately failing test would still fail the workflow;
- frontend projects are not restored or built by this backend workflow.

### Checks

- `git diff --check`
- `dotnet restore Api.Tests/Api.Tests.csproj`
- `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage" --settings Api.Tests/coverage.runsettings --results-directory TestResults --logger "trx;LogFileName=analytics-tests.trx"`
- GitHub Actions run evidence for the resulting commit

### Acceptance

- The backend workflow no longer evaluates the unavailable JavaScript SDK during restore/build.
- The workflow reaches the backend test step.
- No tests are filtered out or allowed to fail.
- The final queue note records the exact GitHub Actions run ID, test totals and result.

### Completion note

- Date: 2026-08-06
- Agent: Codex
- Changed files:
  - `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`
- Validation:
  - `dotnet restore Api.Tests/Api.Tests.csproj` - pass
  - `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage" --settings Api.Tests/coverage.runsettings --results-directory TestResults --logger "trx;LogFileName=analytics-tests.trx"` - fail
- Test totals:
  - 781 total
  - 715 passed
  - 66 failed
- Failing evidence:
  - initial local run hit `password authentication failed for user "postgres"` when the host Postgres password did not match the workflow expectation
  - the matching local run reached real test execution and then failed on existing DB/runtime issues, including `relation "PerformanceLogs" does not exist`
  - failing families included `AccessImportAdminAuthorizationTests`, `AccessImportForeignKeyGuardTests`, `AnalyticsActionsEndpointsTests`, `AnalyticsActionsCriticalWorkflowTests`, `AccessImportJobQueueTests`, `DataQualityPostgresIntegrationTests`, `InventoryListEndpointIntegrationTests`, `SupplierDecisionHubContractTests`, and `WorkerRuntimePolicyServiceTests`
- GitHub Actions evidence:
  - not inspected in this pass; no run ID recorded yet
- Checks:
  - `git diff --check` - pass
- Risk:
  - BCI01 bootstrap is unblocked locally, but the backend test suite still depends on a clean relational test DB and a matching GitHub Actions run is still needed before this can be promoted to `DONE`.
- Next:
  - `BCI02` WAITING
  - `dotnet restore Api.Tests/Api.Tests.csproj` - pass
  - `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage" --settings Api.Tests/coverage.runsettings --results-directory TestResults --logger "trx;LogFileName=analytics-tests.trx"` - fail: 741 passed / 40 failed
- First failing tests:
  - `Api.Tests.DataQualityPostgresIntegrationTests.IssuesHandler_PaginatesAndUsesStableRevenueOrdering`
  - `Api.Tests.AnalyticsActionsCriticalWorkflowTests.Upsert_SameOpenSourceTupleIsIdempotent`
  - `Api.Tests.InventoryListEndpointIntegrationTests.InventoryList_ClampsInvalidPagingArguments`
  - `Api.Tests.AccessImportAdminAuthorizationTests.DeleteBatch_RejectsRequestWithoutAdminKey`
  - `Trendplus2.Tests.AnalyticsActionItemServiceTests.UpsertAsync_PersistsLedgerCreationSnapshotMetadata`
- Risk:
  - Workflow bootstrap is fixed, but the backend suite now reaches real assertion failures and needs root-cause triage in BCI04 instead of more bootstrap changes.
- Next:
  - `BCI04` READY

---

## BCI02 - Stop coverage and artifact steps from creating cascading fake root failures

Status: WAITING
Ready after: `BCI01` reaches the test step, regardless of whether real tests pass
Priority: P1
Type: CI/observability
Feature family: backend-ci-diagnostics
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/BCI02-<agent>.lock.md`
Commit suggestion: `fix(ci): preserve backend failure root cause`

### Why

When restore or build fails, the workflow currently adds extra failures for missing coverage and missing artifacts. GitHub then reports several annotations even though only one primary fault occurred. This makes triage slower and can mislead agents into treating absent artifacts as independent defects.

### Evidence already found

- `Publish coverage summary` uses `if: always()` and calls `sys.exit(1)` when no Cobertura report exists.
- `Upload test results and coverage` uses `if: always()` plus `if-no-files-found: error`.
- In all three inspected failed runs, build and tests were skipped, so neither coverage nor `TestResults` could exist.
- PostgreSQL health logs contain repeated `FATAL: role "root" does not exist` because the health command omits the configured user/database, although the container ultimately becomes healthy.
- Risk class: confirmed diagnostic cascade and log noise; not the primary restore failure.

### Fixed contract

- The earliest real restore/build/test failure must remain the clear root cause.
- Missing coverage must fail the job only when the test step actually ran successfully but the collector unexpectedly produced no report.
- Test artifacts should upload whenever they exist, including failed-test TRX/coverage output.
- Absence of artifacts after an upstream bootstrap failure must not create another red root cause.
- PostgreSQL health checks should use the configured CI user/database.

### Scope only

- `.github/workflows/analytics-tests.yml`
- optional small CI documentation update
- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`

### Do not touch

- test source code
- coverage thresholds or exclusions
- PostgreSQL application connection strings
- frontend workflows
- JavaScript SDK pins

### Read first

- BCI01 final notes and workflow run
- `.github/workflows/analytics-tests.yml`
- GitHub logs for runs `30928945654`, `30983785992`, `30992652247`

### Do

1. Give restore, build and test steps stable step IDs.
2. Preserve test execution result and allow post-test reporting to run without replacing it as the root cause.
3. Make coverage summary behavior conditional:
   - if tests completed and coverage exists, publish the summary;
   - if tests completed successfully but coverage is missing, fail with one explicit collector error;
   - if restore/build/test never ran or failed before producing coverage, append an explanatory summary but do not add a second independent failure.
4. Upload `TestResults` when files exist, including after a failing test run.
5. When no result files exist because an upstream step failed, skip or warn instead of using `if-no-files-found: error`.
6. Change the PostgreSQL health command to explicitly probe the configured user and database, for example:

   ```text
   pg_isready -U postgres -d trendplus_test
   ```

7. Verify that a real failing test still makes the workflow red and preserves its TRX/log artifact.
8. Keep coverage collection mandatory for successful test runs.

### Test matrix

- restore failure -> one primary restore failure, no artifact/coverage cascade;
- build failure -> one primary build failure;
- test assertion failure -> workflow red, TRX uploaded, coverage uploaded if produced;
- successful tests + missing coverage -> explicit coverage collector failure;
- successful tests + coverage -> summary and artifact succeed;
- PostgreSQL health probe has no `role "root" does not exist` noise;
- artifact condition does not silently discard files from failed tests.

### Checks

- `git diff --check`
- workflow YAML validation
- one controlled successful workflow run
- one controlled failing-test verification where practical, reverted before final commit

### Acceptance

- GitHub annotations identify the first actionable failure rather than three secondary symptoms.
- Failed tests still block the workflow.
- Successful runs still require and publish coverage.
- Existing test evidence is uploaded whenever available.

---

## BCI03 - Repair or explicitly isolate unavailable JavaScript SDK pins from whole-solution builds

Status: WAITING
Ready after: `BCI01` is `DONE` or `PARTIAL` with backend restore/build unblocked
Priority: P1
Type: build-system audit/fix
Feature family: mixed-solution-cross-platform-build
Parallel-safe: yes, if BCI02 is not editing the same workflow/solution files
Owner: unassigned
Local lock: `.ai/task-locks/BCI03-<agent>.lock.md`
Commit suggestion: `fix(build): restore javascript sdk compatibility`

### Why

Scoping backend CI to the backend test project is the correct immediate fix, but the two `.esproj` files still pin a package version that the current Linux NuGet resolver cannot obtain. Whole-solution restore remains unreliable and developer/other CI behavior is not explicitly documented.

### Evidence already found

- `Klijent/Klijent.esproj` pins `Microsoft.VisualStudio.JavaScript.Sdk/1.0.3864779`.
- `Trendplus.POS.Ui/Trendplus.POS.Ui.esproj` pins the same version.
- Linux restore reports that exact version unavailable and names `1.0.3982316` as the nearest available version.
- `Trendplus2.sln` includes both `.esproj` projects and marks them for Debug/Release build/deploy.
- React production build is already handled independently through npm/Vercel; `Api/Api.csproj` has its React auto-build target disabled.
- Risk class: confirmed whole-solution portability problem; correct long-term contract requires verification rather than a blind version bump.

### Contract decision required

Choose and prove one supported model:

1. **Supported mixed solution:** pin an available, compatible JavaScript SDK version and prove restore/build on supported Windows and Linux environments; or
2. **Explicit backend solution/filter:** keep IDE frontend wrappers but provide a canonical backend-only `.slnf`/solution for non-Visual-Studio restore/build, with docs and CI commands using it; or
3. **Remove obsolete wrapper(s):** only if source usage proves the project is no longer part of the supported development model.

Do not select a model solely because NuGet reports a nearest version.

### Scope only

- `Klijent/Klijent.esproj`
- `Trendplus.POS.Ui/Trendplus.POS.Ui.esproj`
- `Trendplus2.sln`
- optional new backend `.slnf` or solution file
- relevant build documentation
- optional dedicated cross-platform build workflow
- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`

### Do not touch

- application business logic
- frontend source code
- npm dependency upgrades
- backend test assertions
- BCI01 backend workflow scope unless a documented canonical solution/filter replaces the project-root commands with equivalent coverage

### Read first

- `.github/copilot-instructions.md`
- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- both `.esproj` files
- `Trendplus2.sln`
- `Api/Api.csproj`
- frontend deployment/build docs

### Do

1. Determine whether each `.esproj` is actively required by current Visual Studio workflows and deployment paths.
2. Check available `Microsoft.VisualStudio.JavaScript.Sdk` package versions from an authoritative package source and inspect compatibility/release notes for the candidate version.
3. Test the smallest candidate change in a clean environment; do not trust an existing local SDK cache.
4. Prove the selected model with explicit commands and supported platforms.
5. Ensure frontend npm/Vite builds remain their own quality gate and are not accidentally skipped by claiming whole-solution success.
6. Document the canonical commands for:
   - backend restore/build/test;
   - primary React frontend install/typecheck/test/build;
   - POS frontend install/build if supported;
   - optional whole-solution/IDE restore.
7. Add a regression check that catches an unavailable JavaScript SDK pin before it blocks unrelated CI again.
8. If Windows/Visual Studio compatibility cannot be verified, finish `PARTIAL` and document the missing proof rather than guessing.

### Test matrix

- clean Linux restore/build under the selected model;
- clean Windows/Visual Studio restore when mixed solution support is claimed;
- primary React npm build remains green;
- POS UI build remains green if retained as supported;
- no implicit dependency on a globally cached SDK;
- unavailable SDK pin produces a focused build-system failure, not a backend-test failure.

### Checks

- `git diff --check`
- clean restore commands for selected model
- relevant npm build commands
- GitHub Actions or equivalent clean-run evidence

### Acceptance

- Whole-solution or documented backend-solution behavior is intentional and reproducible.
- No supported build path pins an unavailable SDK version.
- Backend CI remains independent from unrelated frontend wrapper resolution.

---

## BCI04 - Triage the first real backend test failures after bootstrap is fixed

Status: DONE
Ready after: `BCI01` produces a run where restore and build succeed but one or more tests fail
Priority: P0
Type: test triage/docs/prompts; runtime fix only in later root-cause tasks
Feature family: backend-test-failure-triage
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/BCI04-codex.lock.md`
Commit suggestion: `docs(ci): triage real backend test failures`

### Why

Current runs provide no evidence about assertion-level backend failures because the suite never starts. Once BCI01 unblocks execution, actual failures may appear and must be grouped by root cause rather than patched randomly or hidden with test filters.

### Entry condition

Do not start this task unless a GitHub Actions run shows:

- restore: success;
- build: success;
- test step: failure;
- concrete failing test names or test-host crash evidence.

If the first real run passes all tests, mark BCI04 `OBSOLETE` with the run ID and test totals.

### Contract

- Failing tests are evidence, not obstacles to remove.
- Do not delete, skip, weaken, loosen or globally filter a test merely to make CI green.
- Group failures by common root cause and create one small implementation prompt per root-cause family.
- Distinguish deterministic product regressions, environment/schema bootstrap defects, shared-test isolation problems and flaky timing/network behavior.

### Scope only

- workflow logs, TRX and coverage artifacts
- relevant failing test files and implementation files for inspection
- one dated triage document under `docs/qa/`
- this queue plus new focused prompts/addenda

### Do not touch

- runtime code in the triage commit
- test expectations without source-of-truth proof
- production DB/data
- unrelated analytics feature families

### Read first

- BCI01 final notes
- BCI02 final notes if completed
- failing GitHub Actions job logs
- uploaded TRX/coverage artifacts
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`

### Do

1. Record commit SHA, workflow/run/job IDs, .NET SDK, PostgreSQL version, test totals and exact failing test names.
2. Extract the first failure stack trace for each distinct root-cause signature.
3. Reproduce each group using the narrowest exact `dotnet test --filter` command, then verify whether the group also fails inside the full suite.
4. Classify every failure:
   - compile/API contract drift;
   - deterministic assertion/business regression;
   - migration/schema/bootstrap;
   - shared database/test isolation;
   - order dependence;
   - timeout/resource pressure;
   - flaky external dependency;
   - test bug proven by canonical contract.
5. Search current queues before creating a new task. Link or reactivate an existing task if it owns the same root cause.
6. For each new root cause, write a full prompt with evidence, exact files, fixed contract, test matrix, checks and acceptance criteria.
7. Set only the highest-priority non-overlapping repair prompt to `READY`.
8. Keep BCI04 `PARTIAL` until all failures are assigned to a prompt or proven transient with repeated clean evidence.

### Test matrix

- exact failing test alone;
- failing class/namespace group;
- full test project;
- repeated execution for suspected flakes;
- PostgreSQL clean database versus reused context when relevant;
- local versus GitHub Actions environment;
- true product regression versus stale test expectation.

### Checks

- `git diff --check`
- exact reproduction commands recorded with results
- no test skips/filters added to production workflow
- queue duplication check

### Acceptance

- Every real failing test belongs to one evidenced root-cause group.
- The next repair prompt is precise and minimal.
- No failure is hidden or mislabeled as fixed.

### Notes

- 2026-08-06: DONE. Backend bootstrap is fixed and the suite reaches real failures. Triage evidence is recorded in `docs/qa/BACKEND_CI_FAILURE_TRIAGE_2026-08-06.md`.
- Commit / run evidence:
  - commit `568f03c65891e96bf2c0f27592aeea96c2e58361`
  - workflow run `31080378321`
  - workflow job `92547604945`
  - full CI totals: `741 passed`, `40 failed`
- Root-cause groups:
  - Access import test-host route registration and auth-first timeout contract drift.
  - Analytics actions list filter/search/paging regression.
  - Inventory list cached route / count regression.
  - Data quality top-offender SQL scope drift and stale count/order expectations (already assigned to `RQ78` and `RQ77`).
- Focused repros:
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AccessImportAdminAuthorizationTests.DeleteBatch_RejectsRequestWithoutAdminKey|FullyQualifiedName~AccessImportRunEndpointTests.PostRun_WhenStoragePreparationTimesOut_ReturnsGatewayTimeout"` - fail: route mapping body inference on `IBatchLogService`, plus `401 Unauthorized` vs `504 GatewayTimeout`
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AnalyticsActionsCriticalWorkflowTests.List_AppliesCanonicalFiltersSearchPagingAndPriorityOrdering"` - fail: expected `totalCount=2`, actual `0`
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~InventoryListEndpointIntegrationTests.InventoryList_ClampsInvalidPagingArguments"` - fail: expected `totalCount=4`, actual `0`
- Prompt mapping:
  - `STAB09` READY - access import test-host route registration and auth-gated timeout repro
  - `RQ89` WAITING - inventory list cached route regression
  - `RQ90` WAITING - analytics actions list filter/order regression
- Checks:
  - `dotnet restore Api.Tests/Api.Tests.csproj` - pass
  - `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage" --settings Api.Tests/coverage.runsettings --results-directory TestResults --logger "trx;LogFileName=analytics-tests.trx"` - fail
  - `git diff --check` - pass
- Risk:
  - The triage is complete, but the three follow-up prompt families still need separate implementation runs.
- Next:
  - `STAB09` READY

---

## Expected transition

1. Run `BCI01` now.
2. If real tests fail, make `BCI04` READY before assuming application stability.
3. After the workflow reaches tests, run `BCI02` to improve diagnostics without weakening the gate.
4. Run `BCI03` separately to repair/document whole-solution JavaScript SDK compatibility.
5. Return to `STAB01` and the analytics reliability queue only with truthful backend CI evidence.
