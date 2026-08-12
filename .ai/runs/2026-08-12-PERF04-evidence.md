# PERF04 evidence log

Prompt: PERF04 - Prepare M-tier baseline measurement plan  
Date: 2026-08-12  
Status: DONE

## Deliverable

- `docs/architecture/PERFORMANCE_M_TIER_MEASUREMENT_PLAN.md`

## Summary

- Defined M-tier seed recipe `M-PERF-01` with row-count targets (12k products, ~180k sale lines, 180-day window).
- Listed mandatory B1–B8 coverage for pilot claims, mapped to PERF03 backlog IDs (`PERF-MEASURE-01`, `PERF-COLD-01`, `PERF-CACHE-01`, `PERF-MONITOR-01`).
- Documented cold/warm matrix and correctness co-assertions per family; reused PERF01/02 protocol without inventing M-tier timings.

## Checks

- Plan cites `PERF-MEASURE-01` and other backlog IDs — pass
- No optimization authorized — pass
- Queue validators — pending at commit time

## Risks

- `M-PERF-01` generator script does not exist yet; execution prompt must add seed tooling before measurement.
- B4 import requires fixed `.accdb` fixture path to be chosen in execution prompt.

## Next

- Promote runtime measurement execution as **PERF05** using this plan.
- PERF planning lane: PERF05 READY until M-tier evidence recorded.
