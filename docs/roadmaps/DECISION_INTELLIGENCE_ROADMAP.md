# Trendplus Decision Intelligence Roadmap

Updated: 2026-08-18
Status: approved future product direction; runtime implementation remains queue-gated  
Owner queue: `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`

## Definition

Decision Intelligence is the layer that connects trusted analytics evidence to a decision, explains the decision, records alternatives, tracks execution and outcome, and improves future confidence from measured evidence.

It is deliberately separate from the analytics reliability program. Analytics answers questions such as "what happened?", "where is risk?" and "how reliable is this measure?". Decision Intelligence answers "what should we do, why, what else could we do, what did we actually do, and did it work?".

## Non-negotiable boundary

The first Decision Intelligence phases are deterministic and require no AI/LLM dependency.

- backend contracts remain authoritative;
- confidence is calculated from explicit evidence, never generated prose;
- alternatives are derived from known business rules/decision candidates;
- evidence links reference real inputs and source states;
- missing evidence remains missing;
- AI may later summarize authoritative evidence, but may not invent decision truth.

## Workstreams

### DEX - Decision Explainability

Goal: every high-value recommendation can expose a deterministic explanation.

Roadmap sequence:

1. **Decision Graph** - canonical graph of decision, evidence nodes, rules, confidence contributors and downstream action.
   - First mapping example: Product Decision Center, documented in `docs/architecture/DECISION_GRAPH_CONTRACT.md`.
2. **Evidence chain** - trace every explanation back to the concrete metrics/statuses that support it.
3. **Confidence breakdown** - separate confidence contributors such as freshness, coverage, data quality, baseline quality and model/rule reliability.
4. **Alternative recommendations** - show valid competing actions and why they were not selected.
5. **Drill-down** - navigate from decision summary to evidence and source-level supporting data.
6. **Why panel** - concise user-facing explanation generated from deterministic fields, not free-form inference.
7. **Decision Tree** - inspect the rule/branch path that produced the recommendation where rule-based logic applies, and show the absence of a tree explicitly when no branch path exists.
8. **Decision evidence snapshot** - persist a versioned evidence snapshot when a recommendation is acted on so later review is not rewritten by new data.

### DEX02 - First-family rollout plan

First family: Product Decision Center.

Why this family:

- it already exposes the richest row-level recommendation semantics;
- it already feeds the Executive Decision Board as a downstream consumer;
- it already has explicit confidence and candidate-contract audits in the repo.

Rollout slices:

1. **Backend contract alignment** - keep recommendation, status, confidence, impact and reason fields authoritative; preserve null and unknown values; no runtime graph engine.
2. **Frontend Why / drill-down** - render from backend deterministic fields only; missing evidence stays explicit; no local scoring or invented alternatives.
3. **Evidence snapshot / hardening** - preserve period, freshness, data-quality and warning context so later review and timeline reuse are stable.
4. **Regression coverage** - test true zero vs missing evidence, stale evidence, partial confidence and no fake green.

Compatibility and stop conditions:

- additive only for the first bounded implementation slices;
- no AI dependency;
- if a field is absent, show the gap rather than synthesizing a fallback;
- branch-path surfaces are explicit when present and explicit when absent; do not infer them from reasons or evidence chains;
- do not expand beyond Product Decision Center until the first family is stable.

Next bounded prompt:

- DEX06 is the executable Product Decision Center confidence breakdown prompt.

Desired flow:

`Decision -> reason -> evidence -> confidence contributors -> alternatives -> drill-down`

### RL - Recommendation Learning

Goal: improve recommendation confidence from measured outcomes without introducing opaque learning before the evidence loop is reliable.

Authoritative contract:

- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`
- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`

Roadmap sequence:

`Recommendation issued -> Accepted -> Executed -> Measured -> Outcome -> Learning -> Statistics -> Improved confidence`

Phases:

1. define lifecycle event vocabulary and outcome eligibility;
2. record accepted/rejected/ignored recommendation state;
3. link execution evidence to the original recommendation;
4. define measurable outcome windows and attribution constraints;
5. compute deterministic success/failure/insufficient-evidence statistics;
6. segment statistics by recommendation family, product/category/store/supplier where evidence is sufficient;
7. feed stable historical performance into confidence calibration;
8. consider more advanced statistical/ML learning only after deterministic baselines are trustworthy.

No recommendation may be labelled "learned better" merely because it was accepted. Outcome evidence is required.

### DT - Decision Timeline

Goal: provide an auditable historical record of what was recommended, what happened and what result was observed.

Authoritative contract:

- `docs/architecture/DECISION_TIMELINE_CONTRACT.md`
- `docs/architecture/DECISION_TIMELINE_EXPORT_REPORT_CONTRACT.md`

Roadmap sequence:

`Recommendation -> Action -> Execution -> Outcome -> Historical timeline -> Success metrics`

