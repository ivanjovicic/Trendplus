# Supplier analitika — dubinski audit i promptovi (FIX / PROVE / IMPROVE / ENHANCE)

Datum: 2026-09-30
Queue owner: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
Queue: `direct-user-request`
Površina: `/analytics/supplier` — Pregled (`SupplierSalesStatsPage`), Skorkarta (`SupplierDecisionHubPage`), Asortiman (`SupplierFootwearAnalyticsPage`), shell `SupplierConsolidatedPage`, zajedničko stanje, servisi i backend (endpointi, SQL, view-ovi, materijalizovani view-ovi, engine-i).
Audited SHA: `origin/main` `278d37b93356aa7f2011ae5d02bf7e7cf1f801ed`. Ponovo provereno na `8b4e1fc1c2ed89cff450f2be640ca98fecbfd342`: između ta dva SHA nema izmena u auditovanim supplier fajlovima (diff dira samo navigaciju i docs), pa reference `file:line` važe na oba.
Prethodni auditi: `docs/ai/PRODUCTS_SUPPLIER_AUDIT_PROMPTS_2026-09-25.md` (PS01–PS18), `docs/ai/SUPPLIER_DECISION_HUB_AUDIT_PROMPTS_2026-09-22.md` (RQ401–RQ405, RQ458/RQ459).

## Second-pass verifikacija na current main (2026-09-30)

Ova sekcija je autoritativna korekcija prvog prolaza. Supplier runtime fajlovi iz audita nisu menjani između 8b4e1fc1 i second-pass verifikacije; promene do ovog prolaza bile su u dokumentaciji i nepovezanom Daily Sales kodu, pa većina kodnih nalaza i dalje važi.

- **Potvrđen kodni defekt:** N01. MV capability check preko information_schema.columns nije ispravan za PostgreSQL materialized-view kolone; RQ518 je P1/READY.
- **Potvrđen lifecycle rizik, live uzrok nije dokazan:** N02/N03. 014/016 zaista imaju DROP ... CASCADE i readiness/history redosled može ostaviti privremeno/nepopravljeno stanje, ali kasniji rebuild postoji. RQ519 prvo zahteva dokaz i idempotentnost.
- **Potvrđena semantička greška:** N04-N09. Assortment pre/post markdown efekat se prosleđuje shared PoP engine-u, maturity/no-post/zero-baseline i vendor aggregate semantike imaju stvarne nedoslednosti. RQ520 to odvaja bez izmišljanja novih buying pragova.
- **Potvrđeni scorecard input bugovi:** N11/N12 i receipt-population drift N19. N13/N15/N16/N21 su model/policy pitanja, ne automatski bugovi; promene pragova/težina/normalizacije su owner-gated u RQ531.
- **Hipoteze ostaju hipoteze:** N18, N20, N25, N27 i uzrok Pregled 503. Dokaz ide kroz RQ524 ili postojeće owners.
- **N26 je spušten:** Analytics 015 SQL ima problematičan CREATE MATERIALIZED VIEW CONCURRENTLY i transakciono osetljiv CREATE INDEX CONCURRENTLY, ali current initializer ga ne izvršava u startup sekvenci; RQ525 ga tretira kao optional-script hygiene/test, ne startup root cause.
- **C22 je FIXED:** RQ488 je već lokalizovao shared recommendation engine. Odvojeni residual 'Preporuka je gated.' u AnalyticsTrustHeader ostaje.
- **C15/C32 potvrđeni:** focused Supplier share se frontend projekcijom ponovo računa nad vidljivim redom; revenue-rank bedž ne proverava sort smer.
- **N36 nije automatski aritmetički bug:** signed maržni doprinos može matematički dati udeo >100% uz negativne doprinose drugih dobavljača. Denominator mora biti eksplicitan ili owner mora odobriti novu semantiku.
- **N38 je enhancement gap**, ne dokaz postojećeg numeričkog baga.

Queue de-dup: SA-F5 nije dobio novi RQ (safe-error/readiness ide u RQ474/RQ475, Analytics 015 u RQ525, security handoff samo ako se N27 dokaže); SA-I1 je takođe de-duplikovan u RQ474/RQ475. Preostalih 15 promptova su RQ518-RQ532. RQ517 ostaje postojeći DONE Daily Sales prompt.

## Sažetak

Sva tri taba su u produkciji i dalje bez upotrebljivih podataka. Live audit od 2026-09-30 je to potvrdio:

- Pregled vraća HTTP 503.
- Skorkarta za svaki prozor prijavljuje „nije spreman“.
- Asortiman prikazuje samo „nema potvrđen ugovor“.

Kod pokazuje jedan potvrđen uzrok (Skorkarta) i dva jaka kandidata koji zahtevaju runtime dokaz (Asortiman i Pregled):

1. **Skorkarta — potvrđen defekt koda (visoka pouzdanost).**
   - Endpoint proverava kolone materijalizovanih view-ova preko `information_schema.columns` (`Api/Endpoints/SupplierDecisionHubEndpoints.cs:2820-2888`).
   - PostgreSQL ne izlaže materijalizovane view-ove u `information_schema`. Zato je `*_has_required_columns` uvek `false`, pa `HasDecisionScoreCacheForWindow` (`:2788-2793`) uvek vraća `false`.
   - Posledica: `MISSING_SCHEMA` se baca za svaki prozor (`:2461-2468`), čak i kada MV postoje.
   - Startup provera istovremeno koristi `pg_class.relkind = 'm'` (`Infrastructure/Seed/DatabaseInitializer.cs:596-610`) i u logu prijavljuje da su MV „present“. Postoje dve istine o istom objektu.
2. **Asortiman — verovatan uzrok u redosledu startup SQL-a (Hipoteza).**
   - `Database/Migrations/014_FixNivelacijaViewsFromDnevnik.sql:42-44,174` bezuslovno radi `DROP ... CASCADE` i pravi `vw_vendor_sales_nivelacija` bez kolone `change_percent_revenue_semantic`.
   - `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql` (jedini skript sa tom kolonom) se preskače po hash istoriji, osim ako provera pre toga (`DatabaseInitializer.cs:891-905`) nije već videla da kolona fali.
   - Endpoint tada vraća `vendor_sales_nivelacija_contract_missing` (`AllEndpoints.cs:3955-3972`).
   - `DROP ... CASCADE` na tom view-u i na `vw_nivelacija_did` (`016_AnalyticsNivelacijaEnhancements.sql:31`) briše i ceo lanac skorkarte (`vw_supplier_fullprice_signals` → `vw_supplier_markdown_dependency*` → `mv_supplier_decision_score_cache*`).
3. **Pregled — 503 iz skupog upita (Hipoteza; vlasnici `RQ474`/`RQ487`).**
   - Upit materijalizuje jedan red po stavci prodaje (`AllEndpoints.cs:1313-1355`).
   - Pri svakom cache miss-u učitava ceo aktivni snapshot batch troškova (`:1229-1236`) i skenira celu istoriju nivelacija bez filtera po artiklu (`:1299-1311`).
   - Grana za otkazivanje/timeout vraća 503 (`:2089-2123`).

Pored dostupnosti, najvažniji semantički nalazi su:

- **Asortiman pogrešno koristi engine preporuka.** Pre/post efekat nivelacije se predaje engine-u kao da je „PoP“ (`AllEndpoints.cs:4731-4747`). „Pojačaj fokus“ na Asortimanu zato znači „sniženje je podiglo promet ≥12%“, a ne isto što i na Pregledu, iako su labele iste.
- **Skorkarta je relativni percentilni rang, ne apsolutni kvalitet.**
  - Formula: `demand + margin − markdown_penalty − inventory_penalty ± quality`, stegnuta na 0–100 (`029_AddSupplierDecisionWindowedViews.sql:411-440`).
  - Populacija su samo artikli čija je *prva ikada* nivelacija pala u prozor (`018_AddSupplierDecisionHubViews.sql:95-140`; `029:23-27`).
  - Gate-ovi traže 100% pokrivenost signala (`029:382-389,445`; `SupplierDecisionHubEndpoints.cs:3159-3176`).
- **Tri taba za istog dobavljača i period broje različite stvari**, a to nigde nije objavljeno:
  - Atribucija: Pregled koristi `SupplierIdAtSale`; Skorkarta i Asortiman `DnevnikPromena.DobavljacId` → trenutni `Artikli.IDDobavljac`.
  - Trošak: Pregled uzima snapshot trošak po stavci; Skorkarta `ps.nabavna_cena` → trenutni trošak; Asortiman samo trenutni trošak.
  - Populacija računa: Pregled ima policy; nivelacija view-ovi nemaju isključenje `DUG`/`KOREKCIJA`.
  - Period: kod Asortimana je to datum nivelacije, a ne datum prodaje.

Pošto live nijedan broj nije mogao da se proveri, PROVE sekcija ima P1 prioritet.

Ukupno 17 novih promptova (`SA-F1`–`SA-F7`, `SA-P1`–`SA-P5`, `SA-I1`–`SA-I2`, `SA-E1`–`SA-E3`): 6 × P1, 9 × P2, 2 × P3. Hipoteze su označene sa **Hipoteza**. **Nije registrovano u queue; RQ brojevi se dodeljuju pri registraciji.** Promptovi se referenciraju neutralnim ID-jevima `SA-F*`/`SA-P*`/`SA-I*`/`SA-E*`. Pored toga, jedan delta dodatak ne otvara novi prompt nego dopunjuje otvorene `RQ474`/`RQ487`.

## Git stanje (Faza A)

