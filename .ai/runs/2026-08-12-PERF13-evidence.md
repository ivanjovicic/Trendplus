# PERF13 Evidence

- Date: 2026-08-12
- Prompt: PERF13 - Unblock D5 cache footprint or D6 import-overlap evidence
- Pack: `PERF13-G10-cache-footprint-01`
- Milestone / mode: **G10** / **dedicated**
- Dataset: `trendplus_perf_m` (M-PERF-01)
- Raw JSON: `.ai/runs/2026-08-12-PERF13-raw.json`
- Harness: `tmp/perf13_measure.ps1`
- Contract: `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`

## Method

1. Clear analytics cache state through the existing cache invalidate endpoint.
2. Warm several cached analytics routes with distinct parameter sets.
3. Read the cache status endpoint, which now exposes tracked key count for a footprint snapshot.
4. Estimate cache footprint from process RSS delta after warm-up.

## Dimension status

| Id | Status | Result |
|---|---|---|
| D1 | cite_PERF11 | already measured |
| D2 | cite_PERF10 | already measured |
| D3 | cite_PERF10 | already measured |
| D4 | cite_PERF12 | already measured |
| D5 cache footprint | **measured** | tracked keys before/peak/after: 0 / 6 / 6; estimated RSS delta: 0 MB |
| D6 import overlap | **blocked** | no M-PERF Access fixture in this repo scope |
| D7 | n/a | not exercised in this pack |
| D8 | `n/a_dedicated` | MT-owned |

## Interpretation

1. Cache status now exposes tracked key count, which gives a measurable footprint proxy for D5.
2. The warm-up run created a non-zero tracked-key footprint without fabricating cache cardinality.
3. D6 is still a separate blocker and should stay explicit rather than inferred.
4. Process RSS remains an estimate, not a byte-accurate cache allocator report.

## Files

- `tmp/perf13_measure.ps1`
- `.ai/runs/2026-08-12-PERF13-raw.json`
- `.ai/runs/2026-08-12-PERF13-evidence.md`