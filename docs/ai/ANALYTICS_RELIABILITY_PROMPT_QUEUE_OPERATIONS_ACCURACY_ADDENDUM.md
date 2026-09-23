# Analytics Reliability Prompt Queue - Operations Data Accuracy Addendum

Date: 2026-09-23
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none
Main RQ current READY prompt: none

Use this queue with `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

Purpose: close the remaining numerical-truth and durability gaps for screens reachable from the **Operacije** menu, with first priority on **Prodaja po dobavljačima** and **Prodaja po tipu obuće**. This addendum does not duplicate the already delivered margin, comparable-cohort, payload-validation, cache/freshness or detail-provenance work in RQ375-RQ405.

All prompts in this addendum start as `WAITING`. Do not claim or auto-promote them without refreshing dependencies, feature-family ownership and collision checks.

## Audit facts that motivate this addendum

- `/analytics/supplier-sales-stats` is a compatibility route that redirects to the canonical Supplier screen, `/analytics/supplier?tab=overview`; the user-facing menu label therefore does not own a separate page anymore.
- Supplier Sales and Shoe Type Sales both derive historical grouping through `ProdajaStavke -> Artikli`, then read the **current** `Artikli.IDDobavljac` / `Artikli.IDTipObuce`. `ProdajaStavke` currently does not carry immutable supplier/type-at-sale dimensions. Historical rows can therefore be reclassified when current article master data changes unless an explicit frozen/reconstructed attribution contract is introduced.
- RQ375-RQ377 materially strengthen Shoe Type weighted margin, comparable pre/post cohorts, runtime payload validation and detail provenance, but their evidence explicitly says that live PostgreSQL/provider proof was not run.
- Supplier Sales still has owner work in RQ373/RQ378-RQ380. New prompts below must coordinate with that lane rather than reimplement its visible-scope, weighted-margin, runtime-schema or pre/post contracts.
- Existing snapshot reconciliation endpoints compare legacy vs snapshot-aware **cost/margin** behavior. They are not an independent proof that all revenue, quantity, dimension attribution, filters and unknown buckets equal raw business facts.
- Several analytics integration tests are environment-gated and return early when the integration profile is disabled. A green test count must not be used as evidence that a database-backed truth check actually ran.

## Status summary

| Task | Status | Priority | Feature family | Purpose |
|---|---|---:|---|---|
| RQ406 | WAITING | P0 | operations-sale-dimension-attribution | Freeze or explicitly qualify supplier/type attribution for historical sale lines |
| RQ407 | WAITING | P0 | supplier-shoetype-raw-reconciliation | Independently reconcile Supplier/Shoe Type API values to raw sales facts |
| RQ408 | WAITING | P1 | operations-cross-screen-invariants | Add an invariant/reconciliation matrix for every Operacije surface |
| RQ409 | WAITING | P1 | operations-postgres-truth-proof | Make database-backed correctness proof non-false-green and evidence-producing |
| RQ410 | WAITING | P1 | operations-runtime-drift-guard | Detect cache/import/source drift continuously and fail closed for decision signals |

---

## RQ406 - Immutable sale-time supplier and shoe-type attribution

Status: WAITING
Ready after: Supplier Sales owner lane RQ373/RQ378-RQ380 is DONE or explicitly reprioritized by the owner
Priority: P0
Type: backend-data-contract/migration/import/tests
Feature family: operations-sale-dimension-attribution
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ406-<agent>.lock.md`
Commit suggestion: `fix(analytics): freeze historical supplier and shoe type attribution`

### Problem

Supplier Sales and Shoe Type Sales join historical `ProdajaStavke` to the current `Artikli` row and group using current `IDDobavljac` / `IDTipObuce`. If either article dimension changes later, an old sale can move between suppliers or shoe types even though the sale itself did not change.

This is a source-of-truth problem, not a UI formatting problem. Trendplus must distinguish a historically confirmed dimension from a reconstructed/frozen estimate or current-master fallback.

### Evidence

- `AnalyticsDetailReadService` joins sale lines to `Artikli` and reads `a.IDDobavljac` / `a.IDTipObuce`.
- Supplier/Shoe Type snapshot/reconciliation code also projects those dimensions from `Artikli`.
- Current `ProdajaStavke` schema/tests expose sale-line product, quantity, price and cost, but not immutable supplier/type-at-sale fields.
- Existing cost-snapshot work already documents a similar temporal-drift class for historical costs; supplier/type attribution needs its own explicit contract.

### Scope

