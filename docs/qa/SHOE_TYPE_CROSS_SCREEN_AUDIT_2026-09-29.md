# Prodaja po tipu obuće — cross-screen accuracy, semantics and product-value audit

Date: 2026-09-29  
Repository: `ivanjovicic/Trendplus`  
Target: current `main`  
Primary route: `/analytics/shoe-type-sales-stats`

## Executive outcome

`Prodaja po tipu obuće` should **remain a first-class supporting analytics screen**. It answers a business question that Supplier, Daily Sales and Pre/Post do not answer directly: which footwear categories carry revenue, units, gross margin contribution and period growth, and how much each category depends on markdown/nivelacija evidence.

The screen is now materially stronger after `RQ494`-`RQ497`:
- certified sales `dataScope` follows sale-header origin;
- DUG/KOREKCIJA are excluded while signed retail returns remain;
- Supplier/Shoe Type snapshot cost is resolved at exact sale-line grain;
- Shoe Type signed net-sales share is visible and matches the share used by recommendations;
- negative previous-period revenue is no longer labelled “Novo”;
- arbitrary table sorts no longer create fake business rank badges;
- Daily shift analytics now declares business-time/timestamp basis.

The remaining issues are mostly cross-screen consistency, filtering state, sorting of unavailable values, information hierarchy and one conditional financial-parity risk.

## Surfaces compared

| Surface | Main question | Correct role | Overlap with Shoe Type |
|---|---|---|---|
| Supplier Pregled | Which supplier deserves focus? | canonical supplier decision | Same certified sales population before grouping, but different dimension and final decision owner |
| Shoe Type | Which footwear categories carry sales/margin/growth? | supporting category analysis | Primary subject of this audit |
| Color | Which colors carry sales/margin/growth? | supporting attribute breakdown | Closest structural duplicate of Shoe Type |
| Daily Sales | What happened by day/shift/supplier? | operational/forensic monitoring | Same base sales population, different time grain |
| Pre/Post Nivelacija | What changed around price events? | event/cohort impact analysis | Shoe Type embeds one pre/post signal but is not a replacement for causal/event detail |
| Supplier Asortiman | Which footwear types explain one supplier’s result? | supplier-specific drilldown | Same footwear vocabulary but supplier-conditioned comparable cohort |
| Supplier aliases in Operacije | Compatibility routes | redirect only | Navigation duplication, not a distinct analytics owner |

## Cross-screen numeric contract that should hold

For the same period, store and `dataScope`:

1. Certified retail-sales **total revenue and total quantity before dimensional grouping** should reconcile across Supplier, Shoe Type, Color and Daily when they claim the same base population.
2. Shoe Type row revenue/quantity should sum to the authoritative Shoe Type totals including the explicit unknown bucket.
3. Supplier and Shoe Type currently share the exact snapshot-aware cost policy when snapshot mode is enabled; historical line cost remains first, exact snapshot second, product fallback later, unavailable last.
4. Pre/Post metrics are a **different comparable price-event cohort** and must not be expected to equal ordinary period sales.
5. Signed retail returns can make a dimensional net-sales share negative or above 100% when the total net denominator stays positive. The basis must be visible if that share influences recommendation.
6. Missing/unavailable denominators are not zero and must remain unavailable in table, sort, detail and export.

`RQ448` remains the owner of deployed DB -> API -> browser -> detail -> export reconciliation. This audit does not duplicate that certification lane.

## What is now correct on Shoe Type

- Revenue, quantity and sale population use the hardened certified retail contract.
- Store filter is applied on the sale header.
- Sale-time Shoe Type identity is preserved.
- Whole-day date ranges are half-open.
- Previous-only positive-baseline Shoe Types can remain visible with truthful -100% PoP.
- Cost/margin uses the exact sale-line snapshot path when enabled.
- Signed `sharePct` carries basis/numerator/denominator metadata.
- Negative/>100 signed share is not silently hidden while recommendation consumes another value.
- Ranking badge appears only for descending revenue sort.
- Pre/post impact is explicitly distinguished from PoP trend.
- Backend recommendation remains authoritative; frontend does not reconstruct a local score.
- Trust header consumes backend `meta` when available.

## Residual findings

### F1 — P1: Shoe Type and Color use different share semantics for the same signed retail-sales concept

Shoe Type after `RQ496` exposes the mathematically valid signed net-sales share and recommendation consumes that same basis.

