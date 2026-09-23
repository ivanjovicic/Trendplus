Task ID: supplier-insufficient-data-reason
Queue: direct-user-request
Date: 2026-09-23
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: aaeac55e8f947542d2c4c627a0409d433d5ac5f8
Main verification: passed - origin/main contains aaeac55e8f947542d2c4c627a0409d433d5ac5f8
Evidence state: synchronized

## What was done
- Traced the supplier rows showing `Nedovoljno podataka` to a frontend fail-closed status override that discarded the backend reason, while the backend did not expose the missing comparable pre/post signal as a reason code.
- Added a shared backend comparable-signal gate that emits `insufficient_data`, `missing_comparable_signal`, Serbian summary text and `RecommendationAllowed = false`.
- Applied the contract consistently to supplier, shoe-type, color and supplier-footwear endpoint/detail projections.
- Made the reason visible in the supplier, supplier decision hub, ranking, shoe-type, color, footwear and analytics detail surfaces; preserved the existing `Akcija blokirana` label where tests and users depend on it.
- Versioned affected analytics cache keys so old response semantics cannot remain served from cache.

## Files changed
- Api.Tests/AnalyticsDecisionRecommendationEngineTests.cs
- Api.Tests/AnalyticsScreenCacheKeyContractTests.cs
- Api/Endpoints/AllEndpoints.cs
- Api/Services/AnalyticsDetailReadService.cs
- Application/Analytics/AnalyticsDecisionRecommendationEngine.cs
- Infrastructure/Services/Caching/IAnalyticsCacheService.cs
- Klijent/clientapp/src/components/analytics/AnalyticsDetailView.tsx
- Klijent/clientapp/src/components/supplierDecisionHub/SupplierDecisionTable.spec.tsx
- Klijent/clientapp/src/components/supplierDecisionHub/SupplierDecisionTable.tsx
- Klijent/clientapp/src/pages/ColorSalesStatsPage.css
- Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx
- Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.css
- Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx
- Klijent/clientapp/src/pages/SupplierDecisionHubPage.css
- Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx
- Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.css
- Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx
- Klijent/clientapp/src/pages/SupplierSalesStatsPage.css
- Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx
- Klijent/clientapp/src/services/shoeTypeSalesStatsApi.ts
- Klijent/clientapp/src/services/supplierSalesStatsApi.ts
- Klijent/clientapp/src/utils/canonicalRecommendationSemantics.ts
- Klijent/clientapp/src/validation/analyticsResponseSchemas.ts
- .ai/runs/2026-09-23-supplier-insufficient-data-reason-evidence.md

## Validation run
- `git diff --check` -> pass.
- `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsDecisionRecommendationEngineTests" --no-restore` -> pass, 13/13.
- `dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsScreenCacheKeyContractTests" --no-restore` -> pass, 17/17.
- `npm run test -- --run src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/SupplierDecisionHubPage.spec.tsx` -> pass, 33/33.
- Supplier shoe-type/color/footwear page suites -> pass after the compatibility-label correction; 60/60 page tests in the combined run.
- `npm run test -- --run src/components/supplierDecisionHub/SupplierDecisionTable.spec.tsx` -> pass, 7/7.
- `npm run build` -> pass; Vite completed successfully. Existing chunk-size warnings remain.
- `npm run check:encoding` -> pass as part of the analytics guardrail command.
- Main delivery -> pass; `git ls-remote origin refs/heads/main` equals the implementation SHA above.

## Validation not run
- Full .NET test suite -> not run; scoped focused proof was sufficient for the changed recommendation contract.
- Full frontend test suite -> not run; scoped analytics page/component tests and production build were run.
- Remote CI -> not inspected; repository policy does not require waiting for CI before direct-main delivery.

## Documentation impact
- No product/owner documentation required an update. This run log is the durable evidence artifact.

## What was missed
- `npm run check:analytics-guardrails` did not pass in the shared dirty worktree: it reported six new line-based violations and removed baseline entries in existing inventory/SupplierDecisionHub/Color surfaces. The reported inventory changes were pre-existing user work and were not included in the commit; the focused typecheck/build/tests passed.

## Risks
- Guardrail baseline synchronization remains a follow-up because the workspace contains unrelated inventory edits that alter the script's line-based baseline.
- Existing compiler/analyzer and frontend chunk-size warnings remain outside this scope.

## Next
- Re-run the analytics guardrail against a clean worktree or update its baseline in the owning inventory task.
