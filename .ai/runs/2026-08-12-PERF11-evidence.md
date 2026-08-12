# PERF11 Evidence

- Date: 2026-08-12
- Prompt: PERF11 — Capture deferred scalability dimensions (D1/D4-D7)
- Pack: `PERF11-G10-dedicated-deferred-01`
- Milestone / mode: **G10** / **dedicated**
- Dataset: `trendplus_perf_m` (M-PERF-01)
- Raw JSON: `.ai/runs/2026-08-12-PERF11-raw.json`
- Harness: `tmp/perf11_measure.ps1`
- Contract: `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`

## Method

1. **D1:** Warm API, prime bootstrap/sales, then 10 concurrent reads × 2 waves while sampling process RSS; record CPU count, peak RSS, `pg_database_size`, `max_connections`.
2. **D4:** Restart API with `Workers__Enabled=true` (web process; dedicated `PROCESS_TYPE=worker` binds port 8080 and failed health on 8081), POST `AnalyticsAggregationWorker/start`, poll configuration up to 300s for Healthy+heartbeat.
3. D5–D7 marked blocked/deferred with reasons; D8 `n/a_dedicated`.

## Dimension status

| Id | Status | Result |
|---|---|---|
| D1 resource envelope | **measured** | CPU **8** logical; peak RSS **86.53 MB**; DB size **106.39 MB** (0.104 GB); `max_connections` observed; load probe p95 **418.05 ms** |
| D2 concurrent reads | cite_PERF10 | not re-baselined |
| D3 DB pressure | cite_PERF10 | not re-baselined |
| D4 workers | **blocked** | start HTTP **200**, but no Healthy+heartbeat within **303.9 s**; runtime status stayed null/unknown in polls |
| D5 cache footprint | **blocked** | no cardinality/MB instrumentation |
| D6 import overlap | deferred | no M-PERF Access fixture (same as PERF05 B4) |
| D7 export bursts | deferred | out of pack |
| D8 tenant isolation | `n/a_dedicated` | MT-owned |

## Interpretation

1. D1 moves from PERF10 **partial** to **measured** observed dedicated-host envelope. These are **not** reserved multi-customer budgets / SLOs.
2. D4 is an honest **blocked-with-reason** outcome: start accepted, cycle health not proven in 300s on this fixture.
3. D5 is explicitly **blocked** (missing instrumentation), not silently deferred as “later maybe”.
4. Pack does **not** claim G10 pass.

## Files

- `tmp/perf11_measure.ps1`
- `.ai/runs/2026-08-12-PERF11-raw.json`
- `.ai/runs/2026-08-12-PERF11-evidence.md`
