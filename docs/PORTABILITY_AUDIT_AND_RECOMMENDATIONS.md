# Trendplus — Tehnički audit i plan refaktora za cloud & DB portability

Datum: 2026-04-21
Autor: Tehnički audit (senior .NET arhitekta / PostgreSQL konsultant / DevOps)

Sadržaj:
- Kontekst i sažetak nalaza
- 1) Realna procena trenutne portabilnosti
- 2) Prioritizovani roadmap (P0..P3)
- 3) Kratkoročni plan implementacije
- 4) Detaljan tehnički plan za `IFileStorage`
- 5) Detaljan tehnički plan za worker process model
- 6) CI/CD plan za multi-provider deployment (skeleton)
- 7) Detaljan plan portabilnosti baze (Neon → target)
- 8) Najveće prepreke i konkretna rešenja
- 9) Konkrete preporuke za .NET i PostgreSQL (kod i konfiguracija)
- 10) Finalni zaključak
- 11) Sledeći konkretni koraci (prioritetna lista)

---

## Kontekst i sažetak nalaza

Trendplus je .NET 8 aplikacija, EF Core + Npgsql za PostgreSQL. Aplikacija je već uglavnom kontejnerizovana; postoje manifesti za Render i Fly, i standardna konfiguracija preko `appsettings.*` + env vars. Glavni cilj: visok nivo portabilnosti između cloud providera za backend i DB.

Glavni problemi koje treba rešiti pre ozbiljnije migracije:
- Local filesystem dependency (AccessImport i drugi) — blokira ephemeral hosting.
- Background jobs rade unutar web procesa — duplikacija poslova prilikom scale-out.
- PostgreSQL portability zavisi od ekstenzija (`pgvector`, `pg_trgm`).
- Pooling / connection handling i ponovna upotreba `DbContext` u pozadinskim zadacima izazivaju SocketException/timeout probleme.

Već izvršene izmene (sa repozitorijuma / diskusije):
- tools/mark-migration-safe.ps1 (helper za markiranje EF migracija ako je schema već u očekivanom stanju)
- mig_20260419.sql (specifičan migration SQL kreiran/aplikovan)
- Patch: Database/Analytics/013_AddSupplierDecisionCompatibilitySchema.sql — učinjen idempotentnim
- Api/Program.cs — dodata opcija za local overrides; connection-target diagnostics
- Infrastructure/Services/AnalyticsDataQualityHistoryService.cs — ExecuteSqlRawAsync param fix
- Infrastructure/Seed/DatabaseInitializer.cs — deferred backfill: nova instanciranja DbContext-a
- AccessImportBackgroundWorker — sada poštuje globalni worker toggle (lokalno)

---

### Korekcije nakon dodatne repo provere

