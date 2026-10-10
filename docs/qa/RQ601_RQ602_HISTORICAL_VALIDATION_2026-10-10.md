# RQ601/RQ602 — istorijska poslovna validacija posle Access importa #23

Datum: 2026-10-10
Status: **PARTIAL — maksimalna read-only validacija je završena; nedostaju originalne kalkulacije i poslovna/prospektivna potvrda efekta nivelacija**
Baza: lokalni Docker PostgreSQL `trendplus-postgres`, baza `trendplus`
Import: `DataImportBatches.Id = 23`, status `completed`
Opseg: bez novog importa, bez izmena poslovnih podataka, bez brisanja ili repair migracije.

## Poslovni zaključak

Access prodajna populacija iz batch-a #23 je potpuno prisutna u analitičkim faktima: 5.530/5.530 računa i 67.092/67.092 stavki imaju odgovarajuće fakt redove, a uparenim stavkama nema odstupanja količine, cene ili ukupnog iznosa. Retail promet iz operativnog SQL oracle-a i tri proverena API ekrana slažu se za `imported`, `existing` i `all`.

RQ601 ostaje **PARTIAL**. Na 66.233 Access retail prodajnih stavki poznat je trošak za 65.732 stavke (99,24% po broju; 99,27% po prometu), dok 501 stavka ostaje sa `NULL` troškom i 1.711.050 RSD prometa. Legacy `NabavnaCena` ima snažan signal pogrešne razmere, ali bez originalne kalkulacije nije dokazano da je uzrok valuta, jedinica mere ili pogrešno polje. Nije uveden fallback u nulu niti promenjena precedenca.

RQ602 dobija istorijski read-only dry-run nad batch-om #23, ali ostaje **PARTIAL**. Izabrano je 20 tretiranih i 20 kontrolnih SKU×prodavnica determinističkim frozen manifestom. Ranija tvrdnja da su prozori potpuni je povučena: bez dnevnog source-coverage dokaza prozori su `unverified`, a trošak bez dokazanog porekla nije sertifikovan. Rezultat je deskriptivan; nije poslovno dokazan uzročni efekat i ne predstavlja pilot.

## Batch #23 i Access faktovi

| Provera | Rezultat |
|---|---:|
| Batch status / progress | `completed` / 100 |
| Izvorni Access računi | 5.530 |
| SalesFacts za Access račune | 5.530 |
| Izvorne Access stavke | 67.092 |
| SalesLineFacts za Access stavke | 67.092 |
| Uparene stavke sa razlikom u količini/ceni/iznosu | 0 |
| Batch rows read / accepted / written | 206.092 / 205.436 / 243.253 |
| Batch inserted / updated / rejected | 237.886 / 5.367 / 0 |
| Izvorni period | 2011-01-03 — 2026-08-05 |

Sirovi faktovi po poreklu:

| Scope | Računi | Potpisane jedinice | Promet RSD |
|---|---:|---:|---:|
| imported | 5.530 | 67.664 | 240.603.523,64 |
| existing | 110 | 484 | 2.697.200,00 |
| all | 5.640 | 68.148 | 243.300.723,64 |

Nezavisni retail oracle radi nad `prodaja_zaglavlje` + `prodaja_stavke`, izbacuje `DUG` i `KOREKCIJA`, i zadržava potpis povrata:

| Scope | Računi | Potpisane jedinice | Retail promet RSD |
|---|---:|---:|---:|
| imported | 5.226 | 66.520 | 234.829.573,64 |
| existing | 5 | 8 | 750,00 |
| all | 5.231 | 66.528 | 234.830.323,64 |

109 `SalesFacts` redova koji nisu operativni račun su `existing`/DEMO populacija (2.676.700 RSD), van Access batch-a #23. Ne tumače se kao dokaz da je Access promet uvećan za taj iznos.

## RQ601 — nabavne cene i marža

Pokrenuta je postojeća read-only skripta `tools/purchase-cost-scale-reconciliation.sql` nad stvarnom bazom. Rezultat ima 12.428 artikala. Izvor troška je zadržan odvojeno:

