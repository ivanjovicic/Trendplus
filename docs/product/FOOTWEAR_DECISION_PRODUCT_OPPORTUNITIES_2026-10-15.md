# Trendplus — footwear decision opportunities, reality checks and staged product design (2026-10-15)

**Status:** Owner-requested product discovery / architecture proposals only. This is a **candidate catalog, not an implementation queue**, a new scoring contract, a pricing promise, a deployed certification or an instruction to start all items. Read with canonical [retail playbook](RETAIL_ANALYTICS_PRODUCT_PLAYBOOK_2026-10-14.md), [analytics-first scope](ANALYTICS_FIRST_SCOPE_2026-10-14.md), [commercial feasibility](RETAIL_PROFITGUARD_MARKET_FEASIBILITY_2026-10-15.md), [business milestones](../roadmaps/BUSINESS_ROADMAP.md) and exact owner queues. Code/tests override older plans for implementation facts. Investigation base: GitHub main dd0cb01da85396daf016faf8f3eaff72b878a16e, 2026-10-15; no new provider/db access or actual operator interview. Live and business claims remain NOT VERIFIED.

## 1. Opportunity hypothesis: move from KPI summaries to the *next correct merchandising choice*

The strongest possible future differentiator is an **explainable comparison of mutually exclusive actions** at footwear model × size × location × selling-window grain: *move an existing pair*, *replenish*, *hold*, *return to supplier*, *markdown* or *collect better evidence*. Current sales, supplier, inventory, pre/post, action ledger and RQ585 digest already cover parts of that workflow; **do not rebuild them**. There is currently no verified end-to-end source/decision/outcome evidence enabling a monetized “optimal action” claim.

Three independent questions must all pass before a recommendation: **(a) can the specific item/size/store be identified and its true availability observed, (b) can the relative monetary effect be evaluated with confirmed costs and explicit scenario assumptions, and (c) does the owner have authority to execute and verify the outcome?** If not, emit a read-only REVIEW / FIX_DATA candidate, not an estimated monetary “save”.

The small-data danger is large: local batch #23 proves 5,530 Access headers and 67,092 source lines reached facts, not valid customer baskets or complete demand by size; the dated reference July sample is 307 units and 1,561,120 RSD and not sufficient to calibrate sophisticated elasticity for many SKU×store×size cells. Historical stockout demand is **censored**, and even accurate observed sales are not the same as demand. Source: [local validation](../qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md) and [dated value audit](TRENDPLUS_PRODUCT_ANALYTICS_VALUE_AUDIT_2026-10-07.md).

## 2. Current code checked — important things that ALREADY exist

