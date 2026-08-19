# Trendplus Retail Analytics Competitive Gap Audit

Date: 2026-08-12
Repository: `ivanjovicic/Trendplus`
Status: current product/roadmap audit; planning evidence, not a new execution queue

## Purpose

This audit compares the current Trendplus direction and codebase with capabilities commonly exposed by modern BI, decision-intelligence, retail forecasting and replenishment products. It is intentionally not a feature-parity checklist.

The product goal remains narrower and more commercially useful for the first customer:

`customer data -> trustworthy retail signals -> prioritized decisions -> explanation -> action -> outcome`

Trendplus should not try to become a generic Power BI/Tableau/Qlik clone or a full Oracle/Blue Yonder/RELEX planning suite before it has repeatable paying-customer evidence.

## Market reference snapshot

Official product documentation reviewed for the comparison includes these capability classes:

- Power BI / Fabric: semantic reporting plus threshold/condition alerts and workflow triggers;
- Tableau Pulse: followed metrics, threshold/trend alerts, email/Slack digests, guided metric exploration and forecast ranges;
- Qlik Insight Advisor / Alerting: generated analysis, anomaly/change-point analysis, natural-language exploration and configurable data alerts;
- SAP Analytics Cloud: BI plus planning, versions, predictive scenarios and what-if/simulation workflows;
- Oracle Retail Inventory Planning Optimization: retail-specific demand forecasting, exception management, lifecycle allocation and replenishment;
- RELEX / Blue Yonder: retail forecasting, replenishment, root-cause/exception workflows, planner prioritization and execution-oriented inventory decisions.

These products are references for capability maturity, not target scope for one-to-one replication.

## Current Trendplus position

### Strong / differentiated now

#### 1. Data trust and semantic honesty

Trendplus has unusually explicit guardrails for a smaller retail product:

- backend is the source of truth;
- no fake zero;
- no fake green;
- missing, stale, partial, fallback and insufficient evidence remain visible;
- recommendation/confidence semantics are not supposed to be invented by the frontend;
- inventory snapshot contracts now preserve true zero separately from missing evidence and expose truncation truth.

This is commercially important. Generic BI products are excellent at visualization and semantic modeling, but Trendplus can differentiate by making every operational recommendation carry visible trust state.

#### 2. Decision workflow, not dashboard-only analytics

The product already contains more than dashboards:

- Product Decision Center;
- Executive Decision Board;
- inventory risk and action workflow;
- supplier decision surfaces;
- reason codes and confidence;
- action/outcome lifecycle work;
- Decision Timeline work;
- Decision Explainability work including Why/tree/evidence/alternatives in the first family.

The direction `signal -> recommendation -> action -> outcome -> learning` is correct and should remain the product north star.

#### 3. Retail intelligence layer

`analytics_intel` already provides versioned semantic views/materialized caches for:

- product demand signals;
- inventory risk;
- price intelligence;
- trend momentum.

This is a good foundation because expensive signal logic stays close to the data while user surfaces consume stable read contracts.

#### 4. Inventory decision breadth

Trendplus already has meaningful pieces that earlier planning discussions treated as future ideas:

- ABC inventory classification;
- aging/capital-locked views;
- inventory alerts feed;
- forecast snapshot surface;
- rebalance suggestions;
- size-curve signals;
- store comparison;
- report schedules and email delivery;
- replenish/transfer/hold/markdown-oriented workflow support.

Do not create duplicate roadmap work for these capabilities. Future work should increase reliability, depth and productization.

#### 5. Connector architecture direction

The source connector architecture is sound:

- PostgreSQL remains the Trendplus internal store;
- customer systems are read-only sources;
- provider-neutral source contracts are being extracted from the Access implementation;
- mapping, schema drift, checkpoints and idempotency have explicit boundaries;
- Access remains supported while SQL Server is the first planned non-Access proof.

This is a better path than making the internal EF Core model multi-provider.

## Gaps that need only limited additional work

### Cross-family explainability

Product Decision Center is the reference implementation. The next useful step is not another explanation UI concept; it is proving that inventory, supplier and executive decisions can reuse the same evidence vocabulary without local semantics drift. This is already owned by `DEX11`.

### Recommendation learning projections

