# Trendplus / Retail ProfitGuard — commercial feasibility and decision strategy (2026-10-15)

**Status:** product-market research and owner decision support; **not** a READY queue, release certificate, new formula authority or authorization to change production. **Requested directly by owner.** Repository baseline inspected: `main` `94d90e0523b7501045a567f9f513539235ddd487` before this document. Market sources accessed 2026-10-15. Market assumptions are **not customer validation**.

**Canonical precedence:** [analytics-first scope](ANALYTICS_FIRST_SCOPE_2026-10-14.md), [retail playbook](RETAIL_ANALYTICS_PRODUCT_PLAYBOOK_2026-10-14.md), [business roadmap](../roadmaps/BUSINESS_ROADMAP.md), [business progress](TRENDPLUS_BUSINESS_PROGRESS_AND_FOCUS_2026-10-14.md), [pilot operator card](../pilot/RETAIL_OPERATOR_VALUE_SESSION_2026-10-14.md); implementation and work-status truth remain code/tests + `MASTER_ROADMAP.md` and canonical owning queues. This report does not change those rules, add RQ IDs or override completed tasks.

## 1. Executive recommendation and falsifiable thesis

**Recommendation:** keep Trendplus narrowly focused on footwear/seasonal multi-store retailers; sell a **bounded, paid, read-only diagnostic + recurring weekly decision review** before attempting a general SaaS. Reuse existing sales/supplier/inventory/markdown/actions routes and RQ585 digest. Explore a *separate* CSV-based supplier invoice/contract discrepancy service only after a real distributor supplies authentic sample documents and willingness to pay. Do **not** launch seven domains, an extra dashboard, a generic AI optimizer, a POS/ERP replacement, or shared multi-tenancy now.

**Who might buy (hypothesis):** a decision-maker at a footwear/fashion retailer with approximately 2–15 stores, seasonal stock, supplier purchasing decisions, exportable POS/Access history and sufficient transaction volume. Store count is an initial qualification heuristic, **not market data**. A single small shop may not value a separate analytics bill; a sophisticated chain may already have embedded BI.

**Testable job:** “Once per week, identify 1–3 defensible supplier, inventory or markdown decisions, show the source and missing evidence, assign an owner, then measure what actually happened.” This is only differentiated if it is *faster, more trusted or more operationally adopted* than the customer's existing ERP + spreadsheet practice. No evidence yet confirms that condition.

**Anti-thesis:** embedded ERP analytics win on price and convenience; the customer already knows the problem but cannot act; source data/costs are incomplete; onboarding becomes consulting; customers will not pay monthly. If any dominates, sell bounded audits/integrations as a service or stop pursuing standalone SaaS.

## 2. Verified repository capability versus missing proof

