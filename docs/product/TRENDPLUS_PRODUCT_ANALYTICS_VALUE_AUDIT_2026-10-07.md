# Trendplus — product / analytics / business value audit (2026-10-07)

Osnova: `HEAD == origin/main == cb5ae4dfca00e257c711bf1cb7c764d5eec95019` (box clone, 126 commita posle `242361ca`).
Produkcija (jedan read-only GET, 2026-10-07 ~14:50 Beograd): `GET /api/runtime/version` → `commitSha=194df308…`, build 2026-10-07 10:24 (Beograd), `processType=web`; `GET /api/analytics/refresh-status?dataScope=all` → `observedSalesPeriodToUtc=2026-08-05`, `lastSuccessfulRefreshAtUtc=null`, `lastSuccessfulImportAtUtc=null`, `workersEnabled=false`, `dataFreshnessStatus=unknown`, `cacheMode=in-memory`. Ostali live GET-ovi u ovom auditu nisu izvršeni (automatska bezbednosna provera ih je zaustavila), pa su live tvrdnje ispod koje nisu iz ta dva odgovora preuzete iz datiranih audita i označene datumom.
Ovaj dokument je **dated product snapshot**, ne live router. Routing ostaje u `MASTER_ROADMAP.md` i owner queue-ovima.

## Owner correction on freshness vs value (2026-10-07)

Freshness is a **currentness gate**, not a universal value gate. Existing historical imports and certified bounded windows remain valid for formula correctness, historical sales/margin analysis, inventory/markdown semantics, supplier evidence and product implementation. A new import is required when Trendplus claims that a recommendation reflects the **current** business state or when running a prospective action-to-outcome pilot. Canonical execution priority is now maintained in `PRODUCT_VISION.md`, `BUSINESS_ROADMAP.md`, `MASTER_ROADMAP.md` and the RQ queue; this dated audit must not be used to serialize all product work behind STAB16.

## Post-review status (2026-10-07)

Ovaj audit je ponovo proveravan protiv current-main koda, queue/evidence dokumenata i javno dostupnih stranica konkurenata. Koristi sledeće oznake:
- **VERIFIED** — direktno potvrđeno current-main kodom/testom ili datiranim live evidence-om.
- **SUPPORTED JUDGMENT** — zaključak koji dobro sledi iz dokaza, ali nije merena tržišna činjenica.
- **HYPOTHESIS** — komercijalna/product pretpostavka koju pilot mora da potvrdi.
- **CORRECTED** — prvobitna formulacija je bila prejaka ili netačna i ispravljena je ispod.

Ključne korekcije: `workersEnabled=false` važi za web proces i ne dokazuje da poseban worker servis ne postoji; QDB već ima SQL Server discovery/mapping/checkpoint put do `SourceSyncAppliedRows`, ali ne i dokazano canonical `Artikli/Prodaja` punjenje/onboarding; komercijalni pragovi i pricing su hipoteze; konkurentski pregled pokazuje da su size/color, zalihe, nivelacije i reporting široko dostupni, dok zatvoren lokalni recommendation→action→outcome loop ostaje diferencijaciona hipoteza, ne dokazana tržišna ekskluziva.

## Executive verdict

1. **Trendplus danas ne može pouzdano da menja aktuelnu odluku vlasnika jer je potvrđeni prodajni horizont zastareo.** Poslednja potvrđena prodaja u produkciji je 05.08.2026. `refresh-status` 07.10. nema durable uspešan import/refresh i web proces prijavljuje `workersEnabled=false`; postojanje/zdravlje posebnog worker servisa je **UNPROVEN**, ne dokazano odsustvo. **VERIFIED** za zastarelost i durable evidence gap.
2. **Najveći problem više nije osnovna formula.** Promet/komadi i veliki deo maržnih semantika imaju jaku lokalnu oracle/parity evidenciju. Javne stranice lokalnih POS/ERP proizvoda potvrđuju da su inventory, dimenzije artikla, nivelacije i standardni prodajni/zalihni izveštaji već široko dostupni; zato same deskriptivne tabele nisu dovoljan razlog za plaćanje Trendplus-a. **VERIFIED + SUPPORTED JUDGMENT**.
3. **Repo nema dokaz da je ijedna Trendplus preporuka već donela merljiv poslovni rezultat.** Poslednji eksplicitni live dokaz (28.09) imao je 4 smoke zapisa i `measuredSampleSize=0`; nema novijeg evidence-a koji dokazuje realne izmerene akcije. Zato je business outcome proof i dalje **0/10, UNPROVEN**. **VERIFIED kao evidence-gap, ne tvrdnja da se van sistema nikad nije desila korisna odluka.**
4. **Više važnih dimenzija i odluka nema dovoljno dobar izvor.** Live re-audit 04.10 je našao 100% nepoznatu boju/kategoriju/pol/plaćanje/sat; `TipObuce` ipak postoji i RQ574 ga je proglasio autoritativnim za relevantne odluke. Dokument prodaje nije dokazan customer receipt (25 dokumenata / 307 kom u referentnom prozoru), a inventory valuation je istorijski davala ~26 RSD/par zbog lošeg cost lineage-a. Boja, smene, basket metrike i deo inventory vrednosti zato treba fail-closed/degradirati dok izvor nije dovoljan. **VERIFIED, uz korekciju da nisu svi analytics ekrani nepodržani.**
5. **Referentni retail decision scope je mali: Trend PLUS 1/2 i sertifikovani prozor 07.07–05.08 ima 307 kom / 1.561.120 RSD.** Sofisticiranije metode (DiD/elastičnost/MA7/winsor/log-ratio) mogu stvoriti **preveliku percepciju preciznosti** kada su uzorci i pretpostavke slabi. Preferirati jednostavna pravila i prikaz veličine uzorka; napredniju statistiku zadržati samo gde prolazi unapred definisan signal/coverage gate. **VERIFIED podaci + SUPPORTED JUDGMENT.**
6. **Razvojna istorija je nesrazmerno opterećena governance/evidence/UI radom u odnosu na validaciju poslovnog ishoda.** Prvobitni ~79% obračun bio je audit-snapshot klasifikacija, ne kanonska metrika i ne treba ga tretirati kao egzaktan KPI. Ono što jeste dokazano jeste veoma veliki queue/evidence corpus i nula outcome proof-a. Uvesti cilj da governance ostane pomoćni trošak, ne razvojni proizvod. **SUPPORTED JUDGMENT; precizan procenat povučen kao tvrda činjenica.**
7. **Tri modula su potencijalni razlog za plaćanje:** (a) markdown/nivelacija ciklus (šta sniziti → da li je uspelo), (b) zalihe kao kapital (mrtva/spora roba po dobavljaču, rupe u veličinama), (c) dobavljač kao pregovarački argument (marža × obrt × koliko kapitala drži). Sva tri su danas **HV/LC**: vredni, ali bez svežih podataka, bez ispravne nabavne cene i bez ijednog izmerenog ishoda.
8. **Ne sme se još prodavati kao outcome-proven „optimizacija“.** Trenutni komercijalni/pilot put je Access-dominantan i sadrži Ivan-specifičnu semantiku (`Europe/Belgrade`, `DUG/KOREKCIJA`, retail-store policy). QDB ipak već ima provider-neutral contract i SQL Server put do checkpointed staging-a; ono što nedostaje je ponovljiv canonical import/onboarding do istog analytics modela i spoljašnji customer proof. **CORRECTED.**
9. **Najvažniji sledeći korak:** vratiti svež import u produkciju i za 6 nedelja zatvoriti JEDAN loop: 20–30 stvarnih nivelacija koje je Trendplus predložio, sprovedene, i izmerene prema unapred zapisanom poređenju. Do tada: stop novim trust/UI/queue slojevima.

