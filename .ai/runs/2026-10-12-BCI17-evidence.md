# BCI17 - backend skip classification and critical CI proof

Task ID: BCI17
Queue: `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`
Date: 2026-10-12
Agent/tool: Codex, isolated worktree
Delivery target: main
Working branch / PR: `codex/bci17-42-skip-gate`; implementation delivered by parent integration
Main commit SHA: `8ea2ba6acb03d59f09136d197056f8d5496d60e5`
Main verification: passed - refreshed `origin/main` contains the implementation; exact-SHA backend, quality-gate and planning workflows passed
Evidence state: synchronized
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

- `git fetch origin main` -> pass; exact implementation main `8ea2ba6acb03d59f09136d197056f8d5496d60e5` contains the implementation.
- `dotnet restore Api.Tests/Api.Tests.csproj` -> pass.
- `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release` -> pass, 0 errors (88 existing warnings).
- Workflow-equivalent RQ453 commands ran sequentially against local disposable PostgreSQL with `TRENDPLUS_RUN_INTEGRATION_TESTS=true`, each with a separate TRX and exact expected count check -> pass: oracle `5/5`, parity `2/2`, inventory `1/1`, raw-fact `1/1`, six-screen routes `1/1`, OP2 `4/4`; zero skips across all six suites.
- `py scripts/ci/validate_backend_skip_inventory.py --trx TestResults/bci17-full-fresh/analytics-tests.trx --inventory scripts/ci/backend-skip-inventory.json` -> pass: 42 known skips, 13 runnable, 14 superseded, 15 uncovered, 0 external/provider, 0 drift.
- Same validator against the source run TRX -> pass, all 42 source skips accounted for.
- Negative validator check with one source skip reason mutated -> expected failure (exit 1, reason drift detected).
- Full suite: `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --verbosity quiet --collect:'XPlat Code Coverage' --settings Api.Tests/coverage.runsettings --results-directory TestResults/bci17-full-fresh --logger 'trx;LogFileName=analytics-tests.trx'` -> fail as detailed above; report at `TestResults/bci17-full-fresh/analytics-tests.trx`.
- Exact-main Analytics Tests & Data Integrity run `38183667143` on SHA `8ea2ba6acb03d59f09136d197056f8d5496d60e5` -> pass; full backend suite reports 1,963 passed and 42 skipped, followed by a successful inventory classification step.
- Exact-main RQ453 certification in run `38183667143` -> pass; 5/5 supplier/shoe oracle, 2/2 cross-screen parity, inventory 1/1, raw-fact 1/1, six-screen route 1/1, OP2 4/4, and integrity/Nivelacija 49/49; all zero skipped.
- Exact-main Analytics Quality Gates `38183667166` -> pass. Planning Governance `38183667171` -> pass.
- `git diff --check` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass (749 tasks).
- `node scripts/check-agent-instructions.mjs` -> pass (18 canonical files).
- JSON parse, Python bytecode compile and workflow YAML parse -> pass.
- `git merge-base --is-ancestor origin/main HEAD` -> pass on the integrated BCI17 branch.

## Validation not run

- None after exact-main workflows. Full exact-main backend suite passed. The earlier local disposable-PG suite had 14 classified failures (detailed above); they were not suppressed or altered and did not reproduce in the exact-main workflow.

## Documentation impact

- Updated the owning BCI queue with the terminal status and completion evidence; implementation reused existing RQ453 gates and added only missing deterministic proofs.
- Updated `docs/ci/ANALYTICS_CI_GATES.md` with the skip inventory and critical certification contracts.
- Updated `MASTER_ROADMAP.md` to close BCI17 and record current RQ routing after RQ623 completion.

## What was missed

- 15 historically skipped tests remain uncovered; their inventory rows specify the needed fixture/source evidence and unblock event.

## Risks

- The local full backend suite showed the 14 classified failures above. Exact-main CI then passed the full backend suite, while preserving all 42 historical skips as classified inventory entries.

## Post-close routing recovery

- Recovery base: `origin/main` `8ea2ba6acb03d59f09136d197056f8d5496d60e5` contains BCI17. BCI owner queue checked for newly satisfied dependencies; no dependent BCI successor was registered by this change. RQ620 promotion is documented separately in the RQ623 post-close routing run log.

## Next

- No BCI17 follow-up. Keep the 15 uncovered classifications intact until their documented prerequisites arrive.
