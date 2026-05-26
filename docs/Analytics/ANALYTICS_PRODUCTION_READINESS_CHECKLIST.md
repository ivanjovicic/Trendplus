# Analytics Production Readiness Checklist

Koristi ovaj checklist pre svakog merge-a koji menja analytics backend, frontend, report flow, cache, refresh ili KPI metodologiju.

## 1) Build/Test Gates

Obavezno pokrenuti relevantne komande:

```powershell
# backend
cd C:\Users\Ivan\source\repos\Trendplus2
dotnet build
dotnet test

# frontend analytics guardrails
cd Klijent/clientapp
npm run check:analytics-guardrails
npm run build
```

Ciljani Vitest testovi moraju koristiti non-watch režim sa `--run`:

```powershell
cd Klijent/clientapp
npm run test -- --run src/components/__tests__/WorkersPanel.spec.tsx
npm run test -- --run src/components/analytics/__tests__/MetricMethodologyPanel.spec.tsx
npm run test -- --run src/utils/__tests__/analyticsMetricDefinitions.spec.ts
```

Pass kriterijum:
- Nema compile error-a
- Nema failing testova
- Nema preskakanja obaveznih analytics guardrails provera

## 2) Trust/Data Contract

Proveri da svaka core analytics površina poštuje:
- `AnalyticsResponseMeta` prisutan i smislen
- No fake-zero: greška/failure nikad ne izgleda kao validan `0`
- Jasna razlika između `empty`, `error`, `insufficient_data`
- `AnalyticsTrustHeader` gde je standardom obavezan
- Vidljiv refresh status (`last refresh`, freshness)
- Vidljiv `data quality status`

Pass kriterijum:
- Error stanje koristi error meta + odgovarajući UI state
- Empty stanje koristi empty meta + prazne sekcije, bez lažnih KPI nula
- Insufficient data je eksplicitno označen, ne prikriven fallback

## 3) Reports

Proveri report porodicu i kanonske rute:
- Canonical URL pattern koristi `/api/analytics/reports/*`
- Durable URL (`stableQueryUrl`) radi i posle refresh/invalidation ciklusa
- Legacy fallback signalizuje warning (bez tihe degradacije)
- Expired state je eksplicitno prikazan kada je primenljivo
- Print/export putanja radi ili daje graceful fallback poruku
- Report cache versioning je aktivan
- `stableQueryUrl` je prisutan u payload-u

Pass kriterijum:
- Report ostaje reproducibilan preko stabilnog URL-a
- Fallback/expired nisu skriveni korisniku

## 4) Cache

Potvrdi cache ponašanje u ciljnom okruženju:
- `cache mode` je poznat i očekivan
- Redis/shared cache koristi se u multi-instance production
- In-memory nije tiho ostavljen u produkciji bez warning-a
- Report invalidation pokriva sve refresh/import puteve
- `LastReportCacheClearAtUtc` se ažurira kada se čisti report family

Pass kriterijum:
- Nema stale report ključeva koji preživljavaju nakon validnog refresh-a
- Postoji auditabilan signal poslednjeg clear-a report cache-a

## 5) KPI Methodology

Proveri metodologiju KPI-ja:
- KPI definicije koriste central registry
- "Explain" dugmad postoje gde su predviđena
- Methodology panel je prisutan i čitljiv
- Nema dupliranih formula/definicija po komponentama

Pass kriterijum:
- KPI se može objasniti iz jednog izvora istine
- Nema divergentnih formula između ekrana

## 6) UX i Copy

Proveri korisnički prikaz:
- Srpski copy sa dijakritikom (č, ć, š, ž, đ)
- Nema mojibake (`Ä`, `Å`, `â`, `�`)
- Nema prikaza raw tehničkih error poruka krajnjem korisniku
- CTA standard je konzistentan:
  - Proširi period
  - Otvori kvalitet podataka
  - Ponovo generiši report
  - Pokušaj ponovo

Pass kriterijum:
- Copy je lokalizovan i razumljiv
- Error/empty stanja vode korisnika na sledeći korak

## 7) Manual Smoke Routes

Pre merge-a ručno proveri sledeće rute:
- `/analytics`
- `/analytics/products`
- `/analytics/supplier`
- `/analytics/inventory`
- `/analytics/data-quality`
- `/analytics/actions`
- `/analytics/supplier/report?fromDate=...&toDate=...`
- `/analytics/reports/pilot-intake?fromDate=...&toDate=...`

Pass kriterijum:
- Stranice se učitavaju bez runtime crash-a
- Trust/data quality/refresh informacije su vidljive gde se očekuju

## 8) Known Production Blockers

Ne mergovati ako je prisutno bilo šta od sledećeg:
- Full CI failing
- Redis missing u multi-instance production
- Report cache nije invalidiran iz svih refresh puteva
- Endpoint bez `Meta` contract-a
- KPI bez methodology pokrića

## Final Pre-Merge Sign-off

Minimalni sign-off pre merge-a:
- Build/test gates prošli
- Trust/data contract potvrđen
- Report i cache ponašanje potvrđeno
- UX/copy i manual smoke check završeni
- Nema poznatih production blocker-a
