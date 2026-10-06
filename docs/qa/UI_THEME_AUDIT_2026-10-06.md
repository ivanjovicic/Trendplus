# UI/tema audit — 2026-10-06

Polazna tačka: `origin/main` @ `d4ae1b9b`. Ovo je audit sa bezbednim ispravkama, nije redizajn.
Poslovne formule, backend, API ugovori i ponašanje analitike nisu menjani.

## Mapa tokena (jedan izvor istine)

| Sloj | Gde | Uloga |
|---|---|---|
| Runtime vrednosti | `src/context/ThemeContext.tsx` | Kanonske vrednosti za 6 tema (`inventory-dark`, `soft-gray`, `light`, `neon-light`, `neon-dark` — podrazumevana, `high-contrast`); upisuju se inline na `<html>` i pobeđuju stylesheet. |
| Fallback bez JS-a | `src/styles/themes.css` | Ista imena i vrednosti kao ThemeContext (paritet čuva `ThemeContext.tokens.spec.ts`). Jedino mesto koje postavlja `color-scheme`. |
| Tailwind mapiranje | `src/tailwind.css` → `@theme inline` | Samo preslikava semantička imena (`border`, `foreground`, `primary`, `on-primary`, `success`, `warning`, `error`, `danger`, `info`, `accent*`) na runtime tokene. Nije druga paleta. |
| Grafikoni | `src/utils/chartTooltipStyle.ts` | `CHART_AXIS_TICK`, `CHART_GRID_STROKE`, `CHART_LEGEND_STYLE`, `CHART_SERIES_COLORS`, `CHART_METRIC_COLORS` nad `--chart-*` tokenima. |
| Analitika | `src/styles/analytics-system.css` | `--analytics-*` površine/ivice; novo `--analytics-value-glow-color` (sjaj brojki samo u tamnim temama). |

Glavne porodice: `--surface-*`, `--text-*`, `--border-*`, `--accent-*`, `--success/--warning/--error/--info`,
`--status-{success,warning,error,info}-{fill,border,text}`, `--action-*`, `--chart-series-1..8`, `--chart-axis/grid/tooltip-*`.
Pravilo: boja sa poslovnim značenjem ide preko tokena; fiksna boja samo kada je podloga fiksna (štampa, foto-overlay).

## Ispravljeno

1. **Tailwind semantičke klase nisu generisale CSS** (`border-border`, `text-foreground`, `text-success`, `bg-primary`, `border-info/30` …).
   - Uzrok: Tailwind v4 ne čita `tailwind.config.js` (nema `@config`), a `@theme` nije postojao.
   - Posledica: ivice su padale na `currentColor` (jake, svetle linije), statusni tekst bez boje, pune pozadine prozirne — dugmad `bg-info text-white` bila su nevidljiva u svetloj temi.
   - Rešenje: `@theme inline` u `tailwind.css` koji samo pokazuje na postojeće tokene; na punim pozadinama `text-white` → `text-on-primary` (tamni tekst na neon cijan/žutoj, beli na plavoj).
   - Zašto bezbedno: nema nove palete; `muted`/`secondary` namerno nisu mapirani jer postoje ručne klase. Guard test proverava da je svako korišćeno ime mapirano na `var(--…)`.
2. **Statusne boje ispod 4.5:1 u svetlim temama** („Online“, „Sveže“, trend strelice ~2.4:1).
   - Uzrok: `soft-gray` i `light` nasleđivali su tamno-temske `--success/--warning/--error/--info`.
   - Rešenje: tamnije osnovne vrednosti u ThemeContext i themes.css (paritet).
   - Bezbedno: test sada traži ≥4.5 za osnovne statusne boje na obe površine u svih 6 tema.
3. **Sjaj brojki (`text-shadow: currentColor`) razmazivao je KPI vrednosti u svetlim temama.**
   - Rešenje: token `--analytics-value-glow-color` (tamne: `currentColor`, svetle i high-contrast: `transparent`) na 15 mesta u 11 stilskih fajlova.
4. **Dupli izvor tema u `skeleton.css`** (stari `:root`/`[data-theme]` blokovi su prepisivali boje) i lokalni `color-scheme` po stranicama.
   - Rešenje: skeleton zadržava samo senke/radijus; `color-scheme` postavlja samo themes.css (date picker prati temu).