- Korisnikova mašina `DESKTOP-V877DAD` (`8a049e49-…`) bila je **offline** tokom celog audita (ListMachines `connected:false`, više pokušaja). Lokalni branch, ahead/behind i `git status` zato **nisu provereni**.
- Audit je rađen nad javnim `origin/main` (read-only raw/GitHub API, pa box klon).
- Supplier commit-i na `origin/main` od 2026-09-25 (putanje: supplier stranice, hub komponente, supplier servisi, `SupplierDecisionHubEndpoints.cs`, 018/029/Analytics 014, share policy, engine):
  - `afc8597` RQ488 localize decision copy and export units
  - `2d30d2b` Implement supplier positive net revenue share policy
  - `5992301` Add categorical analytics decision readiness
  - `988d06a` validate every tier1 response
  - `e7cab42` enforce metric evidence coverage
  - `d71afbb` add canonical context fingerprint
  - `75f9240` preserve signed shoe type share semantics
  - `d41ed2e` bind snapshot costs to sale lines
  - `749cf2b`, `c3b80a6`, `1c933f7`, `38eb2ec` (Supplier report)
  - `ded6bfc` apply supplier recommendation gate policy
  - `966939e` harden supplier shell and overview
  - `b39a54d`, `4ecea4b`, `6d1e713`, `617f380`, `c3e29a6`, `1cc5f64`, `089a926`, `73b5026`
- Commit `30aa90cd` (09-25 PS promptovi) jeste predak `origin/main`.

## Status ranijih supplier nalaza (na `278d37b9` = `8b4e1fc1` za ove fajlove)

Registracija 09-25 promptova: **jeste urađena** (de-dup 2026-09-28): PS01→RQ469, PS05→RQ470, PS09→RQ471, PS02→RQ472, PS03→RQ473, PS06→RQ474, PS08→RQ475, PS11→RQ476, PS04→RQ483, PS07→RQ484, PS10(+PDC deo PS16)→RQ485, PS12/PS15/PS18/supplier deo PS16/PS13 ostatak→RQ486, PS14→RQ487, PS17→RQ488. PS13 nije poseban RQ (atribucija: `RQ441`/`RQ445` DONE, izveštaj `RQ464`). Tabela statusa u queue fajlu (~`:1940-1980`) delom zaostaje za `Status:` linijama sekcija; sekcije su merodavne.

| Nalaz | Tema | Status na current main | Dokaz / napomena |
|---|---|---|---|
| C5 | PDC vs Supplier izvor troška | DONE (`RQ473`, `8b632770`) za PDC/Supplier; **nova razlika** Skorkarta/Asortiman — vidi `SA-F6` | |
| C6 | preporuke tvrdo zavise od pre/post signala | FIXED | engine `requireComparableSignal:false`, `AllEndpoints.cs:1768-1787`; `OperationsRecommendationGatePolicy` samo −15 confidence |
| C7 | top-level gate uključuje nepoznatog | FIXED | page flag nad poznatim dobavljačima `AllEndpoints.cs:1877-1886`; cohort `includesUnknown=false` `:2044-2055` |
| C15 | fokusirani dobavljač 100% udela | **backend FIXED, frontend OSTAJE** | `SupplierSharePolicy` (`AllEndpoints.cs:1645,1754-1756`), ali `SupplierSalesStatsPage.tsx:1041-1047` filtrira na jedan red, a `:735-747` preračunava udeo nad vidljivim redovima → 100%; `RQ476` DONE zatvorio je backend politiku → `SA-F7` |
| C16 | PoP pristrasan naviše | FIXED | `previousPeriodBasis` `response_totals`/`unavailable` `SupplierSalesStatsPage.tsx:1050-1063`; backend `:1283,1953-1955` (`RQ443` DONE) |
| C17 | trust header reset | FIXED (kod) | `trustState`/`trustRequestKey` u shell-u; live nije reprodukovano jer je sve gated |
| C18 | scope/atribucija između ekrana | Pregled↔PDC rešeno (`RQ441`/`RQ445`); **unutar supplier tabova otvoreno** → `SA-F6` | |
| C19 | snapshot trošak = Min | FIXED | per-line `AllEndpoints.cs:1229-1236,1439-1448` (`d41ed2e`) |
| C20 | težak supplier upit | OSTAJE (`RQ487` WAITING) | `:1313-1355` per-line; `:1299-1311` bez filtera po artiklu; **novo**: ceo snapshot batch u memoriji `:1229-1236` → delta u ovom dokumentu |
| C21 | keš 20 min bez stale signala | OSTAJE (`RQ487`) | TTL `IAnalyticsCacheService.cs:501`; cache-hit bez age/correlation `AllEndpoints.cs:1197-1213`; ključ `:1195` bez integrity-block stanja iako je `blockOperationsDecisionSignals` upečen (`:1749,1794,1880`) |
| C22 | engine na engleskom | **FIXED (`RQ488` DONE)** | current engine summary/caveat/label tekst je na srpskom; odvojeni AnalyticsTrustHeader residual „Preporuka je gated.“ ostaje pod C27/RQ529 |
| C27 | labele | DELIMIČNO | „Low signal“ uklonjen; ostaje „decision preporuke“ `SupplierSalesStatsPage.tsx:1997`, `<h2>Dobavljači</h2>` bez h1 `SupplierConsolidatedPage.tsx:320`, `dataQualityLabels` bez `critical` `:48-54`; „Preporuka je gated.“ `components/analytics/AnalyticsTrustHeader.tsx:316` → `SA-F7`/`SA-I2` |
| C29 | nevalidan period i dalje šalje zahtev | FIXED | child `if (invalidRange) return` `SupplierSalesStatsPage.tsx:957`; hub `:988`, asortiman `:760` |
| C30 | generički catch vraća `ex.Message` | OSTAJE | `AllEndpoints.cs:2137-2140`; hub details `SupplierDecisionHubEndpoints.cs:434-437`; summary/quadrant/ranking u meta `:125,221,358` → `SA-F5` |
| C31 | `getStores` scope | FIXED | `getStores(true, canonicalFilters.dataScope)` |
| C32 | rank bedževi po sortu | DELIMIČNO | sada samo kad je sort `ukupanPromet`, ali bez obzira na smer: rastući sort daje zlato najmanjima `SupplierSalesStatsPage.tsx:2151,2170` → `SA-F7` |
| C33 | margin 0 umesto null | DONE za Pregled (`RQ473`); Asortiman `averageKnownMarginPct` je neponderisan prosek (`AllEndpoints.cs:4716-4720`) → `SA-F3` | |
| C24/C28 | sort side-effect / mrtav kod | FIXED | `handleSort` `:1576-1584`; `displaySignalLabel` uklonjen |
| L5 | Pregled 503 | OSTAJE live 2026-09-30 (`RQ474` WAITING) | vidi delta |
| L6 | Skorkarta „nije spreman“ | OSTAJE — **uzrok nađen** → `SA-F1` (`RQ475` WAITING ostaje za operativni deo) | |
| L7 | Asortiman samo tehnička greška | OSTAJE — uzrok **Hipoteza** → `SA-F2` | |
| L8 | overflow 1280 px | FIXED (filteri se prelamaju); ostaje odsecanje vrednosti select-a → `SA-I2` | |
| L9 | US format datuma | OSTAJE live (native `input type=date` prati locale pregledača) → `SA-I2` | |
| L10 | dupla „Komision (Gospodska 6, N/A)“ | DELIMIČNO — dodat `[ID n]`, ali dva različita ID-a sa istom labelom ostaju dvosmislena (data duplikat? **Hipoteza**) → `SA-I2` | |
| PS06/07/08/11/12/13/14/15/17/18 | | RQ474 WAITING, RQ484 DONE, RQ475 WAITING, RQ476 DONE, RQ486 DONE (residuali gore), PS13 postojeći vlasnici, RQ487 WAITING, RQ486 DONE, RQ488 DONE (residual C22), RQ486 DONE (residual C32) | |

## Live nalazi 2026-09-30 → uzrok u kodu

| Live | Uzrok / vlasnik |
|---|---|
| Pregled 503 za 30/90/180d i za Antilop; >15 s loading | **Hipoteza** C20 + snapshot batch; 503 grana `AllEndpoints.cs:2089-2123`; `RQ474`/`RQ487` + delta; UX `SA-I1` |
| „Preporuka je gated“ | `AnalyticsTrustHeader.tsx:316` (engleski žargon) → `SA-I2` |
| Skorkarta 90d „nije spreman“, correlation id | `information_schema` vs MV → `SA-F1` (visoka pouzdanost) |
| 9–10 zahteva >8 s | summary/quadrant/ranking/details + report paralelno, svaki sa sopstvenim cache ključem; → `SA-I1` |
| datumi 1–30. sep, poruka kaže 90d | 30d se mapira na 90d skup (`GetDecisionScoreWindowDays` `SupplierDecisionHubEndpoints.cs:2968`, poruka `:2467`) → `SA-I1` |
| filteri bez outputa | posledica `SA-F1` |
| Asortiman „nema potvrđen ugovor“ | `AllEndpoints.cs:3955-3972`; redosled 014 Fix/Analytics → `SA-F2` (**Hipoteza**) |
| samo deskriptivno, ništa za odluku | → `SA-E1`, `SA-E3`; semantika preporuke `SA-F3` |
| 30/90/180/365 vs Proizvodi 30/60/90 | `SupplierFootwearAnalyticsPage.tsx:643-647`, supplier shell → `SA-I2` |
| Status „BACKEND Offline“ pa Online; Redis isključen; Workeri 0/1 | **Hipoteza**: nightly refresh worker (`NightlyAnalyticsRefreshOptions.cs:50-55` osvežava i `_90d`/`_180d`) ne radi → MV mogu biti stari/prazni; ali `SA-F1` blokira i kada su MV sveži. Provera u `SA-P1`/`SA-P2` |

