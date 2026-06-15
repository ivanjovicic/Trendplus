# Action Outcome Summary API Plan

Updated: 2026-06-15

## Cilj

Ovaj dokument zaključava minimalni backend/API plan za prvi read-only endpoint:

- `GET /api/analytics/actions/outcomes/summary`

Cilj endpointa nije da zameni `Action Queue` listu, već da vrati agregirani pregled ishoda akcija za operativni i menadžerski nivo.

## Scope za Phase 1

Endpoint u prvoj fazi pokriva:

- created / closed / measured totals
- outcome coverage
- positive / neutral / negative / not_measured breakdown
- expected vs measured impact
- breakdown po `sourceType`
- breakdown po `priority`
- breakdown po `dataQualityStatus`
- confidence / reliability cohort summary

Endpoint u prvoj fazi **ne** pokriva:

- owner / assignee analytics
- store / supplier / article canonical drilldown
- time series po danu ili nedelji
- recommendation retuning
- write/update behavior action queue-a

## Zašto poseban summary endpoint

Postojeći endpointi nisu dovoljni:

- `GET /api/analytics/actions` je paginated list endpoint
- `GET /api/analytics/actions/counts` vraća samo status KPI-je
- `GET /api/analytics/actions/{id}` je detail endpoint

Zbog toga frontend danas nema pouzdan i jeftin način da izvuče summary bez page-by-page agregacije preko celog skupa.

## Endpoint contract

### Route

- `GET /api/analytics/actions/outcomes/summary`

### Query params

| Parametar | Tip | Obavezno | Svrha | Napomena |
|---|---|---|---|---|
| `createdFrom` | `date-time` | ne | filter po datumu kreiranja | koristi se za intake period |
| `createdTo` | `date-time` | ne | filter po datumu kreiranja | inclusive ili jasno dokumentovan exclusive-end pattern |
| `resolvedFrom` | `date-time` | ne | filter po datumu zatvaranja | za throughput cohort |
| `resolvedTo` | `date-time` | ne | filter po datumu zatvaranja | isto pravilo kao gore |
| `measuredFrom` | `date-time` | ne | filter po datumu merenja ishoda | glavni analytics period kada postoji |
| `measuredTo` | `date-time` | ne | filter po datumu merenja ishoda | isto pravilo kao gore |
| `sourceType` | `string` | ne | sužava na jedan source | koristi postojeći enum skup |
| `priority` | `string` | ne | sužava na jedan prioritet | koristi postojeći enum skup |
| `dataQualityStatus` | `string` | ne | trust cohort filter | koristi canonical value uz legacy normalizaciju |

### Query pravila

- ako nijedan period filter nije prosleđen, backend koristi razuman default, npr. poslednjih `90` dana po `createdAtUtc`
- ako je prosleđen `measuredFrom/measuredTo`, period u `meta` mora jasno reći da je summary zasnovan na outcome measurement periodu
- ako su istovremeno prosleđeni `created*` i `measured*` filteri, `meta` mora eksplicitno opisati oba sloja filtera
- invalidan enum/filter vraća `400 BadRequest`, po postojećem pattern-u iz `AnalyticsActionsEndpoints`

## Response shape

```json
{
  "meta": {
    "success": true,
    "periodMode": "measured",
    "createdFrom": "2026-03-01T00:00:00Z",
    "createdTo": "2026-06-15T23:59:59Z",
    "resolvedFrom": null,
    "resolvedTo": null,
    "measuredFrom": "2026-05-01T00:00:00Z",
    "measuredTo": "2026-06-15T23:59:59Z",
    "generatedAtUtc": "2026-06-15T12:00:00Z",
    "sampleSize": 148,
    "measuredSampleSize": 61,
    "warnings": [
      "small_measured_sample",
      "rejected_actions_excluded_from_realization_ratio"
    ]
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
    "expectedImpactRsd": 1825000.00,
    "measuredImpactRsd": 944000.00,
    "realizationRatio": 0.5173,
    "measuredImpactSampleCount": 43
  },
  "bySourceType": [],
  "byPriority": [],
  "byOutcomeStatus": [],
  "byDataQuality": [],
  "byConfidenceBucket": [],
  "byReliabilityBucket": []
}
```

## DTO proposal

### Root

| Polje | Tip | Napomena |
|---|---|---|
| `meta` | `AnalyticsActionOutcomeSummaryMetaDto` | koristi analytics meta/trust pattern |
| `totals` | `AnalyticsActionOutcomeSummaryTotalsDto` | glavni KPI blok |
| `impact` | `AnalyticsActionOutcomeSummaryImpactDto` | odvojeno zbog trust logike |
| `bySourceType` | `AnalyticsActionOutcomeSummaryBucketDto[]` | source breakdown |
| `byPriority` | `AnalyticsActionOutcomeSummaryBucketDto[]` | priority breakdown |
| `byOutcomeStatus` | `AnalyticsActionOutcomeSummaryBucketDto[]` | outcome breakdown |
| `byDataQuality` | `AnalyticsActionOutcomeSummaryBucketDto[]` | trust breakdown |
| `byConfidenceBucket` | `AnalyticsActionOutcomeSummaryBucketDto[]` | bucket summary |
| `byReliabilityBucket` | `AnalyticsActionOutcomeSummaryBucketDto[]` | bucket summary |

