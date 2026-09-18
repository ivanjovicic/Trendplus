Task ID: RQ331-RQ358-operations-audit-round3
Queue: direct-user-request
Date: 2026-09-18
Agent/tool: Codex Cloud Agent
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: da1aee54
Main verification: origin/main contains da1aee54
Evidence state: synchronized

## What was done

- Pulled latest `origin/main` (post RQ312-RQ330 intake).
- Third-round static audit of all 8 Operacije screens plus Supplier redirect targets.
- Verified P1 findings in source: page-local signal KPIs, off-page SKU fake zeros, Pre/Post share recompute, Daily Sales chart sort bug, dead stale refetch paths.
- Added 28 `WAITING` prompts `RQ331`-`RQ358` to canonical queue with full eight-section format.
- Updated `docs/ai/OPERATIONS_AUDIT_PROMPTS_2026-09-18.md` with round 3 matrix.
- Kept `Current READY prompt: none`.

## Files changed

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `docs/ai/OPERATIONS_AUDIT_PROMPTS_2026-09-18.md`
- `.ai/runs/2026-09-18-operations-audit-round3-evidence.md`

## Validation run

- `node scripts/check-prompt-queues.mjs --self-test` -> pass
- `node scripts/check-prompt-queues.mjs` -> pass (497 tasks)
- Operacije focused tests -> not re-run (prior 188/188)

## Validation not run

- Live browser/backend
- Full frontend suite

## Residual risks

- RQ344 may need backend facet endpoint if not already exposed.
- Supplier redirect findings (RQ334/RQ348/RQ354) cross owner boundary with Supplier program but are Operacije-menu reachable.

## Next

- Promote RQ331 (page-local KPIs) or RQ336 (Daily Sales chart sort bug) as first safe P1.
