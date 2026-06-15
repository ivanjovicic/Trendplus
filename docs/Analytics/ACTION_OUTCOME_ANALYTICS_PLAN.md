# Action Outcome Analytics Plan

Updated: 2026-06-15

## Cilj

Action Outcome Analytics treba da objasni da li akcije iz `Action Queue` zaista donose poslovni rezultat, koliko brzo se rezultat vidi i kojim signalima treba više verovati pri budućim preporukama.

Ovo nije novi workflow za unos akcija. Ovo je read-only analytics sloj iznad postojećih polja i statusa.

## Pitanja na koja ekran/report treba da odgovori

### Za operatere

- Koje otvorene ili skoro zatvorene akcije još nemaju izmeren ishod?
- Koliko akcija kasni sa proverom ishoda?
- Koje akcije imaju negativan ili neutralan ishod i traže dodatnu proveru?
- Da li su akcije nastale iz slabog signala (`dataQualityStatus`, `confidencePct`, `reliabilityPct`) češće neuspešne?

### Za menadžere

- Koji tipovi akcija i koji izvori (`sourceType`) najčešće daju pozitivan ishod?
- Koliki deo očekivanog uticaja (`expectedImpactRsd`) je stvarno ostvaren kroz `measuredImpactRsd`?
- Koliko vremena prolazi od kreiranja akcije do zatvaranja i do prvog merenja ishoda?
- Gde postoji veliki broj zatvorenih akcija bez izmerenog outcome-a, pa se pipeline formalno zatvara bez učenja?

## Trenutni source of truth

Postojeća polja već daju dobar minimalni temelj:

- identitet i poreklo: `sourceType`, `sourceKey`, `sourceId`, `actionUrl`
- poslovni kontekst: `title`, `description`, `recommendationStatus`, `priority`
- očekivani signal: `impactEstimateRsd`, `expectedImpactRsd`, `confidencePct`, `reliabilityPct`, `dataQualityStatus`
- lifecycle: `status`, `createdAtUtc`, `updatedAtUtc`, `resolvedAtUtc`
- outcome: `outcomeStatus`, `measuredImpactRsd`, `outcomeMeasuredAtUtc`, `outcomeNotes`
- audit: `createdByUserId`, `updatedByUserId`, `updatedByUserName`, `notes[]`

Važna backend pravila već postoje:

- `rejected` i `done` su terminalni statusi i popunjavaju `resolvedAtUtc`
- `new`, `accepted` i `deferred` su otvoreni statusi i brišu `resolvedAtUtc` kada se akcija ponovo otvori
- `pending` outcome nije neuspeh; tada `measuredImpactRsd` i `outcomeMeasuredAtUtc` ostaju prazni

## Minimalne dimenzije

| Dimenzija | Zašto je potrebna | Source of truth | Napomena |
|---|---|---|---|
| Vreme kreiranja | prati dotok akcija | `createdAtUtc` | za workload i intake trend |
| Vreme zatvaranja | prati resolution flow | `resolvedAtUtc` | samo za `done` / `rejected` |
| Vreme merenja ishoda | glavni period za outcome analytics | `outcomeMeasuredAtUtc` | ne koristiti `updatedAtUtc` kao zamenu |
| Izvor akcije | poređenje dashboard/product/supplier/inventory/nivelacija/data quality | `sourceType` | osnovna poslovna segmentacija |
| Preporuka / recommendation status | grupisanje po vrsti odluke | `recommendationStatus` | bez menjanja enum/logike |
| Prioritet | da li P1 akcije daju veći ili brži rezultat | `priority` | obavezna dimenzija za fokus tima |
| Status akcije | razdvajanje otvorenih, zatvorenih i odbačenih | `status` | ne mešati sa outcome statusom |
| Outcome status | glavni rezultat | `outcomeStatus` | `pending` odvojiti od merenih ishoda |
| Kvalitet podataka | trust analiza | `dataQualityStatus` | posebno važan filter |
| Confidence / reliability cohort | validacija recommendation kvaliteta | `confidencePct`, `reliabilityPct` | bucket-i, ne sirove decimale |
| Autor / update owner | osnovni ownership pogled | `createdByUserId`, `updatedByUserId`, `updatedByUserName` | privremeni owner proxy dok nema assignee model |

## Minimalne metrike

