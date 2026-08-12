# Performance Measured Optimization Backlog

Status: authoritative PERF03 contract  
Date: 2026-08-12  
Roadmap: `docs/roadmaps/PERFORMANCE_ROADMAP.md`  
Baseline evidence:

- `.ai/runs/2026-08-11-PERF02-evidence.md`
- `.ai/runs/2026-08-11-PERF02-raw.json`
- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`

## Purpose

Turn the first recorded **S-tier** measurement pack into a prioritized, evidence-backed optimization backlog. Every item links to a measured benchmark, names a performance family, and defines before/after proof — no speculative tuning.

This document is **planning only**. It does not authorize runtime query/index/cache changes.

## Measurement context (S-tier, commit `4caacff`)

| Field | Value |
|---|---|
| Dataset tier | S (~15 products, 26 sale lines, 10 sale headers) |
| Database | `trendplus_test` on Postgres 18.3 |
| Prewarm | disabled |
| Period | 2026-05-13 – 2026-08-11 UTC |
| Engineering warm p95 targets | `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md` |
| Measured warm paths | **within** warm p95 targets for B1 endpoints |
| Measured cold-start path | **far above** cold p95 target for dashboard bootstrap |

## Executive summary

1. **Cold start (B8)** is the only measured family that clearly breaches demo/readiness targets today.
2. **Warm cache miss (B2 cold-cache on warm process)** is the second candidate; warm steady-state is already fast (~32 ms p50).
3. **Warm SQL read paths (B1 sales/inventory)** are not backlog drivers on S-tier — monitor on M-tier before indexing work.
4. **Decision Board, PDC, workers, import, frontend route load** have **no** S-tier baseline yet — measure before optimizing.

---

## Backlog items (ranked)

Rank uses measured cost × business impact. Lower rank number = investigate or optimize first when a runtime PERF prompt is promoted.

### 1. PERF-COLD-01 — Dashboard bootstrap cold process / cold cache (B8)

| Field | Value |
|---|---|
| Rank | 1 |
| Family | cold-start (`PERF-8`, benchmark `B8`) |
| Surface | `GET /api/analytics/cached/dashboard/bootstrap` |
| Business impact | Executive dashboard entry; demo blocker per ops budgets |
| Measured (2026-08-11) | cold-process/cold-cache: p50 **12.3 s**, p95 **55.4 s**, min 10.5 s, max 55.4 s (N=5) |
| Target (engineering) | cold p95 **&lt; 5 s** (`docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md`) |
| Correctness baseline | `meta.success=true`, `meta.isPartial=true`, `warningCode=ANALYTICS_PARTIAL_DATA`, `summary.totalRevenue>0`, `inventory.totalSkuCount>0` |
| Hypothesis (unproven) | Process startup + first analytics aggregation + partial section fan-out dominate; not yet SQL-profiled |
| Runtime scope gate | Requires promoted PERF cold-start slice; **no index/cache change without profiling evidence** |
| Before/after proof | Repeat B8 protocol on same S-tier seed; report p50/p95, error/timeout rate, same correctness checks; do not hide `isPartial` |
| Rollback | Feature-flag or revert startup/prewarm changes; compare cold samples side-by-side |

### 2. PERF-CACHE-01 — Dashboard bootstrap warm process / cold cache priming (B2)

| Field | Value |
|---|---|
| Rank | 2 |
| Family | cache (`PERF-4`, benchmark `B2/B8` warm branch) |
| Surface | same bootstrap route after process warm, cache empty |
| Business impact | First click after deploy/restart before steady state |
| Measured | first response **1060 ms**; warm steady p50 **31.7 ms**, p95 **35.5 ms** (N=20) |
| Target | warm p95 **&lt; 2 s** — steady state passes; first miss is borderline on single sample |
| Correctness baseline | `meta.success=true`, revenue and SKU counts positive |
| Hypothesis (unproven) | Cache key miss / prewarm disabled; investigate prewarm hosted service + bootstrap cache key ownership |
| Before/after proof | Record cold-cache first hit ms + warm p50/p95; measure cache hit/miss if instrumentation exists; correctness unchanged |
| Rollback | Disable prewarm or revert TTL/key changes; verify stale/partial meta not masked |

### 3. PERF-CACHE-02 — Sales summary warm process / cold cache (B1)

| Field | Value |
|---|---|
| Rank | 3 |
| Family | cache (`PERF-4`, benchmark `B1`) |
| Surface | `GET /api/analytics/cached/sales/summary` |
| Measured | cold-cache first hit **625 ms**; warm p50 **29.8 ms**, p95 **39.1 ms** |
| Target | warm p95 &lt; 2 s (dashboard family budget row applies by analogy) |
| Priority note | Lower than bootstrap; warm path already healthy |
| Before/after proof | Same B1 protocol; revenue/transactions correctness checks |

### 4. PERF-MONITOR-01 — Inventory status warm path (B1)

| Field | Value |
|---|---|
| Rank | 4 (monitor / defer on S-tier) |
| Family | cache + read SQL |
| Surface | `GET /api/analytics/cached/inventory/status` |
| Measured | cold-cache **145 ms**; warm p95 **33.1 ms** |
| Action | **No optimization prompt** on S-tier; re-check on M-tier |

### 5. PERF-SQL-01 — Bootstrap partial-section SQL profiling (unmeasured SQL)

| Field | Value |
|---|---|
| Rank | 5 |
| Family | SQL profiling (`PERF-2`) |
| Trigger | B8 + `isPartial=true` / `ANALYTICS_PARTIAL_DATA` |
| Blocker | No execution plans captured in PERF02 |
| Action | Planning/investigation only: identify bootstrap sub-queries and timeout boundaries before index proposals |
| Before/after proof | Execution plan + duration per section; no semantic change to meta partial honesty |

### 6. PERF-MEASURE-01 — M-tier baseline pack (unmeasured surfaces)

| Field | Value |
|---|---|
| Rank | 6 |
| Family | baseline expansion (`PERF-5`) |
| Surfaces not in S-tier pack | Decision Board aggregate, Product Decision Center, supplier scorecard, import preview/run, workers, frontend route data-ready |
| Action | Record M-tier measurements before any optimization claims |
| Dependency | PERF01/02 protocol; **PERF04** plan: `docs/architecture/PERFORMANCE_M_TIER_MEASUREMENT_PLAN.md` |

---

## Family separation (no mixed prompts)

Future runtime prompts must pick **one primary family** per slice:

| Family | Backlog IDs | Forbidden mix in one prompt |
|---|---|---|
| Cold-start | PERF-COLD-01 | cache TTL tweaks without cold remeasure |
| Cache | PERF-CACHE-01, PERF-CACHE-02 | SQL index migrations |
| SQL / index | PERF-SQL-01 | cache-only changes without plan review |
| Worker throughput | (not measured) | API-only tuning |
| Memory / export | (not measured) | unrelated endpoint family |
| Baseline expansion | PERF-MEASURE-01 | production optimization |

---

## Correctness gates (all items)

Any future optimization must preserve:

- `meta.success=false` or Problem on error — **no fake zero**
- `emptyReason` / insufficient_data honesty on empty success
- `isPartial` / warning codes when data is partial — **no fake green**
- Same numeric totals within documented tolerance when comparing before/after on fixed seed
- RQ/STAB semantics authoritative over latency wins

---

## Explicit non-candidates (S-tier evidence)

Do **not** open optimization prompts for these based on PERF02 alone:

- warm p95 sales summary (39 ms measured vs 2 s target)
- warm p95 inventory status (33 ms vs 3 s target)
- warm p95 dashboard steady state (35 ms vs 2 s target)
- speculative indexes on unprofiled queries
- Redis/distributed cache without MT tenant isolation proof

---

## Suggested promotion order for runtime slices

When owner promotes runtime PERF work:

1. PERF-COLD-01 investigation + remediation plan
2. PERF-CACHE-01 prewarm / bootstrap cache miss
3. PERF-MEASURE-01 M-tier pack
4. PERF-SQL-01 only if profiling confirms SQL dominance
5. PERF-CACHE-02 only if M-tier or operator pain appears

---

## Governance

Runtime PERF prompts must cite:

1. Backlog ID from this file
2. Benchmark ID (B1/B2/B8) and `.ai/runs/2026-08-11-PERF02-*` baseline numbers
3. Family column and rollback plan
4. Correctness checks from the backlog row

---

## Non-goals

- no production code changes in PERF03
- no claiming optimization already applied
- no M/L tier numbers invented from S-tier extrapolation
- no weakening of partial/stale/error visibility for speed
