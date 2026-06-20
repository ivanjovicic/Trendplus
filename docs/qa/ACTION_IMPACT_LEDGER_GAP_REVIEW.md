# Action Impact Ledger Gap Review

Date: 2026-06-19 15:42:00 +02:00
Local HEAD: `2f694cdda0e4bd9a3803f1d52e94cf6fd3af3964`

## Scope

- [docs/Analytics/ACTION_IMPACT_LEDGER_PLAN.md](../Analytics/ACTION_IMPACT_LEDGER_PLAN.md)
- [docs/Analytics/ACTION_OUTCOME_DATA_SHAPE_AUDIT.md](../Analytics/ACTION_OUTCOME_DATA_SHAPE_AUDIT.md)
- [docs/Analytics/ACTION_OUTCOME_SUMMARY_API_PLAN.md](../Analytics/ACTION_OUTCOME_SUMMARY_API_PLAN.md)
- [Domain/Model/Analytics/AnalyticsActionItem.cs](../../Domain/Model/Analytics/AnalyticsActionItem.cs)
- [Domain/Model/Analytics/AnalyticsActionNote.cs](../../Domain/Model/Analytics/AnalyticsActionNote.cs)
- [Infrastructure/Services/Analytics/AnalyticsActionItemService.cs](../../Infrastructure/Services/Analytics/AnalyticsActionItemService.cs)
- [Api/Endpoints/AnalyticsActionsEndpoints.cs](../../Api/Endpoints/AnalyticsActionsEndpoints.cs)
- [Klijent/clientapp/src/types/analytics.ts](../../Klijent/clientapp/src/types/analytics.ts)
- [Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx](../../Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx)
- [Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx](../../Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx)

## Current State

### What the repo already has

- `AnalyticsActionItem` already stores the core operational action row.
- `AnalyticsActionNote` already gives us an audit trail for status transitions and outcome notes.
- `AnalyticsActionItemService` already supports:
  - idempotent open-action upsert
  - status changes with note history
  - outcome updates with note history
  - read-only outcome summary aggregation
- `GET /api/analytics/actions/outcomes/summary` already exists and returns:
  - created / closed / measured totals
  - outcome coverage
  - expected vs measured impact
  - source / priority / outcome / trust cohort breakdowns

### What the plan still expects beyond the current row model

The ledger plan wants a stable, durable recommendation snapshot and explicit measurement history. The current repo does not yet have those as a canonical structured contract.

## Gap Analysis

| Ledger element | Plan expectation | Current repo state | Gap severity |
| --- | --- | --- | --- |
| Creation snapshot | Preserve the exact recommendation shown to the operator | Stored partly in `AnalyticsActionItem` fields and partly in free-form `MetadataJson` | Medium |
| Stable recommendation ID | `sourceRecommendationId` derived deterministically if the source has none | No canonical field yet; idempotency is currently anchored on `SourceType + SourceKey` | Medium |
| Structured expectation basis | `expectedImpactBasis`, `primaryDrivers`, `decisionReason`, `impactWindowDays` | Only partially represented through existing fields (`RecommendationStatus`, `Description`, `ExpectedImpactRsd`, `ConfidencePct`, `ReliabilityPct`, `MetadataJson`) | Medium |
| Outcome measurement evidence | `evidenceSource`, `measuredWindowDays`, `resolutionNote` | Only `OutcomeMeasuredAtUtc`, `OutcomeNotes`, and audit notes are stored explicitly | Medium |
| Append-only measurement history | Multiple updates should leave a clear, durable audit trail | Notes capture transitions, but there is no dedicated append-only ledger record per measurement revision | Medium |
| Ledger UI surface | Action Queue detail and history should show original snapshot vs later outcome | Current pages show row detail and summary, but not a dedicated ledger view that guarantees snapshot/outcome side-by-side | Low/Medium |

## Smallest Safe Phase 1 Gap

The smallest safe Phase 1 implementation gap is **not** a new table or workflow redesign.

Instead, Phase 1 should formalize the ledger as a thin structured projection on top of what already exists:

- keep the action row as the source of truth for creation and current outcome
- keep `Notes` as the audit trail for status/outcome transitions
- define a canonical structured metadata contract for the recommendation snapshot and evidence keys inside `MetadataJson`
- expose a read-only ledger projection in the action detail/summary surfaces without changing write semantics

### Why this is the smallest safe step

- It uses existing persisted primitives.
- It avoids a schema migration before the contract is stable.
- It keeps workflow semantics unchanged.
- It lets us prove the ledger shape before committing to an append-only table.

## Recommended Canonical Metadata Keys

If Phase 1 proceeds without schema expansion, the minimum structured keys should be:

- `sourceRecommendationId`
- `expectedImpactBasis`
- `primaryDrivers`
- `decisionReason`
- `impactWindowDays`
- `evidenceSource`
- `measuredWindowDays`
- `resolutionNote`

These keys would let the existing action row behave like a ledger snapshot without inventing frontend values.

## What Is Already Good Enough

- `expectedImpactRsd` and `measuredImpactRsd` already remain nullable.
- `pending` outcomes already do not count as failures in the outcome summary.
- `Action Outcome Summary` already respects `null` as unknown instead of zero.
- The current queue/detail surfaces already have enough data to keep the UI honest while we wait for a canonical ledger contract.

## What Still Needs Follow-Up

- A dedicated backend contract for the structured snapshot/evidence keys.
- A dedicated read-only ledger projection for action detail history.
- Frontend rendering that shows original snapshot, measured outcome, and evidence on one screen without mixing them into the main queue list.
- Backend tests for snapshot preservation and measurement history once the contract is formalized.

## Verification

- `git diff --check` - pass

## Conclusion

Q41 is complete as a design-to-implementation gap review.

The repo does **not** need a new ledger table yet. The right Phase 1 move is to standardize the existing row-plus-notes model with a small structured metadata contract and a read-only ledger projection, then implement only after that contract is locked.

## Next

- Q42 - Product Decision confidence calibration review
