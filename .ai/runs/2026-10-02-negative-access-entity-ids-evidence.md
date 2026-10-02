Task ID: negative-access-entity-ids (audit NID-0..NID-6, 2026-10-02)
Queue: direct-user-request
Date: 2026-10-02
Agent/tool: Cursor agent
Delivery target: main
Working branch / PR: main (direct)
Main commit SHA: 0360442dfa66ef4719a2f14718a573a0a5244dc2
Main verification: passed - origin/main contains 0360442dfa66ef4719a2f14718a573a0a5244dc2 (rebased onto docs-only fc2d461d)
CI at delivery: Analytics Quality Gates 37023569177 in_progress; Analytics Tests & Data Integrity 37023569561 in_progress
Evidence state: synchronized

## What was done
- Access "Random AutoNumber" IDs are negative for suppliers, shoe types and stores (e.g. Ž.Cipela -2004188974, store -598733481). Extended the Daily Sales supplier fix (1ed9c9e) to every analytics surface.
- Frontend: new `src/validation/entityId.ts` (`entityId`, `nullableEntityId`, `parseEntityIdParam`, `formatEntityFallbackLabel`). All identifier fields in `analyticsResponseSchemas.ts` (tipObuceId, storeId, sezonaId, season id, pre-nivelacija seasonId/footwearTypeId/storeId/filter option id, article ids, inventory store comparison storeId, vendorId/dobavljacId) now accept any Int32; counts/quantities stay non-negative. Removed the duplicate `supplierIdentifier` helper.
- Frontend URL state: Color, Shoe Type, Pre/Post nivelacija, Pre-nivelacija, Product Decision Center, Inventory (storeId/supplierId/compareStores) and `useSupplierCanonicalState` keep negative IDs instead of silently dropping the filter. `SupplierDecisionReportActions` no longer strips the minus sign.
- Frontend labels: raw `#id` fallbacks in Povraćaj, Supplier Decision Hub, DemandForecastPanel, RebalancingTable, inventoryUtils, Inventory rebalance scope use `formatEntityFallbackLabel` ("Nepoznat objekat (ID …)").
- Backend: new `Api/Services/EntityIdentity.cs` (IsAssigned / FallbackLabel). Pre-nivelacija store-name lookup, store/supplier/season/footwear facets and `ResolveStoreName` use `HasValue`; cache key bumped `pre-nivelacija-priority:v9 -> v10`.
- Backend: Access import fast analytics sync tracks negative supplier/season/shoe-type/store IDs. Added admin endpoint `POST /api/access-import/analytics/repair-negative-id-dimensions` (idempotent upsert of negative Dobavljaci/Sezone/TipoviObuce into analytics dims; no deletes; stores excluded because their upsert would overwrite names with a placeholder and they are already projected from sales).
- Backend: data-quality intake counts suppliers / missing supplier names for any non-null, non-zero supplier ID (0 stays "missing", consistent with `missingSupplierCount`). Inventory dataset / store comparison / cached store comparison and their cache keys keep negative store IDs. Inventory store/supplier fallback labels use EntityIdentity.
- Supplier Decision Hub `NormalizeSupplierName` shows `Dobavljač #id` for negative IDs (0 = NULL from GetInt32).
- `InventoryActionSourceKey` keeps negative store IDs distinct from `store=all`.
- Guard test `NegativeEntityIdGuardTests` fails on any sign-based entity-ID check in Api/Application/Infrastructure/Domain.

