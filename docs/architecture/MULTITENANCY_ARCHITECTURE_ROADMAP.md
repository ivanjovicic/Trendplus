# Trendplus Multi-Tenancy Architecture Roadmap

Updated: 2026-08-05  
Status: approved architecture direction; shared SaaS implementation is not yet release-ready  
Repository: `ivanjovicic/Trendplus`

## Executive decision

Trendplus is **not currently safe for multiple customers inside one shared application/database/cache/storage boundary**.

The supported pilot model remains:

- one customer per deployment;
- one PostgreSQL database per customer where practical;
- one storage/import root per customer;
- one cache namespace or Redis instance per customer;
- customer-specific deployment secrets and operational access.

The target product architecture is hybrid:

1. **Standard SaaS mode** — shared application and PostgreSQL database with mandatory `TenantId` isolation, tenant-scoped authorization, cache/storage/job isolation and cross-tenant tests.
2. **Enterprise/private mode** — dedicated deployment and database per customer, using the same application contracts but retaining stronger infrastructure isolation.
3. **Current pilot mode** — dedicated deployment/database/storage only, until the shared-SaaS release gate in this document is complete.

Do not onboard a second customer into the same database, Redis namespace, file root or background-processing scope before the P0 shared-SaaS gates are proven.

## Current repository verdict

| Area | Current state | Shared-SaaS readiness | Main risk |
|---|---|---:|---|
| Tenant identity | No canonical `TenantId` contract | Not ready | customer/store/user identifiers can be confused |
| Request tenant resolution | No authenticated membership-to-tenant resolver | Not ready | arbitrary or missing scope could reach shared data |
| Authorization | Transitional global Admin API key; no production identity pipeline | Not ready | roles are not evaluated inside tenant membership |
| EF Core model | Tenant-owned entities do not consistently carry `TenantId` | Not ready | queries and writes can cross customer boundaries |
| Unique constraints | Source/business keys generally omit tenant dimension | Not ready | equal external keys from two customers can collide |
| Raw SQL/Dapper | No systematic tenant-binding contract | Not ready | global queries can bypass future EF filters |
| Analytics cache | Keys are global `analytics:*` keys | Not ready | one tenant can receive another tenant's cached result |
| Cache invalidation | Prefix invalidation is global | Not ready | one tenant/admin action can evict every tenant |
| Workers/outbox | Job and outbox records are not systematically tenant-owned | Not ready | background processing can run under the wrong scope |
| Imports/connectors | Connection, mapping and checkpoint designs do not yet require tenant ownership | Not ready | credentials, source rows or checkpoints can collide |
| Documents/reports/exports | Records and paths are not systematically tenant-scoped | Not ready | report/download leakage |
| Logs/audit/metrics | Tenant correlation is partial/future | Not ready | incidents cannot be attributed or contained reliably |
| Provisioning/lifecycle | No tenant catalog/onboarding/offboarding contract | Not ready | incomplete setup or deletion |
| Cross-tenant tests | No mandatory negative matrix | Not ready | isolation regressions can ship undetected |

## Non-negotiable tenant identity contract

### Canonical identifier

Use an immutable internal UUID/GUID named `TenantId`.

Rules:

- `TenantId` is the ownership key for tenant data.
- A tenant slug, company name, hostname, domain or external customer code is mutable routing/display metadata, not the ownership key.
- `StoreId`/`IDObjekat` is a store dimension inside a tenant and must never substitute for `TenantId`.
- User ID is an identity subject and must never substitute for `TenantId`.
- Empty/default `TenantId` is invalid for tenant-owned reads, writes, cache entries, files, messages and jobs.
- A deliberate platform-global resource must be explicitly classified and reviewed; it must not use an empty tenant as an accidental convention.

Recommended first contract:

```csharp
public readonly record struct TenantId
{
    public Guid Value { get; }

    public TenantId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(value));

        Value = value;
    }

    public override string ToString() => Value.ToString("D");
}

public interface ITenantContext
{
    bool IsResolved { get; }
    TenantId TenantId { get; }
}
```

This is a target shape. The first prompt may add only the small value/context contract and tests; it must not invent request resolution or migrations in the same change.

### Tenant catalog

The future control-plane model should include:

```text
Tenant
- Id
- Slug
- DisplayName
- Status: provisioning | active | suspended | deleting | deleted
- DataRegion
- DefaultLocale / TimeZone / Currency
- CreatedAtUtc / ActivatedAtUtc / SuspendedAtUtc
- Plan / FeatureSet reference
- Concurrency token

TenantMembership
- TenantId
- SubjectId
- Role: Viewer | Analyst | Manager | Admin
- Status
- CreatedAtUtc / RevokedAtUtc

TenantAlias or TenantDomain
- TenantId
- Alias/domain
- VerifiedAtUtc
```

