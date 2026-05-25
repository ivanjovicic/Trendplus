# Trendplus Copilot Instructions

Ove instrukcije važe za `ivanjovicic/Trendplus`, posebno za analytics, data quality, refresh/workers, supplier scorecard, product decisions, inventory i reports.

## Glavni cilj

Trendplus treba da bude pilot/prodajno spreman proizvod, ne samo skup funkcija.

Kupac mora bez developera pored sebe da razume:
- koji period gleda
- koliko su podaci sveži
- koliko su podaci pouzdani
- zašto sistem daje preporuku
- šta treba da uradi sledeće

Ako izmena ne poboljšava poverenje, jasnoću, stabilnost, performanse ili onboarding, preispitaj scope.

---

## Agent discipline / token saving

Radi u malim, ciljanim izmenama.

Pre nego što kreneš:
1. Pronađi postojeći shared helper/component.
2. Pročitaj samo relevantne fajlove.
3. Ne refaktoriši šire nego što task traži.
4. Ne popravljaj usput nepovezane stvari.
5. Ako komanda dugo traje ili puca zbog okruženja, nemoj naslepo ponavljati istu komandu.

Ako build/test komanda zapne:
- prekini
- probaj užu proveru
- zabeleži šta je zapelo
- ne nastavljaj sa velikim izmenama bez validacije

Primer:
```powershell
# prvo ciljano
cd Klijent/clientapp
npm run check:analytics-guardrails

# za ciljane Vitest testove koristi non-watch
npm run test -- --run src/components/__tests__/WorkersPanel.spec.tsx

# tek onda build
npm run build
```

Za ostale test fajlove koristi isti obrazac: `npm run test -- --run <putanja-do-spec-fajla>`.

Za backend:
```powershell
dotnet build
dotnet test --no-build
```

Ako `dotnet test --no-build` ne može jer build nije urađen, koristi `dotnet test`.

---

## Obavezne provere

Frontend analytics promena:
```powershell
cd Klijent/clientapp
npm run check:analytics-guardrails
npm run build
```

Backend promena:
```powershell
dotnet build
dotnet test
```

Analytics DB migracije:
```powershell
dotnet ef migrations list `
  --project .\Infrastructure\Infrastructure.csproj `
  --startup-project .\Api\Api.csproj `
  --context AnalyticsDbContext
```

Ako ne možeš da pokreneš komande, napiši tačno zašto.

---

## No fake zero

Nikad ne prikazuj `0 RSD`, `0 kom`, `0%` kao rezultat ako je stvarni razlog:
- SQL greška
- timeout
- missing table/materialized view
- cache/refresh problem
- API nije dostupan

Koristi:
- `AnalyticsErrorState` za grešku
- `AnalyticsEmptyState` za stvarno prazno stanje
- `AnalyticsEmptyState variant="insufficient_data"` za nedovoljno signala
- warning banner za stale/partial/fallback podatke

Ako API pukne, ne prikazuj KPI kartice sa nulama.

---

## Backend je source of truth

Frontend ne sme da računa finalne poslovne preporuke.

Frontend sme:
- formatiranje
- sortiranje/filtere
- prikaz labela/boja
- prikaz reason/reasonCodes
- dodavanje u Action Queue

Frontend ne sme:
- računati `recommendationStatus`
- računati `confidencePct`
- računati `reliabilityPct`
- računati `decisionScore`
- izmišljati `reasonCodes`
- uvoditi lokalne threshold-e

---

## Formatter guardrails

Ne dodavati lokalno u page/component fajlovima:
```ts
function formatCurrency(...) {}
function formatPercent(...) {}
const formatCurrency = ...
const formatPercent = ...
function fmtRsd(...) {}
function fmtPct(...) {}
```

Koristi:
```ts
import { fmtNumber, fmtPct, fmtRsd } from "../utils/analyticsFormatters";
```

Ako helper nedostaje, dodaj ga u shared utils.

---

## Trust Header standard

Svaki core analytics ekran treba da ima `AnalyticsTrustHeader`.

Obavezno kada je moguće:
```tsx
<AnalyticsTrustHeader
  title="..."
  description="..."
  mode="recommendation" // recommendation | signal | report
  periodFrom={...}
  periodTo={...}
  lastRefreshAt={...}
  dataFreshnessStatus={...}
  dataSource="..."
  dataQualityStatus={...}
  dataQualitySummary={...}
  dataQualityHref="/analytics/data-quality"
  refreshStatusHref="/admin/configuration?panel=workers"
  compact
