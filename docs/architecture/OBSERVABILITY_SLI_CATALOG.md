# Observability SLI / SLA / Correlation Catalog

Status: authoritative OBS01 contract
Date: 2026-08-11
Roadmap: `docs/roadmaps/OBSERVABILITY_ROADMAP.md`
Related:

- `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md` (OBS05 authoritative service-level terms)
- `docs/architecture/PERFORMANCE_BASELINE_CONTRACT.md`
- `docs/qa/ANALYTICS_OBSERVABILITY_REVIEW.md`
- `docs/qa/ANALYTICS_SQL_OBSERVABILITY_TIMEOUTS.md`
- `docs/Analytics/ANALYTICS_CACHE_INVALIDATION_AND_STALE_POLICY.md`

## Purpose

One catalog for what Trendplus should measure, who owns it, how Import / Analytics / Worker evidence differs, and how correlation IDs connect flows.

This contract is documentation only. It does not choose a paid vendor, publish contractual SLA numbers, or add broad runtime instrumentation.

## Vocabulary

| Term | Meaning |
|---|---|
| SLI | Measured service-level indicator. Always has source, owner, unit and dimensions. |
| SLO | Internal operating target for an SLI. Optional until baseline evidence exists. |
| SLA | External or contractual commitment. Not authorized here without business approval and baseline evidence. |
| Unknown | Telemetry absent, stale or unparseable. Unknown is never green. |
| Correlation ID | Flow identifier for diagnosis. Not authorization, tenant identity or user identity. |

## Non-goals

- no contractual latency or availability percentages invented here
- no secrets, connection strings or customer row payloads in telemetry
- no treating page render time as analytics freshness
- no shared-SaaS tenant dimension until MT authorizes it

## Service-level vocabulary

Authoritative OBS05 glossary for API / import / analytics / worker / report terms, measurement boundaries and error-budget discussion rules:

- `docs/architecture/OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md`

This catalog keeps the short summary below for SLI row readers. Prefer the OBS05 file when naming availability, import SLA, analytics freshness SLA, worker SLA, report SLA or error budgets.

Latency is measured in the OBS-2 / OBS04 contract. The table names the service-level words that wrap measured evidence and keeps SLI, SLO and SLA distinct.

| Service area | Measured evidence | Internal target word | External commitment word | Notes |
|---|---|---|---|---|
| API | ready/health success rate, error rate | SLO | SLA only when externally approved | availability is distinct from latency |
| Import | accept/start/complete timestamps, latest success age | SLO | SLA only when business commits | measure from accept/queue to durable completion |
| Analytics | refresh duration, freshness age, partial/fallback state | SLO | SLA only when business commits | never infer freshness from render time |
| Worker | enabled state, heartbeat age, backlog age, retry/DLQ rate | SLO | SLA only when business commits | disabled-by-policy is explicit, not healthy |
| Report | generation success/failure, age, partial/export warnings | SLO | SLA only when business commits | report/export failure is visible, not hidden |

Error budgets are only discussed after a target is adopted. Until then, keep measured SLI language separate from contract language.

## Authoritative SLI families

Every SLI row uses: `ID`, `Name`, `Unit`, `Source surface`, `Owner`, `Dimensions`, `Unknown behavior`.

### A. API / process health

| ID | Name | Unit | Source | Owner | Dimensions | Unknown behavior |
|---|---|---|---|---|---|---|
| A1 | Process ready | bool + reason | `GET /ready` | OBS/STAB | env, processType | warming_up or degraded = non-green |
| A2 | Dependency DB ok | bool + latencyMs | `GET /health/dependencies` | OBS | db=default/analytics | missing check = unknown/non-green |
| A3 | HTTP request rate | req/s | perf logs / future metrics | OBS/PERF | routeFamily, method, statusClass | no samples in window = unknown |
| A4 | HTTP error rate | errors / requests | same | OBS/PERF | routeFamily, statusClass | unknown if denominator missing |
| A5 | HTTP latency | ms p50/p95/p99 | same + PERF01 protocol | OBS/PERF | routeFamily, cold/warm | unknown is not 0 ms |
| A6 | Runtime version | commit/env | `GET /api/runtime/version` | OBS/STAB | env | missing commit = unknown build evidence |

### B. Import / connector