- `PROCESS_TYPE` ne treba da zameni postojeći `Workers:Enabled` mehanizam, nego da bude dodatni runtime selector. U repou već postoje `WorkerRuntimeControlService` i `WorkerHealthService`, pa je minimalan P0 da `PROCESS_TYPE` određuje da li se hosted servisi uopšte registruju, a `Workers:Enabled` ostaje safety toggle.
- Ako se uvede `PROCESS_TYPE=worker`, production default za `Workers:Enabled` mora biti usklađen. Trenutno je u `Api/appsettings.Production.json` worker runtime podrazumevano ugašen; bez korekcije bi worker proces mogao da se podigne, ali da ostane logički pauziran.
- AccessImport ne može potpuno da napusti lokalni disk tokom izvršenja importa. ODBC / MDB tokovi i postojeći import pipeline rade nad lokalnom putanjom fajla, pa object storage treba da bude durable source-of-truth, a worker zatim da preuzme fajl u lokalni temp/work directory pre obrade.
- `DataImportBatch.SourceFilePath` trenutno je ključan za claim logiku u `Api/Services/Access/AccessImportJobQueue.cs` i koristi se i u `Api/Services/NivelacijaRepairService.cs`. Zato nije bezbedno samo zameniti ga sa `s3://...` ili `storage://...`; minimalan refaktor je dodavanje novih nullable polja tipa `SourceStorageKey` i `SourceStorageProvider`, uz zadržavanje postojećeg polja radi kompatibilnosti.
- P0 filesystem refaktor ne treba da juri svaki `File.*` poziv. Kritični su durable zapisi koji moraju preživeti restart / redeploy: Access import source fajlovi, product image upload, i document output. Request-local temp fajlovi za scoring/search mogu ostati lokalni ako se eksplicitno vode kao ephemeral cache.
- `DeferredStartupTasksHostedService` je veći portability rizik nego što prvobitni plan eksplicitno navodi. Dok god je registrovan i na web replikama, DB init, stale batch recovery i Neon warmup mogu da se pokreću na svakom scale-out događaju.
- Neon warmup treba izolovati po provideru. Trenutno postoji startup opcija za warmup; plan treba da predvidi da bude eksplicitno vezana za Neon ili podrazumevano ugašena na non-Neon targetu.
- `AddDbContextFactory` je već uveden za `TrendplusDbContext`, a najveći konkretan problem je bio deferred backfill u `Infrastructure/Seed/DatabaseInitializer.cs`. Ne treba tvrditi da su svi worker-i trenutno pogrešni; P0 fokus ostaje na dugim taskovima i background tokovima koji izlaze iz standardnog request scope-a.
- `ExecuteSqlRawAsync` regresija ne treba da se testira InMemory providerom. Ovaj bug je vezan za realan EF Core/Npgsql overload binding i mora da se potvrdi integracionim testom nad pravim PostgreSQL-om, idealno preko Testcontainers.
- Pored AccessImport-a, u P0/P1 plan treba eksplicitno uvrstiti i `Api/Endpoints/AllEndpoints.cs` product image putanje i `Infrastructure/Services/Documents/DocumentStorage.cs`, jer su i to trajni fajlovi koji danas žive na lokalnom disku.
- `render.yaml` trenutno nije potpuno poravnat sa ciljem "jedan image za više providera"; koristi provider-native build/start pristup. Ako single-image deployment ostaje cilj, Render manifest treba kasnije prebaciti da referencira isti publish-ovani image kao i Fly.
- U repou postoje pomoćni `tmp/*.js` fajlovi sa hard-coded Neon/pooler connection stringovima. To nije samo security problem nego i config portability šum; treba ih izbaciti iz commit-ovane površine ili prebaciti u lokalni ignored prostor.

## 1) REALNA PROCENA TRENUTNE PORTABILNOSTI

### Code portability — ocena: 8/10
- Šta je dobro:
  - .NET 8 + EF Core standardni pristup.
  - Dockerfile i manifesti za Render / Fly postoje.
  - Konfiguracija preko `appsettings.*` i env vars.
- Šta blokira:
  - Problem u kodu: direktni pozivi `File.*`, `FileStream`, `Path.Combine` u AccessImport i sličnim modulima.
  - Problem u kodu: pozadinski zadaci reuse-uju DI-scoped `DbContext` preko dužih taskova.
- Uticaj na migraciju: bez refaktora filesystem i DbContext patterna, deployment na ephemeral provajdere je visokorizičan.
- Provere u repo-u: pokrenuti grep ( vidi sekciju „šta proveriti“ niže ).

### Config portability — ocena: 7/10
- Šta je dobro:
  - Env vars i `appsettings.*` pattern.
  - `appsettings.Development.local.json` ignorisan u git.
- Šta blokira:
  - Mogući hard-coded connection string u `launchSettings.json` ili developer skriptama.
  - Neujednačena politika za enable/disable workers (koristiti `PROCESS_TYPE` ili `WORKER_PROCESS`).
- Uticaj: srednji; zahteva operativnu disciplinu i standardizovanu env var konvenciju.

### Infra / deployment portability — ocena: 6/10
- Šta je dobro:
  - Jedan image model moguć; Render & Fly mogu da rade sa image+env.
- Šta blokira:
  - Web+worker nisu odvojeni — otežava scaling i kontrolu.
  - Reliance na host FS za temp/uploads/logs.
- Uticaj: zahteva promene u deployment modelu da bi bio siguran.

### Data portability — ocena: 4–6/10
- Šta je dobro:
  - Većina SQL je standardni Postgres.
  - EF migracije su prisutne.
- Šta blokira:
  - `pgvector` i `pg_trgm` koriste se i nisu uvek dostupne.
  - Neon-specifične optimizacije/warmup moraju biti izolovane.
- Uticaj: ako target ne podržava ekstenzije, zahteva se redesign (P0).

---

## 2) PRIORITIZOVANI ROADMAP (P0..P3)

P0 — obavezno pre migracije
- Implementirati `IFileStorage` i migrisati fajlove iz lokalnog FS u S3-compatible storage
  - Težina: medium-high. Rizik: very high ako se ne uradi.
  - Domen: code + infra + data
