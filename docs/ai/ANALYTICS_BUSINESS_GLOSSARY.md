# Analytics Business Glossary (canonical)

Status: canonical for product/business Analytics semantics (2026-10-05)  
Owner: Analytics product + reliability docs  
Code anchors: `Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts`, `analyticsConstants.ts`, `analyticsDateRanges.ts`, `Application/Analytics/*`, owner decisions in `.ai/runs/2026-10-04-analytics-owner-decisions-evidence.md`  
Related: `docs/ai/ANALYTICS_STANDARDS.md`, `docs/ai/ANALYTICS_DESIGN_SYSTEM.md`, `docs/qa/SUPPLIER_SHOETYPE_ACCURACY_CONTRACT_2026-09-26.md`

This glossary locks **business meaning** so agents and developers do not reinvent labels or confuse null with zero. It does **not** authorize renaming owner-approved UI strings without a product decision.

When docs conflict, prefer: (1) current code + focused tests, (2) this glossary for business terms, (3) `ANALYTICS_STANDARDS.md` for screen/UX rules, (4) dated owner-decision evidence.

---

## 1. Money and margin

| Term (UI / docs) | Metric key / code | Business meaning | Why it matters | Do not confuse with |
|---|---|---|---|---|
| **Prihod** | `revenue` | Ukupna **prodajna vrednost** stavki u izabranom periodu i filterima (`SUM(prodajna_vrednost)`). Canonical methodology label. | Pokazuje obim prodaje; **nije** profit. | Neto profit; nabavna vrednost; marža |
| **Promet** | same as `revenue` (alias) | Isti ekonomski pojam kao Prihod na mnogim Operations ekranima (tabele/PoP). Alias u registry-ju: `["Prihod","Promet","Ukupan prihod"]`. | Korisnici u maloprodaji često kažu „promet“. | Ne uvoditi treći sinonim („Ukupna vrednost”) za isti broj |
| **Prihod vs Promet (drift)** | — | **Nema owner rename odluke** da se sve UI površine svedu na jednu reč. Registry label = **Prihod**; Operations/PoP copy često koristi **Promet**. Dok owner ne odluči, oba znače istu metriku `revenue`. | Sprečava lažnu „ispravku“ labela koja menja samo copy. | Ne „ispravljati“ Promet→Prihod masovno bez PO |
| **Nabavna vrednost / nabavni trošak** | `totalCost` / cost coverage | Zbir nabavne vrednosti prodatih stavki gde je pouzdan trošak dostupan. Prioritet: istorijski trošak stavke → snapshot → product fallback/procena. Stavke bez troška **van** zbira. | Bez troška nema pouzdane marže. | Operativni OPEX; potencijalni trošak nabavke za reorder |
| **Maržni doprinos** | `marginContribution` | `SUM(prodajna_vrednost − nabavni_trošak)` gde je trošak dostupan. | Signal profitabilnosti pre operativnih troškova. | Neto profit; marža % |
| **Marža % / prosečna marža** | `grossMarginPct` / `supplierAverageMarginPct` | Procenat: doprinos / promet **sa dostupnim troškom** (ili AVG po dobavljaču — vidi methodology). | Poređenje dobavljača/kategorija. | Apstraktna „marža“ bez imenovanog brojioca |
| **Promena marže u procentnim poenima** | signed pp helpers | Razlika dva procenta (npr. 20% → 25% = **+5 pp**), ne +5% relativno. | Sprečava pogrešno tumačenje trenda. | Relativna % promena prometa |
| **Potencijalni prihod** | `potentialRevenueRsd` / reorder expected revenue | **Procena** prihoda od predložene dopune (Insight/reorder), nije ostvareni promet. | Reorder odluka; mora biti označena kao procena. | `revenue` / ostvareni promet |
| **Procenjeni trošak nabavke** | `estimatedProcurementCostRsd` | Procena nabavnog troška predložene porudžbine. | Poređenje sa potencijalnim prihodom. | `totalCost` ostvarene prodaje |

---

## 2. Quantity, returns, non-retail documents

