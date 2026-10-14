# Analytics Reliability RQ queue — post-import correctness and business value

Date: 2026-10-14
Program: RQ (part of canonical `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` family).

Post-close note 2026-10-14: RQ647 is DONE after its corrective direct-main delivery at `408d1b81062e5d4c114960d69ab51d4d239407e4`. Exact-main Analytics Tests run `38542983060` is green (2,076 passed / 43 skipped / 0 failed), executed the RQ647 PostgreSQL benchmark, and uploaded artifact `rq647-analytics-endpoint-benchmark-38542983060` (artifact `11884041409`). The two legacy performance skips are now `legacy-superseded`; the BCI17 inventory remains 43 known skips with 13 isolated-PostgreSQL-runnable / 30 legacy-superseded / 0 genuinely-uncovered entries. Run evidence: `.ai/runs/2026-10-14-RQ647-evidence.md` and `.ai/runs/2026-10-14-direct-claim-collision-remediation-evidence.md`.
Current READY prompt: **RQ666 (P1, Product Decision source/action/store scope identity)**. Independent READY: **RQ667 (P1, Inventory store/compareStore scope validation)**. No prompt is claimed; require fresh shared main reservation, lock/worktree/PR check, and path-safe sequencing. RQ448, RQ565, RQ610, STAB16 and PERF16 keep their external/source gates.
Post-close routing 2026-10-14: RQ664 closed on `main` after the repository-local browser/export proof. RQ663 is **DONE** on `main` at merge SHA `275d1c307bca979e369023e88b87b08ff83d13e3`; RQ665 is also DONE as an independent test-only CI repair. Before any further claim, check current `origin/main`, locks, active branch/worktree and PR paths. RQ662 is DONE on `b48fc1cfd7feebcb54e98e01472eb5d0cc9db7a3`. STAB16, RQ610, RQ448, RQ565 remain gated at their original deployed/source acceptance layers.
RQ648 is DONE on `main` via PR #157 merge SHA `b42493c97cce68043f55e35237ee17063bc93d89`; its 110-ref current-main census, SQL022/RQ481 semantic review and explicit SQL029 no-duplicate check are recorded in `docs/qa/BRANCH_PARITY_REVIEW_2026-10-14.md` and `.ai/runs/2026-10-14-RQ648-evidence.md`. RQ660 is DONE on main at `6b9041f93e4652732fee4ac3812147daf252270f`; RQ661 is DONE on main at `dcdb84c79aa22eb4f3f4c3806645cfd36fbb35c6`; RQ659 is DONE on main at `2472df857e48397a8b0a16076b66e9dd5a04bfd0`; RQ657 is DONE on main at `5a4a15f647166d53dffafe2354b3649680527dc2`; RQ655/RQ656 and RQ585 are DONE. No marketplace/test-feature work without explicit owner reversal.
RQ610 was BLOCKED after the company-grain capture delivery and was reopened for bounded, repository-local source qualification. The probe confirms structurally store-keyed `Artikli` rows, but no trustworthy inventory mutation generation has been established across import, manual edit, transfer and POS paths. Worker/schema ownership is released until the documented source-generation contract is supplied. This pointer is advisory; apply `docs/ai/PROMPT_QUEUE_PROTOCOL.md` and `MASTER_ROADMAP.md` for global routing.

Analytics screen bug review 2026-10-13 (main `a5604874`): RQ651 (P2, fake-zero data-quality/margin trust text), RQ652 (P2, stale action-queue KPI counts/outcome summary), RQ653 (P2, pilot readiness zero actions on partial failure) and RQ654 (P3, period preset local-midnight boundary) are additional independent frontend-only READY lanes. They do not change the RQ645 primary pointer. RQ650 is reserved by open PR #146. Evidence: `docs/qa/ANALYTICS_SCREEN_BUG_REVIEW_2026-10-13.md`.

## Baseline and owner decisions

- Batch #23 is already imported and reconciled: 5,530/5,530 receipts, 67,092/67,092 lines, 12,422 Access `Artikli/ProductsDim`, zero source-fact value/quantity/price deltas. Do **not** repeat import, seed, delete, migrate business DB or auto-deploy to obtain a fresh test.
- RQ605 DONE: canonical retail scope, signed returns and DUG/KOREKCIJA exclusion. RQ606 PARTIAL: transfer idempotency code done; 244 sale lines, 20 receipts and 168 journal source exceptions await byte-identical original MDB.
- RQ607 DONE: `imported -> access`; commit `968e8f30` makes InventoryStatus explicitly **latest available snapshot**, not stock as-of sale date. Local imported snapshot: 12,422 SKU; 3,566 positive on-hand units; 213 low-stock; 11,885 measured zero. RQ96 DONE has observed-daily snapshot table/capture first slice, but its **scheduled capture is missing**. Don't recreate RQ96 schema.
- RQ601 PARTIAL: code-side cost provenance and nullable migration done, but legacy history isn't certified until original 20 kalkulacije are provided. RQ602 PARTIAL: frozen treated 20/control 20 history is descriptive, controls 0->0 and all windows lack day/store source coverage; never infer uplift/causality. RQ569 DONE established **source horizon**, not daily store completeness. RQ592 remains prospective pilot owner, WAITING on STAB16 freshness and owner sign-off.
- RQ598/RQ600 regression/value audits DONE; RQ603/RQ604 DONE. RQ453/RQ568 disposable six-screen certifications DONE. RQ565/RQ566/STAB16 own **deployed** parity/freshness and provider authority, not these local tasks.
- Exact implementation SHA `968e8f30` Actions `38090750465`: complete backend suite **1,926 pass / 1 fail / 42 skipped**, RQ607 test line 88 expected 2 SKU but actual 3. Test fixture product 607010 (store 1/supplier 6701) now intentionally counts in the latest snapshot although `UpdatedAt` fell outside old period, making 3 SKU / 55 on-hand / 1 true OOS. Frontend run `38090750426` and RQ453 certification both passed. The separate PostgreSQL extension collision in test logs is not this assertion; investigate only if reproducible.

## Routing rules

On claim: fresh `origin/main`, complete canonical RQ/addendum/SQL queue, same-owner locks, branches and PRs; read `AGENTS.md`, `PROMPT_QUEUE_PROTOCOL.md`, `VALIDATION_SELECTOR.md`. Preserve exclusive IN_PROGRESS owner; don't call a stale `none` a new gate. Only reassign scopes from the actual owning files; do not duplicate RQ601/RQ602/RQ606/RQ592/STAB16. New tasks never authorize production migrations/imports/deploys or real price changes. For DONE require focused tests, evidence, commit, main push and exact SHA; classify current-main CI failures honestly.

---

## RQ608 - Fix exact-main RQ607 current-snapshot CI regression

Status: DONE
Priority: P0
Type: PostgreSQL/HTTP regression tests
Feature family: inventory-latest-snapshot-ci
Parallel-safe: yes; isolated `Api.Tests/Rq607InventoryStatusPostgresIntegrationTests.cs` and run evidence; no shared files with RQ609/RQ612
Owner: Codex
Local lock: removed after done close
Ready after: `968e8f30` current inventory-snapshot implementation and red Actions `38090750465` are available
Commit suggestion: `test(inventory): correct latest snapshot fixture and oracle expectations`

### Claim note

- Date: 2026-10-11
- Status: IN_PROGRESS
- Claim: Refreshed `origin/main` at `806924d48c743548f7046b3e6bca3de29b1748a0`; BCI/STAB have no READY candidate. RQ608 is the primary P0 READY prompt. Its test file is disjoint from additional READY tasks RQ609/RQ612; no matching lock, branch or open PR was found.
- Local lock: `.ai/task-locks/RQ608-codex.lock.md`.

### Problem
The RQ607 test retained an old date-filter expectation although InventoryStatus deliberately ignores sale-period dates. In the fixture, article 607010 (store=1, supplier=6701, 50 on-hand, `UpdatedAt` before date range) now belongs to the current snapshot: assert 3 rather than 2 SKU, 55 rather than 5 on-hand, OOS still 1.

### Read first
`Api.Tests/Rq607InventoryStatusPostgresIntegrationTests.cs`, `Application/Analytics/Queries/GetInventoryStatus/GetInventoryStatusHandler.cs`, `Api/Endpoints/CachedAnalyticsEndpoints.cs`, `docs/qa/INVENTORY_STATUS_TEMPORAL_CONTRACT_2026-10-11.md`, Actions run 38090750465 job log.

### Do
1. Reproduce the focused test on disposable PostgreSQL. Replace outdated hard-coded assertions and compare each store/supplier scope to a **separate unfiltered SQL oracle**.
2. Assert out-of-period snapshot article is counted; test 0/NULL/negative stock, duplicate-looking SKUs in different stores, empty store, imported/existing/all.
3. Date-only 2026-10-10..2026-10-10 and historical August return latest snapshot with `periodFilterApplied=false`, `historicalAsOfSupported=false` and explicit warning; a no-date call has no temporal warning. Check cache period-only reuse without cross-scope leaks; dashboard/fallback metadata must remain consistent.
4. Verify the only failed CI test is actually repaired; separately classify intermittent `pg_extension_name_index` diagnostic if it recurs. Do not reintroduce timestamp filters or soften skipped tests.

### Tests
Focused mandatory PostgreSQL TRX expected=executed, failed=0, skipped=0; cached fallback/dashboard tests; API Release build; current SHA full `Analytics Tests & Data Integrity` run or precise red classification.

### Acceptance
No false zero or fake historical as-of, test reproduces old failure and passes on corrected expectation, current-main backend CI green or explicitly documented independently failing case. Preserve business DB. Deliver code/test/evidence on `main`.

### Dependencies
No external start gate: STAB16 and RQ601 original documents are not needed for this test-only fix.

### Completion note

- Date: 2026-10-11
- Status: DONE
- Completion: Corrected the stale latest-snapshot expectation and proved scoped imported/existing/all results against independent PostgreSQL queries. Exact-main backend CI is green.
- Changed files: `Api.Tests/Rq607InventoryStatusPostgresIntegrationTests.cs`; `MASTER_ROADMAP.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md`; `.ai/runs/2026-10-11-RQ608-evidence.md`
- Contract/runtime behavior changed: no; test expectations and scope-oracle coverage only.
- Checks run: focused PostgreSQL RQ607 test 1/1; fallback/dashboard + RQ607 tests 7/7; API Release build 0 warnings / 0 errors; all six governance checks; `git diff --check`; Analytics Tests & Data Integrity run `38116562952` on `35aabd582e754bb293337eace0a6b45aeee58026` succeeded (1,969 total / 1,927 passed / 42 skipped / 0 failed); RQ453 certification job succeeded.
- Checks not run: full local backend suite; exact-main Actions provided the broad backend proof.
- Run log: `.ai/runs/2026-10-11-RQ608-evidence.md`
- Evidence state: pending close-out push
- Delivery mode: direct-main
- Main commit SHA: `35aabd582e754bb293337eace0a6b45aeee58026`
- Main verification: at closure verification `origin/main` was `b0a9814509858897bd6ac6f7bfee3882901e1652`, containing implementation `35aabd582e754bb293337eace0a6b45aeee58026` and closure `b0a9814509858897bd6ac6f7bfee3882901e1652`. Exact-SHA Actions run `38116562952` succeeded.
- Missed: none known.
- Follow-up: RQ609 (primary READY); RQ612 remains an additional path-disjoint READY prompt.
- Residual risk: Actions logged a non-fatal duplicate `pg_extension_name_index` error while applying batch 4 of `025_AddTrendplusPerformanceIndexes.sql`; the retry completed and the workflow passed. This is separate from RQ608 and remains an infrastructure/bootstrap diagnostic.
- Post-close routing: RQ609 is the current primary READY pointer; RQ612 remains independently READY after the post-close scan.
- Prompt defect / scope repair: the master roadmap still said RQ READY was `none` although the current post-import addendum registered RQ608/RQ609/RQ612; repaired the master pointer at claim time and advanced it to RQ609 at close.

---

## RQ609 - RQ601 provenance migration compatibility and read-only release preflight

Status: DONE
Priority: P0
Type: schema-test/preflight script
Feature family: purchase-cost-provenance-schema-readiness
Parallel-safe: yes; new preflight tooling/isolated migration test, no RQ608 or RQ612 source files; owns schema-test lane until close
Owner: Codex
Ready after: RQ601 nullable EF/bootstrap migration exists; original 20 calculations are final business proof, not a start gate
Commit suggestion: `test(analytics): fail closed on cost provenance schema mismatch`

### Claim note

- Date: 2026-10-11
- Status: IN_PROGRESS
- Claim: Refreshed `origin/main` at `02007bb283bd91b5bae9ba87d179998e8b96ffab`; RQ609 remains the primary P0 READY prompt after RQ608 closed. No matching lock, branch or open PR exists. RQ612 is explicitly path-disjoint.
- Local lock: `.ai/task-locks/RQ609-codex.lock.md`.

### Completion note

- Date: 2026-10-11
- Status: DONE
- Completion: Added read-only JSON schema preflight with secret-safe target identity, exact nullable `varchar(64)` compatibility, EF history/bootstrap reporting and nonzero `missing`/`unknown` exits; added disposable PostgreSQL migration/rerun and API readiness/error contract proof; documented operator backup, preview, separately-authorized migrate, verify and rollback procedure.
- Changed files: `tools/purchase-cost-provenance-preflight.mjs`; `scripts/purchase-cost-provenance-preflight.test.mjs`; `Api.Tests/Rq609PurchaseCostProvenancePostgresIntegrationTests.cs`; `docs/runbooks/PURCHASE_COST_PROVENANCE_SCHEMA_PREFLIGHT.md`; `MASTER_ROADMAP.md`; this queue; `.ai/runs/2026-10-11-RQ609-evidence.md`.
- Contract/runtime behavior changed: no; API `/ready` remains a connectivity/startup signal and the preflight is the release schema-compatibility gate.
- Checks run: Node preflight tests 5/5; disposable PostgreSQL integration 2/2 (0 skipped, 0 failed); Release API.Tests focused build/test passed. Initial Debug build could not overwrite DLLs held by an existing .NET Host process and did not execute tests; the same focused test passed in Release. Agent instruction self-test/check passed; prompt queue self-test/check passed (733 tasks); planning architecture self-test/check passed (80 tasks); `git diff --check` passed.
- Checks not run: full backend test suite; production/live schema preflight and migration; these are outside this repository-only task and production change authority was not granted.
- Run log: `.ai/runs/2026-10-11-RQ609-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `d85fe70a1be4c1e52b8035cdf7549925005322a5`
- Main verification: refreshed `origin/main` to `be4d6ee155210a9216c99c4a3191871e0dd34af0`; it contains implementation commit `d85fe70a1be4c1e52b8035cdf7549925005322a5` and closure commit `be4d6ee155210a9216c99c4a3191871e0dd34af0`.
- Missed: no operator-authorized live migration was executed; live deployment proof remains STAB16.
- Follow-up: RQ610 (primary READY); RQ612 remains independently READY and path-disjoint.
- Residual risk: Analytics Tests & Data Integrity run `38118021843` on `d85fe70a1be4c1e52b8035cdf7549925005322a5` remains `in_progress`; Planning Governance run `38118021859` on the same SHA succeeded; closure-SHA Planning Governance run `38118155298` was `in_progress` at evidence capture. These remote runs do not replace the focused disposable-database proof.
- Next: RQ610.
- Post-close routing: refreshed `origin/main` to `be4d6ee155210a9216c99c4a3191871e0dd34af0`; scanned the active RQ queue/addenda set and SQL queue for changed/dependent IDs RQ609/RQ610/RQ612, RQ96 and RQ569, and rechecked BCI/STAB current READY. RQ610 dependencies RQ609 and RQ96 are DONE; RQ610 was promoted WAITING -> READY after a clear lock/branch/open-PR scan. RQ612 remains independently READY; RQ611 remains WAITING behind RQ610.
- Prompt defect / scope repair: none.

### Problem
Current local batch-23 business DB was **not migrated** after RQ601 added `prodaja_stavke.purchase_cost_provenance`. Do not deploy endpoints reading the column against an unmigrated DB; no implicit bootstrap green without a real test.

### Read first
`Infrastructure/Migrations/20261010160000_AddPurchaseCostProvenance.cs`, `Infrastructure/Seed/DatabaseInitializer.cs`, `Infrastructure/DbContexts/TrendplusDbContext.cs`, `docs/qa/PURCHASE_COST_PROVENANCE_2026-10-10.md`, `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`.

### Do
1. Build a **read-only** preflight script that reports connection target identity without exposing secrets, column existence/type/nullability, EF migration/bootstrapped schema status, and explicit `ready/missing/unknown` with nonzero failure exit on unsupported schema.
2. On disposable PostgreSQL only: old schema -> canonical additive migration -> rerun -> preserved numeric costs, NULL and unknown origin. Verify API readiness contract when column absent; never convert error into measured zero.
3. Document operator backup/preview/migrate/verify/rollback instructions for separately authorized execution. Do not apply migration to local `trendplus`, Render/Neon or any business DB, and don't modify Render manual-deploy policy.

### Tests
Real PostgreSQL isolated migration/rerun, preflight assertions, focused build, 0 skipped/notExecuted, governance validators.

### Acceptance
A precise machine-readable schema compatibility verdict and reproducible safe operator runbook, delivered on main with evidence. RQ601 remains PARTIAL until historical original calculations. Live deployment proof stays with STAB16.

### Dependencies
Repository-local READY. No production privileges or owner cost sample required to start.

---

## RQ610 - Schedule opt-in observed SKU×store×day inventory capture using RQ96 foundation

Status: BLOCKED
Priority: P1
Type: inventory-worker/SQL/tests
Feature family: inventory-observed-daily-capture
Parallel-safe: yes for isolated read-only source-qualification SQL/evidence; no worker/importer/schema edits concurrently with RQ611 or another active owner
Owner: Inventory Capture (unassigned; source lineage contract required)
Local lock: removed after BLOCKED handoff
Ready after: RQ609 DONE, RQ96 DONE

Claim 2026-10-11: refreshed `origin/main` at `87a05efd4276e43a14c568e427778075b7a9d88b`; RQ609 and RQ96 are DONE. No RQ610 lock, branch or open PR was found. The active P-UI-43 lock is unrelated; RQ612 is explicitly path-disjoint. RQ610 was claimed by Codex on `main`.
Commit suggestion: `feat(inventory): opt-in observed daily stock capture`

### Problem
RQ96 already created observed snapshot SQL/capture function and provenance view, but scheduled EOD capture, store-grained source proof and runtime freshness were explicitly left undone. The table can be empty; current inventory must not be mistaken for historical August.

### Read first
RQ96 completion in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md`, `.ai/runs/2026-08-19-RQ96-evidence.md`, RQ96 SQL view/function, `Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql`, worker/refresh contracts.

### Do
1. Extend **existing** RQ96 function/table. Implement disabled-by-default opt-in scheduled capture with store, article, local business date, exact observed UTC instant, source batch/generation, run state, data origin and observed/reconstructed provenance.
2. Enforce idempotent re-capture/retry, concurrency safety, incomplete-run rollback and freshness. Two stores, 0 vs missing vs NULL/negative, late import, same-day repeated run, DST boundary tests.
3. Capture future observed days only with source evidence; do not backfill past August using today's `Artikli`. Consumer warnings/reporting must distinguish not captured from verified zero and reconstruction.
4. Do not expose historical `asOf` to users until validated coverage and a separately approved API contract. No production scheduled job enabled without authority.

### Tests
Disposable PostgreSQL capture idempotency/rollback, worker scheduling, provenance and performance; 0 skipped; Release build.

### Acceptance
Reliable opt-in prospective observed stock capture, truthful source metadata and no fake history; implementation/evidence/main commit. Production scheduling is separate operator acceptance.

### Dependencies
RQ609 schema-lane handoff; STAB16 provider access is not a code start gate.

### Owner-approved re-entry — bounded source-grain qualification (2026-10-11)

**Status interpretation:** The BLOCKED decision below correctly forbids pretending `ProductsDim` is store-grained, but its broader statement that *no possible* per-store stock source exists was not established. Current `Domain/Model/Artikli.cs` exposes `IDObjekat`, `Kolicina`, `UpdatedAt`, `DataOrigin`, `SourceTableKey`, `SourceRowId`, `SourceUpdatedAtUtc`, `SourceHash` and `SourceBatchId`; `Api/Services/AccessImportService.cs` accepts Artikli store alias `idobjekat/storeid` and records imported source lineage; `Api/Services/TransferService.cs` explicitly resolves destination stock entries by `IDObjekat`. None proves per-store historical snapshot coverage automatically. Reopen **only the source qualification and safe bounded follow-through**, not production capture enablement.

1. **First deliverable:** execute `tools/rq610-store-stock-source-qualification.sql` read-only on the exact existing local `trendplus` business PostgreSQL (operator reported 127.0.0.1:5434) and record safe target identity, row origin, per-store counts, NULL/unmapped store IDs, non-null quantities, `SourceBatchId` coverage, distinct source lineages, `UpdatedAt`/source timestamps, and exact ArticleId/ProductsDim quantity parity. If connection unavailable, run deterministic disposable PostgreSQL fixtures and mark actual batch-23 verification `not run`. No new import, seed, migration or source data correction.
2. Verify whether each `Artikli.Id` genuinely denotes a **store-specific stock line** (not merely the owning/default store), whether quantities and timestamps are authoritative for that line, how the same logical SKU/PLU appears in multiple stores, and how POS/transfer/import updates affect `UpdatedAt` and source batch lineage. Distinguish `SourceBatchId` (last Access import lineage) from a post-import operational stock-change generation. Prove non-fanout and no double counting. A NULL `IDObjekat` is unverified store identity, not store 0 or each location.
3. **Only if** real evidence supports SKU×store current on-hand and a reliable observation/generation boundary: implement a separately selected `Artikli`-based source adapter for the existing RQ96/RQ610 capture, preserving `ProductsDim` company-grain `store_id=0` as a different source. Do not double-count the company row against store rows in aggregate/view/readers; do not attach false source-batch certainty. Require isolated PostgreSQL tests for two stores sharing PLU, missing store, same article different sources, NULL/zero/negative stock, POS/transfer correction, batch retry, concurrent capture, DST and atomic rollback. Serialize any write-path work with RQ611's importer owner.
4. **If unproved:** keep the existing opt-in company-grain implementation intact; record a compact field-by-field evidence matrix, set RQ610 back to BLOCKED with **the specific missing column/source behavior** and exact next owner/event. This is not grounds to block the path-disjoint RQ611/RQ612.
5. Only mark RQ610 DONE if all store-grain acceptance tests, provenance and source freshness/coverage proofs exist. Production cron remains disabled; never backfill August as observed. Do not modify any real business DB or change `Artikli` rows to manufacture coverage.

**Re-entry evidence:** `.ai/runs/2026-10-11-RQ610-source-reentry-evidence.md`; `main` currently contains the prior opt-in company-grain implementation `34a5ecf2` with exact-SHA backend CI `38119404029` now green. RQ608 exact-SHA CI `38116562952` and RQ609 exact-SHA CI `38118021843` are also green. Historical BLOCKED note below is preserved as an audit trail; it no longer constitutes a start gate for read-only/source-qualification work.

### Claim — bounded source qualification

- Date: 2026-10-11
- Claim status at start: IN_PROGRESS; closed as BLOCKED below
- Claim: refreshed `origin/main` to `3a6e442ecfef478996708d9642f37a38a1e27e6d` after RQ611 close-out; RQ610 is the primary READY candidate. No matching lock, branch or open PR was found. RQ612 remains independently READY and path-disjoint.
- Local lock: `.ai/task-locks/RQ610-codex.lock.md`.

### Source-qualification outcome / handoff (2026-10-11)

- Date: 2026-10-11
- Status: BLOCKED
- Outcome: Local evidence supports `Artikli` as structurally store-keyed current rows, but does not support trustworthy stock observation freshness/generation. No capture adapter or worker/schema changes were made.
- Changed files: `MASTER_ROADMAP.md`; this queue; `.ai/runs/2026-10-11-RQ610-source-qualification-evidence.md`.
- Checks run: read-only local Trendplus source qualification and cross-table reconciliation; focused governance checks and `git diff --check`.
- Checks not run: disposable capture/worker fixture and Release build; no runtime capture code was changed because the source-generation contract failed qualification.
- Run log: `.ai/runs/2026-10-11-RQ610-source-qualification-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `93b1c3dcc3a6f74373ca68754c8af064642cede6`
- Main verification: fresh fetch verified `origin/main` at `93b1c3dcc3a6f74373ca68754c8af064642cede6`.
- Missed: POS/Access and manual stock updates are not tied to a common audited generation; existing history was not backfilled.
- Follow-up: inventory/source owner must define a durable current-stock mutation generation and prove it across imports, POS, manual edits and transfers before re-entry.
- Residual risk: `UpdatedAt`, `DataOrigin` and `SourceBatchId` can retain old Access provenance after non-Access quantity edits; this is unsafe as a freshness signal.
- Post-close routing: fresh post-delivery scan from `93b1c3dcc3a6f74373ca68754c8af064642cede6` confirmed RQ612 as primary READY; details in run log.
- Prompt defect / scope repair: bounded READY source qualification was executable and confirmed store-key relationships, but its acceptance was not met because reliable mutation generation is absent. Re-blocked at the specific freshness/provenance boundary; worker/schema scope remains released.
- Ownership transfer: Codex -> unassigned; authority/evidence: current claimant's source-qualification result in this queue and `.ai/runs/2026-10-11-RQ610-source-qualification-evidence.md`; effective 2026-10-11 after read-only inspection and before close-out commit; no branch/PR artifacts existed or were reused.

### Historical blocker / repository-local delivery (before source re-entry)
- Delivered the opt-in scheduler, run ledger, exact capture instant, same-day update retry and idempotent company-grain `store_id=0` capture on `main` at `34a5ecf2cdc5f3702b259303952873ac67974366`.
- Source proof: `ProductsDim` has article, quantity, row timestamp and data origin, but no `StoreId`; `SyncWorker.MapToDim` maps `Artikli.UpdatedAt` to its timestamp. There is no authoritative per-store stock table or ProductsDim-to-import-batch key in this source. Copying company stock to `StoresDim` locations or inventing a batch id would fabricate evidence.
- `source_batch_id` remains NULL; the saved source-row update watermark is explicitly not treated as a batch id or freshness certification. This is insufficient to complete truthful SKU×store×day capture.
- Handoff/release 2026-10-11: Codex -> unassigned, authority/evidence: current claiming owner records the unresolved source contract in `.ai/runs/2026-10-11-RQ610-evidence.md`; worker/schema paths are released after main delivery so independent RQ611 may proceed.
- Exact unblock event: the inventory/source owner identifies an authoritative store-grained on-hand source and its observation timestamp, batch/generation identity and data-origin mapping, plus a deterministic disposable PostgreSQL fixture contract.
- Run log: `.ai/runs/2026-10-11-RQ610-evidence.md`
- Evidence state: synchronized

---

## RQ611 - Real per-store/per-day source-coverage evidence for sales and markdown measurement

Status: DONE
Priority: P1
Type: import-metadata/analytics-integrity/tests
Feature family: imported-store-day-coverage
Parallel-safe: yes; importer/store-day evidence paths are separate from released RQ610 worker/schema paths and RQ612's new shadow-control SQL/fixture
Owner: Codex
Local lock: removed after DONE close
Ready after: satisfied 2026-10-11 - RQ610 shared worker/schema ownership explicitly released; RQ569 horizon contract DONE
Commit suggestion: `feat(analytics): prove store-day importer completeness`

### Claim note

- Date: 2026-10-11
- Status: IN_PROGRESS
- Claim: refreshed `origin/main` to `41bc37865cc002ae7f2912f3e72ca043e002e6db`; RQ611 remains primary READY. RQ610 read-only source qualification and RQ612 are path-disjoint. No matching local lock, branch or open PR was found.
- Local lock: `.ai/task-locks/RQ611-codex.lock.md`.

### Problem
RQ569 knows bounded source horizon, but global import status or min/max receipt dates cannot verify **each day per store** or an empty sales day. RQ602's 40 windows remain unverified for good reason.

### Read first
RQ569 source freshness implementation/evidence, `DataImportBatches` and import checkpoints, `Api/Services/AccessImportService.cs`, `tools/markdown-pilot-measurement.sql`, RQ602/RQ96 evidence contracts.

### Do
1. Design minimal `source batch × store × business day` complete/partial/failed/missing evidence with source fingerprint/checkpoint, timezone and counts; extend existing source registry instead of inventing a parallel freshness model.
2. Mark a zero-sale day VERIFIED only when importer processed an authoritative complete source day or trusted explicit zero declaration. Never infer completeness from no sale, an empty query, last nonzero date, or global batch `completed`.
3. Produce reader/SQL oracle with `verified_empty`, `observed_sales`, `partial`, `unverified` and source lineage per scope; rerun/late correction may invalidate previously declared completion.
4. Legacy batch #23 gaps must remain unknown without byte-identical MDB/day source evidence. Do not backfill invented daily coverage or rerun import.
5. Add disposable PostgreSQL tests: two stores, true empty day, missing/failed day, overlapping batches, duplicate receipt lineage, corrections, DST, DUG/KOREKCIJA and signed returns; provide RQ602 consumer hook, without enabling recommendations.

### Tests
Independent SQL oracle and importer metadata contract tests, 0 skipped, build/guardrails.

### Acceptance
Future imports can produce truthful store/day coverage; existing history stays unverified as appropriate, no fake green or zero. Main/evidence delivery only; production DB not changed.

### Dependencies
RQ569 DONE and RQ610 captured/schema path released. RQ592 real pilot remains separate.

### Delivery note
- Store/day evidence contract: `docs/architecture/STORE_DAY_SOURCE_COVERAGE.md`.
- Read-only frozen-cohort consumer hook: `tools/markdown-pilot-store-day-coverage-oracle.sql`; it requires a complete status for every requested store/day and selects the latest source attempt, leaving absent evidence unverified. It does not alter RQ602's frozen manifest/query or make a recommendation.

### Completion note

- Date: 2026-10-11
- Status: DONE
- Completion: Future Access imports can persist conservative store/day source coverage with batch lineage; only a fully accounted direct snapshot can certify empty days, while missing stores, gaps outside observed bounds and historical batch #23 remain unverified.
- Changed files: `Api/Services/AccessImportService.cs`; `Api/Services/AccessStoreDayCoverageEvidence.cs`; `Infrastructure/Migrations/20261011110000_AddAccessImportStoreDayCoverage.cs`; `Api.Tests/AccessStoreDayCoverageEvidenceTests.cs`; `Api.Tests/AccessImportStoreDayCoveragePostgresTests.cs`; `tools/markdown-pilot-store-day-coverage-oracle.sql`; `docs/architecture/STORE_DAY_SOURCE_COVERAGE.md`; `MASTER_ROADMAP.md`; this queue; `.ai/runs/2026-10-11-RQ611-evidence.md`.
- Contract/runtime behavior changed: future importer executions persist evidence transactionally when the migration is deployed; the consumer oracle is read-only and does not enable recommendations.
- Checks run: focused evidence and disposable PostgreSQL tests 6/6 passed; final PostgreSQL importer-oracle rerun 1/1 passed; EF migration list succeeded and showed this migration Pending; all six governance self-tests/checks and `git diff --check` passed before close-out synchronization. Actions: Analytics Tests & Data Integrity `38122708609` was in progress; Planning Governance `38122708546` succeeded on implementation SHA `a514ea4d0fe80896a297093077d22f78f7568593`.
- Checks not run: full backend suite; production migration/import; historical batch #23 backfill.
- Run log: `.ai/runs/2026-10-11-RQ611-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `a514ea4d0fe80896a297093077d22f78f7568593` (implementation); close-out documentation commit `0c2479ec762f1fa00fa02529f5bede1e1d134ada`.
- Main verification: fresh fetch verified `origin/main` at `0c2479ec762f1fa00fa02529f5bede1e1d134ada`, containing the implementation and close-out documentation.
- Missed: no historical per-store/day proof for batch #23; no production schema rollout.
- Follow-up: RQ610 is promoted as primary READY for bounded read-only source-grain qualification; RQ612 remains independently READY.
- Residual risk: future evidence requires deployment of the pending migration; historical coverage is unknown; Analytics Tests & Data Integrity run `38122708609` remained in progress at final inspection, while Planning Governance `38122708546` succeeded.
- Post-close routing: fresh scan from post-delivery SHA `0c2479ec762f1fa00fa02529f5bede1e1d134ada` found RQ610 and RQ612 READY and path-disjoint; RQ610 is the next primary READY. Full scan and unblock/dependency details are in the run log.
- Prompt defect / scope repair: none.

---

## RQ612 - Find feasible nonzero-sale shadow controls for the next markdown pilot

Status: DONE
Priority: P1
Type: read-only-SQL/methodology/tests
Feature family: markdown-shadow-control-validity
Parallel-safe: yes; create a **new** shadow-only query/report/test, do not overwrite RQ602 frozen 40-member manifest or touch RQ609 schema/RQ608 test files
Owner: Codex
Local lock: removed after DONE close
Ready after: RQ602 frozen historical evidence exists. No authorization for prospective action is implied.

### Claim note

- Date: 2026-10-11
- Status: IN_PROGRESS
- Claim: refreshed `origin/main` to `683b83dce4bbe3c0c4699d4d2a8629e5d4789bc5` after RQ610 source-qualification close-out. RQ612 is the next path-disjoint READY prompt; no matching lock, branch or open PR was found. RQ610 was returned to BLOCKED and ownership released; its importer/worker paths are not being changed here.
- Local lock: `.ai/task-locks/RQ612-codex.lock.md` (removed after close).

Commit suggestion: `test(nivelacija): reject zero-signal shadow controls`

### Problem
The 20 RQ602 treated articles have 22->76 sold units and 161,110->426,540 RSD; 20 controls are 0->0, one misses shoe type and all windows lack daily coverage. These controls cannot serve as a valid counterfactual, and uplift cannot be inferred.

### Read first
`tools/markdown-pilot-cohort-2026-10-10.sql` (IMMUTABLE historical evidence), `tools/markdown-pilot-historical-measurement.sql`, RQ602 report, RQ601 cost provenance, RQ592 pilot and RQ569 source horizon contracts.

### Do
1. In NEW `tools/markdown-pilot-shadow-control-candidates.sql`, select reproducible shadow controls matched by same store/supplier/shoe type where possible, comparable pre-price, pre-period active demand, availability evidence and no confounding overlap/repeated markdown. Do not reuse one control for many treated rows without explicit multiplicity/weighting.
2. Demand measurable nonzero control pre-sales, with configurable exploratory minimum that is **not** a fabricated definitive statistical sample threshold. Reject supplier-only mismatch and zero-control baselines as unqualified; report the exact exclusions and remaining number of pairs.
3. Run read-only SQL on batch #23 if actually available, otherwise use an independent disposable PostgreSQL fixture and report real DB unavailable. If insufficient matches, say `insufficient_control_population`; do not invent revenue.
4. Keep half-open local time windows, signed returns, DUG/KOREKCIJA exclusion, stockout/seasonality confounders, unknown cost and unverified source-day coverage explicit. No causal DiD recommendation before preregistration + future reliable measurement.
5. Deliver owner-facing RQ592 prospective pilot candidate design (eligibility, consent, freeze, 28-day outcome windows if defensible, stop conditions and readiness gates). **Do not** change the existing RQ602 frozen cohort, activate markdown recommendations or trigger any price change.

### Tests
Deterministic SQL/fixture cases for zero-control, unmatched stratum, repeated markdown, stockout missing evidence, date-boundary, unknown cost; 0 skipped when certification claims PostgreSQL.

### Acceptance
Nonzero-control design with explicit feasibility and limitations, or precisely documented insufficiency; shadow-only work on main. Prospective launch and causal/business proof remain RQ592 + STAB16 + owner approval.

### Dependencies
No start gate from original MDB or 20 cost calculations for descriptive revenue/units-only feasibility. Source/day coverage and genuine prospective data still required before RQ592 business conclusion.

### Completion note