- DB extension preflight + runbook (pgvector/pg_trgm check, CREATE EXTENSION test)
  - Težina: medium. Rizik: very high.
  - Domen: infra + migration
- Merge: ExecuteSqlRawAsync param fix i backfill DbContext isolation
  - Težina: low. Rizik: high ako se ne uradi.
  - Domen: code
- `PROCESS_TYPE` / `WORKER_PROCESS` switch + `AddDbContextFactory` za workers
  - Težina: low. Rizik: high.
  - Domen: code + config

P1 — potrebno za rutinsku portabilnost
- CI build-and-push single Docker image
- Unit/integration tests za ExecuteSqlRaw/backfill
- Connection pooling i retry tuning (Npgsql)

P2 — hardening i operacija
- Split web i worker u odvojene deployable procese
- CI DB extension availability checks

P3 — dugoročno
- Extract vector workloads u specijalizovani vector DB (ako target ne podržava pgvector)
- Query/index optmizacija za `pg_trgm` workloads

---

## 3) KRATKOROČNI PLAN IMPLEMENTACIJE (konkretan redosled)

Cilj: brzo smanjiti rizike i osigurati da se može izvesti migracija dok se paralelno radi na dužim stavkama.

1) PR: merge dve code ispravke (P0)
- Promene:
  - `Infrastructure/Services/AnalyticsDataQualityHistoryService.cs` — ispraviti poziv `ExecuteSqlRawAsync` tako da prima `object[] parameters` i `CancellationToken` kao *separate* argument, izbeći ubacivanje tokena u SQL parametre.
  - `Infrastructure/Seed/DatabaseInitializer.cs` — promeniti deferred backfill da koristi `AddDbContextFactory<TContext>` ili da konstruise nove kontekste iz connection stringa (ne reuse DI-scoped kontekte preko dužih taskova).
- Test: lokalni run sa Neon config; posmatrati logove (deferred backfill, data-quality worker).

2) Dodati unit / integration testove (P1)
- Test cases:
  - Verify `ExecuteSqlRawAsync` param usage (može se testirati kroz mock ili integracioni test sa test Postgres)
  - Parallel backfill integration test: više taskova koriste `IDbContextFactory` i ne bacaju SocketException
- Framework: xUnit + Testcontainers ili lokalni Postgres/Neon test DB

3) Implementirati `IFileStorage` (P0)
- Phase 1 (quick win): dodati `IFileStorage` + `LocalFileStorage` i koristiti DI samo za durable storage tokove; postojeći behavior ostaje isti kada je provider=`local`.
- Phase 2: dodati `S3FileStorage` (AWSSDK.S3 ili Minio client), konfiguracija preko `StorageOptions`, uz download-to-temp obrazac za AccessImport worker.
- Data model: proširiti `Domain/Model/DataImportBatch.cs` sa nullable poljima za storage key/provider; ne rušiti postojeći `SourceFilePath`.
- Queue / repair flow: proširiti `Api/Services/Access/AccessImportJobQueue.cs` i `Api/Services/NivelacijaRepairService.cs` da umeju da rade i sa storage-backed batch-evima.
- Migrate: napraviti migracioni job/skriptu koja kopira postojeće fajlove u bucket i ažurira nove DB reference.

4) `PROCESS_TYPE` / worker control (P0)
- Dodati env var `PROCESS_TYPE=web|worker`.
- Zadržati `Workers:Enabled` kao postojeći runtime safety toggle; `WORKER_PROCESS` eventualno koristiti samo kao compatibility alias.
- U `Program.cs` uslovno registrovati hosted services; minimalni P0 nije poseban worker-only host, nego da web proces jednostavno ne registruje kritične background servise.
- `DeferredStartupTasksHostedService` i drugi startup/background taskovi moraju biti worker-only ili striktno gated.

5) Napisati `docs/db-migration-runbook.md` (P0)
- Sadrži extension preflight, pg_dump/pg_restore steps, logical replication steps, rollback i minimal downtime cutover plan, uključujući proveru i za sekundarne connection stringove ako koriste poseban DB (`OpenProductTrainingConnection`).

6) CI skeleton: build-and-push image (P1)
- GitHub Actions + `docker/build-push-action` → push sha-tag i latest-tag.

---

## 4) DETALJAN TECH PLAN ZA `IFileStorage` (primer implementacije)

### Lokacija
- Interface: `Application/Common/Interfaces/IFileStorage.cs`
- Implementacije: `Infrastructure/Storage/LocalFileStorage.cs`, `Infrastructure/Storage/S3FileStorage.cs`
- Options: `Infrastructure/Configuration/StorageOptions.cs`
- DI: Api/Program.cs

