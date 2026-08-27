Task ID: analytics-deep-gap-audit
Queue: direct-user-request
Date: 2026-08-25
Agent/tool: Codex
Delivery target: none
Working branch / PR: main / none
Main commit SHA: pending
Main verification: not run - no commit to main in this turn
Evidence state: synchronized

## What was done
- Performed a deeper read-only audit of analytics screens and calculation paths, focusing on trust-state gaps, stale/freshness lineage, and potential fake-zero comparison baselines.
- Cross-checked new findings against existing analytics reliability queues to avoid duplicating already-covered prompts such as RQ121, RQ122, RQ124, RQ51-RQ63, and RQ105.
- Added three new `WAITING` prompts to the cross-surface analytics reliability addendum:
- `RQ125` for supplier/shoe/color stats trust/freshness metadata.
- `RQ126` for Daily Sales trust metadata and trust-header parity.
- `RQ127` for supplier/shoe/color missing known-margin baseline being silently treated as `0`.

## Files changed
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md

## Validation run
- `node scripts/check-agent-instructions.mjs --self-test` -> pass
- `node scripts/check-agent-instructions.mjs` -> pass
- `node scripts/check-prompt-queues.mjs --self-test` -> pass
- `node scripts/check-prompt-queues.mjs` -> pass
- `node scripts/check-planning-architecture.mjs --self-test` -> pass
- `node scripts/check-planning-architecture.mjs` -> pass
- `git diff --check` -> pass

## Validation not run
- runtime/frontend/backend tests -> not run - queue-document audit only
- main verification / delivery SHA proof -> not run - no commit or push to main in this turn

## Documentation impact
- Expanded the canonical cross-surface analytics reliability backlog with three new uncovered findings backed by concrete code evidence.

## What was missed
- No prompt was promoted or executed in this turn; `RQ120` remains the current READY prompt in the main analytics queue.
- Other possible non-analytics TODOs and lower-signal style inconsistencies were intentionally left out to keep the new prompts concrete and high-confidence.

## Risks
- The new prompts are intentionally conservative and may still need reprioritization once `RQ120` is completed.
- No main-branch delivery proof was produced in this turn.

## Next
- Keep `RQ120` as current READY.
- Use `RQ125`-`RQ127` as later cross-surface follow-ups after the current trust-metadata lane or explicit owner promotion.
