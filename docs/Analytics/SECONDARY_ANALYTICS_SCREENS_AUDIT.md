# Secondary Analytics Screens Audit

Datum: 2026-06-15  
Repo: ivanjovicic/Trendplus  
Scope: sekundarni analytics ekrani van glavnih tokova `dashboard`, `data quality`, `product decision`, `supplier hub` i `inventory`

## Auditovani ekrani

- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/pages/SupplierDecisionReportPage.tsx`
- `Klijent/clientapp/src/pages/AnalyticsDetails.tsx`
- `Klijent/clientapp/src/pages/AnalyticsDetailPage.tsx`
- `Klijent/clientapp/src/pages/InsightStudioPage.tsx`

Napomena:
- `Daily Sales` je već posebno auditovan u `docs/Analytics/DAILY_SALES_UX_AUDIT.md`
- `PreNivelacijaPriorityPage` ostaje van ovog taska jer ima zaseban `Q16`

## Sažetak

Sekundarni analytics sloj trenutno se deli u tri grupe:

1. **blizu modernog analytics standarda**  
   - `Shoe Type Sales`
   - `Supplier Sales`
   - `Pre/Post Nivelacije`
   - `Supplier Decision Report`

2. **delimično modernizovani, ali još nekonzistentni**  
   - `Color Sales`

3. **legacy / exploratory / power-tool površine**  
   - `Analytics Details`
   - `Analytics Detail`
   - `Insight Studio`

Glavni UX problem nije samo copy ili layout, nego to što sekundarni ekrani trenutno ne daju uvek jasan signal korisniku:
- da li je ekran core poslovni alat ili exploratory analiza,
- da li podacima može da se veruje,
- i da li se sa tog ekrana očekuje akcija ili samo dodatna interpretacija.

## Brzi rang po clarity riziku

| Screen | Status | Glavni rizik | Prioritet follow-up-a |
|---|---|---|---|
| `AnalyticsDetails` | legacy / high risk | nema moderni trust header, koristi fallback nule i meša legacy + production signal | P1 |
| `AnalyticsDetailPage` | legacy wrapper | subtitle `Tabela / Zapis` je tehnički i slabo objašnjava kontekst detalja | P2 |
| `InsightStudio` | exploratory / high complexity | veoma moćan, ali bez trust/freshness sloja i sa visokim kognitivnim opterećenjem | P1 |
| `ColorSalesStatsPage` | mixed | nema standardizovan trust/error/empty sloj kao srodni analytics ekrani | P1 |
| `SupplierSalesStatsPage` | mostly aligned | route i naziv deluju legacy u odnosu na canonical supplier flow | P2 |
| `ShoeTypeSalesStatsPage` | mostly aligned | dobar standard, ali i dalje sporedni ekran bez jasnog mesta u operator toku | P2 |
| `ProdajaPrePostNivelacijePage` | strong | dobar trust sloj, ali kompleksan copy i signal zahtevaju oprezno dalje poliranje | P2 |
| `SupplierDecisionReportPage` | strong durable report | manje problem UX-a, više discoverability i pozicioniranje | P3 |

## Ekrani koji su blizu standarda

### `ShoeTypeSalesStatsPage`

Dobro:
- koristi `AnalyticsTrustHeader`
- ima `AnalyticsErrorState` i `AnalyticsEmptyState`
- generated timestamp je vidljiv
- quality/trust sloj je prisutan

Rizik:
- i dalje deluje kao sekundarni analitički ekran koji korisnik mora da “zna da postoji”
- nije jasno kada se otvara umesto `Odluke o proizvodima` ili glavnog pregleda

Procena:
- dobar template za sekundarni analytics ekran

### `SupplierSalesStatsPage`

Dobro:
- ima trust header i standardizovan empty/error tok
- generated/freshness i quality signal su vidljivi
- business framing je solidan

Rizik:
- route i labela `supplier-sales-stats` zvuče istorijski/legacy u poređenju sa canonical supplier iskustvom
- može zbuniti korisnika da li je pravi ekran `Pregled dobavljača`, `Odluke o dobavljačima` ili ovaj statistics view

Procena:
- UX je uglavnom dobar, ali positioning u navigaciji ostaje sekundarni problem

### `ProdajaPrePostNivelacijePage`

Dobro:
- trust header, generated at, warning i empty/error state su prisutni
- ekran jasno kaže da nije izolovani profit nego poslovni signal
- quality/trust sloj je eksplicitan

Rizik:
- složenost samog domena znači da ekran i dalje traži visoku pažnju korisnika
- nije veliki clarity problem, ali nije ni “brz” ekran za operatera

Procena:
- jedan od zrelijih sekundarnih analytics ekrana

### `SupplierDecisionReportPage`

Dobro:
- durable report pattern
- empty/error stanja su standardizovana
- više liči na dokument nego na raw analytics surface

Rizik:
- nije glavni problem sam UX dokumenta, već gde i kada korisnik uopšte dolazi do njega

Procena:
- dobar report surface; nizak prioritet za polish u odnosu na druge sekundarne stranice

## Ekrani sa srednjim clarity rizikom

### `ColorSalesStatsPage`

Dobro:
- ima generated timestamp
- ima empty hint i quality notes
- domen je poslovno razumljiv

Problem:
- ne koristi standardni `AnalyticsTrustHeader`
- ne koristi standardni `AnalyticsErrorState` / `AnalyticsEmptyState`
- oslanja se na lokalne message blokove i custom formatting/copy

Zašto je to bitno:
- driftuje od glavnog analytics standarda
- korisnik dobija drugačiji UX obrazac od `shoe type`, `supplier sales` i drugih modernizovanih ekrana

Procena:
- najvredniji kandidat među “sekundarnim statističkim” ekranima za mali standardization polish

## Legacy / exploratory ekrani sa najvećim rizikom

### `AnalyticsDetails`

Problem:
- sam ekran se već označava kao `Legacy prikaz`
- nema `AnalyticsTrustHeader`
- nema standardne analytics error/empty komponente
- deo KPI-ja koristi fallback vrednosti poput `0` ili `N/A` na legacy način
- meša production insight, validation i top list view u jednom zastarelom okviru

Zašto je ovo važno:
- korisnik može završiti na ekranu koji vizuelno i semantički ne prati novi analytics standard
- to slabi poverenje u celu analytics površinu

Procena:
- P1 kandidat za docs/label/polish plan ili eksplicitniji legacy framing

### `AnalyticsDetailPage`

Problem:
- wrapper naslov `Analitika detalj` i subtitle `Tabela / Zapis` su tehnički
- kontekst detalja zavisi od internog table-key modela, ne od poslovnog jezika

Zašto je ovo važno:
- ekran je koristan za drill-down, ali korisnički nije samorazumljiv

Procena:
- nije najveći blocker, ali vredi ga svesti na jasniji business detail frame kada dođe red

### `InsightStudio`

Dobro:
- bogat, moćan i ambiciozan analytics alat
- već ima dosta poslovno korisnih tabova
- sadrži preporučene akcije i širok obuhvat signala

Glavni problemi:
- nema standardni trust/freshness header
- nema jedan jasan data quality / freshness ulaz
- veoma je gust i blizak “power tool” iskustvu
- različiti tabovi imaju različite nivoe zrelosti i objašnjenja

Zašto je ovo bitno:
- ekspertu može biti sjajan, ali pilot korisniku može delovati preširoko i nestrukturirano

Procena:
- P1 po clarity riziku, ne zato što je loš, nego zato što je jak ali kognitivno skup

## Gde sekundarni UX najviše driftuje od glavnog standarda

| Drift area | Gde se vidi | Efekat |
|---|---|---|
| Nema trust header-a | `ColorSalesStatsPage`, `AnalyticsDetails`, `InsightStudio` | korisnik teže vidi freshness, period i pouzdanost |
| Nema standardnog empty/error state-a | `ColorSalesStatsPage`, `AnalyticsDetails`, delovi `InsightStudio` | analytics deluje nekonzistentno između ekrana |
| Legacy ili tehnički framing | `AnalyticsDetails`, `AnalyticsDetailPage` | ekran deluje više kao interni alat nego kao sales-ready analytics |
| Nejasno mesto u navigaciji | `SupplierSalesStatsPage`, `ShoeTypeSalesStatsPage`, `ColorSalesStatsPage` | korisnik ne zna kada da koristi koji sekundarni ekran |
| Visoko kognitivno opterećenje | `InsightStudio` | ekran je moćan, ali skup za brzi operator pregled |

## Najvredniji mali follow-up backlog

### P1

1. Audit/polish plan za `AnalyticsDetails` kao eksplicitno legacy surface.
2. Mali standardization polish za `ColorSalesStatsPage` prema modernom analytics pattern-u.
3. Trust/freshness/data quality framing plan za `InsightStudio`.

### P2

1. Business copy polish za `AnalyticsDetailPage`.
2. Navigaciono i label usklađivanje sekundarnih statistical screens sa canonical analytics mapom.
3. Manji consistency pass preko generated/freshness copy-ja na `ShoeType`, `SupplierSales` i sličnim ekranima.

## Šta ne menjati u follow-up-u

- ne raditi broad refactor više sekundarnih ekrana u jednom commit-u
- ne uklanjati legacy ekran bez replacement plana
- ne dirati metric logiku dok se radi clarity polish
- ne izjednačavati exploratory alate sa operator ekranima samo kroz copy

## Preporučeni sledeći mali task posle ovog audita

Ako se bira jedan sekundarni ekran za mali UX dobitak, najbolji redosled je:

1. `ColorSalesStatsPage` standardization polish
2. `AnalyticsDetails` jasnije legacy framing / cleanup
3. `InsightStudio` trust/freshness clarity pass