### `IFileStorage` interfejs (C#)
```csharp
public interface IFileStorage
{
    Task UploadAsync(string key, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default);
}
```

### `StorageOptions`
```csharp
public class StorageOptions
{
    public string Provider { get; set; } // local | s3
    public string LocalBasePath { get; set; }
    public string Bucket { get; set; }
    public string Endpoint { get; set; }
    public string Region { get; set; }
    public string AccessKey { get; set; }
    public string SecretKey { get; set; }
    public bool UsePathStyle { get; set; } = true;
}
```

### DI i selekcija impl
```csharp
builder.Services.Configure<StorageOptions>(configuration.GetSection("Storage"));
var provider = configuration["Storage:Provider"] ?? "local";
if (provider == "s3") builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
else builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
```

### Refactor AccessImport (praktično)
1. Pokrenuti `git grep -n "File\.|FileStream|Path.Combine"` i prikupiti sve use-caseove.
2. Prvi talas refaktora fokusirati na durable tokove: `Api/Endpoints/AccessImportEndpoints.cs`, `Api/Services/AccessImportService.cs`, `Api/Endpoints/AllEndpoints.cs` (product images), `Infrastructure/Services/Documents/DocumentStorage.cs`.
3. Za AccessImport ne menjati obradu tako da radi direktno nad stream-om iz object storage-a; umesto toga čuvati source fajl u `IFileStorage`, a worker ga pre procesiranja spušta u lokalni temp/work folder.
4. Proširiti `DataImportBatch` i claim logiku tako da batch može imati i lokalnu putanju i storage key, bez loma postojećeg enqueue/repair toka.
5. Implementirati migracioni job koji kopira postojeće fajlove iz lokalnog direktorijuma u bucket i ažurira DB zapise.
6. Privremeno držati `LocalFileStorage` za dev scenu i kao fallback za postepeni rollout.

### Presigned URL flow
- Server: `GetPresignedUrlAsync(key, expiry)` → vraća URL za direktan upload/download od strane klijenta.
- Kada koristiti: kada fajlovi mogu biti veliki i želite da izbegnete proxying kroz app.

---

## 5) DETAJLAN PLAN ZA WORKER PROCESS MODEL

### Preporučeni model
- Env var `PROCESS_TYPE` sa vrednostima `web` ili `worker`.
- `WORKER_PROCESS` podržati samo kao alias radi kompatibilnosti sa postojećim env naming-om, ali standardizovati `PROCESS_TYPE`.
- Variant A: jedan image, dva procesa (start param + env var) — brzo i lako za CI.
- Variant B: potpuno odvojeni deployment manifests (web app + worker app) koji koriste isti image, sa različitim env var-ovima — bolja operativna kontrola.

### Program.cs: uslovna registracija (primer)
```csharp
var processType = builder.Configuration["PROCESS_TYPE"]
    ?? (builder.Configuration.GetValue<bool?>("WORKER_PROCESS") == true ? "worker" : "web");
var isWorker = processType.Equals("worker", StringComparison.OrdinalIgnoreCase);
var workersEnabled = builder.Configuration.GetValue<bool?>("Workers:Enabled")
    ?? isWorker
    || builder.Environment.IsDevelopment();

// Common services
builder.Services.AddDbContextFactory<TrendplusDbContext>(o => o.UseNpgsql(connStr));
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(new WorkerRuntimeOptions
{
    Enabled = workersEnabled,
    AllowRuntimeToggle = builder.Configuration.GetValue<bool?>("Workers:AllowRuntimeToggle")
        ?? builder.Environment.IsDevelopment()
});

if (isWorker) {
    builder.Services.AddHostedService<AccessImportBackgroundWorker>();
    builder.Services.AddHostedService<DeferredStartupTasksHostedService>();
    // register only worker-specific hosted services
}
```

### Sprečavanje duplicate runs
- Najbolja opcija: separate worker deploy (jedan ili više worker replica koji su nezavisni od web replikacija).
- Ako to nije moguće odmah: implementirati leader election / distributed lock:
  - Postgres advisory lock (jednostavno i bez eksternih servisa):
```csharp
await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync(ct);
await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock(123456789)", conn);
var locked = (bool)await cmd.ExecuteScalarAsync(ct);
if (!locked) return; // another instance holds the lock
```
- Alternativa: use Redis/etcd for leader election.

