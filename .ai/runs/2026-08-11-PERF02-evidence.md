# PERF02 evidence log

Prompt: PERF02 - Execute the S-tier baseline measurement pack and capture evidence
Date: 2026-08-11
Status: DONE

## Environment

- commit: `4caacff077d1c0df9f3d6539fbf38e36a49b386c`
- machine: `DESKTOP-V877DAD`
- os: `Microsoft Windows 11 Pro` (`10.0.22000`)
- cpu: `Intel(R) Core(TM) i7-6700 CPU @ 3.40GHz`
- ramGiB: `15.86`
- dotnetSdk: `10.0.201`
- postgresVersion: `psql (PostgreSQL) 18.3`
- database: `trendplus_test`
- port: `8080`
- datasetTier: `S`
- seedRecipeId: `Infrastructure/Seed/TrendplusDbSeeder.cs via startup initializer`
- prewarmEnabled: `false`
- cacheState: mixed
- processState: mixed
- period: `2026-05-13T22:02:41.3467238Z` -> `2026-08-11T22:02:41.3487253Z`

## Dataset

- `Artikli`: 15
- `Dobavljaci`: 1
- `Sezone`: 1
- `ProdajaZaglavlja`: 10
- `ProdajaStavke`: 26
- `OutboxMessages`: 5
- `DnevnikPromena`: 0
- `TipoviObuce`: 0

## Commands

- `powershell -ExecutionPolicy Bypass -File tmp/perf02_measure.ps1`
- `psql -h 127.0.0.1 -p 5432 -U postgres -d trendplus_test -v ON_ERROR_STOP=1 -f $env:TEMP\perf02_005_utf8.sql`
- `psql -h 127.0.0.1 -p 5432 -U postgres -d trendplus_test -v ON_ERROR_STOP=1 -f $env:TEMP\perf02_004_utf8.sql`

Note: `004_SimpleTestData.sql` completed the inserts and then hit a final top-level `RAISE NOTICE` syntax error. The inserted rows remained committed, so the seeded dataset is valid for measurement.

## Measurement Pack

### B8 - cold start first useful analytics response

Request:

- `GET /api/analytics/cached/dashboard/bootstrap?fromDate=2026-05-13T22:02:41.3467238Z&toDate=2026-08-11T22:02:41.3487253Z&dataScope=all`

Cold process, cold cache samples from process start to first analytics 200:

- 10491.6253 ms
- 55394.9412 ms
- 41716.5423 ms
- 12283.9023 ms
- 12009.1855 ms

Summary:

- p50: 12283.9023 ms
- p95: 55394.9412 ms
- min: 10491.6253 ms
- max: 55394.9412 ms

Correctness:

- `meta.success=true`
- `meta.isPartial=true`
- `meta.warningCode=ANALYTICS_PARTIAL_DATA`
- `meta.dataQualityStatus=good`
- `summary.totalRevenue=245200.0000`
- `inventory.totalSkuCount=15`

### B2 - dashboard bootstrap warm cache

Same request as above, after one priming request in a warm process.

- first warm-process/cold-cache response: 1060.18 ms
- warm samples: 20
- warm p50: 31.67 ms
- warm p95: 35.45 ms

Correctness:

- `meta.success=true`
- `summary.totalRevenue>0`
- `inventory.totalSkuCount>0`

### B1 - sales summary and inventory status

Sales summary request:

- `GET /api/analytics/cached/sales/summary?fromDate=2026-05-13T22:02:41.3467238Z&toDate=2026-08-11T22:02:41.3487253Z`

Inventory request:

- `GET /api/analytics/cached/inventory/status?lowStockThreshold=2`

Sales summary:

- cold-process/cold-cache first response: 624.61 ms
- warm samples: 20
- warm p50: 29.84 ms
- warm p95: 39.05 ms
- correctness: `meta.success=true`, `totalRevenue>0`, `totalTransactions>0`

Inventory status:

- cold-process/cold-cache first response: 144.92 ms
- warm samples: 20
- warm p50: 29.47 ms
- warm p95: 33.07 ms
- correctness: `meta.success=true`, `totalSkuCount>0`, `totalOnHand>=0`

## Raw Evidence

- `.ai/runs/2026-08-11-PERF02-raw.json`

## Notes

- The baseline pack is honest about partial data: dashboard bootstrap is `success=true` but `isPartial=true`.
- No optimization claims were made.
- PERF03 can now be planned from measured facts, not assumptions.