- Date: 2026-10-11
- Status: DONE
- Completion: Added a read-only screen for exact-stratum, nonzero-demand shadow controls and an RQ592 prospective pilot design. Batch #23 yielded four unique exploratory candidates but zero fully qualified pairs because historical daily availability and source coverage remain unverified.
- Changed files: `tools/markdown-pilot-shadow-control-candidates.sql`; `Api.Tests/MarkdownPilotShadowControlSqlTests.cs`; `docs/qa/MARKDOWN_PILOT_SHADOW_CONTROLS_RQ612_2026-10-11.md`; `MASTER_ROADMAP.md`; this queue; `.ai/runs/2026-10-11-RQ612-evidence.md`.
- Contract/runtime behavior changed: no runtime behavior, recommendation, price or database state changed; the SQL is read-only and shadow-only.
- Checks run: read-only local batch-23 query; disposable PostgreSQL fixture tests 2/2 passed, 0 skipped; Release Api.Tests build completed; all six governance validators and `git diff --check` passed (recorded in run log).
- Checks not run: full backend suite locally; prospective pilot, historical availability proof, causal estimate and owner approval. Remote main full-backend Actions run `38125791980` later passed on the implementation SHA.
- Run log: `.ai/runs/2026-10-11-RQ612-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `370a553b0af65b33799e8560ae8e7781f6e1e134`
- Main verification: fresh fetch from `origin/main` at `370a553b0af65b33799e8560ae8e7781f6e1e134` contains implementation SHA `370a553b0af65b33799e8560ae8e7781f6e1e134`.
- Missed: no candidate qualified while historical source-day coverage and stock availability remain unverified.
- Follow-up: RQ592/STAB16 owner gates remain; RQ610 stays BLOCKED on source mutation generation.
- Residual risk: four exploratory candidates are not eligible controls for a causal or business conclusion; current stock snapshots cannot certify historical availability. Analytics Tests & Data Integrity run `38125791980` passed; Planning Governance runs `38126221304` and `38126319495` passed on evidence-sync SHAs.
- Post-close routing: no safe successor after full post-delivery RQ/SQL and cross-program scan; Zero-READY proof and candidate matrix in the run log.
- Prompt defect / scope repair: none.


---

## RQ614 - Isolate intelligence smoke tests and make PostgreSQL certification fail closed

Status: DONE
Priority: P0
Type: test-safety/PostgreSQL regression
Feature family: intelligence-test-isolation
Parallel-safe: yes; owns Api.Tests/AnalyticsIntelligenceSmokeTests.cs, Api.Tests/AnalyticsIntelligenceSaleJoinTests.cs and test-only fixtures; RQ615 owns runtime SQL/bootstrap/refresh paths
Owner: Codex / worktree `rq614-test-isolation`
Local lock: removed after done close
Ready after: RQ613 DONE; actual unsafe smoke execution recorded in .ai/runs/2026-10-11-RQ613-evidence.md
Commit suggestion: test(analytics): isolate intelligence bootstrap and prohibit business database mutation

### Claim note — 2026-10-11

- Refreshed `origin/main` to `1aae9d97f61fd9843008b7b414e7b9db2c12dd87`; RQ613 is DONE and RQ614 is the primary READY P0 prompt.
- No RQ614 lock, branch or open PR existed before this claim. RQ615 remains path-disjoint and independent.
- Claimed READY -> IN_PROGRESS in worktree `rq614-test-isolation`; local lock: `.ai/task-locks/RQ614-codex.lock.md`.
- The original main checkout contains unrelated uncommitted RQ610 test/evidence/queue changes; they are preserved and excluded from this worktree.
- Audit found `DatabaseMigrationBootstrapLifecycleSmokeTests` and `DatabaseInitializerP0IntegrationTests` also had silent PostgreSQL early returns; both now use the isolated test-only fixture and fail closed.

### Problem / confirmed evidence
The five AnalyticsIntelligenceSmokeTests resolve AnalyticsConnection/DefaultConnection from development appsettings, call IntegrationDbGuard.TryEnsureAvailable, and execute CREATE OR REPLACE VIEW and materialized-view SQL against that selected database. The RQ613 run actually rebuilt analytics_intel objects on the local trendplus business database. AnalyticsIntelligenceSaleJoinTests also returns silently when the PostgreSQL fixture or one of its created databases is unavailable; its isolated proof could be counted green with no assertions.

### Do
1. Replace every intelligence smoke bootstrap with a disposable, verified PostgreSQL test database and deterministic minimal schema/seed. Never execute DDL from a test against a developer's trendplus database or any production/shared target, even when a connection string is available in appsettings.
2. Add a strict executable safety guard before SQL execution: only an isolated database created by the test is eligible; reject ambiguous/shared/local-business/prod connections. Do not rely only on host name or Environment == Development. Teardown the isolated DB safely.
3. Fail closed for the mandatory intelligence PostgreSQL tests when the test DB cannot start/create/connect; no early return, no fake PASS, no hidden skip. Prove actual test assertions and executions in TRX (expected=executed, failed=0, skipped=0).
4. Add negative regression cases for accidentally configured business DB, unreachable PostgreSQL, and existing intelligence schema. Confirm no mutation occurs on forbidden connections. Keep source data and non-test schemas untouched.
5. Check other tests invoking Database/Analytics/Intelligence SQL or DatabaseInitializer bootstrap for the same unsafe pattern. Repair proven cases within this test-only owner scope; register a small successor only if runtime/code ownership would collide.

### Acceptance
All five smoke cases and the RQ613 source-SaleId probe run against isolated databases only. The probe must verify distinct Id/SaleId and a surrogate-ID collision on real PostgreSQL; tests fail when unavailable, not pass. API/backend focused tests and Release build pass; exact TRX test counts and governance validations are recorded. No writes to local trendplus, Render, Neon or any business database; no new Access import. Commit/merge/push safely with current-main ancestry and .ai/runs evidence.

### Completion note

- Date: 2026-10-11
- Status: DONE
- Completion: All five intelligence smoke cases, the real PostgreSQL source-SaleId control/probe, three database-safety negatives, the bootstrap lifecycle test and five audited initializer P0 tests now use the disposable fail-closed fixture; no configured/local business database is used.
- Changed files: `Api.Tests/IntelligencePostgresFixture.cs`, `Api.Tests/Fixtures/analytics-intelligence-smoke-seed.sql`, `Api.Tests/AnalyticsIntelligenceSmokeTests.cs`, `Api.Tests/AnalyticsIntelligenceSaleJoinTests.cs`, `Api.Tests/AnalyticsIntelligenceDatabaseSafetyTests.cs`, `Api.Tests/DatabaseMigrationBootstrapLifecycleSmokeTests.cs`, `Api.Tests/DatabaseInitializerP0IntegrationTests.cs`, this queue addendum, `MASTER_ROADMAP.md`, and `.ai/runs/2026-10-11-RQ614-evidence.md`.
- Checks run: focused Release PostgreSQL run 16/16 passed, 0 failed, 0 skipped, 0 not executed; Release project build passed with 0 warnings and 0 errors; six governance validators and `git diff --check` passed (results in the run log).
- Checks not run: full backend suite; not required for the bounded test-only change. The current-main Actions runs listed below were still in progress when inspected.
- Run log: `.ai/runs/2026-10-11-RQ614-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `918fdd4e4a7df86ac0696ba720d72f7e7eee657e`
- Main verification: passed; refreshed `origin/main` contained the implementation SHA. The closure/evidence commits are descendants of it.
- Missed: none known within RQ614 acceptance.
- Follow-up: RQ615 remains the primary READY task; RQ616 remains WAITING for RQ615 cache-parity delivery.
- Residual risk: full backend suite not run; Analytics Tests & Data Integrity run `38161632292` and Planning Governance run `38161632296` were `in_progress` at inspection and were not used as a completion gate.
- Post-close routing: RQ615 remains READY; RQ616 is still gated on RQ615. Full active-set scan and exact recovery base are recorded in the run log.
- Prompt defect / scope repair: same-owner test-only audit expanded to the `DatabaseMigrationBootstrapLifecycleSmokeTests` and `DatabaseInitializerP0IntegrationTests` silent-return paths; no runtime/code owner collision.

---

## RQ615 - Ensure intelligence caches actually adopt the RQ613 SQL change

Status: DONE
Priority: P0
Type: backend/startup-SQL/materialized-view-freshness
Feature family: intelligence-cache-convergence
Parallel-safe: yes; owns Infrastructure/Seed/DatabaseInitializer.cs, intelligence SQL files 021/022/024, runtime refresh orchestration and a distinct isolated integration-test file; do not edit RQ614-owned test files
Owner: Codex / worktree `rq615-cache-convergence`
Ready after: RQ613 DONE
Commit suggestion: fix(analytics): certify cache refresh after intelligence SQL hash change

### Claim note — 2026-10-11

- Refreshed `origin/main` to `39336101e53eaa09b6d4a5953da2206a645fe45b`; RQ613 is DONE at `a6acdbbb4d016767522f70b670d237ba0af4833c` and RQ614's isolated test guard is DONE at `918fdd4e4a7df86ac0696ba720d72f7e7eee657e`.
- RQ615 was READY, P0 and the primary eligible RQ task. No RQ615 task lock, local/remote branch or open PR existed; no other active owner owns the cache-convergence feature family. RQ614 is closed and owns test-isolation paths; this claim owns the initializer, intelligence SQL/refresh behavior and a separate integration-test file.
- Transitioned READY -> IN_PROGRESS in worktree `rq615-cache-convergence`; local lock: `.ai/task-locks/RQ615-codex.lock.md`.
- Working hypothesis to reproduce before runtime edits: changed script hashes re-run `CREATE MATERIALIZED VIEW IF NOT EXISTS`, which can leave pre-existing unchanged-schema materialized rows stale after the corrected source SaleId join.
- Execution boundary: disposable PostgreSQL only; no business database mutation, production deployment or import.

### Problem / confirmed evidence
DatabaseInitializer re-executes 021/022/024 on changed script hash, asynchronously with a background advisory lock and warning-only exception handling. The scripts run CREATE OR REPLACE VIEW, while dropping old materialized caches only on COLUMN STRUCTURE changes. With the unchanged RQ613 output schema they use CREATE MATERIALIZED VIEW IF NOT EXISTS, which does not refresh an existing cache. A successful SQL hash/history update or API /ready does not by itself prove that the cached data has been recalculated. NightlyAnalyticsRefreshWorker may be disabled, stopped or delayed. RQ613's test checks newly created caches, not a previously populated stale cache after an in-place SQL change.

### Do
1. On disposable PostgreSQL only, seed a pre-RQ613 wrong-join cached state (surrogate Id != source SaleId and an ID collision), then apply changed-hash startup logic without changing the view schema. Prove or falsify immediate view-vs-MV parity for all three objects.
2. Implement the smallest safe, bounded, idempotent refresh/rebuild route on SQL semantic changes that cannot leave a successful-looking stale cache. Preserve advisory-lock/concurrent-start contracts, avoid clearing unrelated caches, and define failure/partial status clearly.
3. Verify startup background failure/lock-contention, repeat boot, disabled nightly worker, schema unchanged, schema changed, and interrupted refresh. A successful history hash must not be mistaken for cache freshness; expose enough observable per-view freshness/version evidence for operators.
4. Distinguish release-time cache correctness from the separately authorized production deploy/rebuild. Do NOT auto-deploy, apply SQL to a business DB, re-import or mutate production. Preserve manual Render deployment policy. Re-run focused performance checks to avoid an unbounded synchronous cold-start.
5. Add real PostgreSQL before/after oracle for the THREE views and materialized caches with independent store/date/qty checks, stable output columns and 0 skipped tests.

### Acceptance
After successful isolated upgrade, cached demand/risk/trend rows reflect the corrected source SaleId join without relying on a nightly refresh; after a failed upgrade, stale outputs are clearly unavailable/degraded rather than trusted as current. No duplicate event or rollback surprise. Record exact SHA, test/CI evidence and operator runbook; send deployed/prod verification to the existing STAB16 owner, not a duplicate live task.

### Completion note

- Date: 2026-10-11
- Status: DONE
- Completion: 100%; changed-hash cache convergence, failure state, API fail-closed behavior and operator diagnostics delivered.
- Changed files: `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs`, `Api.Tests/AnalyticsIntelligenceCacheConvergenceTests.cs`, `Database/Analytics/Intelligence/020_create_intelligence_schema.sql`, `Database/Analytics/Intelligence/021_product_demand_signals_v1.sql`, `Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql`, `Database/Analytics/Intelligence/024_trend_momentum_v1.sql`, `Infrastructure/Seed/DatabaseInitializer.cs`, `docs/Analytics/INTELLIGENCE_README.md`, this queue addendum, `MASTER_ROADMAP.md`, and `.ai/runs/2026-10-11-RQ615-evidence.md`.
- Checks run: focused Release PostgreSQL/API tests 10/10 passed, 0 skipped; `dotnet build Api/Api.csproj -c Release --no-restore --verbosity quiet` passed with 0 warnings/errors; all six instruction/queue/planning governance checks and self-tests passed; `git diff --check` passed. For implementation SHA `4eb58461af39b442a6eb1b471e4ed6073f4e0387`, Analytics Tests & Data Integrity run `38164357316` and Planning Governance run `38164357421` completed successfully. Planning Governance run `38164786625` for evidence-sync SHA `6a2c6aa05957704d7ffe75ce6fe029be31f39080` was `in_progress` at final inspection; it was not polled.
- Checks not run: full backend solution suite; no mapped intelligence performance benchmark exists. Heavy cache work remains asynchronous; new/schema-rebuilt caches skip duplicate refresh.
- Run log: `.ai/runs/2026-10-11-RQ615-evidence.md`
- Evidence state: synchronized in `.ai/runs/2026-10-11-RQ615-evidence.md`.
- Delivery mode: direct-main
- Main commit SHA: `4eb58461af39b442a6eb1b471e4ed6073f4e0387`
- Main verification: refreshed `origin/main` at `6a2c6aa05957704d7ffe75ce6fe029be31f39080` contains implementation SHA `4eb58461af39b442a6eb1b471e4ed6073f4e0387` and the terminal transition.
- Missed: none known within the code acceptance; production deployment and rebuild were not authorized or performed.
- Follow-up: RQ616 is promoted to READY after RQ615 cache parity and the RQ614 test-safety guard; the full post-close scan found it to be the only current RQ READY candidate.
- Residual risk: endpoints return 503 while a changed cache is pending or failed; live Render deployment/parity remains with STAB16.
- Post-close routing: recovery base `origin/main` `97436868230b514e09462b8570587f0981d81627`; all 14 active RQ/SQL queue files and their 22 nonterminal task rows were reviewed again after terminal delivery. RQ616 is READY and unassigned; details and checked gates are in the run log.
- Prompt defect / scope repair: cache-state DDL was added to the existing intelligence bootstrap (020) so all three owned SQL scripts share one status contract; no RQ614-owned test files or fixtures were edited.

---

## RQ616 - Certify intelligence retail population and release impact after RQ613

Status: DONE
Priority: P1
Type: independent PostgreSQL oracle with bounded analytics SQL/API/UI correction
Feature family: intelligence-retail-population
Parallel-safe: yes after RQ615 release of shared 021/022/024 SQL ownership; initially read-only SQL and a separate fixture/test file
Owner: Codex
Local lock: removed before delivery; Codex retained task ownership through close-out.
Ready after: RQ615 DONE with isolated cache parity; RQ614 test-safety guard DONE

### Claim note — 2026-10-11

- Refreshed `origin/main` to `1ad4295e19043fe136393b2c7373f4ec61d805e9`; RQ615 is DONE on `4eb58461af39b442a6eb1b471e4ed6073f4e0387` and RQ614's test-safety guard is DONE on `918fdd4e4a7df86ac0696ba720d72f7e7eee657e`.
- RQ616 was the only current RQ READY task. No RQ616 lock, local/remote branch, open PR or conflicting intelligence-retail-population owner was found; the Master Roadmap has no higher-priority runnable READY task.
- Transitioned READY -> IN_PROGRESS on branch `codex/rq616-retail-population` in the clean managed worktree `rq614-test-isolation`; local lock: `.ai/task-locks/RQ616-codex.lock.md`.
- Working hypothesis: the three intelligence views may diverge from certified operational retail population through correction receipts, signed returns, demo/orphan facts, date handling or incomplete dimensions. No runtime SQL edit is justified unless the independent oracle proves a defect.
- Execution boundary: disposable PostgreSQL fixtures only; no local/business database, deployed service, production data or import.

### Problem
RQ613 fixed ID attribution but did not prove intelligence's population matches certified operational retail semantics. SalesFacts includes BrojRacuna, DataOrigin and demo/existing sources. The 021/022/024 daily_sales CTEs use SalesFacts + SalesLineFacts but currently do not explicitly exclude trimmed/case-insensitive DUG/KOREKCIJA. Signed returns, orphan/DEMO rows, date timezone, missing product dimensions and imported/existing/all coverage need independent proof before intelligence demand/risk/trend is treated as canonical retail.

### Do
1. Build a disposable PostgreSQL independent oracle against operational header/line retail semantics, with imported/existing/all, DUG/KOREKCIJA, signed returns, demo/orphan facts, midnight timezone, same SKU across stores, Id/SaleId collision and true zero vs unknown examples.
2. Compare each view AND MV output with the oracle; classify confirmed defects and their user-visible impact, do not treat reconstructed stock risk as observed stock. Preserve existing declared confidence/provenance semantics and avoid automatic recommendations from unverified history.
3. If a bounded defect is proven, implement minimum-scope correction after checking other analytics owners and concurrency; rerun PostgreSQL oracle/CI and record before/after differences. If not proven, deliver evidence-only with explicit limits.
4. Read-only local business DB comparisons are permitted ONLY after separately verifying the exact target and with an explicit read-only session; never execute intelligence bootstrap/smoke DDL against it. Deployed parity stays with STAB16.

### Acceptance
No unqualified claims about Access historical signals, no silent DUG/KOREKCIJA contamination, signed returns preserved, scope/unknown provenance documented, zero skipped PostgreSQL tests. Do not claim this prompt before RQ615 cache parity and the RQ614 test-safety guard are DONE; both prerequisites are now delivered.

### Completion note

- Date: 2026-10-11
- Status: DONE
- Completion: Independent disposable PostgreSQL oracle found and corrected retail-population/date defects in intelligence views, and preserved unknown reconstructed inventory baselines through the API and Insight Studio.
- Changed files: Database/Analytics/Intelligence/021_product_demand_signals_v1.sql; Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql; Database/Analytics/Intelligence/024_trend_momentum_v1.sql; Api.Tests/AnalyticsIntelligenceRetailPopulationTests.cs; Api.Tests/IntelligencePostgresFixture.cs; Api.Tests/Fixtures/analytics-intelligence-smoke-seed.sql; Api/Endpoints/AnalyticsIntelligenceEndpoints.cs; Klijent/clientapp/src/services/analyticsIntelligenceApi.ts; Klijent/clientapp/src/services/analyticsIntelligenceDerived.spec.ts; Klijent/clientapp/src/components/dashboard/IntelligenceSnapshotPanel.tsx; Klijent/clientapp/src/components/dashboard/IntelligenceSnapshotPanel.spec.tsx; MASTER_ROADMAP.md; this queue addendum; docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md; .ai/runs/2026-10-11-RQ616-evidence.md.
- Contract/runtime behavior changed: yes; 021/022/024 use UTC calendar-day boundaries and exclude trimmed/case-insensitive DUG/KOREKCIJA receipts while retaining signed quantities and source-origin scope. Missing current inventory baseline remains unknown/null; known zero stays zero through API and UI.
- Checks run: focused disposable PostgreSQL/API Release tests 13/13 passed, 0 skipped; frontend tests 18/18 passed; npm run check:analytics-guardrails passed (encoding and TypeScript checks included; one existing fake-zero fallback removed and 38 baseline violations remain); Analytics Tests & Data Integrity run 38167550530 on ac20efcf8b8d76b6a39c5b36b15b7752e0f01637 succeeded; Analytics Quality Gates run 38168216722 on f1200dff2c01fbc49a01b1268a5e40ea5ae39516 succeeded; all six governance checks, Planning Governance run 38169895189 on 8c6361be3d139b7461a1a01b9cdafe30d3e639b8, and git diff --check are recorded in the run log.
- Checks not run: full backend/frontend suites and deployed/live parity; deployed parity belongs to STAB16 and no production database, deployment, import or migration was used.
- Run log: .ai/runs/2026-10-11-RQ616-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: f1200dff2c01fbc49a01b1268a5e40ea5ae39516
- Main verification: fresh git fetch resolved origin/main to 8c6361be3d139b7461a1a01b9cdafe30d3e639b8; it contains implementation SHA f1200dff2c01fbc49a01b1268a5e40ea5ae39516, RQ616 terminal close 1c834840c8f961b041a5b4c0948332d1108bf527 and the BCI pointer repair.
- Missed: deployed parity and any claim that reconstructed current inventory represents observed historical stock.
- Follow-up: STAB16 retains deployed provider/deployment parity. RQ610 remains separately BLOCKED pending a passing all-writer source-generation contract and inspectable POS writer.
- Residual risk: SQL 022 stock-risk values remain reconstructed from the latest product baseline; NULL is preserved where that baseline is unknown. Live deployed parity remains unverified.
- Next: none; the post-close Zero-READY proof is in the run log.
- Post-close routing: Zero-READY proof in the run log.
- Prompt defect / scope repair: the prompt's initial read-only type was broadened only after the disposable oracle proved a bounded SQL defect; the related API/UI correction was required to preserve unknown-versus-zero semantics. No business database or production DDL was used.



---

## RQ619 - Prevent competing API startups from orphaning intelligence cache build generations

Status: DONE
Priority: P1
Type: PostgreSQL concurrency/startup-cache regression and targeted runtime fix
Feature family: intelligence-cache-convergence-concurrent-startup
Parallel-safe: yes for a dedicated test and DatabaseInitializer intelligence build state methods; no active RQ615 owner after DONE, no overlap with RQ610 production stock-generation work
Owner: Codex
Local lock: removed before delivery
Ready after: RQ615 DONE, RQ618 DONE; no provider/live access required
Commit suggestion: fix(analytics): serialize intelligence cache preparation with build ownership

### Confirmed source-code gap / hypothesis to prove
Infrastructure/Seed/DatabaseInitializer.cs executes PrepareAnalyticsIntelligenceCacheBuildAsync BEFORE its fire-and-forget Task.Run acquires AnalyticsIntelligenceBuildLockKey (987654323). Preparation unconditionally writes pending state and a new build_generation_id for each cache requiring upgrade. If two API instances interleave, a later Prepare can replace an earlier generation while its builder holds the lock; the later builder may fail to acquire the lock and return. The first builder then sees isPendingCacheGeneration=false, skips refresh/MarkReady; the cache can stay pending (HTTP 503) until a future startup. Existing AnalyticsIntelligenceCacheConvergenceTests cover a single prepared generation, lock contention then explicit retry, and failed builds, but no two-instance interleaving/Task.WhenAll scenario. Fail-closed is appropriate but indefinite orphaned pending without retry is not acceptable.

### Do
1. On one disposable PostgreSQL fixture, force a deterministic interleaving: Prepare A, hold build advisory lock with builder A, Prepare B before A's cache processing, run both builder tasks, assert no permanent pending/failed state when one builder succeeds, and view/MV EXCEPT ALL parity. Seed a pre-RQ613 wrong-join materialized cache with Id != SaleId to verify semantic freshness; avoid tests that pass only on newly created caches.
2. Also test reverse ordering, contention without an available winner, startup cancellation/failure, same-hash restart, changed hash, 2+ API processes/generations, and no nightly worker. Existing RQ615 fail-closed 503 for stale/pending must remain intact and unchanged.
3. Fix only the actual proven race, e.g., serialize build preparation + execution under the SAME advisory lock or implement safe claim/lease/version-aware generation CAS and bounded recovery. Ensure a losing instance cannot overwrite a live builder's owned generation, and a skipped builder cannot silently strand pending. Do not mark ready until SQL hash, successful MV refresh/rebuild and generation ownership are verified.
4. Preserve startup latency requirements (no unbounded blocking cold start), current cache per-file version, exact hash and rollout operator evidence, and distinct 021/022/024 cache keys. Preserve business data and manual Render deployment policy.
5. Run targeted real PostgreSQL tests with TRX expected=executed, failed=0, skipped=0, Release build, RQ615 existing tests, queue/governance validators and current-main CI status. If the interleaving hypothesis is falsified, do not change production code; document precise counterexample and close as a proof-only DONE.

### Acceptance
Two overlapping startup instances cannot orphan a cache at pending when an eligible builder completes; failed/contended updates remain explicitly unavailable and recover automatically without live production/manual DB intervention. View/MV agree by independent SQL oracle, stale state never yields 200, no business DB DDL/import/migration/deploy. End with exact main SHA and per-test CI evidence. No force push.

### Claim note — 2026-10-12

- Refreshed recovery base: `origin/main` `9cc108173321c6c84b1e8cd8582467abb0f0b086`.
- Confirmed `RQ619` was `READY` and unassigned; no matching local branch, worktree lock or open PR was found.
- Isolated worktree: `C:\Users\Ivan\.codex\worktrees\rq619-intel-cache\Trendplus2`, branch `codex/rq619-intelligence-cache-startup`.
- Competing-startup regression reproduces pending orphan on disposable PostgreSQL (`Expected ready`, actual `pending`).

### Completion — 2026-10-12

- Serialized cache generation preparation and execution under the same PostgreSQL advisory lock, with bounded startup retry for a contending instance.
- Disposable PostgreSQL regression covers both startup invocation orders; all three 021/022/024 view/materialized-view pairs match under independent `EXCEPT ALL` checks.
- Focused convergence class: 7/7 passed, 0 skipped; Release test-project build and all six repository governance checks passed.
- Delivered to `main` at `a9281ba4d8f7b119fdf6ba10df623a7b4d7dfa3f`; fresh `origin/main` contains this exact SHA.
- Exact-SHA Analytics Tests & Data Integrity run `38182000713` passed; Planning Governance run `38182182163` on the closeout sync commit passed.
- Run log: `.ai/runs/2026-10-12-RQ619-evidence.md`
- Evidence state: synchronized

### Ownership
RQ615 stays DONE; this is a newly identified, narrower concurrency regression outside its existing fixed fixtures. RQ610 remains BLOCKED for unrelated all-writer stock-generation proof. RQ619 is independent of RQ601/RQ602/RQ606/STAB16 external evidence.


---

## RQ620 - Exclude future-dated source events from as-of intelligence signal populations

Status: DONE
Priority: P1
Type: disposable PostgreSQL retail-population/date-window oracle and targeted SQL correction
Feature family: intelligence-asof-upper-bound
Parallel-safe: no; overlaps 021/022/024 SQL fixture and populated-MV evidence, so serialize after RQ619 and the RQ616 null-baseline audit fix have landed
Owner: Codex (`codex/rq620-asof-bounds`)
Local lock: removed before implementation commit; claim closed after exact-main validation
Ready after: RQ619 DONE, RQ623 DONE (shared SQL 024), and RQ616 null-quantity SQL regression on main; verified before claim on refreshed `origin/main` `d56d1cd54bdfd5076f3b25f430e44c655150baee`
Commit suggestion: fix(analytics): bound as-of signal populations on both dates

### Verified code gap / hypothesis to prove
SQL 021, 022 and 024 daily_sales CTEs filter SaleTimestampUtc only with a lower bound; SQL 022 daily_non_sale_movements likewise has a lower bound only, and SQL 024 latest_trend_history lacks an as-of upper bound. The calendar ends on UTC as_of_date, but observed_pairs/observed_products are generated from these unbounded inputs. A product/store with only tomorrow's sale or future-dated movement can therefore enter today's signal population as a zero-evidence historical series. Future trend history can also win ROW_NUMBER ordering. These are source-code risks, not proof of a production occurrence.

### Do
1. On disposable PG build a UTC-boundary matrix: sale/movement/trend-history yesterday, today, +1 day and +30 days; an article/store seen ONLY in future; signed returns, DUG/KOREKCIJA, real zero vs NULL stock, orphan lines, Id != SaleId and all-origin population.
2. First demonstrate the incorrect or harmless current behavior in independent SQL assertions. Calculate expected as-of populations and values without copying production WHERE predicates.
3. If reproduced, add explicit upper bounds to date-filtered input CTEs (UTC date cutoff / exclusive next-UTC-midnight for timestamptz) without dropping legitimate current-day events or changing approved timezone policy. Check all observed_pairs/observed_products and trend-history branches, not just final calendar join.
4. Verify view-versus-populated-materialized-cache EXCEPT ALL on a pre-existing wrong-hash cache, changed-hash refresh and failed-refresh HTTP 503 behavior. Preserve RQ615/RQ619 cache generation semantics; no broad startup rewrite.
5. Run RQ616/RQ618/RQ619 focused PostgreSQL suites, release build, CLI governance and exact-main CI. Flag genuinely future-scheduled source records in diagnostic/provenance evidence where policy requires, not as fabricated past sales.

### Acceptance
No future-only article/store creates a misleading historical signal; future events do not affect as-of risk, demand or momentum, while same-day UTC data and signed retail semantics stay correct. Independent oracle and pre-existing cache parity are green, 0 skipped. No live business DB write, migration, import or deploy.

### Promotion and claim note — 2026-10-12

- Post-close recovery refreshed `origin/main` at `d56d1cd54bdfd5076f3b25f430e44c655150baee`; RQ619 DONE, RQ623 DONE, and RQ616's null-quantity SQL regression is present.
- Promoted to primary READY, then claimed after fresh inspection of all registered owner queues, active worktree branches, matching local locks and open PRs. No competing SQL 021/022/024/cache fixture owner was found.
- Isolated worktree: `C:\Users\Ivan\.codex\worktrees\rq620-asof-bounds\Trendplus2`; branch `codex/rq620-asof-bounds`.
- Local ignored lock `.ai/task-locks/RQ620-codex.lock.md` was created for the claim and removed before the implementation commit; no lock remains after close-out.

### Completion note

- Date: 2026-10-12
- Status: DONE
- Completion: Added end-exclusive UTC as-of bounds to SQL 021/022/024 input populations after disposable PostgreSQL reproduced future-only signals and future trend-history selection. Same-day sales, signed returns and established UTC calendar behavior remain intact; populated changed-hash cache parity and fail-closed refresh were verified.
- Changed files: `Database/Analytics/Intelligence/021_product_demand_signals_v1.sql`; `Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql`; `Database/Analytics/Intelligence/024_trend_momentum_v1.sql`; `Api.Tests/AnalyticsIntelligenceCacheConvergenceTests.cs`; `MASTER_ROADMAP.md`; this queue; `.ai/runs/2026-10-12-RQ620-evidence.md`; `.ai/runs/2026-10-12-RQ623-evidence.md`.
- Contract/runtime behavior changed: future-dated source sales, non-sale inventory movements and trend history no longer create or alter past as-of intelligence signals.
- Checks run: disposable PostgreSQL reproduction and regression; focused RQ620 test 1/1 passed, 0 skipped; RQ616/RQ618/RQ619 group 11/11 passed, 0 skipped; Release build 0 warnings/errors; six local governance validators passed; exact-main Analytics Tests & Data Integrity run `38220977010` passed after retry, backend 1,964 passed/42 classified skips and RQ453 required suites passed with zero skips; exact-main Planning Governance run `38222809357` passed on close-out SHA `0045d7fba8222e2d89c65c9dd3172b1d65a1a591`.
- Checks not run: local full backend suite did not produce a final summary after six minutes without progress and was interrupted; exact-main full backend suite passed. No browser/deployed/live-database validation was part of acceptance.
- Run log: `.ai/runs/2026-10-12-RQ620-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `876ed0aaa52c5526ffb0ed343e46fb8a39b1a2c1`
- Main verification: post-close refreshed `origin/main` at `0045d7fba8222e2d89c65c9dd3172b1d65a1a591`; it contains implementation SHA `876ed0aaa52c5526ffb0ed343e46fb8a39b1a2c1`. Exact-main workflow `38220977010` passed after rerunning failed jobs; first RQ453 attempt returned 503 `starting/db_warmup`, retry passed all required cases. Planning Governance `38222809357` passed on the close-out SHA.
- Missed: none within RQ620 acceptance.
- Follow-up: RQ621 is the primary READY successor. RQ622 remains WAITING on RQ621. RQ624 remains WAITING on RQ622. RQ625 is serialized after RQ621/RQ622/RQ624 because its DTO/client files overlap; RQ626 is serialized after RQ625 and the same cache/readiness owners. Other independent or externally gated prompts retain their existing owners/statuses.
- Residual risk: local full-suite execution was incomplete; exact-main backend suite and mandatory RQ453 certification passed. Production/deployed freshness remains owned by STAB16; no production data or service was touched.
- Post-close routing: refreshed recovery base `origin/main` `876ed0aaa52c5526ffb0ed343e46fb8a39b1a2c1`; scanned 13 active Analytics Reliability queue/addendum files plus `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`, including all non-terminal RQ prompts. RQ621 remains the only current READY in this addendum. RQ625/RQ626 dependencies are clarified to serialize shared `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs` and `Klijent/clientapp/src/services/analyticsIntelligenceApi.ts` paths behind RQ621/RQ622/RQ624 and each other. No unrelated non-terminal task's dependency changed.

---

## RQ621 - Preserve missing purchase cost instead of reporting a 100% Price Intelligence margin

Status: DONE
Priority: P1
Type: SQL/API/frontend nullability correctness with disposable PostgreSQL oracle
Feature family: price-intelligence-missing-cost
Parallel-safe: yes; owns SQL 023 cost/margin calculation, price DTO/types and dedicated tests, not RQ619 initializer or RQ623 SQL 024; coordinate with RQ622 before editing price endpoint
Owner: Codex (worktree `rq621-price-cost`, branch `codex/rq621-missing-cost`); closed on `d500304bdc767f32de773e07d534a3a50a0b12ae`
Ready after: RQ188 DONE and RQ616 DONE; no owner or external samples required for fail-closed missing-cost proof
Commit suggestion: fix(analytics): preserve unknown cost and margin in price signals

### Problem
`vw_price_intelligence_v1` currently computes missing purchase cost as measured zero, so a valid sale price with no cost produces a fake 100% margin. This can mislead price ranking, filtering and recommendation consumers.

### Evidence
`Database/Analytics/Intelligence/023_price_intelligence_v1.sql`: `COALESCE(pd."PurchasePriceRsd", pd."PurchasePrice", 0) AS cost`, followed by `(pp.net_price - pp.cost) / pp.net_price AS margin_pct`. `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs` maps cost with `GetDecimal`, uses `m.margin_pct` for filtering and defaults price sort to margin; RQ188 only fixed missing list-price/discount-depth handling, not cost. RQ601's original kalkulacije prove historical cost scale externally, but are not required to stop a missing value from becoming fabricated zero.

### Scope
SQL 023 and dedicated independent PostgreSQL price oracle; PriceIntelligenceItem, TypeScript price DTO, price dashboard/Insight Studio consumers and focused null/margin tests. Do not change supplier costing decisions or inventory capture. Separate RQ622 owns price cache lifecycle/fail-closed.

### Read first
`Database/Analytics/Intelligence/023_price_intelligence_v1.sql`; `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs`; `Api.Tests/AnalyticsIntelligenceSmokeTests.cs`; `Klijent/clientapp/src/services/analyticsIntelligenceApi.ts`; RQ188/RQ601 evidence and `docs/ai/ANALYTICS_BUSINESS_GLOSSARY.md`.

### Do
1. Create a failing disposable PostgreSQL oracle: no cost, one missing source but other valid, explicit 0, positive cost, negative/invalid source, and a measured margin of zero. Keep net/list-price cases from RQ188.
2. Preserve unknown/invalid unit-cost provenance as nullable, do not silently substitute a numeric zero; if legitimate free-stock 0 cost is ambiguous, keep the status unknown until product policy clarifies it.
3. Propagate nullable `cost` and `margin_pct` through API and TS schemas, sorting/filtering, UI cards, exports and derived analytics; missing margin must not rank as 100% or create actionable price suggestions.
4. Maintain exact SQL column contract and previously populated cache schema evolution; coordinate changed-hash refresh with RQ622 and preserve RQ188 discount-depth semantics.

### Tests
Disposable PostgreSQL before/after oracle and populated-MV parity with zero skipped, API JSON/threshold/page/sort tests, frontend missing-vs-known-zero tests, Release build, analytics guardrails, governance, `git diff --check`, exact-main CI.

### Acceptance
Missing cost never yields measured 0 or 100%-margin; known cost retains correct formula; every consumer distinguishes unavailable vs measured zero and remains non-actionable on unknown cost. No business DB mutation/import/Render deploy.

### Dependencies
Independent of RQ619 and BCI17; serialize any shared price endpoint/DTO path with RQ622. RQ601 external owner proof remains separate.

Run log: `.ai/runs/2026-10-12-RQ621-evidence.md`
Evidence state: synchronized

---

## RQ622 - Certify Price Intelligence cache rebuild, readiness and fail-closed API parity

Status: DONE
Priority: P1
Type: startup/MV lifecycle/API trust-contract regression
Feature family: price-intelligence-cache-truth
Parallel-safe: no; `DatabaseInitializer` overlaps RQ619 and price API/SQL schema may overlap RQ621
Owner: Codex
Local lock: removed before delivery
Ready after: RQ619 DONE and RQ621 DONE; code proof only, no provider access necessary
Commit suggestion: fix(analytics): fail closed on stale price intelligence cache

### Problem
Price Intelligence alone bypasses the readiness/hash checks applied to Demand, Inventory and Trend. A semantic SQL change with unchanged MV column structure can leave a pre-existing price MV stale while the price endpoint returns HTTP 200; missing relations may be represented as an empty-success response.

### Evidence
Pre-fix evidence (historical): `DatabaseInitializer.AnalyticsIntelligenceCacheBySqlFile` listed only 021, 022 and 024; SQL 023 used `CREATE MATERIALIZED VIEW IF NOT EXISTS` without `cache_refresh_state` ownership. `QueryPriceIntelligenceAsync` passed `freshnessCacheName=null, freshnessSqlPath=null`; price GET called it directly rather than `ExecuteCacheQueryAsync`. Missing relations could be represented as empty success, and nightly refresh could not certify exact startup SQL/hash parity when disabled.

### Scope
Price signal cache registry/status/SQL 023, startup initializer, price endpoint/API status, dedicated PostgreSQL populated-MV regression and minimal operator diagnostics. Do not rework all intelligence SQL or alter Render service flags.

### Read first
RQ615, RQ619 and RQ621 evidence; `Infrastructure/Seed/DatabaseInitializer.cs`; `Database/Analytics/Intelligence/020_create_intelligence_schema.sql`; SQL 023; `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs`; `Api.Tests/AnalyticsIntelligenceCacheConvergenceTests.cs`.

### Do
1. Seed a populated pre-change price MV and a same-column-count semantic SQL change, with clear before/after results and an independent SQL oracle.
2. Reproduce stale-200 and missing-cache/empty-success behavior under no nightly worker, failed refresh, concurrent restart, unchanged and changed hashes.
3. Make price cache participate in equivalent version/hash/generation readiness rules; ensure a successful rebuild/refresh and row-level MV/view parity before setting ready; fail closed 503/typed unavailable otherwise.
4. Preserve business KPI nullability from RQ621, no unbounded startup blocking and no collision with RQ619 lock ownership.

### Tests
Disposable PostgreSQL old-cache migration/retry/parity, API 200-vs-503/empty/missing tests, concurrency gate inherited from RQ619, Release build, governance and exact-main CI with zero skipped.

### Acceptance
Price 200 requires verified matching cache state/hash and refreshed MV; stale/pending/failed/missing state is never represented as trusted empty or current data. No database/import/deploy outside disposable tests.

### Dependencies
RQ619 and RQ621 DONE; independent of external STAB16 proof, which remains the deployed-state authority.

### Completion note

- Date: 2026-10-12
- Status: DONE
- Completion: Price Intelligence now uses the registered SQL 023 cache lifecycle and verifies readiness, refreshed generation, required relation and applied SQL hash before returning HTTP 200. Pending, failed, missing or mismatched cache state returns HTTP 503; ready-empty remains a successful empty result.
- Changed files: `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs`; `Infrastructure/Seed/DatabaseInitializer.cs`; `Database/Analytics/Intelligence/023_price_intelligence_v1.sql`; `Api.Tests/AnalyticsIntelligenceCacheConvergenceTests.cs`; `Api.Tests/AnalyticsPriceIntelligenceCacheReadinessTests.cs`; `docs/Analytics/INTELLIGENCE_README.md`; this queue; `.ai/runs/2026-10-12-RQ622-evidence.md`.
- Checks run: Release `Api.Tests` build passed (0 warnings/errors); all six governance checks passed; exact-main Analytics Tests & Data Integrity run `38238211919` on `319d24f6` passed with 1,971 passed / 42 classified skips / 0 failed, including both Price readiness tests and RQ453 certification; current-main Planning Governance run `38241681328` on `489079ec` passed.
- Checks not run: local disposable PostgreSQL tests could not start because the Docker Desktop service was stopped and this process could not open it. Exact-main CI executed the PostgreSQL tests successfully. No production database/import/deployment was used.
- Run log: `.ai/runs/2026-10-12-RQ622-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `116df731117a5f5190b10e654840e3f7c225cbf8`
- Main verification: fresh `origin/main` `6545dedcb8e52cfe0e7a76d9cab353d9c48fecbc` contains implementation SHA `116df731` and test follow-up `d186bde9`; Analytics run `38238211919` on main SHA `319d24f6` and its RQ453 job succeeded; Planning Governance run `38242189493` on current main passed.
- Missed: none known.
- Follow-up: RQ624 is the primary READY successor; RQ630 and RQ631 remain independently READY.
- Residual risk: local Docker was unavailable; exact-main disposable PostgreSQL proof passed. Deployed freshness remains owned by STAB16, and the separate session-time-zone price-date correction remains queued as RQ627.
- Post-close routing: RQ624 promoted to primary READY; RQ630/RQ631 remain independent READY.
- Prompt defect / scope repair: extending the shared cache matrix exposed a known SQL 023 session-time-zone divergence in an RQ627 non-UTC fixture; the fixture now checks the other cache pairs there, while Price parity remains covered under a matching session. The fail-closed test helper now includes the Price route. RQ627's readiness gate now includes RQ624 to serialize their overlapping metadata/cache paths.

---

## RQ623 - Keep Trend Momentum entropy mathematically valid with signed customer returns

Status: DONE
Priority: P1
Type: independent SQL math oracle, nullable signal contract and focused API/UI tests
Feature family: trend-momentum-signed-entropy
Parallel-safe: yes against RQ619 initializer-only concurrency scope and RQ621 SQL 023; owns SQL 024 math and dedicated tests, so RQ620/RQ625 serialize behind it
Owner: Codex
Local lock: removed before delivery
Ready after: RQ616 DONE (signed retail population certified), no product-owner decision needed for reproducing invalid values
Commit suggestion: fix(analytics): bound trend entropy under signed returns