- sale-line/domain persistence needed for immutable or provenance-bearing supplier/type attribution;
- POS sale creation and Access/import/synthesis paths;
- Supplier Sales and Shoe Type Sales endpoint/query/detail/snapshot projections;
- migrations/backfill tooling;
- trust/provenance DTOs only where required to expose attribution basis/coverage;
- focused backend/integration tests.

Do not redesign Supplier Decision scoring, margin formulas, Shoe Type comparable-cohort formulas or screen styling.

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `Api/Endpoints/AllEndpoints.cs` Supplier/Shoe Type stats handlers
- `Api/Services/AnalyticsDetailReadService.cs`
- `Api/Services/AnalyticsCostSnapshotService.cs`
- `Domain/Model/ProdajaStavka.cs` or the current sale-line model
- `Api/Services/AccessImportService.cs` and POS sale persistence path
- RQ373/RQ375-RQ380 evidence
- `MARGIN_FORENSIC_AUDIT_FINAL.md`

### Do

1. Define the canonical dimension-attribution contract for a sale line:
   - `sale_snapshot` when supplier/type was captured with the sale;
   - `reconstructed_history` only when a dated source proves the historical value;
   - `frozen_current_master_backfill` when legacy history can only be frozen from today's article master;
   - `unknown` when no defensible attribution exists.
2. Prefer immutable IDs on the sale line (or an equivalent append-only fact/dimension snapshot) for all new sales. Preserve names/keys only if needed for rename/deletion resilience; do not duplicate mutable master data without a reason.
3. Audit Access source and `DnevnikPromena` before claiming reconstruction. Do not label a backfill as historically confirmed unless the source proves the value as of the sale date.
4. Freeze legacy fallback attribution so future edits to `Artikli.IDDobavljac` / `IDTipObuce` cannot silently rewrite already-frozen history.
5. Add attribution provenance and coverage to Supplier/Shoe Type trust/data-quality metadata. Decision/recommendation surfaces must not present `frozen_current_master_backfill` as confirmed sale-time truth.
6. Define compatibility behavior for old databases/migrations and for deleted/unknown suppliers/types.
7. Keep signed quantity/revenue semantics and existing cost-source provenance unchanged.

### Tests

- Seed a sale with supplier A/type X, persist a confirmed sale snapshot, mutate the current article to supplier B/type Y, and prove historical Supplier/Shoe Type results remain A/X.
- Seed legacy rows without snapshot fields, run the backfill/freeze path, mutate the article master, and prove the frozen legacy attribution stays deterministic while provenance remains `frozen_current_master_backfill`.
- Unknown/null supplier and type remain one non-overlapping unknown bucket.
- Import re-run/idempotency does not change frozen attribution unexpectedly.
- Migration/bootstrap test on a database with legacy rows.
- Detail/list/export/report metadata carries the same attribution basis/coverage.

### Acceptance

- A current article-master edit cannot silently reclassify sale lines whose attribution has been frozen.
- Historical attribution quality is explicit and machine-readable; estimated/current-master backfills are never presented as confirmed sale-time facts.
- Supplier and Shoe Type list/detail/report paths use the same dimension-attribution contract.
- No existing signed-sales, margin-cost or comparable-cohort semantics regress.

### Dependencies

- Coordinate with `RQ373`, `RQ378`, `RQ379`, `RQ380`; do not run concurrently on overlapping Supplier Sales backend paths.
- Shoe Type contract work `RQ375`-`RQ377` is DONE and must be preserved.
- If a historically correct reconstruction requires source data that does not exist, do not invent it; complete with explicit fallback provenance and residual risk.

---

## RQ407 - Independent raw-fact reconciliation for Supplier and Shoe Type

