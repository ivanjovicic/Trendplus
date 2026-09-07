# Trendplus Business Roadmap

Updated: 2026-08-15
Status: canonical business milestone map

This roadmap maps commercial/product milestones to existing technical planning programs. It does not create a second implementation backlog.

## Milestone 1 - Pilot Ready

### Business outcome

A real retailer can use Trendplus for a bounded pilot without the team overstating deploy, data, recommendation or recovery readiness.

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

## Milestone 2 - First Customer

### Business outcome

Onboarding the first paying/production customer is repeatable enough that the product is not dependent on undocumented developer knowledge.

### Required evidence

- source connection/import contract documented and tested;
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