| Area | Confirmed implementation/evidence | Constraint / what NOT to claim | Primary files |
| --- | --- | --- | --- |
| Sales by day/store/supplier/type | Existing AnalyticsDashboard/DailySales/Supplier/Shoe Type; batch #23 documented 5,530/5,530 headers and 67,092/67,092 sale-line fact parity | Reconciled historical population != current production, customer baskets or all charts | `Api/Services/DailySalesStatsService.cs`; `docs/qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md` |
| Margin/cost provenance | `MarginAccumulator` separates historical, product fallback and snapshot cost; unknown is not forced to zero | 501 retail sale lines / RSD 1,711,050 sales unknown cost in dated local evidence; numerically present fallback cost != verified original purchase calculation; RQ601 needs originals | `Application/Analytics/AnalyticsMarginPolicy.cs`; RQ601/RQ602 validation |
| Latest stock and inventory action candidates | `InventoryEndpoints.BuildActionWorkflowAsync`: replenishment, markdown and clearance; `InventorySignalCalculator` gates stock-cover/sell-through evidence | Stock snapshot != historical SKU×store availability. Observed capture foundation exists but disabled by default and company-level where store-grain missing. RQ610 not solved | `Api/Endpoints/InventoryEndpoints.cs`; `Api/Endpoints/InventorySignalCalculator.cs`; `docs/architecture/OBSERVED_INVENTORY_DAILY_SNAPSHOT.md` |
| Supplier decision/negotiation facts | Supplier screen/report; `buildSupplierNegotiationFacts` assembles up to five known-only facts | `SupplierBuyingValueEvidenceContract` marks supplier-wide cover/sell-through and purchase-order/lead-time unavailable; not a supplier invoice-control engine | `Klijent/clientapp/src/utils/supplierNegotiationFacts.ts`; `Application/Analytics/SupplierBuyingValueEvidenceContract.cs` |
| Decision/action/outcome | `/api/analytics/actions`, `/decision-pulse` and RQ585 digest; RQ666 + RQ667 completed 2026-10-15 with green exact-commit UI gates | No observed operator session or documented measured customer financial outcome. RQ592 prospective effect still source/owner-gated | `Api/Endpoints/AnalyticsActionsEndpoints.cs`; `.ai/runs/2026-10-15-RQ666-evidence.md`; `.ai/runs/2026-10-15-RQ667-evidence.md` |
| Onboarding | Access import with batch #23 reconciliation; read-only SQL Server discovery/preview and checkpointed `SourceSyncAppliedRows` staging | SQL Server staging is NOT certified canonical end-to-end Artikli/Prodaja ingestion; not “connect any ERP in minutes” | `docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md`; `Api/Services/DataSources/SourceCheckpointSyncEngine.cs` |
| Multi-customer operations | Dedicated app/database per customer is approved pilot topology; an app-level TenantId value object exists | No verified shared-db tenant isolation across queries, cache, storage, workers, files, logs or auth. Do not onboard two firms on a shared tenant boundary | `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md`; `docs/architecture/TENANT_CONTEXT_CONTRACT.md` |
| Current operations | Dated repo product audit explicitly documents stale production sales horizon 2026-08-05 at the 2026-10-07 check | **No new independent Render/Neon/Vercel live inspection in this market analysis**; do not state production is still stale today as fact | `docs/product/TRENDPLUS_BUSINESS_PROGRESS_AND_FOCUS_2026-10-14.md` |

**Proof taxonomy:** (a) code exists; (b) deterministic tests pass on named commit; (c) local source oracle passes for named batch; (d) deployed source is verified current; (e) a human used/paid for it; (f) a financial outcome is measured. None implies the next.

## 3. Outside-market checks: what the numbers do, and do not, tell us

- **Serbia trade count, 2024:** RZS structural business statistics show **65,015 entities** in broad sector G (wholesale + retail + motor vehicle repair), including **60,427 micro**. This is **not** a footwear-retailer count, not a list of qualified analytics buyers and not a defensible TAM. Source: https://data.stat.gov.rs/Home/Result/190102?languageCode=sr-Latn
- **Narrower retail context:** RZS retail trade sampling metadata lists **6,707 enterprises** in the business-register base for activity 47, of which **1,942** fall under broad group 47.7. That group includes more than footwear, excludes most unincorporated sole traders, and describes a sampling/statistical frame, **not** an addressable customer base. Source: https://data.stat.gov.rs/Metadata/21_Unutrasnja%20trgovina/Html/2101_ESMS_G0_2024_2.html
- **Software adoption:** Eurostat reports that in **2025** BI adoption among EU enterprises with 10+ employed persons was **11% small** vs **69% large**; its 2025 country chart shows Serbia near **15% overall among surveyed 10+ enterprises**. Coverage and sizes matter: this cannot be applied to all Serbian micro-retailers or read as willingness to buy Trendplus. Sources: https://ec.europa.eu/eurostat/en/web/products-eurostat-news/w/ddn-20260520-1 and https://ec.europa.eu/eurostat/statistics-explained/SEPDF/cache/22286.pdf
- **Digital commerce tailwind:** NBS reported **110.6 million card/e-money internet purchases in 2025 (+34.3% YoY)**. This signals digital transactions, **not** growth in footwear shops or demand for standalone inventory analytics. Source: https://www.nbs.rs/sr/scripts/showcontent/index.html?id=21315
- **Competitive feature floor:** Serbian PANTHEON already advertises inventory/warehouse flows, supplier ordering, custom BI, profitability and reports. Globally Prediko publicly offers an entry inventory-planning plan at **$49/month** (GMV-tiered) and Lightspeed Retail X-Series shows public **$89/$149/$289 monthly** POS tiers with analytics/inventory features varying by plan/region. Inventory Planner prices by inventory volume by quotation. These are *current public marketing/price pages*, not apples-to-apples Serbian purchase offers and do **not** prove our pricing power. Sources: https://www.datalab.rs/pantheon/accounting/cene/ ; https://www.prediko.io/pricing ; https://www.lightspeedhq.com/pos/retail/pricing/ ; https://www.inventory-planner.com/pricing/

