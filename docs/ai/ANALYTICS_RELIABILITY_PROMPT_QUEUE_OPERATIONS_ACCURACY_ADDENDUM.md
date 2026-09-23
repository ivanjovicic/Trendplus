# Analytics Reliability Prompt Queue - Operations Data Accuracy Addendum

Date: 2026-09-23
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none
Main RQ current READY prompt: none

Use this queue with `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

Purpose: add only the **remaining, non-duplicative** Operations correctness work discovered by the 2026-09-23 data-flow audit, with first priority on **Prodaja po dobavljačima** and **Prodaja po tipu obuće**.

Canonical queue work already present on current `main` must be preserved:
- `RQ406` — Supplier Footwear derived type metrics must not become authoritative when `articleStats` is truncated;
- `RQ407` — deterministic cross-screen proof pack/reconciliation for all eight Operacije routes;
- `RQ373`, `RQ378`-`RQ380` — Supplier Sales visible population, weighted margin/runtime validation and pre/post ownership;
- `RQ375`-`RQ377` — Shoe Type weighted margin, comparable cohort and detail trust/identity;
- `RQ381`, `RQ385`-`RQ400` — Daily Sales, Pre/Post, Pre-Nivelacija and Color correctness contracts.

All prompts below are `WAITING`. Do not claim or auto-promote them without dependency/collision checks.

## New audit facts

- Supplier Sales and Shoe Type Sales both classify historical sale lines through the **current** article master: `ProdajaStavke -> Artikli.IDDobavljac / Artikli.IDTipObuce`.
- Current `ProdajaStavke` does not carry immutable supplier/type-at-sale dimensions in the inspected schema. A later article-master edit can therefore change historical grouping unless attribution is frozen/reconstructed with explicit provenance.
- Canonical `RQ407` will create one deterministic expected-output proof pack, but Supplier/Shoe Type also benefit from an **implementation-independent raw-fact oracle** that cannot accidentally repeat the same production aggregation bug.
- One-time proofs do not prevent later drift from import changes, cache invalidation misses, materialized-view drift or mutable master data.

## Status summary

| Task | Status | Priority | Feature family | Purpose |
|---|---|---:|---|---|
| RQ411 | WAITING | P0 | operations-sale-dimension-attribution | Freeze or provenance-qualify supplier/type attribution for historical sale lines |
| RQ412 | WAITING | P0 | supplier-shoetype-independent-oracle | Independently reconcile Supplier/Shoe Type to raw facts after canonical RQ407 proof |
| RQ413 | WAITING | P1 | operations-runtime-drift-guard | Continuously detect post-import/cache/source drift and fail closed for decision signals |

---

## RQ411 - Immutable/provenance-bearing supplier and shoe-type attribution

Status: WAITING
Ready after: Supplier Sales backend owner lane is collision-safe; coordinate RQ373/RQ378-RQ380
Priority: P0
Type: backend-data-contract/migration/import/tests
Feature family: operations-sale-dimension-attribution
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ411-<agent>.lock.md`
Commit suggestion: `fix(analytics): freeze historical supplier and shoe type attribution`

### Problem

Supplier Sales and Shoe Type Sales join historical `ProdajaStavke` to the current `Artikli` row and group by current `IDDobavljac` / `IDTipObuce`. If either master value changes, an old sale can move between suppliers or shoe types even though the sale fact did not change.

The system must distinguish historically confirmed sale-time dimensions from reconstructed/frozen estimates and current-master fallback. A green endpoint test is not enough if the source attribution itself is time-variant.

### Evidence

- `Api/Services/AnalyticsDetailReadService.cs` joins sale lines to `Artikli` and reads `a.IDDobavljac` / `a.IDTipObuce`.
- `Api/Services/AnalyticsCostSnapshotService.cs` also projects supplier/type from current `Artikli`.
- Current sale-line schema/tests expose product, quantity, sale price and cost but not immutable supplier/type-at-sale IDs.
- Existing cost-snapshot work already proved an analogous temporal-drift risk for mutable product cost.

### Scope

- sale-line/domain persistence or equivalent append-only dimension snapshot;
- POS and Access/import/synthesis write paths;
- Supplier Sales and Shoe Type list/detail/snapshot projections;
- migration/backfill/provenance;
- focused backend/integration tests.

Do not redesign Supplier Decision scoring, weighted-margin formulas, comparable-cohort formulas or UI styling.

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `Api/Endpoints/AllEndpoints.cs`
- `Api/Services/AnalyticsDetailReadService.cs`
- `Api/Services/AnalyticsCostSnapshotService.cs`
- current sale-line model and migrations
- `Api/Services/AccessImportService.cs` and POS persistence path
- RQ373/RQ375-RQ380 evidence
- `MARGIN_FORENSIC_AUDIT_FINAL.md`

