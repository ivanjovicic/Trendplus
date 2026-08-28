# Recommendation Advisory Calibration Contract

Status: authoritative RL10 contract
Date: 2026-08-19
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Related source contracts:

- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`
- `docs/Analytics/RECOMMENDATION_MEASUREMENT_STATISTICS_CONTRACT.md`
- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`

Related rollout plan:

- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`

Related runtime gate:

- `docs/architecture/RECOMMENDATION_ADVISORY_CALIBRATION_RUNTIME_GATE_CONTRACT.md`

Related audit and review inputs:

- `docs/qa/CONFIDENCE_CALIBRATION_AUDIT.md`
- `docs/architecture/RECOMMENDATION_MEASUREMENT_STATISTICS_REVIEW_SURFACE.md`
- `Application/Analytics/RecommendationMeasurementStatisticsDto.cs`

## Purpose

Trendplus needs a frozen advisory calibration contract for a later learning slice, but that contract must stay advisory only. It can describe how measured outcome cohorts would be reviewed, yet it must not mutate live confidence, ranking or recommendation text.

This contract is intentionally narrow:

- it defines the cohort inputs a future calibration service should consume;
- it defines the advisory-only outputs a future calibration service may emit;
- it makes explicit that later human or roadmap approval is still required before any score mutation;
- it stays safe to ignore without changing product behavior.

## Non-goals

- no automatic confidence change
- no live ranking mutation
- no runtime ML model
- no schema migration
- no write-back into recommendation rows
- no frontend-local thresholds
- no treating acceptance as calibration evidence
- no treating insufficient coverage as healthy calibration

## Canonical cohort input

A future calibration job should receive a deterministic cohort record built from the measurement-only statistics contract.

| Field | Meaning |
|---|---|
| `cohortKey` | Stable key for one calibration cohort. |
| `recommendationType` | Canonical recommendation family such as `REPLENISH`, `MARKDOWN`, `NEGOTIATE`, `SIGNAL_REVIEW`. |
| `sourceType` | Origin family such as `product`, `inventory`, `supplier` or `dashboard`. |
| `sourceKey` | Stable business key or entity segment for the cohort. |
| `issuedCount` | Recommendations issued into the cohort. |
| `acceptedCount` | Recommendations accepted by workflow or operator. |
| `rejectedCount` | Recommendations explicitly declined. |
| `ignoredCount` | Recommendations that expired or never entered workflow. |
| `executedCount` | Recommendations that were actually carried out. |
| `measuredCount` | Rows with measurable outcome evidence. |
| `measuredImpactSampleCount` | Rows with usable measured-impact values. |
| `successCount` | Measured positive outcomes. |
| `neutralCount` | Measured neutral outcomes. |
| `negativeCount` | Measured negative outcomes. |
| `notMeasuredCount` | Rows that still lack usable evidence. |
| `measurementWindowDays` | Window length used for measurement comparison. |
| `attributionStartUtc` | First timestamp included in the cohort. |
| `attributionEndUtc` | Last timestamp included in the cohort. |
| `warningCodes` | Machine-readable caveats from the upstream statistics contract. |
| `dataQualityStatus` | Trust state of the cohort inputs. |
| `confidenceBucket` | Confidence segmentation used for cohort analysis. |
| `freshnessBucket` | Freshness segmentation used for cohort analysis. |

The cohort record must preserve the same denominators as the measurement statistics contract. It must not recompute or invent them in the frontend.

## Eligibility rules

Calibration output must remain conservative.

| Eligibility | Meaning |
|---|---|
| `eligible` | Enough measured evidence exists to review a directional advisory result. |
| `directional_only` | Evidence exists, but the cohort is still too weak for a stable score mutation proposal. |
| `insufficient_evidence` | Missing or sparse coverage prevents even directional review from being trusted. |

### Minimum evidence guardrails

- fewer than 10 measured outcomes -> `insufficient_evidence`
- fewer than 5 measured-impact samples -> `insufficient_evidence`
- missing attribution window metadata -> `insufficient_evidence`
- mixed family or incompatible source segmentation -> `directional_only` at best
- missing coverage, missing evidence or `insufficient_data` trust state must never become a healthy-looking calibration result

## Advisory output

A future deterministic calibration result may return advisory metadata only.

| Field | Meaning |
|---|---|
| `calibrationEligibility` | One of `eligible`, `directional_only` or `insufficient_evidence`. |
| `calibrationDirection` | One of `increase`, `decrease`, `no_change` or `unknown`. |
| `adjustmentHintPct` | Signed bounded hint in percentage points, clamped to a small range such as `-5` to `+5`. |
| `reasonCodes` | Machine-readable reasons explaining the advisory result. |
| `measuredImpactSampleCount` | Count of rows with usable measured-impact evidence. |
| `sampleSummary` | Human-readable summary of the denominators and coverage. |
| `approvalRequired` | `true` until a later explicit approval gate authorizes mutation. |
| `autoApplyAllowed` | `false` for this contract. |

### Canonical advisory reason codes

Use a stable vocabulary so future evidence and UI layers can render the same meaning without guessing.

- `insufficient_measured_outcomes`
- `insufficient_measured_impact`
- `missing_attribution_window`
- `mixed_family_segment`
- `coverage_incomplete`
- `missing_data_quality`
- `approval_required`
- `mutation_not_authorized`
- `directional_only`

## Ignore-safely rules

The advisory output must be safe to ignore.

1. If a consumer drops the advisory result entirely, live confidence, ranking and recommendation text stay unchanged.
2. If the advisory result is present but not approved, it still must not mutate the live score.
3. If the cohort is `insufficient_evidence`, the contract must not emit a fake-green calibrated rate.
4. If coverage is missing, the contract must not convert the gap into `0%` or another healthy-looking default.
5. If the advisory result is later approved by a separate prompt, that approval must happen outside this contract and with explicit traceability.

## Relationship to current repo primitives

The current repository already exposes the measured-input building blocks needed by this contract:

- `Application/Analytics/RecommendationMeasurementStatisticsDto.cs`
- `docs/Analytics/RECOMMENDATION_MEASUREMENT_STATISTICS_CONTRACT.md`
- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`
- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`
- `docs/qa/CONFIDENCE_CALIBRATION_AUDIT.md`

This document does not authorize runtime code. It freezes the advisory contract so a later implementation prompt can consume it without redefining the business rules.

## Acceptance

- one citeable Slice 4 advisory calibration contract exists;
- the contract can be ignored without changing product behavior;
- live confidence, ranking and recommendation text remain unchanged;
- insufficient coverage stays ineligible, not healthy-looking or zeroed;
- later explicit approval is required before any score mutation;
- READY pointer remains single for RL.
