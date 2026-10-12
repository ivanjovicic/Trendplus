# Analytics Reliability RQ queue — post-import correctness and business value

Date: 2026-10-11
Program: RQ (part of canonical `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` family).
Current READY prompt: RQ622 primary (P1 Price Intelligence cache readiness and fail-closed parity). RQ621 is DONE on `d500304bdc767f32de773e07d534a3a50a0b12ae`; exact-main Analytics Tests & Data Integrity run `38226160681` passed, including the PostgreSQL regression and RQ453 certification. RQ620 is DONE on `876ed0aaa52c5526ffb0ed343e46fb8a39b1a2c1`; RQ625 and RQ626 remain WAITING because their API DTO/client paths serialize behind RQ621/RQ622/RQ624 and each other. BCI17 is DONE on current-main SHA `8ea2ba6acb03d59f09136d197056f8d5496d60e5`. RQ619 is DONE on `a9281ba4d8f7b119fdf6ba10df623a7b4d7dfa3f`; RQ623 is DONE on `d20b75a6db69b05aea8d8a908614831b6fce07e9`.
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

Status: IN_PROGRESS
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

Status: READY
Priority: P1
Type: startup/MV lifecycle/API trust-contract regression
Feature family: price-intelligence-cache-truth
Parallel-safe: no; `DatabaseInitializer` overlaps RQ619 and price API/SQL schema may overlap RQ621
Owner: unassigned
Ready after: RQ619 DONE and RQ621 DONE; code proof only, no provider access necessary
Commit suggestion: fix(analytics): fail closed on stale price intelligence cache

### Problem
Price Intelligence alone bypasses the readiness/hash checks applied to Demand, Inventory and Trend. A semantic SQL change with unchanged MV column structure can leave a pre-existing price MV stale while the price endpoint returns HTTP 200; missing relations may be represented as an empty-success response.

### Evidence
`DatabaseInitializer.AnalyticsIntelligenceCacheBySqlFile` lists only 021, 022 and 024; SQL 023 uses `CREATE MATERIALIZED VIEW IF NOT EXISTS` with no `cache_refresh_state` ownership. `QueryPriceIntelligenceAsync` passes `freshnessCacheName=null, freshnessSqlPath=null`; price GET calls `QueryPriceIntelligenceAsync` directly rather than `ExecuteCacheQueryAsync`. `ExecutePagedQueryAsync` marks missing relations unavailable only when a freshness cache name exists. Nightly refresh can happen, but cannot certify exact startup SQL/hash parity when disabled.

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

Status: WAITING
Priority: P1
Type: clock-boundary PostgreSQL MV/worker lifecycle and API freshness proof
Feature family: intelligence-cache-calendar-rollover
Parallel-safe: no; startup state methods and endpoint readiness overlap RQ619/RQ622; do not claim together
Owner: unassigned
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

---

## RQ625 - Separate missing external trend evidence from a measured zero trend score

Status: WAITING
Priority: P2
Type: SQL/API/React missing-evidence propagation and signal trust
Feature family: trend-momentum-external-signal-provenance
Parallel-safe: no; shares SQL 024 with RQ623/RQ620 and the intelligence endpoint/client DTO files with RQ621/RQ622/RQ624
Owner: unassigned
Ready after: RQ623 DONE, RQ620 DONE and RQ621/RQ622/RQ624 DONE to serialize shared intelligence endpoint/client DTO paths; no external provider credentials required for null-versus-zero fixture
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
RQ623/RQ620 DONE, separate from STAB16/provider data collection.

---

## RQ626 - Audit negative reconstructed inventory and stockout classification instead of silent clamping

Status: WAITING
Priority: P2
Type: inventory proxy semantic characterization and SQL/UI safety oracle
Feature family: reconstructed-stock-negative-evidence
Parallel-safe: no; shares SQL 022 with RQ620 and the intelligence endpoint/client DTO files with RQ621/RQ622/RQ624/RQ625
Owner: unassigned
Ready after: RQ620 DONE, SQL 022 nullable quantity fix on main, RQ621/RQ622/RQ624 DONE, and RQ625 DONE to serialize shared intelligence endpoint/client DTO paths; no production stock writes
Commit suggestion: test(inventory): distinguish negative proxy from verified stockouts

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

---

## RQ627 - Align Price Intelligence product snapshot dates with the certified UTC calendar

Status: WAITING
Priority: P2
Type: timestamp-with-time-zone read/oracle and bounded SQL contract correction
Feature family: price-intelligence-day-timezone
Parallel-safe: no; shares SQL 023, price cache and DTO with RQ621/RQ622
Owner: unassigned
Ready after: RQ621 DONE and RQ622 DONE; independent of deployed provider
Commit suggestion: fix(analytics): normalize price signal day to UTC

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
RQ621/RQ622 DONE; avoid simultaneous SQL 023 changes.

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

Status: READY
Priority: P2
Type: tests/CI schema-drift guard
Feature family: ef-migration-discovery-guard
Parallel-safe: yes (new PostgreSQL test only; must not edit `DatabaseInitializer.cs`, which RQ622 owns)
Owner: unassigned
Ready after: RQ629 DONE
Commit suggestion: `test(analytics): catalog-vs-model drift guard after migrations`

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
