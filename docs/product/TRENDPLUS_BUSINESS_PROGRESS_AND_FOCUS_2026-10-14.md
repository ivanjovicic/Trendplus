# Trendplus — poslovni napredak i odluke za sledeći rad (14.10.2026)

**Vrsta dokaza:** repository/product audit; baza provere `7b90450b2d1f8ca9b2b39b07b5fca4bbfd50cd01`. Ovo nije novo merenje Render/Neon/Vercel produkcije ni dokaz da je kupac ostvario profit. Stariji podaci ostaju legitimni za **istorijsku** analizu; nisu validacija preporuke „šta uraditi danas“.

**Izvori, a ne drugi backlog:** `docs/product/TRENDPLUS_PRODUCT_ANALYTICS_VALUE_AUDIT_2026-10-07.md`, `docs/qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md`, `docs/qa/MARKDOWN_PILOT_SHADOW_CONTROLS_RQ612_2026-10-11.md`, `docs/qa/ANALYTICS_FRONTEND_UX_AUDIT_2026-10-14.md`, postojeći RQ owner queue i `docs/roadmaps/BUSINESS_ROADMAP.md`. Kanonske poslovne definicije su u `docs/ai/ANALYTICS_BUSINESS_GLOSSARY.md`.

## Executive assessment: od 7. do 14. oktobra

| Dimenzija | 07.10. audit | Procena 14.10. | Vrsta promene |
| --- | ---: | ---: | --- |
| Tačnost osnovne analitike (lokalni ugovori) | 7/10 | ~8/10 | code + CI: source/period, missing-vs-zero, smene, konzistentnost |
| Regresiona/test zaštita | ~6,5/10 | ~8,5/10 | analitički PG testovi, 1.225 frontend testova u RQ668, browser/CSV fixture CI |
| UX jasnoća | 6/10 | ~7/10 | RQ659–663, 668: uklonjen lažni Home, upozorenja, naslov, dostupnost |
| Kvalitet *izvornih* poslovnih podataka | 2/10 | ~3/10 | proverena lokalna podudarnost, ali nepopunjena boja/veličina/sat, originalni troškovi |
| Aktuelnost produkcionih podataka | 2/10 | **2/10, nepotvrđeno poboljšanje** | nema novog approved production provider dokaza u ovom auditu |
| Korisnost za operatera (strukturno) | 3/10 | ~4,5/10 | postojeći digest/akcije/izvozi bolje ograničeni; operator još nije potvrđen |
| Izmereni poslovni efekat | 0/10 dokaza | **0/10 dokaza** | nijedan dokazani izvršeni/izmereni outcome nije pronađen |
| Komercijalna spremnost | 2/10 | ~2,5/10 | demonstracija je sigurnija, ali nije dokazana aktuelnost i kupovni razlog |

Brojevi su **subjektivne uporedive ocene**, nisu industrijski benchmark ni linearna mera napretka. Test score je indikativan (nema merenog 07.10 referentnog skora), ne audit sertifikat.

### Šta je stvarno dobilo vrednost

1. **Tačan osnovni promet i poreklo:** batch #23 je dokazao 5.530/5.530 izvora računa i 67.092/67.092 stavki u facts, bez odstupanja na uparenim količinama/cenama/iznosima. To **nije** 100% ispravnost svih ekrana ni dokaz da su to jedinstvene customer basket porudžbine.
2. **Istinitost prikaza:** RQ593–RQ596, RQ605–RQ612, RQ657–RQ668 sprečavaju lažni novac, lažno merene smene i stare filtere, povezuju izveštaj/izvoz/test. Ugrađen je Puppeteer synthetic Supplier/Shoe CSV prove-out, i zaista se izvršava u frontend Quality Gates, ali nije raw DB→deployed browser sertifikat.
3. **Poslovna hijerarhija:** postojeći owner digest (RQ585 DONE), dobavljački paket i pre/post izveštaji stvaraju početni put od „šta vidim” ka „šta da proverim”, bez inventovanja nove analitike.

