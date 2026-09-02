Task ID: PERF17
Queue: docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
Date: 2026-09-02
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: 2407762d3b2a4bafaf46367910551395f157f369
Main verification: origin/main contains 2407762d3b2a4bafaf46367910551395f157f369 after push
Evidence state: synchronized

## What was done
- Promoted and executed the prepared, parallel-safe PERF17 planning/measurement prompt.
- Measured the current client production bundle and traced the largest chunk to the shared Recharts dependency used by chart-bearing lazy routes.
- Tested removal of the existing Recharts manual split; rejected it because Rollup emitted circular-dependency warnings that could break execution order.
- Added a deterministic post-build bundle budget check with a measured Recharts exception and documented the contract.
- Kept analytics/API/worker behavior and the Vite warning threshold unchanged.

## Files changed
- Klijent/clientapp/package.json
- Klijent/clientapp/scripts/check-bundle-budget.mjs
- docs/architecture/PERFORMANCE_FRONTEND_BUNDLE_BUDGET.md
- docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
- docs/roadmaps/PERFORMANCE_ROADMAP.md
- MASTER_ROADMAP.md

## Validation run
- `cd Klijent/clientapp; npm run typecheck` -> pass
- `cd Klijent/clientapp; npm run build` with existing manual Recharts split -> pass; 2,597 modules, `recharts` 548.04 kB raw / 164.13 kB gzip
- `cd Klijent/clientapp; npm run check:bundle-budget` -> pass; exact Recharts asset size 548,036 bytes
- `cd Klijent/clientapp; npm run build` without manual Recharts split -> pass with 13 circular-dependency warnings; rejected as unsafe alternative
- `git diff --check` -> pass
- `node scripts/check-agent-instructions.mjs --self-test` -> pass
- `node scripts/check-agent-instructions.mjs` -> pass
- `node scripts/check-prompt-queues.mjs --self-test` -> pass
- `node scripts/check-prompt-queues.mjs` -> pass
- `node scripts/check-planning-architecture.mjs --self-test` -> pass
- `node scripts/check-planning-architecture.mjs` -> pass

## Validation not run
- Affected Vitest suite -> not run; no source/component behavior changed.
- CI workflow execution -> not run; CI wiring is outside this planning-only prompt.
- Browser/runtime smoke -> not run; the change is a build guardrail and documentation contract only.

## Documentation impact
- Added `docs/architecture/PERFORMANCE_FRONTEND_BUNDLE_BUDGET.md`.
- Updated the PERF queue, performance roadmap and master routing truth with the PERF17 completion and returned READY state `none`.

## What was missed
- No import-level Recharts optimization was shipped; a separate runtime/browser-proven task would be required.

## Risks
- Vite still prints its existing 500 kB warning for the intentional Recharts exception; the dedicated guardrail fails above 560,000 raw bytes.
- CI has not yet been wired to invoke the new guardrail.

## Next
- Consider a separate runtime/browser-proven Recharts import split only if bundle growth exceeds the measured budget.
