# Recommendation Advisory Calibration Runtime Gate Contract

Status: authoritative RL11 contract
Date: 2026-08-28
Related roadmap: `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md`
Related source contracts:

- `docs/Analytics/RECOMMENDATION_ADVISORY_CALIBRATION_CONTRACT.md`
- `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md`
- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`
- `docs/architecture/RECOMMENDATION_MEASUREMENT_STATISTICS_REVIEW_SURFACE.md`
- `docs/architecture/RECOMMENDATION_LEARNING_STATISTICS_ROLLOUT_PLAN.md`

## Purpose

RL10 froze the advisory calibration payload, but the repo still needed a second contract that says when runtime consumers may read those advisory hints and when they must remain measurement-only.

This gate is intentionally conservative:

- it defines the current default runtime mode as measurement-only;
- it names the only future consumer class that may read advisory hints without mutating live confidence;
- it freezes the write-target boundary for any future advisory snapshot;
- it requires explicit later approval before any score, ranking or recommendation mutation can happen.

## Non-goals

- no runtime implementation
- no calibration job
- no ML model
- no schema migration
- no automatic confidence mutation
- no frontend-local calibration labels or thresholds
- no rewriting of historical recommendation snapshots
- no advisory hints inside current live recommendation DTOs by default

## Current default gate

The current repository state remains:

- `runtimeMode = measurement_only`
- `approvalRequired = true`
- `autoApplyAllowed = false`

If a runtime consumer cannot prove a stronger gate, it must stay in `measurement_only`.

## Source-of-truth layers

| Layer | Authority |
|---|---|
| Outcome-learning denominator and eligibility meaning | `docs/Analytics/RECOMMENDATION_OUTCOME_LEARNING_CONTRACT.md` |
| Advisory cohort inputs and advisory-only outputs | `docs/Analytics/RECOMMENDATION_ADVISORY_CALIBRATION_CONTRACT.md` |
| Current live recommendation confidence semantics | `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md` |
| Existing measurement-only runtime surface | `docs/architecture/RECOMMENDATION_MEASUREMENT_STATISTICS_REVIEW_SURFACE.md` |
| Runtime gate for any future advisory consumer | this document |

Advisory calibration is downstream evidence. It is not the source of truth for the current recommendation row.

## Runtime gate states

| Gate state | Meaning | What runtime may do |
|---|---|---|
| `measurement_only` | Current default state. | Show lifecycle/outcome statistics only. Ignore or omit advisory hints entirely. |
| `advisory_read_only` | Future additive state for a dedicated review consumer. | Read and display advisory hints from a versioned advisory snapshot, clearly labeled as not applied. |
| `mutation_authorization_required` | Any attempt to change live confidence, ranking or recommendation behavior. | Blocked by this contract. Requires a separate later contract and owner-approved runtime prompt. |

This RL11 contract authorizes only the first two states as documented boundaries. It does not authorize mutation.

## Runtime consumer matrix

| Consumer surface | Current rule | Advisory-read-only allowed later? | Forbidden behavior |
|---|---|---|---|
| `GET /api/analytics/actions/outcomes/summary` `measurementStatistics` payload | Measurement-only only | No; keep existing payload semantics calibration-free | Do not inject advisory hints into lifecycle/outcome counts or reuse them as success/confidence |
| `RecommendationMeasurementStatisticsReview` and the same export/report family | Measurement-only only | No; existing review surface stays a statistics surface | Do not show a local “improved confidence” state or recompute advisory labels |
| Future dedicated internal calibration review API/UI/report/export | Not implemented yet | Yes, but only as a separate advisory section or endpoint | Do not present advisory hints as applied live confidence |
| Product Decision Center, Decision Board, Inventory, Supplier decision surfaces | Current backend confidence remains authoritative | No | Do not read advisory hints into `confidenceScore`, `confidenceLevel`, `reliabilityPct`, `expectedImpactRsd`, ranking or recommendation text |
| Action workflow, queue ordering and automatic action generation | Current workflow semantics only | No | Do not prioritize, auto-approve or suppress actions from advisory hints |
| Historical decision snapshots and timeline views | Historical facts stay immutable | Reference-only at most | Do not rewrite a past recommendation snapshot with later calibration output |

The safest future runtime shape is a dedicated advisory consumer, not a piggybacked extension of the current measurement-only review surface.

## Allowed future write targets

Only additive, versioned advisory artifacts may be written after a later explicit runtime prompt.

| Write target | Allowed by this gate? | Required constraints |
|---|---|---|
| Dedicated calibration advisory snapshot/read model keyed by cohort identity and advisory version | Yes, after later explicit runtime approval | Additive only; preserve source cohort metadata, generated time, contract version and warning codes |
| Approval/audit record that references one advisory snapshot version and names the allowed consumer surface | Yes, after later explicit runtime approval | Must be traceable and must not imply auto-apply |
| Dedicated advisory export/report payload derived from the same snapshot | Yes, after later explicit runtime approval | Must remain read-only and clearly labeled as advisory/not applied |
| Current recommendation DTO confidence or reliability fields | No | Live recommendation truth must stay unchanged |
| Current recommendation ranking, selected action, blocker status or expected impact fields | No | Advisory evidence must not mutate operator-facing decision output |
| Original action/outcome rows or historical evidence snapshots | No | Historical evidence must stay immutable |

## Approval evidence required before any runtime advisory consumer

All of the following must exist before a future runtime prompt may surface advisory hints:

1. `RL10` and `RL11` are both `DONE`.
2. An owner-promoted runtime prompt names the exact endpoint/UI/report surface and exact write target.
3. The cohort still meets the RL01/RL10 minimum-evidence guardrails:
   - at least 10 measured outcomes;
   - at least 5 measured-impact samples for impact guidance;
   - attribution window metadata present;
   - no mixed-family cohort being presented as one calibration statement.
4. The advisory artifact carries stable provenance:
   - `cohortKey`;
   - recommendation family/source segmentation;
   - generated timestamp;
   - contract version;
   - warning codes;
   - `approvalRequired`;
   - `autoApplyAllowed`.
5. Focused regression proof exists for:
   - missing approval stays blocked;
   - insufficient evidence stays advisory-ineligible;
   - advisory snapshot/version mismatch is treated as absent;
   - live recommendation confidence/ranking remains unchanged.
6. A disable/rollback path exists so the advisory consumer can be turned off without affecting live recommendation behavior.

## Canonical runtime rules

1. Existing measurement-only statistics remain valid and calibration-free.
2. Advisory hints may only appear in a dedicated future read-only consumer.
3. `approvalRequired` remains `true` in any advisory payload covered by this gate.
4. `autoApplyAllowed` remains `false` in any advisory payload covered by this gate.
5. A missing advisory snapshot is an absent feature, not `0%` improvement and not `no_change`.
6. A stale, mismatched or under-sampled advisory snapshot must be hidden or marked unavailable, not applied.
7. Advisory hints must never overwrite the original recommendation snapshot, lifecycle record or measured-outcome fact set.
8. Frontend must not infer advisory direction, adjustment hints or approval state if the backend has not sent them.

## Ignore-safely rules

The runtime must remain safe when the advisory path is absent or incomplete.

1. If no advisory snapshot exists, all current runtime surfaces remain measurement-only.
2. If an advisory snapshot exists but no approval record exists, it may only appear in a dedicated advisory consumer and must remain visibly not applied.
3. If a dedicated advisory consumer is not implemented, the advisory snapshot may be ignored entirely with no live-behavior change.
4. If a future consumer cannot prove cohort/version parity with the displayed statistics window, the advisory payload must be treated as unavailable.
5. If a later prompt wants real confidence mutation, it must define a new contract and explicit migration/rollback path instead of broadening this gate silently.

## Acceptance

- one citeable runtime gate contract exists for advisory calibration;
- current runtime remains measurement-only by default;
- only a dedicated future read-only advisory consumer is allowed without a new mutation contract;
- write targets and approval evidence are explicit;
- missing approval or missing advisory evidence stays blocked/absent, not silently advisory-on.
