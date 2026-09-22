Task ID: inventory-audit-prompts-2026-09-22
Queue: direct-user-request
Date: 2026-09-22
Agent/tool: GPT-5.6 Luna
Delivery target: main
Working branch / PR: cursor/inventory-audit-prompts-52eb / https://github.com/ivanjovicic/Trendplus/pull/56
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Audited the `/analytics/inventory` page, its Inventory API client, primary/secondary endpoint contracts, signal query handlers and focused tests.
- Confirmed the missing period selector/trust-header period, mixed current-snapshot versus hidden 30-day signal semantics, secondary signal period/data-scope gaps, alert filter/count mismatch and residual user-facing English.
- Updated the canonical RQ queue with `RQ308` as the single current `READY` prompt and added `RQ371`/`RQ372` as later `WAITING` follow-ups.
- Added a durable Serbian audit note and synchronized `MASTER_ROADMAP.md` with the queue state.

## Files changed
- `docs/ai/INVENTORY_AUDIT_PROMPTS_2026-09-22.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-09-22-inventory-audit-prompts-evidence.md`

## Validation run
- `git diff --check` -> pass
- `node scripts/check-agent-instructions.mjs --self-test` -> pass
- `node scripts/check-agent-instructions.mjs` -> pass
- `node scripts/check-prompt-queues.mjs --self-test` -> pass
- `node scripts/check-prompt-queues.mjs` -> pass (511 tasks)
- `node scripts/check-planning-architecture.mjs --self-test` -> pass
- `node scripts/check-planning-architecture.mjs` -> pass (78 planning tasks)

## Validation not run
- Governance validators -> run after commit/push.
- Frontend/backend tests and live browser/API smoke -> not run; this task creates an audit and queue prompts and does not change runtime code.

## Documentation impact
- Updated the canonical RQ queue, RQ current READY pointer and master roadmap routing.
- Added the dated Inventory audit/prompt intake document.

## What was missed
- No runtime fix was implemented for the queued findings.
- Live production/deployed behavior was not inspected.

## Risks
- Queue prompts describe backend period/data-scope work that still requires contract-owner decisions during implementation; no unsupported runtime behavior was introduced.
- Current `main` verification remains pending until branch transport and delivery complete.

## Next
- Run the queue/planning governance checks, deliver the documentation to `main`, verify `origin/main`, then implement `RQ308`.
