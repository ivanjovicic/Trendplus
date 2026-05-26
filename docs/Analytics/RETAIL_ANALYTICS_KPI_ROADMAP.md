# Retail Analytics KPI Roadmap (Trendplus)

Status: planning-only dokument, bez runtime promena.
Scope: definicija naprednih KPI-jeva i decision flow smernica za prodaju, zalihu i dobavljace.

## Cilj

Trendplus treba da predje iz reporting alata u decision platformu: da menadzer dobije jasan signal, stepen pouzdanosti i sledecu akciju.

## Readiness pregled: sta moze odmah vs sta trazi nove podatke

### Moze odmah ili uz postojece delove modela (P0/P1 MVP)

- Sell-through ratio
- Inventory turnover (units varijanta)
- Margin loss zbog nivelacija (signal)
- Markdown efficiency (osnovni pre/post signal)
- Stock cover / days of supply
- OOS lost sales estimate (gruba procena)
- Size/color availability risk (uz coverage prag)
- Supplier dependency risk
- Category contribution margin
- Slow stock capital
- Dead stock aging
- Transfer opportunity izmedju prodavnica (human-in-the-loop)
- Supplier negotiation pack (na osnovu postojecih KPI)
- Replenishment/OOS decision flow (rule-based MVP)

### Trazi nove podatke ili znacajno bolji quality coverage

- GMROI (stabilan valuation snapshot kroz vreme)
- Price elasticity signal (puna istorija cena + kontrolni faktori)
- Return/refund impact (pouzdano mapiranje povrata na originalnu prodaju)
- Markdown optimizer (eksperimentalni uplift/elasticity sloj)

## Legenda dostupnosti podataka

- Postoji: kljucni podaci vec postoje u Trendplus DB i mogu u MVP.
- Delimicno: deo podataka postoji, ali treba bolja granularnost ili coverage.
- Nedostaje: bez novih tabela/eventa ne uvoditi kao final recommendation.

## KPI i Decision Flow katalog (18)

## 1) Sell-through ratio

- Poslovno pitanje: Koliko brzo se roba iz zalihe prodaje u izabranom periodu?
- Formula: sell_through = sold_units / (opening_stock_units + inbound_units).
- Potrebni podaci: prodate jedinice, ulaz robe, pocetna zaliha po periodu/SKU.
- Da li podaci vec postoje u Trendplus DB: Delimicno.
- Data quality rizici: netacan opening stock snapshot, kasnjenje osvezavanja pokreta.
- Recommendation ili signal: Signal u P0, recommendation u P1.
- Gde se prikazuje: /analytics/inventory, /analytics/products, supplier report sekcije.
- Minimalni MVP: 30d i 90d sell-through po SKU/kategoriji sa data quality bedzom.
- Prioritet: P0.

## 2) Inventory turnover

- Poslovno pitanje: Koliko puta se prosecna zaliha okrene u periodu?
- Formula: turnover_units = sold_units / avg_on_hand_units. Finansijski: turnover_value = COGS / avg_inventory_value.
- Potrebni podaci: sold units, average on-hand, COGS (za finansijsku varijantu).
- Da li podaci vec postoje u Trendplus DB: Delimicno.
- Data quality rizici: slaba istorija avg zalihe, cost coverage rupe.
- Recommendation ili signal: Signal.
- Gde se prikazuje: /analytics/inventory i category drilldown.
- Minimalni MVP: units turnover po kategoriji/dobavljacu, bez finansijske varijante.
- Prioritet: P0.

## 3) GMROI

- Poslovno pitanje: Koliko bruto marze dobijamo po ulozenom dinaru u zalihu?
- Formula: GMROI = gross_margin_value / avg_inventory_cost_value.
- Potrebni podaci: prihod, COGS, prosecna vrednost zalihe po nabavnoj ceni.
- Da li podaci vec postoje u Trendplus DB: Delimicno.
- Data quality rizici: netacan COGS, nestabilna procena avg inventory value.
- Recommendation ili signal: Signal dok valuation ne postane stabilan.
- Gde se prikazuje: supplier scorecard, category profitability panel.
- Minimalni MVP: GMROI-lite kao pomocni signal sa jasnim disclaimer-om.
- Prioritet: P1.

## 4) Margin loss zbog nivelacija

