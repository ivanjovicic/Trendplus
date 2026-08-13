# Recommendation Measurement Statistics Projection Contract

Status: planning contract for RL05
Date: 2026-08-12
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Source contract: `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`
Rollout plan: `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`

## Purpose

This contract defines the measurement-only projection surface for recommendation learning statistics.

It turns lifecycle and outcome evidence into a deterministic read model without changing live confidence, ranking or recommendation truth.

## Non-goals

- no runtime learning algorithm
- no confidence mutation
- no calibration output
- no ML model
- no schema migration
- no frontend-local scoring

## Canonical axes

The projection must keep two axes separate:

1. lifecycle: what happened to the recommendation
2. outcome evidence: whether the business result was actually measured

Do not collapse these axes into one status field.

## Canonical response shape

The smallest useful projection response is:

```json
{
  "meta": {
    "success": true,
    "generatedAtUtc": "2026-08-12T12:00:00Z",
    "periodFromUtc": "2026-07-01T00:00:00Z",
    "periodToUtc": "2026-07-31T23:59:59Z",
    "sampleSize": 148,
    "measuredSampleSize": 61,
    "warnings": ["small_measured_sample"],
    "emptyReason": null
  },
  "totals": {
    "issuedCount": 148,
    "acceptedCount": 92,
    "rejectedCount": 18,
    "ignoredCount": 38,
    "executedCount": 64,
    "measuredCount": 61,
    "notMeasuredCount": 3,
    "successCount": 34,
    "neutralCount": 11,
    "negativeCount": 16,
    "expectedImpactRsd": 1825000.0,
    "measuredImpactRsd": 944000.0,
    "measuredImpactSampleCount": 43
  },
  "byRecommendationType": [],
  "bySourceType": [],
  "byDataQuality": []
}
```

The shape may be extended with additional stable cohorts later, but the core fields above must remain readable and machine-stable.

## Meta contract

| Field | Meaning |
| --- | --- |
| `success` | Standard analytics success signal. |
| `generatedAtUtc` | When the projection was produced. |
| `periodFromUtc` / `periodToUtc` | Effective time window for the cohort. |
| `sampleSize` | Total rows considered by the projection. |
| `measuredSampleSize` | Rows with auditable outcome evidence. |
| `warnings` | Trust and sample-size warning codes. |
| `emptyReason` | Present only when the dataset is truly empty but still successful. |

## Totals contract

| Field | Meaning | Denominator rule |
| --- | --- | --- |
| `issuedCount` | Recommendations generated or recorded in the window | base lifecycle denominator |
| `acceptedCount` | Recommendations accepted into workflow | `issuedCount` |
| `rejectedCount` | Recommendations explicitly declined | `issuedCount` |
| `ignoredCount` | Recommendations that expired or were never accepted | `issuedCount` |
| `executedCount` | Recommendations whose action was carried out | `acceptedCount` |
| `measuredCount` | Executed actions with auditable outcome evidence | `executedCount` |
| `notMeasuredCount` | Executed actions without usable outcome evidence | `executedCount` |
| `successCount` | Measured outcomes that were positive | `measuredCount` |
| `neutralCount` | Measured outcomes that were materially neutral | `measuredCount` |
| `negativeCount` | Measured outcomes that were adverse | `measuredCount` |
| `expectedImpactRsd` | Expected impact sum across the cohort | nullable when the numerator is missing |
| `measuredImpactRsd` | Measured impact sum across the cohort | nullable when evidence is missing |
| `measuredImpactSampleCount` | Rows with usable measured impact | measured impact evidence only |

## Cohort dimensions

The projection may segment by any dimension already supported by the contract and rollout plan, including:

- recommendation family
- source type
- source key or stable entity family
- product / category / store / supplier when that dimension already exists
- data quality bucket
- freshness bucket

Segmentation is only valid when the same denominator meaning is preserved.

## Rules

1. `accepted` is not success.
2. `executed` is not success.
3. `not_measured` is not failure.
4. `pending` remains outside success and failure rates.
5. `measuredImpactRsd = null` means unknown, not zero.
6. Zero denominators stay explicit as `null`, `insufficient_evidence` or a warning code.
7. Small samples must emit a warning code such as `small_measured_sample`.
8. The projection must never mutate live recommendation confidence.
9. The projection must never invent a calibration result when the backend has not produced one.
10. Historical statistics must not rewrite the original recommendation snapshot.

## Output guidance

The future runtime implementation should be able to expose:

- a cohort summary;
- per-family breakdowns;
- per-source breakdowns;
- per-data-quality breakdowns;
- stable warning codes;
- explicit empty or insufficient-evidence states.

The projection must keep `empty`, `partial`, `warning` and `error` distinct.

## Compatibility notes

- This document does not change runtime behavior.
- This document does not authorize calibration or confidence mutation.
- This document is the measurement-only companion to `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`.

## References

- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`
- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`
- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`

## Completion note

- Date: 2026-08-13
- Status: DONE
- Changed files:
  - `docs/architecture/RECOMMENDATION_MEASUREMENT_STATISTICS_CONTRACT.md`
  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`
  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `node scripts/check-prompt-queues.mjs` - pass
  - `node scripts/check-planning-architecture.mjs` - fail (DEX and RL now have 0 READY prompts after advancing to DT06)
  - `git diff --check` - pass
- Remaining risk:
  - This is a docs-only measurement projection contract; runtime implementation still needs a later prompt.
- Next:
  - DT06 - Prepare Decision Timeline export and retrospective reporting contract
