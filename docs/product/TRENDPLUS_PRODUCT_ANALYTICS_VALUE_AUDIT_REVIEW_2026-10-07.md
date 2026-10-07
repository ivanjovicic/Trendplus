# Trendplus product / analytics audit — claim review and owner actions (2026-10-07)

Status: current product-direction correction; does not replace live queue routing  
Reviewed base: `origin/main=016ea46fbca87bccc49e562e15df75367e01c06e` before this correction commit  
Primary audited document: `docs/product/TRENDPLUS_PRODUCT_ANALYTICS_VALUE_AUDIT_2026-10-07.md`

## Verdict

The original audit got the **main product diagnosis right**: current value is blocked more by freshness, source quality and missing outcome proof than by basic arithmetic. Its strongest recommendation — focus on inventory capital, markdown outcome and supplier value instead of adding more dashboards/AI/governance — should stand.

It also contained several overclaims. The most important corrections are:

1. `workersEnabled=false` describes the web process; it does not prove a separate worker service is absent.
2. Trendplus is not “Access-only” at architecture level: QDB already proves SQL Server discovery/mapping/checkpoint application into staging. The commercial gap is staging -> canonical retail facts -> repeatable onboarding.
3. Missing category/pol does not universally invalidate Product Decision after RQ574; `TipObuce` is the approved authoritative dimension where applicable.
4. “79% governance commits” was a rough audit classification, not a durable repository KPI.
5. Store-count, inventory-value, pricing and ROI thresholds are hypotheses.
6. Public competitor evidence supports “core reporting is commodity”; it does **not** support “no local competitor has X”. Global vendors already market markdown optimization.

## Evidence labels

| Label | Meaning |
|---|---|
| VERIFIED | current-main code/test or dated live evidence directly supports the statement |
| SUPPORTED JUDGMENT | evidence supports the product conclusion but it is not itself a measured fact |
| HYPOTHESIS | must be tested through customer interviews/pilot/outcomes |
| CORRECTED | original statement was materially too strong or wrong |
| UNPROVEN | no current evidence is sufficient |

## Prompt-by-prompt audit coverage

| Requested audit dimension | Review result |
|---|---|
| Product/business value | VERIFIED that descriptive core overlaps existing retail systems; hero-value thesis is SUPPORTED JUDGMENT |
| Correctness | Strongest area: core sales/oracle evidence is real; production/live coverage remains incomplete |
| Evidence / truth ladder | Correct framing retained; dated live observations are now explicitly dated instead of projected onto current main |
| Data completeness | VERIFIED serious gaps for color/category/pol/payment/time; corrected to acknowledge TipObuce/RQ574 |
| Freshness | VERIFIED primary blocker; separate worker existence remains UNPROVEN |
| Cross-screen consistency | Strong for the certified July window; not equivalent to all-period certification |
| Runtime production proof | Weak relative to local proof; STAB16/RQ545/RQ454/RQ565 remain material |
| Decision usefulness | Low today; high-value surfaces are mostly unvalidated |
| Outcome proof | VERIFIED evidence gap: last explicit Actions live proof had measuredSampleSize=0 |
| UX | Improved substantially; no longer the highest-value investment |
| Differentiation | CORRECTED: not “features nobody has”; thesis is local explainable workflow + outcome evidence |
| Time-to-value/onboarding | CORRECTED: SQL Server foundation exists, but canonical customer onboarding is incomplete |
| Maintainability/governance | SUPPORTED JUDGMENT: excessive process volume; preserve only high-value guardrails |
| Documentation | Product docs needed priority reordering; corrected in this change |
| Commercial readiness | Internal pilot after freshness; external sale as optimization remains UNPROVEN |
| Short/medium/long plan | Retained, with scale/AI moved behind first measured value and external pilot |

## Current product score — reviewed

These are owner/product judgments, not scientific scores:

- current product value: **3/10**
- analytics trust: **5/10 overall** (stronger locally than deployed/current-data proof)
- business outcome proof: **0/10**
- commercial readiness: **2/10**
- potential after focused execution: **7/10 hypothesis**

The score itself is less important than the asymmetry: **high-confidence capabilities are mostly table stakes; high-value capabilities have low outcome confidence.**