- Poslovno pitanje: Koliko marze gubimo zbog nivelacije/korigovanja cena?
- Formula: margin_loss = expected_margin_without_nivelacija - realized_margin_after_nivelacija.
- Potrebni podaci: istorija cena, prodaja pre/posle, nabavna cena.
- Da li podaci vec postoje u Trendplus DB: Delimicno.
- Data quality rizici: nepotpuna istorija promene cene, pogresan event timing.
- Recommendation ili signal: Signal.
- Gde se prikazuje: pre/post nivelacija i supplier decision report.
- Minimalni MVP: agregat po dobavljacu i kategoriji sa confidence indikatorom.
- Prioritet: P0.

## 5) Markdown efficiency

- Poslovno pitanje: Da li markdown oslobadja lager dovoljno efikasno u odnosu na izgubljenu marzu?
- Formula: markdown_eff = incremental_units_cleared / margin_loss_from_markdown.
- Potrebni podaci: markdown event-i, prodaja pre/posle, marza delta.
- Da li podaci vec postoje u Trendplus DB: Delimicno.
- Data quality rizici: attribution bias, preklapanje promocija i sezonalnosti.
- Recommendation ili signal: Signal.
- Gde se prikazuje: pricing/nivelacija report i supplier decision context.
- Minimalni MVP: 14d pre/post analiza bez kauzalnog modela.
- Prioritet: P1.

## 6) Stock cover / days of supply

- Poslovno pitanje: Koliko dana trenutna zaliha pokriva traznju?
- Formula: days_of_supply = current_on_hand_units / avg_daily_sales_units.
- Potrebni podaci: trenutno stanje zalihe, rolling daily sales velocity.
- Da li podaci vec postoje u Trendplus DB: Postoji.
- Data quality rizici: volatilnost kod niskog volumena, sezonski pikovi.
- Recommendation ili signal: Recommendation (dopuna, stop reorder, markdown).
- Gde se prikazuje: /analytics/inventory i akcioni panel.
- Minimalni MVP: risk zone (nizak/srednji/visok cover) + predlog akcije.
- Prioritet: P0.

## 7) OOS lost sales estimate

- Poslovno pitanje: Koliki prihod i marza su izgubljeni zbog OOS stanja?
- Formula: lost_sales_units = expected_units_during_oos - actual_units_during_oos.
- Formula: lost_margin = lost_sales_units * unit_margin.
- Potrebni podaci: OOS intervali, baseline demand signal, marza po artiklu.
- Da li podaci vec postoje u Trendplus DB: Delimicno.
- Data quality rizici: substitucija artikla, slab demand baseline za retke SKU.
- Recommendation ili signal: Signal.
- Gde se prikazuje: OOS risk panel i nedeljni report.
- Minimalni MVP: rough estimate + interval pouzdanosti + warning.
- Prioritet: P1.

## 8) Size/color availability risk

- Poslovno pitanje: Gde gubimo prodaju jer nedostaju kljucne velicine/boje?
- Formula: avail_risk = sum(weight_variant_demand * is_variant_oos).
- Potrebni podaci: prodaja po varijanti, stanje po varijanti, atributi velicina/boja.
- Da li podaci vec postoje u Trendplus DB: Delimicno.
- Data quality rizici: nekonzistentna standardizacija velicina/boja, mapiranje sifara.
- Recommendation ili signal: Recommendation kad coverage atributa predje prag; inace signal.
- Gde se prikazuje: /analytics/products i /analytics/inventory.
- Minimalni MVP: top SKU lista sa risk score i predlogom dopune varijanti.
- Prioritet: P0.

## 9) Supplier dependency risk

- Poslovno pitanje: Koliko smo zavisni od malog broja dobavljaca?
- Formula: dependency_risk = HHI(revenue_share_by_supplier) ili topN_share.
- Potrebni podaci: prihod i marza po dobavljacu kroz vreme.
- Da li podaci vec postoje u Trendplus DB: Postoji.
- Data quality rizici: unknown supplier mapiranje, duplikati dobavljaca.
- Recommendation ili signal: Recommendation na portfolio nivou.
- Gde se prikazuje: /analytics/supplier i supplier report.
- Minimalni MVP: top concentration warning + predlog diverzifikacije.
- Prioritet: P0.

## 10) Category contribution margin