Phases:

1. canonical timeline event model — **DONE** (`docs/architecture/DECISION_TIMELINE_CONTRACT.md`);
2. decision/action/execution/outcome correlation identifiers — covered in DT01; first-class timestamps remain a later additive slice;
3. immutable/append-oriented history semantics for important events — projection-first via notes/snapshots (`docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md`);
4. filtered timeline by entity/recommendation family/time period — **DONE** Slice-2 runtime in DT05; Product Decision Center remains the live filter surface;
5. outcome window and measurement state display — planned UI/export slices in DT02; honesty rules frozen in DT06;
6. success metrics that distinguish issued, accepted, executed, measured and successful denominators — **DONE** in DT01 (aligned with RL01);
7. evidence snapshot links for historical explainability — reuse creation/resolution ledger snapshots and DEX10 evidence snapshots; presence/absence frozen in DT06;
8. export/reporting for decision review and business retrospectives — **DONE** as DT06 contract plus DT07 runtime export (`GET /api/analytics/cached/products/decision-center/timeline/export`) over Slice-2; Slice-5 hardening is **DONE** in `DT08`.

Authoritative planning outputs:

- `docs/architecture/DECISION_TIMELINE_CONTRACT.md`
- `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md`
- `docs/architecture/DECISION_TIMELINE_EXPORT_REPORT_CONTRACT.md`
- `docs/Analytics/RECOMMENDATION_ADVISORY_CALIBRATION_CONTRACT.md`

Current queue READY: none for DEX/RL/DT. `RL10` is DONE = Slice 4 advisory calibration contract. DT none (`DT09` WAITING = first-class timestamps). `DT08` DONE = Slice-5 timeline export hardening. `RL09` DONE = measurement-statistics review surface runtime. `DT07`/`DEX18`/`RL07` DONE. `DEX19` DONE = Executive Board explainability runtime. `DEX17`/`RL06`/`DT06` DONE. `RL08` remains OBSOLETE.

## Decision Engine evolution

Decision Engine evolution is staged and must preserve existing backend source-of-truth rules.

### Stage 1 - Explicit deterministic contract

- recommendation type;
- reason code(s);
- evidence inputs;
- confidence and confidence contributors;
- expected impact where defensible;
- alternatives;
- constraints/blockers;
- data freshness/quality state.

### Stage 2 - Decision graph composition

Multiple evidence signals can support one decision while preserving traceability and stable vocabulary.

### Stage 3 - Historical outcome feedback

Measured historical performance may adjust confidence or ranking only through an explicit, testable calibration contract.

### Stage 4 - Adaptive decision policy

Only after outcome coverage is strong enough should the product consider statistical optimization, experimentation or machine learning. Any such step requires a separate roadmap decision and evaluation gates.

## Alternative decisions

Alternatives are first-class decision evidence, not UI decoration.

Each alternative should eventually state:

- alternative action/type;
- why it is eligible;
- why it ranked below the selected decision;
- expected trade-off;
- confidence/evidence status;
- conditions that would make it preferred.

## Evidence model principles

- evidence IDs must be stable enough to correlate API/detail/action/timeline surfaces;
- units and denominator scope must be explicit;
- evidence timestamp/freshness must be visible;
- missing/partial/fallback evidence must not be coerced into a trusted score;
- reason codes are stable machine-readable vocabulary;
- user-facing explanations are derived from authoritative fields;
- a historical evidence snapshot must not silently mutate after the decision is executed.

## Milestones

### DI-1 - Explainable deterministic decisions

DEX01 contract complete, graph/evidence vocabulary accepted, DEX02 selects Product Decision Center as the first bounded rollout family, and DEX03 prepares the implementation prompt.

### DI-2 - Auditable decision history

DT event model accepted and recommendation/action/execution/outcome correlation defined.

### DI-3 - Outcome learning baseline

RL lifecycle/statistical contract accepted; success metrics do not confuse acceptance with execution or execution with measured success.

### DI-4 - Cross-family rollout

Explainability/timeline/outcome patterns reusable across product, inventory, supplier and other decision families without duplicating semantics.

### DI-5 - Calibrated decision engine

Measured outcome statistics can influence confidence/ranking through a documented deterministic calibration layer.

## Dependencies

- analytics reliability for trustworthy evidence semantics;
- STAB/release safety for current deploy truth;
- OBS for traceability/SLIs and correlation conventions;
- MT before shared-SaaS decision evidence crosses tenant boundaries;
- GAI is optional and downstream, never a prerequisite.

## Non-goals

This roadmap does not authorize:

- free-form LLM recommendations;
- arbitrary SQL generation;
- automatic customer-source writes;
- opaque confidence scores;
- reinforcement learning from clicks/acceptance without outcome evidence;
- rewriting current analytics reliability queues as Decision Intelligence work.
