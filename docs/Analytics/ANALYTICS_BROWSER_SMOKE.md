# Analytics Browser Smoke Procedure

Datum: 2026-05-26
Repo: ivanjovicic/Trendplus
Namena: Manual browser-level smoke za production sign-off (nije zamena za automated route smoke).

Napomena:
- Source of truth za automated smoke route listu je Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts.

## Scope

Obavezne rute:
- /analytics
- /analytics/products
- /analytics/supplier
- /analytics/inventory
- /analytics/data-quality
- /analytics/actions
- /analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all
- /analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all
- /admin/configuration
- /configuration

## Prerequisites

- Frontend aplikacija je pokrenuta lokalno (ili target environment za sign-off).
- Browser console je otvoren (F12) za proveru runtime grešaka.
- Poželjno je imati fallback dataset, ali test ne sme zavisiti od specifične baze.

## Automated Guardrail (complementary)

Code-level route mapping proveri pre manual smoke:

```powershell
cd Klijent/clientapp
npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx
```

Napomena:
- Ovaj test proverava route mapping/lazy route resolution.
- Ne proverava realno browser renderovanje i UX ponašanje posle refresh-a.

## Pass Criteria

Za svaku rutu važi:
- Stranica se učita bez blank screen-a.
- Nema runtime crash-a (nema uncaught error overlay-a).
- Ako backend nema podatke, prikazan je user-friendly empty/error state (bez fake-zero KPI prikaza).
- TrustHeader/refresh/data quality informacije su vidljive gde se očekuju na analytics ekranima.
- Nema mojibake-a u glavnim naslovima i ključnim tekstovima.

Dodatno za durable report rute:
- Browser refresh (`Ctrl+R`) ostaje na istoj report stranici i URL-u.
- Expired state (ako se pojavi) ne prikazuje export dugmad bez payload-a.

## Procedure

1. Otvori svaku rutu iz scope liste direktno u browser address bar-u.
2. Potvrdi da nema blank screen-a niti runtime crash-a.
3. Za analytics rute proveri da su TrustHeader i refresh/data quality info vidljivi.
4. Za durable report rute uradi refresh i potvrdi da stranica ostaje stabilna.
5. Ako se prikaže expired state, potvrdi da export dugmad nisu dostupna bez payload-a.
6. Vizuelno proveri glavne naslove za mojibake (`Ä`, `Å`, `â`, `�`).
7. Zabeleži PASS/FAIL i kratku napomenu po ruti.

## Execution Table

| Route | Expected | PASS/FAIL | Notes |
|---|---|---|---|
| /analytics | Analytics dashboard render, bez crash-a, trust info vidljiv | PASS | Direktan URL i refresh stabilni; TrustHeader i data-quality info vidljivi; nema blank/crash/mojibake. |
| /analytics/products | Product decision page render, bez crash-a | FAIL | Ruta često ostaje na "Učitavanje..." bez stabilnog sadržaja nakon refresh-a; UX nije stabilan za sign-off. |
| /analytics/supplier | Supplier consolidated page render, bez crash-a | FAIL | Detektovan blank/empty prikaz u delu run-a i nestabilan render između direktnog otvaranja i refresh-a. |
| /analytics/inventory | Inventory analytics page render, bez crash-a | FAIL | Stranica u više prolaza ostaje na loading stanju (bez pouzdanog kompletnog prikaza ekrana). |
| /analytics/data-quality | Data quality page render, bez crash-a | FAIL | Ruta se učitava, ali je prikaz nestabilan; primećeni su 503/API fail tragovi i nejasan loading-heavy UX za pilot sign-off. |
| /analytics/actions | Action queue analytics page render, bez crash-a | FAIL | Nije crash, ali render nije konzistentan (učitavanje/partial state) i nije dovoljno stabilan za PASS. |
| /analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all | Durable report render; refresh ostaje na istoj stranici; expired bez export dugmadi bez payload-a | FAIL | URL ostaje stabilan posle refresh-a, ali stranica ostaje u "Učitavam..." stanju bez pouzdanog report rendera/payload-a. |
| /analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all | Durable report render; refresh ostaje na istoj stranici; expired bez export dugmadi bez payload-a | FAIL | URL stabilan, ali ostaje loading/partial prikaz; nije potvrđen stabilan report prikaz za browser sign-off. |
| /admin/configuration | Configuration page render, bez crash-a | FAIL | Povremeno ostaje na "Učitavanje..." i beleži API request fail tragove (nepouzdan prikaz tokom smoke-a). |
| /configuration | Redirect na /admin/configuration i stabilan render | FAIL | Redirect radi na /admin/configuration, ali ciljni render nije stabilan (loading/fail tragovi). |

## Result Summary (fill after run)

- Environment: Local dev (frontend `http://localhost:5173`, backend `http://localhost:8080`)
- Browser: VS Code integrated browser (Playwright-driven manual smoke)
- Date/time: 2026-05-27 (UTC)
- Executed by: GitHub Copilot (GPT-5.3-Codex)
- Overall result: FAIL
- Blocking findings: Više core ruta ne daje stabilan finalni prikaz (loading/blank/partial state), uz API 503/fetch failure tragove tokom smoke sesije; durable report rute nisu stabilno renderovane.
- Follow-up actions: Otvoriti poseban fix task za stabilizaciju ruta označenih kao FAIL, zatim ponoviti isti browser smoke i tek tada razmatrati pilot sign-off.