- `source_sale_line`: 7.277 artikala sa poslednjim poznatim troškom prodajne stavke;
- `master_nabavnacena_backfill`: 3 artikla;
- `unknown`: 141 artikl koji imaju prodajni signal bez poznatog troška;
- poslednji pozitivan inbound trošak postoji za 6.131/12.428 artikala (49,33%);
- poslednji efektivni sale-line trošak postoji za 7.280/12.428 artikala (58,58%).

Za Access retail stavke, bez pretvaranja `NULL` u nulu:

| Poreklo efektivnog troška | Stavke | Potpisane jedinice | Promet RSD | Poznata marža RSD |
|---|---:|---:|---:|---:|
| source sale-line | 65.728 | 66.001 | 233.112.543,64 | 95.829.690,14 |
| master legacy fallback | 4 | 4 | 5.980,00 | 5.824,00 |
| unknown (`NULL`) | 501 | 515 | 1.711.050,00 | `NULL` |
| ukupno | 66.233 | 66.520 | 234.829.573,64 | samo poznata populacija |

Najveća koncentracija nepoznatog troška je:

| Dimenzija | Populacija | Nepoznat trošak | Nepoznat promet | Pokrivenost po stavkama |
|---|---:|---:|---:|---:|
| prodavnica 551466791 / Komision | 327 | 327 | 1.571.450,00 | 0,00% |
| prodavnica 2082886995 / Trend PLUS 1 | 51.694 | 122 | 99.910,00 | 99,76% |
| Cizma | 7.900 | 264 | 1.263.200,00 | 96,66% |
| Sandala | 12.576 | 58 | 246.210,00 | 99,54% |
| Antilop | 1.920 | 327 | 1.571.450,00 | 82,97% |
| Leon | 1.727 | 23 | 45.640,00 | 98,67% |
| 2017-08 | 709 | 327 | 1.571.450,00 | 53,88% |

Ostali Access store-ovi imaju 99,21%–100,00% pokrivenosti po broju stavki. Posle 2017-08 u mesečnom pregledu nema dodatne velike nepoznate populacije; raniji meseci imaju male pojedinačne rupe.

Razmera izvora:

- 7.256 od 7.276 artikala sa parom `master legacy / sale-line` je ispod praga 25%; medijana odnosa je 0,8621%.
- `NabavnaCenaDin / sale-line` ima medijanu 1,00 i nema par ispod 25%.
- Access `Artikli`: 3.566 komada na stanju; legacy vrednost 93.389,00 RSD, `NabavnaCenaDin` vrednost 9.572.063,00 RSD. Ovo je raspon scenarija, ne sertifikovana kapitalna vrednost.
- `NULL` trošak nije uključen u profit, maržu ili kapitalnu vrednost.

### Tačan uzorak od 20 originalnih kalkulacionih stavki

Ovo su tačno redovi za koje vlasnik treba da dostavi originalnu kalkulaciju. Kolone kalkulacije, dokaza i greške namerno ostaju prazne; nijedna vrednost nije izmišljena iz Trendplus-a.

