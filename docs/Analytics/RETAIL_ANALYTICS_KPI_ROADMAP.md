# Retail Analytics KPI Roadmap

Status: planning-only dokument, bez runtime promena, bez migracija.
Namena: definisati sledeci nivo retail analytics KPI-jeva i decision flow logike tako da Trendplus podrzi ozbiljne odluke o prodaji, zalihama, dobavljacima i snizenjima.

## Kako citati roadmap

- `Yes`: podaci verovatno vec postoje u Trendplus DB za MVP signal ili report.
- `Partial`: deo podataka postoji, ali coverage, istorija ili granularnost nisu dovoljno stabilni za pun recommendation mode.
- `No`: bez novih podataka ili novih eventa ne treba uvoditi kao finalni KPI.
- `Unknown`: trenutno nije potvrdeno da li model ili ETL vec nose potreban signal.

## Readiness pregled

### Moze relativno brzo u MVP signal/recommendation sloj

- Sell-through ratio
- Inventory turnover
- Margin loss zbog nivelacija/snizenja
- Stock cover / days of supply
- OOS lost sales estimate
- Size/color availability risk
- Supplier dependency risk
- Category contribution margin
- Slow stock capital
- Dead stock aging
- Transfer opportunity izmedju prodavnica
- Replenishment/OOS decision flow
- Store performance comparison
- Assortment gap detection

### Trazi jaci data foundation ili dodatne evente

- GMROI
- Markdown efficiency
- Price elasticity signal
- Return/refund impact
- Supplier negotiation pack
- Markdown optimizer

## KPI i Decision Flow katalog (20)

### 1. Sell-through ratio

- Poslovno pitanje: Da li artikl, kategorija ili dobavljac prodaje dovoljno brzo u odnosu na raspolozivu zalihu i ulaze?
- Formula: `sell_through_ratio = sold_units / (opening_stock_units + inbound_units)` za izabrani period.
- Potrebni podaci: prodaja, lager, datumi, prodavnica, kategorija, dobavljac.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: missing stock snapshot, kasnjenje refresh-a lagera, netacan opening stock.
- Tip: signal.
- Gde se prikazuje: Dashboard, Product Decision, Inventory, Reports.
- Minimalni MVP: 30d i 90d sell-through po SKU i kategoriji sa quality bedzom.
- Prioritet: P0.
- Rizik od pogresnog tumacenja: nizak sell-through ne znaci automatski los proizvod; moguci su visoka cena, sezonalnost ili kasni ulaz robe.

### 2. Inventory turnover

- Poslovno pitanje: Koliko brzo se prosecna zaliha obrce i gde je kapital spor?
- Formula: `inventory_turnover = sold_units / avg_on_hand_units` za units varijantu; naprednije `COGS / avg_inventory_value`.
- Potrebni podaci: prodaja, lager, nabavna cena, datumi, kategorija, dobavljac, prodavnica.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: missing stock history, missing cost, los avg inventory snapshot.
- Tip: signal.
- Gde se prikazuje: Inventory, Supplier, Reports.
- Minimalni MVP: units turnover po kategoriji i dobavljacu bez pune finansijske valuacije.
- Prioritet: P0.
- Rizik od pogresnog tumacenja: visok turnover moze znaciti i premali lager, ne samo zdravu rotaciju.

### 3. GMROI

- Poslovno pitanje: Koliko bruto marze zaradjujemo po ulozenom dinaru u zalihu?
- Formula: `gmroi = gross_margin_value / avg_inventory_cost_value`.
- Potrebni podaci: prodaja, lager, nabavna cena, datumi, kategorija, dobavljac, prodavnica.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: missing cost, nestabilna avg inventory value, nejasan valuation trenutak.
- Tip: signal.
- Gde se prikazuje: Supplier, Inventory, Reports.
- Minimalni MVP: GMROI-lite po kategoriji i dobavljacu sa jakim disclaimer-om o cost coverage-u.
- Prioritet: P1.
- Rizik od pogresnog tumacenja: korisnik moze GMROI da cita kao finalnu profitabilnost iako je cost coverage delimican.

### 4. Margin loss zbog nivelacija/snizenja

