# RQ664 local Supplier/Shoe browser proof

Date: 2026-10-14
Task: RQ664
Evidence class: `synthetic_fixture_only`

## What this proves

`npm run build` followed by `npm run qa:rq448-fixture-smoke` starts the built client behind a disposable local static server, launches the installed Puppeteer Chromium, intercepts the Supplier and Shoe Type analytics responses with the explicitly marked fixture `Klijent/clientapp/scripts/qa/rq448-fixture.json`, and drives the real routes and `AnalyticsTableToolbar`.

The run proved:

- `/analytics/supplier` and `/analytics/shoe-type-sales-stats` rendered the same `240 RSD` fixture revenue and two fixture rows;
- requested period `2026-09-01` to the half-open `2026-09-02`, store `2` and `imported` scope stayed visible;
- unknown attribution and negative IDs stayed visible in both tables;
- both routes sent the rendered row set to the CSV generator and produced a real downloaded CSV with the route columns and two rows;
- an empty/insufficient response rendered an empty/degraded state, a `503` response rendered an error state, and a deliberately tampered total was detected as a mismatch;
- missing browser/build/fixture/artifact remains fail-closed rather than a passing skip.

Machine-readable verdict: [RQ664_LOCAL_BROWSER_FIXTURE_VERDICT_2026-10-14.json](RQ664_LOCAL_BROWSER_FIXTURE_VERDICT_2026-10-14.json).

## Commands and boundaries

```text
cd Klijent/clientapp
npm ci --no-audit --no-fund
npm run build
npm run qa:rq448-fixture-smoke
```

The harness only intercepts local synthetic responses and export documents. It does not connect to PostgreSQL, production providers, a deployed build, a customer account or a protected credential. The fixture is bound to the existing RQ447/RQ446 contract vocabulary (`SST-ACCURACY-1.0`) but it does not re-execute the RQ447 oracle and does not claim source lineage.

## What RQ448 still needs

RQ448 remains `WAITING`. This rehearsal does not supply:

1. an authenticated staging/production browser and API session;
2. raw source-line reconciliation against the deployed API response;
3. real deployed browser screenshots and downloads bound to an app/deployed SHA;
4. approved read-only source/database evidence and the corresponding detail view;
5. the production freshness, schema and customer acceptance evidence owned by RQ448/RQ451/RQ452/RQ454/RQ455/RQ565.

No RQ448, RQ451 or RQ452 status is changed by this local proof.
