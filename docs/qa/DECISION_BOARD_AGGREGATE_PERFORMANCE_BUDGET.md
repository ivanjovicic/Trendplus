# Decision Board Aggregate Performance and Cache Budget

Date: 2026-06-22T10:13:50+02:00
Local HEAD: `992e89775e9703756c8fa2777328bacbd38b4e6e`

## Scope

- [docs/qa/DECISION_BOARD_BACKEND_AGGREGATE_GATE.md](./DECISION_BOARD_BACKEND_AGGREGATE_GATE.md)
- [docs/qa/DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md](./DECISION_BOARD_CANDIDATE_CONTRACT_AUDIT.md)
- [docs/qa/DECISION_BOARD_DEDUPE_RULES.md](./DECISION_BOARD_DEDUPE_RULES.md)
- [docs/qa/DECISION_BOARD_RANKING_PARITY_PLAN.md](./DECISION_BOARD_RANKING_PARITY_PLAN.md)
- [docs/qa/DECISION_BOARD_FRESHNESS_CONTRACT.md](./DECISION_BOARD_FRESHNESS_CONTRACT.md)
- [docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md](./ANALYTICS_PRODUCTION_READINESS_STATUS.md)
- [docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md](./ANALYTICS_LIVE_SMOKE_RESULT.md)
- [Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx](../../Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx)
- [Klijent/clientapp/src/services/analyticsApi.ts](../../Klijent/clientapp/src/services/analyticsApi.ts)

## Goal

Define the minimum safe performance, cache, and failure-handling budget for any future Decision Board aggregate contract.

This document is intentionally conservative:

- it does not claim the board currently has a proven performance problem
- it does not authorize Q63
- it does not treat cache as a reason to hide stale or partial states

## Current Evidence

### What is already true

- Production readiness says analytics is usable, but cache/freshness is still `WARN`.
- Live smoke captured `dataFreshnessStatus=unknown`, workers disabled, and an in-memory cache warning on the live refresh-status surface.
- The live board route renders successfully in production.
- The current board page performs a single direct board fetch:
  - `getDecisionBoardAggregate({ dataScope: "all" })`

### Why this still does not justify Q63

Even though the runtime page currently uses a single aggregate request, the repo still lacks proof that:

- the board endpoint has a stable long-term contract
- snapshot caching is trustworthy under partial/stale inputs
- the board needs additional aggregate expansion for performance rather than for convenience
- aggregate failures can be diagnosed better than source-by-source failures

In other words:

- one request is not the same thing as one proven architecture

## Current Runtime Request Model

The current page behavior is:

1. page mounts
2. one blocking request is sent:
   - `GET /api/analytics/decision-board?dataScope=all`
3. trust header / loading state render while waiting
4. no secondary blocking board data requests are issued from the page itself

Implications:

- there is no visible browser-side fan-out bottleneck on the current page surface
- future performance work must justify itself against actual latency, operability, or cache-discipline evidence

## Request Budget

### Page-load request budget

For the board route itself, the budget should stay:

- 1 blocking aggregate data request per page load
- 0 required follow-up board data requests before first meaningful content
- 1 explicit manual retry path only after failure or operator action

### Concurrency budget

The board should avoid:

- duplicate in-flight aggregate requests for the same parameter set
- background retries that hide a failing source state

Safe behavior:

- dedupe identical in-flight requests
- allow one user-triggered retry
- expose the error if the retry also fails

### Payload budget

A future aggregate should not ship unlimited lane data by default.

Suggested safe default:

- top N candidates per lane only
- lane metadata summarizing omitted counts if needed later

Reason:

- performance wins disappear quickly if the aggregate becomes an unbounded mega-payload

## Latency Budget

These targets are intentionally modest and pilot-safe rather than highly optimized.

### Aggregate endpoint latency target

Recommended backend targets:

- p50: <= 800 ms
- p95: <= 2,000 ms
- p99: <= 4,000 ms
- hard timeout budget: 5,000 ms

### Page interaction target

Recommended frontend experience targets:

- loading state visible immediately
- meaningful trust shell visible within 250 ms
- board content or honest error/empty/partial state visible within 2.5 s in normal conditions

### Why these targets are conservative

- the board is decision-support, not a trading terminal
- trust and diagnosability matter more than shaving a few hundred milliseconds by hiding degraded states
- the current live evidence does not show a crisis-level latency problem

## Cache Budget

Cache must improve repeatability and reduce duplicate work without faking freshness.

### Cache key requirements

Any future board snapshot cache key must include all supported aggregate parameters:

- `fromDate`
- `toDate`
- `storeId`
- `supplierId`
- `dataScope`
- `category`
- `gender`
- `seasonId`
- `minRevenue`
- `onlyHighConfidence`
- `excludeOosBeforeMarkdown`
- `search`

If any of these are omitted from the key, the board risks cross-view leakage.

