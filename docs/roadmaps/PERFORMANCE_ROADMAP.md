# Trendplus Performance Roadmap

Updated: 2026-08-12
Status: roadmap only; optimization implementation is queue-gated  
Owner queue: `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` (`PERF`)

## Goal

Make Trendplus predictably fast as customer data volume and customer count grow, without trading correctness for speed. Optimization must start from measurements, preserve no-fake-zero/no-fake-green semantics, and avoid hiding slow or failed work behind stale fallback data.

## Principles

- measure before optimizing;
- define representative dataset tiers;
- profile server, database, worker and client separately;
- preserve query/result semantics when adding indexes or caches;
- cache only with explicit freshness/invalidation contracts;
- track p50/p95/p99 where useful rather than averages alone;
- distinguish cold start, warm request and background throughput;
- performance regressions should become observable release evidence.

## Roadmap

### PERF-1 - Baseline and budgets

Establish a repeatable benchmark plan for representative small, medium and large datasets. Define initial budgets for:

- key analytics endpoints;
- Decision Board / Product Decision Center aggregation;
- inventory and supplier queries;
- import preview/run;
- worker batches;
- frontend route load and table interaction;
- API cold start and first useful response.

**Status:** PERF01 contract complete — `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`.
Budgets in that contract and `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md` remain **targets until measured**. The first S-tier measurement pack is recorded in `.ai/runs/2026-08-11-PERF02-evidence.md`. **PERF03** measured backlog: `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md` (cold-start B8 is rank-1; warm B1 paths defer on S-tier). **PERF06** cold-start investigation: `docs/architecture/PERFORMANCE_COLD_START_INVESTIGATION_PLAN.md`. **PERF07** captured bootstrap section timings on M-tier and is recorded in `.ai/runs/2026-08-12-PERF07-evidence.md`. **PERF08** recorded distinct backend/frontend cold-start evidence in `.ai/runs/2026-08-12-PERF08-evidence.md`. **PERF09** scalability gate contract: `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`. **PERF10** first G10 dedicated pack: `.ai/runs/2026-08-12-PERF10-evidence.md` (D2/D3 measured; D1 partial; D4-D7 deferred). Current queue READY: `PERF11`.

No runtime optimization is accepted without a baseline and a before/after comparison.

### PERF-2 - SQL profiling

- capture slow-query candidates using representative filters;
- record execution plans for expensive/high-frequency queries;
- identify repeated scans, N+1 patterns, materialized-view refresh cost and unnecessary cross-database work;
- document query cardinality assumptions;
- separate source connector query cost from Trendplus internal analytics cost.

### PERF-3 - Index strategy

- inventory existing indexes against real query shapes;
- add only evidence-backed indexes;
- measure write/import/refresh cost as well as read improvement;
- detect redundant/unused indexes where provider evidence is available;
- keep migration/rollback impact explicit.

### PERF-4 - Cache strategy

- inventory IMemoryCache/HybridCache/Redis use by feature;
- define cache key ownership, dataScope/tenant dimensions, TTL and invalidation source;
- prohibit cache entries that erase stale/partial/error provenance;
- measure hit rate and invalidation effectiveness;
- prove two-tenant isolation before any shared-SaaS cache.

### PERF-5 - Large dataset benchmarks

Benchmark representative workloads using increasing row counts and realistic date/store/supplier filters. Include:

- products/sales/inventory facts;
- analytics summaries;
- action/outcome history;
- connector imports;
- decision evidence/timeline when implemented.

Record degradation curves rather than one maximum-size result.

### PERF-6 - Memory and allocation

- API process memory under analytics workloads;
- streaming vs materialization behavior;
- import batch memory;
- export/report generation;
- frontend table/chart memory for large result sets;
- worker memory growth across repeated jobs.

### PERF-7 - Worker throughput

Measure and tune:

- import pipeline throughput;
- analytics aggregation/refresh;
- outbox processing;
- sync/redrive jobs;
- future outcome-learning/statistics jobs.

Track queue depth, processing latency, retry cost and poison/dead-letter behavior.

### PERF-8 - Cold start

Measure backend and frontend cold-start paths separately:

- process/container startup;
- database connection/migration checks;
- cache warm-up;
- first analytics request;
- first frontend useful render.

Do not mask cold-start failure with a misleading healthy fallback.

### Completion note

- Date: 2026-08-12
- Agent: codex
- Changed files:
  - `.ai/runs/2026-08-12-PERF08-evidence.md`
  - `.ai/runs/2026-08-12-PERF08-raw.json`
  - `Klijent/clientapp/scripts/perf08_frontend_render.mjs`
  - `tmp/perf08_measure.ps1`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `powershell -ExecutionPolicy Bypass -File tmp/perf08_measure.ps1` ? pass
  - docs/queue validators; `git diff --check` ? pending at commit
- Risks:
  - frontend dev-proxy timing has some run-to-run variance
  - local evidence is not a production SLO commitment
- Next:
  - PERF09 scalability gate evidence contract

### PERF-9 - Scalability gate

Before 10/50-customer milestones, define evidence for:

- per-customer resource envelope;
- concurrent request/load assumptions;
- database connection pressure;
- worker concurrency;
- cache footprint;
- import overlap;
- report/export bursts;
- tenant isolation overhead where shared SaaS is enabled.

Contract: `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md` (PERF09). First dedicated pack: `.ai/runs/2026-08-12-PERF10-evidence.md` (D2/D3 measured). Numeric G10/G50 SLOs remain unmeasured until fuller packs close D1/D4-D7 and (for shared SaaS) D8.

### Completion note

- Date: 2026-08-12
- Agent: Cursor
- Changed files:
  - `tmp/perf10_measure.ps1`
  - `.ai/runs/2026-08-12-PERF10-evidence.md`
  - `.ai/runs/2026-08-12-PERF10-raw.json`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `powershell -ExecutionPolicy Bypass -File tmp/perf10_measure.ps1 -SkipSetup` — pass (D2 p95 468.58 ms; D3 peak total connections 4; error/timeout 0)
  - docs/queue validators — pass
- Risks:
  - D1 partial; D4-D7 deferred
  - shared_saas (D8) blocked on MT fixtures
- Next:
  - PERF11 deferred scalability dimensions (D1/D4-D7)

## Required benchmark evidence

Each future optimization should record:

- exact commit;
- environment/provider;
- dataset tier and seed/source;
- command/request shape;
- warm/cold state;
- before/after p50/p95 where applicable;
- correctness checks proving output did not change unexpectedly;
- residual risk.

## Dependencies

- RQ/STAB correctness before performance shortcuts;
- OBS metrics/tracing for sustainable regression detection;
- MT before shared-SaaS scaling claims;
- QDB for connector-specific throughput benchmarks.

## Non-goals

This roadmap does not justify broad rewrites, speculative micro-optimizations, weakening tests, hiding errors behind cache, or introducing distributed infrastructure before measured need exists.
