# Forenzička revizija marži — Finalni izveštaj

> **Datum:** april 2026  
> **Tip:** Truth-first forensic audit  
> **Obim:** Kompletna verifikacija dve prethodne analize + implementacioni plan  
> **Metod:** Svaki zaključak je potkrepljen dokazom iz koda. Pretpostavke su eksplicitno označene.

---

## 1. Izvršni rezime

### Šta sistem zaista računa

Sistem računa **bruto trgovačku maržu** — razliku između prodajne cene i nabavne cene za artikle gde je nabavna cena poznata. To se interno zove `MarginContribution`.

Formula: `MarginContribution = Σ(Cena × Kolicina) − Σ(NabavnaCena × Kolicina)` samo za stavke gde je `NabavnaCena > 0`.

**To NIJE neto marža, NIJE profit, NIJE realna zarada.** Sistem nema podatke o operativnim troškovima, PDV-u, platama, kiriji, niti bilo čemu drugom potrebnom za neto kalkulaciju.

### Šta je i dalje neizvesno

1. **PDV semantika** — nije dokazano da li su prodajne cene sa PDV-om a nabavne bez PDV-a. Verovatno jeste (srpski maloprodajni standard), ali nema eksplicitnog polja u sistemu.
2. **Kvalitet istorijskih troškova** — za Access-importovane prodaje, nabavna cena je UVEK fallback sa trenutnog stanja artikla, ne snapshot iz trenutka prodaje.

### Koja analiza je tačnija

**Analiza B je tehnički tačnija** — ispravno identifikuje ključne probleme (Access import NabavnaCena=null, retroaktivna promena marži, PDV-nesvesnost).

**Analiza A je semantički opreznija** — ispravno insistira da se metrika ne sme nazivati "profit" ili "neto marža".

Obe su kompatiblne — ne protivreče jedna drugoj u suštini, razlikuju se u tonu i detaljnosti.

### Šta treba prvo promeniti

1. Ukloniti poslednje instance "zarada"/"profit" iz korisničkog teksta (1 preostala u SupplierSalesStatsPage)
2. Popraviti dijakritike na ColorSalesStatsPage (11 mesta)
3. Razmotriti preimenovanje internih varijabli (`shareOfProfit` → `shareOfMargin`, `udelZarade` → `udelDoprinosa`) — ovo je P2, ne utiče na korisnika

---

## 2. Uporedna analiza A vs B

| Tema | Analiza A | Analiza B | Tip konflikta | Moj verdikt |
|------|-----------|-----------|--------------|-------------|
| **Neto marža** | Sistem NE računa neto maržu | Isti zaključak | Slaganje | **Potvrđeno** — nema operativnih troškova, PDV-a, plata |
| **Dominantna formula** | MC = revenue − cost; M% = MC / revenue_with_cost | Isti zaključak, detaljniji | Slaganje | **Potvrđeno** — `AnalyticsMarginPolicy.cs` L91-95 |
| **Access import NabavnaCena** | Pomenuto kao problem | Eksplicitno: UVEK NULL | A je manje specifična | **B je tačna** — 5 kreacionih tačaka, nijedna ne postavlja NabavnaCena |
| **Fallback trošak** | Koristi se trenutni product master | Isti zaključak | Slaganje | **Potvrđeno** — Tier 2/3 u `ResolveUnitCostWithSource` |
| **Istorijska stabilnost** | Nestabilna za fallback | Retroaktivna promena marži | Kompatibilno, B je preciznija | **B je preciznija** — promena Artikli.NabavnaCena menja SVE stare prodaje |
| **PDV** | Nejasan, nije eksplicitno modelovan | PDV-nesvestan sistem | Slaganje | **Potvrđeno** — zero PDV polja. Ali ni A ni B ne DOKAZUJU mismatch |
| **Nivelacija** | Utiče na cenu/promet, ne na maržu | Isti zaključak | Slaganje | **Potvrđeno** — DnevnikPromena ima samo StaraProdajnaCena/NovaProdajnaCena |
| **Supplier terminologija** | Delimično nekonzistentna | Uglavnom čista | B je optimističnija | **A je bliža** — postoji 1 preostala "zaradi" + interni `shareOfProfit` |
| **ShoeType terminologija** | Delimično nekonzistentna | Čista | B je tačna za user-visible | **B je tačna** — korisnički vidljiv tekst je čist |
| **shareOfProfit naming** | Semantički pogrešno | Interno, ne utiče na korisnika | B minimizuje | **A ima pravo** — treba preimenovati, ali je P2 |
| **Coverage prominence** | Nedovoljno prominentno | Isti zaključak | Slaganje | **Potvrđeno** — samo u detail panelu, ne u tabeli |
| **Weighted vs unweighted M%** | Ne pominje eksplicitno | Ne pominje eksplicitno | Obe propuštaju | **Novi nalaz** — `prosecnaMarza` je aritmetički prosek, NE ponderisan |

