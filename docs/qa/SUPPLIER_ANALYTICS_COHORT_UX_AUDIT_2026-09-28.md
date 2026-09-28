# Supplier analytics cohort, trust and information-hierarchy audit

Date: 2026-09-28
Repository: `ivanjovicic/Trendplus`
Target: current `main`
Queue outcome: new residual prompts `RQ498`-`RQ500`; no current READY pointer change

## Scope

This pass re-audits the canonical Supplier experience after the same-day Supplier deliveries (`RQ461`, `RQ462`, `RQ466`, `RQ467`, `RQ468`, `RQ483`, `RQ484`, `RQ486`) and after the new Operations accuracy findings `RQ494`/`RQ495`.

Reviewed:

- `SupplierConsolidatedPage.tsx`
- `SupplierSalesStatsPage.tsx` (Pregled)
- `SupplierDecisionHubPage.tsx` + `supplierDecisionMargin.ts` (Skorkarta)
- `SupplierFootwearAnalyticsPage.tsx` (Asortiman)
- Supplier API/service DTOs
- current Supplier queue owners and live/source audits from 2026-09-28

This is a source/contract audit. No product calculation was changed here.

## Existing higher-priority correctness owners: do not duplicate

The following confirmed problems already have canonical owners:

| Problem | Existing owner |
|---|---|
| Supplier overview live 503 must remain distinct from valid empty data | `RQ474` |
| Scorecard/Assortment semantic schema/readiness is unavailable in live evidence | `RQ475` |
| Supplier raw/API/display/recommendation/export share denominator disagreement | `RQ476` |
| Certified retail-sales `dataScope` must follow sale-header origin, not current article origin | `RQ494` |
| Supplier/Shoe Type snapshot cost is wrongly reusable at article grain instead of exact sale-line grain | `RQ495` |
| Historical supplier identity and DUG/KOREKCIJA population | delivered `RQ441` / `RQ456` |
| Supplier recommendation gate policy and unknown-supplier thresholds | delivered `RQ484` |
| Supplier shell stale-state/date/store/sort/rank hygiene | delivered `RQ486` |

These owners are more important than presentation work below because they can change the population, cost/margin or availability of the numbers.

## Cross-screen metric comparison

| Surface | Real population/question | Current headline data | Audit verdict |
|---|---|---|---|
| Pregled | certified period retail sales by sale-time supplier | revenue, units, covered cost/margin, weighted margin, top-5 positive-revenue concentration, PoP, final recommendation | Correct place for the final supplier decision. Keep as the canonical business overview. |
| Skorkarta | scorecard subset centered on articles with first markdown / scorecard evidence | scorecard revenue, top-5 share, estimated margin contribution, capital at risk, full-price-share delta, ranking signal | Valuable supporting signal, but generic revenue/margin/concentration labels look comparable to Pregled even though the cohort and metric basis differ. |
| Asortiman | vendor-sales-nivelacija comparable pre/post cohort around price events | comparable post revenue, top-5 supplier share, pre/post delta/growth, dominant footwear type, type distribution, elasticity and article coverage | Useful explanatory layer, but “Ukupan promet” is not ordinary period sales; it is comparable post-window revenue. Generic supplier concentration/growth repeats information better owned by Pregled. |

## Confirmed residual findings

### F1 — same-looking KPI names describe different populations

The three tabs intentionally answer different questions, but the visible labels do not carry the cohort in the metric name:

- Pregled “Ukupan promet” is certified period sales.
- Skorkarta “Ukupan prihod” is scorecard revenue, not all supplier sales.
- Asortiman “Ukupan promet” is `postRevenue` from the comparable pre/post nivelacija cohort.
- “Udeo top 5 dobavljača” appears on multiple tabs with different populations.
- “Maržni doprinos” on Skorkarta is explicitly defined as `full-price revenue × pre-markdown margin` estimate, not the same covered retail-sales margin contribution shown in Pregled.

Descriptions/tooltips help, but a user comparing cards can still infer false parity.

Owner: **RQ498**.

### F2 — Scorecard “Trend pune cene” is not a time trend, and percentage-point deltas are displayed as percent-like values

For each scorecard row, `qualityTrendPct` is computed from the contemporaneous difference between `fullPriceRevenueShare` and `markdownRevenueShare`. That is a composition gap, not a period-over-period trend.

Separately, the summary “Promena udela pune cene” is a true current-vs-previous share delta and therefore a **percentage-point** metric. The current UI formats it through a percent formatter and does not make the pp unit explicit.

`RQ459` fixed the underlying previous-period formula and aggregate parity; it did not remove this remaining naming/unit ambiguity.

Owner: **RQ498**.

