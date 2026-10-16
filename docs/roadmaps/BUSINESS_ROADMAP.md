# Trendplus Business Roadmap

Updated: 2026-08-15 (milestone structure); product focus reaffirmed 2026-10-14
Status: canonical business milestone map

This roadmap maps commercial/product milestones to existing technical planning programs. It does not create a second implementation backlog.

Latest user-value prioritization and one-operator validation plan: [business progress and focus, 14.10.2026](../product/TRENDPLUS_BUSINESS_PROGRESS_AND_FOCUS_2026-10-14.md). Use it for business priorities; this roadmap remains the milestone owner; the RQ queue owns task claims.

**Current product proof correction (2026-10-14):** [analytics-first scope](../product/ANALYTICS_FIRST_SCOPE_2026-10-14.md) and [operator/customer playbook](../product/RETAIL_ANALYTICS_PRODUCT_PLAYBOOK_2026-10-14.md) are the current product lens. Do not infer pilot/commercial readiness from completed test infrastructure, old dated "live" reports or a successful Access line reconciliation. `RQ585` digest is already DONE; its adoption and deployed currency remain unproven. Product release must separately demonstrate: **historically correct -> current trustworthy -> operator-used -> outcome measured -> commercially valuable**. A gap in one step cannot be disguised by more screens.

Before calling Milestone 1/2 customer-ready, show one actual operator can (a) select a store and bounded period, (b) reconcile sold units/revenue to source, (c) distinguish unknown/estimated cost and historical/current stock, (d) understand source/date and why a recommendation is blocked, (e) review the existing digest, take or decline one valid action and follow evidence into detail/export. If an outcome is not mature/controlled, record `not_measured`, not ROI. One-deployment-per-customer remains acceptable for the first validated pilot. Pricing, sales conversion and customer demand are **unverified hypotheses** until customer evidence is recorded.

External Amazon/eBay/Shopping/trend experiments and test-feature UI are out of current product scope; legitimate source mapping/import needed for a paying retailer is **not** automatically out of scope.


## Commercial validation overlay — 2026-10-15 (hypothesis, no new milestone)

The [Retail ProfitGuard market feasibility and scenario analysis](../product/RETAIL_PROFITGUARD_MARKET_FEASIBILITY_2026-10-15.md) supplies a *testable* commercial lens, not a queue or a completion claim. Prioritize an actual operator session plus a paid, bounded, historical retail diagnostic **before** recurring SaaS packaging; count separately: source facts, operator decisions, paid pilots, repeat business and measured outcomes. The 0/3/12/35-customer projections are conditional exit-MRR scenarios, **not** forecasts supported by paid-customer data. Invoice/contract discrepancy control remains a distinct discovery candidate, not a shipped supplier feature. Do not use broad Serbian trade totals as the addressable footwear market or shared multi-tenancy as a first-customer prerequisite.

## Milestone 0 - Decision Truth + Currentness

### Business outcome

Trendplus can make useful, reproducible decisions from the latest trustworthy imported horizon **and** can separately prove when those decisions are current. Product-value work and production-freshness recovery run in parallel rather than serially.

### Track A — product truth/value

Required before calling the core decision product internally useful:

- financial aggregates preserve unknown/partial money instead of turning missing values into zero (RQ593);
- Inventory capital/action semantics distinguish valuation coverage, receipt age, sales recency and demand (RQ594);
- one canonical retail markdown action population exists (RQ555);
- Supplier negotiation/value view uses only authoritative revenue/margin/inventory facts (RQ595);
- the owner digest can run on the latest trustworthy horizon with explicit `asOf` semantics (RQ585);
- RQ556 may compare shadow-v9 ranking on historical data without activating new policy.

This track may use existing historical imports, deterministic fixtures and certified bounded windows. It does **not** wait for a fresh import unless the tested claim is specifically about current production state.

### Track B — currentness/operations

Required before calling recommendations "today/current/this week" or starting a prospective outcome pilot:

- production observed sales horizon stays within the RQ583 freshness SLA;
- canonical web/worker/deploy ownership is known and durable successful import/refresh evidence is visible;
- deployed Pre/Post/schema readiness is verified where customer-facing current behavior depends on it;
- operational monitoring can detect failed import/refresh/worker execution.

### Queue mapping

- RQ593: financial unknown/coverage correctness;
- RQ594: Inventory action decision truth;
- RQ555/RQ556: canonical markdown source + shadow next-model evidence;
- RQ595/RQ530: Supplier value/negotiation evidence;
- RQ585: latest-horizon owner digest;
- STAB16/RQ545/RQ454/RQ565/RQ566: production currentness/deployed reconciliation;
- RQ592: prospective markdown action-to-outcome pilot after currentness is restored.

### Exit rule

Track A can progress independently and should not be replaced by more audit/documentation work. **Pilot/current-production claims require both tracks**, but stale production data does not invalidate already-proven historical formulas or bounded analytical value.

## Milestone 1 - Pilot Ready

### Business outcome

A real retailer can use Trendplus for a bounded pilot after Milestone 0, without the team overstating deploy, data, recommendation, outcome or recovery readiness.

### Required evidence

- current backend/frontend release evidence is tied to the deployed SHA/bundle;
- backend CI reaches and passes the required suite for the pilot surface;
- priority analytics correctness gates are resolved or explicitly accepted with visible warnings;
- import freshness/provenance is truthful;
- admin/security boundaries are appropriate for the pilot mode;
- backup/restore rehearsal is usable;
- pilot operator can see why a recommendation exists and whether evidence is stale/partial/unknown;
- every sold recommendation family states its decision grain, scope, units, key exclusions and action limitation rather than relying on dashboard interpretation;
- operational monitoring can detect a failed import, failed refresh and failed worker.

### Queue mapping

- BCI: backend CI truth and real assertion failures;
- STAB: deploy/release/security/recovery gate;
- RQ: analytics correctness and trust semantics;
- OBS: pilot SLI/health vocabulary;
- DEX: deterministic explainability planning for the highest-value recommendations.

### Exit rule

The current STAB/release evidence must say Pilot Ready or Pilot Ready With Accepted Warnings. Old historical readiness documents do not satisfy this milestone.

## Milestone 2 - First External Customer

### Priority note

Do not require shared multi-tenancy or GenAI for this milestone. A dedicated database/deployment per customer is acceptable if it reduces onboarding risk and preserves isolation. The commercial blocker is repeatable mapping into the canonical retail/analytics model, not the existence of another provider driver.


### Business outcome

Onboarding the first paying/production customer is repeatable enough that the product is not dependent on undocumented developer knowledge.

### Required evidence

- source connection/import contract documented and tested end-to-end into the canonical retail/analytics model (not staging only);
- customer mapping/onboarding procedure documented;
- dedicated-customer deployment/isolation model explicit;
- data-quality/freshness and support triage visible;
- import/analytics operational monitoring sufficient for support;
- security responsibilities and credential handling documented;
- deterministic explanation available or planned for the recommendation families sold as decision support;
- the supported source mapping makes product/SKU, variant, store, supplier, price/cost and time context explicit enough for the analytics actually sold;
- the customer can distinguish observed inventory history from any reconstructed/proxy history in high-impact stock, availability and lost-sales views;
- basic support/rollback/recovery runbook complete.

### Queue mapping

- QDB: provider-neutral connector contract and mapping foundations;
- STAB/SEC: safe operational boundary and recovery;
- OBS: customer-operability metrics;
- PERF: first representative production baseline;
- DEX: explainability contract and rollout plan.

## Milestone 3 - 10 Customers

### Business outcome

Trendplus can onboard and operate around ten customers without each customer becoming a bespoke engineering project.

### Required evidence

