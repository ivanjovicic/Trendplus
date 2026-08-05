# Inventory Signal Confidence Contract (Decision Board)

Date: 2026-08-04  
Repo: `ivanjovicic/Trendplus`  
Related: RQ10, R10 in `ANALYTICS_DATA_RELIABILITY_AUDIT.md`

## Purpose

Define what Decision Board inventory cards may claim as confidence, given that workflow suggestions currently lack evidence-grade confidence fields.

## Source of truth layers

| Layer | Location | Confidence meaning |
|---|---|---|
| Inventory list/signal rows | `InventorySignalCalculator` → `SignalConfidencePct`, `RecommendationAllowed`, reason codes | Evidence-based (stock cover, sell-through, DQ, sufficient data) |
| Inventory action workflow | `InventoryActionSuggestionDto` | Operational queue status only (`pending` / `approved` / `deferred` / `closed`) |
| Decision Board inventory cards | `DecisionBoardEndpoints.BuildInventoryCards` | Must not upgrade workflow status into evidence-grade confidence |

## Current Decision Board mapping (after RQ10)

`ResolveInventoryBoardConfidence(status)`:

| Workflow `Status` | Board `ConfidenceLevel` | Board `DataQualityStatus` | Notes |
|---|---|---|---|
| `approved` | `low` | `warning` | Operator accepted suggestion; still not evidence-backed |
| `deferred` | `low` | `warning` | Deferred, still not evidence-backed |
| `pending` / other | `insufficient_data` | `insufficient_data` | No operator confirmation and no evidence fields on DTO |

Always:

- `ConfidenceScore` = `null` (no fabricated %)
- `ReliabilityPct` = `null`
- Warning codes include `confidence_workflow_status_only` plus `ActionType` and `Status`

### Before RQ10 (unsafe)

- `approved` → `medium` (overstated vs available evidence)
- `deferred` → `low`
- else → `insufficient_data`
- DQ: `pending` → `insufficient_data`, else `warning`
- No explicit “workflow-only” warning

## Evidence fields missing on workflow DTO

`InventoryActionSuggestionDto` currently exposes operational fields (`Priority`, `EstimatedValue`, `DaysSinceMovement`, `Status`, …) but **not**:

- `SignalConfidencePct` / confidence level from `InventorySignalCalculator`
- `RecommendationAllowed`
- `DataQualityStatus` / reason codes from signal calculation
- Velocity / avg daily sales / stock-cover days / sell-through
- Movement freshness beyond `DaysSinceMovement`
- Calculation source / dataset version / last refresh of underlying inventory signal

Until those exist (or board joins insights rows by SKU/store), board confidence must stay capped at `low` / `insufficient_data`.

## Product rules

1. Workflow status is **not** evidence quality.
2. Board must not present inventory cards as `medium`/`high` without signal evidence fields.
3. Priority may still use operational `Priority` + value + aging for ranking, but confidence labeling stays conservative.
4. Inventory list/insights remain the place for evidence-based `SignalConfidencePct`.

## Follow-up

**RQ10-F1 / RQ13** — extend `InventoryActionSuggestionDto` (or board join) with signal confidence + recommendationAllowed + reason codes, then map board cards from those fields and drop `confidence_workflow_status_only` when evidence is present.
