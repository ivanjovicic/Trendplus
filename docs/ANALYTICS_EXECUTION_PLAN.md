# Trendplus Analytics — Master Execution Plan

> **Verzija:** 1.0 — Maj 2026  
> **Autor:** Staff-level product/engineering review  
> **Osnova:** Forensički audit analitike + verifikacija koda (AllEndpoints.cs, DailySalesStatsEndpoints.cs, InventoryEndpoints.cs, svih 9 frontend pages)  
> **Cilj:** Konsolidacija analytics sistema iz "više dobrih ekrana" u koherentan decision-support proizvod

---

## 1. Executive Summary

### Najvažniji zaključak

Trendplus analytics ima **solidnu tehničku osnovu** — server-side `AnalyticsDecisionRecommendationEngine`, `AnalyticsMarginPolicy` sa cost fallback chain-om, `AnalyticsNivelacijaSplitPolicy`, hibridni cache (L1+L2) i prewarm — ali ta osnova **nije konzistentno primenjena**. Rezultat je sistem koji daje ispravne odgovore na kanoničnom surface-u (SupplierSalesStats), ali paralelne odgovore — drugačije izračunate — na 4–5 drugih ekrana koji pretenduju da rade isti posao.

### Šta je glavni problem

**Semantički drift i fragmentacija supplier dimenzije.** Konkretno:

1. Tri odvojena supplier ekrana (`SupplierSalesStats`, `SupplierDecisionHub`, `SupplierFootwearAnalytics`) daju *različite* preporuke za istog dobavljača, izračunate *različitim metodologijama*, od kojih su dve implementirane **lokalno u frontendu**.
2. `decisionScore` ima **pet različitih formula** u pet fajlova sa **dva različita set praga** (68/43 i 70/45) — ista metrika, različita semantika.
3. `fmtRsd`, `fmtPct`, `getPresetRange` su kopirani u 8+ strana umesto da postoje kao shared utility.
4. `SupplierDecisionHub` i `PreNivelacijaPriority` nemaju application-level caching, dok sve ostale ključne rute imaju.

### Šta je sledeći pravi korak

**Ne novi ekrani. Konsolidacija i semantička disciplina.**

Specificno: (1) uvesti shared formatters i canonical metric definitions, (2) prebaciti sve frontend decision logike na backend, (3) konsolidovati tri supplier ekrana u jedan tabbed surface, (4) dodati caching na preostalim rutama.

---

## 2. Canonical Architecture Proposal

### 2.1 Supplier Decision Domain

#### Presuda: Koji ekran je canonical

| Ekran | Status | Odluka |
|-------|--------|--------|
| **SupplierSalesStats** | **CANONICAL** | Jedini koji koristi `AnalyticsDecisionRecommendationEngine` server-side; ima prewarm; prikazuje `statusReason` u UI; pokriva revenue, margin, PoP, split signal, cost quality. Ovo je autoritativni supplier decision surface. |
| **SupplierDecisionHub** | **SECONDARY → merge as tab** | Donosi vrednost kroz `SupplierQualityIndex`, `MarkdownDependencyScore`, `FullPriceSellthrough`, `dead_stock_rate` iz analytics DB — ovo je **komplementarno** RecommendationEngine-u, ne zamena. Treba spojiti kao "Scorecard" tab unutar canonical surface-a. Frontend `decisionScore` formula mora biti uklonjena. |
| **SupplierFootwearAnalytics** | **SUPPORTING → fold as drilldown tab** | Supplier × tip obuće cross-section je vredan kao detail view, ali ne kao zasebna decision surface. Treba postati "Asortiman" tab ili drilldown u canonical surface-u. Lokalna formula mora biti uklonjena pre merge-a. |

**Zakljucak za supplier domain:** Jedan tabbed URL. Tri taba: `Pregled` (Stats), `Scorecard` (Hub), `Asortiman` (Footwear breakdown). Jedna preporuka, jedna semantika.

---

### 2.2 Inventory Decision Domain

#### Presuda: Canonical inventory surface

**`Inventory` ekran je canonical za inventory decision flow.**

Inventory ekran već ima:
- Server-side aging buckets (`ResolveAging(daysSinceMovement)`: 0–30 / 31–60 / 61–90 / 90+)
- Server-side gap formula: `max(minimum - quantity, 0)`
- Parametrizovane SQL upite sa properly bound parametrima

**Šta mu nedostaje da bude potpuni decision-support alat:**
- Demand signal iz `analytics_intel.mv_demand_signals` (postoji u InsightStudio, nije surfaovan ovde)
- `dead_stock_risk` oznaka (postoji u analytics_intel MVs, nije spojena u inventory view)
- Replenishment urgency composite (aging + gap + demand signal)

**Šta ostaje u inventory ekranu:** sve gore. **Šta ide u secondary drilldown:** per-SKU forensic (prodaja po periodu, margin history) — ako/kada se doda.

---

### 2.3 Canonical Analytics Surface Model — Ciljna mapa proizvoda