Sažete ocene: **CURRENT PRODUCT VALUE 3/10 · CURRENT ANALYTICS TRUST 5/10 (lokalno 7, produkcija 3) · CURRENT BUSINESS OUTCOME PROOF 0/10 · COMMERCIAL READINESS 2/10 · POTENTIAL IF EXECUTED WELL 7/10.**

---

## 1. Stvarno stanje (osveženo, ne iz starih audita)

| Činjenica | Dokaz | Nivo |
|---|---|---|
| HEAD = origin/main = `cb5ae4df` | `git rev-parse` | — |
| Deployed API = `194df308`, 107 commita iza HEAD, ali svi osim `14690775` (perf merenje) su docs/UI | `git log 194df308..HEAD -- Api Application Infrastructure Domain Database` → samo `14690775` | L6 (SHA) |
| `d4ae1b9b` (stale `vw_vendor_sales_nivelacija` fix) **jeste u deployed SHA** | `git merge-base --is-ancestor d4ae1b9b 194df308` = true | L6 za SHA; **Pre/Post live odgovor posle deploya: UNPROVEN** (nije proveren u ovom auditu; zadnji poznati live = `contract_missing` 04.10, RQ545 PARTIAL) |
| Podaci staju 05.08.2026; `refresh-status` nema durable successful import/refresh; web proces ima `workersEnabled=false`; poseban worker servis nije potvrđen ni opovrgnut; cache je in-memory | live `refresh-status` 07.10 + STAB16 contract | L6 za javni runtime signal / worker-service state UNPROVEN |
| Access import ne stiže u prod | STAB16 BLOCKED („provider deployment authority“), `docs/qa/ANALYTICS_REAUDIT_2026-10-04.md` §5.1 | L6 (negativno) |
| Smene u UTC zbog TimeZoneId | RQ586 DONE (`6d5a7414`) za konfiguraciju; ali live redovi imaju `legacy_access_wall_clock` i izvor nema satnicu → smene **nisu merljive** (reaudit §3) | L2 kod / izvor ne podržava |
| Neon schema drift / AutoMigrate | RQ587/RQ588 DONE (vidljivost + discovery guard); stvarno usklađivanje produkcione šeme = STAB16/RQ545 | UNPROVEN live |
| Queue: 0 READY, 0 IN_PROGRESS na header nivou; 66 WAITING, 5 PARTIAL, 4 BLOCKED | skripta nad `docs/ai/*QUEUE*.md` | — |

Zaključak: produkcija je kodno skoro aktuelna, ali **podatkovno zamrznuta**. Svaki „L6“ u repou je L6 za kod, ne za podatke.

## 2. Product Scorecard (0–10)

| Dimenzija | Ocena | Obrazloženje |
|---|---|---|
| Business value (danas) | 3 | Tačni deskriptivni izveštaji nad zastarelim podacima; nijedna odluka danas ne može da se donese iz produkcije. |
| Analytics correctness | 7 | Promet/komadi/marža usklađeni na 6 ekrana (1.561.120 RSD / 307 kom, jul); oracle testovi RQ445–447, 524–528, 547–550, 561–568. Inventory vrednost/starost pogrešne zbog izvora (RQ576 popravio logiku, izvor ostaje). |
| Correctness evidence | 6 | L4/L5 lokalno za core; L6 samo za kod; RQ454/RQ565 (produkciono sravnjenje) WAITING. |
| Data quality | 2 | 100% nepoznata boja/kategorija/pol/plaćanje/sat; trošak u pogrešnoj razmeri; 1.078 artikala bez troška. |
| Freshness/provenance | 2 | Provenance metadata odlične; svežina 63 dana; refresh nikad uspešan. Odlično dokumentovan problem nije rešen problem. |
| Decision usefulness | 3 | Product Decision 0 primenljivih preporuka (04.10); Decision Board pretežno blokeri; Pulse prazan. |
| Actionability | 3 | Postoje akcije/ledger, ali 0 stvarnih akcija. |
| Outcome measurement | 1 | RQ557 deskriptivni ledger i RL lifecycle postoje; 0 izmerenih ishoda; RL12 (kauzalni gate) WAITING. |
| UX clarity | 6 | P-UI-41…54 doneli hijerarhiju i kontrast; ali 18+ analitičkih ruta za 2 prodavnice. |
| Trust/transparency | 7 | Najjači sloj: no-fake-zero, partial/stale vidljivi. Delimično preteran (vidi §8). |
| Differentiation | 3 | Core reporting se snažno preklapa sa POS/ERP-om. Potencijalna razlika je lokalni explainable decision + action/outcome loop, ali još nije outcome-proven. |
| Time-to-value (novi kupac) | 2 | Access je jedini dokazani pilot-to-analytics put. QDB ima SQL Server discovery/mapping/checkpoint sync do staging-a, ali nema dokazano canonical Artikli/Prodaja punjenje, operator onboarding i external-customer proof. |
| Maintainability | 4 | Dobri testovi, ali ~70k linija queue teksta, 35 root `.md`, tmp/pgdata/`*.txt` artefakti u rootu. |
| Documentation quality | 3 | Mnogo, pedantno, ali nije proizvodno; README opisuje RabbitMQ/Redis/pgvector/ONNX koji u produkciji ne nose vrednost. |
| Test quality | 7 | Oracle/golden/adversarial testovi realno vredni za finansijske greške. |
| Production proof | 2 | Kod L6; podaci, workeri, MV i Pre/Post live nisu dokazani. |
| Commercial readiness | 2 | Jedan korisnik, jedan izvor, nema onboardinga, nema dokaza ishoda. |

