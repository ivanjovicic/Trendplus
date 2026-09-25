# Operacije — audit nalaza i queue promptovi

Datum: 2026-09-25

Queue owner: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

Queue: `direct-user-request` (lokalno, bez remote-a i bez cloud agenata)

Osnova: `main` @ `00accd93`

Površina: osam ekrana menija Operacije (`Klijent/clientapp/src/layout/navConfig.ts:150-189`, rute `App.tsx:105-120`)

## Sažetak

Statički i test audit svih osam Operacije ekrana pronašao je dva P1 problema na ekranu Zalihe i dopuna (dugme „Ponovi učitavanje“ ne pokreće novi zahtev; greška i prazno stanje sakrivaju sve kontrole), P2 nalaze na Prodaji po smeni (dijagnostika „Neusklađeni računi“ poredi nepovezane ID-jeve; prazno stanje se nikad ne prikazuje; ista labela sa dva broja), Prioritetima nivelacije (KPI definicije se ne slažu sa backend sumama i tooltip-ovima; filter opcije se sužavaju na trenutni izbor) i Prodaji po tipu obuće (sirovi `ex.Message`), kao i pet zastarelih testova koji drže suite crvenim.

Novi promptovi su `RQ427`-`RQ437`. Pet njih je `READY` jer su nezavisni, bez zajedničkih fajlova i u različitim feature family-jima; ostali su `WAITING` iza istog fajla ili vlasničke odluke. Novi engleski/ASCII stringovi su dodati u postojeći `RQ325`; `RQ306` je već `DONE` i nije ponovo otvoren.

## Ekrani

| Ekran | Ruta | Fajl |
|---|---|---|
| Zalihe i dopuna | `/analytics/inventory` | `pages/InventoryPage.tsx` |
| Prodaja po dobavljačima | `/analytics/supplier-sales-stats` | preusmerenje na `/analytics/supplier?tab=overview` (`SupplierRedirects.tsx:15-17`) |
| Prodaja po tipu obuće | `/analytics/shoe-type-sales-stats` | `pages/ShoeTypeSalesStatsPage.tsx` |
| Prodaja po smeni i dobavljačima | `/analytics/daily-sales` | `pages/DailySalesStatsPage.tsx` |
| Pre/Posle nivelacije | `/analytics/nivelacije-pre-post` | `pages/ProdajaPrePostNivelacijePage.tsx` |
| Prodaja po boji artikla | `/analytics/color-sales-stats` | `pages/ColorSalesStatsPage.tsx` |
| Prioriteti nivelacije | `/analytics/pre-nivelacija-prioriteti` | `pages/PreNivelacijaPriorityPage.tsx` |
| Dobavljači i tipovi obuće | `/analytics/dobavljaci-tipovi-obuce` | preusmerenje na `/analytics/supplier?tab=assortment` |

Preusmerenja čuvaju query parametre i dodaju `legacySource` (`SupplierRedirects.tsx:6-13`); sam supplier hub nije detaljno ponovo auditovan (RQ373-RQ380, RQ401-RQ406, RQ419-RQ425 su `DONE`).

## Potvrđeni novi nalazi

### 1. Zalihe i dopuna — retry ne radi i nema izlaza iz greške/praznog stanja (`RQ427`, P1)

- `InventoryPage.tsx:436` deklariše `reloadNonce`, postavlja se na `538` i `1360`, ali se nigde ne čita; `inventoryQuery` zavisnosti na `686` ga ne sadrže.
- `InventoryPage.tsx:1363-1400` vraća grešku/prazno stanje pre glavnog layout-a (`1402+`), pa nestaju pretraga, filteri i period; tekst na `1391` traži promenu filtera bez kontrola.
- Test `InventoryPage.queueStatus.spec.tsx:331` pada: posle retry-ja tabela se ne vraća. Regresija pored `RQ351`.

### 2. Prodaja po smeni — „Neusklađeni računi“ spaja nepovezane ključeve (`RQ428`, P2)

- `DailySalesStatsService.cs:120-165` grupiše DnevnikPromena po `d.Id` i poredi sa totalima računa po `pz.Id`; `DnevnikPromena` nema FK ka zaglavlju prodaje. Broj, iznos, crveni ton i uvid „Prodaja traži rekonsilijaciju“ nisu dokaz. `RQ382` pokriva samo `dataScope`, ne ključ.

### 3. Prodaja po smeni — prazno stanje, dupli brojevi, MA7 i tooltip (`RQ429`, P2)

- Backend uvek vraća red po danu (`DailySalesStatsService.cs:440-470`), pa `emptyStateHint`/`emptyStateVariant` (`DailySalesStatsPage.tsx:819-857`) i akcija „Prikaži dostupne podatke“ (`1661-1668`, `1417-1429`) nisu dostižni; period bez prodaje pokazuje 30 nula i „Stabilan pregled“ (`1297-1303`) dok trust header kaže `no_data_in_period` (`583-588`). `RQ352` ovde nema efekta.
- „Dani sa nepotpunom satnicom“: mini-stat `missingShiftCount` (`2037`) vs kartica kvaliteta `incompleteShiftCount` (`1135`).
- `buildRollingAverage` (`549-557`) uključuje tekući dan; prvih šest dana koristi delimične prozore sa labelom MA7.
- Tooltip na `1682` pogrešno opisuje top-N efekat.

