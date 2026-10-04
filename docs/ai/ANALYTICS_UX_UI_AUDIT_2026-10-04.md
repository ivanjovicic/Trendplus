# Trendplus Analytics — duboki UX/UI audit (2026-10-04)

Datum: 2026-10-04 (rad 21:26–22:30 po beogradskom vremenu, CEST)
Rebase: pre registracije rebase-ovano na `origin/main` `44edf98`; ID-jevi promptova deduplicirani sa `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md` (§13).
Osnova koda: `origin/main` `6a2a23b` ("docs(analytics): correct post-registration queue state")
Način rada: čitanje koda (`Klijent/clientapp/src`), stanja queue-a i postojećih audita; live screenshot-i iz paralelnog browser audita (`/workspace/ux-audit/*.png`, desktop, teme Tamna / Meka siva / Svetla / Neon Light / Neon Dark / Visoki kontrast, plus 390×844 za 6 ekrana). Nije menjan runtime kod.
Ciljni design system: `docs/ai/ANALYTICS_DESIGN_SYSTEM.md` (novi kanonski dokument; ranije nije postojao).
Dokazi: `.ai/runs/2026-10-04-ux-ui-audit-evidence.md`

Oznake dokaza:
- **live** — viđeno na screenshot-u browser audita 2026-10-04.
- **live-API** — potvrđeno u read-only re-auditu istog dana (`docs/qa/ANALYTICS_REAUDIT_2026-10-04.md`), ali ne vizuelno.
- **code-derived** — izvedeno iz koda; nije viđeno uživo.

 0. Guardrail (važi za svaki nalaz i prompt)

Frontend ne sme da izmišlja poslovnu istinu. Backend ostaje autoritativan za `recommendationAllowed`, svežinu, confidence, score, periode, DQ i provenance. Svaki UX predlog ovde je **prezentacija postojećih backend polja**: drugačiji raspored, jezik, kontrast, disclosure ili stanje. Kada backend polje ne postoji, predlog je "prikaži nepoznato", nikad "izračunaj u browseru". Ovaj audit ne menja nijedno pravilo preporuke.

 1. Kratak zaključak

Računanje je posle ~170 zatvorenih RQ promptova uglavnom tačno. UX je, međutim, zaostao: korisnik ne vidi najvažniju činjenicu (podaci su stari od 05.08.2026), prvi ekran svakog ekrana troši trust header pun internih oznaka, a odluke su ispod fold-a. Tema sistem ima tri izvora istine i statusne boje koje u svetlim temama nisu čitljive. Globalni header svakom korisniku daje dugmad "Stop/Start" za API ping, workere i Redis.

Ukupna ocena: **4/10** (posle spajanja live nalaza: 50 nalaza). Operativni ekrani (Dobavljači pregled, Vrsta obuće, Dnevna prodaja) su najbolji (6/10). Product Decision, Pre/Post, Decision Pulse i Insight Studio su najslabiji (2–3/10).

# Top 5 problema

1. **UX-001 (P0)** — Zastareli podaci (od 05.08.2026) nisu globalno vidljivi; ekrani otvaraju period "od danas", pa su prazni ili prikazuju lažan pad. Vlasnici: RQ569 (READY), RQ570, RQ583, STAB16.
2. **UX-002 (P1)** — Globalni header svakom korisniku nudi jedan-klik "Stop/Start" za API ping, workere i Redis, bez potvrde. Novi P-UI-48.
3. **UX-004 (P1)** — Trust header zauzima ceo prvi ekran; KPI i odluke su ispod fold-a na Dashboardu, Product Decision, Dobavljačima i Pre-Nivelaciji. Prošireni P-UI-50.
4. **UX-007 (P1)** — Product Decision KPI kartice prikazuju "0" za dopunu/pojačanje/sniženje dok backend blokira preporuke, što se čita kao "nema šta da se radi". Novi P-UI-50 (posle RQ573/RQ574).
5. **UX-011/UX-012 (P1)** — Statusne boje kao tekst u svetlim temama imaju kontrast 1,45–2,54:1. `.card-theme` u svetlim temama prelazi u skoro crnu pozadinu ispod tamnog teksta (1,15:1). Novi P-UI-47.

# Top 5 brzih pobeda

1. **RQ586** — Premestiti `DailySales:TimeZoneId` iz Serilog `WriteTo` niza na pravi nivo (2 JSON fajla + test). Produkcija sada pada na UTC.
2. **P-UI-48** — Sakriti ops toggle-ove iz poslovnog headera i dodati potvrdu (4 komponente).
3. **P-UI-49** — Mapirati postojeće backend reason kodove u shared empty/error stanja (nova tabela, 2 komponente).
4. **P-UI-43** (posle RQ569) — Evidence ID, sha256 i ISO UTC prozore sakriti iza "Detalji dokaza"; zameniti "Preporuka je gated" srpskim tekstom.
5. **P-UI-52** (posle RQ553) — Usaglasiti nazive u meniju sa naslovima ekrana i ukloniti statične "Ready/P0/Ops/DQ/Archive" bedževe.

 2. Analizirani ekrani

18 površina: `/analytics` (Dashboard), `/analytics/pilot-readiness`, `/analytics/decision-board`, `/analytics/products`, `/analytics/supplier` (Pregled/Skorkarta/Asortiman), `/analytics/supplier/report`, `/analytics/shoe-type-sales-stats`, `/analytics/color-sales-stats`, `/analytics/daily-sales`, `/analytics/nivelacije-pre-post`, `/analytics/pre-nivelacija-prioriteti`, `/analytics/inventory`, `/analytics/data-quality`, `/analytics/reports/pilot-intake`, `/analytics/actions`, `/analytics/decision-pulse`, `/analytics/insight-studio`, plus globalni shell (header, sidebar, teme).

Live screenshot-i postoje za Dashboard, Product Decision, Pre-Nivelaciju i Dobavljače u 5 tema (desktop), plus 390×844 (Neon Dark) za te ekrane, Zalihe i Kvalitet podataka. Ostali ekrani su ocenjeni iz koda i re-audit API dokaza.

 3. Scorecard (§47/§65)

Ocena 1–10 je kritična: 10 = spreman za vlasničke odluke bez objašnjavanja; 5 = upotrebljivo uz pomoć; 1 = škodi odluci. Kolone: Jasnoća (C), Bezbednost odluke (S), Konzistentnost sa sistemom (K), Pristupačnost (A), Responsive (R), Tema (T).

