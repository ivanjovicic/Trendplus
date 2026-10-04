# Trendplus Analytics Design System (ciljni)

Status: canonical target (2026-10-04). Uveden iz `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`.
Odnos prema drugim dokumentima: `docs/ai/FRONTEND_UX_STANDARDS.md` ostaje kratak standard (šta je obavezno). Ovaj dokument je detaljna specifikacija (kako). `docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md` ostaje vlasnik vizuelne regresije. Ako dođe do konflikta, važi `FRONTEND_UX_STANDARDS.md`, pa se ovaj dokument ispravlja.
Izvršenje: P-UI queue (`docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`, P-UI-47 do P-UI-52, uz Codex responsive promptove P-UI-39 do P-UI-46). Ovaj dokument ne autorizuje runtime izmene bez prompta.

## 1. Principi

1. **Backend je istina.** UI prikazuje `recommendationAllowed`, svežinu, confidence, score, periode, DQ i provenance tačno onako kako ih backend vrati. Kada polje nedostaje, prikazuje se "nepoznato". UI nikad ne računa, ne podiže i ne spušta poslovni status.
2. **Odluka pre dekoracije.** Prvi ekran mora da odgovori na pitanja: koji period, da li su podaci sveži, šta da uradim.
3. **Tri stanja vrednosti:** vrednost · "—" + razlog (blokirano) · "nepoznato". Nula je samo stvarna nula iz backenda.
4. **Status nikad samo bojom:** svaka statusna informacija ima ikonu i tekst.
5. **Jezik korisnika:** srpski, bez developer žargona (§8).

## 2. Tokeni

### 2.1 Jedan izvor
- Kanonski izvor tema je `context/ThemeContext.tsx` (sve teme, sve vrednosti). `styles/themes.css` sadrži samo no-JS fallback koji je generisan iz istih vrednosti ili ih ručno ogleda, uz test parnosti. Token blok u `tailwind.css` ne definiše boje tema; samo mapira Tailwind imena na CSS varijable.
- `styles/themeTokens.ts` referencira samo varijable koje postoje.
- Komponente koriste samo semantičke tokene (§2.2). Hex, `rgb()` i Tailwind paleta (`amber-500`, `red-600`…) nisu dozvoljeni u analitičkim stranicama, osim u test fixture-ima.

### 2.2 Semantičke uloge (obavezne u svakoj temi)

| Uloga | Tokeni | Pravilo |
|---|---|---|
| Površine | `--surface-default`, `--surface-elevated`, `--surface-light`, `--surface-darker`, `--surface-elevated-light`, `--surface-elevated-dark` | Svaka tema definiše svih šest; svetle teme nemaju tamne vrednosti |
| Tekst | `--text-primary`, `--text-secondary`, `--text-muted` | ≥ 4,5:1 na `--surface-default` i `--surface-elevated` |
| Ivice | `--border-default`, `--border-hover`, `--border-strong` | ≥ 3:1 za ivice kontrola |
| Akcent (brend) | `--accent-primary`, `--text-on-primary` | Samo za interakciju i izbor, nikad za status |
| Status površina | `--status-{success,warning,error,info}-fill`, `-border` | Pozadina bedža ili banera |
| Status tekst | `--status-{success,warning,error,info}-text` | ≥ 4,5:1 na odgovarajućem `-fill` i na `--surface-elevated` |
| Grafikoni | `--chart-series-1..8`, `--chart-axis`, `--chart-grid`, `--chart-tooltip-bg`, `--chart-tooltip-text` | Serije nisu statusne boje; pozitivno/negativno koristi `--chart-positive`/`--chart-negative` |
| Fokus | `--focus-ring` | Vidljiv ≥ 3:1 prema susednoj boji |

Zabranjeno: `color: var(--warning|success|error)` za tekst (koristiti `--status-*-text`); `--dashboard-accent: var(--success)` i slično preusmeravanje statusa u brend.

### 2.3 Tipografija i razmak
- Skala: `--font-size-meta` (11px, samo pomoćne oznake, nikad brojevi), `--font-size-label` (12px), `--font-size-control` (13px desktop / 16px telefon), `--font-size-body` (14,4px), `--font-size-xl` / `--font-size-2xl` (KPI).
- Ključni brojevi (KPI, ćelije sa novcem i procentom) su najmanje 12px i koriste `font-variant-numeric: tabular-nums`.
- Razmak se gradi samo iz `--space-*`; radijusi iz `--radius-*`.

## 3. Raspored ekrana (golden screen)

Redosled je obavezan (proširuje `FRONTEND_UX_STANDARDS.md` "Layout"):

1. Breadcrumb + **jedan** `h1` + jedna rečenica svrhe + najviše jedna primarna akcija.
2. Globalni stale baner (RQ583), samo kada je svežina warning/critical.
3. Trust strip (§5.2).
4. Kontrole (§6.3).
5. KPI red (4–5 kartica).
6. Odluke/signal (tabela sa "Zašto?").
7. Grafikoni.
8. Metodologija · Kvalitet podataka · Export.

