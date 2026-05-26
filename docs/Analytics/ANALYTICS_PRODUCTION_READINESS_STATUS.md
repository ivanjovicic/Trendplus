# Analytics Production Readiness Status

Datum verifikacije: 2026-05-27
Repo: ivanjovicic/Trendplus
Osnovni checklist: docs/Analytics/ANALYTICS_PRODUCTION_READINESS_CHECKLIST.md
Routing/smoke standard: docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md

## Sažetak

Finalni sign-off pass potvrđuje da su prethodni P0 gapovi zatvoreni na code/test nivou:
- Backend test gate prolazi (`dotnet test Api.Tests/Api.Tests.csproj`).
- Core frontend analytics rute su mapirane u aktivnom router-u (`Klijent/clientapp/src/App.tsx`).
- App routing je vracen na lazy loading + Suspense, uz zadrzan route smoke gate za analytics rute (`src/__tests__/AppAnalyticsRoutes.spec.tsx`).
- Legacy/admin kompatibilne rute su vracene: `/analytics/product-decision-center`, `/analytics/data-quality/pilot-intake-report`, `/admin/configuration`, `/configuration`.
- Cache status canonical/legacy mismatch je zatvoren (podržane su obe rute: `/api/analytics/cache/status` i `/api/analytics/cached/cache/status`).

Manual browser smoke je izvršen u ovom pass-u i rezultat je FAIL.
Browser smoke status za ovaj pass: FAIL.

## Status tabela

| Oblast | Status | Dokaz | Gap | Prioritet |
|---|---|---|---|---|
| Build/test gates | FAIL (frontend gates) | `npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx` prolazi (`9/9`), ali `npm run check:analytics-guardrails` ne postoji i `npm run build` failuje sa TypeScript greškama. | Frontend gate set nije zatvoren u ovom pass-u; nema osnova za production sign-off. | P0 |
| Trust/data contract | PASS (code+tests) | No-fake-zero i meta kontrakti pokriveni testovima u `Api.Tests/AnalyticsResponseMetaContractTests.cs`, `Api.Tests/AnalyticsMetaContractTests.cs`, `Api.Tests/AnalyticsReportsContractTests.cs`. Durable report builder koristi `AnalyticsResponseMeta` i eksplicitne error/empty grane (`Api/Endpoints/SupplierDecisionHubEndpoints.cs`, `Api/Endpoints/DataQualityEndpoints.cs`). | Nema novog funkcionalnog gapa potvrđenog u ovoj verifikaciji. | P2 |
| Durable reports | FAIL (browser smoke) | Durable rute su mapirane i code-level route smoke prolazi, ali realan browser smoke je pokazao nestabilan loading/partial prikaz i nevalidiran stabilan report render nakon refresh-a. | Durable browser render nije dovoljno stabilan za pilot sign-off. | P0 |
| Cache/report invalidation | PASS (code+tests) | Cache status payload vraća `cacheMode`, `reportCacheVersion`, `lastReportCacheClearAtUtc` i ostala status polja iz istog shared handler-a za obe rute (`/api/analytics/cache/status` i `/api/analytics/cached/cache/status`) u `Api/Endpoints/CachedAnalyticsEndpoints.cs`. `CoreFamilies` invalidation i bump report verzije pokriveni (`Infrastructure/Services/Caching/AnalyticsCacheAdminService.cs`, `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`, `Api.Tests/AnalyticsCacheAdminServiceTests.cs`). Worker/import invalidacija koristi `CoreFamilies` (`Workers/NightlyAnalyticsRefreshWorker.cs`, `Workers/AnalyticsDataQualityHealthWorker.cs`, `Api/Services/AccessImportService.cs`). | Nema aktivnog rute gapa za cache status; frontend i dalje koristi legacy putanju zbog minimalnog rizika. | P2 |
| KPI methodology | PASS | Canonical registry i alias map postoje (`Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts`), plus testovi (`Klijent/clientapp/src/utils/__tests__/analyticsMetricDefinitions.spec.ts`). KPI explain i methodology panel su integrisani (`KpiExplainButton`, `MetricMethodologyPanel`) kroz core analytics stranice i report komponente. | Nema potvrđenog gapa u ovoj verifikaciji. | P2 |
| UX/copy | FAIL (browser smoke) | Tokom smoke-a nema mojibake u glavnim naslovima, ali više ruta ostaje u loading/partial stanju uz API fail tragove pa UX nije stabilan. | Potreban stabilan, predvidljiv prikaz core ruta bez zaglavljivanja u učitavanju. | P0 |
| Manual smoke routes | FAIL | Browser smoke je izvršen kroz direktno otvaranje + refresh za tražene rute. `/analytics` je PASS, ali više ruta je FAIL zbog loading/blank/partial nestabilnosti. | Manual smoke ne prolazi acceptance kriterijume za pilot sign-off. | P0 |
| Production blockers | FAIL (active blocker) | Aktivni blocker su FAIL rezultati browser smoke-a na core i durable rutama. | Dok browser smoke ne bude PASS, pilot status ostaje Not ready. | P0 |

## Komande i rezultati

Backend:
- `dotnet build`: PASS (build prolazi; prisutna upozorenja).
- `dotnet test`: izvršena komanda sa root-a; rezultat ne prikazuje izvršene testove (samo restore/build).
- `dotnet test Api.Tests/Api.Tests.csproj`: PASS (`Failed: 0, Passed: 448, Skipped: 0, Total: 448`).

