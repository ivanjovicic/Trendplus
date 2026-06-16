# Action Outcome Summary Endpoint Spec

Updated: 2026-06-16

## Svrha

Ovaj dokument zaključava **Phase 1 read-only contract** za summary endpoint koji agregira ishode akcija iz `Action Queue`.

Primary route:

- `GET /api/analytics/actions/outcomes/summary`

Ovo je analytics/read model. Endpoint:

- ne menja action workflow
- ne menja postojeće DTO write tokove
- ne uvodi novi auth model
- ne tretira unknown signal kao `0`

## Repo status

U trenutnom repou već postoji implementacija summary endpointa u:

- `Api/Endpoints/AnalyticsActionsEndpoints.cs`
- `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`

Ovaj dokument je i dalje koristan kao **canonical implementation spec** za dalji rad, testove i frontend integracije.

## Phase 1 scope

Endpoint vraća mali, prodajno koristan summary za:

- created / closed / open totals
- measured / pending / success / neutral / negative / not_measured outcome totals
- expected vs measured impact
- breakdown po `sourceType`
- breakdown po `priority`
- breakdown po `outcomeStatus`
- breakdown po `dataQualityStatus`
- trust warning kodove i sample upozorenja

Van scope-a za ovu fazu ostaje:

- owner / assignee analytics
- store / supplier / article canonical drilldown
- write/update action ponašanje
- recommendation retuning
- broad auth refactor

## Route

- `GET /api/analytics/actions/outcomes/summary`

## Query params

Canonical Phase 1 query params su:

| Parametar | Tip | Obavezno | Svrha |
|---|---|---|---|
| `createdFrom` | `date-time` | ne | donja granica za datum kreiranja |
| `createdTo` | `date-time` | ne | gornja granica za datum kreiranja |
| `measuredFrom` | `date-time` | ne | donja granica za datum merenja ishoda |
| `measuredTo` | `date-time` | ne | gornja granica za datum merenja ishoda |
| `sourceType` | `string` | ne | sužava summary na jedan izvor akcije |
| `priority` | `string` | ne | sužava summary na jedan prioritet |
| `dataQualityStatus` | `string` | ne | trust cohort filter |

### Query behavior

- Ako nijedan period nije prosleđen, backend koristi razuman default window, npr. poslednjih `90` dana po `createdAtUtc`.
- `createdFrom` mora biti ranije ili jednako `createdTo`; u suprotnom vraća se `400`.
- `measuredFrom` mora biti ranije ili jednako `measuredTo`; u suprotnom vraća se `400`.
- `sourceType`, `priority` i `dataQualityStatus` moraju koristiti postojeće canonical vrednosti; invalidna vrednost vraća `400`.
- Ako su prisutni i `created*` i `measured*` filteri, `meta` mora jasno reći da summary kombinuje intake i outcome measurement filtering.

### Napomena o trenutnoj implementaciji

Aktuelni repo već podržava i `resolvedFrom` / `resolvedTo` kao dodatni filter sloj. To je **širi implementation superset**. Ovaj spec zaključava minimalni public Phase 1 contract koji frontend i testovi moraju da razumeju.

## Response shape

```json
{
  "meta": {
    "success": true,
    "periodMode": "measured",
    "createdFrom": "2026-03-01T00:00:00Z",
    "createdTo": "2026-06-15T23:59:59Z",
    "measuredFrom": "2026-05-01T00:00:00Z",
    "measuredTo": "2026-06-15T23:59:59Z",
    "generatedAtUtc": "2026-06-16T09:00:00Z",
    "sampleSize": 148,
    "measuredSampleSize": 61,
    "warnings": [
      "small_measured_sample"
    ],
    "emptyReason": null
  },
  "totals": {
    "createdCount": 148,
    "closedCount": 96,
    "openCount": 52,
    "measuredCount": 61,
    "pendingOutcomeCount": 27,
    "successCount": 34,
    "neutralCount": 11,
    "negativeCount": 9,
    "notMeasuredCount": 7,
    "outcomeCoverageRate": 0.6354,
    "positiveOutcomeRate": 0.5574,
    "negativeOutcomeRate": 0.1475
  },
  "impact": {
    "expectedImpactRsd": 1825000.0,
    "measuredImpactRsd": 944000.0,
    "realizationRatio": 0.5173,
    "measuredImpactSampleCount": 43
  },
  "bySourceType": [],
  "byPriority": [],
  "byOutcomeStatus": [],
  "byDataQuality": []
}
```

## Response contract

### `meta`

| Polje | Tip | Svrha |
|---|---|---|
| `success` | `bool` | standard analytics success signal |
| `periodMode` | `string` | govori da li summary dominantno prati `created` ili `measured` period |
| `createdFrom` | `date-time?` | effective lower created filter |
| `createdTo` | `date-time?` | effective upper created filter |
| `measuredFrom` | `date-time?` | effective lower measured filter |
| `measuredTo` | `date-time?` | effective upper measured filter |
| `generatedAtUtc` | `date-time` | vreme generisanja summary-ja |
| `sampleSize` | `int` | ukupan broj akcija u filtered sample-u |
| `measuredSampleSize` | `int` | broj akcija sa izmerenim outcome signalom |
| `warnings` | `string[]` | trust/sample warning kodovi |
| `emptyReason` | `string?` | validan empty razlog kada dataset postoji ali je prazan |

### `totals`

| Polje | Tip | Svrha |
|---|---|---|
| `createdCount` | `int` | broj kreiranih akcija u sample-u |
| `closedCount` | `int` | broj zatvorenih akcija |
| `openCount` | `int` | broj otvorenih akcija |
| `measuredCount` | `int` | broj akcija sa izmerenim outcome-om |
| `pendingOutcomeCount` | `int` | broj akcija sa `pending` outcome-om |
| `successCount` | `int` | broj pozitivnih outcome-a |
| `neutralCount` | `int` | broj neutralnih outcome-a |
| `negativeCount` | `int` | broj negativnih outcome-a |
| `notMeasuredCount` | `int` | broj zatvorenih akcija bez merljivog ishoda |
| `outcomeCoverageRate` | `decimal?` | nullable kada nema validan denominator |
| `positiveOutcomeRate` | `decimal?` | nullable kada nema validan measured denominator |
| `negativeOutcomeRate` | `decimal?` | nullable kada nema validan measured denominator |

### `impact`

| Polje | Tip | Svrha |
|---|---|---|
| `expectedImpactRsd` | `decimal?` | suma očekivanog efekta |
| `measuredImpactRsd` | `decimal?` | suma izmerenog efekta za merljiv sample |
| `realizationRatio` | `decimal?` | nullable kada denominator nije validan |
| `measuredImpactSampleCount` | `int` | broj akcija koje imaju merljiv `measuredImpactRsd` |

### `bySourceType[]`, `byPriority[]`, `byOutcomeStatus[]`, `byDataQuality[]`

Svaki bucket koristi isti shape:

| Polje | Tip | Svrha |
|---|---|---|
| `key` | `string` | canonical ključ |
| `label` | `string` | user-facing label |
| `totalCount` | `int` | ukupan broj akcija u bucket-u |
| `closedCount` | `int` | broj zatvorenih akcija |
| `measuredCount` | `int` | broj akcija sa izmerenim outcome-om |
| `pendingOutcomeCount` | `int` | broj `pending` outcome-a |
| `successCount` | `int` | broj pozitivnih outcome-a |
| `neutralCount` | `int` | broj neutralnih outcome-a |
| `negativeCount` | `int` | broj negativnih outcome-a |
| `notMeasuredCount` | `int` | broj `not_measured` outcome-a |
| `expectedImpactRsd` | `decimal?` | suma očekivanog efekta |
| `measuredImpactRsd` | `decimal?` | suma izmerenog efekta |
| `outcomeCoverageRate` | `decimal?` | nullable ako denominator nije validan |
| `positiveOutcomeRate` | `decimal?` | nullable ako measured denominator nije validan |
| `negativeOutcomeRate` | `decimal?` | nullable ako measured denominator nije validan |
| `realizationRatio` | `decimal?` | nullable ako denominator nije validan |
| `measuredImpactSampleCount` | `int` | sample veličina za impact |
| `warningCodes` | `string[]` | trust/sample warning kodovi |

## Denominator rules

Ovo su canonical Phase 1 pravila i ne smeju se tiho menjati:

### 1. `pending` nije failure