### Šta nije rešeno

- Poslednji eksplicitno zabeležen prodajni horizont u produkciji (07.10) je **05.08.2026**. Nije sprovedena nova nezavisna live provera u ovoj sesiji. STAB16 i RQ454/RQ565 ostaju pristup/produkciona istina.
- RQ601: **501 stavka / 1.711.050 RSD prodaje sa nepoznatim troškom** u historijskom batch #23, dok 20 originalnih kalkulacija još nedostaje. Čak ni ostale numerički prisutne nabavne cene nisu automatski proverene originalnim dokumentom.
- RQ610: `Artikli` ima store kolone, ali import/POS/transfer/manual quantity promene nemaju dokazano zajedničku immutable generation/observed-stock seriju. Ne prikazivati istorijski on-hand, tačan OOS duration ni potvrđen kapital po starosti na osnovu današnjeg stanja.
- RQ602/RQ612: postoji 20 tretiranih + 20 kontrolnih istorijskih zapisa, ali **0 potpuno kvalifikovanih parova** zbog nedokazane istorijske dostupnosti i potpunosti po prodavnici i danu. Nema causal uplift/ROI.
- Nepoznate boja/veličina/sat ili neproveren receipt-grain ne smeju postati punopravni KPI samo zato što postoje ekrani i regresioni testovi.
- Nema posmatranog rada kupca, vremena do odluke, razumevanja upozorenja, owner potvrde akcije ni izmerenog outcome-a.

## Odluke vlasnika proizvoda za sledeći ciklus

**D1. Fokus na tri vrednosti, ne još ekrana.** Primarni proizvod je: (a) pouzdan promet po danu/dobavljaču/tipu, (b) najnovija istinita zaliha i ograničena analiza vezanog kapitala, (c) opravdan pregled i praćenje konkretne nivelacije. Supplier paketi služe pregovoru, ne univerzalnoj neproverenoj listi najboljih dobavljača. Bez novih dashboarda, AI ili marketplace-a.

**D2. Dva aktivna developerska zadatka maksimalno.** Prvo `RQ666` (Product Decision dataset/action identitet) i nezavisni `RQ667` (Inventory filteri); svaki mora da spreči pogrešnu **poslovnu akciju**, ne samo da oboji UI. Pre sledećeg feature work-a proveriti da li ga postojeći ekran već podržava. Agent ne preuzima drugi zadatak dok prvi nije isporučen ili jasno predat.

**D3. Zamrznuti P3 kozmetiku i generalno čišćenje.** `RQ669` već čeka odluku i eksperimentalni Insight Studio ostaje van aktivnog proizvoda. `RQ670` se vraća na WAITING dok se ne suzi isključivo na primarne pilot ekrane ili operator dokaže blokadu. `RQ671` kao wholesale 120-error lint kampanja prelazi na WAITING; neposredno opasne React hooks/refs greške popravljati *u okviru postojećeg P1 ownera* kada reprodukcija to zahteva, bez masovnog refaktora. Ne predstavljati „0 lint grešaka” kao zamenu za korisničku vrednost.

**D4. Zamrzavanje ne znači zanemarivanje bezbednosti.** P0 regresija novca, curenje između `dataScope`/tenanta, neautorizovana akcija ili nepravilna source/status atribucija preuzima prioritet. Dokumentacija/CI izmena koja sprečava dokazani false-pass i dalje je dozvoljena.

