# Recommendation Measurement Statistics Contract

Status: authoritative RL05 measurement-only contract
Date: 2026-08-13
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Related source contract: `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`
Related rollout plan: `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`
Related runtime projection: `GET /api/analytics/actions/outcomes/summary`
Related review-surface contract: `docs/architecture/RECOMMENDATION_MEASUREMENT_STATISTICS_REVIEW_SURFACE.md`
Related service: `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`

## Purpose

This contract freezes the read-only meaning of measurement-only recommendation statistics.
It defines how the current action/outcome data can be projected into deterministic counts
without mutating confidence, inventing calibration, or collapsing evidence gaps into fake zeros.

The contract is intentionally narrow:

- it keeps lifecycle counts separate from measured outcome evidence;
- it makes the denominator for each rate explicit;
- it preserves `not_measured` as a first-class trust state;
- it stays compatible with the current outcome summary endpoint so later runtime work can
  implement the same semantics without reopening the product decision.

## Non-goals

- no automatic confidence change
- no calibration output
- no ML model
- no schema migration required by this contract
- no new event store
- no frontend-local inference
- no fake healthy rate when the denominator is zero

## Canonical axes

Measurement statistics must distinguish three different questions:

1. what happened to the recommendation lifecycle
2. whether an outcome was actually measured
3. whether the measured outcome was positive, neutral or negative

Do not collapse those axes into one field.

### Lifecycle states

| State | Meaning | Counts toward lifecycle stats? | Notes |
|---|---|---:|---|
| `issued` | The recommendation was generated and recorded. | yes | Baseline funnel denominator. |
| `accepted` | The recommendation entered workflow. | yes | Acceptance is not success. |
| `rejected` | The recommendation was explicitly declined. | yes | Valid funnel outcome. |
| `ignored` | The recommendation expired or was never acted on. | yes | Useful for selection-bias analysis. |
| `executed` | The recommended action was carried out. | yes | Execution is still not success. |

### Outcome evidence states

| State | Meaning | Counts toward success/failure? | Notes |
|---|---|---:|---|
| `pending` | The action is still open or the result has not been claimed. | no | Pending is not failure. |
| `measured` | A business result was captured with evidence. | yes | Measured rows can later be positive, neutral or negative. |
| `not_measured` | The row closed or elapsed without usable evidence. | no | Explicit evidence gap, not a performance state. |

### Outcome result states

| State | Meaning | Eligible for measured outcome rates? | Notes |
|---|---|---:|---|
| `success` | Measured outcome is positive relative to the expected effect. | yes | Requires evidence. |
| `neutral` | Measured outcome is materially neither positive nor negative. | yes | Requires evidence. |
| `negative` | Measured outcome is adverse relative to the expected effect. | yes | Requires evidence. |
| `not_measured` | No usable evidence was captured. | no | Must never be counted as success or failure. |

## Current projection shape

The current runtime summary already exposes a stable read-only projection shape.
This contract freezes the meaning of that shape so later runtime slices stay aligned.

### Meta

| Field | Meaning |
|---|---|
| `Success` | `true` when the projection completed successfully, even if the cohort is empty. |
| `PeriodMode` | The effective period mode used by the query. |
| `CreatedFrom` / `CreatedTo` | Creation window for the cohort. |
| `ResolvedFrom` / `ResolvedTo` | Resolution window when the query asks for closed outcomes. |
| `MeasuredFrom` / `MeasuredTo` | Measurement window for evidence-based analysis. |
| `GeneratedAtUtc` | When the projection was generated. |
| `SampleSize` | Total number of rows in the cohort. |
| `MeasuredSampleSize` | Number of rows with non-pending outcome status. |
| `Warnings` | Machine-readable caveats about sample size, coverage or missing impact evidence. |
| `EmptyReason` | Why the cohort is empty when `SampleSize = 0`. |

### Totals