Status: WAITING
Ready after: RQ406 and the applicable Supplier Sales RQ373/RQ378-RQ380 contracts are DONE
Priority: P0
Type: backend-reference-oracle/integration-tests/evidence
Feature family: supplier-shoetype-raw-reconciliation
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ407-<agent>.lock.md`
Commit suggestion: `test(analytics): reconcile supplier and shoe type to raw sales facts`

### Problem

Contract tests, golden snapshots and endpoint self-consistency can all pass while the endpoint is consistently wrong. Supplier/Shoe Type need an independent oracle that starts from raw business facts and does not reuse the production aggregation implementation.

### Evidence

- Shoe Type has strong focused tests and runtime schema validation, but live database proof is explicitly absent in RQ375-RQ377 evidence.
- Supplier Sales has a manual SQL verification script and cost-snapshot reconciliation, but neither is a continuously enforced full raw-sales reconciliation contract.
- Existing snapshot reconcile endpoints answer “legacy vs snapshot-aware cost/margin delta”, not “does every reported revenue/quantity/group exactly equal the authoritative filtered sale lines?”

### Scope

- Supplier Sales `/api/analytics/supplier-sales-stats` and canonical Supplier overview consumption;
- Shoe Type `/api/analytics/shoe-type-sales-stats`;
- independent reference SQL/query code under tests/QA;
- deterministic Postgres fixtures and optional read-only live proof;
- evidence artifact generation.

Do not copy production helper methods into the oracle.

### Read first

- `scripts/check_supplier_sales_stats.sql`
- Supplier/Shoe Type integration tests and fixtures
- `Api/Endpoints/AllEndpoints.cs`
- `Application/Analytics/AnalyticsMarginPolicy.cs`
- `Application/Analytics/AnalyticsNivelacijaSplitPolicy.cs`
- RQ373/RQ375-RQ380 evidence
- RQ406 attribution contract

### Do

1. Build an independent reference calculation from `ProdajaZaglavlja`, `ProdajaStavke` and the canonical RQ406 dimension snapshot/provenance.
2. Declare one exact contract for:
   - date boundaries and timezone/whole-day semantics;
   - store filter;
   - `dataScope`;
   - supplier filter;
   - signed quantity and signed revenue;
   - null/unknown supplier/type;
   - zero-revenue denominator behavior;
   - cost/margin source tiers and covered-revenue denominator.
3. For identical populations assert:
   - endpoint total revenue == oracle revenue;
   - endpoint total quantity == oracle signed quantity;
   - sum of mutually exclusive dimension rows == total, including explicit unknown bucket;
   - row share denominator matches the declared total and shares reconcile within rounding tolerance;
   - supplier and Shoe Type grouping never double-count a sale line.
4. Add mutation fixtures: return/negative quantity, midnight boundaries, multiple stores, imported/existing data, unknown dimension, renamed/deleted master row, RQ406 legacy fallback and confirmed snapshots.
5. Reconcile detail route values to the same row population used by the table.
6. Produce a JSON/Markdown evidence artifact containing filter set, raw line count, endpoint totals, oracle totals, deltas, unknown share, dimension-attribution coverage and cost-source coverage.
7. Use exact decimal comparison for currency/quantity wherever possible; limit tolerances to presentation rounding (for example 0.01 percentage point), never to hide unexplained material deltas.

### Tests

- Deterministic PostgreSQL fixture covering every case above.
- Property/invariant test: each authoritative sale line belongs to exactly one Supplier bucket and exactly one Shoe Type bucket for the selected attribution contract.
- Row-sum/total equality tests.
- Detail/list parity tests.
- Optional read-only live profile over several fixed periods/stores/data scopes; discrepancies produce evidence and fail that proof run.

### Acceptance

- Supplier and Shoe Type core revenue/quantity/grouping are proven against an implementation-independent oracle, not only against snapshots of themselves.
- Any non-zero unexplained delta fails the proof.
- Unknown/excluded populations and every denominator are explicit in evidence.
- The canonical Supplier overview and Shoe Type screen cannot claim verified numerical truth without the corresponding reconciliation result.

### Dependencies

- `RQ406`.
- Preserve `RQ375`-`RQ377` and complete/coordinate `RQ373`, `RQ378`-`RQ380`.
- Live-provider mode may depend on `STAB16`; hermetic PostgreSQL proof must not.

---

## RQ408 - Operacije cross-screen invariant and reconciliation matrix

Status: WAITING
Ready after: RQ407; owner-specific correctness contracts for the touched surface are DONE or path-safe
Priority: P1
Type: cross-surface-contract/tests/docs
Feature family: operations-cross-screen-invariants
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ408-<agent>.lock.md`
Commit suggestion: `test(analytics): add operations cross-screen invariants`

### Problem

The Operacije menu is a business workflow, but correctness evidence is currently split by page. A regression can make two individually “green” screens disagree on the same underlying sales population.

### Evidence

Current Operacije surfaces include Inventory, canonical Supplier overview, Shoe Type Sales, Daily Sales, Pre/Post Nivelacija, Color Sales, Pre-Nivelacija priorities and Supplier/Shoe Type assortment. Existing audits have repaired many local contracts, but there is no single executable matrix proving shared filter/population invariants across the menu.

### Scope

- test/documentation contract across all Operacije routes;
- screen/API/source/filters/cache/provenance matrix;
- cross-endpoint numerical invariants where business populations are comparable;
- no broad UI redesign.

### Read first

- `docs/ai/OPERATIONS_AUDIT_PROMPTS_2026-09-21.md`
- `docs/qa/ANALYTICS_TRUST_SCREEN_MATRIX_2026-09-04.md`
- `docs/qa/ANALYTICS_ROUTE_LINEAGE_MATRIX_2026-09-05.md`
- owner evidence for RQ371-RQ405
- RQ407 reconciliation evidence

