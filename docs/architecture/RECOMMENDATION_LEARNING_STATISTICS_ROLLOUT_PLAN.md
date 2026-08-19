# Recommendation Learning Statistics Rollout Plan

Status: RL02 rollout plan
Date: 2026-08-11
Roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Source contract: `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`
Related inputs:

- `docs/Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md`
- `docs/qa/ANALYTICS_ACTION_OUTCOME_RELIABILITY_AUDIT.md`
- `docs/qa/CONFIDENCE_CALIBRATION_AUDIT.md`
- `docs/qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md`
- `docs/architecture/DECISION_TIMELINE_CONTRACT.md`

## Purpose

RL01 froze the vocabulary for recommendation lifecycle and outcome evidence. This plan turns that contract into a bounded rollout for deterministic statistics without changing live confidence, ranking, or learning policy yet.

The rollout stays conservative:

- reuse the current action/outcome row and metadata first;
- make measurement coverage explicit before any calibration;
- keep lifecycle counts separate from outcome evidence;
- delay any runtime learning mutation until a later prompt explicitly authorizes it.

## Non-goals

- no runtime learning algorithm
- no ML model
- no automatic confidence change
- no new event store
- no schema migration required by this plan
- no frontend-local scoring or invented inference

## Current baseline

The repository already has the raw material needed for measurement-only statistics:

- `AnalyticsActionItem.Status`
- `AnalyticsActionItem.OutcomeStatus`
- `AnalyticsActionItem.ExpectedImpactRsd`
- `AnalyticsActionItem.MeasuredImpactRsd`
- `AnalyticsActionItem.OutcomeMeasuredAtUtc`
- `AnalyticsActionItem.ResolvedAtUtc`
- `AnalyticsActionItem.ConfidencePct`
- `AnalyticsActionItem.ReliabilityPct`
- `AnalyticsActionItem.DataQualityStatus`
- `AnalyticsActionItem.MetadataJson`
- `AnalyticsActionNote`

The current gap is not missing data entirely. The gap is a lack of canonical cohort slicing, evidence coverage and staged reporting semantics that are stable enough to support learning statistics.

## Rollout order

1. Measurement-only lifecycle statistics.
2. Minimum evidence gates and cohort segmentation.
3. Dashboards and review surfaces.
4. Advisory calibration interface.
5. Hardening and regression coverage.

## Slice 1 - Measurement-only statistics projection

### Goal

Define the deterministic counts that describe recommendation flow without changing runtime behavior.

### Scope

- derive counts for `issued`, `accepted`, `rejected`, `ignored` and `executed`;
- keep `measured` and `not_measured` separate from lifecycle state;
- define `measuredCount`, `notMeasuredCount`, `successCount`, `neutralCount` and `negativeCount` using explicit denominators;
- preserve recommendation family, source type, source key and data-quality state in the projection;
- reuse current action/outcome records and metadata snapshot fields.

### Validation

- acceptance is not counted as success;
- execution is not counted as success;
- `not_measured` stays explicit and never becomes a fake zero;
- zero denominators remain `null`, `insufficient_evidence` or warning-coded instead of pretending to be a healthy rate.

## Slice 2 - Evidence gates and segmentation

### Goal

Make cohort statistics conservative enough that the product can say when evidence is directional only.

### Scope

- define minimum sample floors before a cohort is considered eligible for calibration;
- segment by recommendation family, source type, source key or entity family when stable, and other bounded dimensions already present in the contract;
- require measurement window metadata, attribution bounds and warning codes for meaningful comparison;
- keep mixed or missing window metadata out of any strong claim.

### Validation

- fewer than the minimum measured outcomes remains `insufficient_evidence`;
- cohorts do not mix incompatible recommendation families;
- accepted-only and executed-only views keep separate denominators;
- missing evidence stays visible instead of being backfilled by UI logic.

## Slice 3 - Dashboards and review surfaces

### Goal

Expose the same statistics in operator and product review surfaces without hiding gaps.

Frozen presentation contract: `docs/architecture/RECOMMENDATION_MEASUREMENT_STATISTICS_REVIEW_SURFACE.md`.

Runtime (RL09): Centralne akcije panel `RecommendationMeasurementStatisticsReview` binds funnel, coverage and outcome rates to `measurementStatistics` only.

### Scope

- add a review surface that shows lifecycle funnel counts, measured coverage and outcome distribution;
- include period, freshness, data quality and warning context in the header;
- keep export and report views aligned with the same denominator logic;
- show explicit "no measurement" and "insufficient evidence" states;
- bind rates to `measurementStatistics`, never to legacy `totals` success/coverage aliases.

### Validation

- dashboards do not invent healthy-looking rates when evidence is absent;
- exports preserve the same counts and denominators as the review surface;
- missing/partial data stays visible as a gap, not a fabricated result;
- acceptance and execution are never labeled as success.

## Slice 4 - Advisory calibration interface

### Goal

Define the future input/output shape for a deterministic calibration job without changing live confidence yet.

The citeable Slice 4 advisory contract is frozen in
`docs/Analytics/RECOMMENDATION_ADVISORY_CALIBRATION_CONTRACT.md`.

### Scope

- define the cohort inputs needed for a future calibration service;
- return advisory metadata only: eligibility, direction, reason codes and bounded hint fields;
- require explicit later approval before any score mutation can happen;
- keep the output safe to ignore until a separate runtime prompt authorizes it.

### Validation

- no automatic confidence mutation is introduced by this plan;
- advisory output remains deterministic and auditable;
- the calibration contract can be ignored safely without changing product behavior.

## Slice 5 - Hardening and regression coverage

### Goal

Protect the statistics plan from the same trust bugs that affect other analytics surfaces.

### Scope

- test true zero vs missing evidence;
- test accepted, rejected, ignored and executed rows separately;
- test delayed outcome measurement and not-measured rows;
- test cohort segmentation boundaries and minimum sample floors;
- keep tenant/dataScope boundaries explicit;
- keep UI/export parity aligned with the same counters and denominators.

### Validation

- no fake zero;
- no silent fallback to healthy;
- no cross-family calibration claim;
- no runtime learning mutation.

## Implementation order

1. Measurement-only statistics projection
2. Evidence gates and segmentation
3. Dashboards and review surfaces
4. Advisory calibration interface
5. Hardening and regression coverage

## Acceptance

- the plan names the current reuse path and the future calibration boundary;
- lifecycle counts, evidence coverage and segmentation rules are explicit;
- dashboard/export/report surfaces are bounded and reviewable;
- no runtime learning algorithm, confidence mutation or schema migration is added by RL02.
