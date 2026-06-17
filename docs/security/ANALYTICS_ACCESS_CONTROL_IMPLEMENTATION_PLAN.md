# Analytics P0 Access Control Implementation Plan

Datum: 2026-06-17
Repo: `ivanjovicic/Trendplus`
Scope: plan za P0 zastitu opasnih analytics/admin endpointa bez auth refaktora u ovom tasku

## Svrha

Pre spoljnog pilota ili customer upotrebe, Trendplus mora da zatvori write/destructive analytics i admin surface.

Ovaj plan:

1. definise P0 endpoint grupe
2. uvodi minimalan role model
3. mapira gde backend mora da enforce-uje pristup
4. mapira sta frontend sme da prikazuje po roli
5. navodi testove za svaku grupu
6. razbija implementaciju na male, bezbedne korake

## Procitano

- `docs/security/ANALYTICS_ACCESS_CONTROL_AUDIT.md`
- `docs/security/TENANT_SAFETY_CHECKLIST.md`
- `Api/Program.cs`
- `Api/Endpoints/AllEndpoints.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/AccessImportEndpoints.cs`
- `Api/Endpoints/AccessImportRestoreEndpoints.cs`
- `Api/Endpoints/WorkerConfigurationEndpoints.cs`
- `Api/Endpoints/AdminConfigEndpoints.cs`
- `Api/Endpoints/AdminBackendRoutingEndpoints.cs`
- `Api/Endpoints/AnalyticsActionsEndpoints.cs`
- `Api/Endpoints/AnalyticsReportsEndpoints.cs`
- `Api/Endpoints/DocumentEndpoints.cs`
- `Api/Endpoints/InventoryEndpoints.cs`
- `Api/Endpoints/RedisEndpoints.cs`
- `Api/Endpoints/AnalyticsSnapshotEndpoints.cs`
- `Api/Endpoints/AdminRepairEndpoints.cs`
- `Klijent/clientapp/src/App.tsx`
- `Klijent/clientapp/src/layout/navConfig.ts`

## Danasnje ogranicenje

Bitna osnova za implementaciju:

- `Api/Program.cs` ima `UseAuthorization()`, ali nema potvrden `AddAuthentication()` / `UseAuthentication()` sloj u ovom scope-u.
- Deo endpointa je potpuno javan.
- Deo endpointa koristi ad-hoc `IsAdminRequest(...)` / `X-Admin-Key`.
- Frontend nema route guard sloj; `App.tsx` mapira rute direktno, a `navConfig.ts` prikazuje analytics i admin stavke bez role filtriranja.

Zakljucak:

- Phase 1 ne uvodi novi auth sistem.
- Phase 1 mora da radi sa postojecim `HttpContext.User` kada postoji.
- Gde role nisu prisutne, opasni endpointi moraju fail-closed u production-u, uz ogranicen break-glass admin-key fallback samo za Admin operacije koje ga vec imaju.

## Role model

### Viewer

- sme da cita read-only analytics ekrane i status signale
- ne sme da menja analytics state
- ne sme da pokrece refresh, cache clear, import, worker control ili export sa customer podacima

### Analyst

- ima sve kao Viewer
- sme da koristi read-heavy analytics detalje
- sme da kreira non-destructive radne stavke kada je to dozvoljeno
- ne sme da pokrece admin, import, worker, refresh ili cache operacije

### Manager

- ima sve kao Analyst
- sme da menja poslovne action/outcome statuse
- sme da generise customer report/export output
- ne sme da upravlja workerima, importom, admin konfiguracijom ili destructive infrastrukturom

### Admin

- ima sve kao Manager
- sme da radi refresh, cache clear, import/cleanup, worker control i admin konfiguraciju
- jedina rola za destructive system-state operacije

## Minimalna pravila za Phase 1

1. Read-only analytics ostaje otvoren za `Viewer`.
2. Opasni write/destructive endpointi dobijaju server-side `Manager` ili `Admin` gate.
3. Frontend sakriva admin i destructive akcije za role ispod trazene.
4. Frontend route direktan pristup admin ekranima mora da vrati deny/redirect, ne samo da sakrije nav.
5. `X-Admin-Key` fallback ostaje samo kao privremeni break-glass mehanizam za Admin endpointe koji ga vec koriste.
6. Nema novog auth providera niti token sistema u ovom tasku.

