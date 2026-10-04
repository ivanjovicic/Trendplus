# Responzivnost aplikacije — ponovni audit (2026-10-04)

Datum: 2026-10-04 (merenja 21:10–21:40 po beogradskom vremenu)
Osnova koda: `origin/main` `6a2a23b3`
Živi UI: `https://trendplus.vercel.app`
Dokazi: `.ai/runs/2026-10-04-responsive-reaudit-evidence.md`

## 1. Kratak zaključak

Od 01.10. urađen je veliki deo responsive programa: P-UI-24 do P-UI-30 i P-UI-32, 33, 34 i 37 (11 od 15 promptova). Rezultat se vidi uživo. Na telefonu (360 i 390 px) i tabletu (768 px) nijedan od 34 merena ekrana nema horizontalni skrol na praznom ekranu. Mobilni meni je ispravan dijalog (fokus ostaje u meniju, Esc ga zatvara, pozadina se ne skroluje, širina 320 px). Polja za unos na telefonu imaju 16 px, a forme za prodaju, prijem robe i nivelaciju su prilagođene.

Aplikacija ipak **još ne izgleda dobro na svim uređajima**. Ima pet konkretnih problema:

1. **Telefon, kada ekran ima podatke:** Boja, Vrsta obuće, Nivelacije pre/posle, Pilot izveštaj i Zalihe su širi od ekrana. Telefon zato smanji celu stranicu za oko 17% (širina rasporeda postaje 426–433 px umesto 360) ili traži skrol u stranu. Uzrok su padajuće liste sa dugim nazivima objekata i sezona, kao i tabele i kartice koje se ne skupljaju.
2. **Mali laptop i tablet položen (1024 px):** zaglavlje ima 3 reda (166–177 px) i stalno zauzima 22–23% ekrana. Bočni meni je otvoren (320 px), pa sadržaju ostaje samo oko 704 px. Na Boji, Vrsti obuće i Prioritetima pre nivelacije filteri zato izlaze 104–106 px van ekrana.
3. **Tablet na dodir (768 px):** polja imaju 12–14 px, a dugmad 26–42 px, iako dogovoreni standard traži 16 px i 44 px.
4. **Na telefonu se do podataka dolazi predugo:** blok o pouzdanosti na vrhu svakog ekrana zauzima 650–1436 px. Prvi pokazatelj (KPI) pojavljuje se tek posle 2 do 5 visina ekrana. Na Prioritetima pre nivelacije to je 4056 px.
5. **Široke tabele (Dnevna prodaja 21 kolona, Zalihe 17, Vrsta obuće, Prioriteti) nemaju zakucanu prvu kolonu,** pa se pri skrolu u stranu gubi koji je red u pitanju.

Ocena: telefon je **delimično dobar**, tablet uspravno **dobar uz sitne nedostatke**, tablet položen i mali laptop **slabi**, a veliki desktop **dobar**.

## 2. Metod i ograničenja

- Headless Chrome (Playwright-core sa lokalnim Chrome-om) na živoj aplikaciji, bez prijave. Profili:
  - telefon: 360×780 i 390×844, emulacija mobilnog uređaja i dodira;
  - tablet: 768×1024 i 1024×768, dodir;
  - laptop: 1280×800.
- Prvi prolaz: 34 rute sa podrazumevanim periodom. Drugi prolaz: 10 ključnih ekrana sa periodom `07.07–05.08.2026`, kada tabele i grafikoni imaju prave redove (bez toga su ekrani prazni, jer podaci staju 05.08). Treći prolaz je tražio koji element proširuje stranicu.
- Mereno je: prelivanje dokumenta, stvarna širina rasporeda (`innerWidth`), visina zaglavlja, veličina dugmadi i linkova, veličina slova u poljima, tekst manji od 12 px, širina tabela i grafikona u odnosu na kontejner, i položaj prvog KPI-ja. Uz to su urađeni snimci ekrana.
- Ograničenja:
  - Ovo je Chromium emulacija, ne pravi iPhone i iPad Safari. Zumiranje pri fokusu i tastatura na iOS-u nisu dokazani.
  - Verziju (SHA) Vercel builda nije moguće pročitati iz UI-ja, pa je svaki nalaz povezan i sa linijom koda na sveže `main` grani.
  - Čitljivost oznaka na osama grafikona nije merena. Grafikoni se ispravno skupljaju na širinu kontejnera.
  - Štampa je proverena samo u kodu (6 print stilova).

