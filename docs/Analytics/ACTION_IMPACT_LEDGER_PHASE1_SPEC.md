# Action Impact Ledger Phase 1 Spec

Updated: 2026-06-21

## Purpose

This document turns the existing ledger plan and gap review into an implementation-ready Phase 1 spec.

Phase 1 is intentionally small:

- preserve the current Analytics Action workflow
- keep backend as the source of truth
- keep nullable impact fields nullable
- use the existing action row plus notes model
- formalize a canonical metadata contract before any broader schema redesign

## Inputs

- [Action Impact Ledger Plan](ACTION_IMPACT_LEDGER_PLAN.md)
- [Action Impact Ledger Gap Review](../qa/ACTION_IMPACT_LEDGER_GAP_REVIEW.md)
- [Analytics Decision OS Roadmap](ANALYTICS_DECISION_OS_ROADMAP.md)
- [Analytics Actions Endpoints](../../Api/Endpoints/AnalyticsActionsEndpoints.cs)

## Phase 1 Scope

Phase 1 covers:

- a structured creation snapshot stored with each action
- a structured outcome/evidence payload stored with each action
- API DTO additions needed to write and read those fields safely
- a read-only ledger projection for action detail and outcome summary follow-up work

Phase 1 does not cover:

- a new recommendation algorithm
- event sourcing
- a mandatory append-only ledger table
- workflow redesign
- frontend confidence or impact computation

## Current baseline

The repo already stores these action-level fields:

- source identity: `SourceType`, `SourceKey`, `SourceId`
- recommendation context: `Title`, `Description`, `RecommendationStatus`, `Priority`
- impact fields: `ImpactEstimateRsd`, `ExpectedImpactRsd`, `MeasuredImpactRsd`
- trust fields: `ConfidencePct`, `ReliabilityPct`, `DataQualityStatus`
- workflow/outcome fields: `Status`, `OutcomeStatus`, `OutcomeMeasuredAtUtc`, `OutcomeNotes`, `ResolvedAtUtc`
- audit fields: `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`, `UpdatedByUserName`
- metadata container: `MetadataJson`
- audit trail: `AnalyticsActionNote`

That baseline is good enough for a Phase 1 ledger if the metadata contract becomes explicit and stable.

## Phase 1 canonical model

Phase 1 uses one logical ledger with three layers:

| Layer | Storage | Phase 1 rule |
| --- | --- | --- |
| Creation snapshot | `AnalyticsActionItem` fields plus structured metadata | Immutable after create |
| Resolution snapshot | `AnalyticsActionItem` outcome fields plus structured metadata | Current latest resolution state |
| Audit trail | `AnalyticsActionNote` | Must preserve workflow and outcome note history |

## Action creation fields

The backend should capture the following at action creation time.

| Field | Type | Required | Storage target | Notes |
| --- | --- | ---: | --- | --- |
| `sourceRecommendationId` | `string` | yes | metadata | Deterministic recommendation identifier; not shown as UI label |
| `recommendationType` | `string` | yes | metadata | Canonical family such as `REPLENISH`, `MARKDOWN`, `NEGOTIATE`, `SIGNAL_REVIEW` |
| `expectedImpactBasis` | `string` | yes | metadata | Short structured basis, not a long narrative |
| `impactWindowDays` | `int?` | no | metadata | Nullable when the module has no reliable modeling window |
| `confidenceLevel` | `string` | yes | metadata | `high`, `medium`, `low`, `insufficient_data` |
| `warningCodes` | `string[]` | yes | metadata | Source warnings and caveats as returned by backend |
| `primaryDrivers` | `string[]` | yes | metadata | Canonical machine-readable drivers |
| `decisionReason` | `string` | yes | metadata | Operator-readable reason in business language |
| `recommendedAction` | `string` | yes | metadata | Operator-facing action label |
| `generatedAtUtc` | `DateTime` | yes | metadata | Recommendation generation timestamp |
| `inputFreshnessStatus` | `string` | yes | metadata | `fresh`, `stale`, `critical`, `unknown` |
| `createdByUserName` | existing | no | action row | Keep using existing actor fields where available |

### Creation field mapping rules

- Keep `ExpectedImpactRsd`, `ConfidencePct`, `ReliabilityPct`, and `DataQualityStatus` as top-level action row fields because they are already first-class and queryable.
- Keep `Title`, `Description`, `RecommendationStatus`, `Priority`, `ActionUrl`, and `Source*` on the action row.
- Store the additional structured snapshot fields inside `MetadataJson`.
- Once created, the creation snapshot must not be silently rewritten by later source recalculation.

## Outcome resolution fields

The backend should capture the following when the action outcome is resolved or measured.