**Market-size policy:** do not calculate `65,015 × subscription`, assume a footwear business share, or claim TAM/SAM/SOM as observed until obtaining a **deduplicated** list of operators/branches by retail segment, number of locations, incumbent ERP, decision-maker and export permissions. Use bottom-up named-account discovery, not fabricated percentages.

## 4. Product variants — complexity, strength, risks, sequencing

| Commercial variant | Existing reuse | Additional work / evidence gate | Relative difficulty | Current decision |
| --- | --- | --- | --- | --- |
| **A. Read-only Retail Profit Audit** (dated history + cost caveats + supplier/stock/markdown checklist) | High: sales, margin, inventory, supplier, export, Data Quality | One reproducible source scope, human-readable report, independent oracle/sample checks; do **not** price unknown capital as a proven loss | Low technical / medium customer proof | **FIRST**: sell/observe before more features |
| **B. Weekly Decision Review subscription** (3–5 evidence-backed actions with human disposition) | High: RQ585 digest, Decision Pulse/Actions, source gates | Stable customer refresh, owner training, disposition and follow-through adoption | Medium operations | **SECOND**, only if repeat use/price confirmed |
| **C1. Supplier negotiation dossier** (supplier sales, known margin, known snapshot exposure) | High: existing supplier report and negotiation facts | Cost provenance, business acceptance; no asserted full supplier ROI without coverage | Low technical / medium evidence | Bundle with A/B |
| **C2. Invoice/contract leakage check** (price-list vs supplier invoice) | Low-to-medium: data quality, ETL patterns, audit/report | New effective-dated contract/PO/invoice line model, tax/unit/currency/rebates/credit-note rules, matching and human verification | Medium/high; bounded 2-CSV spike may be 2–4 developer weeks **if authentic examples available** | **SEPARATE validation track**; do not smuggle into retail RQ queue |
| **D. Wholesale true contribution by customer** | Low-to-medium infrastructure reuse | Buyer IDs, logistics costs, discounts/rebates, delivery grain, allocations, collections/returns; current sale headers do not establish these | High / domain extension | Defer until anchor distributor |
| **E. Generic ecommerce profit** | Margin/read models only | Ads, return/shipping/payment/platform attribution & new connectors; crowded market | High | Reject as initial vertical |
| **F. Restaurant cost / service job / production loss** | Infrastructure patterns only | Recipes, work orders, BOM/actual input/output or labor costing are separate domains | High to very high | No direct Trendplus feature expansion |

**Implementation effort is conditional engineering judgment, not a delivery promise:** reuse-only report presentation may be ~2–5 active developer days if existing API data is verified; a bounded 2-CSV discrepancy pilot ~2–4 developer weeks after real sample docs; a reliable new ERP-to-canonical sync ~3–6+ weeks *per first supported format*, subject to schema and access; authoritative historical SKU×store stocks require a source completeness/generation decision and cannot be put on a calendar without that input. Live data certification, customer permission and business discovery cannot be shortened by code generation.

## 5. Different angles / alternative go-to-market

