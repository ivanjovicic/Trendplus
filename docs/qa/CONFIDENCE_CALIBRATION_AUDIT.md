# Confidence Calibration Audit

Date: 2026-06-21 14:07:54 +02:00
Local HEAD: `18d843318c0aba8eb1b1c252b0070f8e50bac740`

## Scope

- [docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md](../Analytics/DECISION_CONFIDENCE_CONTRACT.md)
- [docs/Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md](../Analytics/ACTION_IMPACT_LEDGER_PHASE1_SPEC.md)
- [docs/qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md](./ACTION_IMPACT_LEDGER_GAP_REVIEW.md)
- [docs/qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md](./EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md)
- [Infrastructure/Services/Analytics/AnalyticsActionItemService.cs](../../Infrastructure/Services/Analytics/AnalyticsActionItemService.cs)
- [Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx](../../Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx)
- [Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx](../../Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx)
- [Klijent/clientapp/src/pages/InventoryPage.tsx](../../Klijent/clientapp/src/pages/InventoryPage.tsx)
- [Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx](../../Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx)

## Executive Summary

- Trendplus can already perform a **limited outcome audit** of action confidence using the existing Analytics Actions outcome summary.
- That audit is currently strongest at the **action-row level**:
  - `confidencePct`
  - `reliabilityPct`
  - `sourceType`
  - `priority`
  - `dataQualityStatus`
- Trendplus **cannot yet claim true recommendation calibration** across the full decision system because the canonical Phase 1 ledger fields are not yet written consistently by the main action-creation flows.
- Current confidence remains partly **descriptive** and partly **operational**, not fully **outcome-validated**.

## What Can Be Calibrated Now

### 1. Action Queue confidence buckets

The backend already exposes outcome summary cohorts for:

- `byConfidenceBucket`
- `byReliabilityBucket`
- `bySourceType`
- `byPriority`
- `byDataQuality`

That means Trendplus can already compare, for closed/measured actions:

- outcome coverage by confidence bucket
- positive vs negative outcome rate by confidence bucket
- expected vs measured impact by confidence bucket
- realization ratio by confidence bucket

Current backend bucket definitions:

- `<50`
- `50-69`
- `70-84`
- `85+`
- `unknown`

This is enough for a first-pass audit such as:

- do `85+` actions outperform `50-69` actions?
- do low-confidence actions produce more `not_measured` or negative outcomes?
- does measured impact realization degrade sharply in low-reliability buckets?

### 2. Source-level action calibration

Because outcome summary already groups by `sourceType`, Trendplus can audit whether actions created from:

- `product`
- `inventory`
- `supplier`
- `dashboard`
- `data_quality`
- `nivelacija`

show meaningfully different outcome quality.

This is useful for internal pilot operations even before full recommendation-level calibration exists.

### 3. Product Decision action follow-through

Product Decision Center is the closest surface to calibration-ready because it already has:

- backend confidence contract work
- `confidenceLevel`
- `warningCodes`
- `primaryDrivers`
- `inputFreshnessStatus`
- recommendation semantics strong enough to create actions

However, today those richer fields are still mostly preserved in page logic or free-form metadata rather than consistently written into the canonical ledger envelope at action creation time.

Result:

- **partial calibration is possible now**
- **canonical calibration is not complete yet**

## What Is Only Partially Calibratable

### Inventory

Inventory actions currently persist:

- `confidencePct` from `signalConfidencePct`
- `dataQualityStatus`
- basic action metadata

But they do **not** yet consistently persist:

- `confidenceLevel`
- `reliabilityPct`
- canonical `sourceRecommendationId`
- canonical `warningCodes`
- canonical `primaryDrivers`
- canonical `inputFreshnessStatus`

Result:

- bucket-level confidence audit is possible
- recommendation-contract calibration is still partial

### Supplier

Supplier actions currently persist:

- `confidencePct`
- `reliabilityPct`
- `dataQualityStatus`
- some supplier context inside `metadataJson`

But they do **not** yet consistently persist the Phase 1 ledger creation snapshot.

Result:

- source-level and bucket-level outcome review is possible
- canonical supplier confidence calibration is still partial

## What Is Not Yet Reliably Calibratable

### 1. Recommendation-level calibration by `sourceRecommendationId`

Q58 added backend support for:

- `sourceRecommendationId`
- `recommendationType`
- `confidenceLevel`
- `warningCodes`
- `primaryDrivers`
- `inputFreshnessStatus`

But the main frontend action creation flows are not yet consistently writing those explicit fields.

Without consistent `sourceRecommendationId`, Trendplus cannot yet answer:

- which recommendation families systematically overstate confidence
- whether repeated recommendation instances for the same source behave consistently over time
- whether the same recommendation type performs differently under different warning/freshness states

### 2. Decision Board calibration

Executive Decision Board is intentionally a composed decision surface, not a source-of-truth recommendation ledger.

It should not be calibrated directly yet because:

- it aggregates multiple sources
- it repeats source recommendations across sections by design
- it does not own stable recommendation execution identity