| Term | Meaning | Why it matters |
|---|---|---|
| **Prodato / prodate jedinice** (`unitsSold`) | `SUM(količina)` u retail sales populaciji. Registry label: **Prodate jedinice**. | Obim po komadu, nezavisno od cene. **Nije** sinonim za Promet/Prihod (`revenue`). |
| **Povrat (signed retail return)** | Potpisana količina/vrednost maloprodajnog povrata **unutar** retail sales populacije. | Ne sme se brisati iz net prodaje; utiče na net promet. |
| **DUG / KOREKCIJA** | Broj računa (trim, case-insensitive) = ne-standardni dug/korekcija dokument. **Isključen** iz sertifikovane retail sales populacije (Daily/Supplier/Shoe/Color + oracles; RQ456). | Sprečava lažno naduvavanje prometa. Ostaje auditable van retail turnover. |
| **Nepoznat dobavljač / vrsta / boja** | Missing display identity for a known grain. | Prefer specific Serbian label over bare `Unknown` / `-`. |

---

## 3. Period, horizon, comparison

| Term | Meaning | UI rule |
|---|---|---|
| **Requested period** | Period koji je korisnik izabrao. | Uvek vidljiv u header/summary. |
| **Effective / observed period** | Period za koji postoje podaci / poslednji posmatrani sale day. | Ne prikazivati širi skup kao uži bez oznake. |
| **Half-open UTC range** | API: `[fromUtc, toUtc)`. | Korisniku prikazati **uključivi** poslednji kalendarski dan (`toInclusiveCalendarDate`). |
| **Source horizon** | Poslednji pouzdani datum/izvor prodaje za store/dataScope. | Undated decision default = last 30 calendar days ending at horizon (owner 2026-10-04 / RQ570). |
| **Beyond source horizon** | Poređenje ili period van horizonta. | **Nije dostupno** — nikad lažnih −100% / 0%. |
| **Tekući / prethodni period (PoP)** | Prethodni = neposredno prethodni interval **iste uključive dužine**, isti store/dataScope. | Pored delta pokazati osnovu („u odnosu na prethodni period”). |
| **Freshness / svežina** | Kada su podaci poslednji put uspešno osveženi/uvezeni. | Diskretan trust strip; critical SLA per RQ583 (48h warn / 168h critical) where applicable. |

---

## 4. Null, zero, and empty

| Display | Meaning | When to use |
|---|---|---|
| **`0` / `0 RSD` / `0%`** | Vrednost je **poznata** i jednaka nuli. | Valid empty sales day with evidence; true zero stock. |
| **Nije dostupno** (`ANALYTICS_UNAVAILABLE_LABEL`) | Vrednost **ne može** da se izračuna ili nije isporučena. | null/NaN/Infinity; missing cost; beyond horizon; failed previous period. |
| **Nema podataka** | Nema relevantnih zapisa za filtere. | Empty table/chart after query. |
| **Nedovoljno podataka** | Postoji nešto podataka, ali signal nije dovoljan za pouzdanu preporuku/ocenu. | `insufficient_data` / blocked recommendation. |
| **Nije primenljivo** | Metrika nema smisla u kontekstu. | Rare; prefer explicit reason. |

**Zabranjeno:** prikazati `0` umesto greške/nedostupnosti; jedan generički `N/A` za sva stanja; mock/demo podatke kao stvarne posle API fail-a.

---

## 5. Inventory age and valuation

| Term | Owner rule (2026-10-04) | Meaning |
|---|---|---|
| **Inventory age** | RQ576 | Godine/dani zalihe samo iz **stvarnog inbound** lineage (`last_inbound_receipt`). **Last sale nije proxy za starost.** |
| **Valuation** | RQ576 | Poslednji pouzdani pozitivni inbound unit cost, zatim označena procena sa sale-line; bez unverified master-cost scale; missing ≠ 0. |
| **Lager u riziku / kapital u sporoj zalihi** | `stockAtRisk` / `slowStockCapital` | Kapital u rizičnoj/sporoj zalihi — indikativno, nije knjigovodstvo. |

---