---

## 3. Tabela validacije tvrdnji

### A. Osnovna kalkulacija

| # | Tvrdnja | Status | Dokaz | Pouzdanost | Napomena |
|---|---------|--------|-------|-----------|---------|
| 1 | Sistem NE računa pravu neto maržu | **Potvrđeno** | Nema operativnih troškova, plata, kirije, PDV-a ni u jednom modelu. `MarginContribution = revenue − cost` je jedina formula. | Visoka | Nema nikakvog osnova za neto kalkulaciju |
| 2 | Sistem NE računa čist profit | **Potvrđeno** | Isto kao #1. Nijedan DTO ne sadrži "netProfit" ili "operatingExpense" | Visoka | |
| 3 | Formula: MC = revenue − cost; M% = MC / revenue_with_cost | **Potvrđeno** | `AnalyticsMarginPolicy.cs` L91-95: `marginContribution = _revenueWithCost - _totalCost; marginPct = marginContribution / _revenueWithCost * 100` | Visoka | |
| 4 | M% imenilac je revenue_with_cost, NE ukupan promet | **Potvrđeno** | Isto — `_revenueWithCost` je akumulator koji broji samo stavke sa `IsReliableCost(unitCost) == true` | Visoka | Ovo je semantički ispravno — ne razvodnjava maržu nepoznatim stavkama |

### B. Izvor troška / istorijski integritet

| # | Tvrdnja | Status | Dokaz | Pouzdanost | Napomena |
|---|---------|--------|-------|-----------|---------|
| 5 | `ProdajaStavka.NabavnaCena` je preferiran izvor | **Potvrđeno** | `ResolveUnitCostWithSource`: prvi se proverava `saleLineCost` (Tier 1 = Historical) | Visoka | |
| 6 | Access import NE popunjava `ProdajaStavka.NabavnaCena` | **Potvrđeno** | 5 kreacionih tačaka u AccessImportService.cs, nijedna ne postavlja NabavnaCena. Access .mdb nema tu kolonu na stavkama | Visoka | Ovo je root cause retroaktivnosti |
| 7 | POS/worker flow popunjava `ProdajaStavka.NabavnaCena` | **Delimično potvrđeno** | POS šalje `NabavnaCena = null`. Ali `ProdajaRepository.ProdajAsync` **sinhrono backfill-uje** iz trenutnog `Artikli.NabavnaCenaDin/NabavnaCena` pre nego što metoda vrati. Dakle — popunjava, ali sa TRENUTNOM cenom, ne istorijskom. | Visoka | POS snapshot = trenutna cena artikla u trenutku prodaje. Istorijski tačno samo ako se NabavnaCena ne menja |
| 8 | Analytics često padaju na fallback product master cost | **Potvrđeno** | Za Access-importovane prodaje (~većina istorijskih podataka), `ps.NabavnaCena = null`, pa se koristi `a.NabavnaCenaDin` / `a.NabavnaCena` iz Artikli join-a | Visoka | |
| 9 | Istorijska marža se može retroaktivno promeniti | **Potvrđeno za Access import; delimično za POS** | Access: da — promena Artikli menja maržu svih starih prodaja. POS: backfill zamrzava cenu na stavci, ali ta cena je iz trenutka prodaje, ne iz trenutka nabavke tog artikla | Visoka (Access), Srednja (POS) | Za POS, cena je "zamrznuta" u trenutku prodaje — ali ako je Artikli.NabavnaCena već bila stara/pogrešna u tom trenutku, snapshot je pogrešan |
| 10 | Nema pouzdanog istorijskog snapshot-a za importovane prodaje | **Potvrđeno** | Access ne prenosi NabavnaCena po stavci. Nema repair/backfill logike u AccessImportService | Visoka | |

### C. PDV / poreska osnova

| # | Tvrdnja | Status | Dokaz | Pouzdanost | Napomena |
|---|---------|--------|-------|-----------|---------|
| 11 | Sistem ne modeluje PDV eksplicitno | **Potvrđeno** | 0 PDV polja u modelima, tabelama, migracijama. Jedini match: `"Ukupno bez PDVa"` u starom Access VBA eksportu | Visoka | |
| 12 | Dokazano je da su prodajne cene SA PDV-om | **Nije potvrđeno** | Nema eksplicitnog polja `CenaSaPDV` ili `PDVUkljucen`. Srpski zakon nalaže PDV na policama, ali sistem to ne beleži | Niska | Verovatno DA, ali nije provaljivo iz koda |
| 13 | Dokazano je da su nabavne cene BEZ PDV-a | **Nije potvrđeno** | Isto — nema polja. Srpska praksa jeste bez PDV-a na fakturama, ali sistem ne razlikuje | Niska | Verovatno DA, ali nije provaljivo iz koda |
| 14 | Marža je zato naduvana zbog PDV mismatch-a | **Nije potvrđeno** | Zavisi od #12 i #13 koje NISU potvrđene. Ako su obe cene sa PDV-om, marža je tačna. Ako je samo prodajna sa PDV-om, marža je naduvana ~20% | Niska | MORA se verifikovati sa korisnikom |

### D. Nivelacija

| # | Tvrdnja | Status | Dokaz | Pouzdanost | Napomena |
|---|---------|--------|-------|-----------|---------|
| 15 | Nivelacija meri ponašanje cena/prometa, ne pravu maržu | **Potvrđeno** | `AnalyticsNivelacijaSplitPolicy` deli prodaje na pre/posle. SQL views porede 30-dnevne prozore. Nema marža-specifične nivelacione analize | Visoka | |
| 16 | Nivelacija menja prodajnu cenu, ne trošak | **Potvrđeno** | `DnevnikPromena` ima samo `StaraProdajnaCena/NovaProdajnaCena`. Nivelacioni endpoint (`/api/nivelacija`) ažurira `artikal.ProdajnaCena`, ne NabavnaCena | Visoka | |
| 17 | UI se može pogrešno protumačiti kao "prava marža posle nivelacije" | **Delimično potvrđeno** | Nivelacija split prikazuje `promet pre/posle` — ne prikazuje maržu pre/posle nivelacije. Korisnik MOŽE pogrešno protumačiti ako ne čita pažljivo, ali UI ne tvrdi eksplicitno da je to marža | Srednja | |

### E. Terminologija / stanje UI-a

| # | Tvrdnja | Status | Dokaz | Pouzdanost | Napomena |
|---|---------|--------|-------|-----------|---------|
| 18 | Supplier page i dalje sadrži misleading profit/earnings | **Delimično potvrđeno** | L1319 InfoTip: `"preporuka bazirana na prometu, zaradi, marži i trendu"` — jedina preostala user-visible "zarada". Interno: `shareOfProfit` ×14 | Visoka | User-visible: 1 instanca. Internal: 14 instanci |
| 19 | ShoeType page i dalje sadrži misleading profit/earnings | **Netačno** | 0 instanci "profit"/"zarada" u korisničkom tekstu. Samo `udelZarade` interni dataKey ×3 | Visoka | Potpuno čista korisnički vidljiva terminologija |
| 20 | Širi sistem i dalje sadrži misleading wording | **Potvrđeno** | InsightStudio: "Profit" bar label, "Očekivani profit". RuntimeScoring: "Margina", "Profitna margina". Color: sistematski nedostaju dijakritici | Visoka | |
| 21 | `shareOfProfit` naming je semantički pogrešno | **Potvrđeno** | Metrika računa udeo u `MarginContribution`, ne u profitu. Trebalo bi biti `shareOfMarginContribution` ili kraće `shareOfMargin` | Visoka | Ne utiče na korisnika jer je label "Udeo maržnog doprinosa %" |
| 22 | Tooltip-ovi NE objašnjavaju potpuno formulu | **Delimično potvrđeno** | Supplier i ShoeType: imaju detaljne tooltip-ove sa formulama. Color: NEMA InfoTip tooltip-ove na margin KPI kartama | Visoka | Supplier+ShoeType su uglavnom dobri; Color nijedan |
| 23 | Coverage nije dovoljno prominentan | **Potvrđeno** | Supplier i ShoeType: coverage u detail panelu samo. Color: `marginDataCoveragePct` u detalju, `fallbackCostCoveragePct` se NE prikazuje uopšte. Nigde u tabelama | Visoka | |

### F. Kvalitet agregacije

| # | Tvrdnja | Status | Dokaz | Pouzdanost | Napomena |
|---|---------|--------|-------|-----------|---------|
| 24 | Margin % je pravilno ponderisan u supplier analytics | **Delimično potvrđeno** | Per-entity `marginPct` je ispravan (MC/revenue_with_cost ×100). ALI `prosecnaMarza` je **aritmetički prosek** svih entity marginPct vrednosti — NE ponderisan prometom | Visoka | Dobavljač sa 100 RSD prometa ima isti uticaj kao dobavljač sa 1M RSD |
| 25 | Margin % je pravilno ponderisan u shoe-type analytics | **Delimično potvrđeno** | Isto kao #24. Per-entity ispravan. `prosecnaMarza` NE postoji u shoe-type totals — ali ShoeType tooltip eksplicitno kaže "aritmetički prosek" | Visoka | ShoeType barem upozorava korisnika |
| 26 | Neki proseci su aritmetički i mogu zavarati | **Potvrđeno** | `prosecnaMarza = suppliers.Where(!isUnknown).Select(marginPct).Average()` — čist LINQ `.Average()` | Visoka | Ovo je **novi nalaz** koji obe analize propuštaju |