| # | Artikal | PLU / naziv | Dobavljač | Tip | Prodavnica | Stanje | Legacy NC | NC Din | Poslednji inbound | Sale-line | Kalkulacija RSD | Dokaz / greška |
|---:|---:|---|---|---|---|---:|---:|---:|---:|---:|---|---|
| 1 | 17929 | 1 / 10654 | Antilop | M. Poluduboka cipela | Komision | 23 | 57 | 7.040 | — | 7.040 | — | — |
| 2 | 21434 | 3716 / M1953-60 | Rieker | Ž.Cipela | Trend PLUS 1 | 12 | 39 | 4.600 | 10.490 | 4.600 | — | — |
| 3 | 21491 | 3763 / 26WN0128 | KGFASHION | Ž.Cipela | Trend PLUS 1 | 12 | 34 | 3.992 | 8.990 | 3.992 | — | — |
| 4 | 21626 | 3549 / 70129-1 | UnA plus | Sandala | Trend PLUS 1 | 24 | 36 | 4.226 | — | 4.226 | — | — |
| 5 | 21460 | 3739 / 106820/36 | Planika | Patika | Trend PLUS 1 | 10 | 35 | 4.097 | 8.990 | 4.097 | — | — |
| 6 | 21496 | 3768 / 107261 | Planika | Patika | Trend PLUS 1 | 10 | 36 | 4.321 | 8.990 | 4.321 | — | — |
| 7 | 21642 | 1944 / 70100-02 | UnA plus | Papuca | Trend PLUS 1 | 38 | 19 | 2.253 | — | 2.253 | — | — |
| 8 | 21481 | 3752 / 7021 | Florida | Sandala | Trend PLUS 1 | 11 | 32 | 3.760 | 7.490 | 3.760 | — | — |
| 9 | 21650 | 3860 / 70135-1 | UnA plus | Papuca | Trend PLUS 1 | 31 | 22 | 2.640 | — | 2.640 | — | — |
| 10 | 21477 | 3749 / 1458 | Florida | Ž.Cipela | Trend PLUS 1 | 21 | 32 | 3.760 | — | 3.760 | — | — |
| 11 | 21480 | 3751 / 7003 | Florida | Sandala | Trend PLUS 1 | 20 | 32 | 3.760 | — | 3.760 | — | — |
| 12 | 21479 | 3750 / 7605 | Florida | Sandala | Trend PLUS 1 | 20 | 32 | 3.760 | — | 3.760 | — | — |
| 13 | 21473 | 3745 / 8899 | Florida | Ž.Cipela | Trend PLUS 1 | 20 | 32 | 3.760 | — | 3.760 | — | — |
| 14 | 21478 | 3753 / 521376 | Florida | Sandala | Trend PLUS 1 | 10 | 32 | 3.760 | 7.490 | 3.760 | — | — |
| 15 | 21602 | 3466 / 6010 | Leon | Papuca | Trend PLUS 1 | 13 | 21 | 2.480 | 5.200 | 2.480 | — | — |
| 16 | 21627 | 3547 / 70117-4 | UnA plus | Sandala | Trend PLUS 1 | 16 | 33 | 3.872 | — | 3.872 | — | — |
| 17 | 21621 | 3842 / 26WG2804 | KGFASHION | Sandala | Trend PLUS 1 | 12 | 41 | 4.990 | — | 4.990 | — | — |
| 18 | 21465 | 3742 / 2521 | Moda Pele | Ž.Cipela | Trend PLUS 1 | 13 | 39 | 4.600 | — | 4.600 | — | — |
| 19 | 21208 | 3516 / 28207/35 | Planika | Sandala | Trend PLUS 1 | 8 | 28 | 3.300 | 6.990 | 3.300 | — | — |
| 20 | 21644 | 3854 / 70126.1 | UnA plus | Papuca | Trend PLUS 1 | 19 | 245 | 2.875 | — | 2.875 | — | — |

Napomena: raniji worksheet je za artikal 21644 prikazivao PLU `70126.1`; stvarni batch-23 cilj ima `PLU=3854`, a `Naziv=70126.1`. To je ispravljeno u ovom evidence-u kao identitetska korekcija, ne kao poslovna kalkulacija.

## RQ602 — istorijski dry-run nivelacija

Read-only upit `tools/markdown-pilot-measurement.sql` je korišćen uz postojeću polu-otvorenu semantiku prozora `[action_date - 28, action_date + 28)`, retail filter bez `DUG/KOREKCIJA`, potpisane količine i eksplicitne `NULL` troškove.

| Signal | Rezultat |
|---|---:|
| `Nivelacija` / `Nivelacija cena` događaji | 10.977 |
| Kandidati za sniženje, jedinstveno SKU×store×dan | 6.052 |
| Kandidati sa min/max horizon signalom (nije dokaz potpunosti) | 5.818 |
| Odabrani tretirani kandidati | 20 |
| Odabrane kontrole | 20 |
| Uparene kontrole | 20/20 |
| Tretirani događaj potvrđen | 20/20 |
| `NULL` trošak u ranijem dry-run izboru (ne dokazuje provenance) | 0 linija |

