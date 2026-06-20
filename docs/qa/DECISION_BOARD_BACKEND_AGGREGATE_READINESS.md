# Decision Board Backend Aggregate Readiness Review

Date/time: 2026-06-19 15:52:58 +02:00
Review HEAD: `a8602ae75f9d60708c604dd3482576a4e7161ce3`

## Purpose

Q45 checks whether the current Phase 1 Executive Decision Board model is stable enough to justify a backend aggregate endpoint design review.

## Inputs reviewed

- [Analytics Decision OS Roadmap](../analytics/ANALYTICS_DECISION_OS_ROADMAP.md)
- [Executive Decision Board Plan](../analytics/EXECUTIVE_DECISION_BOARD_PLAN.md)
- [Executive Decision Board Quality Audit](EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md)
- [ExecutiveDecisionBoardPage.tsx](../../Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx)
- [ExecutiveDecisionBoardPage.spec.ts](../../Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts)

## Readiness summary

The board is ready for backend aggregate design review.

The current Phase 1 composition already has the stability needed to define an aggregate contract without guessing semantics:

- seven sections are established and tested
- `insufficient_data` does not outrank real decision cards
- stale and partial states remain visible
- missing expected impact stays nullable, not `0 RSD`
- repeated source cards are intentionally repeated with section context
- action state and source links remain explicit

## What is ready

| Criterion | Evidence | Readiness |
| --- | --- | --- |
| Section model | Seven board sections are stable in the frontend model and tests | Ready |
| Confidence semantics | Missing or weak confidence remains explicit and capped | Ready |
| Nullable impact | Missing impact renders as unavailable rather than fake zero | Ready |
| Warning visibility | Stale/partial/error markers stay visible | Ready |
| Source continuity | Repeated cards keep source and section context | Ready |
| Action-state honesty | Already-open / already-closed states remain explicit | Ready |

## What is not ready yet

| Topic | Why it is still separate from the review |
| --- | --- |
| Aggregate implementation | The backend endpoint itself has not been designed or implemented yet. |
| Global dedupe policy | The current board intentionally repeats some sources across different decision lenses. |
| Server-side ranking rules | They should be derived from the stable Phase 1 model, not invented anew. |

## Decision

Proceed with the backend aggregate design review.

Do not implement the endpoint yet. The next step should be a contract-and-shape task that preserves:

- nullable confidence
- nullable impact
- section context
- source links
- warning codes
- action state

## Recommended follow-up

- design the read-only aggregate response shape
- define section payloads and warning metadata
- keep the Phase 1 frontend board as the reference model until the aggregate matches it

## Checks

- `git diff --check` - not yet run after this doc update
- Existing board quality audit checks are already documented in [Executive Decision Board Quality Audit](EXECUTIVE_DECISION_BOARD_QUALITY_AUDIT.md)