## 6. Signal strength and recommendations

| Term | Meaning | UI consequence |
|---|---|---|
| **Pomoćni signal** | Analitički indikator; **nije** finalna preporuka. | Scorecard / color / shoe supporting surfaces. |
| **Preporuka sistema** | Backend-owned actionable recommendation. | Primary CTA only when `recommendationAllowed`. |
| **Nedovoljno podataka / insufficient signal** | Signal below policy threshold. | Ne prikazivati bold CTA kao da je preporuka pouzdana; koristiti gated copy (npr. „Preporuka nije dostupna”). |
| **Pouzdanost signala (reliability)** vs **sigurnost preporuke (confidence)** | Različiti pojmovi u `ANALYTICS_STANDARDS`. | Ne zvati svaki 0–100 score „verovatnoćom tačnosti”. |
| **Insight Studio** | Experimental (RQ582). | Bez rename-a Velocity/OOS; ne tretirati kao sertifikovani Operations ekran. |

---

## 7. Trust and quality (user-facing)

| Status family | Prefer Serbian | Notes |
|---|---|---|
| Trusted / good | Pouzdano / Dobro | Not OK/PASS/Healthy as user primary copy |
| Partial / warning | Delimično / Upozorenje | Coverage/freshness limitations |
| Stale | Zastareli podaci | Distinct from hard error |
| Unavailable | Nije dostupno | Metric-level |
| Insufficient | Nedovoljno podataka | Recommendation-level |
| Error | Greška + retry | Keep filter context |

---

## 8. Screen-purpose shorthand

| Screen | Primary user question |
|---|---|
| Dashboard | Šta je najvažnije sada i gde da kliknem dalje? |
| Prodaja po dobavljačima | Ko donosi promet/maržu i ko pada? |
| Vrsta obuće / boja | Koji asortiman raste ili gubi udeo? |
| Dnevna prodaja / smena | Kad se prodaje i gde su anomalije? |
| Inventory / Bilans | Koliko imamo, koliko vredi, šta je staro/sporo? |
| Data Quality | Šta blokira pouzdane odluke i gde popraviti? |
| Pre-nivelacija / Pre-Post | Šta prvo spustiti i šta se desilo posle? |
| Supplier / Product Decision | Šta uraditi sledeće i zašto? |

---

## 9. Change control

- Update this file when an owner decision changes a business definition.
- Do not invent formulas here; point to code/methodology registry.
- Presentation-only synonyms belong in UI audits; **semantic** changes need reliability/owner evidence.
- Residual product choice: unify **Prihod** vs **Promet** display labels across all screens (PO).

Evidence for introduction: `.ai/runs/2026-10-05-analytics-business-docs-audit-evidence.md`, `docs/qa/ANALYTICS_BUSINESS_DOCS_PROMPT_AUDIT_2026-10-05.md`.

Recertify 2026-10-05: clarified that `unitsSold` is quantity (not Promet/Prihod); Insight Studio trust helper uses `ANALYTICS_UNAVAILABLE_LABEL` (not `N/D`). See `docs/qa/ANALYTICS_AUDITS_RECERTIFY_2026-10-05.md`.

Recertify-2 2026-10-05: Actions outcome hint and nivelacija FE manual aligned to **Nije dostupno**; Insight average-margin headers use **Prosečna marža** (no `Avg marza`). Evidence SHA drift on prior audit run files corrected. See `docs/qa/ANALYTICS_AUDITS_RECERTIFY_2_2026-10-05.md`.
Recertify-3 2026-10-05: Color PoP tip and Global Trends price/empty/loading copy aligned to **Nije dostupno** / Serbian actions; Color share export uses Brojilac/Imenilac. See `docs/qa/ANALYTICS_AUDITS_RECERTIFY_3_2026-10-05.md`.
Recertify-4 2026-10-05: TrendDashboard Loading/Refresh/null-price and Amazon PriceLabel aligned to **Nije dostupno**; sync toasts Serbianized. See `docs/qa/ANALYTICS_AUDITS_RECERTIFY_4_2026-10-05.md`.
