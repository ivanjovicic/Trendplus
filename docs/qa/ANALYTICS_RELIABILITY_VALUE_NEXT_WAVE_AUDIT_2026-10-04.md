# Trendplus Analytics: pouzdanost, vrednost odluka i sledeći talas promptova (2026-10-04)

Datum: 2026-10-04, rad 22:45–23:30 po beogradskom vremenu (CEST).
Osnova: sveža `origin/main` `f1437ed84c5c22922f4b70b2174ef05d2d0fa970` ("docs(evidence): record UX audit review and corrections"). Pre isporuke je urađen ponovni fetch i rebase (§27).
Produkcija: `https://trendplus-api.onrender.com`, runtime commit `02f9915887f99115348bd241590da581dafee45d`, build `2026-10-04T19:38:31Z` (21:38 CEST), proces `web`, provajder `render`.
Način rada:
- čitanje koda i svih queue fajlova;
- samo read-only GET provere produkcije, za fiksni istorijski period `2026-07-07..2026-08-06` (prodaja u produkciji staje 05.08.2026);
- bez pristupa bazi, logovima provajdera i admin ključu;
- runtime kod nije menjan.

Dokazi: `.ai/runs/2026-10-04-analytics-next-wave-audit-evidence.md`.

Oznake dokaza:
- **live**: viđeno u read-only proveri 2026-10-04 između 22:50 i 22:56 CEST;
- **code**: izvedeno iz koda na `f1437ed`;
- **reuse**: preuzeto iz ranijih audita istog dana (re-audit, UX, nivelacija, dobavljači, responsive, negative-ID, hotfix), uz istu runtime verziju `02f99158`;
- **UNPROVEN-RUNTIME**: tvrdnja je logički izvedena, ali bez loga ili baze nije dokaziva.

## 1. Kratak zaključak

Analitika danas može da odgovori samo na jedno pitanje: "šta se prodavalo u julu 2026". Na to odgovara tačno i konzistentno: Dnevna prodaja, Dobavljači, Vrsta obuće, Boja i datirani Dashboard daju isti promet od 1.561.120 RSD i 307 komada. Za aktuelne odluke ne vredi ništa. Razlog nije kod, nego tri operativna stanja koja kod ne može da popravi:

1. **Uvoz je stao.** Poslednji import je od 12.08.2026, a poslednja prodaja od 05.08. U trenutku audita podaci su stari 1.282 sata.
2. **Na live servisu ne radi startup inicijalizacija baze, ili radi bez fail-fast-a** (novi nalaz, §4.2). Kod dokazuje sledeće: da su `Database__AutoMigrate=true` i `DatabaseInitialization__FailFast=true` stvarno aktivni, servis ne bi mogao da bude `ready` dok `vw_vendor_sales_nivelacija` nema kolonu `change_percent_revenue_semantic`, a supplier MV-ovi ne postoje. On je ipak `ready` od 21:39:00 CEST. Prema tome, `render.yaml` i stvarna konfiguracija na Render-u se razlikuju, ili se greške inicijalizacije tiho gutaju.
3. **Petlja učenja nikada nije korišćena.** Ceo action ledger u produkciji čine 4 smoke zapisa od 22.05.2026. Nema nijedne prave akcije. Zato svaki prompt o ishodima, kalibraciji ili kauzalnosti trenutno nema populaciju.

Queue je u dobrom stanju. Od 703 zadatka, oko 490 RQ promptova je DONE, a skoro svaki problem koji sam našao već ima vlasnika. Zato ovaj audit dodaje samo **2 nova prompta** (RQ587, RQ588). Popravlja **2 postojeća**: RQ479 dobija metadatu i promociju, a RQ586 se spušta sa P1 na P3 jer ga je falsifikacija oborila. Dodaje i datirane addendume na RQ545, RQ565, RQ573, RQ578, STAB16, P-UI-49 i na backlog PROD-AN. Glavna vrednost ovog rada je u falsifikacijama i preporukama šta ne graditi (§13), ne u novim promptovima.

## 2. Osnova, stanje repoa i queue-a (§0)

- `origin/main` = `f1437ed8`, fetchovan u 22:47 CEST. Posle UX audita (`61992ef`) Codex je dodao 13 commitova, svi su docs/queue: P-UI-53 za pristupačnost grafikona, korekcije UX audita i rutiranje.
- Otvoreni PR-ovi: **0** (GitHub API).
- Udaljene grane: 28. Sve osim `main` pripadaju DONE promptovima (RQ474–RQ536, P-UI-24–27, RQ564), sa poslednjim commitom između 30.09. i 04.10. Nijedna ne drži familiju koju ovaj audit dira.
- Lokalni lockovi: `.ai/task-locks/` ne postoji ili je prazan. Nema aktivnog vlasnika.
- READY stanje pre ovog rada, proveravano po sekciji a ne po zaglavlju:
  - RQ: primarni **RQ569**, dodatni RQ553, RQ574, RQ578, RQ580, RQ581, RQ586;
  - P-UI: primarni **P-UI-39**, dodatni P-UI-40, P-UI-41, P-UI-45, P-UI-47, P-UI-49.

  "READY: none" nije pretpostavljeno ni za jedan queue. STAB16 je BLOCKED (pristup provajderu). DATA_SOURCE_CONNECTOR (QDB) nema READY: QDB07 i QDB08 su WAITING.
- Najveći ID-jevi pre registracije: RQ586, P-UI-53. Validator `check-prompt-queues` prolazi na 703 zadatka.

## 3. Obavezno čitanje i stvarni statusi (§1)

