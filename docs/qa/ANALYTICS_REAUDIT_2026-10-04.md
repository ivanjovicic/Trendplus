# Analitika — ponovni audit svih ekrana (2026-10-04)

Datum: 2026-10-04 (provere uživo oko 20:04–20:12 po beogradskom vremenu)
Osnova koda: `origin/main` `7de14c18` (sveža main grana)
Produkcija: API `https://trendplus-api.onrender.com`, runtime `02f9915887f99115348bd241590da581dafee45d`, build 2026-10-04 18:56 (Beograd). To je main bez poslednja dva commita, pa nalazi važe za trenutni kod.
Način rada: samo čitanje (GET) na produkcijskom API-ju, čitanje koda sa `origin/main` i stanja queue-a. Nije bilo pristupa bazi, logovima provajdera ni admin ključu. UI na Vercelu odgovara (HTTP 200), ali ekrani nisu renderovani u browseru, pa su ocene ekrana izvedene iz API odgovora i koda koji ih prikazuje.
Dokazi: `.ai/runs/2026-10-04-analytics-reaudit-evidence.md`

## 1. Kratak zaključak

Napredak je veliki. Od 22.09. zatvoreno je oko 170 RQ promptova. Brojevi na glavnim operativnim ekranima sada se slažu: za isti period (07.07–05.08.2026) Dnevna prodaja, Dobavljači, Vrsta obuće, Boja, KPI snapshot i datirani Dashboard daju isti promet od 1.561.120 RSD i 307 komada. Marža je 46,48% uz 100% pokrivenost istorijskim troškom. Pre 10 dana to nije bilo tako.

Ali analitika još **nije pouzdana za odluke**, iz tri razloga koja nisu bagovi u formulama:

1. **Podaci su stari 60 dana.** Poslednja prodaja u bazi je od 05.08.2026, a poslednji import od 12.08.2026 u 12:30. Workeri za osvežavanje ne rade u produkciji, a skoro svi ekrani po defaultu otvaraju period „poslednjih 30 dana od danas“, pa su prazni ili pokazuju lažan pad od −100%.
2. **Produkcijska šema nije usklađena sa kodom.** Pre/Post nivelacije i dalje ne rade (nedostaje kolona u view-u). Izveštaj dobavljača, Supplier hub, scorecard i Decision Pulse nemaju materijalizovane podatke.
3. **Master podaci iz Access-a su nepotpuni.** Nijedan od 12.422 artikla nema kategoriju, boja i način plaćanja su 100% „Nepoznato“, a nabavna cena na artiklu izgleda kao da je u drugoj razmeri. Zbog toga Product Decision ima **0 primenljivih preporuka**, a Zalihe pokazuju vrednost od 93.389 RSD za 3.566 pari.

Ukratko: računanje je sada uglavnom tačno i dokazano testovima. Ono što je ostalo su svežina podataka, usklađenost produkcije i nekoliko pravila koja zbog stanja podataka blokiraju sve.

## 2. Napredak (brojevi)

- Ukupno jedinstvenih RQ promptova: 560 (RQ1–RQ569, uz duplikate u addendumima).
- DONE: 486, WAITING: 61, PARTIAL: 6, READY: 2 (RQ569 primarni, RQ553), OBSOLETE: 5, IN_PROGRESS: 0.
- Zatvoreno od 22.09.2026: oko 170 promptova sa datiranim completion zapisom (RQ371–RQ568 i drugi).
- U opsegu RQ440–RQ569 (registrovano 25.09–04.10): oko 105 od 130 je DONE.
- Od 61 WAITING promptova, 26 su stari Advanced/Legacy (RQ13–RQ38) koji stoje od početka septembra.
- Zastoji: STAB16 (produkcijski workeri i dokaz deploya) je BLOCKED zbog pristupa provajderu. RQ545 je PARTIAL i čeka produkcijsku proveru. RQ454 i RQ565 (sravnjenje sa produkcijom) su WAITING, a RQ453 (CI koji se ne može preskočiti) je WAITING.

## 3. Ocena po oblastima

