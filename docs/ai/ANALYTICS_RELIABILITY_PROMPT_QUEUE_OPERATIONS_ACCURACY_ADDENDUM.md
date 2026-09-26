# Analytics Reliability Prompt Queue - Operations Data Accuracy Addendum

Date: 2026-09-23
Repo: `ivanjovicic/Trendplus`
Current READY prompt: RQ456
Main RQ current READY prompt: none

Owner promotion/claim 2026-09-26: idle recovery confirmed `RQ445` is the first dependency-complete P0 certification prompt, with no competing Supplier/Shoe Type accuracy-contract owner or active lock. `RQ445` moved `WAITING -> READY -> IN_PROGRESS` in this workspace; local lock `.ai/task-locks/RQ445-codex.lock.md`.

Owner completion 2026-09-26: `RQ445` was delivered directly to `main`. The canonical `SST-ACCURACY-1.0` contract now binds Supplier/Shoe Type source population, period/store/scope, sale-time identity, signed metrics, denominators, negative/unknown behavior, PoP baselines, cost coverage, surface parity, trust states and bounded claim language. RQ456/RQ457/RQ431 retain their implementation/decision ownership. Run log: `.ai/runs/2026-09-26-RQ445-evidence.md`. Evidence state: synchronized.
Owner promotion/claim 2026-09-26: idle recovery confirmed `RQ445` DONE on current `origin/main`, no active Supplier/Shoe Type adversarial fixture owner or conflicting RQ407/RQ412 fixture lock, and RQ446's dependency is satisfied. `RQ446` moved `WAITING -> READY -> IN_PROGRESS` in this workspace; local lock `.ai/task-locks/RQ446-cursor.lock.md`.

Owner completion 2026-09-26: RQ446 added the adversarial appendix to the shared RQ407/RQ412 SQL fixture, an immutable JSON expected-output corpus with SHA-256 sidecar, and deterministic fixture/manifest guard tests. The appendix covers fractional boundaries, signed returns, mixed-case/whitespace `DUG`/`KOREKCIJA`, store/origin scope, known labels versus null identity, previous-only Shoe Type, master mutation, duplicate replay, cost coverage, non-positive margin, top-N unknown and cache evidence states. The exact-byte manifest policy is now explicit in `.gitattributes`, and the focused guard suite passes 2/2. Run log: `.ai/runs/2026-09-26-RQ446-evidence.md`. Evidence state: synchronized on `main`; runtime PostgreSQL/API execution remains RQ447.

Owner promotion/claim 2026-09-26: idle recovery confirmed `RQ445` and `RQ446` DONE on current `origin/main`, no active receipt-population owner/lock/branch/PR, and RQ456's declared dependency is satisfied. `RQ456` moved `WAITING -> READY -> IN_PROGRESS` in this workspace; local lock `.ai/task-locks/RQ456-codex.lock.md`. RQ457 remains WAITING because its Shoe Type identity/PoP work must coordinate with this population owner.
Owner completion 2026-09-26: `RQ456` was delivered to `main` in implementation `74a12194c25bf140cd7438bfe97a2b83a857c7fc`; current `origin/main` contains it through merge `54a465254d53a2e71474877d1828a1c9f97f4195`. The shared receipt policy now excludes trimmed/case-insensitive `DUG`/`KOREKCIJA` from certified Daily/Supplier/Shoe Type/Color populations, detail/data-window/cost-snapshot paths and the independent oracle while retaining signed returns. Focused proof is 24/24. Run log: `.ai/runs/2026-09-26-RQ456-evidence.md`. Evidence state: synchronized.

Certification-layer review 2026-09-25: current main confirms the existing Supplier/Shoe Type semantic owners and bounded integrity runtime, but not a defensible certification claim across contract, adversarial fixture, live execution, browser/render/export, durable evidence, import-trigger freshness, UI evidence, certificate, CI, production or customer acceptance. Added RQ445-RQ455 below as WAITING, collision-safe follow-ups. Semantic closure review later the same day added RQ456-RQ457 for the two remaining source/population and Shoe Type identity/comparison contracts; existing RQ447/RQ449 IDs retain their certification meanings and are not repurposed. These prompts do not reopen RQ407, RQ411-RQ413, RQ373-RQ380 or RQ375-RQ377; they consume their outputs and record the remaining evidence gaps.

Owner promotion 2026-09-25: idle recovery under the user's claim-and-execute request verified `RQ413` is dependency-complete after `RQ407` and `RQ412` delivery. No active drift-guard lock or overlapping cache/freshness owner was found; `RQ413` moved `WAITING -> READY`.

Owner claim 2026-09-25: `RQ413` transitioned `READY -> IN_PROGRESS` in this workspace for bounded Operations drift/cache integrity probes and fail-closed decision gating. Local runtime lock: `.ai/task-locks/RQ413-cursor.lock.md`.

Owner completion 2026-09-25: `RQ413` was delivered directly to `main` in `76d0194a` with daily-audit hardening in `9bb93359`/`00accd93`. Operations integrity exposes `verified/unverified/degraded/drift_detected` snapshots, runs bounded Supplier/Shoe Type probes (raw-fact oracle + cache lane), marks `unverified` on analytics cache clear (with optional background bounded probe when DI scope factory is wired), fail-closes Supplier/Shoe recommendations only on `drift_detected`, and registers `OperationsAnalyticsIntegrityWorker` with poll-interval next-run metadata. Run logs: `.ai/runs/2026-09-25-RQ413-evidence.md`, `.ai/runs/2026-09-25-daily-audit-hardening-evidence.md`. Evidence state: synchronized.

Owner promotion 2026-09-25: idle recovery verified `RQ407`, `RQ411` and applicable Supplier Sales semantic prompts (`RQ373`-`RQ374`, `RQ378`-`RQ380`) are DONE on current `main`. No active independent-oracle lock or overlapping backend claim was found; `RQ412` moved `WAITING -> READY` as the operations-accuracy pointer.

Owner claim 2026-09-25: `RQ412` transitioned `READY -> IN_PROGRESS` in this workspace for the implementation-independent Supplier/Shoe Type raw-fact oracle extending the RQ407 fixture. Local runtime lock: `.ai/task-locks/RQ412-cursor.lock.md`.

Owner completion 2026-09-25: `RQ412` was delivered directly to `main` in `cb755cae`. Supplier and Shoe Type list totals, bucket sums, share percentages and master-mutation invariants are reconciled against raw SQL on `supplier_id_at_sale` / `shoe_type_id_at_sale` via `Api.Tests/Analytics/SupplierShoeTypeRawFactOracle.cs` and `Api.Tests/SupplierShoeTypeIndependentOracleIntegrationTests.cs`. Manifest: `docs/qa/SUPPLIER_SHOETYPE_INDEPENDENT_ORACLE_MANIFEST_2026-09-25.md`. Run log: `.ai/runs/2026-09-25-RQ412-evidence.md`. Evidence state: synchronized. Successor `RQ413` was sequenced separately.

Routing reconciliation 2026-09-25: `RQ411` summary row corrected from stale `WAITING` to `DONE` without reopening the delivered attribution contract.

Owner promotion 2026-09-24: under the user's instruction to claim the next prompt, dependency/collision review confirmed `RQ373` is DONE, no active Supplier Sales backend owner overlaps the historical attribution boundary, and `RQ412`/`RQ413` remain correctly sequenced behind this contract. `RQ411` moved `WAITING -> READY`.

Owner claim 2026-09-24: `RQ411` transitioned `READY -> IN_PROGRESS` in this workspace for immutable/provenance-bearing supplier and shoe-type attribution; local runtime lock `.ai/task-locks/RQ411-codex.lock.md`.

Owner completion 2026-09-24: `RQ411` was delivered directly to `main` in `67d5885399119e8f16d66023e7245bcc456e6c19`. Supplier and Shoe Type now group from immutable sale-line attribution, POS/Access paths preserve or explicitly freeze provenance, and list/detail/snapshot metadata exposes attribution basis and coverage. Run log: `.ai/runs/2026-09-24-RQ411-evidence.md`. Evidence state: synchronized. Main verification: `origin/main` contains the implementation and evidence-closure commits; `RQ412` and `RQ413` remain WAITING.

Routing reconciliation 2026-09-24: `RQ414` already had a synchronized DONE completion note and implementation ancestor on `origin/main` (`7e2f330746311d694cc843beaac6fdb3f4d2611b`), but its live header remained stale at `WAITING`; the header is corrected to `DONE` without reopening or re-running the prompt.

Owner promotion 2026-09-24: idle recovery verified that RQ373 is the dependency-complete Supplier Sales visible-population owner. RQ378 and RQ380 depend on its display/reference contract, while RQ379 must consume the stabilized response schema; no conflicting active claim, branch or PR was found. RQ373 moved `WAITING -> READY`.

Owner claim 2026-09-24: RQ373 transitioned `READY -> IN_PROGRESS` in this workspace for the Supplier Sales display-population/reference-cohort contract across KPI, chart, table, detail, export and recommendation projections. Local runtime lock: `.ai/task-locks/RQ373-cursor.lock.md`.

Owner completion 2026-09-24: RQ373 delivered the bounded Supplier Sales display-population/reference-cohort contract directly to `main`. Visible revenue, units, margin shares and PoP now use the same filtered rows as the table; backend recommendation status and whole-response reference semantics remain explicit in trust, detail and export metadata. Run log: `.ai/runs/2026-09-24-RQ373-evidence.md`. RQ378/RQ379/RQ380 remain separate Supplier Sales owners.

Owner promotion 2026-09-24: after RQ371 completion and fresh scope review, RQ414 moved `WAITING -> READY` as the bounded Inventory list sales-origin parity owner. Its scope is limited to the `/inventory/list` joined velocity query; RQ371, RQ407 and RQ413 retain their broader signal/proof/drift ownership.

Owner claim 2026-09-24: RQ414 transitioned `READY -> IN_PROGRESS` in this workspace for the backend-owned sale-header origin predicate, list signal provenance and focused joined-query tests. Local runtime lock: `.ai/task-locks/RQ414-cursor.lock.md`.

Owner completion 2026-09-24: RQ414 was delivered directly to `main`. Inventory list sell-through now applies the same normalized `all`/`imported`/`existing` data-origin predicate to article rows and joined sale headers, with requested/effective period and scope provenance in the response metadata. Run log: `.ai/runs/2026-09-24-RQ414-evidence.md`. No successor promoted; RQ413 remains gated by its independent drift/proof dependencies.

Owner promotion 2026-09-24: after `RQ425` completion returned the pointer to `none`, idle-recovery review found `RQ422` runnable on the remaining Supplier Footwear type-insight display/weighting contract: backend full-cohort aggregates from `RQ406` are DONE and `RQ411` remains a separate P0 attribution owner without blocking this bounded denominator lane. `RQ422` moved `WAITING -> READY` and was claimed.

Owner completion 2026-09-24: `RQ422` delivered directly to `main` in `fb1ced982c085472ba87a97da63c97fdcd7b4314`; Supplier Footwear type chart now keeps full-cohort share percentages, adds an explicit `Ostali` bucket beyond the top eight categories, declares display denominator metadata, and exposes post-revenue-weighted category elasticity. Run log: `.ai/runs/2026-09-24-RQ422-evidence.md`. Evidence state: synchronized. Main verification: current `origin/main` contains the implementation SHA. Follow-up: no successor promoted; `RQ426` remains WAITING behind `RQ371`.

Owner promotion/completion 2026-09-24: idle-recovery promoted `RQ426` after the forecast list response declared explicit grain/aggregation/evidence-scope metadata; Inventory row risk mapping now maxes only within the same SKU+store (or selected store for all-location rows) and refuses silent cross-store inflation. Run log: `.ai/runs/2026-09-24-RQ426-evidence.md`. Follow-up: `RQ371`/`RQ308` retain broader Inventory period and secondary-signal parity.

Owner promotion 2026-09-24: after `RQ424` completion returned the pointer to `none`, idle-recovery review found `RQ425` dependency-complete (`RQ419`/`RQ389` scope precedents on current `main`) and collision-safe in the Supplier Footwear standalone scope lane; it moved `WAITING -> READY` and was claimed for bounded scope initialization/reload work. `RQ422` remains WAITING behind Supplier Footwear denominator ownership.

Owner completion 2026-09-24: `RQ425` delivered directly to `main` in `a360fec775d053d04814bdc7be82ed1cc5a3de07`; standalone Supplier Footwear now resolves effective data scope from embedded shared filters, explicit URL scope or global storage before the first request, reloads on `trendplus:data-scope-changed`, and exposes requested/effective scope in export metadata. Run log: `.ai/runs/2026-09-24-RQ425-evidence.md`. Evidence state: synchronized. Main verification: current `origin/main` contains the implementation SHA. Follow-up: no successor promoted; `RQ422` remains WAITING.

Owner promotion 2026-09-24: after `RQ423` completion returned the pointer to `none`, idle-recovery review found `RQ424` dependency-complete (`RQ390`/`RQ391`/`RQ388` DONE) and collision-safe now that the overlapping Pre-Nivelacija focus prompt is closed; it moved `WAITING -> READY` and was claimed for the bounded leaderboard denominator/percentage-unit contract. `RQ425` remains WAITING in the adjacent Supplier Footwear scope family.