| Field | Type | Required | Storage target | Notes |
| --- | --- | ---: | --- | --- |
| `outcomeStatus` | existing `string` | yes | action row | Keep current canonical values |
| `measuredImpactRsd` | existing `decimal?` | no | action row | Nullable means unknown, not zero |
| `outcomeMeasuredAtUtc` | existing `DateTime?` | no | action row | Measurement timestamp, not workflow resolution timestamp |
| `measuredWindowDays` | `int?` | no | metadata | Nullable when the measurement window is unknown |
| `evidenceSource` | `string?` | no | metadata | Example: report route, import batch, summary run |
| `evidenceReference` | `string?` | no | metadata | Stable pointer or business identifier, not a secret |
| `resolutionNote` | `string?` | no | metadata | Structured copy of the latest measurement note; can coexist with `OutcomeNotes` |
| `resolvedByUserId` | derived from existing update actor | no | action row + notes | No new dedicated column in Phase 1 |
| `resolvedByUserName` | derived from existing update actor | no | notes / existing row actor fields | No new dedicated column in Phase 1 |

### Outcome field mapping rules

- Keep `OutcomeStatus`, `MeasuredImpactRsd`, `OutcomeMeasuredAtUtc`, and `OutcomeNotes` on the action row.
- Store `measuredWindowDays`, `evidenceSource`, `evidenceReference`, and `resolutionNote` in `MetadataJson`.
- Continue writing `AnalyticsActionNote` entries for status/outcome changes so audit visibility is preserved.
- `ResolvedAtUtc` remains the workflow resolution timestamp and must stay separate from `OutcomeMeasuredAtUtc`.

## Metadata JSON contract

Phase 1 should formalize `MetadataJson` as a small canonical envelope, not a free-for-all payload.

### Proposed JSON shape

```json
{
  "schemaVersion": 1,
  "ledger": {
    "creationSnapshot": {
      "sourceRecommendationId": "product:123:replenish:2026-06-01:2026-06-30",
      "recommendationType": "REPLENISH",
      "expectedImpactBasis": "sales_velocity + stock_risk",
      "impactWindowDays": 14,
      "confidenceLevel": "medium",
      "warningCodes": ["STALE_REFRESH"],
      "primaryDrivers": ["sales_velocity", "stock_risk"],
      "decisionReason": "Artikal ima ubrzanu prodaju i rizik od nestanka zalihe.",
      "recommendedAction": "Dopuni",
      "generatedAtUtc": "2026-06-21T09:00:00Z",
      "inputFreshnessStatus": "stale"
    },
    "resolutionSnapshot": {
      "measuredWindowDays": 14,
      "evidenceSource": "action_outcome_summary",
      "evidenceReference": "summary:2026-07-05:product:123",
      "resolutionNote": "Prodaja je porasla nakon dopune, ali je import kasnio dva dana."
    }
  }
}
```

### Metadata rules

- `schemaVersion` is required.
- `ledger.creationSnapshot` is required after Phase 1 create support lands.
- `ledger.resolutionSnapshot` is optional until an outcome update actually adds evidence fields.
- Unknown fields may exist for source-specific metadata, but canonical Phase 1 keys must live under `ledger`.
- Existing non-ledger metadata may remain temporarily outside `ledger`, but new Phase 1 fields should not be scattered.

## API DTO changes

Phase 1 should add explicit DTO fields rather than requiring callers to hand-author ledger JSON.

### Create/upsert request additions

Add these optional/required fields to `AnalyticsActionUpsertBody` and the corresponding service request:

| Field | Type | Required | Rule |
| --- | --- | ---: | --- |
| `SourceRecommendationId` | `string` | yes for ledger-enabled sources | Required when the source can create actions from recommendations |
| `RecommendationType` | `string` | yes | Canonical recommendation family |
| `ExpectedImpactBasis` | `string` | yes when `ExpectedImpactRsd` is present | Keeps impact explainable |
| `ImpactWindowDays` | `int?` | no | Nullable |
| `ConfidenceLevel` | `string` | yes | Backend derived only |
| `WarningCodes` | `string[]?` | no | Defaults to empty array |
| `PrimaryDrivers` | `string[]?` | no | Defaults to empty array |
| `DecisionReason` | `string` | yes | Human explanation |
| `RecommendedAction` | `string` | yes | Operator action label |
| `GeneratedAtUtc` | `DateTime?` | yes | If omitted by caller, backend may set when the recommendation payload is generated server-side |
| `InputFreshnessStatus` | `string` | yes | Canonical freshness value |

### Outcome update request additions

Add these optional fields to `AnalyticsActionOutcomeUpdateBody` and the corresponding service request:

| Field | Type | Required | Rule |
| --- | --- | ---: | --- |
| `MeasuredWindowDays` | `int?` | no | Nullable |
| `EvidenceSource` | `string?` | no | Required only when a measured outcome is being asserted with supporting evidence |
| `EvidenceReference` | `string?` | no | Optional stable pointer |
| `ResolutionNote` | `string?` | no | Structured latest measurement note |

