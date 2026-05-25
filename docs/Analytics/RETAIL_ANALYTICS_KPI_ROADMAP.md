# Retail Analytics KPI Roadmap (Trendplus)

Status: planning-only dokument, bez runtime promena.
Scope: definicija KPI-ja i decision flow logike za naredne faze razvoja.

## Cilj

Trendplus treba da uvede KPI-je koji su poslovno korisni i dokazivo zasnovani na podacima koje imamo (ili jasno označe koje podatke tek treba uvesti), bez "nasumičnih" metrika.

## Trenutni signalni sloj i dostupni izvori (sažeto)

Prema postojećem analytics modelu i intelligence sloju, već imamo solidnu osnovu kroz:
- SalesFacts, SalesLineFacts
- ProductsDim, SuppliersDim
- InventoryMovementFacts
- postojeće analytics view/materialized view objekte

## Legenda dostupnosti podataka

- Postoji: ključni podaci su već u Trendplus DB i mogu u MVP bez novih eksternih izvora.
- Delimično: deo podataka postoji, ali treba bolja granularnost/modeliranje.
- Nedostaje: bez novih tabela/eventa ne treba uvoditi KPI kao recommendation.

## KPI katalog (15)

### 1) Sell-through ratio

- Poslovno pitanje: Koliko brzo zaliha prelazi u prodaju u izabranom periodu?
- Formula: Sell-through = prodate jedinice / (početna zaliha + ulaz u periodu).
- Potrebni podaci: prodate jedinice po artiklu i periodu, početna zaliha, ulazne količine.
- Da li podaci već postoje u Trendplus DB: Delimično (prodaja i pokreti postoje; početna zaliha po tačnom preseku perioda nije svuda eksplicitna).
- Data quality rizici: nekompletni/pogrešno mapirani pokreti ulaza, neusklađenost šifri artikala, kašnjenje refresh-a.
- Recommendation ili signal: Signal (P0), recommendation (P1 kada snapshot zaliha bude stabilan).
- Gde se prikazuje: Inventory i Product Decision sekcije; sažetak na dashboard-u.
- Prioritet: P0.
- Minimalni prvi MVP: izračun na nivou artikla/kategorije za 30/90d, uz badge pouzdanosti i "insufficient_data" fallback.

### 2) Inventory turnover

- Poslovno pitanje: Koliko puta se prosečna zaliha "okrene" u periodu?
- Formula: Turnover = COGS / prosečna zaliha (ili units sold / average units on hand za units varijantu).
- Potrebni podaci: COGS ili nabavna vrednost prodaje, prosečna zaliha po periodu.
- Da li podaci već postoje u Trendplus DB: Delimično (cost postoji po proizvodu, ali average inventory istorijski nije svuda robustan).
- Data quality rizici: nedostajući nabavni troškovi, outlier cene, rekonstrukcija zaliha iz pokreta.
- Recommendation ili signal: Signal.
- Gde se prikazuje: Inventory risk ekran i category drilldown.
- Prioritet: P0.
- Minimalni prvi MVP: units-based turnover po kategoriji i dobavljaču, bez finansijske verzije dok cost coverage ne pređe prag.

### 3) GMROI

- Poslovno pitanje: Koliko bruto marže generišemo na svaki dinar investiran u zalihu?
- Formula: GMROI = bruto marža / prosečna vrednost zalihe po nabavnoj ceni.
- Potrebni podaci: prihod, COGS, prosečna vrednost zalihe kroz vreme.
- Da li podaci već postoje u Trendplus DB: Delimično.
- Data quality rizici: netačna nabavna cena, nedostajući periodični inventory valuation snapshot.
- Recommendation ili signal: Signal (dok valuation ne bude stabilan), recommendation tek kasnije.
- Gde se prikazuje: Supplier i Category performance paneli.
- Prioritet: P1.
- Minimalni prvi MVP: GMROI-lite na osnovu aproksimacije prosečne zalihe i jasno označen kao pomoćni signal.

### 4) Margin loss zbog nivelacija