| Surface | Actual source checked | Accurate state, not an invented new feature |
| --- | --- | --- |
| Size curve / incomplete size runs | `Application/Analytics/Queries/GetInventorySizeCurve/GetInventorySizeCurveHandler.cs`, `Klijent/clientapp/src/components/inventory/SizeCurveVisualization.tsx` | SQL-backed read of `analytics_size_curve_snapshot` with share/deviation/missing-core/dead-size/broken-run and null-safe evidence. But `SnapshotFreshnessUtc=null`, status UNKNOWN and unavailable table is handled explicitly. **Not evidence of accurate actual size curve for customer data.** |
| Size curve **not** a supplier-wide assortment profile | `Application/Analytics/SupplierAssortmentSizeCurveEvidenceContract.cs` | `SupplierFootwearSizeCurveAvailable=false`; missing supplier × footwear-type × size sold/received/on-hand source. Do not aggregate a SKU/store share into supplier “size profitability” by implication. |
| Transfer/rebalancing and transfer business process | `Klijent/clientapp/src/components/inventory/RebalancingTable.tsx`, `Api/Endpoints/TransferEndpoints.cs`, `Api/Services/TransferService.cs` | Proposals/display and real draft→confirm→complete/cancel transfer workflow exist. **Not** proof that snapshot suggestions are fresh, economically optimized, or supported by authoritative size×store stock. Do not create a second transfer feature. |
| Stock, margin and recommendation gates | `Api/Endpoints/InventoryEndpoints.cs`, `Application/Analytics/InventoryActionDecisionPolicy.cs`, `Application/Analytics/AnalyticsMarginPolicy.cs` | Replenish/markdown/clearance and signal-window policies already exist; true cost/fallback/unknown branches are differentiated. Do not rewrite or bypass backend authority. |
| Returns to **supplier**, distinct from shopper returns | `Domain/Model/Povracaj/PovracajZaglavlje.cs` and existing `ReturnFact` mapping | Header has supplier, reason, status, amount and approval timestamps. This **does not** prove contractual return rights/deadlines, actual supplier credit receipt or customer fit-related returns. |
| Observed daily inventory | `docs/architecture/OBSERVED_INVENTORY_DAILY_SNAPSHOT.md`; `.ai/runs/2026-10-11-RQ610-evidence.md` | Opt-in worker and observed company-grain snapshot foundation exist, disabled by default. No proven per-store stock generation, historical stock-as-of, or retroactive source-day coverage. |
| Price change and action/outcome | Pre/Post endpoints, `Api/Endpoints/AnalyticsActionsEndpoints.cs`, RQ585 digest | Historical descriptive cohorts and action lifecycle exist, but zero qualifying causal shadow-control pairs in dated RQ612, operator session NOT RUN, realized owner outcome not proven. |
| Connectors/onboarding | `docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md`, `Api/Services/DataSources/CanonicalSourceEntities.cs` | Read-only SQL Server discovery and checkpoint staging do not form a certified universal canonical import. Mapping preview currently catalogs Artikli + sale headers + lines, **not** invoice/PO/contract records. |

**Repo collision/readiness reminder:** PR #163 is still draft/open in the GitHub read on this date and addresses sale-header/source identity; it is a correctness gate for future money features, not a reason to attach speculative work to that PR. Open PR #175 is frontend test-oriented; don't modify or merge either through this product study. RQ666/RQ667 and RQ585 are DONE per their existing evidence. No new task ID or READY promotion follows from this document.

## 3. Ten promising feature products; classify source readiness BEFORE writing code

Effort is relative (`S` bounded existing-surface work, `M` added well-scoped source/logic, `L` new history/identity/economics), **not a delivery commitment**.

