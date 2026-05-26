# Analytics Production Readiness Status

Datum verifikacije: 2026-05-26
Repo: ivanjovicic/Trendplus
Osnovni checklist: docs/Analytics/ANALYTICS_PRODUCTION_READINESS_CHECKLIST.md

## Sažetak

Trenutni status nije production-ready zbog dva blokera:
- `dotnet test Api.Tests/Api.Tests.csproj` trenutno pada (5 compile grešaka u `Api.Tests/AnalyticsReportsContractTests.cs`).
- Ključne core rute iz checklist-a nisu mapirane u glavnom frontend router-u (`/analytics/products`, `/analytics/supplier`, `/analytics/actions`, `/analytics/supplier/report`, `/analytics/reports/pilot-intake`).

## Status tabela

| Oblast | Status | Dokaz | Gap | Prioritet |
|---|---|---|---|---|
| Build/test gates | PARTIAL | `dotnet build` prolazi; `npm run check:analytics-guardrails` prolazi; `npm run build` prolazi. `dotnet test Api.Tests/Api.Tests.csproj` pada sa 5 compile grešaka u `Api.Tests/AnalyticsReportsContractTests.cs` (CS1729/CS1503). | Backend test gate nije zadovoljen. Takođe, `dotnet test` na root-u završava bez prikaza izvršenih testova, pa je korišćen eksplicitan test projekat kao izvor istine. | P0 |
| Trust/data contract | PASS (code+tests) | No-fake-zero i meta kontrakti pokriveni testovima u `Api.Tests/AnalyticsResponseMetaContractTests.cs`, `Api.Tests/AnalyticsMetaContractTests.cs`, `Api.Tests/AnalyticsReportsContractTests.cs`. Durable report builder koristi `AnalyticsResponseMeta` i eksplicitne error/empty grane (`Api/Endpoints/SupplierDecisionHubEndpoints.cs`, `Api/Endpoints/DataQualityEndpoints.cs`). | Nema novog funkcionalnog gapa potvrđenog u ovoj verifikaciji; potrebna je ponovna potvrda posle popravke failing testova. | P2 |
| Durable reports | PARTIAL | Backend durable endpointi postoje: `/api/analytics/reports/supplier-decision` i `/api/analytics/reports/pilot-intake` (`Api/Endpoints/AnalyticsReportsEndpoints.cs`). Frontend API klijent koristi te rute (`Klijent/clientapp/src/services/analyticsApi.ts`). Supplier report ima jasan expired state (`Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`) i test (`Klijent/clientapp/src/pages/__tests__/SupplierDecisionReportPage.spec.tsx`). | Glavne SPA rute `/analytics/supplier/report` i `/analytics/reports/pilot-intake` nisu mapirane u `Klijent/clientapp/src/App.tsx`, pa direktno otvaranje/refresh kroz app router nije garantovano. | P0 |
| Cache/report invalidation | PASS (code+tests) | Cache status payload vraća `cacheMode`, `reportCacheVersion`, `lastReportCacheClearAtUtc` (`Api/Endpoints/CachedAnalyticsEndpoints.cs`). `CoreFamilies` invalidation i bump report verzije pokriveni (`Infrastructure/Services/Caching/AnalyticsCacheAdminService.cs`, `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`, `Api.Tests/AnalyticsCacheAdminServiceTests.cs`). Worker/import invalidacija koristi `CoreFamilies` (`Workers/NightlyAnalyticsRefreshWorker.cs`, `Workers/AnalyticsDataQualityHealthWorker.cs`, `Api/Services/AccessImportService.cs`). | Checklist traži proveru `/api/analytics/cache/status`, ali implementiran i korišćen endpoint je `/api/analytics/cached/cache/status`. Nedostaje alias ili dokumentovana migracija putanje. | P1 |
| KPI methodology | PASS | Canonical registry i alias map postoje (`Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts`), plus testovi (`Klijent/clientapp/src/utils/__tests__/analyticsMetricDefinitions.spec.ts`). KPI explain i methodology panel su integrisani (`KpiExplainButton`, `MetricMethodologyPanel`) kroz core analytics stranice i report komponente. | Nema potvrđenog gapa u ovoj verifikaciji. | P2 |
| UX/copy | PARTIAL | Expired state i sledeći koraci su jasni u supplier report stranici (`Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`) i testu. In-memory production warning postoji u cache status sloju (`Api/Endpoints/CachedAnalyticsEndpoints.cs`, `Api/Services/AnalyticsRefreshStatusService.cs`, `Api/Program.cs`). | Manual UI smoke za mojibake/copy preko svih core ruta nije urađen u browser sesiji tokom ovog passa; procena je code/test-based. | P2 |
| Manual smoke routes | FAIL | U `Klijent/clientapp/src/App.tsx` postoje rute za `/analytics`, `/analytics/inventory`, `/analytics/data-quality`; ne postoje rute za `/analytics/products`, `/analytics/supplier`, `/analytics/actions`, `/analytics/supplier/report`, `/analytics/reports/pilot-intake`. Istovremeno, postoje linkovi ka tim rutama (npr. `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`). | Core smoke lista iz checklist-a ne može biti potpuno PASS dok rute nisu mapirane ili preusmerene. | P0 |
| Production blockers | FAIL | Checklist blokeri aktivni: backend test gate fail + durable/core route dostupnost gap. | Potrebna je popravka test kompilacije i route mapiranja (ili eksplicitnih redirect-a) pre production sign-off-a. | P0 |

