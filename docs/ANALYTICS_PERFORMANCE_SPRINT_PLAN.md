# Trendplus Analytics - Performance Sprint Plan

> Verzija: 1.0 - Maj 2026  
> Tip dokumenta: prioritizovan performance sprint artifact  
> Osnova: code-path audit backend/frontend read puteva za supplier analytics, inventory, daily sales, pre/post nivelacije i pre-nivelacija prioritete

---

## 1. Executive Summary

Ovo je staticki code-path audit, ne analiza na osnovu produkcijskih p95/p99 logova. U workspace-u nema sacuvanih runtime timing tragova za ove rute, pa je zakljucak baziran na stvarnoj strukturi endpointa, cache sloja i frontend orchestration-a.

Glavni problem trenutno nije odsustvo cache-a svuda, nego tri precizna obrasca:

1. Isti skupi skup podataka se racuna vise puta po jednom load-u ekrana.
2. Neki teski analytics read-ovi jos uvek koriste samo lokalni `IMemoryCache` umesto deljenog analytics cache sloja.
3. Ponegde se ceo dataset gradi pre paginacije ili pre odvajanja live/mutable dela odgovora.

Najveci ROI sledeceg sprinta nije u sirokom SQL rewrite-u, nego u:

1. shared base-rowset cache-u za Supplier Decision Hub
2. shared cache migraciji i TTL korekciji za Daily Sales
3. razdvajanju base-cache / projection-cache geometrije za Pre-Nivelacija Prioritete
4. izdvajaju zajednickog inventory dataset-a iz vise sibling endpointa

---

## 2. Scope i cilj sprinta

Ovaj sprint nije SQL modernization sprint. Cilj je da ukloni najvece cold-path skokove i smanji dupliranje rada bez scope creep-a.

Ulazi u scope:

1. Supplier Decision Hub
2. Daily Sales
3. Pre-Nivelacija Prioriteti
4. Inventory read model reuse
5. SupplierSalesStats shared cache migracija

Van scope-a za ovaj sprint:

1. sirok rewrite `SupplierSalesStats` SQL upita
2. kompletan read-model redesign za `VendorSalesNivelacija`
3. cache-ovanje mutabilnog decision overlay-a za inventory
4. frontend redesign ili supplier tab host optimizacija

---

## 3. Hotspot pregled

| Povrsina | Cold path problem | Postoji cache | Da li je dovoljan | Sledeci pravi korak |
|---|---|---|---|---|
| Supplier Decision Hub | `summary`, `ranking` i `previous summary` ponovo vuku isti rowset | Da, response-level `IAnalyticsCacheService` | Ne | Uvesti shared base-rowset cache po filterima |
| Daily Sales | 5+ materialization koraka + current/previous paralelno iz UI | Da, lokalni `IMemoryCache` 2 min | Ne | Prebaciti na `IAnalyticsCacheService`, TTL 10-20 min, prewarm za default range |
| Inventory | Veliki fan-out po ekranu + ponovni `BuildInventoryDatasetAsync` | Parcijalno | Ne | Izdvojiti shared read-only inventory base dataset |
| SupplierSalesStats | Tezak endpoint, ali ima prewarm i 20-min lokalni cache | Da, `IMemoryCache` + prewarm | Delimicno | Migrirati na shared analytics cache |
| VendorSalesNivelacija | Ranked query + dodatni rolling/momentum/OOS/DiD enrich koraci | Da, `IAnalyticsCacheService` | Delimicno | Kasnije summary/detail split ili SQL-side enrichment |
| Pre-Nivelacija Prioriteti | Full compute pre paginacije, page u cache key-u | Da, `IAnalyticsCacheService` | Ne | Base-cache bez page/pageSize, projection preko vec kesiranog seta |

---

## 4. Prioriteti sprinta

### P0 - Supplier Decision Hub shared base-rowset cache

**Problem**

Jedan UI load danas pokrece vise backend ruta koje svaka ponovo poziva isti `QuerySupplierRowsAsync` read path.

**Target files**

