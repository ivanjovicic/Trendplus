# Trendplus — skrivena vrednost i UX: nezavisni product/UX audit (15.10.2026)

**Baza:** `origin/main` `5360b34b`. **Metod:** statička analiza koda i testova (rute, `navConfig.ts`, stranice, komponente, međusobni linkovi). **Ekrani nisu otvarani u browseru** — svaka UX ocena ovde je *nije potvrđeno u browseru*. Development okruženje i lokalna baza nisu korišćeni. Ovo nije merenje produkcije, kupca ni poslovnog ishoda.

**Odnos prema kanonskim dokumentima:** ne otvara DONE stavke (RQ585, RQ659–RQ668, RQ666/667) i ne pravi nove RQ ID-jeve ni READY statuse. Vlasništvo veće stavke ostaje u postojećem queue-u; ovde su samo zavisnosti. Prethodni UX audit (`docs/qa/ANALYTICS_FRONTEND_UX_AUDIT_2026-10-14.md`) pokriva formatiranje/i18n/a11y; ovaj dokument pokriva **povezanost, hijerarhiju i istinitost poruka**.

## 1. Inventar funkcija

| Oblast | Ruta / izvor | Stanje | Napomena |
|---|---|---|---|
| Pregled poslovanja | `/analytics` (`AnalyticsDashboard.tsx`) | IZGRAĐENO, DOSTUPNO | Već ima „U 30 sekundi”, „Gde zarađujemo?”, „Gde gubimo novac?”, „Šta treba uraditi ove nedelje?”, kvalitet podataka. Obrasci A/B/C/F postoje. |
| Početna | `/` (`HomePage.tsx`) | DELIMIČNO | Lista linkova + „Backend status” kao jedini broj; vlasnik ne vidi ni period ni prvi problem. |
| Dnevna prodaja, smene | `/analytics/daily-sales` | DOSTUPNO, NEDOVOLJNO KORIŠĆENO | U „Dodatne analize”; nema linka ka dobavljaču/tipu za izabrani dan. |
| Dobavljači (prodaja, ocena, pregovarački paket) | `/analytics/supplier` (tabovi), `/analytics/supplier/report` | DOSTUPNO | Paket za pregovor (vrednost/rizik, linkovi ka prodaji i zalihama) je jak ali duboko u tabu. |
| Tip obuće, boja | `/analytics/shoe-type-sales-stats`, `/analytics/color-sales-stats` | DOSTUPNO | Tip obuće nema nijedan ulazni link sa drugih ekrana. Boja je ispravno označena kao sekundarna. |
| Zalihe (bilans, ABC, staro/sporo, pokrivenost, size curve, prognoza, poređenje prodavnica, akcije, izvoz) | `/analytics/inventory` (`InventoryPage.tsx` + `components/inventory/*`) | IZGRAĐENO, DELIMIČNO POVEZANO | Size curve zavisi od snapshot tabele (status UNKNOWN je pošteno prikazan). Redistribucija nije vodila ka prenosima (popravljeno, §5). |
| Prenosi robe (draft→confirm→complete/cancel) | `/transfers` (`TransferPage.tsx`, `TransferService`) | IZGRAĐENO, IZOLOVANO | Pre ove izmene nijedan analitički ekran nije linkovao na prenose. |
| Odluke o proizvodima | `/analytics/products` | IZGRAĐENO, SKRIVENO | U „Dodatne analize” iako je najbogatiji ekran odluka (preporuka, razlozi, akcija). |
| Prioriteti nivelacije | `/analytics/pre-nivelacija-prioriteti` | DOSTUPNO | Na primarnom ekranu stoji „Eksperimentalno poređenje rangiranja v9”. Nije vodilo ka Pre/Posle (popravljeno). |
| Pre/Posle nivelacije, ishod sniženja | `/analytics/nivelacije-pre-post` | DOSTUPNO, IZOLOVANO | Jedini ekran koji pokazuje šta su ranije nivelacije donele (deskriptivno). Nije imao ulazni link ni sa jednog ekrana. |
| Puls odluka (owner digest, RQ585 DONE) | `/analytics/decision-pulse` | IZGRAĐENO, SKRIVENO | Link samo sa Početne i iz „Dodatne analize”. |
| Izvršni pregled odluka | `/analytics/decision-board` | IZGRAĐENO, SKRIVENO, PREKLAPA SE | Spaja proizvode, zalihe, dobavljače, akcije i kvalitet; nijedan ulazni link osim menija. Naziv se razlikovao od menija (popravljeno). |
| Akcije i preporuke, ishodi | `/analytics/actions` | DOSTUPNO | Sažetak ishoda po statusu/prioritetu/izvoru = obrazac E već postoji. |
| Kvalitet podataka, svežina, pilot izveštaj | `/analytics/data-quality`, `/analytics/reports/pilot-intake` | DOSTUPNO | Svaki ekran linkuje na kvalitet podataka (dobro). |
| Povraćaj robe dobavljaču | `/povracaj` | DOSTUPNO | Kartica na Početnoj je pisala „Obrada reklamacija” (kupac) iako je ekran povraćaj dobavljaču (popravljeno). |
| Uvoz, nabavne cene, produkcija | Access import, RQ601, STAB16 | BLOKIRANO VAN OVOG AUDITA | Vlasnici: RQ601 (originalne kalkulacije, vlasnik), STAB16/RQ565 (pristup produkciji), PR #163 (identitet prodaje, draft), RQ679 (Render konfiguracija). |

