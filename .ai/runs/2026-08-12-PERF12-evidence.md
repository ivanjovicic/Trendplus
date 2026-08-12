# PERF12 Evidence

- Date: 2026-08-12
- Prompt: PERF12 — Close remaining scalability gaps (D4 retry / D5-D7)
- Pack: `PERF12-G10-remaining-gaps-01`
- Milestone / mode: **G10** / **dedicated**
- Dataset: `trendplus_perf_m` (M-PERF-01)
- Raw JSON: `.ai/runs/2026-08-12-PERF12-raw.json`
- Harness: `tmp/perf12_measure.ps1`
- Contract: `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`

## Method

1. **D4 retry:** start API with `PROCESS_TYPE=worker` (AggregationWorker registers only there), admin start, poll `status` + `lastHeartbeat` using Windows PowerShell `ConvertFrom-Json` **without** `-Depth` (PS 5.1 rejects `-Depth` and left PERF11 body null).
2. **D7:** three serial HTML document generates with explicit columns/rows for `daily-sales`.
3. **D5/D6:** keep durable blocked reasons (no cache footprint API; no M-PERF Access fixture).

## Dimension status

| Id | Status | Result |
|---|---|---|
| D1 | cite_PERF11 | already measured |
| D2 | cite_PERF10 | already measured |
| D3 | cite_PERF10 | already measured |
| D4 workers | **measured** | `AnalyticsAggregationWorker` registered; status **Running** with heartbeat; start HTTP 200; cycle ~**3.06 s** to first good poll |
| D5 cache footprint | **blocked** | durable: no cardinality/MB instrumentation |
| D6 import overlap | **blocked** | durable: no M-PERF Access fixture |
| D7 export bursts | **measured** | 3/3 completed; p50/p95 **460.92 ms**; sizes 3622 bytes (html preview) |
| D8 | `n/a_dedicated` | MT-owned |

## Root-cause notes (PERF11 false blocker)

- Web process shows AggregationWorker as `ConfiguredButNotRunning` / not registered.
- Worker process binds the configured URL (use **8080**, not a second port).
- Harness JSON parse must not use `ConvertFrom-Json -Depth` on Windows PowerShell 5.1.

## Interpretation

1. D4 and D7 move from blocked/deferred to **measured** on dedicated G10 fixture.
2. D5/D6 remain **durable blockers** until instrumentation/fixtures exist — not silent deferrals.
3. Still **not** a multi-customer G10 pass and **not** an SLO commitment.
4. D7 is html preview generate, not production PDF burst load.

## Files

- `tmp/perf12_measure.ps1`
- `.ai/runs/2026-08-12-PERF12-raw.json`
- `.ai/runs/2026-08-12-PERF12-evidence.md`