5. **Nedefinisane CSS promenljive** (`--surface-hover`, `--foreground`, `--on-foreground`, `--font-size-2xs`, `--color-foreground`, `--color-hover`, `--radius-full`, `--accent-warning-rgb`, `--text-primary-dark`, `--c-*` …).
   - Posledica: prozirne pozadine, nasleđen tekst, KPI labela je dobijala različitu veličinu po stranici.
   - Rešenje: mapiranje na kanonske tokene (`--row-hover`, `--text-primary`, `--action-*`, `--font-size-label`, `--radius-pill` …).
   - Guard test: nijedan `var(--x)` bez fallback-a ne sme da čita nedeklarisanu promenljivu.
6. **Legacy aliasi** (`--panel`, `--panel-border`, `--row-hover`, `--bg`, `--surface*`, `--danger`, `--primary-600`) sada pokazuju na kanonske tokene umesto na tvrde tamne vrednosti.
7. **Analytics dashboard:** uklonjen lokalni `--warning: var(--error)` (upozorenja su izgledala kao greške); prazna/greška stanja koriste `--status-*` tokene.
8. **Grafikoni:** jedna paleta po poslovnom značenju — prihod = serija 1, dokumenti = 2, komadi = 3, prosek prihoda = 4 (Dashboard, Dnevna prodaja, Detalji). Ose, mreža, legenda i tooltip iz zajedničkih konstanti. Semantika serija nepromenjena; smene na Dnevnoj prodaji ostale kako su bile.
9. **Kontrast i čitljivost:**
   - labele u filter traci: `--text-muted` → `--text-secondary`;
   - dugme za izvoz u tabelama: bilo 1.0:1, sada `bg-primary` + `--text-on-primary`;
   - `.button-big` i AccessImport dugmad: tekst uparen sa pozadinom;
   - Daily Sales/Pre-Posle/Pre-nivelacija statusni bedževi i trake na `--status-*` tokenima;
   - obojeni čipovi u filter traci (Opseg, Prikazano, Fokus): labela `--text-primary` jer tonirana pozadina spušta kontrast;
   - bedž „Odloženo“ (Centralne akcije) i bedževi P0/Ops u meniju: `--status-warning-fill/text` umesto tvrde narandžaste i 10% tinte;
   - svetle teme: `--warning` je `#92400e` (ranije `#b45309`, ~4.3:1 na toniranoj pozadini);
   - aktivni tab na Dobavljačima: `--accent-text` (ranije fiksna plava, 3.4:1 u svetloj temi); info poruka na Boji artikla na `--status-info-*`.
10. **Curenje globalnih klasa:** `.btn`, `.btn-primary`, `.text-muted` … iz `ConfigurationPage.css` i `NivelacijaRepairPage.css` menjali su izgled dugmadi u celoj aplikaciji posle posete tim stranicama. Rešenje: pravila su ograničena na koren stranice.
11. **Štampa analitike:** papir je uvek beo, tekst taman, bez obzira na temu ekrana (ranije svetao tekst u tamnoj temi). Dugmad „Štampaj“ (ranije „Print“) i „Zatvori“ koriste `--action-*`; naslov „Metadata“ → „Metapodaci“.

12. **Ćirilica na osi grafikona Dnevne prodaje** („140 хиљ.“): kompaktni format brojeva koristio je `sr-RS`; sada `sr-Latn-RS` („140 hilj.“, „1,5 mil.“). Ostali formati brojeva su nepromenjeni.

## Namerno ostavljeno (sa razlogom)

- `AnalyticsTrustHeader*` — P-UI-43 je IN_PROGRESS; nije dirano.
- ~1700 obrazaca `var(--theme-color-xxx, #xxx)` — u praksi tvrde boje (promenljiva nikad nije definisana). To su većinom statične dekorativne nijanse; masovna zamena bila bi rizičan redizajn. Kandidat za P-UI-38 (CSS higijena).
- Statične boje (kategorija C): štampa (beli papir), dugme za zatvaranje karusela (uvek na beloj podlozi preko fotografije), `.btn-warning` (žuta + crna, kontrast visok u svim temama).
- `tailwind.config.js` — mrtva konfiguracija (v4 je ne učitava). Nije obrisana da ne bi zbunila druge alate; `@theme inline` je izvor.
- `secondary` Tailwind boja ostaje ručna pomoćna klasa. Follow-up provera je pokazala da `muted` opacity varijante (`bg-muted/20`, `border-muted/30`) ipak koriste stvarne komponente, pa je `muted` naknadno mapiran na `--text-muted` i pokriven guard testom.
- `inventory-dark` (legacy): `text-on-primary` (beli) na svetlim statusnim pozadinama ima <4.5:1. Jedna boja ne može da služi i primarnom dugmetu i svetlim statusnim pozadinama; test to eksplicitno izuzima.
- Pristupačnost grafikona za čitače ekrana — P-UI-53 (WAITING), nije rađeno.

