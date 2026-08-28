Task ID: doc-plan-prompt-seeding
Queue: direct-user-request
Date: 2026-08-28
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Reviewed the active analytics reliability queues, master roadmap, and supporting QA/run-log evidence to find concrete open accuracy/provenance gaps instead of duplicating already-closed prompts.
- Added `RQ130` to the active cross-surface analytics reliability addendum for the remaining vendor recommendation fake-zero known-margin baseline gap.
- Added `RQ131` to the same addendum for vendor pre/post zero-baseline semantic-field parity so downstream surfaces can distinguish normal trend from new/no baseline semantics.
- Synced `MASTER_ROADMAP.md` so the RQ program truth now records both new prompts as later `WAITING` follow-ups without changing the current `READY` state.

## Files changed
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_CROSS_SURFACE_ADDENDUM.md
- MASTER_ROADMAP.md
- .ai/runs/2026-08-28-doc-plan-prompt-seeding-evidence.md

## Validation run
- `git diff --check` -> pass
- `node scripts/check-agent-instructions.mjs --self-test` -> pass
- `node scripts/check-agent-instructions.mjs` -> pass
- `node scripts/check-prompt-queues.mjs --self-test` -> pass
- `node scripts/check-prompt-queues.mjs` -> pass
- `node scripts/check-planning-architecture.mjs --self-test` -> pass
- `node scripts/check-planning-architecture.mjs` -> pass

## Validation not run
- Runtime/backend/frontend tests -> not run - docs/planning/queue changes only; this pass prepared prompts and routing truth without changing application behavior

## Documentation impact
- Added two new executable vendor trust prompts to the active cross-surface analytics reliability queue.
- Updated the canonical master roadmap so the prepared follow-up backlog is visible from the main routing entry point.

## What was missed
- No runtime code or production verification was performed in this planning/documents-only pass.

## Risks
- The new prompts are prepared as `WAITING` only; they do not change current execution priority and still depend on owner promotion / existing gates.

## Next
- `RQ130` when the owner wants the narrower vendor recommendation fake-zero guard after the current higher-priority live-evidence gate.
- `RQ131` after `RQ130` or explicit owner promotion for additive vendor zero-baseline surface parity.