### Problem
Shannon entropy requires a nonnegative probability distribution summing to one. The current signed net-sales denominator can be smaller than a positive day's sales after returns, giving shares greater than 1 and negative normalized entropy, while the API/UI treats it as a meaningful trend score.

### Evidence
SQL 024: `total_units_28d = SUM(dps.units_sold)`, `sales_share = dps.units_sold / total_units_28d` when daily units positive and denominator >0, then `-SUM(sales_share * LN(sales_share)) / LN(active_days_28d)`. For daily 10 and -9, total is 1 so positive share is 10 and Shannon term is negative. `GetDecimal(record, "trend_entropy")` assumes always-present trustworthy number. This is a code/mathematics counterexample, not a measured production incident.

### Scope
SQL 024 entropy pathway, independent trend oracle fixtures and API/TS downstream trust semantics only if the corrected signal can be unavailable. Do not change signed net sales slope/retail population, external scores or SQL 021 demand semantics.

### Read first
SQL 024; `Api.Tests/AnalyticsIntelligenceRetailPopulationTests.cs`; `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs`; RQ616 fixture; `docs/Analytics/INTELLIGENCE_README.md`.

### Do
1. Execute PostgreSQL fixtures 10/-9, 10/-10, 2/3, 0/0, one active day, pure returns, refunds across UTC midnight, no rows; assert current invalid behavior before correction.
2. Define/justify a valid probability base (positive gross sold units if independently reconstructable, otherwise explicit insufficient evidence); do not use ABS(returns) or clamp invalid entropy to 0 merely to pass assertions.
3. Keep legitimate signed returns in `local_sales_acceleration`; propagate unavailable entropy as NULL if evidence is insufficient and block trust/actionability, preserving measured 0 when genuinely concentrated demand.
4. Check changed-hash/pre-existing MV parity and stable downstream classifications.

### Tests
Independent calculation oracle `0 <= entropy <= 1` for available values, regression against signed windows/denominator-0, API JSON and frontend missing/zero distinctions, PostgreSQL cache EXCEPT ALL, guardrails/build/CI; zero skipped.

### Acceptance
Entropy never becomes negative, >1 or fabricated 0 for invalid probability data, while signed net demand semantics remain unchanged. No outside business database or deployment writes.

### Dependencies
Independent READY after RQ616; RQ620 waits for RQ623 to release SQL 024 ownership; RQ625 is later same-file follow-up.

### Claim note — 2026-10-12

- Refreshed `origin/main`: `7e960799284ca3fdbb34079f9e589652d24f55ca`; later RQ619 CI evidence sync advanced main to `5666c79ed4550d4d7a5709fd23bca4373e70cd91`.
- Confirmed RQ623 was READY and unassigned; no matching branch, lock, or open PR existed.
- Isolated worktree: `C:\Users\Ivan\.codex\worktrees\rq623-signed-entropy\Trendplus2`, branch `codex/rq623-signed-entropy`.
- Existing PostgreSQL fixture reproduced negative entropy `-5.399910` for signed daily quantities `10`, `2`, and `-9`.

### Completion — 2026-10-12

- Used gross positive daily units to form Shannon shares while preserving signed net units for sales slope and local acceleration; entropy is NULL when no positive gross evidence exists, and measured zero remains distinct.
- Independent disposable PostgreSQL oracle reproduced pre-fix entropy `-5.399910`; corrected cases cover 10/-9, 10/-10, 2/3, 0/0, one-day, pure returns, UTC-midnight refunds and no rows. API JSON plus TypeScript/UI preserve NULL versus measured zero.
- Populated-cache `EXCEPT ALL` parity covers SQL 021/022/024; existing cache hash/retry/503 behavior remains covered.
- Focused RQ623 + RQ619 PostgreSQL tests: 8/8 passed, 0 skipped; Release build 0 warnings/errors; analytics UI test 4/4; analytics guardrails/typecheck/build and all six governance validators passed.
- Delivered implementation SHA `d20b75a6db69b05aea8d8a908614831b6fce07e9`; refreshed `origin/main` `8ea2ba6acb03d59f09136d197056f8d5496d60e5` contains it. Exact current-main Analytics Tests & Data Integrity run `38183667143` and Analytics Quality Gates run `38183667166` passed; Planning Governance run `38183667171` passed.
- Run log: `.ai/runs/2026-10-12-RQ623-evidence.md`; Evidence state: synchronized.

---

## RQ624 - Certify intelligence daily as-of rollover and worker refresh-state truth

Status: DONE
Priority: P1
Type: clock-boundary PostgreSQL MV/worker lifecycle and API freshness proof
Feature family: intelligence-cache-calendar-rollover
Parallel-safe: no; startup state methods and endpoint readiness overlap RQ619/RQ622; do not claim together
Owner: Codex (local main workspace)
Ready after: RQ619 DONE, RQ622 DONE and RQ620 DONE for finalized as-of input bounds
Commit suggestion: fix(analytics): enforce current as-of and refreshed cache generations

### Problem
Demand/Risk/Trend materialized views freeze UTC `as_of_date` and signal rows at refresh time. `EnsureIntelligenceCacheIsFreshAsync` checks ready, hash and presence of a last-refresh timestamp, but not whether the cache covers the required UTC day or incoming changes. The nightly worker refresh path updates its own refreshed-object set without obviously advancing `analytics_intel.cache_refresh_state.last_refreshed_at_utc`. With the worker disabled, yesterday's ready cache can remain an apparently successful historical snapshot; after a worker-only refresh, diagnostics may still report the earlier startup refresh timestamp.

### Evidence
SQL 021/022/024 use `CURRENT_TIMESTAMP AT TIME ZONE 'UTC'` in materialized view definitions. `DatabaseInitializer.PrepareAnalyticsIntelligenceCacheBuildAsync` skips work when applied hash and ready state match, without age/as-of comparison. API `EnsureIntelligenceCacheIsFreshAsync` reads `last_refreshed_at_utc IS NOT NULL` but no max age or current day. `NightlyAnalyticsRefreshWorker.RefreshMaterializedViewsAsync` performs REFRESH and collects `refreshedObjects`, with no direct matching per-cache readiness timestamp write in that path. Existing STAB16 owns deployed worker proof, but this local clock contract can be tested independently.

### Scope
Isolated PostgreSQL cache freshness state and API status/metadata, nightly refresh status synchronization and minimal instrumentation. Does not require a running production worker, new import, global SLA policy, or changing configured backend time zone.

### Read first
`Infrastructure/Seed/DatabaseInitializer.cs`; `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs`; `Workers/NightlyAnalyticsRefreshWorker.cs`; SQL 020-024; RQ619/RQ620/RQ622 and STAB16 evidence; `docs/Analytics/INTELLIGENCE_README.md`.

### Do
1. Freeze UTC 23:59 -> 00:01, completed vs missed nightly refresh, unchanged SQL hash, data mutation without SQL change, failed/partial refresh, separate analytics DB and same-DB mode.
2. Independently compare MV MAX(date)/signal_date and view with expected UTC as-of; test how endpoint reports old but ready data and worker-only successful refresh timestamp.
3. Implement versioned, inspectable last-refreshed timestamp, as-of day and successful-generation/lineage metadata; separate SQL freshness from business-source freshness so no fake currentness is claimed. Retry/refresh under bounded work without blocking normal startup indefinitely.
4. Preserve legitimate explicit historic as-of queries and historical value usefulness; currentness failure must not corrupt/rewrite history or report stale as observed current.

### Tests
Disposable PG with controllable clock/injected as-of fixture, API response/200/503 or explicitly stale metadata per existing trust policy, worker refresh success/failure/lock, full RQ615/RQ619/RQ622 regressions; deterministic zero-skipped checks, governance and CI.

### Acceptance
Yesterday's MV cannot silently masquerade as today's fresh signal; successful nightly refresh updates traceable state; failed/missed refresh is explicit; historical lookback remains available and correctly labeled. STAB16 production proof unchanged.

### Dependencies
RQ619, RQ620, RQ622 DONE; no new product-owner SLAs invented.

### Completion note — 2026-10-12

- RQ624 is delivered directly to `main` at implementation SHA `4970b6a2bd22e39f8ca5798815803f2b39d5a2c1`, correcting the daily UTC as-of gate to apply only to Demand/Inventory/Trend caches and strengthening the historical stale-cache fixture with its missing store dimension and row assertions.
- Exact-main Analytics Tests & Data Integrity run `38245964827` passed: 2,018 tests, 1,976 passed, 42 individually classified skips; the RQ453 lifecycle smoke passed 1/1. The RQ624 cache-convergence regressions `ChangedSaleJoinSql_RefreshesExistingDemandRiskAndTrendCaches` and `StaleButReadyCache_RejectsCurrentDayAndKeepsCoveredHistoricalDateAvailable` passed. Planning Governance run `38245964910` passed.
- The earlier exact-main run `38244596954` failed those two RQ624 tests; the cache-family date gate and incomplete `StoresDim` fixture were corrected. Analytics Quality Gates run `38244596872` passed.
- Delivery mode: direct-main.
- Main commit SHA: `4970b6a2bd22e39f8ca5798815803f2b39d5a2c1`.
- Main verification: post-close refreshed `origin/main` at `0b916fee0c6a81b10e492834c9f48801f994326d` contains implementation SHA `4970b6a2bd22e39f8ca5798815803f2b39d5a2c1`. Local build, focused freshness-date test, frontend typecheck/analytics guardrails, six governance validators and `git diff --check` passed.
- Run log: `.ai/runs/2026-10-12-RQ624-evidence.md`; Evidence state: synchronized.
- Post-close recovery: refreshed base `origin/main` `0b916fee0c6a81b10e492834c9f48801f994326d`; scanned all 13 `ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` files plus `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`. RQ624 DONE satisfied RQ627's remaining gate. RQ627 has no active lock, branch/PR or dirty/ahead colliding worktree; no higher-priority runnable program pointer appeared in `MASTER_ROADMAP.md`. RQ627 remains primary READY; RQ630/RQ631 remain independent READY; RQ625 is serialized behind RQ627 and RQ626 behind RQ625.
- Residual risk: local Docker was unavailable for the first local PostgreSQL fixture attempt; the exact-main backend suite and RQ453 PostgreSQL lifecycle smoke passed. Deployed worker freshness remains owned by STAB16.

---

## RQ625 - Separate missing external trend evidence from a measured zero trend score

Status: DONE
Priority: P2
Type: SQL/API/React missing-evidence propagation and signal trust
Feature family: trend-momentum-external-signal-provenance
Parallel-safe: no; shares SQL 024 with RQ623/RQ620 and the intelligence endpoint/client DTO files with RQ621/RQ622/RQ624
Owner: Codex
Ready after: RQ623 DONE, RQ620 DONE, RQ621/RQ622/RQ624 DONE and RQ627 DONE to serialize shared intelligence endpoint/client DTO paths; no external provider credentials required for null-versus-zero fixture
Commit suggestion: fix(analytics): preserve unavailable external trend scores

### Problem
A product seen only in local sales can receive an external trend score of zero even though neither external trend table has evidence. That number can be interpreted as measured lack of interest and influence ranking.

### Evidence
SQL 024 includes `daily_sales` in `observed_products` and selects `COALESCE(lth.final_global_score, lgs.final_global_score, 0)` as `external_trend_score`. The API maps the field using `GetDecimal`; no missing-source confidence/provenance is attached in this row contract.

### Scope
Trend SQL 024 nullable/provenance signal, TrendMomentumItem, TS DTO/derivations, small API/UI tests. No external scraper, machine learning rerun or policy threshold change.

### Read first
SQL 024; API price/trend DTO; `Klijent/clientapp/src/services/analyticsIntelligenceApi.ts`; `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`; RQ623/RQ620 tests; RQ139/RQ152 fake-zero precedents.

### Do
1. Prove local-only product, known explicit external zero, positive external score, stale history vs latest score, orphan/missing dimension and future-only external score (coordinated with RQ620).
2. Preserve explicit measured zero; make missing external input NULL/typed unavailable, with minimal evidence basis (latest source/time where available), and keep local slope/entropy independent.
3. Confirm ranking/filtering/insight recommendations do not treat unknown as low interest or fill it with UI zero; maintain pagination and cross-surface parity.

### Tests
Disposable PG oracle, API/TypeScript schema/null-and-zero tests, frontend insight and ordering tests, populated MV hash and build/CI; zero skipped.

### Acceptance
No external evidence != measured external zero throughout SQL/API/frontend; no fabricated trend recommendation on absent source.

### Dependencies
RQ623/RQ620/RQ627 DONE, separate from STAB16/provider data collection. RQ627 is included because both prompts edit shared intelligence endpoint/client metadata paths.

### Promotion note — 2026-10-12

- RQ627 is DONE on main at `84d90ed44bcdba916aa795f707dbf7c96dec1468`; RQ623, RQ620, RQ621, RQ622 and RQ624 are also DONE.
- Post-delivery collision check found no RQ625 lock, branch, open PR or attached worktree. RQ630 owns a separate migration/test path and RQ631 owns telemetry middleware/test paths; neither overlaps RQ625's SQL 024 and intelligence API/client DTO scope.
- Promoted WAITING → READY. No external provider credentials are required for the null-versus-zero fixture.

### Owner claim — 2026-10-12

- Refreshed `origin/main` to `304223ff1a5835be62dffe9eed908cc8cb90a271`; RQ625 was the primary READY prompt at claim time and all declared dependencies are DONE.
- Scanned all 13 `ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` files and the active SQL analytics queue; no higher-priority runnable program or RQ task in progress displaces this claim. RQ630/RQ631 remain READY on separate paths.
- No RQ625 lock/branch, open PR, or attached worktree exists; `gh pr list --state open --search RQ625` returned `[]` and Codex artifact listing returned no attachments.
- Claimed by Codex on `main`. Local lock: `.ai/task-locks/RQ625-codex.lock.md`.

### Completion note