## 3. Šta je urađeno

| Oblast | Prompt | Status | Dokaz uživo |
|---|---|---|---|
| Merni alat (Puppeteer baseline) | P-UI-24 | DONE | `npm run responsive:baseline`, 13 ruta, 5 širina |
| Osnova: 16 px polja i 44 px dugmad na telefonu | P-UI-25 | DONE | na 360 sva polja imaju 16 px (`tailwind.css:604`, `max-width: 759px`) |
| Mobilno zaglavlje i meni kao dijalog | P-UI-26 | DONE | zaglavlje 80 px na 360 i 64 px na 768; meni je dijalog, Esc ga zatvara, fokus ostaje u njemu, pozadina se ne skroluje |
| Modal, InfoTip i tabovi | P-UI-27 | DONE | modali ograničeni na `calc(100vw - 2rem)`, a na telefonu otvaraju se kao donji panel |
| Filter pilot (Zalihe) | P-UI-28 | DONE | važi samo za Dashboard, Dnevnu prodaju i Zalihe |
| Tabela pilot (zakucana prva kolona) | P-UI-29 | DONE | uključena samo na Listi artikala, Akcijama i Boji |
| Unos prodaje, robe i nivelacije | P-UI-30 | DONE | `/prodaja` na 360: polja 16 px, nema prelivanja |
| Product Decision | P-UI-32 | DONE | na 360 su kartice umesto tabele; na 768 zakucana prva kolona |
| Centralne akcije | P-UI-33 | DONE | nema prelivanja |
| Dashboard i Dnevna prodaja | P-UI-34 | DONE | grafikoni prate širinu (296 px na 360) |
| Lista artikala i duži rep | P-UI-37 | DONE | tabela 760 px u skrol oblasti sa zakucanom prvom kolonom |
| Supplier pregled | P-UI-31 | WAITING | zavisnosti su ispunjene; razlog odlaganja (RQ487) je zastareo, RQ487 je DONE |
| Nivelacije pre/posle i Prioriteti | P-UI-35 | WAITING | zavisnosti ispunjene; čeka RQ553/552/556/571 |
| Hub, Vrsta obuće i Boja | P-UI-36 | WAITING | zavisnosti ispunjene; proveriti RQ575/RQ580 |
| Regresioni gate | P-UI-38 | WAITING | čeka da se migracije završe |
| Recharts preload (performanse) | PERF18 | WAITING | uslov (P-UI-24) je ispunjen; vlasnik je PERF |

## 4. Rezultati merenja (sažetak)

**Prelivanje u stranu.** Prvi prolaz, bez podataka: 0 ekrana preliva na 360, 390 i 768. Na 1024 prelivaju Boja (+106 px), Vrsta obuće (+106), Prioriteti (+104) i Logovi (+81). Drugi prolaz, sa podacima: na 360 se širina rasporeda širi na Boji (426), Vrsti obuće (433), Nivelacijama pre/posle (433), Pilot izveštaju (433, i bez podataka) i Zalihama (431).

**Visina zaglavlja.** 80 px na telefonu, 64 px na 768, **177 px na 1024 i 166 px na 1280** (lepljivo zaglavlje).

**Blok pouzdanosti i prvi KPI na 360 (sa podacima):**

| Ekran | Blok pouzdanosti | Prvi KPI ili tabela |
|---|---|---|
| Vrsta obuće | 1436 px | 2403 px |
| Prioriteti pre nivelacije | 1287 px | 4056 px |
| Zalihe | 1341 px | – |
| Dnevna prodaja | 1139 px | 1777 px |
| Boja | 1036 px | – |
| Nivelacije pre/posle | 928 px | – |
| Product Decision | 855 px | 1402 px |
| Dobavljači | 749 px | – |
| Pilot izveštaj | 649 px | 1711 px |

**Tablet na dodir (768).**
- Polja: 13 px na Dashboardu, Zalihama, Akcijama, Boji, Vrsti obuće i Dnevnoj prodaji; 12,16 px na Product Decision; 14 px na `/prodaja`.
- Dugmad i linkovi manji od 44 px: meni-dugme 35×35, „Više“ 70×34, linkovi u putanji (breadcrumb) visoki 17 px, `kpi-explain-button` 116×17, čipovi na `/prodaja` 94×27, akcije na `/dobavljaci` 71×26 (svih 237 elemenata je premalo).

