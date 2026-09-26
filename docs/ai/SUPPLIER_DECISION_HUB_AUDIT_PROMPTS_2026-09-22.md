# Odluke o dobavljačima — audit nalaza i queue promptovi

Datum: 2026-09-22  
Queue owner: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`  
Queue: `direct-user-request`  
Površina: `/analytics/supplier-decision-hub` → redirect na `/analytics/supplier?tab=scorecard` (`SupplierDecisionHubPage` embedded)

## Sažetak

Glavni problem je potvrđen: podrazumevani period od 30 dana nema sopstveni scorecard cache. Backend mapira svaki zahtev ≤90 dana na `mv_supplier_decision_score_cache_90d`, trust metadata to označava kao fallback (`no_mv_30d`) i blokira finalnu preporuku, a precomputed putanja dodatno join-uje all-history `mv_supplier_markdown_dependency_cache`. Korisnik zato vidi period „poslednjih 30 dana“ uz metrike i capital-at-risk koji mogu dolaziti iz šireg ili all-time dokaza.

Audit je razdvojio:

- `RQ401 READY` — usklađivanje traženog perioda i keširanog metric window-a (uključujući markdown/capital provenance);
- `RQ402 WAITING` — očuvanje per-supplier signala kada je page gate blokiran, i razdvajanje PRICE_NEGOTIATE od do_not_trust;
- `RQ403 WAITING` — paritet KPI/chart/report totala sa summary ugovorom i ispravna PoP delta;
- `RQ404 WAITING` — dohvatljivost season/minRevenue/high-confidence filtera na produkcionom redirect putu;
- `RQ405 WAITING` — srpski copy sa tačnim zamenama; širi Operacije ASCII/English ostaje u `RQ306`/`RQ325`.

Nijedan postojeći READY lane (`RQ385`, `RQ388`, `RQ389`) nije demotovan: Supplier Decision Hub je odvojen feature family / owner file set. Supplier Sales `RQ373`–`RQ380` ostaju WAITING.

## Pregled koda

Pregledani su:

- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx` (+ CSS)
- `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`, `supplierSharedState.ts`, `useSupplierCanonicalState.ts`
- `Klijent/clientapp/src/pages/SupplierRedirects.tsx`, `App.tsx` route
- `Klijent/clientapp/src/components/supplierDecisionHub/**`
- `Klijent/clientapp/src/services/supplierDecisionHubApi.ts`, `supplierDecisionReport.ts`, `supplierDecisionReportQuery.ts`, `supplierDecisionMargin.ts`
- `Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`, `components/analytics/SupplierDecisionReport*.tsx`
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `Api.Tests/SupplierDecisionHubContractTests.cs`, `SupplierDecisionSchemaSqlTests.cs`
- `Database/Migrations/018_AddSupplierDecisionHubViews.sql`, `029_AddSupplierDecisionWindowedViews.sql`
- `Api/Services/Startup/SupplierDecisionSchemaRepairHostedService.cs`
- postojeći hub focused specs i raniji supplier-decision RQ completion notes (`RQ122`, `RQ163`, `RQ234`, `RQ249`, `RQ250`)

## Potvrđeni nalazi

### 1. Traženi 30d period nije mereni 30d scorecard prozor

Dokaz:

- kanonski default je `getPresetRange("30d")` (`useSupplierCanonicalState.ts`, hub page);
- `GetDecisionScoreWindowDays` vraća 90 za svaki eksplicitni opseg ≤90 dana;
- `BuildScorecardTrustMetadata` postavlja `UsedFallback` i `no_mv_30d` za 30d zahtev; contract test to zaključava kao očekivano;
- precomputed SQL overlap-filteruje redove iz 90d MV (`period_to >= @fromDate AND period_from <= @toDate`);
- markdown dependency / dead stock / unsold value dolaze iz all-history `mv_supplier_markdown_dependency_cache` bez window predikata;
- trust header prikazuje `effectiveFrom`/`effectiveTo` iz min/max period bounds redova, ne nužno izabrani kalendarski opseg.

Rizik: korisnik donosi odluku na „30 dana“ dok KPI i capital-at-risk mogu biti 90d/all-time dokaz. Ovaj nalaz je `RQ401`.

### 2. Page gate briše per-supplier signal i meša PRICE_NEGOTIATE sa „Ne veruj“

Dokaz:

- kada je `recommendationAllowed === false`, svaki red postaje `insufficient_data`;
- `recommendationToStatus` mapira i `PRICE_NEGOTIATE` i `ASSORTMENT_REDUCE` na `do_not_trust`;
- snapshot bira EXPAND vs ASSORTMENT_REDUCE samo iz booleana gate-a;
- `RQ249` je zatvorio action CTA, ali nije sačuvao identity signala u tabeli.

Rizik: operator vidi jednoliki „pomoćni signal“ ili pogrešan „Ne veruj“ umesto pregovora o ceni. Ovo je `RQ402`.

### 3. KPI / chart / report totali se računaju mimo summary ugovora

