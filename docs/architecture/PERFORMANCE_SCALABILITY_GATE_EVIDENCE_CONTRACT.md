# Performance Scalability Gate Evidence Contract

Status: authoritative PERF09 contract  
Date: 2026-08-12  
Roadmap: `docs/roadmaps/PERFORMANCE_ROADMAP.md` (PERF-9)  
Related:

- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`
- `docs/architecture/PERFORMANCE_MEASURED_OPTIMIZATION_BACKLOG.md`
- `docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md`
- `.ai/runs/2026-08-12-PERF05-evidence.md`
- `.ai/runs/2026-08-12-PERF08-evidence.md`

## Purpose

Define a citeable **scalability gate** evidence contract before 10-customer and 50-customer milestones.

This contract answers:

> For a stated customer count and deployment mode, what resource, concurrency, and overlap evidence must exist before claiming the platform is ready to absorb that load?

It is **documentation only**. It does **not**:

- invent numeric SLOs, connection caps, or customer promises;
- authorize runtime optimization, index, cache, or worker changes;
- replace MT ownership of shared-SaaS tenant isolation;
- treat single-host cold-start timings as multi-customer proof;
- present ops budget targets from `ANALYTICS_PERFORMANCE_BUDGETS.md` as measured facts.

## Milestones (planning labels)

| Milestone | Intent | Deployment modes in scope |
|---|---|---|
| **G10** | ~10 customers | dedicated deploy per customer (default), optional shared SaaS only if MT gates allow |
| **G50** | ~50 customers | same modes; shared SaaS claims require stronger isolation + resource-envelope evidence |

A milestone is **not** passed by a single warm p95 sample on one laptop. Pass requires the evidence pack below for the claimed mode.

## Deployment modes

| Mode | Meaning | Isolation owner |
|---|---|---|
| `dedicated` | One customer per API/DB (or equivalent hard isolation) | Ops + STAB release posture |
| `shared_saas` | Multiple customers on shared compute/DB | **MT** (identity, membership, data isolation) |

Do not claim `shared_saas` scalability from `dedicated` evidence. Do not infer tenant scope from PERF measurements alone.

## Required dimensions

Every scalability-gate evidence pack must address these dimensions. Until measured, mark `status=unmeasured` and leave numeric fields null/unknown.

### D1 — Per-customer resource envelope

| Field | Meaning |
|---|---|
| `cpuCoresReserved` | CPU allocation assumed per customer (or shared pool fraction) |
| `memoryMbReserved` | RSS/working-set envelope for API + workers |
| `diskGbReserved` | DB + export/temp storage envelope |
| `postgresConnectionsBudget` | Max connections expected for that customer under peak |
| `cacheFootprintMbBudget` | Application cache memory envelope |
| `status` | `unmeasured` \| `measured` \| `deferred` |

### D2 — Concurrent request / load assumptions

| Field | Meaning |
|---|---|
| `concurrentUsersAssumed` | Concurrent interactive users for the milestone |
| `concurrentAnalyticsReadsAssumed` | Concurrent warm analytics read requests |
| `representativeRoutes` | B1/B2/B7 surfaces from baseline contract |
| `p50Ms` / `p95Ms` | Latency under the stated concurrency |
| `errorRate` / `timeoutRate` | Separate from latency percentiles |
| `coldWarmMatrix` | Explicit process × cache state (baseline contract rules) |
| `status` | `unmeasured` \| `measured` \| `deferred` |

**Citeable context (not G10/G50 proof):** PERF05 M-tier warm B1/B2 paths are fast on a single host; PERF08 shows backend cold first-useful analytics ≈16 s p50 and warm second bootstrap ≈75 ms. Those are single-load markers, not concurrency envelopes.

**First dedicated pack (PERF10):** `.ai/runs/2026-08-12-PERF10-evidence.md` — warm concurrent reads (10×3) p95 ≈469 ms, peak DB connections 4; D1 partial; D4–D7 deferred; D8 n/a.

### D3 — Database connection pressure

| Field | Meaning |
|---|---|
| `poolSizeConfigured` | API/worker pool settings used |
| `peakActiveConnections` | Observed peak during the pack |
| `waitOrTimeoutCount` | Connection wait/timeout events |
| `statementTimeoutHits` | Query cancel/timeout count |
| `status` | `unmeasured` \| `measured` \| `deferred` |

### D4 — Worker concurrency

| Field | Meaning |
|---|---|
| `workerTypesInScope` | Aggregation, nightly refresh, prewarm, import claimers |
| `maxParallelJobs` | Configured concurrency |
| `queueDepthPeak` | Peak pending work |
| `cycleP95Ms` | One representative cycle latency |
| `retryOrPoisonCount` | Retries / dead-letter signals |
| `status` | `unmeasured` \| `measured` \| `deferred` |

Align with baseline family **B5** and OBS worker-SLA contracts when those evidence packs exist. Do not invent worker SLOs here.

### D5 — Cache footprint

| Field | Meaning |
|---|---|
| `keysOrEntriesPeak` | Peak cache cardinality for the run |
| `estimatedMbPeak` | Estimated memory footprint |
| `hitRate` | When instrumentation exists |
| `invalidationPolicyId` | Pointer to cache/stale policy doc |
| `status` | `unmeasured` \| `measured` \| `deferred` |

### D6 — Import overlap

| Field | Meaning |
|---|---|
| `importJobsOverlapping` | Concurrent import/preview/run count |
| `overlapWithAnalyticsReads` | Whether analytics reads ran during import |
| `overlapWithWorkers` | Whether aggregation/nightly ran during import |
| `failureOrBackoffObserved` | Honest failure/backoff signals |
| `status` | `unmeasured` \| `measured` \| `deferred` |

Align with baseline family **B4** and QDB/OBS import evidence ownership. PERF does not redefine import correctness.

### D7 — Report / export bursts

| Field | Meaning |
|---|---|
| `burstConcurrency` | Parallel report/export jobs |
| `payloadSizeBytes` | Representative export size |
| `p95Ms` | Burst latency under concurrency |
| `memoryPeakMb` | Process RSS during burst |
| `status` | `unmeasured` \| `measured` \| `deferred` |

Align with baseline family **B6**. Ops report warm/cold targets remain **targets only**.

### D8 — Tenant isolation overhead (shared SaaS only)

| Field | Meaning |
|---|---|
| `tenantCountInFixture` | Customers in the shared fixture |
| `crossTenantLeakChecks` | Pass/fail of isolation assertions |
| `overheadP95Ms` | Extra latency vs single-tenant baseline on same hardware |
| `mtGateIds` | MT prompts/contracts that authorize the claim |
| `status` | `unmeasured` \| `measured` \| `deferred` \| `n/a_dedicated` |

For `dedicated` mode, set `status=n/a_dedicated`. Do not fabricate shared-SaaS overhead without MT-approved fixtures.

## Evidence pack template

Record one pack per milestone × deployment mode:

```text
packId:
commit:
datetimeUtc:
milestone: G10|G50
deploymentMode: dedicated|shared_saas
machine:
datasetTier: S|M|L
seedRecipeId:
prewarmEnabled:
workersEnabled:
dimensions:
  D1: status=...
  D2: status=...
  D3: status=...
  D4: status=...
  D5: status=...
  D6: status=...
  D7: status=...
  D8: status=...
