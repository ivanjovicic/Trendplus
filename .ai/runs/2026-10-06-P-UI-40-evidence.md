Task ID: P-UI-40
Queue: docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
Date: 2026-10-06
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: 3c7a84b3099392dc94209eb46e3bf3bafc0dfc4f
Main verification: pushed to origin/main; fresh fetch confirmed HEAD and origin/main both resolve to 3c7a84b3099392dc94209eb46e3bf3bafc0dfc4f
Evidence state: pending post-close routing recovery

## What was done
- Defaulted the sidebar to the 56px rail from 1024px through 1279px. A localStorage preference now preserves an explicit collapse/expand choice and overrides the viewport default; storage and matchMedia access are guarded for SSR/jsdom/privacy-restricted environments.
- Kept the global header on one row from 1024px up. At widths where the complete control group does not fit, the accessible “Više” dialog keeps every action reachable and supports focus entry, Escape dismissal and focus return. The full controls appear at the 2400px wide-layout breakpoint after 2048px measurements showed the complete group did not fit cleanly there.
- Added responsive shell geometry assertions and tests for the 1024px rail default, persisted preference, toggle persistence, sidebar state semantics and the overflow dialog.

## Files changed
- `Klijent/clientapp/scripts/responsive_baseline.mjs`
- `Klijent/clientapp/src/layout/AppLayout.tsx`
- `Klijent/clientapp/src/layout/AppLayout.spec.tsx`
- `Klijent/clientapp/src/layout/components/HeaderStatus.tsx`
- `Klijent/clientapp/src/layout/components/Sidebar.tsx`
- `Klijent/clientapp/src/layout/components/__tests__/HeaderStatus.spec.tsx`
- `Klijent/clientapp/src/layout/components/__tests__/Sidebar.spec.tsx`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-06-P-UI-40-evidence.md`

## Validation run
- `npm run test -- --run src/layout/AppLayout.spec.tsx src/layout/components/__tests__/HeaderStatus.spec.tsx src/layout/components/__tests__/Sidebar.spec.tsx` -> pass, 15/15.
- `npm run responsive:baseline -- --route-ids app_shell --mode fixture --output-dir "$env:TEMP/trendplus-pui40-shell-acceptance" --strict --viewport-only` -> pass, 20 cases, 0 root overflow observations, 0 page errors. At 1024×768 the header is 67.5px and main is 968px; at 1280×800 the header is 67.5px; at 360/768 the header height matches the baseline. At 2400 the full action group fits in a 75.5px header.
- `npm run responsive:baseline -- --self-test` -> pass; intentional overflow and shell regressions are caught.
- `npm run typecheck` -> pass.
- `npm run build` -> pass; existing chunk-size warning remains.
- `npm run check:analytics-guardrails` -> pass; 39 known baseline violations, 0 removed.
- `node scripts/check-agent-instructions.mjs` -> pass, 14 canonical files.
- `node scripts/check-prompt-queues.mjs` -> pass, 709 tasks.
- `node scripts/check-planning-architecture.mjs` -> pass, 80 planning tasks.
- `git diff --check` -> pass.
- Final responsive result JSON: `%TEMP%/trendplus-pui40-shell-acceptance/responsive-baseline.json`.
- GitHub Analytics Quality Gates run `37384995175`, head SHA `3c7a84b3099392dc94209eb46e3bf3bafc0dfc4f`, was `in_progress` when the completion note was prepared; it is recorded as residual status, not as passing proof.

## Validation not run
- Full local analytics Vitest suite -> not run; the focused shell contract and browser matrix cover the changed layout behavior.

## Documentation impact
- Updated the owning UI queue and `MASTER_ROADMAP.md` with the completion note and delivered SHA.

## What was missed
- None known.

## Risks
- Full controls intentionally stay behind “Više” through 2048px because browser measurements showed the full group did not fit while preserving the page title; the full group is shown at 2400px where it fits in one row.
- GitHub Analytics Quality Gates run `37384995175` was in progress at record time. The build retains its existing chunk-size warning; guardrails retain 39 baseline findings.

## Post-close routing recovery
- Pending: refresh `origin/main` after the terminal queue transition, scan all active RQ/SQL/UI queue and addendum files, re-evaluate changed dependencies, and record the collision-safe successor or a durable Zero-READY proof.

## Next
- Pending post-close recovery.
