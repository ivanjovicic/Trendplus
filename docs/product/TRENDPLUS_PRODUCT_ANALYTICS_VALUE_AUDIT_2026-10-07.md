# Trendplus — product / analytics / business value audit (2026-10-07)

Osnova: `HEAD == origin/main == cb5ae4dfca00e257c711bf1cb7c764d5eec95019` (box clone, 126 commita posle `242361ca`).
Produkcija (jedan read-only GET, 2026-10-07 ~14:50 Beograd): `GET /api/runtime/version` → `commitSha=194df308…`, build 2026-10-07 10:24 (Beograd), `processType=web`; `GET /api/analytics/refresh-status?dataScope=all` → `observedSalesPeriodToUtc=2026-08-05`, `lastSuccessfulRefreshAtUtc=null`, `lastSuccessfulImportAtUtc=null`, `workersEnabled=false`, `dataFreshnessStatus=unknown`, `cacheMode=in-memory`. Ostali live GET-ovi u ovom auditu nisu izvršeni (automatska bezbednosna provera ih je zaustavila), pa su live tvrdnje ispod koje nisu iz ta dva odgovora preuzete iz datiranih audita i označene datumom.
Ovaj dokument je **dated product snapshot**, ne live router. Routing ostaje u `MASTER_ROADMAP.md` i owner queue-ovima.

## Executive verdict

1. **Trendplus danas nije proizvod koji vlasniku obuće menja odluku — jer radi nad podacima starim 63 dana.** Poslednja prodaja u produkciji je 05.08.2026; nijedan uspešan import/refresh nije zabeležen; workeri ne rade. Sve ostalo u ovom dokumentu je sekundarno dok se to ne reši.
2. **Najveći problem više nije formula.** Osnovni brojevi (promet, komadi, marža po dobavljaču / vrsti obuće / danu) se slažu između ekrana i imaju nezavisne oracle testove (L4/L5 lokalno). To je realan i težak uspeh. Ali to su brojevi koje POS/ERP (Logosoft, Sverko…) već daje.
3. **Nijedna preporuka Trendplus-a nikada nije dokazano zaradila ili sačuvala ni jedan dinar.** U produkciji su u Akcijama bila 4 smoke zapisa i 0 stvarnih akcija (28.09); `measuredSampleSize=0`. Outcome loop postoji kao šema, ne kao dokaz. Business outcome proof = **0/10, UNPROVEN**.
4. **Izvorni podaci ne podržavaju polovinu ekrana.** Boja, kategorija, pol, način plaćanja i sat su 100% „Nepoznato“; „transakcija“ je dnevni dokument, ne račun (25 dokumenata za 307 pari u julu); nabavna cena na artiklu je u pogrešnoj razmeri; zaliha vredi ~26 RSD po paru. Ekrani Boja, Smene, Korpa i vrednost zaliha su zato **prazne ljušture sa savršenim trust metadata**.
5. **Obim podataka je mali: 2 prodavnice, ~300 pari i ~1,56 mil. RSD za 30 dana (jul).** DiD, elastičnost, MA7, winsorizacija i log-ratio nad ~10 pari dnevno daju lažnu naučnost. Za ovu nišu vredi jednostavna, transparentna pravila + iskren „premalo podataka“.
6. **Razvoj je pretežno ceremonija.** Od 126 poslednjih commita ~100 (≈79%) je queue/agents/evidence/roadmap dokumentacija, 22 UI polish, 1 perf merenje, **0 novih poslovnih mogućnosti**. Queue fajlovi imaju ~70.000 linija, 854 registrovana prompta, 897 run-evidence fajlova. Ovo optimizuje dokazivost rada agenata, ne vrednost za kupca.
7. **Tri modula su potencijalni razlog za plaćanje:** (a) markdown/nivelacija ciklus (šta sniziti → da li je uspelo), (b) zalihe kao kapital (mrtva/spora roba po dobavljaču, rupe u veličinama), (c) dobavljač kao pregovarački argument (marža × obrt × koliko kapitala drži). Sva tri su danas **HV/LC**: vredni, ali bez svežih podataka, bez ispravne nabavne cene i bez ijednog izmerenog ishoda.
8. **Ne sme se prodavati kao „optimizacija“.** Danas je to, u najboljem slučaju, dobro proveren deskriptivni izveštaj za jednog trgovca (Ivan), vezan za Access, Europe/Belgrade, `DUG/KOREKCIJA` i objekte „Trend PLUS 1/2“.
9. **Najvažniji sledeći korak:** vratiti svež import u produkciju i za 6 nedelja zatvoriti JEDAN loop: 20–30 stvarnih nivelacija koje je Trendplus predložio, sprovedene, i izmerene prema unapred zapisanom poređenju. Do tada: stop novim trust/UI/queue slojevima.