- Poslovno pitanje: Koje kategorije donose stvarni doprinos marzi?
- Formula: contribution_margin_pct = (revenue - COGS) / revenue.
- Potrebni podaci: revenue, COGS, kategorija proizvoda.
- Da li podaci vec postoje u Trendplus DB: Postoji (uz cost caveat).
- Data quality rizici: missing cost, nekonzistentna kategorizacija.
- Recommendation ili signal: Recommendation (invest, hold, reduce).
- Gde se prikazuje: /analytics i category sekcije.
- Minimalni MVP: rang lista kategorija sa contribution signalom i quality bedzom.
- Prioritet: P0.

## 11) Slow stock capital

- Poslovno pitanje: Koliko kapitala je vezano u sporoj robi?
- Formula: slow_stock_capital = sum(on_hand_units * unit_cost) for turnover < threshold.
- Potrebni podaci: on-hand, unit cost, turnover klasifikacija.
- Da li podaci vec postoje u Trendplus DB: Delimicno.
- Data quality rizici: slabo cost pokrice, stale on-hand podaci.
- Recommendation ili signal: Recommendation uz human confirmation.
- Gde se prikazuje: /analytics/inventory i /analytics/actions.
- Minimalni MVP: top 50 SKU po vezanom kapitalu sa predlog akcije.
- Prioritet: P1.

## 12) Dead stock aging

- Poslovno pitanje: Koji artikli stoje predugo i koliko kosta taj zastoj?
- Formula: aging_days = today - last_sale_date.
- Formula: dead_stock_exposure = on_hand_units * unit_cost by aging bucket.
- Potrebni podaci: poslednji datum prodaje, on-hand, cost.
- Da li podaci vec postoje u Trendplus DB: Postoji/Delimicno.
- Data quality rizici: phantom stock, netacan last sale datum zbog merge/mapiranja.
- Recommendation ili signal: Recommendation.
- Gde se prikazuje: /analytics/inventory i report sekcije.
- Minimalni MVP: bucketi 30/60/90/120+ sa akcijom hold/markdown/clearance.
- Prioritet: P0.

## 13) Price elasticity signal

- Poslovno pitanje: Kako promena cene utice na traznju?
- Formula: elasticity_proxy = pct_change_units / pct_change_price.
- Potrebni podaci: istorija cena, prodaja po vremenskim prozorima, promo/sezona flag.
- Da li podaci vec postoje u Trendplus DB: Delimicno/Nedostaje.
- Data quality rizici: confounding faktori, mali uzorak, istovremene kampanje.
- Recommendation ili signal: Signal samo u ranoj fazi.
- Gde se prikazuje: pricing laboratorija i pre-nivelacija analitika.
- Minimalni MVP: signal za SKU sa dovoljnim brojem price-change dogadjaja.
- Prioritet: P2.

## 14) Return/refund impact

- Poslovno pitanje: Koliko povrati i refundacije umanjuju profitabilnost?
- Formula: refund_impact_pct = (refund_revenue + refund_cost) / gross_revenue.
- Potrebni podaci: povrati/refundacije, povezivanje sa originalnim sale line-om, cost.
- Da li podaci vec postoje u Trendplus DB: Delimicno/Nedostaje.
- Data quality rizici: nedostajuci linkage povrat-prodaja, dupli refund zapisi.
- Recommendation ili signal: Signal.
- Gde se prikazuje: profitability report i data quality panel.
- Minimalni MVP: refund rate po kategoriji sa coverage upozorenjem.
- Prioritet: P2.

## 15) Transfer opportunity izmedju prodavnica

- Poslovno pitanje: Gde interni transfer smanjuje OOS i dead stock najbrze?
- Formula: transfer_score = (surplus_source - shortage_target) * demand_velocity * margin_weight.
- Potrebni podaci: store-level inventory, store-level velocity, transfer istorija.
- Da li podaci vec postoje u Trendplus DB: Delimicno.
- Data quality rizici: kasnjenje knjizenja prenosa, stale store stanje.
- Recommendation ili signal: Recommendation sa human approval korakom.
- Gde se prikazuje: inventory transfer i /analytics/actions.
- Minimalni MVP: top transfer parovi source->target sa expected recovery.
- Prioritet: P1.

## 16) Supplier negotiation pack