```
┌─────────────────────────────────────────────────────────────────────┐
│  L0 — OVERVIEW / DASHBOARD                                          │
│  Daily Sales  (operativni monitoring, 2-min cache)                  │
│  → "Šta se prodaje danas / ovog perioda"                            │
└─────────────────────────────────────────────────────────────────────┘
        ↓
┌─────────────────────────────────────────────────────────────────────┐
│  L1 — DECISION-SUPPORT (canonical surfaces, prewarm + cache)        │
│                                                                     │
│  Supplier (tabbed):                                                 │
│    Tab 1: Pregled (SupplierSalesStats — RecommendationEngine)        │
│    Tab 2: Scorecard (SupplierDecisionHub — QualityIndex)             │
│    Tab 3: Asortiman (SupplierFootwearAnalytics)                      │
│                                                                     │
│  Inventory (canonical):                                             │
│    Aging / Gap / Demand signal / Dead stock risk                    │
│                                                                     │
│  Pre-Nivelacija Prioriteti (canonical za taj workflow):             │
│    SKU ranking pre markdown eventa                                   │
└─────────────────────────────────────────────────────────────────────┘
        ↓
┌─────────────────────────────────────────────────────────────────────┐
│  L2 — SUPPORTING / ANALYSIS SURFACES                                │
│                                                                     │
│  Color Sales Stats  (prodajni mix po boji — detaljna analiza)       │
│  Shoe Type Sales Stats (prodajni mix po tipu obuće)                 │
│  Pre/Post Nivelacija (seasonalni impact analysis)                   │
└─────────────────────────────────────────────────────────────────────┘
        ↓
┌─────────────────────────────────────────────────────────────────────┐
│  L3 — FORENSIC / POWER-USER                                         │
│                                                                     │
│  InsightStudio (demand-signals, inventory-risk, price-intelligence, │
│    trend-momentum iz analytics_intel MVs — advanced users)          │
│  Daily Sales drilldown (shift breakdown, supplier concentration)    │
└─────────────────────────────────────────────────────────────────────┘
        ↓
┌─────────────────────────────────────────────────────────────────────┐
│  EXPORT / PRINT  (standardizovan za sve L1–L2 površine)             │
│  AnalyticsTableToolbar.tsx + AnalyticsPrintPage.tsx                 │
│  → PDF / Excel / CSV via generateExport API                         │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. Canonical Metric Dictionary

> Ovo je autoritativna definicija metrika za ceo Trendplus analytics sistem.  
> Svaki ekran, tooltip, export i backend service mora biti usklađen sa ovim.

| # | Canonical naziv | Source of truth | Gde se računa | Gde se prikazuje | Obavezni caveat | Trenutni drift |
|---|----------------|-----------------|---------------|------------------|-----------------|---------------|
| 1 | **Promet (RSD)** | `SUM(Kolicina * Cena)` iz `prodaja_stavke JOIN prodaja_zaglavlje` | Backend SQL aggregation | Svi ekrani; formatirati kao `X.XXX RSD` | Uključuje sve linije prodaje; ne neto od povrata | `fmtRsd()` kopiran u 8+ page fajlova umesto shared utility |
| 2 | **Količina (kom)** | `SUM(Kolicina)` iz `prodaja_stavke` | Backend SQL aggregation | Svi ekrani; formatirati kao integer + "kom" | Broji prodajne linije, ne unikatne artikle | Nema značajnog drifta |
| 3 | **Maržni doprinos (RSD)** | `AnalyticsMarginPolicy`: saleLineCost → snapshotCost → productCostRsd → productCostLegacy | Backend via `AnalyticsMarginPolicy` | Uz tier badge (`marginQuality.ts`); formatirati kao `X.XXX RSD` | **Mora** da prati tier badge (confirmed/partial/estimated/no_data) | `marginQuality.ts` je shared, ali `fmtRsd` se injectuje po strani |
| 4 | **Marža %** | `(promet - trošak) / promet * 100`; trošak via `AnalyticsMarginPolicy` | Backend | Uz quality tier caveat; formatirati kao `XX.X%` | Nikad prikazati bez tier indikatora; "estimated" tier → jasno upozorenje | Tooltip tekst varira po ekranu; neki ekrani izostavljaju caveat |
| 5 | **PoP promena %** | `(current_revenue - prev_revenue) / prev_revenue * 100` | Backend | `+XX.X%` ili `-XX.X%` sa strelicom; null-safe | Validan samo kad oba perioda imaju podatke; prikazati "nema pored. perioda" kad je null | ShoeTypeSalesStats koristi `popNorm` u lokalnoj formuli drugačije nego SupplierSalesStats |
| 6 | **Pouzdanost %** | `AnalyticsDecisionRecommendationEngine.reliabilityPct` | Backend (RecommendationEngine) | `XX%`; tooltip: objašnjava komponente (cost coverage, split coverage, period coverage) | Nije statistički confidence interval; mera pokrivenosti podataka | PreNivelacijaPriority mapira string→broj: "high"→90, "medium"→65, "low"→35 (drugačiji input skup); SupplierFootwearAnalytics računa lokalno kao `marginCoveragePct*0.45 + splitCoveragePct*0.20 + ...` |
| 7 | **Sigurnost preporuke %** | `AnalyticsDecisionRecommendationEngine.confidencePct` | Backend (RecommendationEngine) | `XX%`; tooltip: composite od data completeness faktora | Različit od `Pouzdanost %`; ne prikazivati kao jedini signal | ShoeTypeSalesStatsPage koristi `confidencePct` kao ceo `decisionScore` kad backend dostupan — pogrešna semantika |
| 8 | **Preporuka** | `AnalyticsDecisionRecommendationEngine.Evaluate()` → `status`, `statusReason`, `reliabilityPct`, `confidencePct`, `reasonCodes[]` | Backend (canonical) | Badge: `increase_focus / maintain / review / do_not_trust / insufficient_data` + `statusReason` tooltip | `insufficient_data` blokira decision; `do_not_trust` prominentno prikazati | SupplierFootwearAnalytics: lokalni "Pojacaj/Zadrzi/Smanji"; SupplierDecisionHub: paralelni "EXPAND/ASSORTMENT_REDUCE/PRICE_NEGOTIATE" iz analytics DB — tri različita status seta |
| 9 | **Skor odluke** *(decisionScore)* | **DEPRECATED kao korisnički vidljiva metrika** | N/A | Ne prikazivati kao primarni signal; eventualno u debug/transparency mode | — | 5 različitih formula u 5 fajlova; pragovi 68/43 u 4 strane i 70/45 u ShoeTypeSalesStats |
| 10 | **Manjak (kom)** | `max(minimum - quantity, 0)` u `InventoryEndpoints.cs` | Backend | Integer; "0 = bez manjka"; samo kad je `minimum > 0` | Bez definisanog minimuma prikazuje 0 — ne znači da nema potrebe za dopunom | Nema drifta; backend-only |
| 11 | **Starost zalihe** | `ResolveAging(daysSinceMovement)` → bucket: 0–30 / 31–60 / 61–90 / 90+ | Backend (`InventoryEndpoints.cs`) | Bucket label sa color coding | Bazira se na poslednjem pokretu; nula-pokret može biti novo stanje ili mrtva zaliha | Nema drifta |
| 12 | **Pre/Post nivelacija uticaj %** | `AnalyticsNivelacijaSplitPolicy` → comparable article cohort | Backend | `+XX.X%` / `-XX.X%` + `splitCoveragePct %` uvek uz vrednost | Pokriva samo komparabilne artikle; nizak `splitCoveragePct` → interpretirati oprezno | `splitCoveragePct` nije uvek prikazan uz vrednost na svim ekranima |
| 13 | **Pokrivenost troška** | Tier breakdown: `historicalPct + estimatedPct + noCostPct = 100%` | Backend (ratio), Frontend (`marginQuality.ts`) | Badge/tier ikonica + tooltip via `marginQuality.ts` | partial/estimated/no_data tieri uvek vizuelno kaventiraju margin figure | `marginQuality.ts` je shared — ovo je ispravno. Ali `fmtPct` se injectuje kao parametar po strani, dopuštajući formating varijacije |
| 14 | **Indeks kvaliteta dobavljača** | `analytics_intel.mv_supplier_decision_score_cache` (materijalizovani pogled) | Analytics DB (MV refresh) | Uz `MarkdownDependencyScore`, `FullPriceSellthrough`, `dead_stock_rate` | Bazira se na analytics DB refresh ciklusu; može kasniti za glavnom DB | Samo vidljiv u SupplierDecisionHub; nije integrisan u canonical supplier surface |

### Drift elimination plan (po metrici)

| Metrika | Drift | Akcija |
|---------|-------|--------|
| fmtRsd / fmtPct | Dupliciran u 8+ strana | Task T1: Uvesti `analyticsFormatters.ts` i importovati svuda |
| BOOST/KEEP threshold | 68/43 u 4 strane, 70/45 u ShoeType | Task T1: Canonical vrednosti u `analyticsConstants.ts`; ShoeType alinirati na 68/43 |
| Preporuka (status semantics) | 3 različita status seta | Tasks T2–T4: Sve lokalne formule → backend; sve surface-e prikazuju isti status set |
| Tooltip tekst | Varira po ekranu | Task T5: `analyticsMetricDescriptions.ts` sa kanonskim tooltip objektima |
| Pouzdanost % (računanje) | 3 različite metode | Tasks T2–T4: Sve lokalne `reliabilityPct` kalkulacije → RecommendationEngine |
| decisionScore kao metrika | 5 formula, deprecated | Tasks T2–T4 + T9: Ukloniti lokalne compute; "Skor odluke" nije user-visible metric |

---

## 4. Server-Side Authority Plan

### 4.1 Šta MORA biti 100% server-side

| Signal / logika | Gde danas živi | Gde mora biti | Prioritet |
|-----------------|----------------|---------------|-----------|
| Supplier decision recommendation | ✅ Backend (`RecommendationEngine`) na SupplierSalesStats | Proširiti na sve supplier surface-e | P0 |
| Supplier footwear breakdown scoring | ❌ Frontend (`SupplierFootwearAnalyticsPage.tsx` line 305) | Backend (novi endpoint ili proširenje existing) | P0 |
| Pre-Nivelacija Priority decision score | ❌ Frontend (`PreNivelacijaPriorityPage.tsx` line 285) | Backend (`IPreNivelacijaScoringService`) | P0 |
| Pre/Post Nivelacija decision score | ❌ Frontend (`ProdajaPrePostNivelacijePage.tsx` line 608) | Backend | P1 |
| BOOST/KEEP thresholds | ❌ Duplicirani konstantni u 5 frontend fajlova | Backend (ili bar shared utility, ne po strani) | P1 |
| Pouzdanost % za footwear breakdown | ❌ Frontend lokalni composite | Backend (RecommendationEngine) | P0 (uz T2) |
| ColorSalesStats fallback scoring | ❌ Frontend fallback (`ColorSalesStatsPage.tsx` line 449) | Ukloniti fallback; backend mora uvek da vrati recommendation ili `insufficient_data` | P1 |
| ShoeTypeSalesStats fallback scoring | ⚠️ Hybrid — pokušava backend, pada na lokalni | Ukloniti lokalni fallback; standardizovati na backend status | P1 |

### 4.2 Šta mora IZAĆI iz frontenda

```
ZABRANJENO u frontendu:
- Bilo koja formula tipa: scoreNorm * weight1 + norm2 * weight2 + ...
- Lokalne BOOST_SCORE_THRESHOLD / KEEP_SCORE_THRESHOLD konstante
- Lokalne reliabilityPct kalkulacije
- Duplikati fmtRsd / fmtPct / getPresetRange
- "Fallback" decision logika (kad backend vrati null → lokalni compute)
  (dopustivo: kad backend vrati null → prikazati "insufficient_data", ne računati)