## 2. Top 15 predloga (rang: vrednost × lakoća × nizak rizik)

Format: **klasa · prioritet** — ekran/fajlovi — trenutno → problem → predlog — efekat — složenost/rizik — testovi.

1. **QUICK WIN · P1 — URAĐENO** — Prioriteti ↔ Pre/Posle (`PreNivelacijaPriorityPage.tsx`, `ProdajaPrePostNivelacijePage.tsx`). Dva ekrana iste odluke nisu bila povezana; vlasnik nije mogao iz kandidata da vidi kako su prošle ranije nivelacije istog dobavljača. Dodati dvosmerni link sa istim dobavljačem, prodavnicom i opsegom; link ka Pre/Posle se nudi samo kada se opseg poklapa (Pre/Posle čita zajednički opseg). Efekat: obrazac E na mestu odluke. S / nizak. Testovi: href sa kontekstom, sakriven pri različitom opsegu.
2. **QUICK WIN · P1 — URAĐENO** — Redistribucija (`components/inventory/RebalancingTable.tsx`). Kolona „Sačuvana prodaja” i naslov „Pametna redistribucija … očekivanim uštedama” prikazivali su procenu (`expectedSavedSales`) kao ostvarenu uštedu. Preimenovano u „Procena moguće prodaje”, naslov „Predlozi redistribucije”, objašnjenje da nije dokazana ušteda; dodat link „Otvori prenose robe”. Efekat: istinitost + put problem→akcija. S / nizak. Testovi: naslov, kolona, `null`→„Nije dostupno”, link.
3. **QUICK WIN · P2 — URAĐENO** — Izvršni pregled odluka (`ExecutiveDecisionBoardPage.tsx`, `analyticsRouteDefinitions.ts`). Meni „Izvršni pregled odluka”, naslov „Izvršni board odluka”, izvor „Decision Board aggregate” (engleski). Usklađeno na „Izvršni pregled odluka” i srpski opis izvora. S / nizak.
4. **QUICK WIN · P2 — URAĐENO** — Početna, kartica Povraćaj (`components/dashboard/DashboardCards.tsx`): „Obrada reklamacija” → „Vraćanje robe dobavljaču”. S / nizak.
5. **UX IMPROVEMENT · P1** — Početna (`HomePage.tsx`). Jedini broj je „Backend status”. Predlog: prvi blok = datirani period poslednjih pouzdanih podataka i link „Otvori pregled poslovanja”; redosled kartica po nedeljnom toku iz playbook-a (§7): Pregled → Puls odluka → Zalihe → Dobavljači → Prioriteti nivelacije → Kvalitet podataka; backend status u podnožje. Bez novih brojeva na Početnoj dok ih ne vraća postojeći API sa periodom. S / nizak. Testovi: `HomePage.truth.spec.tsx` (nema ručno upisanih KPI), redosled.
6. **EXISTING FEATURE UNDERUSED · P1** — Puls odluka i Odluke o proizvodima u meniju „Dodatne analize”. `navConfig.spec.ts` čuva „owner-approved eight primary” raspored, zato **nije menjano**. Predlog za vlasnika: zameniti „Pre/Posle nivelacije” u primarnoj grupi sa „Puls odluka” (Pre/Posle je sada dostupan iz Prioriteta), a „Pilot spremnost” spustiti na kraj „Dodatne analize”. Potrebna odluka vlasnika. S / nizak.
7. **UX IMPROVEMENT · P1** — Tri ekrana odluka (Puls, Izvršni pregled, Akcije) bez jasne podele. Predlog: Puls = „šta pogledati ove nedelje”, Akcije = „šta smo odlučili i ishod”, Izvršni pregled = sekundarni pregled za sastanak. U `AnalyticsTrustHeader` opisu svakog ekrana jedna rečenica „Za … koristi …” sa linkom. Bez spajanja ekrana. S / nizak.
8. **QUICK WIN · P2** — Prioriteti nivelacije: blok „Eksperimentalno poređenje rangiranja (v9)” je na primarnom ekranu. Predlog: sklopiti u `<details>` „Tehničko poređenje rangiranja” ispod liste. Ne menjati v10 logiku. S / nizak. Test: postojeći test „labels v9 comparison experimental” + zatvoreno podrazumevano.
9. **EXISTING FEATURE UNDERUSED · P2** — Dobavljač: pregovarački paket (vrednost i rizik) postoji samo pri izboru jednog dobavljača. Predlog: na „Gde zarađujemo?/Top dobavljači” u Pregledu poslovanja dodati link „Paket za pregovor” na `/analytics/supplier?supplierId=…` sa istim periodom/prodavnicom/opsegom. S / nizak. Test: href nosi kontekst.
10. **UX IMPROVEMENT · P2** — Tip obuće nema ulaznih linkova. Predlog: iz Dnevne prodaje i Pregleda link „Po tipu obuće za ovaj period” sa istim periodom/prodavnicom. S / nizak.
11. **DATA/LOGIC GAP · P1 (blokirano)** — Nabavna cena: 501 stavka / 1.711.050 RSD prodaje sa nepoznatim troškom (RQ601). Marža i „gde gubimo” ostaju delimični. Vlasnik: RQ601 + vlasnik (originalne kalkulacije). Ne graditi „profit po dobavljaču” bez toga.
12. **DATA/LOGIC GAP · P1 (blokirano)** — Istorijsko stanje zaliha po prodavnici (RQ610). Bez toga nema tačnog trajanja nestanka, kapitala po starosti ni istorijskog sell-through-a. Prikazivati „vezan kapital” isključivo kao trenutnu procenu, nikad kao gubitak.
13. **BUSINESS OPPORTUNITY · P2** — „Ista patika, tri akcije” (prenos / nivelacija / povraćaj dobavljaču) iz `FOOTWEAR_DECISION_PRODUCT_OPPORTUNITIES_2026-10-15.md`: delovi već postoje (redistribucija, prioriteti, povraćaj). Prvi korak je samo povezivanje linkovima sa SKU/dobavljač kontekstom, ne novi model. Zavisi od RQ610 za tačnost.
14. **UX IMPROVEMENT · P3** — Dnevna prodaja: link sa dana na dobavljače/tipove tog dana. S.
15. **ALREADY GOOD** — Pregled poslovanja (pitanja kao naslovi, upozorenje o kvalitetu), Akcije (sažetak ishoda), svuda link ka kvalitetu podataka, oznaka boje kao sekundarne analize, istorijski vs. aktuelni podaci na Početnoj. Ne dirati.