**CURRENT PRODUCT VALUE 3/10 · CURRENT ANALYTICS TRUST 5/10 · CURRENT BUSINESS OUTCOME PROOF 0/10 · COMMERCIAL READINESS 2/10 · POTENTIAL IF EXECUTED WELL 7/10** (realno za 2–10 prodavnica obuće u regionu ako se zatvori markdown + zalihe loop).

## 3. Truth/Evidence Scorecard (L0–L7)

Najviši dostignuti nivo; razdvojeno po vrsti tačnosti. „—“ = ne primenjuje se / nema.

| Metrika / funkcija | Kalkulacija | Semantika | Kompletnost podataka | Svežina | Cross-screen | Runtime (prod) | Poslovna korist | Outcome |
|---|---|---|---|---|---|---|---|---|
| Promet/komadi po dobavljaču, vrsti obuće, danu | L5 (oracle + seeded) | L4 (DUG/KOREKCIJA isključeni, `SalesReceiptPopulationPolicy.cs`) | dobra | **L6 negativno** (05.08) | L5 (6 ekrana isto) | L6 za jul (04.10) | srednja (POS ima) | L0 |
| Marža / maržni doprinos | L4 | L4 (istorijski trošak 100% pokriven u julu) | srednja (trošak u artiklu pogrešan) | stara | L4 | L6 jul | visoka | L0 |
| Prodaja po boji | L2 | L2 | **0% (100% Nepoznato)** | stara | — | radi, bez vrednosti | 0 danas | — |
| Prodaja po smeni | L2 | **nemerljivo** (nema satnice) | 0 | stara | — | — | 0 danas | — |
| Zalihe: vrednost / starost | L4 logika (RQ576) | pogrešna ulazna nabavna cena; starost = datum importa | loša | stara | low-stock 0 vs 213 (04.10) | L6 pogrešno | visoka (potencijal) | — |
| Pre/Post nivelacije | L4 (RQ568 oracle 6 ruta) | L3 (event-aligned DiD RQ542; OOS/LostSales eksplicitno nedostupni) | bez istorije zaliha | stara | L3 | **UNPROVEN posle `d4ae1b9b` deploya** | visoka | **L0** (deskriptivno, RQ557) |
| Pre-nivelacija prioriteti | L3 | heuristika, nekalibrisana (`RQ556` v9 težine owner-gated) | bez veličina/zaliha istorije | stara | RQ555 WAITING | L6 (radi, 537 kandidata 04.10) | visoka | L0 |
| Product Decision | L3 | RQ573/RQ574 popravili rang i uklonili univerzalni category blocker kada `TipObuce` postoji | 04.10 live je imao 0 primenljivih; current-main kvalitet politike je popravljen, deployed reproof nedostaje | stara | parcijalno | 0 primenljivih je **datirani 04.10 live nalaz**, ne current-main tvrdnja | potencijalno visoka | L0 |
| Supplier Report / hub / scorecard | L3 | L3 | MV ne postoji u prod (`MISSING_OBJECT`) | — | share razlike (RQ476) | **ne radi (04.10)** | visoka | L0 |
| Data Quality health | L3 | RQ578 popravio „100 odlično“ | — | — | — | UNPROVEN posle fixa | srednja | — |
| Actions / outcome | L2 | RQ478 denominator fix | 0 stvarnih akcija | — | — | 4 smoke reda (28.09) | — | **L0** |
| Decision Board / Pulse | L3 | L3 | zavisi od svega iznad | — | — | Board ~17 s, pretežno blokeri; Pulse 0 stavki | niska danas | L0 |

Pravilo: ništa nije „100% tačno“. Najviše što se pošteno može reći: *core prodajni brojevi su lokalno nezavisno dokazani (L5) i u produkciji tačni za istorijski period do 05.08.*

## 4. Ekrani → odluka → klasa

| Ekran | Realna odluka | Klasa |
|---|---|---|
| Prodaja po dobavljačima | sa kim više/manje poslovati | descriptive-only (blizu decision-support kad dobije obrt/kapital) |
| Prodaja po vrsti obuće | asortiman po tipu | descriptive-only |
| Prodaja po boji | asortiman boja | descriptive-only, **danas bez podataka** |
| Prodaja po smeni / Dnevna | osoblje, radno vreme | descriptive-only, smene **nemerljive** |
| Zalihe / Bilans | šta stoji, šta prebaciti/sniziti | decision-support / unvalidated (ulaz pogrešan) |
| Data Quality | da li verovati | hygiene |
| Pre/posle nivelacije | da li je sniženje uspelo | decision-support / unvalidated (posmatranje, ne efekat) |
| Pre-nivelacija prioriteti | šta sniziti | decision-support / unvalidated |
| Supplier Report / Hub | pregovori, sledeća porudžbina | decision-support / unvalidated, **ne radi u prod** |
| Product Decision / Decision Board | šta danas | decision-support / unvalidated, 0 primenljivih |
| Analytics Dashboard | pregled | descriptive-only |
| Analytics Actions / outcome | da li je pomoglo | **outcome-capable po šemi, outcome-empty u praksi** |
| Operations / trust / oracle | interno poverenje | hygiene (za developera, ne za kupca) |
| Insight Studio / Advanced | — | karantinovano (RQ582); ipak su RQ589–591 trošili rad na sertifikaciju skrivenih ruta → Low-ROI |

## 5. Module Value Ranking

