# Analytics Meta Contract Audit

Last updated: 2026-05-24
Scope: cached analytics families, supplier decision hub, inventory, data-quality, and nivelacija flows.

## Coverage Table

| Endpoint family | Success meta | Empty meta | Warning/partial meta | Error meta | CorrelationId | Frontend meta-aware | Fake-zero guarded | Notes |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Cached dashboard/bootstrap | Yes | Partial | Partial | Yes | Partial | Yes | Yes | Gap: prove empty/warning/correlation on every bootstrap branch. File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`. Priority: P0. |
| Cached products/decision-center | Yes | Partial | Partial | Yes | Partial | Yes | Yes | Gap: standardize empty/no-data and fallback/insufficient-data metadata on all cached decision-center paths. File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`. Priority: P0. |
| Supplier decision hub summary/ranking | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Gap: none in core contract; keep verifying summary/ranking/report tabs and report export parity. File: `Klijent/clientapp/src/services/supplierDecisionHubApi.ts`. Priority: P1. |
| Inventory family | Yes | Partial | Partial | Yes | Partial | Partial | Yes | Gap: not every inventory route is guaranteed to emit the same meta shape yet. File: `Api/Endpoints/InventoryEndpoints.cs` and `Klijent/clientapp/src/pages/InventoryPage.tsx`. Priority: P1. |
| Data-quality family | Yes | Yes | Partial | Yes | Partial | Yes | Yes | Gap: confirm warning/partial and correlation consistency across all data-quality routes and UI states. File: `Api/Endpoints/DataQualityEndpoints.cs` and `Klijent/clientapp/src/pages/DataQualityPage.tsx`. Priority: P1. |
| Pre/post nivelacija | Yes | Partial | Partial | Partial | Partial | Yes | Yes | Gap: full meta contract still needs hard verification on every response path, including error and fallback branches. File: `Api/Endpoints/AllEndpoints.cs` and `Api/Models/VendorSalesNivelacijaModels.cs`. Priority: P1. |
| Pre-nivelacija prioriteti | Yes | Partial | Partial | Yes | Yes | Yes | Yes | Gap: confirm empty/no-data and partial semantics for edge filter windows, not only error branches. File: `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs` and `Klijent/clientapp/src/services/preNivelacijaApi.ts`. Priority: P2. |

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

- `InventoryPage` je još delimično legacy na page sloju, iako su centralni analytics servisi guardovani.
- Neki cached endpointi imaju meta na success/error granama, ali nije svaka fallback grana eksplicitno potvrđena.
- Ovaj audit pokriva core analytics porodice; endpointi van tog skupa nisu uključeni.

## Manual Smoke List

- Otvori `/analytics` i proveri da li trust header prikazuje period, fresh/stale status i poruku o pouzdanosti.
- Otvori `/analytics/products` sa praznim i sa uskim filterima i proveri empty/warning state.
- Otvori `/analytics/supplier` i potvrdi da helper tumači summary/ranking bez fake-zero prikaza.
- Otvori `/analytics/inventory` i proveri da error state ne prikazuje KPI nule na grešci.
- Otvori `/analytics/data-quality` i proveri warning banner i empty state reason.
- Otvori pre/post i pre-nivelacija stranice i proveri da partial/fallback ugovori ne izgledaju kao finalna preporuka.

