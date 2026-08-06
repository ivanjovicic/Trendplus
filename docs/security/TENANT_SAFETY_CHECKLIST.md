# Tenant Safety Checklist

Updated: 2026-08-05
Current shared-SaaS verdict: **NOT READY**
Current supported customer-isolation mode: **one deployment/database/storage/cache scope per customer**

## Canonical documents

- Architecture and phase order: `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md`
- Executable prompts: `docs/ai/MULTITENANCY_PROMPT_QUEUE.md`
- Current endpoint-role protection plan: `docs/security/ANALYTICS_ACCESS_CONTROL_IMPLEMENTATION_PLAN.md`
- Data-source portability: `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md`

This checklist is the operational safety summary. It does not replace the architecture roadmap or implementation evidence.

## Current pilot recommendation

Until the shared-SaaS gate is complete:

- use one deployment per customer;
- use a separate PostgreSQL database per customer where practical;
- use a separate import/file/object-storage root per customer;
- use a separate cache namespace or Redis instance per customer;
- bind deployment secrets and the transitional Admin API key to only that customer;
- do not place two real customers in the same `TrendplusDbContext`, Redis namespace, file root, worker/outbox scope or source-connector catalog.

Dedicated infrastructure is the current mitigation because `TenantId` is not yet systematically enforced through the runtime.

## Current evidence

| Area | Tenant-safe today? | Evidence/risk | Required action |
|---|---:|---|---|
| Canonical tenant identity | No | no application-wide `TenantId`/`ITenantContext` contract | `MT01` |
| Request resolution | No | no trusted membership-to-tenant resolver | `MT02` |
| Authentication/roles | Partial | Phase 1 global Admin API-key mode; roles are not tenant-membership-scoped | `STAB04/STAB05`, then `MT02` |
| Tenant catalog/membership | No | no durable tenant lifecycle or membership model | `MT03` |
| EF Core entities/queries | No | tenant-sensitive entities do not consistently carry/filter `TenantId` | `MT04` |
| Raw SQL/Dapper/bulk | No | no systematic required tenant parameter/negative matrix | `MT05` |
| Unique keys/FKs | No | source/business unique keys can collide across future customers | `MT04/MT05` |
| Analytics cache keys | No | customer-derived keys use global `analytics:*` namespaces | `MT06` |
| Cache invalidation | No | prefix invalidation is global to known keys | `MT06` |
| Outbox/background jobs | No | messages/jobs are not systematically tenant-owned | `MT07` |
| Import files/batches | No | upload/staging/jobs are not tenant-scoped by contract | `MT07` |
| Connector profiles/checkpoints | No | future connection/mapping/checkpoint identity does not yet require tenant ownership | `MT07` plus `QDB04-QDB06` |
| Report snapshots | No | future snapshot plan mentions TenantId later, not as a current persistent invariant | `MT08` |
| Documents/exports/storage | No | metadata and paths are not systematically tenant-scoped | `MT08` |
| Logs/errors/audit | Partial | customer correlation/masking is incomplete | `MT09` |
| Provisioning/suspension/delete | No | no tenant lifecycle workflow | `MT09` |
| Backup/restore | Partial | environment-level runbooks exist; tenant-level restore isolation is unproven | `MT09` |
| Cross-tenant test suite | No | no mandatory two-tenant negative release gate | `MT10` |
| Quotas/features/metering | No | later SaaS operations work | `MT11` |

## Non-negotiable identity rules

- `TenantId` is an immutable internal UUID/GUID.
- Empty/default tenant is invalid for tenant-owned operations.
- Tenant slug/domain/name is routing/display metadata, not ownership identity.
- `StoreId`/`IDObjekat` belongs inside a tenant and is not `TenantId`.
- User ID, source connection ID, file path and report ID are not tenant authority.
- A public `X-Tenant-Id` or query parameter is never trusted without server-side membership validation.
- Tenant Admin is scoped to one tenant. Platform support/admin is a separate audited capability.
- The transitional Admin API key is safe only in a dedicated single-customer deployment or when server-bound to exactly one tenant.

## Request and authorization checklist

Before a tenant-owned endpoint reaches a service/query:

- [ ] authenticated subject or approved internal service identity exists;
- [ ] tenant is resolved server-side;
- [ ] subject/service is authorized for that tenant;
- [ ] route/host/header hint agrees with membership;
- [ ] role/capability is evaluated inside the tenant;
- [ ] resource record belongs to the same tenant;
- [ ] mismatch returns a non-disclosing denial;
- [ ] denial is safely auditable;
- [ ] public health/version endpoint does not create a fake tenant.

Frontend tenant selection and hidden controls are never sufficient authorization.

## Persistence checklist

For every tenant-owned entity family:

- [ ] non-null `TenantId` after deterministic backfill;
- [ ] EF global query filter;
- [ ] write guard sets/verifies tenant before commit;
- [ ] explicit tenant predicate in raw SQL/Dapper/bulk paths;
- [ ] unique keys include tenant dimension where required;
- [ ] foreign keys cannot connect different tenants;
- [ ] update/delete includes tenant condition;
- [ ] `IgnoreQueryFilters()` is reviewed and tested;
- [ ] two tenants with identical business keys are in integration tests;
- [ ] unresolved or mismatched context fails closed.

