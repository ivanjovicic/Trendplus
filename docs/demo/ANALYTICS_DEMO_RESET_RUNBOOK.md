# Analytics Demo Reset Runbook

Datum: 2026-06-17
Repo: `ivanjovicic/Trendplus`
Scope: Phase 1 demo reset/reseed plan bez uvodjenja opasnog production reset koda

## Svrha

Trendplus demo mora da bude ponovljiv:

1. reset demo stanja
2. seed demo podataka
3. analytics refresh
4. provera `Pilot spremnost`
5. manual smoke prolaz

Ovaj dokument opisuje sta danas postoji u repou, sta je bezbedno koristiti i sta jos nije potvrdjen automated demo flow.

## Executive summary

- Potvrdjen kompletan automated reset/reseed flow za shared demo/pilot okruzenje ne postoji.
- Potvrdjen lokalni seed helper postoji:
  - `scripts/seed_local_db.ps1`
- Potvrdjen startup/bootstrap seed pattern postoji:
  - `Infrastructure/Seed/DatabaseInitializer.cs`
  - `Infrastructure/Seed/TrendplusDbSeeder.cs`
- Potvrdjeni analytics refresh mehanizmi postoje:
  - `Workers/NightlyAnalyticsRefreshWorker.cs`
  - `Workers/AnalyticsAggregationWorker.cs`
  - `GET /api/analytics/refresh-status`
  - postoje i manual refresh/optimization endpointi, ali ih ne treba tretirati kao bezbedan production-like demo reset API
- Fazni zakljucak:
  - Phase 1 ostaje runbook + operator disciplina
  - bez novog reset koda
  - bez one-click destructive flow-a

## Sta trenutno postoji

### 1. Lokalni seed helper

Fajl:
- `scripts/seed_local_db.ps1`

Sta radi:
- cilja lokalni Docker Postgres container `trendplus-postgres`
- pusta SQL seed fajlove iz `Database/Migrations`
- namenjen je lokalnom/test okruzenju

Sta ne radi:
- ne proverava da li je okruzenje demo ili production
- ne pravi backup
- ne resetuje analytics ili akcije
- ne potvrduje pilot readiness

Zakljucak:
- bezbedan je samo kao lokalni, izolovani helper
- nije dovoljan kao shared demo reset flow

### 2. Startup/bootstrap seed pattern

Fajlovi:
- `Infrastructure/Seed/DatabaseInitializer.cs`
- `Infrastructure/Seed/TrendplusDbSeeder.cs`

Potvrdjeno:
- startup initializer moze da seed-uje bazu kada nema `Artikli`
- `TrendplusDbSeeder` kreira osnovne:
  - dobavljace
  - sezone
  - artikle
  - zalihe kroz `Kolicina`
  - prodaje sa `SEED-` prefiksom racuna

Ogranicenja:
- nije potvrden kao operator command za reset/reseed
- ne obuhvata kompletan analytics demo shape
- ne seed-uje potvrdeno:
  - intentional data quality issues
  - central actions/outcomes
  - report artifacts

### 3. Import i cleanup tokovi

Relevantno:
- `Api/Endpoints/AccessImportEndpoints.cs`
- `Api/Services/AccessImportService.cs`

Bitno:
- postoje import i cleanup mehanizmi
- postoje i destruktivni cleanup tokovi
- bezbednosna dokumentacija ih vec tretira kao riziicne admin operacije

Zakljucak:
- ne koristiti access-import cleanup kao demo "reset dugme"
- ne koristiti na customer ili shared pilot bazi bez eksplicitnog backup/approval procesa

### 4. Analytics refresh tokovi

Relevantno:
- `Api/Endpoints/AnalyticsRefreshStatusEndpoints.cs`
- `Workers/NightlyAnalyticsRefreshWorker.cs`
- `Workers/AnalyticsAggregationWorker.cs`
- `Api/Endpoints/AllEndpoints.cs`

Potvrdjeno:
- postoji `GET /api/analytics/refresh-status`
- postoje worker refresh tokovi
- postoje manual refresh/optimization endpointi

Ogranicenje:
- refresh endpointi postoje, ali zbog access-control i safety rizika nisu dobar osnov za genericki demo reset API u ovom koraku

## Safe demo reset pravila

Ova pravila su obavezna.

### Demo-only pravilo

Reset/reseed sme da se radi samo ako je sve ispod tacno:

1. okruzenje je namenski demo ili lokalni izolovani env
2. nije customer baza
3. nije production baza
4. operator moze da potvrdi gde podaci fizicki zive

Ako bilo koja stavka nije potvrdena:
- stop
- ne radi reset

### Backup/check pre destruktivne akcije

Pre bilo kakvog reset-a ili cleanup-a:

1. potvrdi poslednji dobar backup ili snapshot
2. zabelezi datum i vlasnika akcije
3. potvrdi koji DB je meta:
   - operativni DB
   - analytics DB
4. potvrdi da postoji rollback put

Ako rollback nije jasan:
- ne radi destruktivni korak

### Demo labeling pravilo

Demo podaci moraju biti jasno oznaceni.

Preporuceni label obrasci:
- artikli: `[DEMO] ...`
- dobavljaci: `[DEMO] ...`
- racuni: `DEMO-0001`, `DEMO-0002`
- akcije: naslov ili note sa `[DEMO]`
- report export ili snapshot naziv: `demo`

Ako podaci nisu jasno obelezeni:
- ne mesati ih sa customer podacima

