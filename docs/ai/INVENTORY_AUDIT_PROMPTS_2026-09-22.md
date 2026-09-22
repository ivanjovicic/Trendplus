# Zalihe i dopuna — audit nalaza i queue promptovi

Datum: 2026-09-22  
Queue owner: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`  
Queue: `direct-user-request`  
Površina: `/analytics/inventory` (`Klijent/clientapp/src/pages/InventoryPage.tsx`)

## Sažetak

Glavni problem je potvrđen: ekran nema izbor perioda, a trust header zato prikazuje `Period nije definisan`. Istovremeno, lista/detalj koriste skriveni klizni prozor od 30 dana, dok bilans i deo operativnih panela predstavljaju trenutno stanje ili snapshot. To nije dovoljno objašnjeno korisniku i otežava reprodukciju odluke o dopuni.

Audit je razdvojio:

- `RQ308 READY` — vidljiv izbor perioda, URL stanje i trust/provenance objašnjenje;
- `RQ371 WAITING` — usklađivanje perioda i `dataScope` kroz sekundarne signalne endpoint-e, upite i cache ključeve;
- `RQ372 WAITING` — server-side alert filter, tačno brojanje i URL stanje;
- postojeći `RQ325 WAITING` — završni pregled engleskih reči i mešanog copy-ja na svim Operacije površinama.

Queue namerno ima samo jedan `READY` prompt u RQ programu, u skladu sa `PROMPT_QUEUE_PROTOCOL.md`.

## Pregled koda

Pregledani su:

- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/services/analyticsApi.ts`
- `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader.tsx`
- `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.tsx`
- `Klijent/clientapp/src/components/inventory/InventoryAlertsFeed.tsx`
- `Klijent/clientapp/src/components/inventory/DemandForecastPanel.tsx`
- `Klijent/clientapp/src/components/inventory/RebalancingTable.tsx`
- `Klijent/clientapp/src/components/inventory/SizeCurvePanel.tsx`
- `Klijent/clientapp/src/components/inventory/InventoryItemsTable.tsx`
- `Klijent/clientapp/src/components/inventory/InventoryKPICards.tsx`
- `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
- `Api/Endpoints/InventoryEndpoints.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- inventory forecast/alerts/rebalance/size-curve query contracts and handlers
- postojeće Inventory focused specifikacije i raniji Inventory UX/audit dokumenti

## Potvrđeni nalazi

### 1. Period nije izabran i ne postoji kontrola

Dokaz:

- `InventoryPage.tsx` prosleđuje `periodFrom={null}` i `periodTo={null}` u `AnalyticsTrustHeader`;
- `AnalyticsControlBar` sadrži pretragu, prodavnicu, dobavljača, sortiranje i veličinu strane, ali nema period;
- `createInventorySignalWindow()` potajno pravi klizni prozor od 30 dana;
- `InventoryEndpoints.cs` list/detail podrazumevaju poslednjih 30 dana kada datum nije prosleđen;
- bilans, uvidi, workflow i poređenje prodavnica nemaju isti period parametar i koriste snapshot/current-state semantiku.

Rizik: korisnik ne zna da li gleda trenutne zalihe, 30-dnevni signal ili kombinaciju više prozora. Ovaj nalaz je `RQ308`.

### 2. Sekundarni signali nemaju dokazanu period/data-scope paritetu

Dokaz:

- frontend helper automatski dodaje globalni `dataScope` na analytics URL;
- `/inventory/forecast`, `/inventory/alerts`, `/inventory/rebalance-suggestions` i `/inventory/size-curve` u `CachedAnalyticsEndpoints.cs` ne primaju `dataScope`, `fromDate` ili `toDate`;
- odgovarajući MediatR query ugovori i SQL handleri filtriraju po prodavnici/dobavljaču/SKU/limitu, ali ne po periodu ili `dataScope`;
- Inventory stranica te rezultate prikazuje uz listu koja već koristi periodni signalni prozor.

Rizik: nakon promene opsega podataka ili perioda, korisnik može videti listu iz jednog ugovora i forecast/alert/transfer signal iz drugog. Ovo je odvojeno u `RQ371`; ne treba ga rešavati samo frontend tekstom.

### 3. Alert filter prikazuje lokalno filtriran rezultat sa nefiltriranim brojačem

Dokaz:

- `InventoryPage` čuva `alertSeverityFilter`, ali poziva `getInventoryAlerts` bez `severity`;
- `InventoryAlertsFeed` lokalno filtrira već vraćene/top-limitirane stavke;
- `returnedCount` i `totalMatchingCount` ostaju brojevi za nefiltrirani odgovor;
- izabrani nivo upozorenja nije u URL-u.

Rizik: korisnik izabere `Kritično`, vidi samo deo kritičnih upozorenja, a brojač opisuje sve nivoe; rezultat se gubi pri osvežavanju ili deljenju linka. Ovo je `RQ372`.

### 4. Engleski i tehnički termini i dalje postoje u korisničkom copy-ju

Iako je `RQ301` zatvorio primarni cockpit copy, aktuelni pregled je našao ostatke koje treba obraditi u postojećem `RQ325`:

- `Alerts`, `Forecast`, `snapshot`;
- `OOS`, `SKU`, `Status`, `Info`, `N/A`;
- `Sell-through`, `Snapshot`, `Aging 90+`;
- mešanje „opseg podataka“, snapshot i signal terminologije u export/empty/fallback tekstu.

Backend enum/code vrednosti ne treba prevoditi tako da se pokvari ugovor. Korisničke labele, pomoćni tekst, aria-label tekst, empty/error poruke i export metadata treba prevesti na srpski sa dijakriticima i doslednim terminima. Nije otvoren novi prompt jer `RQ325` već poseduje ovaj owner i scope.

### 5. Povezani već postojeći nalazi — ne duplirati

Audit je proverio i ranije promptove:

- `RQ322` — greška učitavanja prodavnica se još mora vidljivo obraditi umesto samo `console.error`;
- `RQ323` — stale sekundarni paneli pri parcijalnom padu moraju se očistiti ili jasno označiti;
- `RQ324` — greška size-curve detalja mora ostati različita od legitimno praznog rezultata;
- `RQ133` — nedostajući forecast risk/confidence ne smeju se rangirati kao `0`;
- `RQ325` — završni Operacije localization pass.

Ovi promptovi ostaju odvojeni od novog period/data-scope ugovora i nisu ponovo kopirani u queue.

## Šta nije menjano

- Nije menjan runtime kod, backend ugovor, baza, algoritam preporuke niti produkcioni podaci.
- Nije rađen live browser/backend smoke; ovo je statički i ugovorni audit.
- Nije izmišljena poslovna odluka da li svaki Inventory panel treba da bude period-dependent. `RQ308` i `RQ371` zahtevaju da se ta razlika eksplicitno dokumentuje i testira.

## Queue stanje posle audita

| Prompt | Status | Namena |
|---|---|---|
| `RQ308` | `READY` | izbor perioda, URL state i trust/snapshot provenance |
| `RQ371` | `WAITING` | period/data-scope paritet sekundarnih signalnih endpoint-a |
| `RQ372` | `WAITING` | alert severity filter, count parity i URL state |
| `RQ325` | `WAITING` | završni srpski copy pass, dopunjen aktuelnim Inventory ostacima |

`MASTER_ROADMAP.md` i kanonski RQ queue su usklađeni sa ovim stanjem.
