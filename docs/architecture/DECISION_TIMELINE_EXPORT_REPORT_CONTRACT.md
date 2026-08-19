# Decision Timeline Export and Retrospective Report Contract

Status: authoritative DT06 docs-only contract
Date: 2026-08-13
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Related event contract: `docs/architecture/DECISION_TIMELINE_CONTRACT.md`
Related rollout plan: `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md` Slice 4
Related live runtime: DT05 Slice-2 filtered timeline plus DT07 export on Product Decision Center; DT08 Slice-5 hardening tests
Related snapshot vocabulary: DEX10 `AnalyticsActionDecisionEvidenceSnapshot` plus ledger creation/resolution snapshots

## Purpose

This contract freezes the meaning of a later Decision Timeline export and retrospective report.

It exists so a future runtime slice can print or download recommendation -> action -> outcome history without inventing events, silently widening the requested period, or presenting missing snapshots as if they were present.

The live runtime baseline remains the DT05 Slice-2 filtered timeline. DT07 added JSON/CSV export over that projection. Print CSS, Excel and PDF remain out of scope.

## Non-goals

- no Excel, PDF, or print-CSS implementation in DT07
- no new event store
- no invented replay history
- no frontend-local reconstruction of missing stages
- no silent period widening to fill an empty report
- no fake zero rates when a denominator is empty

## Live runtime baseline

The current executable timeline is the DT05 filtered projection:

- backend: `AnalyticsActionTimelineFilterProjection` over `AnalyticsActionTimelineProjection`
- DTO: `DecisionTimelineItemDto` / `ProductDecisionTimelineFilterResponseDto`
- UI: Product Decision Center timeline panel

DT07 added a read-only export over that same projection:

- backend: `DecisionTimelineExportProjection` wrapping the Slice-2 filter
- endpoint: `GET /api/analytics/cached/products/decision-center/timeline/export` (`format=json|csv`)
- UI: Product Decision Center `Preuzmi CSV` on the timeline panel

Export/report consumers must reuse that projection's stage names, gap reasons, empty reasons and snapshot objects. They must not build a second history from live product rows, notes text, or current catalog state.

Print CSS, Excel and PDF remain out of scope. Honesty rules in this document stay authoritative.

## Canonical export story

Every exported timeline row is one historical story:

`recommendation issued -> action accepted/rejected/ignored -> action executed -> outcome measured/not measured`

Do not collapse those stages into one generic "closed" or "done" export column.

The retrospective report is a document over the same stories for a declared period. It is not a dashboard screenshot and not a learning/calibration output.

## Honesty header

Every export file and every print/report view must declare the following before any KPI or row:

| Field | Meaning | Honesty rule |
|---|---|---|
| `requestedPeriodFromUtc` / `requestedPeriodToUtc` | Period the caller asked for | Date-only filters use half-open whole-day semantics unless a later runtime prompt says otherwise. |
| `effectivePeriodFromUtc` / `effectivePeriodToUtc` | Period actually used by the projection | Must equal the requested period. Silent widening is forbidden. |
| `periodMode` | How the period was interpreted | Example: issued-at window. Do not switch to resolved-at or measured-at without labeling the change. |
| `generatedAtUtc` | When the export/report was produced | Export time is not a business event time. |
| `freshnessStatus` | Input freshness of the underlying evidence, when known | Missing freshness stays missing. It is not `fresh`. |
| `dataQualityStatus` | Data-quality of the cohort or row | Missing quality stays missing. It is not `ok`. |
| `emptyReason` | Why the cohort has no rows | Required when the successful result is empty. |
| `warningCodes` | Machine-readable caveats | Must survive export failure and empty success. |
| `snapshotCoverage` | Whether creation, resolution and DEX10 evidence snapshots are present, absent, or mixed | Absence is a first-class state. |

If the export cannot populate this header honestly, it must fail visibly. It must not emit a table of zeros.

## Period rules

The Slice-2 filter already uses `issuedAtUtc` against `periodFromUtc`/`periodToUtc`.

Export/report must keep that meaning:

- a row belongs to the period when its issuance/projection timestamp falls in the requested window;
- `updatedAtUtc` is not a period membership date;
- `resolvedAtUtc` is workflow close time, not the export period unless the header explicitly says the report is a resolution-window retrospective;
- `outcomeMeasuredAtUtc` is measurement time, not issuance time.

Forbidden:

- expanding the window because the filtered result was empty;
- substituting a neighboring month, quarter, or "last successful period";
- mixing issued-in-period rows with resolved-in-period rows in one unlabeled cohort;
- labeling returned/visible row count as total matching count without evidence.

Empty-period honesty:

| Case | `success` | `emptyReason` | Report body |
|---|---|---|---|
| No matching entity/family | true | `no_events` | Empty state, no invented rows, no fake rates |
| Matches exist but all outside period | true | `outside_period` | Empty state; keep requested/effective period visible |
| Matches exist in period but none measured | true | `no_measurement` | Show workflow rows; keep outcome rates null / insufficient |
| Projection/export error | false | n/a | Error state; no KPI zeros |

`no_measurement` is not an empty timeline. It is an empty measurement sample inside a non-empty workflow cohort.

## Snapshot presence and absence

Historical explanation prefers snapshots over live lookup. Snapshots are optional in the current repository, so export/report must say when they are missing.

### Snapshot kinds

| Snapshot | Source | Role in export |
|---|---|---|
| Creation snapshot | `AnalyticsActionLedgerSnapshot.CreationSnapshot` | Frozen recommendation at action create time |
| Resolution snapshot | `AnalyticsActionLedgerSnapshot.ResolutionSnapshot` | Frozen outcome/evidence at resolution time |
| DEX10 evidence snapshot | `AnalyticsActionLedgerSnapshot.EvidenceSnapshot` | Immutable Product Decision Center evidence freeze when the recommendation was acted on |

### Presence fields

Every exported timeline and every retrospective row must include explicit flags, not implied presence:

| Field | Meaning |
|---|---|
| `creationSnapshotPresent` | `true` only when a creation snapshot object exists |
| `resolutionSnapshotPresent` | `true` only when a resolution snapshot object exists |
| `evidenceSnapshotPresent` | `true` only when a DEX10 evidence snapshot exists |
| `snapshotAbsenceReason` | Why a needed snapshot is missing, when known |

Recommended absence reasons:

- `creation_snapshot_absent`
- `resolution_snapshot_absent`
- `evidence_snapshot_absent`
- `legacy_partial_history`

Rules:

- absent snapshots stay absent; do not reconstruct them from the current product decision row;
- a present creation snapshot must not be rewritten by later source recalculation in the export;
- a missing DEX10 evidence snapshot does not authorize live evidence-chain lookup as if it were historical truth;
- when a snapshot is absent, the export may still show live workflow fields, but must label them `live_lookup`, not `snapshot`.

## Export fields

### Identity and correlation

These identify the story. They are not authorization tokens.

| Field | Required | Notes |
|---|---|---|
| `timelineId` | yes | Stable export row id from the projection |
| `actionId` | yes | Workflow row identity |
| `sourceRecommendationId` | yes | Recommendation instance identity |
| `correlationId` | when present | Cross-stage grouping; currently derived from `sourceRecommendationId` |
| `sourceType` / `sourceKey` | yes | Business identity, not tenant authority |
| `recommendationType` | when known | From creation snapshot when present |

### Lifecycle timestamps

Export must keep stage timestamps separate from `updatedAtUtc`.

| Field | Meaning | Forbidden substitute |
|---|---|---|
| `issuedAtUtc` | Recommendation became recordable | `updatedAtUtc` |
| `acceptedAtUtc` | Entered workflow, when proven | first note time guessed locally |
| `rejectedAtUtc` | Explicit decline, when proven | missing acceptance |
| `executedAtUtc` | Execution proof, when present | workflow close |
| `resolvedAtUtc` | Workflow closure | measured outcome time |
| `outcomeMeasuredAtUtc` | Business measurement time | `resolvedAtUtc` |
| `updatedAtUtc` | Last mutation time only | any business date |

Missing timestamps remain null and must be accompanied by the matching gap reason from the live projection.

### Events and gaps

Reuse the DT05 event and gap rows:

- `events[].eventType`, `stage`, `occurredAtUtc`, `status`
- `events[].evidenceSource`, `evidenceReference`, `measurementWindowDays`
- `gaps[].stage`, `gapReason`, `message`

Do not add synthetic events to make the funnel look complete. A gap row is the honest export of a missing stage.

Canonical event types remain those in `docs/architecture/DECISION_TIMELINE_CONTRACT.md`. The current Slice-2 projection may not emit every contract event yet; export must not invent the missing ones.

### Workflow and outcome values

| Field | Meaning |
|---|---|
| `currentStatus` | Live or last-known workflow status; label as live lookup unless snapshotted |
| `currentOutcomeStatus` | `pending`, measured result, or `not_measured` |
| `expectedImpactRsd` | Actionable expected impact when present |
| `measuredImpactRsd` | Measured impact when evidence exists |
| `dataQualityStatus` | Row-level quality |
| `projectionState` | Slice-2 projection state |

`rejected` is not `done`. `not_measured` is not failure. A missing measured amount is not `0 RSD`.

### Snapshot payloads

When a snapshot is present, export the snapshot fields themselves, not a rewritten summary:

Creation: `sourceRecommendationId`, `recommendationType`, `recommendedAction`, `decisionReason`, `primaryDrivers`, `warningCodes`, `confidenceLevel`, `inputFreshnessStatus`, `expectedImpactBasis`, `impactWindowDays`, `generatedAtUtc`