- Poslovno pitanje: Koliko marže gubimo zbog korekcija cena (nivelacija/markdown)?
- Formula: Margin loss = (planirana bruto marža bez nivelacije) - (realizovana bruto marža posle nivelacije).
- Potrebni podaci: istorija promene cena, prodaja pre/posle, trošak artikla.
- Da li podaci već postoje u Trendplus DB: Delimično (deo price/cost signala postoji; puna istorija svih promene cena može biti parcijalna).
- Data quality rizici: nepotpuna istorija nivelacija, pogrešna vremenska korelacija price-event i prodaje.
- Recommendation ili signal: Signal (P0), recommendation (P1 uz validirane reason codes).
- Gde se prikazuje: Pre/Post Nivelacija i Supplier Decision report.
- Prioritet: P0.
- Minimalni prvi MVP: agregatni margin loss po dobavljaču i kategoriji, sa confidence indikatorom.

### 5) Markdown efficiency

- Poslovno pitanje: Da li markdown daje dovoljno oslobađanje zaliha uz prihvatljiv gubitak marže?
- Formula: Markdown efficiency = (inkrementalno smanjenje zalihe) / (izgubljena marža zbog markdown-a).
- Potrebni podaci: price-change eventi, sell-through pre/posle, margin delta.
- Da li podaci već postoje u Trendplus DB: Delimično.
- Data quality rizici: attribution bias (spoljni faktori), nedovoljna gustina price-event istorije.
- Recommendation ili signal: Signal.
- Gde se prikazuje: Pricing/Nivelacija analitika, Supplier scorecard pomoćni panel.
- Prioritet: P1.
- Minimalni prvi MVP: jednostavan pre/post prozor (npr. 14 dana) bez kauzalnih tvrdnji.

### 6) Stock cover / days of supply

- Poslovno pitanje: Koliko dana trenutna zaliha pokriva očekivanu potražnju?
- Formula: Days of supply = current stock / prosečna dnevna prodaja (rolling window).
- Potrebni podaci: trenutna zaliha, rolling sales velocity.
- Da li podaci već postoje u Trendplus DB: Postoji (uz rekonstrukciju stock signala iz postojećih izvora).
- Data quality rizici: sezonalnost i promo skokovi, nerealne nule kod sporadične prodaje.
- Recommendation ili signal: Recommendation (dopuna/smanjenje) uz data quality gating.
- Gde se prikazuje: Inventory recommendations i dashboard risk kartice.
- Prioritet: P0.
- Minimalni prvi MVP: category + article cover sa pragovima (nizak/srednji/visok rizik).

### 7) OOS lost sales estimate

- Poslovno pitanje: Koliki prihod/verovatna marža je izgubljena zbog out-of-stock stanja?
- Formula: Lost sales = procenjena potražnja u OOS periodu - realizovana prodaja (najčešće 0), uz margin projection.
- Potrebni podaci: OOS intervali, baseline potražnja, eventualno footfall/session signal.
- Da li podaci već postoje u Trendplus DB: Delimično (OOS proxy postoji kroz inventory signal; baseline model i external demand nisu puni).
- Data quality rizici: visoka model uncertainty, substitucija artikala, sezonalnost.
- Recommendation ili signal: Signal.
- Gde se prikazuje: Inventory/OOS risk panel i weekly report.
- Prioritet: P1.
- Minimalni prvi MVP: rough estimate sa širokim intervalom pouzdanosti i obaveznim warning-om.

### 8) Size/color availability risk

- Poslovno pitanje: Gde gubimo prodaju jer nedostaju ključne veličine/boje?
- Formula: Availability risk score = ponder nedostupnih top varijanti x njihovo istorijsko učešće u prodaji.
- Potrebni podaci: varijantni inventory (size/color), prodaja po varijanti, matrica potražnje po veličini/boji.
- Da li podaci već postoje u Trendplus DB: Delimično (zavisi od kvaliteta varijantnih atributa i mapiranja).
- Data quality rizici: nepotpuna standardizacija veličina/boja, nekonzistentne šifre.
- Recommendation ili signal: Recommendation (kada coverage atributa pređe prag), inače signal.
- Gde se prikazuje: Inventory i Product Decision (variant level panel).
- Prioritet: P0.
- Minimalni prvi MVP: top 20 SKU-ova sa risk score + predlog dopune najkritičnijih varijanti.

### 9) Supplier dependency risk

