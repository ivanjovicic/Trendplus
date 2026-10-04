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

Fiksni prozor **2026-07-07..2026-08-05** je trenutno najbolje cross-screen sertifikovan produkcijski prozor: Dnevna prodaja, Dobavljači, Vrsta obuće, Boja i datirani Dashboard daju isti promet od **1.561.120 RSD i 307 komada**. To ne dokazuje da je svaki drugi istorijski period pogrešan; dokazuje da je ovaj prozor trenutno najjača zajednička referenca. Za **aktuelne** odluke produkcijska analitika trenutno nije dovoljno sveža: poslednji uspešan import je 12.08.2026, a observed sales horizon 05.08.2026.

Tri najvažnija operativna zaključka su:

1. **Uvoz je stao.** Starost poslednjeg importa i starost poslednje posmatrane prodaje nisu ista stvar: import je ~53 dana star u trenutku audita, dok je sales horizon ~60 dana iza 04.10.2026. RQ569/RQ583 moraju ta dva sata da prikazuju odvojeno.
2. **Startup/schema konvergencija nije dokazano stroga.** Live `/ready=true` uz missing Pre/Post kolonu i supplier MV snažno pokazuje da efektivni runtime nije sledio očekivani strict initialization put, ali sam javni dokaz ne identifikuje jedini uzrok. Kandidati su: AutoMigrate nije efektivan, FailFast/non-strict init je završio uz grešku, efektivna config/connection/runtime putanja se razlikuje, ili je schema drift nastao nakon readiness-a. RQ587 + STAB16/RQ545 sada razdvajaju te hipoteze.
3. **Analytics Actions ledger trenutno nema realnu korisničku populaciju.** Live postoje 4 smoke zapisa. To blokira tvrdnje o adoption/outcome učenju iz **Actions** feed-a, ali **ne blokira** Nivelacija price-event outcome ledger (RQ557) niti budući nedeljni digest RQ585, koji imaju druge izvore i zavisnosti.

Originalni audit je registrovao RQ587/RQ588 i popravio RQ479/RQ586. Post-review je dodatno ispravio RQ545/RQ557/RQ558/RQ573/RQ578/STAB16/RQ587/RQ588 i registrovao **PERF19** kao mereni Decision Board performance follow-up posle RQ573. Cilj korekcija je manje novih promptova, ali preciznije granice dokaza i manje lažnih blokera.

## 2. Osnova, stanje repoa i queue-a (§0)

- `origin/main` = `f1437ed8`, fetchovan u 22:47 CEST. Posle UX audita (`61992ef`) Codex je dodao 13 commitova, svi su docs/queue: P-UI-53 za pristupačnost grafikona, korekcije UX audita i rutiranje.
- Otvoreni PR-ovi: **0** (GitHub API).
- Udaljene grane: 28. Sve osim `main` pripadaju DONE promptovima (RQ474–RQ536, P-UI-24–27, RQ564), sa poslednjim commitom između 30.09. i 04.10. Nijedna ne drži familiju koju ovaj audit dira.
- Lokalni lockovi: `.ai/task-locks/` ne postoji ili je prazan. Nema aktivnog vlasnika.
- READY stanje pre ovog rada, proveravano po sekciji a ne po zaglavlju:
  - RQ: primarni **RQ569**, dodatni RQ553, RQ574, RQ578, RQ580, RQ581, RQ586;
  - P-UI u originalnom snapshot-u je bio zapisan sa P-UI-45 kao dodatnim READY, ali je same-day collision review to supersedovao: canonical current READY je **P-UI-39** primary + P-UI-40, P-UI-41, P-UI-47, P-UI-49; P-UI-45 čeka P-UI-40 + P-UI-48 zbog `AppLayout.tsx` kolizije.

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
| F2 | 4 EF migracije glavnog konteksta nemaju atribute ni Designer fajlove | Skeniranje svih `Infrastructure/Migrations/*.cs` | **Potvrđeno (code)**: `20260327120000_CreateTransfersTables`, `20260327153000_AddTransferLifecycleFields`, `20260404183000_AddArtikliIdTipObuceIndex`, `20260407153000_AddDailySalesStatsIndexes`. Nemaju `[Migration]` ni `[DbContext]`, pa ih EF ne otkriva. **Trenutni uticaj nije izmeren kao incident:** transfer šemu kreira bootstrap self-heal (`DatabaseInitializer.cs:2112-2166`), a jedan ID se ručno upisuje u `__EFMigrationsHistory`; index DDL delimično preklapa raniju pokrivenost. Audit nije merio performansni delta tih orphan migracija, pa ne tvrdi da je njihov uticaj nula. Pravi dokazani rizik je **klasa greške**: buduća production-intended migracija može tiho ostati nediscoverable. → RQ588 (P3), sada zasnovan na EF discovery contract-u i eksplicitnom legacy allowlist-u. |
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

### 4.2 Novi nalaz: readiness + missing schema objekti snažno ukazuju na startup/schema convergence problem, ali ne dokazuju jedini uzrok

