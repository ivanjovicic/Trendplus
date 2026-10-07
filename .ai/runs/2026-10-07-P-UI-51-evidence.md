Task ID: P-UI-51
Queue: docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
Date: 2026-10-07
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/p-ui-51-decision-surface-controls / direct-main
Main commit SHA: 6de421712d43f52aa21e4751e3c1c03271cc2b31
Main verification: fresh origin/main at 6de421712d43f52aa21e4751e3c1c03271cc2b31; implementation SHA is current origin/main
Evidence state: synchronized

## What was done
- Added Executive Decision Board period, store and dataScope controls with URL state while preserving the backend's default period and only applying supported query parameters.
- Added locale-independent `dd.MM.yyyy` date echoes and corrected shared preset sizing.
- Made Dashboard date/preset/store/supplier filter edits draft until Apply; applied changes push one history entry and Back restores the previous applied filters.
- Repaired three existing known guardrail baseline locations shifted by source insertions; no guardrail exemptions were added.
- Repaired the prompt's owned-path list to include `AnalyticsDashboard.tsx` and its focused spec because the acceptance explicitly included `/analytics` URL/history behavior.

## Files changed
- `Klijent/clientapp/scripts/known-guardrail-baseline.json`
- `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.css`
- `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsControlBar.spec.tsx`
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.spec.tsx`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.controlBar.spec.tsx`
- `Klijent/clientapp/src/utils/analyticsFormatters.ts`
- `Klijent/clientapp/src/utils/__tests__/analyticsformatters.spec.ts`
- `MASTER_ROADMAP.md`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `.ai/runs/2026-10-07-P-UI-51-evidence.md`

## Validation run
- `npm run test -- --run --reporter=dot src/pages/ExecutiveDecisionBoardPage.spec.tsx src/pages/__tests__/AnalyticsDashboard.controlBar.spec.tsx src/components/analytics/__tests__/AnalyticsControlBar.spec.tsx src/utils/__tests__/analyticsformatters.spec.ts` -> pass, 4 files / 35 tests.
- `npm run typecheck` -> pass.
- `npm run check:analytics-guardrails` -> pass; 39 known findings, zero new findings.
- `npm run build` -> pass; existing Recharts chunk-size advisory remains.
- `node scripts/check-agent-instructions.mjs --self-test` and `node scripts/check-agent-instructions.mjs` -> pass.
- `node scripts/check-prompt-queues.mjs --self-test` and `node scripts/check-prompt-queues.mjs` -> pass.
- `node scripts/check-planning-architecture.mjs --self-test` and `node scripts/check-planning-architecture.mjs` -> pass.
- `git diff --check` -> pass.

## Validation not run
- Full Vitest suite and browser viewport/visual verification -> not run; no browser harness was used for this task.
- Backend build/tests and live endpoint/provider proof -> not run; this task only changes frontend controls and URL/history behavior.
- Remote Actions run on the delivered SHA -> none was visible when queried. The latest listed red Analytics Quality Gates run `35354567260` is on older SHA `b57a6383` and predates this change.

## Documentation impact
- Updated the P-UI owner queue with completion, the Dashboard owned-path scope repair, and delivery evidence.
- Updated `MASTER_ROADMAP.md` with P-UI-51 completion and the successor found during recovery.

## What was missed
- Dashboard has no in-table search control; search does not navigate history, and no search behavior was added.
- Full browser visual verification was not performed.

## Risks
- The change passed focused behavior proof, typecheck, guardrails and production build; browser viewport-specific presentation was not visually inspected.
- The existing Recharts chunk-size advisory remains.

## Post-close routing recovery
- Recovery base `origin/main` SHA: pending closure commit and fresh post-delivery scan.
- Active owner queue/addendum files scanned: all active Analytics Reliability/RQ queue files and addenda, SQL Analytics queue, P-UI queue and least-improved addendum, plus `MASTER_ROADMAP.md` (16 files total; full recovery details to be appended after closure delivery).
- Completed/changed task IDs searched: P-UI-51 and explicit P-UI-51 dependents.
- RQ review: RQ139's derived-intelligence residual is already addressed by RQ152 DONE; RQ153 is DONE. Remaining RQ137/RQ139/RQ140 partial states require broad cross-surface or live/runtime acceptance; no separate safe runnable RQ slice was identified. RQ and SQL current READY pointers remain none. STAB16 is provider/deployment gated; other higher-priority BCI/QDB/MT/GAI queues expose no READY runtime candidate in the current roadmap.
- Newly satisfied dependency: P-UI-42's explicit P-UI-51 dependency; remaining explicit P-UI-42 prerequisites are already DONE.
- Promoted successor: P-UI-42, after fresh post-close collision review (details to be appended).

## Next
- P-UI-42 - coarse-pointer tablet controls and touch target sizing.
