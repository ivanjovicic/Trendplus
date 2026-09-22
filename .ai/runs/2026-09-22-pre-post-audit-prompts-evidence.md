Task ID: PRE-POST-AUDIT-PROMPTS
Queue: direct-user-request
Date: 2026-09-22
Agent/tool: Cursor Cloud Agent
Delivery target: main
Working branch / PR: `cursor/pre-post-audit-prompts-52eb` / pending
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Audited the Pre/Post Nivelacija frontend, API service, endpoint, DTO, cache key, SQL views and nearest tests.
- Added `RQ385` as the primary `READY` prompt for request scope, cache isolation and visible provenance.
- Added `RQ386` as `WAITING` for event/cohort, cap and denominator reconciliation.
- Added `RQ387` as `WAITING` for runtime payload validation and safe traceable errors.
- Routed Pre/Post ASCII Serbian and residual English/technical copy findings to existing `RQ306` and `RQ325`.
- Updated the RQ current-ready pointer and `MASTER_ROADMAP.md` without implementing runtime fixes.

## Files changed
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-09-22-pre-post-audit-prompts-evidence.md`

## Validation run
- `git diff --check` -> pass
- `node scripts/check-agent-instructions.mjs --self-test` -> pass
- `node scripts/check-agent-instructions.mjs` -> pass
- `node scripts/check-prompt-queues.mjs --self-test` -> pass
- `node scripts/check-prompt-queues.mjs` -> pass (`526` tasks)
- `node scripts/check-planning-architecture.mjs --self-test` -> pass
- `node scripts/check-planning-architecture.mjs` -> pass (`78` new planning tasks)

## Validation not run
- Frontend/backend runtime tests and builds -> not run; this iteration changes only queue, roadmap and evidence documentation.
- Live database/browser/CI proof -> not run; audit findings are queued for later implementation.

## Documentation impact
- Updated the canonical analytics reliability queue, roadmap RQ pointer/truth and durable audit evidence.
- No production code or schema was changed.

## What was missed
- No runtime repair was performed; `RQ385`-`RQ387` are the implementation handoff.
- No live data was available to quantify the scope/cache, overlap or cap effects.

## Risks
- Until `RQ385` is delivered, scoped Pre/Post requests can remain global or cache-collided despite the UI labels.
- Until `RQ386` is delivered, capped/overlapping event data can distort aggregate and trust denominators.
- Until `RQ387` is delivered, malformed/provider failure payloads may bypass the shared runtime contract.

## Next
- Promote/claim `RQ385` for the Pre/Post scope and cache lineage implementation.