| Oblast | Ocena | Zašto |
|---|---|---|
| Dobavljači (pregled) | Dobro | Promet, količina i marža se slažu sa Dnevnom prodajom i Vrstom obuće, a pokrivenost troškom je 100%. Problem je samo default period. |
| Dobavljači: hub, scorecard, izveštaj | Loše | `MISSING_OBJECT` (90-dnevni MV). Hub za jul vraća 0 dobavljača, iako pregled ima 15. Izveštaj za jun nosi naslov „Poslednjih 30 dana“. |
| Vrsta obuće | Dobro / srednje | Brojevi se slažu. Za period posle 05.08 prikazuje −100% PoP (lažni pad). |
| Boja | Loše (nema vrednosti) | 100% prometa je „Nepoznato“, jer u izvoru nema podataka o boji. |
| Dnevna prodaja | Dobro / srednje | Brojevi se slažu. Smene se ne mogu meriti (nema satnice). Sravnjenje računa je „verified“ iako je 0 računa upareno. `toDate` ima drugačiji ugovor od ostalih ekrana. |
| Product Decision | Loše | 0 primenljivih preporuka. Na vrhu je 500 redova „Proveri podatke“ sa prometom 0, pa se 132 prodata artikla ne vide. Odgovor ima 13 MB i traje 9–10 s. |
| Nivelacije Pre/Post | Ne radi | `contract_missing` u produkciji (RQ545 PARTIAL). |
| Prioriteti pre nivelacije | Srednje | Radi, ali brojeve računa od danas, a ne od 05.08. Na vrhu su pertle iz objekta „STARO“ iz 2017. |
| Zalihe | Loše | Vrednost zaliha je oko 26 RSD po paru. Sva starost upada u „31–60 dana“ zbog datuma importa. Low-stock je 0 na jednom ekranu, a 213 na drugom. |
| Dashboard / Pilot readiness | Loše | Bez datuma meša april 2026, sve vreme i traženi period pod istim naslovom. Executive „top dobavljači“ imaju promet 0. |
| Kvalitet podataka / Pilot intake | Srednje | Pilot intake je popravljen: default period 07.07–05.08, readiness 67. Kvalitet podataka i dalje kaže „100 / odlično“ i „nema otvorenih problema“. |
| Akcije / Decision Board / Pulse | Srednje / loše | Smoke akcije su još u listi (RQ479). Pulse je prazan (124 potisnuto). Board je pretežno blokeri. |
| Insight Studio / Advanced | Loše | Snapshot-i su od januara i marta, sa „Unknown product“. Pojavljuje se mojibake „NeodreÄ‘eno“, a postoji 26 starih WAITING promptova. |
| Pipeline / svežina / workeri | Loše | Import je star 53 dana, workeri nisu registrovani, refresh status je „unknown“. |
| Produkcijska šema / MV | Loše | Pre/Post view je zastareo, supplier MV i inventory snapshot relacije nedostaju. |
| Testovi / CI / governance | Dobro | Postoje oracle i golden testovi za Supplier, Vrstu obuće, Nivelacije i šest Operacije ekrana (RQ445–447, 524–528, 547–550, 561–568). Nedostaju obavezni CI (RQ453) i sravnjenje sa produkcijom (RQ454, RQ565). |

## 4. Inventar ekrana (rute → endpointi → stanje uživo)

