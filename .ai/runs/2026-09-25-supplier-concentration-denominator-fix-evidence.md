Task ID: direct-supplier-concentration-denominator-fix
Queue: direct-user-request
Date: 2026-09-25
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: 404e4255
Main verification: passed - origin/main contains 404e4255 after push.
Evidence state: synchronized

## What was done
- Changed Supplier Sales visible share, Top 5 and concentration calculations to use the same visible population and a positive net-revenue denominator.
- Included `Nepoznato` whenever it is part of the visible population.
- Kept negative-net-revenue suppliers visible in the table, but made their derived positive-revenue share unavailable and excluded them from concentration ranking.
- Made concentration unavailable for a focused single-supplier view instead of presenting a trivial 100% concentration.
- Preserved the known-only total PoP fail-closed behavior; no backend previous-period aggregate was invented.

## Files changed
- Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx
- Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx
- Klijent/clientapp/scripts/known-guardrail-baseline.json

## Validation run
- `npm run test -- --run src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/SupplierSalesStatsPage.decisionSuppliers.spec.tsx` -> pass, 41/41 tests.
- `npm run check:analytics-guardrails` -> pass; 50 known baseline violations, 0 removed.
- `npm run build` -> pass.
- `git diff --check` -> pass.

## Validation not run
- .NET tests -> not run; no backend code or DTO contract changed.
- Live/browser smoke -> not run; no live environment was requested and focused UI tests cover the changed calculation/render contract.

## Documentation impact
- No owner queue or product contract document changed. Existing PS11/RQ443 documentation already records the known-only previous-period backend gap and the intentional `N/A` behavior.
- This run log records the positive-net-revenue denominator and negative-row treatment introduced by the direct fix.

## What was missed
- Backend aggregate for previous-period revenue of the known-only population remains unimplemented; known-only total PoP remains `N/A` until that contract exists.

## Risks
- The positive-net-revenue share is a derived presentation metric and must remain labelled as such; raw signed revenue totals remain separate.
- Remote CI was not inspected; local focused proof and main verification remain the delivery gate.

## Next
- Add a backend scoped previous-period total for known suppliers if the product requires a numeric known-only PoP trend.