Do not introduce a permanent “missing tenant means pilot/default tenant” fallback.

## Cache checklist

- [ ] every customer-derived key starts with `tenant:{tenantId}:`;
- [ ] L1, Redis and stampede locks use the same namespace;
- [ ] version tokens and report namespaces are tenant-scoped;
- [ ] tenant invalidation cannot evict another tenant;
- [ ] platform-global invalidation is a separate stronger capability;
- [ ] empty/unresolved tenant cannot read/write/invalidate;
- [ ] tests use identical filters for Tenant A/B and prove different values;
- [ ] cache logs expose no secrets or row payloads.

Current global `analytics:*` keys are a blocker for shared SaaS.

## Jobs, workers, outbox and import checklist

- [ ] message/job/outbox/import batch stores non-empty `TenantId`;
- [ ] payload tenant agrees with stored tenant;
- [ ] each execution creates a fresh tenant/DI scope;
- [ ] no tenant context leaks between sequential jobs;
- [ ] retry/claim/idempotency identity includes tenant;
- [ ] one tenant failure cannot advance another checkpoint;
- [ ] source connection/mapping/checkpoint is tenant-owned;
- [ ] import file/staging/rejection path is tenant-owned;
- [ ] suspended tenant cannot enqueue/start new work;
- [ ] scheduler fan-out creates one isolated job per tenant.

Background workers must never infer tenant from the last request or process-global mutable state.

## Documents, reports, exports and storage checklist

- [ ] DB metadata includes `TenantId`;
- [ ] object/file path is under `tenants/{tenantId}/...`;
- [ ] lookup verifies record ID plus tenant;
- [ ] opaque ID/path alone never authorizes download;
- [ ] signed URL is minted only after tenant authorization and expires;
- [ ] customer-defined templates are tenant-owned;
- [ ] cleanup/retention operates only inside one tenant prefix;
- [ ] equal file/report names across tenants do not collide;
- [ ] cross-tenant list/download/delete/traversal tests pass.

## Logs, errors, audit and support checklist

- [ ] structured events contain safe tenant correlation;
- [ ] no secrets, connection strings, source rows or full report payloads;
- [ ] security denials include actor/tenant/correlation safely;
- [ ] support/break-glass access includes actor, tenant, reason and timestamp;
- [ ] incident responders can enumerate operations affecting one tenant;
- [ ] errors do not disclose another tenant's resource existence;
- [ ] metric labels remain bounded as tenant count grows.

## Provisioning and offboarding checklist

Provisioning:

- [ ] immutable tenant record;
- [ ] initial membership/service binding;
- [ ] locale/time zone/currency;
- [ ] storage/cache roots;
- [ ] required seed/config;
- [ ] feature set/quotas;
- [ ] connector/import readiness;
- [ ] idempotent retry and audit evidence.

Suspension/deletion:

- [ ] block new interactive and scheduled work;
- [ ] stop jobs/imports before cleanup;
- [ ] apply retention/legal hold;
- [ ] revoke secrets;
- [ ] delete/anonymize DB, cache, storage, documents, exports and allowed logs;
- [ ] deletion cannot target another tenant;
- [ ] completion evidence contains no customer payload.

## Mandatory two-tenant negative scenarios

Use Tenant A and Tenant B with intentionally identical SKUs, source row IDs, report names and filters.

- [ ] A cannot read/update/delete B through EF;
- [ ] A cannot cross through raw SQL/Dapper/bulk operations;
- [ ] A cannot select B through slug/header/route hint;
- [ ] identical cache inputs never share a value;
- [ ] A invalidation preserves B;
- [ ] sequential A/B worker execution does not leak context;
- [ ] retries/outbox remain tenant-bound;
- [ ] source connection/mapping/checkpoint IDs cannot be guessed cross-tenant;
- [ ] document/export IDs and paths cannot be used cross-tenant;
- [ ] cleanup/retention for A preserves B;
- [ ] suspended/deleting A cannot affect B;
- [ ] deployed smoke passes for the exact release SHA/version.

## Shared-SaaS release gate

Two customers may share a Trendplus data plane only when:

- `MT02`–`MT10` are complete for every enabled production surface;
- current STAB security/release P0 work is complete;
- backend CI executes and the two-tenant isolation suite is green;
- deployed cross-tenant smoke is tied to the exact release;
- security review has no unresolved P0 tenant finding.

Anything less remains:

> **BLOCKED for shared SaaS — use a dedicated deployment/database/storage/cache scope per customer.**

## Operational stop rules

Stop and report rather than implementing when:

- tenant source of truth is unclear;
- a proposed change trusts caller-supplied tenant identity;
- a task combines auth, all migrations, caches, workers and storage in one diff;
- migration/backfill cannot deterministically identify the pilot tenant;
- tests contain only one tenant;
- a raw SQL path cannot prove tenant binding;
- a worker depends on ambient HTTP context;
- a file/report is authorized by path or opaque ID alone;
- a default tenant fallback would remain active in shared mode;
- provider/deployment evidence needed for restore or cross-tenant smoke is unavailable.