Owner completion 2026-09-24: `RQ424` delivered directly to `main` in `a8e7018eb3113d513c4c2e484a543edcb88edb0c`; Pre-Nivelacija supplier action-share now exposes backend denominator metadata (top 7 + Ostali over full leaderboard action score) and percentage fields use explicit percentage-point units without magnitude inference. Run log: `.ai/runs/2026-09-24-RQ424-evidence.md`. Evidence state: synchronized. Main verification: current `origin/main` contains the implementation SHA. Follow-up: no successor promoted; `RQ425`/`RQ422` remain WAITING behind declared gates.

Owner completion 2026-09-24: `RQ423` delivered directly to `main` in `ccd70d30aede4647d5d747438f7b5e453a4dec73`; Pre-Nivelacija focus is now a server-side population filter before pagination, global summary/tab counts stay explicit, and export/detail follow the filtered API contract. Run log: `.ai/runs/2026-09-24-RQ423-evidence.md`. Evidence state: synchronized. Main verification: current `origin/main` contains the implementation SHA. Follow-up: `RQ424` remains WAITING in the same Pre-Nivelacija family; no successor promoted.

Owner idle-recovery promotion 2026-09-24: after `RQ372` and `RQ421` completion returned the RQ pointer to `none`, the non-DONE backlog and recent evidence were re-evaluated instead of stopping. `RQ423` is dependency-complete: `RQ388`, `RQ299`, `RQ326`-`RQ330` and `RQ391` are DONE; `RQ407` is BLOCKED on integration-host proof but has no active conflicting claim and does not own the Pre-Nivelacija focus/pagination implementation path. No open `RQ423` PR was found. `RQ423` therefore moved `WAITING -> READY` as the current RQ/addendum pointer. `RQ424` remains WAITING because it overlaps the same Pre-Nivelacija page/contract family and should not be promoted concurrently.

Owner promotion 2026-09-24: under the user's instruction to claim the next prompt, dependency/collision review found `RQ419` independently runnable within the Supplier Sales scope-event lane after `RQ421`; the bounded reload/trust-metadata repair does not take `RQ373`/`RQ378`/`RQ379` denominator ownership. It moved `WAITING -> READY` as the current addendum pointer.

Owner completion 2026-09-24: `RQ419` delivered directly to `main` in `deed107ef1b2f2b8642f19fa794abf8b68dc23ce`; Supplier Sales reloads on global data-scope changes, clears stale scope-bound rows/trust metadata, and canonical Supplier parent URL filters sync through `useSupplierCanonicalState`. Run log: `.ai/runs/2026-09-24-RQ419-evidence.md`. Evidence state: synchronized. Main verification: current `origin/main` contains the implementation SHA. Follow-up: `RQ422` remains WAITING behind Supplier Footwear denominator ownership; no successor promoted.

Owner promotion 2026-09-24: under the user's instruction to claim the next prompt, dependency/collision review found `RQ421` independently runnable within the Supplier Sales status-identity lane after `RQ420` completion; the bounded frontend mapping/display repair does not take `RQ373`/`RQ378`/`RQ379` denominator ownership. It moved `WAITING -> READY` as the current addendum pointer.

Owner completion 2026-09-24: `RQ421` delivered directly to `main` in `980a671c5451278b7d806fa3c2e2dfef6bb2af4a`; Supplier Sales now preserves backend recommendation status across row/count/detail/export projections and renders actionability as a separate gate aligned with the Color pattern. Run log: `.ai/runs/2026-09-24-RQ421-evidence.md`. Evidence state: synchronized. Main verification: current `origin/main` contains the implementation SHA. Follow-up: `RQ419` remains WAITING behind Supplier scope-event ownership; no successor promoted.

Use this queue with `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

Purpose: add only the **remaining, non-duplicative** Operations correctness work discovered by the 2026-09-23 data-flow audit, with first priority on **Prodaja po dobavljačima** and **Prodaja po tipu obuće**.

Owner promotion/claim/completion 2026-09-23: under the user's instruction to claim the next prompt, dependency/collision review found `RQ414` blocked behind the broader `RQ371` scope contract, while `RQ415` was independently runnable and owned only deterministic Inventory list ordering. `RQ415` moved `WAITING -> READY -> IN_PROGRESS -> DONE`; no successor was promoted because the remaining addendum prompts are dependency/collision gated.

Owner promotion/claim 2026-09-23: after `RQ416` completion, `RQ417` was promoted from `WAITING` to `READY` because its size-alert navigation identity scope is independent of the `RQ324` error/empty-state owner; `RQ372` and `RQ407` remain coordination owners. It is now claimed by Codex for the bounded SKU/size/store identity path.

Owner promotion 2026-09-23: `RQ418` was promoted from `WAITING` to `READY` after `RQ275` was verified `DONE`; its source-key context repair is bounded to Inventory action lookup/create/projection identity and can use explicit period/snapshot values without taking ownership of the `RQ308`/`RQ371` period-contract work. No overlapping RQ418 lock, branch or active owner was found.

Owner promotion/claim 2026-09-23: under the user's instruction to claim the next prompt, `RQ416` was promoted from `WAITING` to `READY` after `RQ369` and `RQ353` were verified `DONE`; it is now claimed by Codex for the bounded Inventory insight identity/cost-provenance scope. `RQ371` and `RQ407` remain coordination dependencies and no overlapping active claim was found.

Canonical queue work already present on current `main` must be preserved:
- `RQ406` — Supplier Footwear derived type metrics must not become authoritative when `articleStats` is truncated;
- `RQ407` — deterministic cross-screen proof pack/reconciliation for all eight Operacije routes;
- `RQ373`, `RQ378`-`RQ380` — Supplier Sales visible population, weighted margin/runtime validation and pre/post ownership;
- `RQ375`-`RQ377` — Shoe Type weighted margin, comparable cohort and detail trust/identity;
- `RQ381`, `RQ385`-`RQ400` — Daily Sales, Pre/Post, Pre-Nivelacija and Color correctness contracts.

Prompts not explicitly marked `READY` or `DONE` remain `WAITING`. Promote only through the dependency/collision checks in the canonical protocol.

## New audit facts

- Supplier Sales and Shoe Type Sales both classify historical sale lines through the **current** article master: `ProdajaStavke -> Artikli.IDDobavljac / Artikli.IDTipObuce`.
- Current `ProdajaStavke` does not carry immutable supplier/type-at-sale dimensions in the inspected schema. A later article-master edit can therefore change historical grouping unless attribution is frozen/reconstructed with explicit provenance.
- Canonical `RQ407` will create one deterministic expected-output proof pack, but Supplier/Shoe Type also benefit from an **implementation-independent raw-fact oracle** that cannot accidentally repeat the same production aggregation bug.
- One-time proofs do not prevent later drift from import changes, cache invalidation misses, materialized-view drift or mutable master data.

## Status summary

| Task | Status | Priority | Feature family | Purpose |
|---|---|---:|---|---|
| RQ411 | DONE | P0 | operations-sale-dimension-attribution | Freeze or provenance-qualify supplier/type attribution for historical sale lines |
| RQ412 | DONE | P0 | supplier-shoetype-independent-oracle | Independently reconcile Supplier/Shoe Type to raw facts after canonical RQ407 proof |
| RQ413 | DONE | P1 | operations-runtime-drift-guard | Continuously detect post-import/cache/source drift and fail closed for decision signals |
| RQ414 | DONE | P1 | inventory-sales-origin-parity | Keep Inventory list sell-through on the same data-origin population as article rows |
| RQ415 | DONE | P2 | inventory-deterministic-pagination | Make Inventory list ordering stable under ties and concurrent changes |
| RQ416 | DONE | P1 | inventory-insight-identity-provenance | Preserve store/supplier identity and cost provenance from Inventory insights to detail |
| RQ417 | DONE | P1 | inventory-size-alert-identity | Preserve SKU, size and store context when an Inventory alert opens size curve |
| RQ418 | DONE | P1 | inventory-action-dataset-idempotency | Prevent Inventory action deduplication from crossing period/scope/snapshot datasets |
| RQ419 | DONE | P1 | supplier-sales-scope-event-lineage | Reload Supplier Sales when global data scope changes and keep trust metadata aligned |
| RQ420 | DONE | P2 | supplier-sales-derived-projection-freshness | Prevent stale Supplier Sales derived shares and cost projections after total changes |
| RQ421 | DONE | P1 | supplier-sales-status-identity | Preserve backend Supplier Sales status when recommendation actionability is blocked |
| RQ422 | DONE | P1 | supplier-footwear-type-insight-denominator | Make Supplier Footwear type share and elasticity metrics full-cohort and provenance-safe |
| RQ423 | DONE | P1 | pre-nivelacija-focus-population-parity | Make Pre-Nivelacija focus filtering population-aware across pages and projections |
| RQ424 | DONE | P1 | pre-nivelacija-leaderboard-denominator | Define Pre-Nivelacija action-share and percentage normalization semantics |
| RQ425 | DONE | P1 | supplier-footwear-scope-default | Prevent standalone Supplier Footwear from silently falling back to all data |
| RQ426 | DONE | P1 | inventory-forecast-risk-aggregation | Prove and correct Inventory forecast risk aggregation across sizes and stores |
| RQ445 | DONE | P0 | supplier-shoetype-accuracy-contract | Canonical accuracy contract for Supplier/Shoe Type metrics, populations, provenance and claim language |
| RQ446 | DONE | P0 | supplier-shoetype-adversarial-golden | Extend the shared fixture with adversarial Supplier/Shoe Type cases and immutable expected outputs |
| RQ447 | WAITING | P0 | supplier-shoetype-live-oracle-reproducers | Execute the four live RQ412 oracle cases and close OP2-01/04/05/14 with runtime reproducers |
| RQ448 | WAITING | P0 | supplier-shoetype-browser-reconciliation | Reconcile raw facts through API, browser-rendered KPI/table/detail and export |
| RQ449 | WAITING | P1 | operations-integrity-evidence-history | Persist immutable integrity evidence/history beyond the process snapshot |
| RQ450 | WAITING | P1 | operations-post-import-probe | Run a bounded Supplier/Shoe integrity probe immediately after Access import |
| RQ451 | WAITING | P1 | supplier-shoetype-verified-evidence-ui | Expose Verified status and inspectable evidence on both customer-facing screens |
| RQ452 | WAITING | P1 | supplier-shoetype-accuracy-certificate | Generate a truthful evidence-backed Supplier/Shoe Type accuracy certificate |
| RQ453 | WAITING | P0 | analytics-certification-ci-gate | Add a non-skippable certification CI gate with executed-versus-skipped accounting |
| RQ454 | WAITING | P0 | supplier-shoetype-production-reconciliation | Produce read-only production reconciliation evidence for certified windows |
| RQ455 | WAITING | P1 | supplier-shoetype-customer-acceptance | Capture customer-side reconciliation and acceptance evidence |
| RQ456 | IN_PROGRESS | P0 | operations-retail-sales-receipt-population | Canonicalize DUG/KOREKCIJA receipt exclusions across retail-sales populations |
| RQ457 | WAITING | P0 | shoetype-identity-pop-margin-semantics | Harden Shoe Type identity, PoP union and margin semantics after receipt population work |

---

## RQ411 - Immutable/provenance-bearing supplier and shoe-type attribution

Status: DONE
Ready after: Supplier Sales backend owner lane is collision-safe; coordinate RQ373/RQ378-RQ380
Priority: P0
Type: backend-data-contract/migration/import/tests
Feature family: operations-sale-dimension-attribution
Parallel-safe: no
Owner: Codex
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

Status: DONE
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

Status: DONE
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

---

## RQ414 - Inventory list sell-through must honor the selected data-origin scope

Status: DONE
Ready after: `RQ371` scope/period boundary is recorded; this prompt owns only `/inventory/list` joined sales velocity
Priority: P1
Type: backend/contract/tests
Feature family: inventory-sales-origin-parity
Parallel-safe: no
Owner: Cursor cloud agent
Local lock: `.ai/task-locks/RQ414-<agent>.lock.md`
Commit suggestion: `fix(analytics): align inventory list sales scope`

### Problem

The Inventory list filters article master rows by `Artikli.DataOrigin`, but its `soldUnitsByArticle` query does not apply the same `dataScope` predicate to `ProdajaZaglavlja.DataOrigin`. Imported/existing list rows can therefore receive sell-through and stock-cover signals calculated from a mixed sales population.

### Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:731-742` applies `normalizedDataScope` to `Artikli`.
- `Api/Endpoints/CachedAnalyticsEndpoints.cs:772-786` joins sale headers and lines for velocity but filters only article IDs, period and store; no sale-header origin predicate is present.
- `:788-795` passes `normalizedDataScope` to journal movement statistics, so two inputs of the same signal already use different scope behavior.
- `RQ408` finding `OP2-21` records the same source-population mismatch; `RQ371` owns the broader Inventory signal period/scope contract.

### Scope

- `/api/analytics/inventory/list` velocity query, DTO metadata and focused backend tests.
- The exact mapping of `all`, `imported` (`access`) and `existing` (`existing`, null or empty) for sale headers.
- Reconciliation of `soldUnits30d`, average daily sales, sell-through, stock-cover and reason codes for this list endpoint only.

