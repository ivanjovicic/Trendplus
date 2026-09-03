Task ID: analytics-followup-audit
Queue: direct-user-request
Date: 2026-09-03
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: 4c8844b9a9c7e97af09d397955a6a98430c1ff91
Main verification: pending - verify after push
Evidence state: pending

## What was done
- Re-verified the earlier incomplete-evidence analytics changes and continued the audit across supplier scorecard/report, inventory, pre/post nivelacija and Executive Decision Board surfaces.
- Stopped supplier scorecard query generation time from being reported as a successful refresh timestamp.
- Made all-history supplier reports use the observed data period instead of synthetic default bounds.
- Prevented inventory ratios, health score and inventory value from becoming valid-looking zero/100 values when the denominator or source is unavailable.
- Prevented pre/post concentration KPI/table/export/tooltips from presenting zero when there is no valid change denominator; clarified zero-baseline growth as “Bez baze”.
- Added regression coverage for freshness lineage, all-history period semantics, empty inventory denominators and Decision Board timestamps.

## Files changed
- Api/Endpoints/SupplierDecisionHubEndpoints.cs
- Api/Endpoints/DecisionBoardEndpoints.cs
- Api.Tests/AnalyticsSalesReadinessRegressionTests.cs
- Api.Tests/DecisionBoardEndpointsTests.cs
- Klijent/clientapp/src/pages/InventoryPage.tsx
- Klijent/clientapp/src/components/inventory/InventoryKPICards.tsx
- Klijent/clientapp/src/components/inventory/InventoryPriorityPanels.tsx
- Klijent/clientapp/src/components/inventory/inventoryUtils.ts
- Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx
- Klijent/clientapp/src/pages/__tests__/analyticsIndicatorRegression.spec.ts

## Validation run
- Live read-only checks of refresh-status, intake-report, data-quality health, dashboard bootstrap and supplier report -> pass; confirmed operational freshness/history and period discrepancies for follow-up.
- npm run test:run -- src/pages/__tests__/analyticsIndicatorRegression.spec.ts -> pass, 6 tests.
- npm run test:run -- InventoryPage.fakeZeroValue.spec.ts, InventoryPage.partialFailure.spec.tsx, InventoryPage.freshnessLineage.spec.tsx, SupplierDecisionHubPage.percentExport.spec.ts, SupplierDecisionHubPage.spec.tsx, PilotReadinessPage.edgeCases.spec.ts -> pass, 27 tests.
- npm run check:analytics-guardrails -> pass (encoding, guardrails, typecheck).
- npm run build -> pass; Vite completed with existing chunk-size warnings.
- dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter AnalyticsSalesReadinessRegressionTests|DecisionBoardEndpointsTests|SupplierDecisionHubContractTests -> pass, 61 tests.
- git diff --check -> pass.
- One initial frontend build attempt failed on a temporary test type introduced during this run; corrected and the subsequent typecheck/build passed.
- One initial backend command was run from the client directory with an invalid relative project path; corrected and the subsequent backend validation passed.

## Validation not run
- Full frontend and backend suites -> not run; focused analytics coverage plus build was sufficient for this scoped follow-up.
- Browser/Vercel deployed smoke test -> not run; requires deployed runtime/session evidence.
- Applying Render migrations or changing production data/workers -> not run; outside safe local proof and requires operational authorization.

## Documentation impact
- No owner documentation required a change; durable evidence is recorded here.

## What was missed
- Render still reports workers disabled, in-memory cache and no refresh history; this remains an operational/data-pipeline follow-up.
- Endpoint-level period alignment across dashboard bootstrap, intake health and supplier report needs a separate contract decision before broad normalization.

## Risks
- Live analytics remains unsuitable for trusted recommendations until refresh lineage and source history are restored; the UI now exposes unknown/insufficient state instead of hiding it.
- Existing compiler and bundle-size warnings remain outside this scope.

## Next
- Deploy/verify this change on main, then run a bounded live smoke check and separately repair the Render refresh/history pipeline and period contract.