| Modul | Business value | Trust | Evidence | Actionability | Differentiation | Complexity | Priority | Klasa |
|---|---|---|---|---|---|---|---|---|
| Markdown ciklus (Pre-nivelacija → Pre/Post → ledger) | 9 | 4 | L4 lokalno, L0 outcome | 6 | 7 (Srbija: nivelacija je ritual, merenje nije) | visoka | **1** | Hero |
| Zalihe kao kapital (mrtva/spora roba, starost, kapital po dobavljaču) | 9 | 2 | L4 logika, ulaz loš | 7 | 6 | srednja | **2** | Hero |
| Dobavljač: vrednost (marža × obrt × kapital) + Report | 8 | 4 | L3, prod ne radi | 6 | 6 | srednja | **3** | Hero |
| Svežina/import pipeline | 10 (preduslov) | 2 | L6 negativno | — | 0 | srednja | **0** | Necessary hygiene |
| Data Quality | 6 | 5 | L3 | 3 | 3 | srednja | 5 | Necessary hygiene |
| Prodaja po dobavljačima / vrsti obuće / dnevna | 5 | 8 | L5 | 2 | 1 | niska | održavati | Supporting (LV/HC) |
| Actions / outcome tracking | 8 (potencijal) | 3 | L2 | 7 | 7 | srednja | **1b** | Hero-enabler |
| Weekly owner digest (RQ585) | 8 | — | L0 | 9 | 5 | niska ako se gradi na gotovom | 4 | Hero shell |
| Product Decision Center | 6 | 3 | L3 | 5 | 3 | **vrlo visoka** (6.000+ linija u jednom endpoint fajlu) | uprostiti | Supporting |
| Decision Board / Pulse / Executive | 4 | 3 | L3 | 4 | 2 | visoka | zamrznuti | Low-ROI dok ulazi nisu ispravni |
| Prodaja po boji / smeni | 2 (danas) | — | L2 | 1 | 1 | niska | sakriti dok izvor nema podatke | Low-ROI |
| Insight Studio / Advanced / forecast / scenario / embedding/ML | 2 | 2 | L2 | 1 | 1 | vrlo visoka | stop | Low-ROI |
| GenAI copilot (GAI03–12) | 2 danas | — | L0 | — | marketinški | visoka | defer | Low-ROI |
| Multi-tenancy (MT02–12) | 1 danas (1 kupac) | — | — | — | — | vrlo visoka | defer | Low-ROI |

## 6. Value × Confidence matrica (glavni zaključak)

| | High confidence | Low confidence |
|---|---|---|
| **High value** | *(praktično prazno danas)* — istorijski promet/marža imaju visoko poverenje, ali su standardna retail reporting sposobnost dostupna i u drugim POS/ERP rešenjima | **Markdown ciklus, Zalihe-kapital, Dobavljač-vrednost, Actions/outcome, svežina.** Ovde ide skoro sav trud: podaci, nabavna cena, produkciono sravnjenje, prvi izmereni ishod. |
| **Low value** | Prodaja po vrsti obuće/dobavljačima/dnevna kao tabele, trust header, Operations integrity ekrani, UI ratchet testovi → **zadržati, ne ulagati** | Boja, smene, korpa, Insight Studio, forecast/scenario, Pulse, GenAI, MT, embedding/ONNX/RabbitMQ → **stop / defer / sakriti** |

Najvažnija istina matrice: **HV/HC kvadrant je prazan.** Sve što je skupo dokazano je jeftino po vrednosti; sve što je vredno nije dokazano.

## 7. Top business opportunities

1. **Mrtva i spora roba kao kapital u RSD po dobavljaču i objektu** (pare koje stoje, starost od poslednjeg ulaza, ne od importa). **Product hypothesis:** odluka je česta, finansijski materijalna i vrednija od još jednog sales dashboarda; pilot mora potvrditi učestalost i willingness-to-pay. Zahteva ispravan cost lineage i datum ulaza.
2. **Markdown lista + izmeren ishod:** „ovih 20 artikala sniziti 20–30%; za 4 nedelje: prodato X od Y pari, realizovana marža Z, ostatak na zalihi“. Prvi verodostojan dokaz vrednosti.
3. **Pregovarački paket za dobavljača** (RQ465 postoji): marža, sell-through, kapital u zalihi, % prodato samo uz sniženje, povrati — jedna strana po dobavljaču pred sezonsku porudžbinu. Retka (2×godišnje) ali vrlo skupa odluka.
4. **Rupe u veličinama** (RQ559 owner/source-gated): za obuću je ovo specifično i vredno, ali zahteva zalihe po veličini — proveriti da li ih Access izvor ima pre ulaganja.
5. **Transfer između Trend PLUS 1 i 2** (spora u jednoj, prodaje se u drugoj): jeftino, reverzibilno, merljivo.

## 8. Top correctness/trust risks (red-team)

| # | Rizik | Stanje | Dokaz / owner |
|---|---|---|---|
| 1 | Zastareli podaci izgledaju kao trenutni (default „poslednjih 30 dana“ → prazno ili −100% PoP) | delimično rešeno horizon-anchoringom (RQ569/570/571), ali uzrok (import) nije | STAB16 BLOCKED |
| 2 | Nabavna cena na artiklu u pogrešnoj razmeri → zalihe ~26 RSD/par, missing-cost DTO emituje 0 uz `costMissing` | residual | reaudit §5.6, accuracy audit 10-05 #7, RQ576 |
| 3 | PDC summary sume `+= estimate ?? 0` | residual | accuracy audit 10-05 #9 |
| 4 | Pre/Post izgleda kauzalno; DiD/elastičnost nad ~10 pari/dan | jezik ublažen (P-UI hijerarhija, RQ557 „deskriptivno“), ali brojevi i dalje deluju precizno | RQ140 PARTIAL, RQ558/RL12 WAITING |
| 5 | Selekcioni bias: snižava se ono što ne ide; post-period uključuje kraj sezone; nema istorije zaliha (OOS nedostupan) | nerešivo bez istorijskih zaliha i kontrolne grupe | RQ542 (OOS eksplicitno unavailable) |
| 6 | „Transakcija“ = dnevni dokument → prosečna korpa 62.445 RSD je besmislena | RQ577 DONE (preimenovanje), izvor ne nosi račune | izvor |
| 7 | Pre-nivelacija heurističke težine nekalibrisane, a lista izgleda kao preporuka | UI odvaja „procena modela“ (P-UI hijerarhija) | RQ556 owner-gated |
| 8 | Supplier share denominator razlike između ekrana | WAITING | RQ55, RQ476 |
| 9 | Ivan-specifična semantika tvrdo kodirana (`DUG`, `KOREKCIJA`, Europe/Belgrade, objekti STARO/Magacin/Komision) | prihvatljivo za 1 kupca, blokira 2. kupca | `Api/Services/SalesReceiptPopulationPolicy.cs:12`, `Application/Analytics/BelgradeCalendarDatePolicy.cs:21`, RQ571 |
| 10 | Trust sloj sam postaje izvor lažne sigurnosti: „verified“ na praznom skupu | RQ579 DONE | — |