## 3. Top 5 funkcija koje treba bolje izložiti

1. **Pre/Posle nivelacije + ishod sniženja** — jedini odgovor na „šta smo već probali”; sada povezano sa Prioritetima.
2. **Puls odluka** (RQ585) — gotov nedeljni digest, sakriven u sekundarnom meniju.
3. **Odluke o proizvodima** — najbogatiji ekran preporuka sa razlozima i akcijom, sekundarni meni.
4. **Pregovarački paket dobavljača** — vrednost, rizik, poznata nabavna cena, linkovi ka prodaji i zalihama; dostupan tek posle izbora dobavljača.
5. **Prenosi robe** — stvaran workflow; sada vidljiv iz redistribucije.

## 4. Početna i navigacija

**Varijante:**
- **V1 (izabrana): Početna kao nedeljni tok.** Postojeći `HomePage.tsx`: naslov sa datumom poslednjih pouzdanih podataka (iz postojećeg freshness odgovora) → 6 kartica poređanih po playbook toku (Pregled poslovanja, Puls odluka, Zalihe, Dobavljači, Prioriteti nivelacije, Kvalitet podataka) sa jednom rečenicom „šta ovde odlučuješ” → backend status u podnožje. Nema novih KPI na Početnoj.
- V2: preusmeriti `/` na `/analytics`. Brže, ali gubi vezu ka unosu/prodaji za operatere.
- V3: na Početnoj top-3 stavke iz Pulsa. Najveća vrednost, ali uvodi novi poziv API-ja i stanje greške na Početnoj; posle V1 i operator sesije.