### Do

1. Define canonical attribution states, for example:
   - `sale_snapshot` — captured with the sale;
   - `reconstructed_history` — only when a dated source proves the as-of-sale value;
   - `frozen_current_master_backfill` — legacy row frozen from today's master;
   - `unknown`.
2. Capture immutable supplier/type attribution for every new sale, or implement an equivalent append-only fact/dimension snapshot.
3. Audit Access source and `DnevnikPromena` before claiming reconstruction. Never label current-master backfill as historically confirmed.
4. Freeze legacy fallback attribution so later edits to `Artikli.IDDobavljac` / `IDTipObuce` cannot silently rewrite already-frozen history.
5. Expose attribution basis/coverage in Supplier/Shoe Type trust/data-quality metadata where it can affect a decision. Estimated backfill must not look like confirmed sale-time truth.
6. Preserve signed quantity/revenue, existing cost-source provenance and completed RQ375-RQ380 semantics.
7. Define deleted/renamed/unknown dimension behavior and migration/bootstrap compatibility.

### Tests

- Sale snapshot A/X -> later article master B/Y -> historical Supplier/Shoe Type results remain A/X.
- Legacy row -> freeze/backfill -> mutate master -> frozen result remains deterministic while provenance remains estimated.
- Null/unmapped dimensions remain one non-overlapping unknown bucket.
- Import rerun/idempotency does not mutate frozen attribution.
- Migration/bootstrap regression with legacy rows.
- List/detail/export/report metadata uses the same attribution basis.

### Acceptance

- Current article-master edits cannot silently reclassify frozen historical sales.
- Historical attribution quality is explicit and machine-readable.
- Estimated/current-master backfill is never presented as confirmed sale-time fact.
- Supplier and Shoe Type paths use one attribution contract without regressing signed-sales, margin or comparable-cohort semantics.

### Dependencies

- Coordinate `RQ373`, `RQ378`, `RQ379`, `RQ380`; do not run concurrently on overlapping Supplier Sales backend paths.
- Preserve DONE `RQ375`-`RQ377`.
- If the source cannot prove historical dimensions, keep explicit fallback provenance instead of inventing accuracy.

---

## RQ412 - Independent raw-fact oracle for Supplier and Shoe Type

Status: WAITING
Ready after: RQ407 and RQ411; applicable Supplier Sales semantic prompts are DONE
Priority: P0
Type: backend-reference-oracle/integration-tests/evidence
Feature family: supplier-shoetype-independent-oracle
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ412-<agent>.lock.md`
Commit suggestion: `test(analytics): add independent supplier and shoe type oracle`

### Problem

Canonical `RQ407` provides the shared deterministic Operations proof pack and expected-output manifest. Supplier/Shoe Type still need a second-line oracle whose calculation is deliberately independent from the production aggregation code. Otherwise a fixture expectation copied from the same helper/query can preserve the same bug on both sides.

### Evidence

- Shoe Type has strong runtime/schema/fixture tests, but its RQ375-RQ377 evidence explicitly records missing live PostgreSQL/provider proof.
- Supplier Sales has manual verification SQL and cost-snapshot reconciliation, but no continuously enforced full revenue/quantity/dimension oracle.
- Snapshot reconciliation compares legacy vs snapshot-aware cost/margin; it does not prove the whole sales population, grouping and filtering contract.
- Canonical `RQ407` already owns the eight-route shared manifest, so this prompt must extend it rather than create a second seed pack.

### Scope

- `/api/analytics/supplier-sales-stats` and canonical Supplier overview;
- `/api/analytics/shoe-type-sales-stats`;
- independent reference SQL/query used only for verification;
- RQ407 fixture plus optional read-only live profile;
- reconciliation evidence artifacts.

Do not reuse production aggregation helpers in the oracle.

### Read first

- canonical `RQ407` and its evidence/manifest
- `scripts/check_supplier_sales_stats.sql`
- Supplier/Shoe Type integration tests and fixtures
- `Api/Endpoints/AllEndpoints.cs`
- margin/nivelacija policies
- RQ411 attribution contract

### Do

1. Build an independent reference calculation from raw `ProdajaZaglavlja`, `ProdajaStavke` and the canonical RQ411 dimension attribution.
2. Declare exact date, store, supplier, `dataScope`, signed quantity/revenue, unknown bucket, zero denominator and cost-coverage semantics.
3. For identical populations assert:
   - endpoint total revenue == oracle revenue;
   - endpoint signed quantity == oracle signed quantity;
   - mutually exclusive Supplier rows + unknown == total;
   - mutually exclusive Shoe Type rows + unknown == total;
   - shares use the declared denominator and reconcile within presentation rounding only;
   - no sale line is double-counted.
4. Add mutation cases: return/negative quantity, midnight boundaries, multiple stores, imported/existing scope, unknown dimension, renamed/deleted master row, confirmed snapshot and legacy attribution fallback.
5. Reconcile detail values to their table row population.
6. Emit JSON/Markdown evidence: filters, raw row count, endpoint/oracle totals, deltas, unknown share, attribution coverage and cost-source coverage.
7. Optional read-only live mode may sample fixed periods/stores; any unexplained delta fails that proof run. If live access is unavailable, report it as not run, never as pass.

### Tests

- Real PostgreSQL deterministic fixture using the RQ407 proof pack.
- Property/invariant: every authoritative sale line belongs to exactly one Supplier and one Shoe Type bucket.
- Row-sum/total equality and detail/list parity.
- Mutation tests for current-master changes after RQ411 freeze.
- Optional live read-only reconciliation.

### Acceptance

- Supplier/Shoe Type core values are proven against an implementation-independent oracle, not only snapshots of themselves.
- Any unexplained non-zero currency/quantity delta fails.
- Unknown/excluded population and denominators are explicit.
- The proof extends RQ407 rather than duplicating its fixture/test framework.

### Dependencies

- `RQ407`, `RQ411`.
- Coordinate Supplier owner work `RQ373`, `RQ378`-`RQ380`.
- Live-provider mode may depend on `STAB16`; deterministic PostgreSQL proof must not.

---

## RQ413 - Continuous Operations drift/cache/freshness guardrails

Status: WAITING
Ready after: RQ407 and RQ412
Priority: P1
Type: backend-observability/cache-invalidation/tests
Feature family: operations-runtime-drift-guard
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ413-<agent>.lock.md`
Commit suggestion: `feat(analytics): detect operations data drift and stale decisions`