Gde je matematika sofisticiranija od ulaza: DiD/control (RQ542), elastičnost, MA7, winsorizacija, z-score (RQ30), forecast backtest, scenario planning, confidence calibration — sve nad 2 prodavnice, ~300 pari/mesec, bez istorije zaliha, bez boje/kategorije. Preporuka: prikazivati sirove brojeve pre/posle + „broj pari prodat“ + jasnu oznaku „posmatranje, ne dokaz efekta“; sakriti elastičnost dok N mature događaja po grupi nije ≥ dogovoreni prag.

## 9. Over-engineering findings

- **Queue ceremonija:** audit-time brojanje je pokazalo veoma veliki queue/run-evidence corpus i dugačke istorijske headere. Tačne brojke su snapshot i brzo zastarevaju; važniji signal je da se mnogo rada meri kroz claim/close/evidence, dok business-outcome dokaz ostaje prazan. Zadržati guardrail-e koji sprečavaju finansijske greške, ali prekinuti dodavanje governance slojeva bez konkretnog rizika.
- **Sertifikacija skrivenih ekrana:** RQ589/590/591 sertifikovali Insight Studio/Advanced koji su karantinovani (RQ582). Rad bez korisnika.
- **Trust metadata > korisnička vrednost:** context fingerprint, generation-bound integrity, readiness evaluator (RQ509–514) su opravdani protiv finansijski opasnih grešaka, ali su završeni pre nego što postoji ijedan svež podatak koji bi štitili.
- **Infrastruktura za veći proizvod:** README: RabbitMQ, Redis, pgvector, ONNX, Python embedding servis, Fly + Render + Vercel + Neon, MT queue (12 promptova), GAI queue (12). Produkcija: jedan web proces, in-memory cache, workeri off.
- **Previše ruta:** ~18 analitičkih ruta + Board + Pulse + Executive + Pilot Readiness za 2 prodavnice. Tri „šta da radim“ površine (Product Decision, Decision Board, Pulse) i nijedna sa primenljivom preporukom.
- **UI polish pre podataka:** P-UI-41…54 (theme, touch, responsive ratchet) — kvalitetno, ali korisnik vidi lepo složene podatke od 05.08.
- **Opravdana kompleksnost:** no-fake-zero, DUG/KOREKCIJA isključenje, istorijski trošak, oracle testovi za promet/maržu — sprečavaju realne finansijske greške. Zadržati.

## 10. Documentation audit

| Dokument | Klasa | Napomena |
|---|---|---|
| `AGENTS.md`, `docs/ai/AGENT_START_HERE.md`, `docs/ai/PROMPT_QUEUE_PROTOCOL.md` | CURRENT AUTHORITATIVE (za agente) | Dobro razdvajaju istoriju od trenutnog; ali opisuju proces, ne proizvod. |
| `MASTER_ROADMAP.md` | CURRENT, ali **engineering backlog/ledger, ne strategija** | Prvih ~80 linija su completion/claim beleške; nema product metrike, nema kupca, nema cilja u RSD. |
| `docs/product/PRODUCT_VISION.md`, `docs/roadmaps/BUSINESS_ROADMAP.md` | CURRENT, **korigovano ovim review-om** | Sada eksplicitno stavljaju freshness → inventory capital → markdown outcome → supplier value → external pilot ispred scale/AI rada. |
| `README.md` | **CORRECTED** | Developer/ops opis ostaje, ali sada odvaja aktivni core path od opcione/eksperimentalne infrastrukture i tačno opisuje Access-dominant pilot + SQL Server staging status. |
| `docs/qa/ANALYTICS_REAUDIT_2026-10-04.md`, `ANALYTICS_ACCURACY_AUDIT_2026-10-05.md` | USEFUL HISTORICAL EVIDENCE | Najkorisniji dokazi u repou. |
| `docs/qa/ANALYTICS_AUDITS_RECERTIFY_{,2,3,4}_2026-10-05.md`, ROUND3–ROUND15 prompt fajlovi | DUPLICATE / CANDIDATE FOR CONSOLIDATION | 4 recertifikacije u jednom danu; 13 „round“ fajlova. |
| `docs/qa/BACKEND_CI_*_2026-08-1x*.md` (15 fajlova) | HISTORICAL → ARCHIVE | |
| `docs/ai/NEXT_PROMPT_QUEUE.md` | HISTORICAL (deklarisano) | |
| `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` | **CURRENT; prvobitna tvrdnja INCORRECT** | QDB01–QDB06/QDB09 imaju provider-neutral + SQL Server staging put; QDB07/08 su WAITING. Review registruje residual canonical-ingestion gap kao QDB10. |
| Root `*.md` (HITNA_POMOC, ONE_LINE_FIX, QUICK_FIX_README, MARGIN_FORENSIC_*, untitled-plan-*.prompt.md…) i root `tmp_*`, `stdout.txt`, `pgdata/` | STALE / LOW VALUE → ARCHIVE | Šum za svakog novog čitaoca. |

Ključni nalaz: dokumentacija meri **da li je prompt zatvoren sa dokazom**, ne **da li je vlasnik doneo bolju odluku**. Acceptance kriterijumi su implementacioni (testovi prolaze, SHA na main), nema nijednog product success kriterijuma (RSD, pari, dani zalihe).

## 11. STOP / DEFER / REDUCE

| Stavka | Odluka | Razlog |
|---|---|---|
| Novi P-UI polish (P-UI-38 ratchet, P-UI-50) | DEFER | Vizuelno je dovoljno dobro; podaci nisu. |
| Governance/queue meta rad (claim/handoff/zero-READY dokazi) | REDUCE na minimum | ~79% poslednjih commita; nula vrednosti za kupca. Max 1 kratka beleška po zatvorenom promptu. |
| Legacy/Advanced WAITING (RQ18, RQ25–RQ38) | STOP (OBSOLETE predlog) | Ekrani karantinovani; 15 promptova bez korisnika. |
| GenAI GAI03–GAI12 | DEFER ≥ 6 meseci | Nema svežih podataka ni ishoda koje bi copilot citirao. |
| MT02–MT12 | DEFER do 2. plaćajućeg kupca | Fallback „jedna baza po kupcu“ je dovoljan. |
| PERF18 (Recharts preload), PERF16 | DEFER | Nije korisnički problem. Izuzetak: Board ~17 s u prod → STAB16. |
| RQ558 (kontrolisani markdown efekat sa sezonalnošću/kanibalizacijom) | DEFER | Nema dovoljno događaja; nema istorije zaliha. |
| Prodaja po boji / smeni kao zasebni ekrani | REDUCE (sakriti iz glavne navigacije dok izvor ne nosi podatke) | 100% nepoznato = šum. |
| Decision Pulse / Executive board | REDUCE (spojiti u weekly digest RQ585) | Tri „šta danas“ površine bez ijedne primenljive preporuke. |
| Embedding/ONNX/vector, RabbitMQ | DEFER / ne promovisati u README | Bez dokazane vrednosti. |
| Nova recertifikacija postojećih oracle-a | STOP dok se podaci ne promene | Nema novog rizika koji bi otkrila. |

