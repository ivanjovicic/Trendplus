# Decision Board Backend Aggregate Contract

Updated: 2026-06-19
Review HEAD: `c79f50bc87a98962c3da51dcb3e9bb8f30272017`

## Purpose

Q46 defines the read-only backend aggregate contract for the Executive Decision Board. This is a contract design step only. It does not implement the endpoint.

## Inputs reviewed

- [Analytics Decision OS Roadmap](ANALYTICS_DECISION_OS_ROADMAP.md)
- [Executive Decision Board Plan](EXECUTIVE_DECISION_BOARD_PLAN.md)
- [Decision Confidence Contract](DECISION_CONFIDENCE_CONTRACT.md)
- [Action Impact Ledger Plan](ACTION_IMPACT_LEDGER_PLAN.md)
- [Decision Board Backend Aggregate Readiness Review](../qa/DECISION_BOARD_BACKEND_AGGREGATE_READINESS.md)
- [Executive Decision Board Quality Audit](../qa/EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md)
- [ExecutiveDecisionBoardPage.tsx](../../Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx)
- [ExecutiveDecisionBoardPage.spec.ts](../../Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts)

## Current phase 1 reference model

The current frontend board is already stable enough to serve as the reference model for the server contract:

- seven sections are established and tested
- stale, partial, and error states remain visible
- `insufficient_data` does not outrank real decision cards
- missing expected impact remains nullable, not `0 RSD`
- repeated source cards are intentional when section context matters
- action state and source links stay explicit

The backend contract should preserve this behavior rather than redefining it.

## Contract goals

1. Keep the board read-only.
2. Preserve nullable confidence, impact, and freshness semantics.
3. Keep partial and stale source states visible.
4. Keep section context and source links traceable.
5. Let the backend own the business semantics while the frontend keeps labels and copy derived from those fields.

## Suggested response envelope

| Field | Type | Purpose |
| --- | --- | --- |
| `generatedAtUtc` | `string` | Freshness anchor for the full board snapshot. |
| `periodFromUtc` | `string?` | Shared decision window start. |
| `periodToUtc` | `string?` | Shared decision window end. |
| `lastRefreshAtUtc` | `string?` | Last successful refresh across the board inputs. |
| `overallDataQualityStatus` | `string` | Combined trust state for the snapshot. |
| `recommendationNote` | `string` | Short explanation of board composition and fallback behavior. |
| `warnings` | `string[]` | Snapshot-level warning codes. |
| `metrics` | `DecisionBoardMetricDto[]` | Executive summary counters. |
| `sourceStates` | `DecisionBoardSourceStateDto[]` | Honest status for the source modules behind the board. |
| `sections` | `DecisionBoardSectionDto[]` | The seven board lanes with already-ranked cards. |

## Source state model

The aggregate should expose source health explicitly so a partial module never looks like a clean success.

| Field | Type | Purpose |
| --- | --- | --- |
| `sourceKey` | `string` | Stable identifier for the module or upstream payload. |
| `displayName` | `string` | Human-readable module name. |
| `status` | `string` | Example values: `good`, `warning`, `critical`, `insufficient_data`, `unknown`. |
| `generatedAtUtc` | `string?` | Freshness anchor for that source. |
| `warningCodes` | `string[]` | Visible caveats for that source. |
| `message` | `string?` | Optional short explanation of the state. |
| `sourceLink` | `string?` | Link back to the originating screen. |

This is the contract-level replacement for scattered frontend fallback logic.

## Section model

| Field | Type | Purpose |
| --- | --- | --- |
| `key` | `string` | Stable section key such as `urgent`, `impact`, `stockRisk`, `supplierRisk`, `blockers`, `actionsDecision`, `actionsOutcome`. |
| `title` | `string` | Section heading. |
| `description` | `string` | One-line business description. |
| `sourceLink` | `string` | Where the operator can inspect the source surface. |
| `emptyMessage` | `string` | Honest empty-state copy. |
| `warnings` | `string[]` | Optional section-specific warning codes. |
| `cards` | `DecisionBoardCardDto[]` | Ranked cards in this section. |

