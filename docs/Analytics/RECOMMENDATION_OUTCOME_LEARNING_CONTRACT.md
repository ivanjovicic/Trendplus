# Recommendation Outcome Learning Contract

Status: authoritative RL01 contract
Date: 2026-08-11
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Related storage spec: `docs/Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md`
Related reliability audits:

- `docs/qa/ANALYTICS_ACTION_OUTCOME_RELIABILITY_AUDIT.md`
- `docs/qa/CONFIDENCE_CALIBRATION_AUDIT.md`
- `docs/qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md`

## Purpose

Trendplus can already record recommendations, actions, outcomes and outcome evidence. This contract defines how those facts may be turned into deterministic learning statistics without confusing acceptance with execution, execution with measurement, or measurement with confidence mutation.

The contract is intentionally read-first and evidence-first:

- it freezes the vocabulary for recommendation lifecycle and outcome measurement;
- it defines which denominators are valid for each rate;
- it states which metadata must exist before cohort statistics are trustworthy;
- it leaves runtime confidence updates unchanged until a later roadmap task explicitly authorizes them.

## Non-goals

- no runtime learning algorithm
- no ML model
- no automatic confidence change
- no new event store
- no schema migration required by this contract
- no frontend-local calibration or score invention

## Contract layers

The learning contract uses two separate axes:

1. recommendation lifecycle: what happened to the recommendation
2. outcome evidence: whether the business result was actually measured

Do not collapse these axes into one status field.

### Lifecycle vocabulary

| State | Meaning | Counts toward learning? | Notes |
|---|---|---:|---|
| `issued` | The recommendation was generated and shown or recorded. | yes | Baseline denominator for funnel metrics. |
| `accepted` | An operator or downstream workflow accepted the recommendation. | yes | Acceptance is not learning evidence by itself. |
| `rejected` | The recommendation was explicitly declined. | yes | Rejection is a valid funnel outcome, not a failure result. |
| `ignored` | The recommendation was visible but never accepted before expiry or workflow cutoff. | yes | Useful for selection-bias analysis. |
| `executed` | The recommended action was carried out. | yes | Execution is still not proof of business success. |

### Outcome evidence vocabulary

| State | Meaning | Counts toward success/failure rates? | Notes |
|---|---|---:|---|
| `measured` | A business result was captured with auditable evidence. | yes | Measured rows may later be success, neutral or negative. |
| `not_measured` | The row closed or elapsed without usable outcome evidence. | no | This is an explicit evidence gap, not failure. |

### Outcome result vocabulary

| State | Meaning | Eligible for outcome rates? | Notes |
|---|---|---:|---|
| `pending` | The recommendation/action is still open and no result is claimed. | no | Pending is not a failure. |
| `success` | Measured outcome is positive relative to the expected business effect. | yes | Requires measurement evidence. |
| `neutral` | Measured outcome is materially neither positive nor negative. | yes | Requires measurement evidence. |
| `negative` | Measured outcome is adverse relative to the expected business effect. | yes | Requires measurement evidence. |
| `not_measured` | No usable evidence was captured. | no | Must not be counted as success or failure. |

## Eligibility rules

### Measured success / neutral / negative

An outcome may be classified as `success`, `neutral` or `negative` only when all of the following are true:

- the recommendation was at least `accepted` and usually `executed`;
- `outcomeMeasuredAtUtc` is present;
- `evidenceSource` is present;
- the measurement is tied to an auditable reference or snapshot;
- the row is not using a fake default to stand in for missing measurement data.

If the business result is known only partially, keep the row in `pending` or `not_measured` instead of overstating certainty.

### Not measured

Use `not_measured` when:

- the action closed without a trustworthy result;
- the result exists informally but cannot be backed by evidence;
- the measured impact is missing and there is no defensible substitute;
- the cohort is being audited for coverage rather than success.

`not_measured` is a trust state, not a performance state.

### Insufficient evidence

`insufficient_evidence` is a cohort-level label, not a row-level result.

Use it when:

- the denominator is too small;
- the measurement window is incomplete or mismatched;
- the segment mixes incompatible recommendation families or sources;
- the evidence coverage is too sparse to support a stable calibration statement.

