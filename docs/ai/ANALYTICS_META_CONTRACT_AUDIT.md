# Analytics Meta Contract Audit

Last updated: 2026-05-26
Scope: cached analytics families, supplier decision hub, inventory, data-quality, and nivelacija flows.

## Coverage Table

| Endpoint family | Success meta | Empty meta | Warning/partial meta | Error meta | CorrelationId | Frontend meta-aware | Fake-zero guarded | Notes |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Cached dashboard/bootstrap | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Proven via code audit. All branches in `/dashboard/bootstrap` covered: `BuildSuccessMeta(isPartial=true, warningCode="ANALYTICS_PARTIAL_DATA")` when errors present; error branches for NpgsqlException/Timeout/Cancellation; `result.Meta.CorrelationId = ResolveCorrelationId(httpContext)` on all paths. File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`. Priority: P0. |
| Cached products/decision-center | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Proven via code audit. Empty branch (`articles.Count == 0`) returns `EmptyReason="no_rows_for_period"`; `sortedRows.Count == 0` now returns `EmptyReason="no_rows_for_period"` (fixed 2026-05-26); error branches use `BuildErrorMeta(...)`; correlationId on all branches. File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`. Priority: P0. |
| Supplier decision hub summary/ranking | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Gap: none in core contract; keep verifying summary/ranking/report tabs and report export parity. File: `Klijent/clientapp/src/services/supplierDecisionHubApi.ts`. Priority: P1. |
| Inventory family | Yes | Yes | Yes | Yes | Yes | Yes | Yes | All gaps closed 2026-05-26. `/insights` (non-cached + cached) wrapped in try-catch with error meta; `/cached/inventory/status` now emits meta with correlationId and error handling; `/cached/inventory/insights` wrapped in try-catch; `/cached/inventory/balance` and `/cached/inventory/list` were already covered. Signal routes (`forecast`, `size-curve`, `rebalance-suggestions`, `alerts`) still use legacy warning fields — tracked separately as lower-priority. Files: `Api/Endpoints/InventoryEndpoints.cs`, `Api/Endpoints/CachedAnalyticsEndpoints.cs`. Priority: P1. |
| Data-quality family | Yes | Yes | Yes | Yes | Yes | Yes | Yes | All 5 routes now wrapped in try-catch (fixed 2026-05-26): `/health`, `/list`, `/top-offenders`, `/trend`, `/intake-report`. All already had success meta and correlationId. Error branches return safe DTO with `AnalyticsResponseMetaFactory.Error(...)`. File: `Api/Endpoints/DataQualityEndpoints.cs`. Priority: P1. |
| Pre/post nivelacija | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Proven via code audit. `BuildVendorSalesNivelacijaMeta(response, correlationId)` covers empty/warning/success; `CreateVendorSalesNivelacijaFallbackResponse(...)` covers PostgresException and general Exception; `meta.CorrelationId = correlationId` on all branches. File: `Api/Endpoints/AllEndpoints.cs`. Priority: P1. |
| Pre-nivelacija prioriteti | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Proven via code audit. `BuildResponse(...)`: `TotalCandidates==0` → `Factory.Empty("no_data_in_period",...)`; error catches use `BuildErrorResponse(code, message, correlationId)`; correlationId propagated everywhere. File: `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs`. Priority: P2. |

## Contract Rule

- Backend greška nikad ne sme da izgleda kao uspeh sa `0 RSD`, `0 kom`, ili `0%`.
- `empty dataset` nije error: vraća se `success=true` uz odgovarajući `emptyReason` ili `insufficient_data` signal.
- `warning/partial` mora biti deo ugovora, ne UI improvizacija.
- `correlationId` treba da bude prisutan na error i fallback granama gde god je to moguće.

## Frontend Rule

- Frontend treba da koristi shared helper i shared state komponente za tumačenje meta ugovora.
- Page fajlovi ne treba da računaju finalnu preporuku, confidence ili reliability lokalno.
- `AnalyticsTrustHeader`, `AnalyticsEmptyState`, i `AnalyticsErrorState` treba da budu primarni prikaz za trust, empty i error states.

## Regression Checklist

1. `dotnet build`
2. Relevantni `dotnet test` paketi za dirnute endpoint porodice
3. `npm run check:analytics-guardrails` iz `Klijent/clientapp`
4. `npm run build` iz `Klijent/clientapp`
5. Manual smoke:
   - `/analytics`
   - `/analytics/products`
   - `/analytics/supplier`
   - `/analytics/inventory`
   - `/analytics/data-quality`
   - `/analytics/pre-nivelacija-prioriteti`
   - `/analytics/prodaja-pre-post-nivelacije`

## Known Gaps

- `InventoryPage` koristi centralni meta helper za error/empty/warning/trust state, ali signal paneli (`forecast`, `size-curve`, `rebalance`, `alerts`) jos nisu kompletno prebaceni na isti meta warning/empty ugovor.
- Neki cached endpointi imaju meta na success/error granama, ali nije svaka fallback grana eksplicitno potvrđena.
- Ovaj audit pokriva core analytics porodice; endpointi van tog skupa nisu uključeni.

## Manual Smoke List

- Otvori `/analytics` i proveri da li trust header prikazuje period, fresh/stale status i poruku o pouzdanosti.
- Otvori `/analytics/products` sa praznim i sa uskim filterima i proveri empty/warning state.
- Otvori `/analytics/supplier` i potvrdi da helper tumači summary/ranking bez fake-zero prikaza.
- Otvori `/analytics/inventory` i proveri da error state ne prikazuje KPI nule na grešci.
- Otvori `/analytics/data-quality` i proveri warning banner i empty state reason.
- Otvori pre/post i pre-nivelacija stranice i proveri da partial/fallback ugovori ne izgledaju kao finalna preporuka.

