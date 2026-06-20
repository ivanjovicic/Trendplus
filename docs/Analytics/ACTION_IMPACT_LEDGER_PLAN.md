# Action Impact Ledger Plan

Updated: 2026-06-18

## Purpose

Trendplus needs a durable ledger that connects each recommendation and action to:

- what we expected to happen
- what actually happened
- what evidence supports the result
- what we should learn next time

`Action Outcome Summary` is the aggregate read model. This document defines the per-action ledger that feeds it.

## Design goals

1. Preserve the original recommendation snapshot at action creation.
2. Separate workflow resolution from business outcome measurement.
3. Keep nullable impact fields nullable when evidence is missing.
4. Make confidence and impact explainable without inventing frontend values.
5. Support learning and calibration without changing action workflow semantics.

## Current repo baseline

The repo already has most of the operational action model in `AnalyticsActionItem`:

- `SourceType`
- `SourceKey`
- `SourceId`
- `Title`
- `Description`
- `RecommendationStatus`
- `Priority`
- `ImpactEstimateRsd`
- `ExpectedImpactRsd`
- `MeasuredImpactRsd`
- `OutcomeStatus`
- `OutcomeMeasuredAtUtc`
- `OutcomeNotes`
- `ConfidencePct`
- `ReliabilityPct`
- `DataQualityStatus`
- `Status`
- `ActionUrl`
- `MetadataJson`
- `CreatedAtUtc`
- `UpdatedAtUtc`
- `ResolvedAtUtc`
- `CreatedByUserId`
- `UpdatedByUserId`
- `UpdatedByUserName`
- `Notes`

The existing action outcome summary also already aggregates:

- expected impact
- measured impact
- realization ratio
- warning codes
- confidence / reliability cohorts

What is still missing is a stable, durable recommendation snapshot and an explicit measurement history that can be read back as a ledger.

## Ledger model

Use one logical ledger with three layers:

| Layer | Purpose | Current storage target | Notes |
|---|---|---|---|
| Creation snapshot | Capture the recommendation exactly as it was shown to the operator | `AnalyticsActionItem` plus structured metadata | Should be immutable after creation |
| Resolution event | Capture workflow completion and measured result | `AnalyticsActionItem` outcome fields plus notes | May be updated over time, but every change should leave an audit trail |
| Derived read model | Compute ratios, delta and calibration signals | Action outcome summary / future projection | Never the source of truth for raw values |

## What is stored at action creation

Store the following when the action is created:

| Field | Meaning | Required | Notes |
|---|---|---:|---|
| `sourceRecommendationId` | Stable recommendation identifier | yes | Derived from source + recommendation family + effective period if the source has no native ID |
| `sourceType` | Recommendation origin | yes | Example: `product`, `inventory`, `supplier`, `dashboard` |
| `sourceKey` | Stable business key | yes | Must be idempotent and stable across retries |
| `recommendationType` | Canonical recommendation family | yes | Example: `REPLENISH`, `MARKDOWN`, `NEGOTIATE`, `SIGNAL_REVIEW` |
| `expectedImpactRsd` | Expected financial impact | recommended | Nullable if denominator or evidence is missing |
| `expectedImpactBasis` | Why the impact was expected | yes | Short structured basis, not a long essay |
| `impactWindowDays` | Time window for expected impact | recommended | Nullable until a module can model it reliably |
| `confidenceLevel` | `high`, `medium`, `low`, `insufficient_data` | yes | Derived from evidence quality, not invented in UI |
| `confidenceScore` | Numeric confidence score | recommended | Nullable if the module cannot compute it |
| `dataQualityStatus` | Trust state of the input data | yes | Canonical backend value |
| `warningCodes` | Warning and caveat codes | yes | Visible near the recommendation |
| `primaryDrivers` | Main decision drivers | yes | Example: sales velocity, margin, stock risk, trend |
| `decisionReason` | Human explanation of the recommendation | yes | Should be readable in business language |
| `recommendedAction` | Operator-facing action | yes | Example: "Dopuni", "Smanji", "Pregledaj" |
| `generatedAtUtc` | When the recommendation was produced | yes | Needed for freshness and replay |
| `inputFreshnessStatus` | Freshness of source inputs | yes | Example: fresh, stale, critical, unknown |
| `createdBy` | Who created the action | if available | Usually the creating user or service account |
| `acceptedBy` | Who accepted the action | if available | Capture when the workflow has an explicit acceptance step |

### Creation snapshot rules

- The snapshot must preserve the recommendation as it was shown at creation time.
- Later changes to the source recommendation must not rewrite the original snapshot.
- If a source does not expose a native `sourceRecommendationId`, derive one deterministically and store the derivation rule.
- `expectedImpactBasis` should be structured enough to support filtering and audit, but still readable by operators.

### Suggested `expectedImpactBasis` content

Keep the basis small and structured, for example:

- `sales_velocity + stock_risk`
- `margin + supplier_reliability`
- `missing_cost + sparse_sales`
- `manual_review_required`

If more detail is needed, put the short narrative in `decisionReason` and the machine-readable driver codes in `primaryDrivers`.

## What is stored at outcome resolution

Store the following when the action is resolved or measured:

| Field | Meaning | Required | Notes |
|---|---|---:|---|
| `outcomeStatus` | Outcome classification | yes | Example: pending, success, neutral, negative, not_measured |
| `measuredImpactRsd` | Actual measured impact | recommended | Nullable if the result is not measured |
| `measuredWindowDays` | Time window covered by the measurement | recommended | Nullable if the measurement window is not known |
| `evidenceSource` | Where the measurement came from | recommended | Example: report, import batch, manual note, calculation batch |
| `resolvedBy` | Who recorded the resolution | if available | Should be explicit when the workflow supports it |
| `resolvedAtUtc` | When the resolution happened | yes | Workflow resolution time |
| `outcomeMeasuredAtUtc` | When the business result was measured | recommended | May be later than resolution time |
| `resolutionNote` | Human note explaining the result | recommended | Can reuse existing notes or outcome notes |

### Resolution rules

- `outcomeStatus = pending` means the action is still open for learning.
- `pending` is not a failure.
- `resolvedAtUtc` is the workflow timestamp.
- `outcomeMeasuredAtUtc` is the business measurement timestamp.
- These timestamps must stay separate because they answer different questions.

### Evidence/source rules

The evidence field should point to something auditable, such as:

- a report route
- a calculation batch
- an import batch
- a manual review note

It should not expose secrets and it should not depend on a frontend-only string.

## What is computed

The ledger should compute the following read-only learning signals:

| Computation | Definition | Null rule |
|---|---|---|
| `realizationRatio` | `measuredImpactRsd / expectedImpactRsd` | `null` if expected or measured impact is missing, or if expected impact is not positive |
| `impactDeltaRsd` | `measuredImpactRsd - expectedImpactRsd` | `null` if either side is missing |
| `confidenceCalibrationBucket` | How well predicted confidence matched actual outcome | `null` or `insufficient_data` when sample size is too small |
| `recommendationQualitySignal` | A learning bucket that summarizes the recommendation result | Never a fake precision score; use a bucket or label |

### Recommended calibration buckets

Use a small set of buckets that is easy to explain:

- `well_calibrated`
- `over_confident`
- `under_confident`
- `insufficient_data`

### How to interpret them

- `well_calibrated` means high confidence outcomes usually behave as expected.
- `over_confident` means the recommendation sounded stronger than the measured result.
- `under_confident` means the recommendation worked better than its stated confidence suggested.
- `insufficient_data` means the sample is too small or the denominator is missing.

## No-fake rules

1. `expectedImpactRsd = null` is unknown, not zero.
2. `measuredImpactRsd = null` is unknown, not zero.
3. `impactDeltaRsd = null` is unknown, not zero.
4. `realizationRatio = null` when the denominator is missing or invalid.
5. `pending` outcomes must not be counted as failed outcomes.
6. Missing evidence must produce a warning, not a fake value.
7. Frontend must not invent confidence, impact or calibration numbers.
8. Historical ledger entries must not be rewritten to hide later learning.

## Recommended storage approach

### Phase 1

Keep the ledger as a logical projection on top of the existing action row plus notes:

- the action row holds the immutable creation snapshot and current outcome fields
- notes hold status transitions and resolution audit text
- the summary endpoint reads both to build aggregates and learning buckets

This is the smallest safe step because it uses current repo primitives without introducing a broad schema change.

### Phase 2

If the team later needs multiple outcome revisions per action, add a dedicated append-only ledger table:

- one record for creation snapshot
- one or more records for measurement updates
- one derived read model for summaries and UI

That split is only needed if the current row-plus-notes model becomes too limiting.

## UI targets

The ledger should be visible in these places:

| UI surface | What it should show |
|---|---|
| Action Queue detail | original recommendation snapshot, expected impact, measured impact, delta, outcome status, evidence and resolution actor/time |
| Action Outcome Summary | expected vs measured impact, realization ratio, calibration buckets, warning codes, sample size caveats |
| Product Decision Center row history | original recommendation, confidence level, expected impact, later outcome and learning signal |

### UI rules

- Show the original expectation next to the measured result.
- Show null or missing data as unknown.
- Keep warning codes visible near the recommendation or outcome.
- Do not let a row look "green" when the measurement is missing.

## Tests needed

### Backend unit tests

- creation stores the recommendation snapshot fields
- expected impact basis and drivers are preserved
- resolution stores measured impact, evidence and resolved timestamps
- `pending` outcome does not become failure
- `null` measured impact does not become zero
- realization ratio is `null` when the denominator is missing
- calibration bucket is `insufficient_data` for small or incomplete samples

### Backend integration tests

- create action -> resolve action -> summary returns the expected totals
- summary respects the creation snapshot and the measured outcome separately
- repeated updates leave a clear audit trail

### Frontend tests

- Action Queue detail renders expected vs measured values honestly
- missing impact does not show fake `0 RSD`
- calibration or confidence buckets are rendered from backend data, not computed locally
- history view shows the original recommendation snapshot and later outcome

## Implementation order

1. Lock the creation snapshot contract.
2. Lock the resolution contract.
3. Add or extend the read model for expected vs measured impact.
4. Render the ledger in Action Queue detail and summary surfaces.
5. Add Product Decision Center history exposure last.

## Non-goals

- No new recommendation algorithm.
- No auth refactor.
- No workflow redesign.
- No fake precision when data is missing.
- No supplier or inventory redesign in this plan.

## Acceptance for the next implementation task

The next implementation task can start when:

- the creation snapshot fields are agreed
- resolution fields are agreed
- null and pending semantics are locked
- the backend remains the source of truth for confidence and impact
- the UI can show the ledger without inventing values

