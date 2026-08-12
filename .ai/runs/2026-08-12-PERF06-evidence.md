# PERF06 evidence log

Prompt: PERF06 - Investigate dashboard bootstrap cold-start (PERF-COLD-01)  
Date: 2026-08-12  
Status: DONE

## Deliverable

- `docs/architecture/PERFORMANCE_COLD_START_INVESTIGATION_PLAN.md`

## Summary

- Documented five ranked variance hypotheses (protocol mismatch highest confidence).
- Mapped 20 sequential bootstrap sections to builders/cache keys with P0–P2 profiling priority.
- Defined standardized B8 before/after protocol and runtime promotion gates for PERF-COLD-01.
- No runtime optimization introduced.

## Key findings (from code + PERF02/PERF05 evidence)

- Bootstrap cold path executes **sequential section builders** inside one outer cache miss.
- S-tier vs M-tier gap (~55 s vs ~6.4 s) is **not** explained by dataset scale alone — harness/startup differences must be controlled.
- M-tier cold p95 still **borderline** vs 5 s target; warm bootstrap on M-tier is fast (52 ms p95).

## Checks

- Plan cites PERF-COLD-01 and M-tier numbers — pass
- No optimization authorized — pass
- Queue validators — pending at commit

## Risks

- Section timings still unmeasured; PERF07 profiling pass required before remediation
- Missing supplier analytics objects on perf DB may affect Advanced/PDC sections differently than prod

## Next

- PERF07: execute bootstrap section timing capture on M-tier (P0 sections)
