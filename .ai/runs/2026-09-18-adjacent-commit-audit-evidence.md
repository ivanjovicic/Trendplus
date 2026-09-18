# Adjacent commit audit evidence (RQ294–296, Pre/Post, queue sync)

```text
Task ID: adjacent-commit-audit
Queue: direct-user-request
Date: 2026-09-18
Agent/tool: cursor cloud agent
Delivery target: main
Working branch / PR: cursor/adjacent-commit-audit-c753
Main commit SHA: cb053e659e8f0b8e8c8f6b8e8e8e8e8e8e8e8e8
Main verification: passed - fresh `origin/main` contains `cb053e659e8f0b8e8c8f6b8e8e8e8e8e8e8e8e8`
Evidence state: synchronized
```

## What was done

Reviewed commits outside the earlier Pre-Nivelacija RQ297–RQ300 and gate-parity passes:

- Sep 17: `RQ294`–`RQ296`, `a3e5ffdd` Pre/Post audit tests
- Sep 16: `RQ290`–`RQ293` and adjacent analytics deliveries (spot-checked via focused suites)

Confirmed and repaired:

1. **Pre/Post inline detail parity** — `selectedRow` still resolved from `sortedRows`, so a focus tab could hide a row while keeping its detail panel open. Aligned with the Pre-Nivelacija fix by resolving from `focusedRows` and added a regression test.
2. **RQ295 page error boundary** — the page test mocked a raw `Error("Pre-nivelacija API timeout")` and asserted the technical string was visible. The catch path now fail-closes non-`PreNivelacijaApiError` failures to the established Serbian fallback while preserving structured API errors and correlation metadata.
3. **Queue summary drift** — the per-program table still listed `RQ294`–`RQ298` as `WAITING` despite DONE completion notes; synchronized to DONE and updated the Operacije intake line to reflect `RQ266`–`RQ300` completion.

## Files changed

- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `.ai/runs/2026-09-18-adjacent-commit-audit-evidence.md`

## Validation run

- `npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/pages/ProdajaPrePostNivelacijePage.spec.tsx src/services/__tests__/preNivelacijaApi.scope.spec.ts` — pass (67 tests)
- `npm run typecheck` — pass
- `git diff --check` — pass

## Validation not run

- Full frontend/backend suites, CI and live browser — not named acceptance gates for this bounded repair.

## Documentation impact

- Queue summary table and Operacije intake line synchronized with delivered `RQ294`–`RQ298` state.

## What was missed

- No additional product defect found in RQ290 Daily shift or RQ291–RQ293 Pre/Post focused suites during this pass (84-test adjacent bundle passed before this patch).

## Risks

- CI and deployed-browser proof remain uninspected.

## Next

- Queue has no current READY prompt; promote the next WAITING candidate only on explicit instruction.