Do not put source database credentials, customer row data or report payloads in the tenant catalog.

## Request tenant resolution

The backend must resolve tenant scope before any tenant-owned service/query executes.

Allowed sources, in preferred order:

1. authenticated identity plus server-side tenant membership;
2. route/host tenant hint validated against that membership;
3. an internal service credential bound server-side to exactly one tenant;
4. a background message/job containing a validated `TenantId`.

Forbidden as authority:

- arbitrary public `X-Tenant-Id`;
- query-string tenant selection without membership validation;
- frontend-local tenant selection alone;
- `StoreId`, source connection ID, user ID or file path as implicit tenant identity;
- a global Admin API key that can choose any tenant supplied by the caller.

Transitional Admin API-key rule:

- the Phase 1 API key may be used only in dedicated single-customer deployments, or it must map in server configuration to exactly one tenant;
- it cannot grant platform-wide arbitrary tenant switching;
- moving to shared SaaS requires a real authentication/membership boundary or a narrowly controlled internal service identity.

Fail-closed behavior:

- unresolved tenant on a tenant-owned endpoint -> `401`/`403` or explicit tenant-resolution error;
- tenant hint not in the user's memberships -> `403`;
- tenant mismatch between route, identity, resource and message -> deny and audit;
- public health/version endpoints must not create a fake tenant context.

## Authorization model

Existing roles remain useful, but in shared SaaS they are always evaluated as:

```text
(subject, tenant, role/capability)
```

not as a global role alone.

Examples:

- `Viewer` in Tenant A has no access to Tenant B.
- `Admin` in Tenant A is not a platform admin.
- Platform support/admin is a separate audited capability, not a reused tenant `Admin` role.
- Frontend visibility is advisory; backend tenant membership and authorization are authoritative.
- Resource ownership checks must verify both `TenantId` and the required role/capability.

## Data ownership classification

Before migrations, every persistent entity/table must be classified as one of:

1. **Tenant-owned** — almost all customer operational, analytics, import, document and configuration data.
2. **Platform-global** — carefully reviewed control-plane data such as supported provider definitions or global feature metadata.
3. **Tenant-derived aggregate** — still tenant-owned even when generated by a worker.
4. **Ephemeral operational** — still tenant-scoped when it can reveal customer state.

Initial tenant-owned candidates include:

- products, suppliers, stores, seasons, sales, returns and change logs;
- analytics facts, summaries, recommendations, actions and refresh history;
- import jobs, batches, rejection/error rows and repair evidence;
- data-source connections, mappings, schema fingerprints and checkpoints;
- documents, templates when customer-defined, audits, exports and report snapshots;
- outbox messages, scheduled jobs and worker execution records;
- customer-specific runtime settings and feature configuration;
- errors/log records that reference customer data.

Any global exception needs an explicit reason, owner and negative test.

## Persistence strategy

### Target shared-database model

Use one PostgreSQL database with `TenantId` discriminator columns for the first SaaS scale stage.

Defense in depth:

1. tenant-aware application context;
2. EF Core global query filters for tenant-owned entities;
3. write guard that sets/verifies `TenantId` before durable commit;
4. tenant-inclusive primary/unique/foreign-key strategy where required;
5. explicit tenant predicates in Dapper/raw SQL;
6. integration tests proving Tenant A cannot read/update/delete Tenant B;
7. optional PostgreSQL Row-Level Security after the application contract is stable and connection-pooling/session-variable behavior is proven.

Do not rely on only one of these layers.

### Migration and existing data

Migration must be staged:

1. create tenant catalog and one default pilot tenant;
2. add nullable `TenantId` to a bounded entity family;
3. backfill existing rows deterministically to the pilot tenant;
4. add indexes/constraints and relationship checks;
5. make `TenantId` non-null;
6. enable query/write guards;
7. repeat by bounded entity family;
8. remove transitional default-tenant fallback before shared SaaS.

Never ship a permanent “missing tenant means default tenant” rule.

### Keys and relationships

- Unique keys for tenant-owned data include `TenantId`.
- Source identity becomes `TenantId + ConnectionId + MappingProfileId + SourceExternalKey`.
- Checkpoint identity becomes `TenantId + ConnectionId + MappingProfileId + SourceStream`.
- Report/document lookup verifies `TenantId` even when the record ID is globally unique.
- Cross-tenant foreign keys are impossible or rejected by application/database constraints.
- Deletes and bulk updates include tenant predicates.
- `IgnoreQueryFilters()` is restricted to reviewed platform operations with explicit tenant handling and tests.

### Raw SQL and Dapper

Every tenant-owned SQL path must:

- accept `TenantId` as a required parameter;
- bind it as a parameter, never string-concatenate it;
- include it in joins, subqueries, updates and deletes;
- preserve it in temporary/staging tables;
- have a negative test with identical business keys in Tenant A and Tenant B;
- fail closed when tenant scope is absent.

## Cache isolation

All customer-derived cache keys use a canonical namespace:

```text
tenant:{tenantId}:analytics:...
tenant:{tenantId}:reports:...
tenant:{tenantId}:imports:...
```

Rules:

- key builders accept `TenantId`, not a free-form caller prefix;
- L1 and Redis use identical tenant-qualified keys;
- single-tenant invalidation removes only `tenant:{tenantId}:...`;
- global invalidation is a separate platform operation with stronger authorization and audit;
- cache metadata/version tokens are tenant-scoped unless proven platform-global;
- stampede locks are tenant-qualified;
- logs may include a safe tenant ID/fingerprint but not secrets or row payloads;
- tests prove identical filters for two tenants produce distinct keys and values.

Current global `analytics:*` keys are a release blocker for shared SaaS.

## Background jobs, workers and outbox

Background work cannot depend on an ambient HTTP tenant.

Every tenant-owned job/message must carry:

- `TenantId`;
- job/message ID;
- correlation/causation ID;
- safe operation type;
- retry/idempotency identity;
- version/schema where needed.

Worker rules:

1. deserialize and validate `TenantId`;
2. create a fresh DI scope;
3. establish a non-mutable tenant context for that scope;
4. load only tenant-owned dependencies/data;
5. commit tenant-owned effects;
6. emit tenant-owned outbox/evidence;
7. clear/dispose scope before the next job.

Do not loop through all tenants in an ordinary request service. A platform scheduler may enumerate active tenants, but each tenant execution must become a separately isolated job with concurrency, retry and audit controls.

Outbox uniqueness, claiming and retry state must include tenant ownership. A worker must never process an outbox payload under a tenant inferred from current process state.

## Imports and data-source connectors

The connector roadmap remains read-only and provider-neutral, with these tenant additions:

```text
DataSourceConnection
- TenantId
- Id
- Provider
- SecretReference
- safe metadata...

SourceMappingProfile
- TenantId
- ConnectionId
- mapping fields...

SourceCheckpoint
- TenantId
- ConnectionId
- MappingProfileId
- SourceStream
- durable cursor...
```

Rules:

- connection IDs are always resolved within tenant scope;
- a tenant cannot test or browse another tenant's source profile;
- secrets are retrieved through tenant-authorized server-side references;
- import files/staging/batches/rejections are tenant-owned;
- checkpoint and idempotency identities include `TenantId`;
- connector discovery and preview endpoints require tenant membership plus Admin capability;
- QDB connector work that persists connections/mappings/checkpoints must not be declared shared-SaaS-ready until the corresponding MT tenant ownership prompt is complete.

## Documents, reports, exports and storage

Tenant-owned metadata and object paths must include tenant ownership.

Recommended storage layout:

```text
tenants/{tenantId}/imports/...
tenants/{tenantId}/documents/...
tenants/{tenantId}/reports/...
tenants/{tenantId}/exports/...
tenants/{tenantId}/temporary/...
```

Rules:

- DB record, storage object and authorization context must agree on `TenantId`;
- never authorize download from an opaque ID or path alone;
- signed URLs are created only after tenant/resource authorization and expire quickly;
- file names do not contain secrets;
- customer-defined templates are tenant-owned;
- retention/cleanup jobs operate inside one tenant scope;
- report snapshots require `TenantId` from their first shared-mode migration;
- export jobs cannot write to a global directory and later “look up” ownership by filename.

## Observability, audit and incident response

Include safe tenant correlation in:

- structured application logs;
- audit records;
- worker/import/report execution records;
- traces and correlation IDs;
- security denial events;
- operational dashboards.

Rules:

- do not log connection strings, access keys, source rows or full report payloads;
- avoid unbounded per-tenant metric labels when tenant count grows; use logs/traces or bounded tiers where appropriate;
- support an incident query for “all operations affecting Tenant X”;
- platform support impersonation/break-glass access requires reason, actor, tenant, timestamp and immutable audit;
- data returned to a tenant must not expose another tenant's identifiers through errors, counts or existence checks.

