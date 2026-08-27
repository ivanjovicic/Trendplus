# Run evidence — last React/C# commit review

- Task: direct user request — review and repair recent React and C# commits.
- Queue: direct-user-request
- Delivery target: `main`
- Date: 2026-08-27

## Interpreted outcome and owner

Review the latest analytics runtime commits, verify the nearest React and .NET proofs, and repair confirmed trust/scope regressions in the owning analytics endpoint/UI surfaces.

## Findings and changes

- Scoped dashboard fallback SQL still referenced `prodaja_zaglavlje` as `p."DataOrigin"`; PostgreSQL bootstraps the column as `data_origin`. Updated velocity, trend, Pareto and top-product period queries to use `p."data_origin"`.
- The Shoe Type React trust header showed requested dates instead of backend-effective dates and dropped the legacy `generatedAt` timestamp when `meta` was absent. It now uses response dates and generated timestamp fallback.
- Color, Shoe Type and Supplier trust headers now preserve `lastRefreshAt` from `meta.lastRefreshAtUtc`, `meta.generatedAtUtc`, or the response `generatedAt` for backward-compatible payloads.
- Added a Postgres regression test covering scoped advanced dashboard velocity/Pareto fallback execution and a React regression assertion for effective period/timestamp forwarding.

## Files changed

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api.Tests/LostSalesValidationScopePostgresIntegrationTests.cs`
- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx`

## Validation

- `dotnet test .\\Api.Tests\\Api.Tests.csproj --filter "FullyQualifiedName~LostSalesValidationScopePostgresIntegrationTests"` — passed, 2/2.
- `dotnet test .\\Api.Tests\\Api.Tests.csproj --filter "FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests|FullyQualifiedName~LostSalesValidationScopePostgresIntegrationTests|FullyQualifiedName~AnalyticsStatsTrustMetaTests|FullyQualifiedName~DailySalesStatsServiceTests|FullyQualifiedName~AnalyticsDecisionRecommendationEngineTests"` — passed, 36/36.
- Frontend targeted Vitest for five analytics pages — passed, 14/14.
- `npm run check:analytics-guardrails` — passed (encoding, guardrails, typecheck).
- `npm run build` — passed before the final equivalent React-only fallback assertions; targeted Vitest and guardrails passed after the final patch.
- `git diff --check` — passed before commit (only expected CRLF conversion warnings).

## Not completed / residual risks

- Existing unrelated dirty React/DataQuality files and untracked historical evidence were preserved.
- Full backend/frontend suites were not rerun; the focused proofs cover the changed paths.
- Existing compiler/analyzer warnings (including duplicate `Microsoft.Data.SqlClient` package references) remain outside this scoped repair.

## Delivery

- Commit: `e77af0ffe6ac1d7185d7c39bc0ec8f81fe590e3a` (`fix(analytics): repair scoped dashboard fallback trust`).
- `git branch --contains e77af0ffe6ac1d7185d7c39bc0ec8f81fe590e3a` — `main`.
- Current `HEAD` is `e77af0ffe6ac1d7185d7c39bc0ec8f81fe590e3a`; delivery is on the intended target branch.
