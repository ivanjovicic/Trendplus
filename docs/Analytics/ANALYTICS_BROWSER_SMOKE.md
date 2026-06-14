# Analytics Browser Smoke Procedure

Datum: 2026-05-31
Repo: ivanjovicic/Trendplus
Namena: Manual browser-level smoke za production sign-off (nije zamena za automated route smoke).

Napomena:
- Source of truth za automated smoke route listu je `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts`.

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

## Automated Guardrail

```powershell
cd Klijent/clientapp
npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx
```

## Execution Table (rerun 2026-05-31)

| Route | Expected | PASS/FAIL | Notes |
|---|---|---|---|
| /analytics | Analytics dashboard render, bez crash-a | PASS | Ranije potvrđen stabilan load/refresh; bez blank/crash. |
| /analytics/products | Product decision page render, bez crash-a | FAIL | Browser rerun blokiran u ovom okruženju (Playwright install TLS: `UNABLE_TO_VERIFY_LEAF_SIGNATURE`). |
| /analytics/supplier | Supplier consolidated page render, bez crash-a | FAIL | Browser rerun blokiran u ovom okruženju (Playwright install TLS: `UNABLE_TO_VERIFY_LEAF_SIGNATURE`). |
| /analytics/inventory | Inventory analytics page render, bez crash-a | FAIL | Browser rerun blokiran u ovom okruženju (Playwright install TLS: `UNABLE_TO_VERIFY_LEAF_SIGNATURE`). |
| /analytics/data-quality | Data quality page render, bez crash-a | FAIL | Browser rerun blokiran u ovom okruženju (Playwright install TLS: `UNABLE_TO_VERIFY_LEAF_SIGNATURE`). |
| /analytics/actions | Action queue analytics page render, bez crash-a | FAIL | Browser rerun blokiran u ovom okruženju (Playwright install TLS: `UNABLE_TO_VERIFY_LEAF_SIGNATURE`). |
| /analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all | Durable report render; refresh ostaje na istoj stranici | FAIL | Browser rerun blokiran u ovom okruženju (Playwright install TLS: `UNABLE_TO_VERIFY_LEAF_SIGNATURE`). |
| /analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all | Durable report render; refresh ostaje na istoj stranici | FAIL | Browser rerun blokiran u ovom okruženju (Playwright install TLS: `UNABLE_TO_VERIFY_LEAF_SIGNATURE`). |
| /admin/configuration | Configuration page render, bez crash-a | FAIL | Browser rerun blokiran u ovom okruženju (Playwright install TLS: `UNABLE_TO_VERIFY_LEAF_SIGNATURE`). |
| /configuration | Redirect na /admin/configuration i stabilan render | FAIL | Browser rerun blokiran u ovom okruženju (Playwright install TLS: `UNABLE_TO_VERIFY_LEAF_SIGNATURE`). |

## Result Summary

- Environment: Local dev
- Date/time: 2026-05-31 (UTC)
- Executed by: Codex (GPT-5)
- Overall result: FAIL
- Blocking findings: Browser smoke rerun nije mogao da se izvrši u headless režimu zbog TLS/CA blokade pri preuzimanju Playwright paketa.
- Follow-up actions: Izvršiti ručni browser smoke u lokalnom browser-u ili otključati TLS/CA za headless okruženje, pa ažurirati PASS/FAIL tabelu.