## Provisioning and lifecycle

Provisioning is an idempotent workflow, not a manual collection of SQL commands.

Minimum lifecycle:

```text
requested -> provisioning -> active -> suspended -> deleting -> deleted
```

Provisioning must establish:

- tenant catalog record and immutable `TenantId`;
- initial Admin membership or dedicated deployment binding;
- default locale/time zone/currency;
- storage prefixes/buckets/directories;
- cache namespace/version;
- required seed/config data;
- feature set and quotas;
- connector/import readiness state;
- audit evidence.

Suspension:

- blocks new interactive and scheduled tenant work;
- does not silently delete data;
- leaves explicit administrative recovery path.

Deletion/offboarding:

- requires authorization and confirmation;
- stops schedules/workers/imports first;
- applies retention/legal-hold rules;
- deletes or anonymizes DB, cache, storage, documents, exports, logs and secrets;
- creates safe completion evidence without retaining customer payloads.

## Backup, restore and data portability

For shared database mode:

- backups must preserve tenant relationships and encryption controls;
- normal restore is environment/database scoped;
- tenant-level restore/export requires a tested extraction/import workflow, not ad-hoc row copying;
- restored tenant data must not overwrite another tenant with equal business/source keys;
- object storage restore must preserve tenant prefixes and metadata;
- restore tests include post-restore query, cache, job and download isolation.

Enterprise dedicated deployments may use database-level restore, but application tenant contracts should remain present so behavior does not fork unnecessarily.

## Quotas, rate limits, feature flags and billing

These are P2 after correctness/isolation:

- API rate limits per tenant and subject;
- worker/import concurrency per tenant;
- storage/document/export quotas;
- source connector count and schedule limits;
- feature flags and plan entitlements resolved by tenant;
- usage/metering events carry `TenantId` and idempotency identity;
- billing failure never weakens data isolation.

Do not implement billing before tenant identity, lifecycle and audit are reliable.

## Threat model and required negative scenarios

At minimum test:

- Tenant A user requests Tenant B resource ID;
- Tenant A supplies Tenant B slug/header;
- same SKU/source key/report name exists in A and B;
- cache key inputs are identical across A and B;
- A invalidates cache while B has a warm value;
- A import connection/mapping/checkpoint ID is guessed by B;
- worker retries a Tenant A job after a Tenant B job in the same process;
- outbox payload tenant differs from stored record tenant;
- document/export ID from A is requested by B;
- storage path traversal or forged path crosses tenant prefix;
- raw SQL query omits tenant predicate;
- bulk update/delete affects only one tenant;
- suspended tenant cannot enqueue or run new work;
- platform support access is audited and cannot silently impersonate.

## Test strategy

### Contract/unit tests

- `TenantId` rejects empty/default values;
- tenant context is explicit and fail-closed;
- key/path builders require and normalize `TenantId`;
- role evaluation includes tenant membership;
- job/message contracts require tenant identity;
- no store/user/source ID is accepted as tenant ownership.

### Persistence integration tests

Use two tenants with intentionally identical business keys.

Prove:

- reads return only current tenant;
- inserts get current tenant;
- explicit mismatched writes fail;
- updates/deletes remain tenant-scoped;
- unique keys permit same business key in different tenants but reject duplicates inside one tenant;
- navigation and raw SQL paths cannot cross tenants;
- query filter bypass paths are reviewed and tested.

### API integration tests

- no tenant -> deny;
- wrong tenant membership -> `403`;
- resource ID from another tenant -> non-disclosing `404` or policy-defined denial;
- correct membership/role -> existing contract;
- admin API-key compatibility is single-tenant only;
- public health/version remains minimal and tenant-neutral.

### Cache/job/storage integration tests

- identical query filters produce separate tenant cache values;
- tenant-local invalidation does not evict another tenant;
- job scope does not leak between sequential messages;
- outbox and retry remain tenant-bound;
- report/document/export/download paths are isolated;
- cleanup and retention cannot cross tenant prefix.

### Release tests

A shared-SaaS deployment needs an automated cross-tenant smoke suite against the exact deployed version. Documentation or unit tests alone are insufficient.

## Prioritized delivery plan

## Phase 0 — Architecture and contract seam

Priority: P1 now; mandatory P0 prerequisite before shared SaaS.

Deliverables:

- this roadmap and upgraded safety checklist;
- canonical `TenantId` and `ITenantContext` contracts;
- contract tests proving fail-closed value semantics;
- no resolver, migration or production behavior change yet.