Lanac iz koda:
1. Kada je web proces i `Database:AutoMigrate=true`, startup readiness zahteva završenu inicijalizaciju baze.
2. `StartupReadinessState` ne može čisto da označi servis spremnim dok je required DB initialization pending.
3. `DatabaseInitializer` proverava/repair-uje upravo Pre/Post contract i supplier decision objekte; uz strict FailFast neprolazna verifikacija propagira grešku.
4. Uz non-strict FailFast deo grešaka može završiti kao "completed with errors".
5. `DeferredStartupTasksHostedService` markira initialization completed samo kada je njegova pozvana putanja završila bez propagirane greške.

Retained live činjenice: servis je bio `ready` od 21:39 CEST, dok su kasnije i dalje nedostajali `change_percent_revenue_semantic` i supplier 90d dataset. To isključuje tvrdnju da je **isti required strict initializer u tom procesu još uvek pending/throwing**, ali ne bira automatski jedan uzrok.

Hipoteze koje ostaju:
- `Database__AutoMigrate` nije efektivan na tom servisu;
- `DatabaseInitialization__FailFast`/effective init path je non-strict pa je greška zabeležena i startup nastavljen;
- runtime koristi drugačiju efektivnu config/connection/process putanju od očekivane;
- inicijalizacija je pri startup-u prošla, a objekat je kasnije promenjen/obrisan ili je došlo do drugog **post-readiness schema drift-a**.

`render.yaml` deklariše AutoMigrate/FailFast/RunDatabaseInitialization, ali YAML nije dokaz efektivnih Render vrednosti. Zato provider config + startup log + admin contract diagnostic ostaju neophodni.

Repo-local cilj RQ587 je **vidljivost startup ishoda**, ne nova schema-certification tvrdnja: public `/ready` pokazuje safe state, a tačne efektivne flagove/stage vidi samo admin-authorized diagnostic. `/api/runtime/version` ostaje deployment identity. Čak i `succeeded` ne znači "schema je i sada zdrava".

### 4.3 Analytics Actions ledger nema realnu populaciju — ali ne blokira sve outcome/value promptove

Retained live Actions ledger ima 4 zapisa i sva četiri su smoke fixture-i iz maja. To znači:
- Actions adoption/outcome statistika trenutno nema realnu pilot populaciju;
- smoke redovi moraju biti quarantined iz operativnih list/count/outcome/Board projekcija → RQ479;
- runtime calibration ili causal claim zasnovan na Analytics Actions ne bi imao dovoljno realnih podataka.

Ali prethodni audit je preširoko iz toga zaključio da su **RQ557 i RQ585** bez populacije:
- **RQ557** koristi Nivelacija price-event + prodaju + cost evidence, ne Analytics Actions. Njegov prompt je sada sužen na **deskriptivni mature-markdown outcome ledger**, bez "ponoviti/izbegavati", bez causal tvrdnje i bez feedback-a u scoring. Historical-stock metrike ostaju null dok nema certified dated-stock izvora. Čeka samo RQ553 zbog shared Pre/Post page ownership-a.
- **RQ585** je nedeljni owner digest iz certified current signal families. Ne zahteva prethodno merene Actions outcomes; njegove stvarne zavisnosti ostaju RQ570/RQ573/RQ574/RQ576 + RQ583 freshness SLA.
- **RQ558/RL12** ostaju kasniji causal/controlled-effect sloj. RQ558 sada eksplicitno čeka RQ557 + dokazanu dovoljnu mature/control populaciju; trenutna category/control coverage nije dovoljna.


## 5. Mapa pokrivenosti (§3), pre bilo kog novog prompta

Klase: FULLY_COVERED (FC), PARTIALLY_COVERED (PC), STALE_PROMPT (SP), WRONG_DEPENDENCY (WD), DUPLICATE (DUP), UNOWNED_GAP (GAP), EXTERNAL_EVIDENCE_ONLY (EXT), NOT_WORTH_BUILDING (NWB).

