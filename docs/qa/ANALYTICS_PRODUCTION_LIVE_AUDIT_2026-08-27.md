# Trendplus production analytics live audit — 2026-08-27

Status: evidence snapshot, not a live router  
Canonical follow-up: `STAB16` (BLOCKED) and `RQ128` (WAITING) in their owner queues

## Scope and method

This audit used the production route configured by the frontend, `https://trendplus-api.onrender.com`, plus unauthenticated read-only API responses. It did not mutate data. Direct database reconciliation was intentionally not attempted because the local `TRENDPLUS_AUDIT_DATABASE_URL` read-only connection was absent. Browser-render verification was also unavailable in this environment after the in-app browser asset initialization failed; a static frontend bundle fetch is not treated as UI proof.

## Observed production state

| Surface | Observation | Assessment |
|---|---|---|
| Runtime | `/health` and `/ready` returned HTTP 200; ready reported `dbOk=true`. `/api/runtime/version` returned commit `d9c4d0a8cd893c8e7cb330f47e41e92843fa9875`. | Available, but behind current main. |
| Refresh/workers | `/api/analytics/refresh-status?dataScope=all` returned process `web`, `workersEnabled=false`, `dataFreshnessStatus=unknown`, an in-memory-cache warning, and no successful job evidence. Six jobs reported that a worker is not registered in the web process. | Blocking operational gap. Heavy jobs belong in a dedicated `PROCESS_TYPE=worker` process, not web. |
| Data quality | The 180-day health response reported score 100/excellent, revenue 11,146,820 RSD, missing cost/supplier 0, and no open issues/trend points. | A bounded health check is good, but it does not prove source freshness or all screen contracts. |
| Inventory | Inventory balance reported 12,422 SKUs, 3,566 on hand, 11,885 out of stock (95.7%), and estimated value 93,389 RSD. | Operationally material; must be reconciled with source data and refresh provenance. |
| Product Decision Center | Retry response reported 12,422 analyzed, 50 returned/visible and 12,372 ignored rows. Of the 50 visible rows, 44 were `INSUFFICIENT_DATA` and six `WATCH`, while every row had `recommendationAllowed=true`; insufficient rows carried warning/critical freshness evidence. | Confirmed false-actionability contract defect on the deployed version. Current main now fails closed; production has not deployed it. |
| Decision Board | Response was partial, with a data-quality warning and `supplier_summary_unavailable`; it contained urgent, impact, stock-risk, blocker, and action sections. | Graceful partial response, not a complete trusted decision surface. |
| Supplier report | Supplier report carried critical trust state and `recommendationAllowed=false`, with period 2011-01-20 through 2026-06-06. | Visible degradation is correct, but the period is stale/unsuitable for an actionable current recommendation. |

## Data reliability verdict

Production analytics is **not pilot-ready for trusted actions**. Availability and bounded data-quality checks work, but deployment parity and refresh provenance are missing, and the deployed Product Decision Center contradicts its own evidence by allowing insufficient/stale signals to look actionable.

Current evidence score: **3.5 / 10**.

- Availability: 7/10 — health/readiness and several endpoints respond.
- Data correctness evidence: 4/10 — one health window is strong, but no direct source reconciliation exists.
- Freshness/operability: 1/10 — no registered worker and unknown freshness.
- Decision/action safety: 2/10 on deployed runtime — PDC actionability contradicts insufficient evidence; current main contains the repair but production does not.
- UI proof: not scored — static bundle fetch succeeded, but no browser interaction/render proof was available.

## Required next proof

1. `STAB16`: deploy exact current main to the canonical Render service and restore a separate `PROCESS_TYPE=worker` service; prove durable refresh timestamps without enabling heavy workers in web.
2. Supply `TRENDPLUS_AUDIT_DATABASE_URL` only as a local, read-only connection and reconcile documented endpoint windows with source totals/counts.
3. `RQ128`: after exact deploy, prove source-blocked, insufficient, critical, stale, and unknown PDC rows have `recommendationAllowed=false`, `confidenceScore=null`, and `expectedImpactRsd=null` across PDC and Decision Board.
4. Run an authenticated browser smoke of the configured frontend and record actual loaded/error/empty/degraded states; do not infer that static HTML or bundle delivery proves rendering.

## Limits

- No production data, schema, or provider configuration was modified.
- No secrets, DSNs, access tokens, or customer-level records are included here.
- The Fly fallback was not treated as canonical after it timed out; the frontend configuration identifies Render as the primary API route.