## Novi nalazi

Oznake: K = korektnost, D = dokaz (proof), V = vrednost za odluku, U = UX/UI, R = robusnost/bezbednost. **H** = hipoteza.

| # | Nalaz | Dokaz | Ozbiljnost | Kat. | Prompt |
|---|---|---|---|---|---|
| N01 | Provera MV kolona preko `information_schema.columns` — MV tamo ne postoje, pa je skorkarta trajno `MISSING_SCHEMA`; initializer koristi `pg_class` i kaže „present“ | `SupplierDecisionHubEndpoints.cs:2820-2888,2788-2793,2461-2468`; `DatabaseInitializer.cs:596-610,660-680` | Critical | R/K | SA-F1 |
| N02 | `014_Fix...` bezuslovno `DROP VIEW vw_vendor_sales_nivelacija CASCADE` i pravi view bez semantičkih kolona; Analytics 014 može biti preskočen po hash-u → Asortiman „contract missing“ (**H**) | `014_FixNivelacijaViewsFromDnevnik.sql:42-44,174`; `DatabaseInitializer.cs:891-905`; `AllEndpoints.cs:3955-3972` | High | R | SA-F2 |
| N03 | CASCADE drop nivelacija view-a / `vw_nivelacija_did` briše ceo lanac skorkarte (views + MV); 018 full-build je odložen | `016_AnalyticsNivelacijaEnhancements.sql:31`; `029:88-100`; `DatabaseInitializer.cs:960-1025` | High | R | SA-F2 |
| N04 | Asortiman predaje pre/post nivelacije engine-u kao PoP (`PopRevenueChangePct = post vs pre`, `PreviousPeriodRevenue = pre`) → iste labele („Pojačaj fokus“) sa drugim značenjem | `AllEndpoints.cs:4731-4747`; labele `SupplierFootwearAnalyticsPage.tsx:116-122` | High | K/V | SA-F3 |
| N05 | Nezreli post prozor: događaji <30 dana stari porede 30 d pre sa delimičnim post prozorom; `coverage_post30` meri dane sa prodajom, ne protekle dane → pristrasnost naniže | `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql:132-170`; scoped `AllEndpoints.cs:7817-7992` | High | K | SA-F3 |
| N06 | Kada posle nivelacije nema prodaje, `post_*` je NULL → `change_*` NULL umesto −100%; najgori ishodi nestaju iz proseka (skorkarta istovremeno radi `COALESCE(post,0)`) | `014_CreateVendorSalesNivelacijaViews.sql:220-262`; `029:64-65` | High | K | SA-F3 |
| N07 | `Pct(pre=0, post>0) = 100` u totalima i po dobavljaču (lažnih +100%) | `AllEndpoints.cs:4529-4533,4578,4678` | Medium | K | SA-F3 |
| N08 | Vendor `ChangeRevenue/ChangeQty` sabira sve redove, a `Pre/PostRevenue` samo uporedive → `Change ≠ Post − Pre` | `AllEndpoints.cs:4634-4637,4676-4679` | Medium | K | SA-F3 |
| N09 | `averageKnownMarginPct` je neponderisan prosek po dobavljaču, uključuje dobavljače sa 0% pokrivenosti troška | `AllEndpoints.cs:4716-4720` | Medium | K | SA-F3 |
| N10 | Asortiman marža koristi trenutni trošak artikla, ne snapshot | `AllEndpoints.cs:4594-4652` | Medium | K | SA-F6 |
| N11 | Skorkarta return rate = povrati / *neto* jedinice (neto već umanjen za povrate) → precenjen; nedostajući return rate se rangira kao najbolji (0) | `029:349-358,374-377,420` | Medium | K | SA-F4 |
| N12 | `sales_in_period` traži i trenutni `IDDobavljac` i `supplier_id_at_sale` = isti → artikli koji su menjali dobavljača ispadaju; period je raspon markdown prozora (do ~150 d), ne traženi | `029:349-358,304-305` | Medium | K | SA-F4 |
| N13 | `REVIEW_QUALITY` čim bilo koja pokrivenost (post/DiD/trošak) <100%; page gate blokira ako *bilo koji* red ima `PostSignalCoverage < 1` → preporuka praktično nikad dozvoljena (**H** na live podacima) | `029:382-389,445`; `SupplierDecisionHubEndpoints.cs:3159,3171-3176` | High | K/V | SA-F4 |
| N14 | Skor je relativni `PERCENT_RANK` (menja se kad se menjaju drugi dobavljači), zbir komponenti do 220 stegnut na 0–100 (izjednačenja na 0/100); pragovi 80/60/40/25 bez obrazloženja | `029:411-453` | Medium | V/D | SA-E2 |
| N15 | `inventory_penalty` rangira apsolutnu vrednost zaliha (RSD), pa model ima ekspoziciju na veličinu dobavljača; da li je to nepoželjno je model-policy odluka, ne automatski bug | `029:135,418,432` | Medium | V/K | RQ531 |
| N16 | `confidence_score` težine 0.4+0.3+0.3+0.1+0.1+0.1 = 1.3 (−0.15) stegnute na 0–1 | `029:434` | Low | K | SA-E2 |
| N17 | Populacija skorkarte = artikli čija je *prva ikada* nivelacija u prozoru; artikli sa ranijom prvom nivelacijom ne ulaze; Asortiman uzima *poslednji* događaj po artiklu i uključuje poskupljenja | `018:95-140`; `029:23-27`; `VendorSalesNivelacijaCohortPolicy.SelectLatestEventPerArticle` `AllEndpoints.cs:4459-4470` | Medium | K/V | SA-F6 |
| N18 | Trošak fallback `NabavnaCenaDin → NabavnaCena`; `NabavnaCena` je možda u drugoj valuti (**H**) | `018:166-173`; `029:73-80`; `AllEndpoints.cs:4603-4649` | Medium | K | SA-F4 |
| N19 | Nivelacija view-ovi i `vw_supplier_fullprice_signals` ne isključuju `DUG`/`KOREKCIJA` račune (Pregled ih isključuje policy-jem; `029:357` samo u `sales_in_period`) | `014_Create...:73-83,141-150`; `018:195-197`; `AllEndpoints.cs:7862-7877` | Medium | K | SA-F4/SA-F6 |
| N20 | Stock-before-markdown proxy oduzima „Povrat kupca“, a prodaja od nivelacije već sadrži negativne količine povrata → dupli odbitak (**H**) | `018:186-192,227-252` | Low | K | SA-F4 |
| N21 | `dead_stock_rate`/`unsold_stock_value` koriste *današnje* zalihe i trošak za istorijski prozor | `029:72-80,133-135` | Low | K/V | SA-E2 |
| N22 | Atribucija dobavljača: Pregled `SupplierIdAtSale`; Skorkarta/Asortiman `DnevnikPromena.DobavljacId`→trenutni `IDDobavljac` | `AllEndpoints.cs:1292,1352`; `014_Create...:48`; `018:101` | Medium | K | SA-F6 |
| N23 | Asortiman „period“ filtrira datum nivelacije; pre/post prodaja pada van izabranog perioda; „Promet“ kolona je 30 d post-markdown prihod | `AllEndpoints.cs:3982-3983`; `SupplierFootwearAnalyticsPage.tsx:82` | Medium | K/U | SA-F6/SA-I1 |
| N24 | Scoped nivelacija SQL agregira *celu* istoriju prodaje (`sales_daily` bez datumske granice) i izvršava se 3× po zahtevu (count, kategorije, redovi) × 2 zahteva (tekući + prethodni period), timeout 45 s svaki | `AllEndpoints.cs:51,3975-4150,7862-7876`; `SupplierFootwearAnalyticsPage.tsx:386-389` | High | R | SA-F2 (perf deo) |
| N25 | Store filter na Asortimanu filtrira i događaje nivelacije po `IDObjekat`; ako su nivelacije na nivou lanca, izbor objekta daje prazno (**H**) | `AllEndpoints.cs:7853` | Medium | K | SA-F6 |
| N26 | Analytics 015 sadrži nevalidan `CREATE MATERIALIZED VIEW CONCURRENTLY` i transakciono osetljiv `CREATE INDEX CONCURRENTLY`, ali current initializer ga tretira kao opcioni overlay i ne izvršava ga u startup sekvenci; script-hygiene/test residual, ne startup root cause | `015:33-46`; DatabaseInitializer optional-overlay komentar | Low | R | RQ525 |
| N27 | Nijedan `AddAuthentication`/fallback policy u `Program.cs`; supplier endpointi nemaju `RequireAuthorization` — podaci o nabavci/marži su javni ako API nije iza zaštićenog proxy-ja (**H**, zavisi od deploy-a) | `Program.cs:1149`; `SupplierDecisionHubEndpoints.cs:39-41` | High (ako je javno) | R | SA-F5 |
| N28 | Summary/quadrant/ranking vraćaju 200 sa nulama + error meta; details vraća 503 sa engleskim naslovom i `ex.Message`; 404 poruka engleska | `SupplierDecisionHubEndpoints.cs:104-126,432-448` | Medium | R/U | SA-F5/SA-I1 |
| N29 | Asortiman chip-ovi prikazuju *draft* vrednosti umesto primenjenih; „Signal“ chip prikazuje sirove enum-e (`good`, `insufficient_data`) | `SupplierFootwearAnalyticsPage.tsx:595-626` | Low | U | SA-F7 |
| N30 | Kategorija na Asortimanu nije u URL-u niti u deljenom stanju (gubi se pri promeni taba/reload-u) | `SupplierFootwearAnalyticsPage.tsx:276,281` | Low | U | SA-F7 |
| N31 | Asortiman greška nema retry dugme; nema AbortController (stari teški zahtevi nastavljaju na serveru) | `SupplierFootwearAnalyticsPage.tsx:374-470,869` | Medium | U/R | SA-I1 |
| N32 | Asortiman ukupni promet je `null` čim *bilo koji* red nije uporediv | `SupplierFootwearAnalyticsPage.tsx:538-549` | Medium | U/V | SA-I1 |
| N33 | Sortabilna zaglavlja bez `aria-sort`; ASCII markeri „ ^“/„ v“; tabovi sa `aria-selected` bez `role="tab"`/`tablist` i istovremeno `aria-current`; nema `h1` | `SupplierSalesStatsPage.tsx:2023-2131`; `SupplierFootwearAnalyticsPage.tsx:109`; `SupplierConsolidatedPage.tsx:320,487-488` | Low | U | SA-I2 |
| N34 | Podrazumevani „danas“ na backendu je UTC (`DateTime.UtcNow.Date`), ne Beograd; nivelacija `from?.ToUniversalTime().Date` | `AllEndpoints.cs:1163,3886-3887` | Low | K | SA-P1 (dokaz) / SA-F6 |
| N35 | Nepoznati dobavljač = null ID *ili* naziv „Nepoznato“; ID koji ne postoji u `Dobavljaci` stvara zaseban „nepoznat“ red (više nepoznatih redova) | `AllEndpoints.cs:1463-1464`; `4653-4657` | Low | K | SA-F6 |
| N36 | Udeo maržnog doprinosa koristi signed ukupni doprinos; uz negativne doprinose pojedinačni udeo može biti >100%. To je matematički moguće i zahteva eksplicitnu denominator semantiku/owner odluku, ne automatski positive-only fix | AllEndpoints.cs; SupplierSalesStatsPage display projection | Low | V/K | RQ523/RQ531 |
| N37 | Engine pragovi (PoP ≥12%, marža ≥ max(8, avg−2), udeo ≥2,5%, pouzdanost ≥60; tiny <3 artikla/<8 kom/<15.000 RSD nezavisno od dužine perioda) nisu objašnjeni u UI | `AnalyticsDecisionRecommendationEngine.cs:~118-230` | Medium | V | SA-E2 |
| N38 | Vrednosti za kupovinu koje fale: pokrivenost zaliha (dani), otvorene porudžbine/lead time, stopa povrata na Pregledu, trend marže, top/bottom artikli, size curve, efikasnost sniženja sa kontrolom | — | Medium | V | SA-E1/SA-E3 |