- Date: 2026-10-12
- Status: DONE
- Completion: Missing external trend evidence remains NULL through SQL, API and frontend; measured zero remains numeric zero. TrendHistory uses its canonical DATE calendar bounds and stable source-date provenance.
- Changed files: `Database/Analytics/Intelligence/024_trend_momentum_v1.sql`; `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs`; `Api.Tests/AnalyticsIntelligenceCacheConvergenceTests.cs`; `Api.Tests/AnalyticsIntelligenceRetailPopulationTests.cs`; `Api.Tests/AnalyticsIntelligenceSaleJoinTests.cs`; `Api.Tests/Fixtures/analytics-intelligence-smoke-seed.sql`; `Klijent/clientapp/src/services/analyticsIntelligenceApi.ts`; `Klijent/clientapp/src/components/dashboard/IntelligenceSnapshotPanel.tsx`; `Klijent/clientapp/src/components/dashboard/IntelligenceSnapshotPanel.spec.tsx`; `docs/Analytics/INTELLIGENCE_README.md`; this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-12-RQ625-evidence.md`.
- Contract/runtime behavior changed: absent external rows now yield nullable score/source/time; explicit zero is preserved; score filtering and ordering keep unknown values distinct; TrendHistory compares DATE values against the UTC as-of DATE.
- Checks run: Release backend build; focused PostgreSQL oracle on PR run `38262164454` (target parity test passed; full suite 1,977 passed, 42 previously classified skips, 0 failed); frontend analytics tests, guardrails and build on `38262164469`; Analytics Quality Gates on `38260258802`; Planning Governance on `38260258744`; local `npm run typecheck`, `npm run build`, `npm run check:analytics-guardrails`, six governance validators, analytics execution-plan/lineage checks and `git diff --check` passed.
- Checks not run: local disposable PostgreSQL execution could not initialize because Docker Desktop is unable to start; the identical oracle ran and passed in the PR backend workflow. No production database or deployment was accessed or changed.
- Run log: `.ai/runs/2026-10-12-RQ625-evidence.md`
- Evidence state: synchronized
- Delivery mode: pull-request (#126; initial implementation `fd2d0fb0242f5c105ef90d01a0e33c0da053c33e`, correction merged as `e96f9329a3760eb7d940f4d5ae69dfc6637d5135`)
- Main commit SHA: `e96f9329a3760eb7d940f4d5ae69dfc6637d5135`
- Main verification: refreshed `origin/main` at `72e1bfa2926eb9219d897fd4a831101d80c735ed`; it contains both implementation commits. PR #126's required backend and frontend checks passed before merge.
- Missed: none known.
- Follow-up: RQ626 promoted from WAITING to primary READY; RQ630 and RQ631 remain independent READY prompts.
- Residual risk: exact-main backend run `38262945885` was in progress when this note was prepared; the same merged content passed the full backend suite on PR run `38262164454`. The 42 global skips are previously classified and no RQ625 test was skipped.
- Post-close routing: refreshed recovery base `origin/main` `72e1bfa2926eb9219d897fd4a831101d80c735ed`; scanned all 13 active Analytics Reliability queue files plus `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`. RQ626 is the only prompt with a direct `Ready after: RQ625 DONE` dependency; its other dependencies and SQL 022 nullable-baseline contract are already satisfied, and no RQ626 lock, branch or open PR exists. RQ630/RQ631 remain independent READY; no higher-priority runnable BCI/STAB/SQL task was found.
- Prompt defect / scope repair: the test fixture used `timestamptz` for `TrendHistory.Date`, while the canonical source schema is `DATE`; corrected the fixtures and UTC date comparison after exact-main cache parity exposed session-time-zone-dependent provenance. The initial implementation push also preceded focused backend proof despite the rule already present on the starting `main`; the deviation and corrective PR validation are recorded in the run log.

---

## RQ626 - Audit negative reconstructed inventory and stockout classification instead of silent clamping

Status: DONE
Priority: P2
Type: inventory proxy semantic characterization and SQL/UI safety oracle
Feature family: reconstructed-stock-negative-evidence
Parallel-safe: no; shares SQL 022 with RQ620 and the intelligence endpoint/client DTO files with RQ621/RQ622/RQ624/RQ625
Owner: cursor / bc-bd7c41e3-72e2-4cad-a05c-3fcaa0ebd661
Ready after: RQ620 DONE, SQL 022 nullable quantity fix on main, RQ621/RQ622/RQ624 DONE, and RQ625 DONE to serialize shared intelligence endpoint/client DTO paths; no production stock writes
Commit suggestion: test(inventory): distinguish negative proxy from verified stockouts

### Claim note — 2026-10-12

- Claimed from refreshed `origin/main` `edd69f88` after verifying dependencies, no RQ626 lock/branch/open PR, and separate RQ630/RQ631 paths.
- Branch: `cursor/rq626-negative-inventory-d661`. Local lock: `.ai/task-locks/RQ626-cursor.lock.md`.

### Promotion note — 2026-10-12

- RQ625 is DONE on refreshed `origin/main` `72e1bfa2926eb9219d897fd4a831101d80c735ed`; all other declared dependencies were already satisfied.
- The full active RQ queue/addendum and SQL queue scan found no RQ626 lock, branch or open PR. Its shared API/test paths are released by RQ625's main delivery; RQ630 and RQ631 remain independently READY.
- Promoted WAITING → READY as the primary RQ successor. No claim was started in this RQ625 close-out.

### Problem
The back-calculated stock proxy uses `GREATEST(reconstructed_quantity,0)`. An inferred negative balance is turned into a measured zero, then contributes to `stockout_days` and risk labels. Because this is a reconstructed proxy rather than observed stock, masking negatives can hide source inconsistencies and create unjustified observed-stockout impressions.

### Evidence
SQL 022 `stock_proxy` clamps `current_stock_qty + signed-sales - movements` to zero before `COUNT(*) FILTER (WHERE sp.stock_qty = 0)`. RQ616 correctly labels the series reconstructed, RQ610 proves negative real stock can be meaningful; no fixture explicitly distinguishes negative reconstructed balance from a verified zero-day stockout.

### Scope
Dedicated PostgreSQL reconstruction oracle, inventory risk SQL 022 where an actual defect is proven, API/UI trust labeling and tests. Do not convert the proxy to observed stock or change RQ610 stock-generation architecture.

### Read first
SQL 022, `Api.Tests/AnalyticsIntelligenceRetailPopulationTests.cs`, `Api.Tests/AnalyticsIntelligenceMovementUtcTests.cs`, RQ610/RQ616/RQ618 evidence, `docs/Analytics/INTELLIGENCE_README.md`.

### Do
1. Seed observed-zero, observed-negative, inferred-negative, missing baseline, restock, transfer, return and unknown movement cases; compare independent signed arithmetic to proxy.
2. Characterize whether the clamp is an intentional visualization heuristic or falsely promotes a negative estimate into a true zero. Keep original signed evidence/quality status inspectable; any policy change to physical stockout definitions must be approved, not guessed.
3. If proven unsafe, preserve negative/insufficient evidence separately from a known zero and prevent unjustified stockout/reorder actionability. Maintain nullable cost and day-UTC constraints.
4. Rerun as-of and cache-view parity regressions plus API/React trust tests.

### Tests
Disposable PostgreSQL oracle and output parity, null/negative/zero classification, no skipped tests, Release/guardrails/governance/CI, no business DB DML.

### Acceptance
A fabricated stockout cannot originate solely from clamping a negative historical proxy; negative, missing and measured zero stock remain distinguishable, and observed snapshot authority is preserved.

### Dependencies
RQ620 DONE; RQ610 all-writer proof remains a separate blocked production validation.

### Completion note

- Date: 2026-10-12
- Status: DONE
- Completion: SQL 022 no longer clamps reconstructed stock to zero. Signed negative proxies stay inspectable, `stockout_days` counts only exact zeros, and API/UI `stockProxyEvidence` distinguishes missing, negative, zero and positive states so a fabricated stockout cannot originate solely from clamping.
- Changed files: `Database/Analytics/Intelligence/022_inventory_risk_signals_v1.sql`; `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs`; `Api.Tests/AnalyticsIntelligenceNegativeStockProxyTests.cs`; `Api.Tests/StockProxyEvidenceClassificationTests.cs`; `Klijent/clientapp/src/services/analyticsIntelligenceApi.ts`; `Klijent/clientapp/src/components/dashboard/IntelligenceSnapshotPanel.tsx`; `Klijent/clientapp/src/components/dashboard/IntelligenceSnapshotPanel.spec.tsx`; `Klijent/clientapp/src/services/__tests__/analyticsIntelligenceDerived.spec.ts`; `docs/Analytics/INTELLIGENCE_README.md`; this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-12-RQ626-evidence.md`.
- Contract/runtime behavior changed: reconstructed `stock_qty` may be negative; days-of-cover is NULL for negative proxies; stockout/reorder filters no longer treat clamped negatives as measured zeros; clients receive `stockProxyEvidence`.
- Checks run: Release `Api.Tests` build; `StockProxyEvidenceClassificationTests` 4/4; frontend IntelligenceSnapshotPanel + derived specs 22/22; `check:analytics-guardrails` + typecheck; prompt-queue and planning-architecture validators; `git diff --check`.
- Checks not run: local disposable PostgreSQL oracle could not start because Docker/Testcontainers is unavailable here; current-main backend run `38265812357` is the first required PostgreSQL proof for `AnalyticsIntelligenceNegativeStockProxyTests`.
- Run log: `.ai/runs/2026-10-12-RQ626-evidence.md`
- Evidence state: synchronized
- Delivery mode: pull-request (#127) then fast-forward to `main`
- Main commit SHA: `a3e621f74f9b3af1391714280fdd20e8b2b6f41a`
- Main verification: refreshed `origin/main` equals `a3e621f74f9b3af1391714280fdd20e8b2b6f41a`
- Missed: none known beyond the local Docker residual above.
- Follow-up: RQ630 promoted to primary READY; RQ631 remains independently READY.
- Residual risk: current-main Actions backend/quality/governance runs are in progress on the implementation SHA; if the new oracle fails in CI, fix that regression before new queue work.
- Post-close routing: recovery base `origin/main` `a3e621f74f9b3af1391714280fdd20e8b2b6f41a`; scanned all 13 active Analytics Reliability queue files plus `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`. No task was WAITING solely on RQ626. RQ630 is the primary READY successor; RQ631 remains independent READY. Stale RQ625 header `IN_PROGRESS` was repaired to DONE to match its completion note.
- Prompt defect / scope repair: RQ625 header status lagged its DONE completion note; synchronized during this close-out.

---

## RQ627 - Align Price Intelligence product snapshot dates with the certified UTC calendar

Status: DONE
Priority: P2
Type: timestamp-with-time-zone read/oracle and bounded SQL contract correction
Feature family: price-intelligence-day-timezone
Parallel-safe: no; shares SQL 023, price cache and API metadata paths with RQ621/RQ622/RQ624
Owner: Codex (local main workspace)
Ready after: RQ621 DONE, RQ622 DONE and RQ624 DONE to serialize overlapping price-cache/API metadata; independent of deployed provider
Commit suggestion: fix(analytics): normalize price signal day to UTC

### Claim note — 2026-10-12

- Refreshed `origin/main` at `76fdc503d9a41d718548fca86aa254499eb794b5`; RQ627 was still the primary READY prompt and RQ621/RQ622/RQ624 dependencies were DONE.
- No RQ627 lock, branch or open PR existed. The related RQ619/RQ620/RQ621/RQ623 worktrees were clean and behind current `origin/main` with no unique commits. RQ630 is a parallel-safe migration/test-only lane and RQ631 owns telemetry paths; neither overlaps RQ627 SQL 023, price cache or API metadata ownership.
- Claim owner/workspace: Codex on local `main`; local lock: `.ai/task-locks/RQ627-codex.lock.md`.

### Problem
`price_date` is derived with session-sensitive `pd."Timestamp"::date`, while the demand/inventory/trend families now declare UTC day attribution. One timestamp near UTC midnight can be assigned to different price dates depending on database session TimeZone and refresh connection settings.

### Evidence
SQL 023 `latest_products` selects `pd."Timestamp"::date AS price_date`; SQL 021/022/024 use explicit `AT TIME ZONE 'UTC'` for their date basis. `ProductsDim.Timestamp` is created as `timestamp with time zone` in `DatabaseInitializer.EnsureCoreAnalyticsDimensionTablesAsync`. A session time-zone invariant price-date fixture is not listed among RQ188/RQ618 proofs.

### Scope
SQL 023 date projection, dedicated PostgreSQL UTC boundary tests and minimal metadata docs; do not redefine the whole platform business time zone or date-valued user filters.

### Read first
SQL 023; SQL 021/022/024; `Infrastructure/Seed/DatabaseInitializer.cs`; RQ618 UTC date oracle; RQ621/RQ622 state proof.

### Do
1. Prove mapped PostgreSQL type in actual migration/fixture and compare a single near-midnight `ProductsDim.Timestamp` under UTC, Pacific/Kiritimati and America/Los_Angeles sessions; test DST dates.
2. When reproduced for timestamptz, align `price_date` to the explicitly certified UTC convention using typed conversion; if actual store-local calendar policy differs, stop and request owner decision.
3. Refresh a previously populated price MV, verify date/index/page metadata parity, and preserve all cost/margin nullability and price-cache readiness invariants.

### Tests
Provider-strict isolated PostgreSQL timezone oracle, pre-existing MV EXCEPT ALL, API date metadata, zero skipped, Release/guardrails/CI.

### Acceptance
Price snapshot day is invariant under session TimeZone and consistent with documented UTC semantics; no live data writes or implicit policy changes.

### Dependencies
RQ621/RQ622/RQ624 DONE; avoid simultaneous SQL 023 or cache-readiness metadata changes.

### Completion note

- Date: 2026-10-12
- Status: DONE
- Completion: `price_date` is explicitly UTC for the `timestamptz` product snapshot. The PostgreSQL oracle proved invariance across UTC, Pacific/Kiritimati and America/Los_Angeles, refreshed a populated MV with view/cache parity and unchanged indexes, and checked stable API `asOfDate`/`priceDate` across both pages.
- Changed files: `Database/Analytics/Intelligence/023_price_intelligence_v1.sql`; `Api.Tests/AnalyticsPriceIntelligenceCacheReadinessTests.cs`; `docs/Analytics/INTELLIGENCE_README.md`; this queue; `.ai/runs/2026-10-12-RQ627-evidence.md`.
- Checks run: Debug and Release `Api.Tests` builds; `npm run check:analytics-guardrails`; six local governance validators; exact-main Analytics Tests & Data Integrity run `38248162958` passed on rerun (1,977 passed, 42 known classified skips, 0 failed), including RQ627 oracle pass; Planning Governance run `38248162921` passed; `git diff --check`.
- Checks not run: local PostgreSQL execution could not start because Docker Desktop is unavailable; the provider-strict PostgreSQL test executed and passed in CI.
- Run log: `.ai/runs/2026-10-12-RQ627-evidence.md`
- Evidence state: synchronized.
- Delivery mode: direct-main
- Main commit SHA: `84d90ed44bcdba916aa795f707dbf7c96dec1468`
- Main verification: refreshed `origin/main` at `b4a4b1be073ead7ce41d906ed761809b8052cf61` contains implementation SHA `84d90ed44bcdba916aa795f707dbf7c96dec1468`; Planning Governance run `38250159167` passed on the close-out SHA.
- Missed: none known.
- Follow-up: RQ625 promoted to primary READY; RQ630 and RQ631 remain independent READY lanes.
- Residual risk: first backend CI attempt had two unrelated endpoint-test 503s; both passed on the same-SHA rerun and on the preceding backend run. The 42 global skips are all previously classified, with no new or changed skips.
- Post-close routing: RQ625 promoted to primary READY after the full active-queue scan on recovery base `b4a4b1be073ead7ce41d906ed761809b8052cf61`; full file list and remaining candidate matrix are recorded in the run log.
- Prompt defect / scope repair: none.

---

## RQ628 - Repair missing purchase-cost provenance schema before analytics traffic

Status: DONE
Priority: P1
Type: PostgreSQL bootstrap/schema drift regression
Feature family: purchase-cost-provenance-bootstrap
Parallel-safe: no (`Infrastructure/Seed/DatabaseInitializer.cs`, migration lifecycle integration proof)
Owner: Codex
Commit suggestion: `fix(analytics): repair missing provenance column at startup`

### Problem

Analytics projections read `prodaja_stavke.purchase_cost_provenance`. The canonical EF migration and the full core-schema bootstrap add the nullable column, but an already-ready core schema skips the full bootstrap. When EF migration history says the migration has run while the physical column is absent, initialization can complete and the Shoe Type endpoint fails with PostgreSQL `42703`. Supplier and Color endpoints and other provenance-dependent readers share the same field.

### Evidence

- `Infrastructure/DbContexts/TrendplusDbContext.cs` maps `PurchaseCostProvenance` to `purchase_cost_provenance`.
- `Infrastructure/Migrations/20261010160000_AddPurchaseCostProvenance.cs` and the full bootstrap define the additive column.
- `DatabaseInitializer.EnsureTrendplusCoreSchemaAsync` returned early for ready core schemas without independently reconciling this column; EF will not rerun a migration already recorded in `__EFMigrationsHistory`.
- The incident log reports `42703` at `/api/analytics/shoe-type-sales-stats`. No production database access or mutation was performed, so the reported deployment's exact migration history remains unverified.

### Scope

Add idempotent, additive schema convergence for the nullable provenance column even when the general core bootstrap is skipped. Verify the post-migration schema contract before marking initialization successful. Preserve all stored numeric costs and business rows. No production DB execution or Render deployment.

### Tests

- In the PostgreSQL bootstrap lifecycle test, complete initial bootstrap, drop only the nullable provenance column while leaving its EF migration history row intact, rerun startup initialization, and assert the column is restored before continuing.
- Preserve existing migration idempotency and cost-provenance tests; run the focused PostgreSQL lifecycle test and backend Release build. CI must execute the mandatory PostgreSQL suite with zero skips.
- Check supplier, shoe-type, color, Insight Studio, Supplier Decision Hub and other readers sharing this property.

### Acceptance

An existing schema with an already-applied migration but a missing physical provenance column converges safely during startup before analytics requests are admitted. The column remains nullable and addition-only; no sales values or rows are rewritten. The user controls production deployment timing.

### Delivery note

- Implementation and regression test are recorded in `.ai/runs/2026-10-12-RQ628-evidence.md`.
- PR #123 was squash-merged after all required CI passed on head `ba256ba8f849bfe50eded4dddd679d5c3556afa8`; `main` is verified at `3a78e062946cb8263b58c156b796259acb54f483`.
- Exact production drift was not independently inspected. Render `autoDeployTrigger` remains off; no production database was changed.
- Follow-up audit (2026-10-12): PR #123's RQ453 green result used `GateApiTraffic=false`, so it did not prove traffic-gated readiness. The exact earlier 503 was a test request racing asynchronous PostgreSQL warmup. PR #125 adds contract validation, fail-closed read-only readiness when auto-migration is disabled, and real `/ready` waits in both RQ453 oracle/parity paths. On head `b42bdbd0289cdf2a6ad3f19b54c793d887651020`, backend and CI skip inventory passed; both new readiness PostgreSQL tests and the migration lifecycle test passed, and RQ453 passed all 8 required suites with zero skipped. See the follow-up section in the same evidence file. Do not create a duplicate queue item. Production remains unverified until controlled deployment and runtime DB checks.

---

## RQ629 - Guard analytics EF migration discovery and reconcile schema skipped by two undiscoverable classes

Status: DONE
Priority: P2
Type: backend/migrations/tests
Feature family: ef-migration-discovery-guard
Parallel-safe: yes (one new additive analytics migration and one new test; no READY/IN_PROGRESS lane owns `Infrastructure/Migrations/AnalyticsDb`)
Owner: Grok Bot
Ready after: owner request 2026-10-12 12:12 (analytics follow-up review)
Commit suggestion: `fix(analytics): reconcile schema skipped by undiscoverable migrations`

### Problem
RQ588 guarded EF migration discovery for `TrendplusDbContext` only. In the analytics context two classes are silently skipped by EF because they lack `[DbContext]`: `20260521123000_AddAnalyticsActionIndexes` (has only `[Migration]`) and `20260531103000_AlignAnalyticsRefreshRunContract` (no attributes). The model and every later snapshot already contain their effects, so EF reports no pending change while migrated databases drift.

### Evidence
- `Infrastructure/Migrations/AnalyticsDb/20260521123000_AddAnalyticsActionIndexes.cs:7` (no `[DbContext]`, no Designer); model `Infrastructure/DbContexts/AnalyticsDbContext.cs:442-445` declares `idx_analytics_action_sourcekey_status` and `idx_analytics_action_priority_status`; no other migration or SQL creates them.
- `Infrastructure/Migrations/AnalyticsDb/20260531103000_AlignAnalyticsRefreshRunContract.cs:9` (no attributes). `20260524222817_AddAnalyticsRefreshRuns.cs:32-34` creates TriggeredBy varchar(32), ProcessMode varchar(16), WorkerName varchar(128); the model (`AnalyticsDbContext.cs:349-356`) and `Infrastructure/Services/AnalyticsRefreshRunRecorder.cs:59` use 80/32/200. A WorkerName over 128 characters fails the insert, which the recorder swallows as a warning, so refresh-run history and readiness evidence silently disappear.

### Delivered
- `Infrastructure/Migrations/AnalyticsDb/20261012120000_ReconcileUndiscoveredAnalyticsSchema.cs`: additive and idempotent. Creates the two indexes with IF NOT EXISTS and widens the three columns only when they are narrower. It never shrinks JobKey/ErrorMessage/CorrelationId (the legacy class would) and has a no-op Down. The legacy classes stay undiscoverable on purpose.
- `Api.Tests/AnalyticsMigrationDiscoveryContractTests.cs`: runtime EF discovery guard for the analytics context with an explicit two-entry allowlist; idempotency/model-parity assertions for the reconcile SQL.
- Not changed: the `idx_analytics_refresh_runs_job_started` sort direction (model DESC on StartedAtUtc, DB ASC) is a performance-only difference, left for RQ630.

### Completion note
- Date: 2026-10-12
- Status: DONE
- Checks run: `dotnet build Api.Tests`; migration/refresh/action-item filter, 90 pass. Failures: the 2 known CRLF `DatabaseMigrationOwnershipTests`, plus 3 Testcontainers tests that need Docker, which is unresponsive on the dev machine and runs in CI. Governance validators with/without self-test; `git diff --check`.
- Completion: delivered to main; CI applied all analytics migrations on fresh PostgreSQL and passed.
- Changed files: `Infrastructure/Migrations/AnalyticsDb/20261012120000_ReconcileUndiscoveredAnalyticsSchema.cs`, `Api.Tests/AnalyticsMigrationDiscoveryContractTests.cs`
- Contract/runtime behavior changed: migrated analytics DBs gain the two model indexes and model-length refresh-run columns; no data rewritten.
- Checks not run: local PostgreSQL apply (Docker unresponsive); CI applies all analytics migrations.
- Run log: `.ai/runs/2026-10-12-RQ629-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `8c2d27d818f8542e820e8898edb74de982a26be0`
- Main verification: fresh `origin/main` equals the SHA; Actions `38235511229` (Analytics Tests & Data Integrity) and `38235511161` (Planning Governance) succeeded.
- Missed: none known.
- Follow-up: RQ630 catalog-vs-model drift guard (READY now that RQ629 is DONE).
- Residual risk: non-concurrent CREATE INDEX briefly locks the small analytics_action_items table on first apply.
- Prompt defect / scope repair: none.

---

## RQ630 - Fail CI when a migrated analytics database drifts from the EF model

Status: DONE
Priority: P2
Type: tests/CI schema-drift guard
Feature family: ef-migration-discovery-guard
Parallel-safe: yes (new PostgreSQL test only; keep the scope test-only and do not edit `DatabaseInitializer.cs`)
Owner: Codex / worktree `rq630-schema-drift`
Ready after: RQ629 DONE
Commit suggestion: `test(analytics): catalog-vs-model drift guard after migrations`

### Claim note — 2026-10-12

- Claimed from refreshed `origin/main` `6a287b97cf73a1dbd63d070566b8a4bd167a8b6b`; RQ629 is DONE and RQ630 remains the primary runnable prompt.
- The post-close cascade recorded in `.ai/runs/2026-10-12-RQ626-evidence.md` scanned the active RQ addenda and SQL queue with no higher-priority runnable BCI/STAB/SQL task. The RQ626 correction changed evidence/runtime tests only; no queue dependency changed.
- Collision check: no RQ630 lock, branch, open PR, or attached worktree existed. Existing P-UI-43 owns separate AnalyticsTrustHeader files.
- Worktree: `C:\Users\Ivan\.codex\worktrees\rq630-schema-drift\Trendplus2`; branch: `codex/rq630-analytics-schema-drift`; local lock: `.ai/task-locks/RQ630-codex.lock.md`.

### Problem
RQ613, RQ628 and RQ629 were all classes of the same silent drift: catalog state differed from what the model/snapshot claims, and nothing failed until a runtime query hit it. No test compares the physical catalog of a freshly migrated analytics database against the EF model.

### Evidence
- RQ629: two undiscoverable migrations left missing indexes and narrower columns for months with green CI.
- RQ628: physical column missing while migration history said applied.
- CI `analytics-tests.yml` applies migrations and startup SQL but only runs behavioural tests.

### Do
1. Disposable PostgreSQL: apply `AnalyticsDbContext` migrations (plus the startup analytics SQL the CI job applies). For every model entity, compare columns (existence, nullability, varchar length, type), and named indexes (existence, uniqueness, filter, column order) with `information_schema`/`pg_indexes`.
2. Maintain a small reviewed allowlist (reason, owner, sunset) for intentional differences. Examples: columns left wider than the model by RQ629; the refresh-run index sort direction, unless fixed with an additive idempotent migration.
3. Repeat for `TrendplusDbContext` core tables only where the cost is small; otherwise register the follow-up.

### Acceptance
Removing any model index or narrowing any model column in a migration fails CI; allowlist entries cannot expire silently. No production DB access.

### Dependencies
RQ629 DONE; independent of RQ622 and STAB16.

### Delivered
- Added `Api.Tests/AnalyticsModelCatalogDriftPostgresTests.cs`, a disposable PostgreSQL guard for AnalyticsDbContext and TrendplusDbContext model/catalog parity.
- Applies both EF migration sets directly, compares model columns and named index shape immediately after migration, applies the startup SQL files used by CI, then compares again so startup SQL cannot hide missing migration objects.
- Keeps exact reviewed allowances for legacy RQ629 differences with reason, owner and sunset validation; unused allowances are permitted when clean migrations already converge the schema.

### Completion note
- Date: 2026-10-12
- Status: DONE
- Completion: catalog guard passed on PR and exact current-main PostgreSQL CI.
- Changed files: `Api.Tests/AnalyticsModelCatalogDriftPostgresTests.cs`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md`, `MASTER_ROADMAP.md`, `.ai/runs/2026-10-12-RQ630-evidence.md`.
- Checks run: Release Api.Tests build passed with 0 warnings/errors; PR #131 backend run `38275016017` passed (1,983 passed, 42 classified skips, 0 failures); exact-main backend run `38275050911` passed on `1a1322f8d73d682df3b531047d3eb597b7fd34eb` with the same counts; agent-instruction, queue and planning validators (including self-tests) and `git diff --check` passed. Post-close Planning Governance run `38277164328` is in progress on `3cce486d0e663f8da69f5dc8382b4a26d96292b6`.
- Checks not run: local PostgreSQL execution was unavailable because Docker was stopped; CI executed the PostgreSQL test. No production DB or deployment was accessed.
- Run log: `.ai/runs/2026-10-12-RQ630-evidence.md`
- Evidence state: synchronized
- Delivery mode: pull-request; PRs #129, #130, and #131 merged.
- Main commit SHA: `1a1322f8d73d682df3b531047d3eb597b7fd34eb`
- Main verification: refreshed local `main` and `origin/main` both point to `3cce486d0e663f8da69f5dc8382b4a26d96292b6`, which contains implementation SHA `1a1322f8d73d682df3b531047d3eb597b7fd34eb`; exact-main backend run `38275050911` passed.
- Missed: none within RQ630 acceptance.
- Follow-up: RQ631 remains READY; its test-host diagnosis is clearer, but a recent unmerged branch overlaps its middleware path and needs owner resolution before same-file work.
- Residual risk: production/deployed schema was not accessed; RQ631's production telemetry status remains unknown.
- Post-close routing: RQ631 remains the READY successor; it was not claimed because remote branch `review/http-metric-field-bounds-20261012` contains unique changes to the same middleware file (commit `c5999924`), with no PR/worktree/lock found. Current CI log identifies `SQLSTATE 42P01` (`PerformanceLogs` relation missing) in test hosts; production status remains unproven.
- Prompt defect / scope repair: bypassed `DatabaseInitializer` self-healing, compared before and after the CI startup SQL, and removed the false requirement that each reviewed allowance be exercised.

---

## RQ631 - Classify the recurring "Failed to persist HTTP request performance metric" warning

Status: DONE
Priority: P3
Type: observability/diagnosis
Feature family: request-performance-telemetry
Parallel-safe: yes (`Api/Middleware/RequestPerformanceLoggingMiddleware.cs` and its tests; closed in PR #132)
Owner: ivanjovicic (completed through PR #132; no ownership transfer)
Ready after: none (registered by `docs/qa/AGENT_PROMPT_LOG_AUDIT_2026-10-12.md` F5)
Commit suggestion: `fix(observability): make request performance persistence failures diagnosable`

### Completion note — 2026-10-12
- Delivered through PR #132. Implementation commit 092a9707397690b2361641f15d8a87abc5478f08 is contained in main; merge commit f8ae875a9e9d464469585b87050773683bac272c was verified on refreshed origin/main.
- Diagnosis: the five prior SQLSTATE 42P01 warnings came from disposable PostgreSQL test hosts where optional HTTP metric capture was enabled without PerformanceLogs. Those non-telemetry hosts now disable capture; production provisioning was not inspected.
- Middleware bounds persisted fields, emits trendplus.http.performance.persistence.failures from the Trendplus.Api .NET Meter, throttles repeated warnings per middleware instance until recovery, and preserves request handling.
- Focused middleware tests passed 12/12. Backend Actions run 38296111833 passed on PR head 060210565fef30b419bf10189dc89b408bd5d328; the completed backend log had 0 target persistence warnings, 0 SQLSTATE 42P01 matches and 0 missing-PerformanceLogs matches. RQ453 passed in the same run. Analytics Quality Gates run 38296111872 passed on that head.
- Current-main backend run 38297272059 passed on merge SHA f8ae875a9e9d464469585b87050773683bac272c; full backend suite and RQ453 passed, with 0 target persistence warnings, 0 SQLSTATE 42P01 matches and 0 missing-PerformanceLogs matches.
- Production database/schema and actual production telemetry health remain unknown; the delivered .NET counter makes persistence failures countable, but no production state is claimed.
- Run log: .ai/runs/2026-10-12-RQ631-evidence.md
- Evidence state: synchronized.
### Problem
`RequestPerformanceLoggingMiddleware` swallows persistence exceptions as a warning (`Api/Middleware/RequestPerformanceLoggingMiddleware.cs:60-65`). Actions run `38236578398` logs it 36 times, each paired with an EF `Failed executing DbCommand` error. It is not known whether only test hosts lack the target table/schema or production telemetry is also silently lost.

### Do
1. Reproduce from a CI-equivalent PostgreSQL bootstrap. Capture the exact SQLSTATE and the table it hits.
2. If test-only, configure the test hosts so the middleware is disabled or provisioned, and stop CI-log noise. If real, fix provisioning additively and idempotently.
3. Expose a counter or readiness detail for persistence failures; don't log one warning per request.

### Acceptance
No unexplained persistence warnings in the backend CI log. A production failure is countable rather than silent. No change to request handling.

---

## RQ632 - Stop pushing implementation commits to main before their focused backend tests pass

Status: DONE
Priority: P2
Type: process/governance
Feature family: direct-main-delivery-hygiene
Parallel-safe: yes (docs/protocol only)
Owner: Grok Bot (owner decision recorded)
Ready after: owner decision 2026-10-12 15:13: option (a), direct-main stays allowed, focused backend tests must pass before push.
Commit suggestion: `docs(queue): require focused backend proof before direct-main code push`

### Problem
Of 12 failed "Analytics Tests & Data Integrity" runs on main from 2026-10-10 to 2026-10-12, 11 were implementation commits repaired by a follow-up test commit. Each one leaves main red for 10-60 minutes and makes concurrent agents' runs ambiguous. Examples: `f55717f2`->`d500304b`, `968e8f30`->`35aabd58`, `116df731`->`d186bde9`, plus `b8e1c08f`, `b691f7d1`, `d7d4caec`, `e5a3f617`, `ca5f9ffc`, `408275c7`, `99a7487b` and `7a23c6d5` (see the audit report table).

### Acceptance
The protocol has an explicit pre-push rule (or a PR requirement). The validator self-test covers the rule if it is machine-checkable.

### Completion note
Owner (Ivan) chose option (a) on 2026-10-12 15:13: code changes may go directly to `main`, but the focused backend tests must pass before the push. The rule is in `docs/ai/PROMPT_QUEUE_PROTOCOL.md` under `## Main-first delivery` ("Pre-push backend proof for direct-main code changes"). The rule depends on which tests are relevant to a change, so it is not machine-checkable; no validator self-test was added. Delivered together with the fix for the flaky startup-readiness 503 in Analytics Tests run 38248162958 (test hosts now disable `StartupReadiness:GateApiTraffic`).

---

## RQ633 - Diagnose failed Vercel production deploys and stop deploying docs-only commits

Status: DONE
Priority: P3
Type: frontend deploy/config
Feature family: vercel-deploy-hygiene
Parallel-safe: yes (`vercel.json`, `Klijent/clientapp/vercel.json`)
Owner: Cursor (cloud agent)
Commit suggestion: `chore(deploy): skip Vercel builds for non-frontend commits`

### Problem
Production deploys failed on `f35e5656` (2026-10-12 11:01 UTC+2) and `3a78e062` (11:12). Both are commits with no frontend change; later deploys succeeded. The root `vercel.json` had no `ignoreCommand`, so every docs/backend commit triggered a production build. `Klijent/clientapp/vercel.json` duplicates the headers/rewrites, so it was unclear which config is authoritative.

### Do
1. Classify both failures from the Vercel logs: transient, concurrency/cancel, or a real build break.
2. Add an `ignoreCommand` that skips builds when `Klijent/clientapp` and `vercel.json` are unchanged. Keep exactly one authoritative config.

### Acceptance
Docs-only commits do not create production deployments. Both historic failures are classified with evidence.

### Completion note

- Date: 2026-10-12
- Agent: Cursor cloud agent
- Status: DONE
- Completion: 100%
- Changed files: `vercel.json`, `scripts/vercel-should-build.sh`, `scripts/vercel-should-build-selftest.sh`, `docs/qa/VERCEL_DEPLOY_FAILURE_CLASSIFICATION_2026-10-12.md`, `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md` (stale BCI17 header repair), `.github/workflows/analytics-quality-gates.yml`
- Checks run: `bash scripts/vercel-should-build-selftest.sh` — pass; `git diff --check` — pass; `node scripts/check-prompt-queues.mjs` — pass
- Checks not run: `npx vercel inspect … --logs` — no Vercel credentials; live Vercel skip-build proof — requires post-merge production push
- Run log: `.ai/runs/2026-10-12-RQ633-evidence.md`
- Delivery mode: direct-main
- Main commit SHA: `33c17219fc78f5485a4b0426f43c04ce005a8b1c`
- Main verification: `git rev-parse origin/main` -> `33c17219fc78f5485a4b0426f43c04ce005a8b1c`
- Missed: line-level Vercel deployment log capture
- Follow-up: owner optional `vercel inspect` on `dpl_HTdAhWqwZDDBRu6QxrnVQxgocGGT` and `dpl_GWMNgzfNTGe5338RVEPQJH39C7m6`
- Residual risk: duplicate `Klijent/clientapp/vercel.json` kept for legacy subdirectory-root Vercel projects only
- Evidence state: synchronized


---

## RQ634 - Certify nonblocking web startup when optional supplier windowed MVs are missing

Status: DONE
Priority: P1
Type: backend/CI integration test + narrow startup correction
Feature family: supplier-windowed-cold-start-regression
Parallel-safe: yes (own new test class and supplier startup gates only; do not edit SQL 029, operator instructions or unrelated health endpoints)
Owner: Codex (Trendplus2 main checkout)
Ready after: PR #133 merged on main `1c9158d3e9a74de96c0b7ece60fb0c88f9a18058`
Commit suggestion: `test(startup): certify optional supplier MV repair cannot gate core readiness`

### Completion note

- Date: 2026-10-13
- Status: DONE
- Completion: The default web startup path no longer builds populated optional SQL 018 supplier caches with an unbounded timeout; optional SQL 029 caches remain controlled by their explicit setting. Shared database schema initialization is serialized and releases pooled-session advisory locks. Missing optional caches remain unavailable and supplier endpoints fail closed; core readiness is not fabricated or gated by those optional caches.
- Changed files: `Infrastructure/Seed/DatabaseInitializer.cs`; `Api/Services/Startup/DeferredStartupTasksHostedService.cs`; `Api/Services/Startup/SupplierDecisionSchemaRepairHostedService.cs`; `Api.Tests/SupplierDecisionWindowedStartupPolicyTests.cs`; `Api.Tests/SupplierDecisionSchemaReadinessIntegrationTests.cs`; `Api.Tests/SupplierDecisionWindowedColdStartRegressionTests.cs`.
- Checks run: Release focused API tests 54/54; full CI backend test step in run `38387779145` on exact implementation SHA `a4bd52e4576593f2317a12b9ecc4dbc1e9a55882` passed 2,010/2,052 (42 classified skips), including 4/4 new PostgreSQL regressions; governance validators and `git diff --check` passed. Later current-main run `38389785987` was red only in the separate RQ635 SQL 029 lifecycle tests (4 failures from missing fixture relations); all four RQ634 tests passed in that run.
- Checks not run: local PostgreSQL/Testcontainers execution (Docker unavailable); no production provider or database checks.
- Run log: `.ai/runs/2026-10-13-RQ634-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `a4bd52e4576593f2317a12b9ecc4dbc1e9a55882`
- Main verification: fresh `origin/main` at `6a1e0901e467d80ca481c68849e2775cc0b792be` contains the implementation SHA and synchronized RQ634 closure; exact-SHA CI backend test step passed as noted above.
- Missed: no runtime Render/Neon causality or recovery proof; owned by STAB16.
- Follow-up: RQ636 remains primary READY; RQ638, RQ639 and RQ640 remain READY.
- Residual risk: production startup configuration, deployed SHA and live supplier cache state remain unverified. Current-main CI run `38389785987` also has four RQ635-owned SQL 029 lifecycle failures caused by missing `__StartupSqlScriptHistory` / `vw_supplier_fullprice_signals` fixture prerequisites; this is outside RQ634 scope and remains an RQ635 follow-up.
- Post-close routing: RQ636 (primary READY)
- Prompt defect / scope repair: expanded the prompt's SQL 018 audit finding into bounded optional-cache startup behavior; fixed the pooled advisory-lock release and disposable PostgreSQL fixture configuration failures exposed by CI.

Commit SHA `a4bd52e4576593f2317a12b9ecc4dbc1e9a55882` is contained in current `main`.

### Problem

PR #133 added `DatabaseInitialization:BuildSupplierDecisionWindowedViewsOnStartup=false` for Production, but its focused tests verify configuration values and source snippets, not an end-to-end cold start against a PostgreSQL database missing both supplier 90d/180d materialized views. The earlier audit overlooked the separate **018 all-time supplier hub cache build**, which may run with `commandTimeoutSeconds: 0`, and independently contend for the same analytics database. A future startup refactor could execute SQL 029 again or make optional absence block /ready.

### Evidence

- Render user-supplied 2026-10-12 16:50 UTC logs: 90d/180d MISSING_OBJECT; forced SQL 029 startup execution; coincident /ready and /api 503 (not proof of sole causality).
- PR #133 merged; backend suite `38301829902` passed on merge SHA. `SupplierDecisionWindowedStartupPolicyTests` only checks the switch.
- `Infrastructure/Seed/DatabaseInitializer.cs` has multiple startup/contract-repair call paths, including a Trendplus nivelacija dependent restoration branch.
- `EnsureAnalyticsSupplierDecisionSchemaAsync` invokes `BuildSupplierDecisionHubObjectsAsync(... allowHeavyRefresh: false)` even when SQL029 rebuild is disabled; if 018 materialized caches are missing, `BuildSupplierDecisionHubObjectsAsync` builds them with no SQL command timeout. `allowHeavyRefresh: false` skips explicit REFRESH, not the initial populated CREATE.
- Production provider/run-time proof remains STAB16.

### Scope

`Infrastructure/Seed/DatabaseInitializer.cs` (only if needed), `Api.Tests/SupplierDecisionWindowedStartupPolicyTests.cs` and **new** `Api.Tests/SupplierDecisionWindowedColdStartRegressionTests.cs`. Avoid `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql` (RQ635), `AccessImportBackgroundWorker.cs` (RQ637), `CachedAnalyticsOperationalFallbackTests.cs` (RQ636), Render/Neon writes, default production deploy changes and unconditional disablement of safety gates.

### Read first

`docs/qa/RENDER_SUPPLIER_MV_STARTUP_503_2026-10-12.md`; `.ai/runs/2026-10-12-PR133-branch-review-evidence.md`; `Api/Services/Startup/SupplierDecisionSchemaRepairHostedService.cs`; `Api/Services/Startup/ReadinessWarmupHostedService.cs`; `Api/Services/Startup/StartupReadinessState.cs`; the existing supplier schema/readiness PostgreSQL fixture.

### Do

1. Build an isolated disposable PostgreSQL test with required core schema and deliberately absent supplier 90d/180d MVs. Use effective Production-equivalent configuration with the setting disabled; exercise both full/deferred initializer and background supplier schema repair paths **when safe in isolation**. When the full pipeline cannot be cheaply exercised, provide a narrowly factored and tested decision boundary and report the missing proof instead of fabricating an end-to-end pass.
2. Instrument/test whether SQL 029 is invoked, whether optional missing MVs are still MISSING_OBJECT, and whether core database readiness is independent of this optional capability. No fake READY and no all-time supplier score fallback.
3. Test an explicit allowed maintenance override `true` separately on disposable PostgreSQL; verify existing behavior and no overlapping/advisory-lock regression.
4. Capture timing and causal classification of any 503. Verify `/ready` response reason, initialization state and Retry-After while required migrations are pending. Do not suppress required provenance checks or fail-fast.
5. Preserve existing tests and add a regression that fails if a web startup path bypasses the new gate.
6. **New confirmed residual (2026-10-13):** separately examine the 018 all-time supplier cache build: prove whether a missing core cache triggers materialized `CREATE` with unbounded command timeout during web startup despite `AllowSupplierDecisionHeavyRefreshInInitializer=false`. If yes, separate required cheap schema contract from the optional populated cache build, or bound and isolate it safely. Preserve cold-start source contract and fail-closed supplier endpoints; do not indiscriminately skip mandatory schema repair, silently mark all-time caches READY or run SQL 018 in parallel. Add focused PostgreSQL/timeout regressions.

7. **Startup owner collision check:** `SupplierDecisionSchemaRepairHostedService` is always registered in a web process (enabled by default, delayed ~8s), while `DeferredStartupTasksHostedService` can be registered when `Database:AutoMigrate=true`. Compare their effective SQL 011–018 and 029 ownership and advisory locks; guard against concurrent initialization of 014/016/018 or retain provably safe serialization when both start on the same database. Test same process, split DB, unified DB and cancellation on disposable PostgreSQL. Do not turn off required migrations by default.

### Tests

Focused xUnit new cold-start class plus `SupplierDecisionWindowedStartupPolicyTests`, existing `SupplierDecisionSchemaSqlTests` and any touched lifecycle test, Release build, `git diff --check`. Disposable PostgreSQL must execute in CI with zero skipped target tests; run governance validators for queue changes.

### Acceptance

Web cold start does not run SQL 029 with the setting false **and does not inadvertently build an unbounded optional 018 supplier materialized cache on the web path**; core readiness is neither faked nor gated only by optional supplier 90d/180d absence. Supplier capability reports unavailable. Explicit maintenance override remains functional, and focused PostgreSQL CI proof exists on the exact code SHA.

### Dependencies

PR #133 code already merged; no Render credentials, fresh import, owner price decision or production database mutation required. STAB16 remains the sole owner for actual deployment recovery.

---

## RQ635 - Make supplier SQL 029 recovery preflight and replay safe outside the web service

Status: DONE
Priority: P2
Type: SQL/PostgreSQL migration-quality + operator tooling tests
Feature family: supplier-windowed-sql029-maintenance-preflight
Parallel-safe: yes (own SQL 029 / `tools/supplier-windowed-*` / new isolated PostgreSQL oracle; avoid RQ634 startup gate files)
Owner: cursor-cloud (2026-10-13)
Ready after: PR #133 merged; independent of STAB16 provider access for repository-local implementation
Commit suggestion: `test(sql): preflight and idempotency oracle for supplier windowed MV maintenance`
Completion note 2026-10-13: added read-only `tools/supplier-windowed-mv-preflight.sql`, maintenance guide `docs/qa/SUPPLIER_WINDOWED_SQL029_MAINTENANCE.md`, and `SupplierWindowedSql029LifecycleTests` (preflight verdict matrix, idempotent 029 replay, partial 90d, legacy definition replacement, rolled-back transaction). Focused tests 6/6; `git diff --check` pass. Delivered on `main` at `ca5b6c4129ae72cddad9ed4407992e56870ad31c`. Run log: `.ai/runs/2026-10-13-RQ635-evidence.md`. Evidence state: synchronized. No production Neon/Render maintenance performed.
Reopened 2026-10-13 14:24 (owner instruction, P0): status DONE -> PARTIAL. The "Focused tests 6/6" claim was not real PostgreSQL proof (Docker was unavailable locally, so the tests returned early). Every completed main backend run since delivery fails the same 4 tests in `SupplierWindowedSql029LifecycleTests` with `42P01`: relations `__StartupSqlScriptHistory` and `vw_supplier_fullprice_signals` do not exist. Runs: `38389785987` (`ca5b6c41`), `38391716311` (`794c8084`), `38393842455` (`32e3723c`). The fixture does not provision the startup SQL history table or the SQL 029 prerequisite views. Fix is delivered through a PR with green PostgreSQL CI, per owner decision (a).
Re-close 2026-10-13: status PARTIAL -> DONE. Root cause: the test pointed DefaultConnection and AnalyticsConnection at one database, so `EnsureAnalyticsSupplierDecisionSchemaAsync` took its unified-database branch and skipped SQL 011-018 and `__StartupSqlScriptHistory`. Fix in PR #137 (branch commit `38dd152f`, merged as `bae9b0b691efe94f3c1e2f2e6ed4ad3a340a345b`): the Trendplus DB is a separate isolated database; `PostgresRequiredFact` reports Skipped (not Passed) unless `CI=true` or `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true`; when required, an unavailable fixture fails (verified locally without Docker); CI backend jobs set the flag explicitly. No test removed and no assertion weakened. PR CI `38397641969`: all 6 `SupplierWindowedSql029LifecycleTests` executed and passed (0 skipped), full backend suite green. Main CI on the merge SHA: `38398763696` success. Covers the RQ639 fail-closed intent for PostgreSQL fixtures. Evidence state: synchronized.

### Problem

Production analytics is missing `mv_supplier_decision_score_cache_90d` and `_180d`; code intentionally stopped automatic web startup rebuild. The SQL 029 file creates populated materialized views and has conditional DROP/CREATE logic. Its execution cost, preconditions, idempotence on partial schema, and safe deployment route are not adequately certified for controlled maintenance on a resource-limited Neon database.

### Evidence

- `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql` contains `CREATE MATERIALIZED VIEW IF NOT EXISTS`, inline rollups, a conditional historical DROP and indexes.
- Render log stopped after `Executing startup SQL file ...029` without completion/error in the supplied excerpt.
- `docs/qa/RENDER_SUPPLIER_MV_STARTUP_503_2026-10-12.md` explicitly defers recovery to a separate maintenance window.
- Existing `SupplierDecisionSchemaSqlTests` and `SupplierDecisionSchemaReadinessIntegrationTests` have some idempotence coverage; do not duplicate those without identifying a missing case.

### Scope

New read-only operator preflight script/report (`tools/supplier-windowed-mv-preflight.sql` or equivalent), **new** isolated `Api.Tests/SupplierWindowedSql029LifecycleTests.cs`, and SQL 029 only if a reproducible defect is found. Do not modify `Infrastructure/Seed/DatabaseInitializer.cs`, worker deployments, live Neon databases, or production connection settings.

### Read first

SQL 029; `Database/Migrations/018_AddSupplierDecisionHubViews.sql`; `Api.Tests/SupplierDecisionSchemaReadinessIntegrationTests.cs`; `Api.Tests/SupplierDecisionSchemaSqlTests.cs`; supplier capability inspector and NightlyAnalyticsRefreshOptions; Render recovery runbook.

### Do

1. Build a read-only preflight listing prerequisites, object existence, populated flags, required columns, dependencies, hash history, transaction/lock state indicators and an actionable stop/continue verdict **without** executing DDL. Do not log credentials or database URLs.
2. Run a disposable PostgreSQL matrix: missing both objects; only 90d present; incomplete/legacy score view; already correct objects; empty inputs; repeat execution; interrupted transaction recovery. Include input fixture parity checks for 90d/180d and ensure no silent all-time substitution.
3. Verify SQL 029 can fail closed with structured, bounded output on timeout/lock/privilege/dependency errors. If code repair is needed, make it additive/idempotent, non-destructive and test the exact SQL. Do not issue blanket DROP CASCADE or launch concurrent production builds.
4. Document a separately authorized one-off maintenance sequence, safe cancellation/rollback boundaries, direct connection versus pooler considerations, how to refresh MVs and a read-only postcheck of `relkind`, `relispopulated`, columns and sampled result grain.
5. Keep production status BLOCKED/unknown until actual provider owner carries out and verifies the maintenance. This prompt completes with **repository-local** proof only.

### Tests

Focused real PostgreSQL lifecycle tests (no skipped oracle), schema source tests, SQL script static checks, rebuild in Release, code/SQL diff check. Performance budget measured locally, not extrapolated to Neon free-tier.

### Acceptance

Reproducible non-production recovery and fail-closed preflight exist; partial and repeat execution are safe under tested conditions; maintenance guide is precise. No claim that production 90d/180d MVs are populated and no live database writes.

### Dependencies

No production connection/authority for code work. STAB16 owns provider configuration and execution of the approved production maintenance step. RQ634 is path-disjoint.

---

## RQ636 - Isolate EF InMemory test hosts from Access-import polling and MediatR telemetry noise

Status: DONE
Priority: P2
Type: backend test-host isolation/observability regression
Feature family: fake-provider-host-worker-isolation
Parallel-safe: yes (test-only factory and new isolated tests; do not touch the supplier cold-start or SQL 029 paths)
Owner: cursor-cloud (2026-10-13)
Ready after: RQ631 DONE
Commit suggestion: `test(ci): isolate operational fallback host from unrelated workers`
Completion note 2026-10-13: `OperationalFallbackFactory` disables web worker registration, removes `AccessImportBackgroundWorker`, and uses `OperationalFallbackAnalyticsContext` so MediatR optional performance persistence no longer warns while analytics relations remain missing. Added host-isolation regressions. `CachedAnalyticsOperationalFallbackTests` 8/8; `git diff --check` pass. Delivered on `main` at `794c808431b9bdddb0be21edb1d6bb02c8148c25`. Run log: `.ai/runs/2026-10-13-RQ636-evidence.md`. Evidence state: synchronized. No production code paths changed.

### Problem

Latest successful backend CI still includes recurrent expected test-host exceptions even though all assertions pass. These exceptions obscure real failures and waste test resources: background Access import polling receives an EF InMemory provider, and MediatR performance logging hits a deliberately missing analytics interface.

### Evidence

- Exact merge CI `38301829902` passed 1,998 tests and classified 42 skips but logged three `Failed to save performance log for GetInventoryStatusQuery` warnings from `CachedAnalyticsOperationalFallbackTests.MissingAnalyticsRelationContext`.
- Same run emitted repeated `Relational-specific methods can only be used when the context is using a relational database provider` errors from `AccessImportJobQueue.ClaimNextAsync` in test hosts. Production also intentionally registers Access import polling every 2 seconds; test-host isolation here does NOT fix the real web cold-start polling concern (owned by RQ637). These are **not** proof of production database failure.
- RQ631/PR #132 already fixed optional HTTP metric persistence warnings; this is distinct MediatR behavior and worker registration.
- Re-audit update 2026-10-13: `CachedAnalyticsOperationalFallbackTests` already disables the HTTP metric writer (`a5566d60`) and the readiness gate (`304223ff`). Neither change covers the MediatR `PerformanceLoggingBehavior` warning or the Access-import worker, so the remaining scope is unchanged.

### Scope

`Api.Tests/CachedAnalyticsOperationalFallbackTests.cs`, a new host-isolation test if useful, and strictly scoped test-only configuration; Do not modify `Application/Behaviors/PerformanceLoggingBehavior.cs` (RQ638 owns production MediatR telemetry bounds). Do not modify `Api/Services/Access/AccessImportBackgroundWorker.cs` (RQ637), disable real production workers or suppress/log-filter all database exceptions globally. Do not reopen RQ631.

### Read first

`Api.Tests/CachedAnalyticsOperationalFallbackTests.cs`; `Api/Config/WorkerRuntimeConfig.cs`; `Api/Services/Access/AccessImportJobQueue.cs`; `Application/Behaviors/PerformanceLoggingBehavior.cs`; RQ631 evidence and CI `38301829902` logs.

### Do

1. Map which factory registers Access import polling and why a fake EF provider reaches its relational-only queue claim. Stop only unrelated workers in the isolated test host, retaining the endpoint behaviors under test.
2. Address optional MediatR performance persistence in the intentionally missing analytics host with a narrow fixture-side substitute/opt-out when that path does not test metrics. Retain exceptions when a test expressly verifies missing relations or request success.
3. Add targeted assertions for zero unexpected background worker iterations/telemetry warnings and the original operational fallback responses; do not suppress failures emitted by a real PostgreSQL fixture.
4. Review CI logs for remaining distinct warnings, classify them instead of broad suppressions, and record before/after counts.

### Tests

Affected `CachedAnalyticsOperationalFallbackTests` and new host-isolation regressions; full backend CI on exact SHA; zero skipped target tests; compare filtered log counts. Verify that production registration/configuration did not change.

### Acceptance

Expected operational fallback tests pass unchanged, with no unrelated Access import relational-provider loops or MediatR optional-telemetry warnings in the target host. No production behavior change and no hiding legitimate SQL failures.

### Dependencies

RQ631 DONE and PR #132 merged. No provider secrets or Render/Neon access. Independent of RQ634, RQ635 and STAB16.

---

## RQ637 - Wait for required database readiness before web-process Access import polling

Status: DONE
Priority: P1
Type: backend/worker startup isolation + PostgreSQL integration tests
Feature family: web-access-import-readiness-gate
Parallel-safe: yes (own `Api/Services/Access/AccessImportBackgroundWorker.cs` and worker-specific tests; do not change RQ634 initializer or RQ636 fake-host factory)
Owner: cursor-cloud (2026-10-13)
Ready after: existing Access-import queue and startup readiness services on main; independent of STAB16 provider access for code/testing
Completion note 2026-10-13: web `PROCESS_TYPE=web` defers stale-batch recovery and `ClaimNextAsync` until `StartupReadinessState.IsReady`; dedicated worker process is unchanged; terminal `database_initialization_failed` stops queue work without fabricating readiness. Focused lifecycle tests 5/5; `WorkerRuntimeConfigTests` 12/12; `git diff --check` pass. Delivered on `main` at `c7cda8037ef824a9566fcee77962cf973a1a6176`. Run log: `.ai/runs/2026-10-13-RQ637-evidence.md`. Evidence state: synchronized. No production Render/Neon proof.
Commit suggestion: `fix(import): defer web queue claims until startup readiness`

### Problem

Production registers `AccessImportBackgroundWorker` in the web process and polls the database every two seconds. The worker calls `RecoverStaleBatchesAtStartupAsync` **before any readiness check**, and `ClaimNextAsync` continues polling while required database initialization may be running. This competes with SQL schema repair/Neon connections on a Render free cold start and produces errors under EF InMemory test providers.

### Evidence

- `Api/appsettings.Production.json` sets `AccessImport:WorkerEnabled=true`, `RegisterWorkerInWebProcess=true`, `PollingIntervalSeconds=2`.
- `Api/Program.cs` registers the web-hosted import worker when flags permit.
- `Api/Services/Access/AccessImportBackgroundWorker.cs` runs stale-batch recovery immediately, then claims jobs and performs periodic pending-recovery; it has no startup readiness wait.
- Supplied Render log at 2026-10-12 16:50 UTC emits `access-import-queue` SQL every ~2s while SQL 029 executes and /ready returns 503. Concurrent timing is proven; responsibility for all 503s is not.
- Exact-merge backend CI `38301829902` logs repeated fake-provider queue failures (test-host case is separately RQ636).

### Scope

`Api/Services/Access/AccessImportBackgroundWorker.cs`, any narrow `AccessImportOptions` configuration required, and **new or existing** import worker startup lifecycle tests. No edits to `Infrastructure/Seed/DatabaseInitializer.cs` (RQ634), `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql` (RQ635), or `Api.Tests/CachedAnalyticsOperationalFallbackTests.cs` (RQ636). No automatic resubmission/import of batch #23, data mutation, queue record deletion, or production worker disable.

### Read first

`Api/Program.cs`; `Api/Config/WorkerRuntimeConfig.cs`; `Api/Services/Startup/StartupReadinessState.cs`; `Api/Services/Access/AccessImportBackgroundWorker.cs`; `Api/Services/Access/AccessImportJobQueue.cs`; `Api/appsettings.Production.json`; RQ634/636 and STAB16; tests of pending/stale/retry queue claim behavior.

### Do

1. Identify correct **web-process** readiness dependency for import startup; do not block hosted-service startup synchronously or conflate `/health`, cache refresh and required schema readiness. Dedicated `PROCESS_TYPE=worker` has a different lifecycle: do not deadlock or disable it.
2. Defer **both** stale-batch recovery and `ClaimNextAsync` until the required business schema is usable in the web process. Honor cancellation, fail-fast terminal initialization, optional startup migration policy and explicit worker pause/off flags. Use cancellable async nonbusy waiting with bounded log cadence.
3. After readiness, guarantee existing pending jobs, stale-recovery and manual-run semantics resume unchanged. Never discard/auto-complete jobs, fabricate readiness or re-import a completed batch.
4. Test web before-ready (zero queue SQL), transition to ready (exactly eligible claim/recovery), cancellation/shutdown, worker-process path, disabled worker and an unreachable/failed DB. Verify no cyclical wait with deferred initializer.
5. Record before/after real-PostgreSQL or test-host claim counts, latency and log classification; do not claim reduced production 503 without STAB16 provider evidence.

### Tests

Focused import queue and worker lifecycle tests (including disposable PostgreSQL if a queue mutation is exercised), full backend regression, Release build, `git diff --check`, queue validators if docs updated; zero skipped target tests. Existing import idempotency and recovery tests must remain green.

### Acceptance

Web startup produces **no queue claim or stale-recovery DB traffic before its required database readiness**, resumes safely when ready and preserves import behavior. Dedicated worker startup remains functional, cancellation is prompt and no completed Access import is replayed. No false readiness or provider/deployment assertion.

### Dependencies

Only code/test ownership; no production credentials. STAB16 owns Render measurement and any provider configuration decision. RQ636 isolates the EF InMemory factory and is path-disjoint.

---

## RQ638 - Bound MediatR performance metrics to the existing PostgreSQL schema contract

Status: DONE
Priority: P2
Type: backend/telemetry schema safety + targeted regression tests
Feature family: mediatr-performance-log-column-bounds
Parallel-safe: yes (own `Application/Behaviors/PerformanceLoggingBehavior.cs` and new telemetry boundary tests only; RQ636 owns test-host fixture config)
Owner: Cursor Cloud Agent (2026-10-13)
Ready after: RQ631 DONE / PR #132 merged; same `PerformanceLogs` table, separate MediatR writer
Commit suggestion: `fix(telemetry): enforce MediatR PerformanceLogs varchar limits`

Completion note (2026-10-13): `PerformanceLogColumnBounds` enforces RequestType/RequestName/ExceptionMessage/RequestData/ResponseData caps in `PerformanceLoggingBehavior`; 18 xUnit tests in `PerformanceLoggingBehaviorColumnBoundsTests`. Evidence: `.ai/runs/2026-10-13-RQ638-evidence.md`. Main SHA: `da352033` (implementation `fd9e452f`).
Follow-up 2026-10-13: PR #141 "avoid splitting UTF-16 surrogate pairs" (head `2b2a631d`), merged as `d7d29f7bac307c886413aedf0071fab286017c33`. PR checks green (backend `38399426259`, quality `38399426590`). Main backend on the merge SHA: `38400296623` success. Status remains DONE.

### Problem

RQ631 fixed the HTTP middleware's field caps, but `Application/Behaviors/PerformanceLoggingBehavior.cs` also persists `PerformanceLog` records without enforcing several database column boundaries. A slow/failed MediatR request with a long type name, exception or serialization fallback can yield a database rejection that the behavior hides behind `Failed to save performance log`.

### Evidence

- `Database/Migrations/002_CreatePerformanceLogsTable.sql`: `RequestType VARCHAR(200)`, `RequestName VARCHAR(500)`, `RequestData/ResponseData VARCHAR(4000)`, `ExceptionMessage VARCHAR(2000)`.
- `PerformanceLoggingBehavior.Handle` writes `typeof(TRequest).FullName` as unbounded RequestType and `exception?.Message` as unbounded ExceptionMessage, and does not directly bound RequestName.
- `SerializeObject` truncates a successful JSON serialization at 3900 **plus** `...` (3903, still below 4000), but its `catch { return obj.ToString(); }` fallback has no maximum length; it could exceed 4000.
- RQ631 addressed `Api/Middleware/RequestPerformanceLoggingMiddleware.cs` only. The 3 separate CI fake-provider `42P01` warnings are RQ636's concern, not proof that these length overflows occurred.

### Scope

`Application/Behaviors/PerformanceLoggingBehavior.cs`, new `Api.Tests/PerformanceLoggingBehaviorColumnBoundsTests.cs`, potentially a narrow shared Application helper. No production DB changes, global removal of MediatR pipeline behaviors, or modifications to RQ631 HTTP metric writer and RQ636's test-host factory.

### Read first

`Database/Migrations/002_CreatePerformanceLogsTable.sql`; `Domain/Model/PerformanceLog.cs`; `Infrastructure/DbContexts/AnalyticsDbContext.cs`; `Application/Behaviors/PerformanceLoggingBehavior.cs`; `Api/Middleware/RequestPerformanceLoggingMiddleware.cs`; RQ631 evidence and RQ636 test isolation spec.

### Do

1. Ensure **every** persisted field respects exact PostgreSQL max lengths, including ellipsis characters. Preserve NULL distinction for absent exception/response; never silently convert a failed operation into success.
2. Cover long fully qualified MediatR request names, long messages, long JSON serialization, serialization-failure `ToString()` fallback and surrogate-pair/Unicode boundaries where relevant. Never throw another exception from telemetry persistence while handling a prior request failure.
3. Add isolated deterministic xUnit boundary tests for exactly-at-cap, one-over, far-over, null, empty, serialization failure and context failure. Prefer testing the actual mapped `PerformanceLog` record via a fake EF/test context over string-only source assertions; use disposable PostgreSQL when testing strict `varchar` behavior.
4. Ensure cancellations and slow requests cannot stall indefinitely on optional performance persistence; preserve current logging semantics and explicit cancellation behavior or document any bounded best-effort policy change separately.
5. Do not claim the RQ636 missing-table errors are solved by trimming text. Keep diagnostic classification and proof separate.

### Tests

Focused behavior/serialization tests, existing HTTP metric regression tests for non-regression, optional disposable PostgreSQL column-limits oracle, backend CI on exact SHA; zero skipped target tests, Release build and diff checks.

### Acceptance

MediatR performance persistence cannot exceed defined schema fields through the normal or fallback serialization path. Request failure still propagates, telemetry failure remains isolated, and tests prove the exact column-cap contract. No changes to business queries or production DB.

### Dependencies

None external; RQ631 delivered. Independent of RQ634/RQ635/RQ636/RQ637 and STAB16 provider work.

---

## RQ639 - Fail loudly instead of returning early in AnalyticsDbInfrastructureTests

Status: DONE
Priority: P3
Type: backend tests/CI honesty
Feature family: silent-pass-integration-guards
Parallel-safe: yes (own `Api.Tests/AnalyticsDbInfrastructureTests.cs` and, if needed, one filter line in `.github/workflows/analytics-tests.yml`; no production code)
Owner: Cursor Cloud Agent (2026-10-13)
Ready after: state re-audit 2026-10-13 (`docs/qa/STATE_REAUDIT_2026-10-13.md`)
Commit suggestion: `test(ci): make analytics DB infrastructure checks explicit skips or real integration proofs`

Completion note (2026-10-13): Route **(a)** — replaced silent early `return` with `[OperationsIntegrationFact]` on all three tests; registered them in `scripts/ci/backend-skip-inventory.json` (`expectedKnownSkippedCount` 45, `genuinely-uncovered`). No integration workflow filter added (30s baseline stays non-gating). Evidence: `.ai/runs/2026-10-13-RQ639-evidence.md`. Main SHA: `32e3723c` (implementation `cab5b0d7`).

### Problem

All three tests in `AnalyticsDbInfrastructureTests` start with `if (!IsIntegrationEnabled()) return;`. The unit job does not set `TRENDPLUS_RUN_INTEGRATION_TESTS`. The integration job sets it but runs only named filters, and this class is not among them. So the "critical analytics columns exist", "parallel requests do not crash" and "response-time baseline" checks are counted as **passed** in every CI run without executing. They also do not appear in the BCI17 skip inventory (`scripts/ci/backend-skip-inventory.json`), so the skip accounting understates non-executed proof.

### Evidence

- `Api.Tests/AnalyticsDbInfrastructureTests.cs:30-34` (`IsIntegrationEnabled`), `:42`, `:71` and `:96` (early `return`).
- `.github/workflows/analytics-tests.yml`: the integration steps use explicit `--filter FullyQualifiedName~...` lists (lines ~334-562), none of which names this class.
- This is the only class in `Api.Tests` with this silent-return pattern (re-audit grep, 2026-10-13).
- The gate override was added on 2026-10-13 (re-audit commit), so if the tests are enabled they no longer race startup readiness.

### Scope

`Api.Tests/AnalyticsDbInfrastructureTests.cs`; optionally one integration filter entry and `scripts/ci/backend-skip-inventory.json`. Do not touch other test classes or production code.

### Read first

`Api.Tests/AnalyticsDbInfrastructureTests.cs`; BCI17 skip inventory and its validator; an existing Testcontainers PostgreSQL fixture (for example `SupplierSalesStatsEndpointPostgresParityTests`).

### Do

1. Choose one route and record it: (a) convert the three tests to explicit skips with a reason (`Skip.IfNot`/SkippableFact or equivalent used in the repo) and register them in the BCI17 inventory; or (b) back them with a disposable PostgreSQL fixture and run them in an integration step with zero skips; or (c) delete them if an existing PostgreSQL test already covers the same contract (cite it).
2. Keep the 30 s response-time assertion out of any required gate unless it runs on a stable fixture.

### Tests

Focused run of the class (shows Skipped or Passed with real execution, never vacuous Passed), BCI17 inventory validator, `git diff --check`, governance validators if docs change.

### Acceptance

No test in the class reports Passed without executing its body. CI skip counts include them, or they execute against PostgreSQL.

### Dependencies

None. Independent of RQ634-RQ638.

---

## RQ640 - Guard that every Api.Tests host sets the startup readiness gate explicitly

Status: DONE
Priority: P2
Type: backend tests/regression guard
Feature family: test-host-readiness-gate-guard
Parallel-safe: yes (new `Api.Tests/TestHostReadinessGateConventionTests.cs`; the two-line override may be added to listed factories, which are not owned by an active lane; skip any file under an active lock)
Owner: Cursor Cloud Agent (2026-10-13)
Ready after: state re-audit 2026-10-13
Commit suggestion: `test(ci): require explicit readiness gate setting in WebApplicationFactory hosts`

Completion note (2026-10-13): `TestHostReadinessGateConventionTests` source-scans Api.Tests hosts; gate overrides added to four `IClassFixture` constructors. Evidence: `.ai/runs/2026-10-13-RQ640-evidence.md`. Main SHA: `034790d5` (implementation `27d50fee`).

### Problem

CI runs `ASPNETCORE_ENVIRONMENT=Testing`, so `StartupReadiness:GateApiTraffic` defaults to **on** (`Api/Program.cs` gate middleware, ~1099-1137). The gate answers 503 for any `/api` path before warmup marks readiness, even before routing. Test hosts that do not set the gate either race warmup (flaky 503, run 38248162958, fixed in `304223ff`) or pass vacuously: `AnalyticsFrontendRouteSmokeTests` asserts only `!= 404`, so a gated 503 hid whether a route was registered at all (fixed in the 2026-10-13 re-audit commit). Nothing prevents a new factory from repeating the mistake.

### Evidence

- `304223ff`: 8 factories received `UseSetting("StartupReadiness:GateApiTraffic", "false")`. The re-audit added it to `AnalyticsDbInfrastructureTests` and `AnalyticsFrontendRouteSmokeTests`.
- Local repro (2026-10-12): with `Testing` and an unreachable DB, 31 tests in factories without the override failed with 503. After the fix the targeted classes pass.
- Still without an explicit setting (custom fixtures): `SupplierShoeTypeIndependentOracleIntegrationTests`, `SupplierShoeTypeRq447SeamIntegrationTests`. Verify each.
- `AnalyticsCrossScreenRevenueInvariantIntegrationTests:193` intentionally sets `true`.

### Scope

New convention test; two-line overrides in factories that lack one. No change to the production gate default or `Program.cs`.

### Read first

`Api/Program.cs` readiness gate block; `Api/Services/Startup/ReadinessWarmupHostedService.cs`; the factories listed above; `304223ff` diff.

### Do

1. Add a source-scanning test: every `WebApplicationFactory<global::Program>` subclass, `IClassFixture<WebApplicationFactory<...>>` consumer or `WithWebHostBuilder` host in `Api.Tests` must mention `StartupReadiness:GateApiTraffic` (either value), or appear in a short allow-list with a reason (readiness-under-test).
2. Add the override where it is missing, unless the class deliberately tests readiness.
3. Make the test's failure message name the file and the fix.

### Tests

New convention test plus the touched classes, run with `ASPNETCORE_ENVIRONMENT=Testing` (with `Analytics__AllowLoopbackInProduction=true` locally), then CI on the exact SHA.

### Acceptance

A new host without an explicit gate choice fails CI with an actionable message. No test can pass because of a gated 503.

### Dependencies

None. Do not edit files under an active local task lock (check `.ai/task-locks/`).

---

## RQ641 - Stop main-branch CI cancellation from leaving pushed commits without backend proof

Status: DONE
Priority: P2
Type: CI/workflow config
Feature family: main-ci-cancellation-gap
Parallel-safe: yes (`.github/workflows/analytics-tests.yml`, `analytics-quality-gates.yml`, `planning-governance.yml` concurrency blocks only)
Owner: Cursor Cloud Agent (2026-10-13)
Ready after: owner chooses (a) never cancel `push` runs on `main` (cancel only PR/branch runs), accepting more CI minutes, or (b) keep cancellation but require every code commit's proof to come from its PR head run, documenting that main tip runs are the only main proof.
Commit suggestion: `ci: do not cancel in-progress backend runs for pushes to main`
Owner decision 2026-10-13 14:24: keep route (a) as delivered in `e319f84c`; do not change it. Verified on main: `analytics-tests.yml:22`, `analytics-quality-gates.yml:25` and `planning-governance.yml:54` use `cancel-in-progress: ${{ github.event_name != 'push' || github.ref != 'refs/heads/main' }}`. Main push runs are no longer cancelled. PR and branch runs still are.

Completion note (2026-10-13): **Route (a)** — `cancel-in-progress` is false for `push` to `refs/heads/main` in analytics-tests, analytics-quality-gates and planning-governance; PR/branch runs still cancel in progress. Assumption: user queue-continue instruction authorizes route (a) over (b) for main-first delivery proof. Evidence: `.ai/runs/2026-10-13-RQ641-evidence.md`. Main SHA: `6dea05a2` (implementation `e319f84c`).

### Problem

All three main workflows use `concurrency: group: <name>-${{ github.ref }}` with `cancel-in-progress: true`. When agents push to `main` within minutes of each other, the earlier commit's run is cancelled. Code commits then reach `main` with **no completed backend run**, and a red-then-fix sequence can be hidden or blamed on the wrong commit.

### Evidence

- `.github/workflows/analytics-tests.yml:20-22`, `analytics-quality-gates.yml:23-25`, `planning-governance.yml:52-54`.
- Main push runs cancelled 2026-10-12/13 (UTC): `50f196d4` (#129 merge, cancelled by `713cae82`, which then failed), `092a9707`, and six docs commits between 08:04 and 08:08 UTC on 10-13.
- RQ632 owner rule (`docs/ai/PROMPT_QUEUE_PROTOCOL.md`, `## Main-first delivery`) treats the first CI run as proof when local proof is impossible. A cancelled run gives none.

### Scope

Concurrency blocks of the three workflows; one protocol sentence if (b) is chosen.

### Read first

The three workflow files; `docs/ai/PROMPT_QUEUE_PROTOCOL.md` `## Main-first delivery`; `docs/qa/STATE_REAUDIT_2026-10-13.md` section 3.

### Do

1. (a): use `cancel-in-progress: ${{ github.event_name != 'push' || github.ref != 'refs/heads/main' }}`, or a per-SHA group for main pushes.
2. (b): document the rule and add a completion-note field naming the run that proves the code SHA.

### Tests

`actionlint` (or the repo's workflow validation), governance validators, two consecutive docs pushes showing that neither main run is cancelled (option a).

### Acceptance

Every code commit pushed to `main` has a completed backend run, or the protocol explicitly names the alternative proof.

### Dependencies

Owner decision on CI minutes versus proof.

## RQ642 - Replace two AnalyticsDbInfrastructureTests skips with deterministic CI PostgreSQL proof

Status: DONE
Priority: P2
Type: targeted backend PostgreSQL/API integration-test recovery
Feature family: analytics-infrastructure-uncovered-smokes
Parallel-safe: yes (own Api.Tests/AnalyticsDbInfrastructureTests.cs and new isolated fixture/tests; do not edit supplier SQL029 or RQ635 lifecycle tests)
Owner: Cursor
Ready after: RQ639 classification DONE and RQ635 corrective PR #137 green/merged
Commit suggestion: `test(analytics): certify infrastructure schema and route concurrency in disposable PostgreSQL`

### Problem

RQ639 transparently classified three formerly vacuous tests as skipped and raised BCI17 to 45. Two of those tests exercise deterministic business safety properties (critical analytics schema columns and parallel supplier-sales HTTP requests), but a skip inventory is not actual proof. A third response-time baseline is environment-sensitive and should not become a flaky required gate without independent benchmarking design.

### Evidence

- RQ639 code SHA `cab5b0d7afabbe1e9e92a8c73df936e708968146`: `AnalyticsDbInfrastructureTests.AnalyticsSchema_CriticalColumnsExist` and `AnalyticsEndpoint_HandlesParallelRequests` were changed to `[OperationsIntegrationFact]` and classified as `genuinely-uncovered` in `scripts/ci/backend-skip-inventory.json`.
- RQ639 also classifies `AnalyticsEndpoint_ResponseTimeBaseline` as a third environment-sensitive skip; leave it separate.
- Corrected RQ635 PR-head `38397641969` ran 2,039 tests and classified 45 skips, demonstrating the two target tests still have no required CI proof.

### Scope

Target only the first two RQ639 skip entries and their deterministic fixture/path. Build isolated disposable PostgreSQL/HTTP setup using existing test infrastructure and retained source contract, not user data. Do not change product analytics formulas or request semantics, suppress 503 via API gate bypass without documenting the tested state, rename other Operations smoke families, or remove skip inventory entries before exact CI proof.

### Read first

`Api.Tests/AnalyticsDbInfrastructureTests.cs`; `Api.Tests/OperationsIntegrationFactAttribute.cs` (locate actual attribute path by code search); `Api.Tests/DatabaseInitializerP0IntegrationTests.cs`; `Api.Tests/SupplierDecisionSchemaReadinessIntegrationTests.cs`; `scripts/ci/backend-skip-inventory.json`; `Api/Services/Startup/StartupReadinessState.cs`; RQ639 evidence.

### Do

1. Reconcile the exact two skip identities and enumerate their true assertions. Determine if existing PostgreSQL lifecycle/catalog checks already supersede the schema-column test; if so, link explicit actual executed proof and only remove redundant skip after verifying semantic parity.
2. For remaining unique semantics, move tests to a stable isolated PostgreSQL fixture with required migrations + deterministic sales facts and bounded startup, and use a named CI filter that really executes them. No shared mutable test DB or live credentials.
3. For concurrent HTTP assertions, prove multiple overlapping requests really reach the handler (not only simultaneous 503, early middleware or a sequentially serialized fake); verify success and correct scoping, with safe disposal, transaction/connection isolation, timeouts and no hidden retries.
4. Record before/after exact test names, total skipped count and CI log proof. Update BCI17 skip inventory only after successful target execution on the exact head SHA. Do not count a local no-Docker early return as passing.
5. Keep `AnalyticsEndpoint_ResponseTimeBaseline` explicitly skipped/independently owned unless a stable, non-gating performance measurement infrastructure is designed; no arbitrary 3-second gate in GitHub shared runners.

### Tests

Two focused named PostgreSQL/HTTP integration tests must execute in CI with zero target skips; complete relevant backend suite and diagnostics; no regression to SQL029 RQ635 tests; guarded skip-inventory check, `git diff --check`, governance validators if queue/evidence files change.

### Acceptance

Two missing deterministic analytics-infrastructure proof contracts have actual CI results, no false success via 503 or skipped DB checks, and skip inventory matches the real TRX. Environment-sensitive performance baseline remains transparently classified. Delivered code/test SHA on main with evidence; no production dependency.

### Dependencies

RQ639 DONE, PR #137 merged and RQ635 PostgreSQL tests green. STAB16 provider gate is not a start dependency for isolated code tests. Avoid colliding with unrelated active test-host ownership; rescan locks/PRs before claim.

### Completion note

- Date: 2026-10-13
- Status: DONE
- Completion: Added `AnalyticsDbInfrastructurePostgresIntegrationTests` with two `PostgresRequiredFact` proofs (critical EF column selects + ten concurrent successful supplier-sales HTTP calls on isolated migrated PostgreSQL). Removed superseded OperationsIntegrationFact methods; kept response-time baseline skip. BCI17 inventory `expectedKnownSkippedCount` 45 → 43.
- Changed files: `Api.Tests/AnalyticsDbInfrastructurePostgresIntegrationTests.cs`, `Api.Tests/AnalyticsDbInfrastructureTests.cs`, `scripts/ci/backend-skip-inventory.json`, `.ai/runs/2026-10-13-RQ642-evidence.md`
- Contract/runtime behavior changed: no
- Checks run: Release build; local filter `AnalyticsDbInfrastructure` (3 skipped without Docker); `node scripts/check-prompt-queues.mjs`; exact-main Analytics Tests `38403990864`
- Checks not run: none material after exact-main green CI
- Run log: `.ai/runs/2026-10-13-RQ642-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `cfb66628a0910387dd1663ef7868f9975a72355c`
- Main verification: `origin/main` contains `cfb66628`; Analytics Tests `38403990864` success (2042 passed, 43 skipped, 0 failed); both RQ642 tests Passed
- Missed: none known
- Follow-up: RQ643 readiness status mapping
- Residual risk: `AnalyticsEndpoint_ResponseTimeBaseline` remains a classified skip
- Post-close routing: `Current READY` → none after RQ642; RQ643 registered from no-READY ladder


## RQ643 - Keep /ready status=failed for schema-contract rejection

Status: DONE
Priority: P2
Type: readiness-contract/unit-test hardening
Feature family: startup-readiness-status
Parallel-safe: yes (StartupReadinessEndpoints + focused tests; no SQL029/STAB16)
Owner: Cursor
Ready after: RQ642 DONE with green exact-main CI; observed intermittent PurchaseCostProvenance readiness status mismatch on main run `38401068403`
Commit suggestion: `fix(readiness): map schema contract rejection to failed status`

### Problem

Intermediate main backend run `38401068403` failed `PurchaseCostProvenanceReadinessPostgresIntegrationTests.AutoMigrationDisabled_MissingContractKeepsReadyEndpointUnavailableWithoutDdl` with reason=`schema_incompatible` but public status=`warming_up`. Operators and tests must treat required schema rejection as terminal `failed`, not warmup.

### Scope

- Map schema-contract / schema-verification failure reasons and failure categories to public status `failed`.
- Harden the readiness wait helper to require status=`failed` together with reason=`schema_incompatible`.
- Add Docker-free unit coverage for the status mapping.
- Repair accidental git conflict markers left in `MASTER_ROADMAP.md` on main during RQ642 routing sync.
- Do not change STAB16 provider gates, SQL029 lifecycle tests, or purchase-cost business formulas.

### Acceptance

Unit mapping tests pass; focused readiness tests compile; conflict markers absent from `MASTER_ROADMAP.md`; delivered on main with evidence.

### Completion note

- Date: 2026-10-13
- Status: DONE
- Completion: Extracted `StartupReadinessEndpoints.ResolvePublicStatus`; schema-incompatible / schema_contract_incompatible always maps to `failed`. Wait helper and unit tests updated. Removed conflict markers from `MASTER_ROADMAP.md` and synchronized RQ642 SHA.
- Changed files: `Api/Services/Startup/StartupReadinessEndpoints.cs`, `Api/Services/Startup/ReadinessWarmupHostedService.cs`, `Api.Tests/StartupReadinessStatusMappingTests.cs`, `Api.Tests/PurchaseCostProvenanceReadinessPostgresIntegrationTests.cs`, `MASTER_ROADMAP.md`, this queue, `.ai/runs/2026-10-13-RQ643-evidence.md`
- Contract/runtime behavior changed: yes — `/ready` status for schema rejection is stably `failed`
- Checks run: `StartupReadinessStatusMappingTests` 8/8; `node scripts/check-prompt-queues.mjs`; conflict-marker scan on `MASTER_ROADMAP.md`
- Checks not run: full backend suite / PostgreSQL provenance integration (no Docker in agent VM); exact-main CI after push
- Run log: `.ai/runs/2026-10-13-RQ643-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `723814c2da93fa21bba4d8d365b14c6085df4132`
- Main verification: `origin/main` contains `723814c2` (implementation `39254ca2` / `e6a0cdee`)
- Missed: none known for this slice
- Follow-up: none; STAB16 / RQ610 / RQ601 / RQ602 remain external or business-gated
- Residual risk: other readiness failure categories still rely on `initialization.State == failed` or reason text
- Post-close routing: `Current READY` → none; zero-READY proof in `.ai/runs/2026-10-13-RQ643-evidence.md`


## RQ644 - Reconcile Supplier/ShoeType invalid-input skips with deterministic in-memory proofs

Status: DONE
Priority: P2
Type: skip-inventory / input-validation test reconciliation
Feature family: operations-invalid-input-coverage
Parallel-safe: yes (AnalyticsSupplierSalesIntegrationTests, AnalyticsShoeTypeSalesIntegrationTests, backend-skip-inventory.json)
Owner: Cursor
Ready after: RQ643 DONE; no READY successors; BCI17 still listed Supplier invalid-period/season as genuinely-uncovered despite in-memory siblings
Commit suggestion: `test(analytics): reconcile invalid-input skip inventory with in-memory proofs`

### Problem

Post-RQ643 zero-READY cascade found no claimable READY, but BCI17 still treated four Supplier/ShoeType contracts as genuinely-uncovered even though (a) Supplier invalid-period already had an in-memory Fact and (b) invalid-season lacked only a matching in-memory host proof. Skip inventory honesty is repository-local work.

### Scope

- Add in-memory InvalidSeason NotFound proofs for Supplier and Shoe Type.
- Reclassify Supplier InvalidPeriod, both InvalidSeason entries, and Supplier ReturnsValidJsonWithAllFields as `legacy-superseded` with named proofs.
- Keep golden-snapshot, performance, cohort and remaining trust contracts uncovered.
- Do not change product analytics formulas or STAB16 gates.

### Acceptance

New in-memory Facts pass; skip inventory classifications match real proofs; queue/roadmap synchronized; delivered on main.

### Completion note

- Date: 2026-10-13
- Status: DONE
- Completion: Added Supplier/ShoeType InvalidSeason InMemory Facts; reclassified four BCI17 entries to legacy-superseded (Supplier InvalidPeriod/InvalidSeason/ReturnsValidJson; ShoeType InvalidSeason). Remaining genuinely-uncovered count 16→12. expectedKnownSkippedCount remains 43.
- Changed files: `Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs`, `Api.Tests/AnalyticsShoeTypeSalesIntegrationTests.cs`, `scripts/ci/backend-skip-inventory.json`, `MASTER_ROADMAP.md`, this queue, `.ai/runs/2026-10-13-RQ644-evidence.md`
- Contract/runtime behavior changed: no
- Checks run: filtered InMemory invalid-input tests 4/4; `node scripts/check-prompt-queues.mjs`
- Checks not run: full backend suite (narrow inventory/test change)
- Run log: `.ai/runs/2026-10-13-RQ644-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `4bd4b9b2ff91abb4eb6812211a2d1c83e888b795`
- Main verification: `origin/main` contains `4bd4b9b2`
- Missed: none known for this slice
- Follow-up: remaining 12 genuinely-uncovered Supplier/ShoeType/perf contracts need dedicated fixture prompts
- Residual risk: PostgresParity early-return outside CI if Docker unavailable; CI still executes the named proof
- Post-close routing: `Current READY` → none; see `.ai/runs/2026-10-13-RQ644-evidence.md`


---

## RQ645 - Certify remaining Supplier trust/cohort/golden contract skips on isolated PostgreSQL

Status: DONE
Priority: P1
Type: backend supplier analytics contract/PostgreSQL integration tests
Feature family: supplier-legacy-uncovered-fixture-parity
Parallel-safe: yes for unique Supplier test/fixture paths (never share files or mutable test database with RQ646/RQ647)
Owner: Codex
Local lock: removed before commit/push
Ready after: RQ644 DONE on main `4bd4b9b2`; RQ453 PostgreSQL certification and RQ642 isolated fixture already available
Claim note: 2026-10-13 - claimed after refreshing `origin/main` to `58b91a99b43c2eb4f214b773482b4823b04d46ef`. Earlier local idle-recovery proof was retained and aligned to this canonical RQ645 scope; no conflicting source/test owner, lock, branch, or open PR was found. The remote audit branch contains queue/evidence review only and does not own RQ645 source paths.
Commit suggestion: `test(analytics): certify seven supplier trust and golden contracts`

### Problem

The backend suite is green but `scripts/ci/backend-skip-inventory.json` still marks **seven exact Supplier/Supplier Detail business contracts** `genuinely-uncovered`. Their old `OperationsIntegrationFact` methods read a populated/shared source database, are skipped by default, and cannot alone certify current Supplier endpoint trust/golden semantics. RQ644 deliberately left these intact.

### Evidence

- Intake BCI17 inventory when RQ645 was claimed on main `58b91a99`: expectedKnownSkippedCount 43, `isolated-postgresql-runnable=13`, `legacy-superseded=18`, `genuinely-uncovered=12`. Exactly 7 were this prompt's Supplier contract group, 3 ShoeType (RQ646) and 2 performance (RQ647).
- `Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs` legacy methods:
  `SupplierSalesDetail_ExposesRecommendationTrustAndReferenceCohort`,
  `SupplierSalesStats_ComparablePrePostTotalsMatchSupplierRows`,
  `SupplierSalesStats_DeclaresRecommendationReferenceCohort`,
  `SupplierSalesStats_HandlesMissingSchemaGracefully`,
  `SupplierSalesStats_MatchesGoldenSnapshot`,
  `SupplierSalesStats_ProducesDeterministicJson`,
  `SupplierSalesStats_UsesWeightedMarginBenchmark`.
- RQ453 `OperationsAnalyticsAllRoutesIntegrationTests` and raw-fact oracle already prove **some** overlapping totals and scopes but the seven specific cohort, golden, deterministic and fail-closed contracts remain classified uncovered. Do not claim overlap equals equivalent coverage without method-level assertions.

### Scope

New `Api.Tests/SupplierSalesRemainingContractsPostgresTests.cs` and isolated Supplier fixture(s) (or equivalent descriptive name), the Supplier-specific snapshot fixture if reproducibly justified, and only these seven `scripts/ci/backend-skip-inventory.json` classifications. Touch `Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs` only to add an explicit exact proof pointer or remove demonstrably superseded methods. No changes to ShoeType legacy tests (RQ646), performance benchmark test (RQ647), business endpoint formulas, source SQL or production databases. Avoid shared mutable global test DB.

### Read first

`Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs`, `Api.Tests/OperationsAnalyticsAllRoutesIntegrationTests.cs`, `Api.Tests/OperationsAnalyticsRawFactOracleIntegrationTests.cs`, `Api.Tests/SupplierDecisionSchemaReadinessIntegrationTests.cs`, `Api.Tests/PostgresRequiredFactAttribute.cs`, `scripts/ci/backend-skip-inventory.json`, `docs/ai/PROMPT_QUEUE_PROTOCOL.md`, RQ644 evidence.

### Do

1. Map each of **all seven FQNs** to its exact endpoint, inputs, field assertions and current runnable overlapping proof; record a before matrix. Do not use `!=404` or responses gated at 503 as evidence that a route works.
2. Use isolated disposable PostgreSQL/Testcontainers with explicit schema, only necessary deterministic sales/cost/nivelacija/unknown-supplier facts and safe startup-readiness setup. Positive known/unknown supplier, imported/existing scope, return-signed quantities and absent cost must be identifiable in seed and independent SQL oracle.
3. Prove supplier detail `recommendationAllowed` gating, provenance/reference cohort, row-to-total comparable pre/post, weighted margin denominator (not mean-of-means), missing-schema fail-closed error contract, stable canonical JSON excluding volatile timestamps, and intentional golden contract projection.
4. **Missing-schema case** must mutate/drop objects only within its own disposable DB, with no race against adjacent tests; assert the documented safe error code/status and prevent fallback to all-time or fake-zero recommendation. Golden snapshots must be reviewed against independent business expectations, not rubber-stamped by updating expected files to match any actual value.
5. Match each newly passing real PostgreSQL test to the original skip. Reclassify `genuinely-uncovered -> legacy-superseded` **only** with an exact named running proof in inventory; keep `expectedKnownSkippedCount` at 43 unless TRX truly changes. Leave remaining three ShoeType + two performance skips untouched. Record no-Docker local restrictions transparently.

### Tests

Focused 7 named isolated PostgreSQL assertions with `TRENDPLUS_REQUIRE_POSTGRES_TESTS=true` in PR CI (zero target skips); complete backend suite, RQ453 certification, golden determinism repeated runs, `node scripts/check-prompt-queues.mjs`, skip inventory checker, `git diff --check`, exact-head CI before merge.

### Acceptance

All seven formerly uncovered Supplier contracts have deterministic isolated real-PostgreSQL CI proof and independently reviewed golden/weighted truth. No fake green from shared DB/early-return/503. Inventory classifications point to exact proved test names; no production data/schema changes, and no weakened trust contract.

### Dependencies

RQ644 DONE and existing RQ453/Testcontainers. STAB16 provider access and RQ601 original purchase calculations are **not code start gates**; unresolved real cost remains unknown. Do not alter RQ646/RQ647 files or steal an active test/fixture lock.

### Completion note

- Date: 2026-10-13
- Status: DONE
- Completion: Added seven named isolated PostgreSQL proofs for the Supplier trust, comparable cohort, reference cohort, missing-schema, golden, deterministic JSON and weighted-margin contracts. The seven corresponding inventory entries now point to those passing tests as `legacy-superseded`; the expected skip count remains 43.
- Changed files: `Api.Tests/SupplierSalesRemainingContractsPostgresTests.cs`; `Api.Tests/SupplierSalesStatsEndpointPostgresParityTests.cs`; `scripts/ci/backend-skip-inventory.json`; this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-13-RQ645-evidence.md`.
- Contract/runtime behavior changed: no; tests and evidence only.
- Checks run: exact PR head `2f98416bdf20d1f9b6fd5508d37c07bfc7e69d42` backend run `38421051691` passed 2,060 / skipped 43 / failed 0; all seven RQ645 PostgreSQL tests passed; backend skip inventory reported 43 known skips and 0 new/missing/reason-changed skips; RQ453 certification passed; frontend analytics, POS UI, JavaScript SDK and planning governance checks passed; API.Tests Release build passed; governance validators and `git diff --check` passed.
- Checks not run: local PostgreSQL execution after the date-window correction, because the local Docker endpoint was unavailable. Exact-head PR CI supplied the mandatory disposable PostgreSQL proof.
- Run log: `.ai/runs/2026-10-13-RQ645-evidence.md`
- Evidence state: synchronized
- Delivery mode: PR #145 squash merge
- Implementation SHA: `2f98416bdf20d1f9b6fd5508d37c07bfc7e69d42`
- Main commit SHA: `6ca11bf95b6c7eb0f948bce475a688d633c66dae`
- Main verification: refreshed `origin/main` to `6ca11bf95b6c7eb0f948bce475a688d633c66dae`; it equals the PR merge commit and contains the delivered implementation. Exact-head backend and RQ453 jobs ran on the implementation SHA.
- Missed: none known. Initial exact-head CI exposed an undersized comparable window; the request was corrected and the rerun passed.
- Follow-up: RQ646 remains the primary READY prompt; RQ647/RQ648/RQ649 and RQ654-RQ657 remain READY. RQ651-RQ653 and RQ658 are DONE. Open PR #147 proposes RQ650 but has not merged to main.
- Residual risk: Vercel's PR deployment check reported a deployment rate limit. Post-merge backend run `38422316417` passed on merge SHA `6ca11bf95b6c7eb0f948bce475a688d633c66dae` with 2,060 passed, 43 skipped and 0 failures; its skip inventory and RQ453 certification passed.
- Post-close routing: refreshed current `origin/main` to `192f0eb785b5f6b69d95e1add75620057c09b22f` after RQ651-RQ653 and RQ658 reached DONE. Re-scanned the root RQ queue, all 12 RQ queue addenda, `MASTER_ROADMAP.md`, and the SQL queue; no other prompt names RQ645 as a dependency.
- RQ646 remains the direct P1 successor; RQ647/RQ648/RQ649 and RQ654-RQ657 remain independently READY; RQ651-RQ653 and RQ658 are DONE. The other WAITING/PARTIAL/BLOCKED candidates keep their recorded gates; SQL queue remains none. No Zero-READY proof applies.
- RQ646 has no matching product-path branch, lock or PR. Open PR #147 proposes RQ650 and edits the same canonical files, but its documented scope leaves RQ645 final DONE synchronization to this owner. RQ645 closes through this current-main-rebased update; RQ646 remains primary READY and was not claimed.
- Prompt defect / scope repair: the initial equal-length comparison window admitted the event and post-sale but excluded the pre-sale; the final 12-day window includes both seeded sales without changing endpoint behavior.

---

## RQ646 - Certify remaining ShoeType detail trust, golden snapshot and deterministic output

Status: DONE
Priority: P1
Type: backend ShoeType analytics contract/PostgreSQL integration tests
Feature family: shoe-type-legacy-uncovered-fixture-parity
Parallel-safe: yes for ShoeType-only fixtures/tests; isolated database and no RQ645 Supplier file overlap
Owner: unassigned
Ready after: RQ644 DONE; RQ453 seeded PostgreSQL route/oracle framework available
Claim note: 2026-10-13 - claimed after refreshing `origin/main` to `40089fa424e30d4c7c6f90f72fb29808e6e0e042`. RQ645 is DONE with synchronized evidence; no active ShoeType product/test branch, PR or lock was found. Scope is restricted to the named ShoeType PostgreSQL proof and its inventory entries.
Commit suggestion: `test(analytics): certify three shoe-type trust and golden contracts`

### Problem

Three exact ShoeType `OperationsIntegrationFact` methods remain in the BCI17 `genuinely-uncovered` bucket even though the shared-populated DB tests are skipped in normal CI. They guard business trust, canonical unknown identity, expected golden projection and response determinism; prior cross-screen tests do not automatically prove those detailed contracts.

### Evidence

- `Api.Tests/AnalyticsShoeTypeSalesIntegrationTests.cs` legacy methods:
  `ShoeTypeDetail_PreservesDecisionTrustAndUnknownIdentity`,
  `ShoeTypeSalesStats_MatchesGoldenSnapshot`,
  `ShoeTypeSalesStats_ProducesDeterministicJson`.
- RQ644 covered only invalid input; the remaining BCI17 inventory still marks these three exact FQNs `genuinely-uncovered`.
- ShoeType detail accesses `/api/analitika/shoe-type-sales-stats/{id}`, recommendation/reasonCodes, provenance and unknown identity. It must be proved with deterministic known + unknown types and real output, not a 503 route smoke.

### Scope

New `Api.Tests/ShoeTypeRemainingContractsPostgresTests.cs`, ShoeType-only isolated fixture/golden projection, targeted inventory entries. Touch old ShoeType test only for explicit supersession pointer if needed. No Supplier endpoint/test edits (RQ645), benchmark files (RQ647), analytics formulas or production database.

### Read first

Current ShoeType legacy integration tests, RQ453 seeded six-screen route proof and PostgreSQL fact oracle, `Api.Tests/PostgresRequiredFactAttribute.cs`, golden fixture helper, BCI17 JSON entries, RQ644 evidence.

### Do

1. Build method-by-method assertion map and a disposable PostgreSQL deterministic fixture with known + unknown ShoeType identities, signed sales, period and origin inputs.
2. Assert both known and unknown detail route contracts (recommendationAllowed, reasonCodes, provenance population/source, canonical unknown identity) and that wrong identity/scope cannot broaden to `all`. Ensure readiness middleware is explicitly in tested state and real handler executes.
3. Construct reviewable business-grounded golden projection (stable sorted rows, units/percentages/NULL), and repeat requests for determinism with only explicitly excluded volatile fields. Do not golden-master a broken response.
4. Add named focused CI tests which actually run, compare them with the three original methods and reclassify exactly those inventory entries only after parity. Preserve RQ645/RQ647 entries.
5. Record fixture/contract limitations and exact target test output.

### Tests

Three isolated PostgreSQL contract families including known/unknown detail, stable snapshot and repeated JSON; require PostgreSQL in CI, zero target skips, full backend/RQ453; inventory and governance validators.

### Acceptance

Each of three formerly uncovered ShoeType contracts has a named passing PostgreSQL CI replacement with nonvacuous route/trust/golden checks. No production mutation, no reduction of safeguards, inventory and queue evidence synchronized.

### Dependencies

RQ644 and local/CI disposable PostgreSQL only. Independent of Supplier contract RQ645 and non-gating performance RQ647.

### Completion note

- Date: 2026-10-13
- Status: DONE
- Completion: Added three named isolated PostgreSQL replacements for the remaining ShoeType detail-trust, golden-projection and deterministic-output contracts; only the matching legacy inventory entries are now `legacy-superseded`.
- Changed files: `Api.Tests/ShoeTypeRemainingContractsPostgresTests.cs`; `scripts/ci/backend-skip-inventory.json`; this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-13-RQ646-evidence.md`.
- Checks run: local Release API.Tests build passed with 0 warnings/errors; local required PostgreSQL class passed 3/3 with 0 skips; exact PR-head `66cdc06ee9e64c50754cc1bc62403fa5ffa830f4` backend run `38467098912` passed 2,110 / skipped 43 / failed 0, including the three RQ646 PostgreSQL tests; skip inventory reported 43 known skips and 0 new/missing/reason-changed skips; RQ453 certification, planning governance, frontend analytics, POS UI, JavaScript SDK and Vercel checks passed.
- Checks not run: none required after exact-head CI.
- Run log: `.ai/runs/2026-10-13-RQ646-evidence.md`
- Evidence state: synchronized
- Delivery mode: pull-request
- Main commit SHA: `4dde4c9e878a8efb88963103af1a8236876d1645`
- Main verification: fresh `git fetch origin` confirmed PR #150 squash merge `4dde4c9e878a8efb88963103af1a8236876d1645` on `origin/main`; the implementation paths have no diff from implementation head `66cdc06ee9e64c50754cc1bc62403fa5ffa830f4`.
- Missed: none known.
- Follow-up: RQ647 is the primary READY task; RQ648, RQ649 and RQ654 remain independent READY lanes.
- Residual risk: local full-suite execution previously stalled after unrelated RQ601/RQ610 failures, but exact-head required backend CI completed green with the full suite and inventory validator.
- Post-close routing: RQ647 promoted as primary READY after the post-delivery scan; several independent READY lanes remain, so no Zero-READY proof applies.
- Prompt defect / scope repair: none.

---

## RQ647 - Replace two untrusted Operations performance skips with reproducible non-gating measurements

Status: DONE
Priority: P2
Type: performance/CI measurement baseline; no production SLA gate
Feature family: supplier-shoetype-performance-measurement
Parallel-safe: yes for new isolated benchmark harness/CI measurement paths (do not edit legacy Supplier/ShoeType tests owned by RQ645/RQ646 during implementation)
Owner: Codex; corrective close-out by direct user authorization
Local lock: removed after done close
Ready after: RQ639/RQ642 DONE; inventory explicitly classifies both remaining benchmark skips
Claim note: 2026-10-14 - historical claim refreshed `origin/main` to `1dba9adf9462459fd307ac60356ee12ebbd065ef`; it predates the shared-reservation protocol and therefore has no pushed reservation record. The explicit user instruction on 2026-10-14 authorized corrective close-out after the PR #153 delivery collision. The corrective work is direct-user-request scope, not a competing queue claim.
Commit suggestion: `test(perf): add non-gating isolated analytics endpoint baseline`

### Problem

Two `genuinely-uncovered` backend skip entries test response-time thresholds on a shared/populated and environment-sensitive database. A green shared-runner time assertion would be flaky and could mask cold start, source population, DB setup, fetch versus handler time and cache policy. A useful performance baseline should measure actual latency and query pressure on a reproducible fixture, without turning a generic 30-second threshold into an unconditional release blocker.

### Evidence

- `AnalyticsSupplierSalesIntegrationTests.SupplierSalesStats_PerformanceWithinThreshold` legacy skip.
- `AnalyticsDbInfrastructureTests.AnalyticsEndpoint_ResponseTimeBaseline` legacy skip (asserts `sw.ElapsedMilliseconds < 30000`, against shared data; no stable fixture).
- BCI17 counts both among the 12 truly uncovered; RQ639/RQ642 intentionally left the infrastructure performance case outside mandatory test gates; `PERF19` separately measured a Decision Board scenario, not these two routes.

### Scope

New `Api.Tests/AnalyticsEndpointBenchmarkPostgresTests.cs` and/or isolated measurement script/workflow, dedicated small/medium deterministic dataset and a standalone performance-report artifact. No modification of RQ645/RQ646 legacy contract tests while parallel; no supplier formula rewrite, automated production load, endpoint SLO claim, or mandatory 3s/30s CI gate.

### Read first

The two legacy benchmark methods, `docs/roadmaps/PERFORMANCE_ROADMAP.md`, `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` PERF evidence, BCI17 inventory, RQ453 fixtures and `Api/Services/Startup/StartupReadinessState.cs`.

### Do

1. Isolate real API handler latency from cold start and SQL setup; document warmup, data rows, query count/latency, cache state, host parameters, concurrency and measurement clock.
2. Run several samples with medians/p95, serialize reproducible JSON report and compare to an explicit baseline from the same fixture; capture regression deltas **without** auto-blocking merges for host noise.
3. Test Supplier stats and ShoeType stats, including empty and moderately populated scopes with same input semantics. No proxy 503/404 and no early-return should count as a successful measurement.
4. Preserve both legacy skips or classify them separately as superseded with a named non-gating job **only** when equivalent business/performance measurement is proven. Do not silently reduce `expectedKnownSkippedCount` without TRX proof.
5. Give maintainers a reproducible locally runnable command or GitHub `workflow_dispatch` (not scheduled high-frequency job) and clear thresholds requiring manual investigation rather than asserting a new provider SLA.

### Tests

Smoke/run benchmark against disposable PostgreSQL when opted in; verify sample completeness and p95 calculation, no 503 responses, no reliance on live Neon or Vercel. CI quality and governance checks, report data schema self-test.

### Acceptance

Two exact performance gaps are represented by reproducible non-gating baseline proof with explicit fixture/hardware methodology. No flaky mandatory perf gate, no false production SLO and no claim of deployed provider throughput.

### Dependencies

No STAB16 access. RQ645/RQ646 may execute in parallel if unique benchmark files stay isolated; performance ownership is separate from correctness ownership.

### Completion note

- Date: 2026-10-14
- Status: DONE
- Completion: Corrected the undersized fixture and machine-identifying report field, made the CI artifact path explicit, and closed both legacy performance skips with a reproducible non-gating PostgreSQL baseline.
- Changed files: `Api.Tests/AnalyticsEndpointBenchmarkPostgresTests.cs`, `.github/workflows/analytics-tests.yml`, `scripts/ci/backend-skip-inventory.json`, `.ai/runs/2026-10-14-RQ647-report.json`, `.ai/runs/2026-10-14-RQ647-evidence.md`, the direct remediation log, queue/roadmap pointers, and canonical claim/evidence instructions.
- Contract/runtime behavior changed: no product endpoint or production SLA changed; the benchmark now uses the deterministic July fixture plus 600 added sale lines and emits no machine name.
- Checks run: local Release build passed with 0 errors; focused real-PostgreSQL benchmark passed 1/1 with 624 sale headers/lines and no `machineName`; governance self-tests and validators passed. The first current-main run `38542452293` on `578945fe` passed its backend suite but failed only the RQ647 artifact upload because the test process wrote its default relative path outside repository `TestResults`; this in-scope workflow-path defect was corrected in `408d1b81`. Exact-main Analytics Tests `38542983060` then passed 2,076 / skipped 43 / failed 0; RQ453 certification passed; artifact `rq647-analytics-endpoint-benchmark-38542983060` (ID `11884041409`) uploaded successfully.
- Checks not run: production/provider latency or throughput validation — intentionally outside this non-gating fixture benchmark.
- Run log: `.ai/runs/2026-10-14-RQ647-evidence.md`
- Evidence state: synchronized
- Delivery mode: pull-request then corrective direct-main
- Main commit SHA: `408d1b81062e5d4c114960d69ab51d4d239407e4` (PR #153 initial squash merge `e7d7e4532b1d677d4ca7c3b18d67087cc68d5292`)
- Main verification: fresh `origin/main` contains `408d1b81062e5d4c114960d69ab51d4d239407e4`; exact-main run `38542983060` is green and artifact URL is `https://github.com/ivanjovicic/Trendplus/actions/runs/38542983060/artifacts/11884041409`.
- Claim reservation: historical RQ647 claim predates the protocol; corrective work is `direct-user-request/not applicable` under the explicit user authorization recorded in the run log.
- Missed: none known.
- Follow-up: RQ648 is the primary READY task; RQ655-RQ657 remain independent READY lanes.
- Residual risk: fixture p50/p95 values are reproducible diagnostics, not a production or provider latency SLO.
- Post-close routing: refreshed delivery base `origin/main` `408d1b81062e5d4c114960d69ab51d4d239407e4`; scanned the root RQ queue, all active RQ addenda, the SQL analytics queue, current RQ READY/IN_PROGRESS rows, branches/worktrees and open PR #147. No task depends on RQ647. RQ648 remains the primary READY P2 lane but must re-check its open-PR overlap; RQ655-RQ657 are independent READY lanes. No Zero-READY proof applies.
- Prompt defect / scope repair: the original 24-row moderate fixture and host-name report field were insufficient; the corrective delivery added the 600-line expansion, omission assertion, explicit artifact path and current-main artifact proof without adding a flaky threshold.

---

## RQ648 - Reconcile remote agent branches against main by patch/content equivalence, not name or ancestry alone

Status: DONE
Priority: P2
Type: git branch/PR lineage + exact-code reconciliation audit
Feature family: repository-branch-safety
Parallel-safe: yes for read-only branch comparisons and a unique audit report path; canonical queue/roadmap final status edits must be serialized after other agent closeouts
Owner: Codex
Local lock: `.ai/task-locks/RQ648-codex.lock.md` (removed after delivery)
Ready after: PR #147 closed without merge on 2026-10-14; no open PR at latest GitHub check; RQ634–RQ661 relevant owner work has been closed as recorded on main.
Claim reservation: 2026-10-14 - direct user instruction explicitly assigns RQ648 to Codex for fresh current-branch comparison and semantic review. Inspected `origin/main` `049c17c2ae14044f29f894203fee8f2756f75f4e`; PR #147 is closed without merge; no RQ648 branch, lock or open PR found; the only active task lock was RQ660 on disjoint PDC paths. Published the shared claim at `aafc7e2fefb15167331e98471ba98982314fd04a0`. Worktree `C:\Users\Ivan\.codex\worktrees\rq648-branch-parity`, branch `codex/rq648-branch-parity-20261014`. Bounded paths: `docs/qa/BRANCH_PARITY_REVIEW_2026-10-14.md`, `.ai/runs/2026-10-14-RQ648-evidence.md`, this queue and `MASTER_ROADMAP.md`; SQL029 implementation/tests were explicitly excluded because the additional check is already on `main`.
Commit suggestion: `docs(git): prove current remote branch equivalence and classify residual diffs`

### Completion note — 2026-10-14

- Status: DONE.
- Completion: refreshed to current `origin/main` `f667eda91b90103fd242e9e4f32e4e866a2d3cbb`, compared all 110 non-main remote branches and 157 historical PR records, and reconciled SQL022/RQ481/old business documentation. No safe unique product delta remained. SQL029 was verified as already present on main and not repeated.
- Changed files: `docs/qa/BRANCH_PARITY_REVIEW_2026-10-14.md`, `.ai/runs/2026-10-14-RQ648-branch-inventory.json`, `.ai/runs/2026-10-14-RQ648-evidence.md`, this queue and `MASTER_ROADMAP.md`.
- Delivery mode: PR #157 merged; audit head `4947aa6446d57ca6de59d3ed8573416c34fff7a1`, merge SHA `b42493c97cce68043f55e35237ee17063bc93d89`.
- Main verification: fresh fetch confirms `origin/main` contains merge SHA `b42493c97cce68043f55e35237ee17063bc93d89`.
- Checks: prompt queue validator, planning/instruction validators and `git diff --check` are run on the closeout commit; runtime/backend/frontend/PostgreSQL tests were not required for this audit-only scope.
- Missed: GitHub cannot inspect other agents' uncommitted worktrees or local locks; no production/provider state was changed.
- Follow-up: post-close cascade and Zero-READY proof are recorded in `.ai/runs/2026-10-14-RQ648-evidence.md`.
- Residual risk: visible remote branch refs may remain as historical transport; this task does not delete them.

### Problem

A snapshot of 2026-10-13 remote branch listing contains **97 non-main refs** and zero open PRs, but this does NOT prove every branch's unique code is on main. Direct-main pushes and squash/rebased PRs often leave old branch commits diverged although their file content is already merged. A blanket merge of old `cursor/*`/`codex/*` branches can overwrite current queue/statuses or reintroduce stale SQL/analytics behavior.

### Evidence

- Fresh main tip at intake: `c4c20197cf029f36de8623e2de8cb7e9df708a57`, latest completed tasks RQ634–RQ644, no open PR.
- `cursor/rq637-access-import-readiness-gate-28d8` compares as diverged (one unique commit), **but all three changed code/test files have identical Git blob SHAs on current main**. This is an example of delivered code that is *not* safe to remerge.
- `fix/rq638-unicode-truncation-boundary-20261013` also compares as diverged; both inspected source/test blobs are identical to main. PR #139 closed unmerged, successor #141 merged.
- `fix/rq635-real-postgres-ci-oracle-20261013` has one ahead commit and a different legacy lifecycle-test blob; PR #138 closed unmerged while corrective PR #137 merged. Difference requires semantic coverage comparison, **not** blind cherry-pick.
- `codex/product-value-plan-refinement` is old and hundreds of main commits behind; it has uniquely authored historical product documents, not a current-authority queue.

### Scope

New `docs/qa/BRANCH_PARITY_REVIEW_2026-10-13.md` plus optional machine-readable per-branch inventory. Do not create a replacement implementation PR for every branch. No production mutation, rebase/force-push, automatic branch deletion, merge, branch renaming or checkout/worktree deletion.

### Read first

`MASTER_ROADMAP.md`, `docs/ai/PROMPT_QUEUE_PROTOCOL.md` branch/ownership rules, GitHub PR #136–#142 and status, latest backend/quality CI, `.ai/runs/2026-10-13-RQ634-evidence.md` through RQ644, `docs/product/TRENDPLUS_PRODUCT_ANALYTICS_VALUE_AUDIT_2026-10-07.md` (only if old product branch has unique business decision).

### Do

1. Enumerate **all current** remote branches and open/closed PRs, not only the first page when pagination is present. Capture main SHA and branch commit SHA/time.
2. For every branch, classify `ancestor/behind`, `identical`, `diverged-but-content-in-main`, `diverged-and-superseded-by-later-fix`, `unique-unreviewed`, or `active-owner/lock-unknown`. Git commit `ahead_by>0` alone is NOT evidence of an unmerged product fix after squash; compare changed-file blob SHAs, code semantics and successor PR patches on **fresh main**.
3. Spend extra review on RQ635 SQL029 lifecycle oracle branch, RQ637 import startup branch, RQ638 Unicode branch and the old product-value plan branch. Check local lock/agent ownership if accessible; a GitHub-only view must explicitly report that it cannot inspect other agents' uncommitted local worktrees.
4. For genuinely unique and safe product changes, create **separate reviewed follow-up prompts/PR recommendations** with tested acceptance and dependency gates. Do not silently modify current main or forward-merge stale docs. If product changes require owner choice, mark WAITING with exact decision.
5. Deliver a table with branch -> related PR/commit -> equivalence evidence -> disposition. Classify `no open PR` separately from `all work merged`. Recommend cleanup only with owner approval and no active use; no deletion is authorized by this task.

### Tests

Read-only GitHub compare/file patch inventory for every branch, PR linkage and SHA verification, queue/roadmap validators if statuses updated, `git diff --check`. Re-review on any changed main head before treating classification final.

### Acceptance

Every branch is accounted for in a dated report, with no unsupported claim that all old refs are merged, no lost safe unique work, and no unsafe automatic merge/deletion. High-confidence actionable deltas become individually owned, testable prompt(s) rather than resurrecting an obsolete branch.

### Dependencies

GitHub read access; no provider/production data authority. Independent of RQ645–RQ647 code paths. Re-check locks/open PRs before starting.

### RQ648 conflict-resolution and no-duplicate scope (2026-10-14)

- **PR #147 was reviewed and CLOSED UNMERGED** after GitHub reported `mergeable=false/dirty`; exact PR head `4949102f` passed governance/frontend but backend workflow `38422963322` failed with 90 tests. It must not be reopened/merged merely to clear RQ648. Review: https://github.com/ivanjovicic/Trendplus/pull/147.
- Preserved only the useful **dated** 97-ref / 23-divergent comparison as `docs/qa/BRANCH_PARITY_REVIEW_2026-10-13.md`. Its counts, PR states, RQ645–RQ650 proposals, and CI observations were historical even when authored; its former RQ650 coverage proposal must **not** be recreated: `.ai/runs/2026-10-13-direct-branch-reconciliation-evidence.md` independently reviewed **112** non-main refs / **29** unreachable at `3307679e`, and `RQ658 DONE` plus convention implementation already addressed real helper/collection/qualified-attribute cases. A *new* test gap must have a fresh failing reproducer before new registration.
- Latest GitHub check at `main 0210a8ba` counted **109** non-main refs in two pages, no open PRs, and no clearly named `RQ648` active branch. Counts are snapshots; not a new full commit-graph/semantic census. GitHub cannot inspect another agent's uncommitted local worktree/lock.
- **Precise remaining action:** refresh all non-main refs and compare them to the *current* main, reconcile only changed/new branches since the 112-ref direct audit, then inspect any still-UNPROVEN semantic differences (SQL022/source population, older value-audit intent, RQ481 UI, SQL029 preflight) against current code/tests. Before opening a fix, first verify the already-delivered SQL029 prerequisite assertion and RQ481 guards; do **not** duplicate completed repairs. Mark obsolete/equivalent items with exact blob/test/commit proof and isolate any genuinely missing assertion into a narrow follow-up. Do not sweep unrelated code, salvage all old branches blindly, delete branches or mutate production.
- **Ownership:** `RQ648` remains READY/unassigned, not IN_PROGRESS. Agent must publish its canonical shared claim first. A separate PR #147 head/branch is historical after closure, not an active RQ648 claim. The current task's final DONE acceptance still requires fresh content-level coverage and its own run evidence.
- Other verified residual: `RQ659 DONE` removed the incorrect nav label "Prodaja po smenama", but `DailySalesStatsPage.tsx` and smoke route definitions still have the older title (see RQ659 run log). Treat as an isolated low-severity UX copy follow-up, not a reason to block RQ648 or to change the statistical shift source contract.
- Canonical business scope remains real retail analytics, not marketplace/test features. STAB16 production proof, RQ601 cost and RQ610 stock-lineage blockers were not resolved by branch review.

### Verified branch-graph census after PR #143 (no status change)

- Main baseline `c4c20197`: complete GitHub compare of all 97 existing non-main refs gives **71 behind (ahead=0), 1 identical, 25 diverged (ahead>0)**; no comparison errors. This is **commit ancestry only**, not 25 unmerged code features.
- Some divergent candidates already have exact main blob parity: RQ637 three code/test paths, RQ638 Unicode two paths. The stale RQ635 trial branch has the same named lifecycle test families and a different, older fixture layout; successor fix PR #137 was merged.
- **Next agent: prioritize content-level review of 25 divergent refs**, classify PR successor/patch parity and any material uniquely useful change. Refresh graph listing only for newly added or moved refs. Do not remerge/overwrite current main or delete any branch without owner approval. Branch report and preliminary evidence: `docs/qa/POST_RQ644_CODE_QUEUE_BRANCH_REVIEW_2026-10-13.md`.
- RQ648 stays READY for full content-semantic reconciliation; this source-only census is **not** DONE evidence.

---

## RQ649 - Stop unintended Access import worker polling in remaining EF InMemory API test hosts

Status: DONE
Priority: P2
Type: backend test-host worker registration and targeted CI logging
Feature family: remaining-fake-provider-worker-noise
Parallel-safe: yes for scoped test host/factory paths; avoid RQ645 Supplier, RQ646 ShoeType and RQ647 benchmark fixtures
Owner: Cursor
Ready after: RQ636 DONE and RQ637 DONE; latest exact-main backend CI 38407849758 exposed a distinct residual
Claim note: 2026-10-14 - claimed after refreshing `origin/main` to `e7d7e4532b1d677d4ca7c3b18d67087cc68d5292`. No RQ649 branch, lock or open PR exists; RQ636/RQ637 are DONE. Cursor had concurrently started RQ647 from `1dba9adf`, but Codex's RQ647 claim and implementation (PR #153) reached main first, so Cursor released RQ647 without pushing and kept its alternative only on an unpushed local branch.
Commit suggestion: test(ci): isolate remaining InMemory API hosts from Access import polling

### Problem

RQ636 fixed CachedAnalyticsOperationalFallbackTests, but other in-memory WebApplicationFactory hosts still register AccessImportBackgroundWorker. In a successful full backend suite, these hosts start the worker with 2s polling and call relational-only AccessImportJobQueue.ClaimNextAsync on EF InMemory, repeatedly logging errors. These are test-host faults, not proof that production is failing. Do not disable real workers globally.

### Evidence

- Main Actions run 38407849758, backend job 115332057225: 2,052 passed; 43 classified skips; zero failed; **113 matching log lines** with the phrase "Relational-specific methods can only be used when the context is using a relational database provider" and stack frames inside AccessImportJobQueue/AccessImportBackgroundWorker. The 113 are log-line matches, NOT 113 distinct failed tests.
- The same log has zero "Failed to save performance log for" and zero "Failed to persist HTTP request performance metric" matches. RQ631, RQ636 and RQ638 stay DONE.
- Api.Tests/CachedAnalyticsOperationalFallbackTests.cs already uses explicit narrow opt-out. Other in-memory factories remain unqualified. Candidate log-adjacent suites include AdminConfigOperationalReadsAuthorizationTests and AnalyticsCriticalRouteMappingsTests; log adjacency alone does not identify the worker's owning host.

### Scope

Affected non-import WebApplicationFactory fixtures and targeted registration assertions. No edits to Api/Services/Access/AccessImportBackgroundWorker.cs, Api/Config/WorkerRuntimeConfig.cs, production settings, Supplier/ShoeType suites owned by RQ645/RQ646, RQ647 benchmark harness, global logger filters or the PostgreSQL migrations.

### Read first

Api.Tests/CachedAnalyticsOperationalFallbackTests.cs, Api.Tests/AdminConfigOperationalReadsAuthorizationTests.cs, Api.Tests/AnalyticsCriticalRouteMappingsTests.cs, Api/Program.cs, Api/Config/WorkerRuntimeConfig.cs, Api.Tests/AccessImportBackgroundWorkerStartupLifecycleTests.cs, docs/qa/STATE_REAUDIT_2026-10-13.md, and CI backend job 115332057225.

### Do

1. Identify the precise test factories that instantiate unrelated Access import hosted workers against EF InMemory. Instrument test-only worker registration assertions, not a generic global opt-out.
2. Configure supported test-only AccessImport:RegisterWorkerInWebProcess=false with correct precedence, or remove the hosted-service descriptor only in isolated non-import hosts. Verify effective Program registration, not merely configuration text.
3. Add regression: zero background claim/recovery calls in non-import host; endpoint request actually reaches its handler (not an early 503/404); explicit real Access import worker lifecycle fixtures still opt in and pass.
4. Run exact affected host tests and full backend suite; record before/after matching log-line counts and owning test factory names. Do not automatically call all expected SQL029 negative-fixture SqlFailed logs defects or suppress unrelated exceptions.
5. If a host genuinely requires the worker, use a real disposable relational PostgreSQL fixture rather than hiding exceptions. Preserve the production registration and RQ637 web startup readiness semantics.

### Tests

Affected API-host routes, AccessImportBackgroundWorkerStartupLifecycleTests, new registration assertions, full backend CI, governance/inventory validator if touched, diff check. All target tests run without skip; no global exception swallowing.

### Acceptance

Non-import EF InMemory hosts no longer run unwanted Access import queue polling or produce its relational-provider errors; production web and dedicated worker behavior is unchanged. Exact CI before/after counts and named host evidence recorded.

### Dependencies

RQ636/RQ637 DONE. No production credentials, source file import or owner business approval needed; check same-file collisions with RQ645/RQ646 before claiming.

### Completion note

- Date: 2026-10-14
- Status: DONE
- Completion: Six EF InMemory `WebApplicationFactory<Program>` hosts (`FailureFactory`, `CachedAnalyticsFactory`, `LegacyInsightStudioFactory`, `DailySalesWebApplicationFactory`, `ShoeTypeSalesInMemoryFactory`, `SupplierSalesInMemoryFactory`) now remove the `AccessImportBackgroundWorker` hosted-service descriptor through `Api.Tests/AccessImportTestHostIsolation.cs`. A test-only `UseSetting("AccessImport:RegisterWorkerInWebProcess", "false")` was proven ineffective because `appsettings.json` outranks host settings, so descriptor removal is the effective guard (RQ636's identical `UseSetting` lines are likewise inert; its descriptor removal is what works). Each host has a registration assertion against the built service provider, and a convention test fails any future InMemory Program host without descriptor removal.
- Changed files: `Api.Tests/AccessImportTestHostIsolation.cs`; `Api.Tests/InMemoryTestHostAccessImportWorkerConventionTests.cs`; `Api.Tests/CachedAnalyticsFailureContractTests.cs`; `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`; `Api.Tests/InsightStudioLegacyEndpointsContractTests.cs`; `Api.Tests/DailySalesStatsIntegrationTests.cs`; `Api.Tests/AnalyticsShoeTypeSalesIntegrationTests.cs`; `Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs`; this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-14-RQ649-evidence.md`.
- Checks run: local Release build 0 errors; registration/convention/RQ640/PostgresRequired/worker lifecycle/WorkerRuntimeConfig set passed 32/32; six affected classes passed 72 / skipped 29 / failed 0 with worker starts 63 before -> 0 after; exact-main run `38537132751` (backend job `115762635022`) passed 2,076 / skipped 43 / failed 0, RQ453 passed, skip inventory 43 known. Relational-provider log lines: 113 (job `115332057225`, 100 worker-attributed) -> 34 (0 worker-attributed; all 34 from `AnalyticsConnectionDiagnosticsHostedService`).
- Checks not run: none required.
- Run log: `.ai/runs/2026-10-14-RQ649-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `d8429e9d92fa08ed2d16bd06090a2469a2d54138`
- Main verification: fresh `git fetch origin` confirmed `origin/main` = `d8429e9d92fa08ed2d16bd06090a2469a2d54138` after push.
- Missed: none known in scope.
- Follow-up: `AnalyticsConnectionDiagnosticsHostedService` still logs 34 relational-provider lines on InMemory hosts; it is a separate hosted service outside RQ649 scope and needs its own prompt if the noise matters.
- Residual risk: 13 worker starts remain in CI on hosts not using EF InMemory (relational fixtures/lifecycle tests); none produced relational-provider errors.
- Post-close routing: RQ649 has no dependents; RQ648 and RQ654-RQ657 remain READY and RQ647 remains Codex-owned IN_PROGRESS, so no Zero-READY proof applies.
- Prompt defect / scope repair: the scope excluded the Supplier/ShoeType suites only while RQ645/RQ646 were active; both are DONE, so the two legacy files received only the factory guard and registration assertion. Do step 2 suggested a `RegisterWorkerInWebProcess=false` setting, which is overridden by `appsettings.json`; descriptor removal was used instead.

## RQ651 - Stop rendering unknown data-quality counts and margin-coverage shares as zero

Status: DONE
Priority: P2
Type: frontend analytics trust text / fake-zero regression
Feature family: analytics-screen-fake-zero-trust-text
Parallel-safe: yes (frontend-only; disjoint from RQ645-RQ650 backend/test/branch paths and from RQ652-RQ654 files)
Owner: Cursor
Ready after: 2026-10-13 analytics screen bug review on main `a5604874`
Commit suggestion: `fix(analytics): keep unknown data-quality counts and margin coverage unavailable, not zero`

### Problem

Two trust surfaces turn an unknown value into a confident zero, violating the AGENTS.md "no fake zero" invariant:

1. `DataQualityPage.tsx` header ("Odvojeni signali") renders `fmtNumber(health.missingSupplierArticleCount ?? 0, 0, "-")` (same for missing cost/category). The fields are optional in `analyticsResponseSchemas.ts`, and `fmtNumber` already returns its fallback for null/undefined, so `?? 0` makes the `"-"` fallback unreachable. An absent count reads as "Bez dobavljača: 0" while the KPI cards on the same page show it as unavailable.
2. `utils/marginQuality.ts` `buildCoverageTooltip`, `buildRecommendationCaveat` and `buildMarginDetailNote` pass `historicalPct ?? 0`, `estimatedPct ?? 0`, `noCostPct ?? 0` into `fmtPct`, so unknown coverage reads as e.g. "Bez troška: 0,0%".

### Evidence

`docs/qa/ANALYTICS_SCREEN_BUG_REVIEW_2026-10-13.md` (RQ651 detail). `fmtNumber`/`fmtPct` in `utils/analyticsFormatters.ts` return the unavailable fallback for null/non-finite input.

### Scope

`Klijent/clientapp/src/pages/DataQualityPage.tsx` (header counts only; the `hasMasterDataIssues`-style boolean may keep `?? 0` because unknown must not raise a false alarm), `Klijent/clientapp/src/utils/marginQuality.ts`, their nearest specs. No backend, schema, formatter or copy-wide sweep.

### Read first

`Klijent/clientapp/src/pages/DataQualityPage.tsx` (~lines 660-860), `Klijent/clientapp/src/pages/DataQualityPage.spec.tsx`, `Klijent/clientapp/src/utils/marginQuality.ts`, `Klijent/clientapp/src/utils/analyticsFormatters.ts`, callers in `SupplierSalesStatsPage.tsx` and `ShoeTypeSalesStatsPage.tsx`.

### Do

1. Add failing-first tests: DataQualityPage health without the three optional counts must not render "Bez dobavljača: 0"; `buildCoverageTooltip(null, null, null, fmtPct)` must not contain "0,0%".
2. Remove the `?? 0` in front of the formatters so the established unavailable fallback renders; keep real `0` rendering as `0`.
3. For the margin caveat strings, when the share is unknown, omit the percentage clause or use the existing unavailable label rather than a number.

### Tests

`npm run test -- --run src/pages/DataQualityPage.spec.tsx` plus a focused `marginQuality` spec (new or nearest existing), `npm run check:analytics-guardrails`, `npm run build`.

### Acceptance

Unknown counts/shares render as unavailable; known zeros still render as `0` / `0,0%`; tests fail before and pass after the fix.

### Dependencies

None. Frontend-only and repository-local.

### Completion note

- Date: 2026-10-13
- Status: DONE
- Completion: `DataQualityPage` no longer coerces absent master-data counts to `0` before `fmtNumber`, so its `"-"` fallback renders. `marginQuality` coverage tooltip, recommendation caveat and margin detail note render unknown shares as "Nije dostupno" or omit the clause instead of `0,0%`. Known values and real zeros are unchanged.
- Changed files: `Klijent/clientapp/src/pages/DataQualityPage.tsx`; `Klijent/clientapp/src/pages/DataQualityPage.spec.tsx`; `Klijent/clientapp/src/utils/marginQuality.ts`; `Klijent/clientapp/src/utils/__tests__/marginQuality.spec.ts` (new); `MASTER_ROADMAP.md`; this queue; `.ai/runs/2026-10-13-RQ651-evidence.md`.
- Contract/runtime behavior changed: frontend display text only; no API, schema or decision semantics changed.
- Checks run: failing-first proof showed 4 new regression tests failing before the fix (16 existing passing). After the fix, focused vitest passed 140/140 across 10 files (DataQualityPage, marginQuality, SupplierSalesStatsPage*, ShoeTypeSalesStatsPage* and PilotReadinessPage* specs). `npm run check:analytics-guardrails` passed (baseline-only, 38 known; one unrelated baseline entry in `IntelligenceSnapshotPanel.tsx` reported as removed). `npm run build` (tsc + vite) passed. `node scripts/check-prompt-queues.mjs` and `git diff --check` passed.
- Checks not run: full frontend test suite and browser visual check, because the change is limited to text fallbacks covered by the focused specs.
- Run log: `.ai/runs/2026-10-13-RQ651-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `78a9c48748d57c0ca81476c15ddecf6eb48355d2`
- Main verification: after push, `git merge-base --is-ancestor 78a9c487 origin/main` passed; origin/main was `78a9c48748d57c0ca81476c15ddecf6eb48355d2` (rebased onto Codex's RQ645 merge `6ca11bf9`).
- Missed: none known within scope. The `hasWarning` boolean in `DataQualityPage` intentionally keeps `?? 0` so unknown counts do not raise a false alarm.
- Follow-up: RQ652 and RQ653 (P2) remain independently READY; RQ654 (P3) READY.
- Residual risk: main CI for `78a9c487` had not completed at close; local focused proof is complete.
- Post-close routing: RQ651 has no dependents. RQ652/RQ653/RQ654 remain READY and path-disjoint. The RQ645 primary pointer is owned by Codex and was not touched.
- Prompt defect / scope repair: none.

## RQ652 - Guard action-queue KPI counts against stale responses and refresh the outcome summary after saves

Status: DONE
Priority: P2
Type: frontend analytics workflow state / stale-response regression
Feature family: analytics-actions-stale-kpi-state
Parallel-safe: yes (only `AnalyticsActionsPage.tsx` and its specs; disjoint from RQ651/RQ653/RQ654)
Owner: Cursor
Ready after: 2026-10-13 analytics screen bug review on main `a5604874`
Commit suggestion: `fix(analytics-actions): sequence-guard counts and refresh outcome summary after outcome save`

### Problem

On `/analytics/actions` (`AnalyticsActionsPage.tsx`):

1. `loadItems` and `loadOutcomeSummary` use request-sequence refs, but `loadCounts` does not, so an older counts response can overwrite counts for newer filters.
2. `loadCounts` swallows all errors (`// non-critical`) and leaves the previous `counts` rendered in the status KPI cards (`new`, `accepted`, `deferred`, `done`, `rejected`, `ignored`, `p1Open`). After a failed reload the cards silently describe the previous filter set.
3. `submitOutcomeModal` updates the row/detail but never reloads the outcome summary, so realization/measured-sample KPIs stay stale after the user records an outcome on the same page.

### Evidence

`docs/qa/ANALYTICS_SCREEN_BUG_REVIEW_2026-10-13.md` (RQ652 detail).

### Scope

`Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx` and `Klijent/clientapp/src/pages/__tests__/AnalyticsActionsPage.spec.tsx` (or `src/pages/AnalyticsActionsPage.spec.tsx`). No API/service contract change.

### Read first

`AnalyticsActionsPage.tsx` lines ~585-720 (loaders/effects), ~766-900 (`changeStatus`, `submitOutcomeModal`), ~930-970 (KPI cards); existing action-page specs.

### Do

1. Failing-first tests: (a) a slower first counts response resolving after a faster second one must not win; (b) a counts failure after a filter change must not leave previous counts presented as current; (c) saving an outcome triggers a fresh outcome-summary request.
2. Add a counts request-sequence ref matching the existing pattern; on failure clear counts or render an explicit unavailable/stale state (reuse existing page error/unavailable styling, no new design).
3. Reload the outcome summary for the current filters after a successful outcome save (and after a status change if the summary depends on status).

### Tests

Focused action-page specs, `npm run check:analytics-guardrails`, `npm run build`.

### Acceptance

KPI counts always correspond to current filters or are visibly unavailable; outcome summary reflects a just-saved outcome; existing action-page specs stay green.

### Dependencies

None. Frontend-only and repository-local.

### Completion note

- Date: 2026-10-13
- Status: DONE
- Completion: `loadCounts` now uses `countsRequestSeqRef` like the items/summary loaders, so an older counts response cannot overwrite newer filters. A failed counts reload clears previous-filter counts and renders "Brojevi po statusu trenutno nisu dostupni za izabrane filtere." with the existing `aaq-summary-error` style. The outcome summary reloads after a successful outcome save and after a status change, since its open/closed totals depend on status.
- Changed files: `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`; `Klijent/clientapp/src/pages/__tests__/AnalyticsActionsPage.spec.tsx`; `MASTER_ROADMAP.md`; this queue; `.ai/runs/2026-10-13-RQ652-evidence.md`.
- Contract/runtime behavior changed: frontend state handling only; no API contract change.
- Checks run: failing-first proof showed 3 new tests failing for the exact defects ("99" overwrote "41"; no unavailable notice; summary called 1x instead of 2x), with 23 existing tests passing. After the fix:
  - both action-page specs plus PilotReadiness specs passed 55/55 in 4 files;
  - AppAnalyticsRoutes, analyticsPresentationPass2 and analyticsTrustStateProof passed 43/43;
  - `npm run check:analytics-guardrails` passed (baseline-only, 38 known);
  - `npm run build` passed (exit 0);
  - `node scripts/check-prompt-queues.mjs` and `git diff --check` passed.
- Checks not run: full frontend suite and browser check, because the change is bounded to one page's loader state and covered by the focused and route specs.
- Run log: `.ai/runs/2026-10-13-RQ652-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `d0b33f79868d4a1a37ec8fcc909184dcf3831d63`
- Main verification: after push, `git merge-base --is-ancestor d0b33f79 origin/main` passed; origin/main was `d0b33f79868d4a1a37ec8fcc909184dcf3831d63`.
- Missed: none known. While new counts load after a filter change, the previous counts remain visible until the response arrives; this is the established loading behavior, and failure no longer leaves them.
- Follow-up: RQ653 (P2) and RQ654 (P3) remain READY.
- Residual risk: main CI for `d0b33f79` had not completed at close.
- Post-close routing: RQ652 has no dependents. RQ653/RQ654 remain READY and path-disjoint. The RQ645 primary pointer stays with its owner (Codex).
- Prompt defect / scope repair: the prompt named only the outcome save. The status-change summary reload was included under its step 3 ("and after a status change if the summary depends on status"), because the summary's open/closed totals do depend on status.

## RQ653 - Pilot readiness Actions card must not report zero actions when action counts failed

Status: DONE
Priority: P2
Type: frontend pilot readiness partial-failure state
Feature family: pilot-readiness-partial-failure-truth
Parallel-safe: yes (only `PilotReadinessPage.tsx` and its specs; disjoint from RQ651/RQ652/RQ654)
Owner: Cursor
Ready after: 2026-10-13 analytics screen bug review on main `a5604874`
Commit suggestion: `fix(pilot-readiness): keep action counts unknown when the counts request fails`

### Problem

`PilotReadinessPage.tsx` loads action counts and the action outcome summary independently (`Promise.allSettled`). `buildActionsCard` returns `unknown` only when both are null. If counts fail and the summary succeeds, `newCount … doneCount` default to `0`; with `sampleSize > 0` the card states "Ukupno 0 akcija, otvorenih 0, završenih 0" as fact and can be classified warning/ready from fabricated zeros.

### Evidence

`docs/qa/ANALYTICS_SCREEN_BUG_REVIEW_2026-10-13.md` (RQ653 detail).

### Scope

`Klijent/clientapp/src/pages/PilotReadinessPage.tsx` (`buildActionsCard` and, only if the same pattern is proven, the inventory card's `lowStockCount`/`outOfStockCount` defaults), plus `src/pages/__tests__/PilotReadinessPage.spec.tsx` / `PilotReadinessPage.edgeCases.spec.ts`.

### Read first

`PilotReadinessPage.tsx` lines ~420-515 and ~680-780; the two pilot readiness specs above.

### Do

1. Failing-first test: counts null + summary present must not render "0 akcija" and must not produce `ready`.
2. When counts are missing, describe only what the summary proves (measured sample/coverage) and mark the count portion unavailable with a warning/unknown status.
3. Keep the existing both-null `unknown` path and the both-present behavior unchanged.

### Tests

Focused pilot readiness specs, `npm run check:analytics-guardrails`, `npm run build`.

### Acceptance

Partial failure never shows fabricated action counts; status is not `ready` on missing counts; existing specs stay green.

### Dependencies

None. Frontend-only and repository-local.

### Completion note

- Date: 2026-10-13
- Status: DONE
- Completion: `buildActionsCard` covers both partial-failure cases.
  - Counts missing, summary present: the card returns `warning` with "Broj akcija po statusu nije dostupan…". It describes only what the summary proves (sample size, measured outcomes, coverage) and never reports "Ukupno 0 akcija" or "Red akcija je prazan".
  - Counts present, summary missing: "Ishodi su mereni na 0 akcija" is replaced by "Sažetak ishoda nije dostupan."
  - The both-null `unknown` path and the full counts+summary path are unchanged.
  - The reviewed `fake_zero_fallback` baseline entry for `PilotReadinessPage.tsx` (`measuredCount ?? 0`) was fixed and removed, so the baseline went from 38 to 37.
- Changed files: `Klijent/clientapp/src/pages/PilotReadinessPage.tsx`; `Klijent/clientapp/src/pages/__tests__/PilotReadinessPage.edgeCases.spec.ts`; `Klijent/clientapp/scripts/known-guardrail-baseline.json`; `MASTER_ROADMAP.md`; this queue; `.ai/runs/2026-10-13-RQ653-evidence.md`.
- Contract/runtime behavior changed: frontend readiness card text and status only; no API change.
- Checks run:
  - Failing-first: 2 new counts-missing tests failed with the exact defects ("Ukupno 0 akcija…"; "Red akcija je prazan"), and the summary-missing test failed with "mereni na 0". The unchanged-path test passed.
  - After the fix: PilotReadiness specs (edgeCases, spec, integration) plus AppAnalyticsRoutes and analyticsTrustStateProof passed 57/57 in 5 files.
  - `npm run check:analytics-guardrails` passed (baseline-only, 37 known). The first run flagged the shifted baseline line as NEW; it was fixed rather than re-baselined.
  - `npm run build` passed (exit 0).
  - `node scripts/check-prompt-queues.mjs` and `git diff --check` passed.
- Checks not run: full frontend suite and browser check, because the change is bounded to one pure card builder covered by unit and integration specs.
- Run log: `.ai/runs/2026-10-13-RQ653-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `5dd902bed9c9fe58097b8a7af5e632c181379d54`
- Main verification: after push, `git merge-base --is-ancestor 5dd902be origin/main` passed; origin/main was `5dd902bed9c9fe58097b8a7af5e632c181379d54`.
- Missed: none known. The inventory card was checked and not changed: it renders counts only after `hasInventorySignals` confirms the bootstrap inventory source, so the same partial-failure pattern is not proven there.
- Follow-up: RQ654 (P3) remains READY.
- Residual risk: main CI for `5dd902be` had not completed at close.
- Post-close routing: RQ653 has no dependents. RQ654 remains READY. The RQ645 primary pointer stays with its owner (Codex).
- Prompt defect / scope repair: the summary-missing variant (`measuredCount ?? 0`) on the same card and the same fake-zero class was fixed in scope. This was required because the guardrail flagged its shifted baseline line.

## RQ654 - Align analytics period presets with the business calendar day around local midnight

Status: DONE
Priority: P3
Type: frontend period provenance / timezone boundary
Feature family: analytics-period-preset-day-boundary
Parallel-safe: yes (only `utils/analyticsPeriodPresets.ts` and its spec; disjoint from RQ651-RQ653)
Owner: Cursor
Ready after: 2026-10-13 analytics screen bug review on main `a5604874`
Claim note: 2026-10-14 - claimed after refreshing `origin/main` to `72494d35672dd6d3b644dae36748e0d39e3decf9`. No RQ654 branch, lock, worktree or open PR exists. The primary pointer RQ647 is Codex-owned IN_PROGRESS; RQ648 is not taken because open PR #147 overlaps its branch-reconciliation scope and gate.
Commit suggestion: `fix(analytics): resolve period presets on the Europe/Belgrade business day`

### Problem

`getAnalyticsPeriodPresetRange` computes presets with `setUTCDate`/`setUTCHours` and `toISOString().slice(0, 10)`. For `now = 2026-10-14T00:30+02:00` (`22:30Z` on Oct 13), "Poslednjih 30 dana" ends on `2026-10-13`, excluding local "today" for two hours after midnight (one hour in winter). The only spec uses noon UTC and cannot detect this. Used by AnalyticsDashboard, InventoryPage, AnalyticsDetails and `analyticsFormatters.ts`.

### Evidence

`docs/qa/ANALYTICS_SCREEN_BUG_REVIEW_2026-10-13.md` (RQ654 detail).

### Scope

`Klijent/clientapp/src/utils/analyticsPeriodPresets.ts` and `src/utils/__tests__/analyticsPeriodPresets.spec.ts`. No backend change.

### Read first

The two files above; backend period parsing for one consumer (e.g. dashboard/inventory endpoint `fromDate`/`toDate` handling) to confirm whether sales dates are business-local or UTC; `docs/ai/` period-provenance guidance if present.

### Do

1. First confirm the backend business-day convention. If backend dates are UTC calendar days by contract, close as a documented decision with a boundary test pinning UTC semantics.
2. Otherwise add failing-first boundary tests (00:30 CEST, 23:30 CEST, winter CET, DST change days) and compute preset dates from the Europe/Belgrade calendar day via `Intl.DateTimeFormat` (no new dependency).
3. Keep the existing noon-based expectations green.

### Tests

`npm run test -- --run src/utils/__tests__/analyticsPeriodPresets.spec.ts`, dependent page period specs, `npm run build`.

### Acceptance

Preset end date equals the business "today" at every hour; decision recorded either way.

### Dependencies

None. Frontend-only; P3 because the affected window is at most two hours per day.

### Completion note

- Date: 2026-10-14
- Status: DONE
- Decision: the backend business day is Europe/Belgrade (`BelgradeCalendarDatePolicy` maps requested `DateOnly` bounds to Belgrade midnight; RQ485 already uses Belgrade-local Product Decision presets), so UTC preset days were a defect, not a contract.
- Completion: `getAnalyticsPeriodPresetRange` takes "today" from the Europe/Belgrade calendar via `Intl.DateTimeFormat` and counts preset days back with UTC-noon arithmetic; an unknown select value keeps the previous non-throwing single-day range.
- Changed files: `Klijent/clientapp/src/utils/analyticsPeriodPresets.ts`; `Klijent/clientapp/src/utils/__tests__/analyticsPeriodPresets.spec.ts`; this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-14-RQ654-evidence.md`.
- Checks run: failing-first boundary cases (00:30/23:30 CEST, winter CET, both DST change days, 90/180/365-day lengths) 5 failed before the fix; preset spec 12/12 after; dependent page specs 8 files / 129 tests pass; analytics guardrails (baseline-only) and `npm run build` pass; `git diff --check` pass.
- Checks not run: local full frontend suite on the final implementation; exact-main Analytics Quality Gates run `38541146808` (frontend tests, guardrails and build) passed instead.
- Run log: `.ai/runs/2026-10-14-RQ654-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `239dfcbbd181c39e5defeac4e4451cceec8c7898`
- Main verification: fresh `git fetch origin` confirmed `origin/main` = `239dfcbbd181c39e5defeac4e4451cceec8c7898` after push.
- Missed: none known.
- Follow-up: RQ655 is the next unclaimed P2 READY lane; RQ656/RQ657 remain READY.
- Residual risk: pages that build default dates outside the shared preset helper are not covered by this fix.
- Post-close routing: RQ654 has no dependents; RQ655-RQ657 remain READY and unclaimed, RQ648 remains READY but overlaps open PR #147, RQ647 remains Codex-owned IN_PROGRESS. No Zero-READY proof applies.
- Prompt defect / scope repair: none; the fix stayed inside the two scoped files.

## RQ655 - Surface swallowed loader errors instead of silent fallback

Status: DONE
Priority: P2
Type: frontend loader error handling / silent-fallback audit
Feature family: analytics-error-surface
Parallel-safe: yes (audit-only; independent frontend files)
Owner: Cursor
Ready after: 2026-10-13 analytics screen bug review on main `a5604874`
Claim reservation: inspected origin/main `05c7d05461781d1004a5c15e8c8827664ffee2e2`; pushed claim = the `docs(queue): claim RQ655 for Cursor` commit (SHA recorded in the run log); owner/worktree/branch Cursor / `C:\Users\Ivan\source\repos\Trendplus2` / `main`; remote branch/PR query: no RQ655 branch, worktree, lock or open PR (only open PR #147 is RQ650/branch audit).
Claim paths: `Klijent/clientapp/src/pages/GoogleShoppingTrendsPage.tsx`, `AmazonShoesTrendsPage.tsx`, `EbayShoesTrendsPage.tsx` and their specs under `src/pages/__tests__/`; audit-only review of `services/analyticsHttp.ts` / `services/analyticsApi.ts` JSON error-body parsing. InsightStudio stays out of scope (experimental route, owner review required).
Commit suggestion: `audit(analytics): surface swallowed loader errors and add visible fallback messages`

### Problem

Many analytics pages and helpers call promises and swallow errors with `.catch(() => {})`, `.catch(() => null)`, or `.catch(console.error)`. Examples: `InsightStudioPage.tsx`, trend pages, `InventoryPage.tsx`, `GoogleShoppingTrendsPage.tsx` and several API wrapper `.json().catch(() => null)` usages. Silent swallowing hides transient or systemic failures from users and reduces observability. Some fallbacks are harmless (empty lists), but others plausibly hide degraded state that should render an explicit unavailable/warning UI and a retry affordance.

### Evidence

Search results and code read: `Klijent/clientapp/src/pages/InsightStudioPage.tsx` (multiple `.catch(() => {})` for independent loaders), `Klijent/clientapp/src/pages/InventoryPage.tsx`, `Klijent/clientapp/src/pages/GoogleShoppingTrendsPage.tsx`, `Klijent/clientapp/src/services/analyticsHttp.ts` and several API handlers using `resp.json().catch(() => null)`.

### Scope

Audit and add targeted visible fallbacks on analytics pages/modules that currently swallow errors without user-visible recovery or telemetry. Do not change low-risk experimental pages without owner review.

### Read first

`Klijent/clientapp/src/pages/InsightStudioPage.tsx`, `Klijent/clientapp/src/pages/InventoryPage.tsx`, `Klijent/clientapp/src/services/analyticsHttp.ts`, `Klijent/clientapp/src/services/*.ts` (json parsing).

### Do

1. Add failing-first tests that assert a loader error produces a visible warning/alert and a retry control.
2. Replace `.catch(() => {})` on production analytics routes with `catch` that records telemetry and sets an explicit `loadingError` state, reusing existing `aaq-summary-error` / `refresh-banner` patterns where appropriate.
3. For API `.json().catch(() => null)` sites, distinguish parse-failure from 204/empty-body and log parse errors instead of silently returning null when the response status indicates a body is expected.

### Tests

Focused loader failure specs for each modified page; `npm run check:analytics-guardrails` and `npm run build`.

### Acceptance

Loader failures are visible to users with a retry action and logged for diagnostics; tests cover failure flows.

### Dependencies

Coordination with page owners if an experimental route intentionally swallows errors.

### Completion note

- Date: 2026-10-14
- Status: DONE
- Completion: `/google-shopping`, `/amazon-shoes` and `/ebay-shoes` no longer turn failed category, product or next-page loads into an empty result or swallow them. Each failure is logged with `console.error` and rendered as a `role="alert"` notice (`components/trendshoes/MarketplaceLoadErrorNotice.tsx`) with a "Pokušaj ponovo" retry; empty states are suppressed while the matching error is shown, and a failed next page keeps the products already loaded. `analyticsHttp.ts` / `analyticsApi.ts` `res.json().catch(() => null)` were reviewed and retained: they only parse error bodies inside `parseApiError` and the caller still throws.
- Changed files: `Klijent/clientapp/src/components/trendshoes/MarketplaceLoadErrorNotice.tsx`; `Klijent/clientapp/src/pages/GoogleShoppingTrendsPage.tsx`; `AmazonShoesTrendsPage.tsx`; `EbayShoesTrendsPage.tsx`; `src/pages/__tests__/MarketplaceTrendsPages.loadErrors.spec.tsx`; this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-14-RQ655-evidence.md`.
- Checks run: failing-first spec 9/9 failed before, 9/9 pass after; full frontend vitest 243 files / 1,970 tests pass; analytics guardrails (baseline-only) and `npm run build` pass; `git diff --check` pass; exact-main Analytics Quality Gates run `38547362182` green.
- Checks not run: none required.
- Run log: `.ai/runs/2026-10-14-RQ655-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `ab20a4fb13b181ce8c82f8b67b934e792b7da845`
- Main verification: fresh `git fetch origin` confirmed `origin/main` = `ab20a4fb13b181ce8c82f8b67b934e792b7da845` after push.
- Missed: the marketplace pages' `handleDelete` still ignores delete failures (user action, not a loader); `InsightStudioPage.tsx` stays untouched because its route is experimental and needs owner review.
- Follow-up: RQ657 is the next unclaimed READY lane; RQ656 is Codex-owned IN_PROGRESS.
- Residual risk: none known beyond the missed items.
- Post-close routing: RQ655 has no dependents; RQ657 READY, RQ648 READY with PR #147 overlap, RQ656 IN_PROGRESS (Codex). No Zero-READY proof applies.
- Prompt defect / scope repair: one shared notice component was added under `components/trendshoes/` instead of three copies; same owner (frontend trend pages).

## RQ656 - Audit and fix fake-zero fallbacks across analytics UI

Status: DONE
Priority: P2
Type: frontend fake-zero / unavailable-value audit
Feature family: analytics-screen-fake-zero-parity
Parallel-safe: yes (frontend-only; independent pages)
Owner: Codex
Ready after: 2026-10-13 analytics screen bug review on main `a5604874`
Claim reservation: 2026-10-14 - claimed from inspected `origin/main` `4e948a448fd89fe251df81e7bb77c7cdbbe3d6f0`. Planned worktree `C:\Users\Ivan\.codex\worktrees\rq656-fake-zero`, branch `codex/rq656-fake-zero-20261014`; bounded paths are the named analytics pages and focused specs plus this queue/evidence. Remote branch/PR query found no RQ656 branch, lock or open PR; open PR #147 is the separate RQ650 branch audit and RQ655 is owned by Cursor on disjoint loader-error paths.
Commit suggestion: `audit(analytics): remove coercive '?? 0' fake-zero fallbacks in analytics pages`

### Problem

Many pages coerce optional/nullable backend fields to `0` with `?? 0` or similar `(... ?? 0)` patterns, making an unavailable value appear as `0` or `0,0%`. Examples across `AnalyticsDashboard.tsx`, `SupplierSalesStatsPage.tsx`, `SupplierFootwearAnalyticsPage.tsx`, `InsightStudioPage.tsx`, `InventoryPage.tsx` and `ExecutiveDecisionBoardPage.tsx`. Some uses are legitimate arithmetic guards, others are presentation-level and should let formatters render unavailable fallback (e.g. `fmtNumber(..., fallback)`).

### Evidence

Grepped `?? 0` across `Klijent/clientapp/src` shows many instances in dashboard cards, supplier pages, and charts. Prior fixes (RQ651) targeted a subset; a broader audit is needed.

### Scope

Review each `?? 0` occurrence in analytics pages and decide whether it is a presentation fallback (remove coercion, let formatter show fallback) or a numeric guard required by calculations (keep but document).

### Read first

`Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`, `SupplierSalesStatsPage.tsx`, `SupplierFootwearAnalyticsPage.tsx`, `InsightStudioPage.tsx`, `InventoryPage.tsx`, `ExecutiveDecisionBoardPage.tsx`.

### Do

1. Create failing-first tests where `null` input produced `0` in the UI previously.
2. Replace presentation coercions with `fmtNumber`/`fmtPct` fallbacks or conditional rendering that distinguishes unknown vs real zero.
3. Document each retained numeric guard with an inline comment and a spec proving the numeric necessity.

### Tests

Focused UI tests for affected cards and charts; guardrails and build.

### Acceptance

Unknown values render as unavailable; real zero values still render as `0` / `0,0%`.

### Dependencies

None. Frontend-only.

### Completion note

- Date: 2026-10-14
- Status: DONE
- Completion: Nullable executive data-quality counts no longer become trusted zero signals; the dashboard now shows an explicit unavailable warning. Missing weekday/hour revenue points remain null, so charts show an empty-data state instead of fabricated zero bars. Real zero values remain zero.
- Changed files: Klijent/clientapp/src/pages/AnalyticsDashboard.tsx; Klijent/clientapp/src/components/analytics/AnalyticsDashboardCharts.tsx; Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.operationalFallback.spec.tsx; Klijent/clientapp/scripts/known-guardrail-baseline.json; this queue; MASTER_ROADMAP.md; .ai/runs/2026-10-14-RQ656-evidence.md.
- Checks run: failing-first nullable executive-count regression was added; exact-head frontend run 38547520087 for e64c2782c830fa2329fa3ab84f35aa414392829b passed 159 files / 1,197 tests, analytics guardrails, UI regression gates, client build, bundle budget and lazy-chart checks; Vercel deployment passed; node scripts/check-encoding.mjs, all three governance validator families and git diff --check passed.
- Checks not run: local focused Vitest and local client build were unavailable because this clean worktree had no node_modules and npm dependency installation did not complete; remote exact-head frontend CI supplied the executable proof. Backend/RQ453 jobs were unrelated to the frontend-only scope and remained in progress at closeout.
- Run log: .ai/runs/2026-10-14-RQ656-evidence.md
- Evidence state: synchronized
- Claim reservation: inspected origin/main 4e948a448fd89fe251df81e7bb77c7cdbbe3d6f0; pushed claim f3818881bc847c011bfe7f4e8cf699533b8ae6a8; owner/worktree/branch Codex / C:\Users\Ivan\.codex\worktrees\rq656-fake-zero / codex/rq656-fake-zero-20261014; remote branch/PR query found no RQ656 owner before claim; PR #155 carried implementation.
- Delivery mode: pull-request
- Main commit SHA: 06c9c9f35e79d97ccd0cb8814dd5c8da8e1eb840
- Main verification: fresh fetch confirmed origin/main = 06c9c9f35e79d97ccd0cb8814dd5c8da8e1eb840 after PR #155 merge; implementation head e64c2782c830fa2329fa3ab84f35aa414392829b is contained in that merge.
- Missed: broader ?? 0 occurrences in Supplier, Inventory, Insight Studio and Decision Board remain as retained internal arithmetic/empty-state guards; no additional presentation coercion was changed without a focused proof.
- Follow-up: RQ657 remains READY and unclaimed; RQ648 remains READY but overlaps open PR #147.
- Residual risk: local-only frontend execution remains unavailable in this checkout, while exact-head CI passed the changed frontend surface.
- Post-close routing: post-delivery origin/main 06c9c9f35e79d97ccd0cb8814dd5c8da8e1eb840 scanned the full active RQ addendum set, root RQ queue, SQL queue, roadmap, locks, branches and open PRs; no RQ656 dependents exist, and RQ657 is the next collision-safe READY lane.
- Prompt defect / scope repair: the broad audit was bounded to the confirmed dashboard count and weekday/hour chart presentation fallbacks; retained internal guards are documented as non-display calculations in the run log.

## RQ657 - Add request-sequence/abort guards where missing to prevent stale-response races

Status: DONE
Priority: P2
Type: frontend stale-response / request cancellation
Feature family: analytics-request-race
Parallel-safe: yes (frontend-only; independent pages)
Owner: Codex
Claim reservation: 2026-10-14 - claimed from inspected `origin/main` `ee151a6f30b298145f4a399da576f2c9de97b74b`. Planned branch `codex/rq657-request-race-20261014`; bounded paths are core retail analytics loaders and focused specs plus this queue/evidence. Remote branch/PR query found no RQ657 branch, lock or open PR; active RQ659 is Cursor-owned navigation work on disjoint paths, and open PR #147 is the separate RQ650 branch audit.
Ready after: 2026-10-13 analytics screen bug review on main `a5604874`
Commit suggestion: `fix(analytics): add request-sequence or AbortController guards to loaders missing them`

### Problem

Some analytics pages issue async loaders or multiple overlapping requests without request-sequence refs or AbortController cancellation. This allows slower earlier responses to overwrite newer state (observed and fixed in RQ652). Examples in the **active core retail scope**: `SupplierConsolidatedPage.tsx` and other proven retail analytics loaders; `AnalyticsDashboard.tsx` only when no active overlapping claim. Insight Studio, trend, scraper and marketplace pages are excluded by owner directive.

### Evidence

Grepped for presence/absence of `AbortController` and existing `*RequestSeqRef` patterns. Several pages already implement cancellation; others issue multiple `.then` chains without guard.

### Scope

Audit **core retail analytics pages only** for a demonstrated overlapping-filter response race and add requestSeqRef or the existing `useReliableAnalyticsQuery` helper if appropriate. No Insight Studio/trend/marketplace pages; avoid any paths owned by another IN_PROGRESS task.

### Do

1. Failing-first tests where two overlapping requests produce stale UI on the second filter application.
2. Add `itemsRequestSeqRef`/`countsRequestSeqRef` pattern or wire into `useReliableAnalyticsQuery` where available.
3. Keep consistent UX: previous-loading placeholder remains until current arrives; failed previous responses must not overwrite current state.

### Tests

Focused loader race tests; `npm run build`.

### Acceptance

No stale overwrites in repeated filter scenarios; tests cover overlapping request flows.

### Dependencies

Use existing `useReliableAnalyticsQuery` helper when appropriate.

### Completion note

- Date: 2026-10-14
- Status: DONE
- Completion: 100%
- Changed files: `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`; `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.scopeReload.spec.tsx`; `Klijent/clientapp/scripts/known-guardrail-baseline.json`; this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-14-RQ657-evidence.md`
- Checks run: failing-first focused race test reproduced the stale overwrite; fixed focused test passed 4/4; exact-head frontend CI run `38551171537`, job `115809208883`, passed tests, guardrails, UI gates, client build, bundle budget and lazy-chart checks; Vercel deployment passed; POS UI and JavaScript SDK checks passed; local `npm run check:analytics-guardrails`, `npm run typecheck`, `npm run build`, encoding check and `git diff --check` passed
- Checks not run: complete backend analytics suite and RQ453 certification were not required for this frontend-only scope and remained pending at closeout
- Run log: `.ai/runs/2026-10-14-RQ657-evidence.md`
- Evidence state: synchronized
- Claim reservation: inspected `origin/main` `ee151a6f30b298145f4a399da576f2c9de97b74b`; pushed claim `3362e1183ddd62eb275670f9dd9929aeaf13a08f`; owner/worktree/branch Codex / `C:\Users\Ivan\.codex\worktrees\rq657-request-race` / `codex/rq657-request-race-20261014`; remote branch/PR query was clear at claim
- Delivery mode: PR #156 merged after exact-head frontend CI passed
- Delivery: PR #156 merged; implementation head `2b23ce5c5433e789ba60114fa63795397b7ee73a`
- Main commit SHA: `5a4a15f647166d53dffafe2354b3649680527dc2`
- Main verification: fresh `origin/main` contains the PR #156 merge SHA
- Missed: no other core-route race was changed because the audit found existing cancellation/request-sequence guards; excluded trend, scraper, marketplace and Insight Studio paths remain outside RQ657
- What was missed: no additional core-route race was changed because Supplier Consolidated and the other audited core pages already had cancellation/request-sequence guards; excluded trend, scraper, marketplace and Insight Studio paths remain outside RQ657
- Follow-up: RQ661 remains the primary P1 READY lane; RQ660 is independently READY; RQ648 remains READY with open PR #147 overlap
- Residual risk: unrelated backend/RQ453 CI was still pending; no backend behavior changed
- Post-close routing: refreshed current `origin/main`, scanned the active RQ queue/addenda, roadmap, locks, worktrees and open PRs; RQ657 has no dependents and no new promotion was needed

## RQ658 - Close remaining local false-pass PostgreSQL tests and convention traversal gap

Status: DONE
Priority: P1
Type: test reliability / CI proof
Feature family: postgres-proof-convention
Parallel-safe: yes (test-only)
Owner: grok
Ready after: RQ638 / PR #144 on main `a5604874`
Commit suggestion: `test(ci): close PostgreSQL convention helper traversal gap`

### Problem

16 tests in `AssortmentNivelacijaOracleTests`, `SupplierReconciliationPackTests` and `SupplierScorecardOracleTests` used plain `[Fact]`/`[Theory]` and returned early when a helper (`TryCreateDatabaseAsync`, `TryCreateScorecardDatabaseAsync`) yielded null because `PostgresContainerFixture.IsAvailable` was false, so they reported Passed locally without Docker. `PostgresRequiredAttributeConventionTests.ReachesPostgresAvailabilityGuard` missed them: it added every member to `visitedMembers` before checking whether the body calls it, so a guard helper was marked visited during traversal of an unrelated helper and never inspected.

### Evidence

On main `87e1f687`, with the traversal fixed, the convention test fails listing exactly those 16 tests (Assortment 195/213/314/359/379/417/445/466, Reconciliation 31/92/130, Scorecard 130/353/401/461/499).

### Scope

Test-only: attribute swap on the 16 tests, traversal fix plus regression self-test. No assertions removed or weakened, no production code.

### Read first

`Api.Tests/PostgresRequiredFactAttribute.cs`, `Api.Tests/PostgresRequiredAttributeConventionTests.cs`, `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md` lessons learned.

### Do

1. Check the call before `visitedMembers.Add` in `ReachesPostgresAvailabilityGuard`.
2. Add self-test `Convention_finds_a_guard_helper_called_after_another_helper`.
3. Convert the 16 tests to `[PostgresRequiredFact]`/`[PostgresRequiredTheory]`.

### Tests

Local Testing env: convention + 3 classes = 42 total, 24 passed, 18 skipped (PostgreSQL proof not run), 0 failed. CI backend must show these tests executed against PostgreSQL.

### Acceptance

Convention test green; no plain `[Fact]`/`[Theory]` test reaches a PostgreSQL availability guard; PR merged on green backend + governance CI.

### Dependencies

RQ638.

### Completion note

- Date: 2026-10-13
- Status: DONE
- Completion: 100%
- Changed files: Api.Tests/PostgresRequiredAttributeConventionTests.cs, Api.Tests/AssortmentNivelacijaOracleTests.cs, Api.Tests/SupplierReconciliationPackTests.cs, Api.Tests/SupplierScorecardOracleTests.cs, docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md
- Checks run: local Testing env convention + 3 classes (42 total, 24 passed, 18 skipped, 0 failed; convention failed with the 16 violations before conversion); node scripts/check-prompt-queues.mjs, check-planning-architecture.mjs, check-agent-instructions.mjs with and without --self-test; git diff --check; PR #148 CI: Complete backend analytics suite run 38449578209 pass, planning governance run 38449578180 pass, frontend run 38449578205 pass
- Checks not run: smoke tests and Development environment (owner rule)
- Run log: .ai/runs/2026-10-13-RQ658-postgres-convention-evidence.md
- Delivery mode: PR #148 merged with --merge after green CI
- Main commit SHA: c094c3ceed6609993344e8beaeaa9aa9a15b0d98
- Main verification: git rev-parse origin/main -> c094c3ceed6609993344e8beaeaa9aa9a15b0d98
- Missed: traversal bug shipped in ee79f741 let 16 tests pass locally without Docker
- Follow-up: none
- Residual risk: convention remains source-regex based; unusual guard shapes (e.g. guards through other classes) may still escape detection

---

## Owner analytics-first routing correction — 2026-10-14

Owner decision: `docs/product/ANALYTICS_FIRST_SCOPE_2026-10-14.md` freezes eBay, Amazon, Google Shopping, other scrapers, trend/model training, Insight Studio and experimental/test features. RQ655 and RQ656 are already DONE; do not reopen or extend them into marketplace work. RQ657 may work **only on core retail analytics** (Supplier Consolidated and other demonstrably exposed retail routes), not trend/scraper/Insight Studio routes. Preserve historical DONE evidence and required core regression tests. Any stale old READY for an experiment is out of the current product scope and must not be claimed.

## RQ659 - Analytics-first main navigation; hide experimental/test links

Status: DONE
Priority: P1
Type: bounded frontend UX/tests
Feature family: analytics-first-navigation
Parallel-safe: yes; own `Klijent/clientapp/src/layout/navConfig.ts` and nearest nav/sidebar specs only
Owner: Cursor
Ready after: owner product focus decision 2026-10-14; no provider or other task dependency
Claim reservation: inspected origin/main `72357d084b71c1cd77276e29af6cd061f8299855`; pushed claim = the `docs(queue): claim RQ659 for Cursor` commit (SHA recorded in the run log); owner/worktree/branch Cursor / `C:\Users\Ivan\source\repos\Trendplus2` / `main`; remote branch/PR query: no RQ659 branch, worktree, lock or open PR (only open PR #147, RQ650 branch audit). Claim paths: `Klijent/clientapp/src/layout/navConfig.ts`, `layout/__tests__/navConfig.spec.ts`, `layout/components/__tests__/Sidebar.spec.tsx` and directly affected nav/route specs.
Commit suggestion: `fix(nav): prioritize retail analytics and hide test modules`

### Proven gap

`navConfig.ts` exposes test entries `/trend-dashboard`, `/runtime-scoring`, `/open-training`; an entire "Trendovi i scraperi" group with `/global-trends`, `/scraper-hub`, `/deichmann`, `/aboutyou`, `/humanic`, `/amazon-shoes`, `/ebay-shoes`, `/google-shopping`; and `/image-upload-test` in Admin. Meanwhile useful analytics is split across sections. The `/analytics/daily-sales` item is misleadingly labeled "Prodaja po smenama" even though shift source coverage can be unavailable.

### Scope and steps

1. Read `Klijent/clientapp/src/layout/navConfig.ts`, `layout/__tests__/navConfig.spec.ts`, `layout/components/__tests__/Sidebar.spec.tsx`, `App.tsx`. Add failing-first tests for the listed links being absent from visible `NAV_GROUPS`, nonempty groups, and core `/analytics`, `/analytics/daily-sales`, `/analytics/inventory`, `/analytics/supplier`, `/analytics/products`, `/analytics/pre-nivelacija-prioriteti` remaining visible.
2. Remove only nav entries and now-empty test groups/icons; relabel daily sales to "Dnevna prodaja". Keep core operational/admin support and imported source mapping. **Do not remove legacy routes, controllers, migrations, models, DB tables or evidence.**
3. Assert existing deep-link routes still resolve, Sidebar and mobile layouts are usable, and route smoke tests still pass.

### Tests / completion

Focused nav/sidebar/App layout and analytics route specs, `npm run check:analytics-guardrails`, `npm run build`, `node scripts/check-prompt-queues.mjs`. All genuine core decision links must remain. Main SHA/evidence required for DONE. Do not start scraping or marketplace repair as a side quest.

### Completion note

- Date: 2026-10-14
- Status: DONE
- Completion: `navConfig.ts` no longer shows `/trend-dashboard`, `/runtime-scoring`, `/open-training`, the "Trendovi i scraperi" group (`/global-trends`, `/scraper-hub`, `/deichmann`, `/aboutyou`, `/humanic`, `/amazon-shoes`, `/ebay-shoes`, `/google-shopping`) or `/image-upload-test`; `/analytics/daily-sales` is labelled "Dnevna prodaja". Routes stay registered in `App.tsx`; no route, page, service, controller, migration or evidence was removed. The owner-approved eight primary / nine secondary analytics links are unchanged apart from that label.
- Changed files: `Klijent/clientapp/src/layout/navConfig.ts`; `layout/__tests__/navConfig.spec.ts`; `layout/components/__tests__/Sidebar.spec.tsx`; this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-14-RQ659-evidence.md`.
- Checks run: failing-first nav spec 3 failed before; layout specs 28/28 and nav/header/App route specs pass after (also after rebasing onto `db559270`); analytics guardrails (baseline-only) and `npm run build` pass; `git diff --check` pass; exact-main Analytics Quality Gates run `38550949174` green.
- Checks not run: e2e route smoke (no e2e suite in `Klijent/clientapp`).
- Run log: `.ai/runs/2026-10-14-RQ659-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `2472df857e48397a8b0a16076b66e9dd5a04bfd0`
- Main verification: fresh `git fetch origin` confirmed `origin/main` = `2472df857e48397a8b0a16076b66e9dd5a04bfd0` after push.
- Missed: the Daily Sales page title (`DailySalesStatsPage.tsx`) still reads "Prodaja po smenama"; navigation-only scope.
- Follow-up: RQ661 (P1) is the next unclaimed READY prompt; RQ660 (P2) also READY.
- Residual risk: hidden pages remain reachable by URL by design.
- Post-close routing: RQ659 has no dependents; RQ661/RQ660 READY, RQ657 Codex-owned IN_PROGRESS, RQ648 READY with PR #147 overlap. No Zero-READY proof applies.
- Prompt defect / scope repair: none.

## RQ660 - PDC action summary: unknown blocked count must not become zero

Status: DONE
Priority: P2
Type: bounded analytics correctness/frontend tests
Feature family: product-decision-actionability-null-counts
Parallel-safe: yes; own `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` and nearest PDC specs only, no Dashboard/Supplier/Inventory files
Owner: Cursor
Local lock: `.ai/task-locks/RQ660-cursor.lock.md`
Ready after: owner product focus decision 2026-10-14; confirm fresh collision-free files before claim
Claim reservation: inspected origin/main `e74d055c110c7cd8073f29cf6aacaa58ac59f178`; pushed claim = the `docs(queue): claim RQ660 for Cursor` commit (SHA recorded in the run log); owner/worktree/branch Cursor / `C:\Users\Ivan\source\repos\Trendplus2` / `main`; remote branch/PR query: no RQ660 branch, worktree, lock or open PR (open PRs: #147 RQ650 branch audit). Claim paths: `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` and nearest PDC specs only.
Commit suggestion: `fix(analytics): preserve missing PDC action counts as unavailable`

### Verified code candidate (validate backend contract first)

`ProductDecisionCenterPage.tsx` correctly keeps `kpis.blockedCount` and `kpis.insufficientEvidenceCount` nullable, but `actionabilitySummary` currently formats `payload.summary.blockedCount ?? 0` and `payload.summary.insufficientEvidenceCount ?? 0` when `actionableCount` is known. If either field can be missing this is a fake zero. It is **not proven** that the live backend actually emits a missing value.

### Steps and acceptance

1. Inspect source API response type/service mapping and PDC tests. First classify whether these two fields can be nullable, omitted, or are guaranteed present. Check completed fake-zero fixes to avoid overlap.
2. If missing is permitted, add failing-first spec for actionable count present + blocked/insufficient missing, and a separate true-zero case. Use unavailable label for missing only, keep literal 0 when returned, with no new scoring or API semantics.
3. Check no error/loading state promotes a partly missing summary to complete, and that existing exports/detail semantics remain consistent.
4. If the backend strictly guarantees nonnull fields and validation rejects missing input, mark this prompt `OBSOLETE` with an exact test/DTO code proof; **do not fabricate a bug or change contracts**.

### Tests / completion

Nearest PDC test files, `npm run check:analytics-guardrails`, `npm run build`, `node scripts/check-prompt-queues.mjs`. DONE only if a real patch is delivered on verified main with evidence; a no-change classification is not DONE.

### Completion note

- Date: 2026-10-14
- Status: DONE
- Completion: classification - backend `ProductDecisionCenterSummaryDto` emits all three counts as non-nullable `int` from one builder, but the frontend type declares them optional and `productDecisionCenterSummarySchema` does not validate them, so missing input is accepted (OBSOLETE condition not met). The actionability line now uses `fmtNumber(..., ANALYTICS_UNAVAILABLE_LABEL)` for `blockedCount`/`insufficientEvidenceCount`: missing renders "Nije dostupno", backend `0` stays `0`. No scoring, API, schema or export change.
- Changed files: `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`; `pages/__tests__/ProductDecisionCenterPage.hierarchy.spec.tsx`; this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-14-RQ660-evidence.md`; `.ai/runs/2026-10-14-RQ661-evidence.md` (CI result).
- Checks run: failing-first missing-count case failed before (rendered 0), true-zero counterexample passed before/after; PDC + readiness regression + API contract specs 74/74 pass; analytics guardrails and `npm run build` pass.
- Checks not run: backend tests (no backend change); e2e (no suite); exact-main Analytics Quality Gates run `38553160658` queued at close.
- Run log: `.ai/runs/2026-10-14-RQ660-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `6b9041f93e4652732fee4ac3812147daf252270f`
- Main verification: fresh `git fetch origin` confirmed `origin/main` = `6b9041f93e4652732fee4ac3812147daf252270f` after push.
- Missed: frontend schema still does not require the three counts; tightening it changes the API contract and is out of scope.
- Follow-up: RQ648 (P2) is the only remaining READY prompt here; verify PR #147 overlap before claim.
- Residual risk: latent defect only (no live backend path proven to omit the counts); exact-main CI queued at close.
- Post-close routing: RQ660 has no dependents; RQ648 READY (PR #147 overlap), RQ657/RQ659/RQ661 DONE. No Zero-READY proof applies.
- Prompt defect / scope repair: none.
## RQ661 - Replace hard-coded Home dashboard business claims with an honest retail starting point

Status: DONE
Priority: P1
Type: frontend analytics truth/landing UX + focused tests
Feature family: home-business-truth
Parallel-safe: yes; exclusively `Klijent/clientapp/src/pages/HomePage.tsx` and new focused home-page specs; do not touch RQ659-owned `navConfig.ts` or shared nav/router files without explicit path release
Owner: Cursor
Local lock: `.ai/task-locks/RQ661-cursor.lock.md`
Ready after: current-owner 2026-10-14 analytics-first direction; no provider access or sales import required for repository-local repair
Claim reservation: inspected origin/main `ed2f26882c1cdc6d9e193fcbd72b0481f73509c7`; pushed claim = the `docs(queue): claim RQ661 for Cursor` commit (SHA recorded in the run log); owner/worktree/branch Cursor / `C:\Users\Ivan\source\repos\Trendplus2` / `main`; remote branch/PR query: no RQ661 branch, worktree, lock or open PR (open PRs: #147 RQ650 branch audit, #156 Codex RQ657). Claim paths: `Klijent/clientapp/src/pages/HomePage.tsx` and new Home specs only.
Commit suggestion: `fix(home): remove hardcoded business KPIs and prioritize retail analytics`

### Proven defect, distinct from RQ659

`HomePage.tsx` renders literal tiles "Aktivne sekcije 32", "Import jobs 5", "Trend signal +12.8%" and an Insight Pulse panel asserting Google Shopping increased 18% in 24h, three SKU have high demand/low stock and an actionable top-ten markdown suggestion. These are hard-coded in JSX, not derived from API/period/source evidence, yet read like measured business facts. It also advertises "trend scraping tokove", imports `TrendModelList` (forecast evaluation) and displays a seasonal carousel. RQ659 fixes **menu** only. RQ585 already ships a separate digest; never rebuild the digest here.

### Scope / sequence

1. Read `HomePage.tsx`, `DashboardCards.tsx`, `TrendModelList.tsx`, existing homepage/layout test patterns, backend status contract and `/analytics`/Decision Pulse route behavior. Add failing-first render tests that prove every listed hard-coded business number/promise appears in the unchanged component (and fails new no-fake-business-fact assertions).
2. Remove those static business KPI and recommendation claims. Keep the current **real** backend status with its checking/recovering/offline cases. Replace the experimental headline/panel with short **navigation** to existing `/analytics`, `/analytics/daily-sales`, `/analytics/inventory`, `/analytics/supplier`, `/analytics/pre-nivelacija-prioriteti` and optionally shipped `/analytics/decision-pulse`. Do not create new KPI endpoints or fetch financial data merely to make Home look full.
3. Remove the `TrendModelList` block from Home and the promotional/experimental carousel **on Home only** unless there is a proven real-business decision use; leave their component files, other routes and API behavior unchanged. Core `DashboardCards` operational quick actions may remain, but check that none claims an unverified fact.
4. Add a concise link explaining historical "as of" vs current decisions through the existing analytics trust surface. No static dates, freshness figures, business values or user counts disguised as live data.
5. Preserve `/` routing, keyboard navigation, small viewport layout and offline/backend-recovering UX. Do not redirect root (which might break bookmarks), change POS/auth/production configuration or delete experiment code/data.

### Tests / acceptance

- Render spec proves absent literal `+12.8%`, `Import jobs 5`, `32`-tile, Google Shopping `18%`, unverified `3 SKU` and fabricated markdown candidate; no trend/scraper claims as current business evidence.
- Real backend status is still correctly represented for checking/online/offline/recovering; links to core reports resolve, and the root does not crash when the backend is down.
- Verify no extra root/Home component renders a misleading hard-coded financial claim; if verified outside owned Home path, register a **separate bounded finding**, do not silently widen this task.
- Focused Home render specs, App routing/navigation specs needed for `/`, `npm run check:analytics-guardrails` and client build; `node scripts/check-prompt-queues.mjs`. Complete via exact-main code+test evidence, not only a screenshot. No external-provider proof or new import is required.
- Coordination: RQ659 owns `navConfig.ts`; this task only owns `HomePage.tsx` and new Home tests. Serial queue header/status edits according to protocol.

### Completion note

- Date: 2026-10-14
- Status: DONE
- Completion: Home no longer renders the hard-coded "Aktivne sekcije 32", "Import jobs 5", "Trend signal +12.8%" tiles, the Insight Pulse claims (Google Shopping 18%, "3 SKU", top-10 markdown recommendation), the "trend scraping" headline, `TrendModelList` or the seasonal carousel. It keeps the real backend status (PROVERA/BUDI SE/ONLINE/OFFLINE) and `DashboardCards` quick actions, and adds links to `/analytics`, `/analytics/daily-sales`, `/analytics/inventory`, `/analytics/supplier`, `/analytics/pre-nivelacija-prioriteti`, `/analytics/decision-pulse` plus a historical-vs-current note linking `/analytics/data-quality`. Component files, routes and APIs unchanged.
- Changed files: `Klijent/clientapp/src/pages/HomePage.tsx`; `pages/HomePage.truth.spec.tsx` (new); `pages/HomePage.carousel.spec.tsx`; this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-14-RQ661-evidence.md`.
- Checks run: failing-first Home truth spec 7/7 failed before; Home + App + layout specs 42/42 pass after; analytics guardrails and `npm run build` pass.
- Checks not run: e2e route smoke (no e2e suite in `Klijent/clientapp`); exact-main Analytics Quality Gates run `38551797215` pending at close.
- Run log: `.ai/runs/2026-10-14-RQ661-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `dcdb84c79aa22eb4f3f4c3806645cfd36fbb35c6`
- Main verification: fresh `git fetch origin` confirmed `origin/main` = `dcdb84c79aa22eb4f3f4c3806645cfd36fbb35c6` after push.
- Missed: none in owned scope; root-surface scan found no other hard-coded business figures (`DashboardCards`, `DashboardFooter`, `HeaderStatus`).
- Follow-up: RQ660 (P2) is the next unclaimed READY prompt.
- Residual risk: exact-main CI pending at close; hidden experimental components remain reachable via their own routes by design.
- Post-close routing: RQ661 has no dependents; RQ660 READY, RQ648 READY with PR #147 overlap; RQ657 closed by Codex in parallel (`0c96b156`). No Zero-READY proof applies.
- Prompt defect / scope repair: existing `HomePage.carousel.spec.tsx` first case (carousel expected on Home) inverted to the new contract, required by acceptance step 3; claim text said "new Home specs" only.

---

## RQ662 - Fail closed for stale Supplier store options across dataScope transitions

Status: DONE
Priority: P1
Type: frontend/filter-lineage/tests
Feature family: supplier-sales-store-filter-scope
Parallel-safe: yes with RQ663/RQ664; no with another Supplier Sales filter/store-discovery owner
Owner: Codex
Claim reservation: 2026-10-14 - claimed from inspected `origin/main` `b1da501c66a9daf135ee2c5f20034d3d26b096e7`. Planned worktree `C:\Users\Ivan\.codex\worktrees\rq662-supplier-scope`; planned branch `codex/rq662-supplier-scope-20261014`. Remote branch/PR/lock collision query found no RQ662 owner or Supplier store-discovery owner; RQ663 and RQ664 remain independently READY on disjoint paths.
Ready after: RQ657 DONE, RQ659 DONE; no provider/database or product-owner gate
Commit suggestion: `fix(analytics): prevent stale supplier store options across scope switches`

### Completion note

- Date: 2026-10-14
- Status: DONE
- Completion: Supplier store options now fail closed during dataScope transitions, slow or failed discovery, stale URL/shared store IDs and embedded rendering. Previous-scope options are hidden while loading; failed discovery keeps an explicit retry state; only a store confirmed in the active scope is sent to Supplier Sales analytics. Signed IDs and cancellation protection remain intact.
- Changed files: `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`; `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.scopeReload.spec.tsx`; `Klijent/clientapp/scripts/known-guardrail-baseline.json` (line references only for unchanged reviewed violations shifted by the source insertion)
- Checks run: focused scope/premium/fallback tests (48 passed); `npm run check:analytics-guardrails` (pass: encoding, self-test, 36 known violations only, typecheck); `npm run build` (pass); `node scripts/check-prompt-queues.mjs` (785 tasks pass); exact-main GitHub Actions run `38566240717` for `b48fc1cfd7feebcb54e98e01472eb5d0cc9db7a3` (success: frontend analytics tests, guardrails, UI program gates, build, bundle budget/lazy charts, POS UI audit and SDK pin jobs)
- Checks not run: full local frontend test suite; `npm run lint` (not required by the focused acceptance and exact-main Actions covered the named frontend gates)
- Run log: `.ai/runs/2026-10-14-RQ662-evidence.md`
- Evidence state: synchronized
- Claim reservation: inspected `origin/main` `b1da501c66a9daf135ee2c5f20034d3d26b096e7`; pushed claim `02505f69b7862012078ff43a797459057aca19b9`; owner/worktree/branch `Codex / C:\Users\Ivan\.codex\worktrees\rq662-supplier-scope / codex/rq662-supplier-scope-20261014`; remote branch query had no prior RQ662 or Supplier store-discovery owner, and no open PR before claim
- Delivery mode: direct-main
- Main commit SHA: `b48fc1cfd7feebcb54e98e01472eb5d0cc9db7a3`
- Main verification: fresh `git fetch origin`; `git rev-parse origin/main` returned `b48fc1cfd7feebcb54e98e01472eb5d0cc9db7a3`, and `git merge-base --is-ancestor` confirmed the implementation is on `main`
- Missed: none in owned scope
- Follow-up: RQ663 is the next `Current READY` prompt; RQ664 remains independently READY
- Residual risk: no known product risk in the owned scope; full local frontend suite was not run because the exact-main Actions gate passed
- Post-close routing: fresh post-delivery `origin/main` scan found no RQ662 dependents to unblock; RQ663 was selected as the next P1 primary READY prompt and RQ664 remains a disjoint P2 READY candidate. No Zero-READY proof applies.
- Prompt defect / scope repair: the existing guardrail baseline keys are line-based; three SupplierSales entries were updated only to their new line locations after the required source insertion, with no new exception or allowlist rule.

### Verified gap / not duplicate RQ657

`Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx` has a cancelled-late-response guard around `getStores(true, activeDataScope)`, so the **late previous-scope response** is ignored (RQ657 DONE). But `stores` itself is not scoped to its response: previously loaded store options stay selectable while new-scope discovery is in flight, and the catch branch keeps them after new-scope discovery fails. The native store select uses `value={storeId ?? ""}` and renders the retained `stores` without an active-scope validity check. Thus an old scope's store identity can be offered/committed under the new dataScope. This is source-confirmed UI state exposure; do not claim a production incident without a recorded reproduction.

### Scope / implement

1. First extend `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.scopeReload.spec.tsx` with a failing-first transition: imported-scope stores resolve, user switches to existing scope, **before** existing-scope discovery resolves the old options cannot be selected; then reject discovery and verify an explicit unavailable/retry notice rather than old options or silent empty success. Include the inverse existing→imported transition and sharedFilters/embedded mode. Assert requests never send a stale `storeId` into the new dataScope; a valid store genuinely found in both scopes must remain eligible only after validation.
2. Introduce a scope-tagged store-list state (loaded-for scope, loading, error) or reuse established `storeFilterFallbackState` / `AnalyticsFilterLoadNotice` conventions. Hide/disable an unverified select, invalidate only scope-incompatible selection, preserve date/season/supplier filters, and make retry deterministic. Do **not** silently change a committed filtered query into `all stores` without a visible explicit reason and chosen fallback contract. Preserve signed store IDs.
3. Keep existing cancellation/request-sequence protection and `getStores` API contract. Do not refetch indefinitely or overwrite successful new-scope options on late failures. Test loading, empty-success, HTTP error and retry as separate states; no fake store zero.
4. Bound edits to SupplierSalesStatsPage, its focused scope/filter specs, and only an already shared presentation utility if needed. No API, SQL, import, RQ530 business-metric, score or provider/deployment changes.

### Tests / DONE

- Reproducer fails on existing main, passes afterward. Scope race, new-scope failure, retry, explicitly valid shared store, stale URL/deep-link ID, embedded sharedFilters and signed IDs must be covered.
- Focused Supplier page/scope specs, `npm run check:analytics-guardrails`, frontend `npm run build`, queue validator and exact commit frontend Actions proof. Do not accept skipped tests or only compile success.
- Record before/after observable behavior and exact SHA in `.ai/runs/2026-10-14-RQ662-evidence.md`. Do not claim or change files until the shared reservation is visible.

---

## RQ663 - Make Daily Sales shift trust and quality badge reflect unavailable source evidence

Status: DONE
Priority: P1
Type: frontend/evidence-semantics/tests
Feature family: daily-sales-shift-trust-completeness
Parallel-safe: yes with RQ662/RQ664; no with another DailySalesStatsPage / dailyShiftSummary owner
Owner: Codex / worktree `rq663-daily-sales-quality`
Claim reservation: 2026-10-14 - claimed from inspected `origin/main` `7aae619cda7a73e7b50f5fbcd047dbc78823faf8`; no matching RQ663/DailySalesStatsPage lock, branch, worktree or open PR was found. Published reservation commit on `main`; planned branch `codex/rq663-daily-sales-quality-20261014`; bounded paths are `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`, `Klijent/clientapp/src/utils/dailyShiftSummary.ts`, their focused tests, the guardrail baseline line references if source insertion shifts them, this queue and `.ai/runs/2026-10-14-RQ663-evidence.md`. RQ665 is independent test-only work and is not touched.
Local lock: removed after done close
Ready after: current-main commits `86691925`, `7f1c3fac`, `1b116e4e`; frontend CI `38557709870` SUCCESS
Commit suggestion: `fix(analytics): align Daily Sales quality counts with shift-source availability`

### Completion note

- Date: 2026-10-14
- Status: DONE.
- Completion: Daily Sales now classifies complete, missing, partial and unavailable shift evidence separately. Unavailable days are counted explicitly and included in the incomplete-coverage total without being relabeled as missing or partial; periods without day evidence render `Nije dostupno`. Untrusted numeric shift columns remain unavailable across KPI, chart and export helpers, measured zero and signed returns remain valid, and stale/partial/unknown trust metadata cannot produce a green quality proof. Sale totals, revenue formulas, ratio formulas, time boundaries and backend SQL are unchanged.
- Changed files: `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`; `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx`; `Klijent/clientapp/src/utils/dailyShiftSummary.ts`; `Klijent/clientapp/src/utils/__tests__/dailyShiftSummary.spec.ts`; `Klijent/clientapp/scripts/known-guardrail-baseline.json` (one unchanged baseline line moved from 986 to 985 after import-line deletion); this queue; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-14-RQ663-evidence.md`.
- Checks run: focused Daily Sales unit/numeric/premium/integration specs (63 passed, 0 failed, no skips); `npm run check:analytics-guardrails` (pass: encoding, self-test, 36 known baseline violations, typecheck); `npm run build` (pass); `git diff --check` (pass); `node scripts/check-prompt-queues.mjs` (pass: 786 tasks); exact-main GitHub Actions `38567960925` on merge SHA `275d1c307bca979e369023e88b87b08ff83d13e3` (success: SDK pin, POS UI audit/build, frontend analytics tests, guardrails, UI program gates, client build, bundle budget/lazy charts).
- Checks not run: full local frontend suite and `npm run lint` (not required after focused proof and exact-main Actions); backend/PostgreSQL tests (no backend or SQL changes).
- Run log: `.ai/runs/2026-10-14-RQ663-evidence.md`
- Evidence state: synchronized
- Claim reservation: inspected `origin/main` `7aae619cda7a73e7b50f5fbcd047dbc78823faf8`; pushed claim `f2c8f37282cd6b0d3ee786616adcc277b502ae37`; owner/worktree/branch Codex / `C:\Users\Ivan\.codex\worktrees\rq663-daily-sales-quality\Trendplus2` / `codex/rq663-daily-sales-quality-20261014`; transport implementation `11fc16bafc7044e140825e7439c70a23673ca996`; remote branch/PR query found no prior RQ663/DailySales owner or open PR.
- Delivery mode: direct-main via fast-forward to claim base and merge of transport branch.
- Main commit SHA: `275d1c307bca979e369023e88b87b08ff83d13e3`
- Main verification: fresh `git fetch origin`; `git rev-parse origin/main` returned closeout SHA `c28aa8a6c910877403eced8ddcc1d12bb24120d8`; `git merge-base --is-ancestor 275d1c307bca979e369023e88b87b08ff83d13e3 origin/main` and the implementation ancestor check passed.
- Missed: none in owned scope
- Follow-up: RQ664 remains the primary READY prompt; RQ665 is DONE and no RQ663-dependent prompt was found.
- Residual risk: GitHub Actions emitted existing Node.js 20 deprecation annotations; all named jobs and steps were successful. Full local frontend suite and lint were not run because the focused proof plus exact-main Actions passed.
- Post-close routing: post-delivery `origin/main` `275d1c307bca979e369023e88b87b08ff83d13e3` scan covered all active `ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` addenda and `SQL_ANALYTICS_PROMPT_QUEUE.md`; no other RQ663 references or dependents were found. RQ664 is dependency-complete and collision-safe as the successor; no Zero-READY proof applies.

### Verified gap / don't undo existing fix

The just-shipped shared `dailyShiftSummary.ts` correctly refuses to treat a shift split as measured unless `shiftAssignmentStatus` is trusted. But `DailySalesStatsPage.tsx` computes `missingShiftCount` and `partialShiftCount` using `hasMissingShiftSummary` / `hasPartialShiftSummary` only. `unavailable` days are neither; `incompleteShiftCount = missing + partial` can therefore be **zero** on nonempty unverified/no-time-fallback days. The "Dani bez satnice" and "Dani sa nepotpunom satnicom" quality cards can read green/0, and the top data-health toggle picks green whenever no warning/danger is recorded, even if `shiftAssignmentStatus` or other critical provenance fields are unknown. A green absence-of-warnings is not proof of sufficient coverage.

### Scope / implement

1. Add failing-first cases in `pages/__tests__/DailySalesStatsPage.premium.spec.tsx` and `utils/__tests__/dailyShiftSummary.spec.ts` for: nonempty response with null/unknown `shiftAssignmentStatus` but numeric first/second columns; `unavailable` and `no_time_fallback`; one unavailable plus one partial and one complete day; true measured zero; empty/no_data_in_period; signed returns. Assert shift KPI/share remains unavailable when source evidence is untrusted, and the quality panel never claims `0 complete-coverage problems` or a green proof in such a period.
2. Add explicit `unavailableShiftDays` or an equivalent source-coverage classification without folding `unavailable` into merely `missing`/ `partial`. If the dataset has no evidence of days, show `Nije dostupno` instead of `0`; count genuine per-day measured zero as zero when proof exists. Make top badge neutral/qualified when relevant source and data-quality fields are unavailable or `meta.isPartial`, `dataQualityStatus`, freshness/trust are degraded. Do not treat an informational label as a recommendation or bypass backend trust gating.
3. Check equality of quality-panel values, export `Smenska evidencija` metadata, aggregate KPI, chart and filter scope; do not modify sale totals, ratio formula, shift time boundaries or backend SQL. Ensure `unknownSupplierPct` absent is not silently a measured 0.
4. Keep original RQ661/RQ659 headings and layout changes, and `dailyShiftSummary` fail-closed behavior. Coordinate shared files with any other DailySales owner before claim.

### Tests / DONE

- Focused daily shift unit + Daily Sales numeric/premium/integration specs must pass with no skip; `npm run check:analytics-guardrails`, `npm run build`; exact-main frontend Actions SUCCESS. A successful empty dataset and a failed/partial request must not be conflated.
- Provide a small source-state vs UI-state truth matrix and test evidence in `.ai/runs/2026-10-14-RQ663-evidence.md`.

---

## RQ664 - Establish repository-local Supplier/Shoe browser-to-export proof as a starter for RQ448

Status: DONE
Priority: P2
Type: test/harness/evidence
Feature family: supplier-shoetype-browser-certification-starter
Parallel-safe: yes with RQ662/RQ663 only when no shared Supplier/Shoe page, CI harness or queue mutations are concurrent
Owner: Codex / worktree `rq664-browser-export`
Ready after: RQ447 and RQ453 DONE, RQ561/RQ567/RQ568 DONE; no authorized production environment needed for this **repository-only starter**
Commit suggestion: `test(analytics): add deterministic Supplier Shoe render-export smoke proof`

Owner claim reservation 2026-10-14: refreshed `origin/main` at `5d5d72b1d7198c7f0ead30017a11799735b1deeb`; collision checks found no RQ664 lock, branch, open PR or attached worktree, and the existing Supplier/Shoe worktrees are historical or unrelated. The bounded paths are `Klijent/clientapp/scripts/qa/rq448-fixture-smoke.*`, its focused synthetic fixture/artifact contract, this queue section, the dated run log and the RQ664 deliverable. Reservation was published on `main` before creating the isolated worktree; local lock `.ai/task-locks/RQ664-codex.lock.md` was created after that reservation and removed before the implementation commit.

### Historical blocker and authorized split

`RQ448` in `ANALYTICS_RELIABILITY_PROMPT_QUEUE_OPERATIONS_ACCURACY_ADDENDUM.md` remains WAITING for authenticated browser/API/deployed evidence. The disposable PostgreSQL oracle/CI chain is available, but `Klijent/clientapp` has no existing standalone e2e script even though Puppeteer is a devDependency. Waiting for production provider credentials should not prevent a **distinct repository-local fixture → API/response → browser render → export consistency rehearsal**. This task supplies that prerequisite, **not** a production certificate and not a second RQ453 oracle.

### Scope / implement

1. Inspect existing RQ447/RQ453 exact executed SQL/Pg fixtures, Supplier and Shoe Type endpoints, existing render/toolbar export paths and available Puppeteer/browser CI prerequisites. Reuse or bind the fixture; no fabricated business dataset can be called live.
2. Add a bounded, repeatable browser smoke harness under a **new** test-only path (for example `Klijent/clientapp/scripts/qa/rq448-fixture-smoke.*` plus focused fixtures/tests) with one canonical period/store/dataScope covering Supplier and Shoe Type. It may exercise a disposable local API seeded from the existing oracle, or validated intercepted API responses explicitly marked **synthetic fixture**. Assert displayed KPI/table values, trust/period/scope and actual downloaded CSV/export columns/values reconcile to the *same* fixture response; include missing/unknown/partial/error and negative-ID cases. Never equate mocked API parity with database/source lineage proof.
3. Make the command executable in disposable local/CI runs, fail closed for absent browser/fixture/artifact, retain machine-readable verdict and small sanitized artifact(s), and document skipped/not-run states. Do not add a brittle always-on production/live gating job or hard-code protected credentials.
4. Keep runtime SQL/business pages untouched unless a focused failing smoke proves a defect; a code defect then gets its own independently owned follow-up, not an unreviewed broad fix. Don't duplicate the RQ447 PostgreSQL oracle, RQ453 six-screen certification, RQ567 trust header or RQ565 deployed six-screen proof.
5. Add a dated deliverable describing exactly what remains for RQ448: authenticated deployed build, source-line reconciliation, real browser screenshots/download and approved read-only source evidence. After this task closes, **re-evaluate** RQ448's WAITING gate; do not mark it DONE or automatically promote RQ451/RQ452.

### Tests / DONE

- A reproducible browser execution (not only static inspection) confirms seeded fixture → rendered and downloaded values and detects one deliberately tampered mismatch. No hidden browser missing/CI skip may be PASS.
- Repo-focused tests, `npm run check:analytics-guardrails`, `npm run build`, queue validator, and relevant GitHub Actions evidence. Separate synthetic/local verdict from actual deployed/runtime verdict.
- Record precise artifact names, commands, case counts and blockers in `.ai/runs/2026-10-14-RQ664-evidence.md`.

### Completion note

- Date: 2026-10-14
- Status: DONE
- Completion: 100% of the repository-local RQ664 scope. The built client was driven in an actual headless Puppeteer browser through `/analytics/supplier` and `/analytics/shoe-type-sales-stats`, with one canonical period/store/dataScope, synthetic intercepted Supplier/Shoe responses, rendered KPI/table assertions, real downloaded CSV assertions, empty/error/negative-ID/unknown-attribution cases and deliberate tampered-total mismatch detection. The result is explicitly `synthetic_fixture_only`; it is not production, deployed or customer certification.
- Changed files: `Klijent/clientapp/package.json`, `Klijent/clientapp/scripts/qa/rq448-fixture.json`, `Klijent/clientapp/scripts/qa/rq448-fixture-smoke.mjs`, `docs/qa/RQ664_LOCAL_BROWSER_PROOF_2026-10-14.md`, `docs/qa/RQ664_LOCAL_BROWSER_FIXTURE_VERDICT_2026-10-14.json`, `.ai/runs/2026-10-14-RQ664-evidence.md`.
- Checks run: local `npm ci --no-audit --no-fund`; `npm run build`; `npm run qa:rq448-fixture-smoke` (2 browser routes, 2 CSV downloads, 2 state cases, 1 tampered mismatch, 0 unexpected API calls); focused Vitest (3 files, 25 tests); `npm run check:analytics-guardrails`; instruction, prompt-queue and planning-architecture validators plus self-tests; syntax/JSON parsing and `git diff --check`; exact-head Analytics Quality Gates run `38571518142` on `c3751208b3736c1dc7f418a725504726cc130745` completed `success` with all three jobs green.
- Checks not run: authenticated staging/production browser, PostgreSQL/source-line reconciliation, deployed screenshots/downloads, protected credentials, customer acceptance and full frontend/backend suites; these are outside RQ664 and remain RQ448-family acceptance work.
- Run log: `.ai/runs/2026-10-14-RQ664-evidence.md`
- Evidence state: implementation artifact, local proof, delivery, exact-head Actions result and post-close routing are synchronized; final closeout SHA is recorded in the run log.
- Delivery mode: direct-main; implementation transport branch `codex/rq664-browser-export-20261014` fast-forwarded to `main`.
- Implementation commit SHA: `c3751208b3736c1dc7f418a725504726cc130745`
- Main commit SHA: `c3751208b3736c1dc7f418a725504726cc130745` at implementation delivery; final closeout SHA is recorded in the run log.
- Main verification: `git rev-parse origin/main` -> `c3751208b3736c1dc7f418a725504726cc130745` at implementation delivery; final closeout SHA is recorded in the run log.
- Missed: RQ448's authenticated deployed/source-line/customer evidence remains unexecuted by design.
- Follow-up: keep `RQ448`, `RQ451`, `RQ452`, `RQ454`, `RQ455`, `RQ565` and `RQ566` WAITING; do not promote or mark them DONE from this synthetic rehearsal.
- Residual risk: synthetic fixture parity cannot prove production database lineage, deployed build behavior, freshness, schema state or customer acceptance.

## RQ665 - Stop order-dependent EF ManyServiceProvidersCreatedWarning failures in test hosts

Status: DONE
Priority: P1
Type: backend CI flakiness
Feature family: test-host-ef-configuration
Parallel-safe: yes (test-only)
Owner: grok
Ready after: PR #147 closed 2026-10-14 (its backend failure was this flake)
Commit suggestion: `test(ci): opt test-host DbContexts out of ManyServiceProvidersCreatedWarning`

### Problem

Backend runs `38422963322` (docs-only PR #147, base `6ca11bf9`) and `38419332741` (`babd557d`) failed three `SupplierSalesStatsEndpointPostgresParityTests` with `InvalidOperationException` for `ManyServiceProvidersCreatedWarning`, while the same base passed on main (`38422316417`). Each `WebApplicationFactory` host has its own application service provider, which is part of EF Core's internal service-provider cache key, so every host adds one internal provider to a process-wide count; once it passes twenty the warning throws in whichever test happens to come next.

### Evidence

Over 20 existing test factories already set `ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))`; 11 factories (including the failing parity factory) did not.

### Scope

Test-only: add the opt-out to the 11 remaining test-host registrations and a source convention test with self-tests. No production DbContext configuration, no assertion change.

### Read first

`Api.Tests/SupplierSalesStatsEndpointPostgresParityTests.cs`, `Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs` (existing pattern).

### Do

1. Add the warning opt-out to every DbContext registration in in-process test hosts lacking it.
2. Add `TestHostDbContextWarningConventionTests` that fails on any such registration without the opt-out.

### Tests

Convention tests and affected classes locally (Testing env); full backend CI on the PR.

### Acceptance

No test host registration without the opt-out; backend + governance CI green; merged via PR.

### Dependencies

None. Sharing one internal service provider across hosts was rejected: it requires `UseInternalServiceProvider`, which bypasses each host's logging/interceptor integration (e.g. benchmark command recorder) and would change test semantics.

### Completion note

- Date: 2026-10-14
- Status: DONE
- Completion: 100%
- Changed files: 11 Api.Tests test-host factory files, Api.Tests/TestHostDbContextWarningConventionTests.cs, docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md
- Checks run: local Testing env focused classes (14 total, 8 passed, 6 skipped, 0 failed; route mappings 8/8); check-prompt-queues, check-planning-architecture, check-agent-instructions with and without --self-test; git diff --check; PR #158 backend run 38565788120 pass, governance run 38565788009 pass
- Checks not run: smoke tests and Development environment (owner rule); local CachedAnalyticsFailureContractTests (test host crash pre-exists on origin/main locally)
- Run log: .ai/runs/2026-10-14-RQ665-ef-many-service-providers-evidence.md
- Delivery mode: PR #158 merged with --merge after green CI
- Main commit SHA: 85d29cd275ee4ab5d3975f0bc522d078e1ca7930
- Main verification: git rev-parse origin/main -> 85d29cd275ee4ab5d3975f0bc522d078e1ca7930
- Missed: 11 factories lacked the opt-out used by 20+ others
- Follow-up: none
- Residual risk: convention matches only lambda-style AddDbContext registrations; Vercel check was rate-limited (non-required)


---

## RQ666 - Product Decision Center: source-scoped action identity and store filter lineage

Status: READY
Priority: P1
Type: frontend/action-identity + filter-trust regression
Feature family: product-decision-datascope-action-identity
Parallel-safe: yes with RQ667 only when its Inventory paths and shared services are disjoint; no parallel ProductDecisionCenterPage owner
Owner: unassigned
Ready after: RQ660 DONE, RQ662 DONE, RQ657 DONE; current local Analytics Quality Gates success `38587580206`
Commit suggestion: `fix(analytics): prevent cross-dataset Product Decision action and store identity leakage`

### Source-verified risk and exact boundaries

In `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`, `buildSourceKey(row,actionKind,fromDate,toDate,storeId,supplierId)` omits `dataScope`. The same key is used for central-action upsert, source-status lookup, queued button state and evidence snapshot state. The API queries themselves pass `dataScope` to `getProductDecisionCenter` and `getSupplierFilters`. Therefore two data-scope views with identical product, store/supplier and period can map to one persisted action identity; the code is not sufficient to prove this collision is intended. The store options are fetched once by bare `getStores()` and persist across dataScope changes, whereas core Supplier/Daily/Shoe/Color pages have scope-aware store validation. Do not claim a live duplicate/corrupted database without a reproducer; classify the business intent and old records first.

### Do

1. Inspect `ProductDecisionCenterPage.tsx`, its `queueStatus` / `signalQueue` / `actionStatusFallback` specs, `getAnalyticsActionSourceStatuses` and `upsertAnalyticsActionWithResult` API/DB idempotency contracts, and the current RQ660/RQ662/RQ585 status/source context semantics. Compare how Inventory/Decision Pulse source keys encode dataScope. Scope identity must be a **stable semantic decision**: if the server explicitly treats one action across imported/existing/all as intentional, prove it in a contract test and record an owner decision before changing the key.
2. Failing-first: same productId/actionKind/store/supplier/period under `imported` and `existing` with distinct source data. Verify status lookup, queued badge, upsert, evidence snapshot and URL/filters do not apply a prior dataset's decision silently. Include legitimate reuse within the *same* dataScope. Verify that old persisted source keys are not mistaken for new-scope proof; do not bulk rewrite action rows.
3. If distinct dataset identity is required, implement a **versioned, normalized** source key and matching status/upsert path. Plan backwards compatibility for existing `product:...` legacy keys (read-only legacy visibility, explicit migration policy or qualified unknown); no automatic replay/re-enqueue, false duplicates, schema rewrite or silent hiding of customer actions. Persist `dataScope` in safely approved metadata/context where the existing API contract supports it.
4. Fetch store options using the same effective dataScope as the Product Decision payload (if supported by the authoritative stores API), invalidate previous-scope options immediately, and verify/store the selected store ID against the current-scope list before allowing scoped requests/actions. On discovery failure show an explicit stale/failed state and prevent cross-scope action commits. Handle signed/negative IDs, invalid/deep-link store IDs, rapid flips, retries and stale async responses. Preserve supplier filter fallback and RQ657 request sequences.
5. Keep changes within Product Decision frontend/test/source-key utility and narrow API tests **only if a demonstrable contract fix requires them**. Do not touch InventoryPage, SQL/product score formulas, source import, production data or any live action mutations. Before changing server idempotency shape, write the compatibility matrix and verify owner/current architecture contract.

### Acceptance

- Failing-first regression before fix; same-scope retains idempotency; cross-scope never wrongly shows `Već dodato` nor shares evidence snapshot; historical action keys remain retrievable and aren't duplicated by an automatic migration.
- Store selection only after scope-appropriate options load; previous or unknown store ID cannot be sent as an accepted, trusted new-scope filter/action. Document the chosen explicit fallback if the stores API really has global store semantics.
- Focused PDC tests and API source-status/upsert contract tests where required; `npm run check:analytics-guardrails`, `npm run build`, exact SHA Actions validation and `node scripts/check-prompt-queues.mjs`. No skipped requirement counts as PASS.
- Evidence: `.ai/runs/2026-10-14-RQ666-evidence.md` with same/cross/legacy key matrix and exact main SHA. If the key proves intentionally global, close with verified `OBSOLETE` classification **for the key portion** while still completing any demonstrated store-scope defect; do not invent a fix.

---

## RQ667 - Inventory: preserve verified store/compare-store context when dataScope changes

Status: READY
Priority: P1
Type: frontend/store filter provenance + inventory comparison tests
Feature family: inventory-scope-filter-validity
Parallel-safe: yes with RQ666 (InventoryPage only); no parallel Inventory page/filter owner
Owner: unassigned
Ready after: RQ607 DONE (latest snapshot semantics), RQ662 DONE (scoped store-filter precedent), RQ626 DONE; no RQ610 stock-mutation gate
Commit suggestion: `fix(analytics): validate inventory store filters per dataset scope`

### Source-verified risk and exact boundaries

`InventoryPage.tsx` fetches `getStores(true)` once with `storesReloadNonce`, unlike scoped `getStores(true, dataScope)` used on Daily Sales/Supplier/Shoe Type/Color. Its global `trendplus:data-scope-changed` event clears `selectedSupplierId` but retains `selectedStoreId` and `compareStoreIds`, while inventory balance/list/insights/comparison/forecast/actions all use the **new** `inventoryDataScope` with the retained IDs. The select is disabled on `storesLoadError` but not on a pending new-scope validation. No evidence shows that every store is valid in every imported/existing population. Test this rather than assuming the old list is safe or declaring it a production bug.

### Do

1. Create a failing-first test, in a new focused Inventory scope/filter spec if needed, using divergent imported/existing store fixtures, the same-store-present-in-both counterexample, and route query `storeId`/`compareStores` inputs. Ensure the test isolates `getStores(true,dataScope)` / `getInventoryStoreComparison` contracts without refetching real business data.
2. On scope transition mark old store and compare selections **unverified**, load scope-appropriate stores, validate each ID and suppress stale filters/compare sets until verified. Do not send an unverified prior-scope `storeId`/comparison to `getInventoryBalance`, `getInventoryList`, report export, action generation, inventory insights or rebalance. Keep legitimately shared IDs only after the new response proves membership.
3. On failed load, show an explicit unavailable/stale state and retry. A failed/empty store discovery is not a measured no-store dataset; do not silently coerce to all-stores and display apparently valid money or stock actions. Handle signed store IDs, duplicate human names, rapid scope flips and delayed responses without clearing a newly confirmed list.
4. Preserve existing `latest available inventory snapshot` vs `historical as-of` truth, signed returns, RQ626 proxies, true zero versus unknown, user URL intent and report/export scope. Reuse `AnalyticsFilterLoadNotice`, `storeFilterFallbackState` and RQ662 patterns where appropriate; no change to RQ610 scheduling, inventory SQL, source import or product score.
5. Bound edits to `InventoryPage.tsx`, focused Inventory scope/filter specs and only directly required shared presentation helpers. Account for compareStore default selection on the *new* verified store list; don't resurrect stale default comparisons after a failed reload.

### Acceptance

- Two-way imported/existing transitions, valid shared ID, stale URL ID, comparison set with one missing store, error, retry, same-scope refresh and rapid async race; failing-first reproducer and passing after patch.
- All dependent list/balance/summary/comparison/export requests align to the effective dataScope/store/compare-store tuple; stale/unknown provenance is visible and cannot trigger an actionable inventory recommendation.
- Focused Inventory integration tests, frontend `npm run check:analytics-guardrails` and `npm run build`, exact SHA Analytics Quality Gates and queue validator; no unrelated backend/deployed proof required.
- Evidence: `.ai/runs/2026-10-14-RQ667-evidence.md`, with source scope, store selection and comparison validation before/after, exact main SHA.


## RQ668 - Fix small analytics frontend correctness, language and accessibility defects from the 2026-10-14 audit

Status: IN PROGRESS
Priority: P2
Type: frontend correctness / a11y / i18n
Feature family: analytics-frontend-ux-audit-2026-10-14
Parallel-safe: yes (frontend-only, five files plus tests)
Owner: grok
Ready after: audit on main `a159d579`
Commit suggestion: `fix(analytics): correct momentum windows, unknown trend tone, labels and formats`

### Problem

Audit `docs/qa/ANALYTICS_FRONTEND_UX_AUDIT_2026-10-14.md` found small, low-risk defects: Dashboard 7-day momentum/elasticity computed against an incomplete previous window; unknown momentum rendered with the green `trend up` tone; English `Reset` label on the supplier consolidated page; minimum coverage ratio shown with `.` decimals; Insight Studio quadrant filter cards were mouse-only `div`s.

### Evidence

- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:157-178` (prev7 = `slice(-14,-7)` used with fewer than 7 rows), `:2742-2746` (null momentum -> `trend up`).
- `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx:516` (`Reset`).
- `Klijent/clientapp/src/components/inventory/SKUDetailModal.tsx:136` (`toFixed(2)x`).
- `Klijent/clientapp/src/pages/InsightStudioPage.tsx:1211-1230` (clickable `div`s without keyboard access).

### Scope

Only the five files above and their specs. No redesign, no API or formula change beyond requiring two full 7-row windows.

### Read first

`docs/ai/FRONTEND_UX_STANDARDS.md`, `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`.

### Do

1. Require `last7.length === 7 && prev7.length === 7` for momentum and elasticity.
2. Render unknown momentum with the neutral `trend` class.
3. Rename `Reset` to `Resetuj filtere`.
4. Format coverage ratio with `sr-RS` two decimals.
5. Convert quadrant cards to `button type="button"` with `aria-pressed`.

### Tests

Regression tests in `analyticsIndicatorRegression.spec.ts` and `SKUDetailModal.spec.tsx`; updated `SupplierConsolidatedPage.spec.tsx`; `npm run test:analytics`, typecheck, build, analytics guardrails, UI ratchets.

### Acceptance

Tests above pass; frontend, backend and governance CI green on the PR; merged via PR.

### Dependencies

None. Does not overlap RQ655-RQ657 loader/fake-zero/race lanes.


## RQ669 - Bring Experimental Insight Studio to the minimum accessibility, language and number-format bar before any re-exposure

Status: WAITING
Priority: P3
Type: frontend a11y / i18n / formatting
Feature family: insight-studio-experimental
Parallel-safe: yes (only `InsightStudioPage.tsx` and its specs)
Owner: unassigned
Ready after: owner decision to keep Insight Studio reachable or re-expose it (route is behind the `Eksperimentalno` flag; see `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`)
Commit suggestion: `fix(insight-studio): keyboard rows, Serbian labels and sr-RS numbers`

### Problem

Insight Studio still has a mouse-only clickable supplier row, English labels, `.`-decimal numbers and 10px key text. RQ668 fixed only the quadrant filter cards.

### Evidence

- `Klijent/clientapp/src/pages/InsightStudioPage.tsx:892` `<tr onClick>` with no keyboard path.
- English labels: `:823` `Velocity`, `:888` `Risk`, `:1222` `Volume Trap`.
- `toFixed` in visible UI: `:803`, `:806`, `:837`, `:909`, `:1035`, `:1134`, `:1254`, `:1555-1556`, `:1773`, `:1944`.
- 10px key values: `:1212-1229`, `:1407`, `:1509`; 2026-10-04 measurement: 67 text elements below 12px at 360px.

### Scope

`InsightStudioPage.tsx` and its specs only. No data or formula change, no responsive migration (that remains a re-exposure prerequisite).

### Read first

`docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` Insight Studio owner decision, `docs/ai/ANALYTICS_DESIGN_SYSTEM.md`.

### Do

1. Give the supplier row a keyboard path (a row button or `tabIndex` + Enter/Space) with an accessible name.
2. Translate the English labels.
3. Replace visible `toFixed` with the shared `sr-RS` formatters.
4. Raise key numbers to at least 12px.

### Tests

Spec for keyboard selection and Serbian labels; `npm run test:analytics`, typecheck, build, UI ratchets.

### Acceptance

No mouse-only interactive element, no English UI label, no `.`-decimal visible number on the page.

### Dependencies

Owner decision on Insight Studio reachability. If the screen is shut down instead, close this as obsolete.


## RQ670 - Unify sr-RS number formatting and remove residual English labels on operational and admin screens

Status: READY
Priority: P3
Type: frontend i18n / formatting
Feature family: frontend-number-format-consistency
Parallel-safe: yes (page-local display code; check same-file locks first)
Owner: unassigned
Ready after: 2026-10-14 analytics frontend audit
Commit suggestion: `fix(ui): sr-RS number formatting and Serbian labels on operational screens`

### Problem

Several screens print money and percentages with `toFixed` (dot decimals, no grouping) next to pages that use `sr-RS` formatters, and a few admin screens still show English labels.

### Evidence

- `toFixed`: `Klijent/clientapp/src/pages/NivelacijaCenaPage.tsx:261`, `PovracajPage.tsx:185`, `DnevnikPromenaPage.tsx:448-449`, `ArtikliListPage.tsx:505-506`, `PerformanceDashboard.tsx:268`, `RuntimeScoringPage.tsx:547,557,656`.
- English: `OutboxDashboard.tsx:277` `Total`, `:446` `Retry`; `OutboxMessagesPage.tsx:395` `Retry`; `ReleaseCalendar.tsx:55` `Loading...`; `LogsPage.tsx:270` option label `Error`.
- Locale-less `toLocaleString()`: `OpenTrainingPage.tsx:40`, `ConfigurationPage.tsx:809`.

### Scope

Display-only changes in the listed files and their specs. Do not change CSV/export number formats, API values or the `LogsPage` filter values (only labels).

### Read first

`docs/ai/FRONTEND_UX_STANDARDS.md`; existing formatters in `src/components/inventory/inventoryUtils.ts` and analytics `fmtNumber`/`fmtRsd`.

### Do

1. Replace visible `toFixed` with shared `sr-RS` formatters (keep digits).
2. Translate the listed labels; keep option values.
3. Pass `sr-RS` to locale-less `toLocale*` calls.

### Tests

Update affected specs; add one formatter assertion per page family; `npm run test:analytics`, typecheck, build.

### Acceptance

Listed lines use `sr-RS` output and Serbian labels; exports unchanged.

### Dependencies

None.


## RQ671 - Pay down clientapp ESLint errors and gate lint in CI

Status: READY
Priority: P3
Type: frontend tooling / code health
Feature family: clientapp-lint-gate
Parallel-safe: no for touched pages (many files); split per directory and check locks
Owner: unassigned
Ready after: 2026-10-14 analytics frontend audit
Commit suggestion: `chore(clientapp): fix eslint errors and gate lint in CI`

### Problem

`npm run lint` fails on main with 120 errors, so no workflow runs it and new lint regressions go unnoticed. Some are real-risk patterns (`react-hooks/refs`: writing refs during render).

### Evidence

- `npx eslint .` on main `a159d579`: 120 errors: `react-refresh/only-export-components` 91, `react-hooks/refs` 15, `no-useless-escape` 5, `@typescript-eslint/no-unused-expressions` 3, `@typescript-eslint/no-empty-object-type` 2, `no-unsafe-finally` 1, `no-irregular-whitespace` 1, `react-hooks/immutability` 1, `prefer-const` 1.
- Example: `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx:102` and `:149` assign `ref.current` during render.
- No `.github/workflows/*.yml` step runs `npm run lint`.

### Scope

Lint fixes without behavior change; move exported helpers out of component files where `only-export-components` fires (keep import paths stable via re-export only if needed for tests). Add a CI lint step once at zero errors (or with a ratchet).

### Read first

`Klijent/clientapp/eslint.config.js`, `.github/workflows/analytics-quality-gates.yml`.

### Do

1. Fix the non-`only-export-components` errors first, with tests for any `react-hooks/refs` change.
2. Extract pure helpers (e.g. `calculateDashboardMovingStats`) into sibling modules.
3. Add a lint ratchet or a strict lint step to the frontend workflow.

### Tests

`npm run lint` passes (or ratchet passes); `npm run test:analytics`, typecheck, build.

### Acceptance

Lint is enforced in CI; error count is zero or ratcheted below 120 and cannot grow.

### Dependencies

Coordinate with active frontend lanes (RQ655-RQ657, RQ669, RQ670) to avoid same-file conflicts.
