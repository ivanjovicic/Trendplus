# Operacije — audit nalaza i promptovi (2026-09-18)

Queue: `direct-user-request`  
Obuhvat: 8 stavki menija **Operacije** iz `Klijent/clientapp/src/layout/navConfig.ts`  
Metod: statički pregled koda, fokusirani frontend testovi (188/188 green), `check:encoding`, `check:analytics-guardrails`

## Mapa ekrana

| Meni (Operacije) | Ruta | Komponenta | Napomena |
|---|---|---|---|
| Zalihe i dopuna | `/analytics/inventory` | `InventoryPage.tsx` | Samostalan ekran |
| Prodaja po dobavljačima | `/analytics/supplier-sales-stats` | `SupplierSalesStatsRedirect` | Redirect → `/analytics/supplier?tab=overview` |
| Prodaja po tipu obuće | `/analytics/shoe-type-sales-stats` | `ShoeTypeSalesStatsPage.tsx` | Samostalan ekran |
| Prodaja po smeni i dobavljačima | `/analytics/daily-sales` | `DailySalesStatsPage.tsx` | Samostalan ekran |
| Pre/Posle nivelacije | `/analytics/nivelacije-pre-post` | `ProdajaPrePostNivelacijePage.tsx` | Samostalan ekran |
| Prodaja po boji artikla | `/analytics/color-sales-stats` | `ColorSalesStatsPage.tsx` | Samostalan ekran |
| Prioriteti nivelacije | `/analytics/pre-nivelacija-prioriteti` | `PreNivelacijaPriorityPage.tsx` | Samostalan ekran |
| Dobavljači i tipovi obuće | `/analytics/dobavljaci-tipovi-obuce` | `SupplierFootwearAnalyticsRedirect` | Redirect → `/analytics/supplier?tab=assortment` |

## Već zatvoreno (RQ265–RQ300)

Prethodni Operacije audit (2026-09-15) je formalno zatvoren kroz `RQ265`–`RQ300` u `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`. Pokriveno je m.in.:

- prazna stanja sa kontekstom, freshness provenance, heading hijerarhija
- legacy supplier redirecti i sidebar aktivna stavka
- scope reload na Inventory, Daily Sales, Pre-Nivelacija
- numerička robusnost, URL state, recommendation gating, pre/post identitet

**Fokus ovog dokumenta:** preostali UX/IA/tekstualni/routing gapovi koji nisu eksplicitno zatvoreni ili su regresivni rizik.

---

## P1 — Visok uticaj na poverenje / orijentaciju

### OPS-301 — Inventory: srpski product copy umesto engleskog cockpit jezika

**Problem**  
Ekran iz menija „Zalihe i dopuna“ (`InventoryPage.tsx`) meša engleske naslove i KPI labele sa srpskim telom stranice. Korisnik vidi „Inventory analytics“, „Decision cockpit“, „Stock cover risk“, „Low cover SKU“, „workflow“, „sell-through“, „snapshot“ — dok meni i ostatak aplikacije koriste srpski.

**Evidence**

- `InventoryPage.tsx:1257-1262` — `title="Inventory analytics"`, `dataSource="Inventory analytics snapshot"`
- `InventoryPage.tsx:1297-1314` — KPI kartice na engleskom
- `InventoryPage.tsx:1321` — duplirani hero naslov „Decision cockpit…“
- `ActionWorkflowPanel.tsx:46`, `SKUDetailModal.tsx:69-113` — engleski loading/copy u istom flow-u

**Scope**

- `InventoryPage.tsx` + inventory child komponente koje renderuju user-facing copy u Operacije flow-u
- Bez promene backend contracta ili KPI formule

**Read first**

- `docs/ai/ENCODING_AND_TEXT_SAFETY.md`
- `InventoryPage.tsx`, `components/inventory/*`
- postojeći inventory spec fajlovi

**Do**