## Komande i rezultati

Backend:
- `dotnet build`: PASS (build prolazi; prisutna upozorenja).
- `dotnet test`: izvršena komanda sa root-a; rezultat ne prikazuje izvršene testove (samo restore/build).
- `dotnet test Api.Tests/Api.Tests.csproj`: FAIL (5 compile grešaka u `Api.Tests/AnalyticsReportsContractTests.cs`: CS1729, CS1503).

Frontend:
- `cd Klijent/clientapp && npm run check:analytics-guardrails`: PASS.
- `cd Klijent/clientapp && npm run build`: PASS.

## Core rute verifikacija

- `/analytics`: mapirana (`Klijent/clientapp/src/App.tsx`).
- `/analytics/products`: NIJE mapirana u `App.tsx` (postoje link/reference, ali bez route definicije).
- `/analytics/supplier`: NIJE mapirana u `App.tsx` (postoje link/reference, ali bez route definicije).
- `/analytics/inventory`: mapirana (`Klijent/clientapp/src/App.tsx`).
- `/analytics/data-quality`: mapirana (`Klijent/clientapp/src/App.tsx`).
- `/analytics/actions`: NIJE mapirana u `App.tsx` (postoje link/reference, ali bez route definicije).
- `/analytics/supplier/report?fromDate=...&toDate=...`: NIJE mapirana u `App.tsx`.
- `/analytics/reports/pilot-intake?fromDate=...&toDate=...`: NIJE mapirana u `App.tsx`.

## API verifikacija

- `/api/analytics/reports/supplier-decision`: postoji (`Api/Endpoints/AnalyticsReportsEndpoints.cs`).
- `/api/analytics/reports/pilot-intake`: postoji (`Api/Endpoints/AnalyticsReportsEndpoints.cs`).
- `/api/analytics/cache/status`: nije pronađen kao aktivna ruta.
- `/api/analytics/cached/cache/status`: postoji i koristi se u frontend-u (`Api/Endpoints/CachedAnalyticsEndpoints.cs`, `Klijent/clientapp/src/services/analyticsApi.ts`).
- Refresh status endpoint: `/api/analytics/refresh-status` postoji (`Api/Endpoints/AnalyticsRefreshStatusEndpoints.cs`).

## Posebne provere

- Durable report URL posle browser refresh-a: DELIMIČNO potvrđeno (code/test logika postoji), ali zbog nedostatka route mapiranja u `App.tsx` ne može se označiti kao pun PASS.
- Expired state je jasan: PASS (`SupplierDecisionReportPage` + test).
- Report export dugmad bez payload-a: PASS (akcije zavise od `payload`; u empty state flow-u prikazuje se recovery UX, ne export flow).
- Cache status prikazuje `cacheMode` i `reportCacheVersion`: PASS (`CachedAnalyticsEndpoints`).
- Production in-memory warning: PASS (`CachedAnalyticsEndpoints`, `AnalyticsRefreshStatusService`, `Program`).
- No fake-zero u error granama gde postoje testovi: PASS (više contract/regression testova u `Api.Tests`).

## Finalni zaključak

Status: Not ready for production sign-off.

Kandidatski status nakon zatvaranja P0/P1 gapova može biti:
- Pilot-ready with known limitations.
