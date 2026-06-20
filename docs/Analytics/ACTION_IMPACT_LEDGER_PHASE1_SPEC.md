# Action Impact Ledger Phase 1 Spec

Updated: 2026-06-20

## Purpose

This document locks the Phase 1 contract for the Action Impact Ledger.

Phase 1 is a read-friendly, implementation-ready projection over the existing action row plus notes model. It must preserve what the operator saw at action creation, preserve what was later measured, and avoid inventing certainty when evidence is missing.

Primary surfaces in scope:

- `GET /api/analytics/actions/{id}`
- `GET /api/analytics/actions/outcomes/summary`
- the existing action queue detail experience that reads the action item payload

Phase 1 does not add a new workflow. It does not require a new ledger table. It only defines the canonical data contract that future backend implementation must follow.

## Repo baseline

The repo already has:

- `AnalyticsActionItem` as the persisted action row
- `AnalyticsActionNote` as the audit trail for status and outcome changes
- `AnalyticsActionItemService.UpsertAsync` for creation
- `AnalyticsActionItemService.UpdateOutcomeAsync` for measured outcome updates
- `GET /api/analytics/actions/outcomes/summary` for aggregate learning signals

That is enough for Phase 1. The gap is not raw storage. The gap is a canonical ledger contract that says which fields are creation snapshot fields, which fields are resolution fields, and which values must remain nullable.

## Phase 1 position

Recommended Phase 1 shape:

1. Keep `AnalyticsActionItem` as the source of truth for the action row.
2. Keep `AnalyticsActionNote` as the audit trail.
3. Reserve a canonical `impactLedger` namespace inside `MetadataJson`.
4. Expose a read-only ledger projection on action detail responses.
5. Keep summary math read-only and derived.

Phase 1 explicitly avoids:

- a new append-only ledger table
- changing the action workflow state machine
- changing recommendation generation logic
- forcing measured outcome data when no evidence exists
- showing `0 RSD` where the correct value is unknown

## Fields stored at action creation

The creation snapshot must preserve the exact recommendation that was shown to the operator.

### Persisted in existing columns

| Field | Required | Notes |
|---|---:|---|
| `SourceType` | yes | Source domain such as dashboard, product, supplier, inventory, data quality |
| `SourceKey` | yes | Stable idempotency key for the source action |
| `SourceId` | no | Optional numeric source identifier |
| `Title` | yes | Operator-facing title |
| `Description` | no | Human-readable explanation |
| `RecommendationStatus` | no | Existing recommendation label |
| `Priority` | yes | Existing action priority |
| `ImpactEstimateRsd` | no | Optional estimated impact |
| `DueAtUtc` | no | Optional review due date |
| `ExpectedImpactRsd` | no | Nullable if evidence is missing |
| `ConfidencePct` | no | Existing confidence input |
| `ReliabilityPct` | no | Existing reliability input |
| `DataQualityStatus` | yes | Canonical trust status, normalized before save |
| `Status` | yes | Creation state, usually `new` |
| `ActionUrl` | no | Source or action link |
| `CreatedAtUtc` | yes | Creation timestamp |
| `CreatedByUserId` | no | Creator identity if available |
| `MetadataJson` | yes | Reserved for canonical ledger metadata and any source-specific extras |

### Canonical `impactLedger` creation metadata

The following keys must live under `MetadataJson.impactLedger`:

| Key | Required | Notes |
|---|---:|---|
| `version` | yes | Ledger schema version, start at `1` |
| `sourceRecommendationId` | no | Native or derived recommendation identifier |
| `sourceRecommendationIdDerivation` | yes | One of `native`, `deterministic`, `missing` |
| `capturedAtUtc` | yes | When the snapshot was captured |
| `expectedImpactBasis` | yes | Short basis for the expectation, not a long essay |
| `primaryDrivers` | yes | Array of machine-readable or canonical driver labels |
| `decisionReason` | yes | Human explanation shown to the operator |
| `impactWindowDays` | no | Nullable until the source can model it reliably |
| `recommendedAction` | yes | Operator-facing recommended action label |
| `inputFreshnessStatus` | yes | Freshness/trust label for the source inputs |
| `sourceModule` | no | Optional source module or feature name |
| `sourcePeriodStartUtc` | no | Optional source period lower bound |
| `sourcePeriodEndUtc` | no | Optional source period upper bound |