**Obrasci A–F (šta postoji):** A „Šta zahteva pažnju” — Pregled („Najkritičniji negativni signali”), Puls. B „Zašto je važno” — Odluke o proizvodima (razlozi), Pregled („Gde gubimo novac?”). C „Sledeći korak” — „Dodaj u centralni red akcija” u Pregledu, akcije u Zalihama. D „Šta se promenilo” — uporedni periodi na Pre/Posle i dnevnoj prodaji; Pregled ima momentum samo uz pune 7-dnevne prozore (RQ668). E „Šta smo probali” — Akcije (ishodi), Pre/Posle (ishod sniženja), sada povezano iz Prioriteta. F „Zašto ne verovati broju” — `AnalyticsTrustHeader` + kvalitet podataka na svakom ekranu. **Zaključak:** obrasci postoje; nedostajali su linkovi između njih, ne novi ekrani.

**Navigacija:** vidi predlog 6 (traži odluku vlasnika jer test čuva odobreni raspored).

## 5. Implementirani quick win-ovi (ova izmena)

| # | Izmena | Fajlovi | Test |
|---|---|---|---|
| 1 | Prioriteti → „Pre/posle ranijih nivelacija” (dobavljač, prodavnica; samo pri istom opsegu) i Pre/Posle → „Prioriteti nivelacije” (dobavljač, prodavnica, opseg) | `PreNivelacijaPriorityPage.tsx`, `ProdajaPrePostNivelacijePage.tsx` | 3 nova testa u postojećim spec fajlovima, 20/20 stabilnih ponavljanja |
| 2 | Redistribucija: procena umesto „sačuvane prodaje”, objašnjenje, link „Otvori prenose robe” | `RebalancingTable.tsx` | `RebalancingTable.spec.tsx`, 20/20 |
| 3 | Jedinstven naziv „Izvršni pregled odluka” i srpski opis izvora | `ExecutiveDecisionBoardPage.tsx`, `analyticsRouteDefinitions.ts` | postojeći spec ažuriran |
| 4 | Kartica Povraćaj: „Vraćanje robe dobavljaču” | `DashboardCards.tsx` | — |

Nisu menjane formule, pragovi, preporuke, cene, zalihe, auth ni izvori podataka. Guardrail baseline nije menjan.

## 6. Sledeći plan (bez dupliranja backlog-a)

- **Kratkoročno (bez novih podataka):** predlozi 5, 7, 8, 9, 10 kao jedan mali frontend PR; predlog 6 posle odluke vlasnika.
- **Srednjoročno:** jedna stvarna operator sesija (`docs/pilot/RETAIL_OPERATOR_VALUE_SESSION_2026-10-14.md`, NOT RUN) da potvrdi da li tok Početna → Pregled → Puls → Zalihe/Prioriteti → Akcije vodi do odluke; tek onda najviše jedan P1 prompt po playbook pravilu.
- **Uslovno:** profit po dobavljaču i „gde gubimo” kao pun KPI — posle RQ601; istorijski sell-through/trajanje nestanka i tačnost redistribucije — posle RQ610; merenje efekta nivelacije — RQ592 posle svežeg izvora; sve aktuelne preporuke — posle STAB16/RQ565.