The lifecycle and eligibility foundation exists. The next step should remain measurement-only statistics and not adaptive recommendation mutation. `RL05` is correctly scoped.

### Decision retrospective/export

Decision Timeline exists far enough that the next gap is a retrospective/report contract rather than a new timeline model. `DT06` is correctly scoped.

### Operational evidence

PERF/OBS are now measuring rather than merely planning. Finish cold-start and worker-SLA evidence before adding broad observability infrastructure. Avoid buying or introducing a large telemetry stack until measured gaps justify it.

## Gaps that need substantial work

### 1. Release truth is still the first blocker

The latest inspected backend GitHub Actions run still restores and builds successfully but fails in the complete backend test step. A locally green suite is useful evidence but does not replace a green repository gate.

Until the backend gate and current release evidence are green, Trendplus should not be described as sale-ready even if individual analytics surfaces are strong.

Priority: highest operational priority, but this is repair/evidence work rather than a new product feature.

### 2. Real heterogeneous-source support

The abstraction work is good, but adaptability is not commercially proven until at least one non-Access source runs end to end.

The existing QDB order is correct:

1. SQL Server real connector proof;
2. safe named source/discovery;
3. deterministic mapping and preview;
4. durable idempotent checkpoints;
5. controlled admin connector experience.

This is one of the largest gaps between a good demo and a product that can be installed against different customers' POS/ERP/database environments.

### 3. Persisted historical inventory foundation

The intelligence documentation explicitly states that Trendplus does not yet have a canonical persisted daily stock snapshot and reconstructs historical inventory risk from current stock, sales and movements.

This limits the quality of:

- historical stock position;
- true OOS duration;
- average inventory;
- sell-through denominator quality;
- GMROI;
- stock-turn validation;
- lost-sales estimates;
- forecast backtesting;
- replenishment evaluation.

A durable SKU/store/day inventory snapshot (variant-aware where source quality permits) is the most important analytics data-foundation gap after release/connectors.

Do not silently backfill fake historical stock. Historical reconstruction must carry provenance and confidence distinct from observed snapshots.

A bounded first slice of that foundation now exists as the observed daily snapshot contract in `analytics_intel.vw_inventory_snapshot_foundation_v1`, but store-aware historical persistence is still not solved and should not be overclaimed as a full warehouse history.

### 4. Forecasting is a surface before it is a proven forecasting product

The current runtime has a forecast snapshot read contract with 7/14/28-day values, OOS probability, overstock risk, confidence and explanation. The inspected repository search did not surface a corresponding production materializer/model owner for `analytics_inventory_forecast_snapshot`.

Treat the current capability as a forecast signal/snapshot contract until end-to-end production generation is explicitly proven.

A serious forecast program later needs:

- deterministic baseline models first;
- train/evaluation windows;
- backtesting by SKU/store/category;
- MAE/WAPE/bias or equivalent retail-appropriate error metrics;
- confidence/uncertainty range, not only point values;
- sparse/intermittent-demand handling;
- seasonality and promotion/markdown awareness only when the inputs are trustworthy;
- new-item fallback/pooling policy;
- model-versus-naive-baseline comparison;
- drift and stale-model detection;
- documented explanation of what the forecast may and may not be used for.

Do not jump directly to ML because large retail suites use ML.

### 5. User-facing exception subscription / Decision Pulse

Trendplus has inventory alerts and scheduled report/email delivery, but no inspected evidence of a general user-configurable metric/decision subscription layer comparable to modern metric-pulse/alert products.

This is a high-value product gap because it changes the workflow from "open Trendplus and inspect dashboards" to "Trendplus tells me the few changes that need attention".

Recommended first version after current gates:

- follow a scoped decision/metric family (store, supplier, category, inventory risk);
- daily/weekly digest using existing delivery infrastructure;
- threshold and adverse-trend conditions from authoritative backend metrics;
- top N exceptions ranked by business impact and confidence;
- include freshness/quality and a short deterministic Why summary;
- deep link to the owning Trendplus decision surface;
- deduplicate repeated unchanged alerts;
- suppress notification when evidence is stale/invalid rather than sending fake certainty.

Do not start with a generic rule DSL, arbitrary SQL, Slack/Teams integrations or dozens of channels. Email plus in-app feed is enough for the first customer.

### 6. Scenario / what-if planning