Budžet fold-a: na 1280×800 stavke 1–5 su vidljive bez skrolovanja. Na 375px trust strip ima najviše 2 reda, a kontrole su u "Filteri" drawer-u (P-UI-28 obrazac).

## 4. Komponente

| Komponenta | Kanonska implementacija | Pravilo |
|---|---|---|
| Trust strip / header | `AnalyticsTrustHeader` (`compact` podrazumevano na operativnim ekranima) | §5.2 |
| Stale baner | `AnalyticsRefreshStatusBanner` / RQ583 globalni baner | Jedan po stranici, iznad trust strip-a |
| KPI kartica | `ExecutiveKpiRow` / KPI obrazac stranice | Tri stanja vrednosti; "Kako je izračunato?" link na metodologiju |
| Tabela | `AnalyticsDataTable` | Sticky header, numerika desno, prioritet kolona (P-UI-29), empty stanje u tabeli |
| Kontrole | `AnalyticsControlBar` | §6.3 |
| Prazno/greška | `AnalyticsEmptyState`, `AnalyticsErrorState` + `analyticsStateTaxonomy` (P-UI-49) | §7 |
| Disclosure | `button[aria-expanded][aria-controls]` | Detalji dokaza, "Zašto?" po redu |
| Bedž/čip | Status bedž sa ikonom i tekstom | Ton prati status iz backenda; statični bedževi u meniju nisu statusi |
| Pomoć | `InfoTip`, `KpiExplainButton`, `MetricMethodologyPanel` | Poslovni jezik, bez formula sa nazivima kolona |
| Dijalog | `Modal` | Fokus zarobljen, Esc zatvara, povratak fokusa |

## 5. Poverenje i svežina

### 5.1 Izvori
Trust prikaz koristi samo `AnalyticsResponseMeta` i odgovarajuća backend polja: requested/effective/observed period, `dataFreshnessStatus`, readiness, integritet, `recommendationAllowed`, `isPartial`, fallback, provenance.

### 5.2 Trust strip
- Jedan red, najviše 56px na desktopu: `● Spremnost · Period dd.MM–dd.MM.yyyy · Podaci do dd.MM.yyyy · Osveženo dd.MM HH:mm · [Detalji ▸]`.
- "Detalji" (disclosure) sadrži: ID dokaza, kontekst/sha, osnovu generisanja, dataset, fallback razlog, DQ brojače sa objašnjenjem.
- Vremena se prikazuju po Europe/Belgrade u formatu `dd.MM.yyyy HH:mm`. ISO i UTC su samo u "Detalji", sa oznakom "UTC".
- Kada stranica ima `h1`, trust strip nema sopstveni naslov.
- Upozorenja koriste `role="status"` (ili `role="alert"` samo za blokadu koju je korisnik upravo izazvao).

### 5.3 Horizont podataka
Kada je observed horizon pre kraja traženog perioda, strip prikazuje oba podatka ("Period 05.09–04.10 · Podaci do 05.08"), a grafikoni posle horizonta prikazuju "nema podataka", ne 0 (RQ569/RQ570).

## 6. Interakcije

### 6.1 Taksonomija
| Tip | Element | Pravilo |
|---|---|---|
| Navigacija | `Link` | Kanonska ruta, nikad redirect alias u meniju |
| Komanda | `button type="button"` | Labela je glagol + objekat ("Dodaj u akcije") |
| Ops/destruktivna | Samo admin površina | Potvrda sa posledicom; vidljivo samo uz backend capability |
| Disclosure | `button[aria-expanded]` | Klik na red nikad nije jedini put |
| Deep link | `Link` sa period/store/dataScope u URL-u | Odredište poštuje kontekst |
| Export/print | `button` | Nosi trust metapodatke (RQ46) |
| Retry | `button` u error stanju | Uvek prisutno za retryable greške |

### 6.2 Tastatura i fokus
Svaki klikabilan element je fokusabilan i ima vidljiv fokus. `onClick` na `tr/div/span` nije dozvoljen bez `role`, `tabIndex=0` i obrade Enter/Space. Preferira se pravo dugme ili link u ćeliji.

### 6.3 Kontrole i primena filtera (odluka za RQ319/RQ320)
- Period, opseg datuma i višepoljni filteri: draft + **"Primeni"**. Dok draft nije primenjen, kontrola prikazuje oznaku "Nije primenjeno", a trust strip opisuje aktivni (primenjeni) period.
- Tabovi, sortiranje, paginacija i pretraga u već učitanom skupu deluju odmah.
- Datum se uz native input uvek prikazuje i kao `dd.MM.yyyy` (eho), nezavisno od lokala browsera.
- Labele preseta se ne seku: preset select ima minimalnu širinu ili kraći tekst ("30 dana").
- Stanje filtera je u URL-u (deljiv link) na svim decision ekranima, uključujući Dashboard i Izvršni board.
- Primenjena promena filtera dodaje jedan unos u istoriju, pa Back vraća prethodno primenjeno stanje. Kucanje u pretrazi ne dodaje unos.

