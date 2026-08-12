# PERF10 Evidence

- Date: 2026-08-12
- Prompt: PERF10 — Capture first scalability-gate evidence pack (PERF-9)
- Pack: `PERF10-G10-dedicated-01`
- Milestone / mode: **G10** / **dedicated**
- Dataset: `trendplus_perf_m` (M-PERF-01)
- Raw JSON: `.ai/runs/2026-08-12-PERF10-raw.json`
- Harness: `tmp/perf10_measure.ps1`
- Contract: `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`

## Method

Warm API process, prewarm disabled, workers disabled. Cache primed with bootstrap + sales summary, then **10 concurrent** analytics reads × **3 waves** (30 samples) alternating `dashboard.bootstrap` and `sales.summary`. Postgres `pg_stat_activity` snapshotted during waves for D3.

Cold/warm matrix: **warm-process × warm-cache** (explicit; not cold-start).

## Dimension status

| Id | Status | Result |
|---|---|---|
| D1 resource envelope | **partial** | API RSS before/after ~89 → ~46 MB; CPU/disk/budgets unmeasured |
| D2 concurrent reads | **measured** | N=30, p50 **243.59 ms**, p95 **468.58 ms**, errorRate **0**, timeoutRate **0** |
| D3 DB connection pressure | **measured** | peak active **1**, peak total **4**, `max_connections=100`, waiting **0** |
| D4 workers | deferred | workers off |
| D5 cache footprint | unmeasured | no instrumentation |
| D6 import overlap | deferred | out of pack |
| D7 export bursts | deferred | out of pack |
| D8 tenant isolation | `n/a_dedicated` | shared_saas needs MT |

## Priming markers (not concurrency)

| Step | ms | status |
|---|---:|---:|
| Bootstrap first (cold cache) | 9075.79 | 200 |
| Sales first | 66.95 | 200 |
| Bootstrap warm | 109.46 | 200 |
| Sales warm | 29.61 | 200 |

## Correctness

- Post-burst bootstrap HTTP **200**
- Harness meta parse still null (same casing gap as PERF08); sample accepted on HTTP success only
- `correctnessChecks.result = pass` under that limited assertion

## Interpretation

1. On a single-host dedicated M-tier fixture, **10 concurrent warm analytics reads stayed under ~0.5 s p95** with zero errors/timeouts.
2. **DB connection pressure stayed low** (peak total 4) under this burst — not a connection-starvation signal at this concurrency.
3. This is **not** a G10 multi-customer pass and **not** an SLO. D1 remains incomplete; D4–D7 deferred; D8 n/a.
4. Do not conflate with PERF08 cold-start (~16 s first useful analytics).

## Files

- `tmp/perf10_measure.ps1`
- `.ai/runs/2026-08-12-PERF10-raw.json`
- `.ai/runs/2026-08-12-PERF10-evidence.md`