Pročitani su:
- `AGENT_START_HERE.md`, `PROMPT_QUEUE_PROTOCOL.md`, `ANALYTICS_AGENT_SAFETY_GATE.md`, `MASTER_ROADMAP.md` (RQ i P-UI redovi);
- zaglavlje RQ queue-a i svi addendumi, STAB queue, `DECISION_INTELLIGENCE_PROMPT_QUEUE.md`, backlog `ANALYTICS_PRODUCTION_VALUE_PROMPT_BACKLOG_2026-08-19.md`;
- `docs/qa/ANALYTICS_REAUDIT_2026-10-04.md`, `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`, `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md`;
- nivelacija audit (RQ537–RQ559), supplier deep audit, run log `.ai/runs/2026-10-02-analytics-013-42p16-evidence.md`.

Stvarni statusi, provereni i u sekciji i u tabeli (slažu se):

| ID | Status sekcije | Status u tabeli | Napomena |
|---|---|---|---|
| RQ142 | OBSOLETE | OBSOLETE | Merena evaluacija forecast/trend modela. Vlasnik je izuzeo forecast iz opsega. |
| RQ147 | DONE | DONE | Registar dokaza za KPI (backend-owned). |
| RQ148 | DONE | DONE | Bruto/neto/povrat/trošak osnova za prodaju i maržu. |
| RQ149 | DONE | DONE | Ekonomski dokazi za zalihe pre GMROI. |
| RQ150 | OBSOLETE | OBSOLETE | Kalibracija forecasta po kohorti. Izuzeto. |
| RL12 | WAITING | (DI queue) | Kauzalno poređenje ishoda. Planiranje; nema runtime vlasnika. |

Posledica: forecast i kalibracija su svesno van opsega. Kauzalni gate (RL12) nema podatke (§4.3). Nijedan od ova tri ne treba promovisati.

## 4. Falsifikacija tvrdnji (§2)

Svaka poznata tvrdnja je prvo napadnuta, pa tek onda prihvaćena.

| # | Tvrdnja | Pokušaj obaranja | Rezultat |
|---|---|---|---|
| F1 | Prodaja u produkciji staje 05.08.2026 (stari import) | Live `cached/validation/freshness` | **Potvrđeno live:** `lastImport=2026-08-12T10:30:04Z`, `freshnessHours=1282,34`, `critical`. |
| F2 | 4 EF migracije glavnog konteksta nemaju atribute ni Designer fajlove | Skeniranje svih `Infrastructure/Migrations/*.cs` | **Potvrđeno (code)**: `20260327120000_CreateTransfersTables`, `20260327153000_AddTransferLifecycleFields`, `20260404183000_AddArtikliIdTipObuceIndex`, `20260407153000_AddDailySalesStatsIndexes`. Nemaju `[Migration]` ni `[DbContext]`, pa ih EF ne otkriva. **Uticaj je ipak nizak:** transfer šemu kreira bootstrap self-heal (`DatabaseInitializer.cs:2112-2166`), a ID migracije se ručno upisuje u `__EFMigrationsHistory` (`:1432`). Indeksi su redundantni sa `20260327201000_AddAnalyticsPerformanceIndexes` (`datum_prodaje,id_objekat`; `id_prodaja,id_artikal`) i `025` (`IDDobavljac,IDTipObuce`). Pri obimu od 67.517 stavki to nije problem performansi. Pravi rizik je **klasa greške**: buduća migracija bez Designer fajla tiho se ne primenjuje. → RQ588 (P3). |
| F3 | Produkcijske migracije nisu radile dok AutoMigrate nije uključen | Kod readiness-a + live `/ready` | **Prošireno i delimično oboreno**, vidi §4.2. Ni sada nema dokaza da startup inicijalizacija radi sa fail-fast-om. |
| F4 | Bug sa redosledom kolona u 013 view-u | Run log 2026-10-02 + git ancestry | **Popravljeno u repou**: `26e09e46` pomera `supplier_id_at_sale` iza `data_origin` u `Database/Analytics/013_AddSupplierDecisionCompatibilitySchema.sql`. Popravka **jeste** u live runtime-u (`git merge-base --is-ancestor 26e09e46 02f99158` = da). Uprkos tome, live objekti i dalje nedostaju. Uzrok zato nije 013, nego to da se startup sekvenca ne izvršava ili se ne završava sa greškom (§4.2). |
| F5 | Negativni Access ID-jevi | Status u queue-u | Frontend šeme su popravljene (`0360442d`), SQL sentineli RQ560 su DONE, a deploy provera je RQ566 (WAITING). U ovom auditu nije ponovo testirano live → pokriveno, nema novog prompta. |
| F6 | "Failed to fetch" na Pilot/Board/Pulse/Akcije je sistemska backend greška | Vreme UI audita naspram `/ready.startedAtUtc` + re-probe | **Uglavnom oboreno.** UI audit je radio od 21:26 do 21:40 CEST. Live `/ready` kaže `startedAtUtc=19:38:36Z` (21:38:36 CEST) i `readyAtUtc=19:39:00Z`, a `runtime/version.buildTimeUtc=19:38:31Z`. Servis je, dakle, **redeployovan usred UI audita**. U 22:50 Akcije, Pulse i Board vraćaju 200, CORS zaglavlje je ispravno za `https://trendplus.vercel.app`, a Board traje 17,3 s. "Failed to fetch" je najverovatnije prekid tokom restarta (UNPROVEN-RUNTIME, nema logova). Taksonomija `backend_unreachable` (P-UI-49) ostaje ispravna, jer se restart i hladni start ponavljaju. Nema novog backend prompta. |
| F7 | Izveštaj dobavljača: "skup od 180 dana ne postoji" | Live `reports/supplier-decision` za jul | **Potvrđeno live** u varijanti za 90 dana: `MISSING_OBJECT` ("Skup podataka odluke dobavljača za period poslednjih 90 dana ne postoji"). Postoji isti uzrok kao F3 (§4.2): startup verifikacija bi bacila izuzetak da se izvršava sa fail-fast-om (`DatabaseInitializer.cs:746-756`). |
| F8 | RQ586 (vremenska zona smena) je P1 korektnost | Live `daily-sales` za jul + `DailySalesStatsService.cs:878-893` | **Prioritet oboren.** Live metadata kaže `shiftTimeZone="UTC"`, pa je pogrešno vezivanje potvrđeno. Ali svih 307 redova ima `shiftTimestampBasis="legacy_access_wall_clock"` i `shiftAssignmentStatus="no_time_fallback"`. Kod za wall-clock osnovu namerno **ne primenjuje** vremensku zonu. Zato popravka danas ne menja nijedan broj, nego samo labelu u metapodacima. → RQ586 P1 → **P3** (§15). |
| F9 | Kvalitet podataka je popravljen | Live `data-quality/health?toDate=2026-08-06` | **Oboreno:** i dalje `score=100 excellent` i `windowTo=2026-10-04T23:59:59Z`, iako je tražen kraj 06.08. → potvrda za RQ578 (READY). |
| F10 | Akcije su "operativni queue" | Live `actions` | **Oboreno:** `totalCount=4`. Sva 4 zapisa su smoke fixture (`inventory:smoke:final…` P1 `new`, `product:smoke:jsonok`…), a `dataQualityStatus="good"`. → RQ479 (§15) i preporuke šta ne graditi (§13). |