- Poslovno pitanje: Koliko je biznis izložen zavisnosti od malog broja dobavljača?
- Formula: Dependency risk = koncentracija prihoda/marže po dobavljaču (npr. HHI ili share top-N).
- Potrebni podaci: prihod i marža po dobavljaču, trend kroz vreme.
- Da li podaci već postoje u Trendplus DB: Postoji.
- Data quality rizici: unknown supplier mapiranje, nekompletni dobavljač atributi.
- Recommendation ili signal: Recommendation na nivou portfolija (diversify/hold).
- Gde se prikazuje: Supplier pregled, supplier report, exec dashboard.
- Prioritet: P0.
- Minimalni prvi MVP: top supplier concentration + alert kada prag bude prekoračen.

### 10) Category contribution margin

- Poslovno pitanje: Koje kategorije realno nose profit nakon troška robe?
- Formula: Contribution margin = (prihod - COGS) / prihod, po kategoriji.
- Potrebni podaci: revenue, cost, kategorična dimenzija.
- Da li podaci već postoje u Trendplus DB: Postoji (uz caveat za cost coverage).
- Data quality rizici: missing/pogrešan cost, kategorije bez standardizovanog mapiranja.
- Recommendation ili signal: Recommendation (invest/reduce) kada quality prag zadovoljen.
- Gde se prikazuje: Category i dashboard contribution sekcije.
- Prioritet: P0.
- Minimalni prvi MVP: contribution rang lista po kategoriji sa data-quality badge-om.

### 11) Slow stock capital

- Poslovno pitanje: Koliko kapitala je vezano u sporo-obrtnoj robi?
- Formula: Slow stock capital = suma (on-hand qty x unit cost) za artikle sa turnover ispod praga.
- Potrebni podaci: inventory qty, unit cost, turnover klasifikacija.
- Da li podaci već postoje u Trendplus DB: Delimično.
- Data quality rizici: cost nedostaci i loša rekonstrukcija istorijske zalihe.
- Recommendation ili signal: Recommendation (akcije: markdown/transfer/stop-reorder) uz quality gating.
- Gde se prikazuje: Inventory action queue i kapital risk panel.
- Prioritet: P1.
- Minimalni prvi MVP: top artikli po vezanom kapitalu i predlog akcije bez automatskog izvršavanja.

### 12) Dead stock aging

- Poslovno pitanje: Koliko dugo roba stoji bez prodaje i kolika je finansijska izloženost?
- Formula: Dead stock aging bucket = dani od poslednje prodaje; exposure = qty x cost po bucket-u.
- Potrebni podaci: poslednja prodaja, trenutna qty, cost.
- Da li podaci već postoje u Trendplus DB: Postoji/Delimično (core signal postoji, finansijski deo zavisi od cost coverage).
- Data quality rizici: "phantom stock" zbog nekonzistentnih pokreta, nedostajući cost.
- Recommendation ili signal: Recommendation (clearance/transfer/hold) uz policy.
- Gde se prikazuje: Inventory dead-stock sekcija i reports.
- Prioritet: P0.
- Minimalni prvi MVP: aging bucketi (30/60/90/120+) sa ekspozicijom i preporukom po bucket-u.

### 13) Price elasticity signal

- Poslovno pitanje: Koliko potražnja reaguje na promenu cene?
- Formula: Elasticity proxy = % promena qty / % promena cene (u kontrolisanom prozoru).
- Potrebni podaci: istorija cena kroz vreme, prodaja po periodu, kontrolni faktori (sezona/promo).
- Da li podaci već postoje u Trendplus DB: Delimično/Nedostaje (zavisi od pune price history evidencije).
- Data quality rizici: confounding efekti, mali uzorak, istovremene promo kampanje.
- Recommendation ili signal: Signal samo (bez automatske preporuke u ranoj fazi).
- Gde se prikazuje: Pricing eksperimenti i pre-nivelacija panel.
- Prioritet: P2.
- Minimalni prvi MVP: elasticitet proxy za artikle sa dovoljno price-change događaja.

### 14) Return/refund impact

- Poslovno pitanje: Koliko povraćaji umanjuju stvarni rezultat po kategoriji/dobavljaču?
- Formula: Return impact = (refund revenue + povraćajni troškovi) / bruto prihod; margin impact analogno.
- Potrebni podaci: refund/return transakcije, povezivanje sa originalnom prodajom, trošak.
- Da li podaci već postoje u Trendplus DB: Delimično/Nedostaje (zavisi od kvaliteta i pokrivenosti povraćaja).
- Data quality rizici: nedovoljno povezivanje povraćaja sa originalnim redom prodaje, duplikati.
- Recommendation ili signal: Signal (P2), recommendation tek posle stabilizacije povraćaja.
- Gde se prikazuje: Data quality i profitability report.
- Prioritet: P2.
- Minimalni prvi MVP: refund rate po kategoriji sa napomenom o coverage-u.

