# Action Outcome Summary Spec vs Implementation Diff

Updated: 2026-06-16

## Svrha

Ovaj audit poredi:

- canonical spec iz `docs/Analytics/ACTION_OUTCOME_SUMMARY_ENDPOINT_SPEC.md`
- trenutnu implementaciju u `Api/Endpoints/AnalyticsActionsEndpoints.cs`
- trenutni service/DTO shape u `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`

Cilj nije novi refactor, već jasan odgovor:

- šta je već usklađeno
- gde je implementacija šira od spec-a
- koje izmene još imaju smisla da se zaključaju

## Kratak zaključak

Trenutna implementacija je **funkcionalno ispred spec-a**.

To znači:

- minimalni Phase 1 contract iz spec-a je pokriven
- implementacija već nudi nekoliko dodatnih polja i filtera
- najveći otvoreni posao nije “dodati endpoint”, već **zadržati usklađenost spec-a, testova i frontend očekivanja**

Nije pronađen P0 mismatch koji zahteva hitan rollback ili gašenje endpointa.

## Usklađeno

Sledeće stavke su usklađene između spec-a i trenutnog koda:

| Oblast | Spec | Implementacija | Status |
|---|---|---|---|
| Route | `GET /api/analytics/actions/outcomes/summary` | isti route | OK |
| Core query params | `createdFrom`, `createdTo`, `measuredFrom`, `measuredTo`, `sourceType`, `priority`, `dataQualityStatus` | prisutni | OK |
| Default period | default poslednjih `90` dana kada nema filtera | prisutan u endpointu | OK |
| Enum validation | invalid `sourceType` / `priority` / `dataQualityStatus` vraća `400` | prisutno | OK |
| Core response blocks | `meta`, `totals`, `impact`, `bySourceType`, `byPriority`, `byOutcomeStatus`, `byDataQuality` | prisutni | OK |
| Denominator semantics | `pending != failure`, `null measuredImpact != 0`, `rejected` odvojeno | implementirano kroz summary agregaciju | OK |
| Empty behavior | validan empty summary sa `meta.success = true` i `emptyReason` | prisutno | OK |

## Gde je implementacija šira od spec-a

Ovo nisu bugovi. Ovo su implementation supersets koje spec trenutno ne zahteva eksplicitno:

| Oblast | Spec | Implementacija | Značenje |
|---|---|---|---|
| Resolved period filter | nije deo minimalnog public contract-a | podržani `resolvedFrom` i `resolvedTo` | korisno za throughput cohort |
| Extra breakdowns | spec traži četiri osnovna breakdown-a | dodatno postoje `byConfidenceBucket` i `byReliabilityBucket` | korisno za trust cohort analizu |
| Meta fields | spec dokumentuje minimalni set | implementacija nosi i `resolvedFrom`, `resolvedTo` | korektno, ali šire od minimalnog contract-a |
| Bucket shape | spec traži osnovne KPI-jeve | implementacija već vraća i `pendingOutcomeCount`, `neutralCount`, `measuredImpactSampleCount`, `warningCodes` | frontend može da koristi bogatiji trust signal |

## Gde je spec uži od implementacije

Ovo je glavni izvor buduće zabune ako se ne zabeleži:

| Tema | Spec kaže | Kod radi | Rizik |
|---|---|---|---|
| Public query contract | fokus na `created*` i `measured*` | backend prihvata i `resolved*` | novi klijent može prevideti podržani filter |
| Response examples | prikazuju samo osnovne nizove | DTO shape je bogatiji | frontend/dev može pogrešno misliti da dodatna polja nisu stabilna |
| Test scope | spec nabraja minimalne testove | implementacija traži i testove za `resolved*` i extra bucket-e | coverage može ostati ispod stvarnog surface-a |

## Gde još postoji realan gap

Nisu svi gapovi funkcionalni bugovi; neki su “contract hygiene” ili rollout rizici.

### 1. Spec i implementacija nisu još jedna canonical celina

Spec trenutno namerno opisuje **minimalni public Phase 1 contract**, a implementacija je već otišla malo dalje.

Rizik:

- frontend ili drugi klijent može tretirati `resolved*` i dodatne bucket-e kao “nestabilne” iako su već vraćeni

Potrebna izmena:

- ili proširiti canonical spec da obuhvati `resolvedFrom`, `resolvedTo`, `byConfidenceBucket`, `byReliabilityBucket`
- ili ih eksplicitno označiti kao supported-but-secondary surface

### 2. Test plan treba da pokrije stvarni, ne samo minimalni surface

Minimalni testovi iz spec-a su dobar početak, ali nisu dovoljni da zaštite trenutnu implementaciju.

Potrebna izmena:

- dodati/zaključati testove za:
  - `resolvedFrom > resolvedTo -> 400`
  - `periodMode` kada postoji resolved-only filtering
  - `byConfidenceBucket` i `byReliabilityBucket` shape i denominator pravila

### 3. Frontend copy mora ostati jasan za mixed-period filtere

Backend već može da kombinuje više period filter slojeva. To je dobro, ali UI mora jasno reći:

- da li summary prikazuje intake period
- da li prikazuje measured outcome period
- da li je uključen i resolved throughput sloj

Potrebna izmena:

- na budućem summary UI sloju zadržati trust copy koji eksplicitno objašnjava aktivan period mode i dodatne filtere

## Potrebne izmene

Najmanji smislen sledeći paket izmena je:

### P1 — Dokumentaciono usklađivanje

- proširiti `docs/Analytics/ACTION_OUTCOME_SUMMARY_ENDPOINT_SPEC.md` da jasno kaže da je `resolvedFrom` / `resolvedTo` supported superset
- dopisati da su `byConfidenceBucket` i `byReliabilityBucket` deo stabilnog DTO-ja, iako nisu obavezni za prvi frontend surface

### P1 — Backend test usklađivanje

- zaključati endpoint test za invalid `resolvedFrom` / `resolvedTo`
- zaključati service test za bucket integrity na `byConfidenceBucket`
- zaključati service test za bucket integrity na `byReliabilityBucket`
- zaključati test za `mixed_period_filters` / `periodMode` ponašanje ako to već postoji kao warning ili meta signal

### P2 — Frontend contract clarity

- dokumentovati u frontend code/comments da su `null` rates i ratios “unknown”, ne nula
- ako summary UI kasnije koristi confidence/reliability breakdown, prikazati ih uz small-sample warning copy

## Šta nije potrebno menjati sada

Na osnovu ovog audita, sledeće nije potrebno dirati u ovom koraku:

- route naziv
- osnovni endpoint path
- core totals shape
- osnovna denominator pravila
- read-only prirodu endpointa

## Preporuka

Najmanji bezbedan sledeći korak posle ovog audita je:

1. zadržati postojeću implementaciju
2. proširiti spec da formalno prizna implementation superset
3. dopuniti backend test coverage za `resolved*` i extra cohort bucket-e

To daje stabilniji contract bez novog refactora i bez širenja scope-a.
