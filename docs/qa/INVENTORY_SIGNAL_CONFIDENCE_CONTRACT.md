# Inventory Signal Confidence Contract (Decision Board)

Date: 2026-08-05
Repo: `ivanjovicic/Trendplus`  
Related: RQ10, RQ13, R10 in `ANALYTICS_DATA_RELIABILITY_AUDIT.md`

## Purpose

Define what Decision Board inventory cards may claim as confidence, given workflow suggestions and optional signal evidence from `InventorySignalCalculator`.

## Source of truth layers

| Layer | Location | Confidence meaning |
|---|---|---|
| Inventory list/signal rows | `InventorySignalCalculator` → `SignalConfidencePct`, `RecommendationAllowed`, reason codes | Evidence-based (stock cover, sell-through, DQ, sufficient data) |
| Inventory action workflow | `InventoryActionSuggestionDto` | Operational queue status + optional signal evidence fields (RQ13) |
| Decision Board inventory cards | `DecisionBoardEndpoints.BuildInventoryCards` | Evidence when present; workflow-only fallback when absent |

## Decision Board mapping (after RQ13)

`ResolveInventoryBoardConfidence(InventoryActionSuggestionDto item)`:

### Path A — signal evidence present (`SignalConfidencePct` set)

| Condition | Board `ConfidenceLevel` | Board `DataQualityStatus` | Notes |
|---|---|---|---|
| `RecommendationAllowed == false` | `insufficient_data` | signal DQ (or `warning` if signal DQ was `good`) | Warning `inventory_recommendation_blocked`; score preserved |
| else | from `ResolveConfidenceLevel(SignalConfidencePct)` | `SignalDataQualityStatus` (fallback `warning` / `insufficient_data`) | No `confidence_workflow_status_only`; `SignalReasonCodes` copied to warnings |

Also sets:

- `ConfidenceScore` = `SignalConfidencePct`
- `ReliabilityPct` = rounded/clamped score 0–100

### Path B — no signal evidence (workflow fallback, RQ10)

`ResolveInventoryBoardConfidenceFromWorkflow(status)`:

| Workflow `Status` | Board `ConfidenceLevel` | Board `DataQualityStatus` | Notes |
|---|---|---|---|
| `approved` | `low` | `warning` | Operator accepted suggestion; still not evidence-backed |
| `deferred` | `low` | `warning` | Deferred, still not evidence-backed |
| `pending` / other | `insufficient_data` | `insufficient_data` | No operator confirmation and no evidence fields on DTO |

Always on Path B:

- `ConfidenceScore` = `null`
- `ReliabilityPct` = `null`
- Warning codes include `confidence_workflow_status_only`

### Before RQ10 (unsafe)

- `approved` → `medium` (overstated vs available evidence)
- No explicit “workflow-only” warning

## Workflow DTO evidence fields (RQ13)

`InventoryActionSuggestionDto` optional fields populated in `InventoryEndpoints.BuildActionWorkflowAsync` / `ToSuggestion`:

- `SignalConfidencePct`
- `RecommendationAllowed`
- `SignalDataQualityStatus`
- `SignalReasonCodes`

Computed via `ComputeSuggestionSignalEvidence`, mirroring inventory list signal logic using `InventorySignalCalculator` and movement/sales window stats.

## Product rules

1. Workflow status is **not** evidence quality when signal fields are absent.
2. Board must not present inventory cards as `medium`/`high` without `SignalConfidencePct` on the DTO.
3. When `RecommendationAllowed == false`, board confidence is capped at `insufficient_data` even if a score exists.
4. Priority may still use operational `Priority` + value + aging for ranking; confidence labeling follows evidence or conservative fallback.
5. Inventory list/insights remain the canonical drill-down for full signal context.