| ID | Decision / candidate | What it could do | Existing reuse | Source blocker / failure mode | Effort and phase |
| --- | --- | --- | --- | --- | --- |
| F1 | **One-page Weekly Decision Brief** | Three defensible actions, supporting evidence and named owner; after a week what was decided | RQ585 digest, Decision Pulse, Actions, Supplier & Inventory | Operator adoption, current source and cost gates; must not fabricate a new digest | **S / NOW observe, adapt existing flow only after real friction** |
| F2 | **Cost Integrity / Margin Detective** | Show historically confirmed cost vs estimate, flag suspicious source scale and documents blocking margin decisions | Cost provenance, Data Quality, RQ601 scripts and import lineage | 20 original calc lines, sale/source identity; false invoice certainty | **S-M / NOW bounded paid data audit; DO NOT automatically rescale** |
| F3 | **Supplier Negotiation Packet 2.0** | Trace known sales, covered gross-margin contribution, stock exposure and documented return candidates for one supplier | RQ595 negotiation text/report, supplier scorecard, Returns | Supplier-wide cover, terms/credits and authoritative age unavailable | **S first as evidence packet; M later for contract facts** |
| F4 | **Supplier Return Window Calendar** | Optional manual date/rights for unsold/damaged goods; prepare authorized return before deadline | Supplier returns workflow, season, stock, supplier attribution | Rights, credit-note confirmation and actual expiry **not in source**; manual contract required | **M / CONDITIONAL one supplier** |
| F5 | **Model/Size/Store Availability Audit** | Identify which entire footwear model has a broken run in a specific shop and where sizes might be available | Size-curve SQL/UI and store inventory views | No verified parent model/style-color grouping, per-store size stock or freshness; all UNKNOWN if missing | **L for reliable recommendation; S for explicit missing-data diagnostic** |
| F6 | **Transfer vs Reorder vs Markdown Decision Card** | Compare 3 alternatives for same model/size/store/time; show marginal economics + owner choice | Existing replenish/markdown/transfer APIs, Actions, stock/price facts | Per-location on-hand, receipt provenance, real cost, transport cost, lead time, stockout-censored demand | **L / LATER; no automatic write or universal 'optimal' claim** |
| F7 | **Seasonal Open-to-Buy / Don't Buy List** | Read-only buyer envelope: spend ceiling, committed orders, core/seasonal budget, models to skip | Existing sold/cost/supplier/season columns, inventory | Open PO, commitments, payable terms, actual budget, style identity **not proven** | **M manual budget scenario only; L automated** |
| F8 | **Next-Season Size-Run Review** | Compare past season sales and final leftover sizes; decide next buy's size mix and exceptions | Historical sale lines, item season/size, size-curve visualization | Cohort identity, per-size historical availability/receipts, minimum order packs, returns, censoring | **L / LATER, deterministic descriptive review before ML** |
| F9 | **Supplier Invoice/Contract Variance Audit** | Compare effective-dated price list to invoice lines; human-confirm currency, unit, discounts and credit notes | Cost/data-quality primitives and read-only import philosophy | New document/contract line model and authentic inputs; must be independent validation lane | **M-L / separate paid sample test, not retail queue now** |
| F10 | **Decision Rejection / Feedback Learning** | Explain why operators reject/override an action, count follow-through and blockers | Analytics Actions statuses/reasons, outcomes, digest | Real user behavior, role, timestamps, treatment confounders; no automated score learning | **S-M / after first observed operator session** |

Additional **conditional** hypotheses, not active features: returns-and-fit issue by model (customer return reason versus supplier return reason must first be sourced), multi-store localization of models by seasonal cohort, supplier exclusivity/concentration/consignment contract evidence, stock/shrinkage discrepancy reconciliation, and transparent buyer scenario simulation. Do not rank into READY absent sources.

## 4. Highest-upside deep concept: "the same pair, three possible actions"

A **decision entity** should be product/variant identity + verified location + source + observation time + considered selling window + decision owner. This is not equal to a raw `Artikli.Id` in every external ERP: validate style/color/size mapping, country size conventions, negative/store IDs and import namespace.

**Inputs and exclusions:**
- **Transfer:** observed available stock in source shop *after* safe minimum and reservations, target demand evidence, estimated freight/handling, arrival date and no stockout in source. A transfer fee without source/arrival evidence cannot justify action.
- **Reorder:** true supplier unit cost, pack/MOQ, lead-time evidence, budget/PO commitments, anticipated season end and the supplier's proven availability. No implicit zero MOQ or freight.
- **Markdown:** current dated selling price, verified cost/tax basis, comparable historical days, customer-approved price floor and mature post-window. Stock duration from latest snapshot is not historical OOS.
- **Hold/return to supplier:** contractual right, cutoff, recovery amount/credit note and opportunity cost. No presumed right to return unsold merchandise.

**Output must distinguish** `observed confirmed values`, `scenario based on owner assumptions`, `missing evidence`, `blocked recommendation`, and `action outcome`. An expected improvement is NOT realized savings. Do not rank alternatives until every option has a comparable unit and scope; showing just one eligible option is acceptable, including **FIX_DATA** as best next step.

**Comparison logic (proposal, NOT canonical financial metric/formula):** net incremental contribution = expected additional contribution from incremental units minus attributable incremental transfer/purchase/price-change costs and risks. If incremental units are unobserved because historical stockout periods are unknown, present scenario ranges or mark unavailable; never materialize exact `expected_saved_sales` as historical fact. A transport cost entered manually must be labelled owner assumption, not system fact.