Do not redesign journal movement semantics, forecast/alerts/rebalance contracts or the cross-screen proof pack.

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/InventoryEndpoints.cs`
- `RQ371`, `RQ407` and `RQ408/OP2-03, OP2-21`
- `InventorySignalCalculator` and the nearest Inventory endpoint tests

### Do

1. Define one backend-owned origin predicate for the list velocity population and use it for `all`, `imported` and `existing`.
2. Reconcile article-origin and sale-header-origin mismatch behavior. If the contract cannot prove a joined row belongs to the requested scope, keep the signal warning/unknown rather than silently mixing it.
3. Keep the half-open UTC sales window and existing signed quantity semantics explicit.
4. Include scope/provenance in the response metadata or evidence used by the list signal so a stale/mixed result cannot look fully trusted.
5. Preserve cache-key scope isolation and do not duplicate the broader `RQ371` secondary-panel work.

### Tests

- One article with imported and existing sale headers: `imported`, `existing` and `all` return distinct expected units.
- Article origin and sale-header origin disagree: response is either excluded by the declared contract or marked degraded/unknown; it is never silently counted in the wrong scope.
- Empty, zero-velocity, negative/return quantity and exact start/end boundary cases.
- Journal movement and sale velocity use the same scope in the returned signal evidence.
- Cache reads for the three scopes do not reuse one another's result.

### Acceptance

- Inventory list sell-through and stock-cover never combine article rows from one data origin with sales from another without explicit provenance.
- `all`/`existing`/`imported` behavior is documented and tested at the joined-query boundary.
- Valid zero remains zero; unavailable/mixed-source evidence remains unavailable or degraded, never a trusted zero.
- `RQ371` and `RQ407` remain the owners of their broader contracts.

### Dependencies

- Coordinate `RQ371`, `RQ407`, `RQ408/OP2-03` and `RQ413`.
- If historical origin attribution is unavailable, record the limitation and use the established warning/fallback contract; do not infer it from the current article row.

### Completion note

- Date: 2026-09-24
- Status: DONE
- Delivery mode: direct-main
- Implementation commits: `f67fc92f`, `71cc714f`
- Main commit SHA: 7e2f330746311d694cc843beaac6fdb3f4d2611b
- Main verification: passed — current `origin/main` contains 7e2f3307
- Run log: `.ai/runs/2026-09-24-RQ414-evidence.md`
- Evidence state: synchronized
- Missed: historical master-data attribution remains outside this list query; mismatched sale headers are excluded by the declared scope predicate
- Follow-up: RQ413 retains broader runtime drift/freshness evidence; no successor promoted
- Residual risk: backend integration proof depends on the unavailable local .NET SDK and must run in CI

---

## RQ415 - Inventory list pagination must have deterministic ordering

Status: DONE
Ready after: no runtime dependency; coordinate with Inventory list owner and `RQ371`
Priority: P2
Type: backend/tests
Feature family: inventory-deterministic-pagination
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ415-<agent>.lock.md`
Commit suggestion: `fix(analytics): stabilize inventory list pagination`

### Problem

Inventory list paging uses non-unique sort expressions for quantity/default/name and only partially unique expressions for value/update. Equal sort values leave database row order unspecified, so the same SKU can move between pages, be duplicated or disappear after a refetch.

### Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:744-752` orders quantity/default by quantity only, name by name only, and value/update by a non-unique secondary name.
- `:753-768` applies `Skip`/`Take` after that ordering.
- `RQ408/OP2-22` identifies the same pagination risk; this prompt is limited to the list ordering proof, not Inventory KPI population semantics.

### Scope

- Inventory list server ordering and its page/size/sort tests.
- All supported `sortBy` values and null-value ordering.
- Only the immutable article identity tie-breaker and the documented null ordering.

Do not change the meaning of totals, risk sorting, export order or secondary panels.

### Read first

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- Inventory list DTO/service tests
- `RQ357`, `RQ371`, `RQ408/OP2-22`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`

### Do

1. Define a total order for every server sort, ending in a unique immutable article key.
2. Declare null quantity/value/update behavior and preserve the current user-facing direction.
3. Prove that page boundaries are stable across repeated reads with ties and during unrelated row updates.
4. Keep cache keys and total counts unchanged except where the deterministic order must be represented.

### Tests

- Equal quantities on pages 1/2; equal names; equal estimated values; equal update timestamps.
- Null versus zero values and duplicate display names.
- Repeated identical request returns identical ordered IDs.
- Updating a non-key sort field does not duplicate or lose unaffected rows across adjacent pages.

### Acceptance

- Every Inventory list sort has a deterministic total order.
- Adjacent pages have no duplicate/missing IDs under ties.
- Sorting remains display-contract compatible and does not invent business ranking.

### Completion note

- Date: 2026-09-23
- Status: DONE
- Completion: Added immutable article-ID tie-breakers to every Inventory list server sort in cached and uncached endpoints; added equal-value pagination regressions for all supported sorts.
- Changed files: `Api/Endpoints/CachedAnalyticsEndpoints.cs`, `Api/Endpoints/InventoryEndpoints.cs`, `Api.Tests/InventoryListEndpointIntegrationTests.cs`, this queue, `MASTER_ROADMAP.md`, `.ai/runs/2026-09-23-RQ415-evidence.md`.
- Contract/runtime behavior changed: Inventory list page boundaries are now deterministic under equal quantity, name, value or update timestamp while preserving current primary sort direction and total counts.
- Checks run: focused `InventoryListEndpointIntegrationTests` 19/19; `git diff --check`; queue/planning validators after delivery.
- Checks not run: full repository suite, live database/deployment/browser proof and remote CI.
- Run log: `.ai/runs/2026-09-23-RQ415-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 2e43a6456a68bb51011cbc4e7233cb401d0d9180
- Main verification: `origin/main` contains 2e43a6456a68bb51011cbc4e7233cb401d0d9180
- Missed: no business ranking, KPI population or export semantics were changed; only server list ordering was hardened.
- Follow-up: `RQ414` remains blocked behind `RQ371`; no successor was promoted automatically.
- Residual risk: live-provider pagination under concurrent production writes remains outside this local deterministic proof.
- Prompt defect / scope repair: expanded the prompt's Inventory list scope to include the existing uncached route because it shared the same non-unique ordering contract and menu surface.

### Dependencies

- Coordinate `RQ371`, `RQ357` and `RQ407` only for proof fixtures.
- If runtime data cannot be loaded, retain a deterministic database/query test rather than calling static inspection a pass.

---

## RQ416 - Inventory insight-to-detail mapping must preserve identity and cost provenance

Status: DONE
Ready after: `RQ369` is DONE; coordinate cost-state behavior with `RQ353`
Priority: P1
Type: backend/frontend/contract/tests
Feature family: inventory-insight-identity-provenance
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ416-<agent>.lock.md`
Commit suggestion: `fix(analytics): preserve inventory insight identity provenance`

### Problem

The Inventory insight-to-row adapter resolves store and supplier IDs by matching display names and derives unit cost as `estimatedValue / quantity`. Duplicate store/supplier names can open the wrong context, while the derived unit cost can look like a measured cost even when the aggregate estimate is incomplete or unavailable.

### Evidence

- `Klijent/clientapp/src/components/inventory/inventoryUtils.ts:347-368` maps IDs by display-name equality and derives `nabavnaCena` from aggregate value/quantity.
- `Klijent/clientapp/src/components/inventory/InventoryInsightPanels.tsx:37-40` resolves rows by article ID without a store discriminator.
- `RQ408/OP2-23` records wrong-store/name-collision/invented-cost scenarios; `RQ353` remains the no-fake-zero cost owner.

### Scope

- Inventory insight DTO/service projection, row adapter and insight/detail navigation.
- Canonical article ID plus store ID and supplier ID provenance.
- Explicit unit-cost source/missing state; no change to Inventory cost policy itself.

Do not redesign Inventory signal formulas, store comparison or cost snapshots.

### Read first

- `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
- `Klijent/clientapp/src/components/inventory/InventoryInsightPanels.tsx`
- Inventory insight endpoint/DTO and `SKUDetailModal`
- `RQ353`, `RQ369`, `RQ408/OP2-23`

### Do

1. Carry canonical `storeId`, `supplierId` and any required identity key in the insight payload.
2. Resolve detail rows by IDs, never by display-name matching; preserve unknown/null identity explicitly.
3. Carry backend-owned `unitCost`, cost source and missing-cost state, or render unit cost unavailable. Do not infer it by dividing an aggregate estimated value.
4. Make article/store row keys collision-safe in panels, snapshots and navigation.

### Tests

- Same SKU in two stores with same/different display names opens the selected store.
- Duplicate supplier/store names do not change IDs or detail target.
- Positive quantity with null/partial estimated value keeps unit cost unavailable.
- Valid zero cost/value remains distinguishable from missing cost.
- Insight row and detail preserve the same signal status, scope and provenance.

### Acceptance

- Insight clicks cannot silently open another store or supplier context.
- No aggregate-derived unit cost is presented as measured cost.
- Unknown identity/cost remains explicit and compatible with `RQ353`.

### Dependencies

- Coordinate `RQ353`, `RQ369`, `RQ371` and `RQ407`.
- If the existing endpoint cannot provide identity/cost provenance, extend it backward-compatibly and record the missing-data state rather than guessing.

Completion 2026-09-23:

- Date: 2026-09-23
- Status: DONE
- Completion: Inventory insight payloads now preserve article/store/supplier identity and backend-owned cost provenance; insight navigation requires the composite identity and no longer derives unit cost from aggregate value/quantity.
- Changed files: `Api/Dtos/InventoryExperienceDtos.cs`, `Api/Endpoints/InventoryEndpoints.cs`, `Api.Tests/InventoryListEndpointIntegrationTests.cs`, `Klijent/clientapp/src/types/analytics.ts`, `Klijent/clientapp/src/validation/analyticsResponseSchemas.ts`, `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`, `Klijent/clientapp/src/components/inventory/InventoryInsightPanels.tsx`, `Klijent/clientapp/src/components/inventory/InventoryInsightPanels.spec.tsx`, `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`, `Klijent/clientapp/scripts/known-guardrail-baseline.json`, this queue, `MASTER_ROADMAP.md`, `.ai/runs/2026-09-23-RQ416-evidence.md`.
- Contract/runtime behavior changed: missing positive-quantity cost is explicit and nullable, valid zero cost remains distinguishable, and duplicate store/supplier names cannot redirect insight detail navigation.
- Checks run: focused backend 24/24; focused frontend 24/24; frontend build; analytics guardrails; `git diff --check`; agent-instruction, prompt-queue and planning-architecture validators.
- Checks not run: full repository suite, live provider/database deployment, browser proof and remote CI.
- Run log: `.ai/runs/2026-09-23-RQ416-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `f1eae0a3`
- Main verification: `origin/main` contains `f1eae0a3`

---

## RQ417 - Inventory size alerts must retain size and store context

Status: DONE
Ready after: `RQ324` remains the size-curve error owner; this prompt owns only navigation identity
Priority: P1
Type: frontend/contract/tests
Feature family: inventory-size-alert-identity
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ417-<agent>.lock.md`
Commit suggestion: `fix(analytics): preserve inventory alert size context`

### Problem

An Inventory alert can contain `sizeCode` and `storeId`, but the size-curve callback receives only `skuId`. A size-specific alert can therefore open an aggregate or wrong-store size curve.

### Evidence

- `Klijent/clientapp/src/components/inventory/InventoryAlertsFeed.tsx:86-91` displays `alert.sizeCode` and `alert.storeId` for other actions but calls `onOpenSizeCurve(alert.skuId)` without either context.
- `RQ408/OP2-24` identifies the dropped size/store identity; `RQ324` covers error presentation, not target identity.

### Scope

- Inventory alert DTO-to-component mapping, callback contract and size-curve request/navigation.
- SKU, `sizeCode` and store identity through alert, URL/request, panel and export/detail context where applicable.

Do not change alert severity filtering or size-curve error copy.

### Read first

- `InventoryAlertsFeed.tsx`
- `InventoryPage.tsx` size-curve state and open handlers
- `analyticsApi.ts` size-curve request
- `RQ324`, `RQ372`, `RQ408/OP2-24`

### Do

1. Extend the callback/request with nullable `sizeCode` and `storeId` where the backend supports them.
2. Define aggregate behavior when no size is present; do not substitute a different size silently.
3. Keep URL/detail identity collision-safe for the same SKU across stores.
4. Preserve explicit empty/error/degraded size-curve states.

### Tests

- Same SKU, two stores, same size: each alert opens its own store curve.
- Same SKU/store, two sizes: each alert opens its own size curve.
- Aggregate alert without size opens only the documented aggregate view.
- Missing/invalid context fails closed with a visible unavailable state.

### Acceptance

- An alert click preserves the full available SKU/size/store identity.
- No size-specific alert opens the wrong aggregate or store curve.
- `RQ324` error/empty semantics remain unchanged.

### Dependencies

- Coordinate `RQ324`, `RQ372`, `RQ407` and size-curve backend owner.
- If size/store filtering is not supported server-side, expose that limitation rather than displaying an unqualified curve.

Completion 2026-09-23:

- Date: 2026-09-23
- Status: DONE
- Completion: Inventory alert size-curve navigation now preserves SKU, store and nullable size identity through the callback, page state, API request, backend filter and cache key; aggregate alerts remain explicit when no size is present.
- Changed files: `Api/Endpoints/CachedAnalyticsEndpoints.cs`, `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveQuery.cs`, `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveHandler.cs`, `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`, `Api.Tests/InventorySnapshotContractTests.cs`, `Api.Tests/AnalyticsScreenCacheKeyContractTests.cs`, `Klijent/clientapp/src/components/inventory/InventoryAlertsFeed.tsx`, `Klijent/clientapp/src/components/inventory/InventoryAlertsFeed.spec.tsx`, `Klijent/clientapp/src/components/inventory/SizeCurvePanel.spec.tsx`, `Klijent/clientapp/src/pages/InventoryPage.tsx`, `Klijent/clientapp/src/services/analyticsApi.ts`, `Klijent/clientapp/src/services/__tests__/analyticsApi.contract.spec.ts`, `Klijent/clientapp/scripts/known-guardrail-baseline.json`, this queue, `MASTER_ROADMAP.md`, `.ai/runs/2026-09-23-RQ417-evidence.md`.
- Contract/runtime behavior changed: a size-specific alert cannot open another store, another size or an aggregate curve; the cache distinguishes size-specific and aggregate responses, and missing size remains nullable rather than guessed.
- Checks run: focused frontend 15/15; focused backend 50/50; API build; frontend build; analytics guardrails; `git diff --check`; agent-instruction, prompt-queue and planning-architecture validators.
- Checks not run: full repository suite, live provider/database deployment, browser proof and remote CI.
- Run log: `.ai/runs/2026-09-23-RQ417-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `1e3b8898`
- Main verification: `origin/main` contains `1e3b8898`

---

## RQ418 - Inventory action idempotency must include dataset context

Status: DONE
Ready after: `RQ275` is DONE; coordinate period semantics with `RQ308`/`RQ371`
Priority: P1
Type: frontend/backend/workflow/tests
Feature family: inventory-action-dataset-idempotency
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/RQ418-<agent>.lock.md`
Commit suggestion: `fix(analytics): scope inventory action idempotency keys`

### Problem

Inventory action queue lookup and creation use a source key composed of signal type, article ID and store, but not the selected data scope, signal period or snapshot generation. The same article can therefore appear already queued, or reuse an action, when the user changes dataset context.

### Evidence

- `Klijent/clientapp/src/pages/InventoryPage.tsx:268-321` builds `sourceKey` from action kind, `row.id` and `row.idObjekat` only.
- `:790-820` uses those keys for queue-state lookup; `:1132-1159` sends the same key when creating the action.
- `RQ408/OP2-25` records the missing dataset context; `RQ275` owns stale queue reset but cannot distinguish two contexts sharing one key.

### Scope

- Inventory signal action source-key contract, queue lookup/create payloads and matching action-state projections.
- Data scope, requested signal window and backend snapshot/fact generation where available.

Do not change action lifecycle states, user permissions or general action prioritization.

### Read first

- `InventoryPage.tsx` action helpers and queue effects
- `analyticsApi.ts` action lookup/create contracts
- backend action source-key persistence and `RQ275` evidence
- `RQ308`, `RQ371`, `RQ408/OP2-25`

### Do

1. Define a canonical versioned source-key schema containing article/store, action kind and the dataset context needed to make idempotency meaningful.
2. Keep equal-context repeated clicks idempotent while different scope/period/snapshot contexts remain distinguishable.
3. Make old keys backward-compatible or explicitly migrate/expire them; never silently merge old and new contexts.
4. Show the action's source context in detail/audit metadata where users could otherwise confuse datasets.

### Tests

- Same SKU/store/action and same scope/period/snapshot deduplicates.
- Same SKU/store/action with `all` vs `existing`, different periods or snapshot generations does not deduplicate.
- Legacy key read/write behavior is explicit.
- Queue-state lookup, create, refresh and Decision Board projections use the same canonical key.

### Acceptance

- An action is idempotent only within its declared dataset context.
- Changing Inventory scope/period cannot inherit a stale queued state from another dataset.
- `RQ275` empty/reset behavior still passes.

### Completion note

- Date: 2026-09-23
- Status: DONE
- Completion: Delivered a canonical versioned Inventory action key with data scope, signal window, snapshot/unknown provenance, size and transfer-store context. Equal-context clicks remain idempotent; different contexts and legacy keys remain distinct. Queue metadata/workflow DTOs now carry the source context, and Decision Board uses the same canonical key without adding a second prefix.
- Changed files: `Application/Inventory/Models/InventoryActionSourceKey.cs`, `Api/Dtos/InventoryExperienceDtos.cs`, `Api/Endpoints/InventoryEndpoints.cs`, `Api/Endpoints/DecisionBoardEndpoints.cs`, `Api.Tests/InventoryActionSourceKeyTests.cs`, `Api.Tests/DecisionBoardEndpointsTests.cs`, `Klijent/clientapp/src/types/analytics.ts`, `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`, `Klijent/clientapp/src/components/inventory/__tests__/inventoryActionSourceKey.spec.ts`, `Klijent/clientapp/src/pages/InventoryPage.tsx`, `Klijent/clientapp/scripts/known-guardrail-baseline.json`, this queue, `MASTER_ROADMAP.md`, `.ai/runs/2026-09-23-RQ418-evidence.md`.
- Contract/runtime behavior changed: v2 keys cannot inherit queued state across scope, period or snapshot contexts; unavailable snapshot generation is explicit `unknown`; legacy opaque keys remain readable/writable but are never used as v2 fallbacks.
- Checks run: focused backend 43/43; focused frontend 30/30; frontend typecheck; frontend build; analytics guardrails (50 reviewed findings, 0 new); prompt-queue validator; planning-architecture validator; `git diff --check`.
- Checks not run: full repository suite, live provider/database deployment, browser proof and remote CI.
- Run log: `.ai/runs/2026-09-23-RQ418-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `097ad3e9f4a42da519b477f12c2025dd09c08009`
- Main verification: `origin/main` equals and contains `097ad3e9f4a42da519b477f12c2025dd09c08009`
- Missed: no automated database migration of historical legacy keys; they remain explicitly separated from v2.
- Follow-up: `RQ308`/`RQ371` retain period/scope semantics; `RQ407` retains cross-screen reconciliation ownership.
- Residual risk: current dirty worktree API build is blocked by an unrelated uncommitted `AnalyticsMarginPolicy.cs` compile error; focused RQ418 backend proof and the delivered main SHA are green.
- Prompt defect / scope repair: re-anchored the reviewed line-based guardrail baseline after legitimate line movement and removed one baseline entry whose finding no longer exists; no analytics violation was waived.

### Dependencies

- Coordinate `RQ275`, `RQ308`, `RQ371`, `RQ407` and the shared action ledger owner.
- If backend snapshot generation is unavailable, use an explicit unknown context and fail closed rather than inventing equivalence.

---

## RQ419 - Supplier Sales must reload and relabel on global data-scope changes

Status: DONE
Ready after: coordinate with canonical Supplier parent filter owner; do not run with another Supplier Sales scope owner
Priority: P1
Type: frontend/tests
Feature family: supplier-sales-scope-event-lineage
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ419-<agent>.lock.md`
Commit suggestion: `fix(analytics): propagate supplier sales data scope`

### Problem

Supplier Sales reads `dataScope` from shared filters, URL or local storage, but the page itself has no visible `trendplus:data-scope-changed` listener. When used outside the canonical parent, a global scope change can leave old values and trust metadata on screen.

### Evidence

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:652-655` derives `activeDataScope` from shared filters, URL or `getDataScope()`.
- `:718-747` sends the captured scope and reloads only when the callback dependency changes.
- The file contains period/filter synchronization but no corresponding scope-change event listener; `RQ408/OP2-02` records the risk.
- `RQ269`, `RQ270` and `RQ294` establish the existing event-lineage pattern for other Operations surfaces.

### Scope

- Supplier Sales standalone and canonical/embedded composition scope propagation, request cancellation and trust header/metadata refresh.
- Precedence between shared parent state, URL state and persisted global scope.

Do not change Supplier Sales denominators, margin policy or runtime schema (`RQ373`, `RQ378`, `RQ379`).

### Read first

- `SupplierSalesStatsPage.tsx`
- canonical Supplier parent filters/composition
- `RQ269`, `RQ270`, `RQ278`, `RQ373`, `RQ379`, `RQ408/OP2-02`

### Do

1. Define the authoritative scope source for embedded and standalone modes.
2. Subscribe to the existing global scope event where the page owns standalone state, or prove the parent always supplies the new scope before render.
3. Abort/ignore old requests and clear or mark stale data while the new scope is loading.
4. Keep URL, API request, response metadata, trust header, table, chart, detail and export on one scope.

### Tests

- Open page with `existing`, switch to `imported` and assert a new request and no old-scope trust state.
- Switch during an in-flight request; late old response cannot overwrite the new scope.
- Standalone and embedded modes follow their declared precedence.
- Scope event failure/invalid value remains explicit and does not fall back to trusted `all` silently.

### Acceptance

- A global scope change cannot leave Supplier Sales showing values or trust metadata from the previous scope.
- Standalone/canonical behavior is documented and tested.
- Existing Supplier metric and status semantics remain owned by their current prompts.

### Dependencies

- Coordinate `RQ278`, `RQ373`, `RQ379`, `RQ407` and the canonical Supplier filter owner.

### Completion note

- Date: 2026-09-24
- Status: DONE
- Completion: Standalone Supplier Sales listens for `trendplus:data-scope-changed`, reloads with persisted scope precedence, clears stale scope-bound rows/trust metadata, and canonical Supplier parent URL filters sync via `useSupplierCanonicalState`.
- Changed files: `SupplierSalesStatsPage.tsx`, `useSupplierCanonicalState.ts`, `SupplierSalesStatsPage.scopeReload.spec.tsx`, guardrail baseline repair, queue routing metadata.
- Contract/runtime behavior changed: global scope changes cannot leave Supplier Sales on prior-scope values or trust metadata; embedded mode remains parent-authoritative.
- Checks run: `npm run test -- --run src/pages/__tests__/SupplierSalesStatsPage.scopeReload.spec.tsx`; related Supplier Sales specs; `npm run check:analytics-guardrails`.
- Checks not run: full Supplier consolidated page suite; backend integration tests.
- Run log: `.ai/runs/2026-09-24-RQ419-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: deed107ef1b2f2b8642f19fa794abf8b68dc23ce
- Main verification: passed — current `origin/main` contains deed107e
- Missed: none known
- Follow-up: none promoted; `RQ422` remains WAITING
- Residual risk: broader visible-scope parity remains owned by `RQ373`
- Next: none
- Prompt defect / scope repair: guardrail baseline updated for intentional scope-clear `setData(null)`; no violation waived

---

## RQ420 - Supplier Sales derived projections must track every total they read

Status: DONE
Ready after: `RQ373`/`RQ378`/`RQ379` owners confirm the response fields are stable
Priority: P2
Type: frontend/tests
Feature family: supplier-sales-derived-projection-freshness
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ420-<agent>.lock.md`
Commit suggestion: `fix(analytics): refresh supplier sales derived projections`

### Problem

The `decisionSuppliers` memo reads revenue, margin and unit totals plus recommendation fields, but its dependency list includes only suppliers and total revenue. A response update that changes margin/unit totals while retaining supplier references can leave share-of-margin, share-of-units and cost projections stale.

### Evidence

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:755-840` reads `ukupanPromet`, `ukupanMarzniDoprinos`, `ukupnaKolicina`, supplier margin/cost/recommendation fields and derives fallback shares.
- `:841` lists only `data?.suppliers` and `data?.totals.ukupanPromet` as dependencies.
- `RQ408/OP2-08` records this stale-derived-memo risk.

### Scope

- Supplier Sales `decisionSuppliers` derived projection and its table/chart/detail/export consumers.
- React dependency correctness and authoritative-versus-derived field policy.

Do not recalculate backend business metrics beyond the existing fallback contract; do not change weighted margin formulas.

### Read first

- `SupplierSalesStatsPage.tsx`
- Supplier Sales DTO/schema and `RQ373`, `RQ378`, `RQ379`
- `RQ362`, `RQ364`, `RQ408/OP2-08`

### Do

1. Make the projection recompute whenever any input it reads changes, or remove the local fallback when backend fields are authoritative.
2. Preserve valid zero, null/missing and non-finite semantics.
3. Prove that all consumers receive the same current projection after a response refresh, scope change or partial response.

### Tests

- Change only total margin; share-of-margin updates.
- Change only total units; share-of-units updates.
- Change cost coverage/recommendation fields with same supplier array; dependent status/projection updates.
- Null/zero totals remain unavailable or valid zero as declared.

### Acceptance

- Supplier Sales never shows a projection calculated from an older total alongside newer rows.
- No frontend formula becomes a second owner for backend-authoritative values.
- Focus, chart, table, detail and export use the same current state.

### Dependencies

- Coordinate `RQ373`, `RQ378`, `RQ379`, `RQ407` and the shared projection contract from `RQ362`/`RQ364`.

### Completion note

- Date: 2026-09-24
- Status: DONE
- Completion: Extracted `buildDecisionSuppliers` and keyed the page projection off the full response object so margin/unit totals and recommendation changes recompute derived shares/status without stale memo dependencies.
- Changed files: `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`, `SupplierSalesStatsPage.decisionSuppliers.spec.tsx`, guardrail baseline line repair.
- Contract/runtime behavior changed: derived share-of-margin/share-of-units and recommendation projection now track every response input they read; no backend formula changes.
- Checks run: `npm run test -- --run src/pages/__tests__/SupplierSalesStatsPage.decisionSuppliers.spec.tsx`; `npm run check:analytics-guardrails`.
- Checks not run: full Supplier Sales page suite; backend integration tests.
- Run log: `.ai/runs/2026-09-24-RQ420-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: e8cf5cc4
- Main verification: passed — current `origin/main` contains e8cf5cc4
- Missed: none known
- Follow-up: none promoted; `RQ421` remains WAITING behind Supplier status-identity ownership
- Residual risk: broader visible-scope parity remains owned by `RQ373`
- Next: none
- Prompt defect / scope repair: guardrail baseline lines moved after extracting `buildDecisionSuppliers`; no violation waived

---

## RQ421 - Supplier Sales must preserve backend status identity when actionability is blocked

Status: DONE
Ready after: `RQ379` runtime schema and `RQ373` status/count ownership are aligned
Priority: P1
Type: frontend/contract/tests
Feature family: supplier-sales-status-identity
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ421-<agent>.lock.md`
Commit suggestion: `fix(analytics): preserve supplier sales status identity`

