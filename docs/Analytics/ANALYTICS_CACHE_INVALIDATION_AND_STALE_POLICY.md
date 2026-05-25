# Analytics Cache Invalidation and Stale Policy

Ovaj dokument opisuje trenutno standardizovan analytics cache tok za Trendplus backend.

## Ciljevi

- nema fake-zero fallback-a kada je problem u backend-u ili cache topologiji
- family prefiksi su kanonski i isti za invalidaciju, status i observability
- uspešan import ili refresh invalidira analytics cache kroz jedan centralni kanal
- stale cache odgovor vraća `IsPartial=true` i `WarningCode=STALE_CACHE` tamo gde endpoint već ima `Meta`
- cache status endpoint jasno pokazuje da li je clear-state lokalni ili deljeni

## Kanonske cache familije

Trenutno standardizovane core familije su:

- `dashboard`
- `product-decision-center`
- `supplier-decision-hub`
- `inventory`
- `data-quality`
- `pre-post`
- `pre-nivelacija-prioriteti`
- `reports` ← trajan report cache; verzionisan sa `rv:{version}` tokenom

Mapiranje prefiksa je definisano u `AnalyticsCachePolicy.ResolveFamilyPrefix(...)`.

## Invalidacija

Centralni ulaz je `AnalyticsCacheAdminService`.

Podržani tokovi:

- `ClearAsync("all"|family)` za admin ručnu invalidaciju
- `ClearFamiliesAsync(...)` za multi-family invalidaciju posle refresh procesa

Trenutno su povezani sledeći lifecycle hook-ovi:

- `AccessImportService`: posle uspešnog importa i pri delete-batch analytics cleanup-u poziva `cacheAdmin.ClearFamiliesAsync(CoreFamilies)` što uključuje `reports` family i bumpa `ReportCacheVersion`. Fallback na sirovi prefix purge ako admin service nije dostupan.
- `NightlyAnalyticsRefreshWorker`: posle uspešnog refresh-a invalidira sve core familije uključujući `reports` → bumpa `ReportCacheVersion` i setuje `LastReportCacheClearAtUtc`.
- `AnalyticsDataQualityHealthWorker`: invalidira `data-quality` i `reports` familije.

## Report cache verzionisanje

Trajan report cache (Supplier Decision Report, Pilot Intake Report, itd.) koristi versioned ključeve:

```
analytics:analytics-report:<slug>:rv:{version}:...
```

Verzija se čuva u `AnalyticsCacheKeys.ReportVersionTokenKey` i persista u Redis-u kada je dostupan.

Svaki put kada se `reports` family invalidira:
- `BumpReportCacheVersionAsync` inkrementira verziju
- `LastReportCacheClearAtUtc` se setuje
- Svi stari ključevi automatski postaju neaktivni (jer nova verzija generiše drugačiji ključ)

| Tok                            | Bumpa report version? | Setuje LastReportCacheClearAtUtc? |
|--------------------------------|----------------------|-----------------------------------|
| AccessImportService import     | ✅ (via CoreFamilies)  | ✅                                 |
| AccessImportService delete     | ✅ (via CoreFamilies)  | ✅                                 |
| NightlyAnalyticsRefreshWorker  | ✅ (via CoreFamilies)  | ✅                                 |
| AnalyticsDataQualityHealthWorker | ✅ (explicitno)      | ✅                                 |
| Admin `ClearAsync("all")`      | ✅                    | ✅                                 |
| Admin `ClearAsync("reports")` | ✅                    | ✅                                 |
| Admin `ClearAsync("dashboard")` | ❌                   | ❌                                 |

## Deljeni clear-state

`AnalyticsCacheAdminService` čuva poslednje clear stanje u deljenom store-u kada su ispunjeni svi uslovi:

- `IDistributedCache` je registrovan
- analytics cache provider koristi Redis
- Redis je dostupan

Ako to nije ispunjeno, `/api/analytics/cached/cache/status` vraća lokalno stanje i warning da cache može biti nekonzistentan između instanci.

## Stale meta signal

Policy-aware helper koristi metadata ključ uz cache entry i poredi starost sa `StaleAfter` pragom iz `AnalyticsCachePolicy`.

Kada je entry stariji od dozvoljenog praga, response meta treba da dobije:

- `IsPartial=true`
- `WarningCode="STALE_CACHE"`
- `WarningMessage` sa pozivom na osvežavanje
- `DataQualityStatus="warning"`
- `LastRefreshAtUtc` iz vremena kreiranja cache entry-ja

U ovom prolazu stale warning je eksplicitno povezan na glavne response-e koji već imaju `Meta`:

- sales summary
- top products
- product decision center
- dashboard bootstrap

## Test pokriće

Dodate regresije proveravaju:

- kanonsko mapiranje family -> prefix
- multi-family invalidaciju i shared clear-state persistenciju
- `ClearFamiliesAsync(CoreFamilies)` bumpa report version (pokriva import i nightly refresh tok)
- `ClearFamiliesAsync([non-report])` ne bumpa report version niti setuje `LastReportCacheClearAtUtc`
- `ClearAsync("reports")` bumpa report version token
- stale cache meta factory kontrakt (`STALE_CACHE`)

## Operativna napomena

Ako worker refresh uspe, a cache invalidacija ne uspe, proces se i dalje vodi kao uspešan refresh sa warning porukom u worker log-u. Ovo sprečava tihi neuspeh invalidacije bez maskiranja uspešnog refresh procesa.