**Best first increment:** show existing transfer/replenish/markdown facts side-by-side in *one already-used operator decision detail* with independent provenance; **no new 'optimal action' engine**. Require an actual example of two credible alternatives from a real retailer before implementation.

## 5. Perspectives earlier feature lists underweighted

1. **Wrong decision is worse than missing report.** A stale source or bad cost scale may turn a seemingly helpful discount/transfer into a real loss; current source integrity and PR #163 identity are commercial prerequisites, not just dev hygiene.
2. **Footwear model hierarchy vs individual SKU:** style→color→size→store is a distinct domain. Existing product-size string is not proof that records can be grouped into a single physical model. Model mapping/merge must be reviewed manually and reversible before the whole size-run concept.
3. **Censored demand and substitution:** zero observed sales can mean out of stock. A peer-reviewed footwear study found ~25% unmet-demand size substitution in *its own retailer sample*; do not transfer this figure to Trendplus and do not extrapolate modeled stockouts without observed availability. Study: https://doi.org/10.1287/msom.2023.0674
4. **Supplier return rights can dominate markdown decisions:** a 70%-credit return right can change the best action, but contracts/actual credit collection aren't in current source; verify one document before automating.
5. **Ownership/agency:** stock clerk sees units, buyer controls next orders, owner controls cash/discount policy. Avoid one universal action CTA without permissions, named approver and recorded execution.
6. **Cash is not gross margin:** capital tied in inventory is not “realized loss”, prepay/consignment/credit changes cash impact and inventory valuation. Without payable terms, a cash release estimate is only a scenario.
7. **Data quality as a product:** a paid verified source-reconciliation/audit may have more defensible value than another dashboard; only one supplier sample and customer acceptance can validate the revenue hypothesis.
8. **Season and substitution:** historical July sandal sales do not by themselves set winter boots reorder. Show explicit season, new vs core model, days carried, store profile and active availability. Sparse SKU cells require sample gates before advanced prediction.
9. **Measurement design:** record *before action* what decision was available, who approved it, stock and cost evidence, exact action and comparison period. Don't make a retrospective graph causal ROI. RQ592 remains canonical.
10. **Privacy/competitor access:** source files, confidential costs/discounts and supplier terms must not be uploaded to public GitHub, shared across customer databases or exposed in illustrative screenshots without authorization.
11. **Incumbent distribution advantage:** Shopify already has transfers/shipments reporting, RELEX plans seasonal price/assortment, and Increff markets size-level replenishment. Being first is not the moat; simple credible local workflows + source integrations are the hypothesis.
12. **Perverse incentives and explanation fatigue:** users can game confidence thresholds or ignore 50 alerts. Show at most 3–5 independently eligible decision opportunities; measure proportion seen, investigated, accepted and actually executed. No daily nagging while source freshness is blocked.
13. **Execution friction as key product metric:** require measured time from opening report to a valid decision and concrete steps saved against ERP/Excel; a technically perfect signal that's never acted on has uncertain value.
14. **Rival mental model:** the retailer may already know which sandals aren't moving. The product must reveal *what they did not know* (wrong cost scale, blocked size run, missed return deadline, wrong store allocation), or it has no independent pricing power.

## 6. Practical prioritization, not a broad implementation backlog

