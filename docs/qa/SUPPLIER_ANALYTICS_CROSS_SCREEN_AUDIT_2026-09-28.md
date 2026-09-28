# Supplier analytics cross-screen audit

Date: 2026-09-28  
Repository: `ivanjovicic/Trendplus`  
Queue: `direct-user-request`  
Target: `main`

## Outcome

A repository-wide source/contract audit was completed for the canonical Supplier analytics surfaces, durable Supplier/Pilot reports, linked Product/Actions/Decision Pulse surfaces and the adjacent Supplier/Shoe Type/Color analytics screens.

The audit confirms that the current canonical queue already owns every confirmed finding from this pass:

- `RQ461`-`RQ468`: Supplier report and Pilot intake
- `RQ469`-`RQ476`: Product Decision and Supplier overview/scorecard/assortment
- `RQ477`-`RQ482`: Actions and Decision Pulse
- Existing owners `RQ441`, `RQ442`, `RQ443`, `RQ444`, `RQ456`, `RQ457`, `RQ458`, `RQ459`, `RQ243`, `RQ325` and earlier supplier/secondary-screen owners cover the already-delivered contracts.

No new prompt was registered. Re-registering PS01-PS18 or creating another supplier-screen owner would duplicate active ownership.

## Audit basis

Reviewed on current `main`:

- `docs/qa/PRODUCTS_SUPPLIER_LIVE_AUDIT_2026-09-28.md`
- `docs/qa/ACTIONS_DECISION_PULSE_SUPPLIER_SCORECARD_LIVE_AUDIT_2026-09-28.md`
- `.ai/runs/2026-09-28-supplier-report-pilot-intake-audit-evidence.md`
- `docs/ANALYTICS_EXECUTION_PLAN.md`
- `docs/Analytics/SECONDARY_ANALYTICS_SCREENS_AUDIT.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `AGENTS.md`, `.github/copilot-instructions.md`, `docs/ai/CODEX_TASK_CHECKLIST.md` and the canonical evidence/queue guidance

Live/source evidence already recorded in the referenced audit artifacts was not silently treated as a new implementation result. The deployed browser helper was unavailable, so pixel-level and click-flow conclusions remain unconfirmed.

## Screen inventory and ownership

| Surface | Canonical data question | Confirmed gap or trust risk | Owner |
|---|---|---|---|
| `/analytics/supplier?tab=overview` / legacy Supplier Sales aliases | Period sales, units, cost-covered margin and supplier concentration | Runtime 503 must remain distinct from empty; share denominator parity remains open | `RQ474`, `RQ476`; prior period/population owners `RQ441`-`RQ444`, `RQ456` |
| `/analytics/supplier?tab=scorecard` | Supplier decision signal and recommendation eligibility | Transport success can carry `MISSING_SCHEMA`; recovery/readiness must be actionable and fail closed | `RQ475`; KPI/report parity `RQ459` |
| `/analytics/supplier?tab=assortment` | Supplier/type pre/post assortment signal | Semantic contract can be unavailable; no recommendation may be inferred from empty arrays | `RQ475`; prior sale/identity semantics `RQ457` |
| `/analytics/supplier/report` | Durable supplier decision artifact | Renderer expects different section/header/meta names; requested/effective period, metric basis, score model, actions and copy drift | `RQ461`-`RQ465`, `RQ468` |
| `/analytics/reports/pilot-intake` | Pilot data readiness and safe recommendation gate | Durable page passes `report={null}`; backend scope/period/refresh truth and readiness semantics are inconsistent | `RQ462`, `RQ466`, `RQ467`, `RQ468` |
| `/analytics/products` | Product-level decision/actionability | Batch cap, dropped search, mixed visible/analyzed populations, hard-coded journal gate and margin-policy drift | `RQ469`-`RQ473` |
| `/analytics/actions` | Work queue and measured outcomes | List, global counts and outcome summary use different populations; not-measured rows contaminate legacy denominators; smoke fixtures are live | `RQ477`-`RQ479` |
| `/analytics/decision-pulse` | Cross-surface prioritized signals | Partial Supplier source can look like a trustworthy empty feed; page omits shared period/scope; Supplier links/codes lack provenance UX | `RQ480`-`RQ482` |
| Supplier/Shoe Type/Color secondary screens | Supporting breakdowns, not another Supplier owner | Existing trust, identity, return, cost and freshness contracts already have owners; no new Supplier queue item is justified | `RQ243`, `RQ325`, `RQ456`, `RQ457`, `RQ179` and prior completed owners |
| Legacy `AnalyticsDetails`, `AnalyticsDetailPage`, `InsightStudio` | Drill-down/exploratory analysis | Legacy framing/trust UX exists, but these are not a new Supplier ownership lane | Existing historical RQ owners; no new prompt |

## Metric contract comparison

| Contract | Required canonical rule | Current evidence / gap | Queue owner |
|---|---|---|---|
| Sales population | Same period, scope and sale-line population as Supplier overview when KPI names imply parity | Supplier report currently mixes markdown/pre-post windows with period sales; linked screens expose different cohorts without always naming them | `RQ463`, `RQ464`, `RQ471`, `RQ476` |
| Receipt population | Exclude `DUG` and `KOREKCIJA`; keep signed retail returns | Delivered for certified sales surfaces by `RQ456`; Supplier decision/pilot extensions remain explicitly owned by `RQ464`/`RQ467` | `RQ456`, `RQ464`, `RQ467` |
| Supplier identity | Prefer sale-time `SupplierIdAtSale`, not current master-data reassignment | Delivered in the Supplier sales path and recorded by `RQ441`; do not introduce a second attribution rule | `RQ441` |
| Store filter | Sales metrics use sale store `ProdajaZaglavlje.IDObjekat`; master-data metrics name their different scope | This distinction is part of the readiness/report decision contract | `RQ464`, `RQ467` |
| Cost/margin | Missing/non-positive cost is unavailable for margin, never zero; revenue/units remain included; expose coverage | `AnalyticsMarginPolicy` is canonical for Supplier sales, but Product/report paths still drift | `RQ464`, `RQ473` |
| Returns | Customer returns are signed retail sale lines; supplier-return facts are not customer return rate | Existing report evidence identified `povracaj_zaglavlje` misuse risk; no second return source should be introduced | `RQ464` |
| Recommendation | Backend owns one score/status model; fallback or missing evidence blocks recommendation | Supplier report MV/live paths and linked Product/Supplier surfaces can use different models or gates | `RQ464`, `RQ472`, `RQ475` |
| Period lineage | Requested, effective, observed and generated/refresh timestamps are separate | Historic/non-precomputed ranges and query-time refresh fallbacks were confirmed | `RQ463`, `RQ466`, `RQ467` |
| Failure/empty/partial | Error, successful empty, insufficient data and partial/fallback are distinct states | 503, `MISSING_SCHEMA`, `PULSE_PARTIAL` and the durable Pilot empty-state defects are confirmed | `RQ461`, `RQ462`, `RQ474`, `RQ475`, `RQ480` |
| Share denominator | Raw API, cards, recommendations and export must declare the same positive/negative/unknown policy | Supplier raw/display/Hub shares still differ | `RQ476` |
| Actionability | Blocked rows cannot create executable supplier/product actions; all-supplier view cannot invent a single supplier | Negotiation pack/fallback supplier and Product action-status defects are confirmed | `RQ465`, `RQ469`, `RQ471` |
| Export/report | Preserve period, scope, freshness, quality, methodology, warnings and unavailable values | Supplier report renderer/export metadata and Pilot durable report remain open | `RQ461`, `RQ462`, `RQ468` |

## Redundancy and product recommendations

- Keep `/analytics/supplier` as the canonical Supplier navigation surface with Overview, Scorecard and Assortment tabs.
- Keep legacy Supplier Sales and Supplier Footwear routes as compatibility redirects until their replacement contracts are fully proven; do not create a second active Supplier overview.
- Keep the durable Supplier report as a report/export artifact, not a fourth competing analytics dashboard.
- Keep Pilot intake as a readiness/report surface; it must not become a second Supplier scorecard.
- Keep Product Decision, Actions and Decision Pulse as adjacent decision surfaces, but expose their population, period, scope and source lineage when they link to Supplier evidence.
- Do not hide data gaps by merging scorecard/assortment failures into empty states or by rendering zero-valued KPIs.
- The most useful supplier information is: period sales/units, covered-margin contribution with cost coverage, sale-time supplier attribution, concentration/share with denominator, recommendation status/reason, freshness/quality and an explicit next action. Pre/post markdown metrics should remain a separate, clearly named block.
- Redundant or low-value data includes duplicate local scoring, repeated raw status codes, duplicate Supplier report/dashboard KPI blocks, unexplained global counts and decorative rows that only restate a section row count.

## Decisions carried forward

The owner decisions from the referenced conversation are recorded and remain the implementation contract:

- `RQ463`: fail closed for a requested period that is not exactly covered by a precomputed rolling window; show the requested dates and block recommendation, while exact period sales may still be calculated from base sales facts.
- `RQ464`: align Supplier report period sales/units with Supplier overview; exclude `DUG/KOREKCIJA); exclude unknown cost only from margin; use signed retail returns for customer returns; use sale-store; converge scoring.
- `RQ465`: show negotiation pack and final advice only for exactly one selected supplier; never choose a top-revenue supplier as a silent fallback.
- `RQ467`: no-sale/insufficient signal is not automatically a readiness hard gate; blocked recommendations count distinct articles; default period anchors to the latest observed imported business date, not today.