1. Uskladiti `AnalyticsTrustHeader` title/description sa menijem („Zalihe i dopuna“ / operativni inventory cockpit na srpskom).
2. Prevesti KPI kartice i sekcijske opise na srpski, zadržati postojeće `KpiExplainButton` metricKey-e.
3. U child panelima zameniti engleske loading poruke (`Ucitavam workflow…`, `Size curve`, `aging`) srpskim ekvivalentima sa dijakriticima.
4. Ne uvoditi nove abstrakcije — samo copy i test assertion update.

**Tests**

- Ažurirati/a dodati fokusirani spec koji assert-uje srpske naslove na inventory stranici.
- `npm run test -- --run src/pages/__tests__/InventoryPage*.spec.tsx`

**Acceptance**

- Nema engleskih naslova/KPI labela u primarnom inventory decision surface-u.
- UTF-8 dijakritici su ispravni (`č ć š đ ž`).
- Postojeći inventory guardrail testovi ostaju green.

**Predloženi queue ID:** `RQ301` (feature family: `operations-inventory-serbian-copy`)

---

### OPS-302 — Operacije sales ekrani: nedostaju u core route smoke matrici

**Problem**  
6 od 8 Operacije ekrana nisu u `CORE_ANALYTICS_ROUTE_DEFINITIONS` / `AppAnalyticsRoutes.spec.tsx`. Regresija lazy route-a, redirect-a ili App shell greške ne bi bila uhvatljena smoke testom.

**Evidence**

- `routes/analyticsRouteDefinitions.ts` — samo `/analytics/inventory` iz Operacije grupe
- Nedostaju: `shoe-type-sales-stats`, `daily-sales`, `nivelacije-pre-post`, `color-sales-stats`, `pre-nivelacija-prioriteti`, legacy supplier redirect rute
- `App.tsx:105-120` registruje rute koje smoke ne pokriva

**Scope**

- `analyticsRouteDefinitions.ts`, `AppAnalyticsRoutes.spec.tsx`, minimalni App mock stubovi
- Ne dirati produktnu logiku stranica

**Do**

1. Dodati Operacije rute u smoke definicije (bez durable report query stringa osim gde je neophodno).
2. Za redirect rute assert-ovati finalni mapped route (`SupplierConsolidatedPage` stub).
3. Dokumentovati u `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md` ako postoji gap u standardu.

**Tests**

- `npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx`

**Acceptance**

- Svih 8 Operacije stavki ima smoke pokrivanje u App shell testu.
- Redirect rute ne bacaju i ne ostaju na praznom ekranu.

**Predloženi queue ID:** `RQ302` (feature family: `operations-route-smoke`)

---

### OPS-303 — Daily Sales: engleski mismatch badge i mešovit QA copy

**Problem**  
Na `/analytics/daily-sales` korisnik vidi engleski badge „Check“ i mešovit srpsko-engleski QA tekst (`mismatch`, `total kolone`, `top+others`), što narušava operativno poverenje u data quality panel.

**Evidence**

- `DailySalesStatsPage.tsx:1784` — `<span className="mismatch-badge">Check</span>`
- `DailySalesStatsPage.tsx:1228-1236, 1794-1795` — mešoviti terminologija
- `dailySalesStatsApi.ts:80` — fallback greška bez dijakritika: „Greska pri ucitavanju…“

**Do**

1. Zameniti „Check“ srpskim operativnim signalom (npr. „Proveri“ / „Neusklađeno“) sa `role="status"` ili tooltip objašnjenjem.
2. Uskladiti footnote i quality insight copy na srpski (`neusklađenost`, `ukupna kolona`, `top + ostali`).
3. Ispraviti fallback error copy u API servisu.

**Tests**

- Proširiti `DailySalesStatsPage.premium.spec.tsx` za badge copy i mismatch footnote.
- `npm run test -- --run src/pages/__tests__/DailySalesStatsPage*.spec.tsx`

**Acceptance**