1. **Sell “decision evidence” not dashboards.** Each item needs amount *type* (observed invoice difference / valued stock / scenario), scope, date, cost coverage, reason, source trace, owner and an explicit next action. This avoids liability from naming cash tied up as “loss”.
2. **Audit-as-product:** one-off evidence review for an owner hesitant to buy a subscription; deliver an exportable *verified findings packet*, then offer recurring monitoring only if the same issues recur.
3. **Embedded/white-label channel:** ERP integrators and accounting/retail consultants already have trusted access. Investigate referral/revenue-sharing or a read-only add-on. Risk: partner may already sell competing BI, own the relationship or demand bespoke reports.
4. **Private deployment:** dedicated installation may be more credible than sending stock/cost data to a new multi-tenant SaaS; creates operating/support burden and slower upgrades. Pilot mode is already architecturally compatible.
5. **Exception detector first:** cost provenance discrepancies, bad joins, duplicated sales documents and inconsistent store attribution may yield a clearer proof than speculative stock/price optimization. Source-incorrectness can itself be a *paid data audit*, not “saved profit”.
6. **Merchandising specialty:** size-run/season/transfer review for footwear chains may be more defensible than generic retail KPIs, **only when per-store size and historical receipts exist**. Today's source gaps block strong claims.
7. **Human decision rights:** recommendations fail commercially if the buyer cannot change ordering, prices or supplier terms. Qualify authority before promising ROI.
8. **Cohort fragility:** 2-store / sparse SKU cohorts, seasonal shifts, returns and stockouts can create false markdown uplift. No causal claim before the pre-registered, mature source/availability design (RQ592).
9. **Data quality tax / integration moat:** a repeatable mapping and reconciliation playbook is defensible; if every integration is custom, a “SaaS multiple” is unjustified. Measure support and onboarding hours separately.
10. **Channel conflict / incumbent squeeze:** incumbents add recommendation emails and reports; product must work as a trusted read-only complementary layer or be sold as service, not rely on exclusive ABC/sell-through/forecast features.
11. **Privacy and operational liability:** source credentials, employee/receipt data, documents and exports need explicit permission, retention, access control and tenant isolation. Avoid uploading raw invoices, personal details or secrets into public repo.
12. **Value selection:** a significant cash-exposure estimate is not a realized margin benefit; a small verified mispricing discrepancy might be a stronger first customer case than a large unsupported “potential saving”.
13. **Internationalization:** Serbian footwear/Access know-how is a discovery asset, not an export moat. Expansion requires connector localization, tax/currency semantics, sales channel/grain and legal review; start with regional partners, then test one outside market by named interviews.

## 6. Revenue scenarios and economics (ILLUSTRATIVE, NOT FORECAST EVIDENCE)

**Twelve-month exit MRR scenarios**; mutually alternative assumptions, not probabilities or commitments. EUR/month before fees, tax, support, labor, hosting or sales expense; “ARR run-rate” = exit MRR × 12, **not** cash earned during the first 12 months.

| Scenario | Active paying customers at month 12 | Average recurring charge | Exit MRR | Exit annualized run-rate |
| --- | ---: | ---: | ---: | ---: |
| Stress / stall | 0 | €0 | €0 | €0 |
| Conservative | 3 | €149 | €447 | €5,364 |
| Base hypothesis | 12 | €249 | €2,988 | €35,856 |
| Upside hypothesis | 35 | €349 | €12,215 | €146,580 |

**No probability or confidence interval is justified.** The 35-customer case requires substantial support/sales/onboarding capacity and must **not** be represented as a realistic solo founder baseline. The €149–349 anchors are **test prices**, not established willingness-to-pay; competitor offerings limit unsubstantiated pricing premiums.

**Unit-economics stress test:** on hypothetical €249/account/month and €50/account/month variable infrastructure/support, contribution is **€199/account/month before acquisition cost and founder salary**. Hypothetical €2,000/month fixed cost requires `ceil(2000/199)=11` active accounts to cover those *specified* fixed costs, not full business profitability. At a one-time onboarding fee of €900, if implementation consumes 25 hours at €40/hour internal cost plus €100 external cost, contribution on setup is **-€200**. Capture real onboarding labor, customer acquisition effort, refunds, payment delays and churn before calling this scalable.

**Concentration hazard:** with 3 customers, one cancellation removes one-third of subscription MRR; at 12, one cancellation removes 8.3%. First-year recurring revenue can remain negligible even when historical analytics functionality is technically sophisticated.