| Field | Meaning |
|---|---|
| `CreatedCount` | Rows created in the cohort window. |
| `ClosedCount` | Rows closed or otherwise no longer open. |
| `OpenCount` | Rows still open. |
| `MeasuredCount` | Rows whose outcome status is not `pending`. |
| `MeasuredOutcomeCount` | Alias of `MeasuredCount` in the current projection. |
| `PendingOutcomeCount` | Rows whose outcome status is still `pending`. |
| `SuccessCount` | Rows with measured outcome `success`. |
| `NeutralCount` | Rows with measured outcome `neutral`. |
| `NegativeCount` | Rows with measured outcome `negative`. |
| `NotMeasuredCount` | Rows with explicit `not_measured` evidence state. |
| `OutcomeCoverageRate` | Measured closed rows divided by closed rows. |
| `PositiveOutcomeRate` | Success rows divided by measured rows. |
| `NegativeOutcomeRate` | Negative rows divided by measured rows. |
| `ClosedOutcomeCoverageRate` | Alias of `OutcomeCoverageRate` in the current projection. |
| `MeasuredPositiveOutcomeRate` | Alias of `PositiveOutcomeRate` in the current projection. |
| `MeasuredNegativeOutcomeRate` | Alias of `NegativeOutcomeRate` in the current projection. |

### Impact

| Field | Meaning |
|---|---|
| `ExpectedImpactRsd` | Sum or aggregate of expected impact for measured-impact rows. |
| `MeasuredImpactRsd` | Sum or aggregate of measured impact for rows with measured impact evidence. |
| `RealizationRatio` | Measured impact divided by expected impact when both are valid. |
| `MeasuredImpactSampleCount` | Number of rows with measurable impact evidence. |

### Buckets

The current runtime also breaks the same semantics down by:

- recommendation type
- source type
- data quality status
- confidence bucket
- reliability bucket

Bucketed views must preserve the same denominator rules as the top-level totals.

## Denominator rules

Each rate must declare its denominator explicitly.
Never infer a denominator from a label.

| Metric | Numerator | Denominator | Contract meaning |
|---|---|---|---|
| Outcome coverage | measured closed rows | closed rows | How much of the closed cohort has measurable evidence. |
| Positive outcome rate | success rows | measured rows | Share of measured outcomes that were positive. |
| Negative outcome rate | negative rows | measured rows | Share of measured outcomes that were adverse. |
| Not-measured share | not_measured rows | closed rows | How much of the closed cohort still lacks usable evidence. |
| Realization ratio | measured impact | expected impact | Only valid when both sides exist and expected impact is positive. |

### Denominator guardrails

- acceptance is not success
- execution is not success
- `not_measured` is never a fake zero
- `pending` is never a failure
- zero denominators must remain `null`, `insufficient_evidence`, or warning-coded
- a row with missing evidence must not be silently moved into success or failure

## Evidence and data quality rules

The projection must keep data-quality and evidence gaps visible.

### Required evidence signals

- `OutcomeStatus`
- `OutcomeMeasuredAtUtc`
- `MeasuredImpactRsd` when impact is reported
- `ExpectedImpactRsd` when realization is reported
- `DataQualityStatus`
- `WarningCodes`

### Evidence handling

- if outcome evidence is absent, keep the row in `pending` or `not_measured`
- if the measured impact is missing, do not fabricate a realization ratio
- if the expected impact is missing, keep the realization ratio null and add a warning code
- if the cohort is too small, mark the result as `insufficient_evidence` at the presentation layer instead of pretending the rate is reliable

### Current warning vocabulary

The current runtime already uses warning codes such as:

- `small_sample`
- `small_measured_sample`
- `outcome_coverage_low`
- `expected_impact_denominator_missing`
- `measured_impact_missing`
- `rejected_actions_present`

The contract does not require these exact strings forever, but it does require stable warning semantics.

## Empty and partial cohorts

An empty cohort is not an error.

- `Success` may still be `true`
- `SampleSize` may be `0`
- `MeasuredSampleSize` may be `0`
- `EmptyReason` must explain why the result is empty
- rates should remain null instead of becoming fake zeros

A partial cohort is also not an error.

- keep the warning visible
- keep the empty denominator visible
- do not upgrade an uncertain cohort into a confident one

## Mapping to current repo primitives

The current repository already exposes the raw fields needed for this contract:

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

The stable read-only summary endpoint is:

- `GET /api/analytics/actions/outcomes/summary`

The RL06 runtime helper that projects lifecycle and measured-evidence counts from existing action rows is:

- `Application/Analytics/RecommendationMeasurementStatisticsProjection.cs`

That helper is attached to the outcome summary as `measurementStatistics`. Existing `totals` keep their prior closed/open meaning; measurement-only funnel counts live on the new object so acceptance and execution cannot be mistaken for success.

## Acceptance rules

- acceptance is not counted as success
- execution is not counted as success
- `not_measured` stays explicit
- empty cohorts stay empty instead of becoming fake healthy zeros
- the contract can be implemented later without changing the trust model
- this document does not authorize confidence mutation or calibration