Sažete ocene: **CURRENT PRODUCT VALUE 3/10 · CURRENT ANALYTICS TRUST 5/10 (lokalno 7, produkcija 3) · CURRENT BUSINESS OUTCOME PROOF 0/10 · COMMERCIAL READINESS 2/10 · POTENTIAL IF EXECUTED WELL 7/10.**

---

## 1. Stvarno stanje (osveženo, ne iz starih audita)

| Činjenica | Dokaz | Nivo |
|---|---|---|
| HEAD = origin/main = `cb5ae4df` | `git rev-parse` | — |
| Deployed API = `194df308`, 107 commita iza HEAD, ali svi osim `14690775` (perf merenje) su docs/UI | `git log 194df308..HEAD -- Api Application Infrastructure Domain Database` → samo `14690775` | L6 (SHA) |
| `d4ae1b9b` (stale `vw_vendor_sales_nivelacija` fix) **jeste u deployed SHA** | `git merge-base --is-ancestor d4ae1b9b 194df308` = true | L6 za SHA; **Pre/Post live odgovor posle deploya: UNPROVEN** (nije proveren u ovom auditu; zadnji poznati live = `contract_missing` 04.10, RQ545 PARTIAL) |
| Podaci staju 05.08.2026; nema uspešnog refresh/importa; workeri off; in-memory cache | live `refresh-status` 07.10 | L6 |
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
| Differentiation | 2 | Danas ništa što POS/ERP + Excel ne daje, osim trust sloja koji kupac ne plaća. |
| Time-to-value (novi kupac) | 1 | Samo Access/Windows import, Ivan-specifična semantika, prazan `DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` (0 linija). |
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
| Product Decision | L3 | RQ573/RQ574 popravili rang | kategorija 0% → blocker na svakom redu | stara | parcijalno | 0 primenljivih (04.10) | potencijalno visoka | L0 |
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
| **High value** | *(prazno danas)* — samo istorijski promet/marža po dobavljaču; to je vrednost koju POS već daje | **Markdown ciklus, Zalihe-kapital, Dobavljač-vrednost, Actions/outcome, svežina.** Ovde ide skoro sav trud: podaci, nabavna cena, produkciono sravnjenje, prvi izmereni ishod. |
| **Low value** | Prodaja po vrsti obuće/dobavljačima/dnevna kao tabele, trust header, Operations integrity ekrani, UI ratchet testovi → **zadržati, ne ulagati** | Boja, smene, korpa, Insight Studio, forecast/scenario, Pulse, GenAI, MT, embedding/ONNX/RabbitMQ → **stop / defer / sakriti** |

Najvažnija istina matrice: **HV/HC kvadrant je prazan.** Sve što je skupo dokazano je jeftino po vrednosti; sve što je vredno nije dokazano.

## 7. Top business opportunities

1. **Mrtva i spora roba kao kapital u RSD po dobavljaču i objektu** (pare koje stoje, starost od poslednjeg ulaza, ne od importa). Najčešća odluka (nedeljno), visok finansijski uticaj, reverzibilno, Excel to teško radi preko 12k artikala. Zahteva ispravnu nabavnu cenu (RQ576 residual) i ispravan datum ulaza.
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

