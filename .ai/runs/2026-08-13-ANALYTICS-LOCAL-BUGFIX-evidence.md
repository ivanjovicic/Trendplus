Task ID: ANALYTICS-LOCAL-BUGFIX
Queue: none (direct repository request)
Date: 2026-08-13
Agent/tool: Cursor
Model: Cursor Grok 4.6
Delivery target: none
Main commit SHA: pending
Main verification: skipped; user did not request commit or push

## What was done
- Supplier Decision Hub no longer treats HTTP 200 error meta as success; fake-zero KPI render is blocked.
- Inventory page clears failed primary slices so a stale balance cannot remain on screen after a refresh/load error.
- Pre-nivelacija sales/markdown query failures now return Error meta and do not score candidates as if units/markdown were zero.
- Cached inventory status fallback from Artikli is marked as a warning, not Success.
- Color sales no longer invents decision score 0 or reliability from margin coverage.
- Supplier decision report now matches the UTF-8 header "Korišćen fallback" and stringifies metadata scalars safely.

## Files changed
- Klijent/clientapp/src/services/supplierDecisionHubApi.ts
- Klijent/clientapp/src/services/__tests__/supplierDecisionHubApi.spec.ts
- Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx
- Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx
- Klijent/clientapp/src/pages/InventoryPage.tsx
- Klijent/clientapp/src/pages/__tests__/InventoryPage.partialFailure.spec.tsx
- Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx
- Klijent/clientapp/src/pages/__tests__/ColorSalesStatsPage.spec.tsx
- Klijent/clientapp/src/components/analytics/SupplierDecisionReport.tsx
- Api/Endpoints/PreNivelacijaPriorityEndpoints.cs
- Api/Endpoints/CachedAnalyticsEndpoints.cs
- Application/Analytics/Queries/GetInventoryStatus/GetInventoryStatusQuery.cs
- Api.Tests/PreNivelacijaQueryFailureMetaTests.cs

## Validation run
- git diff --check -> pass (workspace-wide; whitespace warnings only)
- dotnet test Api.Tests --filter FullyQualifiedName~PreNivelacijaQueryFailureMetaTests -> pass (2/2)
- npm run test -- --run supplierDecisionHubApi.spec.ts SupplierDecisionHubPage.spec.tsx ColorSalesStatsPage.spec.tsx InventoryPage.partialFailure.spec.tsx SupplierDecisionReport.spec.tsx ColorSalesStatsPage.premium.spec.tsx -> pass
- npm run check:analytics-guardrails -> pass

## Validation not run
- full Api.Tests suite - focused analytics proof only
- npm run build - typecheck already passed via guardrails
- live endpoint smoke against a missing analytics relation

## What was missed
- Cached `/api/analytics/cached/sales/daily` still falls back from SalesFacts to operational tables without a meta/warning channel because the response is a bare array.
- Dashboard bootstrap inventory can still show operational Artikli counts without a user-facing warning even though `UsedOperationalFallback` is now on the DTO.

## Risks
- Pre-nivelacija now fail-closes when markdown history cannot be read, even if sales succeeded.
- Inventory refresh now shows a blocking error instead of keeping a previous balance after a failed reload.

## Next
- Add warning meta/wrap for cached daily sales fallback
- Surface inventory operational-fallback on dashboard bootstrap