| Ruta | Glavni endpoint | Stanje 04.10 |
|---|---|---|
| `/analytics` | `cached/dashboard/bootstrap` | 200. Bez datuma traje 18,5 s (hladno) i meša periode. Sa datumima je ispravan za summary, ali executive daje 0. |
| `/analytics/pilot-readiness` | bootstrap (bez datuma) + refresh/DQ | Prikazuje 836.350 RSD / 15 / 145 iz aprila. |
| `/analytics/products` | `cached/products/decision-center` | 200, 13,3 MB, 9–10 s, 0 primenljivih preporuka. |
| `/analytics/supplier` | `supplier-sales-stats` (+ hub) | Pregled je OK za jul. Default period je prazan. Hub je prazan. |
| `/analytics/supplier/report` | `reports/supplier-decision` | 200 sa `MISSING_OBJECT` i pogrešnim naslovom perioda. |
| `/analytics/shoe-type-sales-stats` | `shoe-type-sales-stats` | OK za jul. −100% PoP posle horizonta. |
| `/analytics/color-sales-stats` | `color-sales-stats` | Jedan red „Nepoznato“ sa 100%. |
| `/analytics/daily-sales` | `daily-sales` | OK. Smene nisu merljive. |
| `/analytics/nivelacije-pre-post` | `vendor-sales-nivelacija` | Rezervni režim (`contract_missing`). |
| `/analytics/pre-nivelacija-prioriteti` | `pre-nivelacija-prioriteti` | 200, 537 kandidata, period se računa od danas. |
| `/analytics/inventory` | `inventory/*`, `cached/inventory/*` | Vrednost i starost su pogrešne. Forecast, size-curve, rebalance i alerts nemaju relaciju. |
| `/analytics/data-quality` | `data-quality/health`, `list`, `trend` | „100 odlično“, 0 problema, trend prazan. |
| `/analytics/reports/pilot-intake` | `reports/pilot-intake` | OK: 07.07–05.08, readiness 67, 1.078 bez troška, 12.422 bez kategorije. |
| `/analytics/actions` | `actions`, `counts`, `outcomes/summary` | Smoke zapisi su još u listi. |
| `/analytics/decision-board` | `decision-board` | 11,4 s, ukupno critical, period 180 dana od danas. |
| `/analytics/decision-pulse` | `decision-pulse` | 11,6 s, 0 stavki, `PULSE_PARTIAL`. |
| `/analytics/insight-studio` | `advanced/*`, `advanced/v2/*`, `intelligence/*` | 200, ali stari snapshot-i, mojibake, metrike vezane za danas. |
| Preusmerenja (`supplier-sales-stats`, `dobavljaci-tipovi-obuce`, `supplier-decision-hub`) | — | Zadržana kao legacy rute (RQ507). |

## 5. Najvažniji nalazi (sa dokazima uživo)