---

## 4. Root cause i model istine

### 4.1 Šta sistem zapravo računa

Sistem računa **razliku između prodajne vrednosti i nabavne vrednosti** za prodatu robu gde je nabavna cena poznata. To se u računovodstvenoj terminologiji naziva **bruto trgovačka marža** ili **maržni doprinos** (margin contribution).

Ovo NIJE:
- **Neto marža** — nema operativnih troškova
- **Profit** — nema PDV-a, plata, kirije, amortizacije
- **Realna zarada** — sugeriše profit, što ovo nije

Ovo JESTE:
- **Maržni doprinos** — koliko roba "doprinosi" pokriću operativnih troškova i eventualno profita
- **Bruto trgovačka marža** — tačan računovodstveni termin za (prodajna − nabavna)

### 4.2 Najveći tehnički rizik: Nestabilnost istorijskog troška

```
Access-importovane prodaje (istorijski podaci):
  ProdajaStavka.NabavnaCena = NULL (uvek)
  → Analytics endpoint: JOIN Artikli → koristi TRENUTNU NabavnaCenaDin/NabavnaCena
  → Promena na Artikli = promena marže za SVE stare prodaje tog artikla
  
POS prodaje (nove prodaje):
  ProdajaStavka.NabavnaCena = backfill iz TRENUTNOG Artikli stanja (zamrznuto u trenutku prodaje)
  → Stabilno za budućnost, ali snapshot odražava cenu u trenutku prodaje, ne u trenutku nabavke
```

**Kvantifikacija rizika:** Ako dobavljač promeni cene (što je uobičajeno), SVE importovane prodaje tog artikla dobijaju novu maržu. Za POS prodaje, marža ostaje zamrznuta na vrednosti iz trenutka prodaje.

### 4.3 Najveći poslovni/semantički rizik

Korisnik može protumačiti "Maržni doprinos" kao profit. Postojeći tooltip-ovi na Supplier i ShoeType stranicama eksplicitno objašnjavaju da to NIJE profit i šta NIJE uključeno — ali:
- ColorSalesStatsPage NEMA tooltip-ove
- InsightStudioPage koristi reč "Profit" eksplicitno
- `prosecnaMarza` KPI je aritmetički prosek (ne ponderisan), što može zavarati

### 4.4 Da li su istorijske marže pouzdane?

| Izvor podataka | Pouzdanost marže | Objašnjenje |
|----------------|-----------------|-------------|
| POS prodaje (skorašnje) | ⚠️ Srednja | NabavnaCena zamrznuta u trenutku prodaje, ali je snapshot trenutne cene artikla, ne cene iz fakture nabavke |
| Access import (istorijske) | ❌ Nepouzdano | NabavnaCena = null, koristi se TRENUTNA cena na artiklu — menja se retroaktivno |
| Artikli sa stabilnom nabavnom cenom | ✅ Visoka | Ako se NabavnaCena ne menja, fallback daje ispravan rezultat |

### 4.5 PDV — otvoreno pitanje ili potvrđen problem?

**Otvoreno pitanje.** Analiza B tvrdi da je marža "naduvana ~20%", ali to se oslanja na pretpostavku koja NIJE dokazana iz koda:
- Pretpostavka: ProdajnaCena je SA PDV-om, NabavnaCena je BEZ PDV-a
- Realnost: Moguće je da su OBE cene u istoj PDV bazi (npr. obe sa PDV-om iz Access-a, ili obe bez)
- **Bez verifikacije sa korisnikom/poslovnim ekspertom, ovo ostaje nepotvrđeno**

### 4.6 Da li su Supplier i ShoeType formula-konzistentni?

**Da.** Oba endpointa koriste identičnu putanju:
1. Isti JOIN: `ProdajaStavke` → `Artikli`
2. Isti 3 troškovna polja: `ps.NabavnaCena`, `a.NabavnaCenaDin`, `a.NabavnaCena`
3. Isti `MarginAccumulator.Add()` poziv
4. Isti `MarginSnapshot.Build()` sa `totalRevenue` kao denominatorom za coverage

Jedina razlika: grupišu po `IDDobavljac` vs `IDTipObuce`.

---

## 5. Bezbednost naziva metrika