### 4.1 Live rezime (22:50–22:56 CEST)

| Provera | HTTP | Vreme | Ključno |
|---|---|---|---|
| `/api/runtime/version` | 200 | 0,36 s | `02f99158`, build 19:38:31Z, `processType=web` |
| `/ready` | 200 | 0,3 s | `ready=true`, `reason=ready`, start 19:38:36Z, ready 19:39:00Z |
| `/health/dependencies` | 200 | — | `defaultDb` i `analyticsDb` ok |
| `refresh-status` | 200 | 0,3 s | `workersEnabled=false`, `processType=web`, 6 poslova bez statusa, `cacheMode=in-memory` |
| `cached/validation/freshness` | 200 | — | `critical`, import 12.08 |
| `vendor-sales-nivelacija` (jul) | 200 | 0,9 s | `vendor_sales_nivelacija_contract_missing`, nedostaje `change_percent_revenue_semantic` |
| `reports/supplier-decision` (jul) | 200 | 0,8 s | `MISSING_OBJECT` (90d) |
| `decision-board` (jul) | 200 | **17,3 s** | `BOARD_PARTIAL`, `critical`, 9 upozorenja |
| `decision-pulse` (jul) | 200 | 1,9 s | 0 stavki, 24 potisnuto, "Product Decision izvor nije dostupan" |
| `actions` | 200 | 0,3 s | 4 smoke zapisa, 0 pravih |
| `operations-integrity` | 200 | 0,3 s | `unverified` (bucket keševi nedostupni) |
| `data-quality/health` (jul) | 200 | 0,4 s | 100 / excellent, `windowTo` 04.10 |
| `daily-sales` (jul) | 200 | 0,8 s | `shiftTimeZone=UTC`, `no_time_fallback`, 307 wall-clock redova |

### 4.2 Novi nalaz: live readiness dokazuje da startup inicijalizacija baze ne radi sa fail-fast-om

Lanac iz koda (`f1437ed`, isti kod kao u runtime-u `02f99158` za ove fajlove):
1. `Program.cs:99-100,827-832`: kada je `Database:AutoMigrate=true` (web proces, bez workera), readiness zahteva završenu inicijalizaciju baze (`RequireDatabaseInitialization`).
2. `StartupReadinessState.cs:25-38`: `MarkReady()` odbija `ready` dok inicijalizacija nije završena.
3. `DatabaseInitializer.cs:970-986,800-821`: `InitializeTrendplusDbAsync` izvršava 013, pa 014 i 016, i **baca izuzetak** ako `vw_vendor_sales_nivelacija` i dalje nema `change_percent_revenue_semantic`. Supplier MV verifikacija takođe baca izuzetak (`:746-756`).
4. `DatabaseInitializer.cs:87-121`: ako je `DatabaseInitialization:FailFast=true`, greška se propagira. Ako je `false`, upisuje se upozorenje "completed with errors (non-strict mode)" i inicijalizacija se **ipak** prijavljuje kao završena.
5. `DeferredStartupTasksHostedService.cs:189-211`: posle iscrpljenih pokušaja servis nije označen kao spreman. Ako greška nije propagirana, poziva se `MarkDatabaseInitializationCompleted()`.

Live činjenice: servis je `ready` od 21:39:00 CEST, a 70 minuta kasnije view i dalje nema kolonu i MV i dalje ne postoji. To je moguće samo ako važi bar jedno:
- (a) `Database__AutoMigrate` nije efektivno `true` na live servisu, pa se startup SQL uopšte ne izvršava;
- (b) `DatabaseInitialization__FailFast` nije efektivno `true`, pa se greška inicijalizacije guta;
- (c) inicijalizacija radi nad drugom bazom nego endpoint. Ovo je malo verovatno, jer i nivelacija endpoint (`AllEndpoints.cs:3924-3946`) i inicijalizacija koriste `DefaultConnection`.

`render.yaml:35-40` deklariše `AutoMigrate=true`, `FailFast=true` i `RunDatabaseInitialization=true`. Live ponašanje mu protivreči. Render Blueprint se ne primenjuje sam ako sync nije uključen (UNPROVEN-RUNTIME).