Queue owner: `MT01`.

## Phase 1 — Tenant resolution and membership

Priority: P0 before shared SaaS.

Deliverables:

- tenant catalog/membership decision;
- authenticated request resolution;
- transitional API-key single-tenant binding;
- no arbitrary external header authority;
- endpoint middleware/filter and negative tests.

Queue owner: `MT02`, then `MT03`.

## Phase 2 — Persistence isolation

Priority: P0 before shared SaaS.

Deliverables:

- default pilot tenant and staged backfill;
- TenantId on bounded entity families;
- EF query filters and write guard;
- tenant-inclusive constraints/FKs;
- raw SQL/Dapper audit and tests.

Queue owner: `MT04` and `MT05`.

## Phase 3 — Cache and asynchronous isolation

Priority: P0 before shared SaaS.

Deliverables:

- tenant-qualified cache keys and invalidation;
- tenant-owned outbox/messages/jobs;
- isolated worker scopes;
- tenant-owned imports/connections/mappings/checkpoints.

Queue owner: `MT06` and `MT07`.

## Phase 4 — Files, reports and lifecycle

Priority: P0/P1 before shared SaaS depending on enabled surfaces.

Deliverables:

- tenant-scoped document/report/export metadata and storage;
- provisioning/suspension/deletion;
- backup/restore/export runbooks and tested behavior;
- audit and incident correlation.

Queue owner: `MT08` and `MT09`.

## Phase 5 — Cross-tenant release gate

Priority: P0 before any shared-customer deployment.

Deliverables:

- two-tenant negative integration suite;
- deployed smoke evidence;
- no unresolved cross-tenant data, cache, job, storage or auth finding;
- explicit go/no-go record tied to exact commit/deployment.

Queue owner: `MT10`.

## Phase 6 — SaaS operations and enterprise topology

Priority: P2 after isolation correctness.

Deliverables:

- quotas, rate limits, feature flags and metering;
- platform support/break-glass workflow;
- optional dedicated database/deployment orchestration for enterprise customers.

Queue owner: `MT11` and `MT12`.

## Shared-SaaS release gate

Do not place two real customers in the same application data plane until all are true:

- [ ] production authentication can create a trusted subject;
- [ ] tenant is resolved from server-validated membership/service binding;
- [ ] no public caller-controlled tenant header is authoritative;
- [ ] all enabled tenant-owned tables have non-null `TenantId`;
- [ ] EF and raw SQL reads/writes are tenant-scoped;
- [ ] unique constraints and foreign keys prevent cross-tenant collisions;
- [ ] cache keys and invalidation are tenant-scoped;
- [ ] jobs, workers, outbox, imports and checkpoints carry tenant identity;
- [ ] documents, reports, exports and storage are tenant-scoped;
- [ ] provisioning, suspension, deletion and restore procedures are tested;
- [ ] two-tenant negative tests pass in CI;
- [ ] deployed cross-tenant smoke passes for the exact release;
- [ ] security review has no unresolved P0 tenant finding.

Until then, the release decision is **dedicated deployment per customer only**.

## Explicit non-goals for early phases

- microservices solely for tenancy;
- database-per-tenant orchestration before shared-database contracts are proven;
- global platform-admin impersonation without audit;
- client-selected arbitrary tenant headers;
- permanent default-tenant fallback;
- billing before isolation;
- PostgreSQL RLS as the only isolation layer;
- one huge migration covering every table and worker;
- automatic customer data movement between regions;
- cross-tenant analytics or benchmarking without a separate anonymization/consent design.

## Documentation and queue ownership

- This document owns the target multi-tenant architecture, phase order and release gate.
- `docs/security/TENANT_SAFETY_CHECKLIST.md` owns operational do/don't rules and current readiness.
- `docs/ai/MULTITENANCY_PROMPT_QUEUE.md` owns executable task sequencing.
- `docs/security/ANALYTICS_ACCESS_CONTROL_IMPLEMENTATION_PLAN.md` owns the current single-tenant Phase 1 role/endpoint protection plan.
- `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md` owns provider portability and must adopt tenant ownership before persistent shared-SaaS connector profiles.
- `docs/Analytics/REPORT_SNAPSHOT_PLAN.md` owns snapshot behavior and must require tenant ownership when persistence is implemented.
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` continues to outrank multi-tenancy runtime work while its P0 release/security tasks remain unresolved.

When runtime work changes an invariant here, update this roadmap, the safety checklist and the focused queue in the same delivery or record an explicit follow-up.
