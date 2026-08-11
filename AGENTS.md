# AGENTS.md — Trendplus AI Agent Standard

Ovaj fajl je za Codex, Copilot agent mode i druge AI agente koji menjaju repo.

Pre rada obavezno pogledaj i kanonske AI vodiče:
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/ENCODING_AND_TEXT_SAFETY.md`
- `docs/ai/COMMON_FAILURES_AND_FIXES.md`
- `docs/ai/VALIDATION_SELECTOR.md`

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
- Ako task pređe u drugi owner/program ili dobije drugi nezavisan cilj, stani i prijavi gap. Mali isti-owner scope repair potreban za acceptance zabeleži i nastavi.

### Autonomy and questions

- Assume the user may be offline after assigning the task.
- A direct repository request authorizes normal, reversible work in this repo.
- Do not stop for routine choices like whether to inspect source, add a focused test, update the mapped doc, commit, or verify `main`.
- Ask only when the remaining decision has material business/product impact, tenant/privacy/security/secret implications, destructive data/schema consequences, production impact, external cost, or irreversible effects outside this repo.
- If two same-owner options are both safe, choose the smaller reversible one and record the assumption.

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

Local diff, local commit, pushed branch or open PR are transport states. File-changing work is closed only after the exact delivered SHA is verified on current `main`.

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

Choose the narrowest applicable proof through `docs/ai/VALIDATION_SELECTOR.md`; do not run every command below for every change.

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

If the task comes from a live queue, follow `MASTER_ROADMAP.md`, `docs/ai/AGENT_START_HERE.md` and `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

### Rules

1. Resolve the owner program from `MASTER_ROADMAP.md`.
2. Start only the current `READY` prompt in that owner queue after checking dependencies and global priority.
3. Treat `docs/ai/NEXT_PROMPT_QUEUE.md` as a historical ledger, not a live router.
4. Use only protocol statuses: `READY`, `WAITING`, `IN_PROGRESS`, `BLOCKED`, `PARTIAL`, `DONE`, `OBSOLETE`.
5. Work one prompt per session/commit unless the prompt explicitly allows a bounded docs consolidation.
6. Before implementation, set the prompt to `IN_PROGRESS` or create the local lock from `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.
7. After work, record status, commit SHA, changed files, checks, remaining risk and next step.
8. If scope crosses into another program, the same failure repeats twice, or required proof cannot be produced, stop as `PARTIAL` or `BLOCKED` instead of guessing.

### Stop rules

Stop if:
- the prompt is not the current `READY` item for its owner program
- source of truth, tenant authority or business contract is unclear
- build/test fails twice without new evidence
- the fix needs unrelated files/programs or a broad rewrite
- secrets, production access or unresolved security decisions are required

### Final report

Agent should finish with:

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

Main verification:
- pass/fail/not run
```
