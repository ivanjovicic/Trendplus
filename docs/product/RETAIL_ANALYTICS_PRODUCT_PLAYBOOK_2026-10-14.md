# Trendplus — Retail Analytics product and execution playbook

Status: **current product decision and working specification**, 2026-10-14.
Product owner scope: [ANALYTICS_FIRST_SCOPE_2026-10-14.md](ANALYTICS_FIRST_SCOPE_2026-10-14.md).
This file describes **why, for whom and how to prioritize**. It is **not** a task queue, a new formula registry or proof that deployed production is healthy. Code/tests and [ANALYTICS_BUSINESS_GLOSSARY](../ai/ANALYTICS_BUSINESS_GLOSSARY.md) define executable metric semantics. `MASTER_ROADMAP.md` and the named owning queues alone determine formal task status, claims and readiness.

## Commercial feasibility update — 2026-10-15

Read the [evidence-based ProfitGuard market/competition and unit-economics study](RETAIL_PROFITGUARD_MARKET_FEASIBILITY_2026-10-15.md) before treating product expansion, pricing, SAM or supplier invoice controls as ready work. **Decision:** first validate a bounded paid, dated, read-only retail audit and actual operator use of the *existing* digest/actions; subscription and invoice-mismatch add-on remain unproven alternatives. Public competitor price pages and broad trade-sector counts are not customer demand or footwear TAM. No new RQ, screen, scoring family, source connector or live/deployed claim is authorized by the study. RQ666/RQ667 and RQ585 are already DONE.

## Footwear-specific opportunity discovery — 2026-10-15

The [source-verified footwear decision opportunity catalog](FOOTWEAR_DECISION_PRODUCT_OPPORTUNITIES_2026-10-15.md) evaluates size-run/store decisions, transfer-versus-buy-versus-markdown comparisons, supplier-return rights, seasonal buying constraints and data-integrity revenue opportunities. **It is discovery, not a new execution queue or runtime scope authorization.** Existing size-curve, rebalance, transfer, weekly digest and Actions components are not to be rebuilt; strong recommendations remain blocked where model-family/store-level stock, cost, provenance, customer authority or observed outcomes are missing. Follow this playbook's existing Wave B/C gates and only promote a feature after real customer/source proof.

## Hidden-value / UX audit — 2026-10-15

Povezanost, hijerarhija i istinitost poruka postojećih ekrana, top 15 predloga i sprovedeni quick win-ovi: [TRENDPLUS_HIDDEN_VALUE_UX_AUDIT_2026-10-15](TRENDPLUS_HIDDEN_VALUE_UX_AUDIT_2026-10-15.md). Nema novih RQ ID-jeva ni READY statusa.

Nastavak 2026-10-15: Početna prikazuje poslednji potvrđeni period prodaje i vodi na šest poslovnih pitanja; Puls odluka je u glavnom meniju (detalji u §8 audita).

## 1. Product decision — small, credible and sellable

**Positioning:** footwear-retail decision support on top of a customer's existing sales/stock system, not a POS/ERP replacement, scraping service, generic BI toolkit or prediction platform. Start with one retailer and a repeatable source import. A separate deployment per customer is acceptable for the first pilot.

**Initial buyer hypothesis (not market validation):** owner/manager or purchasing lead of a small/mid-sized Serbian footwear retailer with one or several stores, Access/POS history, seasonal purchasing, supplier negotiations and excess stock. End users: owner, operations manager, buyer/merchandiser. Do not silently assume they are comfortable with statistics. Verify who actually makes price/stock decisions and who has authority to enter an action.

**Core promise (to be validated with customers):** each week answer *where money is made; where it is tied up; what to inspect or change; what evidence supports it; what happened afterward*. Never promise causal profit improvement or fully automatic ordering/price changes before validated action/outcome proof.