## What to do now

### P0 — restore current production truth

Owner: STAB16 / provider access.

Done means:
- exact deployed SHA known;
- worker/deploy topology known;
- durable successful import/refresh visible;
- observed sales horizon inside RQ583 SLA;
- no silent schema/object drift;
- bounded source-to-endpoint reconciliation recorded.

Do not add another trust layer while this is unresolved.

### P1 — weekly owner decision surface (RQ585)

Owner decision in this review: P3 -> P1.

Why:
- RQ570/RQ573/RQ574/RQ576 are already DONE;
- real remaining start gate is freshness;
- one bounded owner worklist has more product value than another Board/Pulse layer.

Constraints:
- compose existing certified signals only;
- size/replenishment is optional until RQ559/source proof;
- do not manufacture ten rows;
- record owner disposition for four weekly cycles.

### P1 — first measured markdown pilot (RQ592)

Change:
- pre-registration starts immediately after freshness + live Pre/Post are restored;
- do **not** wait 14 fresh days before defining the cohort;
- freshness stability is required through the measurement window.

Result language remains “observed outcome” until the causal gate is satisfied.

### P1/P2 — inventory capital truth

No new screen first. Prove:
- inbound date;
- cost lineage;
- coverage;
- reconciliation against source/accounting sample;
- aged-stock RSD by supplier/store.

Only then make “capital tied in slow stock” a hero metric.

### P2 — second-customer ingestion (QDB10)

QDB09 ends at `SourceSyncAppliedRows`. QDB10 is registered to own the missing non-Access staging -> canonical retail/analytics path after QDB07/QDB08 and external-pilot source choice.

This is important, but it should not outrank first-value proof for the existing pilot.

## What to defer

- new GenAI product work;
- shared-SaaS multi-tenancy beyond actual second-customer need;
- more certification work on quarantined Advanced/Insight routes;
- new statistical complexity without sufficient mature/control sample;
- additional “executive” decision surfaces before RQ585 proves a simpler weekly workflow;
- presentation polish that does not remove a material usability blocker.

## Competitor reality check

Reviewed public pages on 2026-10-07:

- Logosoft SmartPOS/POSitiv publicly describe footwear/apparel dimensions, stock, margin/sales/no-sale reports, ordering and nivelation workflows.
- TiramisuPOS publicly describes size/color stock, multi-store inventory, advanced sales analytics, discounts and shift reporting.
- SVEra publicly describes inventory movement/reporting and marketing-action effect tracking.
- Konty publicly offers sales/inventory analysis and low-price SMB packages.
- Lightspeed explicitly targets shoe retail with variant inventory and retail insights.
- Retalon explicitly markets promotion-impact and markdown optimization.

Therefore:
- “supplier/type/color/daily sales dashboard” is not a moat;
- “markdown optimization” as a phrase is not unique globally;
- the plausible Trendplus wedge is a **simpler regional merchandising decision layer** on top of existing POS/ERP, with local semantics, transparent evidence and a customer-specific outcome history;
- whether local buyers will pay for that is still a hypothesis.

## Documentation changes made by this review

- corrected the dated product/value audit;
- changed `PRODUCT_VISION.md` to make first measured value the near-term product focus;
- added Milestone 0 to `BUSINESS_ROADMAP.md`;
- corrected README positioning and connector status;
- applied RQ585 P1 and repaired RQ592 gating in the canonical RQ queue;
- registered QDB10 for the real non-Access canonical-ingestion residual;
- added a concise product-priority overlay to `MASTER_ROADMAP.md`.

## Final product thesis

Trendplus should not try to win as another POS, generic BI suite, AI copilot or enterprise forecasting platform.

The best near-term thesis is:

> Turn existing retail/POS data into a short, explainable weekly list of inventory/markdown/supplier decisions, then prove what happened after the owner acted.

The first commercial milestone is not “more analytics”. It is **fresh data + trustworthy inventory capital + one closed markdown outcome loop + repeated owner use**.

## Single next milestone

**First proven dinar, without causal overclaim:** current data, at least 20 pre-registered real markdown actions, four-week observed outcomes, reproducible RSD/units/remaining-stock evidence, and owner-readable limitations.