Šta to znači: popravke iz RQ519, RQ545, RQ475 i 42P16 su u kodu tačne, ali se možda **nikad ne izvršavaju** u produkciji. Vlasnik to može da proveri za 3 minuta u Render dashboardu, bez novog koda (§22). Repo-local deo je da se ovo više nikad ne pogađa: `/ready` treba da prijavi stanje inicijalizacije → **RQ587**.

### 4.3 Novi nalaz: petlja učenja nema populaciju

Live ledger ima 4 zapisa, svi su smoke fixture iz maja. RL12 (kauzalni gate), RQ557 (knjiga ishoda nivelacije), RQ558 (kontrolisani efekat markdowna), RQ585 (nedeljni pregled iz merenih ishoda) i PROD-AN-14 pretpostavljaju da postoje prave akcije sa ishodima. Pre bilo kakvog modela potrebno je da vlasnik zaista koristi Akcije nekoliko nedelja. Do tada su ovi promptovi **NOT_WORTH_BUILDING_YET** (§13).

## 5. Mapa pokrivenosti (§3), pre bilo kog novog prompta

Klase: FULLY_COVERED (FC), PARTIALLY_COVERED (PC), STALE_PROMPT (SP), WRONG_DEPENDENCY (WD), DUPLICATE (DUP), UNOWNED_GAP (GAP), EXTERNAL_EVIDENCE_ONLY (EXT), NOT_WORTH_BUILDING (NWB).

| # | Nalaz | Klasa | Vlasnik / odluka |
|---|---|---|---|
| 1 | Stari import (05.08 / 12.08) | EXT | Vlasnik pokreće Access import; RQ583 (SLA/baner/alarm) WAITING na RQ569 |
| 2 | Ekrani ne kažu da su podaci stari | FC | RQ569 (primarni READY), RQ583 |
| 3 | Live servis je `ready` iako startup SQL nije primenjen | **GAP** (repo-local vidljivost) + EXT (konfiguracija) | **RQ587** novi; STAB16 addendum za proveru konfiguracije |
| 4 | Pre/Post `contract_missing` | PC | RQ545 PARTIAL (dijagnoza) + addendum §4.2; zavisi od #3 |
| 5 | Supplier MV 90d/180d nedostaju (izveštaj, hub, scorecard) | PC | RQ475 DONE (fail-closed), STAB16, RQ545 addendum |
| 6 | Workeri nisu registrovani (`workersEnabled=false`) | EXT | STAB16 (BLOCKED); `render.yaml` deklariše worker servis, ali ne postoji dokaz da je kreiran |
| 7 | 4 EF migracije bez atributa | GAP (P3 higijena) | **RQ588** novi |
| 8 | Bug 013 42P16 | FC | Popravljeno `26e09e46`, i u runtime-u |
| 9 | Negativni Access ID-jevi | FC | `0360442d`, RQ560 DONE, RQ566 deploy provera |
| 10 | "Failed to fetch" (4 ekrana) | FC (UX) / NWB (backend) | Restart tokom audita; P-UI-49 `backend_unreachable` + addendum |
| 11 | Board 17 s, PDC 9–10 s, 13 MB | PC | RQ573 (PDC payload) + addendum: Board sekvencijalno gradi ceo PDC (`DecisionBoardEndpoints.cs:71-148`); PROD-AN-02 je planirani duplikat |
| 12 | Pulse prazan ("Product Decision izvor nije dostupan") | EXT | `product_decision_snapshot` posao zahteva worker (STAB16) |
| 13 | DQ "100 / odlično" | FC | RQ578 READY (live ponovo potvrđeno) |
| 14 | Verified na praznom skupu (integrity) | FC | RQ579 WAITING na RQ569 |
| 15 | Product Decision 0 primenljivih | FC | RQ573, RQ574 (READY) |
| 16 | Zalihe: vrednost/starost | FC | RQ576 WAITING na RQ569 |
| 17 | Dashboard meša periode | FC | RQ572 |
| 18 | Default periodi od "danas" | FC | RQ570 |
| 19 | Pre-Nivelacija od "danas", STARO/Oprema | FC | RQ571, RQ556 |
| 20 | Boja/kategorija/plaćanje 100% nepoznato | FC (prikaz) / EXT (izvor) | RQ575; Access izvor nema polja |
| 21 | Korpa/transakcija = dnevni dokument? | FC | RQ577 |
| 22 | Smene (vremenska zona) | **SP** (pogrešan prioritet) | RQ586 → P3 |
| 23 | Smoke akcije u produkciji | **WD** (RQ479 nema `Ready after`, "seed-owner gated") | RQ479 popravljen i promovisan (repo-local filter za čitanje, bez brisanja) |
| 24 | Insight Studio stari snapshot-i, mojibake | FC | RQ581 READY, RQ582 |
| 25 | Planni backlog PROD-AN-01..14 (19.08) | **SP / DUP** | Mapiranje na izvršne vlasnike (§15); ne promovisati |
| 26 | Forecast/Trend modeli | NWB | RQ142/RQ150 OBSOLETE; ostaje tako |
| 27 | Kauzalni efekat preporuka / markdowna | NWB (sada) | RL12, RQ558: nema populacije (§4.3) |
| 28 | Multitenancy MT02–MT12 | NWB (sada) | Jedan tenant (pilot) |
| 29 | Transfer TP1↔TP2 predlozi | PC | Inventory rebalance endpoint postoji, a relacija nedostaje (STAB16). Nema novog prompta dok #3/#6 nisu rešeni. |
| 30 | Brojevi su konzistentni između Operations ekrana | FC | RQ561–RQ568 DONE; RQ565/RQ454 produkcijsko sravnjenje WAITING (EXT) |
| 31 | Neobavezni CI za sertifikaciju | FC | RQ453 WAITING (RQ552 → RQ569) |
| 32 | UX trust header / taksonomija / tokeni | FC | P-UI-39..53 |

