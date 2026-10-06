Task ID: direct-supplier-response-casing
Queue: direct-user-request
Date: 2026-10-06
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: e03145e9e650fe51399692cf269ab5d730e4a9e9
Main verification: passed - fresh `origin/main` resolves to e03145e9e650fe51399692cf269ab5d730e4a9e9 and contains the implementation
Evidence state: synchronized

## What was done
- Investigated the production supplier analytics response used by `/analytics/supplier`.
- Confirmed the cached backend payload serialized `AnalyticsResponseMetaDto` with CLR property names such as `Success`, while the frontend contract requires web JSON names such as `success`.
- Updated supplier cache serialization to use web JSON options and bumped the supplier cache-key version from `v7` to `v8` so malformed cached payloads are not replayed.
- Added regression assertions for metadata casing and updated cache-key test helpers/contracts.

## Files changed
- Api/Endpoints/AllEndpoints.cs
- Infrastructure/Services/Caching/IAnalyticsCacheService.cs
- Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs
- Api.Tests/AnalyticsScreenCacheKeyContractTests.cs
- Api.Tests/SupplierSalesStatsEndpointPostgresParityTests.cs

## Validation run
- Direct production API inspection of `supplier-sales-stats`: pass for reproducing the casing mismatch; response had `meta.Success` and frontend schema requires `meta.success`.
- `git diff --check`: pass.
- `dotnet build Api.Tests/Api.Tests.csproj --no-restore --tl:off --nologo`: pass; 0 errors, existing warnings only.
- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsScreenCacheKeyContractTests" --no-build --no-restore --logger "console;verbosity=minimal"`: pass; 21/21.
- Post-delivery direct Render API smoke: pass for reachability, but live response still exposes `meta.Success`; this is deployment evidence, not a code-test failure.

## Validation not run
- Live `AnalyticsSupplierSalesIntegrationTests.SupplierSalesStats_ReturnsValidJsonWithAllFields`: skipped because `TRENDPLUS_RUN_INTEGRATION_TESTS=true` and an integration database are not configured in this workspace.
- Browser visual smoke test: not run because no browser session was available; API contract and frontend schema were inspected directly.
- Render redeploy: not triggered; the repository has a manual fallback workflow and no explicit authorization was given to start a production deployment.

## Documentation impact
- No owner documentation required; this is a focused backend response-contract and cache invalidation fix.

## What was missed
- The code is on `main`, but the live Render service has not yet picked up the revision. Production source data is currently observed through 2026-08-05, so a current-period request can still legitimately render an insufficient-data/empty state after the format error is fixed.

## Risks
- The live integration regression remains unexecuted without the external test database.
- Existing production cache entries are intentionally bypassed by the `v8` key; new entries use web JSON casing.
- Until Render redeploys, the public endpoint remains on the old serialization behavior.

## Post-close routing recovery
- not applicable for direct-user-request

## Next
- Trigger the configured Render deployment for `main`, then verify the public API exposes `meta.success` and `/analytics/supplier` renders its established empty/insufficient-data state when the observed data horizon does not cover the requested period.
