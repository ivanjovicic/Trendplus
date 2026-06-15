# Action Outcome Data Shape Audit

Updated: 2026-06-15

## Cilj

Ovaj audit proverava da li trenutni action queue data shape već može da podrži **Phase 1** read-only `Action Outcome Summary` bez nove baze, novog workflow-a ili promene postojećeg DTO shape-a.

Zaključak unapred:

- **da**, za osnovni summary po `sourceType`, `priority`, `status`, `outcomeStatus` i impact coverage
- **delimično**, za cohort analizu po trust signalima
- **ne još**, za pravi owner view, store/supplier/article drilldown i precizan “time to first action”

## Šta danas već postoji

### API surface

Trenutno postoje tri relevantna read endpointa:

| Endpoint | Šta vraća | Ograničenje za outcome summary |
|---|---|---|
| `GET /api/analytics/actions` | paginated list akcija sa filterima po `status`, `priority`, `sourceType`, `dataQualityStatus`, `search` | nije aggregate endpoint; za summary bi klijent morao da paginira kroz ceo skup |
| `GET /api/analytics/actions/counts` | counts po action `status` + `p1Open` | nema outcome, impact ni trust breakdown |
| `GET /api/analytics/actions/{id}` | detalj jedne akcije + notes timeline | dobar za drilldown, ne za summary |

### Polja koja su već stabilna

| Polje | Upotreba u Phase 1 summary | Spremno |
|---|---|---|
| `sourceType` | breakdown po izvoru preporuke | da |
| `priority` | breakdown po poslovnoj hitnosti | da |
| `status` | open vs terminal throughput | da |
| `outcomeStatus` | success / neutral / negative / pending / not_measured | da |
| `expectedImpactRsd` | planirani efekat | da |
| `measuredImpactRsd` | ostvareni efekat | da |
| `createdAtUtc` | created trend i intake period | da |
| `resolvedAtUtc` | closed trend i time to resolve | da |
| `outcomeMeasuredAtUtc` | outcome trend i time to measure | da |
| `dataQualityStatus` | trust cohort | da |
| `confidencePct` | confidence bucket | da |
| `reliabilityPct` | reliability bucket | da |
| `recommendationStatus` | dodatni business breakdown | da, uz normalizovan UI prikaz |

## Šta može da se agregira bez schema promene

### Potpuno spremno

| Summary pogled | Kako se računa | Napomena |
|---|---|---|
| Created actions | count po `createdAtUtc` periodu | osnovni intake |
| Closed actions | count gde je `status in (done, rejected)` po `resolvedAtUtc` | ne mešati sa outcome periodom |
| Outcome coverage | count gde `outcomeStatus != pending` i zatvorene akcije | glavni process KPI |
| Positive / negative / neutral outcome rate | breakdown po `outcomeStatus` | `pending` van success/failure rate |
| Expected vs measured impact | suma `expectedImpactRsd` i `measuredImpactRsd` | `null` measured nije nula |
| Breakdown po `sourceType` | grupisanje po izvoru | glavni manager view |
| Breakdown po `priority` | grupisanje po prioritetu | workload + quality signal |
| Breakdown po `dataQualityStatus` | trust cohort | posebno vredno za recommendation validation |

### Spremno uz oprez

| Summary pogled | Problem | Kako ipak može u Phase 1 |
|---|---|---|
| Confidence cohort | vrednost je numerička i treba bucket | grupisati npr. `<50`, `50-69`, `70-84`, `85+` |
| Reliability cohort | isti problem kao confidence | bucket, ne raw broj |
| Recommendation breakdown | status tekst može biti širi business tekst | prikazati top N + `other`, bez menjanja source-of-truth |
| Time to measure | nije svaka akcija izmerena | računati samo gde `outcomeMeasuredAtUtc` postoji |
| Impact realization ratio | denominatori mogu biti mali ili pristrasni | prikazati samo za sample sa merenim outcome-om |

## Šta nije spremno bez dodatnog rada

| Potreba | Zašto nije spremno | Minimalni budući korak |
|---|---|---|
| Owner / assignee analytics | nema eksplicitnog `assignedTo` polja | dodati owner model ili canonical assignment polje |
| Time to first action | nema `acceptedAtUtc` niti canonical first-touch timestamp | dodati polje ili izvući iz note istorije u posebnoj projekciji |
| Store / supplier / article summary | nije kanonski modelovano kroz posebna polja | standardizovati structured metadata ili dodatna columns |
| Drilldown po poslovnom segmentu iz `metadataJson` | payload je slobodan tekst, ne ugovor | definisati whitelist structured keys |
| Cross-screen learning by exact recommendation family | `recommendationStatus` može biti dovoljan samo delimično | eventualno dodati canonical reason/recommendation family field |

## Najvažnije ograničenje trenutnog API-ja

Najveći blocker za pravi Phase 1 summary nije schema nego **shape read endpointa**:

- `GET /api/analytics/actions` je list endpoint i vraća samo jednu stranu rezultata
- `GET /api/analytics/actions/counts` vraća samo status KPI-je
- trenutno nema aggregate endpoint koji vraća outcome coverage, impact sum i cohort breakdown

To znači da je za Phase 1 najčistiji sledeći backend korak:

- novi **read-only summary endpoint**
- bez promene postojećeg action workflow-a
- bez promene postojećih write DTO-jeva

## Preporučeni minimalni summary contract

Ovo je predlog za sledeću fazu, ne implementacija:

### Endpoint

- `GET /api/analytics/actions/outcomes/summary`

### Query params

- `createdFrom`
- `createdTo`
- `measuredFrom`
- `measuredTo`
- `sourceType` optional
- `priority` optional
- `dataQualityStatus` optional

### Response shape

| Polje | Svrha |
|---|---|
| `meta` | period, freshness, sample warnings, denominator notes |
| `totals` | created, closed, measured, pending, positive, negative, notMeasured |
| `impact` | expected sum, measured sum, realization ratio |
| `bySourceType[]` | summary po izvoru |
| `byPriority[]` | summary po prioritetu |
| `byOutcomeStatus[]` | summary po outcome statusu |
| `byDataQuality[]` | trust cohort |
| `timeSeries[]` optional | outcome trend po danu/nedelji za Phase 2 |

## Trust pravila koja summary endpoint mora da poštuje

- `pending` nije failure
- `measuredImpactRsd = null` nije `0`
- `resolvedAtUtc` i `outcomeMeasuredAtUtc` nisu isti datum i ne smeju se spajati bez oznake
- `rejected` treba prikazati odvojeno od `done`
- male sample size cohort-e treba označiti warning-om
- ako je filter period “measured period”, endpoint ne sme potajno koristiti `updatedAtUtc`

## Da li je Phase 1 moguć bez schema promene?

**Da.**

Za prvi read-only summary dovoljno je ono što već postoji u `AnalyticsActionItem` modelu, pod uslovom da prihvatimo sledeći scope:

- fokus na `sourceType`, `priority`, `status`, `outcomeStatus`, `impact`, `dataQuality`, `confidence/reliability bucket`
- bez owner dashboards
- bez canonical store/supplier/article segmentacije
- bez novih write polja i bez promene action queue ponašanja

## Preporučeni sledeći mali task

Ako se nastavlja odmah posle ovog audita, najbolji sledeći mali task je:

- napisati backend/API plan za `GET /api/analytics/actions/outcomes/summary`
- zaključati response contract i denominator rules
- tek posle toga implementirati minimalni read-only summary endpoint