- Nema engleskog UI teksta u mismatch indikatorima na srpskom ekranu.
- Mismatch i dalje ostaje vidljiv (ne skrivati data quality signal).

**Predloženi queue ID:** `RQ303` (feature family: `daily-sales-localization`)

---

### OPS-304 — Color Sales: detail panel koristi „Decision score“ umesto „Skor odluke“

**Problem**  
Tabela i export koriste srpski „Skor odluke“, ali detail panel prikazuje engleski label „Decision score“ — kontradikcija u istom decision surface-u.

**Evidence**

- `ColorSalesStatsPage.tsx:124` — kolona `Skor odluke`
- `ColorSalesStatsPage.tsx:1125-1126` — detail `<span>Decision score</span>`
- Guardrail scanner već flag-uje `decisionScore_assign` u istom fajlu

**Do**

1. Uskladiti detail label sa kolonom/tab export metadata (`Skor odluke`).
2. Razmotriti da li detail treba prikazati `confidencePct` ili backend `decisionScore` eksplicitno — bez promene semantike sortinga.
3. Ažurirati premium spec koji assert-uje detail panel.

**Tests**

- `ColorSalesStatsPage.premium.spec.tsx`, `ColorSalesStatsPage.spec.tsx`

**Acceptance**

- Detail i tabela koriste isti srpski label za score.
- Guardrail violation za `decisionScore_assign` ostaje adresiran ili dokumentovan ako je namerno mapiranje.

**Predloženi queue ID:** `RQ304` (feature family: `color-sales-detail-label-parity`)

---

## P2 — Srednji uticaj (UX, IA, konzistentnost)

### OPS-305 — Operacije meni: dve stavke vode u isti Supplier shell bez jasne IA oznake

**Problem**  
„Prodaja po dobavljačima“ i „Dobavljači i tipovi obuće“ redirectuju na `/analytics/supplier` (tab overview / assortment). Sidebar aktivira „Pregled dobavljača“ u grupi **Odluke**, ne stavku iz **Operacije**. RQ268 je ovo prihvatio kao kompatibilnost, ali korisnik i dalje vidi 8 različitih stavki u Operacije meniju dok su 2 zapravo alias tabova.

**Evidence**

- `SupplierRedirects.tsx`
- `Sidebar.spec.tsx` — očekuje canonical active link u Odluke
- `SupplierConsolidatedPage.tsx:69-71` — legacy banner postoji, ali meni label ostaje „samostalan izveštaj“

**Do (product/engineering choice — oba su validna, izabrati jedno)**

**Opcija A (preporučeno za pilot):** U `navConfig.ts` označiti legacy stavke badge-om „Tab“ / „→ Pregled dobavljača“ i tooltip-om; skratiti label duplikate.

**Opcija B:** Vratiti standalone rute (`SupplierSalesStatsPage`, `SupplierFootwearAnalyticsPage`) sa jasnim „embedded vs standalone“ ownership-om.

**Tests**

- Sidebar + redirect matrix; assert tačan badge/tooltip.

**Acceptance**

- Korisnik razume da Operacije stavka otvara tab unutar canonical supplier ekrana **pre** klika.
- Nema dva aktivna nav itema.

**Predloženi queue ID:** `RQ305` (feature family: `operations-supplier-ia-clarity`)

---

### OPS-306 — Operacije: masovno nedostaju dijakritici u korisničkom copy-ju (nije mojibake)

**Problem**  
`check:encoding` prolazi (nema mojibake), ali desetine stringova u Operacije površinama koriste ASCII varijante (`Greska`, `Ucitavam`, `poredjenje`, `Pokrice`, `obuce`, `kolicina`, `Predlozen`, `Podrska`, `Jos nema`, `stampu`). To nije encoding corruption, ali je produktni kvalitet teksta i konzistentnost sa ostatkom aplikacije.

**Evidence (reprezentativno)**