### 4. Prodaja po smeni — sortiranje, štampa, objekat u izvozu (`RQ430`, P3)

- `handleSort` (`1351-1360`) menja stanje unutar updater-a, pa se pod StrictMode (`main.tsx:20`) smer menja dvaput; ternarni izraz je bez efekta.
- Obrazac za štampu (`1438-1446`) meša ključeve i zaglavlja („Uk. sm.“ / „Ost.“).
- Izvoz prikazuje ID objekta umesto naziva (`889`).

### 5. Prodaja po smeni — ugovor koncentracije dobavljača (`RQ431`, P2, čeka odluku)

- `DailySalesStatsPage.premium.spec.tsx:605-644` očekuje upozorenje kada top dobavljač (25) premaši ukupno (18); `buildSupplierConcentration` (`376-484`) nema tu proveru posle `RQ381`. Potrebna je odluka vlasnika: regresija `RQ242` ili izmenjen ugovor.

### 6. Prioriteti nivelacije — KPI definicije (`RQ432`, P2)

- „Zaliha pod rizikom“: backend sabira samo visok prioritet (`PreNivelacijaPriorityEndpoints.cs:567`), tooltip kaže sve prikazane (`PreNivelacijaPriorityPage.tsx:1156`).
- „Procena povećanja prihoda“: backend sabira pozitivne delte svih kandidata, i blokiranih (`569`), tooltip kaže „Pojačaj“ (`1161`), tabela skriva blokirane delte (`239-241`, `1306`).
- „Izbegljiv gubitak“ uključuje redove bez troška (`568`), detalj kaže „Nije dostupno bez troška“ (`1378-1382`).

### 7. Prioriteti nivelacije — filter opcije se sužavaju (`RQ433`, P2)

- Opcije dobavljača dolaze iz `supplierLeaderboard` (`531-534`) koji je već filtriran (`129-142`, `429-467`); sezone/tipovi isto (`873`, `895`). Delimična regresija `RQ344`.

### 8. Prioriteti nivelacije — prikaz (`RQ434`, P3)

- Negativan WoW (`643-651`) se seče na 0..100 (`703`, `206-210`); tooltip grana `174-176` je mrtva (posledica `RQ424` ugovora).
- Nevalidne vrednosti filtera (`987`, `994`) se tiho vraćaju na 40/14 uz dupli fetch (`117-127`, `430-431`, `451-466`).
- Sirovi kodovi `high/medium/low` (`1502`, `1520`, `1537`), brojevi redova posle `.Take(30)` (`475`, `480`, `488`), zeleni ton za rizik (`1151`, `933`), sirovi ID-jevi u izvozu (`812-818`), marker sortiranja `^`/`v` (`271`).

### 9. Tip obuće (i prikaz Pre/Posle, Boja) — sirove greške (`RQ435`, P2)

- `AllEndpoints.cs:2810-2813` vraća `detail: ex.Message` i naslov bez dijakritika; `analyticsErrorMessages.ts:7-35` je blacklist; stranice prikazuju `reason.message` (`ShoeTypeSalesStatsPage.tsx:441-443`, `ProdajaPrePostNivelacijePage.tsx:633-635`, `ColorSalesStatsPage.tsx:362-364`).

### 10. Mrtvi i frontend-izvedeni KPI (`RQ436`, P3)

- „Udeo top 5“ je uvek N/A (Tip obuće `543`, `1090-1092`; Boja `457`, `980-981`); nedostižna `filtered_out` grana (`ShoeTypeSalesStatsPage.tsx:690`); Pre/Posle „Top 5 udeo u promeni“ i rast perioda računati u frontendu (`765-779`, `1572-1574`); sirovi ID objekta (`ShoeTypeSalesStatsPage.tsx:699`, `ProdajaPrePostNivelacijePage.tsx:1344`); `setTimeout` bez čišćenja (`ColorSalesStatsPage.tsx:444-454`).

### 11. Zastareli testovi (`RQ437`, P2)

- `ColorSalesStatsPage.premium.spec.tsx:623` („Pokrice marze“ vs „Pokriće marže“), `ExportSchedulerPanel.spec.tsx:91` (generički vs specifični fallback), `DailySalesStatsServiceTests.cs:355` („iskljucena“ vs „isključena“), `SupplierDecisionSchemaSqlTests.cs:139` (source-text token vs `AllEndpoints.cs:1804`), `AnalyticsCostSnapshotServiceTests.cs:138` (fixture bez `ShoeTypeIdAtSale`, RQ411).