Calibration should happen at the underlying recommendation/action layer first, then be summarized for the board later.

### 3. Ignored recommendation denominator

Current outcome summary measures only actions that entered the action workflow.

That means Trendplus still lacks a clean denominator for:

- recommendations shown but never queued
- recommendations deferred indefinitely
- recommendations ignored outside the system

This creates selection bias:

- acted-on items are not a neutral sample of all recommendations

## Current Blocking Gaps

### 1. Small measured sample risk

The outcome summary already emits warnings such as:

- `small_sample`
- `small_measured_sample`
- `outcome_coverage_low`

This is the correct behavior.

It also means any current confidence calibration should be labeled:

- pilot-only
- directional
- not yet statistically stable

### 2. Confidence buckets use `confidencePct`, not canonical `confidenceLevel`

The current summary groups by numeric action-row `confidencePct`.

That is useful, but it is not the same as validating the canonical contract:

- `high`
- `medium`
- `low`
- `insufficient_data`

Until creation snapshots are written consistently and summarized directly, confidence-tier calibration remains incomplete.

### 3. Reliability and confidence semantics still vary by source

Product, Inventory, and Supplier do not yet share a single persisted calibration contract at write time.

So current bucket analysis can tell us:

- how action rows performed

but not always:

- whether each source used equivalent confidence semantics when the action was created

### 4. Outcome coverage is optional and incomplete

`pending`, `not_measured`, and missing measured impact are handled safely today, but they still reduce calibration power.

This is correct behavior, not a bug.

It means calibration should always report:

- total sample size
- measured sample size
- measured impact sample size
- outcome coverage rate

## Recommended Calibration Buckets

### Use now

For current pilot reviews, use these buckets:

#### Confidence percent buckets

- `<50`
- `50-69`
- `70-84`
- `85+`
- `unknown`

#### Reliability percent buckets

- `<50`
- `50-69`
- `70-84`
- `85+`
- `unknown`

#### Operational slices

- by `sourceType`
- by `priority`
- by `dataQualityStatus`

### Add later, once ledger writes are consistent

- by `confidenceLevel`
  - `high`
  - `medium`
  - `low`
  - `insufficient_data`
- by `recommendationType`
  - `REPLENISH`
  - `MARKDOWN`
  - `NEGOTIATE`
  - `SIGNAL_REVIEW`
- by `inputFreshnessStatus`
  - `fresh`
  - `stale`
  - `critical`
  - `unknown`
- by warning cohort
  - `missing_cost`
  - `sparse_sales`
  - `stale_refresh`
  - mixed warning count

## Recommended Metrics

### Metrics usable now

- `outcomeCoverageRate`
- `positiveOutcomeRate`
- `negativeOutcomeRate`
- `measuredImpactSampleCount`
- `expectedImpactRsd`
- `measuredImpactRsd`
- `realizationRatio`

### Metrics to add later

- positive outcome rate by canonical `confidenceLevel`
- realization ratio by `recommendationType`
- expected-vs-measured calibration error by confidence cohort
- warning-conditioned calibration drift
- freshness-conditioned calibration drift
- operator override / rejection rate by confidence cohort

## Surface-by-Surface Readiness

| Surface | Can calibrate now? | Why | Main blocker |
| --- | --- | --- | --- |
| Analytics Actions outcome summary | Yes, partially | Existing summary already groups by confidence/reliability/source/priority/data quality | Small samples and selection bias |
| Product Decision Center | Partially | Strongest confidence contract and action linkage | Explicit ledger creation fields are not yet consistently written |
| Inventory | Partially | Action workflow exists and confidence percent is persisted | Missing canonical confidence-level and ledger snapshot fields |
| Supplier Decision Hub | Partially | Confidence and reliability reach the action row | Canonical ledger snapshot not consistently written |
| Executive Decision Board | No, not directly | Composite surface, not recommendation source of truth | No stable recommendation execution identity |

## Recommended Next Steps

1. Keep Q60 as a docs audit only; do not invent calibration UI yet.
2. Use current outcome summary for pilot reviews, but label it as directional.
3. In Q57-Q59 follow-up work, make all primary action creation flows write:
   - `sourceRecommendationId`
   - `recommendationType`
   - `confidenceLevel`
   - `warningCodes`
   - `primaryDrivers`
   - `inputFreshnessStatus`
4. After ledger writes are consistent, extend outcome summary with canonical calibration cohorts.
5. Only after that, add user-facing confidence calibration summaries.

## Conclusion

Trendplus is ready for a **first calibration audit**, but not yet for a **full trust claim** that recommendation confidence has been outcome-validated across the whole decision system.

Current truth:

- action-level confidence auditing exists
- canonical recommendation-level calibration is still incomplete
- small samples and incomplete outcome coverage must stay visible

That means Q60 should be considered successful as an audit, while implementation follow-up remains necessary before confidence can be marketed as learned or calibrated.

## Verification

- `git diff --check` - pending until task completion