### `AnalyticsActionOutcomeSummaryBucketDto`

| Polje | Tip | Svrha |
|---|---|---|
| `key` | `string` | canonical bucket key |
| `label` | `string` | user-facing label |
| `totalCount` | `int` | ukupan broj akcija u bucket-u |
| `closedCount` | `int` | broj zatvorenih |
| `measuredCount` | `int` | broj sa izmerenim outcome-om |
| `successCount` | `int` | broj pozitivnih |
| `negativeCount` | `int` | broj negativnih |
| `notMeasuredCount` | `int` | broj `not_measured` |
| `expectedImpactRsd` | `decimal?` | planirani efekat |
| `measuredImpactRsd` | `decimal?` | ostvareni efekat |
| `outcomeCoverageRate` | `decimal?` | nullable ako denominator nije validan |
| `positiveOutcomeRate` | `decimal?` | nullable ako measured sample nije validan |
| `realizationRatio` | `decimal?` | nullable ako expected/measured sample nije validan |
| `warningCodes` | `string[]` | small sample, insufficient denominator itd. |

## Denominator rules

Ovo su najvažnija zaključana pravila:

### Outcome coverage

- numerator: zatvorene akcije gde `outcomeStatus != pending`
- denominator: zatvorene akcije (`done + rejected`)
- ako denominator = `0`, rezultat je `null`, ne `0`

### Positive / negative outcome rate

- numerator: broj `success` ili `negative`
- denominator: samo akcije sa izmerenim outcome-om (`success`, `neutral`, `negative`, `not_measured`)
- `pending` nikad ne ulazi u denominator

### Realization ratio

- numerator: suma `measuredImpactRsd` samo gde vrednost postoji
- denominator: suma `expectedImpactRsd` za isti sample koji ulazi u numerator ili jasno dokumentovan “measured sample expected sum”
- ako expected denominator nije validan ili sample size premali, rezultat je `null`

### Time metrics

Phase 1 endpoint ne mora da vraća median time metrike ako to komplikuje prvi release.
Ako ih ipak vrati:

- `timeToResolve` koristi `resolvedAtUtc - createdAtUtc`
- `timeToMeasure` koristi `outcomeMeasuredAtUtc - resolvedAtUtc` kada `resolvedAtUtc` postoji
- bez fallback-a na `updatedAtUtc`

## Trust / meta behavior

Summary endpoint treba da prati isti analytics trust pristup:

- `success with data` -> `meta.success = true`
- `success empty` -> `meta.success = true`, uz `emptyReason`
- `warning/partial` -> `meta.success = true`, warning codes i trust copy
- `error` -> `Problem(...)` ili analytics error meta pattern

### Minimalna warning kod lista

- `small_sample`
- `small_measured_sample`
- `outcome_coverage_low`
- `expected_impact_denominator_missing`
- `measured_impact_missing`
- `rejected_actions_present`
- `mixed_period_filters`

## Query implementation plan

### Preporučeni backend shape

- dodati novi read-only method u `AnalyticsActionItemService`
- ne dirati postojeće upsert/status/outcome metode
- endpoint registracija ostaje u `Api/Endpoints/AnalyticsActionsEndpoints.cs`

### Preporučeni servisni potpis

```csharp
Task<AnalyticsActionOutcomeSummaryDto> GetOutcomeSummaryAsync(
    AnalyticsActionOutcomeSummaryQuery query,
    CancellationToken ct = default)
```

### Preporučeni query object

```csharp
public sealed record AnalyticsActionOutcomeSummaryQuery(
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    DateTime? ResolvedFrom,
    DateTime? ResolvedTo,
    DateTime? MeasuredFrom,
    DateTime? MeasuredTo,
    string? SourceType,
    string? Priority,
    string? DataQualityStatus
);
```

## Performance i indexing note

Za Phase 1 ne treba odmah uvoditi novu storage tabelu.

Ali summary query će najviše zavisiti od:

- `CreatedAtUtc`
- `ResolvedAtUtc`
- `OutcomeMeasuredAtUtc`
- `SourceType`
- `Priority`
- `Status`
- `OutcomeStatus`

Ako se pokaže da je query spor na realnom volumenu, sledeći korak je:

- proveriti postojeće indekse nad `AnalyticsActionItems`
- tek onda razmatrati dodatni index ili materialized summary pristup

## Security i access note

Pošto access-control audit već označava action queue write/outcome rute kao P0 gap:

- ovaj endpoint mora ostati **read-only**
- role cilj za budućnost: `Viewer` i više
- ne uvoditi auth refactor u ovom tasku; samo plan dokumentuje očekivani access target

## Acceptance za sledeću implementaciju

Implementacija Phase 1 summary endpointa je spremna za početak kada:

- response contract iz ovog dokumenta bude prihvaćen
- denominator rules budu zaključane
- frontend zna da su `null` rates/ratios “unknown”, ne nula
- nema potrebe za novim write poljima

## Sledeći logičan korak

Posle ovog plana, sledeći najmanji bezbedan task je:

- implementirati `GET /api/analytics/actions/outcomes/summary`
- dodati targeted backend testove za denominator pravila i `pending != failure`
