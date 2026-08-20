# Operational Dashboard Panel Inventory and Correlation Contract

Status: authoritative OBS11 docs-only contract  
Date: 2026-08-20  
Related:

- `docs/architecture/OBSERVABILITY_OPERATIONAL_DASHBOARD_HONESTY_CONTRACT.md`
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`
- `docs/architecture/OBSERVABILITY_WORKER_SLA_EVIDENCE_CONTRACT.md`
- `docs/architecture/OBSERVABILITY_IMPORT_SLA_EVIDENCE_CONTRACT.md`
- `docs/architecture/OBSERVABILITY_ANALYTICS_SLA_EVIDENCE_CONTRACT.md`
- `docs/planning/QUEUE_REFILL_2026-08-20.md`

## Purpose

Freeze which operational dashboard panels may appear under each OBS10 honesty layer, which correlation fields they require when present, and how missing panels or missing correlation stay `unknown`/`warn` instead of green or `0`.

This contract is safe to ignore. No dashboard UI ships from this prompt.

## Non-goals

- no runtime dashboard product
- no vendor, alerting, or SLA numbers
- no new metric catalog beyond existing OBS SLI IDs
- no tenant-scoped panels until MT authorizes them
- no business analytics KPI panels (margin, revenue, recommendation rates)

## Allowed panel inventory

| Layer (OBS10) | Allowed panel IDs | Required when present | Missing panel rule |
|---|---|---|---|
| Business readiness | `panel.business.readiness_status`, `panel.business.blockers`, `panel.business.evidence_links` | readiness verdict + evidence path | omit or `unknown`; never invent `healthy` |
| API / analytics | `panel.api.request_health`, `panel.analytics.freshness_age`, `panel.analytics.partial_fallback` | freshness/timestamp from analytics SLA evidence when shown | missing freshness → `unknown`, not `0 seconds` |
| Import / connectors | `panel.import.last_success_age`, `panel.import.partial_cancelled`, `panel.import.source_scope` | import SLA last-success or explicit unknown | no success → `unknown`, not `0` |
| Workers / runtime | `panel.worker.enabled_state`, `panel.worker.heartbeat_age`, `panel.worker.backlog_age`, `panel.worker.last_success_age`, `panel.worker.retry_dlq` | worker SLA evidence fields when shown | missing depth/age → `unknown`; disabled ≠ healthy |
| Database / cache | `panel.db.dependency_ok`, `panel.cache.hit_miss`, `panel.cache.invalidation` | existing baseline/catalog evidence only | missing → `unknown`; do not infer from API success |

Panels not listed above are out of inventory. A later prompt must add them explicitly.

## Correlation fields

When a panel shows a concrete event, job, request or import instance, it may include:

| Field | Meaning | Missing rule |
|---|---|---|
| `correlationId` | request/job correlation already owned by OBS | stay absent/`unknown`; do not invent |
| `importJobId` / `runId` | import or worker run identity when the owning contract exposes it | omit; do not fabricate |
| `sourceStream` / `connectionId` | connector scope when QDB evidence exposes it | omit; do not invent tenant ids |

Correlation is optional decoration on evidence that already exists. Absence of correlation is not failure and is not green.

## Tenant dimensions

- Dedicated deploy: `TenantScope=n/a_dedicated` remains the only allowed scope label until MT authorizes more.
- Do not add customer/tenant pickers, tenant IDs from caller headers, or shared-SaaS panel dimensions in this contract.

## Forbidden panels / behaviors

1. Revenue, margin, sell-through, recommendation acceptance rate, or other business analytics KPIs on the operational dashboard.
2. Green summary tiles built from missing panels.
3. `0` ages, depths, or counts when the source field is absent.
4. Mixing Decision Pulse / GenAI chat health into these layers without a later owner prompt.
5. Frontend-invented correlation IDs.

## Status vocabulary reuse

Reuse OBS10 words only: `healthy`, `warn`, `blocked`, `unknown`.

## Acceptance

- allowed panel inventory is citeable per honesty layer;
- missing panel/correlation honesty is explicit;
- no runtime UI or schema change in OBS11.
