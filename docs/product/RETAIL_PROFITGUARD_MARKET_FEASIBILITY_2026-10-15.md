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


---

## 9. Independent product, market and execution verdict — 2026-10-17

**Nature of this addendum:** owner-requested repository+public-source review, not a customer interview, production test, readiness certificate, booking commitment, or implementation authorization. Baseline main SHA: 8239678f71eefc79fe5d5741364f3dfa043016cf (checked 2026-10-17). Commercial figures below are **assumptions, not observed customer pricing**. For status precedence use the current canonical owning queue; older READY mentions in this research are historical. This addendum augments sections 1–8 rather than rewriting their recorded research.

### 9.1 Source-truth and commercial readiness map

| Job and existing evidence | Verified code / record | Unverified or unsafe commercial promise |
| --- | --- | --- |
| Day/store/supplier sales | Api/Services/DailySalesStatsService.cs; Klijent/clientapp/src/pages/AnalyticsDashboard.tsx; docs/qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md documents batch-23 5,530/5,530 headers and 67,092/67,092 lines reconciled in that dated local audit | Current live sales, every secondary dashboard projection and multi-ERP portability; RQ680 IN_PROGRESS owns secondary sales population |
| Margin and supplier capital | Application/Analytics/AnalyticsMarginPolicy.cs; Api/Endpoints/SupplierDecisionHubEndpoints.cs; RQ601 source-provenance metadata | 501 retail lines and RSD 1,711,050 historical turnover with unknown cost in batch-23 validation; even numerically present Artikli.NabavnaCena fallback is NOT verified historical cost; 20 original kalkulacija proofs missing |
| Latest stock and proposed movements | Api/Endpoints/InventoryEndpoints.cs; Application/Analytics/Inventory/InventoryDailyStockProvenance.cs; Inventory/rebalancing pages | RQ610 is BLOCKED: no unified source generation across import, manual adjustments, POS and transfers; current snapshot cannot reconstruct historical SKU x store out-of-stock or historical ROI |
| Markdown recommendations | Existing pre/post, priority and action surfaces; docs/qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md | 20 treated and 20 control historical candidates are descriptive; zero fully qualified causal shadow pairs in RQ612; no measured uplift |
| Weekly executive actions | Existing RQ585 Decision Pulse, Analytics Actions, reports and exports; docs/pilot/RETAIL_OPERATOR_VALUE_SESSION_2026-10-14.md | No observed operator adoption or paid ROI; do not implement a duplicate digest |
| SEF invoice preview and draft | Api/Services/SefXml/SefXmlInvoiceParser.cs, Api/Endpoints/SefXmlReconciliationEndpoints.cs, Klijent/clientapp/src/components/sef-xml-pilot/SefXmlReceiptDraftWizard.tsx; QDB11/12/13 DONE | Real operator/customer SEF sample NOT RUN; isolated preview/CSV is not booked goods receipt; QDB14 WAITING |
| Separate goods receipt ledger | Api/Endpoints/GoodsReceiptEndpoints.cs and GoodsReceiptWriteAccess.cs, Api/Services/GoodsReceiptService.cs, UnosRobePage/UnosRobeForm; PR #184 MERGED 2026-10-16 | Admin key alone is insufficient; GoodsReceipts:WritesEnabled OFF by default and named operator provision required; no live writes authorized. XML draft is **not integrated into automatic posting**. PR #185 remains open, pending exact-head proof |
| Access incremental import | STAB17 in docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md; PR #163 draft | #42 completed; #43–#49 failures, empty AccessImportCursors and full overwrite evidence in roadmap; no proved durable cursor or cross-database namespace, no authorization to replay or fix production |
| Deployed availability | STAB16, RQ565, Render/provider evidence owned by STAB | Deployed SHA, durable worker refresh, current Neon/Render/Vercel and source freshness not jointly certified by this study |

**Critical cross-program correction:** PR #184 is already merged and contains atomic goods-receipt write/reverse operations behind fail-closed gates; earlier text stating that the manual form merely logs receipt lines is now historical and must not be treated as current functionality. PR #163 remains OPEN DRAFT, not production-safe; PR #185 remains OPEN at this review. Do not revive QDB11–QDB13; QDB14 remains independently owner-gated and its design must be reconciled with the existing ledger before any approval. Exact PR URLs: https://github.com/ivanjovicic/Trendplus/pull/163 ; https://github.com/ivanjovicic/Trendplus/pull/184 ; https://github.com/ivanjovicic/Trendplus/pull/185 .

