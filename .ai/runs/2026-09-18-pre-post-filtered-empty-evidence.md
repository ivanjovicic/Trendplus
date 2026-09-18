Task ID: pre-post-filtered-empty-audit
Queue: direct-user-request
Date: 2026-09-18
Agent/tool: Cursor Cloud Agent
Delivery target: main
Working branch / PR: main (direct delivery)
Main commit SHA: b57a6383
Main verification: passed - origin/main contains b57a6383
Evidence state: synchronized

## What was done
- Audited remaining Sep 17–18 commits vs prompt acceptance; confirmed Pre-Nivelacija RQ294–300 and adjacent fixes already on main.
- Closed Pre/Post RQ296 parity gap: when focus chips filter all vendor rows but backend data exists, page now shows shared `AnalyticsEmptyState` (`filtered_out`) instead of an empty table shell.
- Added reset action "Vrati prikaz svih dobavljača." and hid KPI/table chrome while filtered-out empty state is active.
- Added regression test for focus-filter-to-zero behavior.

## Files changed
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx`
- `.ai/runs/2026-09-18-pre-post-filtered-empty-evidence.md`

## Validation run
- `npm run test -- --run src/pages/ProdajaPrePostNivelacijePage.spec.tsx src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/services/__tests__/preNivelacijaApi.scope.spec.ts` — pass (68/68)
- `npm run typecheck` — pass

## Validation not run
- Full frontend build / CI — not inspected (residual risk only)

## Documentation impact
- None beyond this run log; queue summary already marks RQ294–300 DONE.

## What was missed
- none known

## Risks
- none known beyond uninspected CI

## Next
- none