## 12. Plan 2–6 nedelja (rangirano: vrednost × smanjenje neizvesnosti × učestalost / trošak)

1. **Svež import u produkciji (STAB16 + import put).** WHY NOW: bez ovoga sve je istorija. Vrednost: maksimalna. Radimo: uz owner/provider pristup utvrditi stvarni worker/deploy/import put, obnoviti dnevni import i dokazati durable uspeh. NE radimo: nove trust slojeve. Metrika: `observedSalesPeriodTo` ≤ 48 h iza očekivanog izvora; cilj 14 uzastopnih dana u SLA. DoD: live `refresh-status` + Supplier/Daily brojevi za poslednjih 7 dana sravnjeni sa izvorom (RQ454/RQ565).
2. **Ispravna nabavna cena i datum ulaza → zalihe u RSD.** WHY NOW: bez toga nema mrtve robe, kapitala ni marže po artiklu. Radimo: potvrditi razmeru troška na 20 artikala ručno sa kalkulacijom; ispraviti mapiranje u importu. NE: novi inventory ekrani. Metrika: vrednost zalihe u ±5% od knjigovodstvenog lagera. DoD: tabela 20 artikala trošak Trendplus = trošak kalkulacija.
3. **Prvi zatvoreni markdown loop (novi RQ592).** WHY NOW: jedini put do dokaza vrednosti. Radimo: Ivan bira 20–30 artikala iz Pre-nivelacija liste, zapisuje akciju u Actions (datum, artikal, objekat, stara/nova cena), unapred zapisujemo poređenje (isti artikli 4 nedelje pre, i kontrolni artikli istog dobavljača/tipa koji nisu sniženi), merimo za 4 nedelje: pari, realizovana marža RSD, preostala zaliha. NE: kauzalni model, elastičnost. Metrika: N akcija sa izmerenim ishodom ≥ 20; izveštaj „RSD marže realizovano iz robe koja je inače stajala“. DoD: jedna stranica ishoda koju vlasnik razume.
4. **Pre/Post i Supplier Report proveriti uživo posle `d4ae1b9b` deploya.** WHY NOW: kod je deployovan, dokaz nije. Radimo: read-only GET + browser, zatvoriti RQ545 ili ga precizno re-otvoriti; Supplier MV kreirati/osvežiti (STAB16). Metrika: `contract_missing`/`MISSING_OBJECT` = 0. DoD: evidence fajl sa live odgovorima.
5. **Jedna „ove nedelje“ stranica (RQ585, sada P1).** Max 10 stavki iz **postojećih sertifikovanih** signal families: markdown, inventory/transfer i supplier; size-run samo ako RQ559/source proof postoji. Sa linkom na dokaz. NE: nova Board logika ni novi scoring. Metrika: vlasnik je koristi 4 nedelje zaredom. DoD: 4 nedeljne verzije sa zapisanom odlukom po stavci.
6. **Sakriti ili degradirati prazne ekrane (Boja, Smene, Pulse, Executive) i ažurirati README.** Mali rad, veliko smanjenje šuma. Metrika: ≤ 8 ruta u glavnoj analitičkoj navigaciji.
7. **Zamrznuti queue ceremoniju:** jedna stranica „product status“ (ovaj dokument + nedeljni update) umesto paragraf-istorije u headerima. Metrika: < 20% commita su governance.
8. **Demo za drugog vlasnika obuće** (posle 1–3): 20 minuta, njihov problem = „koliko para mi stoji u robi koja se ne prodaje i šta da snizim“.

## 13. Plan 2–6 meseci (redosled i zašto)

1. **Zalihe-kapital cockpit** (mrtva/spora roba, dani zalihe po dobavljaču/tipu/objektu, kapital u RSD, transfer predlozi) — najčešća i najlakše merljiva odluka; deterministička pravila.
2. **Markdown intelligence v2** — iz izmerenih ishoda RQ592 kalibrisati jednostavna pravila (npr. „starost > 120 dana i < 2 para/mes → 20%“); elastičnost tek kad bude ≥ 50 mature događaja po grupi.
3. **Supplier value + pregovarački paket** pred sezonsku porudžbinu (marža × sell-through × kapital × % prodato tek uz sniženje).
4. **Outcome learning** — RL12 iz dokumenta u minimalni runtime: unapred registrovano poređenje, ne ML.
5. **Import/onboarding kvalitet** — konfigurabilna semantika (`DUG/KOREKCIJA`, vremenska zona, objekti, šta je obuća) po kupcu; završiti QDB admin/onboarding i canonical-ingestion seam tako da non-Access izvor zaista puni isti retail model. CSV/Excel može biti sledeći jednostavan ulaz tek nakon izbora prvog spoljnog pilota; nije potrebno paralelno graditi više konektora.
6. **Komercijalni pilot** sa 1 spoljnim trgovcem (2–5 prodavnica), jedna baza po kupcu.
7. **Veličine** (size holes) samo ako izvor nosi lager po veličini.
AI/ML samo gde pobeđuje pravilo na izmerenim ishodima; GenAI kao „objasni ovu stavku“ tek posle toga.

## 14. Strategija 6–24 meseca

Najrealnija pozicija: **„merchandising decision layer“ za male/srednje lance obuće i odeće iznad postojećeg POS/ERP-a** (ne zamena za POS, ne opšti BI). Fokus: zalihe kao kapital + nivelacije sa izmerenim ishodom + dobavljač kao pregovarački argument.

