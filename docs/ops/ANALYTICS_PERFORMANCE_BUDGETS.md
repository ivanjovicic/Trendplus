# Analytics Performance Budgets

This document defines performance expectations for demo-critical analytics flows.

It is a budget and operations guide, not an implementation claim. It does not optimize SQL, change cache code, or promise that every environment already meets these targets.

Definitions:

- Warm: cache hit, precomputed response, or already-refreshed dataset on a normal repeat request.
- Cold: cache miss, first request after deploy/refresh/import, or a path that must compute the response without a warm cache.

## Budget table

| Endpoint family | Warm target | Cold target | Cache expected | Risk | Notes |
|---|---|---|---|---|---|
| dashboard/bootstrap | <2s | <5s | Yes | Critical | First screen in a demo. If this is slow, confidence drops immediately. |
| product decision | <3s | <8s | Yes | Critical | Must stay fast enough to support the recommendation story and "why" discussion. |
| supplier scorecard | <3s | <8s | Yes | Critical | Supplier walkthrough should not stall. Slow scorecard is a direct demo blocker. |
| inventory | <3s | <8s | Yes | Critical | Inventory decisions must feel operational, not batch/report driven. |
| data quality | <3s | <10s | Yes | High | Slightly slower is acceptable, but trust and freshness context must still load reliably. |
| pre/post nivelacija | <4s | <12s | Recommended | High | Heavy analytical slice. Acceptable only if the delay is explained and consistent. |
| reports | cached <5s | <15s | Yes | High | Cached report open/export should feel stable. Cold report generation must not surprise a demo. |

## What to measure per request

At minimum, every monitored analytics request should capture:

- duration
- cache hit or miss
- row count
- timeout
- correlationId

Operational note:

- If duration is recorded without cache state, the metric is not actionable enough for demo readiness.
- If timeout happens without correlationId, troubleshooting is too slow during a customer-facing incident.

## Demo rule

- Warm cache and run refresh before a sales demo when the story depends on dashboard, scorecard, product decision, inventory, or report flows.
- Do not begin a live demo on a known cold path if the likely cold response time is near or above the budget.
- If warm-up is not possible, the presenter should explicitly avoid cold-only flows or switch to a prepared report/snapshot path.

## Demo blockers

Treat the following as demo blockers until they are mitigated or the flow is avoided:

- dashboard/bootstrap exceeds the cold target or repeatedly misses the warm target after warm-up
- supplier scorecard, product decision, or inventory exceeds the warm target during rehearsal
- cache state is unknown before the demo for a flow that depends on warm response times
- repeated timeouts occur without clear correlationId-based diagnostics
- report generation requires a cold path that is likely to exceed 15 seconds

## Top optimization candidates

If budgets are missed, investigate these areas first:

1. Dashboard/bootstrap fan-out and any sequential dependency chain before the first useful paint.
2. Product decision payload size, explanation joins, and unnecessary fields loaded before the main table can render.
3. Supplier scorecard ranking/score aggregation, especially when fallback or trust metadata expands the response.
4. Inventory endpoints with large row counts, wide joins, or work that should be precomputed.
5. Pre/post nivelacija queries that should rely on pre-aggregation, materialized data, or narrower result sets.
6. Report generation paths that should open from cached/snapshot output instead of computing on demand during the demo.

## Scope boundary

- This document sets expectations and operational guardrails only.
- It does not change SQL, cache invalidation, endpoint contracts, or timeout configuration in this commit.