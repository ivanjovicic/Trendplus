# Performance Baseline Contract

Status: authoritative PERF01 contract  
Date: 2026-08-11  
Roadmap: `docs/roadmaps/PERFORMANCE_ROADMAP.md`  
Related ops targets (not measured facts):

- `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md`
- `docs/qa/DECISION_BOARD_AGGREGATE_PERFORMANCE_BUDGET.md`
- `docs/Analytics/ANALYTICS_CACHE_INVALIDATION_AND_STALE_POLICY.md`
- `docs/qa/ANALYTICS_SQL_OBSERVABILITY_TIMEOUTS.md`

## Purpose

Define a reproducible measurement plan for Trendplus performance work so later PERF prompts can require before/after evidence without guessing.

This contract:

- inventories what to measure and what already exists;
- defines small/medium/large dataset tiers and cold vs warm rules;
- proposes engineering budgets as **targets**, not as measured facts;
- requires correctness co-assertions so speed never buys fake-zero/fake-green.

It does **not**:

- change queries, indexes, caches or workers;
- claim that current p50/p95 values are known;
- authorize PERF02 backlog items without recorded measurements.

## Non-goals

- no BenchmarkDotNet/k6/nBomber harness introduced by this contract
- no production optimization
- no SLA/contractual customer promises
- no weakening of analytics reliability semantics

## Current measurement inventory

### Present today

| Artifact | Role | Limitation |
|---|---|---|
| `RequestPerformanceLoggingMiddleware` / `PerformanceLoggingBehavior` | Duration logging; slow threshold config | Not a repeatable benchmark gate |
| Performance dashboard / `PerformanceLog` | Ops visibility | Not automated CI budgets |
| Cache prewarm hosted service + tests | Warm-path support | Not a p95 suite |
| Cached analytics failure/meta tests | Correctness under timeout/fallback | Not latency gates |
| Index SQL/migrations (`*PerformanceIndexes*`) | Historical index work | No paired before/after latency pack |
| Ops budget docs | Target table by family | Explicitly unmeasured |

### Missing today

- no load/benchmark runner or CI perf gate
- no canonical S/M/L dataset seed recipe
- no recorded cold/warm protocol results tied to commit + env
- no worker throughput scripts (queue depth, batch latency)
- no API/frontend cold-start first-useful-response script

## Benchmark families (priority order)

Each family maps to a future PERF phase. Measure the listed representative request/job shapes first.

| ID | Family | Representative surfaces | PERF phase |
|---|---|---|---|
| B1 | Analytics read SQL | Cached sales summary/daily/top; supplier sales stats; inventory balance/status/list | PERF-2/3/5 |
| B2 | Decision aggregates | Decision Board aggregate; Product Decision Center; Supplier Decision Hub summary/ranking | PERF-2/4/5 |
| B3 | Cache behavior | Same as B1/B2 with cache miss then hit; prewarm on/off | PERF-4/8 |
| B4 | Import / connector | Access import preview + run (or dry-run equivalent); job enqueue/claim latency | PERF-5/7 |
| B5 | Workers | `AnalyticsAggregationWorker` cycle; nightly refresh one MV/table batch; cache prewarm probe | PERF-7/8 |
| B6 | Memory / export | Large inventory/supplier table payloads; report/export generation | PERF-5/6 |
| B7 | Frontend useful load | Dashboard route; Product Decision Center; Inventory; Actions list (first paint + data ready) | PERF-8/5 |
| B8 | Cold start | API process start → first analytics 200/empty-success; frontend first useful render | PERF-8 |

Do not expand to speculative micro-benchmarks until B1–B3 have at least one recorded S-tier baseline.

## Dataset tiers

Tiers are engineering fixtures, not customer labels. Prefer synthetic or anonymized disposable data. Never require production customer payloads for PERF01 methodology.

| Tier | Intent | Approximate scale (guidance) | How to reproduce safely |
|---|---|---|---|
| **S (small)** | Local/dev smoke baseline | ~1 store, ~1–3 suppliers, ≤5k products, ≤30 days sales, ≤50k fact rows | Dedicated test DB name; seed script or Import fixture; document row counts after seed |
| **M (medium)** | Pilot-like | ~3–10 stores, multiple suppliers, ≤50k products, ≤180 days, ≤1M fact rows | Same seed recipe scaled; record generation parameters |
| **L (large)** | Stress / degradation curve | ≥10 stores, multi-year or dense daily facts, multi-million fact rows | Isolated DB only; never shared with pilot; may use generated data |

### Tier metadata (required on every run)

- `datasetTier`: `S` \| `M` \| `L`
- `seedRecipeId` / script path / commit that produced the data
- row counts for products, sales headers/lines, inventory snapshots, actions
- period filters used (`from`/`to`, store, supplier)
- provider: Postgres version, whether InMemory was used (InMemory is **not** valid for SQL baseline claims)

## Cold vs warm rules

| State | Definition | Setup |
|---|---|---|
| **Cold process** | API/worker process just started | Restart process/container; no prior requests |
| **Cold cache** | Application caches empty for the measured key space | Restart or explicit cache clear; disable prewarm for the run |
| **Warm cache** | Target keys already populated | One unmeasured priming request, or prewarm completed successfully |
| **Warm process** | Process has handled traffic; pools open | At least one prior request completed; DB connections established |

