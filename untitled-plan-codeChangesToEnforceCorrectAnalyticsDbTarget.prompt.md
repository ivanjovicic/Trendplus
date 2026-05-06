## Plan: Verifikacija i ispravka produkcionog analytics DB targeta

TL;DR:
Prvo dokazati koji DB target koristi Render web proces. Ako runtime target nije Neon `trendplus` baza u kojoj postoje `public.vw_supplier_fullprice_signals` i `public.mv_supplier_decision_score_cache`, popraviti Render secret za `ConnectionStrings__AnalyticsConnection`, redeploy, pa tek onda testirati Scorecard. Ne dirati frontend, worker refresh, SQL view definicije ili supplier UX dok DB target nije potvrdjen.

---

## 1. Operativna verifikacija

### Koraci

1. U Render web servisu proveriti env/secrets:
   - `ConnectionStrings__DefaultConnection`
   - `ConnectionStrings__AnalyticsConnection`
   - `DATABASE_URL`
   - `PGHOST`, `PGDATABASE`, `PGUSER`
   - sve `ANALYTICS_*` varijable ako postoje

2. Redeploy ili restart web servisa i proveriti startup logove:
   - `DefaultConnection target:`
   - `AnalyticsConnection configured target:`
   - `AnalyticsConnection resolved target:`
   - `Analytics DB runtime target:`

3. Ocekivani runtime rezultat:
   - database = `trendplus`
   - user = ocekivani Neon user, npr. `neondb_owner`
   - schema = `public`
   - `vw_supplier_fullprice_signals` nije `<missing>`
   - `mv_supplier_decision_score_cache` nije `<missing>`

4. Ako `configured target` ili `resolved target` nisu pravi:
   - postaviti `ConnectionStrings__AnalyticsConnection` na pravi Neon connection string
   - ukloniti ili ispraviti override koji vodi na pogresan host/bazu/granu
   - redeploy

5. Tek kada runtime diagnostics potvrdi pravi target, pozvati Scorecard:

```bash
curl -sS "https://<service>/api/analytics/suppliers/decision-hub/summary?fromDate=2019-01-06&toDate=2026-05-06&dataScope=all"
```

Ocekivanje: `supplierCount > 0` i KPI vrednosti nisu sve nula.

### Ako Scorecard i dalje vraca nule

Dalju forenziku raditi samo posle potvrdjenog DB targeta:
1. server-side cache
2. endpoint filter/default logic
3. worker refresh/MV freshness
4. query path kroz `SupplierDecisionHubEndpoints`

---

## 2. Stvarno stanje koda

Ovo je bitno: plan ne treba da pretpostavlja da resolver ne postoji.

Vec postoji:
- `Api/Config/AnalyticsConnectionResolver.cs`
- `Api/Program.cs` vec racuna `analyticsConnection` preko resolvera
- `AnalyticsDbContext` se vec registruje preko resolved/tuned analytics konekcije
- `SupplierDecisionHubEndpoints` vec koristi `AnalyticsConnectionResolver.Resolve(configuration)`
- `AnalyticsIntelligenceEndpoints` vec koristi `AnalyticsConnectionResolver.Resolve(configuration)`
- `ReadinessWarmupHostedService` vec koristi resolver
- `AnalyticsConnectionDiagnosticsHostedService` vec otvara isti `AnalyticsDbContext` koji web proces koristi

Preostale rupe / korekcije plana:
- Diagnostics log trenutno kaze `Analytics DB configured target`, ali cita konekciju iz `AnalyticsDbContext`; to je u praksi resolved runtime target, ne raw configured target. Treba preimenovati ili dodati oba.
- Resolver nema eksplicitan `source` rezultat, pa je teze videti da li je koriscen `AnalyticsConnection`, fallback na `DefaultConnection`, ili nesto iz appsettings/env override-a.
- `DatabaseInitializer` i dalje ima direktan `GetValidatedConnectionString(configuration, "AnalyticsConnection", ...)`. Ne menjati naslepo; prvo proveriti koji init koraci stvarno treba da koriste analytics DB, a koji trendplus/default DB.
- `AnalyticsCachePrewarmHostedService` ne koristi DB konekciju; to je HTTP prewarm. Tu nije "resolved connection" tema, nego `AnalyticsPrewarm:BaseUrl` i guard protiv pogresnog localhost prewarm-a.

---

## 3. Minimalni code hardening plan

Ovo je mali, merge-friendly hardening, ne redesign.

### 3.1 Prosiriti resolver rezultat

Target file:
- `Api/Config/AnalyticsConnectionResolver.cs`

Dodati result model, npr:

```csharp
public sealed record AnalyticsConnectionResolution(
    string ConnectionString,
    string Source,
    bool UsedFallback,
    string? Warning);
```

Zadrzati postojeci `Resolve(...)` API radi kompatibilnosti, ali dodati `ResolveDetailed(...)`.

Source vrednosti:
- `AnalyticsConnection`
- `DefaultConnectionFallback`
- `MissingAnalyticsFallback`
- `LoopbackAnalyticsFallback`

Dodati config policy:
- `Analytics:AllowLoopbackInProduction` default `false`
- ako je production i analytics pokazuje na loopback, resolver ne sme koristiti loopback osim ako je flag eksplicitno `true`
- default ponašanje ostaje fallback na `DefaultConnection` ako je validan i non-loopback

Ne uvoditi fail-fast po defaultu. Eventualni flag za to moze biti kasnije:
- `Analytics:FailOnInvalidTarget`

### 3.2 Program.cs logovi

Target file:
- `Api/Program.cs`

Sada vec postoji:
- `AnalyticsConnection configured target:`
- `AnalyticsConnection resolved target:`

