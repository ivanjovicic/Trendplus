# Analytics Production Readiness Checklist

Koristi ovaj dokument kao finalni pre-merge checklist za svaku analytics promenu. Važi za backend endpoint-e, frontend analytics ekrane, durable reports, cache, worker/refresh tokove i KPI methodology rollout.

Routing i smoke standard (obavezna dopuna ovog checklist-a):
- `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md`

Smoke reference izvori:
- `docs/Analytics/ANALYTICS_BROWSER_SMOKE.md`
- `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts`

Evidence-based readiness status:
- `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`

Ako makar jedna stavka iz odeljka Production blockers padne, promena nije spremna za merge.

## 1) Build/Test Gates

Obavezne komande:

```powershell
cd C:\Users\Ivan\source\repos\Trendplus2
dotnet build
dotnet test

cd Klijent/clientapp
npm run check:analytics-guardrails
npm run build
```

Za Vitest koristi isključivo non-watch režim sa `--run`:

```powershell
cd Klijent/clientapp
npm run test -- --run <putanja-do-spec-fajla>
```

Pravila:
- Ne ostavljati terminal u watch modu.
- Ako build ili test padne, ne merge-ovati dok uzrok nije jasan.
- Ako komanda ne može da se pokrene zbog okruženja, to mora biti eksplicitno navedeno u PR opisu.

Pass kriterijum:
- `dotnet build` prolazi.
- `dotnet test` prolazi.
- `npm run check:analytics-guardrails` prolazi.
- `npm run build` prolazi.
- Ciljani Vitest testovi, kada postoje, pokrenuti su sa `--run` bez watch moda.

## 2) Trust/Data Contract

Svaki core analytics response mora provereno da ispunjava:
- Svaki core response ima `AnalyticsResponseMeta` ili kompatibilan meta contract kada shape istorijski mora da ostane stabilan.
- Backend greška nikad ne izgleda kao validan `0`, `0 RSD`, `0 kom` ili `0%`.
- `empty`, `error` i `insufficient_data` su jasno razdvojeni u backend contract-u i UI prikazu.
- `correlationId` postoji gde je to moguće i ne sme nestati u error flow-u.
- `AnalyticsTrustHeader` prikazuje period, refresh/freshness, source i `dataQualityStatus` gde je standardom obavezan.

Pass kriterijum:
- Error stanje koristi error meta i `AnalyticsErrorState`, bez fake-zero KPI kartica.
- Empty stanje koristi empty meta i `AnalyticsEmptyState`, bez maskiranja problema kao greške.
- Insufficient data koristi jasan signal, warning ili `AnalyticsEmptyState variant="insufficient_data"` kada nema dovoljno pouzdanih podataka.
- Trust header korisniku jasno kaže koji period gleda, koliko su podaci sveži i koliko su pouzdani.

## 3) Durable Reports

Obavezno proveriti durable report porodicu:
- `/api/analytics/reports/supplier-decision`
- `/api/analytics/reports/pilot-intake`

Checklist:
- URL radi i posle browser refresh-a.
- `stableQueryUrl` je user-facing UI URL, ne interni tehnički link.
- Legacy browser preview ima jasan warning kada se koristi fallback ili preview režim.
- Expired state je jasan i korisniku objašnjava šta da uradi sledeće.
- Print i Excel opcije postoje samo kada payload zaista postoji.
- PDF je sakriven ili graceful fallback postoji ako PDF izlaz nije dovoljno pouzdan.
- Report payload i UI eksplicitno komuniciraju durable/stable identitet kada je dostupan.

Pass kriterijum:
- Durable report može da se otvori direktnim URL-om i posle refresh-a bez raspada stanja.
- `stableQueryUrl` ostaje stabilan kroz normalan refresh/invalidation ciklus.
- Legacy preview i expired stanja nisu skriveni ili tihi.
- Korisnik nikad ne vidi export CTA za payload koji ne postoji.

## 4) Cache

Pre merge-a proveriti report i analytics cache ponašanje:
- Cache status endpoint je dostupan na canonical ruti `/api/analytics/cache/status`.
- Legacy ruta `/api/analytics/cached/cache/status` je i dalje aktivna zbog backward compatibility.
- `cacheMode` je vidljiv i razumljiv.
- Redis/shared naspram in-memory status je vidljiv u administraciji ili status payload-u.
- In-memory cache u production okruženju ima warning ako je deployment multi-instance ili deli load.
- `reportCacheVersion` postoji i menja se kada je očekivano.
- `LastReportCacheClearAtUtc` postoji i ažurira se pri čišćenju report family cache-a.
- Report invalidation se dešava posle import-a i posle refresh-a.
- Admin clear cache čisti celu reports family, ne samo uski podskup ključeva.

Pass kriterijum:
- Nema stale report odgovora posle validnog import/refresh događaja.
- Canonical i legacy cache status rute vraćaju isti payload shape.
- `cacheMode`, cache backend i warning signal su vidljivi i ne traže čitanje koda da bi bili razumljivi.
- Admin clear cache za reports family zaista invalidira durable report payload-e.