### Problem

Supplier Sales replaces every backend recommendation status with `insufficient_data` whenever `recommendationAllowed` is false. This can hide meaningful `review` or `do_not_trust` states and make header, counts, detail or export disagree with the backend decision.

### Evidence

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:780-792` computes `backendStatus`, then sets `status = recommendationAllowed ? backendStatus : "insufficient_data"`.
- `RQ408/OP2-11` records the status identity risk; existing `RQ284` is Shoe Type-specific and does not own Supplier Sales.
- Backend status, actionability and reason are already distinct fields in the Supplier response contract.

### Scope

- Supplier Sales row status, header summary, counts, detail and export projections.
- Mapping of backend status versus `recommendationAllowed`, confidence/reliability and reason.

Do not recreate recommendation scoring or weaken the actionability gate.

### Read first

- `SupplierSalesStatsPage.tsx`
- Supplier Sales DTO/schema and backend recommendation projection
- `RQ284`, `RQ373`, `RQ379`, `RQ408/OP2-11`

### Do

1. Keep backend `status` as the status identity even when actionability is false or unknown.
2. Render actionability as a separate gate/affordance; blocked status must remain visible with safe reason text.
3. Define null/omitted/invalid status behavior as unknown or insufficient only when the backend status itself is unavailable.
4. Reconcile row, count, header, detail, export and action CTA projections.

### Tests

- `review`, `do_not_trust`, `insufficient_data` and `increase_focus` with `recommendationAllowed=false`.
- Missing/invalid status with blocked/unknown actionability.
- Allowed status preserves existing action behavior.
- Counts, table, detail and export use the same status identity.

### Acceptance

- A blocked recommendation cannot become a different backend status merely because it is not actionable.
- Users see both the status meaning and the actionability gate.
- No score/confidence/recommendation logic is reconstructed in the frontend.

### Dependencies

- Coordinate `RQ373`, `RQ374`, `RQ379`, `RQ407` and the shared decision-status contract.

### Completion note

- Date: 2026-09-24
- Status: DONE
- Completion: Preserved backend recommendation `status` identity when actionability is blocked; row badges now use `displayStatusLabel(status)`, actionability renders via separate reason chips/detail copy, and header/count/export projections stay keyed to backend status.
- Changed files: `SupplierSalesStatsPage.tsx`, focused Supplier Sales specs, guardrail baseline line repair, queue routing metadata.
- Contract/runtime behavior changed: blocked recommendations no longer collapse to `insufficient_data` or `Pomoćni signal`; users see both status meaning and actionability gate.
- Checks run: `npm run test -- --run src/pages/__tests__/SupplierSalesStatsPage.decisionSuppliers.spec.tsx src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`; `npm run check:analytics-guardrails`.
- Checks not run: full Supplier Sales page suite; backend integration tests.
- Run log: `.ai/runs/2026-09-24-RQ421-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: 980a671c5451278b7d806fa3c2e2dfef6bb2af4a
- Main verification: passed — current `origin/main` contains 980a671c
- Missed: none known
- Follow-up: none promoted; `RQ419` remains WAITING behind Supplier scope-event ownership
- Residual risk: broader visible-scope parity remains owned by `RQ373`
- Next: none
- Prompt defect / scope repair: guardrail baseline lines moved after status-display edits; no violation waived

---

## RQ422 - Supplier Footwear type insights must use an explicit full-cohort denominator

Status: DONE
Ready after: `RQ406` and `RQ411` attribution/completeness rules are agreed
Priority: P1
Type: backend/frontend/contract/tests
Feature family: supplier-footwear-type-insight-denominator
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ422-<agent>.lock.md`
Commit suggestion: `fix(analytics): align supplier footwear type insight denominator`

### Problem

Supplier Footwear derives global type shares from only the top eight categories and computes dominant-type elasticity as a simple article average. The chart can therefore normalize the visible top eight to 100% of a partial denominator, while sparse articles can dominate elasticity. If `articleStats` is truncated, these metrics are also incomplete.

### Evidence

- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx:149-190` aggregates `articleStats`, slices `globalCategoryRevenue` to eight entries, divides by the top-eight total and averages elasticities arithmetically.
- `:158-172` uses only returned comparable article rows; `RQ406` proves that the detail payload can be truncated.
- `RQ408/OP2-31, OP2-32` record top-eight denominator and unweighted-elasticity risks.

### Scope

- Backend-owned full comparable-cohort type aggregates or explicit completeness metadata.
- Supplier Footwear global type chart, vendor top-type share, dominant-type elasticity and export/detail projections.

Do not change the Supplier Footwear recommendation formula, historical attribution contract or presentation top-eight limit without declaring the denominator.

### Read first

- `SupplierFootwearAnalyticsPage.tsx`
- `Api/Endpoints/AllEndpoints.cs` Vendor Sales Nivelacija payload builder
- `RQ406`, `RQ411`, `RQ412`, `RQ408/OP2-31, OP2-32`

### Do

1. Define whether type share is over the full comparable cohort, the displayed top eight or another named population.
2. Supply full-cohort type totals and weighted/coverage-aware elasticity evidence, or mark the derived metric partial/unavailable when only capped rows exist.
3. Keep top-eight display trimming separate from business denominator calculations and surface excluded/unknown share.
4. Preserve valid zero and missing/insufficient evidence semantics.

### Tests

- Nine or more categories where displayed top eight must not silently sum to a full-population 100%.
- Capped article detail with a ninth category/dominant type outside the returned rows.
- Equal and sparse elasticity rows; compare declared weighted and unweighted behavior.
- Full cohort, partial cohort, unknown type and empty cohort states across chart/table/export.

### Acceptance

- Type shares have one explicit denominator and do not present top-eight normalization as full-cohort truth.
- Elasticity is either backend-owned with declared weighting/evidence or visibly unavailable/partial.
- Truncation cannot leave a dominant type or elasticity looking authoritative.

### Dependencies

- Coordinate `RQ406`, `RQ411`, `RQ412`, `RQ413` and existing Supplier Footwear route ownership.

### Completion note

- Date: 2026-09-24
- Status: DONE
- Completion: Completed the remaining OP2-31/32 display/weighting contract on top of `RQ406`: type chart uses full-cohort shares with top-eight plus `Ostali`, explicit denominator copy, and backend post-revenue-weighted category elasticity metadata.
- Changed files: `VendorSalesNivelacijaTypeInsightPolicy.cs`, `AllEndpoints.cs`, `VendorSalesNivelacijaModels.cs`, `SupplierFootwearAnalyticsPage.tsx`, focused tests, guardrail baseline line repair.
- Contract/runtime behavior changed: nine-category fixtures no longer imply a closed 100% top-eight population; excluded share is visible; elasticity weighting is machine-readable.
- Checks run: Supplier Footwear/type-insight vitest specs; `npm run check:analytics-guardrails`.
- Checks not run: `dotnet test VendorSalesNivelacijaTypeInsightPolicyTests` (dotnet unavailable in agent VM).
- Run log: `.ai/runs/2026-09-24-RQ422-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: fb1ced982c085472ba87a97da63c97fdcd7b4314
- Main verification: passed — current `origin/main` contains fb1ced98
- Missed: none known
- Follow-up: none promoted
- Residual risk: historical sale-line attribution remains owned by `RQ411`
- Next: none
- Prompt defect / scope repair: treated `RQ406` as satisfying the backend full-cohort aggregate prerequisite; `RQ422` closed the remaining display denominator and elasticity-weighting gaps

---

## RQ423 - Pre-Nivelacija focus must be population-aware across pages and projections

Status: DONE
Ready after: `RQ388` is DONE; coordinate current Pre-Nivelacija API/pagination owner
Priority: P1
Type: backend/frontend/contract/tests
Feature family: pre-nivelacija-focus-population-parity
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ423-<agent>.lock.md`
Commit suggestion: `fix(analytics): align pre-nivelacija focus population`

### Problem

Pre-Nivelacija sends page and page size to the API but applies `focus` only to the current `tableRows` page. A focus such as high priority or `review` can hide matching candidates that exist on later pages, while summary/total candidates remain global. Detail/export projections then mix page-local filtered rows with global totals.

### Evidence

- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx:491-501` does not send `focus` in `getPreNivelacijaPrioriteti`.
- `:669-678` filters only the returned `tableRows` locally.
- `:680-692` passes global summary alongside page-local `filteredRows`, `detailRows` and export rows.
- `RQ408/OP2-46, OP2-49` identify the page-local focus and mixed population contract; `RQ388` owns the completed global KPI population baseline.

### Scope

- Pre-Nivelacija focus query contract, pagination, row counts, empty state, detail and export projections.
- Explicit page, filtered-population and global-population metadata/labels.

Do not change the backend scoring window or recommendation gate (`RQ390`, `RQ297`).

### Read first

- `PreNivelacijaPriorityPage.tsx`
- `getPreNivelacijaPrioriteti` service and backend endpoint/DTO
- `RQ388`, `RQ390`, `RQ391`, `RQ299`, `RQ408/OP2-46, OP2-49`

### Do

1. Decide whether focus is a server-side population filter or a deliberately page-local display filter; the UI must not imply the other.
2. Prefer server-side focus filtering before pagination when the user expects to browse all matching candidates.
3. Recompute `totalCandidates`, page navigation, empty state, row counts, detail and export against the declared filtered population.
4. Keep global KPI summary separate and visibly labelled when it is not focus-filtered.
5. Preserve URL focus/page state and reset page deterministically when focus changes.

### Tests

- Matching focus row exists only on page 2: focus view finds it and counts it under the declared contract.
- Focus with no matches, page beyond filtered total and scope change.
- Global summary versus filtered counts remain explicitly distinct.
- Detail/export contain only the declared filtered population and preserve status/reason/provenance.

### Acceptance

- Focus cannot silently hide matching candidates because they are outside the current page.
- Every count/label identifies global, filtered or current-page population.
- URL, pagination, table, detail and export use one focus contract.

### Dependencies

- Coordinate `RQ388`, `RQ299`, `RQ326`, `RQ330`, `RQ391` and `RQ407`.

### Completion note

- Date: 2026-09-24
- Status: DONE
- Completion: Added backend `focus` query filtering before pagination; frontend sends focus to the API, removed page-local focus masking, and kept global summary KPIs/tab badges separate from the visible page slice.
- Changed files: `PreNivelacijaPriorityEndpoints.cs`, `PreNivelacijaPopulationTests.cs`, `PreNivelacijaPriorityPage.tsx`, `preNivelacijaApi.ts`, focused specs, guardrail baseline line repair.
- Contract/runtime behavior changed: focus browsing uses one server-side filtered population for pagination, table, detail and export; global summary counts remain unfiltered.
- Checks run: `npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/services/__tests__/preNivelacijaApi.scope.spec.ts`; `npm run check:analytics-guardrails`.
- Checks not run: `dotnet test` for `PreNivelacijaPopulationTests` (dotnet unavailable in agent VM); full backend suite.
- Run log: `.ai/runs/2026-09-24-RQ423-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: ccd70d30aede4647d5d747438f7b5e453a4dec73
- Main verification: passed — current `origin/main` contains ccd70d30
- Missed: none known
- Follow-up: none promoted; `RQ424` remains WAITING
- Residual risk: Pre-Nivelacija percentage denominator contract remains owned by `RQ424`
- Next: none
- Prompt defect / scope repair: updated page-local URL restore test to use a valid focused page-1 contract after server-side pagination semantics

---

## RQ424 - Pre-Nivelacija action-share and percentage normalization need one explicit contract

Status: DONE
Ready after: `RQ390`/`RQ391` remain authoritative for scoring-window and payload validation
Priority: P1
Type: backend/frontend/contract/tests
Feature family: pre-nivelacija-leaderboard-denominator
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ424-<agent>.lock.md`
Commit suggestion: `fix(analytics): define pre-nivelacija percentage denominators`

### Problem

The Pre-Nivelacija supplier action-share helper normalizes only the top seven leaderboard rows to 100%, while the generic percentage normalizer treats every numeric value `<= 1` as a ratio. Without an explicit API unit contract, a valid `1%` can display as `100%`, and a top-seven-only chart can look like a full-population share.

### Evidence

- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx:702-719` sorts, slices seven suppliers and divides by their subtotal without an “Ostali” bucket or denominator label.
- `:205-210` maps `1` to `100` and `0.5` to `50`, while treating values above one as percentage points.
- `RQ408/OP2-47, OP2-48` record both risks.

### Scope

- Supplier action-share API unit/denominator metadata and Pre-Nivelacija chart/table/export projections.
- Exact values `0`, `0.5`, `1`, `1.0`, `100`, null, negative and over-100.

Do not change the scoring formula or priority ranking.

### Read first

- `PreNivelacijaPriorityPage.tsx`
- Pre-Nivelacija DTO/schema/backend leaderboard projection
- `RQ390`, `RQ391`, `RQ398`, `RQ408/OP2-47, OP2-48`

### Do