- Poslovno pitanje: Koliko marze gubimo zbog promene cena, nivelacija i markdown odluka?
- Formula: `margin_loss = expected_margin_without_price_change - realized_margin_after_price_change`.
- Potrebni podaci: prodaja, nabavna cena, datumi, nivelacije, kategorija, dobavljac, prodavnica.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: nepotpuna istorija cena, netacan trenutak promene, missing cost.
- Tip: signal.
- Gde se prikazuje: Dashboard, Supplier, Reports.
- Minimalni MVP: agregat po dobavljacu i kategoriji za pre/post intervale.
- Prioritet: P0.
- Rizik od pogresnog tumacenja: pad marze ne mora biti posledica samo markdown-a; mesaju se sezona, mix i akcije.

### 5. Markdown efficiency

- Poslovno pitanje: Da li markdown oslobadja lager dovoljno brzo u odnosu na izgubljenu marzu?
- Formula: `markdown_efficiency = incremental_units_sold_after_markdown / margin_loss_from_markdown` ili skor kombinacija sell-through uplift i margin loss.
- Potrebni podaci: prodaja, lager, nabavna cena, datumi, nivelacije, kategorija, dobavljac, prodavnica.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: confounding zbog promocija i sezonalnosti, nepotpuna price history.
- Tip: signal.
- Gde se prikazuje: Reports, Supplier, Dashboard.
- Minimalni MVP: 14d pre/post markdown analiza po SKU ili kategoriji.
- Prioritet: P1.
- Rizik od pogresnog tumacenja: korisnik moze poverovati da markdown "radi" i kada je uplift posledica spoljnog faktora.

### 6. Stock cover / days of supply

- Poslovno pitanje: Koliko dana trenutna zaliha pokriva ocekivanu traznju?
- Formula: `days_of_supply = current_on_hand_units / avg_daily_sales_units`.
- Potrebni podaci: prodaja, lager, datumi, prodavnica, kategorija, dobavljac.
- Da li podaci vec postoje u Trendplus DB: Yes.
- Data quality rizici: missing stock refresh, volatilnost kod sporih SKU, sezonalni pikovi.
- Tip: recommendation.
- Gde se prikazuje: Inventory, Dashboard, Actions.
- Minimalni MVP: low/medium/high cover zone sa reason code-om.
- Prioritet: P0.
- Rizik od pogresnog tumacenja: kratak cover ne znaci automatski dopunu ako je artikal pri kraju sezone ili planiran za markdown.

### 7. OOS lost sales estimate

- Poslovno pitanje: Koliki prihod i marza su izgubljeni zbog out-of-stock perioda?
- Formula: `lost_sales_units = expected_units_if_in_stock - actual_units`; `lost_sales_value = lost_sales_units * avg_selling_price`.
- Potrebni podaci: prodaja, lager, nabavna cena, datumi, prodavnica, kategorija, velicina, boja.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: missing stock intervals, slaba baseline traznja, substitucija drugim SKU.
- Tip: signal.
- Gde se prikazuje: Inventory, Dashboard, Reports.
- Minimalni MVP: gruba procena po SKU/store sa confidence warning-om.
- Prioritet: P0.
- Rizik od pogresnog tumacenja: estimate moze preceniti izgubljenu prodaju ako je kupac presao na slican artikal.

### 8. Size/color availability risk

- Poslovno pitanje: Gde gubimo prodaju jer nedostaju kljucne velicine ili boje?
- Formula: `availability_risk = sum(variant_demand_weight * is_variant_oos)` po SKU/store.
- Potrebni podaci: prodaja, lager, velicina, boja, prodavnica, datumi.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: incomplete size/color atributi, nekonzistentna standardizacija varijanti, missing stock po varijanti.
- Tip: recommendation.
- Gde se prikazuje: Product Decision, Inventory, Reports.
- Minimalni MVP: lista SKU sa visokim risk score-om za velicinu/boju i predlog dopune.
- Prioritet: P0.
- Rizik od pogresnog tumacenja: korisnik moze misliti da je ceo SKU problem, iako je problem samo u jednoj kljucnoj velicini ili boji.

### 9. Supplier dependency risk