### 9.2 Buyer segmentation and accessible market, without inflated TAM

| Segment | Recurring buyer job | Adoption blocker / likely paid package | Evidence status |
| --- | --- | --- | --- |
| Independent footwear shop, 1 location | Find stuck sizes, check receipts, avoid manual recoding | Budget, POS already enough, modest transaction volume. Receipt draft or paid one-off check first | Demand/price NOT VERIFIED |
| Footwear/apparel retailer, 2–15 shops | Compare stores, seasonal slow sellers, supplier capital, decision priority | Cross-location product/style/size identity, independent reconciliation, ERP export rights. Best initial **hypothesis** | Demand/price NOT VERIFIED |
| Sports/outdoor/seasonal chain | Transfers and seasonal sell-through | Variant identity, true stock timeline, lead times; do not sell forecasts yet | NOT VERIFIED |
| Wholesaler/distributor | Receipt/invoice discrepancies, supplier contract terms | Purchase order, packing/quantity, unit/currency/tax/rabate and legal rights; not present as certified canonical source | Discovery only |
| ERP implementation/accounting partner | Additional read-only diagnostic and fewer reconciliations | Reproducible adapters, service-level guarantees, liability, data-processing agreement | Partner channel NOT VERIFIED |
| Generic grocery and low-SKU retail | Basic POS, cash and compliance | Incumbents adequate; low differentiation | Reject as initial ICP |

Existing 2026-10-15 research records RZS 2024 broad sector G **65,015 entities** and statistical sampling-frame activity 47 **6,707 enterprises** with 47.7 **1,942**. These are **not** footwear-shop counts, unique buying accounts, measured willingness-to-pay or an addressable SOM. Definitions and methods: https://data.stat.gov.rs/Home/Result/190102?languageCode=sr-Latn ; https://data.stat.gov.rs/Metadata/21_Unutrasnja%20trgovina/Html/2101_ESMS_G0_2024_2.html . The latter is survey sampling metadata, not a census of qualified customers. Validate fresh official NACE 47.71/47.72 enterprise and store counts, legal forms and multi-store firm deduplication with RZS/APR before numeric TAM. Public dataset entry: https://www.stat.gov.rs/en-us/oblasti/strukturne-poslovne-statistike/ . For 2026 no narrower independently verified footwear buyer count was obtained, so **TAM/SAM in number of accounts = NOT VERIFIED**.

For decision-making use a bottom-up **sales-reach model, not an invented market statistic**: owner builds a named list of 30 independently relevant Serbian firms, records store count, current POS, decision maker and export availability; 10 actual interviews; 3 permissioned diagnostics; 2 explicit paid offers; at least 1 signed paid pilot. Each conversion ratio will be calculated only afterward. A 3-year commercial reach assumption of 100–300 qualified contacted organizations is a *sales-capacity scenario*, not an estimate of all Serbian buyers. A working funnel illustration: 150 contacted x 30% agreeing to demo x 20% purchase = 9 customers; both percentages are **unvalidated assumptions**. Count companies, not shops and not POS licences. SAM subset also requires feasible integration, lawful data access and demonstrable unique decision; SOM requires actually reachable names and capacity to deliver.