### Problem

A one-time proof can become stale after import changes, cache-key drift, missed invalidation, materialized-view refresh problems, schema changes or master-data edits. Accuracy needs a durable integrity signal, not only a passing test suite.

### Evidence

Operacije surfaces mix live facts, caches, materialized views, snapshots and refresh workers. Existing RQs repair individual cache/freshness contracts, but there is no common integrity state backed by raw-fact reconciliation probes.

### Scope

- bounded reconciliation/drift probes reusing RQ407/RQ412;
- import/refresh/cache invalidation verification;
- integrity health/telemetry metadata;
- recommendation/actionability fail-closed behavior on confirmed material drift;
- operator evidence.

Do not run full-table reconciliation synchronously on normal user requests.

### Read first

- analytics cache/invalidation services
- Access import completion path
- refresh/nightly workers
- snapshot/data-quality health endpoints
- RQ396 cache/freshness evidence
- RQ407/RQ411/RQ412 evidence

### Do

1. Define backend-owned integrity states at minimum: `verified`, `unverified`, `degraded`, `drift_detected`, with last-check timestamp and evidence ID.
2. Run bounded representative probes after relevant import, aggregate/materialized refresh, snapshot/data-quality activation and cache invalidation/prewarm events.
3. Emit exact deltas and dimensions; quantity/currency partitions expect zero unexplained delta, with tolerance only for declared presentation rounding.
4. Add stale-cache regressions: warm cache, mutate/import facts, execute official invalidation, prove next read reflects canonical data.
5. Add RQ411 temporal-drift regression: current article supplier/type edits do not move frozen history.
6. On confirmed material drift for a decision-critical dataset, block recommendation/actionability and expose trust metadata. Descriptive data may remain visible with warning where safe.
7. Monitor failure is `unverified/degraded`, never `verified`.
8. Persist/log compact evidence suitable for support and regression triage.

### Tests

- import -> invalidation -> read parity;
- materialized refresh -> read parity;
- deliberately stale cache -> drift detected;
- monitor unavailable -> unverified/degraded;
- confirmed drift -> decision signal blocked;
- bounded probe performance test.

### Acceptance

- Post-deploy/import drift can be detected and explained before users must notice wrong numbers.
- Confirmed material drift cannot remain visually green on decision-critical Operacije surfaces.
- Cache/freshness/invalidation behavior has repeatable proof and durable evidence.
- Integrity checks remain independent enough from production aggregation to catch shared-code regressions.

### Dependencies

- `RQ407`, `RQ412`.
- Coordinate existing cache/freshness owners, especially RQ396 and Supplier/Inventory paths.
- Keep probes bounded/read-only; remediation remains a separate owner action.
