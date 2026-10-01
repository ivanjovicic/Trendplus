Task ID: daily-sales-negative-supplier-id
Queue: direct-user-request
Date: 2026-10-01
Agent/tool: Cursor Cloud Agent
Delivery target: main
Working branch / PR: cursor/rq-daily-sales-negative-supplier-id-51d0 / pending
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Frontend Zod now accepts any finite integer (including negative Access AutoNumber IDs) for supplier identifiers on Daily Sales, preNivelacija, and inventory insight schemas.
- Daily Sales supplier headers expose `unknownReason` and `attributionBasis`; dangling IDs keep SupplierId, set IsUnknown=true, and use a non-empty display name.
- Analytics data-quality health no longer treats `IDDobavljac <= 0` as unknown; unknown = null or missing Dobavljaci row.
- Daily Sales cache key bumped to `daily:v2:`.
- DailySalesStatsPage renders an "Nepoznat" badge/tooltip for unknown suppliers.
- Focused frontend schema + page regression tests added/updated.

## Files changed
- `Klijent/clientapp/src/validation/analyticsResponseSchemas.ts`
- `Klijent/clientapp/src/validation/__tests__/analyticsResponseSchemas.spec.ts`
- `Klijent/clientapp/src/services/dailySalesStatsApi.ts`
- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx`
- `Api/Models/DailySalesStatsDto.cs`
- `Api/Services/DailySalesStatsService.cs`
- `Api.Tests/DailySalesStatsServiceTests.cs`
- `Api.Tests/AnalyticsDataQualityHealthServiceTests.cs`
- `Api.Tests/AnalyticsDataQualityConsistencyTests.cs`
- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`
- `.ai/runs/2026-10-01-daily-sales-negative-supplier-id-evidence.md`

## Validation run
- `cd Klijent/clientapp && npm run typecheck` → pass
- `cd Klijent/clientapp && npm run test -- --run src/validation/__tests__/analyticsResponseSchemas.spec.ts src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx` → pass (41/41)

## Validation not run
- `dotnet test` for DailySalesStatsServiceTests / AnalyticsDataQualityHealthServiceTests / AnalyticsDataQualityConsistencyTests → not run (`dotnet` not available in this environment)
- Prod SQL checks from the audit note → not run (read-only Neon access not part of this delivery)
- Shift timezone / Access sync ops fixes → out of scope for this change

## Documentation impact
- Run evidence only. No queue prompt owner docs updated (direct user request). Separate ops notes (stale Access sync, `DailySales__TimeZoneId`) remain outside this patch.

## What was missed
- Backend unit tests not executed locally without .NET SDK.
- No production verification of `/api/analytics/daily-sales?fromDate=2026-07-06&toDate=2026-08-05&topN=15` after deploy.

## Risks
- CI must run backend tests; local proof is frontend-only.
- Cached daily-sales responses invalidate via `daily:v2:` prefix; confirm heavy-analytics cache consumers pick up the new key after deploy.
- Stale sales data (maxAvailableDate 2026-08-05) and UTC shift timezone remain separate production issues.

## Next
- Deliver to `main` and verify origin/main contains implementation SHA.
- Ops follow-up (not this PR): Render worker/SourceSyncCheckpoints freshness; set `DailySales__TimeZoneId=Europe/Belgrade`.
