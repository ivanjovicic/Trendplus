# Analytics Reliability RQ queue — post-import correctness and business value

Date: 2026-10-11
Program: RQ (part of canonical `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` family).
Current READY prompt: RQ608
Additional path-disjoint READY: RQ609 and RQ612.
Next: RQ610 after RQ609; RQ611 after RQ610, with fresh collision checks. This pointer is advisory; apply `docs/ai/PROMPT_QUEUE_PROTOCOL.md` and `MASTER_ROADMAP.md` for global routing.

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
Local lock: `.ai/task-locks/RQ608-codex.lock.md`
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

---

## RQ609 - RQ601 provenance migration compatibility and read-only release preflight

Status: READY
Priority: P0
Type: schema-test/preflight script
Feature family: purchase-cost-provenance-schema-readiness
Parallel-safe: yes; new preflight tooling/isolated migration test, no RQ608 or RQ612 source files; owns schema-test lane until close
Owner: Analytics Schema / Release QA (unclaimed)
Ready after: RQ601 nullable EF/bootstrap migration exists; original 20 calculations are final business proof, not a start gate
Commit suggestion: `test(analytics): fail closed on cost provenance schema mismatch`

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

Status: WAITING
Priority: P1
Type: inventory-worker/SQL/tests
Feature family: inventory-observed-daily-capture
Parallel-safe: no with active schema/worker owner; run after RQ609 and refresh collision check
Owner: Inventory Capture (unclaimed)
Ready after: RQ609 DONE, RQ96 DONE
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

---

## RQ611 - Real per-store/per-day source-coverage evidence for sales and markdown measurement

Status: WAITING
Priority: P1
Type: import-metadata/analytics-integrity/tests
Feature family: imported-store-day-coverage
Parallel-safe: no while RQ610 owns worker/bootstrap paths
Owner: Import/Data Quality (unclaimed)
Ready after: RQ610 shared worker/schema ownership released; RQ569 horizon contract DONE
Commit suggestion: `feat(analytics): prove store-day importer completeness`

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

---

## RQ612 - Find feasible nonzero-sale shadow controls for the next markdown pilot

Status: READY
Priority: P1
Type: read-only-SQL/methodology/tests
Feature family: markdown-shadow-control-validity
Parallel-safe: yes; create a **new** shadow-only query/report/test, do not overwrite RQ602 frozen 40-member manifest or touch RQ609 schema/RQ608 test files
Owner: Analytics Value / Decision Science (unclaimed)
Ready after: RQ602 frozen historical evidence exists. No authorization for prospective action is implied.
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
