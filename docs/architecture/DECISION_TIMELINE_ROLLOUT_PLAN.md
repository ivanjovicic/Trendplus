# Decision Timeline Rollout Plan

Status: DT02 rollout plan
Date: 2026-08-11
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Related contract: `docs/architecture/DECISION_TIMELINE_CONTRACT.md`
Related queue: `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`
Related inputs:

- `docs/Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md`
- `docs/Analytics/ACTION_OUTCOME_ANALYTICS_PLAN.md`
- `docs/qa/ANALYTICS_ACTION_OUTCOME_RELIABILITY_AUDIT.md`
- `docs/qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md`

## Purpose

DT01 defined the canonical historical vocabulary for recommendation, action, execution and outcome events. This plan turns that contract into a bounded rollout that can be implemented safely in later prompts.

The plan is intentionally conservative:

- reuse the current action row, metadata and note history first;
- make history readable before making it mutable or append-only;
- keep missing stages explicit instead of inferring them;
- delay any new event store or schema migration until a later prompt proves it is necessary.

## Non-goals

- no runtime implementation in DT02
- no schema migration
- no append-only event table yet
- no new write workflow
- no frontend-local reconstruction of history
- no automatic learning or calibration

## Current baseline

The repository already has enough information to start a read-only historical timeline:

- `AnalyticsActionItem.Status`
- `AnalyticsActionItem.OutcomeStatus`
- `AnalyticsActionItem.CreatedAtUtc`
- `AnalyticsActionItem.UpdatedAtUtc`
- `AnalyticsActionItem.ResolvedAtUtc`
- `AnalyticsActionItem.OutcomeMeasuredAtUtc`
- `AnalyticsActionItem.MeasuredImpactRsd`
- `AnalyticsActionItem.ExpectedImpactRsd`
- `AnalyticsActionItem.MetadataJson`
- `AnalyticsActionNote`

The main gaps are still the same ones identified by the outcome audits:

- accepted vs executed vs measured are not yet first-class timestamps on the action row;
- `not_measured` can still be confused with a closed workflow if the UI or projection is careless;
- `outcomeMeasuredAtUtc` and `ResolvedAtUtc` must stay separate;
- export/detail/timeline parity is not yet a single canonical surface.

## Compatibility and migration approach

### Recommended path

1. Reuse the current action row, metadata envelope and note history as the initial source of truth.
2. Add a read-only timeline projection on top of those existing records.
3. Keep historical gaps explicit when a timestamp or evidence field is missing.
4. Add a dedicated append-only event store only if a later prompt proves the projection cannot preserve meaning or traceability.

### Migration stance

- no schema migration in DT02;
- no destructive rewrite of historical rows;
- no automatic backfill of missing accepted/executed timestamps;
- no hidden repair of `not_measured` into a measured outcome;
- if the projection cannot express a gap honestly, stop and split the work into a later prompt.

### Stability rules

- `createdAtUtc` is the issuance anchor until a separate recommendation-issued timestamp exists;
- `resolvedAtUtc` is workflow closure, not business result time;
- `outcomeMeasuredAtUtc` is the business measurement anchor;
- `AnalyticsActionNote` remains the audit trail for lifecycle changes;
- exported and UI timeline views must show the same stage meanings.

## Rollout slices

### Slice 1 - Projection and correlation baseline

Goal: make one historical story readable from existing data without inventing new storage.

Scope:

- derive a read-only timeline projection from `AnalyticsActionItem` + `AnalyticsActionNote` + metadata snapshot fields;
- preserve `sourceRecommendationId`, `actionId`, `sourceType`, `sourceKey` and `recommendationType` where available;
- surface gap reasons such as `no_acceptance_record`, `no_execution_proof`, `no_measurement_evidence` and `legacy_partial_history`;
- keep `done`, `rejected`, `pending` and `not_measured` distinct in the projection.

Stop condition:

- if the projection cannot preserve stage order without guessing, do not invent event rows; move the gap into a later prompt.

### Slice 2 - API and DTO contract

Goal: define a read-only timeline API that returns the canonical event order and explicit gaps.

Proposed API shape:

- `GET /api/analytics/actions/{actionId}/timeline`
- optional collection filter endpoint for historical review:
  - `GET /api/analytics/actions/timeline?sourceType=&sourceKey=&recommendationType=&from=&to=`

Proposed response concepts:

- `timelineId`
- `actionId`
- `sourceRecommendationId`
- `correlationId`
- `sourceType`
- `sourceKey`
- `recommendationType`
- `events[]`
- `gapReasons[]`
- `snapshot`
- `outcomeSnapshot`
- `warningCodes`
- `dataQualityStatus`
- `periodFromUtc`
- `periodToUtc`
- `emptyReason`

Event payload requirements:

- `eventType`
- `occurredAtUtc`
- `stage`
- `status`
- `evidenceSource`
- `evidenceReference`
- `measurementWindowDays`
- `isGap`
- `gapReason`

Rules:

- the API is read-only in this plan;
- the API must not fabricate timestamps for missing stages;
- `emptyReason` must distinguish `no_events`, `no_measurement` and `outside_period`.

### Slice 3 - Timeline UI

Goal: make the historical story visible without reconstructing business truth in the frontend.

UI requirements:

- a timeline panel or detail drawer on the action/history surface;
- filter by entity, recommendation family and time period;
- trust header with period, freshness and data-quality context;
- visible split between workflow closure and outcome measurement;
- explicit gap rows for missing acceptance, execution or measurement;
- no local event inference from notes alone.

UI stop condition:

- if the UI cannot explain a gap, it must show the gap label rather than guessing the missing event.

### Slice 4 - Export, report and evidence retention

Goal: keep export/report consumers aligned with the same timeline semantics.

Export/report requirements:

- include correlation identifiers when available;
- include stage timestamps separately from `updatedAtUtc`;
- include evidence fields and gap reasons;
- include period, freshness and denominator labels;
- export failures must be graceful and must not fabricate zero rates;
- report/print views must preserve the same stage names as the UI.

Evidence-retention rules:

- keep the creation snapshot and outcome resolution snapshot readable in historical exports;
- do not rewrite earlier history when later data arrives;
- if an export cannot show a stage, show the gap instead of hiding it.

### Slice 5 - Hardening and validation

Goal: protect the timeline contract from the same semantic bugs that have affected other analytics surfaces.

Hardening requirements:

- missing accepted/executed/measured timestamps remain explicit;
- `rejected` is not collapsed into `done`;
- `not_measured` is not counted as success or failure;
- zero denominators stay explicit;
- tenant/correlation boundaries remain visible;
- authorization checks stay on the backend, not in the UI copy;
- timeline and export surfaces stay in parity.

Suggested tests for the future implementation prompt:

- full lifecycle example
- rejected recommendation example
- executed but not measured example
- delayed outcome example
- missing evidence example
- export failure example
- period filter example

## Implementation order

1. Projection and correlation baseline
2. API and DTO contract
3. Timeline UI
4. Export, report and evidence retention
5. Hardening and parity tests

## Stop conditions

Stop and split the work if any of the following becomes true:

- the projection needs more than one new source of truth;
- a missing timestamp would need to be inferred instead of shown as a gap;
- the API cannot preserve `resolvedAtUtc` vs `outcomeMeasuredAtUtc`;
- the UI would need to guess history from free-form notes;
- the export surface would diverge from the UI meaning;
- a schema migration becomes necessary before the projection is proven.

## Acceptance criteria for DT02

- the rollout plan names the current storage reuse path and the future migration boundary;
- the API/UI/export slices are bounded and reviewable;
- evidence-retention and hardening requirements are explicit;
- no runtime timeline implementation or schema migration was added in DT02.