| Sequence | Product / engineering experiment | Smallest real evidence | Pass / fail / next condition |
| --- | --- | --- | --- |
| **P0 existing safety** | PR #163 source/header identity; STAB16 provider fresh-source audit; RQ601 original costs; RQ610 store-grain stock | Exact canonical owners and source evidence | Financial recommendations withheld until corresponding proof; **do not create duplicate RQ tasks** |
| **V0 no-feature pilot** | Observe existing weekly owner decision flow with one actual footwear operator | Real 20-min card, dated source/export, comparison with POS/Excel | If no useful distinct task, STOP new dashboard work |
| **V1 fast bounded value** | Known-vs-estimated margin explanation, 3 supplier facts, dated slow-stock caveats and action disposition | One reconciled customer audit and owner approval | Customer confirms *new useful decision*, ideally pays for bounded report |
| **V2 small source-backed product choice** | Evaluate **supplier return-window** if a real supplier agreement exists; otherwise **cost anomaly packet** | One authentic redacted contract/invoice and sample stock or cost records | Repeated validated cases and willingness to pay, else abandon |
| **V3 size-run/store feasibility** | Prove style-color-size mapping and current per-store quantities before any new algorithm | A real sample covering 10–20 model families across at least 2 stores with explicit unknown rows | Only then decide whether existing size curve/rebalance can support reliable operator choices |
| **V4 one alternative comparison** | Side-by-side transfer/hold/markdown facts using current APIs, no automatic action | Two actual eligible alternatives with matching source and cost scope | An observed buyer understands reason and chooses or rejects |
| **V5 seasonal planning and true measurement** | Pre-registered next-season / markdown / transfer outcome on verified source | Complete observations over maturity window; signed approvals | Only move to forecasting/scoring on observed need and mature backtests |

Sample quantities above are **proposed discovery sample sizes**, not proven statistical adequacy. This plan creates **no new READY prompt, PR claim, runtime task or deadline**. Proposed work should be converted to a single owned prompt only when a concrete data source/customer proof and missing capability are confirmed.

## 7. Tests a real implementation would need

- **Identity:** same style/size SKU in two shops, old Access ID collision, nonmatching variant names/aliases, imported/existing scope, supplier-return location, future duplicate mapped parent.
- **Financial:** missing purchase cost vs numeric zero; tax/discount unknown; negative signed return; confirmed versus estimated inventory capital; disputed invoice line; wrong currency/pack and out-of-period price list.
- **Time:** observed inventory before/after import, date-only vs exact observation time, DST/Belgrade business day, incomplete coverage, stale price, supplier return deadline boundary.
- **Safety:** no supplier return assumed from a status value; no automatic price change, stock movement or purchase order; no transfer leaving source below safe minimum; no money impact on unknown demand.
- **UI/exports:** same filters/dataScope/source keys; evidence and status preserved; exception/empty/stale/unknown != zero; cards and export cannot silently promote a proposal to an executed action.
- **Commercial:** operator can perform same actual decision faster or with a new verified finding than incumbent; count paid engagements, repeated usage, genuine executed choices, and measured outcomes separately.

## 8. External research and calibration limits

- Peer-reviewed footwear size substitution study, published 2025 / 2026 issue: https://doi.org/10.1287/msom.2023.0674 — limited to its retailer/sample; not default Trendplus behavior.
- Increff footwear case descriptions (vendor-reported commercial evidence, **not independently verified Trendplus outcomes**): https://www.increff.com/case-studies/why-footwear-brands-choose-increff-for-inventory-planning-and-merchandising
- RELEX current merchandising/assortment/seasonal portfolio: https://www.relexsolutions.com/solutions-overview/
- Shopify inventory transfer/shipments product docs: https://help.shopify.com/en/manual/inventory-and-locations/inventory-transfers
- 2025 NRF returns study (US broad retail and online, **not Serbian footwear incidence**): https://nrf.com/research/2025-retail-returns-landscape — motivates separate *customer returns* investigation but supplies no local rate.

**Owner-level decision:** Keep core product narrowly centered on reliable footwear retail decisions. New best-feature bets are **(1) source integrity as a saleable audit, (2) a validated supplier-return rights calendar, (3) only after source proof, size-run-aware comparison of move/rebuy/discount, and (4) seasonal purchase discipline**. Build fewer, stronger decisions; no additional generic dashboards, experimental scoring or new RQ prompt wave.