- Poslovno pitanje: Koliko smo izlozeni riziku zbog prevelike zavisnosti od malog broja dobavljaca?
- Formula: `supplier_dependency_risk = HHI(revenue_share_by_supplier)` ili `top_5_supplier_share`.
- Potrebni podaci: prodaja, nabavna cena, dobavljac, kategorija, datumi.
- Da li podaci vec postoje u Trendplus DB: Yes.
- Data quality rizici: missing supplier, duplikati dobavljaca, loše mapiranje nepoznatih dobavljaca.
- Tip: recommendation.
- Gde se prikazuje: Supplier, Dashboard, Reports.
- Minimalni MVP: top concentration warning sa pragovima i listom dobavljaca koji nose rizik.
- Prioritet: P0.
- Rizik od pogresnog tumacenja: visok share jednog dobavljaca nije uvek los ako je strategijski ili ekskluzivni partner.

### 10. Category contribution margin

- Poslovno pitanje: Koje kategorije stvarno nose marzu, a koje samo prihod?
- Formula: `category_contribution_margin = (revenue - COGS) / revenue` i apsolutni `revenue - COGS`.
- Potrebni podaci: prodaja, nabavna cena, kategorija, datumi, prodavnica.
- Da li podaci vec postoje u Trendplus DB: Yes.
- Data quality rizici: missing cost, nekonzistentna kategorizacija, outlier cost.
- Tip: recommendation.
- Gde se prikazuje: Dashboard, Supplier, Reports.
- Minimalni MVP: top/bottom kategorije po contribution margin-u sa quality status-om.
- Prioritet: P0.
- Rizik od pogresnog tumacenja: visoka marza procenat ne znaci i najveci ukupni doprinos u dinarima.

### 11. Slow stock capital

- Poslovno pitanje: Koliko kapitala je vezano u robi koja se krece presporo?
- Formula: `slow_stock_capital = sum(on_hand_units * unit_cost)` za SKU gde je turnover ili velocity ispod praga.
- Potrebni podaci: lager, nabavna cena, prodaja, datumi, kategorija, dobavljac, prodavnica.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: missing cost, stale stock, loš threshold izbor po kategoriji.
- Tip: recommendation.
- Gde se prikazuje: Inventory, Actions, Reports.
- Minimalni MVP: top 50 SKU po slow stock kapitalu sa predlogom markdown/transfer/hold.
- Prioritet: P1.
- Rizik od pogresnog tumacenja: spor artikal ne mora biti problem ako je sezonski ili strateški assortment anchor.

### 12. Dead stock aging

- Poslovno pitanje: Koji artikli stoje predugo bez prodaje i koliki je finansijski teret toga?
- Formula: `aging_days = today - last_sale_date`; `dead_stock_value = on_hand_units * unit_cost` po aging bucket-u.
- Potrebni podaci: prodaja, lager, nabavna cena, datumi, kategorija, prodavnica.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: phantom stock, netacan last sale date, missing cost.
- Tip: recommendation.
- Gde se prikazuje: Inventory, Dashboard, Reports.
- Minimalni MVP: aging bucketi 30/60/90/120+ sa akcijom hold/markdown/clearance.
- Prioritet: P0.
- Rizik od pogresnog tumacenja: dugo bez prodaje ne znaci automatski otpis; moguca je sezonska obnova traznje.

### 13. Price elasticity signal

- Poslovno pitanje: Kako promena cene menja traznju za dati SKU ili kategoriju?
- Formula: `elasticity_signal = pct_change_units / pct_change_price` uz filtriranje neuporedivih perioda.
- Potrebni podaci: prodaja, datumi, nivelacije, kategorija, prodavnica, povrati.
- Da li podaci vec postoje u Trendplus DB: Unknown.
- Data quality rizici: mali uzorci, akcije i sezona kao confounder, nepotpuna istorija cena.
- Tip: signal.
- Gde se prikazuje: Reports, Product Decision.
- Minimalni MVP: proxy signal samo za SKU sa vise price-change dogadjaja.
- Prioritet: P2.
- Rizik od pogresnog tumacenja: korisnik moze izvesti kauzalni zakljucak iz korelacije bez kontrole za sezonu i promocije.

### 14. Return/refund impact

