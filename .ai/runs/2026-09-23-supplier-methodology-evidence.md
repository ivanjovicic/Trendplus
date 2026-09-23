Task ID: supplier-methodology
Queue: direct-user-request
Date: 2026-09-23
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: 02b22e295508d8942e1a5b5e5f2ef82c676896ed
Main verification: passed - fresh fetch confirms `origin/main` equals `02b22e295508d8942e1a5b5e5f2ef82c676896ed`
Evidence state: synchronized

## What was done
- Investigated the live Supplier overview URL and confirmed the deployed page currently returns a generic application error, so live numeric data could not be used as proof.
- Traced the Supplier overview KPI methodology actions to `analyticsMetricDefinitions.ts`, the Supplier Sales page and the `/api/analytics/supplier-sales-stats` endpoint.
- Added documented methodology for PoP revenue change, supplier total cost and supplier-average margin; corrected the top-five supplier share definition; and bound the Supplier average-margin KPI to its specific methodology key.
- Added a regression test proving all Supplier overview KPI methodology keys render as documented rather than using the generic fallback message.

## Files changed
- Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts
- Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx
- Klijent/clientapp/src/components/analytics/__tests__/MetricMethodologyPanel.spec.tsx
- .ai/runs/2026-09-23-supplier-methodology-evidence.md

## Validation run
- `npm run test -- --run src/components/analytics/__tests__/MetricMethodologyPanel.spec.tsx` -> pass (3 tests)
- `npm run test -- --run src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/utils/__tests__/analyticsMetricDefinitions.spec.ts` -> pass (27 tests)
- `npm run check:analytics-guardrails` -> pass (encoding, guardrail self-test, baseline check, typecheck)
- `npm run build` -> pass (`BUILD_EXIT=0`; Vite production build completed)
- `git diff --check` -> pass

## Validation not run
- Live deployed Supplier data verification -> not proven; the supplied Vercel URL returned the generic error state during inspection.
- Backend integration/live database proof -> not run; this change only updates the frontend methodology registry and its presentation binding.

## Documentation impact
- The shared frontend metric registry is now the durable methodology documentation for the affected Supplier overview KPI actions.
- No queue, roadmap or backend contract document was changed because this was a direct UI documentation/projection repair.

## What was missed
- The deployed environment was not changed or rechecked after the local fix.

## Risks
- The displayed `prosecnaMarza` remains the backend's current arithmetic average of known supplier margins; the new wording documents that behavior and does not change its business calculation.
- Existing Vite chunk-size warnings remain outside this scope.

## Next
- Recheck the deployed Supplier overview once the Vercel deployment for `02b22e295508d8942e1a5b5e5f2ef82c676896ed` is available.
