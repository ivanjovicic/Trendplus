Task ID: supplier-margin-quality-indicators
Queue: direct-user-request
Date: 2026-09-23
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: 711e588f0ce5a4555d1033df387f28b6f16a1cb6
Main verification: passed - origin/main contains 711e588f0ce5a4555d1033df387f28b6f16a1cb6
Evidence state: synchronized

## What was done

- Fixed the supplier analytics response so each supplier row exposes the backend margin quality tier, label, short label and tooltip used by the table and detail drawer.
- Corrected supplier coverage semantics so `revenueWithCost` and `marginDataCoveragePct` include all covered cost sources, while direct, fallback, snapshot and uncovered percentages remain separate.
- Centralized uncovered-revenue calculation and prevented rounding residues from rendering as negative zero.
- Applied the same uncovered-revenue protection to footwear rows, footwear totals and shared margin classification.
- Bumped supplier and footwear analytics cache-key versions so old payloads without the quality fields cannot be served after deployment.
- Added backend and UI regression coverage for explicit quality fields, fallback-vs-uncovered semantics and the false recommendation caveat.

## Files changed

- Api/Endpoints/AllEndpoints.cs
- Application/Analytics/AnalyticsMarginPolicy.cs
- Infrastructure/Services/Caching/IAnalyticsCacheService.cs
- Api.Tests/AnalyticsMarginPolicyTests.cs
- Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs
- Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx

## Validation run

- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AnalyticsMarginPolicyTests --no-restore` -> pass (14 tests).
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AnalyticsSupplierSalesIntegrationTests --no-build` -> pass (15 tests; database integration is environment-gated, in-memory coverage executed).
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AnalyticsSupplierSalesIntegrationTests --no-restore` -> pass (15 tests; compile and test filter verified).
- `npm run test -- --run src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx` -> pass (20 tests).
- `npm run check:analytics-guardrails` -> pass (encoding, guardrail self-test, analytics guardrails and typecheck).
- `git diff --check` -> pass.

## Validation not run

- Real external-database integration fixture -> not run because `TRENDPLUS_RUN_INTEGRATION_TESTS` was not enabled.
- Frontend production build -> not run; no production frontend module, route or build configuration changed, and the focused UI suite plus typecheck passed.

## Documentation impact

- No product documentation changed; the correction is an implementation/contract fix covered by tests.
- `analytics-nivelacija` guidance was applied to keep direct, fallback and uncovered cost evidence distinct.

## What was missed

- No live tenant/browser verification was performed in this local run.
- Existing color signed-net semantics were intentionally left unchanged.

## Risks

- Existing repository-wide analyzer warnings remain outside this scope.
- The visible recommendation may still remain `Nedovoljno podataka` when comparable pre/post evidence is genuinely absent; that is an intentional fail-closed decision state, separate from margin-cost quality.

## Next

- Deploy/restart the API and reload analytics pages; the cache-key version bump forces fresh supplier and footwear payloads.