- Poslovno pitanje: Koliko povrati i refundacije umanjuju stvarnu profitabilnost?
- Formula: `return_impact = (refund_revenue + return_handling_cost + lost_margin) / gross_revenue`.
- Potrebni podaci: prodaja, povrati, refundacije, nabavna cena, datumi, kategorija, prodavnica.
- Da li podaci vec postoje u Trendplus DB: Unknown.
- Data quality rizici: no returns history, nepovezan refund sa originalnim sale line-om, dupli zapisi.
- Tip: signal.
- Gde se prikazuje: Reports, Data Quality, Dashboard.
- Minimalni MVP: refund rate i procena uticaja po kategoriji sa coverage warning-om.
- Prioritet: P2.
- Rizik od pogresnog tumacenja: korisnik moze potceniti profitabilnu kategoriju zbog tehnički loše povezanih refund događaja.

### 15. Transfer opportunity izmedju prodavnica

- Poslovno pitanje: Gde interni transfer resava OOS u jednoj prodavnici i slow stock u drugoj?
- Formula: `transfer_opportunity_score = shortage_target * surplus_source * demand_velocity * margin_weight`.
- Potrebni podaci: lager, prodaja, prodavnica, datumi, nabavna cena, transfer istorija.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: no transfer history, stale store stock, kasnjenje osvezavanja po prodavnici.
- Tip: recommendation.
- Gde se prikazuje: Inventory, Actions, Reports.
- Minimalni MVP: top source-target parovi sa ocekivanim recovery signalom.
- Prioritet: P1.
- Rizik od pogresnog tumacenja: predlog transfera moze ignorisati operativni trosak ili pravila visual merchandising-a.

### 16. Supplier negotiation pack

- Poslovno pitanje: Kako pripremiti pregovor sa dobavljacem kroz jedan dokazni paket KPI-jeva i akcija?
- Formula: nije jedan KPI; paket = kombinacija `sell-through`, `margin trend`, `markdown loss`, `dependency risk`, `stock risk`, `OOS impact`.
- Potrebni podaci: prodaja, lager, nabavna cena, dobavljac, kategorija, datumi, nivelacije.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: missing supplier, missing cost, nekonzistentni periodi izmedju KPI-jeva.
- Tip: report.
- Gde se prikazuje: Supplier, Reports.
- Minimalni MVP: print-ready supplier report sa 5 KPI, quality sazetkom i CTA preporukama.
- Prioritet: P1.
- Rizik od pogresnog tumacenja: korisnik moze paket citati kao finalnu presudu o dobavljacu iako je deo signala pomocni ili fallback.

### 17. Markdown optimizer

- Poslovno pitanje: Koji nivo markdown-a najverovatnije balansira clearance i marzu?
- Formula: `argmax(markdown_pct) of expected_margin_after_markdown - holding_cost - clearance_penalty`.
- Potrebni podaci: prodaja, lager, nabavna cena, datumi, nivelacije, price elasticity signal, kategorija.
- Da li podaci vec postoje u Trendplus DB: No.
- Data quality rizici: slab elasticity signal, nema policy ogranicenja, nema dovoljno pre/post istorije.
- Tip: recommendation.
- Gde se prikazuje: Product Decision, Reports.
- Minimalni MVP: simulator scenarija 5/10/15/20% bez automatske preporuke za primenu.
- Prioritet: P2.
- Rizik od pogresnog tumacenja: korisnik moze tretirati simulator kao pouzdanu optimizaciju iako je samo scenario tool.

### 18. Replenishment/OOS decision flow

- Poslovno pitanje: Koja je sledeca najbolja akcija po SKU/store: dopuna, transfer, hold ili markdown?
- Formula: `decision_score = w1*oos_risk + w2*days_of_supply_gap + w3*margin_priority + w4*availability_risk - w5*data_quality_penalty`.
- Potrebni podaci: prodaja, lager, nabavna cena, dobavljac, prodavnica, velicina, boja, datumi, transfer signal.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: stale stock, missing supplier lead-time, incomplete size/color coverage.
- Tip: recommendation.
- Gde se prikazuje: Inventory, Actions, Product Decision, Dashboard.
- Minimalni MVP: rule-based flow sa izlazima `replenish`, `transfer`, `hold`, `markdown` i reason code-ovima.
- Prioritet: P0.
- Rizik od pogresnog tumacenja: recommendation moze delovati autoritativno iako je data quality slab ili lead-time nije poznat.

