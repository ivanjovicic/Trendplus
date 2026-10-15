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

Dodatne familije: `color-sales`, `supplier-sales`, `shoe-type-sales`, `nivelacija`.

Mapiranje je eksplicitno u `AnalyticsCachePolicy` (`ResolveFamilyPrefixes`, `TryNormalizeFamily`); nepoznata familija je validaciona greška, `all` je dozvoljen.

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
- `BumpReportCacheVersionAsync` inkrementira verziju (serijalizovano po instanci, `max(remote, local) + 1`; istovremeni bump sa dve instance može dati istu novu verziju, što je bezopasno jer je i dalje veća od stare)
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

## Generacije (Memory, Hybrid, Redis)

- Fizički ključ = logički ključ + `|g:` + hash tokena generacija svih pokrivajućih root-ova (`AnalyticsCachePolicy.GenerationRoots`).
- Invalidacija familije upisuje novi GUID token (bez read-modify-write); u Redis-u `analytics-gen:v1:{root}` (30 dana). Druge instance vide novu generaciju pri sledećem čitanju, pa se Hybrid prefix-brisanje više ne oslanja na lokalni L1 scan.
- Upis posle invalidacije: `SetIfGenerationAsync` odbacuje upis ako je generacija promenjena dok je factory radio (nema stale republish-a).
- Redis ispad: lokalni tokeni se rotiraju; pri oporavku se svi root-ovi bumpuju pre nego što se Redis ponovo veruje.
- L2 envelope čuva istek; promocija L2 -> L1 zadržava preostali TTL (ranije fiksnih 5 min).
- Vrednosti se čuvaju serijalizovano; svaki hit vraća novu instancu (nema deljenja mutabilnih objekata). Neslaganje tipa = miss.
- Per-key lock sprečava stampede unutar instance.

## Policy entry i stale metadata

- Policy-aware endpointi čuvaju `{Value, Metadata}` pod `cacheKey|policy`; entry bez metadata je miss (nema izmišljenog `CreatedAt`).
- Dashboard bootstrap sa greškama ili `Meta.IsPartial=true` se ne kešira.
- `/api/analytics/cached/cache/status` vraća i `metrics` (L1/L2 hit, miss, factory izvršavanja, preskočeni stale upisi, invalidacije, greške serijalizacije), Meter `Trendplus.AnalyticsCache`.

## Frontend browser cache

- `clearAnalyticsCache` briše i browser cache i in-flight mapu.
- Deljeni in-flight zahtev nije vezan za signal pojedinačnog pozivaoca; abort jednog ne prekida ostale.
- Invalidacija se emituje drugim tabovima kroz `BroadcastChannel("trendplus-analytics-cache")`.
- Ručni refresh (`refetch` u `useReliableAnalyticsQuery`) ide sa `withClientCacheBypass`.
- `useReliableAnalyticsQuery` odbacuje poslednji snapshot kada se promeni `contextKey` (period/store/supplier/dataScope), pa se snapshot drugog konteksta ne prikazuje kao aktuelan.

## Test pokriće

Dodate regresije proveravaju:

- kanonsko mapiranje family -> prefix
- multi-family invalidaciju i shared clear-state persistenciju
- `ClearFamiliesAsync(CoreFamilies)` bumpa report version (pokriva import i nightly refresh tok)
- `ClearFamiliesAsync([non-report])` ne bumpa report version niti setuje `LastReportCacheClearAtUtc`
- `ClearAsync("reports")` bumpa report version token
- stale cache meta factory kontrakt (`STALE_CACHE`)
- `AnalyticsCacheGenerationTests`: memory / hybrid-local / hybrid-redis, multi-instance invalidacija, spori factory, stampede, Redis ispad, L2 TTL, nemutabilnost, tip-miss, policy entry, shoe-type izolacija
- nepoznata familija -> 400 (`AnalyticsCacheInvalidateAuthorizationTests`)
- frontend: clear, abort deljenja, bypass, broadcast, izolacija konteksta

## Operativna napomena

Ako worker refresh uspe, a cache invalidacija ne uspe, proces se i dalje vodi kao uspešan refresh sa warning porukom u worker log-u. Ovo sprečava tihi neuspeh invalidacije bez maskiranja uspešnog refresh procesa.
