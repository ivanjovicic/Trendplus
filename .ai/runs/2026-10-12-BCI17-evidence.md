# BCI17 - backend skip classification and critical CI proof

Task ID: BCI17
Queue: `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`
Date: 2026-10-12
Agent/tool: Codex, isolated worktree
Delivery target: main
Working branch / PR: `codex/bci17-42-skip-gate`; implementation delivered by parent integration
Main commit SHA: `8ea2ba6acb03d59f09136d197056f8d5496d60e5`
Main verification: passed - fresh `origin/main` is `8ea2ba6acb03d59f09136d197056f8d5496d60e5` and contains the implementation
Evidence state: pending - exact-SHA CI and final queue close-out remain with parent coordinator
Ownership transfer: Codex (BCI17 worktree) -> Codex (parent coordinator); parent instruction in this task on 2026-10-12; implementation commit `d652f41f015d8ae89366dc063a31b0b98506ddae` was integrated at main SHA `8ea2ba6acb03d59f09136d197056f8d5496d60e5`; branch/worktree retained untouched

## What was done

- Retrieved the exact TRX artifact for backend run `38178775379`, job `114586754440`, source SHA `4c6038d5aaadd2118489a96d84ff3f2352f7b8b7`; inventoried all 42 skipped tests by FQN, display name, exact skip reason, source run/job and individual prerequisite/proof.
- Classification: 13 isolated-PostgreSQL runnable, 14 legacy tests superseded by named deterministic proof, 15 genuinely uncovered, 0 opt-in external/provider. The uncovered entries name the missing fixture/source prerequisite and the event needed to unblock them.
- Added a fail-closed TRX inventory validator that rejects new, missing, duplicate-in-inventory, unclassified or reason-changed skip entries and requires provenance plus proof/unblock metadata.
- Strengthened the existing disposable-PostgreSQL RQ453 job with strict count gates for 5 Supplier/Shoe oracle tests, 2 cross-screen parity tests, 1 inventory integrity probe, and 1 runtime raw-fact oracle test. Existing six-screen route (1) and OP2 seam (4) gates remain strict. No suite filter or skip policy was added to the full backend test job.
- Corrected the OP2-14 assertion to compare UTC instants explicitly; this preserves the date contract across local time zones.
- Full Release suite on a fresh isolated PostgreSQL database ended `1948 passed / 14 failed / 42 skipped` (2,004 total). Failures were not hidden or altered: 2 cached analytics assertions returned 1,500 instead of 1,100 under full-suite order; 1 PreNivelacija expected-date mismatch in Belgrade UTC+02; 2 source-contract tests used LF literals against CRLF source; 5 SQL Server Testcontainers cases timed out on Docker named-pipe startup; 2 RQ610 tests correctly failed closed because `RQ610_LEDGER_TEST_POSTGRES` was not supplied; and 2 pgvector fixture startup/cleanup results timed out on Docker named-pipe access. These failures are unrelated to BCI17's CI/skip inventory scope and remain unresolved.

## Files changed

- `.github/workflows/analytics-tests.yml`
- `Api.Tests/SupplierShoeTypeRq447SeamIntegrationTests.cs`
- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`
- `docs/ci/ANALYTICS_CI_GATES.md`
- `scripts/ci/backend-skip-inventory.json`
- `scripts/ci/validate_backend_skip_inventory.py`
- `.ai/runs/2026-10-12-BCI17-evidence.md`

## Validation run

- `git fetch origin main` -> pass; latest observed base `5666c79ed4550d4d7a5709fd23bca4373e70cd91`.
- `dotnet restore Api.Tests/Api.Tests.csproj` -> pass.
- `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release` -> pass, 0 errors (88 existing warnings).
- Workflow-equivalent RQ453 commands ran sequentially against local disposable PostgreSQL with `TRENDPLUS_RUN_INTEGRATION_TESTS=true`, each with a separate TRX and exact expected count check -> pass: oracle `5/5`, parity `2/2`, inventory `1/1`, raw-fact `1/1`, six-screen routes `1/1`, OP2 `4/4`; zero skips across all six suites.
- `py scripts/ci/validate_backend_skip_inventory.py --trx TestResults/bci17-full-fresh/analytics-tests.trx --inventory scripts/ci/backend-skip-inventory.json` -> pass: 42 known skips, 13 runnable, 14 superseded, 15 uncovered, 0 external/provider, 0 drift.
- Same validator against the source run TRX -> pass, all 42 source skips accounted for.
- Negative validator check with one source skip reason mutated -> expected failure (exit 1, reason drift detected).
- Full suite: `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --verbosity quiet --collect:'XPlat Code Coverage' --settings Api.Tests/coverage.runsettings --results-directory TestResults/bci17-full-fresh --logger 'trx;LogFileName=analytics-tests.trx'` -> fail as detailed above; report at `TestResults/bci17-full-fresh/analytics-tests.trx`.
- `git diff --check` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass (749 tasks).
- `node scripts/check-agent-instructions.mjs` -> pass (18 canonical files).
- JSON parse, Python bytecode compile and workflow YAML parse -> pass.
- `git merge-base --is-ancestor origin/main HEAD` will be recorded after the final branch commit/rebase; current implementation is based on the freshly fetched main listed above.

## Validation not run

- Exact-main GitHub Actions result for the BCI17 implementation -> not run; implementation is awaiting sequential integration by the parent coordinator.
- Full Linux/CI reproduction of the 14 local full-suite failures -> not run; local classification evidence is recorded, and the required RQ453 certification suites all passed locally.

## Documentation impact

- Updated the owning BCI queue with the active claim and corrected the BCI17 step to reuse existing RQ453 gates while adding only missing deterministic proofs.
- Updated `docs/ci/ANALYTICS_CI_GATES.md` with the skip inventory and critical certification contracts.
- `MASTER_ROADMAP.md` was intentionally not edited because the parent coordinator owns sequential master routing updates during RQ619/RQ623 work.

## What was missed

- 15 historically skipped tests remain uncovered; their inventory rows identify the needed isolated fixture or source-data prerequisite and the event required to close that gap.
- Main delivery, exact-main Actions evidence and formal queue closeout remain pending.

## Risks

- The full backend suite is locally red with the 14 classified failures above. This work does not claim the backend suite is green.
- The new CI assertions have passed locally with exact counts, but the remote required job must execute on the delivered main SHA before BCI17 can be marked DONE.

## Post-close routing recovery

- Pending main delivery. After integration, refresh `origin/main`, scan the full active queue/addendum set for dependencies changed by BCI17, record any newly READY task, then synchronize terminal queue/evidence state.

## Next

- Parent coordinator: integrate this isolated branch sequentially on `main`, run/inspect the exact-SHA required workflow, then complete BCI17 closeout and post-close routing recovery.
