# Product Decision Confidence Audit

Date: 2026-06-19 16:05:00 +02:00
Local HEAD: `3a0def1505f697c8188a5df785812229793c7a2d`

## Scope

- [docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md](../Analytics/DECISION_CONFIDENCE_CONTRACT.md)
- [docs/Analytics/ANALYTICS_DECISION_OS_ROADMAP.md](../Analytics/ANALYTICS_DECISION_OS_ROADMAP.md)
- [Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx](../../Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx)
- [Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx](../../Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx)
- [Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx](../../Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx)
- [Api/Endpoints/CachedAnalyticsEndpoints.cs](../../Api/Endpoints/CachedAnalyticsEndpoints.cs)
- [Klijent/clientapp/src/types/analytics.ts](../../Klijent/clientapp/src/types/analytics.ts)

## Summary

Product Decision Center is already the strongest reference implementation for the shared Phase 1 confidence contract.

The current implementation already does the important things the contract requires:

- `insufficient_data` does not render as high confidence.
- Missing impact remains nullable and is shown as unavailable, not `0 RSD`.
- Warning codes stay visible near the recommendation.
- `inputFreshnessStatus` is derived from backend data quality and freshness, not invented locally.
- Product recommendations send the backend-provided confidence and impact fields into action creation metadata instead of fabricating new values.

## What We Verified

### 1. Confidence mapping follows the backend contract

- The backend already computes `confidenceLevel`, `confidenceScore`, `primaryDrivers`, `warningCodes`, `expectedImpactRsd`, `impactWindowDays`, and `inputFreshnessStatus` for Product Decision Center rows.
- The frontend reads those fields directly and only falls back to score-based tiering when the explicit backend confidence level is missing.
- `insufficient_data` is preserved as its own tier and is never promoted to a high-confidence presentation.

### 2. Missing impact behavior is honest

- When `expectedImpactRsd` is missing, the row text says the expected impact is unavailable.
- The UI shows an inline warning instead of a fake `0 RSD`.
- Action creation metadata also keeps the impact fields nullable instead of replacing them with a fabricated zero.

### 3. Calibration inputs stay visible

- The recommendation drawer and row view show:
  - main drivers
  - warning codes
  - expected impact
  - impact window
  - risk if ignored
  - freshness of input
- That keeps the page explainable without inventing new confidence semantics.

## Current Calibration View

The current page does **not** compute a separate local calibration bucket.

That is intentional for Phase 1:

- Product Decision Center should expose confidence and explainability.
- Outcome calibration should be learned from the action outcome summary / ledger layer, not invented in the UI.

In other words:

- Product Decision Center = recommendation confidence surface
- Action Outcome Summary = calibration feedback surface

## Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - backend computes recommendation confidence, primary drivers, expected impact, impact window, and freshness
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
  - frontend renders backend confidence, warnings, impact, and freshness
- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx`
  - confirms strong confidence rendering and missing-impact handling
- `Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx`
  - confirms optional action status failure does not hide recommendations

## Known Limitations

- There is no separate frontend calibration bucket for Product Decision Center yet.
- That bucket belongs to a future learning layer, not to the recommendation row itself.
- The fallback from row-level numeric signals to the displayed expected impact should stay backend-led; if the backend changes its source fields, the page should continue to prefer backend truth over local heuristics.

## DEX09 Decision Tree Test Plan

The branch-path surface should stay contract-driven and backend-led.

- backend contract tests should verify that a branch path is emitted only for rule-based recommendations and that the rule-set/version metadata stays stable for the same evaluated path;
- frontend tests should verify that Product Decision Center renders the branch path when present and a plain no-tree state when it is absent;
- `ReasonCodes`, `PrimaryDrivers`, `ConfidenceBreakdown`, `EvidenceChain` and `AlternativeRecommendations` must not be used as a local fallback tree;
- the Why panel must remain readable even when the tree is unavailable.

## Verification

- `git diff --check` - pass
- `cd Klijent/clientapp && npm run test -- --run ProductDecisionCenterPage.confidence` - pass

## Conclusion

Q42 is complete as an audit.

No code changes were needed because the Product Decision Center already obeys the shared confidence contract closely enough for Phase 1.

## Next

- Q43 - Supplier confidence contract mapping
