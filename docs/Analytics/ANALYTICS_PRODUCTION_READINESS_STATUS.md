# Analytics Production Readiness Status

Datum verifikacije: 2026-05-26
Repo: ivanjovicic/Trendplus
Osnovni checklist: docs/Analytics/ANALYTICS_PRODUCTION_READINESS_CHECKLIST.md

## Sažetak

Trenutni status nije production-ready zbog blokera u frontend route pokrivenosti:
- Verifikacija aktivnog router-a pokazuje da je runtime source of truth `Klijent/clientapp/src/App.tsx` (uvozi se iz `Klijent/clientapp/src/main.tsx`), gde ključne core rute iz checklist-a i dalje nisu mapirane (`/analytics/products`, `/analytics/supplier`, `/analytics/actions`, `/analytics/supplier/report`, `/analytics/reports/pilot-intake`).

## Status tabela

| Oblast | Status | Dokaz | Gap | Prioritet |
|---|---|---|---|---|
| Build/test gates | PASS (explicit gate) | `dotnet build` prolazi; `npm run check:analytics-guardrails` prolazi; `npm run build` prolazi. `dotnet test Api.Tests/Api.Tests.csproj` prolazi (`Failed: 0, Passed: 448`). | `dotnet test` na root-u i dalje nije pouzdan signal za test execution u ovom okruženju; eksplicitni test projekat ostaje izvor istine. | P1 |
| Trust/data contract | PASS (code+tests) | No-fake-zero i meta kontrakti pokriveni testovima u `Api.Tests/AnalyticsResponseMetaContractTests.cs`, `Api.Tests/AnalyticsMetaContractTests.cs`, `Api.Tests/AnalyticsReportsContractTests.cs`. Durable report builder koristi `AnalyticsResponseMeta` i eksplicitne error/empty grane (`Api/Endpoints/SupplierDecisionHubEndpoints.cs`, `Api/Endpoints/DataQualityEndpoints.cs`). | Nema novog funkcionalnog gapa potvrđenog u ovoj verifikaciji; potrebna je ponovna potvrda posle popravke failing testova. | P2 |
| Durable reports | PARTIAL | Backend durable endpointi postoje: `/api/analytics/reports/supplier-decision` i `/api/analytics/reports/pilot-intake` (`Api/Endpoints/AnalyticsReportsEndpoints.cs`). Frontend API klijent koristi te rute (`Klijent/clientapp/src/services/analyticsApi.ts`). Supplier report ima jasan expired state (`Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`) i test (`Klijent/clientapp/src/pages/__tests__/SupplierDecisionReportPage.spec.tsx`). | Rute za durable report flow nisu mapirane u aktivnom router-u (`Klijent/clientapp/src/App.tsx`); direktno otvaranje i refresh sa query parametrima ne mogu se potvrditi kao stabilni dok se route mapping ne doda i browser smoke ne izvrši. | P0 |
| Cache/report invalidation | PASS (code+tests) | Cache status payload vraća `cacheMode`, `reportCacheVersion`, `lastReportCacheClearAtUtc` i ostala status polja iz istog shared handler-a za obe rute (`/api/analytics/cache/status` i `/api/analytics/cached/cache/status`) u `Api/Endpoints/CachedAnalyticsEndpoints.cs`. `CoreFamilies` invalidation i bump report verzije pokriveni (`Infrastructure/Services/Caching/AnalyticsCacheAdminService.cs`, `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`, `Api.Tests/AnalyticsCacheAdminServiceTests.cs`). Worker/import invalidacija koristi `CoreFamilies` (`Workers/NightlyAnalyticsRefreshWorker.cs`, `Workers/AnalyticsDataQualityHealthWorker.cs`, `Api/Services/AccessImportService.cs`). | Nema aktivnog rute gapa za cache status; frontend i dalje koristi legacy putanju zbog minimalnog rizika. | P2 |
| KPI methodology | PASS | Canonical registry i alias map postoje (`Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts`), plus testovi (`Klijent/clientapp/src/utils/__tests__/analyticsMetricDefinitions.spec.ts`). KPI explain i methodology panel su integrisani (`KpiExplainButton`, `MetricMethodologyPanel`) kroz core analytics stranice i report komponente. | Nema potvrđenog gapa u ovoj verifikaciji. | P2 |
| UX/copy | PARTIAL | Expired state i sledeći koraci su jasni u supplier report stranici (`Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`) i testu. In-memory production warning postoji u cache status sloju (`Api/Endpoints/CachedAnalyticsEndpoints.cs`, `Api/Services/AnalyticsRefreshStatusService.cs`, `Api/Program.cs`). | Manual UI smoke za mojibake/copy preko svih core ruta nije urađen u browser sesiji tokom ovog passa; procena je code/test-based. | P2 |
| Manual smoke routes | PARTIAL | U `Klijent/clientapp/src/App.tsx` potvrđene su rute `/analytics`, `/analytics/inventory`, `/analytics/data-quality`; i dalje nisu mapirane `/analytics/products`, `/analytics/supplier`, `/analytics/actions`, `/analytics/supplier/report`, `/analytics/reports/pilot-intake`. Istovremeno, postoje linkovi ka tim rutama (npr. `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`). | Core smoke lista iz checklist-a ne može biti potpuno PASS dok nedostajuće rute ne budu mapirane ili preusmerene; browser smoke nije rađen. | P0 |
| Production blockers | FAIL | Checklist bloker aktivan: durable/core route dostupnost gap u SPA router-u. | Potrebno route mapiranje (ili eksplicitni redirect-i) pre production sign-off-a. | P0 |