## Delta za otvorene 09-25 promptove (bez novog RQ)

- **`RQ474` (PS06, WAITING)** — dodati u dokaz:
  - live 503 i dalje postoji 2026-09-30 za 30/90/180 d i za jednog dobavljača;
  - prvi load čeka više od 15 s pre greške, a klijentski timeout nije usklađen sa serverskim budžetom;
  - „Redis: isključen“ i „Workeri 0/1“ u sistemskom statusu; klasifikovati da li je 503 posledica DB timeout-a, poola ili hladnog keša.
- **`RQ487` (PS14, WAITING)** — dodati u „Do“ tačku 2:
  - ne učitavati ceo aktivni snapshot batch troškova (`AllEndpoints.cs:1229-1236`), nego samo `ProdajaStavkaId` iz tekućeg perioda;
  - keš ključ (`:1195`) i cache-hit meta (`:1197-1213`) moraju da reflektuju integrity-block stanje, ili da se block evaluira pri čitanju.
- **`RQ475` (PS08, WAITING)** — `SA-F1` i `SA-F2` su preduslovi (defekt koda). `RQ475` ostaje vlasnik operativne spremnosti i UI poruke „nije spreman“.

## FIX — bagovi i korektnost

### SA-F1 — Scorecard MV capability check must see materialized views

Suggested status: WAITING (recommended next P1)
Priority: P1
Type: backend/sql/tests
Feature family: supplier-scorecard-mv-capability
Parallel-safe: no (`Api/Endpoints/SupplierDecisionHubEndpoints.cs`)
Owner: Analytics Reliability / Supplier Decision
Findings: N01, L6
Commit suggestion: `fix(analytics): detect supplier scorecard materialized views via pg_catalog`

#### Problem

The Supplier scorecard throws `MISSING_SCHEMA` for every window, and the live 90d screen says "nije spreman", even when the materialized views exist.

`GetPrecomputedQueryCapabilitiesAsync` counts required columns through `information_schema.columns`. PostgreSQL does not list materialized views there, so `decision_score_cache*_has_required_columns` is always false. The same applies to `decision_score_cache_has_ml_supplier_score`.

The startup initializer uses `pg_class.relkind = 'm'` and reports the same MVs as present.

#### Evidence

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs:2814-2890` (capability SQL), `:2788-2793` (`HasDecisionScoreCacheForWindow`), `:2461-2468` (throw).
- `Infrastructure/Seed/DatabaseInitializer.cs:596-610` (`IsPublicMaterializedViewAsync` via `pg_class`), `:660-680`.
- The queue notes production `MISSING_SCHEMA` for every window (live 2026-09-28 and 2026-09-30).

#### Scope

- Capability detection only.
- No formula change, no MV rebuild, no production writes.

#### Read first

- `AGENTS.md`, `RQ401`/`RQ404`/`RQ475` sections, `Api.Tests/SupplierDecisionSchemaSqlTests.cs`, `SupplierDecisionHubContractTests.cs`.

#### Do

1. Replace the `information_schema.columns` checks for `mv_supplier_decision_score_cache`, `_90d`, `_180d` and `mv_supplier_markdown_dependency_cache` with `pg_attribute` joined to `pg_class`/`pg_namespace` (`attnum > 0 AND NOT attisdropped`). Keep `information_schema` for plain views/tables if you want, but use one shared helper.
2. Distinguish these states in the error code and meta: `MISSING_OBJECT`, `MISSING_COLUMNS` (list them) and `NOT_POPULATED` (`pg_matviews.ispopulated = false`).
3. Emit the same capability result in the startup log and in the endpoint, so there is one truth.
4. Re-check `decision_score_cache_has_ml_supplier_score` with the same helper.

#### Tests

- A real-PostgreSQL test (shared harness from `SA-P2`): create an MV with the 18 required columns. The capability must be true. Drop one column and it must report `MISSING_COLUMNS`. Create the MV `WITH NO DATA` and it must report `NOT_POPULATED`.
- Update the static SQL tests so they assert that `information_schema` is not used for MVs.

#### Acceptance

- On a database where the MVs exist and are populated, the scorecard returns rows, not `MISSING_SCHEMA`.
- The error states name the concrete missing piece.

#### Dependencies

- None. `RQ475` consumes the new states for operator messaging.

---

### SA-F2 — Make vendor-sales nivelacija view creation idempotent and stop CASCADE wiping the scorecard chain

Suggested status: WAITING
Priority: P1
Type: backend/sql/startup/tests
Feature family: vendor-sales-nivelacija-schema-lifecycle
Parallel-safe: no (`Infrastructure/Seed/DatabaseInitializer.cs`, `Database/Migrations/013/014/016`, `Database/Analytics/014`)
Owner: Analytics Reliability / Schema
Findings: N02, N03, N24, L7
Commit suggestion: `fix(analytics): keep nivelacija semantic contract and scorecard chain stable at startup`

#### Problem

Asortiman shows only "Pre/post nivelacija nema potvrđen ugovor za prihodnu promenu".

**Hypothesis:** the startup order is:

1. readiness check;
2. `Database/Migrations/014_FixNivelacijaViewsFromDnevnik.sql`, which unconditionally runs `DROP VIEW vw_vendor_sales_nivelacija CASCADE` and recreates the view without `change_percent_revenue_semantic`;
3. `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`, which is skipped by hash history.

The CASCADE drop, and the one in `016` on `vw_nivelacija_did`, also removes `vw_supplier_fullprice_signals`, `vw_supplier_markdown_dependency*` and every `mv_supplier_decision_score_cache*`. Their rebuild (018 full build) is deferred.

The unbounded scoped query also runs 3× per request × 2 requests, with a 45 s timeout each.

#### Evidence

- `Database/Migrations/014_FixNivelacijaViewsFromDnevnik.sql:42-44,174-220` (no semantic columns).
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql:195-262`.
- `Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql:13,31`.
- `Infrastructure/Seed/DatabaseInitializer.cs:167-177,888-906,960-1025`.
- `Api/Endpoints/AllEndpoints.cs:3951-3972` (contract check on the trendplus connection), `:3975-4150`, `:7817-7992` (scoped SQL, `sales_daily` without a date bound), `:51`.

#### Scope

- Startup SQL ordering and idempotence, readiness re-check, and query bounding.
- No change to the pre/post semantics (that is `SA-F3`).

#### Do

1. **Read-only first:** on the target DB, record the `vw_vendor_sales_nivelacija` columns, the startup SQL history rows for 013/014/016/018/029, and whether the scorecard views/MVs exist. Record the outcome in the run log.
2. Remove the unconditional `DROP ... CASCADE` from `014_Fix` (or retire the file from the startup sequence), so there is exactly one owner of `vw_vendor_sales_nivelacija`: Analytics 014.
3. Re-run the readiness check *after* every script that can replace the view. If the semantic column is missing, force Analytics 014 in the same startup.
4. If a CASCADE is unavoidable, schedule the 018/029 rebuild and log the dependent objects that were dropped.
5. Bound `sales_daily` in the scoped SQL to `[min(event)-30d, max(event)+30d)`. Compute count, categories and rows from one materialized CTE, or one query.