1. Declare whether each percentage field is a ratio `[0,1]` or percentage points `[0,100]`; do not infer units from magnitude.
2. Define the action-share denominator: full leaderboard, all candidates, visible top seven, or top seven plus “Ostali”.
3. Prefer backend-owned normalized values and denominator metadata; fail closed on ambiguous/malformed units.
4. Keep chart, tooltip, table, export and empty/zero-denominator states consistent.

### Tests

- Exact `1` under the declared unit contract, plus `0.5`, `100`, null, negative and `101`.
- Seven suppliers versus eight suppliers with a non-zero eighth action score.
- Zero total, unknown supplier and partial leaderboard states.
- Export/chart labels state the same denominator and unit.

### Acceptance

- `1%` cannot be silently rendered as `100%`, and ratio/percentage units are machine-readable.
- Top-seven values cannot be labelled as full-population share without an explicit denominator or “Ostali”.
- Invalid or ambiguous values remain unavailable, not plausible.

### Dependencies

- Coordinate `RQ390`, `RQ391`, `RQ388`, `RQ407` and shared percentage-formatting contracts.

### Completion note

- Date: 2026-09-24
- Status: DONE
- Completion: Added backend `supplierActionShare` projection with explicit denominator policy/units and switched the page to percentage-point normalization without magnitude inference.
- Changed files: `PreNivelacijaPriorityEndpoints.cs`, `PreNivelacijaPriorityModels.cs`, `PreNivelacijaPopulationTests.cs`, `PreNivelacijaPriorityPage.tsx`, types/specs.
- Contract/runtime behavior changed: action-share chart uses full-leaderboard denominator with top-seven plus `Ostali`; `1` renders as `1%`, not `100%`.
- Checks run: focused Pre-Nivelacija vitest specs (45 tests); `npm run check:analytics-guardrails`.
- Checks not run: `dotnet test` (dotnet unavailable in agent VM).
- Run log: `.ai/runs/2026-09-24-RQ424-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: a8e7018eb3113d513c4c2e484a543edcb88edb0c
- Main verification: passed — current `origin/main` contains a8e7018e
- Missed: none known
- Follow-up: none promoted
- Residual risk: Supplier Footwear scope default remains owned by `RQ425`
- Next: none
- Prompt defect / scope repair: none

---

## RQ425 - Supplier Footwear standalone scope must not default silently to all data

Status: DONE
Ready after: canonical Supplier parent/embedded scope owner confirms standalone precedence
Priority: P1
Type: frontend/contract/tests
Feature family: supplier-footwear-scope-default
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ425-<agent>.lock.md`
Commit suggestion: `fix(analytics): preserve supplier footwear data scope`

### Problem

Supplier Footwear initializes `activeFilters.dataScope` to `sharedFilters?.dataScope ?? null`. In standalone mode, `null` can be normalized by the request layer as `all`, even when the user's global scope is `existing` or `imported`.

### Evidence

- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx:259-266` stores `dataScope` as `null` when no shared parent is present.
- `:326-340` sends `filters.dataScope` to both current and previous period calls.
- `RQ408/OP2-30` records the standalone default risk; `RQ389` is Color-specific and does not prove this Supplier Footwear path.

### Scope

- Supplier Footwear standalone route initialization, global-scope read, URL state and current/previous period requests.
- Scope lineage in trust metadata and comparable cohort calculations.

Do not change Supplier Footwear type metrics (`RQ422`) or Supplier Sales scope event handling (`RQ419`).

### Read first

- `SupplierFootwearAnalyticsPage.tsx`
- shared scope hook/storage and canonical Supplier parent filters
- `RQ278`, `RQ389`, `RQ406`, `RQ408/OP2-30`

### Do

1. Define precedence for embedded shared scope, explicit URL scope and standalone global scope.
2. Normalize the effective scope before the first request; never use null as an implicit trusted `all` without contract evidence.
3. Keep previous-period/comparable requests on the same effective scope.
4. Expose requested/effective scope in trust metadata and clear stale data on scope transition.

### Tests

- Standalone route with global `existing` and `imported` loads the selected scope on first request.
- Explicit URL scope overrides only when the contract says it should.
- Embedded parent scope and previous-period request remain aligned.
- Missing/invalid global scope is explicit/unavailable, not silently trusted as `all`.

### Acceptance

- Supplier Footwear cannot silently display all-source data when a narrower global scope is active.
- Current, previous, comparable and trust metadata share one effective scope.
- Canonical Supplier composition remains compatible.

### Dependencies

- Coordinate `RQ278`, `RQ389`, `RQ406`, `RQ411`, `RQ419` and canonical Supplier filter ownership.

### Completion note

- Date: 2026-09-24
- Status: DONE
- Completion: Standalone Supplier Footwear resolves `effectiveDataScope` from embedded shared filters, URL `dataScope` or global storage; current/previous API calls and export metadata use that scope; scope transitions clear stale rows/trust state and reload.
- Changed files: `SupplierFootwearAnalyticsPage.tsx`, `SupplierFootwearAnalyticsPage.scope.spec.tsx`, guardrail baseline.
- Contract/runtime behavior changed: `null` dataScope is no longer sent on standalone first load; global `existing`/`imported` scope is honored before request normalization.
- Checks run: `npm run test -- --run src/pages/__tests__/SupplierFootwearAnalyticsPage.scope.spec.tsx src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx`; `npm run check:analytics-guardrails`.
- Checks not run: full backend suite.
- Run log: `.ai/runs/2026-09-24-RQ425-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: a360fec775d053d04814bdc7be82ed1cc5a3de07
- Main verification: passed — current `origin/main` contains a360fec7
- Missed: none known
- Follow-up: none promoted; `RQ422` remains WAITING
- Residual risk: Supplier Footwear type-share denominator remains owned by `RQ422`
- Next: none
- Prompt defect / scope repair: none

---

## RQ426 - Inventory forecast risk aggregation must match row granularity