1. **Podaci su stali 05.08.2026, a ekrani to ne kažu (P0, operativno).** `validation/freshness` vraća `lastImport=2026-08-12T10:30Z` i `freshnessHours=1279,6`. Supplier, Vrsta obuće i Boja vraćaju `dataWindowTo=2026-08-05`. `refresh-status` daje „unknown“ za svih 6 poslova („Worker nije registrovan u web procesu“). Vlasnik → RQ569 (READY), STAB16 (BLOCKED), novi RQ583 (SLA i baner).
2. **Pre/Post nivelacije i dalje ne rade (P0).** Odgovor sadrži „nedostaje kolona change_percent_revenue_semantic u relaciji public.vw_vendor_sales_nivelacija“, a integrity porodica „nivelacija“ je `unverified`. Vlasnik → RQ545 (addendum), STAB16.
3. **Izveštaj dobavljača, hub i scorecard nemaju podatke (P0).** `reports/supplier-decision` za jun vraća `MISSING_OBJECT` za 90-dnevni skup. `decision-hub/summary` za jul daje `supplierCount=0`, iako pregled ima 15 dobavljača sa 1.561.120 RSD. Uzrok je produkcijski MV koji nije kreiran ili osvežen (workeri). Vlasnik → STAB16, RQ545 addendum.
4. **Product Decision ne daje nijednu preporuku (P1).** Za jul je `actionableCount=0`, a svih 500 vraćenih redova su „Proveri podatke“ sa prometom 0. Kod `CachedAnalyticsEndpoints.cs:6623-6628` stavlja FIX_DATA (prioritet 7) ispred svega, pa 132 artikla sa prodajom ostaju van top-500. Svaki red nosi i `data_quality_blocker`, jer nijedan artikal nema kategoriju (`ProductDecisionReasoningHelper.cs:181-182`). → RQ573, RQ574.
5. **Dashboard i Pilot readiness mešaju periode (P1).** Bez datuma meta kaže 05.09–04.10, ali summary pokazuje 836.350 RSD / 15 / 145 iz 05–21.04, a plaćanja, dani u nedelji i sati su za sve vreme (5.550 „transakcija“, 240,6 mil. RSD). Executive „top dobavljači“ imaju promet 0 čak i za jul. Isti brojevi su prijavljeni još 19.08. → RQ572.
6. **Zalihe imaju pogrešnu vrednost i starost (P1).** Vrednost je 93.389 RSD za 3.566 pari. Nabavna cena na artiklu 21644 je 245 RSD, a na prodajnim stavkama isti dobavljač ima oko 3.038 RSD po paru. Svih 12.422 artikla su u „31–60 dana“, jer je import 12.08 upisao „Ulaz robe“ sa iznosom 0. → RQ576.
7. **Default periodi i PoP preko horizonta daju lažne slike (P1).** Vrsta obuće za 04.09–04.10 pokazuje −100% (`Ž.Cipela` 6.980 → 0), iako za taj period nema podataka. Product Decision, Board i Pulse po defaultu gledaju period od danas. → RQ570 (traži odluku).
8. **Prioriteti pre nivelacije računaju od danas (P1).** Prozor je 07.04–04.10, pa je 60 dana bez podataka uračunato kao „bez prodaje“. Alarm „nema prodaju 157 dana“ je od datuma podataka zapravo oko 97 dana. Na vrhu su pertle (tip „Oprema“) iz objekta STARO, a red sa `recommendationAllowed=false` je u redu „highlightNow“. → RQ571, RQ556 addendum.
9. **Kvalitet podataka tvrdi da je sve odlično (P2).** `data-quality/health` vraća `score=100 excellent`, a `list` daje 0 problema. U isto vreme Pilot intake ima readiness 67, svežina je critical, a 1.078 artikala nema trošak. Period je prijavljen do 23:59:59 na dan 04.10, iako podaci postoje samo do 05.08. → RQ578 (READY).
10. **„Verified“ na praznom skupu (P2).** Integrity za supplier_shoe_type kaže „reconciled“ za 0 = 0. Sravnjenje računa u Dnevnoj prodaji je „verified“ uz 0 uparenih i 25 neuparenih računa. → RQ579.

Ostali nalazi: Boja, kategorija, pol, plaćanje i sat su 100% nepoznati (RQ575). „Transakcija“ je verovatno dnevni dokument, a ne račun kupca: 25 dokumenata za 307 komada u julu i prosečna korpa 62.445 RSD (RQ577). Mojibake je u 7 Insight Studio endpointa (RQ581, READY). Naslov „Poslednjih 30 dana“ stoji na izveštaju za jun (RQ580, READY). `toDate` u Dnevnoj prodaji je inkluzivan, za razliku od ostalih ekrana (RQ584). Smoke akcije su još u listi (RQ479 addendum). Engleska oznaka „Insufficient data“ se pojavljuje na prioritetima (RQ553 addendum).

## 6. Šta fali do „pouzdane i vredne“ analitike (gap analiza)

1. **Svežina i pipeline.** Treba redovan Access import i dedicated worker proces (`PROCESS_TYPE=worker`, STAB16), uz SLA za svežinu i alarm (RQ583). Bez ovoga nijedna preporuka nije aktuelna.
2. **Usklađenost produkcijske šeme.** Treba jednokratna provera i popravka produkcije (`vw_vendor_sales_nivelacija`, `mv_supplier_decision_score_cache_90d/180d`, inventory snapshot relacije) preko postojećeg idempotentnog lifecycle-a i admin dijagnostike. Za to treba admin ključ ili pristup provajderu, a to je odluka vlasnika (STAB16/RQ545).
3. **Ugovori o metrikama i parity testovi.** Ovo je uglavnom urađeno za Operacije. Nedostaje isto za Dashboard bootstrap, Product Decision, Zalihe i Insight Studio (RQ572, RQ573, RQ576, RQ582).
4. **Sravnjenje između ekrana.** Operacije se slažu. Dashboard bez datuma, executive panel, hub i low-stock se ne slažu (RQ572, RQ576, STAB16).
5. **Provenance i metodologija.** Postoje (RQ509, RQ510), ali bez horizonta podataka (RQ569, RQ570) i uz „verified“ na praznom skupu (RQ579).
6. **Observability.** Nema alarma za stari import ni za zastareli view. Integrity probe postoji, ali ne vidi emptiness ni staleness (RQ579, RQ583). Sravnjenje sa produkcijom (RQ454, RQ565) čeka pristup.
7. **Master podaci.** Kategorija, boja, pol i način plaćanja se ne prenose iz Access-a, a razmera nabavne cene na artiklu je sumnjiva. To se mora ili popraviti na izvoru (import), ili pošteno prikazati kao „nije popunjeno u izvoru“ (RQ574, RQ575, RQ576).

