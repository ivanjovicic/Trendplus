# Analytics Navigation Audit

Datum: 2026-06-14  
Repo: ivanjovicic/Trendplus  
Scope: analytics route registration, sidebar navigation, analytics landing izlazi i legacy/compatibility signal.

## Sažetak

Analytics ima dobar funkcionalni coverage, ali navigacija trenutno meša:
- canonical analytics rute koje su važne za operatera,
- legacy ili redirect entry pointe,
- tehničke ili exploratory ekrane,
- i mešavinu srpskih i engleskih labela.

Najveći UX problem nije manjak ruta, nego to što korisnik ne dobija jasan odgovor na pitanje:
- kada treba da otvori `Pregled analitike`,
- kada `Odluke o proizvodima`,
- kada `Pregled dobavljača`,
- kada `Zalihe i dopuna`,
- kada `Kvalitet podataka`,
- i gde se nalazi `Centralne akcije`.

## Auditovani izvori

- `Klijent/clientapp/src/App.tsx`
- `Klijent/clientapp/src/layout/navConfig.ts`
- `Klijent/clientapp/src/layout/components/Sidebar.tsx`
- `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts`
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`
- `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md`

## Canonical analytics mapa

Ovo su rute koje već izgledaju kao core operator flow:

| Ruta | Canonical label | Namena |
|---|---|---|
| `/analytics` | Trendplus pregled | ulazni pregled stanja, trust/freshness i prioritetni signali |
| `/analytics/products` | Odluke o proizvodima | šta uraditi sa artiklima |
| `/analytics/supplier` | Pregled dobavljača | gde dobavljač zaslužuje fokus |
| `/analytics/inventory` | Zalihe i dopuna | lager, OOS, spora zaliha i replenishment odluke |
| `/analytics/data-quality` | Kvalitet podataka | da li su podaci dovoljno pouzdani za odluke |
| `/analytics/actions` | Centralne akcije | radna lista i status akcija |
| `/analytics/supplier/report?...` | Izveštaj dobavljača | durable report / dokument |
| `/analytics/reports/pilot-intake?...` | Pilot intake izveštaj | intake/readiness dokument |

## Verovatni operator tokovi

### 1. Nedeljni komercijalni pregled

1. `Trendplus pregled`
2. `Odluke o proizvodima`
3. `Pregled dobavljača`
4. `Centralne akcije`

### 2. Posle importa ili refresh incidenta

1. `Kvalitet podataka`
2. `Pilot intake izveštaj`
3. povratak na `Trendplus pregled`
4. tek onda `Odluke o proizvodima` ili `Pregled dobavljača`

### 3. Operativni lager tok

1. `Trendplus pregled`
2. `Zalihe i dopuna`
3. `Centralne akcije`

## Glavne confusion tačke

| Area | Trenutno stanje | Zašto zbunjuje | Prioritet | Safe follow-up |
|---|---|---|---|---|
| Sidebar nema `/analytics/products` | core ruta postoji, ali nije first-class item u sidebaru | korisnik ne vidi direktno gde su odluke o artiklima; oslanja se na dashboard deep link | P1 | dodati sidebar stavku `Odluke o proizvodima` |
| Sidebar nema `/analytics/supplier` | canonical supplier pregled postoji, ali sidebar vodi na `supplier-sales-stats` redirect i `supplier-decision-hub` | nije jasno da li je pravi ekran `Pregled dobavljača`, `Prodaja po dobavljačima` ili `Odluke o dobavljačima` | P1 | dodati sidebar stavku `Pregled dobavljača`; legacy/exploratory ostaviti kao sekundarne |
| Sidebar nema `/analytics/actions` | akcioni ekran postoji kao core ruta, ali nema stalni ulaz u navigaciji | operator ne dobija jasan “gde su moje akcije ove nedelje?” odgovor | P1 | dodati sidebar stavku `Centralne akcije` |
| `Data quality` label je na engleskom | sidebar koristi `Data quality`, route definitions koriste `Kvalitet podataka`, page header koristi `Provera kvaliteta podataka` | meša poslovni srpski i tehnički engleski jezik | P1 | preimenovati sidebar label u `Kvalitet podataka` |
| `/analytics/inventory` je nazvan `Bilans stanja` | canonical smisao ekrana je širi: zalihe, dopuna, OOS i slow stock odluke | `Bilans stanja` zvuči kao statičan pregled, ne kao decision screen | P1 | preimenovati u `Zalihe i dopuna` ili `Zalihe i odluke` |
| `/analytics` label nije dosledan | route definitions kažu `Trendplus pregled`, sidebar kaže `Pregled analitike` | korisnik ne zna da li su to različiti nivoi ili isti ekran | P2 | izabrati jedan canonical naziv i koristiti ga svuda |
| Dashboard CTA copy je mešan | na istom ekranu postoje `Supplier pregled`, `Product decisions`, `Inventory`, `Data Quality` | landing signal deluje nedovršeno i otežava mentalni model | P2 | uskladiti dashboard CTA copy sa canonical sidebar labelama |
| Exploratory/legacy analytics linkovi su pomešani sa core flow-om | `Prodaja po dobavljačima`, `Dobavljači i tipovi obuće`, `Insight Studio`, `Detaljne analize` stoje u istoj grupi kao core decision ekran | operator teže razlikuje dnevni rad od dodatnih analiza | P2 | grupisati ih kao `Dodatne analize` ili ih vizuelno spustiti ispod core flow-a |

## Legacy i compatibility signal

Legacy/compatibility rute trenutno imaju legitimnu svrhu i ne treba ih uklanjati u ovom tasku:

- `/analytics/product-decision-center` -> redirect na `/analytics/products`
- `/analytics/data-quality/pilot-intake-report` -> legacy alias za pilot intake report
- `/analytics/supplier-sales-stats`
- `/analytics/dobavljaci-tipovi-obuce`
- `/analytics/supplier-decision-hub`

Problem nije njihovo postojanje, nego to što su neke od tih ruta trenutno vidljivije u navigaciji nego canonical operator ekrani.

## Preporučeni minimalni polish backlog

### P1 — prvi mali UX commitovi

1. Dodati sidebar stavku za `/analytics/products` sa labelom `Odluke o proizvodima`.
2. Dodati sidebar stavku za `/analytics/supplier` sa labelom `Pregled dobavljača`.
3. Dodati sidebar stavku za `/analytics/actions` sa labelom `Centralne akcije`.
4. Preimenovati `Data quality` u `Kvalitet podataka`.
5. Preimenovati `Bilans stanja` u `Zalihe i dopuna`.

### P2 — posle toga

1. Uskladiti naziv `/analytics` između sidebar-a, route definition-a i page copy-ja.
2. Uskladiti dashboard CTA copy sa canonical labelama.
3. Vizuelno odvojiti exploratory/legacy analytics ekrane od core operator flow-a.

## Šta ne menjati u follow-up-u

- ne uklanjati compatibility rute bez redirect plana
- ne dirati lazy routing pattern
- ne raditi broad reorganizaciju sidebar-a u istom commit-u
- ne uvoditi novu IA bez malih, proverljivih koraka

## Preporuka za sledeći UX redosled

Ako se radi mali frontend polish posle ovog audita, redosled treba da bude:

1. sidebar canonical entry points
2. rename nejasnih labela
3. dashboard CTA usklađivanje
4. tek onda dublje reorganizacije dodatnih analytics ekrana
