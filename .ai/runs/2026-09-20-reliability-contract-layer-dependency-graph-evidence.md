Task ID: 2026-09-20-reliability-contract-layer-dependency-graph
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-20
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / no PR
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Made the systemic Reliability Contract Layer dependency graph explicit.
- Frontend reliability chain: RQ359 -> RQ360 -> RQ366 -> RQ361 -> RQ362 -> RQ363 -> RQ364.
- Marked RQ365 as an independent database-reliability branch and `Parallel-safe: yes`.
- Made RQ367 wait for RQ361 + RQ363 + RQ364 + RQ365, with RQ361 transitively requiring RQ366.
- Kept the queue at `Current READY prompt: none`; no prompt was promoted or claimed.

## Files changed
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
- .ai/runs/2026-09-20-reliability-contract-layer-dependency-graph-evidence.md

## Validation run
- `git diff --check` -> pass.
- `node scripts/check-agent-instructions.mjs --self-test` -> pass.
- `node scripts/check-agent-instructions.mjs` -> pass, 8 canonical files checked.
- `node scripts/check-prompt-queues.mjs --self-test` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass, 506 tasks checked.
- `node scripts/check-planning-architecture.mjs --self-test` -> pass.
- `node scripts/check-planning-architecture.mjs` -> pass, 78 planning tasks checked.

## Validation not run
- Runtime/frontend/backend tests -> not run; this is queue governance documentation only.
- Remote CI -> not inspected; docs-only change does not require waiting for CI before main delivery.

## Documentation impact
- Added explicit READY AFTER conditions to RQ359-RQ367 so a selector can identify the next safe candidate after each completion.
- Preserved the canonical one-READY/explicit-none rule; independence of RQ365 is recorded without promoting it automatically.

## What was missed
- No runtime implementation or prompt claim was performed.

## Risks
- The queue remains intentionally idle until an owner explicitly promotes the first candidate, RQ359 or an allowed independent RQ365 branch.

## Next
- When execution is authorized, promote RQ359 for the frontend reliability chain or RQ365 for the independent database smoke branch, subject to current global priority and queue state.