1. `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
2. `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`

**Promena**

1. Uvesti base-cache key za Supplier Decision Hub rowset po filter setu.
2. `summary`, `quadrant`, `ranking` i `details` neka prvo traze shared rowset cache.
3. Tek nakon toga raditi response projection za konkretan endpoint.
4. Zadrzati postojeci response contract.

**Predlog cache geometrije**

Base key treba da ukljucuje samo poslovne filtere koji menjaju row universe:

1. `fromDate`
2. `toDate`
3. `category`
4. `gender`
5. `seasonId`
6. `minRevenue`
7. `onlyHighConfidence`
8. `excludeOosBeforeMarkdown`
9. `supplierId`

`page`, `pageSize`, `sortBy` i `sortDir` ne smeju da budu u base rowset key-u.

**TTL**

1. Base rowset: `CacheExpiration.HeavyAnalytics`
2. Endpoint projection: opciono bez dodatnog cache-a, ili kratak `Medium` ako profiling pokaze korist

**Ocekivani dobitak**

1. 40-70% manje DB rada na prvom load-u
2. 2-3x manji cold-load ovog ekrana

**Rizik**

Srednji. Mora pazljivo da se testira da projection sloj ne promeni ponasanje `ranking` sortiranja i `details` fallback logike.

**Tezina**

Srednja.

---

### P0 - Daily Sales shared cache migracija + TTL review + prewarm

**Problem**

`DailySalesStatsService` radi vise odvojenih materialization koraka, a endpoint koristi samo lokalni `IMemoryCache` sa TTL-om od 2 minuta.

**Target files**

1. `Api/Endpoints/DailySalesStatsEndpoints.cs`
2. `Api/Services/DailySalesStatsService.cs`
3. `Api/Services/Startup/AnalyticsCachePrewarmHostedService.cs`
4. `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`

**Promena**

1. Zameniti `IMemoryCache` sa `IAnalyticsCacheService` u `DailySalesStatsEndpoints`.
2. TTL podici na 10-20 minuta za analytics read path.
3. Dodati prewarm za default current 30d i previous 30d range.
4. Ne dirati servisnu logiku u prvom koraku osim ako je potrebno za cache integration.

**Predlog TTL-a**

1. `DailySalesStats`: 15 minuta
2. Prewarm samo za najcesci preset i `dataScope=all`

**Ocekivani dobitak**

1. 60-90% bolji warm repeat path
2. znacajno manje cold-start udara posle deploy-a ili na drugoj instanci

**Rizik**

Nizak za cache migraciju. Srednji tek ako se kasnije bude radio combined current/previous response.

**Tezina**

Niska.

---

### P1 - Pre-Nivelacija Prioriteti split base-cache / projection-cache

**Problem**

Endpoint gradi kompletan `allCandidates` univerzum pre paginacije, ali `page` i `pageSize` ulaze u cache key. To znaci da promena strane ponavlja skup compute.

**Target files**

1. `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs`
2. `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`

**Promena**

1. Uvesti base key bez `page` i `pageSize`.
2. Kesirati kandidat-set i agregate po poslovnim filterima.
3. Paginaciju i eventualni sort raditi nad vec kesiranim setom.
4. Ako response ostane veliki, odvojiti summary/leaderboard/queues od candidate page slice-a.

**Predlog cache geometrije**

Base key:

1. `supplierId`
2. `seasonId`
3. `footwearTypeId`
4. `stockMin`
5. `stockMax`
6. `noSaleDaysMin`
7. `minScore`
8. `marginFloor`

Projection key:

1. base key + `page` + `pageSize`
2. projection TTL moze biti `Medium` ili uopste ne mora postojati ako paging ide in-memory

**Ocekivani dobitak**

1. 40-70% manje recompute-a pri paginaciji i filter reuse-u

**Rizik**

Nizak do srednji.

**Tezina**

Niska do srednja.

---

### P1 - Inventory base dataset cache sa live decision overlay-om

**Problem**

Inventory page ima veliki fan-out, a vise endpointa opet i opet zove `BuildInventoryDatasetAsync`. Najskuplji reuse problem je sto `insights`, `store-comparison` i `action-suggestions` racunaju slican read-only dataset odvojeno.

**Target files**

1. `Api/Endpoints/InventoryEndpoints.cs`
2. `Api/Endpoints/CachedAnalyticsEndpoints.cs`
3. `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`
4. po potrebi novi helper/service u `Api/Services` ili `Application/Analytics`

**Promena**

1. Izdvojiti shared read-only inventory dataset builder.
2. Kesirati dataset po filterima koji menjaju skup artikala.
3. `insights` i `store-comparison` da koriste isti base dataset.
4. `action-suggestions` da koristi isti read-only base dataset, ali da decision overlay ostane live.

**Veoma vazno**

Ne kesirati ceo `action-suggestions` response kao gotov final payload bez jasne invalidacije. Read-only osnova moze u cache; mutable odluka ne.

**Ocekivani dobitak**

1. 30-60% manje DB/CPU troska na ulazu u Inventory
2. stabilniji perceived performance zbog manjeg duplicated work-a

**Rizik**

Srednji, jer se mora jasno razdvojiti read-only osnova od mutabilnih decision informacija.

**Tezina**

Srednja.

---

### P2 - SupplierSalesStats shared analytics cache migracija

**Problem**

Endpoint je vec relativno dobar na toplom putu zbog 20-min TTL-a i startup prewarm-a, ali je cache i dalje samo lokalni `IMemoryCache`.

**Target files**

1. `Api/Endpoints/AllEndpoints.cs`
2. `Api/Services/Startup/AnalyticsCachePrewarmHostedService.cs`

**Promena**

1. Migrirati response cache sa `IMemoryCache` na `IAnalyticsCacheService`.
2. Zadrzati postojecu prewarm strategiju za `30d/all` default put.
3. Ne dirati SQL shape u ovom sprintu.

**Ocekivani dobitak**

1. 50-80% manje cold recompute-a izmedju instanci i posle deploy-a

**Rizik**

Nizak.

**Tezina**

Niska.

---

## 5. Cache RFC - pravila koja sprint mora da postuje

### 5.1 Vrste cache entry-ja

Sprint uvodi tri tipa cache entry-ja:

1. **Response cache** - finalni payload za read-only, stabilne analytics rute
2. **Base dataset cache** - skupi zajednicki rowset/dataset iz kog vise endpointa pravi projekcije
3. **Projection cache** - opcioni lagani cache za pagination/sort nad vec kesiranim base setom

### 5.2 Cache key pravila

1. Base key sadrzi samo filtere koji menjaju skup podataka.
2. `page`, `pageSize`, `sortBy`, `sortDir` ne idu u base dataset key.
3. `storeId`, `supplierId`, `seasonId`, `fromDate`, `toDate`, `dataScope` i slicni business filteri moraju biti eksplicitno serijalizovani.
4. Null vrednosti moraju imati stabilan string format (`all`, `none`, `null`) umesto implicitnog praznog stringa.
5. Decimal filtere formatirati kultur-nezavisno.

### 5.3 TTL pravila

Predlog TTL-a za ovaj sprint:

1. Supplier Decision Hub base rowset: 20 min
2. Daily Sales response: 15 min
3. Pre-Nivelacija base dataset: 20 min
4. Inventory base dataset: 5 min
5. SupplierSalesStats response: 20 min

Razlog: cilj je smanjiti hladan recompute na read-only analytics putanjama, ali ne produzavati TTL nasumicno na mutabilnim odlukama.

### 5.4 Invalidation pravila

1. Ne oslanjati se na prefix invalidation kao jedini mehanizam za mutable view-e.
2. Za read-only analytics summary dataset-e prihvatljiv je TTL-based invalidation.
3. Za inventory decision overlay i slicne mutable delove ostaviti live read ili exact-key invalidation kad postoji write path.
4. Ako write path ne zna tacne kljuceve koje treba invalidirati, ne kesirati finalni mutable payload.

### 5.5 Prewarm pravila

Prewarm ne treba prosirivati nekontrolisano.

Dozvoljeno u ovom sprintu:

1. `supplier-sales-stats` default 30d / `dataScope=all`
2. `daily-sales` current 30d / previous 30d / `dataScope=all`

Nije za ovaj sprint:

1. prewarm svih supplier filter kombinacija
2. prewarm svih inventory supplier/store permutacija
3. prewarm mutable decision response-a

---

## 6. Implementacioni redosled

Predlog redosleda implementacije unutar jednog sprinta:

1. Supplier Decision Hub shared rowset cache
2. Daily Sales cache migracija + TTL + prewarm
3. Pre-Nivelacija Prioriteti base/projection split
4. Inventory base dataset cache
5. SupplierSalesStats shared cache migracija

Razlog redosleda:

1. Prvo se uklanja najveci duplicated cold-load.
2. Zatim se resava ruta sa najociglednijim shared cache deficitom.
3. Onda se radi low-risk cache geometrija fix.
4. Tek nakon toga ide slozeniji inventory reuse rad.
5. SupplierSalesStats ostaje poslednji jer je vec operationalno najstabilniji medju skupim supplier read-ovima.

---

## 7. Acceptance Criteria po stavci

### Supplier Decision Hub

1. Jedan backend load filter seta ne sme da radi isti skup row-query-ja vise puta za `summary` i `ranking`.
2. Response contract ostaje isti.
3. Default i explicitni date-range filteri daju iste rezultate kao pre promene.

### Daily Sales

1. Ruta koristi `IAnalyticsCacheService`, ne lokalni `IMemoryCache`.
2. Default 30d current/previous scenariji su prewarm-ovani.
3. Nema regresije u warning i data-quality logici.

### Pre-Nivelacija Prioriteti

1. Promena stranice ne sme da radi puni recompute kandidat seta.
2. Summary, leaderboard i queue rezultati ostaju semanticki isti.

### Inventory

1. `insights`, `store-comparison` i `action-suggestions` koriste isti base dataset sloj.
2. Mutable decision overlay ostaje sveza i nije slepo kesiran.

### SupplierSalesStats

1. Response ostaje isti.
2. Warm odgovor mora raditi preko deljenog analytics cache sloja.
3. Postojeci prewarm scenario ostaje validan.

---

## 8. Sta ne dirati jos

1. Ne raditi sirok SQL rewrite za `SupplierSalesStats` u ovom sprintu.
2. Ne kesirati finalni `inventory/action-suggestions` payload bez precizne invalidacije.
3. Ne optimizovati supplier tab host; on vec renderuje samo aktivan tab.
4. Ne produzavati TTL svuda kao univerzalni lek.
5. Ne uvoditi prewarm za veliki broj parametarskih kombinacija.

---

## 9. Minimalni validation paket posle implementacije

Za svaku zavrsenu stavku uraditi:

1. narrow integration proveru za pogodjeni endpoint
2. log proveru da li su repeated cold query-jevi smanjeni
3. proveru da response shape nije promenjen
4. proveru da cache hit/miss logovi imaju smisla

Ako sprint dobije dodatni kapacitet, prva sledeca srednjorocna investicija treba da bude eksplicitni precomputed/windowed read model za Supplier Decision Hub date-range scenarije.