### DbContext u workers
- Use `AddDbContextFactory<T>` i create short-lived contexts per unit of work.
- Ne držati scoped DbContext instance kroz dugačke pozadinske zadatke.

---

## 6) CI / CD PLAN ZA MULTI-PROVIDER DEPLOYMENT

Cilj: jedan image, reproducibilan build, lako deploy-ovati na Render i Fly.

### Tagging + artifact
- Tag image: `${REGISTRY}/trendplus:${GIT_SHA}` i `:latest` (opcionalno `:staging`, `:prod`).

### Secrets
- Registry creds, DB connection strings, S3 keys, provider-specific secrets.
- Ne commitovati lokalne override fajlove.

### GitHub Actions skeleton
(videti u repou: .github/workflows/build-and-push.yml)
```yaml
name: Build and push image
on:
  push:
    branches: [ main ]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: docker/setup-buildx-action@v2
      - uses: docker/login-action@v2
        with:
          registry: ${{ secrets.REGISTRY_URL }}
          username: ${{ secrets.REGISTRY_USER }}
          password: ${{ secrets.REGISTRY_PASS }}
      - uses: docker/build-push-action@v4
        with:
          context: .
          push: true
          tags: ${{ secrets.REGISTRY_URL }}/trendplus:${{ github.sha }}
          platforms: linux/amd64,linux/arm64
```

### Deploy na Render i Fly
- Render: napraviti dva servisa (web i worker) koristeći isti image; podesiti env `PROCESS_TYPE`. To praktično znači da `render.yaml` treba uskladiti sa image-based deployment modelom umesto source build/start varijante.
- Fly: koristiti dva app entry-a ili `fly deploy --image` sa različitim env var-ovima.

---

## 7) DETAJLAN PLAN PORTABILNOSTI BAZE (Neon → drugi provider)

### Preflight (obavezno)
- Lista dostupnih ekstenzija:
```bash
psql "host=$HOST user=$USER dbname=$DB" -c "SELECT name, default_version FROM pg_available_extensions WHERE name IN ('pg_trgm','vector');"
psql "host=$HOST user=$USER dbname=$DB" -c "SELECT extname FROM pg_extension;"
```
- Test `CREATE EXTENSION` (u test DB sa odgovarajućim privilegijama):
```sql
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS vector; -- (ili tačan naziv instalirane ekstenzije)
```
- Ako ne radi → target provajder ne dozvoljava; preispitati plan.
- Istu proveru uraditi nad svakim zasebnim connection string-om koji vodi na poseban Postgres, posebno ako `OpenProductTrainingConnection` nije isti DB kao glavni aplikacioni connection string.

### Kada pg_dump/pg_restore
- Koristiti za male/srednje baze (< ~100GB) ili gde downtime može biti tolerisan.
- Komande:
```bash
pg_dump -Fc -h <src> -U <user> -d <db> -f dumpfile.dump
createdb -h <tgt> -U <user> <newdb>
pg_restore -d <newdb> -h <tgt> -U <user> -j 8 dumpfile.dump
```
- Nakon restore: `REINDEX`, `VACUUM ANALYZE`.

### Kada logical replication
- Koristiti za velike baze i low-downtime cutover.
- Koraci:
  1. Source: `CREATE PUBLICATION mypub FOR ALL TABLES;`
  2. Target: create DB + ensure extensions present
  3. Target: `CREATE SUBSCRIPTION mysub CONNECTION 'host=... dbname=... user=... password=...' PUBLICATION mypub;`
  4. Monitor `pg_stat_subscription` lag; quiesce application writes; final cutover.
- Napomena: extensions must exist on target before subscription if database objects depend on them.

### Rollback
- Držati source DB live dok se ne potvrdi funkcionisanje targeta.
- Swap connection string (config flip) za rollback; ako ne može — restore latest dump iz S3.

### Testiranje performansi
- Pokrenuti `EXPLAIN ANALYZE` na kritičnim upitima i porediti planove.
- Pokrenuti smoke tests + automated functional tests.
- Ako postoji razlika u planovima, proveriti statistike, indeksiranje i `pg_trgm`/vector support.

---

## 8) NAJVEĆE PREPREKE I NJIHOVA REŠENJA (detaljno)

1) Missing DB extensions / CREATE EXTENSION restrictions
- Root cause: managed provajder ograničava ekstenzije.
- Simptomi: `CREATE EXTENSION` error, queries/indexes failing.
- Rešenje: izabrati provajdera koji podržava ekstenzije ili ekstraktovati vector workload van PostGresa.
- Hitnost: P0.