| Metrika | Definicija | Zašto je bitna |
|---|---|---|
| Kreirane akcije | broj akcija po periodu kreiranja | meri intake |
| Zatvorene akcije | broj `done` + `rejected` po periodu zatvaranja | meri throughput |
| Akcije sa izmerenim outcome-om | broj akcija gde `outcomeStatus != pending` | meri coverage učenja |
| Outcome coverage rate | izmereni outcome / zatvorene akcije | otkriva “zatvoreno bez učenja” |
| Pozitivan outcome rate | `success` / izmereni outcome | glavni signal uspešnosti |
| Negativan outcome rate | `negative` / izmereni outcome | otkriva štetne akcije |
| Not measured share | `not_measured` / zatvorene akcije | otkriva procesni gap |
| Expected impact sum | suma `expectedImpactRsd` | planirani efekat |
| Measured impact sum | suma `measuredImpactRsd` gde postoji | ostvareni efekat |
| Impact realization ratio | measured / expected na merljivom uzorku | menadžerski signal kvaliteta akcija |
| Median days to resolve | `resolvedAtUtc - createdAtUtc` | brzina izvršenja |
| Median days to measure | `outcomeMeasuredAtUtc - resolvedAtUtc` ili `createdAtUtc` kao fallback samo u analizi | brzina zatvaranja feedback loop-a |
| Overdue open actions | otvorene akcije posle `dueAtUtc` | operativni backlog signal |

## Trust constraints

Action Outcome Analytics mora da poštuje sledeća pravila:

- `pending` outcome nije negativan ishod i ne sme da ulazi u success/failure rate kao failure.
- `measuredImpactRsd = null` nije `0 RSD`; tretirati kao unknown, ne kao nulti efekat.
- Outcome vremenske serije moraju da koriste `outcomeMeasuredAtUtc` kada postoji; `updatedAtUtc` nije biznis datum ishoda.
- Resolution metrike i outcome metrike moraju imati različite denominatore.
- `rejected` akcije treba prikazivati odvojeno od `done`, jer “nije sprovedeno” nije isto što i “sprovedeno bez efekta”.
- Source-of-truth za kvalitet preporuke ostaje backend polje (`confidencePct`, `reliabilityPct`, `dataQualityStatus`); frontend samo grupiše i prikazuje.
- Na malim uzorcima treba prikazati warning tipa “nedovoljno izmerenih ishoda”, ne jaka poređenja.

## Zavisnosti i poznati gapovi

### Već dostupno

- action list i details već nose outcome i resolution polja
- notes timeline već beleži status promene i outcome audit note
- `AnalyticsActionsPage` već prikazuje status, outcome, expected/measured impact i detalje

### Gde trenutno nedostaje struktura

- nema eksplicitnog `assignedTo` / owner polja; `updatedBy*` je samo privremeni proxy
- nema zasebnog `acceptedAtUtc`, pa “time to first action” nije pouzdano merljiv bez čitanja note istorije
- store / supplier / article drilldown nisu kanonska polja action item-a; deo segmentacije danas živi samo u `sourceKey`, `sourceId` ili `metadataJson`
- route `/analytics/actions` nema jasan period/freshness/trust sourcing kao ostali analytics report ekrani
- access-control audit je već označio action queue write/outcome rute kao P0 security gap, pa rollout outcome analytics treba da ostane read-only dok se auth ne stabilizuje

## Preporučeni staged rollout

| Faza | Scope | Šta se isporučuje | Zavisnosti |
|---|---|---|---|
| Faza 0 | definicije | zaključane metric definitions, denominator rules i trust copy | ovaj dokument |
| Faza 1 | osnovni summary | read-only summary po `sourceType`, `priority`, `outcomeStatus`, uz coverage i impact realization | postojeća action polja, bez novog workflow-a |
| Faza 2 | time series | outcome trend po periodu merenja + aging otvorenih akcija | jasan izbor datuma (`outcomeMeasuredAtUtc`, `resolvedAtUtc`, `createdAtUtc`) |
| Faza 3 | manager report | cohort pogled po confidence/data quality/source i poređenje expected vs measured impact | dovoljno velik uzorak i trust warnings |
| Faza 4 | ownership i closed-loop learning | owner/assignee pogled i jača veza sa recommendation tuning-om | budući ownership model i eventualni structured metadata |

## Preporuka za prvi isporučivi ekran

Prvi ekran/report treba da ostane mali i prodajno koristan:

- KPI: kreirane, zatvorene, outcome coverage, pozitivan outcome rate, measured impact sum
- breakdown: po `sourceType`, po `priority`, po `outcomeStatus`
- warning panel: zatvorene bez outcome-a, negativni ishodi, mali uzorak, slab data quality cohort
- trust header: period merenja outcome-a, poslednje osvežavanje, coverage warning

To je dovoljno da pilot tim pokaže da action queue nije samo lista zadataka, već zatvoren feedback loop.

## Non-goals za ovaj task

- nema novog action status workflow-a
- nema menjanja `AnalyticsActionItem` DTO shape-a
- nema novog auth sistema
- nema automatskog recommendation retuning-a na osnovu ishoda u ovoj fazi

## Sledeći logičan korak

Kada ovaj plan bude prioritet, sledeći mali task treba da bude audit postojećeg action data shape-a za agregaciju:

- koje segmentacije mogu pouzdano iz postojećih polja
- koje segmentacije danas zavise od `metadataJson`
- da li je za Phase 1 dovoljan read-only summary endpoint bez schema promene