### 15) Transfer opportunity između prodavnica

- Poslovno pitanje: Gde možemo smanjiti OOS i dead stock internim transferom između prodavnica?
- Formula: Transfer opportunity score = višak stock-a u prodavnici A - manjak stock-a u prodavnici B, ponderisano brzinom prodaje i marginom.
- Potrebni podaci: store-level inventory, store-level sales velocity, transfer lead-time i istorija prenosa.
- Da li podaci već postoje u Trendplus DB: Delimično (pokreti prenosa postoje; store-level snapshot i SLA/lead-time često nisu kompletni).
- Data quality rizici: kašnjenje knjiženja prenosa, netačno stanje po prodavnici u trenutku odluke.
- Recommendation ili signal: Recommendation (human-in-the-loop potvrda).
- Gde se prikazuje: Inventory transfer page i Action Queue.
- Prioritet: P1.
- Minimalni prvi MVP: predlog top transfer parova (store-source -> store-target) sa očekivanim efektom.

## Decision flow blueprint (bez runtime implementacije)

### Flow A: Replenish / Hold / Markdown (Inventory)

- Ulazni KPI set: stock cover, sell-through, dead stock aging, slow stock capital.
- Gating:
  - ako data quality nije dobra -> samo signal, bez recommendation statusa
  - ako postoji fallback dataset -> recommendationAllowed = false
- Izlaz:
  - Replenish: nizak cover + stabilna potražnja
  - Hold: neutralni pragovi
  - Markdown/Clearance: visok aging + nizak turnover

### Flow B: Supplier portfolio actions

- Ulazni KPI set: supplier dependency risk, category contribution margin, margin loss zbog nivelacija.
- Gating:
  - unknown supplier udeo iznad praga -> pomoćni signal, bez final recommendation
- Izlaz:
  - Diversify supplier mix
  - Expand/keep supplier
  - Reduce exposure

### Flow C: Transfer between stores

- Ulazni KPI set: transfer opportunity, OOS lost sales estimate, size/color risk.
- Gating:
  - store-level inventory stale -> recommendation blocked
- Izlaz:
  - Predlog transfer naloga uz expected recovery (units/revenue)

## Prioritetni rollout

### P0 (može sa postojećim ili delimično postojećim podacima)

- Sell-through ratio (signal)
- Inventory turnover (units varijanta)
- Margin loss zbog nivelacija (signal)
- Stock cover / days of supply
- Size/color availability risk (uz coverage prag)
- Supplier dependency risk
- Category contribution margin
- Dead stock aging

### P1 (zahteva bolju stabilnost modela/snapshot-a)

- GMROI
- Markdown efficiency
- OOS lost sales estimate (grubi model)
- Slow stock capital
- Transfer opportunity između prodavnica

### P2 (zahteva nove ili znatno bolju istorijsku evidenciju)

- Price elasticity signal
- Return/refund impact

## Predlog minimalnih data enabler-a (pre runtime implementacije KPI recommendation-a)

- Stabilan periodični inventory snapshot po prodavnici i artiklu.
- Dosledna istorija promene cena (event-level).
- Jači coverage cost polja i validacija outlier-a.
- Standardizacija size/color atributa.
- Jače povezivanje povraćaja sa originalnim sale line-om.

## Guardrails za rollout KPI-ja

- Bez fake zero prikaza kada je problem u izvoru/refresh-u.
- Svaki KPI mora imati data quality status i reason kod kada je signal degradiran.
- KPI sa Delimično/Nedostaje statusom ne sme odmah biti final recommendation.
- UI prikaz mora razlikovati:
  - Recommendation (akcija)
  - Signal (indikator)
  - Insufficient data (nema validnog zaključka)

## Definition of Done za svaku buduću KPI implementaciju

- Formula dokumentovana i testabilna.
- Data requirements mapirani na konkretne tabele/kolone.
- Data quality rizici eksplicitno navedeni.
- Recommendation vs signal odluka eksplicitna.
- Minimal MVP jasno definisan pre razvoja.