- Poslovno pitanje: Kako pripremiti pregovore sa dobavljacem na osnovu dokaza?
- Formula: nije jedan KPI; scorecard paket = weighted index od margin trend, sell-through, markdown loss, OOS impact, dependency risk.
- Potrebni podaci: supplier-level KPI istorija, quality status, trendovi po periodima.
- Da li podaci vec postoje u Trendplus DB: Delimicno (uglavnom postoji, treba standardizovan paket).
- Data quality rizici: neuskaldjene metrike po periodima, fallback dataset bez oznake.
- Recommendation ili signal: Recommendation za pregovaracke akcije (human-in-the-loop).
- Gde se prikazuje: /analytics/supplier/report i report export.
- Minimalni MVP: PDF/print-ready supplier pack sa 5 kljucnih KPI i CTA koracima.
- Prioritet: P1.

## 17) Markdown optimizer

- Poslovno pitanje: Koji markdown procenat daje najbolji balans izmedju clearance i marze?
- Formula: optimize markdown_pct to maximize objective = expected_margin_after_markdown - holding_cost - stockout_penalty.
- Potrebni podaci: price elasticity signal, historical markdown outcomes, inventory aging, margin floor policy.
- Da li podaci vec postoje u Trendplus DB: Delimicno/Nedostaje.
- Data quality rizici: slab elasticity signal, policy ogranicenja nisu modelovana, mali uzorci.
- Recommendation ili signal: Signal u P1, recommendation u P2.
- Gde se prikazuje: pricing/nivelacija decision panel.
- Minimalni MVP: simulator scenarija (5/10/15/20%) bez auto-primene.
- Prioritet: P2.

## 18) Replenishment/OOS decision flow

- Poslovno pitanje: Koja je sledeca akcija po SKU/store: dopuna, transfer, markdown ili hold?
- Formula: decision_score = w1*oos_risk + w2*days_of_supply_gap + w3*margin_priority - w4*data_quality_penalty.
- Potrebni podaci: stock cover, OOS risk, transfer opportunity, marza, quality status, lead-time.
- Da li podaci vec postoje u Trendplus DB: Delimicno (za rule-based MVP dovoljno; za optimizaciju treba bolji lead-time).
- Data quality rizici: stale inventory, nepotpuni lead-time, fallback period mismatch.
- Recommendation ili signal: Recommendation flow sa explicit gating pravilima.
- Gde se prikazuje: /analytics/inventory, /analytics/actions, supplier/product decision kontekst.
- Minimalni MVP: rule engine sa 4 izlaza (replenish, transfer, markdown, hold) + reason codes.
- Prioritet: P0.

## Decision Governance i rollout redosled

### P0 (odmah za planning i MVP implementaciju)

- 1 Sell-through ratio
- 2 Inventory turnover (units)
- 4 Margin loss zbog nivelacija
- 6 Stock cover / days of supply
- 8 Size/color availability risk
- 9 Supplier dependency risk
- 10 Category contribution margin
- 12 Dead stock aging
- 18 Replenishment/OOS decision flow

### P1 (zahteva stabilizaciju i data quality podizanje)

- 3 GMROI (lite)
- 5 Markdown efficiency
- 7 OOS lost sales estimate
- 11 Slow stock capital
- 15 Transfer opportunity izmedju prodavnica
- 16 Supplier negotiation pack

### P2 (zahteva nove podatke ili napredniji model)

- 13 Price elasticity signal
- 14 Return/refund impact
- 17 Markdown optimizer

## Predlog data enabler-a pre pune recommendation faze

- Stabilan periodicki inventory snapshot po store/SKU.
- Dosledna istorija promene cena na event nivou.
- Jaci cost coverage i validacija outlier-a.
- Standardizacija atributa velicine i boje.
- Pouzdano povezivanje refund transakcije sa originalnim sale line-om.
- Jasno modelovan lead-time po dobavljacu/store ruti.

## Guardrails

- No fake-zero: greska ili timeout nikad ne sme da izgleda kao validna nula.
- Svaki KPI mora imati data quality status i reason code kada je degradiran.
- Delimicno/Nedostaje podaci: prikazivati kao signal, ne kao final recommendation.
- Trust, refresh i quality signal moraju biti vidljivi pre svake akcione preporuke.

## Napomena

Ovaj dokument je roadmap i plan specifikacija. Ne uvodi runtime promene backend/frontend sloja.