#### Tests

- A real-PostgreSQL startup test (`SA-P2` harness): run the startup sequence twice, and run it again after touching 014 Fix. The semantic column and the scorecard MVs must still exist.
- Scoped query equivalence test: bounded and unbounded give identical rows on the fixture.

#### Acceptance

- Asortiman returns data on a DB where the source tables have data.
- A restart never leaves the scorecard chain dropped.
- Request time is measured before and after.

#### Dependencies

- `SA-P2` harness. `RQ475` for operator messaging.

---

### SA-F3 — Give Asortiman honest price-change semantics instead of reusing the PoP engine

Suggested status: WAITING
Priority: P1
Type: backend/frontend/sql/tests
Feature family: supplier-assortment-price-change-semantics
Parallel-safe: no (`Api/Endpoints/AllEndpoints.cs` vendor-sales-nivelacija block, `SupplierFootwearAnalyticsPage.tsx`, `Database/Analytics/014`)
Owner: Analytics Reliability / Supplier
Findings: N04, N05, N06, N07, N08, N09
Commit suggestion: `fix(analytics): separate assortment price-change effect from supplier PoP`

#### Problem

Asortiman feeds the pre/post-markdown comparison into `AnalyticsDecisionRecommendationEngine` as if it were a period-over-period trend. The same labels ("Pojačaj fokus", "Zadrži", "Proveri") therefore mean different things on Pregled and Asortiman.

The pre/post metrics also have these defects:

- the immature post window is not flagged;
- when there are no post sales the result is NULL instead of a −100% change;
- a zero baseline produces a fake +100%;
- vendor `ChangeRevenue` is summed over a different population than `Pre`/`PostRevenue`;
- the margin benchmark is an unweighted average.

#### Evidence

- Engine reuse: `AllEndpoints.cs:4731-4747`; labels `SupplierFootwearAnalyticsPage.tsx:116-122`.
- Post window: `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql:132-170`; scoped `AllEndpoints.cs:7919-7934`.
- NULL post: `014_Create...:220-262`. The scorecard uses `COALESCE(post,0)` (`029:64-65`).
- `Pct`: `AllEndpoints.cs:4529-4533,4578,4678`.
- Vendor sums: `:4634-4637` vs `:4676-4679`.
- Margin average: `:4716-4720`.

#### Scope

- Asortiman backend DTO/semantics and page labels.
- The shared engine stays unchanged for Pregled.

#### Do

1. Replace the engine call with a dedicated price-change evaluation. Suggested statuses: `effective`, `neutral`, `ineffective`, `immature`, `insufficient_data`, each with Serbian labels that describe the markdown effect, not supplier focus. Keep the result explicitly non-actionable for buying unless the owner approves otherwise.
2. Add `post_window_days_elapsed` and `is_mature = event_date + 30 <= as_of_date`. Exclude immature events from effect totals and show them as "u toku".
3. Treat missing post sales on a mature event as `post = 0`, i.e. −100% when a baseline exists, in the view, in the scoped SQL and in the endpoint. Keep `NULL` only for a missing baseline.
4. Replace `Pct(0, >0) = 100` with `null` plus a reason (`no_baseline`) in totals and vendor rows.
5. Compute vendor `Change*` from the same comparable rows as `Pre`/`Post`.
6. Weight the margin benchmark by revenue, over vendors with margin coverage ≥ the engine threshold.

#### Tests

- Backend unit tests for each rule (mature/immature, no-post, zero baseline, vendor sums, weighted benchmark).
- A frontend spec asserting that the new labels render and that the Pregled labels are not reused.
- Oracle coverage in `SA-P4`.

#### Acceptance

- Σ vendor `ChangeRevenue` = `Σ Post − Σ Pre` over comparable rows.
- No +100% without a baseline.
- Immature events are visibly separated.

#### Dependencies

- `SA-F2` (data must load). The owner decides whether any assortment recommendation is actionable (record the decision in the run log).

---

### SA-F4 — Repair scorecard formula defects that are bugs, not policy
> **Second-pass correction:** superseded by RQ521. Automatski se popravljaju samo potvrđeni input bugovi; coverage pragovi, inventory penalty, confidence težine i cost fallback ne menjaju se bez owner odluke/dokaza (RQ531/RQ524).


Suggested status: WAITING
Priority: P2
Type: sql/backend/tests
Feature family: supplier-scorecard-formula-defects
Parallel-safe: no (`Database/Migrations/018`, `029`, `SupplierDecisionHubEndpoints.cs` trust gate)
Owner: Analytics Reliability / Supplier Decision
Findings: N11, N12, N13, N15, N18, N19, N20
Commit suggestion: `fix(analytics): correct supplier scorecard return, coverage and cost inputs`

#### Problem

Several scorecard inputs are computed wrongly, independent of any weighting policy:

- **Return rate.** It divides returns by *net* units, and a missing return rate ranks as the best value (0).
- **Supplier match in `sales_in_period`.** It requires both the current and the at-sale supplier to match.
- **Period span.** The period is the span of markdown windows, not the requested window.
- **Coverage gates.** Coverage below 100% on any single article forces `REVIEW_QUALITY`, and one row below 100% post coverage blocks the whole page.
- **Inventory penalty.** It ranks absolute RSD stock value.
- **Cost fallback.** It falls back to `NabavnaCena`, possibly in a foreign currency (**Hypothesis**).
- **Receipt exclusions.** `DUG`/`KOREKCIJA` receipts are included in the fullprice signals.
- **Stock proxy.** It may subtract customer returns twice (**Hypothesis**).

#### Evidence

- `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql:349-358,374-389,418-434,445` (and the `_180d` copy).
- `018_AddSupplierDecisionHubViews.sql:141-252`.
- `SupplierDecisionHubEndpoints.cs:3159,3171-3176`.

#### Scope

- Input correctness and gate thresholds that are clearly defects.
- Weight and threshold *policy* belongs to `SA-E2`.
- Migrations must be additive/idempotent; windowed MVs are recreated through the existing 029 path.

#### Do

1. Return rate = returned units / gross sold units (positive quantities). Keep a missing return rate neutral in ranking (exclude it or use the median), not best.
2. Match `sales_in_period` on `supplier_id_at_sale` only, and document it. Bound it to the requested/window dates.
3. Replace all-or-nothing coverage with thresholds, proposed at ≥0.8 for post/DiD and ≥0.7 for cost, each with an explicit reason code. Replace the page-level `Any(<1)` gate with a revenue-weighted coverage threshold. **Owner decision:** record the chosen thresholds in the run log.
4. Normalize the inventory penalty by supplier revenue or by stock units (stock value / 90-day COGS, i.e. days of cover).
5. Verify the currency of `NabavnaCena` (read-only data check). If it is not RSD, drop that fallback and count the row as missing cost.
6. Exclude `DUG`/`KOREKCIJA` in `vw_supplier_fullprice_signals.sales_profile` and in the nivelacija `sales_daily`, consistent with `SalesReceiptPopulationPolicy`.
7. Prove or disprove the double subtraction of returns in the stock proxy on a fixture; fix it if proven.

#### Tests

- The `SA-P3` oracle fixture covers each rule.
- `SupplierDecisionSchemaSqlTests` assert the new predicates.

#### Acceptance

- The oracle matches the MV output for all fixture suppliers.
- At least one fixture supplier with 90% coverage is no longer forced to `REVIEW_QUALITY`.

#### Dependencies

- `SA-F1`, `SA-P3`. `SA-E2` for policy.

---

### SA-F5 — Safe supplier errors, auth confirmation and ML migration hygiene
> **DE-DUP second-pass:** nema novog RQ. Safe-error/readiness pripada RQ474/RQ475; deploy auth posture ide security owner-u samo ako se gap dokaže; Analytics 015 je u RQ525.


Suggested status: WAITING
Priority: P2
Type: backend/security/sql/tests
Feature family: supplier-endpoint-safety
Parallel-safe: partly (`AllEndpoints.cs` supplier catch block, `SupplierDecisionHubEndpoints.cs` handlers, `Database/Analytics/015`)
Owner: Analytics Reliability / Platform
Findings: C30 residual, N26, N27, N28
Commit suggestion: `fix(analytics): safe supplier errors and valid ML ranking migration`

#### Problem

Several supplier endpoints expose internals or behave unsafely:

- The generic supplier-sales-stats catch returns `detail: ex.Message` with a 500.
- Hub details returns `ex.Message` with an English title. The hub summary/quadrant/ranking put `ex.Message` into meta and return 200 with zeros.
- The cancellation titles are ijekavian ("Zahtjev otkazan").
- `Program.cs` configures no authentication or fallback authorization policy, and the supplier groups have no `RequireAuthorization`. **Hypothesis:** the deployment relies on a protecting proxy.
- `015_AddSupplierMlRanking.sql` contains invalid `CREATE MATERIALIZED VIEW CONCURRENTLY` and `CREATE INDEX CONCURRENTLY` statements that cannot run in a transaction.

#### Evidence

- `AllEndpoints.cs:2089-2140`.
- `SupplierDecisionHubEndpoints.cs:104-126,221,358,432-448`.
- `Program.cs:1149` (only `UseAuthorization`, no `AddAuthentication`).
- `Database/Analytics/015_AddSupplierMlRanking.sql:33-46`; `DatabaseInitializer.cs:2418`.