## Denominator vocabulary

Each rate must declare its denominator explicitly. Never mix funnel stages without naming the base count.

| Metric | Numerator | Denominator | Contract meaning |
|---|---|---|---|
| Acceptance rate | `acceptedCount` | `issuedCount` | How many issued recommendations entered workflow. |
| Rejection rate | `rejectedCount` | `issuedCount` | How many were explicitly declined. |
| Ignored rate | `ignoredCount` | `issuedCount` | How many never entered workflow before cutoff. |
| Execution rate | `executedCount` | `acceptedCount` | How many accepted recommendations were actually carried out. |
| Measurement coverage | `measuredCount` | `executedCount` | How many executed actions have auditable outcome evidence. |
| Success rate | `successCount` | `measuredCount` | How many measured outcomes were positive. |
| Neutral rate | `neutralCount` | `measuredCount` | How many measured outcomes were neutral. |
| Negative rate | `negativeCount` | `measuredCount` | How many measured outcomes were adverse. |
| Not-measured share | `notMeasuredCount` | `executedCount` | How many executed actions still lack usable evidence. |
| Expected-vs-measured realization | `measuredImpactRsd` | `expectedImpactRsd` | Only valid when both sides exist and expected impact is positive. |

### Rules for denominators

- acceptance, execution and measurement rates must never share a denominator implicitly;
- `measuredCount` must not silently include rows that have no measurement evidence;
- `not_measured` must not count as a success or failure;
- zero denominators must stay explicit as `null`, `insufficient_evidence` or a warning code, never as a fake `0%` rate.

## Attribution and window metadata

Before recommendation statistics are considered trustworthy, each cohort must carry enough metadata to explain what was counted and over which window.

### Required metadata dimensions

| Field | Meaning | Why it matters |
|---|---|---|
| `sourceRecommendationId` | Stable recommendation instance identifier | Prevents double counting and allows repeat-instance analysis. |
| `recommendationType` | Canonical family such as `REPLENISH`, `MARKDOWN`, `NEGOTIATE`, `SIGNAL_REVIEW` | Prevents mixing incompatible decision types. |
| `sourceType` | Origin family such as product, inventory or supplier | Supports cross-source segmentation. |
| `sourceKey` | Stable business key for the source record | Connects outcomes back to the same business entity. |
| `generatedAtUtc` or `issuedAtUtc` | When the recommendation was issued | Establishes the cohort start. |
| `acceptedAtUtc` | When the recommendation entered workflow | Distinguishes acceptance from issuance. |
| `executedAtUtc` | When the recommended action was carried out | Distinguishes action from evidence. |
| `outcomeMeasuredAtUtc` | When the business result was observed | Primary measurement timestamp. |
| `measurementWindowDays` | Number of days covered by the measurement | Needed to compare cohorts fairly. |
| `attributionStartUtc` / `attributionEndUtc` | Exact inclusion window for the outcome | Prevents leakage across periods. |
| `evidenceSource` | Auditable source of the measurement | Proves the result is not invented. |
| `evidenceReference` | Stable pointer to the supporting evidence | Helps reproduce the statistic later. |
| `dataQualityStatus` | Trust state of the underlying signal | Stops weak evidence from being treated like strong evidence. |
| `warningCodes` | Machine-readable caveats | Keeps caveats visible in stats and UI. |

### Mapping to current repo primitives

The current action/outcome layer already gives the contract enough raw material:

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

The current gap is not lack of data entirely. The gap is a lack of canonical lifecycle and measurement semantics that are stable enough to be used as learning denominators.

## Segmentation rules

Learning statistics must be segmented only where the evidence remains comparable.

### Allowed segmentation dimensions

- recommendation family
- source type
- source key or entity family, when stable
- product / category / store / supplier, when the source already has that dimension
- confidence bucket, only when the bucket definition is backend-led and stable
- freshness bucket
- data-quality bucket

### Segmentation guardrails