- **Realistic:** 5–20 kupaca u Srbiji/regionu sa 2–15 prodavnica; konektori za 2–3 najčešća lokalna POS/ERP (izvoz CSV/SQL); istorija sopstvenih ishoda nivelacija kao moat po kupcu.
- **Possible:** anonimizovani benchmark između trgovaca (sell-through po tipu/sezoni, tipičan efekat sniženja) — pravi moat, ali tek sa ≥ 10 kupaca i čistom semantikom.
- **Speculative:** prediktivna optimizacija cena, AI copilot, open-to-buy planiranje na nivou Retalon-a.

Moat sastojci: lokalna semantika (nivelacija, kalkulacija, KEP — POS ih *dokumentuje*, ne *ocenjuje*), sopstvena istorija ishoda akcija, poverenje (trust sloj, ali nevidljiv dok ne zatreba). **Danas moat ne postoji.**

## 15. Konkurencija (reality check)

- Lokalni POS/ERP već nude dimenzije veličina/boja, nivelacije, dnevnu prodaju, maržu, „artikli bez prodaje“, stanje zaliha na dan, naručivanje: [Logosoft SmartPOS](https://www.logosoft.rs/smartpos/), [Logosoft POSitiv](https://www.logosoft.rs/positiv/), [Sverko SVEra (nivelacije po rasteru, OLAP prodaja)](https://svera.rs/moduli-2/), [TiramisuPOS (multi-objekat zalihe, min. količine)](https://tiramisuerp.com/rs/pos-retail), [Konty (izveštaji po lokaciji, Excel uvoz)](https://konty.com/rs/products/retail).
- SMB globalno: [Lightspeed za obuću (size/color forecasting, reorder points, slow movers)](https://www.lightspeedhq.com/pos/retail/shoe-store-pos/), [Lightspeed Analytics dynamic reorder](https://retail-support.lightspeedhq.com/hc/en-us/articles/16090664795035-Inventory-forecasting-with-Lightspeed-Analytics). Enterprise: [Retalon markdown optimization](https://retalon.com/solutions/markdown-optimization-software).
- Zaključak: prodaja/zalihe/dimenzije/nivelacije i standardni izveštaji su **commodity capabilities**. U javnim stranicama lokalnih proizvoda pregledanim 07.10 nisam našao eksplicitno dokumentovan closed-loop „preporuka → izvršenje → izmeren ishod nivelacije“, ali to **nije dokaz da ga nijedan lokalni proizvod nema**. Globalno, Retalon eksplicitno nudi markdown/promotion optimization i impact claims, pa Trendplus diferencijacija nije sama ideja optimizacije nego jednostavniji regionalni workflow, lokalna semantika, explainability i dokaz ishoda na podacima kupca. **SUPPORTED MARKET OBSERVATION, ne ekskluzivnost.**

## 16. Nivelacija deep-dive

| Korak | Stanje | Ocena |
|---|---|---|
| Problem (roba stoji) | Pre-nivelacija prioriteti, 537 kandidata | postoji, heuristika |
| Izbor artikla | weeks-of-cover/starost; retail scope Trend PLUS 1+2 (RQ571) | postoji, nekalibrisano |
| Odluka o % | **ne postoji** preporuka koliko sniziti | MISSING |
| Izvršenje | nivelacija u POS-u (Access); Trendplus je čita | postoji (pasivno) |
| Post prodaja | Pre/Post sa mature windows, overlap, direction (RQ541/543) | lokalno L4, live UNPROVEN |
| Kontrola/baseline | DiD event-aligned (RQ542), ali bez istorije zaliha; selekcioni bias nerešen | slabo |
| Rezultat RSD/marža/obrt | RQ557 deskriptivni ledger | postoji, 0 ishoda u prod |
| Sledeća preporuka | ne uči iz ishoda (RL12 WAITING) | MISSING |

Odgovori: Pre/Post meri **promenu posle događaja, ne efekat**. DiD smanjuje sezonsku pristrasnost samo ako kontrolna grupa liči na tretiranu — kod ~10 pari/dan i snižavanja upravo „mrtve“ robe, ne liči. Ne možemo razlikovati uspešno sniženje od robe koja bi se prodala ionako (nema istorije zaliha, nema slučajne dodele). Minimum-signal pravila štite od najgorih tvrdnji (dobro). Vlasnik razume „pre 2 para/mes, posle 9 para/mes, marža i dalje 18%, ostalo 14 pari“ — ne razume elastičnost. Moat nastaje tek kada Trendplus kaže „sniženja od 20% na ženskim čizmama posle 120 dana kod vas prodaju 60% zalihe za 4 nedelje; 40% ne pomaže više od 20%“ — iz sopstvenih izmerenih ishoda.

## 17. Supplier intelligence

Pouzdano danas (za period do 05.08): promet, komadi, marža i maržni doprinos po dobavljaču (L5). **Ne može pouzdano:** sell-through (nema pouzdane istorije zaliha/ulaza), kapital vezan po dobavljaču (pogrešan trošak), spora roba po dobavljaču (starost = datum importa), „prodaje se samo uz duboko sniženje“ (moguće iz nivelacija + prodaje, ali nije sastavljeno), kandidat za veću/manju porudžbinu (nema). Supplier Report/Hub u prod: `MISSING_OBJECT` (04.10). Potencijalni money-maker: da, ako se popravi trošak + ulaz i dobije jedna strana „argument za pregovore“. Danas: history, ne negotiation.

## 18. Inventory cockpit (samo što podaci mogu podržati)

Uz ispravan trošak i datum ulaza: mrtva roba (0 prodaje N dana), spora roba, dani zalihe, kapital vezan, overstock po tipu/dobavljaču, transfer 1↔2, sezona-kraj rizik (sezona postoji kao atribut — proveriti). **Ne predlagati sada:** stock-out rizik i replenishment forecast (nema istorije zaliha po danu), rupe u veličinama (proveriti izvor), boja (0% podataka).

## 19. Outcome loop

| Korak | Postoji | Stanje |
|---|---|---|
| Signal | da | lokalno dokazan, prod zastareo |
| Preporuka | delimično | PDC 0 primenljivih (04.10); Pre-nivelacija heuristika |
| Akcija | šema da (`AddAnalyticsActionOutcomeTracking` migracija, `AnalyticsActionItemService.cs`) | 0 stvarnih akcija |
| Rezultat | RQ557 ledger, RQ478 denominator | 0 izmerenih |
| Učenje | RL04–RL11 lifecycle, RL12 WAITING | ne postoji |
| Bolja preporuka | — | ne postoji |

**Može li Trendplus danas dokazati da je ijedna preporuka zaradila/sačuvala novac? NE.** Najkraći put: (1) svež import, (2) 20–30 ručno potvrđenih nivelacija upisanih kao akcije sa unapred zapisanim poređenjem, (3) 4 nedelje, (4) jedna stranica ishoda u RSD. To je RQ592.

## 20. North Star metrike

| Metrika | Merljivo danas? |
|---|---|
| **RSD realizovane bruto marže iz robe starije od 120 dana, prodate posle Trendplus-potvrđene akcije** (vs isti artikli pre i kontrolna grupa) | Ne — 0 akcija, zastareli podaci. Merljivo za ~6 nedelja posle RQ592. |
| **Vrednost mrtve zalihe (RSD, nabavna cena) mesec-na-mesec** | Ne — trošak pogrešan. Merljivo posle tačke 2 plana. |
| Udeo prihvaćenih preporuka sa izmerenim pozitivnim ishodom | Ne — N=0. |

## 21. Commercial readiness verdict

**Nije spreman za prodaju. Spreman je za interni pilot kod Ivana kada se vrati svež import.**

- Idealan prvi kupac: lanac obuće/odeće 3–10 prodavnica u Srbiji, sa lokalnim POS-om koji može da izveze prodaju, lager i kalkulacije; vlasnik koji sam radi nivelacije i nabavku.
- **HYPOTHESIS:** početni ICP verovatno ima 2–10 prodavnica i dovoljno lagera da je ručna analiza skupa; prag „30–50 mil. RSD zalihe“ nije tržišno dokazan i ne treba ga koristiti kao hard qualification pre intervjua/pilota.
- Demo problem: „koliko para vam stoji u robi koja se ne prodaje 120+ dana, i koja sniženja su vam prošle sezone stvarno vratila novac“.
- Prve 3 funkcije: zalihe-kapital/mrtva roba; nivelacija → ishod; dobavljač-vrednost jedna strana.
- Ne pokazivati: Boja, Smene, Insight Studio, Pulse, Executive, Operations/trust ekrane, confidence/fingerprint metadata, DiD/elastičnost.
- **HYPOTHESIS za pricing discovery:** ~100–200 €/mes po prodavnici i cilj 5–10× merljivog benefita mogu biti početna test-teza, ali nisu validirana cena ni willingness-to-pay. Pilot/intervjui treba da ih potvrde ili odbace.
- Pricing: fiksno po prodavnici mesečno + jednokratni onboarding; kasnije opcioni success fee na izmerenu mrtvu-zalihu redukciju (zahteva kredibilan outcome loop).
- Onboarding: danas i dalje developer-heavy. Access je dokazani end-to-end pilot put; QDB ima SQL Server discovery/mapping/checkpoint application do `SourceSyncAppliedRows`, ali QDB09 eksplicitno ne upisuje canonical `Artikli/Prodaja`, a QDB07/08 admin/onboarding su WAITING. Tvrdo kodirani/pilot-specifični poslovni policy-ji (`DUG/KOREKCIJA`, `Europe/Belgrade`, retail store scope) takođe moraju postati customer configuration pre ponovljivog onboardinga.
- Šta blokira prodaju drugoj firmi: (1) izvor podataka, (2) Ivan-specifična semantika u kodu, (3) nula dokaza ishoda, (4) previše ekrana za objasniti.

## 22. Queue/prompt usklađivanje (posle audita)

**Već pokriveno — samo referencirati:** STAB16 (svežina/workeri/deploy), RQ454/RQ565/RQ566 (produkciono sravnjenje), RQ545 (Pre/Post live), RQ576 residual (trošak), RQ585 (weekly digest), RQ465 (pregovarački paket), RQ555/RQ556 (markdown izvor/težine), RQ559 (veličine), RL12 (kauzalni gate), RQ455 (customer acceptance).

**Owner priority decision 2026-10-07 (primenjeno u canonical queue-u):**
- RQ585 P3 → **P1**. Repo-zavisnosti `RQ570/573/574/576` su DONE; start gate ostaje production freshness unutar RQ583 SLA. Digest mora da radi i kada size/supplier family nije dostupna — bez inventovanja signala.
- STAB16 ostaje P0, ali je to **jedini** P0; owner = Ivan (provider pristup), ne agenti.
- Previsok prioritet u odnosu na vrednost: P-UI-38 (ratchet gate), P-UI-50, PERF18, RQ319/RQ320 (filter apply semantika), RQ18/RQ25–RQ38 (legacy, predlog OBSOLETE), RQ46/RQ50, RQ558, GAI03–GAI12, MT02–MT12, SEC05.
- RQ589–RQ591 (DONE) kao primer: ne ponavljati sertifikaciju karantinovanih ruta.

**Novi prompt:** `RQ592` — prvi zatvoreni markdown action→outcome pilot. Ostaje P1 i WAITING na obnovljen production freshness + live Pre/Post ugovor, ali **pre-registration ne čeka 14 uzastopnih dana**; 14-dnevni SLA je dokaz stabilnosti kroz pilot, ne razlog da se kasno zabeleži plan. Nije duplikat RQ557/RL12/RQ455.

## The five things I would do if this were my product

1. **Vratio bih svež dnevni import u produkciju i ne bih radio ništa drugo dok `observedSalesPeriodTo` nije juče** (STAB16; Ivan lično, provider pristup).
2. **Ispravio bih nabavnu cenu i datum ulaza, pa pokazao „koliko para stoji u robi 120+ dana“ po dobavljaču i objektu** — prvi broj koji vlasnik ne dobija iz POS-a.
3. **Zatvorio bih jedan markdown loop: 20–30 stvarnih nivelacija iz Trendplus liste, unapred zapisano poređenje, 4 nedelje, jedna stranica ishoda u RSD** (RQ592).
4. **Srezao bih proizvod na ~8 ruta i jednu nedeljnu „šta da uradim“ stranicu (RQ585), i zamrznuo UI/governance/GenAI/MT rad.**
5. **Izvukao bih Ivan-specifičnu semantiku u konfiguraciju i dodao CSV/Excel uvoz, pa uradio demo kod jednog spoljnog trgovca obućom.**

## The single most important next milestone

**„Prvi dokazani dinar“:** svež produkcioni import + ≥ 20 Trendplus-predloženih nivelacija sprovedenih kod Ivana, izmerenih 4 nedelje prema unapred zapisanom poređenju, sa jednom stranicom koja kaže koliko je RSD marže realizovano iz robe koja je stajala — i koliko nije.
