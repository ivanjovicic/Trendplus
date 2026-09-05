# Analytics Queue Split Evidence

Task ID: queue-split-2026-09-05
Queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
Date: 2026-09-05
Agent/tool: Codex
Delivery target: main
Working branch / PR: `main`
Main commit SHA: `4265070a0c328b5dd1a0a49f5c69c62d89e1105f`
Main verification: passed - current `origin/main` contains `4265070a0c328b5dd1a0a49f5c69c62d89e1105f`
Evidence state: synchronized

## What was done

Reviewed unfinished analytics reliability prompts and extracted only bounded work that is executable without forecast, Shopify, vendor-comparison or live-worker access. Promoted `RQ151` as the single current `READY` prompt, and added `RQ152` and `RQ153` as later `WAITING` continuations. Existing unfinished prompts remain in place with their broader scope and dependencies unchanged.

`RQ151` isolates the confirmed raw unknown warning-code leak in `AnalyticsActionsPage`. `RQ152` isolates the remaining `analyticsIntelligenceDerived.ts` numeric fallback residual. `RQ153` isolates the offline route lineage matrix while explicitly excluding live refresh claims owned by `STAB16`.

## Files changed

- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-09-05-queue-split-evidence.md`

## Validation run

- `node scripts/check-prompt-queues.mjs --self-test` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass, 299 tasks.
- `node scripts/check-agent-instructions.mjs` -> pass, 8 canonical files.
- `node scripts/check-planning-architecture.mjs` -> pass, 78 planning tasks.
- `git diff --check` -> pass.

## Validation not run

- Runtime/backend/frontend tests and builds -> not run; this is a queue and planning documentation change only.
- Live database, refresh worker, browser console and production proof -> not run; explicitly excluded and owned by `STAB16` where applicable.

## Documentation impact

Updated the RQ queue current-ready pointer/status matrix and `MASTER_ROADMAP.md` routing row. Added three bounded prompt definitions with required queue sections and explicit exclusions.

## What was missed

No unfinished prompt was marked DONE. Forecast (`RQ142`/`RQ150`), Shopify and similar excluded work remains WAITING. The original broad prompts retain their unresolved parity, lineage, schema, live-runtime and measurement dependencies.

## Risks

`RQ151` is the only executable RQ prompt because the queue protocol permits one READY prompt per program. The new prompts deliberately do not prove live runtime behavior or business metric correctness; those claims remain gated by their declared dependencies.

## Next

Execute `RQ151`; after completion promote either `RQ152` or `RQ153` one at a time. Keep `STAB16` before live proof and keep forecast/Shopify work WAITING by user instruction.