Doraditi:
- logovati `AnalyticsConnection source:`
- logovati `UsedFallback=true/false`
- koristiti `ResolveDetailed(...)` za isti resolved string koji ide u `AnalyticsDbContext`

Ne menjati DbContext shape, retry policy ili business config.

### 3.3 Runtime diagnostics wording

Target file:
- `Api/Services/Startup/AnalyticsConnectionDiagnosticsHostedService.cs`

Promeniti log label:
- sa `Analytics DB configured target`
- na `Analytics DB context target`

Razlog: service cita `analyticsDb.Database.GetConnectionString()`, tj. stvarni DbContext/runtime target.

Zadrzati:
- `current_database()`
- `current_user`
- `current_schema()`
- `search_path`
- server addr/port
- `to_regclass('public.vw_supplier_fullprice_signals')`
- `to_regclass('public.mv_supplier_decision_score_cache')`

### 3.4 ReadinessWarmup

Target file:
- `Api/Services/Startup/ReadinessWarmupHostedService.cs`

Vec koristi resolver. Doraditi samo ako se uvede `ResolveDetailed(...)`:
- logovati source/fallback warning
- readiness probe da koristi detailed resolved connection

Ne dodavati teze object checks u `/ready`.

### 3.5 Supplier/Analytics raw callers

Target files:
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs`

Vec koriste resolver. Potrebno je samo:
- prebaciti na shared helper/service ako uvodimo `ResolveDetailed(...)`
- ili ostaviti postojece ako nema dodatne vrednosti

Ne menjati SQL shape.
Ne schema-qualify kao incident fix, jer objekti vec postoje u `public` na pravoj bazi.

### 3.6 DatabaseInitializer

Target file:
- `Infrastructure/Seed/DatabaseInitializer.cs`

Ne raditi blind replace.

Potrebno:
1. Identifikovati koje sekcije koriste `AnalyticsConnection`.
2. Ako sekcija stvarno targetira analytics DB, koristiti resolver ili jasno dokumentovan raw analytics connection.
3. Ako sekcija treba da targetira default/trendplus DB, ne prebacivati je na analytics resolver.

Ovo je potencijalno jedina oblast gde code hardening moze imati rizik, zato je drzati odvojeno od hitnog incident fixa ako nije direktno potrebno.

### 3.7 Prewarm

Target file:
- `Api/Services/Startup/AnalyticsCachePrewarmHostedService.cs`

Korekcija plana:
- service ne koristi DB connection string
- proveriti samo HTTP base URL

Minimalni hardening:
- ako je non-dev i `AnalyticsPrewarm:BaseUrl` nije setovan, trenutni fallback na `127.0.0.1:<PORT>` je prihvatljiv samo ako web proces zaista slusa lokalno
- dodati log koji jasno kaze koji base URL koristi
- ne tretirati prewarm warning kao root cause za `42P01`

---

## 4. Test plan

### Unit tests za resolver

Dodati testove za:
1. explicit valid AnalyticsConnection -> koristi AnalyticsConnection
2. missing AnalyticsConnection + valid DefaultConnection non-dev -> fallback na DefaultConnection + warning
3. loopback AnalyticsConnection u production + valid DefaultConnection -> fallback na DefaultConnection + warning
4. loopback AnalyticsConnection u development -> dozvoljeno
5. loopback AnalyticsConnection u production + `Analytics:AllowLoopbackInProduction=true` -> dozvoljeno
6. missing oba connection stringa -> throw

Ako test projekat ne postoji ili nije lako uklopljen, ne blokirati incident fix; dodati manual verification umesto toga.

### Build

```bash
dotnet build Api/Api.csproj
```

### Runtime verification

Pokrenuti lokalno sa env kombinacijama:
- missing AnalyticsConnection
- loopback AnalyticsConnection
- valid Neon AnalyticsConnection

U logovima proveriti:
- configured target
- resolved target
- source
- used fallback
- runtime diagnostics

---

## 5. Render deploy checklist

Required secret:
- `ConnectionStrings__AnalyticsConnection`

Vrednost:
- Neon connection string ka bazi `trendplus`, schema `public`, ssl enabled

Ne oslanjati se na:
- `DATABASE_URL` osim ako aplikacija eksplicitno mapira taj env na `ConnectionStrings__AnalyticsConnection`
- `PGHOST`/`PGDATABASE` jer .NET config trenutno ne sklapa EF connection string iz PG varijabli

Render log mora pokazati:
- `AnalyticsConnection configured target: <neon-host>:5432/trendplus user=<user>`
- `AnalyticsConnection resolved target: <same target>`
- `AnalyticsConnection source: AnalyticsConnection`
- `Analytics DB context target: <same target>`
- `Analytics DB runtime target: database=trendplus ... vw_supplier_fullprice_signals=public.vw_supplier_fullprice_signals mv_supplier_decision_score_cache=public.mv_supplier_decision_score_cache`

---

## 6. Final decision

Plan je validan kao incident response, ali code section treba smanjiti:

1. Ne uvoditi novi connection system od nule.
2. Ne menjati frontend, worker refresh, SQL view definicije ili supplier UX.
3. Ne schema-qualify SQL kao primarni fix.
4. Dovrsiti postojeći resolver: detailed result, source logging, diagnostics wording.
5. Operativno popraviti Render secret ako runtime target nije pravi.

Najkraci sledeci korak:
1. redeploy trenutni build
2. procitati startup diagnostics
3. ako target nije pravi, popraviti `ConnectionStrings__AnalyticsConnection` u Render-u
4. tek zatim raditi code hardening ako logovi pokazu da je resolver/source tracing jos nejasan
