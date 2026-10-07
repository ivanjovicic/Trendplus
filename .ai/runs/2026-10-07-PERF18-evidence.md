Task ID: PERF18
Queue: docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
Date: 2026-10-07
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/perf18-recharts-preload / none
Main commit SHA: pending
Main verification: pending
Evidence state: pending
Ownership transfer: none

## What was done
- Claimed PERF18 from refreshed `origin/main` `d80564ad367a21aa08dbc12f94e6e71feb8af4be` after confirming no matching active remote branch, lock or PR.
- Repaired the circular start gate: current-main build/browser evidence is an executable first step in PERF18, not an external prerequisite dependent on P-UI-24.
- Reproduced the eager Recharts preload on the baseline build. `dist/index.html` preloaded `recharts-BFPhEesj.js` (548,036 bytes); `/prodaja` requested it with zero chart nodes. The source map showed React, ReactDOM, Scheduler and React Redux in that manual chunk.
- Set Rollup `onlyExplicitManualChunks: true`. The resulting Recharts chunk is 263,632 bytes and contains 198 Recharts source modules with no React/ReactDOM/Scheduler/React Redux source modules. The 141,410-byte shared runtime chunk is preloaded, while Recharts is absent from the application entry's modulepreload links.
- Extended the existing bundle budget guard to reject Recharts modulepreloads and added a seeded recurrence self-test.
- Updated the measured bundle contract and PERF routing/evidence. PERF remains PARTIAL until deterministic browser proof records a chart SVG render after an SPA route-hop.

## Files changed
- `Klijent/clientapp/vite.config.ts`
- `Klijent/clientapp/scripts/check-bundle-budget.mjs`
- `docs/architecture/PERFORMANCE_FRONTEND_BUNDLE_BUDGET.md`
- `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
- `docs/roadmaps/PERFORMANCE_ROADMAP.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-07-PERF18-evidence.md`

## Validation run
- `npm ci` -> pass; 464 packages installed, audit reported 0 vulnerabilities.
- `npm run build` on baseline `d80564ad...` -> pass; 2,732 modules transformed; reproduced Recharts preload; no circular/execution-order warning.
- Puppeteer baseline `/prodaja` and `/analytics` at `domcontentloaded` -> pass as reproduction; Recharts requested on both routes; no page errors or failed asset requests. Network-idle was intentionally not used because fixture-free API requests remained active.
- `npm run build` after the chunking change -> pass; 2,732 modules transformed; entry 326,002 bytes, shared runtime 141,410 bytes, Recharts 263,632 bytes; no new circular/execution-order warning.
- Puppeteer after change -> `/prodaja` did not request Recharts; direct `/analytics/daily-sales` did request Recharts and had no page JavaScript errors. Backend API calls failed because no local backend was running.
- `npm run typecheck` -> pass.
- `npm run check:bundle-budget -- --self-test` -> pass; seeded Recharts preload recurrence detected.
- `npm run check:bundle-budget` -> pass.
- `npm run test -- --run src/pages/__tests__/DailySalesStatsPage.spec.tsx` -> pass, 5/5 tests.
- `node scripts/check-agent-instructions.mjs --self-test` and `node scripts/check-agent-instructions.mjs` -> pass.
- `node scripts/check-prompt-queues.mjs --self-test` and `node scripts/check-prompt-queues.mjs` -> pass, 711 tasks.
- `node scripts/check-planning-architecture.mjs --self-test` and `node scripts/check-planning-architecture.mjs` -> pass, 80 planning tasks.
- `git diff --check` -> pass.

## Validation not run
- Data-backed browser chart SVG render and SPA route-hop -> not proven. The existing `responsive_baseline.mjs --route-id daily_sales --mode fixture` failed inside its geometry collector while reading `scrollWidth` from a null target; fixture-free browser runs correctly exposed the missing backend and therefore did not render chart data. A subsequent direct browser route proved the Recharts chunk loads without module-init errors, but this does not replace the requested render/hop proof.
- GitHub Actions status -> pending post-delivery inspection.

## Documentation impact
- Updated the PERF18 source-of-truth bundle budget with current build/import/network measurements and the guard behavior.
- Repaired the circular P-UI-24 `Ready after` prerequisite and synchronized the PERF current pointer/status across owner queue, master roadmap and performance roadmap.

## What was missed
- Deterministic browser evidence that a chart SVG renders after an SPA route-hop from `/prodaja`.

## Risks
- The build and source-map evidence show the preload regression is fixed, and the chart route loads the Recharts chunk without a JavaScript page error. The missing data-backed route-hop render keeps acceptance incomplete; PERF18 is PARTIAL.

## Post-close routing recovery
- Pending: refresh post-delivery `origin/main`, inspect current queue state and relevant Actions, then record the cascade/Zero-READY result here.

## Next
- Resume PERF18 with a deterministic API fixture and prove an SVG render after the SPA route-hop; then complete post-close routing recovery and update this evidence.