- never mix different recommendation families in one calibration statement;
- never mix accepted-only analysis with executed-only analysis;
- never compare measured impact against outcome rates unless the measurement window and denominator are identical;
- never apply a calibration label to a cohort with insufficient measured sample size;
- never segment below the minimum sample threshold and still call the result reliable;
- never collapse `unknown`, `missing` and `zero` into one bucket.

### Minimum-evidence principle

Use conservative thresholds before any cohort is treated as more than directional:

- fewer than 10 measured outcomes -> `insufficient_evidence`
- fewer than 5 measured-impact samples -> `insufficient_evidence` for impact calibration
- any mixed or missing window metadata -> directional only

These thresholds are intentionally conservative and can only be changed by a later roadmap decision, not by local UI logic.

## Future deterministic calibration interface

This contract allows a future calibration layer to read stable statistics without changing runtime recommendation confidence yet.

The canonical citeable Slice 4 advisory version of this interface is frozen in
`docs/Analytics/RECOMMENDATION_ADVISORY_CALIBRATION_CONTRACT.md`.

### Input contract

A future calibration job should receive:

- `cohortKey`
- `recommendationType`
- `sourceType`
- `sourceKey` or entity segment
- `issuedCount`
- `acceptedCount`
- `rejectedCount`
- `ignoredCount`
- `executedCount`
- `measuredCount`
- `measuredImpactSampleCount`
- `successCount`
- `neutralCount`
- `negativeCount`
- `notMeasuredCount`
- `measurementWindowDays`
- `attributionStartUtc`
- `attributionEndUtc`
- `warningCodes`
- `dataQualityStatus`
- `confidenceBucket`
- `freshnessBucket`

### Output contract

A future deterministic calibration result should return advisory metadata only:

| Field | Meaning |
|---|---|
| `calibrationEligibility` | `eligible`, `directional_only`, or `insufficient_evidence` |
| `calibrationDirection` | `increase`, `decrease`, `no_change`, or `unknown` |
| `adjustmentHint` | Small bounded hint for later review, not an automatic score mutation |
| `reasonCodes` | Why the cohort fell into that calibration class |
| `measuredImpactSampleCount` | Count of outcomes with a usable measured-impact value |
| `sampleSummary` | Human-readable summary of the denominators used |

### Output rules

- the output must not automatically change live confidence or ranking;
- the output must stay deterministic and auditable;
- the output must be safe to ignore until a separate implementation prompt says otherwise;
- the output must distinguish directional evidence from statistically strong evidence.

## No fake rules

1. Acceptance is not learning.
2. Execution is not success.
3. Measured impact without measurement evidence is not allowed.
4. `not_measured` is not failure.
5. `pending` is not failure.
6. `null` denominators are not zero.
7. Small samples are not calibrated confidence.
8. Frontend must not invent a calibration result when the backend has not provided one.
9. Historical statistics must not rewrite the original recommendation snapshot.
10. Outcome learning must not weaken the existing no-fake-zero and no-fake-green rules.

## Validation examples

### Example 1: accepted but not executed

- `issuedCount = 1`
- `acceptedCount = 1`
- `executedCount = 0`
- `measuredCount = 0`

Interpretation:

- acceptance rate is `100%`
- execution rate is `0%`
- there is no outcome learning signal yet

### Example 2: executed but not measured

- `issuedCount = 1`
- `acceptedCount = 1`
- `executedCount = 1`
- `measuredCount = 0`
- `notMeasuredCount = 1`

Interpretation:

- the workflow happened
- the outcome evidence did not
- success/failure rates stay undefined or zero-denominator guarded

### Example 3: measured negative outcome

- `issuedCount = 1`
- `acceptedCount = 1`
- `executedCount = 1`
- `measuredCount = 1`
- `negativeCount = 1`

Interpretation:

- the recommendation is learning-eligible
- the measured result counts toward negative outcome rate
- calibration may be directional only if the cohort is still small

## Acceptance criteria for RL01

- one canonical lifecycle and outcome-learning vocabulary exists;
- acceptance, execution and measurement denominators are explicitly distinct;
- no-measurement cannot be counted as success or failure;
- small-sample statistics remain labeled as insufficient or directional;
- a future calibration interface can be implemented without changing runtime confidence yet;
- no ML or automatic score change has been introduced.
