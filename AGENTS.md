# AGENTS.md — Trendplus AI Agent Standard

Ovaj fajl je za Codex, Copilot agent mode i druge AI agente koji menjaju repo.

## Misija

Trendplus treba da postane pouzdan pilot/prodajni proizvod za maloprodaju obuće/odeće.

Analytics mora da pokaže:
- šta se prodaje
- gde je stvarna marža
- gde je mrtav lager
- koji dobavljači zaslužuju fokus
- koje podatke ne treba verovati
- koje akcije treba uraditi ove nedelje

Ne razvijati "još jedan ekran" ako postojeći ekran ne objašnjava period, refresh, data quality i razlog preporuke.

---

## Agent operating mode

### Pre rada

1. Pročitaj task.
2. Identifikuj tačno koje fajlove diraš.
3. Pronađi shared helper/component pre pisanja novog.
4. Proveri da li postoje guardrails/testovi.
5. Planiraj mali commit.

### Tokom rada

- Ne refaktoriši nepovezane oblasti.
- Ne uvodi novi pattern ako postoji stari standard.
- Ne ponavljaj istu neuspešnu komandu više puta.
- Ako build/test traje predugo ili zapne, prekini i probaj uži scope.
- Ako nisi siguran da li je broj 0 ili greška, tretiraj kao unknown/error.
- Ako se task proširi preko planiranog, stani i prijavi gap.

### Posle rada

U izveštaju napiši:
```text
Promenjeno:
- ...

Provere:
- dotnet build: pass/fail/not run
- dotnet test: pass/fail/not run
- npm run check:analytics-guardrails: pass/fail/not run
- npm run build: pass/fail/not run

Rizici:
- ...

Sledeće:
- ...
```

---

## Token / scope discipline

Za velike fajlove:
- koristi targeted search
- čitaj relevantne delove
- ne učitavaj ceo repo
- ne radi masovni rewrite
- ne generiši ogromne komponente ako može patch

Ako se izgubiš:
1. stani
2. napiši šta je potvrđeno
3. napiši šta nije potvrđeno
4. predloži najmanji sledeći korak

Ne nastavljaj naslepo.

---

## Obavezni standardi

### 1. No fake zero

Backend greška nikad ne sme izgledati kao validan `0 RSD`.

Backend:
- `AnalyticsResponseMetaFactory.Error(...)`
- ili `Results.Problem(...)`

Frontend:
- `AnalyticsErrorState`
- bez KPI nula na error

### 2. Empty nije error

Prazan uspešan dataset:
- `AnalyticsEmptyState`
- `meta.success=true`
- `emptyReason`
- `dataQualityStatus=insufficient_data` ako nema signala

### 3. Backend je source of truth

Backend vraća:
- recommendationStatus
- confidence
- reliability
- reasonCodes
- dataQualityStatus
- decision score ako postoji

Frontend prikazuje.

### 4. Shared formatteri

Koristi:
- `fmtRsd`
- `fmtPct`
- `fmtNumber`
- `fmtSignedPct` ako postoji

Ne pravi lokalne formattere.

### 5. Theme tokens

Koristi CSS variables. Ne hardkoduj boje.

### 6. UTF-8

Nema mojibake. Ako vidiš `Ä`, `Å`, `â`, `�`, popravi.

---

## Backend rules

Core analytics endpointi treba da imaju meta contract:

```csharp
AnalyticsResponseMetaDto
```

Standard:
- success with data -> `Success`
- success empty -> `Empty`
- fallback/partial/stale -> `Warning`
- error -> `Error` ili Problem

Ako response shape mora ostati isti, `Meta` neka bude optional dodatak.

### Error logging

Error logging ne sme izazvati novi 500.
- trim dugačke poruke
- correlationId
- full stack u log sink, safe summary u DB

---

## Frontend rules

Core analytics page treba:
- TrustHeader
- ErrorState
- EmptyState
- refresh/freshness
- data quality link
- methodology/help panel
- export/report ako ima smisla

Ne prikazuj raw backend code korisniku ako postoji mapping.

### Frontend routing guardrails