2) Local filesystem dependency
- Root cause: apps write files to container FS.
- Simptomi: lost files after restart, inconsistent state.
- Rešenje: `IFileStorage` i migracija durable fajlova u centralizovanu S3-compatible lokaciju; lokalni disk zadržati samo za temp/work fajlove tokom obrade.
- Hitnost: P0.

3) Background jobs inside web process
- Root cause: hosted services start on every replica.
- Simptomi: duplicate jobs, DB contention.
- Rešenje: `PROCESS_TYPE` switch OR separate worker service; add advisory locks as safety.
- Hitnost: P0/P1.

4) Under-tuned pooling / connection handling
- Root cause: default pool and reuse DbContext across tasks.
- Simptomi: Npgsql socket exceptions, connection refused from provider.
- Rešenje: `AddDbContextFactory`, tune `MaxPoolSize`, `EnableRetryOnFailure`, consider PgBouncer.
- Hitnost: P1.

---

## 9) KONKRETNE PREPORUKE ZA .NET I POSTGRESQL (praktično)

- IConfiguration / Options:
  - Typed options + validation.
  - Izolovati provider-specific sekcije.

- Connection string management:
  - Centralizovati u `ConnectionStrings`.
  - Primer tuninga sa `NpgsqlConnectionStringBuilder` (MaxPoolSize, ConnectionIdleLifetime).

- AddDbContextFactory / pooling:
  - Background workers -> `IDbContextFactory<T>`; Web -> `AddDbContextPool<T>` ili per-request `AddDbContext`.

- Retry / transient faults:
  - `EnableRetryOnFailure` i Polly za cross-cutting retries.

- Migration strategy:
  - Prefer CI-driven migrations; avoid long-running migrations on web startup.

- Health checks:
  - Readiness: DB connectivity, replication lag; Storage: S3 head-object.

- Storage abstraction:
  - `IFileStorage` + Local/S3 impl; presigned URLs za direktan client upload.

- Provider-specific code isolation:
  - `Database/Providers/<provider>/` za specijalne SQL skripte.

---

## 10) FINALNI ZAKLJUČAK

- Trenutna portabilnost:
  - Backend: visoko portable (8/10).
  - Baza: umeren (4–6/10) — zavisi od ekstenzija i veličine podataka.

- Minimum da sistem postane low-risk:
  1. `IFileStorage` + migracija fajlova
  2. DB extension preflight + runbook
  3. `PROCESS_TYPE` + `AddDbContextFactory`
  4. CI build-and-push + smoke/integration tests

---

## 11) SLEDEĆI KONKRETNI KORACI (hitno — izvršni)

1. Otvori branch `fix/backfill-dbcontext-execsql` i commit-uj izmene za:
   - Infrastructure/Services/AnalyticsDataQualityHistoryService.cs
   - Infrastructure/Seed/DatabaseInitializer.cs

2. Pokreni lokalni integracioni test ciljano za backfill worker.

3. Dodaj skeleton `IFileStorage` + `LocalFileStorage` i DI registrovanje (branch `feat/ifilestorage-skeleton`).

4. U istom PR-u sa storage skeleton-om proširi:
   - `Domain/Model/DataImportBatch.cs`
   - `Api/Services/Access/AccessImportJobQueue.cs`
   - `Api/Services/NivelacijaRepairService.cs`
   - da podrže storage-backed batch bez loma postojećeg `SourceFilePath` flow-a.

5. Dodaj `PROCESS_TYPE` handling u `Api/Program.cs` i prebaci `DeferredStartupTasksHostedService` / ostale kritične hosted servise na worker-only registraciju.

6. Refaktoriši durable file tokove van AccessImport-a:
   - `Api/Endpoints/AllEndpoints.cs` za product image upload
   - `Infrastructure/Services/Documents/DocumentStorage.cs` za dokumente

7. Napiši `docs/db-migration-runbook.md` i odsledi ga kroz dry-run na staging.

8. Dodaj `.github/workflows/build-and-push.yml` (skeleton) i pokreni CI za build/push image.

9. Pokreni repo grep-ove i priloži rezultate:
   - `git grep -n "pgvector\|pg_trgm\|vector"`
   - `git grep -n "File\.|FileStream|Path.Combine"`
   - `git grep -n "AddHostedService"`

10. Ako target provajder ne podržava `pgvector`, kreirati proof-of-concept za ekstrakciju vector workloads u Milvus/Weaviate.

---

Kraj fajla.