## P0 endpoint grupe

### Tabela plana

| Group | Representative endpoints / UI | Current access | Required role | Backend enforcement location | Frontend visibility rule | Tests required |
|---|---|---|---|---|---|---|
| Manual analytics refresh and repair | `/api/admin/run-analytics-optimization`, `/api/analytics/optimize`, `/api/admin/init-scoring-tables`, `/api/admin/sync-analytics-db`, `/admin/repair/*`, snapshot generate/activate endpoints | Mesano: neke rute su javne, neke admin-key | Admin | `Api/Endpoints/AllEndpoints.cs`, `Api/Endpoints/AdminRepairEndpoints.cs`, `Api/Endpoints/AnalyticsSnapshotEndpoints.cs` | Dugmad i admin panels samo za Admin; direktna ruta deny za ostale | Backend 401/403/200 matrix; frontend hidden buttons; route deny smoke |
| Clear analytics cache | `/api/analytics/cached/cache/invalidate`, `/api/redis/toggle` | Javno | Admin | `Api/Endpoints/CachedAnalyticsEndpoints.cs`, `Api/Endpoints/RedisEndpoints.cs` | Nema cache-control UI za non-Admin; infra toggles nikad u regular analytics nav | Backend deny for Viewer/Analyst/Manager; admin allow; no cache side effect on deny |
| Import / access import | `/access-import`, `/api/access-import/*`, cleanup execute/archive export/delete/restore-script | Read delovi uglavnom javni; write/destructive delovi mesano admin-key i javno | Admin | `Api/Endpoints/AccessImportEndpoints.cs`, `Api/Endpoints/AccessImportRestoreEndpoints.cs` | `/access-import` i import akcije samo za Admin; non-Admin ne vidi ulaznu tacku | Backend deny for non-Admin on POST/DELETE cleanup/import; UI nav hidden; direct route guard |
| Worker control | `/api/workers/control/*`, `/api/workers/{workerName}/start|stop|restart|schedule/*`, `/api/admin/workers/*`, `/api/workers/configuration` | Read delovi javni; write delovi admin-key; ponegde dev-open | Admin | `Api/Endpoints/AllEndpoints.cs`, `Api/Endpoints/WorkerConfigurationEndpoints.cs`, `Api/Endpoints/AdminConfigEndpoints.cs` | Worker panel i `/admin/configuration` worker sekcija samo za Admin | Backend deny matrix; worker read visibility tests; UI hidden controls |
| Admin configuration | `/admin/configuration`, `/api/admin/pending-batches`, `/api/admin/requeue-batch/*`, `/api/admin/run-stale-recovery`, `/api/admin/audit-log`, `/api/admin/backend-routing/*`, `/api/logs/clear` | Frontend route javna; backend mesano javno i admin-key | Admin | `Api/Endpoints/AdminConfigEndpoints.cs`, `Api/Endpoints/AdminBackendRoutingEndpoints.cs`, `Api/Endpoints/AllEndpoints.cs` | Admin nav i route samo za Admin; non-Admin redirect ili 403 page | Backend deny for all non-Admin; nav hidden; App route smoke |
| Destructive action updates | `POST /api/analytics/actions`, `PATCH /api/analytics/actions/{id}/status`, `PATCH /api/analytics/actions/{id}/outcome`, inventory action decision writes, inventory report-schedule writes/run-now | Uglavnom javno, oslanja se samo na user context ako postoji | Manager za status/outcome/report schedule; Analyst za create/upsert samo ako se potvrdi business rule; Admin override | `Api/Endpoints/AnalyticsActionsEndpoints.cs`, `Api/Endpoints/InventoryEndpoints.cs` | Viewer ne vidi create/update akcije; Analyst vidi create only ako zadrzimo to pravilo; Manager/Admin vide status/outcome/schedule controls | Backend role split tests; component/nav button visibility tests; negative tests da Viewer ne moze write |
| Report / export endpoints with customer data | `/api/analytics/reports/*`, `/api/documents/*`, `/api/exports*`, `/api/analytics/inventory/export`, `/api/analytics/inventory/print-preview`, inventory schedule delivery setup | Report reads javni; document listing ima ownership filter ali ne i role gate; generation/export otvoreni | Manager | `Api/Endpoints/AnalyticsReportsEndpoints.cs`, `Api/Endpoints/DocumentEndpoints.cs`, `Api/Endpoints/InventoryEndpoints.cs` | Report/export links i export buttons samo za Manager/Admin; Viewer moze da vidi analytics ekran bez export CTA | Backend deny for Viewer/Analyst on generate/export; Manager allow; ownership tests remain intact |