These are decisions for implementation acceptance, not permission to mark the WAITING prompts as DONE before focused proof and main verification.

## De-duplication map

| Historical proposal | Canonical owner | Action |
|---|---|---|
| PS01 | `RQ469`/linked Product owner | Do not re-register |
| PS02 | `RQ472` | Do not re-register |
| PS03 | `RQ473` | Do not re-register |
| PS05 | `RQ470` | Do not re-register |
| PS06 | `RQ474` | Do not re-register |
| PS08 | `RQ475` | Do not re-register; live `MISSING_SCHEMA` is confirmed |
| PS09 | `RQ471` | Do not re-register |
| PS11 | `RQ476` plus existing supplier concentration owners | Do not re-register |
| PS13 | Existing sale-time attribution owner `RQ441` | Do not re-register |
| Remaining PS proposals | Reviewed against `RQ441`-`RQ482` and secondary-screen owners | Register only if a future reproducer proves a new, non-overlapping contract |

## Validation and delivery

- Repository tree, current queue, roadmap, source audits and evidence artifacts were fetched from GitHub.
- Queue inspection confirmed `RQ461`-`RQ482` exist with the intended statuses and no new ID was required.
- Roadmap inspection confirmed the RQ program row points to the current READY set and the 2026-09-28 audit evidence.
- Existing live/source evidence was cross-checked against the screen/metric matrix above.
- Local build, test and guardrail commands: not run — this is a connector-only docs/governance audit and the current workspace has no Trendplus checkout.
- Browser visual smoke: not run — the native browser helper could not initialize during the referenced live audits.
- Production data mutation, schema migration and code changes: not performed.

## Delivery note

This file is a docs-only consolidation artifact. The GitHub connector write that adds it is the delivery event on `main`; the final task response records the exact commit SHA returned by GitHub and the fresh `main` verification. No queue item was claimed or falsely closed.

## Residual risk and next step

The next safe execution lane remains the existing `RQ461` READY prompt (with parallel-safe `RQ462` and `RQ466`), subject to the queue protocol and collision checks. `RQ463`, `RQ464` and `RQ467` remain product-decision/acceptance follow-ups, not duplicates. Live production availability/readiness and browser layout still require a runtime-enabled verification pass.
