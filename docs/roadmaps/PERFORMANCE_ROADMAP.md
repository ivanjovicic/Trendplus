# Trendplus Performance Roadmap

Updated: 2026-08-08  
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
Budgets in that contract and `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md` remain **targets until measured**. The first S-tier measurement pack is recorded in `.ai/runs/2026-08-11-PERF02-evidence.md`. **PERF03** measured backlog: `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md` (cold-start B8 is rank-1; warm B1 paths defer on S-tier). **PERF04** M-tier plan: `docs/architecture/PERFORMANCE_M_TIER_MEASUREMENT_PLAN.md`. Current queue READY: `PERF05` (M-tier baseline pack execution).

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