Status: DONE
Ready after: `RQ371` forecast scope/period contract is explicit
Priority: P1
Type: frontend/backend/contract/tests
Feature family: inventory-forecast-risk-aggregation
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/RQ426-<agent>.lock.md`
Commit suggestion: `fix(analytics): align inventory forecast risk aggregation`

### Problem

Inventory maps all matching forecast rows to one SKU/store row and takes the maximum OOS and overstock risk across sizes/forecast records. This may overstate SKU-level risk when the backend rows represent independent sizes or periods, and it can mix records when the selected store is null.

### Evidence

- `Klijent/clientapp/src/pages/InventoryPage.tsx:762-770` matches by SKU and optionally store, then reduces `probabilityOfOOSIn7d` and `overstockRisk` with `Math.max`.
- The same file warns that local risk sorting is page-local, so the aggregated number is used in table sorting and export-facing display paths.
- `RQ408/OP2-29` records the size-row aggregation risk; `RQ371` owns the wider forecast period/scope contract.

### Scope

- Forecast-to-inventory row identity and aggregation for SKU/store/size granularity.
- Risk sort, row display, warning metadata and export projections that consume the mapped risk.

Do not change the underlying forecast model or page-local/global sorting policy except to preserve the declared metric meaning.

### Read first

- `InventoryPage.tsx`
- forecast DTO/service and backend forecast endpoint
- `DemandForecastPanel.tsx` and inventory export helpers
- `RQ274`, `RQ371`, `RQ408/OP2-29`

### Do

1. Document forecast row grain and define whether SKU risk is max, weighted probability, independent-size list or another backend-owned aggregate.
2. Match store identity strictly when a store is selected; do not merge all-store rows into a store row.
3. Prefer an authoritative backend aggregate; otherwise expose the aggregation basis and partial/unknown state.
4. Keep valid zero distinct from missing risk and ensure exports use the same value/basis.

### Tests

- Two sizes with different risks: expected SKU aggregate follows the declared policy.
- Same SKU across two stores with a selected store: no cross-store merge.
- No matching rows, null risks, zero risks and partial forecast metadata.
- Table, risk sort, warning and export agree on value and aggregation basis.

### Acceptance

- Inventory risk is not silently overstated by a frontend `max` over rows with a different grain.
- Store and size scope are explicit and tested.
- Missing/partial forecast evidence cannot appear as a measured zero or fully trusted risk.

### Completion note

- Date: 2026-09-24
- Status: DONE
- Delivery mode: direct-main
- Main commit SHA: 85f0da051d17b5df04e6725281d99e34a629151d
- Main verification: passed — current `origin/main` contains 85f0da05
- Run log: `.ai/runs/2026-09-24-RQ426-evidence.md`
- Evidence state: synchronized
- Missed: full `RQ371` secondary-signal period/data-scope threading remains a separate owner
- Follow-up: promote `RQ308`/`RQ371` for Inventory period selector and cached signal parity; `RQ414` remains WAITING on `RQ371`
- Residual risk: page-local risk sort scope unchanged; global forecast panel still lists per-size rows
- Next: none promoted

### Dependencies

- Coordinate `RQ274`, `RQ371`, `RQ407`, forecast backend owner and `RQ413` drift/freshness evidence.

---

## RQ445 - Define the canonical Supplier/Shoe Type accuracy contract

Status: DONE
Ready after: current certification queue review confirms no competing contract owner
Priority: P0
Type: docs/contract/tests
Feature family: supplier-shoetype-accuracy-contract
Parallel-safe: yes for documentation; coordinate before changing shared DTO names
Owner: Analytics Reliability / Supplier + Shoe Type
Local lock: removed after DONE
Commit suggestion: docs(analytics): define Supplier and Shoe Type accuracy contract

### Problem

RQ411, RQ412 and the existing golden manifests define important attribution and arithmetic facts, but there is no single canonical contract that binds formulas, population, period, store, scope, unknown handling, provenance, cost coverage, trust status, freshness and the permitted customer-facing accuracy claim across Supplier and Shoe Type. Without that contract, separate proofs can all pass while describing different populations.

### Evidence

- The Supplier/Shoe Type independent oracle manifest covers RQ412 raw-fact semantics but not the full API-to-screen/export/certificate contract.
- RQ407 proves shared fixture route arithmetic; RQ411-RQ413 provide attribution and bounded runtime integrity semantics.
- Current evidence distinguishes numeric proof from browser, live and durable certification proof.

### Scope

- One canonical contract document linked from the Operations accuracy addendum and roadmap.
- Supplier and Shoe Type revenue, signed quantity, shares, unknown buckets, period/store/scope, attribution basis, cost coverage, trust states and certificate claim language.
- Canonical retail-sales document population: `BrojRacuna` values `DUG` and `KOREKCIJA` (trimmed, case-insensitive) are non-standard adjustment/debt documents and are excluded from certified retail turnover on Daily Sales, Supplier, Shoe Type and Color; if needed commercially they must be surfaced separately, never silently mixed into retail sales.
- Shoe Type identity is ID-based: `tipObuceId == null` is the unknown bucket; a non-null ID with a blank or `Nepoznato` display label remains a known identity and is a label-quality issue.
- Margin contribution share is available only when the aggregate margin-contribution denominator is strictly positive; zero/negative totals render the share unavailable and absolute RSD contribution remains the truthful fallback.
- Shoe Type row-level PoP population is the union of current and previous-period identities so a previous-only type receives a current-zero row and can expose a truthful `-100%` change when its previous denominator is positive.
- The headline `Prosečna marža` card is a covered-revenue-weighted aggregate over the full current response sales population (`sum(marginContribution) / sum(costCoveredRevenue)`); recommendation benchmarking may separately use the known-identity cohort and must be named/provenanced separately.
- Explicit distinction between reconciled sales facts and qualified or estimated margin evidence.

Do not redefine the RQ411 attribution source, RQ412 oracle SQL or RQ442 date implementation.

### Tests

- Contract examples for all/imported/existing, store filter, returns, unknown buckets, zero/negative totals, missing cost and comparable pre/post populations.
- Validator or lint check that every certification artifact names contract version, source population and evidence id.

### Acceptance

- A reviewer can determine one authoritative formula and denominator for every certified metric.
- The contract states when Supplier/Shoe Type may be marked VERIFIED and when it must be unverified, degraded or unavailable.
- The contract explicitly forbids an unqualified 100% accurate claim for incomplete cost or unknown attribution evidence.

### Dependencies

- Consume RQ373-RQ380, RQ375-RQ377, RQ407, RQ411-RQ413 and RQ442; do not duplicate their implementation ownership.

### Completion note

- Date: 2026-09-26
- Status: DONE
- Completion: canonical `SST-ACCURACY-1.0` contract established for bounded Supplier/Shoe Type certification claims
- Changed files: `docs/qa/SUPPLIER_SHOETYPE_ACCURACY_CONTRACT_2026-09-26.md`, certification plan, RQ addendum, RQ queue routing and `MASTER_ROADMAP.md`
- Contract/runtime behavior changed: contract and governance documentation only; no runtime code or data changed
- Checks run: `git diff --check`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs`; `node scripts/check-agent-instructions.mjs` — pass
- Checks not run: frontend/backend runtime suites — not run because RQ445 is docs/contract-only
- Run log: `.ai/runs/2026-09-26-RQ445-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `7ce49b17a382c411b2d24ec3f5c7e28de4eabb33`
- Main verification: passed — fresh `origin/main` equals `7ce49b17a382c411b2d24ec3f5c7e28de4eabb33`; implementation commit `8548667a627c276a605d8c1143a19b86eff015c1` is an ancestor
- Missed: live PostgreSQL execution, browser/render/export reconciliation, durable runtime evidence, production proof and customer acceptance remain later prompts
- Follow-up: re-enter idle recovery for RQ456/RQ457 after collision/dependency refresh
- Residual risk: the contract is authoritative, but the product is not yet certified until the later execution and evidence gates pass
- Prompt defect / scope repair: none; main queue pointer remains `none` because RQ445 belongs to the Operations accuracy addendum

---

## RQ446 - Build the adversarial Supplier/Shoe Type golden dataset

Status: DONE
Ready after: RQ445 contract version is approved
Priority: P0
Type: fixture/tests/docs
Feature family: supplier-shoetype-adversarial-golden
Parallel-safe: no with RQ407/RQ412 fixture files
Owner: Analytics Reliability / Test Infrastructure
Local lock: removed after DONE
Commit suggestion: test(analytics): add adversarial Supplier and Shoe Type golden cases

### Problem

The shared RQ407 seed proves core arithmetic, but it does not yet provide the complete adversarial certification corpus needed to defend the two target screens against boundary, duplication, mutation and evidence-quality failures.

### Scope

Extend the existing RQ407 fixture and RQ412 manifest; do not create a second seed system. Include returns and negative net revenue, null supplier/type, a non-null Shoe Type whose label is literally `Nepoznato`, a non-null Shoe Type with blank name, previous-period-only Shoe Type, `DUG`/`KOREKCIJA` mixed-case/whitespace documents, two stores, Access and POS scope, exact lower and upper date boundaries including fractional seconds, post-sale master mutation, duplicate-import attempt, missing and mixed cost coverage, zero/negative total margin contribution, top-N with unknown and cache-before/after-import states. Store expected totals, buckets, denominators and evidence classifications as immutable JSON or Markdown.

### Tests

- Raw SQL oracle and API expectations for every case, with Supplier and Shoe Type bucket, total, share and line-count assertions.
- Mutation and duplicate-import invariants; adjacent whole-day non-overlap; known versus unknown and cost coverage semantics.
- Deterministic manifest hash and reviewable diff when a fixture changes.

### Acceptance

- Every listed adversarial case has an explicit expected result and a named proof owner.
- The fixture remains the shared RQ407/RQ412 source; no production aggregation helper is reused as the oracle.
- A changed current Artikli master cannot change frozen historical bucket expectations.

### Dependencies

- RQ445, RQ407, RQ411, RQ412 and RQ442. Coordinate file ownership before implementation.

### Completion note

- Date: 2026-09-26
- Status: DONE
- Completion: Extended the shared RQ407/RQ412 fixture with an out-of-window adversarial corpus and added `SUPPLIER_SHOETYPE_ADVERSARIAL_GOLDEN_MANIFEST_2026-09-26.json` plus its SHA-256 sidecar. Added deterministic tests for manifest integrity, required case coverage and fixture markers; no production aggregation helper was used as an oracle.
- Changed files: `Api.Tests/Fixtures/operations-analytics-all-routes-seed.sql`; `Api.Tests/SupplierShoeTypeAdversarialGoldenManifestTests.cs`; `docs/qa/SUPPLIER_SHOETYPE_INDEPENDENT_ORACLE_MANIFEST_2026-09-25.md`; `docs/qa/SUPPLIER_SHOETYPE_ADVERSARIAL_GOLDEN_MANIFEST_2026-09-26.json`; `docs/qa/SUPPLIER_SHOETYPE_ADVERSARIAL_GOLDEN_MANIFEST_2026-09-26.sha256`.
- Checks run: focused `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SupplierShoeTypeAdversarialGoldenManifestTests" --no-restore --no-build` (2/2); manifest JSON/hash/arithmetic validation; `git diff --check`; `node scripts/check-agent-instructions.mjs --self-test`; `node scripts/check-agent-instructions.mjs`; `node scripts/check-prompt-queues.mjs --self-test`; `node scripts/check-prompt-queues.mjs`; `node scripts/check-planning-architecture.mjs --self-test`; `node scripts/check-planning-architecture.mjs`.
- Checks not run: live PostgreSQL/API/browser/export execution remains RQ447/RQ448.
- Run log: `.ai/runs/2026-09-26-RQ446-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `5979ef652cbab90df9ca5a722c0570b956edb5e1` before the evidence/hash-stability closure commit
- Main verification: completed after pushing the closure commit; `origin/main` contains the implementation and synchronized evidence.
- Missed: RQ456/RQ457 receipt/identity runtime semantics, RQ447 live execution and later certification layers remain open.
- Follow-up: promote RQ447 only after RQ456, RQ457 and a PostgreSQL integration host are available.
- Residual risk: the immutable fixture expectations are reviewable and deterministic, but the API has not yet been executed against every adversarial case.

---

## RQ447 - Execute the live oracle and close the OP2 runtime seams

Status: DONE
Ready after: PostgreSQL integration host is available and RQ446, RQ456 and RQ457 are DONE
Priority: P0
Type: integration-tests/evidence
Feature family: supplier-shoetype-live-oracle-reproducers
Parallel-safe: no with RQ412 integration host/test files
Owner: Analytics Reliability / QA
Commit suggestion: test(analytics): execute Supplier Shoe Type oracle and seam reproducers

### Problem

RQ412 implementation exists, but its four PostgreSQL integration cases were not executed in the recorded run. OP2-01, OP2-04, OP2-05 and OP2-14 remain potential only; static classification is not certification evidence.

### Scope

Run the four SupplierShoeTypeIndependentOracleIntegrationTests cases on PostgreSQL and add focused runtime reproducers for OP2-01 date boundaries, OP2-04 legacy/canonical route parity, OP2-05 backend status-to-UI projection parity and OP2-14 requested versus effective period. This is a proof/triage lane: implement a minimal fix only if a reproducer fails and assign the failure to the correct existing or new owner.

### Tests

- Exact executed/pass/skip counts for all four RQ412 cases; no opt-in skip accepted for the certification run.
- OP2-01/04/05/14 each has a reproducible fixture, observed result, owner decision and evidence link.
- Supplier and Shoe Type totals, buckets, shares, unknown rows, store/scope/period metadata and detail-level result are compared, not only headline totals.

### Acceptance

- RQ412 is recorded as executed on PostgreSQL with unexpectedDeltaCount=0 or a classified, reproducible variance.
- Each OP2 seam is either closed by a passing runtime proof or becomes one minimal owner-linked defect; no speculative duplicate prompt is created.
- Evidence explicitly distinguishes skipped, unavailable and passed tests.

### Dependencies

- RQ412, RQ445, RQ446, RQ456, RQ457, RQ442 and the existing OP2 classification. Do not execute certification against the superseded receipt/identity/PoP semantics and do not reopen potential findings without a failing reproducer.

---

## RQ448 - Reconcile raw facts through API, browser render and export

Status: WAITING
Ready after: RQ447 has a runnable fixture and authenticated test browser/API environment
Priority: P0
Type: e2e/browser/export/tests
Feature family: supplier-shoetype-browser-reconciliation
Parallel-safe: no with Supplier/Shoe Type page and export owners
Owner: Analytics Reliability / Frontend QA
Commit suggestion: test(analytics): reconcile Supplier and Shoe Type from DB to export

### Problem

RQ407 live PostgreSQL proof and focused frontend tests do not prove the deployed request-to-render chain. The missing chain is raw facts -> independent oracle -> API -> frontend model -> rendered KPI/table -> detail -> CSV/XLSX export.

### Scope

For both Supplier and Shoe Type, seed the certified fixture, execute the independent oracle, open the actual route in a browser, apply period/store/scope, capture request/response and rendered KPI/table/detail values, and export the same population. Include legacy Supplier route redirect parity where applicable.

### Tests

- Browser assertions for requested/effective period, store, data scope, trust state and every visible row.
- Detail and export assertions use the same evidence id, population and totals as the page.
- Capture a trace or screenshot plus machine-readable reconciliation artifact; static bundle fetch does not count as render proof.

### Acceptance

- DB oracle, API, rendered screen, detail and export have zero unexplained quantity/revenue/bucket/denominator deltas.
- A failed, stale or partial evidence state is rendered truthfully and cannot receive a Verified result.
- Artifact records deployed/app commit, schema, route, filters, evidence id and executed test counts.

### Dependencies

- RQ445-RQ447, RQ442, RQ413, browser test account and deployment access. Do not duplicate route business logic owners.

---

## RQ449 - Persist integrity evidence history

Status: WAITING
Ready after: RQ445 contract and RQ413 runtime states are stable
Priority: P1
Type: backend/data-contract/migration/tests
Feature family: operations-integrity-evidence-history
Parallel-safe: no with RQ413 integrity persistence paths
Owner: Analytics Reliability / Operations
Commit suggestion: feat(analytics): persist integrity evidence history

### Problem

RQ413 keeps a process-wide integrity snapshot and evidence id, but a restart does not provide a durable history of what was checked. A customer-facing claim needs immutable, queryable evidence records rather than only the latest in-memory state.

### Scope

Add a durable, append-only integrity evidence record for the certified Supplier/Shoe Type run. Record tenant/store, database fingerprint, app commit, schema/migration version, contract/fixture version, requested/effective period, scope, raw/API totals, bucket deltas, unknown/attribution/cost coverage, cache version, status, timestamps and failure classification. Keep secrets and raw customer data out of the record.

### Tests

- Append/retrieve/restart persistence, idempotency for one evidence id and concurrent-run behavior.
- Tenant/store/scope isolation and redaction tests.
- Status transitions preserve history; a later drift does not rewrite a previously verified run.

### Acceptance

- Evidence survives process restart and can be retrieved by evidence id.
- Stored data is sufficient to reproduce the claim without storing credentials or unrestricted sale-line payloads.
- RQ413 live status remains authoritative; this prompt adds history, not a second probe formula.

### Dependencies

- RQ413, RQ445, existing migration/bootstrap conventions and approved data-retention policy.

---

## RQ450 - Probe immediately after Access import

Status: WAITING
Ready after: RQ413 and RQ449 evidence lifecycle are available
Priority: P1
Type: backend/import/worker/tests
Feature family: operations-post-import-probe
Parallel-safe: no with Access import and integrity trigger paths
Owner: Analytics Reliability / Import Operations
Commit suggestion: feat(analytics): probe integrity after Access import

### Problem

RQ413 marks analytics unverified on cache clear and has worker or on-demand probes, but the recorded evidence does not prove an immediate post-import Supplier/Shoe Type probe. A fresh import can therefore leave the UI ambiguous until a later worker tick.

### Scope

Wire successful Access-import completion to invalidate caches, persist an unverified transition, run one bounded Supplier/Shoe Type integrity probe against the imported dataset, then publish verified/degraded/drift_detected with evidence id. Failure must remain visible and fail closed; import success must not be rewritten as analytics verification.

### Tests

- Successful import triggers exactly one bounded probe after commit; failed or rolled-back import triggers none.
- Probe failure, timeout and drift preserve unverified/degraded/drift_detected state and recommendation gating.
- Import retry/idempotency does not duplicate evidence or race the previous dataset.

### Acceptance

- After import, Supplier/Shoe Type cannot show Verified before the corresponding probe finishes successfully.
- Probe is bounded, observable and linked to the import batch and evidence id.
- Existing RQ413 cache-clear and worker behavior remains compatible; no duplicate drift algorithm is introduced.

### Dependencies

- RQ413, RQ449, Access import transaction boundary and worker/DI configuration.

---

## RQ451 - Show Verified status and inspectable evidence in both screens

Status: WAITING
Ready after: RQ445, RQ448 and RQ449 expose stable evidence fields
Priority: P1
Type: frontend/contract/tests
Feature family: supplier-shoetype-verified-evidence-ui
Parallel-safe: no with Supplier/Shoe Type page trust surfaces
Owner: Analytics Reliability / Frontend QA
Commit suggestion: feat(analytics): expose Supplier and Shoe Type verification evidence

### Problem

Backend already exposes operationsIntegrity status, checked time and evidence id, but the current Supplier and Shoe Type screens do not present a complete, inspectable proof surface to the customer.

### Scope

Add a shared evidence/status surface to both screens with status, checked-at, evidence id, reconciled line count, quantity and revenue deltas, attribution and unknown coverage and cost coverage. Keep requested versus effective period and data scope visible; do not hide warning, degraded or unknown states behind a green badge.

### Tests

- Verified, unverified, degraded and drift_detected fixtures for header, table/detail context and recommendation gate.
- Evidence link or drawer shows the same id and metrics as the API; loading/error/empty states remain explicit.
- Browser/accessibility proof covers both canonical and legacy Supplier entry points.

### Acceptance

- Verified appears only when current evidence is fresh, contract-compatible and has zero unexplained deltas.
- Users can inspect what was checked, when, for which period/store/scope and with which evidence id.
- Unknown attribution and incomplete cost coverage remain visible and qualified.

### Dependencies

- RQ445, RQ448, RQ449, RQ450, RQ421, RQ413 and existing trust-header ownership.

---

## RQ452 - Generate an evidence-backed accuracy certificate

Status: WAITING
Ready after: RQ449 and RQ451 are DONE
Priority: P1
Type: backend/frontend/export/docs/tests
Feature family: supplier-shoetype-accuracy-certificate
Parallel-safe: no with evidence/export surfaces
Owner: Analytics Reliability / Customer Readiness
Commit suggestion: feat(analytics): generate Supplier Shoe Type accuracy certificate

### Problem

Trendplus has no customer-facing certificate that binds a Supplier/Shoe Type claim to a specific evidence id, build, schema, period and source reconciliation result.

### Scope

Generate a PDF or HTML certificate from immutable evidence, with tenant/store, requested/effective period, scope, app commit, schema, contract version, verification time, Supplier and Shoe Type raw/API/render/export totals and deltas, unknown/attribution/cost coverage, status and limitations. Qualify margin and cost estimates instead of presenting them as raw-sales certainty.

### Tests

- Verified, degraded, drift, unavailable and stale evidence cannot generate a green certificate.
- Repeated generation for one evidence id is deterministic and includes the same metrics as the evidence record.
- PDF or HTML text and download metadata preserve evidence id and contract version.

### Acceptance

- Every certificate is traceable to one immutable evidence id and exact build/schema/contract versions.
- A certificate never claims zero unexplained deltas when a required layer was skipped or unavailable.
- Document is suitable for customer review and states the boundary between reconciled sales facts and estimated margin.

### Dependencies

- RQ445, RQ448, RQ449, RQ451 and existing document export contracts.

---

## RQ453 - Make analytics certification CI non-skippable

Status: WAITING
Ready after: RQ446 and RQ447 define the executable certification set
Priority: P0
Type: CI/tests/release-gate
Feature family: analytics-certification-ci-gate
Parallel-safe: yes for workflow/docs; coordinate test resource ownership
Owner: Analytics Reliability / CI
Commit suggestion: ci(analytics): add non-skippable Supplier Shoe Type certification gate

### Problem

RQ412 integration tests are opt-in and the recorded run explicitly skipped them when PostgreSQL was unavailable. A release cannot carry a Verified analytics claim when certification tests were not executed.

### Scope

Add a dedicated certification job with PostgreSQL, adversarial fixture, independent oracle, API tests, browser/render/export proof where available, schema and migration smoke and exact executed/passed/skipped accounting. The job fails on missing service, test skip, missing artifact, non-zero delta or stale/unverified evidence.

### Tests

- Forced missing database, skipped test, failed oracle, failed browser reconciliation and missing artifact each fail the job.
- Successful run reports expected=executed=passed for the required set and publishes machine-readable evidence.
- Normal developer CI may retain opt-in tests, but certification job may not.

### Acceptance

- No release or certificate path can treat skipped certification tests as pass.
- Gate runs on the exact commit being released and records app/schema/contract/fixture versions.
- Failed or unavailable certification blocks the Verified release claim while preserving diagnostic evidence.

### Dependencies

- RQ445-RQ448, RQ452, repository CI conventions and deployment credentials only through existing secret mechanisms.

---

## RQ454 - Run read-only production reconciliation for certified windows

Status: WAITING
Ready after: STAB16 grants approved read-only production connection and exact deployed commit proof
Priority: P0
Type: operations/read-only/reconciliation/evidence
Feature family: supplier-shoetype-production-reconciliation
Parallel-safe: no with production audit connection
Owner: Operations / Analytics Reliability
Commit suggestion: docs(analytics): record Supplier Shoe Type production reconciliation

### Problem

Existing STAB16 and older production audits identify missing read-only database and browser proof, but there is no Supplier/Shoe Type-specific production reconciliation artifact. Fixture correctness alone does not prove the customer deployed data path.

### Scope

Against an approved read-only production or replica connection, compare raw sale-line facts and the independent oracle with Supplier and Shoe Type API results for yesterday, 7-day, 30-day, year-to-date or full-year, each store and each supported data scope, plus a documented random sample. Record deployed SHA, schema, query and contract versions, counts, totals, bucket deltas, unknown and cost coverage and every variance. Never mutate source data, caches or production state.

### Tests

- Connection is read-only and runner fails before any write attempt.
- Each window, store and scope records executed counts and exact or approved variance classification.
- Any unexplained delta blocks the production certificate; timeouts or missing access are recorded as not-run, never pass.

### Acceptance

- Dated, reviewable artifact proves production Supplier/Shoe Type reconciliation or clearly records why certification is blocked.
- Zero unexplained deltas is required for a VERIFIED production claim; tolerances are explicit and never silently applied.
- Result is bound to deployed build and database/schema fingerprint.

### Dependencies

- STAB16, RQ445-RQ448, RQ453 and approved read-only credentials. Do not duplicate STAB16 deployment or worker ownership.

---

## RQ455 - Capture customer acceptance evidence

Status: WAITING
Ready after: RQ452 and at least one approved production or pilot reconciliation run
Priority: P1
Type: docs/acceptance/evidence
Feature family: supplier-shoetype-customer-acceptance
Parallel-safe: yes after the certificate/evidence schema is fixed
Owner: Customer Readiness / Analytics Reliability
Commit suggestion: docs(analytics): add Supplier Shoe Type customer acceptance pack

### Problem

Technical tests and internal reconciliation do not show that a customer recognizes the same source totals and understands qualified, unknown and cost-limited metrics. There is no customer acceptance artifact for these two screens.

### Scope

Create a reusable acceptance pack and one pilot execution template covering the customer's locked source window, store and scope; total revenue and quantity, returns, each Supplier bucket and each Shoe Type bucket; unknown, attribution and cost coverage; every variance and explanation; Trendplus build, schema and evidence id; customer reviewer and date. Keep credentials, raw personal data and unrestricted sale-line exports out of the repository.

### Tests

- Template validation requires source extract id/window, Trendplus evidence id, metric units, denominator, variance classification and reviewer approval.
- Missing approval, skipped reconciliation or unexplained variance cannot produce an accepted status.
- Redaction review confirms no secrets or unnecessary customer-level raw data are committed.

### Acceptance

- A customer can sign off a fixed period with zero unexplained discrepancies, or the pack remains pending with every variance classified.
- Acceptance is traceable to the same certificate/evidence id and contract version used by the product.
- Artifact distinguishes sales-fact reconciliation from qualified margin and cost evidence and never broadens the claim beyond the tested window.

### Dependencies

- RQ445, RQ448, RQ452, RQ454 and customer-provided source report plus named reviewer.


---

## RQ456 - Canonicalize retail-sales receipt population

Status: DONE
Ready after: RQ445 accuracy contract records the exclusion policy
Priority: P0
Type: backend/data-contract/oracle/detail/export/tests
Feature family: operations-retail-sales-receipt-population
Parallel-safe: no with Supplier/Shoe/Color aggregation or RQ412/RQ446 oracle fixture files
Owner: Analytics Reliability / Sales Population
Commit suggestion: fix(analytics): unify non-retail receipt exclusions

### Problem

Daily Sales explicitly excludes receipt numbers `DUG` and `KOREKCIJA` (trimmed/case-insensitive) and reports them as non-standard debt/correction documents. Supplier Sales, Shoe Type Sales and Color Sales currently join the same sale headers/lines without the same `BrojRacuna` population predicate. The screens can therefore report different retail turnover for the same period/store/scope and cannot be certified against one source population.

### Canonical decision

- `DUG` and `KOREKCIJA` are excluded from the certified retail-sales population everywhere, using trim + case-insensitive comparison.
- Their values remain auditable as excluded/non-standard documents; they are not deleted and may later receive a separate adjustments/debt surface.
- Returns represented as signed retail sale lines remain included; exclusion is document identity, not a rule that removes negative quantities.

### Scope

Centralize a reusable sales-receipt population policy and apply it consistently to Daily Sales, Supplier, Shoe Type and Color current-period, previous-period, data-window, detail and export paths. Apply the identical rule to RQ412 independent oracle SQL and RQ407/RQ446 certification fixtures/expectations. Avoid four endpoint-local string lists.

### Tests

- Exact/mixed-case/leading-trailing-space `DUG` and `KOREKCIJA` are excluded on all four sales surfaces.
- Standard receipts and signed return lines remain included.
- Same period/store/dataScope yields equal authoritative revenue/quantity populations before dimension grouping.
- Detail/export/oracle do not reintroduce excluded headers.
- Evidence records excluded document count/revenue where useful without adding it to retail turnover.

### Acceptance

- Daily/Supplier/Shoe Type/Color reconcile to one receipt population contract.
- No certified path silently counts DUG/KOREKCIJA after another certified path excludes them.
- RQ412 raw-fact oracle and RQ447 certification run use the same declared exclusion semantics without reusing production aggregation code.

### Dependencies

- RQ445, RQ407, RQ412, RQ446 and Daily Sales forensic evidence. Complete before RQ447 certification execution.

### Completion note

- Date: 2026-09-26
- Status: DONE
- Completion: Centralized the trim + case-insensitive `DUG`/`KOREKCIJA` retail-receipt exclusion and applied it to Daily, Supplier, Shoe Type and Color analytics, previous-period/detail/data-window/cost-snapshot paths and the independent RQ412 raw-fact oracle. Signed retail returns remain included; excluded documents remain available to Daily diagnostics.
- Changed files: `Api/Services/SalesReceiptPopulationPolicy.cs`; `Api/Services/DailySalesStatsService.cs`; `Api/Services/AnalyticsDetailReadService.cs`; `Api/Services/AnalyticsCostSnapshotService.cs`; `Api/Endpoints/AllEndpoints.cs`; `Api.Tests/Analytics/SupplierShoeTypeRawFactOracle.cs`; `Api.Tests/SalesReceiptPopulationPolicyTests.cs`.
- Checks run: focused .NET tests 24/24; `git diff --check`; queue/planning validators after delivery.
- Checks not run: live PostgreSQL/oracle execution, browser/export parity and full repository suite remain certification follow-ups.
- Run log: `.ai/runs/2026-09-26-RQ456-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `74a12194c25bf140cd7438bfe97a2b83a857c7fc`
- Main verification: passed — `origin/main` contains the implementation through merge `54a465254d53a2e71474877d1828a1c9f97f4195`.
- Missed: runtime proof against the RQ446 DUG/KOREKCIJA fixture and deployed API/browser chain remain RQ447/RQ448.
- Follow-up: promote RQ457 after this population contract is delivered and the Shoe Type owner remains collision-safe.
- Residual risk: the shared predicate is EF-translatable and unit-tested, but live SQL behavior still requires the PostgreSQL integration host.
- Prompt defect / scope repair: none; RQ412 oracle SQL was updated within the declared receipt-population scope.