### Do

1. Create/refresh one authoritative Operacije lineage matrix with, for every route:
   - user-facing route and redirect target;
   - frontend page/client;
   - API endpoint(s);
   - source tables/views/materialized data;
   - date/store/supplier/dataScope semantics;
   - metric units and numerator/denominator;
   - unknown/exclusion policy;
   - cache/freshness/invalidation owner;
   - recommendation authority and fail-closed condition;
   - exact proof tests/evidence.
2. Add executable cross-screen invariants where populations match:
   - Supplier known + unknown revenue == raw filtered sales revenue;
   - Shoe Type known + unknown revenue == the same raw revenue;
   - Color known + unknown revenue == the same raw revenue when its filters/exclusions are identical;
   - Daily Sales daily/shift/supplier partitions + “other” reconcile to its total signed quantity/revenue;
   - pre/post observed totals reconcile to their declared observed sales cohort, while comparable cohort remains explicitly separate;
   - Supplier assortment totals reconcile to the canonical Supplier population for the same filters;
   - Inventory metrics are not forced to equal sales totals, but their stock/value/freshness source and period semantics must be explicit and internally reconcilable.
3. If two surfaces intentionally use different populations, codify the difference as provenance/exclusion metadata instead of weakening the invariant.
4. Add boundary fixtures for returns, zero lines, unknown dimensions, dataScope, store and date limits.
5. Ensure exports/details/actions use the same semantics as the visible row/KPI they originate from.

### Tests

- Cross-endpoint fixture tests with one common seeded dataset.
- Invariant helpers that fail with a readable delta showing source rows and filters.
- Frontend route/redirect test proving “Prodaja po dobavljačima” opens the canonical overview and carries active filters.
- Existing focused suites for Daily Sales, Pre/Post, Color, Pre-Nivelacija, Inventory, Supplier and Shoe Type.

### Acceptance

- Every Operacije screen has a documented and executable source-to-screen lineage.
- Comparable sales totals cannot drift silently between Supplier, Shoe Type, Color and Daily Sales.
- Intentional population differences are visible and test-locked.
- No screen presents a value as total/verified when it only represents a returned page, top-N subset or comparable subcohort.

### Dependencies

- `RQ407`.
- Coordinate unresolved Inventory `RQ371`/`RQ372` and Supplier `RQ373`/`RQ378`-`RQ380`.
- Preserve completed Daily Sales/Pre-Post/Pre-Nivelacija/Color contracts `RQ381`, `RQ385`-`RQ400`.

---

## RQ409 - Non-false-green PostgreSQL truth proof and evidence gate

Status: WAITING
Ready after: RQ407 and RQ408
Priority: P1
Type: test-infrastructure/CI/evidence
Feature family: operations-postgres-truth-proof
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/RQ409-<agent>.lock.md`
Commit suggestion: `test(analytics): require real postgres operations truth proof`

### Problem

Some analytics integration tests return early when `TRENDPLUS_RUN_INTEGRATION_TESTS` is not enabled. xUnit can therefore report a passing test method even though its database-backed assertions never executed. This is useful for optional local tests but is not valid correctness evidence.

### Evidence

RQ375-RQ377 run logs explicitly record that live PostgreSQL/provider proof was not run. `AnalyticsShoeTypeSalesIntegrationTests` contains environment-gated early returns. Similar proof language exists across analytics evidence.

### Scope

- dedicated Operations truth-proof test profile;
- PostgreSQL fixture/service-container setup;
- CI/release evidence command and artifact;
- truth-test result classification;
- no production deployment changes.

### Read first

- current `Api.Tests` integration factories/fixtures
- `AnalyticsShoeTypeSalesIntegrationTests.cs`
- Supplier/Daily/PrePost/Color/Inventory integration tests
- CI workflows
- `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`
- RQ407/RQ408

### Do

1. Create a mandatory hermetic PostgreSQL proof profile for the Operations truth suite. Prefer a disposable real PostgreSQL instance (CI service container/Testcontainers or repository-standard equivalent), not EF InMemory, for SQL/provider-sensitive assertions.
2. A proof command must fail clearly if the required PostgreSQL fixture cannot start/connect. It must never convert “not executed” into “passed”.
3. Optional live-provider tests may remain gated, but evidence must report `NOT RUN`/blocked rather than count their early-return method as proof.
4. Separate three evidence states:
   - hermetic PostgreSQL proof passed;
   - live read-only reconciliation passed;
   - live proof unavailable/blocked.
5. Run RQ407/RQ408 truth tests in the mandatory PostgreSQL profile and publish machine-readable + Markdown artifacts with executed test count, dataset seed/hash, migration SHA, filters and deltas.
6. Add a guard test/script that detects new decision-critical integration methods using silent environment early-return patterns in the truth suite.
7. Do not make live production access a prerequisite for ordinary PR correctness; live replay remains an additional release/operator proof and may be blocked by STAB16.

### Tests

- prove the mandatory profile fails when its DB connection/fixture is intentionally unavailable;
- prove seeded migrations/bootstrap run against PostgreSQL;
- prove all registered Operations truth cases actually execute;
- prove evidence distinguishes pass, fail and not-run.

### Acceptance

- “Operations truth proof passed” can only be emitted when PostgreSQL assertions actually executed.
- No decision-critical truth test can silently early-return and still contribute a green proof count.
- CI/release evidence names exact executed cases and database/migration identity.
- Live production/provider proof is never fabricated when unavailable.

### Dependencies

- `RQ407`, `RQ408`.
- Live read-only mode may remain dependent on `STAB16`; hermetic proof must remain runnable without it.

---

## RQ410 - Continuous Operations drift, cache and freshness guardrails

Status: WAITING
Ready after: RQ407-RQ409
Priority: P1
Type: backend-observability/cache-invalidation/tests
Feature family: operations-runtime-drift-guard
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ410-<agent>.lock.md`
Commit suggestion: `feat(analytics): detect operations data drift and gate stale decisions`