### Read/detail response additions

The action detail response returned by `GET /api/analytics/actions/{id}` should expose a parsed read model, not only raw `MetadataJson`.

Recommended additions:

- `ledgerSnapshot`
  - `creationSnapshot`
  - `resolutionSnapshot`
- `metadataJson` may remain for backward compatibility, but the UI should not be forced to parse canonical ledger fields itself

Phase 1 read DTO shape:

```text
AnalyticsActionLedgerSnapshotDto
- CreationSnapshot?: AnalyticsActionCreationSnapshotDto
- ResolutionSnapshot?: AnalyticsActionResolutionSnapshotDto
```

## Migration options

Phase 1 offers three possible persistence approaches.

| Option | Recommendation | Why |
| --- | --- | --- |
| Reuse existing `MetadataJson` only | Recommended | Smallest safe step, no new table required, aligns with gap review |
| Add dedicated nullable columns now | Not recommended for Phase 1 | Expands schema before the contract proves stable |
| Add append-only ledger table now | Do not do in Phase 1 | Too broad; belongs to a later phase only if evidence demands it |

### Phase 1 migration decision

No DB migration is required for Q58 if the implementation uses the canonical `MetadataJson` envelope.

If the implementation team later finds a hard queryability blocker, that should become a follow-up design task, not a silent Phase 1 expansion.

## No-fake rules

1. `ExpectedImpactRsd = null` means unknown, not `0`.
2. `MeasuredImpactRsd = null` means unknown, not `0`.
3. `impactWindowDays = null` means unknown measurement horizon, not a default window.
4. `measuredWindowDays = null` means unknown measurement coverage, not zero days.
5. `OutcomeStatus = pending` means still waiting for measurement, not failure.
6. `ResolutionNote` or `OutcomeNotes` missing means no note captured yet, not positive evidence.
7. `evidenceSource` missing must not allow the UI to imply measured proof exists.
8. Frontend must not derive `confidenceLevel`, `realizationRatio`, or calibration output from incomplete data unless the backend explicitly returns them.
9. Later source recalculation must not mutate the stored creation snapshot for an already created action.
10. Ledger metadata must not be hidden by stuffing audit history into `Description`.

## Validation rules

### Create/upsert validation

- Reject create requests missing `SourceRecommendationId`, `RecommendationType`, `ConfidenceLevel`, `DecisionReason`, `RecommendedAction`, or `InputFreshnessStatus` once the source is ledger-enabled.
- If `ExpectedImpactRsd` is present, `ExpectedImpactBasis` must also be present.
- `GeneratedAtUtc` must be a valid UTC timestamp.
- `WarningCodes` and `PrimaryDrivers` should be deduplicated and trimmed by backend normalization.

### Outcome validation

- If `MeasuredImpactRsd` is present, `OutcomeStatus` must not be `pending`.
- `OutcomeMeasuredAtUtc` may be null only when the outcome has not been measured.
- `EvidenceReference` must not store secrets, tokens, or raw connection details.
- `ResolutionNote` and `OutcomeNotes` remain optional and must stay length-limited consistently with the current endpoint contract.

## Implementation notes for Q58

- Preserve existing idempotent open-action behavior based on `SourceType + SourceKey`.
- Generate and persist the canonical `ledger` metadata envelope inside the service layer, not in the frontend.
- Keep `MetadataJson` readable even when historical rows do not yet contain `schemaVersion` or `ledger`.
- Treat legacy rows without a ledger envelope as `ledgerSnapshot = null`, not as invalid failures.
- Do not break the existing outcome summary endpoint while adding snapshot/evidence support.

## Required tests before implementation is complete

### Backend unit tests

- upsert persists creation snapshot metadata fields
- existing open-action idempotency still works with ledger metadata present
- outcome update persists resolution snapshot metadata fields
- `pending` outcome remains non-failure
- nullable impact and nullable windows remain null, not zero
- detail projection reads ledger snapshot safely from `MetadataJson`
- legacy rows without a `ledger` envelope still deserialize safely

### Backend integration tests

- create action with full snapshot -> fetch detail returns parsed ledger snapshot
- update outcome with evidence -> fetch detail returns both creation and resolution snapshot
- create action -> outcome summary still treats missing measured impact as unknown
- repeated outcome/status updates preserve note history

### Frontend tests needed for later tasks

- action detail panel shows expected vs measured impact from backend projection
- missing measured impact renders as unknown/unavailable, never `0 RSD`
- missing ledger snapshot on historical rows does not crash the detail UI
- confidence/calibration UI only renders when backend provides explicit fields

## Acceptance for Q57

Q57 is complete when:

- the creation snapshot fields are explicit
- the outcome/evidence fields are explicit
- the metadata JSON envelope is canonical
- migration options are constrained
- DTO changes are concrete enough for Q58
- no-fake rules are locked for expected vs measured outcome handling
