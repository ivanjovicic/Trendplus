# Forenzička analiza marži — Trendplus2

> **Datum:** jun 2025  
> **Obim:** Kompletna revizija pipeline-a za obračun marže — od Access import-a do frontend prikaza

---

## Sadržaj

1. [Izvršni rezime](#1-izvršni-rezime)
2. [Formula maržnog doprinosa](#2-formula-maržnog-doprinosa)
3. [Pipeline nabavne cene — 3 nivoa](#3-pipeline-nabavne-cene--3-nivoa)
4. [Bruto vs Neto marža](#4-bruto-vs-neto-marža)
5. [PDV / Porez](#5-pdv--porez)
6. [Nivelacija i uticaj na marže](#6-nivelacija-i-uticaj-na-marže)
7. [Identifikovani rizici](#7-identifikovani-rizici)
8. [Stanje terminologije na frontendu](#8-stanje-terminologije-na-frontendu)
9. [Preporuke](#9-preporuke)

---

## 1. Izvršni rezime

**Šta sistem prikazuje kao "maržni doprinos" je razlika između prodajne i nabavne cene — NE čist profit.**

Tri kritična nalaza:

| # | Nalaz | Ozbiljnost |
|---|-------|-----------|
| 1 | **Access import NE postavlja `NabavnaCena` na stavkama prodaje** — svi importovani računi imaju `NabavnaCena = null`, pa obračun pada na fallback (trenutna cena na artiklu) | 🔴 Kritično |
| 2 | **Nema snapshot mehanizma** — promena `NabavnaCena` na artiklu retroaktivno menja marže za SVE stare prodaje tog artikla | 🔴 Kritično |
| 3 | **Sistem je potpuno PDV-nesvestan** — nema polja, kolona, ni obračuna PDV-a bilo gde u kodu | 🟡 Srednje |

---

## 2. Formula maržnog doprinosa

**Centralna logika:** `Application/Analytics/AnalyticsMarginPolicy.cs`

### Akumulacija (MarginAccumulator)

```
Za svaku stavku prodaje:
  unitCost = ResolveUnitCost(saleLineCost, productCostRsd, productCostLegacy)
  
  ako unitCost > 0:
    TotalCost       += kolicina × unitCost
    RevenueWithCost += kolicina × prodajnaCena    ← samo promet gde je cena POZNATA
    QuantityWithCost += kolicina
  inače:
    QuantityWithoutCost += kolicina
```

### Snapshot (finalni KPI-evi)

```
MarginContribution   = RevenueWithCost − TotalCost
MarginPct            = MarginContribution / RevenueWithCost × 100
MarginDataCoveragePct = RevenueWithCost / UkupanPromet × 100
```

### Ključna distinkcija

| Pojam | Formula | Napomena |
|-------|---------|----------|
| **Maržni doprinos** | `Σ(Cena − NabavnaCena) × Kolicina` | Samo za stavke gde je NabavnaCena poznat |
| **Marža %** | `MaržniDoprinos / PrometSaPoznatimTroškom × 100` | Imenilac je promet sa poznatom NabavnomCenom, NE ukupan promet |
| **Pokrivenost podataka** | `PrometSaPoznatimTroškom / UkupanPromet × 100` | Koliki % prometa ima poznatu nabavnu cenu |

---

## 3. Pipeline nabavne cene — 3 nivoa

```
Nivo 1 (Istorijski):  ProdajaStavka.NabavnaCena   → MarginCostSource.Historical
               ↓ null?
Nivo 2 (Fallback):    Artikli.NabavnaCenaDin       → MarginCostSource.ProductFallbackRsd
               ↓ null?
Nivo 3 (Fallback):    Artikli.NabavnaCena          → MarginCostSource.ProductFallbackLegacy
               ↓ null?
             NabavnaCena = NEPOZNATA → stavka se isključuje iz obračuna marže
```

### Kako se NabavnaCena popunjava po putanji

| Putanja | ProdajaStavka.NabavnaCena | Artikli.NabavnaCenaDin | Artikli.NabavnaCena |
|---------|--------------------------|----------------------|-------------------|
| **POS prodaja** (OutboxProcessorWorker) | ✅ Popunjava sofisticiranom 3-tier logikom | Postoji ako je postavljeno ranije | Postoji ako je postavljeno ranije |
| **Access import** (AccessImportService) | ❌ **UVEK NULL** — import mapira samo `Cena` (prodajnu), ne i nabavnu | ✅ Importuje se iz Access-a za Artikli master | ✅ Importuje se iz Access-a za Artikli master |
| **Sync endpoint** (AllEndpoints.cs) | ❌ **NE POSTAVLJA** | Nepromenjen | Nepromenjen |

### Posledica

Za **sve importovane prodaje** iz Access-a (~većina istorijskih podataka), obračun marže **uvek** koristi Nivo 2/3 fallback — trenutnu `NabavnaCena` sa Artikli mastera. To znači:

- **Nema istorijskog snimka** nabavne cene u trenutku prodaje
- Ako se nabavna cena na artiklu promeni (novi ulaz robe, ručna izmena, re-import), **sve stare prodaje dobijaju novu maržu**
- MarginAccumulator prati pokrivenost (`HistoricalCostRevenue` vs `EstimatedCostRevenue`), ali ovo se ne prikazuje prominentno u UI-u

---

## 4. Bruto vs Neto marža

### Šta sistem računa

Sistem računa **bruto trgovačku maržu** (razliku između prodajne i nabavne cene). To je maržni doprinos — koliko "ostane" od prodaje pre svih operativnih troškova.

### Šta sistem NE uključuje

| Stavka | Status |
|--------|--------|
| Operativni troškovi (kirija, režije) | ❌ Nije u sistemu |
| Plate zaposlenih | ❌ Nije u sistemu |
| Prevozni troškovi | ❌ Nije u sistemu |
| Gubitak od kala/loma/krađe | ❌ Nije u sistemu |
| Otpisi | ❌ Nije u sistemu |
| PDV | ❌ Nije u sistemu (vidi sekciju 5) |
| Kursne razlike | ⚠️ Delimično — postoji NabavnaCena (EUR) i NabavnaCenaDin (RSD), ali nema dinamičkog preračuna po kursu na dan prodaje |

**Zaključak:** „Maržni doprinos" je tačan termin. To NIJE profit, NIJE neto zarada, NIJE realna zarada.

---

## 5. PDV / Porez

### Nalaz: Sistem je potpuno PDV-nesvestan

| Pretraga | Rezultat |
|----------|---------|
| `PDV`, `pdv`, `Pdv` | 1 match — stari Access VBA label u `tmp/access_exports/frmStatistika.txt`: `"Ukupno bez PDVa"` (samo prikaz) |
| `VAT`, `vat`, `porez`, `tax`, `taxRate` | 0 pravih poklapanja |
| PDV kolona u bazi | Ne postoji — ni u jednoj tabeli |
| PDV polje na modelu | Ne postoji — ni na Artikli, ni na ProdajaStavka, ni na ProdajaZaglavlje |

### Implikacije

1. **Sve cene su verovatno SA PDV-om** — u srpskom maloprodajnom poslovanju, cene na policama obavezno sadrže PDV
2. **NabavnaCena je verovatno BEZ PDV-a** — dobavljačke fakture su po pravilu bez PDV-a (ulazni PDV se koristi za odbitak)
3. **Maržni doprinos se računa na ceni sa PDV-om minus nabavna bez PDV-a** — ovo daje *veći* doprinos nego realni, jer PDV nije oduzet od prodajne cene

### Formula sa PDV korekcijom (za referentnost)

```
Prava bruto marža = (ProdajnaCena / 1.20) − NabavnaCena    ← za PDV 20%
Trenutno u sistemu = ProdajnaCena − NabavnaCena             ← veća od stvarne
```

> ⚠️ **Ovo je procena.** Bez eksplicitnog PDV polja u sistemu, ne možemo sa sigurnošću tvrditi da li su nabavne cene sa ili bez PDV-a. Potrebna je verifikacija sa korisnikom.

---

## 6. Nivelacija i uticaj na marže

### Šta je nivelacija u sistemu

Nivelacija (promena prodajne cene) se beleži u `DnevnikPromena` tabeli:

| Polje | Sadržaj |
|-------|---------|
| `TipPromene` | `"Nivelacija cena"` (web UI) ili `"Nivelacija"` (Access import) |
| `StaraProdajnaCena` | Stara prodajna cena |
| `NovaProdajnaCena` | Nova prodajna cena |
| `Iznos` | `|NovaCena − StaraCena| × Kolicina` — apsolutna vrednost finansijskog uticaja |

### Šta nivelacija menja

| Polje | Menja? |
|-------|--------|
| `Artikli.ProdajnaCena` | ✅ Da (samo web UI putanja, ne Access import) |
| `Artikli.NabavnaCena` | ❌ Ne — nikad |
| `Artikli.NabavnaCenaDin` | ❌ Ne — nikad |
| `ProdajaStavka.Cena` | ❌ Ne — prošle prodaje su nepromenjive |

### Uticaj na marže

**Nivelacija sama po sebi ne uzrokuje problem u maržama.** Ona:
- Menja samo buduću prodajnu cenu (ProdajnaCena na artiklu)
- Ne dira nabavnu cenu
- Ne menja istorijske stavke prodaje

**ALI** — `Artikli.PrvaProdajnaCena` ostaje nepromenjena (originalna cena iz Access-a), što omogućava analizu kumulativnog sniženja.

### Analitika vezana za nivelaciju

Sistem ima razvijenu analitiku nivelacije:
- `AnalyticsNivelacijaSplitPolicy` — deli prodaje na pre/posle nivelacije
- SQL views: `vw_sales_pre_nivelacija`, `vw_sales_post_nivelacija` — porede 30-dnevne prozore pre i posle
- InsightStudio prikazuje skorašnje nivelacije (poslednjih 7 dana)
- `PreNivelacijaPriorityEndpoints` — analiza sniženja i prosečnog % markdown-a

---

## 7. Identifikovani rizici

### 🔴 Kritični

| # | Rizik | Opis | Uticaj |
|---|-------|------|--------|
| R1 | **Retroaktivna promena marži** | Promena NabavnaCena na Artikli menja maržu za SVE stare prodaje tog artikla (za Access-importovane podatke koji nemaju snapshot) | Marže za prošle period nisu stabilne — mogu se promeniti pri svakom novom ulazu robe |
| R2 | **Access import → NabavnaCena = NULL** | Nijedna importovana stavka prodaje nema nabavnu cenu. SVE padaju na fallback. | 100% istorijskih prodaja koristi fallback = nestabilnu, nesnapšotovanu cenu |
| R3 | **Nema PDV obračuna** | Prodajna cena verovatno sadrži PDV, nabavna verovatno ne → maržni doprinos je naduvan | Prikazane marže su ~20% veće nego realne (ako je PDV 20%) |

### 🟡 Srednji

| # | Rizik | Opis |
|---|-------|------|
| R4 | **Kursne razlike ignorisane** | NabavnaCena (EUR) se pretvara u RSD statičkim kursom (verovatno u trenutku importa/unosa), ne kursom na dan prodaje |
| R5 | **Pokrivenost podataka slabo vidljiva** | MarginAccumulator računa pokrivenost, ali UI je ne prikazuje prominentno |
| R6 | **Nedosledna terminologija na nekim stranicama** | ColorSalesStatsPage i InsightStudioPage imaju zastarele/nedosledne labele |

### 🟢 Niski

| # | Rizik | Opis |
|---|-------|------|
| R7 | **Dijakritici nedosledni** | Neke stranice koriste `"marzni"` umesto `"maržni"`, `"Marza"` umesto `"Marža"` |
| R8 | **"Profit" labele u InsightStudio** | Bar label koristi "Profit" umesto "Profitabilnost" ili "Maržni doprinos" |

---

## 8. Stanje terminologije na frontendu

### ✅ Potpuno čiste stranice

| Stranica | Status |
|----------|--------|
| `SupplierSalesStatsPage.tsx` | Svi KPI-evi, tabele, grafikoni i tooltip-ovi koriste "Maržni doprinos" sa detaljnim formulama |
| `ShoeTypeSalesStatsPage.tsx` | Isto kao gore — potpuno konzistentno |
| `AnalyticsPrintPage.tsx` | Nema sopstvene terminologije — koristi podatke iz pozivajuće stranice |
| `DailySalesStatsPage.tsx` | Nema margin labela |
| `SupplierFootwearAnalyticsPage.tsx` | Nema margin KPI labela |

### ⚠️ Stranice sa problemima

#### ColorSalesStatsPage.tsx
- `"Ukupan marzni doprinos"` — nedostaje InfoTip tooltip + nedostaju dijakritici
- `"Marza %"` u detail sekciji — nema tooltip, nedostaju dijakritici
- Tooltip na tabeli je kraći nego na drugim stranicama

#### InsightStudioPage.tsx
- `"Profit"` kao bar label u grafikonu (L730) — treba `"Profitabilnost"`
- `"Očekivani profit"` (L1716) — treba `"Očekivani maržni doprinos"`
- `"Bruto marža"` umesto `"Marža %"` — drugi kontekst, ali nekonzistentno
- `"Marza %"` kolone bez dijakritika

#### RuntimeScoringPage.tsx
- `"Margina"` i `"Profitna margina"` umesto standardizovanih pojmova

### ✅ Uspešno uklonjene zastarele labele

| Stari termin | Novo stanje |
|-------------|-------------|
| `"Realna zarada"` | **0 instanci** — potpuno uklonjeno |
| `"zarada"` (kao KPI label) | **0 instanci** — potpuno uklonjeno |
| `"realnaZaradaPokomadu"` | **0 instanci** — potpuno uklonjeno |

---

## 9. Preporuke

### P1: Rešiti retroaktivnu promenu marži (Kritično)

**Problem:** Nabavna cena nije "zamrznuta" na stavci prodaje za Access-importovane podatke.

**Opcije:**
1. **Batch popunjavanje** — jednom pokrenuti skriptu koja za svaku `ProdajaStavka` gde je `NabavnaCena = null` postavi nabavnu cenu iz `Artikli.NabavnaCenaDin` / `Artikli.NabavnaCena`. Od tog trenutka se koristi Nivo 1, i buduće promene na artiklu ne utiču na stare prodaje.
   - Prednost: jednostavno, jednokratno
   - Mana: snapshot je sa *današnjom* cenom, ne sa cenom u trenutku prodaje
2. **Obeležiti importovane marže kao procenjene** — u UI-u jasno prikazati da su marže za importovane podatke procenjene (fallback), ne tačne

### P2: Razjasniti PDV semantiku (Srednje)

**Potrebno:** Verifikovati sa korisnikom:
- Da li su ProdajnaCena u Access-u cene sa PDV-om?
- Da li je NabavnaCena iz Access-a bez PDV-a?
- Ako da, dodati korekciju `ProdajnaCena / 1.20` pri obračunu marže

### P3: Ujednačiti terminologiju (Nisko)

- Popraviti ColorSalesStatsPage: dodati InfoTip tooltip-ove, popraviti dijakritike
- Popraviti InsightStudioPage: `"Profit"` → `"Profitabilnost"`, `"Očekivani profit"` → `"Očekivani maržni doprinos"`
- Popraviti RuntimeScoringPage: `"Margina"` → `"Marža %"`, `"Profitna margina"` → `"Maržni doprinos"`

### P4: Prikazati pokrivenost podataka (Nisko)

- `AnalyticsMarginPolicy` već računa `MarginDataCoveragePct` i `HistoricalMarginCoveragePct`
- Prikazati ovo u UI-u kao info badge ili tooltip pored KPI-eva marže
- Npr: "⚠️ 34% prometa ima poznatu nabavnu cenu" ili "Pokrivenost: 78% (62% istorijski, 16% procenjeno)"

---

## Dijagram pipeline-a

```
┌─────────────────────┐       ┌────────────────────┐
│   Access .mdb/.accdb │       │   POS Prodaja      │
│                     │       │                    │
│  Artikli:           │       │  ProdajaStavka:    │
│   NabavnaCena  ✅   │       │   Cena        ✅    │
│   NabavnaCenaDin ✅ │       │   NabavnaCena ✅    │
│   ProdajnaCena ✅   │       │   (3-tier worker)  │
│                     │       │                    │
│  ProdajaStavka:     │       └────────┬───────────┘
│   Cena         ✅   │               │
│   NabavnaCena  ❌   │               │
│   (UVEK NULL)       │               │
└────────┬────────────┘               │
         │                            │
         ▼                            ▼
┌──────────────────────────────────────────────┐
│              PostgreSQL                       │
│                                              │
│  prodaja_stavke:                              │
│    cena           ← prodajna cena            │
│    nabavna_cena   ← NULL (Access) ili        │
│                     popunjena (POS)           │
│                                              │
│  artikli:                                    │
│    nabavna_cena      ← EUR                   │
│    nabavna_cena_din  ← RSD                   │
│    prodajna_cena     ← trenutna              │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│         AnalyticsMarginPolicy                 │
│                                              │
│  Za svaku stavku:                            │
│    Nivo 1: stavka.NabavnaCena      ──┐       │
│    Nivo 2: artikal.NabavnaCenaDin  ──┤→ cena │
│    Nivo 3: artikal.NabavnaCena     ──┘       │
│    (null = bez marže)                        │
│                                              │
│  Akumulira:                                  │
│    RevenueWithCost, TotalCost, Coverage      │
└──────────────────┬───────────────────────────┘
                   │
                   ▼
┌──────────────────────────────────────────────┐
│              Frontend                         │
│                                              │
│  "Maržni doprinos" = Revenue − Cost          │
│  "Marža %" = Doprinos / PrometSaCenom × 100  │
│  "Pokrivenost" = PrometSaCenom / Ukupno      │
└──────────────────────────────────────────────┘
```

---

## Glosar

| Termin u sistemu | Englesko ime | Formula |
|-----------------|-------------|---------|
| Maržni doprinos | Margin Contribution | `Σ(Cena − NabavnaCena) × Kolicina` za stavke sa poznatom cenom |
| Marža % | Margin % | `MaržniDoprinos / PrometSaPoznatimTroškom × 100` |
| Promet sa poznatom nabavnom cenom | Revenue with known cost | Promet samo za stavke gde je NabavnaCena resolovana |
| Pokrivenost podataka o marži | Margin data coverage | `PrometSaPoznatimTroškom / UkupanPromet × 100` |
| Istorijska pokrivenost | Historical coverage | % prometa gde je korišćen Nivo 1 (snapshot sa stavke) |
| Procenjena pokrivenost | Estimated coverage | % prometa gde je korišćen Nivo 2/3 (fallback sa artikla) |
| Nivelacija | Price revaluation | Promena ProdajnaCena na artiklu, beleži se u DnevnikPromena |
| PrvaProdajnaCena | First selling price | Originalna prodajna cena iz Access-a, ne menja se nivelacijom |