- reusable connector/mapping model for common customer sources;
- onboarding checklist and migration/version procedure;
- customer isolation model is explicit for every data/cache/file/job surface;
- tenant work required for the chosen deployment topology is complete;
- representative performance/data-volume budgets exist;
- import, analytics and worker SLIs are monitored;
- incidents can be correlated to the correct customer/source/job;
- decision/action/outcome data model is ready for reusable product learning.
- data-quality coverage shows which sources/products/stores are excluded from a decision rather than treating incomplete mapping as a normal result.

### Queue mapping

- QDB: connector/provider/mapping progression;
- MT: staged tenant ownership/isolation;
- PERF: data-volume and concurrency baselines;
- OBS: import/analytics/worker SLI dashboards;
- SEC: post-STAB security evolution;
- DT/RL: decision history and outcome-learning contracts.

## Milestone 4 - 50 Customers

### Business outcome

Trendplus has a scalable operating model with predictable performance, support signals and customer isolation suitable for broader SaaS operation.

### Required evidence

- shared-SaaS surfaces, if used, satisfy the full tenant release gate;
- per-customer resource envelope and large-dataset behavior are measured;
- operational SLOs and incident ownership exist;
- connector onboarding is standardized;
- security reviews cover tenant boundaries, secrets, privileged operations and auditability;
- decision timeline/outcome statistics are reliable enough for product-level learning;
- predictive or planning claims, if sold, are backed by a declared baseline, backtest population, uncertainty and measured outcome rather than a point forecast alone;
- support can distinguish product defects, source-data defects and customer configuration defects quickly.

### Queue mapping

- MT: shared-SaaS release gate;
- PERF: scalability/concurrency/worker throughput;
- OBS: SLO/error-budget evidence where adopted;
- SEC: tenant-aware operational security;
- QDB: connector scale/operations;
- DT/RL/DEX: auditable and measurable decision intelligence.

## Milestone 5 - SaaS Ready

### Business outcome

Trendplus can be sold/operated as a repeatable SaaS product for the supported topology rather than as a collection of dedicated pilot deployments.

### Required evidence

- tenant identity, persistence, caches, workers, storage, reports and exports are isolated for every shared surface;
- tenant-owned connector profiles/mappings/checkpoints are enforced;
- provisioning/offboarding/lifecycle procedures exist;
- performance and cost envelopes are understood;
- operational SLOs, alerting and incident response are in place;
- security and audit controls match the supported customer model;
- migrations/rollback/backup/restore are repeatable across supported environments.

### Queue mapping

- MT is the primary gate;
- QDB must be tenant-owned for persistent connector configuration;
- PERF/OBS/SEC are mandatory operational gates;
- STAB remains the release-evidence owner.

## Milestone 6 - AI Ready

### Business outcome

Trendplus can add customer-facing GenAI features without making AI a dependency of core analytics or weakening data/security truth.

### Required evidence

- core Pilot/SaaS readiness appropriate to deployment mode is already proven;
- GenAI security/data-boundary audit is current;
- retrieval/tool access is tenant/user authorized;
- evaluation/citation/grounding policy is defined;
- provider secrets and cost controls are safe;
- core product works when AI is disabled;
- deterministic Decision Intelligence remains the authoritative source for decisions, confidence and evidence;
- AI output cannot turn unknown/stale/partial data into confident prose.

### Queue mapping

- GAI: AI product/evaluation/security runtime;
- MT/SEC: tenant and authorization boundaries;
- OBS/PERF: AI latency/cost/trace evidence if enabled;
- DEX: authoritative explanation/evidence layer AI may summarize but not replace.

## Milestone governance

- Business milestones do not automatically promote queue prompts to READY.
- Each milestone consumes evidence from the canonical owner queues in `MASTER_ROADMAP.md`.
- A milestone cannot be marked complete from docs-only assumptions where live/runtime evidence is required.
- Warnings must be explicit, owned and visible; they are not silently converted to PASS.
- Commercial material, demos and customer-facing documentation must not describe a metric as a forecast, availability, lost-sales estimate, optimization or recommendation beyond the evidence its owner contract supports.
- A new analytical capability earns a customer-facing claim only after its grain, provenance, coverage, freshness, decision constraint and validation outcome are documented in the owning program.