| ID | Name | Unit | Source | Owner | Dimensions | Unknown behavior |
|---|---|---|---|---|---|---|
| I1 | Latest successful import age | seconds | batch `CompletedAtUtc` where status=completed | OBS/QDB | sourceSystem, batchId | no success = unknown/critical |
| I2 | Import in-flight age | seconds | now minus `StartedAtUtc` for running | OBS/QDB | batchId, status | heartbeat missing beyond threshold = stale |
| I3 | Import accepted-to-complete latency | ms/s | `QueuedAtUtc` to `CompletedAtUtc` | OBS/QDB | sourceSystem, outcome | incomplete jobs excluded from success latency |
| I4 | Import failure rate | failed / started | batch statuses | OBS/QDB | sourceSystem | unknown if starts not recorded |
| I5 | Import cancel/partial rate | count/rate | cancel + partial statuses | OBS/QDB | sourceSystem | explicit, not folded into success |
| I6 | Cursor last success age | seconds | `AccessImportCursors.LastRunCompletedAtUtc` | OBS/QDB | tableKey | null = unknown for that table |

Import SLA boundary: operational evidence that a source ingest completed successfully within an agreed freshness window for a named source or scope. Measurement starts at accept/queue and ends at durable completed success, not at UI render.

### C. Analytics freshness / refresh

| ID | Name | Unit | Source | Owner | Dimensions | Unknown behavior |
|---|---|---|---|---|---|---|
| R1 | Data freshness status | enum | `GET /api/analytics/refresh-status` | OBS/RQ | dataScope | unknown = non-green |
| R2 | Last successful refresh age | seconds | `LastSuccessfulRefreshAtUtc` | OBS/RQ | job | null = unknown |
| R3 | Refresh failure rate | failed / attempted | refresh run history | OBS/RQ | job | missing history = unknown |
| R4 | Refresh duration | ms/s | run history | OBS/RQ | job, cold/warm | timeout is not success |
| R5 | Response meta DQ status | enum | `AnalyticsResponseMetaDto.dataQualityStatus` | OBS/RQ | routeFamily | null or missing must not look healthy |
| R6 | Partial/fallback rate | partial responses / total | meta.isPartial / warnings | OBS/RQ | routeFamily | unknown if meta absent |
| R7 | Analytics fact presence | counts | `GET /api/analytics/health` | OBS/RQ | table | zero may be true empty; pair with meta or emptyReason |

Analytics SLA boundary: evidence that analytics read models are refreshed from known provenance within a freshness policy, with stale, partial or error states remaining visible. UI must use backend freshness fields, never clock-at-render.

### D. Worker / runtime control

| ID | Name | Unit | Source | Owner | Dimensions | Unknown behavior |
|---|---|---|---|---|---|---|
| W1 | Workers globally enabled | bool | workers control/health | OBS/STAB | env | unknown if control API fails |
| W2 | Worker heartbeat freshness | seconds | `LastHeartbeat` | OBS/STAB | workerName | >10m without heartbeat = stale/error |
| W3 | Worker status mix | counts | `/api/workers/health` | OBS/STAB | status | missing inventory = unknown |
| W4 | Worker last error present | bool/count | LastError | OBS/STAB | workerName | empty error is not healthy if stale |
| W5 | Queue depth / oldest work age | count / seconds | gap - not first-class today | OBS/STAB | workerName | unknown until instrumented |
| W6 | Retry / dead-letter rate | rate | gap for most workers | OBS/STAB | workerName | unknown is not zero |

Worker SLA boundary: evidence that required background processors are enabled, heartbeating and completing work without silent backlog growth. Disabled-by-policy is explicit, not green.

### E. Cache

| ID | Name | Unit | Source | Owner | Dimensions | Unknown behavior |
|---|---|---|---|---|---|---|
| C1 | Cache hit ratio | hits / lookups | gap - policy docs exist | OBS/PERF | family | unknown if not recorded |
| C2 | Stale-served rate | stale responses / total | meta freshness warnings | OBS/RQ | family | stale is not fresh success |
| C3 | Prewarm success | bool/age | prewarm hosted service | OBS/PERF | probe | failed prewarm = cold-path risk |

### F. Decision / action / outcome

| ID | Name | Unit | Source | Owner | Dimensions | Unknown behavior |
|---|---|---|---|---|---|---|
| D1 | Action outcome measurement coverage | measured / executed | actions summary + RL/DT contracts | OBS/RL/DT | sourceType | not_measured explicit |
| D2 | Explainability field coverage | rows with required reason/confidence | PDC/DEX surfaces | OBS/DEX | family | missing reason = incomplete |
| D3 | Timeline gap rate | gaps / timelines | future DT projection | OBS/DT | family | gap is not an inferred event |