The current roadmap correctly describes scenario planning as later work, and the markdown audit confirms there is no standalone markdown optimizer/simulator yet.

A narrow first simulator should be preferred over a generic planning engine:

- one SKU/category decision;
- fixed scenarios such as no-change / 5% / 10% / 15% markdown or conservative/base/aggressive replenishment;
- explicit assumptions;
- ranges rather than fake exact results;
- no automatic write-back;
- compare expected margin, stock release, OOS/overstock risk and confidence;
- store the scenario as evidence only if a later action is created.

This should follow historical stock and forecast/outcome validation, not precede them.

## Useful but lower-priority gaps

### XYZ / demand-variability segmentation

ABC already exists. XYZ-style demand variability/stability classification was not found as a runtime capability. It can be useful for replenishment policy and forecast method selection, but it is not more important than historical stock, connector proof or Decision Pulse.

### Generic anomaly detection

Qlik-style spike/change-point analytics are useful, but Trendplus should prefer retail-specific exceptions first (OOS risk, margin collapse, dead stock, supplier deterioration, unusual markdown outcome). A generic anomaly engine becomes valuable only when operators repeatedly need anomalies outside existing decision domains.

### Basket/customer segmentation

Basket analysis, RFM/customer segments and loyalty analytics should remain conditional on customer-level/transaction-level data availability and commercial demand. They should not be added merely for BI feature parity.

### Natural-language analytics / AI

Qlik, Tableau, Microsoft and other platforms increasingly expose conversational analytics. Trendplus already has a GenAI plan, but keeping it blocked behind the core release/data/security gates is correct. Deterministic decision evidence should remain authoritative even after AI is added.

## What Trendplus should explicitly NOT chase now

- generic drag-and-drop dashboard authoring;
- a full self-service semantic modeling studio;
- arbitrary user SQL;
- write-back into customer ERP/POS systems by default;
- multi-echelon enterprise replenishment before the first real use case requires it;
- broad ML forecasting before historical stock and backtesting exist;
- shared multi-tenant SaaS before dedicated-deployment pilot evidence;
- chatbot-first positioning;
- copying every BI visualization type.

These would consume substantial scope while weakening the current differentiation.

## Recommended capability sequence

### Gate 0 - make the current product truthfully releasable

- close backend CI/GHA red state;
- refresh exact-SHA release/smoke evidence;
- finish active analytics correctness/security blockers only when evidence proves them.

### Gate 1 - make first-customer integration repeatable

- QDB03 SQL Server proof;
- QDB04-QDB07 safe discovery/mapping/checkpoint/admin flow;
- onboarding mapping templates and import diagnostics;
- remain one-customer/one-deployment where appropriate.

### Gate 2 - strengthen the analytics moat

- canonical observed daily inventory snapshot foundation;
- DEX11 cross-family explainability readiness;
- RL05 measurement-only learning statistics;
- DT06 retrospective/export contract;
- Decision Pulse / exception digest contract and narrow MVP.

### Gate 3 - prove predictive value

- forecasting baseline and backtesting contract;
- forecast generation/materialization ownership;
- uncertainty/calibration and forecast scorecard;
- replenishment quantity evaluation against observed outcomes;
- XYZ variability as a supporting policy signal if useful.

### Gate 4 - add controlled scenario planning

- narrow markdown or replenishment what-if simulator;
- compare scenarios against measured historical behavior;
- only then consider richer price elasticity/optimization.

### Gate 5 - scale the platform

- additional connectors based on customer demand;
- multi-tenant/shared-SaaS work only after isolation gates;
- GenAI only after authoritative evidence/tool/evaluation boundaries are proven.

## Roadmap verdict

The overall Trendplus direction is good and should not be reset.

The strongest strategic choice is moving away from generic analytics toward an explainable retail decision system. The main risk is now not a lack of ideas; it is implementing advanced features before the data foundation, integration repeatability and release evidence are proven.

The next planning improvements should therefore be depth-first:

1. release truth;
2. source adaptability;
3. historical inventory truth;
4. exception delivery / Decision Pulse;
5. validated forecasting;
6. scenario planning;
7. AI and shared SaaS later.

This audit does not create a new execution program. New runnable prompts should be mapped to existing owners first, following `MASTER_ROADMAP.md` and the prompt queue protocol.