## 7. Završna procena (bez ulepšavanja)

- **3 najvrednije neiskorišćene funkcije:** Pre/Posle sa ishodom sniženja; Puls odluka; Odluke o proizvodima.
- **3 najveće UX slabosti:** Početna ne pokazuje ništa poslovno; tri preklapajuća ekrana odluka bez podele uloga; ekrani iste odluke nisu bili povezani (prioritet ↔ ishod, redistribucija ↔ prenos).
- **Ukloniti/pojednostaviti:** eksperimentalno v9 poređenje sa primarnog ekrana Prioriteta; „Backend status” kao glavni broj Početne; sekundarnu ulogu Izvršnog pregleda jasno označiti umesto trećeg „centra odluka”.
- **Najveći neiskorišćen komercijalni potencijal:** pregovarački paket dobavljača (datiran, sa poznatom nabavnom cenom) — konkretan dokument za sastanak sa dobavljačem; vrednost raste tek sa RQ601.
- **Šta bi vlasnik odmah primetio:** link sa kandidata za nivelaciju na rezultate ranijih nivelacija; iskren naziv procene u redistribuciji i direktan put do prenosa.
- **Šta apsolutno ne graditi dalje:** novi dashboard/centar odluka/digest, AI skorove, marketplace/scraping, „uštede” ili „profit” bez kontrolne grupe i bez originalnih kalkulacija.
- **Najbolja sledeća investicija:** originalne kalkulacije nabavnih cena (RQ601) + jedna stvarna operator sesija — ne novi kod.

## 8. Sprovedeno posle audita — 2026-10-15 (direktan zahtev, bez novog RQ)

- **Početna kao početni tok (predlog 5, V1):** naslov i opis; potvrđeni period prodaje iz postojećeg `/api/analytics/refresh-status` (`observedSalesPeriodFrom/ToUtc` = min/max datum Access prodaje, ukupno, ne po radnji ni izvoru; `lastSuccessfulImportAtUtc` = poslednji čist Access uvoz; `dataFreshnessStatus` = svežina uvoza). Jedan poziv pri otvaranju, bez pollinga i novog endpointa. Ako period nije dostupan ili je odgovor greška: „Poslednji potvrđeni period nije dostupan” + link na Kvalitet podataka; nepoznat status nikad nije prikazan kao svež. Link „Otvori pregled poslovanja”, 6 kartica sa poslovnim pitanjem (Pregled, Puls, Zalihe, Dobavljači, Prioriteti, Kvalitet), 5 operativnih akcija (Prodaja, Unos robe, Prenosi, Nivelacija, Povraćaj dobavljaču), manji backend status; OFFLINE je `role="alert"` uz objašnjenje.
- **Navigacija (predlog 6):** Puls odluka u glavnoj grupi, Pre/Posle u „Dodatne analize”, Pilot spremnost niže; odluka zapisana u `docs/ai/ANALYTICS_OWNER_DECISIONS_RQ601_RQ603_2026-10-09.md`.
- **Prioriteti → Pre/Posle:** link sada glasi „isti dobavljač i radnja (vremenski okvir bira odredišni ekran)”; prenose se samo pozitivni `vendorId`/`storeId` (negativan ID se ranije prosleđivao — nađeno testom), nikad period.
- **v9 senka:** u zatvorenom `<details>` „Tehničko poređenje rangiranja (ne menja aktivnu preporuku)”; bez promene proračuna.
- **Akcije:** „Završeno” nosi objašnjenje da je akcija sprovedena, a ne da je korist ostvarena.
- **Nije rađeno (preporuka):** opisi uloga Puls/Izvršni pregled/Akcije u zaglavljima (predlog 7) — više fajlova i specova, posebna mala izmena; isticanje linka „Izveštaj za razgovor sa dobavljačem” — `SupplierDecisionHub` je blizu RQ680 putanja; objašnjenje Prenosi u `RebalancingTable` je već urađeno u PR #179.