| Metrika / kandidat za label | Bezbedno za upotrebu? | Zašto | Gde koristiti | Gde NE koristiti |
|----------------------------|----------------------|-------|--------------|------------------|
| **Razlika u ceni** | ⚠️ Tehnički tačno ali previše simplifikovano | Sugeriše per-item kalkulaciju, ne agregirani KPI | Moguć tooltip za objašnjenje | Ne kao naziv KPI-a |
| **Maržni doprinos** | ✅ **Da — primarni termin** | Računovodstveno tačan: doprinos marže pokriću troškova | KPI kartice, tabele, tooltipovi, grafikoni | — |
| **Marža %** | ✅ **Da** | Procenat maržnog doprinosa u prometu sa poznatom nabavnom cenom | KPI kartice, tabele | — |
| **Bruto marža** | ⚠️ Prihvatljivo ali neprecizno | Tehnički ispravno, ali "bruto" implicira da postoji i "neto" | Može u tooltip objašnjenju | Ne kao primarni label — sugerira da postoji neto pendant |
| **Neto marža** | ❌ **NE** | Sistem NEMA operativne troškove, PDV, ni druge troškove potrebne za neto kalkulaciju | NIGDE | Svugde — zabraniti |
| **Profit** | ❌ **NE** | Profit zahteva potpun P&L. Sistem ima samo MC | NIGDE | Svugde — zabraniti |
| **Realna zarada** | ❌ **NE** | "Realna" sugeriše pouzdanost koja ne postoji. "Zarada" sugeriše profit | NIGDE | Svugde — zabraniti |
| **Udeo u profitu** | ❌ **NE** | Metrika je udeo u maržnom doprinosu, ne u profitu | NIGDE | Svugde — zabraniti |
| **Udeo u maržnom doprinosu** | ✅ **Da** | Tačno opisuje šta se računa: % maržnog doprinosa koji taj entitet čini | Tabele, detail paneli | — |
| **Pokrivenost podataka** | ✅ **Da** | Koliki % prometa ima poznatu nabavnu cenu | Info badge, tooltip, detail | — |
| **Procenjeno / Fallback** | ✅ **Da — kao kvalifikator** | Jasno komunicira nesigurnost fallback troška | Uz maržne KPI-eve | — |

---

## 6. Šta se mora dalje istražiti

| # | Pitanje | Zašto je nerazrešeno | Potrebna akcija | Prioritet |
|---|---------|---------------------|-----------------|----------|
| 1 | **Da li su cene u Access-u sa ili bez PDV-a?** | Sistem ne beleži PDV status. Ni A ni B ne mogu ovo dokazati iz koda | Pitati korisnika/vlasnika biznisa. Proveriti realne fakture | P0 pre bilo kakve PDV korekcije |
| 2 | **Koliki % ukupne prodaje je Access-importovan vs POS-originalan?** | Određuje severity retroaktivnog rizika. Ako je 90% Access = kritično. Ako je 10% = niže | `SELECT COUNT(*) FROM prodaja_stavke WHERE nabavna_cena IS NULL` vs `IS NOT NULL` | P1 |
| 3 | **Da li se Artikli.NabavnaCena zaista menja u praksi?** | Ako se nikad ne menja, retroaktivni rizik je teorijski | Proveriti DnevnikPromena za promene nabavne cene ili audit log | P1 |
| 4 | **prosecnaMarza: da li korisnik razume da je aritmetički prosek?** | ShoeType tooltip upozorava, Supplier ne. Misleading? | Dodati tooltip na Supplier; razmotriti ponderisani prosek | P2 |
| 5 | **Postoji li stranica ili izveštaj koji treba da prikaže pravi neto profit?** | Ako da, potreban potpuno novi modul. Ako ne, trenutni pristup je dovoljan | Razgovor sa stakeholderima | P2 |

---

## 7. Finalni plan za Supplier analytics (`Prodaja po dobavljačima`)

### 7.1 Metrike za prikaz

| Metrika | Već postoji? | Status | Akcija |
|---------|-------------|--------|--------|
| Ukupan promet | ✅ | Ispravan | Bez promene |
| Ukupna količina | ✅ | Ispravan | Bez promene |
| Maržni doprinos | ✅ | Ispravan label + tooltip | Bez promene |
| Marža % | ✅ | Ispravan per-entity | Bez promene per-entity |
| Prosečna marža | ✅ | **Aritmetički prosek — misleading** | Dodati tooltip upozorenje (vidi 7.4) |
| Udeo prometa % | ✅ | Ispravan | Bez promene |
| Udeo maržnog doprinosa % | ✅ | Ispravan label | Bez promene labela |
| Pokrivenost podataka | ⚠️ Samo u detail panelu | Nedovoljno vidljivo | Razmotriti info badge na KPI kartici (vidi 7.5) |

### 7.2 Metrike koje se NE SMEJU prikazivati