## 7. Ideje za veću vrednost (odluke koje bi vlasnik donosio bolje)

- **Nedeljni pregled „šta da uradim“** (RQ585): najviše 10 akcija sa dokazom, iz proverenih signala.
- **Dopuna najprodavanijih modela po veličinama:** Product Decision na nivou veličine plus size-run (RQ559) daju listu za porudžbinu.
- **Premeštanje robe između Trend PLUS 1 i Trend PLUS 2:** postoje podaci po objektu, pa bi se mogao napraviti predlog transfera sporih artikala u objekat gde se prodaju.
- **Knjiga ishoda nivelacija** (RQ557): da li je sniženje radilo i šta ponoviti.
- **Panel vrednosti nabavke po dobavljaču** (RQ530, PARTIAL) za pregovore.
- **Čišćenje legacy zaliha:** pertle i stara roba (sezona 2017) iz objekta STARO. Odvojeni red za otpis ili akciju, umesto da zagušuje prioritete.

## 8. Novi promptovi (registrovani 2026-10-04)

| RQ | Prioritet | Status | Ukratko |
|---|---|---|---|
| RQ570 | P1 | WAITING (odluka + RQ569) | Default period vezan za horizont podataka. PoP preko horizonta postaje „nedostupno“, a ne −100%. |
| RQ571 | P1 | WAITING (RQ569, redom sa RQ552/RQ556) | Prioriteti pre nivelacije računaju od horizonta podataka, a ne od danas. |
| RQ572 | P1 | WAITING (RQ569; executive posle RQ573) | Dashboard bootstrap koristi jedan period za sve sekcije. Pilot readiness je ispravan, a executive dobavljači dolaze iz stvarnog prometa. |
| RQ573 | P1 | WAITING (RQ569, preklapanje fajla) | Product Decision: FIX_DATA ne sakriva prodate artikle, a payload je 13 MB → < 2 MB. |
| RQ574 | P1 | WAITING (odluka) | Nedostajuća kategorija ne sme da blokira 100% preporuka. |
| RQ575 | P2 | WAITING (RQ569 + odluka iz RQ574) | Umesto stubića sa 100% „Nepoznato“ prikazati „nije popunjeno u izvoru“. |
| RQ576 | P1 | WAITING (odluka + RQ569) | Tačna vrednost i starost zaliha. |
| RQ577 | P1 | WAITING (odluka) | Odrediti šta je zaglavlje prodaje, pre nego što se prikazuju metrike korpe. |
| RQ578 | P2 | **READY** | Kvalitet podataka više ne prikazuje „100 / odlično“ i „0 problema“ nad starim i nepotpunim podacima. |
| RQ579 | P2 | WAITING (RQ569) | „Verified“ samo kada postoji neprazan i uparen skup. |
| RQ580 | P2 | **READY** | Izveštaj i hub ne zovu eksplicitan period „Poslednjih N dana“. |
| RQ581 | P2 | **READY** | Popravka mojibake „NeodreÄ‘eno“ u Insight Studio. |
| RQ582 | P2 | WAITING (odluka) | Sakriti ili konsolidovati Insight Studio i legacy Advanced (RQ13–RQ38). |
| RQ583 | P2 | WAITING (odluka + RQ569) | SLA za svežinu, baner na svim ekranima i alarm. |
| RQ584 | P3 | WAITING (RQ569) | Dnevna prodaja koristi isti `toDate` ugovor kao ostali ekrani. |
| RQ585 | P3 | WAITING (posle RQ570/573/574/576 i SLA) | Nedeljni pregled odluka iz proverenih signala. |