### F3 — central Supplier trust shell has two provenance/severity defects

Current `SupplierConsolidatedPage.tsx`:

- has labels for `good`, `warning`, `insufficient_data`, `error`, `unknown`, but no `critical`; a child payload with `dataQualityStatus="critical"` therefore falls back to “Pouzdanost nije potvrđena” and the tone logic does not classify it as critical;
- uses `"Materijalizovani prikaz skorkarte dobavljača"` as the fallback data source for every active tab while the tab-specific trust payload is absent/pending. Pregled and Asortiman therefore can temporarily display a source that belongs to Skorkarta;
- treats “payload not arrived yet” similarly to “recommendation not allowed”, rather than an explicit pending trust state;
- describes Skorkarta everywhere as a supporting signal/final-decision helper, while the trust-header mode can still become `recommendation` when the scorecard gate is allowed. The role needs one consistent user-facing meaning without changing backend recommendation gating.

Owner: **RQ499**.

### F4 — unique scorecard evidence is under-promoted while generic Supplier KPIs are repeated

The scorecard API already exposes `fullPriceRevenueShare`, `fullPriceSellthrough`, `markdownRevenueShare`, `preMarkdownMarginPct` and `capitalAtRisk`. Yet headline cards spend space on generic total revenue, top-5 concentration and estimated margin contribution — concepts already central in Pregled.

For the user’s decision, the more distinctive scorecard questions are:

- how much revenue survives at full price;
- how dependent sales are on markdown;
- sell-through before/without markdown pressure;
- pre-markdown margin;
- capital at risk;
- change in full-price share;
- scorecard recommendation reason/confidence/quality.

Owner: **RQ500**.

### F5 — unique assortment evidence is under-promoted while generic Supplier concentration/growth is repeated

Asortiman’s unique value is not “another Supplier overview”; it is the structure of the comparable price-event cohort:

- dominant footwear type and its share;
- full type distribution/concentration;
- comparable cohort/data coverage;
- active articles versus total articles;
- type elasticity/response;
- pre/post revenue and quantity behavior with explicit comparability.

The current headline repeats total post revenue, top-5 Supplier share and generic growth. Those should be context, not the primary information hierarchy.

Owner: **RQ500**.

## What should remain

- Keep one canonical route: `/analytics/supplier`.
- Keep three tabs because they answer genuinely different questions.
- Keep Pregled as the only final business-decision surface.
- Keep Skorkarta as a supporting markdown/quality/risk signal.
- Keep Asortiman as the explanatory assortment/pre-post layer.
- Keep legacy routes only as compatibility redirects, not as separate decision owners.
- Keep detailed/export values even when they are demoted from headline cards.

## Recommended information hierarchy

### Pregled — “Da li je dobavljač poslovno dobar za fokus?”

Primary:
1. period revenue and units;
2. margin contribution with cost coverage;
3. weighted margin;
4. positive-revenue concentration/share with denominator;
5. PoP change with baseline state;
6. final recommendation, reason, confidence/reliability and quality.

Secondary:
- leading footwear types;
- detailed cost-source/coverage diagnostics;
- expanded row detail.

### Skorkarta — “Zašto je rezultat dobar/loš i koliko zavisi od sniženja?”

Primary:
1. full-price revenue share;
2. markdown revenue share;
3. full-price sell-through;
4. pre-markdown margin;
5. capital at risk;
6. full-price share change in percentage points;
7. scorecard signal/reason.

Context only:
- scorecard-cohort revenue;
- scorecard-cohort concentration;
- estimated full-price/pre-markdown margin contribution.

### Asortiman — “Koji tipovi nose rezultat i kako se ponašaju oko nivelacije?”

Primary:
1. dominant footwear type + share;
2. type distribution/concentration;
3. comparable cohort coverage;
4. active/total articles;
5. type elasticity;
6. pre/post revenue and quantity changes with explicit event-window semantics.

Context only:
- post-window total revenue;
- supplier top-5 share;
- generic cross-supplier growth.

## Queue registration

- `RQ498`: cross-tab cohort naming, units and metric-basis truth.
- `RQ499`: central Supplier trust/provenance severity and pending/source truth.
- `RQ500`: Supplier information hierarchy; promote unique Scorecard/Assortment metrics and demote duplicate generic KPIs.

All three are registered as **WAITING**. They must not outrank the population/cost/readiness owners `RQ474`, `RQ475`, `RQ476`, `RQ494`, `RQ495`.

## Validation

- Current-main source and current queue/addendum owners were inspected through the GitHub connector.
- No runtime calculation, database schema, production data or application code was changed.
- No browser pixel/click verification was performed in this pass.
