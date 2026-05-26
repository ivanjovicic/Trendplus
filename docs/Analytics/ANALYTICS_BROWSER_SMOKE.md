# Analytics Browser Smoke Procedure

Datum: 2026-05-26
Repo: ivanjovicic/Trendplus
Namena: Manual browser-level smoke za production sign-off (nije zamena za automated route smoke).

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
- Browser console je otvoren (F12) za proveru runtime gresaka.
- Pozeljno je imati fallback dataset, ali test ne sme zavisiti od specificne baze.

## Automated Guardrail (complementary)

Code-level route mapping proveri pre manual smoke:

```powershell
cd Klijent/clientapp
npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx
```

Napomena:
- Ovaj test proverava route mapping/lazy route resolution.
- Ne proverava realno browser renderovanje i UX ponasanje posle refresh-a.

## Pass Criteria

Za svaku rutu vazi:
- Stranica se ucita bez blank screen-a.
- Nema runtime crash-a (nema uncaught error overlay-a).
- Ako backend nema podatke, prikazan je user-friendly empty/error state (bez fake-zero KPI prikaza).
- TrustHeader/refresh/data quality informacije su vidljive gde se ocekuju na analytics ekranima.
- Nema mojibake-a u glavnim naslovima i kljucnim tekstovima.

Dodatno za durable report rute:
- Browser refresh (`Ctrl+R`) ostaje na istoj report stranici i URL-u.
- Expired state (ako se pojavi) ne prikazuje export dugmad bez payload-a.

## Procedure

1. Otvori svaku rutu iz scope liste direktno u browser address bar-u.
2. Potvrdi da nema blank screen-a niti runtime crash-a.
3. Za analytics rute proveri da su TrustHeader i refresh/data quality info vidljivi.
4. Za durable report rute uradi refresh i potvrdi da stranica ostaje stabilna.
5. Ako se prikaze expired state, potvrdi da export dugmad nisu dostupna bez payload-a.
6. Vizuelno proveri glavne naslove za mojibake (`Ä`, `Å`, `â`, `�`).
7. Zabelezi PASS/FAIL i kratku napomenu po ruti.

## Execution Table

| Route | Expected | PASS/FAIL | Notes |
|---|---|---|---|
| /analytics | Analytics dashboard render, bez crash-a, trust info vidljiv |  |  |
| /analytics/products | Product decision page render, bez crash-a |  |  |
| /analytics/supplier | Supplier consolidated page render, bez crash-a |  |  |
| /analytics/inventory | Inventory analytics page render, bez crash-a |  |  |
| /analytics/data-quality | Data quality page render, bez crash-a |  |  |
| /analytics/actions | Action queue analytics page render, bez crash-a |  |  |
| /analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all | Durable report render; refresh ostaje na istoj stranici; expired bez export dugmadi bez payload-a |  |  |
| /analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all | Durable report render; refresh ostaje na istoj stranici; expired bez export dugmadi bez payload-a |  |  |
| /admin/configuration | Configuration page render, bez crash-a |  |  |
| /configuration | Redirect na /admin/configuration i stabilan render |  |  |

## Result Summary (fill after run)

- Environment:
- Browser:
- Date/time:
- Executed by:
- Overall result: PASS / FAIL
- Blocking findings:
- Follow-up actions:
