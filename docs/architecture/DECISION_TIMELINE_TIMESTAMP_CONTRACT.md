# Decision Timeline First-Class Timestamp Contract

Status: authoritative DT09 docs-only contract  
Date: 2026-08-20  
Related:

- `docs/architecture/DECISION_TIMELINE_CONTRACT.md`
- `docs/architecture/DECISION_TIMELINE_ROLLOUT_PLAN.md`
- `docs/architecture/DECISION_TIMELINE_EXPORT_REPORT_CONTRACT.md`
- `docs/qa/DECISION_TIMELINE_SLICE5_HARDENING_2026-08-17.md`

## Purpose

Freeze which timeline times are **first-class**, which are **derived**, and which must stay **absent**.

DT01 already names stage times and forbids using `updatedAtUtc` as a business date. The live Slice-2 projection still coalesces and reconstructs several of those times. A later runtime slice must not invent missing stages, collapse `rejected` into `done`, or treat `not_measured` as a completed measured timestamp.

This contract is safe to ignore. Product behavior stays unchanged until a later prompt authorizes persistence or projection changes.

## Non-goals

- no new event store
- no schema migration
- no automatic backfill of missing accepted/executed/measured times
- no frontend-local reconstruction from notes text
- no inventory-forecast runtime or materializer work

## Classification

| Class | Meaning | API/export rule |
|---|---|---|
| First-class | Stored or snapshotted with an explicit stage meaning | May be named as that stage time |
| Derived | Reconstructed from notes, coalesced fields, or live lookup | May appear only as `events[].occurredAtUtc` with derivation, never as a substitute first-class column |
| Absent | Stage not proven | Stay `null` plus a gap reason. Do not emit `0` duration or a synthetic clock |

## First-class timestamps

These names match the DT06 export columns and DT01 time metrics. They become first-class **only** when the named source exists. Coalescing two sources into one column is derived, not first-class.

| Field | Stage meaning | Authoritative source when present | Must not be |
|---|---|---|---|
| `recommendationGeneratedAtUtc` | Recommendation became recordable on the originating surface | Creation snapshot `generatedAtUtc` | `updatedAtUtc`, export `generatedAtUtc` |
| `issuedAtUtc` | Action row became the issuance anchor | `AnalyticsActionItem.CreatedAtUtc` | Last-note time, `resolvedAtUtc` |
| `acceptedAtUtc` | Entered workflow | Dedicated persisted acceptance time, when a later prompt adds it | First note guessed as “probably accept” without `StatusTo=accepted` |
| `rejectedAtUtc` | Explicit decline | Dedicated persisted rejection time, when a later prompt adds it | Missing acceptance, or `resolvedAtUtc` |
| `ignoredAtUtc` | Expired/never accepted before cutoff | Dedicated persisted ignore/cutoff time, when captured | Period end of a report |
| `executedAtUtc` | Execution proven | Dedicated persisted execution time, when a later prompt adds it | `resolvedAtUtc`, `Status=done` without proof |
| `resolvedAtUtc` | Workflow closure | `AnalyticsActionItem.ResolvedAtUtc` | Measured outcome time |
| `outcomeMeasuredAtUtc` | Business measurement time | `AnalyticsActionItem.OutcomeMeasuredAtUtc` | `resolvedAtUtc`, `updatedAtUtc` |
| `updatedAtUtc` | Last mutation clock | `AnalyticsActionItem.UpdatedAtUtc` | Any stage time or period membership date |

`generatedAtUtc` on an **export honesty header** remains export-production time (DT06). It is not a timeline stage.

Until dedicated acceptance/rejection/execution columns exist, those three fields stay **derived or absent**. They must not be written back onto the action row from notes in this prompt.

## Derived timestamps (current Slice-2 / DT07)

The live projection may keep these reconstructions. Consumers must treat them as derived.

