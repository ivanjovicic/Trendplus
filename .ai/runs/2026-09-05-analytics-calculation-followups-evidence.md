Task ID: ANALYTICS-CALCULATION-FOLLOWUPS-2026-09-05
Queue: direct-user-request
Date: 2026-09-05
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Reviewed the existing analytics reliability audit, route lineage and queue state.
- Inspected backend/frontend calculation paths and nearest tests for additional false-zero, denominator, period and provenance failures.
- Documented five bounded follow-up prompts: `RQ157` Product Decision baseline/coverage state, `RQ158` nullable inventory stock state, `RQ159` inventory summary count semantics, `RQ160` inventory health provenance, and `RQ161` Analytics Details period/trend state.
- Kept `RQ154` as the only `READY` prompt. New prompts are `WAITING` behind the queue order.
- Did not promote or add forecast, Shopify, vendor-comparison or other explicitly excluded work.

## Files changed
- `docs/qa/ANALYTICS_STABILITY_AUDIT_2026-09-05.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-09-05-analytics-calculation-followups-evidence.md`

## Validation run
- Static source/history review of the five findings -> pass.
- `git diff --check` -> pass.
- `node scripts/check-agent-instructions.mjs --self-test` -> pass.
- `node scripts/check-agent-instructions.mjs` -> pass (8 canonical files checked).
- `node scripts/check-prompt-queues.mjs --self-test` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass (307 tasks).
- `node scripts/check-planning-architecture.mjs --self-test` -> pass.
- `node scripts/check-planning-architecture.mjs` -> pass (78 new planning tasks checked).
- `node scripts/check-analytics-lineage-matrix.mjs` -> pass (17 route/family rows and required trust fields).

## Validation not run
- Focused frontend/backend runtime tests -> not run - this task writes audit/queue/evidence documentation and does not implement runtime fixes.
- Backend build/test and frontend build -> not run - no production code changed.
- Browser/live refresh/database/migration/console proof -> not run - remains owned by existing runtime/live follow-ups.

## Documentation impact
- Updated the analytics stability audit with concrete evidence and residual risks.
- Added five detailed reliability prompts to the owning analytics queue and updated the roadmap pointer.

## What was missed
- The five documented defects are not fixed by this docs-only task.
- Complete route/table/chart/export/report parity, runtime schema/migration proof and live browser/refresh proof remain open under their existing owners.

## Risks
- Until `RQ157`-`RQ161` execute, the affected screens can still expose misleading zero, trend, count or synthetic-series semantics.
- No runtime behavior or deployed state is claimed as fixed here.

## Next
- Execute `RQ154`, then advance the new prompts in queue order after their dependencies are satisfied.
