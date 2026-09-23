# Operations data accuracy audit evidence

- Date: 2026-09-23
- Task ID: OPERATIONS-DATA-ACCURACY-AUDIT
- Queue: direct-user-request
- Delivery target: `main`
- Repository: `ivanjovicic/Trendplus`
- Evidence state: synchronized

## Goal

Audit the source-to-screen data flow for all screens reachable from the **Operacije** menu, decide whether their numerical correctness is actually proven, focus deeply on **Prodaja po dobavljačima** and **Prodaja po tipu obuće**, and add non-duplicative reliability prompts that can make correctness durable.

## Current Operacije routing

| Menu item | Route | Effective surface |
|---|---|---|
| Zalihe i dopuna | `/analytics/inventory` | `InventoryPage` |
| Prodaja po dobavljačima | `/analytics/supplier-sales-stats` | redirect to canonical `/analytics/supplier?tab=overview` |
| Prodaja po tipu obuće | `/analytics/shoe-type-sales-stats` | `ShoeTypeSalesStatsPage` |
| Prodaja po smeni i dobavljačima | `/analytics/daily-sales` | `DailySalesStatsPage` |
| Pre/Posle nivelacije | `/analytics/nivelacije-pre-post` | `ProdajaPrePostNivelacijePage` |
| Prodaja po boji artikla | `/analytics/color-sales-stats` | `ColorSalesStatsPage` |
| Prioriteti nivelacije | `/analytics/pre-nivelacija-prioriteti` | `PreNivelacijaPriorityPage` |
| Dobavljači i tipovi obuće | `/analytics/dobavljaci-tipovi-obuce` | redirect to canonical Supplier assortment tab |

## Main conclusion

**No: the repository does not currently prove that every number on every Operacije screen is correct against authoritative business facts.**

There is substantial correctness work already delivered: contract tests, Zod/runtime validation on important payloads, deterministic fixtures, signed-sales fixes, comparable-cohort semantics, weighted margin rules, cache/freshness repairs, detail provenance and trust metadata. Those establish important local invariants.

What is still missing is a full proof chain:

`raw business facts -> canonical historical dimensions -> filtered population -> independent reference calculation -> API -> detail/export/report/action -> screen -> PostgreSQL/live replay -> ongoing drift detection`

The largest newly confirmed gap is historical dimension attribution for Supplier/Shoe Type.

## Supplier Sales: confirmed data flow and residual risk

The Operacije link does not render an isolated Supplier Sales page. It redirects to `SupplierConsolidatedPage`, tab `overview`, which embeds `SupplierSalesStatsPage` as the primary/final recommendation surface. The same consolidated screen also owns the auxiliary scorecard and assortment tabs.

Core sales stats still derive from:

`ProdajaZaglavlja -> ProdajaStavke -> Artikli -> Dobavljaci`

Supplier attribution is read from current `Artikli.IDDobavljac`. Detail and snapshot/reconciliation code show the same pattern.

### What is already strong

- canonical route/redirect and shared filter state;
- trust/provenance header;
- Supplier Decision RQ401-RQ405 are DONE for scorecard cache/effective period, detail, filter parity, requested/effective/observed periods and localization;
- Supplier Sales has focused fixtures/manual SQL and existing RQ373/RQ378-RQ380 ownership for visible-population, weighted-margin/runtime validation and comparable-pre/post correctness;
- cost snapshot reconciliation can compare legacy vs snapshot-aware margin behavior.

### What is not proven

- historical supplier attribution is not immutable on `ProdajaStavke`;
- changing an article's current supplier can reclassify older sales unless a separate historical source happens to reconstruct it;
- no continuously enforced independent oracle proves every Supplier row/total/unknown bucket/filter against raw sale lines;
- existing cost snapshot reconciliation is not a full sales-fact reconciliation;
- live/provider proof remains incomplete.

This makes Supplier Sales the highest-priority correctness surface in this audit.

## Shoe Type Sales: confirmed data flow and residual risk

Flow:

`ShoeTypeSalesStatsPage -> shoeTypeSalesStatsApi.ts -> /api/analytics/shoe-type-sales-stats -> sales headers/lines + Artikli + TipoviObuce + margin/nivelacija policies`

The client uses runtime schema validation, and RQ375-RQ377 are DONE.

### What is already strong

- weighted margin uses backend-owned covered-revenue logic instead of a simple row average;
- cost-source quality is separated;
- total pre/post values use the comparable article cohort while wider observed values remain explicit;
- detail route is server-authoritative and preserves recommendation/trust/provenance;
- canonical unknown identity is explicit;
- tests cover store/dataScope, unknown grouping, deterministic JSON, margin fields, pre/post fields and golden contract shape.

### What is not proven

- integration tests are environment gated; evidence explicitly records that live PostgreSQL/provider proof was not run;
- a passing method that returns early when the integration profile is disabled is not proof that DB assertions executed;
- `IDTipObuce` is read from current `Artikli`, so historical sales can be retroactively reclassified when the article master changes;
- golden snapshots prove stability of a known fixture/contract, not equality with authoritative production facts;
- no independent raw-fact oracle currently proves the whole response.