## 5) KPI Methodology

Pre merge-a proveriti methodology rollout:
- Postoji centralni registry za KPI definicije.
- Canonical metric keys su definisani i koriste se dosledno.
- Alias map postoji za legacy nazive i mapira ih na canonical ključeve.
- Explain button postoji na KPI karticama gde je ta metrika korisniku važna za odluku.
- Methodology panel postoji u reportima gde se prikazuju KPI kartice ili durable methodology sekcije.
- Nema dupliranih formula po komponentama, reportima ili ad-hoc helperima.

Pass kriterijum:
- Jedna poslovna metrika ima jedan primarni izvor definicije.
- UI ne izmišlja formulu lokalno kada registry već postoji.
- Report methodology ne kontradiktuje KPI karticama na glavnim ekranima.

## 6) UX

Pre merge-a proveriti UX i copy:
- Srpski tekst koristi dijakritiku.
- Nema skrivenih encoding problema u korisnički vidljivom copy-u.
- Korisniku se ne prikazuje raw SQL, DTO, MV ili interni exception tekst.
- CTA standard koristi sledeće formulacije gde su primenljive:
  - Proširi period
  - Otvori kvalitet podataka
  - Ponovo generiši report
  - Pokušaj ponovo
  - Otvori status osvežavanja

Pass kriterijum:
- Copy je razumljiv poslovnom korisniku bez developera pored sebe.
- Error, empty i warning stanja daju sledeći korak, a ne tehnički dump.
- UI ne otkriva interne nazive tabela, DTO-a, MV-eva ili stack trace detalje.

## 7) Manual Smoke Rute

Detaljna browser-level procedura i tabela za evidenciju rezultata:
- `docs/Analytics/ANALYTICS_BROWSER_SMOKE.md`

Automated smoke route source of truth:
- `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts`

Ručno proveriti sledeće rute pre merge-a:
- `/analytics`
- `/analytics/products`
- `/analytics/supplier`
- `/analytics/inventory`
- `/analytics/data-quality`
- `/analytics/actions`
- `/analytics/supplier/report?fromDate=...&toDate=...`
- `/analytics/reports/pilot-intake?fromDate=...&toDate=...`
- `/analytics/product-decision-center` (legacy redirect na `/analytics/products`)
- `/analytics/data-quality/pilot-intake-report` (legacy route)
- `/admin/configuration`
- `/configuration` (redirect na `/admin/configuration`)

Preporučeni automated guardrail (non-watch):

```powershell
cd Klijent/clientapp
npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx
```

Napomena:
- Ovaj route smoke je automated/code-level verifikacija (ukljucujuci lazy route resolution) i nije zamena za manual browser smoke.

Pass kriterijum:
- Rute se učitavaju bez runtime crash-a.
- Period, refresh i data quality informacije su vidljive gde se očekuju.
- Report rute rade i pri direktnom otvaranju URL-a ili browser refresh-u.
- Manual browser sign-off se evidentira kroz tabelu u `docs/Analytics/ANALYTICS_BROWSER_SMOKE.md`.

## 8) Production Blockers

Ne merge-ovati ako postoji bilo šta od sledećeg:
- Frontend build failing.
- `dotnet build` failing.
- Fake-zero endpoint.
- Report cache nije invalidiran.
- In-memory cache radi u multi-instance production bez warning-a.
- Durable report URL ne radi posle refresh-a.
- KPI bez metodologije na glavnom ekranu.
- Mojibake u UI.

## 9) PR Checklist

Svaki PR za analytics promenu treba da navede:
- Šta je promenjeno.
- Koje komande su pokrenute.
- Koji manual smoke je odrađen.
- Koji rizici ostaju.

Pre merge-a proveriti da PR opis sadrži barem:
- Kratak opis scope-a.
- Rezultate build/test komandi.
- Manual smoke coverage po rutama ili jasno naveden uzorak.
- Preostale rizike, fallback-e ili stvari koje nisu mogle da se verifikuju.

Dodatne obavezne stavke za routing/smoke PR:
- App.tsx i dalje koristi lazy/Suspense (nema masovnog prelaza na direktne import-e radi testa).
- Nema dupliranih ruta (`/settings/themes`, `/analytics/*`, `/admin/*`, `/configuration`).
- Legacy/admin rute imaju replacement + redirect kada se menjaju.
- `ThemeProvider defaultTheme` nije promenjen slucajno.
- Route smoke test prolazi: `npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx`.

## Final Pre-Merge Sign-off

Minimalni sign-off pre merge-a:
- Build/test gates su prošli.
- Trust/data contract je potvrđen.
- Durable reports i cache ponašanje su provereni.
- KPI methodology rollout je provereno pokriven.
- UX/copy i manual smoke su završeni.
- Nema production blocker-a.