### Cache TTL policy

Recommended snapshot TTL policy:

- fresh snapshot:
  - soft TTL: 60 seconds
  - hard TTL: 300 seconds
- warning / unknown freshness snapshot:
  - soft TTL: 0 to 30 seconds
  - hard TTL: 60 seconds
- critical / blocking freshness state:
  - do not extend trust through cache
  - either bypass cache or cache only the warning/error shell for a very short TTL

### Why TTL must stay short

- the board mixes operational actions, blockers, outcomes, and freshness-sensitive inputs
- long TTLs would make the board look more stable than the underlying evidence really is
- production readiness already says the freshness/cache story is not fully clean

## Cache Invalidation Triggers

The board snapshot should be invalidated when any of these happen:

### 1. Analytics refresh changes source freshness materially

Invalidate on:

- successful refresh completion
- refresh failure that changes source warning state
- cache clear affecting analytics snapshots

### 2. Action workflow changes the board's lifecycle view

Invalidate on:

- action create/upsert
- action status update
- action outcome update

Reason:

- the board includes `actionsDecision` and `actionsOutcome`
- action state is part of candidate interpretation, not a side panel concern

### 3. Trust-blocker inputs change

Invalidate on:

- pilot readiness changes
- data quality health changes
- refresh-status changes
- dashboard freshness validation changes

### 4. Query-shape changes

Invalidate naturally by cache key on:

- any parameter change listed in the key contract

## Partial-Failure Behavior

The aggregate must not hide degraded upstream sources behind a green or empty-success response.

### PASSABLE partial response

A `200` partial response is acceptable when:

- at least one meaningful board section still has trustworthy content
- failed or stale sources are explicitly surfaced in:
  - snapshot warnings
  - source states
  - section-level or candidate-level warnings

Required:

- `meta.isPartial = true`
- aggregate warning codes present
- affected sources identifiable

### Non-passable partial response

A `200` partial response is **not** acceptable when:

- the aggregate silently drops a critical source and still looks healthy
- candidate counts collapse to zero with no warning
- stale data appears fresh
- blockers disappear because the source that raised them failed

### Hard failure threshold

Return error rather than partial-success when:

- the aggregate cannot produce any trustworthy sections
- trust metadata itself is missing or contradictory
- the board would otherwise render as false green

## Correlation ID and Error Behavior

### Required error contract

If the aggregate fails completely, the response path should support:

- stable error message
- `correlationId`
- machine-readable error code
- no secret leakage

### Required partial contract

If the aggregate partially succeeds, it should preserve:

- aggregate warning codes
- per-source warning state
- affected-source traceability
- a correlation ID if one or more source fetches failed internally

### Why correlation matters here

The board is an orchestration surface.
Without correlation-friendly failures, aggregate debugging gets harder, not easier.

## Snapshot Freshness Rules

The aggregate must not infer “fresh enough” from one healthy source.

Recommended rule:

- snapshot freshness is bounded by the worst blocking freshness signal among required sources

At minimum, the aggregate should explicitly decide:

1. what sources are board-critical
2. whether one critical stale source invalidates:
   - the whole board
   - one lane only
   - one candidate family only
3. whether unknown freshness is closer to:
   - warning
   - insufficient data
   - hard failure

Until those rules are encoded, cache must remain conservative.

## What Would Justify More Aggregate Performance Work

The repo should only treat aggregate performance work as justified if at least one of these becomes true with evidence:

### 1. Measured latency problem

Examples:

- p95 board load exceeds 2.5 to 3 seconds on normal pilot use
- repeat operator retries are caused by slow aggregate completion

### 2. Request amplification problem

Examples:

- the board or neighboring workflows begin reintroducing multi-call fan-out for one screen state
- duplicate requests for the same board parameters become common

### 3. Cache inconsistency problem

Examples:

- action updates do not reflect in the board quickly enough
- stale blocker state remains visible after source recovery
- different parameter sets accidentally share snapshots

### 4. Operability problem

Examples:

- aggregate errors are harder to trace than source-level errors
- lack of correlation data blocks production diagnosis

If none of those are proven, more aggregate performance work is architecture drift, not justified engineering.

## Minimum Acceptance Bar Before Q63 Can Use This Budget

A future aggregate path should not be considered performance-ready unless it can show:

1. a deterministic cache key over all supported parameters
2. conservative TTL behavior that does not mask stale/warning states
3. invalidation on refresh and action lifecycle changes
4. honest partial-failure semantics
5. correlation-friendly error behavior
6. measured evidence that the chosen budget is actually met or at least observable

## Conclusion

The safest current performance position is:

- keep the board honest
- keep cache TTLs short
- invalidate aggressively on trust-changing events
- require evidence before treating aggregate expansion as a performance necessity

That is enough to unblock later review work.
It is not enough to unblock Q63.