Odabrana proba, deskriptivno:

| Populacija | Pre parova | Posle parova | Pre promet RSD | Posle promet RSD | Pre poznata marža | Posle poznata marža |
|---|---:|---:|---:|---:|---:|---:|
| 20 tretiranih | 24 | 82 | 177.510 | 465.870 | 85.671 | 163.390 |
| 20 kontrola | 23 | 37 | 159.390 | 265.870 | 76.480 | 129.388 |

Opisna promena prihoda je +162,44% za tretirane i +66,80% za kontrole. Jednostavna razlika promena je +181.880 RSD, ali to **nije uzročna procena**: istorijski izbor nije randomizovan, prozori su retrospektivni, kontrolni skup je mali, a stanje zalihe je trenutni snapshot (169 tretirano / 119 kontrola), ne istorijska zaliha na kraju prozora. Marže su izračunate samo na poznatom troškovnom osnovu i trenutno su pod RQ601 ograničenjem.

Za batch-23 je u periodu 2026-07-01 — 2026-08-05 pronađeno 132 deduplikovana event-a u istom canonical event upitu. API pre/post odgovor je `dataQualityStatus=critical`, `recommendationAllowed=false` i `missing_comparable_rows` za traženi prozor, jer koristi kraj traženog intervala kao granicu zrelosti 30-dnevnog post-prozora; za događaje 2026-08-01 taj prozor nije završen na 2026-08-05. Integrity evidence dodatno prijavljuje drift za 7 bounded event-a. To je razlog da se odgovor ne koristi kao poslovna preporuka; ne predstavlja dokaz da je istorijski SQL dry-run netačan.

## Sravnjenje analitičkih ekrana

Period za ekrane je `[2026-07-01, 2026-08-05)`, uz UTC normalizaciju i bez računanja 2026-08-05.

| Ekran | Scope | Nezavisni SQL | API | Verdict |
|---|---|---:|---:|---|
| Dnevna prodaja | imported | 1.833.020 RSD / 359 | 1.833.020 RSD / 359 | PASS za retail promet |
| Dnevna prodaja | existing | 480 RSD / 5 | 480 RSD / 5 | PASS |
| Dnevna prodaja | all | 1.833.500 RSD / 364 | 1.833.500 RSD / 364 | PASS |
| Prodaja po dobavljačima | imported | 1.833.020 / 359 | 1.833.020 / 359 | PASS za total; pre/post polja nisu isti dry-run ugovor |
| Prodaja po dobavljačima | existing | 480 / 5 | 480 / 5 | PASS za total |
| Prodaja po dobavljačima | all | 1.833.500 / 364 | 1.833.500 / 364 | PASS za total |
| Prodaja po vrsti obuće | imported | 1.833.020 / 359 | 1.833.020 / 359 | PASS za total |
| Prodaja po vrsti obuće | existing | 480 / 5 | 480 / 5 | PASS za total |
| Prodaja po vrsti obuće | all | 1.833.500 / 364 | 1.833.500 / 364 | PASS za total |
| Pre/post nivelacija | imported | 132 event-a u sirovom oracle-u | API 0 comparable totals, critical/blocked | NOT CERTIFIED; zrelost/drift evidence otvoren |
| Inventar / kapital | imported | `Artikli.DataOrigin=access`: 12.422 SKU, 3.566 kom, 9.572.063 RSD po NC Din | API status vraća 0 SKU / 0 on-hand | FAIL: scope token bug u handleru |

Potvrđena inventory greška je u `Application/Analytics/Queries/GetInventoryStatus/GetInventoryStatusHandler.cs`: `ProductsDim` se filtrira sa `DataOrigin == normalizedDataScope`, pa `imported` traži literal `imported`, dok kanonska politika i stvarni podaci koriste `access`. Operativni fallback koristi ispravno mapiranje. Ova popravka pripada InventoryStatus vlasniku, nije tiho ugrađena u RQ601/RQ602 i nije promenila bazu u ovom run-u.