- **Queue ceremonija (kvantifikovano):** `docs/ai/*QUEUE*.md` ≈ 70.384 linija; `ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` sam 30.044; 854 prompta sa statusom (759 DONE); 897 fajlova u `.ai/runs/`; 153 u `docs/qa/`; 348 `.md` u `docs/`; 35 `.md` u rootu. Od 1.187 commita od 01.09: `docs(queue)` 217, `docs(analytics)` 138, `docs(evidence)` 97, `chore(queue)` 37, `docs(agents)` 44 → ~530 dokumentacionih vs 17 `feat(analytics)`. Poslednjih 126: ~100 governance. Header jednog queue fajla je paragraf od ~1.500 reči istorije claim/close. Ovo je trošak koordinacije više agenata, ne proizvod.
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
| `docs/product/PRODUCT_VISION.md`, `docs/roadmaps/BUSINESS_ROADMAP.md` | CURRENT po nameri, LOW VALUE po sadržaju | Nemaju north-star metriku ni outcome cilj. |
| `README.md` | **STALE / impresivniji od proizvoda** | „production-grade“, RabbitMQ/Redis/pgvector/ONNX; ne pominje da je jedini izvor Access i da su podaci stari. |
| `docs/qa/ANALYTICS_REAUDIT_2026-10-04.md`, `ANALYTICS_ACCURACY_AUDIT_2026-10-05.md` | USEFUL HISTORICAL EVIDENCE | Najkorisniji dokazi u repou. |
| `docs/qa/ANALYTICS_AUDITS_RECERTIFY_{,2,3,4}_2026-10-05.md`, ROUND3–ROUND15 prompt fajlovi | DUPLICATE / CANDIDATE FOR CONSOLIDATION | 4 recertifikacije u jednom danu; 13 „round“ fajlova. |
| `docs/qa/BACKEND_CI_*_2026-08-1x*.md` (15 fajlova) | HISTORICAL → ARCHIVE | |
| `docs/ai/NEXT_PROMPT_QUEUE.md` | HISTORICAL (deklarisano) | |
| `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` | **prazan fajl (0 linija)** dok je onboarding drugog kupca najveći komercijalni blokator | |
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

1. **Svež import u produkciji (STAB16 + import put).** WHY NOW: bez ovoga sve je istorija. Vrednost: maksimalna. Radimo: utvrditi zašto Access import ne stiže (provider pristup koji Ivan ima), pokrenuti worker ili ručni dnevni import, dokazati `lastSuccessfulImportAtUtc` < 48 h. NE radimo: nove trust slojeve. Metrika: `observedSalesPeriodTo` ≥ juče, 14 dana zaredom. DoD: live `refresh-status` + Supplier/Daily brojevi za poslednjih 7 dana sravnjeni sa POS dnevnim izveštajem (RQ454/RQ565).
2. **Ispravna nabavna cena i datum ulaza → zalihe u RSD.** WHY NOW: bez toga nema mrtve robe, kapitala ni marže po artiklu. Radimo: potvrditi razmeru troška na 20 artikala ručno sa kalkulacijom; ispraviti mapiranje u importu. NE: novi inventory ekrani. Metrika: vrednost zalihe u ±5% od knjigovodstvenog lagera. DoD: tabela 20 artikala trošak Trendplus = trošak kalkulacija.
3. **Prvi zatvoreni markdown loop (novi RQ592).** WHY NOW: jedini put do dokaza vrednosti. Radimo: Ivan bira 20–30 artikala iz Pre-nivelacija liste, zapisuje akciju u Actions (datum, artikal, objekat, stara/nova cena), unapred zapisujemo poređenje (isti artikli 4 nedelje pre, i kontrolni artikli istog dobavljača/tipa koji nisu sniženi), merimo za 4 nedelje: pari, realizovana marža RSD, preostala zaliha. NE: kauzalni model, elastičnost. Metrika: N akcija sa izmerenim ishodom ≥ 20; izveštaj „RSD marže realizovano iz robe koja je inače stajala“. DoD: jedna stranica ishoda koju vlasnik razume.
4. **Pre/Post i Supplier Report proveriti uživo posle `d4ae1b9b` deploya.** WHY NOW: kod je deployovan, dokaz nije. Radimo: read-only GET + browser, zatvoriti RQ545 ili ga precizno re-otvoriti; Supplier MV kreirati/osvežiti (STAB16). Metrika: `contract_missing`/`MISSING_OBJECT` = 0. DoD: evidence fajl sa live odgovorima.
5. **Jedna „ove nedelje“ stranica (RQ585, podignut prioritet).** Max 10 stavki: mrtva roba za sniženje, transfer 1↔2, best-seller rupa, dobavljač za razgovor. Sa linkom na dokaz. NE: nova Board logika. Metrika: Ivan je koristi 4 nedelje zaredom. DoD: 4 nedeljne verzije sa zapisanom odlukom po stavci.
6. **Sakriti ili degradirati prazne ekrane (Boja, Smene, Pulse, Executive) i ažurirati README.** Mali rad, veliko smanjenje šuma. Metrika: ≤ 8 ruta u glavnoj analitičkoj navigaciji.
7. **Zamrznuti queue ceremoniju:** jedna stranica „product status“ (ovaj dokument + nedeljni update) umesto paragraf-istorije u headerima. Metrika: < 20% commita su governance.
8. **Demo za drugog vlasnika obuće** (posle 1–3): 20 minuta, njihov problem = „koliko para mi stoji u robi koja se ne prodaje i šta da snizim“.