**Široke tabele (sa podacima).**
- Dnevna prodaja: 21 kolona, 1795 px u oblasti od 297 px na 360; prva kolona nije zakucana; ćelije 12 px.
- Zalihe: 17 kolona, 1323 px.
- Vrsta obuće: 10 kolona, 1730 px u 260 px.
- Prioriteti: 956 px.
- Pilot izveštaj: tabele od 406–417 px bez ikakve skrol oblasti, jer CSS klasa ne postoji.

**Sitan tekst (<12 px) na 360:** Insight Studio 67 elemenata, Pilot spremnost 28, Product Decision 22, ostali analitički ekrani 9–19.

**Dugačke stranice na 360:** `/dobavljaci` 8940 px (237 redova bez straničenja), `/nivelacije` 6174, `/dnevnik-promena` 5177, Zalihe sa podacima 30.298, Prioriteti 18.538.

**Ostalo.**
- Indikator učitavanja je na engleskom („Loading data / 1 request in progress“) i na telefonu prekriva sadržaj.
- Sezonski karusel je na svakom ekranu: sam se pomera svaka 4 s, bez poštovanja podešavanja za smanjeno kretanje (reduced motion), a dugmad su široka 36 px.
- Viewport meta je ispravan (`width=device-width, initial-scale=1.0`, zumiranje nije zabranjeno).
- Datumi koriste nativni `input type=date` (31 mesto), što je dobro za mobilne uređaje.
- U CSS-u postoji 26 različitih breakpoint vrednosti (900, 940, 960, 1200 …). Shell koristi Tailwind `lg` = 1024.

## 5. Najvažniji nalazi (sa dokazom)

1. **Filteri šire stranicu na telefonu i laptopu (P1).** Kod: `AnalyticsControlBar.css:153-158` (`repeat(4, minmax(180px,1fr))`), `:174-183` (select bez `width`/`min-width`); bezbedna pravila postoje samo za pilot varijantu (`:192-203`), a pilot je uključen na samo 3 stranice. Uživo: `innerWidth` 426–433 na 360, prelivanje 104–106 px na 1024. → **P-UI-39**
2. **Zaglavlje sa 3 reda i bočni meni od 320 px na 1024–1279 (P1).** Kod: `HeaderStatus.tsx:400-401,457`, `Sidebar.tsx:90,231`, `AppLayout.tsx:13`. Uživo: zaglavlje 177/166 px, sadržaj 704 px. → **P-UI-40**
3. **Pilot izveštaj i Zalihe šire stranicu na telefonu (P1).** Kod: `PilotDataQualityIntakeReport.tsx:220-221` (klase bez CSS-a), `InventoryInsightPanels.tsx:66-67` (grid stavke bez `min-w-0`). Uživo: 433 i 431 px. → **P-UI-41**
4. **Tablet na dodir nema standard od 16 i 44 px (P2).** Kod: `tailwind.css:604-622` (pravila važe samo do `max-width: 759px`). → **P-UI-42**
5. **Blok pouzdanosti zatrpava telefon (P2).** Kod: `AnalyticsTrustHeader.tsx:292`, a jedini breakpoint je u `.css:409`. → **P-UI-43** (posle RQ569)
6. **Široke tabele bez zakucane kolone (P2).** Kod: `AnalyticsDataTable.css:125-160` (zakucana kolona samo do 900 px i samo za pilot), `InventoryItemsTable.tsx`. → **P-UI-44**; Vrsta obuće i Prioriteti ostaju u P-UI-35/36.
7. **Engleski indikator učitavanja i karusel u pokretu (P2).** Kod: `GlobalRequestSpinner.tsx:26-29`, `SeasonalImageCarousel.tsx:74-79`. → **P-UI-45**
8. **Operativne i šifarnik liste nisu za telefon (P3).** `/dobavljaci`, `/nivelacije`, `/dnevnik-promena`, `/logs` (+81 px na 1024), `ConfigurationPage.css:493` (`min-width: 1120px`). → **P-UI-46**
9. **P-UI-31, 35 i 36 stoje iako su im zavisnosti ispunjene.** Razlog za P-UI-31 je zastareo (RQ487 je DONE). → addendumi
10. **Merni alat (baseline) nije uhvatio stvarne greške:** radi sa fixture podacima, ima 13 ruta i ne proverava `innerWidth`. → addendum P-UI-38

## 6. Novi promptovi (queue `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`)