Rezultat: od 32 nalaza samo 2 su prave repo-local praznine (#3, #7). Dva postojeća prompta su imala pogrešnu metadatu ili prioritet (#22, #23), a jedan planni dokument je zastareo (#25).

## 6. Oblasti A–F

- **A. Svežina i uvoz.** Uvoz je stao 12.08. Ovo je najveći pojedinačni blokator vrednosti. Kod ga ne rešava. RQ569 i RQ583 ga čine vidljivim. → Talas A (vlasnik).
- **B. Produkcijska šema, startup inicijalizacija i migracije.** Nalaz §4.2 (RQ587) i F2 (RQ588). 013 je popravljen. Lanac 014/016/029 u kodu je ispravan i fail-closed, ali na live servisu verovatno ne radi.
- **C. Deploy paritet i workeri.** Runtime `02f99158` je predak `main`-a, tj. kod je relativno svež (main je ispred samo docs commitovima i nekoliko DONE izmena). Workeri ne rade, pa je osvežavanje nepoznato, a Pulse i MV-ovi su prazni. → STAB16 (EXT).
- **D. Ugovori o metrikama i sravnjenje.** Operations su sertifikovani (RQ561–568). Produkcijsko sravnjenje (RQ454/RQ565) čeka pristup. Nema praznine.
- **E. Period i horizont.** RQ569 → RQ570/RQ571/RQ572/RQ584. Nema praznine.
- **F. Product Decision.** RQ573 i RQ574 pokrivaju redosled, payload i kategoriju. Addendum na RQ573: Board latencija (17,3 s live) dolazi od sekvencijalnog `BuildProductDecisionCenterAsync`, pa bi prihvatanje RQ573 trebalo da meri i Board.

## 7. Oblasti G–L

- **G. Dobavljači.** Pregled je tačan (1.561.120 RSD). Izveštaj, hub i scorecard su prazni zbog MV-ova → B/C. RQ580 (labela perioda) je READY. RQ530 je PARTIAL.
- **H. Dimenzije (vrsta obuće, boja).** Vrsta obuće je tačna. Boja je 100% "Nepoznato" jer izvor nema podatke → RQ575 prikaz. Ne graditi analizu boje dok izvor nema podatke (§13).
- **I. Dnevna prodaja.** Brojevi su tačni. Smene su nemerljive (`no_time_fallback`). RQ586 → P3 (F8). RQ584 pokriva `toDate`.
- **J. Nivelacija.** Pre/Post ne radi (B). Pre-Nivelacija prioriteti rade, uz pogrešno sidro (RQ571). RQ552–RQ559 pokrivaju trošak, ishode i efekat. RQ557 i RQ558 nemaju populaciju (§4.3).
- **K. Zalihe.** Vrednost 93.389 RSD za 3.566 pari je pogrešna (RQ576). Forecast, size-curve, rebalance i alerts relacije nedostaju (B/C).
- **L. Dashboard i Pilot readiness.** RQ572. Nema praznine.

## 8. Oblasti M–R

- **M. Kvalitet podataka i integritet.** RQ578 (READY, live ponovo potvrđeno), RQ579.
- **N. Akcije, ishodi i učenje.** Ledger čine samo fixture-i. RQ479 je popravljen. Promptove o učenju ne graditi (§13).
- **O. Board i Pulse.** Board je spor (RQ573 addendum). Pulse zavisi od snapshot posla (STAB16).
- **P. Insight Studio i Advanced.** RQ581 → RQ582 (vlasnik je odlučio: "Eksperimentalno"). 26 starih WAITING promptova (RQ13–RQ38) ne promovisati.
- **Q. Taksonomija grešaka i UX poverenje.** P-UI-49 (taksonomija), P-UI-43/RQ569 (trust header). Live greške su klasifikovane u §12.
- **R. Performanse.** Obim je mali: 5.545 zaglavlja, 67.517 stavki, 12.422 artikla, 2 aktivna objekta. Spori su samo kompozitni endpointi (Board 17 s, bootstrap 18,5 s hladno, PDC 9–10 s). Nema potrebe za M-tier PERF radom dok obim ne poraste (§13).

## 9. Oblasti S–X

- **S. Bezbednost i ID-jevi.** Negativni ID-jevi su pokriveni (F5). Admin dijagnostika vraća 401, što je ispravno. RQ587 izlaže samo enum stanja, bez detalja (STAB pravilo: detalji samo za admina).
- **T. Testovi i CI.** Postoje oracle i golden testovi. RQ453 (neobavezan CI → obavezan) je WAITING u lancu. RQ588 dodaje jedan guard test.
- **U. Observability i health.** `/health` i `/ready` su "healthy" dok analitika ne radi → RQ587. Status Render konfiguracije → STAB16.
- **V. Master podaci.** Kategorija, boja i pol se ne prenose iz Access-a. Razmera `Artikli.NabavnaCena` je sumnjiva → RQ574, RQ575, RQ576. Popravka na izvoru je odluka vlasnika (EXT).
- **W. Forecast i kauzalnost.** Van opsega (RQ142/RQ150 OBSOLETE, RL12 WAITING). Potvrđeno: ne graditi.
- **X. Upravljanje queue-om.** Planni backlog PROD-AN je zastareo (§15). RQ479 nije imao `Ready after`. Prioritet RQ586 je bio prenaduvan. Validatori prolaze.

## 10. Live dokazi (§34)

Samo GET zahtevi, bez autentifikacije i upisa. Period je fiksan, `2026-07-07..2026-08-06` (kraj je ekskluzivan; 05.08 je poslednji dan prodaje). Sirovi odgovori su box-local u `/workspace/nwlive/`, nisu u repou. Detalji su u §4.1 i u evidence fajlu.

## 11. UNPROVEN-RUNTIME

1. Tačna efektivna vrednost `Database__AutoMigrate` i `DatabaseInitialization__FailFast` na live Render servisu (§4.2). Moguće je samo (a) ili (b); šta važi kažu Render dashboard ili logovi.
2. Da li Render worker servis `trendplus-worker` iz `render.yaml` uopšte postoji.
3. Da je "Failed to fetch" tokom UI audita izazvan restartom u 21:38 (vremenska korelacija je jaka, ali nema loga).
4. Da li indeksi iz orphan migracija postoje u produkciji (verovatno ne postoje, ali ekvivalenti postoje).
5. Uzrok praznog Pulse-a (pretpostavka: nema `product_decision_snapshot` jer nema workera).

Sve ostalo u §4.1 je dokazano live.

## 12. Taksonomija grešaka: live greške kao dokaz

| Live simptom | Klasa (design system §7 / P-UI-49) | Pravi uzrok | Vlasnik |
|---|---|---|---|
| "Failed to fetch" (Pilot/Board/Pulse/Akcije, 21:26–21:40) | `backend_unreachable` | Redeploy u 21:38:36 (UNPROVEN) | P-UI-49 (prikaz); bez backend prompta |
| Izveštaj dobavljača "skup … 90/180 dana ne postoji" (`MISSING_OBJECT`) | `source_not_ready` | MV nije kreiran jer startup SQL ne radi (§4.2) | RQ587 (vidljivost), STAB16/RQ545 (popravka) |
| Pre/Post "nije dostupna" + correlation ID (`contract_missing`) | `source_not_ready` + `error_retryable` (kopiranje ID-a) | Isto kao gore | RQ545, RQ587 |
| Board 17 s | `loading_slow` | Sekvencijalni PDC + inventory + supplier | RQ573 addendum, P-UI-49 |
| Pulse 0 stavki, `PULSE_PARTIAL` | `partial` | Nema snapshot posla (worker) | STAB16 |
| DQ 100 / excellent | (lažno "dobro") | `windowTo` ignoriše traženi period | RQ578 |

## 13. Ne graditi (§28)

| Ne graditi (sada) | Zašto | Kada ponovo razmotriti |
|---|---|---|
| Forecast/Trend modeli, kalibracija (RQ142, RQ150) | Vlasnik ih je izuzeo; 60 dana nema svežih podataka | Posle 3 meseca neprekidnog uvoza |
| Kauzalni gate i uplift (RL12), kontrolisani markdown efekat (RQ558) | 0 pravih akcija; 2 objekta; mali broj događaja sniženja | Kada ledger ima ≥ 30 pravih akcija sa izmerenim ishodom |
| Nedeljni pregled iz merenih ishoda (RQ585), knjiga ishoda (RQ557) | Isto, nema populacije | Posle RQ479 i 4–6 nedelja korišćenja Akcija |
| Analiza smena i sati (bilo šta preko RQ586) | Izvor nema vreme dana (`no_time_fallback`) | Kada izvor (npr. SQL Server konektor) donese satnicu |
| Analiza boje, kategorije i plaćanja | 100% "Nepoznato" u izvoru | Kada Access ili import prenese polja |
| Metrike korpe | Zaglavlje je verovatno dnevni dokument (RQ577) | Posle odluke RQ577 |
| Multitenancy MT02–MT12 | Jedan pilot tenant | Pre drugog kupca |
| M-tier perf (PERF) i dodatni indeksi | 67 hiljada stavki; spori su samo kompozitni endpointi | Kada obim poraste 10× |
| Redizajn Insight Studio / Advanced (RQ13–RQ38) | Odluka "Eksperimentalno" (RQ582) | Nikad bez sertifikacije |
| Admin UI konektora (QDB07/QDB08) | Jedan izvor (Access) | Pri drugom izvoru |
| Ponovno primenjivanje 4 orphan migracije | Šema već postoji ili su indeksi redundantni; dodavanje atributa bi pokrenulo duplikate na produkciji | Nikad. RQ588 ih briše ili dokumentuje. |

## 14. Prioritizacija (§29)

Nije sve P0/P1. Raspodela posle ovog rada (samo za ono što ovaj audit dira):
- **P0, operativno (vlasnik, bez koda):** uvoz, provera Render konfiguracije, worker. To nisu promptovi.
- **P1:** RQ587 (bez njega se svaka produkcijska popravka šeme radi naslepo).
- **P2:** RQ479 (lažna P1 akcija u produkciji; jeftino).
- **P3:** RQ588 (higijena), RQ586 (spušten).

Postojeći prioriteti P0/P1 u RQ569/RQ570–RQ577 nisu menjani. Njihova vrednost je potvrđena live.

## 15. Popravke postojećih promptova

1. **RQ586**: Priority P1 → **P3**, status ostaje READY. Dodat je addendum sa live dokazom F8: popravka je ispravna i jeftina, ali danas menja samo labelu `shiftTimeZone`.
2. **RQ479**:
   - dodata metadata koja je nedostajala (`Ready after`, owned/avoid paths);
   - opseg je suzen na repo-local zaštitu pri čitanju: fixture zapisi (prepoznati po `sourceKey` obrascu `:smoke:` ili po eksplicitnom fixture markeru) isključuju se iz operativne liste i brojeva, uz vidljiv brojač "isključeno kao test podaci";
   - brisanje produkcijskih redova ostaje odluka vlasnika;
   - status WAITING → **READY**, prioritet P1 → P2. Promocija je dozvoljena jer nema locka, grane ni PR-a, nijedan READY prompt ne drži `AnalyticsActionsEndpoints.cs`, a familija je posebna.
3. **Addendumi bez promene statusa:**
   - RQ545 (§4.2 lanac);
   - STAB16 (tri provere vlasnika);
   - RQ573 (Board latencija);
   - RQ578 (live ponovo potvrđeno);
   - RQ565 (live greške su prekid tokom deploya);
   - P-UI-49 (vremenska korelacija "Failed to fetch" sa restartom; `backend_unreachable` ostaje).
4. **Backlog PROD-AN-01..14**: dodata je datirana napomena o zastarelosti, sa mapiranjem na izvršne vlasnike:
   - 01 → STAB15/STAB16;
   - 02 → RQ573 + addendum;
   - 03 → RQ580/RQ530;
   - 04 → RQ576;
   - 05 → RQ578;
   - 06 → RQ576;
   - 07 → RQ453;
   - 08 → RQ585;
   - 09 → RQ479;
   - 10 → RQ574/RQ575/RQ576;
   - 11 → RQ569/RQ572;
   - 12 → RQ545/RQ587/STAB16;
   - 13 → RQ557/RQ558;
   - 14 → RQ557/RL12.

   Nijedan nije promovisan.

## 16. Novi promptovi

- **RQ587** (P1, READY), `startup-database-initialization-truth`: `/ready` i `runtime/version` prijavljuju da li je inicijalizacija baze zahtevana i da li je završena (čisto, sa greškama ili neuspešno), kao enum bez detalja. Detalji (imena skripti i relacija) idu samo admin-only. Kada FailFast nije uključen, završetak sa greškama se ne sme prikazati kao čist `ready`.
- **RQ588** (P3, READY), `ef-migration-discovery-guard`: test da svaka `Migration` klasa ima `[Migration]` i `[DbContext]`, plus razrešenje 4 orphan fajla (brisanje uz dokumentovan razlog, bez primene na produkciji).

Pun tekst je u `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`.

## 17. Tabela promptova (§37)

| ID | Status | P | Porodica | Svrha | Ready after | Paralelno |
|---|---|---|---|---|---|---|
| RQ587 | READY | P1 | startup-database-initialization-truth | Readiness prijavljuje stanje startup inicijalizacije | — | da (Program.cs `/ready` i `runtime/version` handleri, Startup servisi; nijedan READY ne drži te putanje) |
| RQ588 | READY | P3 | ef-migration-discovery-guard | Guard test + razrešenje 4 orphan migracije | — | da (samo `Infrastructure/Migrations` orphan fajlovi + jedan test) |
| RQ479 | READY (bilo WAITING) | P2 (bilo P1) | analytics-actions-live-fixture-hygiene | Fixture zapisi van operativne liste, bez brisanja | — | da |
| RQ586 | READY | P3 (bilo P1) | daily-sales-timezone-config | Nepromenjen opseg | — | da |

## 18. Promocija i kaskada (§38)

- **RQ READY posle ovog rada:** primarni **RQ569** (nepromenjeno). Dodatni: RQ553, RQ574, RQ578, RQ580, RQ581, RQ586, **RQ587**, **RQ588**, **RQ479**.
- **P-UI:** bez promene (primarni P-UI-39; dodatni P-UI-40, P-UI-41, P-UI-45, P-UI-47, P-UI-49).
- **STAB16:** ostaje BLOCKED. Addendum navodi da je prvi korak vlasnika provera konfiguracije, a ne pristup bazi.
- **Kaskada:** RQ587 ne odblokira nijedan WAITING prompt sam po sebi. Posle RQ587 i ispravke Render konfiguracije, RQ545 može da pređe u završnu proveru (iz addenduma), a RQ454/RQ565 dobijaju čvrst preduslov. RQ479 → RQ557/RQ585 tek kada ledger ima prave akcije (§13).
- Ažurirani su zaglavlje RQ queue-a (linija 5), dodata je linija o registraciji i ažuriran je RQ red u `MASTER_ROADMAP.md`.

## 19. Talasi A–E (§39)

- **Talas A (vlasnik, danas, oko 30 min, bez koda):**
  1. Pokrenuti Access import.
  2. U Render dashboardu proveriti efektivne `Database__AutoMigrate`, `DatabaseInitialization__FailFast` i `StartupTasks__RunDatabaseInitialization` na `trendplus-api`, i da li postoji `trendplus-worker`.
  3. Posle restarta pročitati startup log (traži se "Supplier nivelacija dependencies verified" ili izuzetak).
- **Talas B (READY, paralelno):** RQ569 (primarni), RQ587, RQ578, RQ574, RQ479, RQ580, RQ581, RQ553; RQ586 i RQ588 usput.
- **Talas C (posle RQ569):** RQ570, RQ571, RQ572, RQ573 (+ Board), RQ576, RQ579, RQ583, RQ584, RQ552 → RQ453.
- **Talas D (posle talasa A i STAB16):** RQ545 završna provera, RQ454, RQ565, RQ566, Pulse i hub ponovo živi.
- **Talas E (vrednost, tek uz svež uvoz i prave akcije):** RQ559 (dopuna po veličini), RQ530, RQ557, RQ585. RQ558 i RL12 tek uz populaciju.

## 20. Test poslovne vrednosti (§40)

| Prompt | Koja odluka postaje bolja | Ko | Koliko često | Šta se desi ako se ne uradi |
|---|---|---|---|---|
| RQ587 | "Da li je produkcija stvarno primenila popravku?", tj. da li se tom ekranu sme verovati | Vlasnik / dežurni agent | Pri svakom deployu | Svaka popravka šeme (RQ545, RQ475, 42P16) se proverava naslepo. Tri dana su već izgubljena. |
| RQ479 | Koje akcije su prave | Vlasnik | Svaki put kad otvori Akcije | Lažna P1 akcija "Smoke Inventory Final" i lažni brojevi |
| RQ588 | Nijedna, posredno: sprečava tihu ne-primenu buduće migracije | Agenti | Pri svakoj migraciji | Mali rizik; P3 |
| RQ586 | Nijedna danas (wall-clock podaci) | — | — | Labela "UTC" u metapodacima |

## 21. Odluke vlasnika (§33)

Samo jedna, i to neizbežna:
- **Render konfiguracija (talas A):** da li je `trendplus-api` sinhronizovan sa `render.yaml` (AutoMigrate, FailFast) i da li postoji `trendplus-worker`. Preporuka: uključiti obe vrednosti i pratiti prvi restart. Ako inicijalizacija padne, servis neće biti `ready`, što je namerno: bolje vidljiv pad nego tiho pogrešni ekrani.

Brisanje smoke zapisa iz produkcije **nije** potrebno za RQ479. Može kasnije, uz izričitu odluku.

## 22. Provider i eksterni koraci (EXTERNAL_EVIDENCE_ONLY)

1. Access import (vlasnik, sa računara u radnji).
2. Render env: AutoMigrate, FailFast, RunDatabaseInitialization; da li postoji worker servis (STAB16).
3. Render startup log posle restarta (RQ545).
4. Read-only konekcija za sravnjenje (RQ454/RQ565).
5. Izvor master podataka: kategorija, boja, pol, razmera nabavne cene (RQ574/RQ575/RQ576).

## 23. Rizici

- Kad se FailFast stvarno uključi, servis može ostati `not ready` dok se šema ne popravi. To je ispravno ponašanje, ali znači da UI neće raditi dok vlasnik ne reaguje. Preporuka: uključiti van radnog vremena i pratiti log.
- RQ587 dira `Program.cs`, vruć fajl sa mnogo vlasnika. Opseg je ograničen na dva handlera.
- Codex radi paralelno, pa je pre isporuke urađen ponovni fetch (§27).

## 24. Šta je stvarno testirano

- 16 live GET provera (§4.1), 22:50–22:56 CEST.
- Code tracing za readiness lanac (§4.2), shift logiku (F8), Board kompoziciju (F6/#11) i orphan migracije (F2).
- `git merge-base --is-ancestor 26e09e46 02f99158` (popravka 42P16 jeste u runtime-u).
- GitHub API: 0 otvorenih PR-ova. `git ls-remote`: 28 grana, sve stare i DONE.
- Validatori `check-agent-instructions`, `check-prompt-queues` i `check-planning-architecture` (sa `--self-test` i bez njega) i `git diff --check` (§27).
- Nisu pokretani .NET ni frontend testovi: ovo je promena samo u dokumentaciji i queue-u.

## 25. Pitanja kvaliteta (§41)

- *Da li je svaka tvrdnja prvo napadnuta?* Da, §4 (F1–F10). F6 i F8 su oborene, a F2 i F4 su sužene.
- *Da li je mapa pokrivenosti napravljena pre novih promptova?* Da, §5. Iz nje su proizašla samo 2 nova prompta.
- *Da li neki novi prompt duplira aktivnog vlasnika?* Ne. RQ587 je vidljivost readiness-a, dok RQ545 i STAB16 rade dijagnozu i popravku. RQ588 je jedini vlasnik migration guard-a.
- *Da li je sve P0/P1?* Ne. Novi su P1 i P3, a jedan postojeći je spušten.
- *Da li postoji live dokaz ili je jasno označeno UNPROVEN?* Da, §4.1 i §11.
- *Da li je "READY: none" pretpostavljen?* Ne, §2.
- *Da li se frontend traži da izmišlja istinu?* Ne. RQ479 je backend filter, a RQ587 je backend stanje.
- *Da li je odluka vlasnika neizbežna?* Samo jedna (§21).
- *Šta je najbolji sledeći prompt?* Ukupno **RQ569**. Najveći dobitak po trošku je **talas A** (vlasnik, 30 min), a od koda **RQ587**.

## 26. Ograničenja

Nema pristupa bazi, Render dashboardu, logovima ni admin ključu. Ekrani nisu renderovani u browseru u ovom auditu; UX dokazi su preuzeti iz `ANALYTICS_UX_UI_AUDIT_2026-10-04.md`. Nalaz §4.2 je logički dokaz iz koda i live readiness-a, ne direktno očitavanje konfiguracije.

## 27. Izlazi i isporuka

- Ovaj dokument.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`: zaglavlje, RQ587, RQ588, popravke RQ479 i RQ586, addendumi RQ573 i RQ578.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_NIVELACIJA_AUDIT_ADDENDUM.md` (RQ545), `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_OPERATIONS_ACCURACY_ADDENDUM.md` (RQ565), `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` (STAB16), `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` (P-UI-49), `docs/ai/ANALYTICS_PRODUCTION_VALUE_PROMPT_BACKLOG_2026-08-19.md` (napomena o zastarelosti).
- `MASTER_ROADMAP.md` (RQ red i datirana napomena).
- `.ai/runs/2026-10-04-analytics-next-wave-audit-evidence.md`.
- Box-local: `/workspace/nwlive/` (sirovi live odgovori), `/workspace/out/next-wave/` (bundle i patch).