**Competitive reality:** sell-through, ABC, aging, supplier comparisons and inventory turns are standard retail-report capabilities (examples: [Shopify product inventory analytics](https://help.shopify.com/en/manual/products/analytics), [Square aging inventory](https://squareup.com/help/us/en/article/8265-track-aging-inventory-with-square-for-retail), [Lightspeed sell-through](https://x-series-support.lightspeedhq.com/hc/en-us/articles/25534083755675-Using-the-sell-through-inventory-report)). Treat these as table-stakes, not unique Trendplus inventions. A possible differentiator is trustworthy import lineage + local footwear/store context + recommendation-to-action-to-measured-outcome continuity. **Commercial distinctiveness is a hypothesis** until customers use/pay for it.

## 2. Five customer jobs; favor an existing route over a new screen

| Job / question | Primary screen (existing) | Decision/output | What must be true |
|---|---|---|---|
| How did we perform in a chosen period, shop, supplier, footwear type? | `/analytics`, `/analytics/daily-sales`, `/analytics/shoe-type-sales-stats` | reliable sales, signed returns, comparison and drill-down | canonical retail population, same effective period, source-horizon truth |
| Which goods consume capital or need a stock check? | `/analytics/inventory`, `/analytics/products` | inspect slow stock, possible transfer/replenishment; no automatic execution | observed latest stock vs historical stock distinguished, cost coverage, size/store identity |
| Which supplier merits an assortment or purchasing conversation? | `/analytics/supplier`, supplier report/scorecard | dated, explainable supplier negotiation facts | revenue, margin and cost provenance; show unknown share, not a fabricated rank |
| Which price changes merit review, and what followed? | `/analytics/pre-nivelacija-prioriteti`, `/analytics/nivelacije-pre-post` | descriptive markdown candidates, measured historical outcomes if eligible | event time, source store/day coverage, costs, returns and sample/maturity gates |
| What are this week's highest-priority *eligible* actions? | `/analytics/decision-pulse`, `/analytics/actions` | decision digest and accepted/deferred/rejected action with reason | **RQ585 is already DONE**; validate existing UX/adoption, don't rebuild; fresh/current claims remain gated |

The app's `/` home must guide users to these jobs and **never display manually typed business numbers as real KPIs**. The previously confirmed fixed-count/trend/Google Shopping claims were removed by **RQ661 DONE** (`dcdb84c79aa22eb4f3f4c3806645cfd36fbb35c6`); **RQ659 DONE** separately removed experimental primary navigation. These are completed corrections, not an open implementation plan.

## 3. Four decision-proof levels (one indicator may be at a different level in each store/period)

1. **OBSERVED / CERTIFIED HISTORY** — reconciled source facts for bounded period/store, explicit denominator and provenance. Permitted: descriptive historical sales and tested cohort reports; not automatically a current action.
2. **PARTIAL / ESTIMATED** — missing or estimated cost, partial daily coverage, reconstructed inventory, unknown variant attributes. Permitted: labeled exploration or `FIX_DATA`/review signals; block money-action claims needing missing evidence.
3. **UNAVAILABLE / NOT SUPPORTED** — no trustworthy stock mutation history, no receipt-grain proof, missing source time, no valid comparison. Show reason; do not render zero, causal outcome, shift basket or time-of-day claim.
4. **CURRENT-ACTION ELIGIBLE** — recent authoritative source, complete corresponding decision inputs, backend `recommendationAllowed`/reason, allowed role, nonempty evidence. This is a separate gate from correctness of historical math.

**Never elevate a tier using UI copy, cached data, a previous period, a synthetic fixture or a plausible-but-undocumented source fallback.** `imported/existing/all` is an explicit population choice and cannot be mixed silently.

## 4. Current evidence and the false-certainty traps

Facts in dated repository evidence, **not a new live-provider check**:

- Local Access import batch #23: 5,530 source receipts and 67,092 source sale lines reconciled to analytic facts with zero line value/quantity delta. This validates imported source-line parity, **not** every downstream chart, current production or receipt-as-customer-basket semantics.
- Cost: 501 Access retail lines / RSD 1,711,050 sales still have unknown cost in the 2026-10-10 validation; positive master fallback cost is an **estimate**, never a confirmed historical price. RQ601 awaits 20 original calculations and source confirmation. Numeric coverage does not prove provenance quality.
- RQ602 froze a descriptive 20 treated/20 control historical markdown cohort, but complete store/day coverage, mature comparable controls and causal impact are not proven. A before/after percentage is not recovered profit.
- RQ610 needs reliable SKU × store stock-mutation generation; current inventory snapshot cannot be backdated into historical OOS, GMROI or turnover as-of.
- STAB16 still owns **provider/deployed** freshness, schema, worker/refresh and supplier MV proof. Verified GitHub CI ≠ verified Render/Neon/Vercel business result.
- RQ585 owner digest **implemented on main (2026-10-08, `739815e36f9c...`)** with up to ten eligible items, dated context and action disposition. Next work is usability/adoption and operational proof, **not reimplementing a five-item digest**.
- RQ655–RQ665 relevant closed frontend/test tasks are DONE, including RQ657 race guards, RQ659 navigation, RQ660 nullable PDC counters, RQ661 homepage truth, RQ662 supplier store-scope filters, RQ663 daily shift evidence and RQ664 synthetic browser/export rehearsal. PR #159 hardened the rehearsal; it does not certify production. Next distinct source-reviewed frontend risks are RQ666 PDC dataScope/action identity and RQ667 Inventory store/compare-scope selection (both READY, not claimed).

**Do not** use the older 2026-06 Decision OS roadmap's past "live smoke passed" sentence as evidence that production is healthy in October.

## 5. KPI readiness and calculation integrity (gate per dimension)

| KPI group | Current safe claim | Prohibited upgrade without additional proof |
|---|---|---|
| Sales by date/store/supplier/type, signed net units/revenue | **core**, bounded and cross-screen reconcilable for proven cohorts | synthetic receipts, all-time fallbacks, unchecked day completeness, local currentness |
| Contribution/margin by product/supplier | **partial where provenance unknown**; display known-cost numerator and matched revenue denominator, coverage by rows *and revenue* | `0` for missing cost, implicit historical confirmation, profit net of OPEX |
| Latest observed SKU/store stock | **latest snapshot**, with store/date metadata | historical stock as-of, observed OOS days or lost sales from that snapshot alone |
| Sell-through / turns / GMROI / stock cover / dead-stock age | **conditional/proxy only** until proper opening/receipts/stock-series and trustworthy costs; explain which definition is used | borrowing an industry formula without matching its denominator or calling estimated age observed |
| Supplier rank / negotiation | **descriptive evidence** with cost/quality coverage; no unsupported universal score | supplier ROI ranking when cost or stocked capital is unknown |
| Pre/post markdown | **descriptive** with comparable observation windows and explicit limitations | controlled causal uplift, price elasticity or recovered-margin claim without control/coverage design |
| Color/size and shift/basket | **conditional** on actual variant, time and receipt-grain source availability | charts dominated by "Nepoznato", customer basket or shift KPIs from unproven sale headers/times |
| Forecast/AI/external trend | not in active saleable scope | new model/scorer/scraping/claim without owner reapproval and validated outcomes |

For exact terms and math, reuse `ANALYTICS_BUSINESS_GLOSSARY.md` and existing backend policies. **Do not change established formulas from this playbook.** A new metric requires a separate authored contract specifying numerator, denominator, price basis, net-return policy, tax treatment if known, calendar, SKU/store grain, cost provenance, handling of zero/negative denominators, sample/coverage and regression oracle.

## 6. Data-contract checklist before showing a financial or causal claim

For each metric/action record: source system and import batch; `DataOrigin`/`dataScope`; tenant/store; product/variant and supplier identity; requested vs effective period; Belgrade business-day versus source UTC timestamp; sales-grain versus proved customer receipt; signed return/DUG/KOREKCIJA policy; revenue tax/discount semantics (unknown if not verified); cost provenance and cost-covered **revenue**; historical vs latest stock, materialized-cache version/refresh; missing/zero/insufficient status; result unit and denominator; sample size and whether control is mature; eligibility/reason; source route and export parity.

If a required dimension is missing, lower the claim level rather than forcing an arbitrary source backfill. For seasonal footwear, do not compare summer stock with winter demand without recording cohort/season context. Returns, incomplete size runs, promotions and cross-store transfers can alter conclusions.

## 7. The operator's weekly five-step workflow

1. **Choose explicit evidence window:** "Podaci o prodaji do …"; show import/worker status separately. On a stale source, allow **historical** review but not "what to order today".
2. **Review 3–5 biggest material problems:** unproven cost, slow capital (when valuation coverage exists), supplier exposure and markdown candidates. Prioritize **backend eligible** items; show blocked items under data checks, not among top actions.
3. **Explain:** reason, source grain, filtered shop, known revenue, actual units, missing share, alternatives and confidence/proxy limitations; one click to underlying table and export.
4. **Decide with human:** accept, defer, reject or investigate. Don't auto-write price, transfer, order or inventory state to customer systems.
5. **Close outcome:** log action date, who acted, scope, baseline, observation window, execution evidence, then observed result. Label no outcome as `not_measured`. Historical changes without controls are descriptive, not causal lift.

Existing Decision Pulse/Actions implement **parts** of this loop. Use the [20-minute operator value card](../pilot/RETAIL_OPERATOR_VALUE_SESSION_2026-10-14.md) for a real, observed run; empty forms remain NOT RUN, not customer proof. Do not invent a new board/score first.

## 8. Roadmap / capacity management: no more 50-prompt cycles

**14.10 current execution decision:** see [business progress and focus](TRENDPLUS_BUSINESS_PROGRESS_AND_FOCUS_2026-10-14.md). RQ666/RQ667 are the only immediate P1 core frontend work; RQ669/670/671 P3 work is paused, and any separate operator session is *external business evidence*, not an invented agent test. Preserve the already shipped RQ585 digest and RQ659–RQ668; judge the next sprint by correct decisions, freshness and owner-observed usability, not prompt closures.


**Wave A — remove deception & simplify (existing tasks first):** RQ659 hide experiment navigation; RQ661 replace fake business facts on `/`; RQ660 null-count contract proof only if real; RQ657 only when an actual race is reproduced. Preserve core API/compatibility, tests, import history. No blanket deletion.

**Wave B — make core trustworthy:** STAB16 deployed currentness independent of historical formula proof; cost provenance RQ601; inventory generation RQ610; metadata/coverage defects only when a specific financial or decision claim is at risk. Don't recreate RQ585 or the completed RQ593/594/595/596/651–656 work.

**Wave C — prove usefulness with one retailer:** use existing weekly digest and pre-/post-nivelacija flows; conduct observed user sessions, log real disposition and outcomes; RQ592 prospective pilot only after owner/provider/coverage gates. Gate marketing claims on outcome evidence.

**Wave D — expand deliberately:** footwear size-run, season cohorts, transfer economics, reorder lead time or supplier negotiation scenario **only if** source fields + user-paid problem are verified. Current experimental forecasts, marketplaces, generic GenAI, shared tenant platform and random additional charts are not priorities.

Do not register new RQ IDs for speculative Wave C/D concepts. Promote an existing owner task or add a bounded prompt only after a concrete uncovered code/source gap is proved and an active owner is established.

## 9. Pilot/customer-discovery protocol (hypothesis, not established demand)

Ask three to five *actual* footwear operators to show last week's sales, slowest stock, markdown decisions, supplier purchasing discussion and how their source data is exported. Observe the work; don't sell dashboard mocks as proof. For a willing pilot, agree **before implementation** on who approves decisions, which stores, how imports are refreshed, whether confirmed purchase costs exist, whether raw source and sales event chronology can be inspected, and whether outcomes can be measured.

Suggested **owner-set trial goals**, not industry facts: at least one operator can reconcile sales without developer help; at least one real decision is accepted with traceable evidence; a full working-week review is completed with explicit stale/unknown labels; a prospective outcome claim is deferred until its maturity and coverage checks pass. Time-to-first-useful-decision, review completion, evidence-link use, decision disposition and measured action coverage are product success metrics, not counts of rendered screens or queue files.

Capture candid objections: "my ERP already shows this", "I don't trust cost", "I cannot change supplier orders", "we have no clean stock movements", "price changes depend on season/size", "why pay monthly?". Record owner decisions as **hypotheses, observations, implementation contracts** in separate fields; never promote an interview guess to measured business proof.

## 10. Practical implementation/QA definition of done

For any task:
- State one named user decision and whether it is **signal, report, recommendation or outcome**.
- Identify canonical backend owner, source grain and **existing implementation**; show why the work is not a duplicate DONE task.
- Include affected route/DTO/SQL/table/chart/export/action, with contract for scope, date, returns, cost, negative/zero/unknown, freshness and error/retry.
- Use a failing-first smallest real test, independent SQL/source oracle for financial aggregation when appropriate, a no-fake-zero/no-fake-green adversarial case and UI/source/export parity. Do not treat skipped PG tests as successful runtime proof.
- Define a user-observable acceptance (e.g. "the owner can tell why supplier A ties cash, for which shop/period, or sees Nije dostupno").
- Require GitHub exact-main proof per canonical protocol; record provider proof as missing when it is missing. No production import, migrations, destructive writes or unsafe cleanup under a planning prompt.
- Keep evidence lightweight: one short outcome, relevant test IDs, SHA, residual blocker and the next unblocked business action. Avoid redundant audits, duplicate queue copies and adding a parallel business dictionary.

## 11. Document authority / how an agent starts

Read, in order: `AGENTS.md` → `MASTER_ROADMAP.md` (routing) → [scope](ANALYTICS_FIRST_SCOPE_2026-10-14.md) → **this playbook only for product/value choices** → exact owner queue + selected source/tests. For formulas go to [glossary](../ai/ANALYTICS_BUSINESS_GLOSSARY.md); for test commands [test strategy](../ai/ANALYTICS_TEST_STRATEGY.md). Dated audits and `RETAIL_ANALYTICS_KPI_ROADMAP.md`/`ANALYTICS_DECISION_OS_ROADMAP.md` are historical/candidate context, **not READY selectors**.

Commercial and provider validation are not proven by this documentation change; this document contains no claim of running live Render/Neon checks on 2026-10-14.