/>
```

Ne koristi `Date.now()` kao lažni refresh. Ako se ne zna, prosledi `null`.

Mode:
- `/analytics`: `recommendation`
- `/analytics/products`: `recommendation`
- supplier pregled: `recommendation`
- supplier scorecard: uglavnom `signal`
- `/analytics/inventory`: `recommendation`
- `/analytics/actions`: `report`
- `/analytics/data-quality`: `report`
- daily/shoe/color sales: `signal`
- pre/post nivelacija: `report` ili `signal`
- pre-nivelacija prioriteti: `recommendation`

---

## Supplier Scorecard

Najčešća opasna greška: tihi fallback perioda.

Pravila:
- 30d ne sme tiho prikazati 90d/180d/all-time.
- Ako se koristi pomoćni dataset, `usedFallback=true`.
- Ako je fallback, `recommendationAllowed=false`, osim eksplicitno dokumentovanog izuzetka.
- UI treba da kaže `Pomoćni signal`, ne finalna preporuka.
- Ako nema redova, koristi `insufficient_data`, ne fake zero.
- Scorecard ne sme kontradiktovati canonical Supplier Pregled.

Backend response treba da ima:
- requested/effective dataset
- usedFallback/fallbackReason
- recommendationAllowed
- rowCount/ignoredRowCount
- zeroRevenueRowsExcluded
- missingSupplierNameCount
- dataCoverageStatus
- meta

---

## Product Decision Center

Svaki red mora imati:
- recommendationStatus
- recommendationLabel
- recommendedAction
- recommendationReason
- reasonCodes
- confidencePct
- reliabilityPct
- dataQualityStatus

Frontend samo prikazuje. Ne izmišlja.

Add to Action Queue:
- stabilan `sourceKey`
- backend upsert sprečava duplikate
- UI posle uspeha kaže `U akcijama`

---

## Action Queue

Action Queue je radni sistem, ne samo tabela.

Obavezno:
- sourceType/sourceKey stabilni
- upsert bez duplikata
- status validacija u endpointu i service-u
- `done/rejected` setuju `ResolvedAtUtc`
- open statusi brišu `ResolvedAtUtc`
- audit notes/status history ako postoji
- metadata čitljiva u detail panelu

---

## Worker/refresh

Ne brkati web i worker proces.

Web:
- API i UI
- refresh status
- admin manual trigger ako je bezbedno

Worker:
- import/backfill
- materialized view refresh
- precompute/nightly jobs

Ako worker nije aktivan, UI mora to jasno prikazati.

---

## Theme i CSS

Ne hardkodovati boje:
- bez `#...`
- bez `rgb(...)`
- bez Tailwind color class kao poslovni stil
- bez inline color style

Koristi CSS variables:
- `--surface-default`
- `--surface-elevated`
- `--border-default`
- `--text-primary`
- `--text-secondary`
- `--text-muted`
- `--accent-primary`
- `--success`
- `--warning`
- `--danger`

---

## UTF-8 i srpski tekst

Pre commit-a pretraži:
```text
Ä
Å
â
�
DobavljaÄ
marÅ
osveÅ
uÄ
Å¡
Å¾
```

Ako ih vidiš u UI stringovima, popravi mojibake.

Koristi dosledno:
- Dobavljač
- Maržni doprinos
- Prihod
- Lager u riziku
- Kvalitet podataka
- Pouzdanost signala
- Sigurnost preporuke
- Nedovoljno podataka
- Pomoćni signal
- Preporuka sistema
- Analitički signal
- Izveštaj

---

## Report/export

Report mora imati:
- period
- datum generisanja
- poslednji refresh ili "nije dostupno"
- data quality status
- methodology
- warnings za fallback/insufficient data

Ako PDF ne radi, prikaži poruku i ponudi Print/Excel. Ne ruši UI.

---

## Commit messages

Dobro:
```text
fix(scorecard): prevent 30d fallback to wider supplier dataset
fix(analytics-ui): repair Serbian encoding and formatter guardrails
fix(api): add meta contract to inventory endpoints
feat(reports): add supplier decision print report
test(analytics): add fake-zero regression tests
```

Loše:
```text
analytics fix
chore: commit all
final fix
update
```
