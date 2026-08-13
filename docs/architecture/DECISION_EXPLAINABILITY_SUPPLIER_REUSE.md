# Decision Explainability Supplier Reuse Contract

Status: planning contract for DEX12
Date: 2026-08-13
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Reference contracts: `docs/architecture/DECISION_GRAPH_CONTRACT.md`, `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`

## Purpose

This contract freezes how Supplier Decision Hub can reuse the shared DEX vocabulary without inventing local tree, Why or evidence-snapshot semantics.

Supplier Decision Hub is the smallest non-reference reuse candidate because it already exposes explicit confidence, reliability, recommendation allowance, fallback metadata and report semantics.

## Supplier surfaces in scope

| Surface | Already present | Reuse meaning |
| --- | --- | --- |
| Summary/header | `recommendationAllowed`, `usedFallback`, `fallbackReason`, `fallbackReasonCode`, `dataCoverageStatus`, `dataNote`, `lastRefreshAtUtc`, `requestedDataset`, `effectiveDataset`, `effectivePeriodLabel` | Trust and freshness context for the current supplier view |
| Header explanation | `aiExplanation`, `topFeature1`, `topFeature2`, `topFeature3`, `recommendationCode`, `confidenceScore`, `supplierQualityIndex`, `mlSupplierScore` | Narrative support only; not a synthetic decision tree |
| Ranking row | `recommendationCode`, `confidenceScore`, `reliabilityPct`, `dataQualityStatus`, `statusReason`, `reasonCodes` | Backend-led recommendation and explanation vocabulary |
| Detail row | `confidenceAvailable`, `reliabilityAvailable`, `normalizedConfidence`, `reasonCodes`, `statusReason` | Explicit missing-state handling for the visible signal |
| Report payload | period, freshness, fallback, `confidenceAvailable`, `reliabilityAvailable`, `dataQualityStatus`, `reasonCodes` | Print/export rendering of the same authoritative supplier signal |

## Canonical reuse rules

1. Backend remains the source of truth for recommendation status, confidence, reliability, reason codes and data quality.
2. Missing confidence or reliability stays explicit. Do not coerce it into `0`, `100`, green or trusted.
3. `aiExplanation` and `topFeature*` are descriptive helpers, not a locally inferred Why panel.
4. Do not infer a decision tree from `reasonCodes`, `statusReason`, `topFeature*`, alternatives or report prose.
5. A report is a presentation of the supplier decision surface, not a second source of truth.
6. When `recommendationAllowed=false` or fallback is used, the UI must label the surface as helper or signal-based, not final recommendation truth.

## Gap matrix

| Surface | What is already present | What still blocks reuse | Reuse state |
| --- | --- | --- | --- |
| Summary/header | Trust metadata, freshness, fallback and dataset scope | No frozen supplier-specific Why contract or evidence snapshot | Partial |
| Ranking/detail | Confidence, reliability, reason codes and explicit unavailable states | No canonical supplier tree or deterministic explanation object | Partial |
| Report/print | Period, freshness, fallback and visible confidence gating | No versioned supplier evidence snapshot or alternative set contract | Partial |
| Executive consumer views | Can consume supplier output as downstream signal | Must not become a second semantic owner | Consumer only |

## Smallest next rollout

The smallest safe supplier rollout is a frozen supplier Why payload that reuses only backend-led fields:

- `recommendationCode`
- `confidenceScore`
- `reliabilityPct`
- `reasonCodes`
- `dataQualityStatus`
- `usedFallback`
- `fallbackReason`

That payload can be rendered consistently in summary, scorecard and report surfaces without authorizing runtime tree inference or snapshot behavior.

## Compatibility notes

- This document does not change runtime behavior.
- This document does not authorize new API shapes.
- This document does not replace `DECISION_GRAPH_CONTRACT.md` or `DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`.
- If a supplier surface lacks one of the fields above, the gap should stay visible instead of being synthesized away.

## References

- `docs/architecture/DECISION_GRAPH_CONTRACT.md`
- `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`
- `docs/qa/SUPPLIER_CONFIDENCE_CONTRACT_AUDIT.md`
- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
- `Klijent/clientapp/src/services/supplierDecisionReport.ts`