## Product-commercial execution overlay — 2026-10-17 (NOT a second queue)

Canonical evidence and competitive/pricing calculation: [Retail ProfitGuard market feasibility, section 9](../product/RETAIL_PROFITGUARD_MARKET_FEASIBILITY_2026-10-15.md). Source baseline main 8239678f71eefc79fe5d5741364f3dfa043016cf. Market validation, operator interviews, willingness to pay, time-to-task and actual recurring revenue remain NOT VERIFIED.

**Policy:** one commercial wedge, dated read-only retail diagnostic using existing analytics and separately one strictly unposted SEF XML operator study, before any recurring subscription or broad ERP integration investment. Maintain the authorized analytics-first product vision; do not convert research to engineering claims or resume eBay/Amazon/Google Shopping/AI model training.

| Window | Business owner proof | Technical dependency (status remains in owning queue) | Hold/stop |
| --- | --- | --- | --- |
| NOW (0–14d) | Complete the existing 20-min operator business session; compare 3 decisions to POS/Excel. Seek source-proven 20 original cost calculations | Existing RQ585 DONE; RQ601 PARTIAL; RQ680 IN_PROGRESS; no new READY | Without operator results, no new dashboard tasks |
| NOW (0–30d) | Run private 2–3-invoice SEF preview/draft timing trial, show 100% line accounting and blocked ambiguous matches | QDB11–QDB13 DONE; PR #184 merged with goods-receipt writes disabled by default; QDB14 WAITING; PR #185 open exact-head CI | No posting, Access writes, or production flag changes |
| NEXT (15–60d) | Offer 3 bounded paid historical diagnostics to independently qualified stores; record cash/budget decision | STAB16 BLOCKED deployed refresh, STAB17 WAITING incremental identity, PR #163 open draft, RQ610 BLOCKED stock history | Label source horizon and unknown cost; no current inventory or saved-profit promise |
| VALIDATE (30–90d) | Ten interviews, two named paid proposals, one accepted independent paid pilot target; measure onboarding hours | Compare against BizniSoft/Minimax/PANTHEON existing reports; only one small reproducible user-blocking UX repair | Halt if existing ERP/Excel meets job cheaper |
| LATER | Invoice/PO/receipt Guard, size/run/store transfers, partner OEM, region expansion | Need original sources, stock generation, licensed integration, legal review, paying customer | Not new implementation READY |
| DROP now | Generic ERP, new POS, generic forecasting, automatic price/transfer execution, marketplace scrapers and model-training | Out of owner-approved commercial lane | No implementation |

Engineering envelope: **200 hours for 90 days** split 40 truth/safety, 35 XML pilot fixes and tests, 30 diagnostic report, 25 proved UX friction, 25 second-source proof contingent on prospect, 25 security/onboarding, 20 contingency. Total 200; unused contingent hours move to existing safe P0/P1 family, not speculative product scope. Owner discovery/sales are separate. Pricing 300–600 EUR diagnostic and 99–249 EUR/month subscription is TESTABLE HYPOTHESIS, not approved published offer. Break-even model 26 companies at 149 EUR blended/month, 80% contribution and 2,983 EUR fixed/month *including founder opportunity cost*; exact formulas, scenario range and competitor links are in the research owner, not copied here.

**No new READY task created from this study.** Relevant already-owned entries: STAB16 BLOCKED; STAB17 WAITING; RQ601 PARTIAL; RQ610 BLOCKED; QDB14 WAITING; RQ680 IN_PROGRESS; RQ682 PARTIAL; PR #163 open DRAFT and #185 open. Respect independently claimed paths and WIP <= 2. Do not mutate statuses from a product research task.