## Grupa po grupa

### 1. Manual analytics refresh and repair

Obuhvat:

- `/api/admin/run-analytics-optimization`
- `/api/analytics/optimize`
- `/api/admin/init-scoring-tables`
- `/api/admin/sync-analytics-db`
- `/admin/repair/nivelacije/*`
- `/api/analytics/snapshots/batches/*` write operacije

Current access:

- deo je potpuno javan
- deo koristi `X-Admin-Key`
- nema jedinstvenog `Admin` role gate-a

Required role:

- `Admin`

Backend enforcement location:

- direktno u endpoint registraciji u navedenim fajlovima
- preporuka za implementation task:
  - uvesti shared helper za endpoint guard poziv
  - zameniti lokalne `IsAdminRequest(...)` provere jednim konzistentnim helper-om gde je moguce

Frontend visibility rule:

- nikakav refresh/repair CTA ne sme biti vidljiv ispod `Admin`
- linkovi ka `/admin/configuration?panel=workers` ostaju, ali panel akcije moraju biti role-gated

Tests required:

- backend:
  - anonymous -> deny
  - Viewer -> deny
  - Manager -> deny
  - Admin -> allow
- frontend:
  - non-Admin ne vidi refresh/repair dugmad
  - direct admin route access vraca deny state

### 2. Clear analytics cache

Obuhvat:

- `POST /api/analytics/cached/cache/invalidate`
- `POST /api/redis/toggle`

Current access:

- javno

Required role:

- `Admin`