### Problem

A correct implementation can become incorrect later through import changes, cache-key drift, missed invalidation, stale materialized views or schema/provider changes. Accuracy needs a durable runtime signal, not only one-time tests.

### Evidence

Operacije surfaces use a mix of direct facts, caches, materialized views, cost snapshots and workers. Prior RQs have fixed several cache/freshness issues individually, but there is no single runtime integrity status backed by raw-fact reconciliation probes.

### Scope

- lightweight reconciliation/drift probes based on RQ407/RQ408 invariants;
- cache/materialized-view invalidation checks after imports/refreshes;
- integrity telemetry/health metadata;
- recommendation gating on confirmed drift;
- admin/operator evidence;
- no high-frequency full-table scans on user requests.

### Read first

- `IAnalyticsCacheService` and analytics cache invalidation paths
- Access import completion/invalidation flow
- analytics refresh/nightly workers
- snapshot health/reconciliation endpoints
- data-quality health service
- RQ396 cache/freshness evidence
- RQ407-RQ409 evidence

### Do

1. Define backend-owned `integrityStatus`: at minimum `verified`, `unverified`, `degraded`, `drift_detected`, with last check timestamp and evidence ID.
2. Reuse independent RQ407/RQ408 probes over bounded representative windows/stores/scopes after:
   - sales/import completion;
   - analytics aggregate/materialized refresh;
   - relevant data-quality/snapshot activation;
   - cache invalidation/prewarm.
3. Emit exact deltas and metric dimensions. Currency/quantity partitions should expect zero unexplained delta; presentation-rounding tolerances must be explicit.
4. Add stale-cache tests: warm Supplier/Shoe Type/other relevant cache, mutate/import facts, execute the official invalidation path, and prove the next read reflects the new canonical data.
5. Add temporal-drift test from RQ406: changing current article supplier/type cannot alter frozen historical partitions.
6. When `drift_detected` affects a decision-critical dataset, fail closed for recommendation/actionability and show trust metadata; descriptive data may remain visible with a warning where safe.
7. Do not mark the whole system healthy merely because the monitor itself failed. Monitor failure is `unverified/degraded`, not `verified`.
8. Persist/log a compact reconciliation evidence record suitable for support and regression triage.

### Tests

- import -> invalidation -> read parity;
- materialized refresh -> read parity;
- stale cache deliberately injected -> drift detected;
- monitor failure -> unverified/degraded, never verified;
- decision signal blocked on confirmed material drift;
- bounded probe performance test to prevent user-request latency regressions.

### Acceptance

- The system can detect and explain post-deploy/import drift without waiting for a user to notice wrong numbers.
- Confirmed material drift cannot remain visually “green” on decision-critical Operacije surfaces.
- Cache/freshness/invalidation behavior is covered by repeatable tests and durable evidence.
- Accuracy status survives future refactors because the oracle/invariants are independent of the production aggregation implementation.

### Dependencies

- `RQ407`, `RQ408`, `RQ409`.
- Coordinate with existing cache/freshness owners, especially RQ396 and Supplier/Inventory cache paths.
- Keep probes bounded and read-only; production remediation remains a separate owner action.