## Card model

The card model should keep business semantics on the wire and avoid hard-coding presentation labels that the frontend can derive.

| Field | Type | Purpose |
| --- | --- | --- |
| `id` | `string` | Stable card identifier. |
| `kind` | `string` | Example values: `product`, `inventory`, `supplier`, `blocker`, `action`, `outcome`. |
| `sectionKey` | `string` | Section membership. |
| `sourceModule` | `string` | Which source produced the card. |
| `sourceType` | `string?` | Canonical origin like `product`, `inventory`, `supplier`, `data_quality`. |
| `sourceKey` | `string?` | Stable business key for the originating row or aggregate. |
| `title` | `string` | Business-facing decision label. |
| `summary` | `string?` | Short explanation of the signal. |
| `confidenceLevel` | `string` | `high`, `medium`, `low`, `insufficient_data`. |
| `confidenceScore` | `number?` | Numeric confidence if the backend has one. |
| `reliabilityPct` | `number?` | Optional reliability signal if available from the source. |
| `expectedImpactRsd` | `number?` | Nullable impact. Never fake zero. |
| `measuredImpactRsd` | `number?` | Nullable realized impact for outcome cards. |
| `realizationRatio` | `number?` | Nullable ratio where both values are known. |
| `riskIfIgnored` | `string` | Short downside explanation. |
| `recommendedNextAction` | `string` | Concrete next step. |
| `actionHref` | `string` | Target screen or action surface. |
| `alreadyInAction` | `boolean` | Whether a linked action is already open. |
| `alreadyClosed` | `boolean` | Whether the linked action was already closed. |
| `warningCodes` | `string[]` | Visible caveats and blockers. |
| `dataQualityStatus` | `string` | Trust state from the source. |
| `generatedAtUtc` | `string?` | Item-level freshness anchor. |
| `priorityScore` | `number` | Backend-ranked ordering value. |
| `impactScore` | `number` | Backend-ranked impact component used for ordering. |

### Card semantics

- `confidenceLevel` follows [Decision Confidence Contract](DECISION_CONFIDENCE_CONTRACT.md).
- `insufficient_data` must never be promoted to high confidence.
- missing impact stays nullable, not `0 RSD`.
- `alreadyInAction` and `alreadyClosed` should reflect the actual workflow state, not a frontend guess.
- labels such as `actionCta`, `actionStateLabel`, and similar copy should remain frontend derivations unless the backend has a strong reason to standardize them.

## Duplicate and overlap policy

The aggregate should not silently hide overlaps.

- If the same business item appears in multiple decision lenses, it may be repeated with different `sectionKey` values when the repetition helps the operator understand the decision.
- If the backend deduplicates overlapping cards, it should preserve why the item was hidden or merged.
- A repeated card should still carry enough source context that the operator can trace it back to the originating module.

## Backend responsibilities

- Compose from the same source modules used in Phase 1.
- Preserve nullable confidence and nullable impact.
- Keep stale data and data-quality blockers visible.
- Rank cards with a shared ordering function instead of ad hoc per-section sorting.
- Keep source state and section state honest when a module is partial or unavailable.

## Recommended tests

- aggregate endpoint returns all seven sections
- urgent lane still prioritizes blockers and strong opportunities
- `insufficient_data` and missing impact remain nullable, not zero
- action-linked cards keep `alreadyInAction` / `alreadyClosed` states
- response metadata stays honest when one source is partial or missing
- source link mapping still points back to the originating screen
- duplicate handling preserves section context

## Non-goals

- No implementation in this task.
- No new analytics algorithm.
- No hidden fallback that makes a partial source look clean.
- No destructive demo reset flow.

## Next step

Use this contract to implement the read-only aggregate endpoint and the adapter tests. Do not switch the frontend until the aggregate matches the Phase 1 model.