| Exposed today | How it is produced | Honesty |
|---|---|---|
| Projection `IssuedAtUtc` | `creationSnapshot.generatedAtUtc` else partial metadata else `CreatedAtUtc` | Derived coalesced issuance. Do not claim separate generated vs action-created times from this one field. |
| Export `AcceptedAtUtc` | First `action_accepted` event = matching note `CreatedAtUtc` | Derived from audit notes. Missing notes → absent + `no_acceptance_record`. |
| Export `RejectedAtUtc` | First `action_rejected` event = matching note `CreatedAtUtc` | Derived. Do not copy onto `acceptedAtUtc`. |
| Export `ExecutedAtUtc` | First `action_executed` event = note whose `StatusTo=done` | Derived. `Status=done` without that note is `legacy_partial_history`, not an invented execute time. |
| `outcome_not_measured` `occurredAtUtc` | `ResolvedAtUtc ?? UpdatedAtUtc` | **Not** `outcomeMeasuredAtUtc`. Must not fill the measured column. Prefer labeling the event time as workflow/mutation-derived. |

`FindEventTime` on export rows is a derived lookup over events, not a first-class store.

## Absent timestamps

Stay null. Emit the matching gap. Do not backfill a complete funnel.

| Missing field | Gap / empty meaning |
|---|---|
| no proven acceptance | `no_acceptance_record` |
| accepted, no execution proof | `no_execution_proof` |
| done without execution note | `legacy_partial_history` |
| no usable measurement evidence | `no_measurement_evidence` |
| measured-looking status without `outcomeMeasuredAtUtc` | `no_measurement_evidence`; do not invent the clock |
| no ignore cutoff captured | no `action_ignored` time; do not use report `periodToUtc` |
| no `outcome_measurement_started` store | delayed/pending stays without a started event (DT08) |

`not_measured` is an outcome branch, not a measured timestamp. `rejected` is not `done` and does not receive `executedAtUtc`.

## Time metrics

DT01 durations remain valid only when **both** endpoints are first-class or both are explicitly labeled derived from the same projection.

| Metric | Requires | If either end is absent |
|---|---|---|
| Time to accept | issued + accepted | `null` / insufficient; never `0` |
| Time to execute | accepted + executed | `null`; never close-time minus issued |
| Time to measure | `outcomeMeasuredAtUtc` minus the declared basis (`resolvedAtUtc` or issued) | `null`; never `updatedAtUtc` |
| Time to close | `resolvedAtUtc` minus issued | `null` if `resolvedAtUtc` missing |

Zero elapsed time is allowed only when both clocks exist and are equal. Missing is not zero.

## Export and report parity

Reuse DT06 lifecycle columns and DT08 Slice-5 cases:

- missing timestamps stay absent, not a synthetic completion time;
- `rejected` ≠ `done`;
- `not_measured` ≠ success/failure and ≠ `outcomeMeasuredAtUtc`;
- empty/error CSV still omit fake `0%` / `0` KPI rates;
- UI labels stay mapped from the same event/gap codes.

Do not add a second history from live product rows.

## Current repository mapping (no schema in DT09)

Already first-class on `AnalyticsActionItem`:

- `CreatedAtUtc`, `UpdatedAtUtc`, `ResolvedAtUtc`, `OutcomeMeasuredAtUtc`

Still not first-class on the action row (DT06 already listed this as later work):

- dedicated `acceptedAtUtc` / `rejectedAtUtc` / `executedAtUtc` / `ignoredAtUtc`
- separate stored `recommendationGeneratedAtUtc` vs action `CreatedAtUtc`

A later persistence prompt may add those columns. This contract does not authorize that migration. Automatic backfill from notes remains forbidden.

## No-fake rules

1. Do not invent a stage time to complete the funnel.
2. Do not use `updatedAtUtc` as issued, accepted, executed, resolved, or measured time.
3. Do not use `resolvedAtUtc` as `outcomeMeasuredAtUtc`.
4. Do not stamp `outcomeMeasuredAtUtc` on `not_measured` rows.
5. Do not treat note-derived execute time as proof when `Status=done` has no execution note.
6. Do not present derived coalesced `IssuedAtUtc` as two independent first-class clocks.
7. Do not report duration `0` for a missing endpoint.
8. Frontend must not parse note bodies into timestamps.

## Acceptance

- first-class vs derived vs absent is citeable;
- Slice-5 honesty remains binding;
- no runtime timestamp persistence or schema change in DT09.