Color still uses `ColorSignedEvidencePolicy.ResolveNonNegativePercentage`:
- negative numerator -> `null`;
- ratio >100% -> `null`;
- recommendation then receives `sharePct ?? 0`.

Therefore a return-heavy category can be visible as a negative/>100 share on Shoe Type while the analogous Color row becomes `N/A` and contributes a zero share input to recommendation. This was intentional under older `RQ392`, so it must be reconciled explicitly rather than silently changed.

Owner: **RQ501**.

### F2 — P1: store dropdown can belong to the previous dataScope while the data query already uses the new dataScope

Shoe Type and Color listen to `trendplus:data-scope-changed` and immediately change the analytics request scope, but their store-list effect calls `getStores(true)` and depends only on `storesReloadNonce`.

The store API contract accepts/uses `dataScope` and has a focused test proving `getStores(true, "imported")` sends the scoped request. The Supplier consolidated surface is stronger: it calls `getStores(true, canonicalFilters.dataScope)` and reloads on scope change.

Risk:
- dropdown options can remain from the old scope;
- a selected store that is not valid for the new scope can stay selected;
- the analytics result is new-scope while the filter vocabulary is stale;
- duplicate store names are not disambiguated on Shoe Type/Color as they are on Supplier.

The same family should be checked on Daily and Pre/Post before fixing only two pages.

Owner: **RQ502**.

### F3 — P2: unavailable values are sorted through magic sentinels on Shoe Type and Color

Shoe Type uses values such as:
- `totalCost ?? -1`;
- `marginPct ?? -Infinity`;
- `popRevenueChangePct ?? -9999`;
- `prePostNivelacijaRevenueImpactPct ?? -9999`.

Color uses equivalent `-1` / `-9999` sentinels.

This mixes the “not measured” state with legitimate signed numeric values. With ascending/descending sorts, unavailable rows can appear as extreme business values rather than a separate state. Signed returns make sentinel sorting especially unsafe.

Owner: **RQ503**.

### F4 — P2: Shoe Type information hierarchy is too wide and promotes diagnostics before identity

The main table starts with **“Pokriće artikala %” before “Tip obuće”** and has 12 visible columns. Coverage diagnostics, total cost, two trends and recommendation all compete at the same level.

For normal decision use, the first columns should be:
1. Tip obuće;
2. Promet;
3. Količina;
4. Neto udeo;
5. Maržni doprinos;
6. Marža + cost quality;
7. PoP;
8. Pre/Post impact + comparable coverage;
9. Preporuka;
10. Detalj.

“Udeo artikala sa nivelacijom”, raw total acquisition cost and deeper cost-source diagnostics remain useful but belong in detail/export or optional columns.

A related copy defect remains after the new cost contract: Shoe Type tooltips describe cost as “istorijski ili procenjeni”, but the actual policy now has a distinct exact **snapshot** source. The UI should say historical / exact snapshot / product fallback / unavailable.

Owner: **RQ504**.

### F5 — P2: Color and Shoe Type are both supporting breakdowns but the product framing is inconsistent

Current architecture documentation classifies both as L2 supporting/analysis surfaces. Shoe Type trust header uses `mode="signal"`; Color uses `mode="recommendation"` and the copy says it supports deciding which colors to increase in purchasing.

Color also exposes “Skor odluke (0–100)” in detail even though `docs/ANALYTICS_EXECUTION_PLAN.md` still says decisionScore is deprecated as a user-visible metric. `RQ400` intentionally made Color decisionScore backend-owned and visible, so the documentation and product role now contradict each other.

This is not a numeric bug by itself, but it makes two nearly identical attribute screens communicate different authority.

Owner: **RQ505**.

### F6 — P2 conditional financial-parity risk: Color remains non-snapshot while Supplier/Shoe Type can be snapshot-aware

Repository default is currently `Analytics:UseSnapshotCost=false`, so this is not classified as a current production numeric failure.

However, when snapshot mode is enabled:
- Supplier/Shoe Type use exact sale-line snapshot cost before product fallback;
- Color intentionally remains on its non-snapshot cost path;
- same underlying sale facts can therefore produce different gross-margin contribution and margin % by dimension if historical cost is missing and snapshot differs from current product fallback.