**Pricing tests, not publication claims:** one-off limited diagnostic €300–900; assisted monthly review €149–349 for qualified small chains; more complex custom integration separately costed. Test if a lower software-only price or higher service-priced tier performs better, not both unboundedly.

## 7. Discovery and decision gates — no new RQ IDs

**Days 1–14 / named owner task, not an AI queue:** 12–15 interviews across 2–15-store seasonal retailers, including at least 5 who already use an established ERP. Ask for the *last real* stocking/markdown/supplier decision, the incumbent report, actual data-export path, who approved the decision and what money was at stake. Record objection verbatim and the decision-maker, not an inferred desire for AI.

**Days 15–30:** 3 authorized, bounded, read-only sample datasets; reconcile source items, returns, stores and cost provenance; run the existing [20-min operator card](../pilot/RETAIL_OPERATOR_VALUE_SESSION_2026-10-14.md) in person. No raw customer data/credentials in public git. Historical samples can validate a historical diagnostic, **not** current ordering advice.

**Days 30–60:** attempt 2 *paid* scoped diagnostic projects with signed acceptance and price (owner-selected threshold, not a market standard); count verified differences, blocked findings, time to first useful decision and manual steps saved vs ERP/Excel. Only after recurring use is observed propose a subscription.

**Proceed to repeatable Retail SKU/store connector work only if**: at least 2 paid pilots, an independently reconciled result useful to the operator, a named purchasing decision-maker, a repeatable export source, and written customer intent for continued use. Customer rejection/absence of data is a no-go **for that segment or packaging**, not permission to invent evidence or automatically pivot to AI.

**Reposition to service/ERP add-on** if the audit is valued but customers refuse monthly fees, each source needs extensive one-off mapping, or ERP already supports all target decisions. **Stop** the vertical SaaS hypothesis if repeated qualified operators show no incremental value vs existing workflow, or critical cost/store evidence cannot be obtained.

## 8. Safe engineering decision and non-goals

- No new application/queue work from this document. RQ666/RQ667 are DONE; RQ585 digest is DONE. Next activity is real operator proof, STAB16 authorized deployed source/currentness, RQ601 original purchase-cost docs and RQ610 stock-history source design.
- No new money KPI without canonical numerator/denominator, net returns, tax/discount basis, customer/store/source, cost provenance, unknown vs real zero, horizon, eligibility, tests and SQL oracle. Preserve existing `AnalyticsMarginPolicy` and backend decision authority.
- Do not backdate current inventory, fabricate historical OOS/lost sales, reuse synthetic browser fixtures as deployed evidence, or call descriptive markdown comparisons causal profit recovered.
- Shared-tenancy is not a 2nd-customer pilot prerequisite; one customer per dedicated deployment/database/storage/cache is the approved short-term model. Never join independent firms in shared current app/database.
- Respect scope freeze: no marketplace scraping, model-training/runtime scoring, universal GenAI, unrelated dashboard families, cosmetic mass refactors or speculative forecasts.
- Track business outcomes separately from CI passes, reconciled source lines, and “actions created”. No additional queue/prompts before a named source or observed customer gap actually appears.

## 9. Explicit assumptions to revisit

| Assumption | Confidence today | Disconfirming evidence |
| --- | --- | --- |
| Footwear decision-maker pays for a separate analytics layer | **Unvalidated** | Current ERP/Excel same decision in same time; refuses first paid diagnostic |
| “Weekly decision packet” is better than dashboard | **Product hypothesis** | Operators prefer manual report, do not read follow-ups or never act |
| Cost provenance can be verified | **Partially constrained** | No original inbound calculations; legacy cost scale unresolved |
| Stock decision can be made current | **Not certified for deployed data** | Missing SKU×store generation and/or failed refresh |
| Multi-client onboarding is repeatable | **Not proven** | Each customer needs unique ETL/exception rules |
| Margin/stock changes can prove impact | **No measured outcome yet** | Incomplete coverage, no approved prospective action/control |

**Final owner choice required only after first real interviews:** continue *Retail read-only audit → weekly decision service*, or choose *supplier-invoice discrepancy* as a separate product; do not code both full-featured products in parallel.


## 10. Longer-range perspective, 2027–2029 — *conditional paths, not market forecasts*