**D5. Prva poslovna validacija je posmatrani tok odluke, ne još jedan audit.** Koristiti postojeći `docs/pilot/ANALYTICS_PILOT_OPERATOR_RUNBOOK.md`, `docs/roadmaps/BUSINESS_ROADMAP.md` i owner digest RQ585. Organizovati **jednu** 20-minutnu sesiju sa stvarnim trgovcem/operaterom: poslednji pouzdani period → promet/dobavljač → zaliha → kandidat nivelacije/akcija ili razlog blokade → proverljiv izvoz. Posmatrač beleži da li je korisnik sam uspeo, vreme do konkretne odluke, 3 glavne nedoumice, postoji li dobar *poslovni* razlog za akciju, i da li je korisnik spreman za drugi sastanak/plaćeni pilot. Bez operatora sesija je NOT RUN, ne PASSED. Istorijski period je dozvoljen za usability test uz jasno vidljiv „as-of”; ne za kupovinu zaliha danas.

**D6. Za merenje prvog efekta koristiti RQ592, ne novi model.** Nakon potvrđenog svežeg izvora i vlasničke dozvole zamrznuti kohortu *pre akcija*, po `MARKDOWN_PILOT_PREREGISTRATION_TEMPLATE.md`. Čuvati posmatrani ishod, uzročno tvrđenje samo kad uslovi uporedne grupe prođu. Nema izvršavanja cena od strane agenta.

**D7. Pored koda, vlasnik mora da pribavi 3 nedostajuća dokaza:** ovlašćen read-only / deployment pristup za STAB16 i RQ448/565; originalnih 20 kalkulacija RQ601; imenovanog korisnika za operator walkthrough. Dok oni ne postoje, repo-lokalni agenti ne troše sate na nove ponovljene „zero READY” izveštaje.

## Poslovni milestones, ne broj promptova

| Sledeća isporuka | Potreban trag | Šta ne predstavlja |
| --- | --- | --- |
| **A — Bezbedan filter/akcija** | RQ666/667 failing-first test, exact main CI, ista/različita populacija, nepromenjen historijski action ledger | Komercijalna vrednost |
| **B — Jedna stvarna operator sesija** | Datum, user role, koraci, as-of period, odluka ili `blocked`, razumljivost 0–3 (praktična rubrika), trajanje i sledeći korak | Dokazan novčani rezultat |
| **C — Aktuelnost i trošak** | Deployed SHA, refresh/import success, izvor/servis, local-or-live provenance, owner kalkulacija; neuspeh ostaje `not verified` | Svežina samo zato što je CI zelen |
| **D — Prvi posmatrani ishod** | Pre-reg akcija, stvarno sprovedena izmena, post-period, coverage, eventualni validan control, originalna dokumentacija i RQ592 ledger | Causal „ušteda/profit” bez kontrole |

## Operativno pravilo za razvoj

- **WIP ≤2** vlasnički nepoklapajuća feature/risk zadatka; **jedan business/operator proof track** van developerskih READY statusa.
- Agent sme da pravi novu dokumentaciju samo ako predstavlja **novu poslovnu odluku**, pravni/izvorni ugovor, proverljivu regresiju/CI dokaz ili stvarno promenjeno routing stanje. Inače update-ovati postojeći kanonski fajl i linkovati stari evidence.
- Razlikovati četiri broja: `tests passed`, `source lines reconciled`, `operator decisions confirmed`, `measured financial outcomes`. Nijedan nije zamena za sledeći.
- Pre svake nove RQ registracije autor navodi: **jedna stvarna odluka koju korisnik dobija**, source/provenance, failure case, postojeći owner, expected test i zašto nije već DONE. Nedostatak jednog od tih odgovora = ne kreirati novi prompt.
- Zadatak zatvoriti sa maksimalno **jednim kratkim evidence logom** + linkovima ka CI; nema novog audita audita niti prepakivanja istog nalaza u 5 addenduma.
- Izdvojiti lokalnu istinitost istorijskih formula od live currentness. Provider blokada ne zaustavlja lokalne regresije; lokalni uspeh ne promovise production gate.

## Dva gotova agent prompta — izvršavati iz kanonskog queue-a

### Agent A — RQ666 (primarni)