```

```
DOZVOLJENO u frontendu:
- Formatiranje (fmtRsd, fmtPct) — ali iz shared utility
- Sortiranje i filtriranje tabela na osnovu backend-returned polja
- Prikaz backend preporuke, statusReason, tier badge-a
- Lokalna UX logika (expand/collapse, paginacija, export trigger)
```

### 4.3 Redosled prebacivanja

1. **Odmah (Wave 1):** Shared formatters + constants → nema backend promene, samo refactor
2. **Wave 2, iteracija 1:** SupplierFootwearAnalytics → backend (P0, direktno utiče na canonical supplier surface)
3. **Wave 2, iteracija 1:** PreNivelacijaPriority → backend (P0, high-stakes workflow)
4. **Wave 2, iteracija 2:** ProdajaPrePostNivelacija → backend (P1, secondary surface)
5. **Wave 2, iteracija 2:** ColorSalesStats + ShoeTypeSalesStats fallback uklanjanje (P1, clean-up)
6. **Wave 2, iteracija 3:** Konsolidacija supplier surface-a u tabbed view (P2, zahteva T2 kao prerequisite)

### 4.4 Minimalni rizik strategija

- Svaki backend migration zadatak: **novi endpoint** (ne modifikacija existing) → frontend prebaci API poziv → stari endpoint ostaje u freezing period od jednog sprint-a → briše se
- Lokalna formula u frontendu ostaje **samo kao feature flag / debug mode** tokom tranzicije — NIKAD kao fallback koji korisnik ne vidi
- Acceptance test pre svakog merge-a: isti dobavljač/artikal → isti status na starom i novom ekranu (ili dokumentovano objašnjenje zašto se razlikuju)

---

## 5. Execution Roadmap

---

### TALAS 1 — Stabilizacija i semantička disciplina

**Trajanje:** 1–2 sprinta  
**Cilj:** Eliminisati duplikaciju, alinirati semantiku, ne lomiti ništa

#### Šta ulazi:
- **T1:** Shared `analyticsFormatters.ts` + `analyticsConstants.ts` (fmtRsd, fmtPct, fmtQty, getPresetRange, BOOST/KEEP thresholds)
- **T5:** Shared `analyticsMetricDescriptions.ts` (canonical tooltip tekst za Marža %, PoP, Pouzdanost, Coverage, Pre/Post Impact)
- **T9:** Ukloniti ColorSalesStats frontend fallback scoring
- Alinirati ShoeTypeSalesStats threshold na 68/43 (umesto 70/45)
- `statusReason` expose na svim ekranima koji već imaju backend recommendation (ShoeTypeSalesStats, ColorSalesStats)
- Reviditati DailySalesStats upozorenja da odgovaraju revidiranim forensic zaključcima

#### Poslovni dobitak:
- Terminolška konzistentnost: isti postotak prikazan na isti način na svim ekranima
- Korisnik više ne dobija različite "preporuke" na dve verzije istog ekrana zbog fallback-a
- Osnova za Wave 2 (nemoguće konsolidovati supplier surface dok god formatteri nisu shared)

#### Tehnički obim:
- Samo frontend + utils refactor
- Nula backend promena
- Nula novih API endpointa

#### Rizik: **NIZAK**
- Samo refactor; logika se ne menja, samo se pomera u shared module
- Svaki ekran ostaje funkcionalan za vreme tranzicije

#### Dependency:
- Nema (ulazni talas; ovo je prerequisite za sve ostalo)

#### Šta mora biti gotovo pre Talasa 2:
- Svi page fajlovi importuju `fmtRsd`, `fmtPct`, `getPresetRange` iz shared utility-a
- `BOOST_SCORE_THRESHOLD` i `KEEP_SCORE_THRESHOLD` ne postoje ni u jednom page fajlu
- ShoeTypeSalesStats threshold aliniran
- ColorSalesStats ne sadrži lokalni score fallback
- `analyticsMetricDescriptions.ts` postoji i importuje se na 4+ surface-a

---

### TALAS 2 — Konsolidacija i canonical surfaces

**Trajanje:** 3–4 sprinta  
**Cilj:** Prebaciti svu decision logiku na backend; konsolidovati supplier surface; uvesti caching na preostalim rutama

#### Iteracija 2.1 (Sprint 3–4):
- **T2:** SupplierFootwearAnalytics lokalni decisionScore → backend
- **T3:** PreNivelacijaPriority lokalni decisionScore → backend
- **T6:** Cache layer za SupplierDecisionHub endpoint
- **T7:** Cache layer za PreNivelacijaPriority endpoint

#### Iteracija 2.2 (Sprint 5–6):
- **T4:** ProdajaPrePostNivelacija lokalni decisionScore → backend
- ShoeTypeSalesStats: ukloniti lokalni fallback (posle T2, jer isti pattern)
- SupplierDecisionHub: frontend decisionScore formula ukloniti (posle T6)

#### Iteracija 2.3 (Sprint 7):
- **T8:** Konsolidacija supplier surface-a u tabbed view (zahteva T2 + T6 + T7 gotove)
- Shared filter state model između tabova (isti period, isti store filter za sve tabove)

#### Poslovni dobitak:
- Jedna preporuka za jednog dobavljača, svuda ista, iz jednog engine-a
- Pre-nivelacija workflow radi bez cold-path spike-ova
- Korisnik vidi kompletnu supplier sliku u jednom URL-u bez navigacije između 3 ekrana

#### Tehnički obim:
- Backend: 2–3 nova/proširena endpoint-a
- Frontend: uklanjanje ~600 linija lokalne compute logike; dodavanje tabbed routing
- Infrastructure: 2 nova cache konfiguracije

#### Rizik: **SREDNJI**
- Backend migracioni zadaci zahtevaju pažljivo testiranje (isti input → isti output)
- Tabbed supplier surface menja UX → potrebna validacija sa korisnicima pre finalnog release-a
- SupplierDecisionHub koristi analytics_intel MVs → dependency na analytics DB availability

#### Dependencies:
- Talas 1 mora biti kompletan (shared utils, bez fallback-a u Color/ShoeType)
- Analytics DB i analytics_intel MVs moraju biti dostupni i refreshovani

#### Šta mora biti gotovo pre Talasa 3:
- Ni jedna frontend strana ne sadrži lokalni decision formula
- Supplier tabbed surface je live
- SupplierDecisionHub i PreNivelacijaPriority imaju application-level cache
- Svi canonical surfaces imaju konzistentnu preporuku iz jednog engine-a

---

### TALAS 3 — Performanse i Intelligence sloj

**Trajanje:** 2–3 sprinta  
**Cilj:** Precompute/snapshot pattern na burst rutama; intelligence signale uvući u operativne tokove

#### Šta ulazi:
- **T10:** Surfaovati analytics_intel demand signals u Inventory ekranu
- Proširiti prewarm service da uključi PreNivelacijaPriority (sezonski burst pattern → prewarm pre sezone)
- Daily-sales cache TTL review + opciono prewarm za najčešće date range presets
- AllEndpoints.cs modularizacija (izdvojiti supplier/shoetype/color endpoint grupe u odvojene fajlove — maintainability, ne funkcionalna promena)
- Dead stock risk signal iz analytics_intel → inventory ekran (uz demand signal iz T10)
- InsightStudio: bolje pozicioniranje u nav (power-user flow, ne buried)

#### Poslovni dobitak:
- Inventory postaje action-oriented (vidi aging + gap + demand signal → jasna odluka o dopuni)
- Pre-nivelacija period ne uzrokuje server spike
- InsightStudio demand/price intelligence postaje dostupno iz inventory workflow-a, ne samo iz zasebnog menija

#### Tehnički obim:
- Backend: cross-DB join ili secondary query (analytics_intel → inventory response)
- Infrastructure: prewarm proširenje; opciono background job za pre-nivelacija snapshot
- Frontend: Inventory ekran + 2 nova kolone/signala; InsightStudio nav update

#### Rizik: **SREDNJI**
- Cross-DB upit (main DB + analytics_intel) → latency risk; mora biti lazy/async ili cached
- Analytics_intel MV refresh schedule mora biti definisan i monitored

#### Dependencies:
- Talas 2 mora biti kompletan
- Analytics_intel MVs moraju biti popunjeni i refreshed na stabilan schedule

---

## 6. Top 10 Implementation Tasks

---

### T1 — Uvesti shared `analyticsFormatters.ts` i `analyticsConstants.ts`

**Problem koji rešava:**  
`fmtRsd`, `fmtPct`, `fmtQty`, `getPresetRange` su kopirani u 8+ page fajlova. `BOOST_SCORE_THRESHOLD = 68` i `KEEP_SCORE_THRESHOLD = 43` duplirani su u 4 fajla, a `ShoeTypeSalesStatsPage` koristi 70/45 — isti koncepti, različite vrednosti.

**Scope:** Frontend only (`Klijent/clientapp/src/utils/`)

**Težina:** S

**Prioritet:** P1

**Rizik:** Nizak

**Dependency:** Nema

**Acceptance criteria:**
- `src/utils/analyticsFormatters.ts` exportuje `fmtRsd(value)`, `fmtPct(value, digits?)`, `fmtQty(value)`, `fmtSignedPct(value, digits?)`, `getPresetRange(preset)`
- `src/utils/analyticsConstants.ts` exportuje `BOOST_SCORE_THRESHOLD = 68`, `KEEP_SCORE_THRESHOLD = 43`
- Ni jedan od 8+ page fajlova ne definiše lokalno ove funkcije ili konstante
- `ShoeTypeSalesStatsPage.tsx` koristi threshold-e iz shared konstante
- `grep -r "function fmtRsd" src/pages/` vraća 0 rezultata

---

### T2 — Prebaciti SupplierFootwearAnalytics decisionScore na backend

**Problem koji rešava:**  
`SupplierFootwearAnalyticsPage.tsx` linija 305 računa:  
`Math.round(shareNorm * 0.35 + deltaNorm * 0.30 + trendNorm * 0.20 + reliabilityPct * 0.15)`  
Ova formula nije testirana, nije auditabilna, i daje različitu preporuku od canonical SupplierSalesStats za istog dobavljača na istom periodu.

**Scope:**  
- Backend: novi endpoint `/analytics/supplier-footwear-breakdown` (ili proširenje `/analytics/supplier-sales-stats` da uključuje per-footwear-type breakdown sa recommendation per row)
- Backend: `AnalyticsDecisionRecommendationEngine.Evaluate()` per footwear type
- Frontend: `SupplierFootwearAnalyticsPage.tsx` uklanja lokalni compute; prikazuje `item.recommendation.status` + `statusReason`

**Težina:** M

**Prioritet:** P0

**Rizik:** Srednji

**Dependency:** T1 (shared formatters), T6 (cache za Hub, radi konzistentnosti pattern-a)

**Acceptance criteria:**
- Backend vraća `recommendation` objekat per footwear type row (isti struktura kao u SupplierSalesStats)
- `SupplierFootwearAnalyticsPage.tsx` ne sadrži keyword `decisionScore =` ni `shareNorm *`
- Za istog dobavljača i isti period: SupplierSalesStats i SupplierFootwearAnalytics prikazuju kompatibilne preporuke (ne nužno identične jer je granularnost različita, ali isti engine)
- `statusReason` prikazan u tooltip-u u frontend-u

---

### T3 — Prebaciti PreNivelacijaPriority decisionScore na backend

**Problem koji rešava:**  
`PreNivelacijaPriorityPage.tsx` linija 285 računa lokalni decision score za high-stakes workflow (šta puniti pre markdown eventa). Ovo je isti problem kao T2, ali u kontekstu gde je greška skuplja (nabavna odluka).

**Scope:**  
- Backend: proširiti `IPreNivelacijaScoringService` / endpoint da vraća `recommendation` per SKU  
- Backend: primena `AnalyticsDecisionRecommendationEngine` ili dedicated scoring service  
- Frontend: ukloniti lokalni compute; prikazivati backend decision

**Težina:** M

**Prioritet:** P0

**Rizik:** Srednji

**Dependency:** T1, T7 (caching pre-nivelacija endpoint-a mora biti gotov)

**Acceptance criteria:**
- `PreNivelacijaPriorityPage.tsx` ne sadrži lokalni score formula
- Backend endpoint vraća `decisionStatus`, `decisionScore`, `statusReason` per SKU
- `reliabilityFromConfidence()` string→number mapping (`"high"→90`) ne postoji u frontend kodu
- Paginacija i sorting rade na backend-returned `decisionScore` polju

---

### T4 — Prebaciti ProdajaPrePostNivelacija decisionScore na backend

**Problem koji rešava:**  
`ProdajaPrePostNivelacijePage.tsx` linija 608 računa lokalni decision score koristeći `BOOST_SCORE_THRESHOLD = 68`. Ista semantička nekonzistentnost kao T2/T3, ali za secondary surface.

**Scope:**  
- Backend: endpoint za pre/post nivelacija treba da vrati `recommendation` per article/supplier  
- Frontend: ukloniti lokalni compute

**Težina:** M

**Prioritet:** P1

**Rizik:** Nizak (secondary surface, manja business criticality od T2/T3)

**Dependency:** T1, T2 (isti pattern, može se paralelizovati sa T3)

**Acceptance criteria:**
- `ProdajaPrePostNivelacijePage.tsx` ne sadrži lokalni decisionScore formula
- Backend vraća decision status per row
- Prikaz je vizuelno konzistentan sa T2 i T3 rešenjima

---

### T5 — Canonical tooltip tekst za ključne metrike

**Problem koji rešava:**  
"Marža %" opisana je različitim rečima na SupplierSalesStats, ShoeTypeSalesStats i ColorSalesStats. "PoP trend" tooltip varira. "Pouzdanost %" nema konzistentno objašnjenje. Korisnik ne može da zna da li "marža" znači isto na dva ekrana.

**Scope:**  
- Frontend: `src/utils/analyticsMetricDescriptions.ts` sa canonical tooltip objektima  
- Metrics koje moraju biti pokrivene: `Marža %`, `PoP promena %`, `Pouzdanost %`, `Sigurnost preporuke %`, `Pre/Post nivelacija uticaj %`, `Pokrivenost troška`, `Preporuka` status objašnjenja

**Težina:** S

**Prioritet:** P1

**Rizik:** Nizak

**Dependency:** T1

**Acceptance criteria:**
- `analyticsMetricDescriptions.ts` postoji sa exportovanim objektima po metrici
- Minimum 4 surface-a importuju iste tooltip stringove (umesto lokalnih)
- Tekst za "Marža %" uključuje caveat o quality tier-u i nije identičan na svakoj strani slučajno

---

### T6 — Dodati HybridCache na SupplierDecisionHub endpoint

**Problem koji rešava:**  
`SupplierDecisionHubEndpoints.cs` nema application-level cache. Svaki request ide direktno na analytics DB. Pri višekorisničkom pristupu (2–5 simultanih korisnika na istom periodu) uzrokuje redundantne teške SQL upite na analytics DB.

**Scope:**  
- Backend: inject `IHybridCacheService` u `SupplierDecisionHubEndpoints`  
- Cache key: `supplier-decision-hub:{storeId}:{fromDate}:{toDate}`  
- TTL: 15 minuta (isti red veličine kao supplier-sales-stats)

**Težina:** S

**Prioritet:** P1

**Rizik:** Nizak

**Dependency:** Nema (HybridCacheService već postoji u Infrastructure)

**Acceptance criteria:**
- Drugi identičan request u 15 minuta vraća cached response (< 50ms)
- Cache miss: request ide na analytics DB (normalni flow)
- Cache key je specifičan po store i date range (nema false cache hit-ova)
- `IAnalyticsCacheService` cache keys su definisani u centralizovanoj listi (ne inline stringovi)

---

### T7 — Dodati HybridCache na PreNivelacijaPriority endpoint

**Problem koji rešava:**  
Pre-nivelacija priority endpoint nema caching. Koristi 4+ SQL upite i in-memory scoring. Pattern korišćenja je burst (sezonski): 3–6 korisnika istovremeno pre markdown eventa. Bez caching-a, svaki user request pokreće kompletan DB round-trip.

**Scope:**  
- Backend: inject `IHybridCacheService` u `PreNivelacijaPriorityEndpoints`  
- TTL: 30 minuta (pre-nivelacija data je per-season, ne per-minute)  
- Opciono: dodati u `AnalyticsCachePrewarmHostedService` pre-season prewarm

**Težina:** S

**Prioritet:** P1

**Rizik:** Nizak

**Dependency:** Nema

**Acceptance criteria:**
- Burst test (5 simultanih identičnih request-ova): samo prvi pogodi DB; ostali dobijaju cached response
- Cache TTL = 30 minuta ili do explicitnog invalidate
- Prewarm opcija dokumentovana (čak i ako nije odmah implementirana)

---

### T8 — Konsolidacija supplier surface-a u tabbed canonical view

**Problem koji rešava:**  
Korisnik koji želi kompletnu supplier sliku mora da navigira između 3 zasebna URL-a (`/analytics/supplier-sales-stats`, `/analytics/supplier-decision-hub`, `/analytics/supplier-footwear-analytics`), ručno da menja periode, i da mentalno reconciluje tri potencijalno konfliktne preporuke za istog dobavljača.

**Scope:**  
- Frontend: tabbed layout na `/analytics/supplier` (ili `/analytics/supplier-sales-stats` postaje host)  
- Tab 1 "Pregled": postojeći SupplierSalesStats sadržaj (canonical, nepromenjen)  
- Tab 2 "Scorecard": postojeći SupplierDecisionHub sadržaj (nakon T6)  
- Tab 3 "Asortiman": SupplierFootwearAnalytics sadržaj (nakon T2)  
- Shared state: period filter, store filter (promena u jednom tabu menja sve tabove)  
- Routing: stari URL-ovi rade redirect na novi tabbed URL

**Težina:** L

**Prioritet:** P2

**Rizik:** Srednji (UX promena; zahteva user validation)

**Dependency:** T2 (footwear scoring na backend), T6 (Hub cache), T7 (pre-niv cache za pattern učenja)

**Acceptance criteria:**
- Jedan URL za supplier decision surface
- Period i store filter su shared između tabova
- Tab promjena ne resetuje filter state
- Stari URL-ovi redirectuju na novi (bez broken bookmarks)
- Preporuke u svim tabovima dolaze iz backend engine-a (ni jedna lokalna formula)

---

### T9 — Ukloniti ColorSalesStats frontend fallback scoring

**Problem koji rešava:**  
`ColorSalesStatsPage.tsx` linija 416 pokušava `mapRecommendationStatus(item.recommendation?.status)` (backend), a linija 449 pada na lokalni score ako backend recommendation nije prisutan. Korisnik ne zna koji path je pogođen. Dve verzije iste stranice daju drugačije rezultate.

**Scope:**  
- Frontend only: `ColorSalesStatsPage.tsx`  
- Ako `item.recommendation` je null → prikazati `insufficient_data` badge, ne računati  
- Ukloniti lokalni score formula (linija 449 i okolina)

**Težina:** S

**Prioritet:** P1

**Rizik:** Nizak

**Dependency:** T1 (shared constants, formatters)

**Acceptance criteria:**
- `ColorSalesStatsPage.tsx` ne sadrži lokalni score formula
- Kad backend recommendation nije dostupan: prikazuje se "Nedovoljno podataka" badge
- Nema vizuelne regresije za boje gde backend recommendation postoji

---

### T10 — Surfaovati demand signal iz analytics_intel u Inventory ekranu

**Problem koji rešava:**  
Inventory ekran prikazuje stanje (aging, gap, quantity) ali ne prikazuje demand signal. Korisnik koji vidi aging=90+ i gap=5 ne zna da li je to urgentno (visoka potražnja) ili zanemarljivo (nema prodaje). Mora da ide u InsightStudio posebno.

**Scope:**  
- Backend: proširiti `InventoryEndpoints.cs` da uključuje demand signal per SKU iz `analytics_intel.mv_demand_signals` (secondary query ili LEFT JOIN)  
- Frontend: `Inventory` ekran + "Tražnja" kolona (rising/stable/declining/insufficient)  
- Tooltip: objašnjenje signala

**Težina:** M

**Prioritet:** P2

**Rizik:** Srednji (cross-DB upit ili secondary query; analytics_intel mora biti refreshovan)

**Dependency:** Talas 2 mora biti kompletan; analytics_intel MVs moraju biti na stabilnom refresh schedule-u

**Acceptance criteria:**
- Inventory tabela prikazuje demand signal per SKU
- Signal ima 4 stanja: rising / stable / declining / insufficient
- Tooltip objašnjava osnov signala i datum poslednjeg refresh-a MVa
- Latency za inventory endpoint ne raste za > 200ms (signal mora biti cached ili async)

---

## 7. Keep / Merge / Deprecate Matrix

| Analytics surface | Odluka | Razlog | Uslov / napomena |
|-------------------|--------|--------|-----------------|
| **Supplier Sales Stats** | ✅ **Keep as CANONICAL** | Jedini server-side RecommendationEngine; prewarm; statusReason u UI; najpotpuniji metrik set za supplier domain | Postaje host za tabbed supplier view (T8); bez promene logike |
| **Supplier Decision Hub** | 🔀 **Merge → Tab 2 u canonical supplier** | SupplierQualityIndex + MarkdownDependencyScore + FullPriceSellthrough su komplementarni signali; vredna perspektiva ali ne zasebna decision surface | Uslov: T6 (cache), frontend decisionScore formula uklonjena, tab dostupan iz canonical URL-a |
| **Supplier Footwear Analytics** | 🔀 **Fold → Tab 3 / Drilldown u canonical supplier** | Supplier × tip obuće cross-section je vredan kao detail; nije zasebna decision surface | Uslov: T2 (lokalni scoring → backend pre merge-a); fold se dešava u Talasu 2 |
| **Insight Studio** | 🔵 **Keep as secondary (power-user)** | demand-signals, inventory-risk, price-intelligence, trend-momentum su jedinstven analitički sloj koji nije dupliran nigde drugde; Nije u main nav — dobro za power usere | Long-term: demand signal prebaciti u Inventory (T10); InsightStudio ostaje za dublje istraživanje |
| **Color Sales Stats** | 🔵 **Keep as supporting** | Prodajni mix po boji je vredna dimenzija; nije decision surface, nego analysis detail | Ukloniti local fallback scoring (T9); standardizovati backend recommendation display |
| **Daily Sales** | 🔵 **Keep as L0 overview (operational)** | Jedini operativni monitoring ekran; 2-min cache; dobro radi svoju ulogu | Ne proširivati u decision surface; prikazuje šta se prodaje, ne šta treba raditi |
| **Inventory** | ✅ **Keep as CANONICAL (inventory domain)** | Server-side aging, gap, parametrizovani upiti; jasna decision-support funkcija | Proširiti sa demand signal (T10); ne spajati sa supplier surface-om |
| **Pre/Post Nivelacija** | 🔵 **Keep as supporting (seasonal analysis)** | Komparabilna analiza pre/posle markdown eventa; vredna ali sezonska; nije stalno korišćena | Ukloniti lokalni scoring (T4); može postati tab u Pre-Nivelacija Prioriteti ako korisnici to traže |
| **Pre-Nivelacija Prioriteti** | ✅ **Keep as CANONICAL (nivelacija workflow)** | Jedina decision surface za "šta puniti pre markdowna"; visoka business criticality | Ukloniti lokalni scoring (T3); dodati cache (T7); dugoročno može absorbovati Pre/Post tab |
| **Shoe Type Sales Stats** | 🔵 **Keep as supporting** | Prodajni mix po tipu obuće; koristan za asortiman odluke; server-side recommendation (hybrid, zahteva cleanup) | Alinirati threshold (T1); ukloniti lokalni fallback; ostaje na svom URL-u |

---

## 8. Leadership Memo

### Analitički sistem u raskrsnici

Trendplus analytics u ovom trenutku nema problem sa podacima, nema problem sa kapacitetom, i nema problem sa tehničkim fundamentima. `AnalyticsDecisionRecommendationEngine`, `AnalyticsMarginPolicy`, i `HybridCacheService` su solidna osnova koju malo sistema ima u ovakvom stadijumu razvoja. Realni problem je drugačiji: sistem je narastao kao kolekcija sposobnih ali izolovanih ekrana, a ne kao koherentan proizvod. Svaki ekran radi dobro sam za sebe. Zajedno, šalju konfuzne i ponekad konfliktne signale.

### Zašto nije rešenje dodavati ekrane

Dodavanje novog ekrana pored tri existirajuća supplier ekrana ne rešava problem — pogoršava ga. Fragmentacija supplier analitike nije pitanje broja ekrana; pitanje je semantičke discipline. Dok god tri ekrana prikazuju tri različite preporuke za istog dobavljača (jer svaki koristi svoju formulu i sopstvene pragove), korisnik mora sam da vrši reconciliation u glavi — a to je posao koji bi decision-support sistem trebalo da uradi umesto njega. Svaki novi ekran bez centralizovane semantike povećava kognitivni teret korisnika i troškove održavanja sistema.

### Semantički drift kao skriveni dug

`decisionScore` metrika postoji u pet različitih formula u pet fajlova, sa dva različita set praga (68/43 na četiri strane, 70/45 na jednoj). Korisnik koji pored dva ekrana vidi isti dobavljač rangiran kao "Pojačaj" na jednom i "Zadrži" na drugom ne može da zna da li ta razlika znači nešto ili je artefakt različitih formula. Ovo nije edge case — ovo je normalan workflow za svakog korisnika koji koristi više analitičkih ekrana istovremeno. Rešenje nije u popravljanju pojedinačnih formula; rešenje je u tome da postoji samo jedna formula, na jednom mestu, na serveru.

### Canonical surface kao poslovni prioritet

Preseka je jasna: `SupplierSalesStats` je canonical supplier decision surface. On je jedini koji koristi server-side `AnalyticsDecisionRecommendationEngine`, prikazuje `statusReason` u UI, ima prewarm caching na startup-u, i pokriva sve relevantne dimenzije (revenue, margin, PoP, split signal, cost quality). `SupplierDecisionHub` donosi vredan komplementarni pogled — `SupplierQualityIndex` iz analytics DB-a je drugačija i validna perspektiva — ali mora biti dostupan kao drugi tab unutar canonical surface-a, ne kao zasebna decision surface koja paralelno generiše svoju preporuku. `SupplierFootwearAnalytics` je vredan drilldown, ali mora biti tretiran kao detail tab, ne zasebni entry point.

### Zašto caching nije opcija nego obaveza

`SupplierDecisionHub` i `PreNivelacijaPriority` nemaju application-level cache. Ovo nije performance nijansa — ovo je stability risk. Pre-nivelacija workflow ima burst pattern: 3–6 korisnika istovremeno na istom periodu u sezoni. Bez cachinga, svaki korisnik pokreće kompletan DB round-trip sa 4+ upita. Ovo je rešivo u jednom spritu dodavanjem `HybridCacheService` koji već postoji u projektu. Odlaganje ovog popravlja nije štednja, to je odlaganje stabilizacije.

### Šta nosi najveći ROI

Tri Wave-a roadmapa su sekvencirana prema ROI/rizik omeru. **Wave 1** je čist pozitivan doprinos sa nultim rizikom: shared formatters i canonical tooltip tekst eliminišu vidljive nekonzistentnosti bez ijedne backend promene. **Wave 2** je suštinska konsolidacija: backend migration scoring logike i tabbed supplier surface direktno povećavaju pouzdanost sistema i smanjuju kognitivni teret korisnika. Ovo je onaj korak koji transformiše sistem iz "više dobrih ekrana" u "jedan koherentan alat". **Wave 3** je inteligentni sloj: demand signal iz analytics_intel unutar Inventory ekrana znači da korisnik može da donese odluku o dopuni bez otvaranja InsightStudio posebno. Ovo je razlika između dashboarda i decision-support sistema.

### Preporuka

Sledeći razvojni ciklus treba da počne sa Wave 1 (1–2 sprinta, nulti rizik, vidljivi rezultati) i da odmah nastavi sa Wave 2, iteracija 2.1 (T2 + T3 + T6 + T7). Do kraja Wave 2, Trendplus analytics biće sistem sa jednom canonical supplier preporukom, bez frontend-lokalnih decision formula, sa caching-om na svim ključnim rutama, i sa jednim tabbed URL-om za supplier odluke. To je minimalni skup koji system čini koherentnim. Sve ostalo — Wave 3 intelligence, InsightStudio integracija, demand signals u inventory — dolazi na toj osnovi i dodaje vrednost jer osnova postoji.

---

## 9. Final Self-Check

- [x] **Canonical surface je jasno definisan** — SupplierSalesStats = canonical supplier; Inventory = canonical inventory; PreNivelacijaPriority = canonical za nivelacija workflow; ShoeTypeSalesStats + ColorSalesStats = supporting; SupplierDecisionHub + SupplierFootwearAnalytics = tab/drilldown u canonical
- [x] **Canonical metric semantics su definisane** — Metric dictionary (Sekcija 3) pokriva 14 metrika sa source of truth, caveat, i drift analizom; `decisionScore` je označen kao deprecated user-visible metric
- [x] **Server-side authority plan postoji** — Sekcija 4 navodi tačno koji frontend lokalni compute mora izaći, kojim redom, sa minimalnim rizik strategijom
- [x] **Roadmap je sekvenciran** — 3 talasa sa jasnim dependency lancima; Wave 1 → Wave 2 → Wave 3; svaki talas ima defined "done before next" kriterijume
- [x] **Top taskovi su konkretni** — Svaki task referencuje tačne fajlove i linije koda (npr. "SupplierFootwearAnalyticsPage.tsx linija 305"); acceptance criteria su testabilni
- [x] **Keep/Merge/Deprecate odluke su eksplicitne** — 9 surface-a adjudikována; nijedno nije ostavljeno u "svi su korisni" statusu
- [x] **Plan je implementabilan bez scope creep-a** — Wave 1 je čisto frontend refactor; Wave 2 dodaje 2–3 backend endpoint-a i konsoliduje routing; Wave 3 uvodi jedan cross-DB feature; nijedan talas ne uvodi novu analytics domenu

### Šta nije moglo pouzdano da se preseče:

- **Analytics_intel MV refresh schedule** nije bio vidljiv u kodu. T10 (demand signal u Inventory) pretpostavlja stabilan refresh. Ako MVs nisu na redovnom schedule-u, T10 mora biti blokiran dok se to ne reši.
- **SupplierDecisionHub tabbed merge UX** (T8) zahteva validaciju sa stvarnim korisnicima pre finalnog layout-a. Predložena struktura (3 taba) je logična sa tehničke strane, ali distribucija sadržaja između tabova može zahtevati korekciju po feedback-u.
- **PreNivelacijaPriority lokalna formula** (linija 285) nije bila direktno pročitana u ovom sesiji — verifikacija je bazirana na session memory. Pre T3 implementacije: pročitati tačnu formulu i potvrditi da backend replacement engine pokriva iste input varijable.