The realistic alternatives are not simply “startup succeeds” vs “fails”:

| 2027–2029 path | Observable threshold / operational form | Economic implication | What to do |
| --- | --- | --- | --- |
| **No repeat demand** | Diagnostic interviews/pilots fail; no real repeated decision beyond incumbent reports | Development sunk cost; MRR may remain **€0** | Stop software expansion; preserve reusable retail code and offer narrowly paid audits if requested |
| **Specialist service business** | 2–10 recurring companies, repeated human review and bespoke mappings | Owner can earn revenue, but delivery hours grow with clients; not high-multiple SaaS | Price onboarding and review labor explicitly; keep dedicated isolated instances |
| **Repeatable micro-SaaS** | 10–30 recurring clients, 1–3 supported export families, bounded onboarding and support hours | Attractive small product if churn/support are contained; still vulnerable to customer concentration and incumbent features | Automate source QA and weekly evidence, build reseller channel only after retention |
| **Partner-led regional product** | Named ERP/retail integrators bring qualified accounts; verifiable 30+ repeatable deployments | Scalable distribution possible but margin split, partner dependency, compliance and support burden rise | Negotiate reseller terms, test one regional data format before expansion |
| **Invoice anomaly control wins instead** | Retail audits low-value; distributors repeatedly pay for matched contract-vs-invoice exceptions | Different customer and data-contract architecture; Trendplus acts as technical donor, not shared core domain | Create separate product after sample-backed paid pilots; do not bury it inside footwear UI |

These are **qualitative states, not predicted counts by calendar year**. No available churn, acquisition, sales-cycle, partner, real ARPU or interview data supports probabilistic 3-year forecasts. Annualized exit MRR in section 6 is a *sensitivity model*, not a valuation or cash-flow plan.

**2027 decision horizon:** validate one retail vertical and a repeatable source; do not widen industry coverage if two paid pilots/regular usage are missing. **2028 conditional horizon:** if retention and low onboarding effort are proven, standardize 1–3 common POS/ERP export formats and a channel partner; otherwise operate as a deliberate high-touch service. **2029 conditional horizon:** only after repeatable acquisition, retention, costs and security are measured, evaluate shared SaaS, regional licensing or embedded OEM/API distribution.

**Structural risk over three years:** general-purpose AI-generated reports and better incumbent ERP BI may make generic dashboards cheaper and less differentiated. Durable advantage, if any, must come from *verified source lineage, explicit business decisions, traceable human execution and independently measured results*, not model novelty. This is a strategic inference, not measured market share.

## 11. Decision scorecard and pre-mortem (owner evaluation, not product KPI)

- **Should we even sell a standalone tool?** Compare the exact same business question in the incumbent ERP/Excel. If improvement is only cosmetic or the customer is unable to act, a separate product is not justified.
- **Which economic pain is easiest to *prove*?** A corrected supplier invoice discrepancy (if contracts exist) may be more provable than recovered markdown margin; a large inventory holding estimate can look impressive while conveying no realized saving.
- **Who will distribute it?** Direct sales to many tiny shops likely have poor economics; 2–15-store owner/operators and ERP implementation partners are test segments, not validated ICP.
- **What is the real moat?** Existing formulas/data quality and tested retail provenance are reusable technical strengths. Incumbent access, users and integration distribution can be stronger competitive moats than code sophistication.
- **Where does it fail dangerously?** Wrong cost scale, stale source, dataScope/store mismatch, leaked tenant data and “projected savings” framed as guaranteed profit. Gate these before charging for automated recommendations.
- **When should we NOT build more?** No paying pilot; inability to reproduce source data; onboarding longer than recurring revenue can justify; partner incentives conflicting with customers; repeat recommendations never acted on.
- **Owner decision after 60-day evidence window:** **CONTINUE RETAIL**, **SERVICE/CHANNEL**, **TEST INVOICE DIFFERENCES SEPARATELY**, or **STOP NEW PRODUCT DEVELOPMENT**. Record the choice in the existing business progress document only when customer evidence exists. Do not pre-authorize a new prompt queue or client production deployment here.
