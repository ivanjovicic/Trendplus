# InventoryStatus, RQ601 and RQ602 follow-up evidence

- Date: 2026-10-11
- Request: direct user request to inspect current GitHub state, safely repair/verify InventoryStatus imported/access behavior, review RQ601/RQ602 tests and synchronize prompt/evidence.
- Recovery base: `origin/main` and local `HEAD` both `954ce5f4b084846a6cdf1ca274427d1dca49127e` when documentation edits began. While the audit was in progress, two unrelated upstream evidence commits advanced `main`; fresh `git fetch origin` now confirms current `origin/main` and `HEAD` at `45e8ae16eddeba4911bf0c749a035dc465190afb` before this delivery.
- Delivery status: documentation-only synchronization on the existing current-main checkout; no product code change was needed because RQ607 already fixed the mapping.
- Scope: RQ607 InventoryStatus current-snapshot/fallback mapping; RQ601 cost provenance and unresolved source sample; RQ602 historical measurement and prospective pilot gates.
- Production data/import: no production or local business data changed; no Access import, schema migration or business-data write was run.

## GitHub and ownership review

- GitHub latest commit before the final documentation delivery: `45e8ae16eddeba4911bf0c749a035dc465190afb` (`docs(evidence): record blocked prompt review`, 2026-10-11); fresh fetch matched the local branch. The preceding commits were `2a17f219` (roadmap evidence refresh) and `954ce5f4` (RQ612 governance evidence).
- Repository default branch is `main`. GitHub reported no open PRs. Branch searches found no RQ601/RQ602-named delivery branch; local worktree inventory was reviewed before edits.
- Existing local untracked attachment/temp directories were preserved. The P-UI-43 lock covers separate trust-header files. A separate RQ131 lock and dirty queue/roadmap changes exist in the old `queue-next-20261007` worktree; its vendor endpoint/UI scope and edited queue/roadmap sections were checked and do not overlap the RQ601/RQ602 sections changed here. No RQ601/RQ602 lock was present.
- RQ607's original fix is already delivered in `ca5f9ffc72216c3d212a4ee915c5e8ad44e6ced5`; its queue status is DONE. The canonical handler maps `imported` to Access origin (`access`) for snapshots, and the operational fallback applies the same mapping.
- RQ601 remains PARTIAL: provenance distinctions are delivered in code, but the genuine 20 original kalkulacija lines remain an owner gate.
- RQ602 remains PARTIAL: batch-23 dry-run is descriptive; RQ611 future store/day evidence is awaiting migration deployment and cannot certify batch #23 retroactively. RQ612 found zero fully qualified historical shadow-control pairs.

## Tests actually executed

- `dotnet vstest Api.Tests/bin/Release/net8.0/Api.Tests.dll --TestCaseFilter:"FullyQualifiedName~Rq607InventoryStatusPostgresIntegrationTests" --logger:"console;verbosity=normal"` — passed, 1/1. This created an isolated PostgreSQL database and compared InventoryStatus API results with the independent SQL oracle. The test host emitted best-effort performance-log errors for its intentionally absent `PerformanceLogs` table; the assertion and test passed.
- `dotnet vstest Api.Tests/bin/Release/net8.0/Api.Tests.dll --TestCaseFilter:"FullyQualifiedName~PurchaseCostScaleReconciliationContractTests|FullyQualifiedName~MarkdownPilotMeasurementContractTests" --logger:"console;verbosity=normal"` — passed, 2/2. These prove read-only SQL and explicit nullable/unproven-cost contract behavior.
- Combined focused run of RQ607, RQ601, RQ602 and cost-provenance PostgreSQL selections — 6 total, 4 passed, 2 failed, 0 skipped. The RQ601 and RQ602 integration tests failed at their explicit fixture-availability assertions because neither `RQ601_TEST_POSTGRES` nor `RQ602_TEST_POSTGRES` was set and `127.0.0.1:5434` was unreachable. They did not fail query assertions.
- `dotnet test Api.Tests/Api.Tests.csproj ...` returned exit code 0 without running/discovering tests in this shell; it is not counted as validation. Direct `dotnet vstest` was used to obtain actual execution results.
- No broad suite, production API, historical RQ602 SQL execution or import was run in this follow-up.

## Documentation synchronized

- `docs/qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md`: clarifies that the InventoryStatus defect identified in the original historical validation was subsequently fixed by RQ607, without claiming the historical batch population was re-certified.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`: corrects stale RQ602 routing text and records the RQ611/RQ612 readiness outcomes and actual local test constraints.
- `MASTER_ROADMAP.md`: corrects its stale RQ routing table from `RQ611 READY` to the current post-RQ612 `none` state, matching the latest RQ612 zero-READY scan and the roadmap active-section summary.

## Residual items

- Provide the 20 traceable original calculation records to finish RQ601's business-scale verdict.
- Deploy the additive RQ611 schema migration through its owning release path before relying on future store/day coverage; do not backfill or re-import batch #23 to imply historical coverage.
- Run RQ601 and RQ602 PostgreSQL fixture tests in an environment with an explicitly provisioned disposable test database. The tests are intentionally fail-closed when infrastructure is unavailable.
- Obtain owner approval and the named readiness inputs before any prospective RQ602 pilot or business recommendation.