| # | Nalaz | Klasa | Vlasnik / odluka |
|---|---|---|---|
| 1 | Stari import (05.08 / 12.08) | EXT | Vlasnik pokreće Access import; RQ583 (SLA/baner/alarm) WAITING na RQ569 |
| 2 | Ekrani ne kažu da su podaci stari | FC | RQ569 (primarni READY), RQ583 |
| 3 | Live servis je `ready` dok startup-owned analytics objekti nedostaju | **GAP** (startup-outcome vidljivost) + EXT (efektivna config/log/schema-drift klasifikacija) | **RQ587** + STAB16/RQ545; uzrok nije jedinstveno dokazan public GET-om |
| 4 | Pre/Post `contract_missing` | PC | RQ545 PARTIAL (dijagnoza) + addendum §4.2; zavisi od #3 |
| 5 | Supplier MV 90d/180d nedostaju (izveštaj, hub, scorecard) | PC | RQ475 DONE (fail-closed), STAB16, RQ545 addendum |
| 6 | Web proces nema heavy worker registraciju i nema durable successful refresh run evidence | EXT | STAB16; process-local `workersEnabled=false` **ne dokazuje** da zaseban Render worker servis ne postoji |
| 7 | 4 EF migracije bez atributa | GAP (P3 higijena) | **RQ588** novi |
| 8 | Bug 013 42P16 | FC | Popravljeno `26e09e46`, i u runtime-u |
| 9 | Negativni Access ID-jevi | FC | `0360442d`, RQ560 DONE, RQ566 deploy provera |
| 10 | "Failed to fetch" (4 ekrana) | FC (UX) / NWB (backend) | Restart tokom audita; P-UI-49 `backend_unreachable` + addendum |
| 11 | Board 17 s, PDC 9–10 s, 13 MB | PC | RQ573 prvo slimuje/meri PDC + Board; **PERF19** posle RQ573 meri contributor timings i optimizuje samo dokazani dominantni uzrok; PDC nije dokazan kao jedini uzrok 17 s |
| 12 | Pulse prazan ("Product Decision izvor nije dostupan") | EXT/PC | Snapshot/materialization nije dostupan; STAB16 koreliše worker servis + durable run history. Web-process status sam ne dokazuje uzrok |
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
| 27 | Kauzalni/controlled markdown efekat | NWB (runtime sada) | RQ558/RL12: čeka deskriptivni RQ557 + dokaz mature/control sample; nije blokirano praznim Analytics Actions ledgerom |
| 28 | Multitenancy MT02–MT12 | NWB (sada) | Jedan tenant (pilot) |
| 29 | Transfer TP1↔TP2 predlozi | PC | Inventory rebalance endpoint postoji, a relacija nedostaje (STAB16). Nema novog prompta dok #3/#6 nisu rešeni. |
| 30 | Brojevi su konzistentni između Operations ekrana | FC | RQ561–RQ568 DONE; RQ565/RQ454 produkcijsko sravnjenje WAITING (EXT) |
| 31 | Neobavezni CI za sertifikaciju | FC | RQ453 WAITING (RQ552 → RQ569) |
| 32 | UX trust header / taksonomija / tokeni | FC | P-UI-39..53 |