- Ne menjaj App.tsx iz lazy/Suspense u direktne import-e radi testa.
- Ako route smoke test ne radi sa lazy importima, popravi test/mocks, ne runtime routing.
- Ne uklanjaj legacy/admin compatibility rute bez replacement + redirect plana.
- Ne menjaj `ThemeProvider defaultTheme` u taskovima koji nisu theme/design-system.
- Za detaljna pravila koristi `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md`.

---

## Supplier Scorecard rules

Scorecard je često izvor regresija.

Obavezno:
- requested/effective period/dataset
- no silent fallback
- recommendationAllowed
- fallback warning
- pomoćni signal ako nije finalna preporuka
- no fake zero
- empty state sa razlozima

---

## Product Decision rules

Svaki product decision mora imati:
- status
- label
- action
- reason
- reasonCodes
- confidence/reliability
- data quality
- "Zašto?"

Ne prikazuj preporuku bez razloga.

---

## Inventory rules

Inventory ekran treba da vodi odluku:
- dopuni
- OOS rizik
- mrtav lager
- transfer/rebalans
- workflow/action queue

Export/scheduler ne sme dominirati iznad decision sekcija.

---

## Reports rules

Report je sales artefakt:
- izgleda kao dokument
- ima period/freshness/data quality
- methodology
- warnings
- print CSS
- graceful export failure

---

## Commands

Frontend:
```powershell
cd Klijent/clientapp
npm run check:analytics-guardrails
npm run build
```

Za ciljane frontend testove koristi non-watch režim da se terminal ne zaglavi na `Waiting for file changes`:
```powershell
cd Klijent/clientapp
npm run test -- --run src/components/__tests__/WorkersPanel.spec.tsx
```

Ako menjaš drugi spec, zadrži isti obrazac: `npm run test -- --run <putanja-do-spec-fajla>`.

Backend:
```powershell
dotnet build
dotnet test
```

Analytics migrations:
```powershell
dotnet ef migrations list `
  --project .\Infrastructure\Infrastructure.csproj `
  --startup-project .\Api\Api.csproj `
  --context AnalyticsDbContext
```

Ako komanda ne može zbog okruženja, napiši razlog. Ne izmišljaj da je prošla.

---

## Kada napraviti test

Dodaj test kada menjaš:
- period/fallback
- fake-zero behavior
- recommendation semantics
- action queue status/resolved/note
- worker refresh behavior
- report export fallback
- formatter/guardrail helper

Ako nema lako dostupne test infrastrukture, dodaj najmanji testable helper ili dokumentovan TODO, ali ne preskači regresiju bez objašnjenja.

---

## Prompt queue workflow

Ako postoji `docs/ai/NEXT_PROMPT_QUEUE.md`, agent mora da radi po queue pravilima.

### Pravila

1. Uzmi prvi task sa `Status: TODO`.
2. Ne preskači taskove bez eksplicitnog zahteva korisnika.
3. Ne radi više od jednog taska po sesiji/commitu.
4. Pre izmene postavi status na `IN_PROGRESS`.
5. Posle rada postavi `DONE`, `PARTIAL` ili `BLOCKED`.
6. Dodaj belešku u task:
   - datum
   - commit SHA ako postoji
   - promenjeni fajlovi
   - provere
   - rizik
   - sledeći korak
7. Ako je task `BLOCKED`, ne prelazi na sledeći task osim ako korisnik eksplicitno kaže.
8. Ako je task `PARTIAL`, sledeći task treba biti follow-up za partial gap, osim ako queue kaže drugačije.

### Stop rules za queue

Stani ako:
- task traži više od 6–8 fajlova
- build/test pada dva puta
- nema jasnog source-of-truth
- endpoint/security/cache pattern nije jasan
- potrebni su secrets ili produkcioni pristup
- postoji rizik od broad rewrite-a

### Finalni izveštaj

Agent mora da završi porukom:

```text
Queue task:
- Qxx title

Status:
- DONE/PARTIAL/BLOCKED

Promenjeno:
- ...

Provere:
- ...

Rizici:
- ...

Sledeće:
- Qyy title
```