Frontend:
- `cd Klijent/clientapp && npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx`: PASS (`9/9` testova).
- `cd Klijent/clientapp && npm run check:analytics-guardrails`: FAIL (missing script `check:analytics-guardrails`).
- `cd Klijent/clientapp && npm run build`: FAIL (13 TypeScript grešaka; npr. `DailySalesStatsPage.tsx` i `SupplierDecisionHubPage.tsx`).

Napomena:
- Route smoke je automated/code-level verifikacija i prolazi i sa lazy route resolution.
- Manual browser smoke je izvršen i rezultat je FAIL.

## Core rute verifikacija

| Ruta | Mapirana | Browser smoke | Status |
|---|---|---|---|
| `/analytics` | DA (`Klijent/clientapp/src/App.tsx`) | PASS | Stabilan direktan load + refresh, bez blank/crash/mojibake; trust info vidljiv. |
| `/analytics/products` | DA (`Klijent/clientapp/src/App.tsx`) | FAIL | U više pokušaja ruta ostaje u loading/partial stanju; render nije stabilan. |
| `/analytics/supplier` | DA (`Klijent/clientapp/src/App.tsx`) | FAIL | U delu prolaza detektovan blank/empty prikaz i nestabilan refresh render. |
| `/analytics/inventory` | DA (`Klijent/clientapp/src/App.tsx`) | FAIL | Ruta često ostaje na loading prikazu bez stabilnog finalnog sadržaja. |
| `/analytics/data-quality` | DA (`Klijent/clientapp/src/App.tsx`) | FAIL | Prisutni API fail tragovi i nestabilan loading-heavy prikaz tokom smoke-a. |
| `/analytics/actions` | DA (`Klijent/clientapp/src/App.tsx`) | FAIL | Nema crash-a, ali prikaz nije konzistentan i ostaje u loading/partial stanju. |
| `/analytics/supplier/report?fromDate=...&toDate=...` | DA (`Klijent/clientapp/src/App.tsx`) | FAIL | URL i refresh stabilni, ali durable report ostaje u loading/partial stanju. |
| `/analytics/reports/pilot-intake?fromDate=...&toDate=...` | DA (`Klijent/clientapp/src/App.tsx`) | FAIL | URL i refresh stabilni, ali report render nije stabilno kompletiran. |
| `/analytics/product-decision-center` | DA (redirect na `/analytics/products` u `Klijent/clientapp/src/App.tsx`) | NOT EXECUTED | NOT EXECUTED (code-level route mapping postoji) |
| `/analytics/data-quality/pilot-intake-report` | DA (`PilotIntakeReportPage` u `Klijent/clientapp/src/App.tsx`) | NOT EXECUTED | NOT EXECUTED (code-level route mapping postoji) |
| `/admin/configuration` | DA (`Klijent/clientapp/src/App.tsx`) | FAIL | API fail tragovi i povremeni loading-only prikaz tokom smoke-a. |
| `/configuration` | DA (redirect na `/admin/configuration` u `Klijent/clientapp/src/App.tsx`) | FAIL | Redirect radi, ali ciljni prikaz nije stabilan (loading/fail tragovi). |

## API verifikacija

- `/api/analytics/reports/supplier-decision`: postoji (`Api/Endpoints/AnalyticsReportsEndpoints.cs`).
- `/api/analytics/reports/pilot-intake`: postoji (`Api/Endpoints/AnalyticsReportsEndpoints.cs`).
- `/api/analytics/cache/status`: postoji kao canonical alias (`Api/Endpoints/CachedAnalyticsEndpoints.cs`).
- `/api/analytics/cached/cache/status`: postoji i dalje (legacy/frontend kompatibilnost) (`Api/Endpoints/CachedAnalyticsEndpoints.cs`, `Klijent/clientapp/src/services/analyticsApi.ts`).
- Refresh status endpoint: `/api/analytics/refresh-status` postoji (`Api/Endpoints/AnalyticsRefreshStatusEndpoints.cs`).

## Posebne provere

- Durable report URL posle browser refresh-a: PASS za URL stabilnost, ali FAIL za stabilan render sadržaja (ostaje loading/partial prikaz).
- Expired state je jasan: PASS (`SupplierDecisionReportPage` + test).
- Report export dugmad bez payload-a: PASS (akcije zavise od `payload`; u empty state flow-u prikazuje se recovery UX, ne export flow).
- Cache status prikazuje `cacheMode` i `reportCacheVersion` na canonical i legacy ruti: PASS (`CachedAnalyticsEndpoints`).
- Production in-memory warning: PASS (`CachedAnalyticsEndpoints`, `AnalyticsRefreshStatusService`, `Program`).
- No fake-zero u error granama gde postoje testovi: PASS (više contract/regression testova u `Api.Tests`).

## Finalni zaključak

Status: Not ready.

Known limitations:
- Manual browser smoke je izvršen, ali više core i durable ruta je FAIL zbog nestabilnog loading/blank/partial prikaza.
- Frontend validation je nekompletan za sign-off (`check:analytics-guardrails` script nedostaje, `npm run build` ne prolazi).
- `dotnet test` na root-u ostaje nepouzdan signal u ovom okruženju; koristi se eksplicitni `dotnet test Api.Tests/Api.Tests.csproj` kao gate.

Production blockers:
- Browser smoke FAIL na više core/durable ruta.
- Frontend guardrails script nije dostupan i frontend build failuje.