| Zabranjena metrika | Razlog |
|-------------------|--------|
| Neto marža | Nema operativne troškove |
| Profit / Zarada | Sugeriše P&L koji ne postoji |
| Realna zarada | Potpuno misleading |
| Neto profitabilnost | Isti razlog kao neto marža |

### 7.3 Promene terminologije

| Gde | Stari tekst | Novi tekst | Prioritet |
|-----|------------|-----------|----------|
| L1319 InfoTip | `"preporuka bazirana na prometu, zaradi, marži i trendu."` | `"preporuka bazirana na prometu, maržnom doprinosu, maržnom procentu i trendu."` | **P0** |
| L1339 varijabla | `profitVsRevenueMismatch` | `marginVsRevenueMismatch` | P2 (interno) |
| L1342 varijabla | `highProfitLowRevenue` | `highMarginLowRevenue` | P2 (interno) |
| L39, L54, L81 itd. | `shareOfProfit` (×14) | `shareOfMargin` | P2 (interno) |
| L719, L727, L1235 | `udelZarade` (chart dataKey) | `udelDoprinosa` | P2 (interno) |

**P0 = jedina korisnički vidljiva promjena.** Sve P2 su interne i ne utiču na korisnika.

### 7.4 Zahtevi za tooltip-ove

| Tooltip | Trenutno stanje | Zahtevana izmena |
|---------|----------------|-----------------|
| Maržni doprinos KPI | ✅ Postoji, detaljan | Bez promene |
| Marža % KPI | ✅ Postoji, sa formulom | Bez promene |
| Prosečna marža KPI | ⚠️ Postoji ali ne kaže da je aritmetički prosek | **Dodati:** `"Ovo je aritmetički prosek marže po dobavljačima — nije ponderisan prometom. Dobavljači sa malim prometom utiču jednako kao oni sa velikim."` |
| Udeo doprinosa kolona | ✅ Postoji | Bez promene |
| Preporuka kolona | ⚠️ Koristi "zaradi" | **Popraviti** (vidi 7.3) |

### 7.5 Coverage / prikaz pouzdanosti

**Trenutno stanje:** Coverage se prikazuje samo u detail panelu kad se klikne na dobavljača.

**Preporuka (P1):** Dodati diskretan info badge na KPI kartici "Ukupan maržni doprinos" koji pokazuje procenat prometa sa poznatom nabavnom cenom:
```
Ukupan maržni doprinos: 1,234,567 RSD
[ℹ️ Pokriva 67% prometa]
```

**Preporuka (P2):** Dodati kolonu u tabeli "Pokrivenost %" (skrivena po default-u, vidljiva na zahtev).

### 7.6 Preporuka (recommendation) logika — formulacija

**Trenutno stanje:** Recommendation engine koristi `MarginPct`, `MarginCoveragePct` kao inpute. Output statusima su: `increase_focus`, `maintain`, `review`, `do_not_trust`, `insufficient_data`.

**Zahtev:** Tekst preporuka NE SME koristiti:  
- "profit", "profitabilnost", "zarada", "zarađuje"

**Dozvoljeno:**
- "maržni doprinos", "marža", "doprinos", "pokriće troškova"

**Specifična popravka:** L1319 `"zaradi"` → `"maržnom doprinosu"` (jedina preostala instanca).

### 7.7 Backend promene

**Nema potrebnih backend promena za Supplier analytics.** Formula je ispravna. DTO struktura je adekvatna. Coverage metrike se već šalju u API odgovoru.

Opciono (P2): preimenovati DTO field `shareOfProfit` → `shareOfMargin` u `AllEndpoints.cs`. Ovo je breaking change za frontend koji zahteva sinhronizovanu promenu.

### 7.8 Frontend promene

| Fajl | Promjena | Prioritet |
|------|---------|----------|
| `SupplierSalesStatsPage.tsx` L1319 | "zaradi" → "maržnom doprinosu" | **P0** |
| `SupplierSalesStatsPage.tsx` L1180 | Dodati tooltip objašnjenje da je prosek aritmetički | **P1** |
| `SupplierSalesStatsPage.tsx` L1339,1342 | Preimenovati interne varijable (profit→margin) | P2 |
| `SupplierSalesStatsPage.tsx` L39,54,81... | Preimenovati `shareOfProfit`→`shareOfMargin` (uz backend) | P2 |
| `SupplierSalesStatsPage.tsx` L719,727,1235 | Preimenovati `udelZarade`→`udelDoprinosa` | P2 |

---

## 8. Finalni plan za Shoe-type analytics (`Prodaja po tipu obuće`)

### 8.1 Metrike za prikaz