Country order: Serbia (Access/SEF process knowledge) -> Croatia/BiH/Montenegro/Slovenia only after language/tax/ERP/export and partner checks -> EU/international only as an optional read-only planning layer, NOT as portable Serbian SEF compliance. PANTHEON publicly markets localized installations across RS/HR/BIH/ME/SI (https://www.datalab.eu/); this proves an incumbent presence, not Trendplus demand. Country-specific rival density, store counts and budgets: **NOT VERIFIED**.

### 9.3 Competitor matrix A — verified capability, not false absence

Snapshot of vendor-published offers checked 2026-10-17. Prices represent public list figures at check time, exclude applicable taxes/implementation unless stated; geography and billing selection may change amounts. Unknown means **NOT VERIFIED**, not 'does not exist'.

| Product / reach / competitor role | POS + compliance | Stock / receipts | Decision analytics | Public entry price or status | Trendplus testable distinction |
| --- | --- | --- | --- | --- | --- |
| **BizniSoft (RS)** direct operational incumbent | POS/fiscal/SEF integration vendor-confirmed | Robno, retail, remote data sync confirmed | Owner dashboards confirmed; footwear store-size decision detail NOT VERIFIED | Full BizniSoft suite **12 EUR/month + PDV per workstation, minimum 3 licences (36 EUR/month minimum), paid 12 months ahead**; includes POS; optional integration/LPFR extra | Do not replace POS; dated cross-system decisions only if owner cannot answer existing reports |
| **Minimax (RS/region)** ERP/accounting incumbent | eRačun, fiscal workflows | Retail stock, receipts, pricing/nivelacija vendor-confirmed | Business reporting confirmed, specific tailored size-run NOT VERIFIED | Maksi **28 EUR/month** at observed monthly setting, other packages 10/16/21 EUR | Lower-friction owner diagnosis, not commodity invoicing |
| **PANTHEON / Datalab (SI/RS/HR/BIH/ME)** incumbent ERP | Retail/POS capabilities confirmed | Multi-location retail + accounting confirmed | Detailed footwear decision compare NOT VERIFIED | RS POS RT rental **19.90 EUR/month** listed, license/edition/hosting conditions vary | Narrow analyst layer only if ROI above ERP configuration cost |
| **Odoo (global)** configurable suite/substitute | POS/Inventory/Accounting apps | Purchasing, stock and extensibility vendor-confirmed | Configurable reporting; identical decision method NOT VERIFIED | US Standard **$24.90/user/month on yearly view** (observed vendor display); integration/implementation extra | Less setup than ERP customization, if proven |
| **Shopify POS (global)** retail platform | POS, payments by supported geography | Transfers/stock, Pro | Advanced stock/product reports Pro | Shopify Basic **$39/month** monthly view, **+$89/location/month POS Pro** | Existing non-Shopify data compatibility; weak global differentiator |
| **Lightspeed Retail (global)** POS incumbent | POS | Inventory and vendor tools | Retail analytics present, feature-by-plan to check | G2 **$69/$119/$199** historical listing; current official region plan **NOT VERIFIED** | Not an attractive market to displace without partner |
| **Cin7 Core (global)** IMS / direct adjacent | Multi-channel integration | PO, stock, warehouses, variants | Reporting, optional forecasting | **$349/$599/$1,199 per month** vendor Core tiers; Omni PRICE NOT PUBLIC | Lower-cost limited workflow, not feature parity |
| **Inventory Planner (global)** direct planning specialist | Not a POS substitute | Multi-location planning / purchasing | Planning, replenishment | **PRICE NOT PUBLIC** on official quote page | Local source-integrity + decisions; Trendplus cannot yet match inventory optimization |
| **Footwear-specific regional providers** | NOT VERIFIED | NOT VERIFIED | NOT VERIFIED | PRICE NOT PUBLIC | No claimed gap or market advantage until verified product demo |

Primary links: https://www.biznisoft.com/cenovnik ; https://www.biznisoft.com/ ; https://www.minimax.rs/sr-latn-rs/cenovnik ; https://www.datalab.rs/cenovnik-pantheon-licenci/ ; https://www.datalab.eu/pantheon/retail-advanced-pos/ ; https://www.odoo.com/pricing ; https://www.shopify.com/rs/pos/pricing ; https://www.lightspeedhq.com/pos/retail/ ; https://www.cin7.com/pricing/ ; https://www.inventory-planner.com/pricing/ .
Minimax explicitly documents retail repricing (https://help.minimax.rs/help/promena-cene-vrednosna-maloprodaja). BizniSoft advertises 3,949 firms / 19,027 licences; these are vendor claims as displayed 2026-10-17, not audited counts, footwear customers or actual addressable market. Its POS page labels 2,578 outlets **December 2024**, not a current Trendplus market count. Price-comparison must separate addon/base suite, seat, location, annual billing, tax and implementation.

### 9.4 Competitor matrix B — process, usability, implementation, total cost

| Workflow | Trendplus current | Incumbent advantage | Trendplus real risk and TCO |
| --- | --- | --- | --- |
| Fiscal checkout and statutory ledger | Exists as project features, but no proven full replacement pilot | BizniSoft, Minimax, PANTHEON and localized ERP already own regulated workflow | High validation/support/liability -> **DO NOT SELL ERP replacement** |
| Raw XML invoice -> matched catalog -> draft | Implemented synthetic preview, manual matching and unposted CSV | ERP already knows supplier/product master; eInvoice imports may exist, exact human comparison NOT VERIFIED | Setup is source mapping + authentic sample test; no auto-post without explicit approval |
| Supplier / margin comparison | Historical reports + cost coverage | Incumbent has current invoice/cost facts when properly configured | Wrong fallback cost = potentially misleading financial decision; require source proofs |
| Store/size seasonal decisions | Existing Inventory/Decision/Pulse and partial size curves | Inventory Planner, Cin7, established retail BI have replenishment tooling | Missing location/time identity; false OOS/transfer claim would destroy trust |
| Go-live / operator experience | Onboarding from one legacy Access context; 20-minute validation NOT RUN | Existing ERP already embedded in bookkeeping and daily routine | New connector plus security + staff training; target 1-day read-only ingestion is only a **hypothesis**, not demonstrated |
| Five-year TCO | Fee + export + per-connector maintenance + training + source fix + support | ERP may bundle analytics within existing subscription | If our annual fee plus upkeep exceeds cost of one Excel review, buyer rationally declines |

Reviews: G2 Inventory Planner feedback mentions sync delays (3 aggregated mentions) and high cost (3 mentions), based on self-selected review corpus, **not population estimates**: https://www.g2.com/products/inventory-planner-by-sage/reviews?qs=pros-and-cons . A fashion merchant discusses planning and 88–90 day replenishment on Reddit (one person, NOT Serbian prevalence): https://www.reddit.com/r/shopify/comments/1o6uk23/inventory_forecasting_help/ . This is problem discovery, not measured willingness to pay; do not cite a lone Reddit complaint as evidence that an entire incumbent is bad.

### 9.5 Ranked opportunities — weighted judgement, not measurements

Scoring definition (1 poor, 5 favorable); weights add to 100: demonstrated problem 16, buyer access/ability to pay 10, competitor headroom 8, existing Trendplus reuse 12, available data 12, development ease 8, integration/support ease 8, sales ease 6, revenue ceiling 6, portability 6, legal safety 4, time to paid evidence 4. Total = sum(weight x assessor score / 5). **Every score below is subjective, low confidence pending interviews.** Low-score data-dependent ideas are NOT bad forever: they are premature.

| Rank | Candidate / score / effort estimate | Strongest FOR | Strongest AGAINST / stage |
| --- | --- | --- | --- |
| 1 | Owner trust/navigation of existing decision flow **78.8/100**; 12–28 h | Very high reuse and safety | Commercial value unproven; NOW only on observed friction |
| 2 | Paid dated read-only retail diagnostic **77.2**; 18–40 h packaging | Fastest realistic invoice + existing analytics | Can be done in Excel; VALIDATE buyer before product expansion |
| 3 | Existing SEF XML unposted receipt trial **77.2**; 12–35 h pilot/repair | Clear repeatable manual workflow and existing QDB code | ERP may already import; authentic invoices absent; NOW owner validation only |
| 4 | Historical purchase-cost provenance **75.2**; 20–55 h engineering if originals provided | Core trustworthy margin differentiation | 20 original calculations not yet supplied; WAITING owner |
| 5 | Existing weekly Pulse + action disposition **74.8**; 10–25 h | Recurring management value without new dashboard | Without fresh inputs may not be a weekly product; VALIDATE |
| 6 | Supplier negotiation pack **68.4**; 15–40 h | Actionable export already partly exists | Financial proof/RQ601 incomplete; NEXT after provenance |
| 7 | Reusable source onboarding/adapter **68.0**; 40–100+ h per nontrivial ERP | Cross-customer leverage | Integration and maintenance heavy; VALIDATE first |
| 8 | Supplier Invoice Guard discrepancy check **59.2**; 35–90 h for narrow CSV prototype | Tangible document error reduction | No confirmed PO/contract/receipt basis; no automatic overcharge claims; VALIDATE |
| 9 | Size-run/store transfer decision **54.0**; 70–150+ h | Footwear-specific, defensible if observed | Style/variant/stock history inadequate; LATER |
| 10 | Fully trusted historical SKU×store stock **51.6**; 80–180+ h | Enables durable inventory value | Missing full mutation generation; BLOCKED RQ610 |

TOP 5 improvements to core: cost provenance, live freshness/identity truth, existing weekly decision path, source-reconciled stock status, human-readable supplier evidence. TOP 5 possible paid adjuncts: dated Retail Diagnostic, guided XML receiving draft, recurring Weekly Decision Review, bounded Supplier Invoice Guard, partner-delivered source reconciliation service. Best effort/risk/cash candidates: diagnostic, real SEF draft trial, existing Pulse review. Best existing-store benefit: reconcile sale period, trust costs, blocked/eligible stock decision. Best first paid pilots: historical audit, manual SEF drafting, weekly owner review. International longer horizon: connector-neutral read-only profit diagnostics, supplier discrepancy workflows **with contractual sources**, style/size/location planning **after observed availability**. Definitely DO NOT build now: new POS/ERP replacement, new prediction/AI scoring, speculative automatic transfers or markdown, broad many-tenant architecture, external eBay/Amazon/Shopping/scraper/model-training features.

**Gates beat scores:** a 78.8 UX tweak is NOT permitted to displace P0 financial integrity or an active agent claim. The above is a commercial comparison, **not READY routing**.

### 9.6 Six commercial strategies, decision and 200 development-hour envelope

A generic retail SaaS: widest features, weakest single-developer feasibility. B footwear analytics overlay: strongest current software fit but source quality gating. C receipt/invoice control: clearest workflow wedge but direct ERP competition and liability. D dated read-only audit + advisory subscription: smallest initial go-to-market risk. E white-label/OEM: attractive only after partner commits distribution, costly support contracts otherwise. F **one core analytical decision product + narrowly approved paid diagnostic/receipt add-ons**: preferred conditional strategy, no permanent split into 3 products before pilot.

Recommended opening offer **hypothesis**: fixed-scope, 1–2 week historical read-only diagnostic **300–600 EUR** (source reconciliation, dated sales, supplier, slow-stock caveats and owner meeting), with no guaranteed saved amount. After proven repeat use: **99–249 EUR/company/month**, priced per company with a small included store allocation; negotiated location/volume overages, setup **300–900 EUR** for new adapter mapping. XML draft as an optional service only after genuine time saved; no automated financial posting promises. Validate actual invoice/payment terms before public price list. The published low-priced ERP subscriptions (BizniSoft full suite can start at 36 EUR/month for three workstations, excluding VAT/other extras) make **29 EUR generic dashboard** a weak standalone proposition. Proof of a specific recurring decision and differentiated integration is essential.

**200 hours in 90 days, conditional developer allocation:** (1) 40 h source/production correctness safety evidence and targeted existing STAB/RQ gates (no unapproved production action); (2) 35 h SEF XML draft operator reproduction, high-value UI defects and isolated regression checks (not QDB14 posting); (3) 30 h reusable dated diagnostic report/export from existing routes; (4) 25 h operator-tested trust wording/navigation **only if reproducible blocker**; (5) 25 h narrow second-source CSV/ERP read-only intake proof **only after prospect export**; (6) 25 h security, data deletion, permission and support-onboarding proof; (7) 20 h release/regression contingency. Sum 200. If gates/customer permissions are missing, stop contingent implementation and transfer capacity to existing independent P0/P1 tasks, not to speculative screens. Business-owner interviews/sales are tracked separately; even if developer hours are budgeted, there is no guarantee customers can be recruited.

### 9.7 Financial model for 12/24/36 months — illustrative, not forecast

Use exit-month active paying customers, **not** cumulative signups: exit MRR = active firms x blended subscription price. Assumed blended recurring price 149 EUR/company/month; variable hosting/support/payment costs **20%** of revenue; founder time opportunity cost **35 EUR/hour**; founder engineering/support allocation **200 h/quarter = 66.67 h/month** (2,333 EUR/month); other fixed infrastructure 250 EUR/month and prospecting/operations 400 EUR/month = **2,983 EUR/month fixed**. Setup fees and consulting revenue are excluded from MRR and recurring break-even (conservative treatment). Assumptions vary by customer and are NOT actual Trendplus P&L.

| Scenario | 12m / 24m / 36m active clients (assumptions) | Exit MRR EUR 12/24/36 | 36m recurring contribution after 20% variable and 2,983 fixed |
| --- | --- | --- | --- |
| Conservative | 1 / 3 / 6 | 149 / 447 / 894 | **-2,268 EUR/month** |
| Base | 3 / 10 / 20 | 447 / 1,490 / 2,980 | **-599 EUR/month** |
| Optimistic | 7 / 20 / 40 | 1,043 / 2,980 / 5,960 | **+1,785 EUR/month** |

Break-even active firms = ceil(2,983 / (149 x 0.8)) = **26 companies** under the defined founder-time allocation. For a full-time 160 h/month founder at 35 EUR/h with the same 650 EUR other fixed costs, break-even = ceil(6,250 / 119.20) = **53 firms**. A solo developer at 20 clients is NOT automatically earning a sustainable wage. For a 249 EUR blended package and same 80% contribution, the part-time model requires **15 firms**; at 99 EUR it requires **38 firms**. VAT, receivables, tax, churn, implementation customizations and enterprise support are **not modeled**. Record actual monthly invoicing, cash collection, cancellations, onboarding hours and 30/90-day retention; do not project investor-grade ARR until cohort evidence exists.

Setup revenue scenario (NOT recurring): new paying projects per year x actually collected setup fee, minus actual onboarding hours x founder hourly cost and third-party expense. Discovery diagnostic unit economics: 450 EUR price example - 10 h founder x 35 EUR - 30 EUR direct cost = 70 EUR pre-tax contribution; at 15 h the same project loses 105 EUR. Therefore sell a strictly bounded deliverable, paid upfront, and measure time. Do not use license revenue to conceal unpaid customer-specific custom engineering.

### 9.8 Operator jobs, onboarding UX and falsifiable interviews

**No timed UX study has been performed.** Static code surfaces and older UX audit do not prove click/time costs. Record actual taps, task completion, source check, time and errors for these five jobs per role:
- Single-shop owner: daily revenue, returns, stranded stock, purchase priority, supplier check.
- 3–10-shop owner: compare shops, transfer candidates, excess capital, weekly exceptions, action follow-up.
- Buyer: style-size shortages, replenishment basis, supplier terms, reorder budget, late deliveries.
- Store lead: goods received, mismatches, POS returns, inventory differences, branch transfers.
- Bookkeeper/receiving clerk: XML parse, supplier identity, tax/discount check, physical quantity, archived receipt trace.
- Distributor: order/contract, delivered quantity, invoiced quantity, supplier price variance, disputes.

For each persona's 5 tasks use one observation sheet: entry page, source identity, expected outcome, **actual click count (NOT MEASURED)**, time (NOT MEASURED), error/empty/partial states, Excel/ERP alternative, whether operator can name next decision. Proposed nav: **Pregled -> Prodaja -> Zalihe -> Dobavljači -> Cena/Nivelacije -> Odluke -> Prijem dokumenata**, with technical Admin/Workers/Data Source status outside owner flow. Routes are already present; this is a **study hypothesis**, not authorization for global restructure. Only revise proven high-friction screen.

Highest-priority UX acceptance examples:
1. Klijent/clientapp/src/pages/AnalyticsDashboard.tsx: period/store/source freshness always visible; unknown cost must show NEDOVOLJNO PODATAKA rather than profit; export and detail use same scope. Test bad source, stale source and negative returns.
2. Klijent/clientapp/src/pages/DecisionPulsePage.tsx plus Actions: one clear next action, reason/disabled explanation, status, human owner; no invented gains; compare with current ERP/Excel.
3. Klijent/clientapp/src/pages/UnosRobePage.tsx and components/sef-xml-pilot/SefXmlReceiptDraftWizard.tsx: prominent **NEKNJIŽENO** in XML mode, explicit distinction from gated manual committed receipt, no password persisted or confidential XML logged; pending duplicate, unmatched, invalid unit/currency show blocking reasons. Verify at phone 375 px and desktop, named-operator denial and default OFF.
4. Klijent/clientapp/src/pages/HomePage.tsx: separate last recorded source sale horizon from last successful import and operational health; no 'fresh' label from status alone.

**Six buyer interviews, 5 prompts each (ask for real artifact or screen, not praise):**
- Current operator: last inventory decision? last invoice entry time? where cost came from? show same report in Access? would you approve a paid repeated run? Evidence: time baseline + scrubbed receipts/screen; stop if existing system wins.
- Unrelated independent shop: which weekly question takes longest? show last exported stock? who approves software? what was last paid addon? can we send fixed-price offer? Stop if neither painful nor budgeted.
- 3–10-shop owner: largest recent transfer/overstock? how located size gaps? who decides across stores? demonstrate ERP report? approve paid diagnostic with anonymized source? Stop if no accessible store/source grain.
- Distributor: show actual disputed invoice line? frequency and monetary impact? what PO/contract is authority? who resolves exception? permit paid bounded CSV comparison? Stop if no authoritative line evidence.
- Bookkeeper/receiving clerk: typical XML variant? actual count of manual edits? what physical receiving evidence exists? how are credits/PDV/rabates reconciled? which repeat step has monetary cost? Stop if protected data cannot be lawfully tested or current ERP already automates.
- ERP partner: what client reports take manual SQL? show allowed export/API? frequency of bespoke reports? liability/support split? pilot referral on a specific account? Stop if partner refuses integration rights or support boundary.

**Actual payment signal:** signed paid diagnostic, paid pilot PO/invoice, or written commitment with named budget owner, deliverable, deadline and acceptance. 'Interesting, call me later' = politeness, not demand. A refusal after observing a complete one-source workflow is an equally valuable falsification.

### 9.9 Ninety-day operating gates, not a new READY queue

| Deadline / owner | One deliverable | Go / stop criterion and dependency |
| --- | --- | --- |
| Days 1–14 / product owner | One genuine 20-minute operator value session on bounded source, including three business questions and current ERP/Excel alternative | 2 of 3 jobs independently completed and one decision supported or honestly blocked; session NOT RUN until observed |
| Days 1–21 / owner + approved provider operator | Exact deploy/source/refresh evidence and original 20 cost kalkulacija request; PR #163 lineage owner decision | No production currentness or true purchase margin without respective STAB16/RQ601 gates |
| Days 7–30 / operator + QDB code owner | 2–3 authorized original SEF XML samples (privately, never Git), manual-time baseline, XML preview/draft output, negative ambiguous case | No customer pass before tested; no stock write; QDB14 remains WAITING |
| Days 15–60 / product owner | 10 interviews outside incumbent, 3 bounded diagnostics offered, 2 concrete paid proposals | If 0 verifiable paid offers or all say incumbent sufficient, stop broad feature work |
| Days 45–90 / engineer after evidence gates | One small source-safe report/UX refinement with independent regression and access/revocation test | Do not create another feature family without observed blocker; WIP <=2 and owner queue controls READY |

A prospective client must receive signed data-processing scope, retention/export/delete rules, read-only import process, data-source mapping, error disclaimers and clear owner acceptance. **Do not upload real SEF invoice XML, Access data, secret keys, customer names, receipt screenshots or raw extracted purchase prices to this public repository.** The currently tracked legacy Access binary in repository root merits separate owner-led **privacy/classification review** before presenting the public repo to prospects; this document does not inspect or publish its contents or authorize file deletion.

### 9.10 Second-pass adversarial verdict and explicit no-go reasons

The buyer can decline because existing ERP already supports basic turnover, stock and repricing, Excel cheaply provides small one-off reports, cost numbers lack original receipts, or sharing sensitive sales data with a new provider is not justified. A full business-grade product would need measured secure onboarding, easy independent source reconciliation, a reasonable liability/retention agreement, real backup/deletion and operator proof. ERP incumbents could readily copy (1) weekly digest, (2) top slow-stock suggestions and (3) SEF XML import preview; these are **not durable moats**. More defendable but unproven would be cross-ERP source reconciliation, auditable source-quality lineage, and a validated footwear style-size-store action workflow.

An investor would still lack: verified segmented buyer counts, signed paid pilots, repeated 30/90-day usage, collection, gross margin, churn, average onboarding hours, customer ROI ground truth, security due diligence and specific partner distribution. A sole developer must keep one supported first customer and one narrow revenue wedge, not an ERP rewrite. **GO** to restricted operator validation + paid diagnostic discovery; **NO-GO** for full SaaS expansion until at least one independent paying buyer and repeatable source onboarding are proven. This is a decision gate, not a guarantee of profit.


### 9.11 Live read-only provider verification — 2026-10-17 (new independent evidence)

Unlike the dated local batch-23 audit above, this is a fresh **read-only** check through authorized connected Render/Neon account tooling. Provider URLs and query shape only; no customer payloads, names or secrets copied. No deploy, data write, SEF operation, import attempt or schema mutation was performed.

| Boundary | Observed on 2026-10-17 | Consequence |
| --- | --- | --- |
| Render production service | `trendplus-api` web service, https://dashboard.render.com/web/srv-d55c2tre5dus73c4bve0, `autoDeploy=no`/trigger off, latest LIVE manual deploy finished 2026-10-16 11:10 UTC at SHA **16bc5fc7124fc0ac6019edd00f45da1a930e8a24**; repository baseline current main **8239678f71eefc79fe5d5741364f3dfa043016cf** | Latest web deploy is not the inspected main head. Service being LIVE alone does not certify data freshness, source identity, SEF flag or frontend version |
| Render services / logs | Only a Trendplus web service appears in accessible Render service list; no separate dedicated Trendplus worker entry. Logs on 2026-10-16 22:12 UTC repeatedly show `ExecuteReader on access-import-queue` at ~2-second intervals | Polling is not evidence that an import or analytics refresh successfully completed; integrated worker registration/runtime cannot be inferred from a service list |
| Neon `trendplus` database, production branch | Read-only aggregate SQL: `AccessImportCursors` row count **0**; latest `DataImportBatches.Id` **49**, last status `completed` **#42** on **2026-08-12 10:32 UTC**, attempts #43–#49 **failed** with `IsIncremental=false`, `OverwriteExisting=true`; latest `prodaja_zaglavlje.datum_prodaje` **2026-08-05 00:00 UTC** | Safe incremental delta is NOT proven and current-sales recommendations are stale. Cross-database source identity and rollback acceptance in STAB17/PR #163 are prerequisites. Do not convert these reads into an unauthorized import |
| Neon separate `analytics` database | Read-only `SELECT count(*), max("FinishedAtUtc") FROM analytics_refresh_runs` returned **zero** rows and NULL latest refresh time | The queried analytics refresh-run table provides **no recorded durable refresh proof**. This is not by itself proof that every other analytical process never ran; reconcile the deployed runtime source/worker path in STAB16 |

Reproducible read-only query shapes:

```sql
SELECT (SELECT count(*) FROM "AccessImportCursors") AS cursors,
       (SELECT max("Id") FROM "DataImportBatches") AS latest_batch_id,
       (SELECT max("Id") FROM "DataImportBatches" WHERE lower("Status")='completed') AS last_completed_batch_id,
       (SELECT max(datum_prodaje) FROM prodaja_zaglavlje) AS last_sale_date;
SELECT "Id", "Status", "StartedAtUtc", "CompletedAtUtc", "IsIncremental", "OverwriteExisting"
FROM "DataImportBatches" ORDER BY "Id" DESC LIMIT 8;
SELECT count(*) AS refresh_run_count, max("FinishedAtUtc") AS last_refresh_finished
FROM analytics_refresh_runs;
```

Important: the Neon default **neondb** is an empty incidental database and is NOT the `trendplus` business database. Both named `trendplus` and `analytics` databases were queried explicitly. No customer row contents were accessed.

**Adversarial commercial change:** ranking above is still a cross-opportunity *subjective product* comparison, but **source recovery STAB16/STAB17 and PR #163 source-lineage safety outrank all discretionary engineering for any recurring 'this week' promise**. A bounded **historical as-of diagnostic** can still be offered when its exact scope reconciles; a recurring current-state decision subscription should be withheld. STAB16 remains BLOCKED and STAB17 WAITING per owning queues, not silently promoted. Escalation is to an authorized owner-approved provider/source-reconciliation workflow, not this docs-only PR.
