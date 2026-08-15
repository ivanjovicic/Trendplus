# Trendplus Performance Roadmap

Updated: 2026-08-15
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
Budgets in that contract and `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md` remain **targets until measured**. The first S-tier measurement pack is recorded in `.ai/runs/2026-08-11-PERF02-evidence.md`. **PERF03** measured backlog: `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md` (cold-start B8 is rank-1; warm B1 paths defer on S-tier). **PERF06** cold-start investigation: `docs/architecture/PERFORMANCE_COLD_START_INVESTIGATION_PLAN.md`. **PERF07** captured bootstrap section timings on M-tier and is recorded in `.ai/runs/2026-08-12-PERF07-evidence.md`. **PERF08** recorded distinct backend/frontend cold-start evidence in `.ai/runs/2026-08-12-PERF08-evidence.md`. **PERF09** scalability gate contract: `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`. **PERF10** first G10 dedicated pack: `.ai/runs/2026-08-12-PERF10-evidence.md` (D2/D3 measured). **PERF11** deferred-dimension pack: `.ai/runs/2026-08-12-PERF11-evidence.md` (D1 measured; D4/D5 initially blocked). **PERF12** remaining-gap pack: `.ai/runs/2026-08-12-PERF12-evidence.md` (D4/D7 measured; D5/D6 durable blockers). **PERF13** cache-footprint follow-up: `.ai/runs/2026-08-12-PERF13-evidence.md` (D5 measured; D6 blocked). **PERF14** import-overlap evidence: `.ai/runs/2026-08-12-PERF14-evidence.md` (D6 measured). **PERF15** shared-saas evidence gate: `docs/architecture/PERFORMANCE_SHARED_SAAS_EVIDENCE_GATE.md` (D8 remains MT-owned / `n/a_dedicated`). Current queue READY: `PERF16`.

No runtime optimization is accepted without a baseline and a before/after comparison.

### PERF-2 - SQL profiling

- capture slow-query candidates using representative filters;
- record execution plans for expensive/high-frequency queries;
- identify repeated scans, N+1 patterns, materialized-view refresh cost and unnecessary cross-database work;
- document query cardinality assumptions;
- separate source connector query cost from Trendplus internal analytics cost.
- measure the cost of daily/period fact materialization separately from interactive query cost.
- retain the declared product/location/time grain in benchmark fixtures so a fast aggregate does not hide an incorrect join or duplicate fact.

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
- observed inventory history and forecast/backtest facts when owner-gated.

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
- future observed inventory snapshot, forecast materialization and exception-digest jobs, each with independent backlog/freshness evidence.

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

Contract: `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md` (PERF09). PERF10 D2/D3; PERF11 D1; PERF12 D4/D7 measured; PERF13 D5 measured; PERF14 D6 measured. D8 remains MT-owned; PERF15 froze that boundary in `docs/architecture/PERFORMANCE_SHARED_SAAS_EVIDENCE_GATE.md`. Numeric G10/G50 shared-SaaS SLOs remain unmeasured.

### Completion note

- Date: 2026-08-12
- Agent: Cursor
- Changed files:
  - `tmp/perf12_measure.ps1`
  - `.ai/runs/2026-08-12-PERF12-evidence.md`
  - `.ai/runs/2026-08-12-PERF12-raw.json`
  - `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `powershell -ExecutionPolicy Bypass -File tmp/perf12_measure.ps1 -SkipSetup` - pass
  - docs/queue validators - pending at commit
- Risks:
  - D5/D6 durable blockers remain
  - D7 html preview is not production PDF burst
- Next:
  - PERF14 D6 import-overlap evidence

### Completion note

- Date: 2026-08-12
- Status: DONE
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`
  - `Infrastructure/Services/Caching/HybridCacheService.cs`
  - `Infrastructure/Services/Caching/InMemoryCacheService.cs`
  - `Infrastructure/Services/Caching/DisabledAnalyticsCacheService.cs`
  - `tmp/perf13_measure.ps1`
  - `.ai/runs/2026-08-12-PERF13-evidence.md`
  - `.ai/runs/2026-08-12-PERF13-raw.json`
  - `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `powershell -ExecutionPolicy Bypass -File tmp/perf13_measure.ps1 -SkipSetup` - pass (D5 measured)
  - `dotnet build Trendplus2.Backend.slnf -v minimal` - pass
  - governance validators - pass
- Risks:
  - D6 import overlap still lacks a real M-PERF Access fixture
  - cache footprint is tracked-key based; RSS delta stayed flat on this host
- Next:
  - D6 import-overlap blocker follow-up

### Completion note

- Date: 2026-08-12
- Status: DONE
- Changed files:
  - `tmp/perf14_measure.ps1`
  - `.ai/runs/2026-08-12-PERF14-evidence.md`
  - `.ai/runs/2026-08-12-PERF14-raw.json`
  - `docs/roadmaps/PERFORMANCE_ROADMAP.md`
  - `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `powershell -ExecutionPolicy Bypass -File tmp/perf14_measure.ps1 -SkipSetup` - pass
  - `git diff --check` - pending at commit
- Risks:
  - worker/import overlap evidence is local-host only
  - D8 remains MT-owned and n/a_dedicated
- Next:
  - PERF16 D8 reopen after MT fixtures; current execution is OBS08

## Required benchmark evidence

Each future optimization should record:

- exact commit;
- environment/provider;
- dataset tier and seed/source;
- command/request shape;
- warm/cold state;
- before/after p50/p95 where applicable;
- correctness checks proving output did not change unexpectedly;
- fact grain, source/proxy provenance and population coverage of the fixture;
- residual risk.

## Dependencies

- RQ/STAB correctness before performance shortcuts;
- OBS metrics/tracing for sustainable regression detection;
- MT before shared-SaaS scaling claims;
- QDB for connector-specific throughput benchmarks.

## Non-goals

This roadmap does not justify broad rewrites, speculative micro-optimizations, weakening tests, hiding errors behind cache, or introducing distributed infrastructure before measured need exists.

It also does not authorize collapsing raw SKU/store/day facts into only a fast aggregate when downstream inventory, availability, forecast or outcome contracts require the lower-grain evidence.