| ID | Prioritet | Status | Ukratko |
|---|---|---|---|
| P-UI-39 | P1 | **READY** (primarni) | Filter traka bezbedna po defaultu: bez širenja stranice na telefonu i na 1024 |
| P-UI-40 | P1 | **READY** | Mali laptop: zaglavlje u jednom redu i uzan bočni meni koji pamti izbor |
| P-UI-41 | P1 | **READY** | Pilot izveštaj (tabele u skrol oblasti) i paneli Zaliha bez širenja |
| P-UI-42 | P2 | WAITING (posle P-UI-39) | 16 px polja i 44 px dugmad i na tabletima na dodir |
| P-UI-43 | P2 | WAITING (posle RQ569) | Sažet blok pouzdanosti na telefonu, sa detaljima na dodir |
| P-UI-44 | P2 | WAITING (posle RQ569) | Zakucana prva kolona za Dnevnu prodaju i Zalihe |
| P-UI-45 | P2 | **READY** | Indikator učitavanja na srpskom koji ne prekriva sadržaj; karusel samo na početnoj strani (odluka 04.10.) i poštuje smanjeno kretanje |
| P-UI-46 | P3 | WAITING (posle P-UI-42) | Operativne i šifarnik liste upotrebljive na telefonu |

Addendumi: P-UI-31, P-UI-35, P-UI-36, P-UI-38 i RQ582 (Insight Studio: 67 sitnih tekstova; po Ivanovoj odluci ekran se sakriva, pa responsive rad nije potreban dok je sakriven).

Redosled: prvo shell i zajedničke komponente (P-UI-39, 40, 41, 45 paralelno, jer dele različite fajlove), zatim temelj za tablet (P-UI-42), pa ekrani (P-UI-43 i 44 posle RQ569, P-UI-35 i 36 kada se oslobode njihovi RQ vlasnici, P-UI-31), zatim duži rep (P-UI-46) i na kraju gate (P-UI-38).

## 7. Druga provera (verifikacija nalaza i promptova)

Svaka citirana linija koda ponovo je pročitana na `6a2a23b3`. Ispravke posle druge provere:

- `tailwind.css:470` **nije** pravilo za mobilna polja (to je modal kao donji panel). Pravilo za 16 px i 44 px je u `tailwind.css:604-622` i važi do `max-width: 759px`. Zato je P-UI-42 opisan za opseg 760–1023 px, a ne 640–1023.
- Linija za glatko pomeranje karusela je `SeasonalImageCarousel.tsx:88-93`, a ne 86-90.
- P-UI-42 sada eksplicitno proverava preklapanje sa P-UI-40 (oba diraju klase u `HeaderStatus.tsx`).
- Potvrđeno je da se problem filtera ne javlja na Dobavljačima (360 i 1024 bez prelivanja), pa P-UI-39 ne tvrdi da je tamo greška.
- Potvrđeno je da Pilot izveštaj širi stranicu i bez podataka (prvi prolaz, 390 → 433).
- RQ582 već ima Ivanovu odluku (sakriti Insight Studio). Addendum i P-UI-46 su ispravljeni tako da ne traže novu odluku.
- RQ583 već ima odobren globalni baner na svim analitičkim rutama. P-UI-40 sada definiše kako se baner uklapa u visinu zaglavlja.

## 8. Odluke vlasnika

1. **Sezonski karusel — ODLUČENO (Ivan, 2026-10-04 21:36):** karusel se prikazuje samo na početnoj strani `/` i uklanja se sa analitičkih ekrana i ekrana za unos. Upisano u P-UI-45 (obim, koraci, testovi i prihvatanje). Pitanje je zatvoreno.
2. **Insight Studio (RQ582):** Ivan je već odlučio (04.10.) da se sakrije iza oznake „Eksperimentalno“. Zato nije registrovan responsive prompt za njega. Responsive migracija je uslov samo ako se ekran ponovo vrati u meni. Odluka nije potrebna.
3. **Pravi uređaji:** za potpuni dokaz potreban je bar jedan iPhone (Safari) i jedan iPad. Preporuka: posle P-UI-39 i P-UI-42 jedan ručni prolaz po kontrolnoj listi iz `ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md`.

## 9. Dopuna 2026-10-04 21:36

Ivan je odlučio da se sezonski karusel prikazuje samo na početnoj strani. Otvoreno pitanje iz odeljka 8 je zatvoreno, a odluka je upisana u P-UI-45.
