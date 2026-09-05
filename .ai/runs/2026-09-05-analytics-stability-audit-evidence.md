Task ID: ANALYTICS-STABILITY-AUDIT-2026-09-05
Queue: direct-user-request
Date: 2026-09-05
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: `077903443862ee36d0015f533d8fb1045861bba2`
Main verification: passed - `git rev-parse origin/main` matched `077903443862ee36d0015f533d8fb1045861bba2`; `git merge-base --is-ancestor HEAD origin/main` passed after push
Evidence state: synchronized

## What was done
- Inspected the canonical analytics guidance, current route lineage matrix, suspicious-result audit, affected frontend sources, nearest tests and recent Git history.
- Confirmed three bounded local gaps: Daily Sales null/missing/non-finite values can fall back to zero; Dashboard unknown trends are silently omitted from gain/loss lists; supplier/color/shoe pre/post coverage null can take the measured-zero branch.
- Added a durable stability audit with explicit proof boundaries.
- Added `RQ154` as the sole current `READY` prompt and added `RQ155`/`RQ156` as later `WAITING` prompts. Forecast, Shopify, vendor-comparison and live-runtime work was not promoted.

## Files changed
- `docs/qa/ANALYTICS_STABILITY_AUDIT_2026-09-05.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-09-05-analytics-stability-audit-evidence.md`

## Validation run
- `git diff --check` -> pass
- `node scripts/check-agent-instructions.mjs --self-test` -> pass
- `node scripts/check-agent-instructions.mjs` -> pass (8 canonical files)
- `node scripts/check-prompt-queues.mjs --self-test` -> pass
- `node scripts/check-prompt-queues.mjs` -> pass (302 tasks)
- `node scripts/check-planning-architecture.mjs --self-test` -> pass
- `node scripts/check-planning-architecture.mjs` -> pass (78 new planning tasks)
- `node scripts/check-analytics-lineage-matrix.mjs` -> pass (17 route/family rows and required trust fields)

## Validation not run
- Focused frontend tests, backend tests/build, frontend build -> not run - this task creates audit/queue prompts and does not implement a runtime fix.
- Live database, schema/migration, refresh worker, deployed browser console, theme and chart dimension smoke -> not run - external/runtime proof remains owned by `STAB16`, `RQ145` and `RQ146`.

## Documentation impact
- `ANALYTICS_STABILITY_AUDIT_2026-09-05.md` records concrete evidence, prior repair history, current proof gaps and prompt promotion boundaries.
- The RQ queue and `MASTER_ROADMAP.md` now expose one executable next step and preserve later work as `WAITING`.

## What was missed
- No product runtime code was changed in this planning/audit task.
- Complete cross-route parity, endpoint/schema/migration proof and live refresh/browser proof remain open under existing owners.

## Risks
- The documented defects remain present until `RQ154`, `RQ155` and `RQ156` are executed.
- `READY` means the prompt is runnable, not that the analytics behavior is already fixed.

## Next
- Execute `RQ154` with failing-first Daily Sales null/zero/non-finite regression tests, then advance `RQ155` and `RQ156` according to the queue protocol.
