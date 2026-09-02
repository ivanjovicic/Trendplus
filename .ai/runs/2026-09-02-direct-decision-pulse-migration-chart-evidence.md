Task ID: direct-decision-pulse-migration-chart
Queue: direct-user-request
Date: 2026-09-02
Agent/tool: Codex
Delivery target: main
Working branch / PR: main
Main commit SHA: 137952c18c5c6eb5e042080f53764e0ab2dea84a
Main verification: origin/main contains 137952c18c5c6eb5e042080f53764e0ab2dea84a
Evidence state: synchronized

## What was done
- Confirmed `DecisionPulse` is registered, mapped at `/api/analytics/decision-pulse`, and live on Render with HTTP 200.
- Fixed migration 029 so the 180-day dependency view exposes the coverage columns consumed by its score cache.
- Kept the repaired 180-day columns append-only for safe re-application when a partial view already exists.
- Added a positive-size `ResizeObserver` guard before mounting the two Supplier Sales charts, with a visible preparation state.

## Files changed
- Database/Migrations/029_AddSupplierDecisionWindowedViews.sql
- Api.Tests/SupplierDecisionSchemaSqlTests.cs
- Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx
- Klijent/clientapp/src/pages/SupplierSalesStatsPage.css
- .ai/runs/2026-09-02-direct-decision-pulse-migration-chart-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SupplierDecisionSchemaSqlTests --no-restore` -> pass (25/25)
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SupplierDecisionSchemaSqlTests --no-restore --no-build` -> pass (25/25)
- `npm run test:run -- src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx --reporter=dot` -> pass (4/4)
- `npm run check:analytics-guardrails` -> pass
- `npm run build` -> pass
- `dotnet build Api/Api.csproj --no-restore` -> pass (0 errors; existing analyzer warnings)
- Render `GET /api/analytics/decision-pulse?dataScope=all` -> HTTP 200; route mapping confirmed
- Render supplier decision summary before final corrected deploy -> HTTP 200 with `MISSING_TABLE`; this confirmed the migration failure was still active in the old process
- Render `/api/runtime/version` before final corrected deploy -> old deployed commit observed; latest pushed code was not yet active at audit time
- `git diff --check` -> pass

## Validation not run
- Direct SQL inspection/re-run against Render Analytics -> not run - Render secret is not available in this session; the local development credential was rejected by the remote database.
- Browser visual smoke -> not run - code/build/test proof was available, but no authenticated browser session was used.

## Documentation impact
- This run log records the implementation, production observations, validation and the external Render deployment limitation.

## What was missed
- Final post-deploy confirmation that the Render Analytics database contains both `mv_supplier_decision_score_cache_90d` and `mv_supplier_decision_score_cache_180d` remains pending until Render activates the latest `main` deploy.

## Risks
- Render was still serving the previous commit during the last smoke; `MISSING_TABLE` cannot be declared resolved until `/api/runtime/version` reports the latest pushed commit and the supplier summary no longer returns that code.

## Next
- Wait for Render to activate the latest `main` deploy, then repeat the supplier summary and DecisionPulse smoke checks.