#### Do

1. Return stable Serbian titles plus an `errorCode` and `correlationId`. Log `ex` server-side only. Never return `ex.Message`.
2. Summary/quadrant/ranking: keep the 200-with-meta contract only if the frontend proves it renders an error state and not zeros (add a spec). Otherwise return 503 consistently.
3. Localize the details 404/503 titles.
4. **Owner decision (read-only check first):** confirm how the API is protected in production (proxy/IP allowlist/none). If it is unprotected, open a separate security prompt; do not add auth ad hoc in this prompt.
5. Fix or retire `015`: remove `CONCURRENTLY` from `CREATE MATERIALIZED VIEW`, and run index creation outside a transaction (or without `CONCURRENTLY`). Confirm whether any startup path executes it.

#### Tests

- `VendorSalesNivelacijaSafeErrorTests`-style tests for the supplier-sales-stats and hub handlers: no exception text in the body.
- An SQL test that parses or executes `015` on the `SA-P2` harness.

#### Acceptance

- No endpoint in scope leaks exception text.
- `015` runs cleanly or is explicitly retired.
- The auth posture is recorded.

#### Dependencies

- `SA-P2` harness for step 5.

---

### SA-F6 — Declare and align supplier attribution, cost basis, receipt population and period semantics across the three tabs