| Ekran | Ukupno | C | S | K | A | R | T | Glavni razlog | Dokaz |
|---|---|---|---|---|---|---|---|---|---|
| Globalni shell / meni | 4 | 4 | 3 | 5 | 5 | 6 | 4 | Ops "Stop/Start" u headeru; 5 grupa "Analitika"; interni bedževi | live |
| Dashboard | 4 | 4 | 3 | 6 | 5 | 6 | 5 | Trust header pun ekran; mešani periodi (RQ572) | live + live-API |
| Pilot spremnost | 4 | 5 | 3 | 6 | 5 | 6 | 5 | Brojevi iz aprila pod tekućim naslovom (RQ572); bedž "Ready" | live-API + code |
| Izvršni board | 4 | 5 | 4 | 5 | 4 | 6 | 5 | Nema kontrole perioda/opsega ni URL stanja; 180 dana "od danas" | code + live-API |
| Product Decision | 3 | 3 | 2 | 5 | 4 | 6 | 5 | KPI "0" dok je blokirano; 13 MB/0 primenljivih (RQ573/574) | live + live-API |
| Dobavljači — pregled | 6 | 6 | 6 | 7 | 6 | 5 | 5 | Brojevi tačni; default period prazan; dupli naslovi | live |
| Dobavljači — hub/izveštaj | 3 | 4 | 3 | 6 | 5 | 5 | 5 | `MISSING_OBJECT`; "Poslednjih 30 dana" za jun (RQ580) | live-API |
| Vrsta obuće | 6 | 6 | 5 | 7 | 6 | 5 | 5 | −100% PoP preko horizonta (RQ570) | live-API |
| Boja | 4 | 4 | 4 | 7 | 5 | 6 | 6 | 100% "Nepoznato" (RQ575) | live-API |
| Dnevna prodaja | 6 | 6 | 5 | 7 | 6 | 6 | 6 | UTC fallback (RQ586); "verified" na 0 uparenih (RQ579) | code + live-API |
| Pre/Post nivelacija | 2 | 4 | 2 | 6 | 5 | 4 | 5 | `contract_missing` u produkciji (RQ545/STAB16) | live-API |
| Pre-Nivelacija | 4 | 3 | 3 | 6 | 6 | 4 | 5 | Trust header sa sha256/ISO; period od danas (RQ571) | live |
| Zalihe | 4 | 5 | 2 | 6 | 5 | 6 | 5 | Vrednost ~26 RSD/par; starost od importa (RQ576) | live-API |
| Kvalitet podataka | 4 | 5 | 2 | 6 | 6 | 6 | 6 | "100 / odlično" nad starim podacima (RQ578) | live-API |
| Pilot intake izveštaj | 6 | 6 | 6 | 6 | 5 | 6 | 6 | Ispravan period i brojevi; gust tekst | live-API |
| Centralne akcije | 5 | 5 | 5 | 6 | 6 | 7 | 6 | Smoke zapisi (RQ479); 14× "N/A" | code + live-API |
| Decision Pulse | 3 | 3 | 3 | 2 | 4 | 6 | 3 | Sirovi enum-i, engleski žargon, bez trust headera (RQ481/482) | code + live-API |
| Insight Studio | 2 | 3 | 2 | 1 | 1 | 4 | 2 | 50 hex boja, 0 aria, 58× tekst <12px; stari snapshot-i (RQ582) | code + live-API |
| **Ukupno** | **4** | 4 | 3 | 6 | 5 | 6 | 5 | Svežina, trust header gustina, tema kontrast, ops kontrole | |

 4. Taksonomija interakcija (§5) i audit akcija (§71)

| Tip interakcije | Ciljno pravilo (design system §6) | Trenutno stanje | Nalaz |
|---|---|---|---|
| Navigacija (link) | `<a>`/`Link`, vodi na kanonsku rutu | Meni vodi na redirect alias `/analytics/supplier-decision-hub` | UX-021 |
| Komanda (dugme) | `<button type="button">` sa glagolom u labeli | Uglavnom OK; Pulse/IS koriste kratke engleske labele | UX-006 |
| Destruktivna/ops komanda | Samo admin površina, potvrda, opis posledice | "Stop" workera/API pinga/Redis u globalnom headeru, bez potvrde | UX-002 |
| Promena filtera | Eksplicitno "Primeni" za period/opseg; trenutno za sort/tab | Mešano: Vrsta obuće auto, Boja/Pre-Post eksplicitno (RQ319/RQ320) | UX-018 |
| Otkrivanje detalja (disclosure) | `button` + `aria-expanded` + `aria-controls` | PDC red: `<tr onClick>` + "Zašto?" bez `aria-expanded` | UX-023 |
| Klik na red | Nikad jedini put; uvek i dugme/link u ćeliji | PDC ima "Zašto?" dugme (OK); Insight Studio `tr`/`div` samo mišem | UX-014 |
| Deep link | Prenosi period/store/dataScope | Pulse → Supplier gubi kontekst (RQ482) | UX-015 |
| Export/print | Nosi trust metapodatke | Pokriveno RQ46 (WAITING) | — |
| Retry | U svakom error stanju | Pulse error stanje nema retry | UX-015 |
| Ops status (pasivno) | Pasivni indikator, bez akcije za poslovnog korisnika | Status + akcija spojeni u istom čipu | UX-002 |

**Audit akcija (§71).** Primarne akcije po ekranu su nejasne zato što trust header gura sadržaj ispod fold-a, a u headeru je 9 kontrola (Komande, Obaveštenja, Kontekst, Prikaz, Teme, Osveži, API/Workeri/Redis). Na Product Decision je "Izvoz" vidljivo onemogućen bez objašnjenja (live, Meka siva). "Osveži dashboard" i globalno "Osveži" su dve različite akcije sa sličnim imenom (live).

 5. Matrica usklađenosti tema (§18/§72)

Teme: `inventory-dark` (Tamna), `light` (Svetla), `soft-gray` (Meka siva), `neon-light`, `neon-dark`, `high-contrast`. Izvori tokena: `styles/themes.css` (`:root` i `[data-theme]`), `tailwind.css` `:root` (tamne vrednosti kao default), `context/ThemeContext.tsx` (inline `style.setProperty` na `<html>`), `styles/themeTokens.ts` (`var(--c-*)` koji nisu definisani u `themes.css`).

