# Direct Pre-Nivelacija gate-parity review evidence

```text
Task ID: direct-pre-nivelacija-gate-parity
Queue: direct-user-request
Date: 2026-09-18
Agent/tool: cursor cloud agent
Delivery target: main
Working branch / PR: cursor/pre-nivelacija-gate-parity-c753
Main commit SHA: pending
Main verification: pending
Evidence state: pending
```

## What was done

Reviewed today's `main` commits (`c6284037`–`62e11962`) against RQ297–RQ300 acceptance and the later same-day review repairs.

Confirmed remaining gaps in the Pre-Nivelacija owner:

- RQ297 table/detail/export gated recommendation deltas, but tooltips still leaked raw RSD and blocked rows kept `trend-down` styling.
- Sorting by revenue delta still ranked blocked rows by the hidden number.
- RQ300 still used leftover `toFixed(1)` for the score cell instead of the shared finite formatter.
- RQ299 acceptance asked for reset and back/forward proof that the focused spec did not cover.
- Inline detail stayed open after a focus tab hid the selected row.

Repairs stayed in the Pre-Nivelacija page/spec owner:

- shared gated RSD/trend helpers for table, detail and tooltip;
- unavailable-last sort for blocked recommendation deltas;
- `formatFiniteNumber` for the score cell (locale-safe, no `toFixed`);
- selected detail now follows the visible filtered row set;
- regression tests for export gating, reset URL, history back/forward, focus-hidden detail and blocked-delta ranking.

## Files changed

- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`
- `.ai/runs/2026-09-18-direct-pre-nivelacija-gate-parity-evidence.md`

## Validation run

- `npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/services/__tests__/preNivelacijaApi.scope.spec.ts` — pass (42 tests)
- targeted ESLint on changed Pre-Nivelacija files — pass (0 errors / 0 warnings)
- `npm run typecheck` — pass
- `git diff --check` — pass

## Validation not run

- `npm run build` and full frontend/backend suites — not required for this bounded page/spec repair
- live browser session and CI — not inspected; not named acceptance gates
- `npm run check:analytics-guardrails` — encoding passed; existing repository-wide assignment-pattern violations remain in baseline files including Pre-Nivelacija score/reliability projection. No new local formatter was introduced.

## Documentation impact

- Direct-review evidence only. RQ297–RQ300 remain DONE; this is a same-owner hardening pass, not a new queue prompt.

## What was missed

- `SupplierFootwearAnalyticsPage` and Color surfaces still have similar toolbar/identity patterns outside this owner.
- Guardrail script still flags pre-existing `decisionScore` / `reliabilityPct` assignments used to project backend values.

## Risks

- CI and deployed-browser proof remain uninspected.
- Score cell display now uses `sr-RS` grouping (`0,0` instead of `0.0`), matching the shared formatter.

## Next

- No current READY RQ prompt. Promote the next WAITING candidate only when explicitly requested.
