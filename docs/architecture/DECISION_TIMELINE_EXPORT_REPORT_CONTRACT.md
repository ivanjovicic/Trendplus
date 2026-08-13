# Decision Timeline Export and Retrospective Contract

Status: planning contract for DT06
Date: 2026-08-12
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Source contract: `docs/architecture/DECISION_TIMELINE_CONTRACT.md`
Rollout plan: `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md`

## Purpose

This contract defines the read-only export and retrospective report surface for decision timelines.

The report must show what happened, when it happened, what evidence supports it and how fresh or trustworthy the data was at the time of generation.

## Non-goals

- no runtime timeline implementation
- no new event store
- no schema migration
- no automatic learning or calibration
- no frontend-local reconstruction of history
- no invented replay history

## Canonical report identity

Every export or retrospective report should be identifiable and reproducible with a stable report identity.

### Required identity fields

| Field | Meaning |
| --- | --- |
| `reportId` | Stable identifier for the report or snapshot instance. |
| `reportType` | Canonical report family, e.g. `decision_timeline_retrospective`. |
| `generatedAtUtc` | When the report was produced. |
| `periodFromUtc` / `periodToUtc` | Effective report window. |
| `sourceType` | Optional origin family filter. |
| `sourceKey` | Optional business key filter. |
| `recommendationType` | Optional recommendation family filter. |

## Canonical report shape

The smallest useful timeline retrospective should expose:

```json
{
  "meta": {
    "success": true,
    "reportId": "dt-report-2026-08-12-001",
    "reportType": "decision_timeline_retrospective",
    "generatedAtUtc": "2026-08-12T12:00:00Z",
    "periodFromUtc": "2026-07-01T00:00:00Z",
    "periodToUtc": "2026-07-31T23:59:59Z",
    "lastRefreshAtUtc": "2026-08-12T11:50:00Z",
    "dataQualityStatus": "warning",
    "warnings": ["snapshot_partial", "small_measured_sample"],
    "emptyReason": null,
    "methodology": "Deterministic decision timeline retrospective based on backend snapshot evidence."
  },
  "summary": {
    "issuedCount": 148,
    "acceptedCount": 92,
    "rejectedCount": 18,
    "ignoredCount": 38,
    "executedCount": 64,
    "measuredCount": 61,
    "notMeasuredCount": 3,
    "successCount": 34,
    "neutralCount": 11,
    "negativeCount": 16,
    "measuredImpactSampleCount": 43
  },
  "timeline": [],
  "evidence": [],
  "gaps": [],
  "sections": []
}
```

The shape may grow later, but the report must keep the core identity, period, freshness, data quality and evidence links explicit.

## Meta contract

| Field | Meaning |
| --- | --- |
| `success` | Standard analytics success signal. |
| `reportId` | Stable report or snapshot key. |
| `reportType` | Report family and contract version anchor. |
| `generatedAtUtc` | Generation timestamp. |
| `periodFromUtc` / `periodToUtc` | Report window. |
| `lastRefreshAtUtc` | Freshness timestamp for the source data. |
| `dataQualityStatus` | Trust state for the report. |
| `warnings` | Warning and caveat codes. |
| `emptyReason` | Present only when the report is truly empty but successful. |
| `methodology` | Short human-readable explanation of what the report includes and excludes. |

## Timeline section contract

The timeline section must preserve the canonical event order from `DECISION_TIMELINE_CONTRACT.md`.

### Required fields per timeline row

| Field | Meaning |
| --- | --- |
| `sourceRecommendationId` | Stable recommendation identity. |
| `actionId` | Workflow row identity when present. |
| `correlationId` | Cross-event trace identifier when present. |
| `eventType` | Canonical event name. |
| `occurredAtUtc` | When the event happened. |
| `status` | Current stage or terminal state. |
| `gapReason` | Explicit reason when the event is missing or incomplete. |
| `isGap` | Marks explicit absence instead of inferred history. |

### Export and report rules

- timeline rows must never infer a missing stage from notes alone;
- missing stages must remain explicit gaps;
- `resolvedAtUtc` and `outcomeMeasuredAtUtc` must stay separate when both exist;
- `not_measured` must remain visible as an evidence gap, not a success row;
- export and on-screen report must use the same stage meaning;
- silent period widening is not allowed.

## Evidence section contract

Reports must link back to the evidence that supports the historical story.

### Required evidence fields

| Field | Meaning |
| --- | --- |
| `snapshotId` | Stable reference to the historical snapshot when present. |
| `snapshotCapturedAtUtc` | When the snapshot was frozen, if applicable. |
| `evidenceSource` | Where the measured result came from. |
| `evidenceReference` | Stable pointer to the supporting evidence. |
| `measurementWindowDays` | Measurement horizon for the evidence. |
| `decisionReason` | Canonical reason text from the snapshot. |
| `primaryDrivers` | Backend-led explanation drivers. |
| `warningCodes` | Evidence caveats that must stay visible. |

### Evidence rules

- evidence links are optional only when the source row genuinely has no captured evidence;
- if a snapshot exists, the report should prefer the snapshot over live mutable state;
- snapshot absence must stay explicit;
- evidence should not be reconstructed from current state if the snapshot can be read directly;
- a report must not claim historical certainty that the backend cannot prove.

## Summary rules

The summary section should retain the same denominator meaning as the canonical timeline contract.

### Required summary semantics

- issued, accepted, rejected, ignored, executed, measured and not-measured counts must remain distinct;
- success, neutral and negative counts must use the measured denominator;
- measured impact sample count must remain explicit;
- zero denominators must stay nullable or warning-coded;
- small samples must emit warning codes rather than confident claims;
- historical report totals must not be relabeled as current live truth.

## Empty and failure behavior

### Empty report

An empty but valid report must:

- set `meta.success = true`;
- include an `emptyReason`;
- keep `dataQualityStatus` explicit;
- avoid fake zero counts that look like real history.

### Failed report

If the report cannot be generated:

- use the backend error pattern already used by analytics contracts;
- do not fabricate an empty report payload;
- do not hide the failure behind placeholder totals;
- do not convert a retrieval failure into a false-ready report.

## Compatibility notes

- This document does not change runtime behavior.
- This document does not replace the canonical timeline contract.
- This document is the export/report companion to `docs/architecture/DECISION_TIMELINE_CONTRACT.md`.
- Future implementation may map this contract to either a GET report endpoint or a durable snapshot endpoint, but the report semantics must remain the same.

## References

- `docs/architecture/DECISION_TIMELINE_CONTRACT.md`
- `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md`
- `docs/Analytics/REPORT_SNAPSHOT_PLAN.md`
- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`

## Completion note

- Date: 2026-08-13
- Status: DONE
- Changed files:
  - `docs/architecture/DECISION_TIMELINE_EXPORT_REPORT_CONTRACT.md`
  - `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`
  - `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `node scripts/check-prompt-queues.mjs` - pass
  - `node scripts/check-planning-architecture.mjs` - fail (DEX, RL and DT now have 0 READY prompts after closing DT06)
  - `git diff --check` - pass
- Remaining risk:
  - This is a docs-only timeline export contract; runtime export or report implementation still needs a later prompt.
- Next:
  - none