Pročitaj `AGENTS.md`, `MASTER_ROADMAP.md`, `PROMPT_QUEUE_PROTOCOL.md` i `RQ666` u post-import queue-u. Osveži origin/main, proveri remote PR i aktivne rezervacije; objavi shared READY→IN_PROGRESS claim. Napravi failing-first test za isti Product Decision action `sourceKey` pod `imported` i `existing`, proveri postojeći API idempotency/legacy storage. Obezbedi da korisnik ne vidi tuđu odluku kao `već dodato` i da filter prodavnice ne pređe u drugi izvor; očuvaj istu izvorno opravdanu akciju i istorijsku evidenciju. Izmeni samo potvrđenu grešku, izvrši fokusirane/API testove, guardrail/build i isporuči na main. STOP ako bi bila potrebna destruktivna migracija stare akcije ili neodobrena poslovna politika.

### Agent B — RQ667 (nezavisan)

Pročitaj ista kanonska uputstva i `RQ667`. Nakon nezavisne shared rezervacije testiraj prebacivanje `dataScope` na Inventory pri već izabranoj prodavnici i poređenju više prodavnica; dokaži da neprovereni ID-jevi nisu poslati na API, u export ili u predlog akcije. Sačuvaj shared store ID samo ako novi source to potvrdi, i napravi failure/retry i signed-ID counterexamples. Ne menjaj nabavnu cenu, stock-history, RQ610, poslovne formule ili ProductDecisionCenter fajlove. Isporuka = minimalni kod/test, redovni guardrails/build, main CI, jedan dokaz.

### Business owner — operator rehearsal (nije agent READY)

Koristi postojeći runbook i playbook; ne generiši lažni razgovor niti lažne korisničke odluke. Pronađi trgovca koji je spreman 20 minuta da demonstrira svoju nedeljnu odluku; pripremi istorijski period i 3 konkretna pitanja za svaki od: promet, dobavljač, zaliha/nivelacija. Posmatraj *bez vođenja*, zabeleži gde ne veruje podacima, gde se izgubi, koja informacija nedostaje, i da li bi za pregled/rešenje platio. Rezultat uneti kao stvarnu evidenciju/anonimizovan sažetak; `NOT RUN` dok susret nije obavljen.

## Ponovni pregled

Posle RQ666/RQ667 i prve stvarne operator sesije, uporediti da li postojeći ekran omogućava odluku bez dodatnog razvoja. Ako ne, registrovati **najviše jedan** novi P1 prompt sa stvarnim reprodukovanim trenjem i izmerljivim acceptance uslovima, umesto sledeće velike liste AI-audita.

## Poslovni proof pack za prvi rezultat (14.10.2026)

Odluka: prvo validirati **jednog stvarnog korisnika, jedan izvor i tri korisne odluke**, umesto trećeg paralelnog developer zadatka ili novog dashboarda. Izvršiv [formular od 20 minuta](../pilot/RETAIL_OPERATOR_VALUE_SESSION_2026-10-14.md) sadrži kriterijume, evidence zapis i tri delegabilna prompta: OP-PREP, COST-INPUT, PROVIDER-INPUT. Nijedan nije novi RQ niti sme da preglasa RQ666/RQ667.

**Ponuda za proveru:** datirani read-only pregled prometa, dobavljača i sporih zaliha sa jasnim uslovima izvora; ne "AI optimizer" niti ROI. Prvo dokazati samostalnost trgovca i razliku u odnosu na POS/Excel. Ako je problem samo nejasan ekran, registrovati *jedan* fokusiran P1 posle stvarnog posmatranja. Ako nedostaju originalne kalkulacije ili aktuelni provider dokaz, od vlasnika tražiti upravo te ulaze, a ne praviti novi generički audit.

**KPI razvoja držati odvojeno:** izvršeni testovi, izvorno potvrđeni redovi, stvarne korisničke odluke, izmereni poslovni ishodi. Prva dva mogu biti visoka dok poslednja dva nisu dokazani. Dokumentacija ne sme da sabira ove kategorije kao "procent završenosti proizvoda".