## 13. Plan 2–6 meseci (redosled i zašto)

1. **Zalihe-kapital cockpit** (mrtva/spora roba, dani zalihe po dobavljaču/tipu/objektu, kapital u RSD, transfer predlozi) — najčešća i najlakše merljiva odluka; deterministička pravila.
2. **Markdown intelligence v2** — iz izmerenih ishoda RQ592 kalibrisati jednostavna pravila (npr. „starost > 120 dana i < 2 para/mes → 20%“); elastičnost tek kad bude ≥ 50 mature događaja po grupi.
3. **Supplier value + pregovarački paket** pred sezonsku porudžbinu (marža × sell-through × kapital × % prodato tek uz sniženje).
4. **Outcome learning** — RL12 iz dokumenta u minimalni runtime: unapred registrovano poređenje, ne ML.
5. **Import/onboarding kvalitet** — konfigurabilna semantika (`DUG/KOREKCIJA`, vremenska zona, objekti, šta je obuća) po kupcu; CSV/Excel uvoz lagera, prodaje i kalkulacija kao drugi izvor (danas samo Access). Popuniti prazan connector queue.
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
- Zaključak: prodaja po dobavljaču/tipu/boji/danu je **commodity**. Niko od lokalnih ne tvrdi da *meri da li je nivelacija uspela* niti da rangira mrtvu robu po kapitalu i dobavljaču — to je jedini prostor, i Trendplus ga danas ne pokriva dokazano.

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
- Minimalno: 2–3 prodavnice i ≥ 30–50 mil. RSD zalihe (inače Excel pobeđuje).
- Demo problem: „koliko para vam stoji u robi koja se ne prodaje 120+ dana, i koja sniženja su vam prošle sezone stvarno vratila novac“.
- Prve 3 funkcije: zalihe-kapital/mrtva roba; nivelacija → ishod; dobavljač-vrednost jedna strana.
- Ne pokazivati: Boja, Smene, Insight Studio, Pulse, Executive, Operations/trust ekrane, confidence/fingerprint metadata, DiD/elastičnost.
- Potreban benefit: da bi ~100–200 €/mes po prodavnici bilo lako opravdano, proizvod mora pokazati ≥ 5–10× (npr. ≥ 150–300k RSD/mes realizovane marže iz oslobođenog kapitala za lanac od 3–5 prodavnica). **UNPROVEN.**
- Pricing: fiksno po prodavnici mesečno + jednokratni onboarding; kasnije opcioni success fee na izmerenu mrtvu-zalihu redukciju (zahteva kredibilan outcome loop).
- Onboarding: danas težak (nedelje, developer). Samo Access/Windows import (`Api/Services/AccessImportService.cs`, `Api/Services/Access/WindowsAccessSession.cs`); tvrdo kodirani `DUG/KOREKCIJA` (`SalesReceiptPopulationPolicy.cs`), `Europe/Belgrade` (`BelgradeCalendarDatePolicy.cs`), retail scope po imenima objekata (RQ571); prazan connector queue.
- Šta blokira prodaju drugoj firmi: (1) izvor podataka, (2) Ivan-specifična semantika u kodu, (3) nula dokaza ishoda, (4) previše ekrana za objasniti.