### 19. Store performance comparison

- Poslovno pitanje: Koje prodavnice ostvaruju bolju prodaju, marzu i rotaciju za uporediv assortment?
- Formula: kombinovani benchmark po prodavnici, npr. `store_score = z(revenue_per_sku) + z(margin_pct) + z(turnover) - z(oos_rate)`.
- Potrebni podaci: prodaja, lager, nabavna cena, prodavnica, kategorija, datumi, velicina, boja.
- Da li podaci vec postoje u Trendplus DB: Yes.
- Data quality rizici: neuporediv assortment, razlike u footfall-u, missing stock po store-u.
- Tip: report.
- Gde se prikazuje: Dashboard, Reports, Inventory.
- Minimalni MVP: store benchmark tabela po prihod/marza/turnover/OOS sa filterom po kategoriji i periodu.
- Prioritet: P1.
- Rizik od pogresnog tumacenja: korisnik moze kriviti store tim iako je problem u assortmanu, veličinama ili supply-u.

### 20. Assortment gap detection

- Poslovno pitanje: Gde nedostaje bitan assortment u odnosu na traznju, sezonu ili peer store obrazac?
- Formula: `assortment_gap_score = expected_assortment_presence - actual_presence` po kategoriji, brendu, velicini, boji ili store-u.
- Potrebni podaci: prodaja, lager, kategorija, velicina, boja, prodavnica, dobavljac, datumi.
- Da li podaci vec postoje u Trendplus DB: Partial.
- Data quality rizici: incomplete size/color, nedovoljno bogat assortment master, slab baseline za peer comparison.
- Tip: recommendation.
- Gde se prikazuje: Product Decision, Inventory, Dashboard, Reports.
- Minimalni MVP: lista kategorija ili SKU klastera koji imaju demand signal ali slabo pokrice assortmana.
- Prioritet: P1.
- Rizik od pogresnog tumacenja: gap ne znaci automatski da treba kupiti vise; moze znaciti da peer store ima drugaciji profil kupca.

## Sta moze odmah, a sta trazi nove podatke

### P0: visoka vrednost, relativno brz MVP

- Sell-through ratio
- Inventory turnover
- Margin loss zbog nivelacija/snizenja
- Stock cover / days of supply
- OOS lost sales estimate
- Size/color availability risk
- Supplier dependency risk
- Category contribution margin
- Dead stock aging
- Replenishment/OOS decision flow

### P1: vredno, ali trazi bolji quality ili standardizaciju

- GMROI
- Markdown efficiency
- Slow stock capital
- Transfer opportunity izmedju prodavnica
- Supplier negotiation pack
- Store performance comparison
- Assortment gap detection

### P2: trazice nove podatke, napredniji modeling ili ozbiljniji eksperiment design

- Price elasticity signal
- Return/refund impact
- Markdown optimizer

## Predlog data enabler-a pre pune recommendation faze

- Stabilan periodicki inventory snapshot po SKU, store-u i po mogucstvu po varijanti.
- Dosledna istorija cena i markdown event-a sa datumom vazenja.
- Jaci coverage nabavne cene i alerting za missing cost.
- Standardizacija atributa velicina i boja.
- Jasno mapiranje povrata i refundacije na originalni sale line.
- Pouzdaniji transfer history i store-level inventory refresh signal.

## Guardrails za buducu implementaciju

- No fake-zero: greska, timeout ili missing dependency nikad ne smeju izgledati kao validna nula.
- Delimicni podaci daju signal ili report, ne finalnu recommendation odluku.
- Svaki KPI mora imati data quality status i reason code kada je degradiran.
- Trust, refresh i quality signal moraju biti vidljivi pre svake akcione preporuke.
- Frontend ne sme lokalno izmisljati finalne recommendation score-ove bez backend contract-a.

## Napomena

Ovaj dokument je roadmap i specifikacija za planiranje. Ne uvodi runtime promene ni u frontend ni u backend sloju.