The old audit called this latent and phase-2. After `RQ495`, the exact snapshot contract is stronger, so enabling the flag without a Color guard would make cross-dimensional margin comparison unsafe.

Owner: **RQ506**.

### F7 — P2 product/navigation redundancy: compatibility Supplier aliases still occupy Operacije navigation

Operacije currently shows:
- “Prodaja po dobavljačima” -> compatibility redirect to canonical Supplier Pregled;
- “Dobavljači i tipovi obuće” -> compatibility redirect to Supplier Asortiman.

These are not separate analytics surfaces. They duplicate navigation entries for the same canonical Supplier route.

Shoe Type and Color themselves are **not redundant**:
- Shoe Type is a meaningful merchandising/category decision layer;
- Color is a lower-level assortment attribute and should remain available, but visually/product-wise as a supporting breakdown rather than a second canonical decision hub.

Owner: **RQ507**.

### F8 — P2 documentation drift can misdirect future agents

`docs/ANALYTICS_EXECUTION_PLAN.md` still contains historical claims such as:
- Shoe Type using different local BOOST/KEEP thresholds;
- Shoe Type/Color frontend fallback scoring;
- SupplierFootwear local scoring;
- decisionScore treated globally as deprecated user-visible even though Color `RQ400` intentionally exposes an authoritative backend score.

Several of those statements no longer describe current code after the September hardening. Since agents use this document for architecture decisions, stale assertions can cause valid fixes to be “re-fixed” or current contracts to be reverted.

Owner: **RQ508**.

## Which screens/data are redundant?

### Keep

**Prodaja po tipu obuće — keep.** High-value global category view.

**Prodaja po boji — keep, but demote.** Useful merchandising drilldown; lower decision value than Shoe Type and strong structural overlap.

**Daily Sales — keep.** Unique time/shift operational evidence.

**Pre/Post Nivelacija — keep.** Unique event/cohort analysis; do not merge its totals into ordinary sales screens.

**Supplier Asortiman — keep inside Supplier.** It answers supplier-conditioned type-mix questions that global Shoe Type does not.

### Remove from primary navigation, preserve redirects

- “Prodaja po dobavljačima” alias;
- “Dobavljači i tipovi obuće” alias.

They are compatibility routes, not unique screens.

## Most useful Shoe Type information

### Primary

1. Promet;
2. Količina;
3. signed net-sales share / concentration with denominator basis;
4. maržni doprinos;
5. weighted margin plus cost-quality/coverage;
6. PoP revenue trend with previous-period baseline state;
7. recommendation/status reason with confidence/reliability when actionable.

### Secondary but important

8. pre/post nivelacija impact **together with comparable revenue coverage**;
9. comparable article count;
10. article count / share of articles with nivelacija;
11. cost source breakdown: historical / exact snapshot / product fallback / unavailable.

### Detail/export rather than headline

- raw total cost;
- observed pre/post values that are not the authoritative comparable cohort;
- every individual cost-coverage component when a compact quality tier already summarizes them;
- technical reason codes unless expanded.

## Recommended product hierarchy

- Supplier: canonical supplier decision.
- Inventory: canonical replenishment decision.
- Pre-Nivelacija Priorities: canonical pre-markdown action queue.
- Shoe Type: primary supporting merchandising/category analysis.
- Color: secondary attribute breakdown.
- Pre/Post: event-effect analysis.
- Daily: operational/forensic time analysis.
- Supplier aliases: redirect-only, not sidebar peers.

## Queue mapping

- RQ501 — Shoe Type/Color signed-share and recommendation-basis parity.
- RQ502 — scope-aware store option lists across Operations pages.
- RQ503 — null/unavailable sorting without numeric sentinels.
- RQ504 — Shoe Type information hierarchy and cost-source vocabulary.
- RQ505 — Color/Shoe Type role and decision-score authority consistency.
- RQ506 — conditional Color snapshot-cost parity/guard.
- RQ507 — Operations navigation de-duplication while preserving compatibility redirects.
- RQ508 — reconcile stale analytics execution plan with delivered contracts.

## Validation / limits

- Source, current queue/addenda, recent run evidence and architecture docs were inspected on current main.
- No production database mutation or runtime business logic was changed by this audit.
- The current repo default keeps snapshot cost disabled; no claim is made that production overrides that flag.
- Deployed authenticated browser/render/export proof remains owned by RQ448 and is not replaced by this source audit.