## 22. Queue/prompt usklađivanje (posle audita)

**Već pokriveno — samo referencirati:** STAB16 (svežina/workeri/deploy), RQ454/RQ565/RQ566 (produkciono sravnjenje), RQ545 (Pre/Post live), RQ576 residual (trošak), RQ585 (weekly digest), RQ465 (pregovarački paket), RQ555/RQ556 (markdown izvor/težine), RQ559 (veličine), RL12 (kauzalni gate), RQ455 (customer acceptance).

**Predložene korekcije prioriteta (nisu primenjene — traže vlasničku odluku):**
- RQ585 P3 → **P1** (jedina „šta da radim“ površina koja može da zameni tri postojeće).
- STAB16 ostaje P0, ali je to **jedini** P0; owner = Ivan (provider pristup), ne agenti.
- Previsok prioritet u odnosu na vrednost: P-UI-38 (ratchet gate), P-UI-50, PERF18, RQ319/RQ320 (filter apply semantika), RQ18/RQ25–RQ38 (legacy, predlog OBSOLETE), RQ46/RQ50, RQ558, GAI03–GAI12, MT02–MT12, SEC05.
- RQ589–RQ591 (DONE) kao primer: ne ponavljati sertifikaciju karantinovanih ruta.

**Novi prompt:** `RQ592` — prvi zatvoreni markdown action→outcome pilot (WAITING na STAB16/svežinu). Nije pokriven: RQ557 je deskriptivni ledger bez akcija, RL12 je samo dokument, RQ455 je opšti acceptance. Materijalan, jasan acceptance, dokaz problema: `measuredSampleSize=0`, 4 smoke akcije.

## The five things I would do if this were my product

1. **Vratio bih svež dnevni import u produkciju i ne bih radio ništa drugo dok `observedSalesPeriodTo` nije juče** (STAB16; Ivan lično, provider pristup).
2. **Ispravio bih nabavnu cenu i datum ulaza, pa pokazao „koliko para stoji u robi 120+ dana“ po dobavljaču i objektu** — prvi broj koji vlasnik ne dobija iz POS-a.
3. **Zatvorio bih jedan markdown loop: 20–30 stvarnih nivelacija iz Trendplus liste, unapred zapisano poređenje, 4 nedelje, jedna stranica ishoda u RSD** (RQ592).
4. **Srezao bih proizvod na ~8 ruta i jednu nedeljnu „šta da uradim“ stranicu (RQ585), i zamrznuo UI/governance/GenAI/MT rad.**
5. **Izvukao bih Ivan-specifičnu semantiku u konfiguraciju i dodao CSV/Excel uvoz, pa uradio demo kod jednog spoljnog trgovca obućom.**

## The single most important next milestone

**„Prvi dokazani dinar“:** svež produkcioni import + ≥ 20 Trendplus-predloženih nivelacija sprovedenih kod Ivana, izmerenih 4 nedelje prema unapred zapisanom poređenju, sa jednom stranicom koja kaže koliko je RSD marže realizovano iz robe koja je stajala — i koliko nije.