### Production safety pravilo

Nije dozvoljeno:
- masovno brisanje shared/customer podataka
- uvodjenje novog reset endpointa bez environment gate-a
- oslanjanje na "cleanup" endpoint kao demo tooling bez dodatne zastite

## Minimalni demo dataset shape

Ovo je ciljni shape za demo/pilot tok.

### Obavezno

1. Products
   - 50-200 artikala
   - vise kategorija ili tipova
   - makar nekoliko artikala sa jasnim margin i stock signalom

2. Suppliers
   - 5-10 dobavljaca
   - makar jedan "dobar", jedan "rizan", jedan "za proveru"

3. Sales
   - 90 ili 180 dana prodaje
   - dovoljno signala za dashboard, supplier i product odluke
   - racuni ili prodajni redovi sa jasnim demo prefiksom ako se seed-uju direktno

4. Inventory
   - artikli sa stock stanjem
   - makar jedan OOS rizik
   - makar jedan dead stock ili spor obrt

5. Data quality issues
   - deo artikala bez dobavljaca
   - deo artikala bez nabavne cene
   - deo signala sa `insufficient_data`
   - warning/partial primer ako postoji bezbedan nacin

6. Actions/outcomes
   - makar jedna otvorena akcija
   - makar jedna zavrsena ili rejected akcija
   - makar jedan source link nazad na analytics ekran

### Sta je danas potvrdeno kao najblize ovom shape-u

- products: delimično potvrdeno
- suppliers: delimično potvrdeno
- sales: delimično potvrdeno
- inventory: delimično potvrdeno
- data quality issues: nije potvrden siguran automated seed pattern
- actions/outcomes: nije potvrden siguran automated seed pattern

Zakljucak:
- danas postoji parcijalni seed temelj
- puni analytics demo dataset jos nije kompletno automatizovan

## Preporuceni Phase 1 run order

### Put A: lokalni ili namenski izolovani demo env

1. Confirm environment
   - potvrdi da je env lokalni ili namenski demo
   - potvrdi da nije customer/prod

2. Snapshot / backup check
   - proveri da postoji backup ili disposable DB plan

3. Reset demo state
   - preferirani reset nije row-level cleanup
   - preferirano je:
     - restore cistog demo DB snapshot-a
     - ili recreate izolovanog demo DB instance/container-a

4. Seed demo base data
   - ako se koristi lokalni Docker DB helper:
   ```powershell
   .\scripts\seed_local_db.ps1
   ```
   - ako startup bootstrap seed radi u praznoj bazi, potvrdi da se inicijalizacija zavrsila bez greske

5. Seed or verify analytics-specific demo gaps
   - rucno potvrdi da postoje:
     - data quality problemi
     - inventory signal
     - supplier signal
     - product decision signal
     - actions/outcomes ako su deo demo price

6. Run analytics refresh
   - koristi postojeci, odobren admin/demo refresh put za to okruzenje
   - proveri:
     - `GET /api/analytics/refresh-status`
   - ne uvoditi novi reset API u ovom koraku

7. Verify Pilot Readiness
   - otvori:
     - `/analytics/pilot-readiness`
   - proveri da checklist stavke ne glume green stanje ako nesto nije spremno

8. Run manual smoke checklist
   - koristiti:
     - `docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md`

### Put B: shared pilot/customer-like env

Phase 1 preporuka:
- ne raditi automated reset
- ne raditi destruktivni cleanup kao demo convenience flow
- koristiti poseban demo deployment ili poseban demo DB

## Operativna odluka za Phase 1

Najbezbedniji podrzani model danas je:

1. dedicated demo environment
2. disposable DB ili known-good snapshot
3. lokalni/existing seed pattern
4. analytics refresh
5. Pilot Readiness verification
6. manual smoke checklist

## Preporuceni operator checklist

Pre reset/reseed:

- env potvrden kao demo-only
- backup ili disposable restore potvrden
- vlasnik akcije upisan
- plan rollback-a poznat

Posle seed-a:

- artikli postoje
- dobavljaci postoje
- prodaja postoji
- stock signal postoji
- makar jedan warning/data quality signal postoji
- akcije postoje ili je eksplicitno poznato da nisu jos seed-ovane

Posle refresh-a:

- `/api/analytics/refresh-status` ne pokazuje aktivan kvar
- dashboard radi
- Pilot Readiness radi
- smoke checklist prolazi bez fake green/fake zero problema

## Sta nije podrzano u ovom koraku

- one-click reset shared demo baze
- production-safe automated reseed flow
- automated actions/outcomes demo seed
- automated intentional data quality issue generator
- novi destructive API za demo reset

## Zasto nije dodat wrapper script

Wrapper nije dodat zato sto:

1. postojeci `scripts/seed_local_db.ps1` je lokalni Docker helper, ne generalni demo reset alat
2. nije potvrden bezbedan automated reset korak za shared env
3. puni demo dataset nije kompletno pokriven postojecim seed patternima
4. prioritet ovog koraka je bezbedan runbook, ne opasna automatizacija

## Sledeci mali implementacioni korak

Ako se bude radio Phase 2, najmanji bezbedan sledeci korak je:

1. demo-only wrapper oko lokalnog/disposable DB recreate + `scripts/seed_local_db.ps1`
2. explicit environment guard
3. post-seed refresh check
4. post-seed Pilot Readiness check
5. bez podrske za production ili customer DB