Resolution: `outcomeStatus`, `measuredImpactRsd`, `outcomeMeasuredAtUtc`, `measuredWindowDays`, `evidenceSource`, `evidenceReference`, `resolutionNote`

DEX10 evidence: `capturedAtUtc`, `periodFromUtc` / `periodToUtc`, `dataQualityStatus`, `confidenceLevel`, `confidencePct`, `reliabilityPct`, `reasonCodes`, `warningCodes`, `explainabilityText`, evidence-chain and confidence-breakdown nodes with `isMissing` preserved

When a snapshot is absent, export the presence flag and absence reason only. Do not fill those columns from live lookup without the `live_lookup` label.

## Retrospective report fields

A print/report document over the same cohort must look like a document, not a raw table dump.

Required document sections:

1. Honesty header (period, freshness, data quality, snapshot coverage, generated time).
2. Scope explanation from Slice-2 (`scopeExplanation`).
3. Funnel counts with named denominators from `docs/architecture/DECISION_TIMELINE_CONTRACT.md`.
4. Outcome rates only over `measuredCount`; never over `issuedCount` unless the metric is explicitly a funnel metric.
5. Timeline list or appendix with events and explicit gaps.
6. Methodology note: projection-first, no replay store, snapshots optional but explicit.
7. Warnings and empty reasons.

Funnel counts in the report:

| Count | Denominator rule |
|---|---|
| Issued | n/a |
| Accepted / rejected / ignored | issued |
| Executed | accepted |
| Measured / not measured | executed |
| Success / neutral / negative | measured |

Zero denominators stay `null`, `insufficient_evidence`, or warning-coded. They must not render as `0%`.

Matched counts:

- `matchedActionCount` is the number of timeline stories in the effective period;
- `matchedEventCount` is the number of projected events, not a substitute for issued count;
- visible export rows are not automatically total matching rows.

## Print and export failure

Export and print are allowed to fail. Failure must be graceful and visible.

| Failure | Required behavior | Forbidden behavior |
|---|---|---|
| Projection error | Error document or error payload with correlation id | KPI table of zeros |
| Empty successful cohort | Empty document with `emptyReason` | Invented sample rows |
| Snapshot missing | Presence=false and absence reason | Live lookup presented as historical freeze |
| File/print renderer failure | Keep on-screen timeline; show export/print error | Fabricate a download that looks complete |
| Partial renderer success | Mark the artifact `partial` / warning | Drop gap rows to make the PDF shorter |
| Authorization failure | Deny the export | Use `actionId` / `sourceKey` as access rights |

The on-screen DT05 timeline remains the fallback surface. A failed export must not replace that surface with fake zeros.

Print/report views must keep the same stage names as the UI. If a print stylesheet hides gap rows, the report is non-conformant.

## Surface parity

API, Product Decision Center timeline, export file and print/report must agree or explain the difference.

| Surface | Source of truth |
|---|---|
| Filtered timeline API | DT05 projection |
| Timeline UI | Same projection |
| Export file | Same projection plus honesty header |
| Retrospective print/report | Same projection plus document sections above |

If an export column cannot be populated from the projection, omit it or mark it unknown. Do not compute a substitute in the client.

## Mapping to current repository primitives

No new storage is required for this contract.

Already available:

- `DecisionTimelineItemDto` including `LedgerSnapshot`
- `AnalyticsActionCreationSnapshot`
- `AnalyticsActionResolutionSnapshot`
- `AnalyticsActionDecisionEvidenceSnapshot`
- Slice-2 empty reasons `no_events`, `outside_period`, `no_measurement`
- gap reasons `no_acceptance_record`, `no_execution_proof`, `no_measurement_evidence`, `legacy_partial_history`

Still later, and not authorized here:

- a dedicated export endpoint
- first-class accepted/executed timestamps on the action row
- an append-only event store
- automatic backfill of missing snapshots

## No-fake rules for export/report

1. Do not invent timeline events to fill gaps.
2. Do not silently widen the requested period.
3. Do not treat an empty period as an error, or an error as an empty period.
4. Do not render missing rates as `0%` or missing impact as `0 RSD`.
5. Do not present live lookup as a snapshot.
6. Do not rewrite creation or DEX10 evidence snapshots after the fact.
7. Do not count `not_measured` as success or failure.
8. Do not count `rejected` as `done`.
9. Do not use identity fields as authorization.
10. Do not implement Excel, PDF, or print CSS under this contract; JSON/CSV runtime is DT07.

## Acceptance rules

- a timeline export/retrospective contract exists;
- filtered Slice-2 remains the live timeline baseline;
- snapshot presence/absence is explicit;
- empty-period and export-failure honesty are explicit;
- JSON/CSV runtime is DT07; Excel/PDF/print CSS remain out of scope.
