# Decision Explainability Cross-Family Readiness Contract

Status: planning contract for DEX11
Date: 2026-08-12
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Reference contract: `docs/architecture/DECISION_GRAPH_CONTRACT.md`

## Purpose

This contract defines when another decision family may reuse the Product Decision Center explainability vocabulary without inventing local scoring, local tree logic, or local fallback semantics.

The reference point is Product Decision Center because it already owns the richest deterministic explanation surface:

- decision identity and source correlation;
- evidence and trust contributors;
- confidence breakdown;
- alternatives;
- Why panel inputs;
- decision-tree availability / absence;
- immutable evidence snapshots.

## Scope

The readiness check covers these families:

- Product Decision Center
- Supplier Decision Hub
- Inventory decision surfaces
- Executive Decision Board

The contract is about reuse readiness, not runtime authorization. A family can be explainable and still not be ready to own the shared contract as a first-class source of truth.

## Readiness checklist

A family is ready for explainability reuse only when all of the following are true:

1. The backend, not the frontend, owns recommendation status, confidence, reliability, reason codes and data-quality semantics.
2. Missing, unknown, stale or fallback states remain explicit instead of collapsing into zero, green or confident defaults.
3. The family can expose deterministic Why inputs without requiring local inference.
4. Alternatives are explicit objects or explicit absence, never inferred from prose or reason codes.
5. Decision-tree availability is explicit as `available`, `unavailable`, or `unknown`.
6. Evidence snapshot or historical replay needs are either documented or deliberately out of scope.
7. Table, detail, report and action surfaces agree on the same underlying business meaning.
8. Any workflow-only fallback is clearly labeled so it cannot be mistaken for evidence-backed recommendation truth.

## Family gap matrix

| Family | Current explainability surface | What is already present | What still blocks reuse | Readiness inference |
| --- | --- | --- | --- | --- |
| Product Decision Center | Reference implementation | `RecommendationStatus`, `RecommendationLabel`, `RecommendedAction`, `RecommendationReason`, `ReasonCodes`, `PrimaryDrivers`, `ConfidencePct`, `ReliabilityPct`, `ConfidenceBreakdown`, `DataQualityStatus`, `InputFreshnessStatus`, `RecommendationAllowed`, `AlternativeRecommendations`, `decisionTree`, evidence snapshot support | None for the first-family contract; this is the baseline | Ready as the source contract |
| Supplier Decision Hub | Trust-first supplier surfaces and report output | Confidence/reliability mapping, `recommendationAllowed`, fallback metadata, warning codes, report surfaces, explicit missing-confidence behavior documented in `docs/qa/SUPPLIER_CONFIDENCE_CONTRACT_AUDIT.md` | No shared decision-tree / Why-panel / evidence-snapshot contract is frozen for the supplier family; some surfaces remain signal- or report-shaped rather than full decision surfaces | Closest non-reference candidate, but still partial |
| Inventory decision surfaces | Signal and workflow surfaces | Evidence-backed inventory signal fields, `SignalConfidencePct`, `RecommendationAllowed`, `SignalDataQualityStatus`, `SignalReasonCodes`, conservative board fallback documented in `docs/architecture/DECISION_GRAPH_CONTRACT.md` and `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`, frozen detail/insight snapshot contract documented in `docs/architecture/DECISION_EXPLAINABILITY_INVENTORY_REUSE.md` | Workflow-only fallback can still own the visible confidence story on some surfaces; the family still needs runtime wiring so the snapshot and workflow truth stay visibly separated | Partial |
| Executive Decision Board | Aggregate consumer with frozen board reuse contract | Downstream aggregate fields such as `ConfidenceLevel`, `ConfidenceScore`, `ReliabilityPct`, `ExpectedImpactRsd`, `MeasuredImpactRsd`, `RealizationRatio`, `RiskIfIgnored`, `RecommendedNextAction`, `WarningCodes`, `DataQualityStatus` | The board is still a consumer, not an independent truth source; DEX18 freezes the board-specific reuse contract, but runtime wiring remains later | Contract frozen; runtime pending |

## Shared gap rules

- Do not add local confidence formulas in pages or cards.
- Do not infer a decision tree from reason codes, alternatives or evidence text.
- Do not convert workflow status into evidence-backed confidence without an explicit backend contract.
- Do not mark a family ready just because it can display a subset of the contract.
- Do not hide missing context behind a visually complete but semantically weaker fallback.

## Smallest next rollout

Inference from the current docs:

1. Supplier Decision Hub is the smallest cross-family explainability rollout candidate because it already has explicit confidence, reliability, recommendation allowance and report semantics.
2. Inventory should follow with a stricter evidence-vs-workflow separation because its current fallback model can still blur operational status with decision truth, even though the detail/insight snapshot contract is now frozen.
3. Executive Decision Board should remain a consumer of the canonical contract, not a separate source of truth, even now that the board reuse contract is frozen.

This is an inference from the current contract and audit files, not a runtime change request.

## Compatibility notes

- This document does not change runtime behavior.
- This document does not authorize new API shapes.
- This document does not replace the Product Decision Center contract.
- If a future family lacks one of the checklist items above, the gap should stay visible instead of being synthesized away.

## References

- `docs/architecture/DECISION_GRAPH_CONTRACT.md`
- `docs/architecture/DECISION_EXPLAINABILITY_EXECUTIVE_BOARD_REUSE.md`
- `docs/qa/PRODUCT_DECISION_CONFIDENCE_AUDIT.md`
- `docs/qa/SUPPLIER_CONFIDENCE_CONTRACT_AUDIT.md`
- `docs/architecture/DECISION_GRAPH_CONTRACT.md`
- `docs/architecture/DECISION_TIMELINE_CONTRACT.md`
- `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`