## Već u queue-u — ne duplirati

- `RQ384` — Daily Sales `detail: ex.Message` (`DailySalesStatsEndpoints.cs:103`) i poruka sa imenima parametara (`35`).
- `RQ382` — dijagnostika i dostupnost ignorišu `dataScope`.
- `RQ383` — prodaja van smene/bez vremena mapirana u prvu smenu.
- `RQ321` — greška učitavanja prodavnica se guta (Daily, Tip obuće, Pre/Posle, Boja).
- `RQ322` — Inventory `console.error`/`console.warn` pri bootstrap-u.
- `RQ327` — sortiranje Daily Sales nije u URL-u; `RQ326` — isto za Prioritete.
- `RQ317`/`RQ318` — fokus/filteri nisu u URL-u; `RQ319` — preset primenjuje neprimenjene izmene.
- `RQ328` — Pre/Posle proširen red se resetuje na svaku promenu podataka.
- `RQ325` — preostali engleski copy; dopunjen ovim auditom (vidi dole).
- `RQ329` — mrtva labela skraćivanja na Tipu obuće.

## Copy (RQ325 dopuna)

Dodato u `RQ325` Evidence kao „Addendum 2026-09-25“: engleski/tehnički stringovi na Daily Sales, sirovi `dataScope` kodovi na četiri ekrana, developerski tekst na Prioritetima i Pre/Posle, Inventory „refresh status i data quality signal“, i ASCII ostaci posle `RQ306` (npr. „Cet“ → „Čet“, „Prosecna“ → „Prosečna“, „Najveci skok/pad“ → „Najveći skok/pad“, „Podaci jos nisu“ → „Podaci još nisu“). `RQ306` je `DONE`, zato je dodata samo kratka napomena o preusmeravanju ostataka u `RQ325`.

## Mehaničke ispravke queue-a

- `Status:` linije sekcija `RQ301`, `RQ302`, `RQ306`, `RQ308` i `RQ371` bile su `WAITING` iako njihove completion note beleže `DONE` sa sinhronizovanim dokazima — usklađeno na `DONE`.
- Redovi status tabele za `RQ306` (`IN_PROGRESS`) i `RQ307` (`WAITING`) usklađeni na `DONE` prema completion notama.
- Nisu menjani stariji nesklađeni redovi bez completion note (npr. `RQ128`, `RQ141`-`RQ149`, `RQ191`, `RQ192`) jer nisu jasna mehanička ispravka.

## Provere tokom audita

- `npm run typecheck` — prolazi.
- Ciljani Vitest (74 fajla, 537 testova) — 533 prolaze, 4 padaju: `InventoryPage.queueStatus.spec.tsx:331` (stvarni bag, `RQ427`), `DailySalesStatsPage.premium.spec.tsx:605-644` (`RQ431`), `ColorSalesStatsPage.premium.spec.tsx:623` i `ExportSchedulerPanel.spec.tsx:91` (`RQ437`).
- Ciljani `dotnet test` (245 testova) — 225 prolaze, 15 preskočeno, 5 pada: tri zastarela testa (`RQ437`) i dva integraciona Inventory testa koja zahtevaju PostgreSQL (greška autentifikacije 28P01 — okruženje, nije dokazano).

## Šta nije menjano

- Nije menjan runtime kod, testovi, backend ugovor, baza ni produkcioni podaci.
- Nije rađen live browser/backend smoke.
- Promene su lokalno commit-ovane na `main` (bez push-a), po zahtevu korisnika.

## Queue stanje posle audita

| Prompt | Status | Ekran | Namena |
|---|---|---|---|
| `RQ427` | `READY` (primarni) | Zalihe i dopuna | retry koji zaista osvežava + kontrole u grešci/praznom stanju |
| `RQ428` | `READY` | Prodaja po smeni | ključ rekonsilijacije računa |
| `RQ429` | `WAITING` | Prodaja po smeni | prazno stanje, nepotpuna satnica, MA7, tooltip |
| `RQ430` | `WAITING` | Prodaja po smeni | sortiranje, štampa, naziv objekta u izvozu |
| `RQ431` | `WAITING` | Prodaja po smeni | odluka o ugovoru koncentracije |
| `RQ432` | `READY` | Prioriteti nivelacije | KPI definicije |
| `RQ433` | `WAITING` | Prioriteti nivelacije | filter opcije iz celog univerzuma |
| `RQ434` | `WAITING` | Prioriteti nivelacije | WoW, validacija filtera, labele, brojevi, tonovi |
| `RQ435` | `READY` | Tip obuće / Pre-Posle / Boja | bezbedne greške |
| `RQ436` | `WAITING` | Tip obuće / Boja / Pre-Posle | mrtvi i frontend-izvedeni KPI |
| `RQ437` | `READY` | više ekrana (testovi) | zastareli testovi |

`MASTER_ROADMAP.md` i kanonski RQ queue su usklađeni sa ovim stanjem.