Addendumi (bez novih duplikata): RQ545 (Pre/Post i supplier MV i dalje nedostaju na deployu od 04.10), RQ556 (pertle, STARO, highlightNow sa nedozvoljenim redom), RQ553 (engleska oznaka „Insufficient data“), RQ479 (smoke akcije uživo), RQ569 (samo dokazi uživo, bez promene scope-a).

## 9. Odluke vlasnika (sa preporukom)

1. **RQ570:** da li default period na svim ekranima odlučivanja postaje „poslednjih 30 dana do poslednjeg datuma prodaje“, kao u Pilot intake-u, uz baner „Podaci zaključno sa …“? Preporuka: **da**.
2. **RQ574:** da li je TipObuce glavna dimenzija asortimana, tako da nedostajuća kategorija bude samo upozorenje? Preporuka: **da**. Kategorija blokira samo kada nedostaju i tip i kategorija.
3. **RQ576:** kojom cenom vrednovati zalihe? Preporuka: poslednja nabavna cena iz dokumenata, a kao rezerva istorijski trošak sa prodajne stavke, označen kao procena. `Artikli.NabavnaCena` ne koristiti dok se ne proveri razmera. Import „Ulaz robe“ ne računati u starost.
4. **RQ577:** da li je `ProdajaZaglavlje` račun kupca ili dnevni dokument? Preporuka: ako je dnevni dokument, preimenovati ga i sakriti metrike korpe.
5. **RQ582:** da li sakriti Insight Studio dok se ne sertifikuje? Preporuka: **da**, pod oznakom „Eksperimentalno“, a RQ13–RQ38 konsolidovati.
6. **RQ583:** koji SLA za svežinu važi? Preporuka: upozorenje posle 2 dana, kritično posle 7 dana od poslednjeg uspešnog importa, uz baner i dnevni alarm (alarm samo uz izričito uključivanje).
7. **RQ556 addendum:** da li su STARO, Magacin, Komision i „Objekat 20828“ objekti za odlučivanje, i da li „Oprema“ ide u prioritete za nivelaciju? Preporuka: isključiti neaktivne objekte, a Opremu staviti u poseban red.
8. **STAB16 / RQ545 (operativno):** treba pristup provajderu ili admin ključ za jednokratnu dijagnostiku i popravku produkcijske šeme, kao i pokretanje worker procesa.

## 10. Preporučeni redosled rada

1. **Odmah, operativno (vlasnik):** pokrenuti Access import, kako bi podaci prestali da budu stari 60 dana. Dati pristup za STAB16 (worker proces, MV refresh) i za RQ545 dijagnostiku.
2. **RQ569** (primarni READY), paralelno sa **RQ578**, **RQ580** i **RQ581** (READY, nezavisni) i sa RQ553.
3. Posle RQ569: **RQ573** (Product Decision redosled i payload), zatim **RQ572** (bootstrap), **RQ579**, **RQ571** (redom sa RQ552 i RQ556) i **RQ584**.
4. Kada vlasnik odgovori: **RQ574**, **RQ570**, **RQ576**, **RQ577**, **RQ575**, **RQ583**, **RQ582**.
5. Na kraju **RQ585** (nedeljni pregled) i vrednosni promptovi RQ557, RQ559 i RQ530.

## 11. Ograničenja ovog audita

- Ekrani nisu renderovani u browseru. Vizuelni nalazi su izvedeni iz koda i API odgovora.
- Nema pristupa bazi ni logovima provajdera, pa uzrok za MISSING_OBJECT i contract_missing nije potvrđen na samoj bazi.
- Brojevi na nivou celog queue-a su mašinski izvučeni iz `Status:` linija. Kod promptova sa duplikatima u addendumima brojan je DONE ako postoji.