Dokaz:

- page sabira `totalRevenue`, top-5 share i margin contribution iz ranking redova;
- summary već ima ponderisane agregate i `capitalAtRisk`;
- PoP „promena udela pune cene“ koristi `calculateSupplierQualityTrendPct`, čija je definicija full-price minus markdown share;
- report payload prima klijentske totale.

Rizik: kartice, grafikon i PDF/report se razilaze; PoP delta liči na markdown trend. Ovo je `RQ403`.

### 4. Produkcija sakriva filtere koje API i report i dalje podržavaju

Dokaz:

- `/analytics/supplier-decision-hub` redirectuje na consolidated scorecard tab;
- embedded hub ne renderuje season / minRevenue / onlyHighConfidence;
- `SupplierCanonicalFilters` nema ta polja;
- report href builder i backend i dalje prihvataju te filtere;
- page specs montiraju hub mimo redirecta.

Rizik: dokumentovani filteri nisu dohvatljivi na stvarnom putu. Ovo je `RQ404`.

### 5. Engleski / ASCII copy na površini

Dokaz (izbor):

- `Supplier decision scorecard`, `Supplier decision materialized view`, `Supplier explainability snapshot`;
- `Scorecard signal`, `Confidence signala`, `Data quality`, `canonical decision surface`;
- `Supplier analytics`, `Sell-through`, `Dead stock`, `Trust signala`, `Fallback`, `Dataset`;
- `Otvori Scorecard`;
- ASCII: `Povecati`, `Zadrzati`, `Kriticno`, `Pojacaj`, `Zadrzi`, backend `Trazeni`/`Neograniceno`/`pomocni`.

Tačne zamene su u `RQ405`. Širi Operacije pass ostaje `RQ306`/`RQ325` — nije otvoren novi globalni copy prompt.

### 6. Povezani već postojeći nalazi — ne duplirati

- `RQ234` — filter fidelity u report deep-linkovima (DONE);
- `RQ249` — actionability gate na CTA (DONE);
- `RQ250` — margin contribution weighting (DONE);
- `RQ163` — post-observation vs measured zero (DONE);
- `RQ373`–`RQ380` — Supplier Sales lane (WAITING; drugi owner surface);
- `RQ305` — Operacije menu alias IA;
- `RQ306` / `RQ325` — shared diacritics / residual English.

Ovi promptovi nisu ponovo kopirani.

## Šta nije menjano

- Nije menjan runtime kod, backend ugovor, SQL MV, algoritam preporuke niti produkcioni podaci.
- Nije rađen live browser/backend smoke; ovo je statički i ugovorni audit.
- Nije izmišljena poslovna odluka da li mora postojati 30d MV; `RQ401` zahteva eksplicitan ugovor i testove.

## Queue stanje posle audita

| Prompt | Status | Namena |
|---|---|---|
| `RQ401` | `READY` | traženi period vs keširani metric window + markdown/capital provenance |
| `RQ402` | `WAITING` | per-supplier signal identity i PRICE_NEGOTIATE mapiranje |
| `RQ403` | `WAITING` | KPI/chart/report parity i PoP delta semantika |
| `RQ404` | `WAITING` | dohvatljivost scorecard filtera posle redirecta |
| `RQ405` | `WAITING` | srpski copy za hub; referenca na `RQ306`/`RQ325` |
| `RQ385`/`RQ388`/`RQ389` | `READY` | nepromenjeni nezavisni lane-ovi |

`MASTER_ROADMAP.md` i kanonski RQ queue su usklađeni sa ovim stanjem.

## RQ439 triage na aktuelnom `main` — 2026-09-26

PR #63 nije merdžovan: aktuelni `main` je u međuvremenu upotrebio RQ401–RQ405 za druge Supplier Decision vlasnike i te promptove završio. Triage je zato mapirao nalaze bez ponovne upotrebe tih ID-jeva:

| Nalaz iz PR #63 | Aktuelni ishod |
|---|---|
| Traženi period naspram cache window-a | Pokriven isporučenim `RQ401` + `RQ404` cache/provenance i effective-period ugovorom |
| Filteri koji nisu dohvatljivi na redirect putu | Pokriven isporučenim `RQ403` canonical filter parity ugovorom |
| Engleski/ASCII copy | Pokriven isporučenim `RQ405` localization ownerom |
| Per-supplier signal se briše kada je actionability gate blokiran; `PRICE_NEGOTIATE` se prikazuje kao `do_not_trust` | I dalje potvrđeno na aktuelnom kodu (`SupplierDecisionHubPage.tsx:213-219,533-540`); re-queue kao `RQ458 WAITING` |
| KPI/chart/report totals i PoP delta koriste različite/popunjene projekcije | I dalje potvrđeno (`SupplierDecisionHubPage.tsx:520-594,840-860`); re-queue kao `RQ459 WAITING` |

`RQ439` je završen kao read-only triage. PR #63 je superseded i ne treba ga merdžovati; nova implementacija treba da koristi `RQ458` i `RQ459`.
