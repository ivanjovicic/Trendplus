# Trendplus Backend CI Repair - Evidence Follow-up Addendum

Created: 2026-08-10
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none in this addendum
Owner program: `BCI`
Parent queue: `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`

Purpose: close evidence that the original BCI prompts explicitly required but their completion notes did not fully observe. This addendum does not reopen already-implemented CI/build changes and must not be used to bypass the real backend assertion repairs owned by `RQ89` and `RQ90`.

## Status summary

| Task | Status | Purpose |
|---|---|---|
| BCI05 | WAITING | Re-run the complete backend suite and prove final GitHub Actions restore/build/test/coverage/artifact behavior after RQ89/RQ90 |
| BCI06 | WAITING | Verify the BCI03 mixed-solution/JavaScript SDK model in Windows/Visual Studio or document a proven support boundary |

---

## BCI05 - Close full backend suite and GitHub Actions evidence

Status: WAITING
Ready after: `RQ89` DONE and `RQ90` DONE
Priority: P0
Type: CI/evidence/tests
Feature family: backend-ci-final-evidence
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/BCI05-<agent>.lock.md`
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

### Dependencies

- `RQ89` DONE
- `RQ90` DONE
- no known unassigned BCI04 root-cause family remains

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

### Dependencies

- BCI03 implementation already present
- Windows/Visual Studio-capable verification environment
