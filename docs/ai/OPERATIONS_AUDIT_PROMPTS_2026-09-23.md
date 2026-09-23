# Operacije — audit toka podataka i dokazivanja tačnosti

Datum: 2026-09-23  
Zahtev: direktan repository task  
Queue: `direct-user-request`  
Status: audit i preciziranje queue promptova; proizvodni kod nije menjan

## Zaključak

Nije dokazano da su svi podaci na ekranima iz menija **Operacije** tačni u smislu end-to-end poslovne reconciliacije. Dokazani su delovi sistema: rute i redirecti, pojedinačni API ugovori, deo frontend runtime validacije i više fokusiranih regresija. Nije dokazano da jedan isti skup prodajnih, povratnih, cenovnih, zališnih i scope činjenica daje usklađene rezultate na svih osam ekrana, niti je za sve ekrane izvršena validacija nad živom bazom/deploymentom.

To znači:

- ne postoji dokaz da su svi KPI-jevi brojčano tačni za realne podatke;
- postoje poznati otvoreni rizici koji su već dobili vlasnike u queue-u;
- pronađen je novi konkretan rizik na ekranu **Dobavljači i tipovi obuće**;
- najveće unapređenje je jedan deterministički Operations proof pack koji proverava tok od izvora do KPI-ja, tabele, detalja i izvoza.

## Obuhvat menija

| Stavka menija | Ruta / stvarni ekran | API porodica | Status dokaza |
|---|---|---|---|
| Zalihe i dopuna | `/analytics/inventory` → `InventoryPage` | inventory balance/list/insights/actions + forecast/alerts/rebalance/size-curve | Rute i deo kontrakta su pokriveni; scope/period sekundarnih signala je otvoren (`RQ371`), severity/URL filter je otvoren (`RQ372`). |
| Prodaja po dobavljačima | `/analytics/supplier-sales-stats` → canonical Supplier overview redirect | `/api/analytics/supplier-sales-stats` i canonical Supplier površine | Backend i deo fixture testova postoje; klijentski Supplier Sales odgovor nema runtime schema validaciju (`RQ379`), a visible-scope/detail/margin/aggregate follow-up-i su `RQ373`, `RQ374`, `RQ378`-`RQ380`. |
| Prodaja po tipu obuće | `/analytics/shoe-type-sales-stats` → `ShoeTypeSalesStatsPage` | `/api/analytics/shoe-type-sales-stats` | Strict schema, backend contract i raniji aggregate/detail follow-up-i postoje; nije deo jedinstvene cross-screen reconciliacije nad istim fixture-om. |
| Prodaja po smeni i dobavljačima | `/analytics/daily-sales` → `DailySalesStatsPage` | `/api/analytics/daily-sales` | Signed quantity/revenue i reconciliation ugovori imaju vlasnike `RQ381`-`RQ384`; nije dokazano da su rezultati usklađeni sa Supplier/Shoe/Color ekranima na istom ulazu. |
| Pre/Posle nivelacije | `/analytics/nivelacije-pre-post` → `ProdajaPrePostNivelacijePage` | `/api/analytics/vendor-sales-nivelacija` | Scope/cache, cohort/denominator i safe-error ugovori su `RQ385`-`RQ387`; ostaje potreba za cross-screen i detail-derived dokazom. |
| Prodaja po boji artikla | `/analytics/color-sales-stats` → `ColorSalesStatsPage` | `/api/analytics/color-sales-stats` | Scope, signed values, cost/margin, cohort, cache, detail, identity, provenance i decision score su `RQ389`, `RQ392`-`RQ400`; i dalje nema zajedničkog Operations source-to-screen dokaza. |
| Prioriteti nivelacije | `/analytics/pre-nivelacija-prioriteti` → `PreNivelacijaPriorityPage` | `/api/analytics/pre-nivelacija-prioriteti` | Population, scoring window i runtime schema su `RQ388`-`RQ391`; nije izvršena zajednička reconciliacija sa prodajnim ekranima. |
| Dobavljači i tipovi obuće | `/analytics/dobavljaci-tipovi-obuce` → canonical Supplier assortment redirect | `/api/analytics/vendor-sales-nivelacija` | Osnovni pre/post ugovor postoji, ali je pronađen novi problem sa izvedenim tip-insight metrima kada je article detail skraćen; novi `RQ406`. |

Rute su proverene u `Klijent/clientapp/src/layout/navConfig.ts`, `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts` i `Klijent/clientapp/src/App.tsx`. Route smoke pokriva dostupnost i canonical redirect ponašanje, ali sam po sebi ne dokazuje brojke.

## Dokazi koji postoje

1. Frontend stranice uglavnom prosleđuju period, prodavnicu i `dataScope` za svoje glavne aggregate endpoint-e.
2. Shoe Type, Color, Daily Sales, Pre-Nivelacija i Pre/Post imaju typed/runtime response ugovore ili fokusirane validacije; backend više površina vraća `meta`, quality i provenance informacije.
3. `RQ114` je već isporučio reusable deterministic seed pack za šire pilot analytics porodice.
4. Postoje ciljane regresije za empty/error/degraded/no-fake-zero i za više scope/cohort edge case-ova.

## Poznate greške i otvoreni rizici

### 1. Inventory sekundarni paneli nemaju isti scope/period ugovor

`InventoryPage` u istom lifecycle-u poziva balance/list/insights/store comparison/action suggestions sa scope informacijom, ali forecast, alerts i rebalance poziva bez istog `dataScope`/period konteksta. `getForecast` i `getRebalanceSuggestions` nemaju te parametre, a alerts poziv sa stranice ne prosleđuje severity filter. Ovo može prikazati signal iz druge populacije ili perioda pored glavnog KPI-ja. Vlasnici su već `RQ371` i `RQ372`; ovaj audit ne pravi duplikat.

### 2. Supplier Sales nema zatvoren klijentski runtime schema dokaz

`getSupplierSalesStats` koristi generički `fetchAnalyticsJson<SupplierSalesStatsResponse>` bez prosleđene Zod/response schema validacije, za razliku od nekih novijih Operacije endpoint-a. Tipovi u TypeScript-u nisu runtime dokaz da je payload koji je stigao sa servera potpun i semantički validan. To je već obuhvaćeno `RQ379`.

### 3. Supplier Footwear može prikazati izvedene tip-metrike iz skraćenog detalja — novi nalaz

Backend u `Api/Endpoints/AllEndpoints.cs` formira `analyzed` iz punog canonical cohort-a, računa totals/category aggregate nad punim skupom, ali vraća `articleStats = analyzed.Take(maxRows)`; podrazumevani limit je 5.000. Istovremeno `SupplierFootwearAnalyticsPage.tsx` funkcija `buildTypeInsights` računa globalnu koncentraciju po tipu, dominantan tip, vendor top-type share i prosečnu elastičnost iteriranjem samo kroz `articleStats`.

Odgovor već nosi `ReturnedRows`, `TruncatedRows` i `IsDetailTruncated`, a warning se može prikazati. Međutim, sama izvedena type metrika se i dalje formira iz parcijalnog niza i nema sopstveni full-cohort denominator/partial label. Ako se redosled prvih 5.000 redova razlikuje od punog skupa, dominantan tip i udeo mogu biti pogrešni iako su backend totals tačni. Ovo je novi `RQ406`.

### 4. Nema jednog dokaza za svih osam ekrana

Postoje pojedinačni testovi i route smoke, ali nema jednog manifest-a koji istovremeno proverava:

- signed prodaju, povrate i korekcije;
- dobavljač/tip/boju i nepoznate dimenzije;
- trošak i nedostajući trošak;
- pre/post cohort i Pre-Nivelacija scoring;
- smene i off-shift promet;
- inventory stanje, upozorenja i data-origin scope;
- isti period, prodavnicu i `all`/`existing`/`imported` scope kroz osam route porodica.

Supplier i Shoe Type integration testovi sadrže guard `TRENDPLUS_RUN_INTEGRATION_TESTS=true` i inače se vraćaju bez asercija. To je prihvatljivo kao lokalna opt-in konvencija samo ako se u dokaznom izveštaju jasno prikaže da live fixture nije izvršen; nije dokaz da su realni podaci tačni. Novi `RQ407` treba da proširi postojeći `RQ114` pack, a ne da napravi drugi paralelni seed sistem.

## Šta unaprediti, po prioritetu

1. **P1 — RQ406:** ukloniti mogućnost da se type concentration/dominant type/elasticity računaju kao autoritativne metrike iz skraćenog detail payload-a.
2. **P1 — RQ407:** napraviti deterministic cross-screen manifest i izvršnu reconciliaciju svih osam Operacije ruta.
3. **Postojeći P1/P2 backlog:** završiti `RQ371`/`RQ372` za Inventory, `RQ379` za Supplier runtime schema, `RQ373`/`RQ374`/`RQ378`-`RQ380` za Supplier Sales i `RQ381`-`RQ400` follow-up ugovore prema njihovim statusima.
4. **Operativni dokaz:** svaki budući report tačnosti mora jasno razdvojiti route/contract dokaz, deterministic integration dokaz i live/deployed dokaz; “test nije pokrenut” ne sme biti predstavljeno kao pass.

## Ograničenja ovog audita

- Nije pokrenut browser protiv produkcijske ili razvojne žive baze.
- Nije izvršena potpuna end-to-end reconciliacija osam ekrana.
- Nisu menjani endpoint-i, SQL, frontend projekcije niti runtime ponašanje.
- Validacija ovog deliverable-a je dokumentaciona/governance prirode; rezultat ne treba tumačiti kao numerički dokaz za konkretan tenant ili period.

## Queue odluka

`RQ406` i `RQ407` ostaju osnovni promptovi za parcijalni Supplier Footwear detail i zajednički deterministic proof za svih osam Operacije ruta. Dublji drugi prolaz je prvo katalogizovan u `RQ408`, a zatim je na osnovu ponovnog čitanja aktuelnog `main` stanja razložen u `RQ414`-`RQ426` u `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_OPERATIONS_ACCURACY_ADDENDUM.md`.

Svi novi promptovi su ostavljeni u statusu `WAITING`; current READY pointer ostaje `none` dok owner za svaki pojedinačni prompt ne izvrši zasebnu promociju, dependency proveru i collision proveru. `OP2` nalazi koji su već pokriveni `RQ371`-`RQ413` nisu ponovo otvoreni.
