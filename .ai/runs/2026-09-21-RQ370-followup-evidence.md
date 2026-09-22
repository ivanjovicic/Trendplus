Task ID: RQ370-followup
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-21
Agent/tool: Cursor Cloud Agent
Delivery target: main
Working branch / PR: direct main follow-up
Main commit SHA: ff2e563fcde0238d80bf7a83abd99ada1a4292f8
Main verification: passed - origin/main contains implementation SHA ff2e563fcde0238d80bf7a83abd99ada1a4292f8
Evidence state: synchronized

## What was done
- Re-audited the latest RQ370 implementation against its acceptance criterion for superseding data-scope requests.
- Found that both size-curve effects owned AbortControllers but omitted `inventoryDataScope` from their dependency arrays, so a scope change did not run cleanup.
- Added the missing dependency to the detail and standalone size-curve effects.
- Added a regression test proving the active size-curve request is aborted after a data-scope change.

## Files changed
- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/InventoryPage.offPageDetail.spec.tsx`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-09-21-RQ370-followup-evidence.md`

## Validation run
- `npm run test:run -- src/pages/__tests__/InventoryPage.offPageDetail.spec.tsx` -> pass, 1 file / 5 tests
- `npm run test:run -- src/pages/__tests__/InventoryPage.signalWindow.spec.tsx src/pages/__tests__/InventoryPage.offPageDetail.spec.tsx src/services/__tests__/inventoryDataScopeApi.spec.ts` -> pass, 3 files / 12 tests
- `npm run check:analytics-guardrails` -> pass, baseline-only with 51 known violations, 0 new; encoding, guardrail self-test and typecheck passed
- `git diff --check` -> pass
- `git push origin main` -> pass; `origin/main` contains `ff2e563fcde0238d80bf7a83abd99ada1a4292f8`

## Validation not run
- Full frontend suite, live browser, backend tests and CI -> not run; the correction is frontend-only and local `dotnet` is unavailable.

## Documentation impact
- Updated the RQ370 completion note and `MASTER_ROADMAP.md` to record the missed dependency and its correction.

## What was missed
- The original RQ370 implementation did not include scope dependencies on the two size-curve effects; this follow-up closes that gap.

## Risks
- No endpoint, backend, tenant or business-metric contract changed.
- Full frontend suite, live browser, backend tests and CI remain residual validation risk.

## Next
- Return to the remaining Operacije WAITING backlog.