Identične metrikama za Supplier analytics (sekcija 7.1). Isti izvor podataka, ista formula, samogrupisanje po `IDTipObuce` umesto `IDDobavljac`.

**Napomena:** ShoeType totals NE sadrži `prosecnaMarza` polje u DTO odgovoru (za razliku od Supplier). Tooltip na stranici eksplicitno kaže da je prosek aritmetički — ovo je ispravno komunikacija.

### 8.2 Metrike koje se NE SMEJU prikazivati

Identično Supplier-u (sekcija 7.2).

### 8.3 Promene terminologije

| Gde | Stari tekst | Novi tekst | Prioritet |
|-----|------------|-----------|----------|
| L613, L620, L1005 | `udelZarade` (chart dataKey) | `udelDoprinosa` | P2 (interno) |
| L954 | `"prosečk"` ×2 (typo) | `"prosek"` | **P0** |
| L1287 | `"Napomena za marža:"` | `"Napomena za maržu:"` | **P0** |
| L1266-1267 | `"pokrice"` | `"pokriće"` | **P1** |

### 8.4 Zahtevi za tooltip-ove

**Trenutno stanje:** ShoeType ima detaljne tooltip-ove sa formulama na svim KPI karticama. `prosecnaMarza` tooltip eksplicitno upozorava na aritmetički prosek.

**Zahtev: Bez promena** — tooltip-ovi su adekvatni.

### 8.5 Coverage / prikaz pouzdanosti

Isto kao Supplier (sekcija 7.5). Coverage u detail panelu, preporučiti info badge na KPI kartici.

### 8.6 Preporuka logika — formulacija

**Trenutno stanje:** ShoeType recommendation tekst NE sadrži "profit", "zarada", niti slične misleading termine. Potpuno čist.

**Zahtev: Bez promena.**

### 8.7 Backend promene

**Nema potrebnih backend promena za ShoeType analytics.** Ista situacija kao Supplier.

### 8.8 Frontend promene

| Fajl | Promjena | Prioritet |
|------|---------|----------|
| `ShoeTypeSalesStatsPage.tsx` L954 | Popraviti typo `"prosečk"` → `"prosek"` ×2 | **P0** |
| `ShoeTypeSalesStatsPage.tsx` L1287 | `"Napomena za marža:"` → `"Napomena za maržu:"` | **P0** |
| `ShoeTypeSalesStatsPage.tsx` L1266-1267 | `"pokrice"` → `"pokriće"` | **P1** |
| `ShoeTypeSalesStatsPage.tsx` L613,620,1005 | Preimenovati `udelZarade`→`udelDoprinosa` | P2 |

---

## 9. Cross-system cleanup plan

| # | Komponenta | Problem | Ispravka | Prioritet |
|---|-----------|---------|---------|----------|
| 1 | **ColorSalesStatsPage.tsx** | 11 mesta sa nedostajućim dijakritikama (`marzni`→`maržni`, `marza`→`marža`, `pokrice`→`pokriće`, `delimicno`→`delimično`, `zadrzati`→`zadržati`, `pojacan`→`pojačan`) | Popraviti sve dijakritike | **P0** |
| 2 | **ColorSalesStatsPage.tsx** | `"Ukupan marzni doprinos"` KPI — nema InfoTip tooltip | Dodati InfoTip sa formulom (kopirati iz Supplier/ShoeType) | **P1** |
| 3 | **ColorSalesStatsPage.tsx** | Detail sekcija: `"Marza %"` nema InfoTip | Dodati InfoTip sa formulom | **P1** |
| 4 | **ColorSalesStatsPage.tsx** | `fallbackCostCoveragePct` se NE prikazuje | Dodati u detail panel | **P1** |
| 5 | **ColorSalesStatsPage.tsx** | Nema `Marža %` kolone u tabeli | Dodati kolonu (ako postoji u DTO) | P2 |
| 6 | **InsightStudioPage.tsx** L730 | Chart bar label `"Profit"` | → `"Profitabilnost"` | **P1** |
| 7 | **InsightStudioPage.tsx** L1716 | `"Očekivani profit"` | → `"Očekivani maržni doprinos"` | **P1** |
| 8 | **InsightStudioPage.tsx** L182,193,202,221 | `"Marza %"`, `"Avg marza %"` — nedostaju dijakritici | → `"Marža %"`, `"Prosečna marža %"` | **P1** |
| 9 | **InsightStudioPage.tsx** L553 | `"Bruto marža"` kao KPI label | Razmotriti → `"Marža %"` za konzistentnost. Ali "Bruto marža" je tehnički tačno — odluka je stilska, ne semantička | P2 |
| 10 | **RuntimeScoringPage.tsx** L44 | `"Margina"` / `"Profitna margina na osnovu unetih cena"` | → `"Marža %"` / `"Maržni doprinos na osnovu unetih cena"` | P2 |
| 11 | **DTO/Internal naming** | `shareOfProfit` (Supplier), `udelZarade` (Supplier+ShoeType), `profitVsRevenueMismatch`, `highProfitLowRevenue` | Preimenovati u margin-bazirane nazive | P2 |