- `pending` outcome nije negativan ishod.
- `pending` ne ulazi u success/failure rate kao failure.
- `pending` ostaje poseban count (`pendingOutcomeCount`).

### 2. `measuredImpactRsd = null` je unknown, ne zero

- `null` measured impact ne znači `0 RSD`.
- Takav zapis ne sme oboriti realization ratio kao da je nulti efekat.
- `measuredImpactSampleCount` broji samo akcije koje stvarno imaju merljiv impact signal.

### 3. `resolvedAtUtc` i `outcomeMeasuredAtUtc` nisu isti događaj

- `resolvedAtUtc` opisuje zatvaranje workflow-a.
- `outcomeMeasuredAtUtc` opisuje merenje poslovnog rezultata.
- Endpoint ne sme spajati ova dva datuma u jednu metriku bez eksplicitnog objašnjenja.

### 4. `rejected` je odvojeno od `done`

- `rejected` nije isto što i sprovedena akcija bez efekta.
- `rejected` ostaje poseban terminalni status i ne sme biti tiho stopljen sa `done` u interpretaciji rezultata.
- Ako realization ratio koristi samo sproveden/measured sample, to mora biti jasno dokumentovano kroz `warnings` ili meta napomenu.

### 5. Small sample warning je obavezan

- Kada je measured sample mali, endpoint mora vratiti warning kod, npr. `small_measured_sample`.
- Na malom uzorku UI ne sme prikazivati jake poređajne zaključke bez upozorenja.

## Trust behavior

- `success with data` -> `meta.success = true`
- `success empty` -> `meta.success = true` + `emptyReason`
- `warning/partial` -> `meta.success = true` + warning kodovi
- `error` -> `Problem(...)` ili postojeći analytics error pattern

Preporučeni warning kodovi:

- `small_sample`
- `small_measured_sample`
- `outcome_coverage_low`
- `expected_impact_denominator_missing`
- `measured_impact_missing`
- `rejected_actions_present`
- `mixed_period_filters`

## Backend tests needed

Najmanji potreban backend test set:

### Query validation

- vraća `400` kada je `createdFrom > createdTo`
- vraća `400` kada je `measuredFrom > measuredTo`
- vraća `400` za nevažeći `sourceType`
- vraća `400` za nevažeći `priority`
- vraća `400` za nevažeći `dataQualityStatus`

### Empty / default behavior

- bez query perioda koristi default created period window
- validan prazan sample vraća `meta.success = true` i smislen `emptyReason`
- prazan sample ne vraća fake zero rates kada denominator ne postoji

### Denominator semantics

- `pending` ne ulazi u negative rate denominator
- `pending` ne ulazi u positive rate denominator
- `not_measured` ostaje odvojeno od `pending`
- `measuredImpactRsd = null` ne tretira se kao `0`
- `outcomeCoverageRate` je `null` kada nema zatvorenih akcija
- `positiveOutcomeRate` i `negativeOutcomeRate` su `null` kada nema measured sample-a
- `realizationRatio` je `null` kada nema validan measured impact denominator

### Lifecycle semantics

- `resolvedAtUtc` i `outcomeMeasuredAtUtc` ostaju odvojeni u period interpretaciji
- `rejected` se broji odvojeno od `done`
- kombinovani `created*` + `measured*` filteri vraćaju `mixed_period_filters` warning kada je to primenljivo

### Bucket integrity

- `bySourceType` pravilno grupiše po source-u
- `byPriority` pravilno grupiše po prioritetu
- `byOutcomeStatus` pravilno grupiše `success`, `neutral`, `negative`, `pending`, `not_measured`
- `byDataQuality` pravilno grupiše trust cohort
- bucket rates ostaju `null`, ne `0`, kada denominator nije validan

## Non-goals

- nema promene action schema modela
- nema promene write DTO-jeva
- nema promene recommendation logike
- nema novog auth sistema
- nema owner analytics-a u ovoj fazi

## Sledeći korak

Ako se spec koristi za novu implementaciju u drugom branch-u ili drugom repou, sledeći najmanji korak je:

- implementirati ili uskladiti `GET /api/analytics/actions/outcomes/summary` sa ovim contract-om
- zaključati gore navedene backend testove pre širenja frontend surface-a
