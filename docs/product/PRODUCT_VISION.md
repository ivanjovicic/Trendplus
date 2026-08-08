# Trendplus Product Vision

Updated: 2026-08-08  
Status: canonical product direction

## Why Trendplus exists

Trendplus exists to turn fragmented retail operational data into trustworthy, explainable weekly decisions. The product should help a retailer understand what deserves attention, why it deserves attention, what action is recommended, how confident the evidence is, and what happened after the action was taken.

The product is not successful when it merely shows more charts. It is successful when an operator can make a better decision with less manual reconciliation and can later audit whether that decision worked.

## Target users

Primary users:

- owner/managers of small and mid-sized retail businesses;
- commercial/category managers responsible for assortment, stock and suppliers;
- store/operations managers who need a short list of actions rather than raw analysis;
- analysts who need reliable data lineage, exports and drill-down evidence.

Secondary users:

- implementation/support teams onboarding a customer's data source;
- finance or management stakeholders reviewing performance and decision outcomes;
- future SaaS administrators operating multiple isolated customer environments.

## Problems solved

Trendplus should reduce these recurring problems:

1. Data is distributed across Access/databases/files and is difficult to validate consistently.
2. Managers see numbers but still have to decide manually what matters now.
3. Missing, stale or partial data is often visually indistinguishable from healthy data in typical reporting tools.
4. Recommendations are hard to trust when the reason, evidence, confidence and alternatives are hidden.
5. Actions are rarely linked back to measurable outcomes, so the organization cannot learn which recommendations work.
6. Growth from one pilot to many customers requires repeatable connectors, isolation, performance, observability and security rather than ad-hoc deployments.

## Competitive positioning

Trendplus is positioned between generic BI and heavyweight enterprise planning/AI platforms.

It should differentiate through:

- retail-specific decision workflows rather than generic dashboards;
- explicit freshness, quality, confidence and reason codes;
- deterministic recommendations and explainability before AI;
- evidence that follows the user from summary to drill-down, export and action;
- outcome tracking that closes the loop between recommendation and result;
- practical connectors for existing customer systems without replacing the customer's source database;
- a path from dedicated-customer deployment to shared SaaS without weakening tenant isolation.

## What Trendplus is NOT

Trendplus is not:

- a generic BI/dashboard builder;
- an ERP replacement;
- an accounting system;
- a customer-source database replacement;
- a free-form SQL console;
- a system that writes back to customer databases by default;
- an LLM-first product where AI invents business truth;
- a platform that labels missing evidence as zero, green, fresh, measured or confident;
- a shared multi-tenant deployment until tenant isolation gates are complete.

## Product principles

1. **No fake zero.** Missing evidence is not zero.
2. **No fake green.** Unknown, stale, partial or insufficient data is not healthy.
3. **Backend source of truth.** Recommendations, confidence, evidence semantics and decision state are authoritative server-side contracts.
4. **Explain the decision.** Important recommendations need a deterministic evidence chain and alternatives.
5. **Close the loop.** Recommendation -> action -> execution -> outcome -> learning should become a first-class product lifecycle.
6. **Portable ingestion, stable core.** Customer sources may vary; Trendplus internal domain/analytics storage remains controlled.
7. **Scale only with proof.** Performance, security, tenant isolation and observability are measured gates, not assumptions.
8. **AI is additive.** Core analytics and decisions remain usable when AI is disabled.

## Two-year product vision

### Horizon 1 - Trustworthy pilot and first customers

Trendplus becomes a dependable decision-support product for real retail operations:

- current CI/release blockers closed;
- analytics correctness and trust metadata stable;
- provider-neutral source connector contract established;
- deterministic Decision Explainability available for high-value recommendations;
- business/technical observability sufficient to operate a pilot;
- security and backup/restore evidence repeatable;
- first customer onboarding reproducible rather than bespoke.

### Horizon 2 - Repeatable multi-customer product

Trendplus expands from a pilot architecture to a scalable product:

- multiple connector providers and reusable mappings;
- staged tenant isolation completed for supported shared-SaaS surfaces;
- decision timeline and outcome measurement available across recommendation families;
- measured performance budgets for larger data volumes;
- SLA/SLI dashboards for imports, analytics and workers;
- onboarding and operations standardized for roughly 10 customers.

### Horizon 3 - Decision-learning SaaS

Trendplus becomes a decision-learning retail platform:

- recommendation outcomes feed deterministic statistics and confidence improvement;
- alternatives and evidence history are auditable;
- tenant-safe SaaS operation proven for larger customer counts;
- GenAI/RAG may provide cited natural-language exploration and explanation only after security/evaluation gates;
- AI remains downstream of authoritative Trendplus data, decisions and permissions.

## Product success measures

Roadmaps should increasingly measure outcomes such as:

- time from import to trusted decision availability;
- percentage of high-priority recommendations with complete evidence/explanation;
- recommendation acceptance/execution/outcome coverage;
- measurable business impact where attribution is defensible;
- onboarding lead time per customer/source;
- import/analytics/worker SLA attainment;
- support incidents caused by stale/incorrect/ambiguous data;
- performance and cost per customer/data volume tier.

Exact metric definitions belong to the OBS, DT and RL roadmaps rather than this vision document.