## Za buduće uglađivanje

- Zameniti `var(--theme-color-*, #…)` tokenima u trend/marketplace stranicama (Scraper, eBay, Deichmann, AboutYou).
- Ukloniti duple deklaracije (`--text`, `--surface-muted`, `--info-10`) u `:root` bloku themes.css.
- Ujednačiti radijuse dugmadi (8/12/16px mešavina) kroz `--radius-*`.
- Tekst bez dijakritika u porukama i naslovima (npr. „Greska pri ucitavanju …“ u `analyticsApi.ts`, „Prodaja po nacinu placanja“ na kontrolnoj tabli) — pripada P-UI-52 (sweep naziva/rečnika), ovde nije menjano.
- `inventory-dark`: linkovi na `--accent-primary` (#2563eb) imaju ~3.5:1 na tamnoj pozadini; prelazak na `--accent-text` za linkove.
- `text-primary` (12 linkova) u `inventory-dark` ima 3.5:1 — razmotriti `--accent-text`.

## Preklapanja sa redom zadataka

- P-UI-47 (DONE) — ovaj rad ga proširuje, ne pravi paralelni sistem.
- P-UI-38 (WAITING) — guard testovi za nedefinisane promenljive i Tailwind mapiranje su mali deo „CSS higijene“; ostatak (`--theme-color-*`) ostaje njemu.
- P-UI-43 (IN_PROGRESS) — TrustHeader nije diran.
- P-UI-44 (READY) — Daily Sales: menjane su samo boje statusa, ne raspored tabele ni sticky kolone.
- P-UI-52 (READY) — bedževi P0/Ops u meniju: promenjena samo boja (statusni tokeni), ne tekst ni logika.
- P-UI-45 (READY) — `imagecarousel.css`: promenjena je samo boja X dugmeta.
- P-UI-53 (WAITING) — nije rađeno.

## Merenje u pregledaču (fixture, 16 ruta)

Broj tekstova ispod 4.5:1 na 1280px (sonda nad `responsive_baseline` fixture-ima), pre → posle:

| Tema | Pre | Posle |
|---|---|---|
| light | 100 | 3 |
| soft-gray | 71 | 4 |
| neon-dark (podrazumevana) | 37 | 1 |
| neon-light | — | 4 |
| high-contrast | — | 1 |
| inventory-dark (legacy) | — | ≤54 (plavi linkovi `--accent-primary` na tamnoj pozadini; vidi „Namerno ostavljeno“) |

Preostalo: × na toast poruci, strelica ▼ na bedžu kvaliteta (3.9–4.3), poruka „Nema kritičnih … signala“ na zelenoj tinti u soft-gray (4.13), labela KPI kartice na toniranoj pozadini (3.7–4.4).
Napomena: sonda meri i elemente sa `transition` tokom promene teme, pa pojedinačni nalazi mogu biti lažno pozitivni.

Responsive strict (`responsive_baseline.mjs --mode fixture --strict --viewport-only`): light, neon-dark i soft-gray × 320/360/390/768/1024/1280/1800 — svih 21 kombinacija PASS, root overflow 0, page errors 0.
Tokom rada nađen je i ispravljen **postojeći** overflow Dnevne prodaje na 320px (bedž „⚠ 3 problema u podacima“ širio je stranicu na 326px; isto i na `d4ae1b9b`): zaglavlje panela sada prelama red.

## Validacija

Videti izveštaj predaje (vitest, guardrails, typecheck, build, encoding, `git diff --check`, responsive strict po širinama).


## Follow-up verifikacija posle predaje

Naknadna nezavisna provera `cc57cace` našla je dva mala propusta koja CI nije vizuelno mogao da uhvati:

- Inventory analitički paneli su na theme-aware `--surface-elevated` površinama još imali fiksni `text-white`, što je bilo pogrešno u svetlim temama. Naslovi, KPI vrednosti i nazivi artikala sada koriste `--text-primary`; eksplicitni beli tekst ostaje samo tamo gde je podloga namerno fiksna/gradijentna.
- `muted` Tailwind opacity varijante su bile navedene kao namerno nemapirane, iako ih koriste skeleton/track elementi. `--color-muted` je zato povezan sa `--text-muted`, a guard sada proverava i `muted` semantičke klase.
- Neutralne `bg-white` površine u SKU detalju i Open Training tabu prebačene su na theme-aware surface tokene. Dodata je ciljana regresiona provera da se fiksna bela boja ne vrati u te panele.

Ovo ne menja poslovnu logiku, API ugovore, analytics formule niti semantiku podataka.