### 6.4 Hijerarhija akcija
Nivoi: primarna (jedna po ekranu) · sekundarna · tercijarna (export/print) · link (navigacija na DQ/metodologiju) · destruktivna. Retry, export, print i DQ linkovi ne dele isti stil. Workflow akcija ("Dodaj u proveru", "Dodaj u akcije") je vizuelno odvojena od informativnih kontrola. "Zašto?" (obrazloženje reda) i "Kako je izračunato?" (metodologija) imaju različit izgled.

### 6.5 Fokus i landmark
Prvi tab stop je "Preskoči na sadržaj" i vodi na `main`. Redosled: naslov → kontrole → sadržaj, pre bočnog menija.

## 7. Prazna stanja i greške

Mapiranje backend koda na prikaz drži jedna tabela (`utils/analyticsStateTaxonomy.ts`, P-UI-49):

| Kod | Ton | Naslov (primer) | Akcija |
|---|---|---|---|
| `empty_no_data` | neutral | Nema prodaje u izabranom periodu | Proširi period |
| `empty_filtered_out` | neutral | Nema rezultata za filtere | Reset filtera |
| `insufficient_data` | warning | Nema dovoljno podataka za pouzdanu analizu | Kvalitet podataka |
| `beyond_source_horizon` | warning | Podaci postoje do {horizon} | Prikaži period do horizonta |
| `source_dimension_not_populated` | info | {Dimenzija} nije popunjena u izvoru | Kvalitet podataka |
| `source_not_ready` | error | Izvor nije spreman na serveru | Status osvežavanja |
| `blocked_by_readiness` | warning | Preporuka je blokirana: {razlog} | Šta nedostaje |
| `partial` | warning | Prikaz je delimičan | Ponovo učitaj |
| `suppressed` | info | {N} kandidata je potisnuto: {razlog} | Detalji |
| `error_retryable` | error | Podaci trenutno nisu dostupni | Pokušaj ponovo (+ correlation ID sa dugmetom za kopiranje) |
| `backend_unreachable` | error | Server trenutno nije dostupan (moguće buđenje servera) | Pokušaj ponovo |
| `schema_mismatch` | error | Server je vratio neočekivan format podataka | Pokušaj ponovo · prijavi (correlation ID) |
| `loading_slow` | neutral | Učitavanje traje duže nego obično | Pokušaj ponovo · Otkaži |
| `error_permission` | error | Nemate pristup ovom prikazu | — |
| `unknown_code` | neutral | Prikaz nije dostupan | Detalji (sirovi kod) |

Pravilo: UI ne izvodi kod iz broja redova kada backend vrati razlog; kod bez backend potvrde nije dozvoljen. Greška ne prikazuje KPI nule.

## 8. Jezik

| Ne koristiti | Koristiti |
|---|---|
| gated | blokirano |
| N/A | Nije dostupno |
| actionable | za akciju |
| stale | zastarelo |
| deep link | Otvori odluku |
| unverified | nije provereno |
| Insufficient data | Nedovoljno podataka |
| Decision Pulse | Puls odluka (ili ime koje odobri vlasnik) |
| Premium workspace | (ukloniti) |
| P0 / Ops / DQ / Archive / Lab (bedževi) | bez bedža ili opisni tekst |

Sirovi enum-i (`n/a_dedicated`, `warning`, `stale`) se nikad ne prikazuju direktno; prolaze kroz mapu labela.

## 9. Pristupačnost
- Kontrast: tekst ≥ 4,5:1, veliki brojevi i ikone ≥ 3:1, u **svakoj** temi (test u P-UI-47).
- Mete na dodir ≥ 44px na telefonu (P-UI-25).
- `prefers-reduced-motion` gasi ne-esencijalne animacije.
- Grafikoni imaju tekstualni sažetak ili tabelu.
- Jedan `h1`; naslovi sekcija su `h2`/`h3` redom.

## 10. Responsive
Prati P-UI-24 do P-UI-38: širine 320/375/768/1024/1280, bez root overflow-a, tabele sa prioritetom kolona, filteri u drawer-u na telefonu.

## 11. Teme
Šest tema (Tamna, Svetla, Meka siva, Neon Light, Neon Dark, Visok kontrast) moraju da prođu matricu iz audita §5. Nova tema se dodaje samo u `ThemeContext.tsx` i mora da prođe test kontrasta.

## 12. Usklađenost i gate-ovi
- `check-analytics-guardrails` čuva poslovnu istinu.
- P-UI-38 ratchet čuva ovaj dokument: hex/paleta/inline boje, tekst ispod 12px, statusna boja kao tekst, klikabilni ne-semantički elementi. Baseline brojevi smeju samo da padaju.
- Svaki prompt koji menja analitički UI navodi koji deo ovog dokumenta primenjuje.