correctnessChecks: pass|fail
notes:
residualRisks:
```

Attach raw JSON under `.ai/runs/` with the same metadata rules as `PERFORMANCE_BASELINE_CONTRACT.md`.

## Correctness co-assertions (mandatory)

Scalability samples are invalid for gate promotion if any fail:

1. **No fake zero** on analytics error paths.
2. **Empty ≠ error** when the dataset is honestly empty.
3. **Partial/stale/fallback visible** — never masked as healthy success.
4. **Timeout ≠ healthy** — cancel/timeout must surface as failure/warning.
5. **Tenant isolation** — for `shared_saas`, cross-tenant leak checks must pass (MT-owned assertions).
6. **Cold/warm honesty** — do not average cold-start with warm steady-state to “pass” a gate.

## Mapping to existing PERF evidence

| Dimension | Existing citeable evidence | Gap for G10/G50 |
|---|---|---|
| D2 latency (single load) | PERF05 warm B1/B2; PERF08 cold vs warm bootstrap | No concurrent multi-user pack |
| D4 workers | Baseline B5 family defined; OBS08 worker SLA contract (docs) | No throughput/queue-depth pack |
| D5 cache | Baseline B3; prewarm flags in PERF harnesses | No multi-customer cache footprint |
| D6 import | Baseline B4 deferred in PERF05 | No overlap pack |
| D7 export | Baseline B6 | No burst pack |
| D8 isolation | MT program | Blocked until MT fixtures/gates |
| D1/D3 envelopes | Unmeasured | Need envelope + connection-pressure pack |

## Non-claims

- PERF08 cold-start (~16 s first useful analytics) is a **startup** risk, not a concurrency envelope.
- Ops budget tables are **engineering targets**, not scalability gate SLOs.
- Passing G10 does not imply G50.
- Passing `dedicated` does not imply `shared_saas`.

## Acceptance (PERF09)

- scalability gate dimensions D1–D8 are explicit with measurement placeholders;
- milestones G10/G50 and deployment modes are named;
- existing PERF05/PERF08 evidence is cited without inventing multi-customer SLOs;
- no runtime optimization or harness semantics change shipped by this contract alone.
