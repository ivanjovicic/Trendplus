# Analytics Reliability RQ queue — post-import correctness and business value

Date: 2026-10-11
Program: RQ (part of canonical `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` family).
Current READY prompt: none (post-close recovery at origin/main 530e99503b8b914c8740cafdbb82513392877341 scanned 21 active execution queues, 2 future planning queues and the historical NEXT ledger; no READY task or new RQ618 dependency remains. RQ610 is still BLOCKED pending the all-writer stock-generation/POS contract and isolated PostgreSQL test repair).
RQ610 was BLOCKED after the company-grain capture delivery and was reopened for bounded, repository-local source qualification. The probe confirms structurally store-keyed `Artikli` rows, but no trustworthy inventory mutation generation has been established across import, manual edit, transfer and POS paths. Worker/schema ownership is released until the documented source-generation contract is supplied. This pointer is advisory; apply `docs/ai/PROMPT_QUEUE_PROTOCOL.md` and `MASTER_ROADMAP.md` for global routing.

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