---

## RQ457 - Harden Shoe Type identity, PoP union and margin semantics

Status: WAITING
Ready after: RQ445 records the canonical semantics; coordinate with RQ456 population work
Priority: P0
Type: backend/frontend/detail/export/tests
Feature family: shoetype-identity-pop-margin-semantics
Parallel-safe: no with Shoe Type endpoint/detail/page owners
Owner: Analytics Reliability / Shoe Type
Commit suggestion: fix(analytics): close Shoe Type identity and comparison semantics

### Problem

Current Shoe Type code has three residual certification defects:

1. Some backend/detail paths infer unknown identity from the display text `"Nepoznato"` instead of `tipObuceId == null`, so a legitimate non-null type with that/blank label can be merged into or excluded as unknown.
2. The response rows are created only from current-period sale lines; a type sold only in the previous comparison period contributes to total previous-period revenue but has no row, hiding a real -100% row-level change.
3. The top-level weighted margin code currently excludes unknown types by display label. The KPI needs a direct full-population weighted aggregate, while recommendation benchmarking may use a separate known-ID cohort.

The frontend margin-share helper was immediately hardened on 2026-09-25 so any total margin contribution <= 0 returns unavailable instead of a percentage; this prompt makes backend/detail/export semantics match that rule.

### Canonical decision

- Unknown Shoe Type identity means `tipObuceId == null` only. Display names never define identity.
- Row-level PoP population is the union of current and previous-period IDs. Previous-only rows have current revenue/units = 0, previous values preserved and -100% change when the previous denominator is positive.
- A previous-only row must not invent current-period cost, margin, pre/post or recommendation evidence; unavailable fields remain null/qualified and actionability is fail-closed.
- `Udeo u maržnom doprinosu` is N/A whenever total margin contribution <= 0; show absolute RSD contribution instead.
- Headline `Prosečna marža` = full current-response covered-revenue-weighted margin. A known-ID recommendation benchmark is separate metadata/field and must not masquerade as the headline KPI.

### Scope

Replace name-based Shoe Type unknown predicates in `AllEndpoints.cs`, `AnalyticsDetailReadService.cs`, integration/snapshot helpers and related frontend identity consumers with ID/null semantics. Build the current/previous identity union server-side and keep detail/export parity. Separate headline weighted-margin aggregate from known-ID recommendation benchmark with explicit DTO/provenance names. Preserve sale-time `ShoeTypeIdAtSale` from RQ411.

### Tests

- Non-null type named exactly `Nepoznato` remains known; blank name with non-null ID remains known and addressable by ID.
- Null ID is the only unknown bucket across list/detail/export/oracle.
- Current-only, both-period and previous-only types reconcile; previous-only row shows current 0 and previous > 0 without invented current evidence.
- Total/current/previous sums reconcile across rows and totals.
- Margin share is null/N/A for zero and negative aggregate contribution and percentage mode only for strictly positive aggregate contribution.
- Headline weighted margin equals `sum(current marginContribution) / sum(current costCoveredRevenue)` over the full response population; known-ID recommendation benchmark has its own proof.
- Detail and frontend rendered values preserve these semantics.

### Acceptance

- Shoe Type identity cannot change because of display-name text.
- Row-level PoP no longer drops previous-only types.
- The headline margin KPI, row/detail margin shares and recommendation benchmark have explicit non-conflicting denominators.
- RQ447/RQ448 can reconcile these semantics without special-case UI inference.

### Dependencies

- RQ375-RQ377, RQ411, RQ412, RQ285, RQ346, RQ445, RQ446 and RQ456 population contract. Complete before RQ447 certification execution.