| Proveravana stavka | Tamna | Svetla | Meka siva | Neon L | Neon D | Visok kontrast | Dokaz |
|---|---|---|---|---|---|---|---|
| Osnovni tekst/pozadina ≥ 4,5:1 | ✅ | ✅ | ✅ (7,07) | ✅ | ✅ | ✅ | code |
| Muted tekst ≥ 4,5:1 | ✅ 4,69 | ⚠️ 4,43 | ✅ | ✅ | ✅ 5,68 | ✅ | code |
| Warning boja kao tekst | ✅ | ❌ 2,15 | ❌ 1,47 | ✅ 4,83 | ✅ | ✅ | code + live (Meka siva header) |
| Success boja kao tekst | ✅ | ❌ 2,54 | ❌ 1,73 | ✅ | ✅ | ✅ | code + live |
| `--error-text` | ✅ | ❌ 1,45 (#fecaca) | ❌ 1,45 | n/a | n/a | n/a | code |
| `.card-theme` gradijent | ✅ | ❌ prelazi u #1f2430 (1,15) | ❌ | ❌ | ✅ | ✅ | code-derived |
| Chart paleta iz tema tokena | ⚠️ page-local `--dashboard-*` | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | code |
| Semantika statusa ≠ brend | ❌ `--dashboard-accent: var(--success)` | ❌ | ❌ | ❌ | ❌ | ❌ | code |
| Bez hardkodovanih hex u stranicama | ❌ Insight Studio 50 hex / 34 inline | isto | isto | isto | isto | isto | code |
| Tailwind paleta umesto tokena | ❌ Pulse `amber-*` | isto | isto | isto | isto | isto | code |
| Focus prsten vidljiv | ✅ (P-UI-25) | ✅ | ⚠️ 0,18 alpha | ✅ | ✅ | ✅ | code |

Zaključak: tamne teme su uglavnom ispravne. Svetle teme (Svetla, Meka siva) imaju sistemski kontrastni problem čim se statusna boja koristi kao boja teksta (50 CSS pravila `color: var(--warning|success|error)`). Vlasnik: P-UI-47 (tokeni + `*-text` statusni tokeni), P-UI-38 (ratchet guard), P-UI-31/35/36 (migracija page-local paleta).

 6. Taksonomija grešaka i praznih stanja (§24–25)

Shared komponente: `AnalyticsEmptyState` (varijante `no_data`, `insufficient_data`, `filtered_out`) i `AnalyticsErrorState` (bez vrste). Backend već vraća razloge koje UI sada prikazuje kao generičko "Nema podataka".

| Stanje (ciljni kod) | Šta korisnik treba da vidi | Izvor istine | Trenutno | Vlasnik |
|---|---|---|---|---|
| `loading` | Skeleton + šta se učitava | UI | OK | — |
| `empty_no_data` | "Nema prodaje u izabranom periodu" | backend count=0 + fresh | OK | — |
| `empty_filtered_out` | "Nema rezultata za filtere" + reset | UI filteri | OK | — |
| `insufficient_data` | Šta nedostaje, bez preporuke | backend readiness | OK | — |
| `beyond_source_horizon` | "Podaci postoje do 05.08.2026" + "Prikaži poslednjih 30 dana podataka" | backend horizon (RQ569/570) | **Live dobar primer:** Boja i Dnevna prodaja već kažu "Izabrani period je van dostupnog raspona prodaje (3. 1. 2011. - 5. 8. 2026.)" + "Prikaži dostupne podatke"; ostali ekrani prazno / −100% | P-UI-49 + RQ570 |
| `source_dimension_not_populated` | "Boja nije popunjena u izvoru" | backend (RQ575) | Prikazuje se kao 100% "Nepoznato" | P-UI-49 + RQ575 |
| `source_not_ready` (`MISSING_OBJECT`/`contract_missing`) | "Izvor nije spreman na serveru; nije problem u vašim filterima" | backend error code | **Live:** izveštaj dobavljača "skup … 180 dana ne postoji" (dobra poruka, ali tehnička); Pre/Post "Podaci trenutno nisu dostupni" | P-UI-49 + RQ545/STAB16 |
| `blocked_by_readiness` | "Preporuka blokirana: <razlog>"; KPI kao "—" | backend `recommendationAllowed=false` | KPI "0" (PDC) | P-UI-50 |
| `partial` | Baner + šta nedostaje | backend `isPartial` | Baner OK; `role="note"` | P-UI-43 |
| `suppressed` | "124 kandidata potisnuto zbog …" | backend `suppressedCount` | `?? 0`, samo kod partial | RQ482 |
| `stale` / `critical_stale` | Globalni baner sa importom i horizontom | backend freshness (RQ583) | Nema globalnog banera | RQ583 |
| `error_retryable` | Poruka + "Pokušaj ponovo" + correlation ID (kopiranje) | backend status | Live: Pre/Post i izveštaj dobavljača imaju retry + correlation ID, ali bez kopiranja; Pulse bez retry-a | RQ482, P-UI-49 |
| `backend_unreachable` | "Server trenutno nije dostupan" + retry | mreža | **Live:** sirovo "Failed to fetch" na Pilot spremnosti, Izvršnom boardu, Pulsu i Akcijama | P-UI-49 |
| `schema_mismatch` | "Server je vratio neočekivan format" + retry + ID | `AnalyticsResponseValidationError` | **Live:** "… response nije u očekivanom formatu" na Dobavljačima i Zalihama | P-UI-49, RQ565 |
| `loading_slow` | Objašnjenje (buđenje servera) + retry/otkaži | UI tajmer | **Live:** izveštaj dobavljača i Insight Studio ostaju u učitavanju | P-UI-49 |
| `error_permission` | "Nemate pristup" | backend 401/403 | Nije razlikovano | P-UI-49 |
| `unknown_code` | Generičko stanje + kod u "Detalji" | — | — | P-UI-49 |

Pravilo: UI mapira samo kodove koje backend vrati; nepoznat kod je `unknown_code`, nikad pogađanje.

 7. Audit vidljivosti kontrola (§70)

| Ekran | Period | Store/opseg | URL stanje | Export | Metodologija | Problem |
|---|---|---|---|---|---|---|
| Dashboard | da (ispod fold-a) | da | delimično | da | da | Kontrole ispod trust headera i ops panela (live) |
| Product Decision | da | da | ručni `URLSearchParams` | onemogućen bez razloga | da (23) | Filteri ispod fold-a (live) |
| Izvršni board | **ne** | **ne** | **ne** | da | ne | Period fiksan na backendu |
| Decision Pulse | **ne** | **ne** | **ne** | ne | ne | RQ481 |
| Dobavljači | da | da | da | da | da | Labela perioda odsečena "Poslednjih 3…"; datum mm/dd (live) |
| Pre-Nivelacija | da | da | da | da | InfoTip (14) | Kontrole ispod 2 reda trust kartica (live) |
| Zalihe | da | da | da | da | da | — |
| Kvalitet podataka | da | da | da | da | da | — |
| Akcije | da | da | sourceType | da | ne | — |
| Insight Studio | ne | ne | ne | da | ne | RQ582 |

 8. Bezbednost odluka (§73)

| ID | Rizik | Ekran | Dokaz | Vlasnik |
|---|---|---|---|---|
| DS-1 | Stari podaci izgledaju kao "nema prodaje" | svi | live + live-API | RQ569/RQ570/RQ583 |
| DS-2 | KPI "0" dok je preporuka blokirana | Product Decision | live | P-UI-50 |
| DS-3 | Ops "Stop" dostupan svakom korisniku | shell | live | P-UI-48 |
| DS-4 | Statičan bedž "Ready" na Pilot spremnosti dok je readiness 67 | meni | live | P-UI-52 |
| DS-5 | "100 / odlično" nad starim podacima | Kvalitet podataka | live-API | RQ578 |
| DS-6 | "Verified" na praznom skupu | Daily/integrity | live-API | RQ579 |
| DS-7 | `recommendationAllowed=false` u `highlightNow` | Pre-Nivelacija | live-API | RQ571 |
| DS-8 | Vrednost zaliha ~26 RSD/par | Zalihe | live-API | RQ576 |
| DS-9 | Pulse `suppressedCount ?? 0` | Pulse | code | RQ482 |
| DS-10 | Status samo bojom (warning nevidljiv u svetlim temama) | svi | code + live | P-UI-47 |
| DS-11 | Smene po UTC umesto Beograda | Dnevna prodaja | code | RQ586 |

 9. Ekran po ekran (§29–36)

# 9.1 Product Decision (§29)
- Live: breadcrumb, trust header i sekcija nose isti naslov ("Odluke o proizvodima" 3×). Trust header zauzima ~70% prvog ekrana. KPI red je ispod fold-a: "Za dopunu 0 · Za pojačanje 0 · Za sniženje 0 · Ne naručivati 0 · Proveriti podatke 0", a pored toga "Procena izgubljene prodaje N/A". "Izvoz" je onemogućen bez razloga. "Redova: 0".
- Code: `ProductDecisionCenterPage.tsx:1843` `<tr onClick>` (postoji "Zašto?" dugme na `:1898`, ali bez `aria-expanded`); 18× `"N/A"`; nema `AnalyticsDataTable`.
- Live-API: 13 MB, 9–10 s, 0 primenljivih, FIX_DATA ispred svega (RQ573), kategorija kao univerzalni blocker (RQ574 READY).
- Cilj: wireframe §11.3. KPI za blokirane akcije prikazuje "—" sa razlogom iz backend readiness polja; brojevi se ne računaju u browseru.

# 9.2 Pre-Nivelacija (§30)
- Live (Tamna): trust kartice prikazuju `pre_nivelacija.evidence`, "Poslednji nalaz: unverified", "ID dokaza: bounded_probe:nivelacija-20261004191021-…", "Kontekst dokaza: sha256:72ac…", "UTC prozor 2026-04-07T19:28:23.9530737Z – …". To su developer podaci (krši FRONTEND_UX_STANDARDS "Language").
- Live-API: period od danas, STARO/Oprema na vrhu, `highlightNow` sa blokiranim redom (RQ571); engleski "Insufficient data" (RQ553 READY).
- Cilj: wireframe §11.4. Readiness + horizon u jednoj liniji, evidence iza "Detalji dokaza".

# 9.3 Zalihe (§31)
- Code: 3× tekst 9–11px; ne koristi `AnalyticsDataTable` (vlastite tabele).
- Live-API: vrednost/starost pogrešne (RQ576); forecast/size-curve/rebalance bez relacije (STAB16).
- Cilj: wireframe §11.5. Vrednost sa oznakom "procena" kada backend kaže fallback; starost "nepoznato" kada nema prijema.

# 9.4 Dobavljači (§32)
- Live: dva naslova "Dobavljači" + breadcrumb "Pregled dobavljača"; labela perioda odsečena ("Poslednjih 3…"); native date input prikazuje `09/05/2026` (lokal browsera) pored "5. 9. 2026." u trust headeru; "Učitavanje pouzdanosti" ostaje u headeru dok se tab učitava.
- Code: page-local paleta `--dashboard-accent: var(--success)` (`SupplierSalesStatsPage.css:1-40`), neon fallback boje.
- Live-API: hub/izveštaj `MISSING_OBJECT` (STAB16/RQ545), pogrešna labela perioda (RQ580 READY).

# 9.5 Dashboard (§33)
- Live: trust header + ops panel ("Poslednji uspešan refresh: Nije zabeležen · Proces: Web proces") pre "Opseg i filteri"; nijedan KPI nije vidljiv na prvom ekranu (krši "Above the fold" standard). Čip "Ignorisani redovi 12.122" bez objašnjenja.
- Live-API: mešani periodi i executive dobavljači sa prometom 0 (RQ572).

# 9.6 Kvalitet podataka (§34)
- Live-API: "100 / odlično", 0 problema (RQ578 READY).
- Cilj: wireframe §11.6. Skor nikad bez svežine; "unknown" nije zeleno.

# 9.7 Akcije / Board / Pulse (§35)
- Akcije: 14× "N/A"; smoke zapisi (RQ479).
- Board: bez kontrole perioda/opsega i bez URL stanja (`ExecutiveDecisionBoardPage.tsx`, nema `<select>`/`<input>`); period 180 dana od danas (live-API).
- Pulse (`DecisionPulsePage.tsx:51-133`): naslov "Decision Pulse", tekst "Stale, prazno i greška nisu alert", "actionable", "deep link", "KPI nule…"; čipovi `svežina: {inputFreshnessStatus}`, `DQ: {dataQualityStatus}`, `{tenantScope}` prikazuju sirove kodove; error stanje bez retry-a; `suppressedCount ?? 0`; Tailwind `amber-*` umesto tokena; ne koristi shared trust/empty/error komponente. Vlasnici RQ481/RQ482 (proširen RQ482).

# 9.8 Insight Studio (§36, poštuje RQ582)
- RQ582 (WAITING posle RQ581) je vlasnik odluke: sakriti iz glavne navigacije i ostaviti iza "Eksperimentalno" flag-a. Ovaj audit **ne** predlaže redizajn. Dodaje samo minimalni prag ako ekran ostane dostupan: shared stale/uncertified baner, tastaturom dostupni klikabilni elementi, bez teksta ispod 12px u ključnim brojevima.
- Code: 50 hex boja, 34 inline stila, 0 `aria-*`/`role`, 58× `text-[9|10|11px]`, 12 sirovih `<table>`, `tr`/`div` sa `onClick` (`InsightStudioPage.tsx:823`, `:1131-1146`).

 10. Katalog nalaza (§66)

Format: `UX-XXX — naslov` · Ozbiljnost · Ekran · Kategorija · Dokaz · Problem · Uticaj · Preporuka · Vlasnik.

# UX-001 — Zastareli podaci nisu vidljivi kao globalno stanje
- Ozbiljnost: P0 · Ekran: svi analitički · Kategorija: decision-safety / freshness
- Dokaz: live (svi screenshot-i: period 5.9–4.10.2026, "Vreme osvežavanja nije dostupno · Nije poznato"); live-API (poslednja prodaja 05.08, import 12.08).
- Problem: korisnik vidi prazan period ili −100% umesto "podaci postoje do 05.08".
- Uticaj: pogrešne odluke ili gubitak poverenja.
- Preporuka: horizon u trust headeru (RQ569), default do horizonta (RQ570), globalni baner (RQ583); vizuelni obrazac je u design system §5.3.
- Vlasnik: RQ569 (READY), RQ570, RQ583, STAB16 — prošireni RQ570/RQ583.

# UX-002 — Ops "Stop/Start" kontrole u globalnom headeru
- Ozbiljnost: P1 · Ekran: shell · Kategorija: action-safety
- Dokaz: live (svi ekrani, ≥1280px: "API ON Stop", "Workeri 0/1 Stop", "Redis: isključen Start"); code `layout/components/HeaderStatus.tsx:450-453,650-653`, `WorkerControlFlag.tsx:63-75` (bez potvrde).
- Problem: jedan klik gasi workere/ping za ceo sistem sa poslovnog ekrana.
- Uticaj: slučajno gašenje osvežavanja; zbunjujući žargon.
- Preporuka: poslovni korisnik vidi samo pasivan status; akcije su na admin/observability površini, uz potvrdu i opis posledice; vidljivost samo kada backend capability to dozvoljava.
- Vlasnik: **novi P-UI-48**.

# UX-003 — Trust header prikazuje interne evidence podatke
- Ozbiljnost: P2 · Ekran: Pre-Nivelacija i Operacije · Kategorija: copy / progressive disclosure
- Dokaz: live (Pre-Nivelacija, sve teme); live sirovi kodovi i na drugim ekranima: `decision_readiness_unavailable` (Zalihe, Pre/Post), `no_shoe_type_sales` (Vrsta obuće), `color_is_supporting_signal` (Boja), `daily_sales.actionability_not_assessed` (Dnevna prodaja); code `AnalyticsTrustHeader.tsx:327-328,371`.
- Problem: sha256, probe ID, ISO UTC sa 7 decimala, engleski `unverified`.
- Preporuka: ljudski sažetak plus "Detalji dokaza" disclosure; datumi kao `dd.MM.yyyy HH:mm` po Beogradu.
- Vlasnik: **prošireni P-UI-50** (posle RQ569).

# UX-004 — Trust header troši prvi ekran; odluke ispod fold-a
- Ozbiljnost: P1 · Ekran: Dashboard, Product Decision, Dobavljači, Pre-Nivelacija · Kategorija: hierarchy
- Dokaz: live (svi 4 ekrana, sve teme, desktop); live 390×844 (Neon Dark): na Product Decision, Kvalitetu podataka, Dashboardu i Dobavljačima trust header zauzima više od celog prvog ekrana, a nijedan KPI ni kontrola nisu vidljivi.
- Problem: krši `FRONTEND_UX_STANDARDS.md` "Above the fold".
- Preporuka: kompaktni trust strip (1 red) sa disclosure-om; budžet visine u design system §5.2.
- Vlasnik: **prošireni P-UI-50**; prošireni RQ572 (Dashboard).

# UX-005 — Trostruki naslovi
- Ozbiljnost: P3 · Ekran: Product Decision, Dobavljači, Pre-Nivelacija · Kategorija: hierarchy
- Dokaz: live.
- Preporuka: jedan `h1` po ekranu; trust header bez sopstvenog naslova kada stranica ima `h1`.
- Vlasnik: P-UI-43.

# UX-006 — Engleski/developer žargon u korisničkom tekstu
- Ozbiljnost: P2 · Ekran: više · Kategorija: copy
- Dokaz: live ("Preporuka je gated", "Premium workspace"); code (Pulse "actionable/Stale/deep link"; 50× `"N/A"`: PDC 18, Akcije 14, SupplierFootwear 11).
- Preporuka: rečnik u design system §8.
- Vlasnik: P-UI-43 (trust header), RQ482 (Pulse), RQ553 (nivelacija), P-UI-50 (PDC), P-UI-52 (meni/Akcije).

# UX-007 — KPI "0" dok je preporuka blokirana
- Ozbiljnost: P1 · Ekran: Product Decision · Kategorija: decision-safety
- Dokaz: live (sve teme: "Preporuka je gated" + KPI 0).
- Problem: "0 za dopunu" se čita kao "ništa ne treba", a zapravo nije dozvoljeno preporučiti.
- Preporuka: kada backend `recommendationAllowed=false`/readiness blokira, KPI akcija prikazuje "—" i razlog; broj se ne izvodi u browseru.
- Vlasnik: **novi P-UI-50** (posle RQ573/RQ574).

# UX-008 — Product Decision payload i FIX_DATA redosled
- P1 · live-API · Vlasnik: RQ573 (WAITING) — prošireni sa UX zahtevom "Prikazano N od M".

# UX-009 — 0 primenljivih preporuka zbog kategorije
- P1 · live-API · Vlasnik: RQ574 (READY).

# UX-010 — Tri izvora tema tokena
- Ozbiljnost: P2 · Kategorija: theme architecture
- Dokaz: code `styles/themes.css`, `tailwind.css:345-389` (`:root` sa tamnim vrednostima) i `:414+` (`[data-theme="light"]`), `context/ThemeContext.tsx:12-180` (inline vars), `styles/themeTokens.ts` (`var(--c-*)` bez definicije u `themes.css`).
- Problem: boja zavisi od redosleda učitavanja/specifičnosti; nova tema mora da se menja na 3 mesta.
- Vlasnik: **novi P-UI-47**.

# UX-011 — Statusne boje kao tekst ispod WCAG 4,5:1 u svetlim temama
- Ozbiljnost: P1 · Kategorija: a11y / theme
- Dokaz: code (warning 2,15:1 na beloj, 1,47:1 na Meka siva pozadini; success 2,54/1,73; `--error-text` #fecaca 1,45; `--muted` 2,54); 50 CSS pravila `color: var(--warning|success|error)`; live (Meka siva: zeleni tekst u headeru jedva čitljiv).
- Preporuka: odvojeni `--status-*-text` tokeni po temi (≥4,5:1) i `--status-*-fill` za površine.
- Vlasnik: **novi P-UI-47**; ratchet u P-UI-38.

# UX-012 — `.card-theme` prelazi u tamnu pozadinu u svetlim temama
- Ozbiljnost: P1 · Kategorija: theme
- Dokaz: code-derived. `ThemeContext.tsx:68-69` postavlja `--surface-elevated-light:#1f2430` i `--surface-elevated-dark:#0f1116` za sve teme; svetle teme ih ne prepisuju; `themes.css:248` gradijent ih koristi; `InventoryPageShell.tsx:21` (Prodaja, Unos robe, Artikli, Nivelacije, Access import, AnalyticsDetailPage).
- Problem: tamni tekst na skoro crnoj pozadini (1,15:1) u desnoj polovini header kartice.
- Vlasnik: **novi P-UI-47** (potvrditi live pre izmene).

# UX-013 — Semantičke statusne boje korišćene kao brend/grafikon boje
- Ozbiljnost: P2 · Dokaz: code (`SupplierSalesStatsPage.css`, `ShoeTypeSalesStatsPage.css`, `ProdajaPrePostNivelacijePage.css`: `--dashboard-accent: var(--success)`, `--dashboard-accent-strong: var(--warning)`; fallback `#66ff7e`, `#8bff00`).
- Problem: promet izgleda kao "uspeh/upozorenje"; boja više ne nosi status.
- Vlasnik: P-UI-47 (chart tokeni) + prošireni P-UI-31/P-UI-35/P-UI-36 (migracija stranica).

# UX-014 — Insight Studio van sistema
- P2 · code + **live** (prihod 0 / marža 0,0% / transakcije 0 pored "No demand signal" i "Unknown product"; engleske labele) · Vlasnik: RQ582 (prošireni sa minimalnim pragom; bez redizajna).

# UX-015 — Decision Pulse: sirovi kodovi, žargon, bez retry-a
- P2 · code + live-API + **live** (sirovo "Failed to fetch" i "KPI nule se ne prikazuju kao validan alert.") · Vlasnik: RQ482 (prošireni), RQ481.

# UX-016 — Shared empty/error stanja ne razlikuju backend razloge
- Ozbiljnost: P1 · Dokaz: code `AnalyticsEmptyState.tsx:21-41`, `AnalyticsErrorState.tsx`; live-API (beyond horizon, `MISSING_OBJECT`, `contract_missing`, 100% Nepoznato).
- Vlasnik: **novi P-UI-49**; RQ570/RQ575 dodaju kodove.

# UX-017 — Izvršni board bez kontrole perioda/opsega i URL stanja
- P2 · code + live-API + **live** (board bez perioda; sirovo "Backend decision board aggregate nije dostupan") · Vlasnik: **novi P-UI-51** (posle RQ570).

# UX-018 — Nekonzistentna semantika primene filtera
- P2 · code + **live** (većina auto-apply; Boja/Dnevna/Pre-Post/Pre-Nivelacija imaju "Primeni filtere", ponekad onemogućeno, bez oznake "nije primenjeno") · Vlasnik: RQ319/RQ320 (prošireni pravilom iz design system §6.3).

# UX-019 — Datum u native inputu prati lokal browsera
- P3 · live (Dobavljači `09/05/2026`) · Vlasnik: **novi P-UI-51** (eho datuma `dd.MM.yyyy` uz input).

# UX-020 — Odsečena labela perioda
- P3 · live ("Poslednjih 3…") · Vlasnik: P-UI-51.

# UX-021 — Informaciona arhitektura menija
- Ozbiljnost: P2 · Dokaz: live + code `layout/navConfig.ts:108-220` (5 grupa sa `label: "Analitika"`, bedževi P0/Ops/DQ/Archive/Ready/Board/Hub/Task/Lab uglavnom u warning tonu; "Prodaja po smeni i dobavljačima" naspram "Dnevna prodaja", "Prioriteti nivelacije" naspram "Prioriteti Pre-Nivelacije", "Pre/Posle" naspram "Pre/Post"; link na redirect `/analytics/supplier-decision-hub`).
- Vlasnik: **novi P-UI-52** (posle RQ553; Insight Studio unos ostaje RQ582).

# UX-022 — Statičan bedž "Ready" na Pilot spremnosti
- P2 · **live** (meni "Ready" naspram tela "Spremno 0 od 8; nepoznato 8" / "Nedovoljno podataka") · decision-safety · Vlasnik: P-UI-52.

# UX-023 — PDC proširenje reda bez ARIA stanja
- P3 · code `ProductDecisionCenterPage.tsx:1843,1898` · Vlasnik: P-UI-50.

# UX-024 — Vrednost i starost zaliha
- P1 · live-API + **live** ("TRENUTNO OOS 11.885" istaknuto dok je akcija blokirana; nule rizika pored "Nije dostupno"; sirovi kodovi `stock_cover_no_velocity`…) · Vlasnik: RQ576 (prošireni wireframe-om §11.5).

# UX-025 — Kvalitet podataka "100 / odlično" i "nema problema"
- P2 · live-API + live (390×844: "Nema otvorenih data quality problema za izabrani filter" odmah iznad čipova "Redovi bez nabavne cene 1.078 · Artikli bez kategorije 12.422 · Nedovoljni signali 12.290"; izvor "Data quality checks" na engleskom) · Vlasnik: RQ578 (READY; prošireni wireframe-om §11.6 i copy napomenom).

# UX-026 — "Verified" na praznom skupu
- P2 · live-API · Vlasnik: RQ579.

# UX-027 — Dashboard meša periode
- P1 · live-API · Vlasnik: RQ572 (prošireni zahtevom za fold).

# UX-028 — Pre-Nivelacija od danas; STARO/Oprema; highlightNow
- P1 · live-API · Vlasnik: RQ571.

# UX-029 — Pre/Post ne radi u produkciji
- P0 · live-API + **live** ("Podaci trenutno nisu dostupni… (correlation: 00-b7332b43…)", "PRIKAZANO 0 / 0", `decision_readiness_unavailable`) · Vlasnik: RQ545 (PARTIAL), STAB16.

# UX-030 — Supplier hub/izveštaj/scorecard bez podataka
- P0 · live-API + **live** (izveštaj: "Skup podataka odluke dobavljača za period poslednjih 180 dana ne postoji…" + correlation ID; drugi pokušaj ostao u učitavanju) · Vlasnik: STAB16/RQ545; labela perioda RQ580 (READY).

# UX-031 — Boja 100% "Nepoznato"
- P2 · live-API · Vlasnik: RQ575.

# UX-032 — Basket metrike na nedokazanom receipt grain-u
- P1 · live-API · Vlasnik: RQ577.

# UX-033 — Smoke akcije u listi
- P2 · live-API · Vlasnik: RQ479.

# UX-034 — Mojibake "NeodreÄ‘eno"
- P2 · live-API · Vlasnik: RQ581 (READY).

# UX-035 — `DailySales:TimeZoneId` je ugnježden u Serilog `WriteTo`
- Ozbiljnost: P1 · Ekran: Dnevna prodaja (smene) · Kategorija: correctness (hotfix nalaz 2026-10-01, i dalje otvoren)
- Dokaz: code `Api/appsettings.Production.json:13-20` i `Api/appsettings.json:127-131`: objekat `{"DailySales": {...}, "Name": "File", ...}` je element niza `Serilog:WriteTo`, pa se ključ čita kao `Serilog:WriteTo:1:DailySales:TimeZoneId`, a aplikacija pada na UTC.
- Vlasnik: **novi RQ586** (repo-local). RQ566 ostaje vlasnik deploy provere.

# UX-036 — Neobjašnjen čip "Ignorisani redovi 12.122"
- P3 · live (Dashboard) · Vlasnik: P-UI-43 (labela i objašnjenje iz backend polja).

# UX-037 — Tamni fallback u chart stilovima
- P3 · code (`SupplierSalesStatsPage.tsx:187-199`, `ShoeTypeSalesStatsPage.tsx:172-184`: `#8ad5a8`, `#0f172a`, `#dbffe8`) · Vlasnik: P-UI-47.

# UX-038 — Upozorenja kao `role="note"`
- P3 · code `AnalyticsTrustHeader.tsx:399-403` · Vlasnik: P-UI-43.

# UX-039 — Nema statičkog guard-a za teme/a11y regresije
- P2 · code (`check-analytics-guardrails.mjs` pokriva poslovnu istinu, ne teme) · Vlasnik: P-UI-38 (prošireni ratchet-om).

# UX-040 — Status RQ560 nekonzistentan (tabela DONE, sekcija PARTIAL)
- P3 · governance · Rešeno u ovom commit-u mehaničkim usklađivanjem sa completion note-om (DONE, `2fe46155`).

# UX-041 — Globalni indikator učitavanja na engleskom prekriva sadržaj
- P3 · live (390×844: "Loading data · 1 request in progress" preko trust headera) · code `components/GlobalRequestSpinner.tsx:26-29` · Vlasnik: P-UI-45 (Codex responsive re-audit; rečnik; "Učitavanje podataka · N zahteva u toku", bez prekrivanja ključnog sadržaja na telefonu).

# UX-042 — Horizontalni overflow na Zalihama pri 390px
- P2 · **live** (`inventory_Neon-Dark_390x844.png`, capture 431px, horizontalni scrollbar) · Vlasnik: **prošireni P-UI-49** (samo CSS).

# UX-043 — Back ne vraća prethodno primenjene filtere; Dashboard ne upisuje datume u URL
- P3 · **live** · Vlasnik: P-UI-51 (pravilo istorije iz design system §6.3).

# UX-044 — Redosled fokusa počinje shell-om; nema skip linka
- P2 · **live** (prvih 15 tab stopova: "Prosiri", "Zatvori", "Pokusaj ponovo", "Skupi meni", pa sidebar) · Vlasnik: P-UI-48 (skip link + `main` landmark).

# UX-045 — Primarne i sekundarne akcije izgledaju isto
- P2 · **live** (retry, export, print, DQ linkovi i "Dodaj u proveru" dele isti pill stil; "Osveži dashboard" pored statusnih linkova) · Vlasnik: P-UI-47 (tokeni nivoa akcija), P-UI-50 (PDC usvajanje).

# UX-046 — Dobavljači: konačna preporuka i pomoćna skorkarta kao ravnopravni tabovi
- P2 · **live** · Vlasnik: P-UI-31 (prošireni).

# UX-047 — Greška validacije šeme prikazana sirovo i blokira ekran
- P1 · **live** (Dobavljači: "Greška pri učitavanju statistike dobavljača response nije u očekivanom formatu."; Zalihe: "…predloga akcije response nije u očekivanom formatu.") · code `validation/analyticsResponseValidation.ts:10` · Vlasnik: P-UI-49 (`schema_mismatch` stanje), uzrok RQ565 (prošireni: cold start vs drift šeme vs nedostajući objekat).

# UX-048 — Nedostaju dijakritici u shell/globalnim tekstovima
- P3 · **live** ("Prosiri", "Pokusaj ponovo", "Osvezi", "Greska pri ucitavanju", "jos nije dostupan", "Pojacaj") · Vlasnik: P-UI-52.

# UX-049 — Beskonačno učitavanje bez objašnjenja
- P2 · **live** (Izveštaj dobavljača ostao na "Učitavam trajni izveštaj dobavljača…"; Insight Studio "Loading data" sa 7 zahteva) · Vlasnik: P-UI-49 (`loading_slow`).

# UX-050 — Mobilni header seče naslov; "Više" nije opisno
- P3 · **live** ("Trendplus pre…", "Odluke o proi…", "Pregled doba…") · Vlasnik: P-UI-48 (premešteno iz nav prompta jer deli header sa P-UI-48).

# Brojevi po ozbiljnosti

| Ozbiljnost | Broj | ID |
|---|---|---|
| P0 | 3 | UX-001, UX-029, UX-030 |
| P1 | 14 | UX-002, UX-004, UX-007, UX-008, UX-009, UX-011, UX-012, UX-016, UX-024, UX-027, UX-028, UX-032, UX-035, UX-047 |
| P2 | 21 | UX-003, UX-006, UX-010, UX-013, UX-014, UX-015, UX-017, UX-018, UX-021, UX-022, UX-025, UX-026, UX-031, UX-033, UX-034, UX-039, UX-042, UX-044, UX-045, UX-046, UX-049 |
| P3 | 12 | UX-005, UX-019, UX-020, UX-023, UX-036, UX-037, UX-038, UX-040, UX-041, UX-043, UX-048, UX-050 |
| **Ukupno** | **50** | 32 sa novim ili proširenim UX vlasnikom; 18 već pokriveno postojećim RQ/STAB vlasnikom. Live potvrđeno: 31 (vidi §16.1; UX-012 ostaje code-derived); ostalo code-derived ili live-API |

 11. Golden screen i wireframe-ovi (§50–51)

# 11.1 Golden screen (anatomija za svaki analitički ekran)

```
┌ Breadcrumb · jedan H1 · 1 rečenica svrhe ─────────────── [Primarna akcija] ┐
├ Trust strip (1 red, ≤ 56px): ● Spremnost · Period 07.07–05.08 · Podaci do 05.08 │
│   · Osveženo 12.08 12:30 · [Detalji ▸]   (boja + ikona + tekst, nikad samo boja) │
├ Globalni stale baner (RQ583) — samo kada je warning/critical, iznad svega ────┤
├ Kontrole: Period ▾ [dd.MM.yyyy–dd.MM.yyyy] · Objekat ▾ · Opseg ▾ · [Primeni] · Reset │
├ KPI red (4–5 kartica): vrednost | "—" + razlog kada je blokirano │
├ Odluke / signal (tabela sa prioritetima, "Zašto?" disclosure po redu) │
├ Grafikoni (podrška odluci, nikad iznad odluka) │
└ Metodologija · Kvalitet podataka · Export (nose trust metapodatke) ──────────┘
```

Pravila: na 1280×800 trust strip, kontrole i KPI red moraju biti iznad fold-a. Evidence ID, sha256, probe ID i ISO vremena su samo u "Detalji". Svaka vrednost koja zavisi od backend readiness polja ima tri stanja: vrednost / "—" + razlog / "nepoznato".

# 11.2 Dashboard

```
H1 Pregled poslovanja                                   [Osveži podatke]
Trust: ● Ograničeno · Period 07.07–05.08.2026 · Podaci do 05.08 · Import 12.08 [Detalji]
[Stale baner: Podaci stari 60 dana — poslednji import 12.08.2026 12:30]
Period ▾ | Objekat ▾ | Opseg ▾ | Primeni
KPI: Promet 1.561.120 RSD | Komadi 307 | Marža 46,5% (pokriće 100%) | Odluke spremne: — (blokirano: svežina)
Top odluke (iz Board-a, max 5, svaka sa razlogom i linkom)
Top dobavljači (isti period kao KPI — RQ572)
Grafikon dnevnog prometa (do horizonta; posle horizonta "nema podataka", ne 0)
Footer: Metodologija · Kvalitet podataka · Export
```

# 11.3 Product Decision

```
H1 Odluke o proizvodima                                  [Export (sa metapodacima)]
Trust: ● Preporuke blokirane: nedostaje svežina · Period … · Podaci do … [Detalji]
Pretraga artikla | Akcija ▾ | Dobavljač ▾ | Objekat ▾ | Primeni
KPI: Dopuna — | Pojačanje — | Sniženje — | Proveri podatke 1.078 (razlog: bez troška)
Tabela (prikazano 500 od 12.422 · sortirano po prioritetu backenda):
 Artikal/SKU · Tip · Dobavljač · Promet/kom · Brzina · Marža (pokriće) · Zalihe · Akcija · [Zašto? ▾]
  ▾ Zašto: razlog backenda · dokazi · alternativa · link na Zalihe/Dobavljača
```

# 11.4 Pre-Nivelacija

```
H1 Prioriteti pre nivelacije                              [Dodaj izabrane u akcije]
Trust: ● Signal (nije preporuka) · Period do horizonta 05.08 · Integritet: nije provereno [Detalji]
Objekat ▾ (Trend PLUS 1/2) | Sezona ▾ | Tip ▾ | Band ▾ | Primeni
KPI: Kandidati 537 | Visok prioritet N | Vrednost zaliha u riziku (procena) | Nedelje pokrića (medijana)
Tabela: SKU · Objekat · Nedelje pokrića · Dana bez prodaje (od horizonta) · Sezona · Band · [Zašto?]
Odvojena lista: Legacy/STARO i Oprema (cleanup, ne nivelacija) — RQ571
```

# 11.5 Zalihe

```
H1 Zalihe i dopuna
Trust: ● … · Vrednost: procena iz prodajnih stavki (RQ576) [Detalji]
Objekat ▾ | Tip ▾ | Dobavljač ▾ | Primeni
KPI: Pari 3.566 | Vrednost (procena) X RSD | Starost: nepoznato za N artikala | Low-stock (jedna definicija)
Tabela: SKU · Veličine · Stanje · Poslednja prodaja · Starost (ili "nepoznato") · Predlog (backend)
```

# 11.6 Kvalitet podataka

```
H1 Kvalitet podataka
Trust: ● Svežina: kritično (import pre 53 dana) · Pokriće troška X% · Kategorija popunjena 0 od 12.422 [Detalji]
KPI: Svežina (status, nikad zeleno kada je unknown) | Bez troška 1.078 | Bez kategorije 12.422 | Otvoreni problemi N
Lista problema: problem · uticaj na ekrane · broj zapisa · akcija (link)
Trend (samo kada postoji istorija; inače empty stanje "nema istorije")
```

# 11.7 Dobavljači

```
H1 Dobavljači                                             [Izveštaj]
Trust: ● … · Period 07.07–05.08 · Podaci do 05.08 [Detalji]
Period ▾ [07.07.2026–05.08.2026] | Objekat ▾ | Opseg ▾ | Dobavljač ▾ | Primeni | Reset
Tabovi: Pregled (glavni) · Skorkarta · Asortiman
KPI: Promet | Komadi | Marža (pokriće) | Dobavljači 15
Tabela/grafikon: chart boje iz `--chart-series-*`, ne `--success/--warning`
```

 12. Unakrsne reference sa ranijim auditima

| Raniji audit | Stanje u queue-u 2026-10-04 | Odnos prema ovom auditu |
|---|---|---|
| Responsive audit RSP-N01–N33 (`docs/ai/RESPONSIVE_UI_AUDIT_PROMPTS_2026-10-01.md`) | Registrovano kao P-UI-24–P-UI-38; DONE 24–30, 32–34, 37; WAITING 31, 35, 36, 38 | Ne dupliram. P-UI-31/35/36 su prošireni zahtevom za tema tokene; P-UI-38 ratchet-om za temu i a11y |
| Negative-ID audit NID-0–6 | Frontend šeme popravljene u `0360442d` (`nullableEntityId`); SQL sentineli RQ560 DONE (`2fe46155`); deploy provera RQ566 WAITING | Nema novih UX nalaza; RQ560 status usklađen |
| Nivelacija audit (`docs/ai/NIVELACIJA_ANALYTICS_AUDIT_PROMPTS_2026-10-01.md`) | RQ537–RQ559 (RQ553 READY, RQ552/RQ555 WAITING, RQ554 DONE) | Copy/a11y za nivelaciju ostaje RQ553; shared trust header je P-UI-43 |
| Supplier audit (cohort/trust UX 2026-09-28) | RQ498–RQ500, RQ530 PARTIAL, RQ580 READY | Ne dupliram; page-local paleta ide u P-UI-31/36 |
| Hotfix 2026-10-01: podaci stali 05.08 | RQ569 READY, RQ570/RQ583 WAITING, STAB16 BLOCKED | UX-001; RQ570/RQ583 prošireni vizuelnim obrascem |
| Hotfix 2026-10-01: TimeZoneId → UTC | Nije bilo repo-local vlasnika (RQ566 samo deploy provera) | **Novi RQ586** |
| Re-audit 2026-10-04 (RQ570–RQ585) | Registrovano | 18 nalaza mapirano na postojeće vlasnike |

 13. Dedupe i promene queue-a (§52–55, §74)

Prvo su prošireni postojeći vlasnici, pa su tek onda dodati novi promptovi za praznine. Svaki novi prompt ima punu metadata (Status, Ready after, Priority, Type, Feature family, Parallel-safe, Owner, Owned/avoid paths, Commit suggestion) i osam sekcija.

**Prošireni postojeći (addendum "UX/UI audit 2026-10-04", bez promene statusa):** RQ319, RQ320, RQ482, RQ565, RQ566, RQ570, RQ572, RQ573, RQ576, RQ578, RQ582, RQ583, P-UI-31, P-UI-35, P-UI-36, P-UI-38, P-UI-41 (live dokaz overflow-a na Zalihama) i P-UI-43 (desktop budžet trust stripa, disclosure dokaza, jedan H1).

**Dedupe sa responsive re-auditom istog dana:** Codex je paralelno registrovao P-UI-39..P-UI-46 iz `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md` (39 overflow `AnalyticsControlBar`, 40 shell za male laptopove, 41 Pilot/Zalihe overflow na telefonu, 42 coarse-pointer tableti, 43 kompaktan trust header na telefonu, 44 široke Operations tabele, 45 globalni chrome/spinner/karusel, 46 šifarnici). Zato su nacrti ovog audita prenumerisani u P-UI-47..P-UI-52. Nacrt "trust strip" je spojen u P-UI-43, a nacrt "Zalihe 390px" u P-UI-41. Stavka spinnera je izbačena iz nav prompta (vlasnik je P-UI-45), a mobilni breadcrumb je prebačen u P-UI-48.

**Novi (porodice §53–54):**

| ID | Status | P | Porodica | Svrha | Ready after |
|---|---|---|---|---|---|
| P-UI-47 | READY | P1 | analytics-theme-token-contract | Jedan izvor tokena, statusni `*-text` tokeni ≥4,5:1, popravka `.card-theme`, chart tokeni, nivoi akcija | — |
| P-UI-48 | WAITING | P1 | global-header-ops-safety | Ops toggle-ovi van poslovnog headera, potvrda, capability gating, skip link, mobilni breadcrumb | P-UI-48 DONE (deli `HeaderStatus.tsx`/`AppLayout.tsx`) |
| P-UI-49 | READY | P2 | analytics-state-taxonomy | Mapiranje backend reason kodova u shared empty/error/loading stanja | — |
| P-UI-50 | WAITING | P2 | product-decision-hierarchy | KPI "—" kada je blokirano, hijerarhija, ARIA, rečnik | RQ573 + RQ574 + P-UI-49 DONE |
| P-UI-51 | WAITING | P2 | decision-surface-controls | Board period/opseg/URL stanje; eho datuma; pravilo istorije | RQ570 + P-UI-47 DONE (deli `AnalyticsControlBar`) |
| P-UI-52 | WAITING | P2 | analytics-nav-ia-copy | Usklađeni nazivi, bez statičnih bedževa, kanonske rute, rečnik (bez spinnera, to je P-UI-52) | RQ553 DONE |
| RQ586 | READY | P1 | daily-sales-timezone-config | `DailySales:TimeZoneId` na pravi nivo konfiguracije + test | — |

Kolizije: P-UI-47 (`ThemeContext.tsx`, `themes.css`, token blok u `tailwind.css` 345-389, `themeTokens.ts`, `analytics-system.css`) i P-UI-49 (`AnalyticsEmptyState*`, `AnalyticsErrorState*`, novi `utils/analyticsStateTaxonomy.ts`) imaju disjunktne putanje međusobno i sa Codex READY P-UI-39/40/41/45. P-UI-48 čeka P-UI-40 (isti header fajlovi), a P-UI-51 čeka P-UI-39 (isti control bar). Nijedan ne dira `AnalyticsTrustHeader*` (RQ569), nivelacija stranice/`navConfig.ts` (RQ553), backend PDC (RQ574), DQ (RQ578), Supplier label helper (RQ580) ni Insight Studio endpointe (RQ581). RQ586 dira samo `Api/appsettings*.json` i jedan test.

 14. Strategija testiranja (§58–62)

1. **Statički (bez browsera):** `check-analytics-guardrails` ostaje vlasnik poslovne istine. P-UI-38 dodaje ratchet za: hex/rgb u `pages/**/*.tsx`, Tailwind paletu (`amber|red|green-NNN`) u analitici, `text-[9|10|11px]`, `color: var(--warning|success|error)` bez `*-text` tokena, `onClick` na `tr/div/span` bez `role`+`tabIndex`. Baseline brojevi iz §5/§9 su početna allowlista; broj sme samo da pada.
2. **Kontrast (unit):** P-UI-47 dodaje Vitest koji za svaku temu iz `ThemeContext` računa WCAG odnos za parove tekst/pozadina i `--status-*-text`/površina (prag 4,5:1; 3:1 za velike brojeve i ikone).
3. **Komponente (Vitest + RTL):** P-UI-49 testira tabelu mapiranja (svaki poznati kod → naslov/poruka/akcija; nepoznat → `unknown_code`). P-UI-43 testira da evidence ID/sha256 nisu u podrazumevanom DOM-u, da je disclosure dostupan tastaturom i da upozorenje ima `role="status"`. P-UI-50 testira da `recommendationAllowed=false` daje "—" + razlog, nikad "0". P-UI-48 testira da poslovna uloga ne vidi akciju i da akcija traži potvrdu.
4. **Browser (postojeći Puppeteer `responsive:baseline`):** proširiti na 6 tema × (1280, 768, 375) za Dashboard, PDC, Pre-Nivelaciju, Dobavljače, Zalihe, DQ; meriti visinu trust strip-a i da li je prvi KPI iznad fold-a na 1280×800.
5. **Fixture pravilo:** testovi koriste fiksne backend odgovore sa `meta` (horizon, readiness, reason code); nijedan test ne računa poslovnu vrednost u frontendu.
6. **Uvek:** `npm run check:analytics-guardrails`, `npm run typecheck`, fokusirani Vitest, tri doc validatora, `git diff --check`.

 15. Rutiranje posle registracije (§75)

- **RQ (viši prioritet):** primarni READY ostaje **RQ569**. Dodatni READY: RQ553, RQ574, RQ578, RQ580, RQ581 i novi **RQ586** (parallel-safe, disjunktne putanje).
- **P-UI (dopunska traka):** primarni READY ostaje Codex **P-UI-39**; dodatni READY P-UI-40, P-UI-41, P-UI-45 i iz ovog audita **P-UI-47** i **P-UI-49**. WAITING iz ovog audita: P-UI-48 (P-UI-40), P-UI-50 (RQ573+RQ574+P-UI-49), P-UI-51 (RQ570+P-UI-39), P-UI-52 (RQ553); P-UI-31/35/36/38 i Codex P-UI-42/43/44/46 kao ranije.
- **Najbolji sledeći prompt:** po lancu prioriteta **RQ569**. Za UX traku iz ovog audita **P-UI-47** (P1 kontrast/tokeni) ili **P-UI-49** (taksonomija stanja, live "Failed to fetch"). Kao brza korektnost pobeda **RQ586**.

 16. Live dokazi i spajanje

Paralelni browser audit je snimio 30 screenshot-a u `/workspace/ux-audit/` (box-local, nisu u repou) i napisao `LIVE_FINDINGS.md`, spojen u §16.1. Nalazi označeni "live" potiču odatle; svi ostali su code-derived ili live-API, kako je označeno.

# 16.1 Spojeni nalazi iz LIVE_FINDINGS.md

Browser audit 21:26–21:40 CEST: 6 tema na desktopu (Tamna, Meka siva, Svetla, Neon Light, Neon Dark, Visoki kontrast) za Dashboard, Product Decision, Dobavljače i Pre-Nivelaciju, plus 390×844 za te ekrane, Zalihe i Kvalitet podataka; 30 screenshot-a. Backend je tokom audita imao cold start, a zatim je postao nedostupan. Neke greške su zato delom okruženje, ali njihov prikaz je UX nalaz.

| Live top-15 zapažanje | Nalaz / vlasnik |
|---|---|
| 1. Backend greške sistemske i nekonzistentne ("Failed to fetch") | UX-047, UX-049, §6 `backend_unreachable` → P-UI-49; RQ565 |
| 2. Zalihe overflow na 390px | UX-042 → P-UI-41 (Codex, live potvrda) |
| 3. Nedostupno kao 0 / 0% / N/A / "Nije dostupno" | UX-007, UX-014, UX-024 → P-UI-50, RQ582, RQ576 |
| 4. Sirovi enum-i u trust UI | UX-003 → P-UI-43 |
| 5. PDC 1.200 redova, svi blokirani, a izgleda primenljivo | UX-007 → P-UI-50 |
| 6. Back ne vraća filtere | UX-043 → P-UI-51 |
| 7. Nekonzistentno "Primeni" | UX-018 → RQ319/RQ320 |
| 8. Svežina ispod hero-a | UX-001, UX-004 → RQ569/RQ583, P-UI-43 |
| 9. Pre-Nivelacija evidence/SHA iznad liste | UX-003 → P-UI-43 |
| 10. Dobavljači: konačni vs pomoćni tab | UX-046 → P-UI-31 |
| 11. Mešan srpski/engleski | UX-006, UX-041, UX-048 → P-UI-43/52, RQ578, RQ482 |
| 12. Primarne/sekundarne akcije slične | UX-045 → P-UI-47/50 |
| 13. Prazno i validna nula se ne razlikuju | UX-007, UX-016 → P-UI-49/50 |
| 14. Fokus počinje shell-om | UX-044 → P-UI-48 |
| 15. Mobilni: seče orijentaciju, kontrole duboko ispod hero-a | UX-004, UX-050 → P-UI-43/48 |

Dodatna potvrđena zapažanja: metodologija drawer na PDC radi i dobar je obrazac (formula, izvor, interpretacija, "Zatvori"), ali prikazuje enum `REPLENISH`. Akcije imaju dobar tekst za prazno stanje ("Prazan uzorak nije greška i ne prikazuje stope kao 0%"). Pilot spremnost lepo kaže "Nepoznato nikad ne znači zeleno". Početna strana `/` (van analitike) meša engleske naslove (`Dashboard`, `Insight Pulse`, `Trend Models`); to je zabeleženo, ali nije registrovano jer nije deo analitike.

 17. Ograničenja

- Screenshot-i su desktop; mobilni i 768px nalazi se oslanjaju na P-UI-24 baseline i kod.
- Kontrast je računat iz token vrednosti, ne iz renderovanih piksela; UX-012 treba potvrditi live pre izmene.
- Nije bilo pristupa bazi ni produkcijskim logovima; poslovni nalazi su preuzeti iz re-audita istog dana.
- Format scorecard-a i nalaza prati brief (§47/§65/§66) kako je prenet ovom agentu.
