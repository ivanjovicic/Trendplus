Task ID: PERF18
Queue: docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
Date: 2026-10-07
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/perf18-recharts-preload / none
Main commit SHA: 315f3e5824105734b2f4367e5a0ae37841ea0640
Main verification: pass - refreshed `origin/main` `1bd1d65c15d2b3f35b4f69fe10a0b9e537fbeb90` contains the implementation SHA
Evidence state: synchronized after browser-proof follow-up below
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
- GitHub Actions on `42baa32987590ad44660afc72b4a3641baff576f`: Planning Governance `37649069166` -> success; Analytics Quality Gates `37649069125` -> failure, 153 passed / 6 failed in five analytics UI specs outside changed paths. Its analytics guardrail and build steps were skipped after tests failed. No causal link to PERF18's Rollup chunk setting was found; report as unrelated-scope CI risk, not passing validation.

## Validation not run at initial close (resolved in browser-proof follow-up)
- At the first close, data-backed browser chart SVG render and SPA route-hop were not proven. The existing `responsive_baseline.mjs --route-id daily_sales --mode fixture` failed inside its geometry collector. The follow-up below adds an independently scoped Puppeteer fixture and completes this acceptance.

## Documentation impact
- Updated the PERF18 source-of-truth bundle budget with current build/import/network measurements and the guard behavior.
- Repaired the circular P-UI-24 `Ready after` prerequisite and synchronized the PERF current pointer/status across owner queue, master roadmap and performance roadmap.

## What was missed
- Deterministic browser evidence that a chart SVG renders after an SPA route-hop from `/prodaja`.

## Risks at initial close (updated by follow-up)
- Initial risk was missing data-backed route-hop render proof. The deterministic synthetic fixture now proves route/chunk/render integration. It does not claim production API semantics or deployed performance.

## Post-close routing recovery after initial close (historical snapshot; superseded by browser-proof follow-up)
- Recovery base `origin/main`: `42baa32987590ad44660afc72b4a3641baff576f`.
- Active owner queue files scanned: `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` (full active PERF/OBS/SEC set), `MASTER_ROADMAP.md` current priority/current-READY table, and all current owner queues/addenda named there through the canonical active queue inventory.
- Changed task searched: `PERF18`; dependencies checked: `PERF17` remains DONE; no newly satisfied dependency was discovered.
- PERF candidates at the initial close: `PERF18` was PARTIAL pending its browser-proof follow-up; `PERF19` was DONE; `PERF16` was BLOCKED on MT10/shared-SaaS authority; OBS queue was complete; SEC05 was WAITING on MT09. No different PERF/OBS/SEC task was promoted.
- Higher-priority/global candidates: BCI has no READY/IN_PROGRESS; STAB16 remains provider/deployed-evidence gated; RQ and SQL current pointers are none with remaining WAITING/PARTIAL prompts gated by dependencies/evidence; P-UI-52 is already IN_PROGRESS under its named owner; P-UI-50 is blocked by a user-owned local Product Decision page edit; P-UI-38 is the final gate; QDB07 waits for release gates; MT02 waits for an owner decision; GAI waits for core-pilot/release evidence; DEX/RL/DT have no current READY prompt.
- Safe/disjoint split: none identified that is both dependency-complete and collision-safe; do not take over P-UI-52 or alter the user's blocked P-UI-50 checkout edit.
- Successor at the initial close: no different prompt was promoted because the explicit same-task PERF18 browser fixture remained open. That follow-up is resolved below.
- The remaining active queue inventory considered: BCI parent/addendum; STAB; all RQ queue/addenda and active SQL queue; P-UI main/addenda; QDB; MT; GAI; DEX/RL/DT; PERF/OBS/SEC. No other READY candidate was promoted. Existing owners and blockers remain unchanged.

## Browser-proof follow-up (2026-10-07)
- Resumed the same PERF18 owner branch from refreshed `origin/main` `e0502a9d4c6e102c4a13b014c782ee9fba2492a0`; worktree was clean and no existing task lock was present.
- Added `Klijent/clientapp/scripts/check-recharts-route-loading.mjs` plus `check:recharts-route-loading` npm script. The helper uses synthetic daily-sales/store/refresh fixtures, CORS preflight responses for the app's configured remote API origin, and the real `Operacije` sidebar link to perform the SPA navigation.
- Production preview result: PASS. `/prodaja` requested no Recharts asset; after SPA navigation to `/analytics/daily-sales`, the Recharts asset loaded and 13 `.daily-sales-chart-wrap svg.recharts-surface` nodes rendered. No browser `pageerror` occurred.
- The first fixture attempt omitted CORS response headers and correctly failed closed in the browser; adding explicit wildcard origin/method/header allowances made the deterministic fixture work without contacting the remote API. Unknown API routes return 503 fixture responses.
- Fresh validation on the final implementation: `npm run build` PASS (2,732 modules; entry 326,002 bytes, Recharts 263,632 bytes); `npm run check:bundle-budget -- --self-test` PASS; `npm run check:bundle-budget` PASS; `npm run test:run -- src/pages/__tests__/DailySalesStatsPage.spec.tsx` PASS (5/5); route-loading helper PASS (13 SVG surfaces); `node --check scripts/check-recharts-route-loading.mjs` PASS.
- Evidence limits: browser fixture is synthetic and proves route/chunk/render integration, not API/data semantics or deployed performance.
- PERF18 status: DONE; no PERF18 acceptance item remains. Queue, master roadmap and performance roadmap synchronized. Implementation/evidence commit `315f3e5824105734b2f4367e5a0ae37841ea0640` is contained by verified `origin/main` `1bd1d65c15d2b3f35b4f69fe10a0b9e537fbeb90`.

## Next
- None for PERF18.