Backend enforcement location:

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/RedisEndpoints.cs`

Frontend visibility rule:

- cache clear dugme i Redis toggle nikad ne smeju biti prikazani regularnim analytics korisnicima

Tests required:

- backend deny/allow po roli
- backend test da unauthorized poziv ne menja cache clear state / redis enabled state
- frontend hidden-control test

### 3. Import / access import

Obuhvat:

- `/access-import`
- `POST /api/access-import/jobs`
- `POST /api/access-import/run`
- `POST /api/access-import/preview`
- `DELETE /api/access-import/batches/{id}`
- cleanup preview/execute/archive export/restore-script
- enqueue/cancel i ostale write operacije

Current access:

- read/status delovi su siroko otvoreni
- destruktivni delovi su delom admin-key, delom javni
- development okruzenje cesto prolazi bez zastite

Required role:

- `Admin`

Backend enforcement location:

- `Api/Endpoints/AccessImportEndpoints.cs`
- `Api/Endpoints/AccessImportRestoreEndpoints.cs`

Frontend visibility rule:

- nav stavka i ruta `/access-import` samo za `Admin`
- non-Admin ne sme da vidi import, cleanup ili archive export akcije

Tests required:

- backend:
  - non-Admin deny za sve write/import/cleanup/delete operacije
  - Admin allow
- frontend:
  - nav hidden za non-Admin
  - direct route guard za `/access-import`

### 4. Worker control

Obuhvat:

- `POST /api/workers/control/enable|disable`
- `POST /api/workers/{workerName}/start|stop|restart|schedule/*`
- `POST /api/admin/workers/{workerName}/resume|stop|schedule/*`
- `GET /api/workers/configuration`
- `GET /api/admin/workers/*`

Current access:

- read konfiguracija/status delovi su javni
- write delovi su mesano admin-key i dev-open

Required role:

- `Admin`

Backend enforcement location:

- `Api/Endpoints/AllEndpoints.cs`
- `Api/Endpoints/WorkerConfigurationEndpoints.cs`
- `Api/Endpoints/AdminConfigEndpoints.cs`

Frontend visibility rule:

- worker control panel samo za `Admin`
- eventualni read-only worker health moze ostati sakriven od regularnih korisnika i ostati dostupan samo admin sekciji u phase 1

Tests required:

- backend deny/allow matrix
- backend test da dev/prod fallback vise ne ostavlja endpoint otvoren za non-Admin
- frontend route/nav test za admin configuration panel

### 5. Admin configuration

Obuhvat:

- `/admin/configuration`
- `/api/admin/pending-batches`
- `/api/admin/requeue-batch/{batchId}`
- `/api/admin/run-stale-recovery`
- `/api/admin/health-check`
- `/api/admin/audit-log`
- `/api/admin/backend-routing/*`
- `DELETE /api/logs/clear`

Current access:

- frontend admin route je javna
- backend je mesavina javnih i admin-key ruta

Required role:

- `Admin`

Backend enforcement location:

- `Api/Endpoints/AdminConfigEndpoints.cs`
- `Api/Endpoints/AdminBackendRoutingEndpoints.cs`
- `Api/Endpoints/AllEndpoints.cs`

Frontend visibility rule:

- admin nav grupa i `/admin/configuration` ruta samo za `Admin`
- non-Admin dobija redirect na `/analytics` ili dedicated 403 screen

Tests required:

- frontend:
  - nav group hidden
  - route smoke deny
- backend:
  - deny for all non-Admin roles
  - allow for Admin

### 6. Destructive action updates

Obuhvat:

- `POST /api/analytics/actions`
- `POST /api/analytics/actions/status`
- `PATCH /api/analytics/actions/{id}/status`
- `PATCH /api/analytics/actions/{id}/outcome`
- `POST /api/analytics/inventory/action-suggestions/{suggestionKey}/decision`
- `POST|PUT /api/analytics/inventory/report-schedules*`
- `POST /api/analytics/inventory/report-schedules/{id}/run-now`

Current access:

- write rute su uglavnom javne
- oslanjaju se na `IDocumentUserContextAccessor` ili `HttpContext.User` samo za identitet, ne i za dozvolu

Required role:

- `Manager` za status/outcome/decision/schedule write
- `Analyst` za `POST /api/analytics/actions` samo ako proizvodni owner potvrdi da Analyst sme da otvara akcije
- `Admin` override za sve

Open question for implementation:

- da li `POST /api/analytics/actions` treba da bude `Analyst+` ili odmah `Manager+`
- preporuka za phase 1:
  - `Manager+` za sve destructive action writes
  - eventualni Analyst create ostaviti za follow-up ako je potreban

Backend enforcement location:

- `Api/Endpoints/AnalyticsActionsEndpoints.cs`
- `Api/Endpoints/InventoryEndpoints.cs`

Frontend visibility rule:

- Viewer ne vidi create/update CTA
- Analyst vidi read-only action stanje
- Manager/Admin vide status/outcome i inventory schedule akcije

Tests required:

- backend role matrix po endpointu
- frontend komponentni testovi za skrivanje action dugmadi
- regression test da Viewer ne moze da upise odluku ni direktnim pozivom

### 7. Report / export endpoints with customer data

Obuhvat:

- `/api/analytics/reports/supplier-decision`
- `/api/analytics/reports/pilot-intake`
- `POST /api/documents/generate`
- `POST /api/documents/batch`
- `POST /api/documents/print-preview`
- `GET /api/exports`
- `GET /api/exports/{jobId}/status`
- `GET /api/documents/{id}`
- `GET /api/documents/{id}/print`
- `POST /api/analytics/inventory/export`
- `POST /api/analytics/inventory/print-preview`

Current access:

- report routes su read-open
- document list/download koristi ownership filtering, ali nema role gate
- generate/export endpointi su otvoreni

Required role:

- `Manager`
- `Admin` override

Backend enforcement location:

- `Api/Endpoints/AnalyticsReportsEndpoints.cs`
- `Api/Endpoints/DocumentEndpoints.cs`
- `Api/Endpoints/InventoryEndpoints.cs`

Frontend visibility rule:

- Viewer i Analyst vide analytics ekran, ali ne vide export/report generation CTA
- Manager/Admin vide report/export CTA
- stable report URL direktan pristup mora vratiti deny ako rola nije dovoljna

Tests required:

- backend deny/allow matrix
- ownership testovi ostaju i dalje prolaze za Manager/Admin
- frontend hidden export buttons
- route smoke za report pages sa deny stanjem za nedovoljne role

## Backend enforcement strategy za implementation taskove

Phase 1 preporuka:

1. Ne dirati globalni auth sistem.
2. Uvesti mali shared access helper za analytics/admin endpointe.
3. Helper treba da podrzi:
   - `Viewer`
   - `Analyst`
   - `Manager`
   - `Admin`
   - privremeni admin-key fallback samo za `Admin` operacije koje ga danas vec koriste
4. Endpointi treba da fail-closed:
   - anonymous bez validnog role/admin-key -> deny
   - rola ispod trazene -> deny
5. U implementation taskovima zadrzati postojece route shape-ove.

Preporucena deny semantika:

- API:
  - `401` ako nema identiteta
  - `403` ako identitet postoji ali rola nije dovoljna
- UI:
  - sakriti link/dugme
  - direct route access prikazati 403/redirect, ne blank screen

## Frontend Phase 1 pravila

### Nav

- `Klijent/clientapp/src/layout/navConfig.ts` dobija role metadata po grupi ili item-u
- admin i import stavke se filtriraju pre renderovanja
- analytics read-only stavke ostaju vidljive `Viewer+`

### Route guards

- `Klijent/clientapp/src/App.tsx` dobija lagani route gate za:
  - `/admin/*`
  - `/access-import`
  - report/export pages ako imaju dedicated rute
- ne raditi broad routing refaktor

### Page-level controls

- dugmad za refresh/cache/import/export/action update se sakrivaju po roli
- i dalje ostaje obavezna backend provera

## Test plan po sloju

### Backend tests

Za svaku grupu dodati:

1. anonymous deny
2. insufficient role deny
3. required role allow
4. admin allow
5. state does not change on deny

Minimalni prioritet test fajlova:

- `Api.Tests/*AccessControl*`
- ili endpoint-spec testovi pored postojecih endpoint testova za:
  - access import
  - worker config/control
  - analytics actions
  - cache invalidate
  - document/export

### Frontend tests

Dodati:

1. nav visibility tests
2. route guard smoke tests
3. critical button visibility tests

Minimalni prioritet:

- `Klijent/clientapp/src/layout/__tests__/navConfig.spec.ts`
- route smoke test pored `App` route testova
- komponentni testovi za admin panels i action controls

## Implementacioni redosled

Preporucen P0 redosled:

1. Worker control + admin configuration
2. Cache invalidate + Redis toggle
3. Manual analytics refresh and repair
4. Access import write/destructive routes
5. Reports/export
6. Destructive action updates
7. Frontend nav/route hiding for sve gore navedeno

Zasto ovim redom:

- prvo se zatvaraju sistemske i infrastrukturne operacije
- zatim destructive data/import putanje
- zatim business write i export surface

## Out of scope za ovaj plan

- novi auth provider
- token issuance
- multi-tenant auth model
- potpuni RBAC refaktor kroz ceo repo
- fine-grained per-customer policy model

## Sledeci taskovi

Najmanji sledeci implementacioni taskovi mogu biti:

1. `Worker/Admin P0 guard`
2. `Cache invalidate/Admin refresh P0 guard`
3. `Access import P0 guard`
4. `Report/export P0 guard`
5. `Analytics actions P0 guard`

Svaki od ovih taskova moze da se uradi zasebno, sa malim brojem fajlova i jasnim acceptance kriterijumima.