### Measurement protocol

1. Record environment: OS, CPU/RAM, commit SHA, config flags (`CI`, connection strings redacted), prewarm enabled Y/N.
2. State the cold/warm matrix cell being measured (process × cache).
3. For latency: collect N≥20 successful samples for warm; N≥5 for cold (document if fewer).
4. Report p50 and p95 (and p99 when N≥50). Do not report average alone.
5. Discard timed-out/error samples from latency percentiles; report error/timeout rate separately.
6. Never present a budget from `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md` as a measured result.

## Engineering budgets (targets only)

These are initial engineering targets for prioritization. They are **not** measured baselines and **not** contractual SLAs.

### API / analytics (aligns with ops budget doc)

| Surface | Warm p95 target | Cold p95 target |
|---|---:|---:|
| Dashboard / bootstrap | 2s | 5s |
| Product Decision Center | 3s | 8s |
| Supplier scorecard / decision hub | 3s | 8s |
| Inventory analytics | 3s | 8s |
| Data quality | 3s | 10s |
| Pre/post nivelacija | 4s | 12s |
| Reports (cached path) | 5s | 15s |
| Decision Board aggregate | 3s | 8s |

### Workers / import / memory (initial proposals)

| Surface | Target | Notes |
|---|---|---|
| Aggregation worker one full cycle (S tier) | p95 &lt; 60s | Exclude nightly MV rebuild |
| Nightly single heavy refresh unit (S tier) | complete without timeout under configured `CommandTimeoutSeconds` | Record duration; do not raise timeout to “pass” |
| Access import preview (S tier file) | p95 &lt; 30s | Exclude first-time CLI/ODBC install cost |
| Access import run throughput (S tier) | rows/sec + peak RSS | Record batch size settings |
| API RSS under B1 warm load (S tier) | document peak; flag unexplained growth across repeats | No hard kill threshold yet |
| Frontend route data-ready (S tier, warm API) | p95 &lt; 3s for primary analytics routes | Separate from API-only budgets |

When a measurement exists, store it beside the target and mark `status=measured` vs `status=target_only`.

## Correctness co-assertions (mandatory)

Every performance run that claims a “successful” sample must also assert product honesty:

1. **No fake zero:** error/unavailable paths must not look like trusted `0` KPI.
2. **Empty ≠ error:** empty success keeps `meta.success=true` and explicit empty/DQ reason where contract requires it.
3. **Stale/partial visible:** cache hits must not erase freshness/warning provenance.
4. **Timeout ≠ healthy:** SQL/cancel timeout must surface as failure/warning per existing endpoint contract, not silent empty success.
5. **Snapshot stability:** for decision/action surfaces, creation snapshots must not be rewritten by measurement.
6. **Golden/parity (when available):** Decision Board / ranking parity checks remain green for the same filters.

If correctness fails, the latency sample is invalid for baseline promotion.

## Environment metadata template

Record for each run:

```text
commit:
datetimeUtc:
machine:
os:
dotnetSdk:
postgresVersion: (or n/a)
datasetTier:
seedRecipeId:
prewarmEnabled:
cacheState: cold|warm
processState: cold|warm
benchmarkId: B1..B8
requestOrJob:
samples:
p50Ms:
p95Ms:
p99Ms:
errorRate:
timeoutRate:
rowCounts:
correctnessChecks: pass|fail
notes:
```

## Mapping to PERF roadmap phases

| PERF phase | Baseline requirement from this contract |
|---|---|
| PERF-1 | This document + first recorded S-tier B1/B2/B3 runs |
| PERF-2 SQL profiling | Slow candidates from B1/B2 with plans on M tier |
| PERF-3 indexes | Only against measured B1/B2 shapes |
| PERF-4 cache | B3 hit/miss + invalidation freshness checks |
| PERF-5 large datasets | Degradation curves S→M→L for B1/B2/B4/B6 |
| PERF-6 memory | B6 + repeated B5 cycles |
| PERF-7 workers | B5 throughput/latency/retries |
| PERF-8 cold start | B8 + cold cells of B1/B7 |
| PERF-9 scalability | After multi-customer assumptions; requires MT for shared-SaaS claims |

## Suggested first measurement pack (post-PERF01)

1. S-tier seed + metadata recorded
2. B1 cached sales summary + inventory list (cold cache, warm process) then warm cache
3. B2 Decision Board + Product Decision Center same matrix
4. B3 explicit miss→hit comparison
5. B8 API cold start first useful analytics response
6. Attach correctness co-assertions for each

Recorded baseline pack:

- `.ai/runs/2026-08-11-PERF02-evidence.md`
- `trendplus_test` seeded baseline with B1 sales summary, B2 dashboard bootstrap and B8 cold-start measurements

## Acceptance (PERF01)

- reproducible baseline methodology exists in one contract;
- dataset tiers and cold/warm rules are explicit;
- budgets are labeled targets, not measured facts;
- benchmark families cover SQL, cache, large datasets, memory, workers and cold start;
- correctness co-assertions are mandatory;
- no runtime optimization was introduced.
