# PERF10 Evidence

- Date: 2026-08-12
- Prompt: PERF10 - Capture first scalability-gate evidence pack (PERF-9)
- Pack: G10 dedicated evidence index
- Commit: `f026a7f9570b5f20c72fbefd2eff89424037e9f3`
- Dataset: `trendplus_perf_m` (M-PERF-01)
- Raw JSON: `.ai/runs/2026-08-12-PERF10-raw.json`

## Summary

This pack is the first dedicated-mode scalability-gate evidence index for the G10 milestone. It does not claim G10 pass.

What it does establish:

- dedicated mode is the only mode in scope for this pack;
- the contract dimensions D1-D8 are explicit and mapped to evidence or gaps;
- single-load anchors from PERF05 and cold-start anchors from PERF08 are citeable;
- shared SaaS claims remain out of scope and MT-owned.

What it does not establish:

- no concurrent multi-user proof;
- no database connection pressure run;
- no worker concurrency run;
- no cache footprint run;
- no import overlap run;
- no report/export burst run;
- no shared-SaaS tenant isolation proof.

## Evidence anchors

- `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`
- `.ai/runs/2026-08-12-PERF05-evidence.md`
- `.ai/runs/2026-08-12-PERF08-evidence.md`

Existing single-load anchors used as context for later G10 measurement slices:

- PERF05 warm B1/B2 markers on M-tier, including dashboard warm p95 at 52.13 ms and decision-board warm p95 at 126.26 ms.
- PERF08 backend cold-start markers, including first useful analytics p50 at 15,992.42 ms and warm second bootstrap at 75.3 ms.
- PERF08 frontend cold-start markers, including useful render p50 at 8,538 ms with a dev-proxy frontend path.

## Dimensions

| Dimension | Status | Notes |
|---|---|---|
| D1 Per-customer resource envelope | deferred | No reusable per-customer envelope pack exists yet. |
| D2 Concurrent request / load assumptions | deferred | Existing anchors are single-load only; no concurrent-user harness is present. |
| D3 Database connection pressure | deferred | No dedicated connection-pressure pack exists yet. |
| D4 Worker concurrency | deferred | No worker throughput pack exists yet for the G10 milestone. |
| D5 Cache footprint | deferred | No multi-customer cache footprint run exists yet. |
| D6 Import overlap | deferred | No overlap run with analytics reads/workers exists yet. |
| D7 Report / export bursts | deferred | No burst pack exists yet. |
| D8 Tenant isolation overhead | n/a_dedicated | Shared SaaS remains MT-owned; not claimed by this dedicated pack. |

## Interpretation

The current evidence is enough to define the next measurement slices, but not enough to claim milestone readiness.

G10 now has a citeable dedicated evidence index that shows:

1. which dimensions are already anchored by earlier PERF evidence;
2. which dimensions still need a real concurrency/envelope run;
3. why shared-SaaS claims remain blocked until MT-owned fixtures and gates exist.

## Residual risk

- Dedicated single-host measurements do not generalize to concurrent customer load.
- The repo still lacks a reusable concurrent-load harness for a true G10 run.
- Shared SaaS remains blocked on MT fixtures and isolation gates.

## Next

- PERF10 follow-up measurement pack for D1/D2/D3 on a dedicated fixture with actual concurrency and connection pressure.