Rezultat originalne mape bio je 2 nova RQ gap-a (#3, #7), ali post-review je otkrio još jedan **measured owner gap** koji nije nova analytics semantika: Decision Board composition performance nema izvršni post-RQ573 owner → PERF19. Takođe su ispravljene pogrešne zavisnosti RQ557/RQ585 i precizirane RQ545/RQ578/STAB16 granice.

## 6. Oblasti A–F

- **A. Svežina i uvoz.** Uvoz je stao 12.08. Ovo je najveći pojedinačni blokator vrednosti. Kod ga ne rešava. RQ569 i RQ583 ga čine vidljivim. → Talas A (vlasnik).
- **A2. Temporal/backfill i as-of master.** Fresh import nije preduslov da se ovaj rizik analizira. Supplier i Shoe Type history su već značajno hardened kroz RQ411: sale-line `SupplierIdAtSale`/`ShoeTypeIdAtSale` + `AttributionBasis`, a legacy backfill je eksplicitno `frozen_current_master_backfill`, pa kasnija promena mastera ne preklasifikuje te dve dimenzije. RQ450 već pokreće bounded integrity probe posle uspešnog Access importa. Preostale mutable dimenzije (npr. boja/kategorija/sezona u pojedinim path-ovima) nemaju dovoljno izvorne pokrivenosti da opravdaju novi SCD runtime prompt sada; zahtevaju prvo eksplicitan current-master-vs-as-of contract kada izvor postane popunjen. To je **NWB sada**, ne "neanalizirano".
- **B. Produkcijska šema, startup inicijalizacija i migracije.** Nalaz §4.2 (RQ587) i F2 (RQ588). 013 fix je u runtime ancestry-ju. Public evidence pokazuje nekonvergiranu live šemu, ali ne bira između config/non-strict/alternate-path/post-ready-drift hipoteza bez provider/admin dokaza.
- **C. Deploy paritet i workeri.** Runtime `02f99158` je bio recent code runtime. Web proces namerno ne registruje heavy workere; `refresh-status` ipak čita durable `AnalyticsRefreshRuns` pre process-local fallback-a. Trenutno je dokazano da required job families nemaju usable durable success evidence vidljiv API-ju, ne da worker servis sigurno ne postoji. Provider worker + run-history korelacija → STAB16.
- **D. Ugovori o metrikama i sravnjenje.** Operations su sertifikovani (RQ561–568). Produkcijsko sravnjenje (RQ454/RQ565) čeka pristup. Nema praznine.
- **E. Period i horizont.** RQ569 → RQ570/RQ571/RQ572/RQ584. Nema praznine.
- **F. Product Decision.** RQ573/RQ574 pokrivaju redosled, payload i kategoriju. Board od 17,3 s sekvencijalno čeka više izvora; PDC je poznat skup deo, ali nije dokazan kao jedini dominantni uzrok. RQ573 meri PDC+Board pre/posle; PERF19 zatim profilira contributor timings samo ako Board i dalje probija postojeći <=2 s p95 budget.

## 7. Oblasti G–L

- **G. Dobavljači.** Pregled je tačan (1.561.120 RSD). Izveštaj, hub i scorecard su prazni zbog MV-ova → B/C. RQ580 (labela perioda) je READY. RQ530 je PARTIAL.
- **H. Dimenzije (vrsta obuće, boja).** Vrsta obuće je tačna. Boja je 100% "Nepoznato" jer izvor nema podatke → RQ575 prikaz. Ne graditi analizu boje dok izvor nema podatke (§13).
- **I. Dnevna prodaja.** Brojevi su tačni. Smene su nemerljive (`no_time_fallback`). RQ586 → P3 (F8). RQ584 pokriva `toDate`.
- **J. Nivelacija.** Pre/Post live schema i dalje nije konvergirala (B). Pre-Nivelacija ima RQ571 horizon/population rad. RQ557 sada ima validnu price-event populaciju nezavisno od Actions ledger-a, ali je sužen na deskriptivne mature-markdown ishode i čeka RQ553 path ownership. RQ558 ostaje kasniji controlled-effect rad tek uz dokazanu mature/control pokrivenost.
- **K. Zalihe.** Vrednost 93.389 RSD za 3.566 pari je pogrešna (RQ576). Forecast, size-curve, rebalance i alerts relacije nedostaju (B/C).
- **L. Dashboard i Pilot readiness.** RQ572. Nema praznine.

## 8. Oblasti M–R

- **M. Kvalitet podataka i integritet.** RQ578 (READY, live ponovo potvrđeno), RQ579.
- **N. Akcije, ishodi i učenje.** Analytics Actions ledger čine samo fixture-i → RQ479 quarantine. To blokira Actions-based adoption/outcome učenje, ali ne price-event outcome RQ557 niti signal-digest RQ585. Runtime causal/calibration ostaje kasnije.
- **O. Board i Pulse.** Board je spor: RQ573 prvo rešava poznati PDC payload i meri Board; PERF19 zatim radi measured composition profiling ako p95 i dalje probija budget. Pulse nema snapshot/source rezultat; STAB16 dokazuje worker/materialization/run-history uzrok umesto pretpostavke.
- **P. Insight Studio i Advanced.** RQ581 → RQ582 (vlasnik je odlučio: "Eksperimentalno"). 26 starih WAITING promptova (RQ13–RQ38) ne promovisati.
- **Q. Taksonomija grešaka i UX poverenje.** P-UI-49 (taksonomija), P-UI-43/RQ569 (trust header). Live greške su klasifikovane u §12.
- **R. Performanse.** Mali trenutni obim ne opravdava nasumične indekse ili generalni scaling projekat, ali **17,3 s Board**, 9–10 s PDC i 18,5 s cold bootstrap su realni UX/reliability problemi. Postoji Decision Board budget (p95 <=2 s) i measured PERF metodologija. Zato je dodat PERF19 posle RQ573: prvo profilisanje, zatim samo dokazani bounded fix.

## 9. Oblasti S–X

- **S. Bezbednost i ID-jevi.** Negativni ID-jevi su pokriveni (F5). Admin dijagnostika vraća 401, što je ispravno. RQ587 izlaže samo enum stanja, bez detalja (STAB pravilo: detalji samo za admina).
- **T. Testovi i CI.** Postoje oracle i golden testovi. RQ453 (neobavezan CI → obavezan) je WAITING u lancu. RQ588 dodaje jedan guard test.
- **U. Observability i health.** Process readiness, analytics schema integrity i data freshness su različite ose. RQ587 izlaže startup-init outcome; RQ545/integrity dokazuju schema/contract; RQ569/RQ583 data horizon/freshness. `ready=true` se više ne tumači kao "analytics schema je zdrava".
- **V. Master podaci.** Supplier/Shoe Type istorijska atribucija je već zamrznuta/provenanced kroz RQ411; zato as-of problem nije globalno otvoren. Kategorija/boja/pol su trenutno slabo ili potpuno nepopunjeni u Access podacima, a deo drugih path-ova koristi current master. Ne uvoditi SCD samo zbog teorije; kada izvor postane popunjen, prvo odobriti eksplicitan `current_master` vs `as_of_sale` contract. Cost/valuation ostaje RQ576.
- **W. Forecast i kauzalnost.** Forecast runtime/model work ostaje van opsega (RQ142/RQ150 OBSOLETE po owner odluci). RL12 ostaje future causal-claim gate; RQ558 ostaje WAITING zbog mature/control evidence, ne zbog praznog Actions ledger-a. Ne graditi runtime causal calibration sada.
- **X. Upravljanje queue-om.** Planni backlog PROD-AN je zastareo (§15). RQ479 nije imao `Ready after`. Prioritet RQ586 je bio prenaduvan. Validatori prolaze.

## 10. Live dokazi (§34)

Samo GET zahtevi, bez autentifikacije i upisa. Period je fiksan, `2026-07-07..2026-08-06` (kraj je ekskluzivan; 05.08 je poslednji dan prodaje). Sirovi odgovori su box-local u `/workspace/nwlive/`, nisu u repou. Detalji su u §4.1 i u evidence fajlu.

## 11. UNPROVEN-RUNTIME

1. Koja efektivna startup hipoteza objašnjava ready+missing-schema kombinaciju: AutoMigrate off, non-strict/failfast behavior, druga efektivna config/connection/runtime putanja ili post-readiness schema drift.
2. Da li Render `trendplus-worker` servis postoji/radi. Web `workersEnabled=false` to ne dokazuje; API samo nema usable durable refresh-run dokaz za required jobs.
3. Da je "Failed to fetch" tokom UI audita izazvan restartom u 21:38 (vremenska korelacija je jaka, ali nema provider loga).
4. Da li orphan-index DDL postoji u produkciji i da li njegov izostanak ima merljiv query-cost efekat; audit to nije benchmarkovao.
5. Zašto je Pulse source prazan: snapshot/materialization je nedostupan, ali worker existence/run failure nije dokazan samo iz web statusa.
6. Dominantni contributor Board 17,3 s latencije; sequential code shape je poznat, per-section deployed timings nisu.

Live GET vrednosti u §4.1 su retained audit evidence sa runtime-a `02f99158`; ovaj post-review ih nije ponovo mrežno reprodukovao.

## 12. Taksonomija grešaka: live greške kao dokaz

| Live simptom | Klasa (design system §7 / P-UI-49) | Pravi uzrok | Vlasnik |
|---|---|---|---|
| "Failed to fetch" (Pilot/Board/Pulse/Akcije, 21:26–21:40) | `backend_unreachable` | Redeploy u 21:38:36 (UNPROVEN) | P-UI-49 (prikaz); bez backend prompta |
| Izveštaj dobavljača "skup … 90/180 dana ne postoji" (`MISSING_OBJECT`) | `source_not_ready` | Live schema/materialized source nije konvergirao; tačan startup/provider uzrok UNPROVEN | RQ587 (startup outcome), STAB16/RQ545 (dijagnoza/popravka) |
| Pre/Post "nije dostupna" + correlation ID (`contract_missing`) | `source_not_ready` + `error_retryable` (kopiranje ID-a) | View postoji ali contract nije konvergirao; startup/config/drift uzrok tek treba klasifikovati | RQ545, RQ587, STAB16 |
| Board 17 s | `loading_slow` | Sekvencijalna kompozicija; dominantni contributor nije izmeren | RQ573 → PERF19; P-UI-49 za sporo-loading stanje |
| Pulse 0 stavki, `PULSE_PARTIAL` | `partial` | Product Decision snapshot/source nije dostupan; worker/run uzrok UNPROVEN | STAB16 |
| DQ 100 / excellent | (lažno "dobro") | `windowTo` ignoriše traženi period | RQ578 |

## 13. Ne graditi (§28)

| Ne graditi (sada) | Zašto | Kada ponovo razmotriti |
|---|---|---|
| Forecast/Trend modeli, kalibracija (RQ142, RQ150) | Vlasnik ih je izuzeo; 60 dana nema svežih podataka | Posle 3 meseca neprekidnog uvoza |
| Runtime causal calibration / kontrolisani markdown efekat (RQ558; RL12 claim layer) | Nedovoljno dokazana mature/control/category populacija; causal tvrdnja nije dozvoljena iz before/after | Posle RQ557 + eksplicitnog sample/coverage dokaza i owner promocije |
| Actions-based recommendation learning/adoption calibration | Produkcijski Actions feed ima samo smoke zapise | Posle RQ479 i stvarnih akcija sa mature outcomes; ne mešati sa RQ557 price-event ledgerom |
| Analiza smena i sati (bilo šta preko RQ586) | Izvor nema vreme dana (`no_time_fallback`) | Kada izvor (npr. SQL Server konektor) donese satnicu |
| Analiza boje, kategorije i plaćanja | 100% "Nepoznato" u izvoru | Kada Access ili import prenese polja |
| Metrike korpe | Zaglavlje je verovatno dnevni dokument (RQ577) | Posle odluke RQ577 |
| Multitenancy MT02–MT12 | Jedan pilot tenant | Pre drugog kupca |
| Spekulativni indeksi / široki scaling projekat | Trenutni obim sam po sebi ne opravdava tuning bez profila | Ne čekati 10× za dokazano spor Board: PERF19 posle RQ573 već meri 17,3 s problem i radi samo bounded measured fix |
| Redizajn Insight Studio / Advanced (RQ13–RQ38) | Odluka "Eksperimentalno" (RQ582) | Nikad bez sertifikacije |
| Admin UI konektora (QDB07/QDB08) | Jedan izvor (Access) | Pri drugom izvoru |
| Ponovno primenjivanje 4 orphan migracije | Šema već postoji ili su indeksi redundantni; dodavanje atributa bi pokrenulo duplikate na produkciji | Nikad. RQ588 ih briše ili dokumentuje. |

## 14. Prioritizacija (§29)

Nije sve P0/P1. Raspodela posle ovog rada (samo za ono što ovaj audit dira):
- **P0, operativno (vlasnik, bez koda):** uvoz, provera Render konfiguracije, worker. To nisu promptovi.
- **P1:** RQ587 (bez njega se svaka produkcijska popravka šeme radi naslepo).
- **P2:** RQ479 (lažna P1 akcija u produkciji; jeftino).
- **P3:** RQ588 (migration-discovery higijena), RQ586 (spušten).
- **P1 WAITING (performance owner):** PERF19 posle RQ573, samo ako Board i dalje probija postojeći p95 <=2 s budget.

Postojeći prioriteti P0/P1 u RQ569/RQ570–RQ577 nisu menjani. Njihova vrednost je potvrđena live.

## 15. Popravke postojećih promptova

1. **RQ586**: Priority P1 → **P3**, status ostaje READY. Dodat je addendum sa live dokazom F8: popravka je ispravna i jeftina, ali danas menja samo labelu `shiftTimeZone`.
2. **RQ479**:
   - dodata metadata koja je nedostajala (`Ready after`, owned/avoid paths);
   - opseg je suzen na repo-local zaštitu pri čitanju: fixture zapisi (prepoznati po `sourceKey` obrascu `:smoke:` ili po eksplicitnom fixture markeru) isključuju se iz operativne liste i brojeva, uz vidljiv brojač "isključeno kao test podaci";
   - brisanje produkcijskih redova ostaje odluka vlasnika;
   - status WAITING → **READY**, prioritet P1 → P2. Promocija je dozvoljena jer nema locka, grane ni PR-a, nijedan READY prompt ne drži `AnalyticsActionsEndpoints.cs`, a familija je posebna.
3. **Post-review korekcije postojećih promptova:**
   - RQ545: startup uzrok više nije predstavljen kao jedinstveno dokazan; config/non-strict/alternate-path/post-readiness-drift ostaju razdvojene hipoteze;
   - STAB16: process-local web worker status ≠ dokaz da zaseban worker servis ne postoji; durable run history je autoritet;
   - RQ573: Board 17,3 s zahteva PDC+Board before/after, ali PDC nije proglašen jedinim uzrokom;
   - RQ578: standalone health endpoint trenutno uopšte ne prima `fromDate/toDate`; prompt sada zahteva eksplicitan istorijski period contract i client parity;
   - RQ587: public safe startup state + admin-only effective config/stage; runtime/version ostaje deployment identity;
   - RQ588: EF discovery semantics + reviewed allowlist, bez neutemeljene performance tvrdnje;
   - RQ557: sužen na deskriptivne mature-markdown ishode sa fail-closed stock/cost evidence; ne zavisi od Actions ledger-a;
   - RQ558: čeka RQ557 + dokaz mature/control sample; bez runtime causal/calibration promene;
   - RQ585: nema dependency na Actions ledger; ostaju njegove stvarne signal/freshness zavisnosti;
   - RQ565/P-UI-49 retained deploy-window evidence ostaje klasifikovano kao verovatna korelacija, ne dokaz provider uzroka.
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

- **RQ587** (P1, READY), `startup-database-initialization-truth`: public `/ready` prikazuje safe startup-init outcome; tačni effective flags/stage su admin-only; `/api/runtime/version` ostaje deployment identity; startup `succeeded` nije schema certificate.
- **RQ588** (P3, READY), `ef-migration-discovery-guard`: EF-runtime discovery guard + explicit reviewed allowlist/supersession klasifikacija 4 orphan klase; bez automatskog dodavanja atributa/DDL-a.
- **PERF19** (P1, WAITING posle RQ573), `decision-board-composition-performance`: meri contributor timings posle PDC slimming-a i radi samo najmanji dokazani fix ako Board i dalje probija postojeći p95 budget.

Puni RQ tekstovi su u `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; PERF19 je u `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`.

## 17. Tabela promptova (§37)

| ID | Status | P | Porodica | Svrha | Ready after | Paralelno |
|---|---|---|---|---|---|---|
| RQ587 | READY | P1 | startup-database-initialization-truth | Public safe startup outcome + admin effective config/stage | — | da uz fresh Program/startup collision check |
| RQ588 | READY | P3 | ef-migration-discovery-guard | EF discovery guard + reviewed orphan classification | — | da |
| RQ479 | READY (bilo WAITING) | P2 (bilo P1) | analytics-actions-live-fixture-hygiene | Fixture zapisi van operativne liste, bez brisanja | — | da |
| RQ586 | READY | P3 (bilo P1) | daily-sales-timezone-config | Nepromenjen opseg | — | da |
| PERF19 | WAITING | P1 | decision-board-composition-performance | Izmeri/ograniči Board 17,3 s posle PDC slimming-a | RQ573 DONE | ne sa aktivnim DecisionBoard backend ownerom |

## 18. Promocija i kaskada (§38)

- **RQ READY posle ovog rada:** primarni **RQ569** (nepromenjeno). Dodatni: RQ553, RQ574, RQ578, RQ580, RQ581, RQ586, **RQ587**, **RQ588**, **RQ479**.
- **P-UI:** canonical current: primarni P-UI-39; dodatni collision-safe READY P-UI-40, P-UI-41, P-UI-47, P-UI-49. P-UI-45 je WAITING iza P-UI-40 + P-UI-48.
- **STAB16:** ostaje BLOCKED. Addendum navodi da je prvi korak vlasnika provera konfiguracije, a ne pristup bazi.
- **Kaskada:** RQ587 ne odblokira sam nijedan runtime prompt; on daje dokaz za STAB16/RQ545. RQ557 čeka samo RQ553/shared Pre/Post ownership, ne Actions ledger. RQ585 ostaje iza RQ570/RQ573/RQ574/RQ576 + RQ583 freshness. PERF19 čeka RQ573. RQ558 čeka RQ557 + explicit mature/control sample evidence.
- Ažurirani su zaglavlje RQ queue-a (linija 5), dodata je linija o registraciji i ažuriran je RQ red u `MASTER_ROADMAP.md`.

## 19. Talasi A–E (§39)

- **Talas A (owner/provider dokaz, bez promene config pre snimka):**
  1. Pokrenuti odobren Access import.
  2. U Render dashboardu prvo **zabeležiti** efektivne `Database__AutoMigrate`, `DatabaseInitialization__FailFast`, `StartupTasks__RunDatabaseInitialization` i postojanje/status `trendplus-worker`.
  3. Pročitati startup log i korelisati sa `/ready.startedAt/readyAt` i admin contract diagnostic. Tek nakon snimka stanja odlučiti da li config treba menjati; strict/fail-fast promena može namerno ostaviti servis `not ready`.
- **Talas B (READY, paralelno):** RQ569 (primarni), RQ587, RQ578, RQ574, RQ479, RQ580, RQ581, RQ553; RQ586 i RQ588 usput.
- **Talas C (posle RQ569):** RQ570, RQ571, RQ572, RQ573, RQ576, RQ579, RQ583, RQ584, RQ552 → RQ453. Posle RQ573: PERF19 samo ako Board i dalje probija budget.
- **Talas D (posle talasa A i STAB16):** RQ545 završna provera, RQ454, RQ565, RQ566, Pulse i hub ponovo živi.
- **Talas E (vrednost):** RQ559/RQ530 prema svojim source gate-ovima; RQ557 čim RQ553 oslobodi Pre/Post path (ne čeka Actions ledger); RQ585 čim završe njegovi signal/freshness prerequisites. RQ558/runtime causal work tek uz RQ557 + dokaz mature/control coverage; Actions-based learning tek uz realne action outcomes.

## 20. Test poslovne vrednosti (§40)

| Prompt | Koja odluka postaje bolja | Ko | Koliko često | Šta se desi ako se ne uradi |
|---|---|---|---|---|
| RQ587 | "Da li je produkcija stvarno primenila popravku?", tj. da li se tom ekranu sme verovati | Vlasnik / dežurni agent | Pri svakom deployu | Svaka popravka šeme (RQ545, RQ475, 42P16) se proverava naslepo. Tri dana su već izgubljena. |
| RQ479 | Koje akcije su prave | Vlasnik | Svaki put kad otvori Akcije | Lažna P1 akcija "Smoke Inventory Final" i lažni brojevi |
| RQ588 | Nijedna, posredno: sprečava tihu ne-primenu buduće migracije | Agenti | Pri svakoj migraciji | Mali rizik; P3 |
| RQ586 | Nijedna danas (wall-clock podaci) | — | — | Labela "UTC" u metapodacima |

## 21. Odluke vlasnika (§33)

Provider/owner korak nije odmah "uključi obe vrednosti". Prvo treba snimiti efektivno stanje i startup log:
- da li je `trendplus-api` sinhronizovan sa očekivanom AutoMigrate/FailFast/RunDatabaseInitialization politikom;
- da li `trendplus-worker` postoji i ima durable run history;
- da li je missing schema posledica startup skip/non-strict failure/alternate target ili post-ready drift.
Ako se zatim odluči da produkcija pređe na strict fail-fast, promenu treba planirati operativno jer sledeći restart može legitimno ostati `not ready` dok se šema ne popravi.

Brisanje smoke zapisa iz produkcije **nije** potrebno za RQ479. Može kasnije, uz izričitu odluku.

## 22. Provider i eksterni koraci (EXTERNAL_EVIDENCE_ONLY)

1. Access import (vlasnik, sa računara u radnji).
2. Render env: AutoMigrate, FailFast, RunDatabaseInitialization; da li postoji worker servis (STAB16).
3. Render startup log posle restarta (RQ545).
4. Read-only konekcija za sravnjenje (RQ454/RQ565).
5. Izvor master podataka: kategorija, boja, pol, razmera nabavne cene (RQ574/RQ575/RQ576).

## 23. Rizici

- Ako se posle evidencije promeni runtime politika na strict FailFast, servis može ostati `not ready` dok se šema ne popravi. To je moguća namerna posledica, ali config ne menjati pre hvatanja trenutnog efektivnog stanja/logova.
- RQ587 dira `Program.cs`, vruć fajl sa mnogo vlasnika. Opseg je ograničen na dva handlera.
- Codex radi paralelno, pa je pre isporuke urađen ponovni fetch (§27).

## 24. Šta je stvarno testirano

- 16 live GET provera (§4.1), 22:50–22:56 CEST.
- Code tracing za readiness lanac (§4.2), shift logiku (F8), Board kompoziciju (F6/#11) i orphan migracije (F2).
- `git merge-base --is-ancestor 26e09e46 02f99158` (popravka 42P16 jeste u runtime-u).
- GitHub API: 0 otvorenih PR-ova. `git ls-remote`: 28 grana, sve stare i DONE.
- Validatori `check-agent-instructions`, `check-prompt-queues` i `check-planning-architecture` (sa `--self-test` i bez njega) i `git diff --check` (§27).
- Originalni audit nije pokretao .NET/frontend testove jer je bio docs/queue-only. Ni ovaj post-review ne tvrdi novi runtime test rezultat; izvršni promptovi sada imaju pojačane acceptance/test zahteve. Retained live GET-ovi nisu ponovo reprodukovani u post-review okruženju.

## 25. Pitanja kvaliteta (§41)

- *Da li je svaka tvrdnja prvo napadnuta?* Da, §4 (F1–F10). F6 i F8 su oborene, a F2 i F4 su sužene.
- *Da li je mapa pokrivenosti napravljena pre novih promptova?* Da, §5. Iz nje su proizašla samo 2 nova prompta.
- *Da li neki novi prompt duplira aktivnog vlasnika?* RQ587/RQ588 ne. Post-review PERF19 je eksplicitno sequenced posle RQ573 i koristi postojeći PERF metod/budget; ne preuzima Product Decision semantiku.
- *Da li je sve P0/P1?* Ne. Novi su P1 i P3, a jedan postojeći je spušten.
- *Da li postoji live dokaz ili je jasno označeno UNPROVEN?* Da, §4.1 i §11.
- *Da li je "READY: none" pretpostavljen?* Ne, §2.
- *Da li se frontend traži da izmišlja istinu?* Ne. RQ479 je backend filter, a RQ587 je backend stanje.
- *Da li je odluka vlasnika neizbežna?* Provider config/worker stanje prvo zahteva dokaz, ne unapred izabranu config promenu (§21).
- *Šta je najbolji sledeći prompt?* Ukupno **RQ569**. Najveći dobitak po trošku je **talas A** (vlasnik, 30 min), a od koda **RQ587**.

## 26. Ograničenja

Nema provider dashboard/log/admin-key pristupa u retained auditu. Ekrani nisu ponovo renderovani u ovom post-review-u. §4.2 je constraint iz koda + retained live readiness/schema simptoma, **ne** direktno očitavanje Render konfiguracije. Zato su svi provider-cause zaključci zadržani kao hipoteze do STAB16/RQ587 dokaza.

## 27. Izlazi i isporuka

- Ovaj dokument.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`: RQ587/RQ588 hardening, RQ479 fixture-quarantine contract, RQ573/RQ578/RQ585 corrections.
- `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`: PERF19 measured Decision Board composition follow-up.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_NIVELACIJA_AUDIT_ADDENDUM.md` (RQ545), `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_OPERATIONS_ACCURACY_ADDENDUM.md` (RQ565), `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` (STAB16), `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` (P-UI-49), `docs/ai/ANALYTICS_PRODUCTION_VALUE_PROMPT_BACKLOG_2026-08-19.md` (napomena o zastarelosti).
- `MASTER_ROADMAP.md` (RQ red i datirana napomena).
- `.ai/runs/2026-10-04-analytics-next-wave-audit-evidence.md`.
- Box-local: `/workspace/nwlive/` (sirovi live odgovori), `/workspace/out/next-wave/` (bundle i patch).


## 28. Post-review korekcije (2026-10-04)

Fresh review je urađen nakon originalnog `8ae06a50` delivery-ja na novijem `main`. Originalni live GET rezultati su tretirani kao retained evidence i nisu mrežno ponavljani.

Ključne korekcije:
1. startup readiness ne dokazuje jedinstveno da su AutoMigrate/FailFast isključeni; dodate su non-strict/config-path/post-readiness-drift hipoteze i RQ587 je razdvojio public startup outcome od admin effective-config dijagnostike;
2. web `workersEnabled=false` ne dokazuje odsustvo zasebnog worker servisa; authoritative signal je durable refresh-run history, koju API trenutno nema za required jobs;
3. standalone Data Quality health endpoint ne prihvata `fromDate/toDate`; RQ578 sada dobija pravi historical-period contract;
4. Board 17,3 s nema per-section profil i PDC nije dokazan kao jedini uzrok; RQ573 meri before/after, a PERF19 je bounded measured follow-up;
5. RQ557 ne koristi Analytics Actions ledger; sužen je na deskriptivne mature-markdown outcomes i fail-closed stock/cost evidence;
6. RQ585 ne zavisi od realnih Actions outcomes; ostaju njegove signal/freshness zavisnosti;
7. RQ558 ostaje kasniji controlled-effect rad tek uz dokaz mature/control coverage;
8. Supplier/Shoe Type historical-as-of problem je već značajno rešen RQ411 sale-time attribution ugovorom; preostale slabo-popunjene master dimenzije ne opravdavaju SCD implementaciju sada;
9. P-UI-45 nije READY: canonical P-UI routing je P-UI-39 primary + P-UI-40/P-UI-41/P-UI-47/P-UI-49 READY;
10. "samo jul je pouzdan" je suženo na "jul je trenutno najbolje cross-screen certified fixed window", bez tvrdnje da su svi drugi istorijski periodi netačni.