Suggested status: WAITING
Priority: P2
Type: backend/frontend/contract/tests
Feature family: supplier-cross-tab-basis
Parallel-safe: no (touches all three supplier endpoints' meta and the shell)
Owner: Analytics Reliability / Supplier
Findings: C18 residual, N10, N17, N19, N22, N23, N25, N34, N35
Commit suggestion: `fix(analytics): declare supplier attribution and cost basis per tab`

#### Problem

For the same supplier and period, the three tabs differ on every basis:

| Basis | Pregled | Skorkarta | Asortiman |
|---|---|---|---|
| Supplier attribution | `SupplierIdAtSale` | `DnevnikPromena.DobavljacId` → current `IDDobavljac` | `DnevnikPromena.DobavljacId` → current `IDDobavljac` |
| Cost | per-line snapshot | `ps.nabavna_cena` → current cost | current cost |
| Receipt policy | yes | no | no |
| Cohort | — | first-ever markdown | latest event, including price increases |
| Period | sale date | rolling window | markdown event date |

Other gaps:

- Store filtering on Asortiman also filters the nivelacija events (**Hypothesis:** events may be chain-wide).
- The unknown-supplier rules produce several unknown rows.
- Backend "today" is UTC.

None of these differences is disclosed.

#### Evidence

- `AllEndpoints.cs:1292,1352,1229-1236,1463-1464,1163,3886-3887,3982-3983,4594-4652,4653-4657,7853`.
- `Database/Analytics/014_Create...:48`; `018:95-140,166-173`; `029:23-27`; `SupplierFootwearAnalyticsPage.tsx:82`.

#### Do

1. Add a shared `basis` block to each tab's meta: `supplierAttribution`, `costBasis`, `receiptPopulation`, `cohort`, `periodSemantics`, `storeScope`, `asOfDate`/`timezone`. Render it in the shell's trust panel as a short "Kako se broji" section per tab.
2. Align where cheap and safe:
   - receipt exclusion in the nivelacija views (`SA-F4` step 6);
   - one unknown bucket (group every unresolved id under one "Nepoznat dobavljač" with a count of source ids);
   - business "today" in `Europe/Belgrade`.
3. **Read-only data check:** is `DnevnikPromena.IDObjekat` set for nivelacija rows? If they are chain-wide, apply the store filter to sales only.
4. **Owner decision:** whether Skorkarta/Asortiman should move to sale-time attribution (larger change; propose it in the run log).

#### Tests

- Contract tests asserting that `basis` is present and matches the code path.
- A unit test that several unknown ids collapse into one bucket.
- A timezone test for the default period at 00:30 Belgrade time.

#### Acceptance

- Every tab states its basis.
- `SA-P5` parity differences are explained by declared basis fields.

#### Dependencies

- `SA-F2`, `SA-F4`; `RQ445` (attribution contract, DONE) is the reference.

---

### SA-F7 — Supplier overview and assortment frontend residuals
> **Second-pass correction:** C22 je uklonjen iz scope-a jer ga je RQ488 već popravio. N36 se ne menja prećutno na positive-only denominator; signed semantika se prvo eksplicitno objašnjava ili owner odobrava promenu.


Suggested status: WAITING
Priority: P3
Type: frontend/copy/tests
Feature family: supplier-frontend-residuals
Parallel-safe: no (`SupplierSalesStatsPage.tsx`, `SupplierFootwearAnalyticsPage.tsx`, `SupplierConsolidatedPage.tsx`, engine caveat strings)
Owner: Analytics UI
Findings: C15 residual, C22 residual, C27 residual, C32 residual, N29, N30, N36
Commit suggestion: `fix(analytics): supplier share focus, badges, chips and copy residuals`

#### Problem and evidence

- **C15:** a focused supplier still shows 100% share, because the frontend recomputes share over the visible rows (`SupplierSalesStatsPage.tsx:735-747` after the `:1041-1047` filter), although the backend `SupplierSharePolicy` value exists (`AllEndpoints.cs:1754-1756`).
- **C32:** rank badges appear when sorting by revenue in *ascending* order, so the smallest suppliers get gold (`:2151,2170`).
- **N36:** margin-contribution share can exceed 100% when some contributions are negative (`AllEndpoints.cs:1757-1759`, `SupplierSalesStatsPage.tsx:738-750`). Use a positive-contribution denominator with an explicit label, like the revenue share.
- **C22:** the engine caveats are still English (`AnalyticsDecisionRecommendationEngine.cs:255,259`).
- **C27:**
  - "decision preporuke" appears at `SupplierSalesStatsPage.tsx:1997`;
  - `dataQualityLabels` has no `critical` (`SupplierConsolidatedPage.tsx:48-54`);
  - "Preporuka je gated." appears at `AnalyticsTrustHeader.tsx:316`.
- **N29:** Asortiman chips show draft values, and the "Signal" chip shows raw enums (`SupplierFootwearAnalyticsPage.tsx:595-626`).
- **N30:** the Asortiman category is not in the URL or shared state (`:276,281`).

#### Do

1. Use the backend share (whole population) when a supplier is focused, and label it "udeo u ukupnom".
2. Show badges only for a descending revenue sort.
3. Use a positive-contribution denominator for the margin-contribution share.
4. Serbian caveats; `critical` → "Kritično — ne koristiti za odluku"; replace "gated" with "Preporuka je zadržana".
5. Chips read `activeFilters`; map the signal enum to Serbian labels.
6. Persist `category` in the URL (`?category=`).

#### Tests

- Update the existing specs (`SupplierSalesStatsPage.decisionSuppliers.spec.tsx`, `SupplierFootwearAnalyticsPage.spec.tsx`, `AnalyticsTrustHeader.spec.tsx`, which currently asserts the "gated" text).

#### Acceptance

- The focused share equals the backend value.
- No English or raw enums remain on the three tabs.

#### Dependencies

- None blocking; coordinate with `SA-I2` on the same files.

## PROVE — testovi, rekoncilijacija, dokazi

Inventar postojećeg dokaza:

- **Pregled** ima najviše pokrića:
  - `AnalyticsSupplierSalesIntegrationTests.cs`: `SupplierSumEqualsTotal`, `SharesSumTo100`, `MatchesGoldenSnapshot`, `MetricsMatchFixtureValues`, `UsesWeightedMarginBenchmark`, `DataScopeFiltersRows`, `HandlesMissingSchemaGracefully`, `PerformanceWithinThreshold`;
  - fixture `Api.Tests/Fixtures/supplier-sales-stats-seed.sql`;
  - `SupplierSharePolicyTests`, `AnalyticsNivelacijaSplitPolicyTests`;
  - frontend specs.
- **Skorkarta** ima ugovorne i statičke SQL testove (`SupplierDecisionHubContractTests`, `SupplierDecisionSchemaSqlTests`, bez stvarnog PostgreSQL-a).
  - Nema oracle-a za skor/preporuku.
  - Nema testa koji bi uhvatio `N01`.
- **Asortiman** ima policy testove (`VendorSalesNivelacijaCohortPolicyTests`, `TypeInsightPolicyTests`, `SafeErrorTests`) i scope specs.
  - Nema oracle-a za pre/post prozore ni za NULL/zrelost.
- **Između tabova** nema testa pariteta.
- **Live 2026-09-30**: nijedan broj nije proveren.

### SA-P1 — Read-only supplier reconciliation SQL pack and evidence run

Suggested status: WAITING (recommended with SA-F1)
Priority: P1
Type: sql/qa/evidence
Feature family: supplier-reconciliation-evidence
Parallel-safe: yes (new files under `scripts/` and `docs/qa/` only)
Owner: Analytics Reliability / QA
Findings: all K findings; live "no numbers validated"
Commit suggestion: `test(analytics): add supplier reconciliation queries and evidence`

#### Problem

None of the supplier numbers can currently be validated. The existing `scripts/check_supplier_sales_stats.sql` covers only part of the overview.

#### Do

Create `scripts/supplier_analytics_reconciliation.sql`: read-only, parameterized by `from`, `to`, `store`, `dataScope`, with Belgrade day bounds. Record expected tolerances. Checks:

- **R1** Σ supplier revenue (overview grain: `Kolicina*Cena`, receipt policy, `SupplierIdAtSale`) = total sales for the same scope/period. Unknown included; a difference of 0 is expected.
- **R2** Known + unknown split, and the number of distinct unresolved supplier ids (N35).
- **R3** Previous-period total, including suppliers present only in the previous period (C16 guard).
- **R4** Cost coverage: the share of lines with a snapshot cost vs the current-cost fallback, and the `NabavnaCena` vs `NabavnaCenaDin` currency sanity check (N18).
- **R5** Attribution drift: revenue where `SupplierIdAtSale ≠ current IDDobavljac` (N22).
- **R6** `DUG`/`KOREKCIJA` revenue share in the period (N19).
- **R7** MV health: `pg_matviews` (`ispopulated`), row counts, and the last refresh from the refresh-status table for `mv_supplier_decision_score_cache*`; and whether the capability SQL returns true (N01).
- **R8** `vw_vendor_sales_nivelacija` columns and the startup SQL history rows for 013/014/016/018/029 (N02).
- **R9** Assortment totals: Σ comparable `post_revenue` = endpoint `totals.postRevenue`; share sum = 100.
- **R10** Scorecard revenue (`pre+post` of the markdown cohort) vs overview revenue for the same suppliers. The difference is expected and must be explained by the declared basis (`SA-F6`).
- **R11** Nivelacija events with `IDObjekat` null vs set (N25).
- **R12** Immature events (`event_date > as_of − 30`) and events with no post sales (N05/N06).

Run it read-only against the production analytics DB **only with owner approval** (no writes). Otherwise run it against the fixture DB. Store the results in `docs/qa/SUPPLIER_ANALYTICS_RECONCILIATION_<date>.md`.

#### Acceptance

- Every check has a result, a tolerance and a pass/fail/explained verdict.
- Failures link to the owning prompt.

#### Dependencies

- Owner approval for any production read.

---

### SA-P2 — Real-PostgreSQL harness for supplier schema readiness

Suggested status: WAITING
Priority: P1
Type: tests/infrastructure
Feature family: supplier-schema-readiness-tests
Parallel-safe: yes (new test project files; reuse the existing integration DB setup if present)
Owner: Analytics Reliability / Schema
Findings: N01, N02, N03, N26
Commit suggestion: `test(analytics): real postgres readiness tests for supplier views`

#### Do

1. Add a Testcontainers-based (or existing CI Postgres) fixture that applies the supplier startup SQL sequence (013 compatibility, 014 Fix, Analytics 014, 016, 018 core + full, 029, optionally 015) onto a seeded schema.
2. Tests:
   - the capability check returns true for existing MVs (fails today → `SA-F1`);
   - running the sequence twice, and running it after a changed 014 Fix hash, keeps `change_percent_revenue_semantic` and all `mv_supplier_decision_score_cache*` (`SA-F2`);
   - `REFRESH MATERIALIZED VIEW CONCURRENTLY` works for `_90d`/`_180d` (unique index present);
   - `015` executes or is explicitly skipped.
3. Mark the tests as a CI category, so they run in the analytics workflow.

#### Acceptance

- The harness reproduces `N01` before the fix and passes after `SA-F1`/`SA-F2`.

---

### SA-P3 — Scorecard formula oracle and golden fixture

Suggested status: WAITING
Priority: P2
Type: tests/sql
Feature family: supplier-scorecard-oracle
Parallel-safe: yes
Owner: Analytics Reliability / Supplier Decision
Findings: N11–N17, N20, N21
Commit suggestion: `test(analytics): independent oracle for supplier scorecard`

#### Do

1. Seed 5–6 suppliers with hand-computable markdown cohorts: different sell-through, margin, markdown share, stock, returns, a supplier-change article, a `DUG` receipt, a missing cost, and an article whose first markdown predates the window.
2. Write an independent C# oracle for `demand`/`margin`/penalties/`quality`/`confidence`/`recommendation_code` from the documented formula. Compare it with the `_90d` MV on the `SA-P2` harness.
3. Property tests:
   - the score is monotonic in sell-through with everything else fixed;
   - clamping to 0/100 is reported;
   - adding an unrelated supplier changes relative ranks (documents N14).

#### Acceptance

- The oracle equals the MV for every fixture supplier.
- Known defects are asserted as expected failures until `SA-F4` lands, then flipped.

---

### SA-P4 — Assortment pre/post oracle and golden fixture

Suggested status: WAITING
Priority: P2
Type: tests
Feature family: supplier-assortment-oracle
Parallel-safe: yes
Owner: Analytics Reliability / Supplier
Findings: N04–N09, N23, N24
Commit suggestion: `test(analytics): oracle for assortment pre/post metrics`

#### Do

Build a fixture that contains:

- mature and immature events;
- an event with no post sales;
- a zero baseline;
- two events on one article (the latest-event cohort);
- a price increase;
- a store-specific vs a chain-wide event;
- a vendor with some non-comparable rows.

The oracle computes the per-article windows and the vendor/total aggregates.

Assert:

- totals = Σ vendors;
- vendor `Change` = `Post − Pre`;
- share sum = 100;
- bounded vs unbounded scoped SQL equivalence (`SA-F2`).

#### Acceptance

- The golden JSON is committed. The oracle agrees, or failures are mapped to `SA-F3`.

---

### SA-P5 — Cross-tab parity contract for one supplier and period

Suggested status: WAITING
Priority: P2
Type: tests/contract
Feature family: supplier-cross-tab-parity
Parallel-safe: yes
Owner: Analytics Reliability / Supplier
Findings: N22, N23, N17, C18 residual
Commit suggestion: `test(analytics): supplier cross-tab parity contract`

#### Do

On the shared fixture, call the overview, the scorecard and the assortment for the same supplier/period/store/scope. Assert:

- where the bases are equal (after `SA-F6`), the numbers are equal: e.g. supplier revenue on the overview = Σ that supplier's sales in the fixture;
- where the bases differ, each difference equals the amount explained by the declared basis fields (attribution drift, receipt exclusion, cohort);
- the same supplier id resolves to the same display name on all tabs.

#### Acceptance

- The test fails when a tab silently changes its basis.

## IMPROVE — UX/UI

### SA-I1 — Honest loading, error, retry and requested-vs-effective period on supplier tabs
> **DE-DUP second-pass:** nema novog RQ. Pregled deo pripada RQ474; Skorkarta/Asortiman readiness/error/retry deo pripada RQ475.


Suggested status: WAITING
Priority: P2
Type: frontend/backend-meta/tests
Feature family: supplier-tab-state-clarity
Parallel-safe: no (`SupplierDecisionHubPage.tsx`, `SupplierFootwearAnalyticsPage.tsx`, `SupplierSalesStatsPage.tsx`, `supplierDecisionHubApi.ts`)
Owner: Analytics UI
Findings: N23, N28, N31, N32; live: >15 s loading, 9–10 in-flight requests, "90d" error while 1–30 Sep is shown
Commit suggestion: `fix(analytics): supplier tab loading, retry and period clarity`

#### Problem

- **Pregled** stays in loading for more than 15 s before the 503.
- **Skorkarta**:
  - fires 9–10 parallel requests;
  - names "90d" while the controls show 1–30 Sep, because 30d is served from the 90d set (`SupplierDecisionHubEndpoints.cs:2968,2467`);
  - summary errors arrive as 200 with zeros.
- **Asortiman**:
  - has no retry and no abort (`SupplierFootwearAnalyticsPage.tsx:374-470,869`);
  - its total revenue disappears when any single row is non-comparable (`:538-549`);
  - the "Promet" column is 30-day post-markdown revenue, while its label suggests period revenue (`:82`).

#### Do

1. Show a staged loading state after 3 s ("Učitavanje traje duže…"), a cancel action, and a Retry button on every error.
2. Use `AbortController` in all three tabs, and dedupe identical in-flight scorecard requests. Measure the request count before and after, and keep only the requests needed for the visible panels.
3. Always show "Traženo: 1.–30. sep · Izvor: 90 dana (pomoćni signal)" when the requested and effective windows differ, including in the error state.
4. Render the summary-with-error meta as an error state, never as zeros (spec).
5. Show the Asortiman total over the comparable rows, with a note "N redova nije uporedivo" instead of `null`. Rename "Promet" → "Promet 30 d posle nivelacije".

#### Tests

- Page specs for the slow, error+retry, abort, requested≠effective and summary-error cases.

#### Acceptance

- There is no state where a failed request looks like zero data.
- The scorecard request count is reduced and measured.

#### Dependencies

- `RQ474` (overview error classification); `SA-F1`.

---

### SA-I2 — Supplier tabs semantics, accessibility, formatting and jargon

Suggested status: WAITING
Priority: P3
Type: frontend/a11y/copy/tests
Feature family: supplier-a11y-formatting
Parallel-safe: no (same pages as `SA-F7`)
Owner: Analytics UI
Findings: N33, L8 residual, L9, L10 residual, C27 residual; live: period presets 30/90/180/365 vs Products 30/60/90, jargon
Commit suggestion: `fix(analytics): supplier tabs a11y, dates and glossary`

#### Do

1. Add one `h1` ("Dobavljači") in the shell. Tabs get `role="tablist"`/`role="tab"`/`aria-controls` (or links with `aria-current` only, not both). Sortable `th` get `aria-sort`. Replace the " ^"/" v" markers with icons plus visually hidden text.
2. Show selected select values in full (min-width or a tooltip for "Poslednjih 30 dana").
3. Display dates as `dd.MM.yyyy` next to or instead of the native date input (a locale-independent display; L9).
4. Align the period presets across Products and Supplier (**owner decision**: one canonical preset list), or explain the difference.
5. Store labels: if two store IDs share a label, show a distinguishing field (address/code). Run a read-only data check for a master-data duplicate (L10) and report it.
6. Glossary InfoTips for "Skorkarta", "nivelacija", "pouzdanost signala", "maržni doprinos", "pomoćni signal".

#### Tests

- An axe/RTL a11y spec for tabs and sortable headers, plus a date-format spec.

#### Acceptance

- Keyboard-only navigation across the tabs and sort works.
- No truncated preset labels at 1280 px.

## ENHANCE — nova vrednost za nabavku

### SA-E1 — Supplier buying panel: stock cover, sell-through, returns, margin trend, top/bottom articles

Suggested status: WAITING
Priority: P2
Type: backend/frontend/product
Feature family: supplier-buying-panel
Parallel-safe: no (new endpoint plus the Pregled detail drawer)
Owner: Analytics Product / Supplier
Findings: N38; live "nothing supports a decision"
Commit suggestion: `feat(analytics): supplier buying panel`

#### Problem

The screens describe past sales and markdown effects, but they don't answer buyer questions:

- "How many days of stock do I hold per supplier?"
- "Which articles to reorder or drop?"
- "Is margin improving?"
- "What is the return rate?"
- "What is open on order and the lead time?"

#### Do

1. **Read-only discovery:** which sources exist for open purchase orders, receipts (`Ulaz robe`) and lead time (order→receipt date). Record them. Don't invent fields.
2. Per supplier (same basis as Pregled):
   - current stock units/value at cost;
   - days of cover = stock / average daily units over the last 90 d;
   - sell-through over the period (sold / (sold + ending stock));
   - return rate (gross);
   - margin % for the last 3 equal periods (trend);
   - top 10 and bottom 10 articles by margin contribution and sell-through;
   - aged stock (>180 d without sale).
3. If PO data exists: open-order units/value and median lead time. Otherwise show "nije dostupno" with the reason.
4. Show everything in the Pregled detail drawer, with basis/coverage metadata and `SA-P1`-style reconciliation for each new metric.

#### Tests

- Fixture-based endpoint tests per metric, plus a frontend drawer spec.

#### Acceptance

- Every metric has a formula InfoTip, coverage and a test.
- Missing sources are explicit.

#### Dependencies

- `SA-F6` basis; `RQ474`/`RQ487` (the overview must load).

---

### SA-E2 — Explainable, owner-approved scorecard weights and thresholds

Suggested status: WAITING
Priority: P2
Type: product/sql/frontend/docs
Feature family: supplier-scorecard-explainability
Parallel-safe: no (`029`/`018` formula, hub components)
Owner: Analytics Product / Supplier Decision (owner decision required)
Findings: N14, N16, N21, N37
Commit suggestion: `feat(analytics): explain supplier scorecard components and thresholds`

#### Problem

These choices are hard-coded and unexplained in the UI:

- the scorecard weights (0.60/0.40 demand, 0.5/0.5 inventory, quality ±20, seasonal discounts 75/85 via category-name matching);
- the recommendation thresholds 80/60/40/25 applied to relative percentile ranks;
- the confidence weights summing to 1.3;
- the engine thresholds (12%, 8%, 2.5%, 60, 15,000 RSD).

#### Do

1. Write `docs/analytics/SUPPLIER_SCORECARD_FORMULA.md`: every component, its range, its weight, and its rationale or "unjustified — needs owner".
2. Expose the per-supplier component contributions in the details endpoint and the drawer: "Skor 62 = potražnja 70 + marža 55 − sniženja 40 − zalihe 30 + kvalitet 7 (stegnuto)".
3. Show whether the score is relative ("rang među N dobavljača") and add the formula version to the meta.
4. **Owner decision:** approve or adjust the weights and thresholds; express the period-length-dependent sample thresholds per day. Record the decision.
5. Replace `dead_stock`/`unsold` current-stock inputs with period-end stock where available, or label them "trenutno stanje".

#### Tests

- The `SA-P3` oracle is updated to the approved formula version, plus a drawer spec.

#### Acceptance

- A buyer can see why a supplier got its recommendation, and which formula version produced it.

---

### SA-E3 — Footwear size curve and price-change effectiveness with control

Suggested status: WAITING
Priority: P3
Type: backend/frontend/product
Feature family: supplier-assortment-size-curve
Parallel-safe: no (Asortiman page, new endpoint)
Owner: Analytics Product / Supplier
Findings: N38, N05
Commit suggestion: `feat(analytics): supplier footwear size curve and controlled markdown effect`

#### Do

1. **Read-only discovery:** is the size (broj) available per article or sale line? If not, stop and report.
2. Per supplier × footwear type: sold vs received vs on-hand by size (size curve), highlighting sizes that sell out early and sizes left over after markdown.
3. Markdown effectiveness: use the existing `vw_nivelacija_did`/control group (`016`) to report the uplift *vs control* for mature events only, with a confidence band; show the markdown depth vs the uplift.
4. Put this in Asortiman as the decision section: "Koje brojeve/tipove naručiti više/manje", "Koliko sniženje je radilo".

#### Tests

- Fixture tests for the size curve and the DiD aggregation.

#### Acceptance

- Each insight shows its population, maturity and control basis.

#### Dependencies

- `SA-F3`, `SA-P4`.

## Pokrivenost nalaz → prompt

| Nalaz | Prompt(i) | Hipoteza |
|---|---|---|
| N01 | SA-F1, SA-P2 | ne (visoka pouzdanost; ponašanje `information_schema` je dokumentovano ponašanje PostgreSQL-a) |
| N02, N03 | SA-F2, SA-P2 | N02 da; N03 ne (CASCADE semantika) |
| N04–N09 | SA-F3, SA-P4 | ne |
| N10 | SA-F6 | ne |
| N11, N12, N13, N15 | SA-F4, SA-P3 | N13 da (samo efekat na live podacima) |
| N14, N16, N21, N37 | SA-E2, SA-P3 | ne |
| N17 | SA-F6, SA-P5 | ne |
| N18 | SA-F4, SA-P1 (R4) | da |
| N19 | SA-F4, SA-F6, SA-P1 (R6) | ne |
| N20 | SA-F4, SA-P3 | da |
| N22, N23 | SA-F6, SA-P5, SA-I1 | ne |
| N24 | SA-F2, SA-P4 | ne (cena na live je hipoteza) |
| N25 | SA-F6, SA-P1 (R11) | da |
| N26 | SA-F5, SA-P2 | da (da li se izvršava) |
| N27 | SA-F5 | da (deploy) |
| N28 | SA-F5, SA-I1 | ne |
| N29, N30, N36 | SA-F7 | ne |
| N31, N32 | SA-I1 | ne |
| N33 | SA-I2 | ne |
| N34, N35 | SA-F6, SA-P1 | ne |
| N38 | SA-E1, SA-E3 | ne |
| C15, C22, C27, C32 residual | SA-F7 (C27 i SA-I2) | ne |
| C20, C21 residual | delta `RQ487` | C20 kao uzrok 503: da |
| C30 residual | SA-F5 | ne |
| L5 | delta `RQ474`, SA-I1 | uzrok: da |
| L6 | SA-F1 (+ `RQ475`) | ne |
| L7 | SA-F2 (+ `RQ475`) | da |
| L8, L9, L10 residual | SA-I2 | L10 data duplikat: da |
| live: presets, jargon, gated | SA-I2, SA-F7 | ne |
| live: Workeri 0/1, Redis off | SA-P1 (R7), `RQ475` | da |

## Canonical queue mapping posle second-pass verifikacije

| Audit prompt | Canonical owner | Prioritet | Status |
|---|---|---:|---|
| SA-F1 | RQ518 | P1 | READY |
| SA-F2 | RQ519 | P1 | WAITING |
| SA-F3 | RQ520 | P1 | WAITING |
| SA-F4 (corrected) | RQ521 | P2 | WAITING |
| SA-F5 | RQ474/RQ475 + RQ525; security handoff only if N27 proven | — | DE-DUP |
| SA-F6 | RQ522 | P2 | WAITING |
| SA-F7 (corrected) | RQ523 | P3 | WAITING |
| SA-P1 | RQ524 | P1 | WAITING |
| SA-P2 | RQ525 | P1 | WAITING |
| SA-P3 | RQ526 | P2 | WAITING |
| SA-P4 | RQ527 | P2 | WAITING |
| SA-P5 | RQ528 | P2 | WAITING |
| SA-I1 | RQ474/RQ475 | — | DE-DUP |
| SA-I2 | RQ529 | P3 | WAITING |
| SA-E1 | RQ530 | P2 | WAITING |
| SA-E2 | RQ531 | P2 | WAITING / owner-gated |
| SA-E3 | RQ532 | P3 | WAITING |

RQ517 je već DONE Daily Sales prompt i nije prepisan. Addendum owner: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_SUPPLIER_AUDIT_ADDENDUM.md.

## Šta nije urađeno / nije provereno

- Korisnikova mašina je bila offline, pa lokalni branch, ahead/behind i `git status` nisu provereni. Dokument je objavljen iz zasebnog worktree-a na `origin/main`, bez diranja glavnog checkout-a.
- Nije rađen live SQL/API/browser; live nalazi su preuzeti iz paralelnog UI audita od 2026-09-30.
- Nije pokrenut nijedan test. Hipoteze N02, N13, N18, N20, N25, N26, N27 i uzrok L5 zahtevaju read-only proveru podataka ili deploy-a (vidi `SA-P1`).
- Nisu menjani runtime kod, SQL, migracije niti produkcioni podaci.