## Files changed
- Klijent/clientapp/src/validation/entityId.ts (new), Klijent/clientapp/src/validation/__tests__/entityId.spec.ts (new)
- Klijent/clientapp/src/validation/analyticsResponseSchemas.ts, Klijent/clientapp/src/validation/__tests__/analyticsResponseSchemas.spec.ts
- Klijent/clientapp/src/pages/{ColorSalesStatsPage,ShoeTypeSalesStatsPage,ProdajaPrePostNivelacijePage,PreNivelacijaPriorityPage,ProductDecisionCenterPage,InventoryPage,SupplierDecisionHubPage,PovracajPage}.tsx, Klijent/clientapp/src/pages/useSupplierCanonicalState.ts
- Klijent/clientapp/src/components/analytics/SupplierDecisionReportActions.tsx
- Klijent/clientapp/src/components/inventory/{DemandForecastPanel,RebalancingTable}.tsx, Klijent/clientapp/src/components/inventory/inventoryUtils.ts
- Klijent/clientapp/src/pages/__tests__/{PreNivelacijaPriorityPage,SupplierConsolidatedPage}.spec.tsx
- Klijent/clientapp/scripts/known-guardrail-baseline.json (9 entries shifted by added import lines; no new debt)
- Api/Services/EntityIdentity.cs (new), Api/Services/AccessImportService.cs, Api/Models/AccessImportModels.cs, Api/Endpoints/AccessImportEndpoints.cs
- Api/Endpoints/{PreNivelacijaPriorityEndpoints,DataQualityEndpoints,InventoryEndpoints,CachedAnalyticsEndpoints,SupplierDecisionHubEndpoints}.cs
- Infrastructure/Services/Caching/IAnalyticsCacheService.cs, Application/Inventory/Models/InventoryActionSourceKey.cs
- Api.Tests/NegativeEntityIdGuardTests.cs (new), Api.Tests/{AnalyticsScreenCacheKeyContractTests,InventoryActionSourceKeyTests,AccessImportAdminAuthorizationTests,AccessImportRunEndpointTests,DemoEnvironmentVerificationEndpointTests}.cs

## Validation run
- `npx tsc -b` (Klijent/clientapp) -> pass
- `npm run check:analytics-guardrails` -> pass (baseline-only; 2 pre-existing stale entries reported as removed: AnalyticsActionsPage:301, ArtikliListPage:189)
- `npx vitest run src/validation src/components/analytics/__tests__/SupplierDecisionReportActions.spec.tsx src/components/inventory <affected pages> src/pages/__tests__` -> 804/807 pass; 2 failures were tests encoding the old "negative ID = invalid" rule (updated), 1 was a 5 s timeout under load
- re-run of PreNivelacijaPriorityPage / SupplierConsolidatedPage / ProductDecisionCenterPage.confidence specs -> pass (92/92)
- `dotnet test Api.Tests --filter <NegativeEntityIdGuard|PreNivelacija|InventoryActionSourceKey|AnalyticsScreenCacheKeyContract|AccessImport|SupplierDecision|DailySalesStats|DataQuality|Inventory|DemoEnvironmentVerification>` -> 582 pass, 1 skip, 2 fail with EF `ManyServiceProvidersCreatedWarning` (harness); both pass in isolation

## Validation not run
- Full `dotnet test` / full vitest suite -> not run; focused owners covered above.
- Production SQL from the audit (§4) -> not run; no production DB access from this workspace.
- Repair endpoint against a real analytics DB -> not run; needs admin key + production analytics DB.

## Documentation impact
- No owner doc changed; the audit `NEGATIVE_ID_AUDIT_PROMPTS_2026-10-02.md` is the source of findings. This run log records the contract (any Int32 is a valid entity ID; unknown = NULL or no master row).

## What was missed
- N14 sentinels (`COALESCE(vendor_id, -1)` in AllEndpoints/NivelacijaRepairService, `COALESCE("IDObjekat", -1)`, `store_id = 0` in 025 snapshot) not changed; P3, needs the §4 sentinel-collision SQL first.
- N16 article-ID `> 0` checks (inventoryUtils.ts:60, ProductDecisionCenterPage productId > 0) kept: article IDs are sequential positives and 0 is used as "missing".
- DailySalesStatsService keeps its existing `Nepoznat dobavljač #id` label (golden contract); EntityIdentity uses `(ID …)` wording to match the frontend.
- Render tasks (sync worker / SourceSyncCheckpoints check, `DailySales__TimeZoneId=Europe/Belgrade`) not done: no Render or production DB access in this workspace.

## Risks
- `InventoryActionSourceKey` now produces `store=<negative id>` instead of `store=all` for negative stores; previously persisted action items for those stores keep their old key and may not dedupe against new ones.
- Fast sync now upserts StoresDim for negative store IDs coming from articles; name falls back to `Objekat {id}` when the batch lacks store data (same behaviour positive IDs already had).
- Pre-nivelacija cache is invalidated by the v10 key bump (one cold load).

## Next
- Run the audit §4 read-only SQL on prod (Trendplus + analytics) to confirm Sezone negatives and sentinel collisions.
- After deploy: call `POST /api/access-import/analytics/repair-negative-id-dimensions` with the admin key once and compare §4 analytics counts.
- Render: check sync worker + `SourceSyncCheckpoints`, set `DailySales__TimeZoneId=Europe/Belgrade` and restart.