| Fajl | Primer |
|---|---|
| `DailySalesStatsPage.tsx` | `Greska pri ucitavanju dnevne prodaje` |
| `ColorSalesStatsPage.tsx` | `Pre nivo kolicina`, `Pokrice marze` |
| `ShoeTypeSalesStatsPage.tsx` | `Pokrice direktnom nabavnom %`, `trosak`, `marze` |
| `StoreComparisonPanel.tsx` | `Ucitavam poredjenje lokacija` |
| `SupplierFootwearAnalyticsPage.tsx` | `grafikon tipova obuce`, `Podrska odluci` |
| `InventoryPage.tsx` | `poredjenje prodavnica`, `stampu` |
| API servisi | `Greska pri ucitavanju statistike...` |

**Do**

1. Napraviti ciljani grep listu samo za Operacije owner fajlove (pages + inventory components + pripadajući `*Api.ts`).
2. Ispraviti user-facing stringove na srpski sa dijakriticima; API fallback poruke uskladiti.
3. Ažurirati testove koji assert-uju stari ASCII tekst.
4. Pokrenuti `npm run check:encoding`.

**Tests**

- Fokusirani page specovi po ekranu; opciono mali encoding allowlist test za Operacije folder.

**Acceptance**

- Operacije ekrani koriste ispravne dijakritike u svim primary/error/loading porukama.
- Nema promene business logike.

**Predloženi queue ID:** `RQ306` (feature family: `operations-diacritics-pass`)

---

### OPS-307 — Shoe Type: engleski signal label „Low signal“ u nivelacija impact metric

**Problem**  
`describeNivelacijaImpactMetric` vraća engleski `label: "Low signal"` u tabeli pre/post signala — jedini engleski status label u srpskom decision redu.

**Evidence**

- `ShoeTypeSalesStatsPage.tsx:300` — `label: "Low signal"`
- Ostali statusi su srpski (`Bez baze`, `0% pokriće`, `N/A`)

**Do**

1. Mapirati na postojeći canonical copy (npr. „Slab signal“ / „Nedovoljan signal“) kroz `canonicalRecommendationSemantics` ako već postoji ekvivalent.
2. Dodati regression test u `ShoeTypeSalesStatsPage.premium.spec.tsx`.

**Acceptance**

- Nema engleskog status labela u shoe-type tabeli za nivelacija impact kolonu.

**Predloženi queue ID:** `RQ307` (feature family: `shoe-type-impact-label`)

---

### OPS-308 — Inventory trust header: period uvek null iako ostali Operacije ekrani nose period

**Problem**  
Inventory je jedini Operacije ekran bez eksplicitnog period filtera u control bar-u, ali `AnalyticsTrustHeader` dobija `periodFrom={null}` / `periodTo={null}`. Korisnik ne vidi requested/effective period provenance dok KPI/lista imaju filtere po prodavnici/dobavljaču — kontrast sa Daily Sales / sales stats ekranima.

**Evidence**

- `InventoryPage.tsx:1259-1260`
- Inventory koristi snapshot semantics (`dataSource="Inventory analytics snapshot"`) bez period lineage u header-u

**Do**

1. Definisati authoritative period za inventory (npr. signal window iz `createInventorySignalWindow`, export contract, ili backend meta).
2. Proslediti period u `AnalyticsTrustHeader` i export metadata ako contract već nosi datume.
3. Ako period nije definisan na backend-u, eksplicitno reći „Snapshot bez period filtera“ umesto praznog header-a.

**Tests**

- `InventoryPage.freshnessLineage.spec.tsx` proširenje

**Acceptance**

- Trust header objašnjava period ili eksplicitno „bez perioda“ — nikad prazan/null bez objašnjenja.

**Predloženi queue ID:** `RQ308` (feature family: `inventory-period-provenance`)

---

### OPS-309 — Operacije meni: duplirana ShoppingBag ikona

**Problem**  
„Prodaja po tipu obuće“ i „Prodaja po smeni i dobavljačima“ koriste istu `ShoppingBag` ikonu u `navConfig.ts`, što otežava brzo prepoznavanje u sidebar-u.