Shoe Type is therefore substantially better protected than before, but still not at “mathematically proven against source data” level.

## Other Operacije surfaces

### Daily Sales

Flow is explicit through `DailySalesStatsPage -> /api/analytics/daily-sales -> DailySalesStatsService -> sales facts`. Historical forensic work fixed import duplicate risks and added duplicate warnings. RQ381 delivered signed quantity/revenue semantics. This is meaningful proof, but not a cross-screen/live reconciliation guarantee.

### Pre/Post nivelacije

Uses `vendorSalesNivelacijaApi`, `/api/analytics/vendor-sales-nivelacija`, nivelacija split policy and relevant materialized/live sources. RQ385-RQ387 are DONE for request scope/cache lineage, cohort/denominator semantics and runtime payload/error safety. Live raw-source parity is still a separate proof concern.

### Color Sales

Uses sales facts + article color + nivelacija context. RQ392-RQ400 are DONE for signed sales, weighted margin/cost quality, comparable cohorts, runtime validation, cache/freshness, detail identity/provenance and authoritative decision score. It is locally well protected, but cross-screen raw-total reconciliation is still useful.

### Pre-Nivelacija priorities

Uses inventory/article state plus bounded sales history and scoring rules. RQ388-RQ391 are DONE for population/KPI parity, bounded scoring window, signed-sales evidence and runtime schema/error safety. The remaining risk is mainly cross-surface/source drift rather than the previously identified local contract defects.

### Inventory

Inventory mixes current stock snapshot semantics with sales-derived signals/forecast/rebalance data. Existing audit work identified period/dataScope consistency and alert-count issues in RQ371/RQ372. Inventory should not be forced to reconcile to sales totals, but each stock/signal panel still needs an explicit source, period, freshness and internal invariant.

### Supplier assortment

The menu compatibility route lands in the canonical Supplier assortment tab. It inherits Supplier/Shoe Type source lineage and is therefore affected by the same historical dimension-attribution problem unless the backend source is proven independent.

## Cross-cutting proof gap

Several pages can be individually correct according to their own implementation and still disagree because of:

- mutable current-master dimensions applied to history;
- different `dataScope` interpretation (article-origin vs sale-header origin);
- date boundary/timezone differences;
- unknown/excluded rows;
- signed-return handling;
- top-N/visible-row denominators vs full population;
- cost-source fallback;
- stale cache/materialized view;
- import/refresh invalidation;
- integration tests that were not actually executed against PostgreSQL.

Correctness therefore requires an independent oracle plus cross-screen invariants, not more UI snapshots.

## Queue integration and deconfliction

A concurrent canonical Operacije audit landed on `main` during this analysis and already created:

- `RQ406` — prevent Supplier Footwear dominant-type/type-share/elasticity metrics from being presented as authoritative when `articleStats` is truncated;
- `RQ407` — deterministic expected-output proof pack/reconciliation for all eight Operacije routes, including explicit handling of integration tests that did not execute.

Those canonical owners are preserved. The separate addendum `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_OPERATIONS_ACCURACY_ADDENDUM.md` now contains only new non-duplicative follow-ups:

- `RQ411` P0 — immutable/provenance-bearing supplier and shoe-type attribution for historical sale lines;
- `RQ412` P0 — implementation-independent raw-fact oracle for Supplier/Shoe Type, extending rather than duplicating RQ407;
- `RQ413` P1 — continuous drift/cache/freshness reconciliation guardrails.

All are `WAITING`; none was claimed or promoted.

## Validation performed

Repository/source inspection only:

- current Operacije navigation/routes;
- Supplier canonical/redirect surface;
- Supplier/Shoe Type API clients and backend source joins;
- focused Supplier/Shoe Type integration tests and evidence;
- snapshot reconciliation owners;
- Operations audit/trust/lineage documents;
- current RQ ownership around RQ371-RQ405;
- prompt-queue protocol.

## Validation not run

- no repository checkout/runtime command execution was available in this connector-only audit;
- no `node scripts/check-prompt-queues.mjs`;
- no backend/frontend test suite;
- no live PostgreSQL/Neon/API/browser proof;
- no production cache/import replay.

These are intentionally converted into executable queue acceptance work instead of being claimed as already proven.

## Delivery

- Canonical concurrent Operacije queue/audit owner: `7a3330044e610c133ed99634e2a7017e2da31e91` plus synchronized evidence follow-ups.
- Initial addendum commit: `2f1d19f0dcabc4e6fc6debe05adbb1f270b7d8dd`.
- Deconflicted addendum commit: `6917fb09dafb6c70b30a520cbcf9cc1a5c792013`.
- Roadmap synchronization is recorded separately on `main`.

## Residual risk

Until RQ411/RQ412 are implemented, Supplier/Shoe Type historical dimension classification can remain semantically unstable or current-master-based, and their numerical truth is not independently reconciled to raw facts. Canonical RQ407 owns the deterministic eight-screen proof and must explicitly distinguish executed database proof from tests that were skipped/not run. RQ413 is the durability layer after those proofs.