Za supplier/shoe type nije nađen gubitak zbog `Artikli` inner join-a u canonical retail totalima; oracle radi preko prodajnih zaglavlja/stavki i dimenziju prikazuje kao unknown kada nedostaje. Nema dokaza dvostrukog agregiranja u proveravanim totalima.

## Odluke i ograničenja

1. RQ601 ostaje `PARTIAL`: originalnih 20 kalkulacionih dokaza nije u lokalnom repozitorijumu/bazi, pa nema dozvole za promenu mapiranja ili proglašenje RSD kapitala sertifikovanim.
2. RQ602 ostaje `PARTIAL`: istorijski dry-run je dostavljen sa kontrolama i svim gap-ovima, ali nema vlasnički odobren prospektivni pilot ni uzročni dokaz.
3. Inventory scope mismatch je potvrđen i evidentiran kao zaseban owner-boundary nalaz; nije otvoren duplikat queue zadatka i nije izvršena popravka mimo RQ601/RQ602 vlasništva.
4. Nisu menjane formule prometa/marže, DEMO/testni redovi, produkcijska baza niti originalni Access/MDB izvor.

SQL dokazi:

- `tools/purchase-cost-scale-reconciliation.sql`
- `tools/markdown-pilot-measurement.sql`
- `tools/access-import-23-fact-audit.sql`
- `tools/access-import-23-mdb-event-reconciliation.sql`

Svi upiti u ovom run-u su izvršeni read-only ili nad izolovanim testnim fixture-om; nema `INSERT`, `UPDATE`, `DELETE`, repair migracije ili novog importa.

## RQ602 završno očvršćavanje — frozen cohort i dokaz potpunosti

Ovaj odeljak dopunjuje i superseduje raniju formulaciju da su odabrani
istorijski prozori bili „potpuni“. Per-store minimum/maksimum datuma može da
ograniči horizon, ali ne dokazuje da je svaki dan izvora bio pokriven. U
operativnoj šemi batch-a #23 nema dnevnog store/source coverage manifesta.
Zato je nova merna skripta namerno konzervativna: svaki prozor je
`unverified_source_coverage_not_proven`, a nijedna poznata numerička marža nije
prikazana kao sertifikovana.

### Zamrznuta kohorta

Tačan manifest svih 20 tretiranih događaja i 20 kontrolnih artikala je u
`tools/markdown-pilot-cohort-2026-10-10.sql`. Manifest čuva event ID,
`SourceRowId`, artikal, prodavnicu, dobavljača, tip obuće, datum akcije,
prethodnu/narednu cenu, kontrolni artikal i kriterijum izbora. Kohorta je
ponovljiva: događaji su uređeni po datumu opadajuće, artiklu, prodavnici i
event ID-ju; kontrole su birane po determinističkom najmanjem neiskorišćenom
ID-ju u odgovarajućem stratumu. Nema slučajnog izbora ni zavisnosti od
trenutnog redosleda rezultata.

Sažetak provere manifest-a nad PostgreSQL batch-om #23:

| Provera | Rezultat |
|---|---:|
| Tretirani članovi / kontrole | 20 / 20 |
| Potvrđen akcioni događaj po tretiranom članu | 20/20 |
| Tretirani događaji sa više markdown-a u 56 dana | 0 |
| Kontrolni članci sa markdown događajem u prozoru | 0 |
| Store×batch horizon | 2082886995 / batch 23 |
| Store horizon | 2011-01-06 — 2026-08-05 |
| Posmatrani prodajni dani u horizon-u | 3.565 |
| Age-band dokaz | `unknown` za svih 40 članova |
| Kontrola sa punim supplier×shoe-type stratumom | 19/20 |