### G. Security / tenancy

| ID | Name | Unit | Source | Owner | Dimensions | Unknown behavior |
|---|---|---|---|---|---|---|
| S1 | Authz deny rate on admin surfaces | denies / attempts | admin auth logs | OBS/STAB/SEC | route | never log secrets |
| S2 | Cross-tenant negative evidence | pass/fail | MT tests/incidents | MT/SEC | n/a until shared SaaS | absent is not proven isolation |

## Latency, error, freshness, throughput dimensions

| Dimension | Applies to | Notes |
|---|---|---|
| Latency | A5, I3, R4, worker durations | Use PERF01 cold/warm plus p50/p95 rules |
| Error | A4, I4, R3, W4 | Timeouts are errors or warnings per route contract, not empty success |
| Freshness | I1, I6, R1, R2, W2 | Age from authoritative timestamps only |
| Throughput | A3, import rows/sec, worker jobs/hour | Pair with correctness co-assertions |

## Correlation-ID lifecycle

### Current state

| Channel | Identifier | Notes |
|---|---|---|
| HTTP | `X-Correlation-ID` request/response | Created or accepted by exception/logging middleware |
| Analytics meta | `correlationId` | Copied into client error/refresh UI when present |
| Refresh runs | `CorrelationId` on recent runs | May be absent if no recent run |
| SQL/request context | `RequestId` = TraceIdentifier; `TraceId` = Activity | Not identical to HTTP correlation header |

### Target propagation rules

1. Inbound HTTP: accept or create `X-Correlation-ID`; echo on response.
2. Logs/problem details: include the same correlation ID when safe.
3. Import batch/job: store correlation at accept; child steps inherit.
4. Workers/outbox: carry correlation from enqueue into processing logs.
5. Analytics refresh/report/export: persist correlation on run records.
6. Decision timeline/outcome: attach correlation to lifecycle events without using it as auth.
7. Never treat correlation ID as tenant ID, user ID or admin capability.

### Gap to close in OBS02+

- unify HTTP correlation vs `RequestLogContext.TraceId` naming in docs and instrumentation
- end-to-end trace from API -> import -> worker -> refresh is not yet a product guarantee

## Existing telemetry vs gaps

### Present

- Health/ready/dependencies endpoints
- Access-import batch lifecycle timestamps
- Refresh-status plus analytics response meta
- Worker health heartbeats plus runtime enable switch
- HTTP performance logging plus correlation on many error paths
- Frontend surfacing of correlation on analytics errors and refresh warnings

### Gaps

- productized p50/p95 SLI time series (logs are not the same as a product SLI)
- import accepted-to-complete histograms and success-age alerts
- worker queue depth, oldest-work age and DLQ rates
- cache hit/miss metrics
- distributed tracing across process boundaries
- single dashboard that fails closed on unknown
- tenant-safe dimensions, blocked on MT for shared SaaS

## Proposed dashboard layers

Dashboards summarize evidence; they do not replace source events.

1. Business readiness - freshness, last import age, DQ health, decision coverage
2. API latency/error - A3/A4/A5 by route family
3. Import/connectors - I1-I6
4. Workers/runtime - W1-W6
5. Database/cache - dependency latency, C1-C3
6. Decision/action/outcome - D1-D3, after DT/RL surfaces exist

### Unknown / non-green rules for dashboards

- Missing panel data -> unknown/WARN, never green.
- `dataFreshnessStatus=unknown` -> non-green.
- Workers disabled by policy -> explicit paused state, not healthy success.
- No successful import ever -> critical/unknown for I1, not "0 seconds old".

## Mapping to OBS roadmap phases

| Phase | Catalog coverage |
|---|---|
| OBS-1 | This document |
| OBS-2 | A5 plus PERF01 protocol |
| OBS-3 | `OBSERVABILITY_SERVICE_LEVEL_VOCABULARY.md` (OBS05) + summary table above |
| OBS-4 | I1-I6 |
| OBS-5 | R1-R7 |
| OBS-6 | W1-W6 |
| OBS-7 | Dashboard layers |
| OBS-8 | Tracing gap -> OBS02 plan |
| OBS-9 | Correlation lifecycle section |

## Acceptance

- one SLI/SLA/correlation catalog exists
- import, analytics and worker evidence boundaries are explicit
- every listed metric has source, owner, unit and dimensions
- unknown telemetry is explicitly non-green
- correlation IDs are not auth or tenant identity
- no contractual SLA numbers or vendor choice introduced
