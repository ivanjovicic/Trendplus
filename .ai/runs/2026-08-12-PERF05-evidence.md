# PERF05 evidence log

Prompt: PERF05 - Execute M-tier baseline measurement pack and capture evidence  
Date: 2026-08-12  
Status: DONE

## Environment

- commit: `77919b4689c9a1f9ff0cb1842a872bf9d3818e1b`
- machine: `DESKTOP-V877DAD`
- os: `Microsoft Windows 11 Pro` (`10.0.22000`)
- dotnetSdk: `10.0.201`
- postgresVersion: `psql (PostgreSQL) 18.3`
- database: `trendplus_perf_m`
- port: `8080`
- datasetTier: `M`
- seedRecipeId: `M-PERF-01` (`Database/Perf/M-PERF-01_seed.sql`)
- prewarmEnabled: `false`
- workersEnabled: `false`
- period: `2026-02-13T06:00:00Z` → `2026-08-12T06:00:00Z`

## Dataset counts (post-seed)

| Entity | Count |
|---|---:|
| Artikli (M-PERF) | 12,000 |
| ProdajaZaglavlja | 45,000 |
| ProdajaStavke | 180,000 |
| Dobavljaci (M-PERF) | 8 |

## Commands

- `powershell -ExecutionPolicy Bypass -File tmp/perf05_setup_db.ps1`
- `powershell -ExecutionPolicy Bypass -File tmp/perf05_measure.ps1 -SkipSetup`

## Measurement pack

### B8 — cold process / cold cache (dashboard bootstrap)

| Sample | ms | status |
|---:|---:|---:|
| 1 | 5065.27 | 200 |
| 2 | 6700.00 | 200 |
| 3 | 4476.18 | 200 |
| 4 | 4955.84 | 200 |
| 5 | 4884.16 | 200 |

Summary: p50 **4955.84 ms**, p95 **6373.05 ms** (target cold p95 &lt; 5 s — **borderline breach** on M-tier)

Note: JSON meta fields were not parsed in harness (response casing); HTTP 200 recorded for all samples.

### B2/B8 — dashboard bootstrap warm path

- cold-cache first hit: **5108.31 ms**
- warm p50: **45.19 ms**, warm p95: **52.13 ms** (N=20)

### B1 — sales summary

- cold-cache first hit: **26.68 ms**
- warm p50: **23.83 ms**, warm p95: **25.95 ms**

### B1 — inventory status

- cold-cache first hit: **24.76 ms**
- warm p50: **22.47 ms**, warm p95: **24.49 ms**

### B2 — decision board

- cold-cache first hit: **288.89 ms**
- warm p50: **109.45 ms**, warm p95: **126.26 ms**

### B2 — product decision center

- cold-cache first hit: **47.31 ms**
- warm p50: **3.18 ms**, warm p95: **38.67 ms**

### B2 — supplier decision-hub ranking

- **blocked by HTTP 429** (rate limiter) on first request; subsequent warm samples ~3 ms but not valid for correctness co-assertion
- recorded as measurement gap, not a performance win

### B5 — workers configuration

- `GET /api/workers/configuration`: **54.23 ms**, status 200
- full aggregation cycle not executed (`Workers__Enabled=false`)

### B4 — import preview

- **skipped** — no M-PERF `.accdb` fixture in repo

### B7 — frontend routes

- **deferred** — API surrogate timings recorded; Playwright harness not run in this pack

## Raw evidence

- `.ai/runs/2026-08-12-PERF05-raw.json`

## Notes

- M-tier cold-start p95 (~6.4 s) is **much lower** than S-tier p95 (55.4 s) on this machine — likely reflects `--no-build` startup, warm OS cache, and different partial/bootstrap path; do not treat as cross-tier regression fix.
- Warm B1 paths remain fast at M-tier; index work still deferred per PERF-MONITOR-01.
- Supplier ranking needs rate-limit bypass or backoff in harness before optimization claims.

## Next

- PERF06: investigate/profile PERF-COLD-01 with M-tier evidence and execution plans
- Add import fixture + frontend harness for remaining PERF-MEASURE-01 gaps