**Evidence**

- `navConfig.ts:159-160`

**Do**

1. Dodeliti različite lucide ikone (npr. `Clock`/`CalendarDays` za smene, zadržati `ShoppingBag` za tip obuće).
2. Ažurirati `navConfig.spec.ts` ako postoji snapshot/icon test.

**Acceptance**

- Sve Operacije stavke imaju vizuelno razlikovane ikone.

**Predloženi queue ID:** `RQ309` (feature family: `operations-nav-icons`)

---

## P3 — Niži prioritet / tech debt

### OPS-310 — Test harness i dalje mount-uje `/analitika/...` umesto produkcijskih `/analytics/...` ruta

**Problem**  
Produkcija koristi `/analytics/*` za stranice i `/analitika/:table/:id` za detail modal. Mnogi Operacije spec fajlovi i dalje renderuju stranice na `/analitika/shoe-type-sales-stats` itd. To nije korisnički bug, ali maskira routing regresije.

**Evidence**

- `ShoeTypeSalesStatsPage.spec.tsx:132`, `ColorSalesStatsPage.spec.tsx:221`, `PreNivelacijaPriorityPage.spec.tsx` (brojni slučajevi)
- Produkcijski `App.tsx` nema list route na `/analitika/shoe-type-sales-stats`

**Do**

1. Uvesti shared test helper sa canonical `/analytics/...` initial entries za page mount.
2. Zadržati `/analitika/:table/:id` samo za detail/modal testove gde je to produkcijski contract.

**Acceptance**

- Page-level testovi odgovaraju produkcijskim rutama; detail testovi i dalje pokrivaju modal put.

**Predloženi queue ID:** `RQ310` (feature family: `operations-test-route-alignment`)

---

### OPS-311 — Analytics guardrails: 13 preostalih decisionScore/confidence/reliability violations u Operacije-related fajlovima

**Problem**  
`npm run check:analytics-guardrails` prijavljuje violations u `ColorSalesStatsPage`, `PreNivelacijaPriorityPage`, `ProdajaPrePostNivelacijePage` (Operacije owner). Skripta trenutno exit 0, ali violations su dokumentovani rizik za pogrešno mapiranje score polja.

**Evidence**

- Guardrail output 2026-09-18 (13 violations, 4 u Operacije stranicama)

**Do**

1. Za svaki violation klasifikovati: namerno mapiranje vs bug.
2. Gde je bug — uskladiti sa backend contractom; gde je namerno — dodati allowlist comment + test.

**Acceptance**

- Operacije stranice nemaju unexplained guardrail violations.

**Predloženi queue ID:** `RQ311` (feature family: `operations-guardrail-cleanup`)

---

## Brzi redosled implementacije (preporuka)

1. **RQ302** — route smoke (jeftin, sprečava regresije)
2. **RQ303 + RQ304 + RQ307** — mali copy fixevi sa visokom vidljivošću
3. **RQ301 + RQ306** — širi localization pass (inventory + dijakritici)
4. **RQ305** — IA odluka za supplier alias stavke (zahteva product smjer)
5. **RQ308** — inventory period provenance (contract pitanje)
6. **RQ309–RQ311** — polish / tech debt

## Validacija iz ovog audita

| Check | Rezultat |
|---|---|
| Operacije focused tests (15 fajlova) | **188/188 passed** |
| `npm run check:encoding` | **pass** |
| `npm run check:analytics-guardrails` | **13 violations reported** (exit 0) |
| Live browser / backend | **not run** — VM nema produkcijski backend |

## Kako koristiti promptove

Svaki `OPS-3xx` blok je spreman za unos u `ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` kao `RQ301+` ( sledeći slobodan ID ). Pre implementacije:

1. klasifikovati prema `AGENTS.md` (direct vs queue),
2. dodeliti jedan owner subsystem po promptu,
3. definisati fokusirani proof pre šireg build-a.
