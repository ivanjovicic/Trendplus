# Decision Timeline Contract

Status: authoritative DT01 contract
Date: 2026-08-11
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Related contracts:

- `docs/Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md`
- `docs/Analytics/ACTION_OUTCOME_ANALYTICS_PLAN.md`
- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`
- `docs/qa/ANALYTICS_ACTION_OUTCOME_RELIABILITY_AUDIT.md`
- `docs/qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md`

## Purpose

Trendplus needs one deterministic historical contract that can explain what was recommended, what the user or workflow did next, what evidence was observed, and what outcome was finally recorded.

This contract is deliberately narrower than the recommendation-learning contract:

- it focuses on historical event order and success metrics;
- it does not define calibration or automatic confidence updates;
- it does not introduce a new event store or schema migration;
- it keeps the backend as the source of truth for history and evidence.

## Non-goals

- no runtime event store
- no schema migration
- no automatic learning or calibration
- no frontend-local event reconstruction
- no replacing the current action row and note history model

## Canonical timeline shape

`recommendation issued -> action accepted/rejected/ignored -> action executed -> outcome measured/not measured -> historical timeline -> success metrics`

Do not collapse these stages into one generic "closed" state.

## Event model

The canonical timeline is an ordered sequence of events. The current repository does not need to persist a separate event stream to satisfy this contract, but any future timeline implementation must use these event meanings.

### Event types

| Event type | Meaning | Required evidence | Counts toward success metrics? |
|---|---|---|---:|
| `recommendation_issued` | The recommendation was generated and became visible or recordable. | recommendation snapshot | yes |
| `action_accepted` | The recommendation entered the workflow. | actor or system trace, timestamp | yes |
| `action_rejected` | The recommendation was explicitly declined. | actor or system trace, timestamp | yes |
| `action_ignored` | The recommendation expired or was never accepted before cutoff. | cutoff rule, timestamp window | yes |
| `action_executed` | The recommended action was carried out. | execution proof or workflow trace | yes |
| `outcome_measurement_started` | A measurement window opened or was scheduled. | measurement window metadata | no |
| `outcome_measured` | A business result was observed and backed by evidence. | `evidenceSource`, `evidenceReference`, measured timestamp | yes |
| `outcome_not_measured` | The action closed or elapsed without usable outcome evidence. | explicit missing-evidence reason | no |

### Event ordering rules

- `recommendation_issued` is the baseline event.
- `action_accepted`, `action_rejected` and `action_ignored` are mutually exclusive terminal workflow branches.
- `action_executed` can only follow `action_accepted` in the normal path.
- `outcome_measured` and `outcome_not_measured` are outcome branches, not workflow branches.
- `outcome_measured` must never be inferred from `updatedAtUtc`.
- `outcome_not_measured` must never be inferred from a missing number alone if evidence was never captured.

### Current repository primitives

The current action/outcome model already exposes enough material for this contract:

- `AnalyticsActionItem.Status`
- `AnalyticsActionItem.OutcomeStatus`
- `AnalyticsActionItem.CreatedAtUtc`
- `AnalyticsActionItem.UpdatedAtUtc`
- `AnalyticsActionItem.ResolvedAtUtc`
- `AnalyticsActionItem.OutcomeMeasuredAtUtc`
- `AnalyticsActionItem.MeasuredImpactRsd`
- `AnalyticsActionItem.ExpectedImpactRsd`
- `AnalyticsActionItem.DataQualityStatus`
- `AnalyticsActionItem.MetadataJson`
- `AnalyticsActionNote`

The contract gap is not lack of history altogether. The gap is that the current model does not yet expose canonical accepted/executed/measurement timestamps as first-class values.

## Correlation and identity rules

The same recommendation or action may be visible across multiple surfaces. Timeline identity must stay stable without turning IDs into authorization tokens.

### Stable correlation fields

| Field | Role | Notes |
|---|---|---|
| `sourceRecommendationId` | Stable recommendation instance identity | Prefer a backend-derived deterministic value when the source has no natural ID. |
| `recommendationType` | Canonical recommendation family | Examples: `REPLENISH`, `MARKDOWN`, `NEGOTIATE`, `SIGNAL_REVIEW`. |
| `sourceType` | Origin family | Examples: product, inventory, supplier, dashboard. |
| `sourceKey` | Stable business key | Must stay stable across retries and replays. |
| `actionId` | Action-row identity | Useful for workflow and note correlation. |
| `eventId` | Timeline event identity | Needed if a future append-only timeline is added. |
| `correlationId` | Cross-event trace identifier | Used to group issued, accepted, executed and outcome events. |

### Correlation rules

- `sourceRecommendationId` groups the recommendation history.
- `actionId` groups the workflow row and its notes.
- `correlationId` groups one historical story across stages.
- `sourceKey` and `sourceType` are business identifiers, not access rights.
- No identity field in this contract authorizes access, edits or tenant selection.

## Snapshot vs live lookup

Historical truth must not be reconstructed from current mutable state when a snapshot exists.

### Snapshot fields

The following should be treated as snapshot data for historical explanation:

- `sourceRecommendationId`
- `recommendationType`
- `recommendedAction`
- `decisionReason`
- `primaryDrivers`
- `warningCodes`
- `confidenceLevel`
- `confidenceScore` or `confidencePct` if present
- `inputFreshnessStatus`
- `dataQualityStatus`
- `expectedImpactRsd`
- `expectedImpactBasis`
- `impactWindowDays`
- `generatedAtUtc`

### Live lookup fields

The following may remain live workflow state and should not be used as the only historical source:

- `status`
- `updatedAtUtc`
- current assignee or actor fields
- current notes text
- current open/closed classification

### Outcome snapshot fields

The following are the outcome-history fields that must remain explicit:

- `outcomeStatus`
- `measuredImpactRsd`
- `outcomeMeasuredAtUtc`
- `resolvedAtUtc`
- `evidenceSource`
- `evidenceReference`
- `measurementWindowDays`
- `resolutionNote`

## Success metrics

Every metric in this contract must declare its denominator. Different lifecycle stages are different denominators.

### Core lifecycle metrics

| Metric | Numerator | Denominator | Meaning |
|---|---|---|---|
| Issued count | `issuedCount` | n/a | Total recommendations issued. |
| Acceptance rate | `acceptedCount` | `issuedCount` | Share of issued recommendations that entered workflow. |
| Rejection rate | `rejectedCount` | `issuedCount` | Share of issued recommendations explicitly declined. |
| Ignored rate | `ignoredCount` | `issuedCount` | Share of issued recommendations never accepted before cutoff. |
| Execution rate | `executedCount` | `acceptedCount` | Share of accepted recommendations that were executed. |
| Measurement coverage | `measuredCount` | `executedCount` | Share of executed recommendations with auditable outcome evidence. |
| Not-measured share | `notMeasuredCount` | `executedCount` | Share of executed recommendations lacking usable outcome evidence. |

### Outcome metrics

| Metric | Numerator | Denominator | Meaning |
|---|---|---|---|
| Success rate | `successCount` | `measuredCount` | Share of measured outcomes that were positive. |
| Neutral rate | `neutralCount` | `measuredCount` | Share of measured outcomes that were neutral. |
| Negative rate | `negativeCount` | `measuredCount` | Share of measured outcomes that were adverse. |
| Measured impact sample | `measuredImpactSampleCount` | n/a | Count of measured rows with usable impact value. |
| Impact realization ratio | `measuredImpactRsd / expectedImpactRsd` | valid only when both are present and expected impact is positive | Financial realization signal, not a generic score. |

### Time metrics

| Metric | Definition | Notes |
|---|---|---|
| Time to accept | `acceptedAtUtc - issuedAtUtc` | If `acceptedAtUtc` is missing, do not infer zero. |
| Time to execute | `executedAtUtc - acceptedAtUtc` | Execution requires a valid acceptance event. |
| Time to measure | `outcomeMeasuredAtUtc - resolvedAtUtc` or `outcomeMeasuredAtUtc - issuedAtUtc` when the workflow timestamp is absent | Use one basis consistently per report. |
| Time to close | `resolvedAtUtc - issuedAtUtc` | Workflow close time, not business outcome time. |

### Denominator rules

- success and failure rates must never reuse `issuedCount` unless the metric is explicitly a funnel metric;
- `measuredCount` must not silently include `not_measured` rows;
- `rejected` must not be counted as `done`;
- `done` must not be counted as `rejected`;
- zero denominators must stay explicit as `null`, `insufficient_evidence`, or a warning code;
- `updatedAtUtc` is never a substitute denominator or a substitute business date.

## Missing and partial event behavior

Timeline gaps must stay visible instead of being silently repaired.

### Missing event rules

- missing `acceptedAtUtc` means the recommendation was not accepted or the acceptance point was not captured;
- missing `executedAtUtc` means execution was not proven;
- missing `outcomeMeasuredAtUtc` means outcome evidence is absent;
- missing `evidenceSource` means the result must not be presented as a measured business fact;
- missing `measurementWindowDays` means the measurement horizon is unknown, not zero.

### Partial event rules

- if a recommendation is executed but not measured, keep that gap explicit;
- if a recommendation is measured without a clean execution event, mark the history as incomplete or legacy, not as a fully validated funnel;
- if a row was historically updated several times, preserve the latest known status but do not erase the audit trail;
- if a result is `not_measured`, do not backfill a measurement timestamp just to make the funnel look complete.

### Gap reasons

Recommended gap reason vocabulary:

- `no_acceptance_record`
- `no_execution_proof`
- `no_measurement_evidence`
- `measurement_window_unknown`
- `legacy_partial_history`
- `closed_without_outcome`

## Segmentation rules

Historical success metrics are useful only when the segment is comparable.

### Allowed segmentation dimensions

- recommendation family
- source type
- source key or entity family, when stable
- product / category / store / supplier, when those dimensions are already present
- confidence bucket, only when backend-defined and stable
- freshness bucket
- data-quality bucket

### Segmentation guardrails

- never mix recommendation families in one historical success statement;
- never compare accepted-only and executed-only samples as if they were the same cohort;
- never compare measured-impact sample counts with measured outcome counts without naming the difference;
- never declare a cohort reliable if the measured sample is too small;
- never collapse `unknown`, `missing` and `zero` into the same bucket;
- never infer cross-period outcomes from a current row state.

### Minimum evidence principle

Conservative thresholds for trustworthy cohort statements:

- fewer than 10 measured outcomes -> `insufficient_evidence`
- fewer than 5 measured-impact samples -> `insufficient_evidence` for impact-related claims
- mixed or missing attribution windows -> directional only

These thresholds are contract guidance, not a runtime learning algorithm.

## Current reporting implications

The current Action Outcome Analytics and Action Impact Ledger plans already point in the same direction:

- keep `createdAtUtc`, `resolvedAtUtc` and `outcomeMeasuredAtUtc` separate;
- keep `done` and `rejected` visible as different lifecycle results;
- keep `pending` and `not_measured` explicit;
- keep `AnalyticsActionNote` as the audit trail for state changes;
- use read-only summary and timeline surfaces before any broader workflow redesign.

## No fake rules

1. `updatedAtUtc` is not the business date of the outcome.
2. `resolvedAtUtc` is not the measured outcome timestamp.
3. `rejected` is not `done`.
4. `not_measured` is not failure.
5. Missing measurement evidence cannot be relabeled as measured success.
6. A zero denominator is not a zero rate.
7. A historical gap is not a valid inferred event.
8. The frontend must not invent timeline events from local heuristics.
9. Historical evidence must not rewrite the original recommendation snapshot.
10. Timeline metrics must not weaken the no-fake-zero rule.

## Validation examples

### Example 1: full lifecycle

- `issuedCount = 1`
- `acceptedCount = 1`
- `executedCount = 1`
- `measuredCount = 1`
- `successCount = 1`

Interpretation:

- the recommendation moved through the full funnel;
- all key timestamps exist;
- success rate is valid.

### Example 2: rejected recommendation

- `issuedCount = 1`
- `rejectedCount = 1`
- `acceptedCount = 0`
- `executedCount = 0`
- `measuredCount = 0`

Interpretation:

- the recommendation was explicitly declined;
- it should remain visible as a distinct historical branch;
- it must not count as a completed execution.

### Example 3: executed but not measured

- `issuedCount = 1`
- `acceptedCount = 1`
- `executedCount = 1`
- `measuredCount = 0`
- `notMeasuredCount = 1`

Interpretation:

- the workflow happened;
- the feedback loop did not;
- outcome rates stay guarded by the measurement denominator.

## Acceptance criteria for DT01

- one canonical timeline and success-metric contract exists;
- issued, accepted, executed, measured and outcome states are distinct;
- success metrics always name their denominator;
- missing events and partial histories remain explicit;
- no runtime event store or schema migration was introduced.