Batch metadata čuva `SourceFileName=a61a7605bf7b441a965ec65edd262f68.mdb`, ali
ne čuva `SourceFileHash`. Dve lokalne MDB datoteke koje su bile dostupne za
read-only proveru nisu korišćene kao zamena: `Trend plus.mdb` ima SHA-256
`00F54148475BA1FD4271C29CA3D9BC3ADC49C878F6BBDBEF84D881EA83624DD6` i period
do 2026-04-05, dok `TRENDPLUS.accdb` završava 2019-11-13. Manifest i metrike
iznad koriste isključivo već učitanu PostgreSQL populaciju sa
`SourceBatchId=23`; byte-preserving originalni MDB ostaje dokazni blocker za
dalju istorijsku sertifikaciju.

Jedini kontrolni par bez istog tipa obuće je kohorta 3 (supplier je isti,
shoe-type nije mogao biti uparen); zadržan je kao eksplicitno označen
`matched_supplier_only`, ne kao puni match. Svi prozori koriste
`Europe/Belgrade` lokalni datum i poluotvorene granice:
`pre [action_date - 28, action_date)`, `post [action_date, action_date + 28)`.

### Read-only rezultat iste kohorte

`tools/markdown-pilot-historical-measurement.sql` je izvršen nad stvarnim
lokalnim PostgreSQL-om. DUG/KOREKCIJA su isključeni, potpisane količine su
sačuvane, a trošak bez dokazanog porekla nije pretvoren u nulu.

| Populacija | Pre količina | Posle količina | Pre promet RSD | Posle promet RSD | Promena prometa RSD | Pre neproverena marža RSD | Posle neproverena marža RSD |
|---|---:|---:|---:|---:|---:|---:|---:|
| 20 tretiranih | 22 | 76 | 161.110 | 426.540 | +265.430 | 76.015 | 146.064 |
| 20 kontrola | 0 | 0 | 0 | 0 | 0 | — | — |

Ukupna promena potvrđene cene u manifestu je −31.380 RSD po zbiru članova;
to je samo opisna promena cena, ne poslovna preporuka. Kontrolna populacija
ima nultu prodaju u ovom uzorku, pa razlika promena nema validan kontrolni
benchmark. Brojevi su promenili odnos prema ranijem dry-run-u zato što je
sada korišćen frozen manifest i poluotvoreni prozor bez lažnog „complete“
statusa; raniji rezultat ne treba mešati sa ovom kohortom.

### Cost/margin i potpunost

U lokalnom batch-23 targetu nema pouzdanog per-line istorijskog cost-provenance
signala dovoljnog za sertifikaciju. Numerički `nabavna_cena` je zato prikazan
kao `descriptive_unproven_cost`; `NULL` ostaje nepoznat i ne utiče na zbir kao
nula. Nema dokaza da inbound ili povrat kupca sami po sebi predstavljaju
originalnu kalkulacionu cenu.

Skripta sada razdvaja:

- observed sales od verified empty dana;
- store horizon od dnevnog source-coverage dokaza;
- poznatu numeričku maržu od sertifikovane istorijske marže;
- tretirani događaj od kontrole i ponovljenog markdown-a.

Ne koristi trenutno `Artikli.Kolicina` kao istorijski završni stock, ne aktivira
Actions/preporuke i ne tvrdi uzročni efekat. RQ602 ostaje **PARTIAL** dok
vlasnik ne odobri prospektivni pilot sa daily coverage manifestom,
sertifikovanim cost provenance-om i unapred registrovanom kohortom.

### PostgreSQL sertifikacija

Fokusirana regresiona suite je izvršena na postojećem lokalnom PostgreSQL-u,
sa izolovanom test bazom koju test kreira i uklanja. TRX rezultat
`artifacts/rq602-results/RQ602.postgres.trx` ima `total=2`, `executed=2`,
`passed=2`, `failed=0`, `notExecuted=0`; nema tihog `return`/skip-a za
nedostupnu bazu. Test pokriva poluotvorene 28-dnevne granice, potpisani povrat,
DUG/KOREKCIJA, NULL/procenjeni trošak, ponovljeni markdown, neprovereni store
horizon i determinističko ponovno izvršavanje.