## Komande i rezultati

Backend:
- `dotnet build`: PASS (build prolazi; prisutna upozorenja).
- `dotnet test`: izvršena komanda sa root-a; rezultat ne prikazuje izvršene testove (samo restore/build).
- `dotnet test Api.Tests/Api.Tests.csproj`: PASS (`Failed: 0, Passed: 448, Skipped: 0, Total: 448`).

Frontend:
- `cd Klijent/clientapp && npm run check:analytics-guardrails`: PASS.
- `cd Klijent/clientapp && npm run build`: PASS.

## Core rute verifikacija

| Ruta | Mapirana | Browser smoke | Status |
|---|---|---|---|
| `/analytics` | DA (`Klijent/clientapp/src/App.tsx`) | nije urađen | PASS code-level |
| `/analytics/products` | NE (`Klijent/clientapp/src/App.tsx`) | nije urađen | PARTIAL |
| `/analytics/supplier` | NE (`Klijent/clientapp/src/App.tsx`) | nije urađen | PARTIAL |
| `/analytics/inventory` | DA (`Klijent/clientapp/src/App.tsx`) | nije urađen | PASS code-level |
| `/analytics/data-quality` | DA (`Klijent/clientapp/src/App.tsx`) | nije urađen | PASS code-level |
| `/analytics/actions` | NE (`Klijent/clientapp/src/App.tsx`) | nije urađen | PARTIAL |
| `/analytics/supplier/report?fromDate=...&toDate=...` | NE (`Klijent/clientapp/src/App.tsx`) | nije urađen | PARTIAL |
| `/analytics/reports/pilot-intake?fromDate=...&toDate=...` | NE (`Klijent/clientapp/src/App.tsx`) | nije urađen | PARTIAL |

## API verifikacija

- `/api/analytics/reports/supplier-decision`: postoji (`Api/Endpoints/AnalyticsReportsEndpoints.cs`).
- `/api/analytics/reports/pilot-intake`: postoji (`Api/Endpoints/AnalyticsReportsEndpoints.cs`).
- `/api/analytics/cache/status`: postoji kao canonical alias (`Api/Endpoints/CachedAnalyticsEndpoints.cs`).
- `/api/analytics/cached/cache/status`: postoji i dalje (legacy/frontend kompatibilnost) (`Api/Endpoints/CachedAnalyticsEndpoints.cs`, `Klijent/clientapp/src/services/analyticsApi.ts`).
- Refresh status endpoint: `/api/analytics/refresh-status` postoji (`Api/Endpoints/AnalyticsRefreshStatusEndpoints.cs`).

## Posebne provere

- Durable report URL posle browser refresh-a: DELIMIČNO potvrđeno (code/test logika postoji), ali zbog nedostatka route mapiranja u aktivnom `App.tsx` ne može se označiti kao pun PASS.
- Expired state je jasan: PASS (`SupplierDecisionReportPage` + test).
- Report export dugmad bez payload-a: PASS (akcije zavise od `payload`; u empty state flow-u prikazuje se recovery UX, ne export flow).
- Cache status prikazuje `cacheMode` i `reportCacheVersion` na canonical i legacy ruti: PASS (`CachedAnalyticsEndpoints`).
- Production in-memory warning: PASS (`CachedAnalyticsEndpoints`, `AnalyticsRefreshStatusService`, `Program`).
- No fake-zero u error granama gde postoje testovi: PASS (više contract/regression testova u `Api.Tests`).

## Finalni zaključak

Status: Not ready for production sign-off.

Kandidatski status nakon zatvaranja P0/P1 gapova može biti:
- Pilot-ready with known limitations.