### Creation rules

- The snapshot must not be rewritten when the source recommendation changes later.
- If the source has no native recommendation ID, derive one deterministically and store the derivation rule.
- If no safe deterministic ID exists, store `sourceRecommendationId = null` and mark `sourceRecommendationIdDerivation = missing`.
- `expectedImpactRsd = null` means unknown, not zero.
- `primaryDrivers` must be a small list, not a free-form blob.
- `decisionReason` must be business-readable.

## Fields stored at outcome resolution

Outcome resolution must preserve the workflow result and the measured result separately.

### Persisted in existing columns

| Field | Required | Notes |
|---|---:|---|
| `OutcomeStatus` | yes | `pending`, `success`, `neutral`, `negative`, `not_measured` |
| `MeasuredImpactRsd` | no | Nullable if no measured evidence exists |
| `OutcomeMeasuredAtUtc` | no | Business measurement timestamp |
| `OutcomeNotes` | no | Free-form explanatory note |
| `ResolvedAtUtc` | yes | Workflow resolution timestamp |
| `UpdatedAtUtc` | yes | Row update timestamp |
| `UpdatedByUserId` | no | Last updater identity if available |
| `UpdatedByUserName` | no | Last updater display name if available |

### Canonical `impactLedger` resolution metadata

The following keys must live under `MetadataJson.impactLedger`:

| Key | Required | Notes |
|---|---:|---|
| `evidenceSource` | no | Auditable source such as report, import batch, calculation batch, or manual note |
| `measuredWindowDays` | no | Window covered by the measurement |
| `resolutionNote` | no | Short note explaining the observed result |
| `measurementMethod` | no | Optional canonical label for how the measurement was produced |
| `resolvedByUserId` | no | Duplicate only if the detail projection needs it in JSON |
| `resolvedByUserName` | no | Duplicate only if the detail projection needs it in JSON |

### Resolution rules

- `resolvedAtUtc` and `outcomeMeasuredAtUtc` are different events.
- `resolvedAtUtc` means the workflow was closed.
- `outcomeMeasuredAtUtc` means the business effect was measured.
- `pending` is not failure.
- `not_measured` means the action was closed but the business effect was not measured.
- Missing evidence must stay null and must not be silently converted to zero.

## Canonical metadata JSON shape

`MetadataJson` should remain a JSON object with a reserved `impactLedger` namespace.

Recommended shape:

```json
{
  "impactLedger": {
    "version": 1,
    "sourceRecommendationId": "rec-2026-06-20-001",
    "sourceRecommendationIdDerivation": "deterministic",
    "capturedAtUtc": "2026-06-20T10:00:00Z",
    "expectedImpactBasis": "sales_velocity + stock_risk",
    "primaryDrivers": ["sales_velocity", "stock_risk"],
    "decisionReason": "Stock risk is rising and the item still has demand.",
    "impactWindowDays": 14,
    "recommendedAction": "Dopuni",
    "inputFreshnessStatus": "fresh",
    "sourceModule": "inventory.replenishment",
    "sourcePeriodStartUtc": "2026-06-06T00:00:00Z",
    "sourcePeriodEndUtc": "2026-06-20T00:00:00Z",
    "evidenceSource": "calculation_batch",
    "measuredWindowDays": 14,
    "resolutionNote": "Measured against the follow-up report after two weeks.",
    "measurementMethod": "follow_up_report"
  },
  "sourceMetadata": {
    "anyOtherSourceSpecificFields": true
  }
}
```

### JSON rules

- `impactLedger` is reserved for the canonical ledger contract.
- Source-specific extras should stay under a separate namespace such as `sourceMetadata`.
- Do not put derived summary math in `MetadataJson` if the service can compute it on read.
- Do not overwrite the canonical keys with UI-only strings.

## API DTO changes

Phase 1 should keep the list and summary contracts stable where possible and add only the read-only ledger projection that the detail view needs.

### Required DTO change

Add an optional `impactLedger` object to the action detail response returned by `GET /api/analytics/actions/{id}`.

Suggested DTO shape:

```csharp
public sealed record AnalyticsActionImpactLedgerDto(
    int Version,
    string? SourceRecommendationId,
    string SourceRecommendationIdDerivation,
    DateTime CapturedAtUtc,
    AnalyticsActionImpactLedgerSnapshotDto Snapshot,
    AnalyticsActionImpactLedgerResolutionDto Resolution,
    AnalyticsActionImpactLedgerDerivedDto? Derived
);
```

```csharp
public sealed record AnalyticsActionImpactLedgerSnapshotDto(
    string ExpectedImpactBasis,
    IReadOnlyList<string> PrimaryDrivers,
    string DecisionReason,
    int? ImpactWindowDays,
    string RecommendedAction,
    string InputFreshnessStatus,
    string? SourceModule,
    DateTime? SourcePeriodStartUtc,
    DateTime? SourcePeriodEndUtc
);
```

```csharp
public sealed record AnalyticsActionImpactLedgerResolutionDto(
    string? EvidenceSource,
    int? MeasuredWindowDays,
    string? ResolutionNote,
    string? MeasurementMethod,
    string OutcomeStatus,
    decimal? MeasuredImpactRsd,
    DateTime? OutcomeMeasuredAtUtc,
    DateTime? ResolvedAtUtc
);
```

```csharp
public sealed record AnalyticsActionImpactLedgerDerivedDto(
    decimal? ImpactDeltaRsd,
    decimal? RealizationRatio,
    string CalibrationBucket,
    bool HasEvidence
);
```

### DTO guidance

- `AnalyticsActionItem` list rows do not need to grow in Phase 1 unless a follow-up task proves they must.
- The summary DTO can remain read-only and derived from existing fields.
- The ledger projection should never fabricate values from missing data.

## Migration options

### Option A - Recommended for Phase 1

No schema migration.

- Keep the action row as-is.
- Store canonical ledger keys under `MetadataJson.impactLedger`.
- Parse and project the ledger in the service layer.
- Add tests around serialization, read projection, and null handling.

Why this is best now:

- smallest risk
- no backfill required
- no schema churn before the contract is proven
- keeps the write path stable

### Option B - Later indexing columns

Add nullable scalar columns only if search, filtering, or reporting proves that JSON is not enough.

Possible future columns:

- `SourceRecommendationId`
- `ExpectedImpactBasis`
- `ImpactWindowDays`
- `EvidenceSource`
- `MeasuredWindowDays`

Only use this path if we need database-level filtering or indexing on the ledger keys.

### Option C - Phase 2 append-only table

Add a dedicated ledger table only if we need multiple resolution revisions per action or a full revision history that the row-plus-notes model cannot represent.

This is explicitly not a Phase 1 requirement.

## No-fake rules

1. `expectedImpactRsd = null` is unknown, not zero.
2. `measuredImpactRsd = null` is unknown, not zero.
3. `impactDeltaRsd = null` is unknown, not zero.
4. `realizationRatio = null` when the denominator is missing, zero, or invalid.
5. `pending` must never count as a failure.
6. Missing evidence must produce a warning or a null value, not a fake metric.
7. `calibrationBucket` must be `insufficient_data` when the sample is too small or incomplete.
8. Frontend must not display a green success story when the measurement is missing.
9. Historical ledger entries must not be rewritten to hide later learning.

## Tests required before implementation starts

### Backend tests

- creation stores the snapshot keys under `MetadataJson.impactLedger`
- deterministic `sourceRecommendationId` derivation is stable across retries
- resolution stores measured impact, evidence, and timestamps without converting null to zero
- `pending` outcome does not count as failure in summary math
- `not_measured` stays separate from `negative`
- realization ratio is null when expected or measured impact is missing or invalid
- detail response projects the ledger contract without losing existing fields
- summary response still respects empty, warning, and null semantics

### Frontend tests

- action detail rendering shows snapshot and resolution as separate blocks
- missing evidence renders as unknown or warning, not as zero
- summary/KPI rendering does not invent impact numbers when the backend returns null
- action queue smoke coverage still works with the new detail payload

## Implementation note

Q58 should implement the smallest safe backend slice that matches this spec:

- keep write semantics unchanged
- project the ledger from the existing row and metadata
- preserve nulls end to end
- add tests before adding any larger schema change

