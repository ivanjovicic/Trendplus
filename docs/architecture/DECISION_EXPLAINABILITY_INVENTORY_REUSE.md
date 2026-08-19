# Decision Explainability Inventory Reuse Contract

Status: frozen contract for DEX15
Date: 2026-08-13
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Reference contracts: `docs/architecture/DECISION_GRAPH_CONTRACT.md`, `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`, `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`

## Purpose

This contract freezes how inventory decision surfaces reuse the shared DEX vocabulary without inventing local tree, Why or workflow semantics.

## Detail and insight snapshot contract

Inventory detail and insight surfaces use a backend-led snapshot summary rather than a locally invented explanation tree.

- `InventoryItemDetailDto` keeps the item-level facts and history visible; any explanation text on the detail surface must come from backend-led inventory evidence, not from age or quantity heuristics.
- `InventoryInsightsDto` stays an aggregate view; top items and totals remain factual rollups, while any explanation framing must still come from backend-led signal fields.
- `InventoryInsightItemDto` may surface the same snapshot vocabulary as the list and workflow surfaces, but it must not invent a local decision score, Why panel or recommendation rule.
- If a backend-led field is absent, the gap stays explicit instead of being synthesized into a confident or green default.
- This snapshot is a presentation contract only; it does not authorize a new API shape by itself.

## Inventory surfaces in scope

| Surface | Already present | Reuse meaning |
| --- | --- | --- |
| Inventory list rows | `InventoryListItemDto`: `StockCoverDays`, `StockCoverStatus`, `StockCoverStatusLabel`, `SellThroughRatio`, `SellThroughStatus`, `SellThroughStatusLabel`, `SignalConfidencePct`, `RecommendationAllowed`, `ReasonCodes`, `DataQualityStatus` | Backend-led signal for list ranking and guidance, not a local workflow truth |
| Workflow suggestions | `InventoryActionSuggestionDto`: `SignalConfidencePct`, `RecommendationAllowed`, `SignalDataQualityStatus`, `SignalReasonCodes`, `Status`, `Note`, `UpdatedAtUtc` | Signal-backed suggestion with explicit fallback handling; workflow status is not evidence |
| Decision Board inventory cards | `ResolveInventoryBoardConfidence(...)` and the inventory board card composition | Board may consume signal evidence when present, otherwise fall back to workflow-status-only confidence with a visible warning |
| Inventory detail and insight surfaces | `InventoryItemDetailDto`, `InventoryInsightsDto`, `InventoryInsightItemDto` | Consumer surfaces for inventory state and rollups, not a synthetic explainability tree |

## Canonical reuse rules

1. Backend remains the source of truth for confidence, recommendation allowance, reason codes, data quality and fallback classification.
2. Missing signal evidence stays explicit.
3. Workflow status is not evidence.
4. Do not infer a decision tree from stock cover or sell-through status.
5. `confidence_workflow_status_only` is a fallback warning only.
6. `RecommendationAllowed=false` remains visible as a constraint.

## Gap matrix

| Surface | What is already present | What still blocks reuse | Reuse state |
| --- | --- | --- | --- |
| Inventory list rows | Signal confidence, recommendation allowed, status labels and data quality | No frozen inventory Why payload or tree | Partial |
| Workflow suggestions | Backend-led signal evidence and explicit reason codes | No canonical inventory decision-tree contract | Partial |
| Decision Board cards | Board can read signal evidence or fall back on workflow status | Fallback-only behavior still needs clear surface language | Partial |
| Detail/insight surfaces | Inventory facts and rollups | Frozen snapshot contract still needs runtime rendering on the detail and insight surfaces | Snapshot contract frozen |

## Smallest next rollout

The smallest safe inventory rollout is a runtime slice that renders the frozen explainability snapshot using only backend-led fields:

- `SignalConfidencePct`
- `RecommendationAllowed`
- `SignalDataQualityStatus`
- `SignalReasonCodes`
- `StockCoverStatus`
- `SellThroughStatus`
- `DataQualityStatus`
- `ReasonCodes`
- `Status`
- `Note`

The list above is the allowed vocabulary for the snapshot. It should be rendered as evidence-backed inventory context, not as a synthetic tree or locally derived recommendation.

## Compatibility notes

- This document does not change runtime behavior.
- This document does not authorize new API shapes.
- If a surface lacks one of the fields above, the gap should stay visible instead of being synthesized away.

## References

- `docs/architecture/DECISION_GRAPH_CONTRACT.md`
- `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`
- `docs/qa/INVENTORY_SIGNAL_CONFIDENCE_CONTRACT.md`
- `Api/Dtos/InventoryListItemDto.cs`
- `Api/Dtos/InventoryExperienceDtos.cs`
- `Api/Endpoints/InventoryEndpoints.cs`
- `Api/Endpoints/DecisionBoardEndpoints.cs`