---

## 10. Kriterijumi prihvatanja

### Terminološka ispravnost
- [ ] Nijedna korisnički vidljiva metrika ne koristi reči "profit", "zarada", "realna zarada", "neto marža"
- [ ] Svi korisnički vidljivi labeli koriste ispravne dijakritike (ž, č, ć, đ, š)
- [ ] "Maržni doprinos" se koristi umesto svih prethodnih varijanti za MC
- [ ] "Marža %" umesto "Bruto marža %" na Supplier i ShoeType stranicama

### Konzistentnost formula
- [ ] Per-entity `marginPct` = `marginContribution / revenueWithCost × 100` na svim stranicama
- [ ] `prosecnaMarza` ima tooltip koji objašnjava da je aritmetički prosek (gde postoji)
- [ ] Ista formula se koristi na Supplier, ShoeType i Color stranicama

### Iskrenost tooltip-ova
- [ ] Svaki margin KPI na svakoj analytics stranici ima InfoTip koji objašnjava:
  - Formulu
  - Šta JESTE uključeno
  - Šta NIJE uključeno (operativni troškovi, PDV, plate)
  - Da su nabavne cene delimično procenjene (fallback)
- [ ] Nema tvrdnji o "profitu" ili "zaradi" u tooltip-ovima

### Vidljivost pokrivenosti
- [ ] Supplier i ShoeType prikazuju coverage u detail panelu (već ispunjeno)
- [ ] Razmotrena (ne obavezno implementirana) opcija info badge-a na KPI kartici

### Konzistentnost Supplier ↔ ShoeType
- [ ] Iste metrike (MC, M%, coverage) se prikazuju na oba ekrana
- [ ] Isti tooltip standardi na oba ekrana
- [ ] Isti format prikaza

### Široka konzistentnost
- [ ] ColorSalesStatsPage ima InfoTip tooltip-ove na margin karticama
- [ ] InsightStudioPage ne koristi "Profit" kao chart label
- [ ] Nijedna stranica u sistemu ne koristi "Realna zarada"

---

## 11. Finalna preporuka

### Spremno za implementaciju ODMAH

1. **P0 popravke** (minimalan rizik, maksimalan semantički benefit):
   - SupplierSalesStatsPage L1319: `"zaradi"` → `"maržnom doprinosu"` (1 red)
   - ShoeTypeSalesStatsPage L954: typo `"prosečk"` → `"prosek"` ×2
   - ShoeTypeSalesStatsPage L1287: `"Napomena za marža:"` → `"Napomena za maržu:"`
   - ColorSalesStatsPage: 11 dijakritičkih popravki

2. **P1 popravke** (srednji napor, značajan benefit):
   - ColorSalesStatsPage: dodati InfoTip tooltip-ove na margin KPI
   - InsightStudioPage: `"Profit"` → `"Profitabilnost"`, `"Očekivani profit"` → `"Očekivani maržni doprinos"`
   - ShoeTypeSalesStatsPage: `"pokrice"` → `"pokriće"`

### Mora čekati dalju verifikaciju

1. **PDV korekcija** — NE implementirati dok korisnik ne potvrdi da li su cene sa/bez PDV-a
2. **Batch backfill NabavnaCena** — NE pokretati dok se ne utvrdi obim problema (query: koliki % stavki ima null NabavnaCena)
3. **Ponderisani prosek marže** — razmotriti, ali zahteva analizu uticaja na korisničko razumevanje

### Šta uraditi prvo u kodu

```
1. Popraviti L1319 "zaradi" u SupplierSalesStatsPage.tsx        ← 30 sekundi
2. Popraviti 2 typo-a + 1 grammar u ShoeTypeSalesStatsPage.tsx  ← 1 minut
3. Popraviti 11 dijakritika u ColorSalesStatsPage.tsx            ← 5 minuta
4. Dodati InfoTip na Color margin KPI                            ← 10 minuta
5. Popraviti InsightStudio "Profit" labele                       ← 5 minuta
```

Sve gore navedeno je **zero-risk** — ne menja formule, ne menja DTO-ove, ne menja backend. Čisto terminološko čišćenje.

**Interno preimenovanje (`shareOfProfit`→`shareOfMargin`, `udelZarade`→`udelDoprinosa`) ostaviti za P2** — ne utiče na korisnika, zahteva sinhronizovanu frontend+backend promenu, i nosi rizik regresije.
