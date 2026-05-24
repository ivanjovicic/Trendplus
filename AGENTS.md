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
