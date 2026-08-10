# Trendplus Multi-Tenancy Prompt Queue

Created: 2026-08-05
Repository: `ivanjovicic/Trendplus`
Queue purpose: introduce tenant isolation in reviewable phases while preserving the current safe dedicated-customer pilot model.
Current READY prompt: `none`

## Global routing

This queue is the canonical owner for tenant identity, tenant resolution, tenant-owned persistence, cache/job/storage isolation, lifecycle and the shared-SaaS release gate.

Priority rules:

1. `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` P0 work remains first. `STAB04`/`STAB05` security work is not displaced by this queue.
2. `MT01` is a small application-contract/tests task and may run in parallel with unrelated P0 work when exact paths do not collide.
3. `MT02`–`MT10` are mandatory before two real customers share one Trendplus data plane.
4. Until `MT10` is `DONE`, supported customer isolation is one deployment/database/storage/cache scope per customer.
5. Connector prompts `QDB01`/`QDB02` may proceed when path-safe. Persistent connection/mapping/checkpoint work (`QDB04`–`QDB06`) must include or wait for the corresponding tenant-ownership contract.
6. GenAI work may operate in the current dedicated-customer mode, but any shared-SaaS GenAI/RAG/tool surface must wait for tenant authorization, storage and retrieval isolation.
7. One prompt per branch/commit unless the prompt explicitly allows a bounded documentation update.

## Governance rules

- Follow `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.
- Use only `READY`, `WAITING`, `IN_PROGRESS`, `BLOCKED`, `PARTIAL`, `DONE`, `OBSOLETE`.
- Create a local uncommitted lock under `.ai/task-locks/`.
- Before claim, inspect current `main`, open PRs, remote branches and exact changed-path ownership.
- Never put real customer data, tenant secrets, API keys, connection strings or report payloads in tests/evidence.
- Tenant selection must fail closed. Do not introduce a permanent default-tenant fallback.
- `StoreId`, `IDObjekat`, user ID, source connection ID and file path are not tenant identity.
- An external `X-Tenant-Id` or query parameter is never authoritative without server-side membership validation.
- Do not use frontend visibility as tenant authorization.
- Do not implement all entity families or workers in one migration.
- If the current backend workflow fails before tests execute, route the CI failure to `BACKEND_CI_REPAIR_PROMPT_QUEUE.md` and record this task as `PARTIAL` rather than claiming green validation.
- A prompt may be marked `DONE` only with exact code/tests/evidence for its bounded scope.

## Shared-SaaS stop rule

Stop any plan to put two customers in one database/cache/storage/job scope when one of these is absent:

- trusted tenant resolution;
- tenant-owned persistence and write guards;
- tenant-qualified cache keys;
- tenant-owned background work;
- tenant-scoped documents/files/exports;
- two-tenant negative tests.

The safe fallback is a dedicated deployment per customer.

---

## MT01 - Add the canonical TenantId and tenant-context contract seam

Status: DONE
Priority: P1 now; P0 prerequisite before shared SaaS
Type: application contract/tests/docs
Feature family: tenant-context-contract
Parallel-safe: yes, when no task owns the exact Application tenancy/test/doc paths
Owner: Cursor
Local lock: removed after DONE
Commit suggestion: `feat(tenancy): add tenant context contract`

### Why

The repository discusses future `TenantId`, but no canonical value type or application-layer context contract exists. Starting with migrations, middleware or global query filters before fixing the identity vocabulary would allow different modules to invent incompatible nullable strings, GUIDs, store IDs or headers.

This is the smallest useful implementation. It creates a fail-closed type seam and durable contract documentation without changing production request behavior, DI, database schema, cache keys or workers.

### Current evidence

- `docs/security/TENANT_SAFETY_CHECKLIST.md` says every query/cache/report/job must eventually be tenant-scoped but records that `TenantId` is not systemically introduced.
- `TrendplusDbContext` maps tenant-sensitive operational, analytics, outbox and document records without a canonical tenant contract.
- `AnalyticsCacheKeys` builds global `analytics:*` keys.
- Current production authorization is a transitional Admin API-key boundary, not a tenant membership pipeline.
- `StoreId`/`IDObjekat` already has business meaning and must not be reused as tenant identity.

### Fixed contract

- `TenantId` is an immutable internal GUID/UUID value.
- `Guid.Empty`/default is invalid.
- Canonical formatting is stable lowercase/standard `D` GUID format or another explicitly documented single format.
- Tenant-owned operations do not expose a usable tenant when context is unresolved.
- The interface distinguishes resolved from unresolved state without returning an accidental default tenant.
- No resolver accepts `StoreId`, user ID or arbitrary header value in this prompt.
- This task adds no ambient static/global tenant state.

### Scope only

- `Application/Common/Tenancy/TenantId.cs`;
- `Application/Common/Tenancy/ITenantContext.cs`;
- `Api.Tests/TenantContextContractTests.cs`;
- new `docs/architecture/TENANT_CONTEXT_CONTRACT.md`;
- `docs/ai/MULTITENANCY_PROMPT_QUEUE.md` completion note/status only.

Maximum changed files: 5.

### Do not touch

- `Api/Program.cs`;
- authentication/authorization middleware;
- endpoint handlers;
- `TrendplusDbContext` or migrations;
- domain entities;
- caches;
- workers/outbox/import code;
- frontend;
- deployment configuration.

### Read first

- `.github/copilot-instructions.md`;
- `AGENTS.md`;
- `docs/ai/AGENT_START_HERE.md`;
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`;
- `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md`;
- `docs/security/TENANT_SAFETY_CHECKLIST.md`;
- `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md`;
- `Application/Application.csproj`.

### Test-first contract

Mode: required.

Required first tests or equivalent explicit behaviors:

- `TenantId_EmptyGuid_IsRejected`;
- `TenantId_SameGuid_HasValueEquality`;
- `TenantId_ToString_IsCanonicalAndStable`;
- `TenantContext_Unresolved_DoesNotExposeDefaultTenant`;
- `TenantContext_Resolved_ExposesExactTenant`;
- `TenantId_IsNotConstructedFromStoreIdByContract`.

Red command:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~TenantContextContractTests
```

Red expectation: unchanged `main` has no canonical tenancy contract or focused suite.

Green command: the same focused command passes after adding only the contract/types/docs.

Counterexample proof: existing application and API projects still compile without DI/runtime registration.

### Do

1. Add a small immutable `TenantId` value type under `Application/Common/Tenancy`.
2. Reject an empty GUID at construction/conversion boundaries.
3. Add a minimal `ITenantContext` application contract that cannot silently return a default tenant when unresolved.
4. Keep resolver/mutable request state out of Application.
5. Add focused contract tests with simple test doubles where needed.
6. Document:
   - canonical identity;
   - resolved/unresolved semantics;
   - why store/user/source IDs are not tenant IDs;
   - why public headers are not authority;
   - expected consumers in later phases.
7. Record exact checks and remaining work in this queue.

### Checks

```powershell
git diff --check
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~TenantContextContractTests
dotnet build Application/Application.csproj --configuration Release
dotnet build Api.Tests/Api.Tests.csproj --configuration Release
node scripts/check-prompt-queues.mjs
```

### Acceptance

- one canonical tenant ID type exists;
- empty/default tenant cannot represent a real tenant;
- unresolved context fails closed;
- no runtime behavior, DI or database schema changed;
- focused tests document value/context semantics;
- contract documentation explicitly rejects StoreId/user/header as ownership authority;
- later prompts can depend on exact types rather than reinventing tenancy primitives.

### Completion note

- Date: 2026-08-09
- Agent: Cursor
- Changed files:
  - `Application/Common/Tenancy/TenantId.cs`
  - `Application/Common/Tenancy/ITenantContext.cs`
  - `Api.Tests/TenantContextContractTests.cs`
  - `docs/architecture/TENANT_CONTEXT_CONTRACT.md`
  - `docs/ai/MULTITENANCY_PROMPT_QUEUE.md`
- Checks:
  - `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~TenantContextContractTests` - pass (6/6)
  - `dotnet build Application/Application.csproj --configuration Release` - pass
  - `dotnet build Api.Tests/Api.Tests.csproj --configuration Release` - pass
  - `git diff --check` - pass
  - `node scripts/check-prompt-queues.mjs` - pass
- Notes:
  - Canonical `TenantId` rejects `Guid.Empty`; string form is GUID `D`.
  - Unresolved `ITenantContext` throws on `TenantId` access; no DI/runtime registration added.
  - Contract doc rejects StoreId/user/header as ownership authority.
- Remaining:
  - `MT02` stays WAITING until owner approves identity/membership source or single-tenant API-key binding.

### Stop conditions

- stop if production middleware, entity changes or migrations become necessary;
- stop if another task owns the same Application tenancy/test paths;
- stop at the fifth changed file;
- stop if making tests pass requires a service locator, static ambient state or default pilot tenant;
- stop after the same command fails twice for the same reason.

---

## MT02 - Implement trusted request tenant resolution and membership authorization

Status: WAITING
Ready after: `MT01` is `DONE` and the owner approves the production identity/membership source or an explicitly single-tenant API-key binding
Priority: P0 before shared SaaS
Type: backend security/contracts/integration tests
Feature family: tenant-resolution-membership
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/MT02-<agent>.lock.md`
Commit suggestion: `feat(tenancy): resolve trusted tenant context`

### Goal

Resolve tenant scope from server-validated identity/membership before tenant-owned endpoint work, without trusting an arbitrary caller header.

### Required boundaries

- scoped immutable request tenant context;
- authenticated subject -> server-side tenant membership;
- route/host/header may be only a hint validated against membership;
- transitional Admin API key maps to exactly one configured tenant and cannot select arbitrary tenant;
- public health/version endpoints remain tenant-neutral;
- no database-wide entity migration in this prompt.

### Required proof

- missing identity/tenant -> deny;
- user member of Tenant A cannot select Tenant B;
- conflicting route/header/membership -> deny and audit safely;
- correct membership resolves exact tenant;
- context does not leak between sequential requests/scopes;
- 401/403 behavior is intentional and non-disclosing.

---

## MT03 - Add tenant catalog, membership model and default-pilot provisioning migration

Status: WAITING
Ready after: `MT02` is `DONE`, migration owner approves the model and backend CI can execute migration tests
Priority: P0 before shared SaaS
Type: backend persistence/migration/integration tests
Feature family: tenant-catalog-provisioning
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/MT03-<agent>.lock.md`
Commit suggestion: `feat(tenancy): add tenant catalog and membership`

### Goal

Create the control-plane tenant/membership records and deterministically bind existing single-customer data/deployment to one pilot tenant.

### Required boundaries

- immutable Tenant UUID, unique mutable slug, lifecycle status;
- membership joins subject + tenant + role;
- no secrets/customer payloads in tenant catalog;
- migration is additive and reversible where practical;
- seed/backfill creates exactly one configured pilot tenant;
- no permanent “missing tenant means pilot tenant” runtime fallback;
- provisioning is idempotent.

### Required proof

- repeated provisioning does not duplicate tenant/membership;
- invalid/empty tenant is rejected;
- slug change does not change ownership identity;
- existing environment is bound to the exact pilot tenant;
- migration rollback/forward behavior is documented and tested.

---

## MT04 - Add tenant ownership, EF query filters, write guard and tenant-inclusive constraints

Status: WAITING
Ready after: `MT03` is `DONE`; owner selects one bounded entity family for the first migration
Priority: P0 before shared SaaS
Type: backend/domain/persistence/migration/integration tests
Feature family: tenant-persistence-isolation
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/MT04-<agent>.lock.md`
Commit suggestion: `feat(tenancy): isolate first entity family`

### Goal

Prove the full tenant persistence pattern on one bounded vertical slice before applying it to every table.

### First-slice selection rule

Choose one coherent low-collision family with API and integration-test coverage. Do not combine operational products/sales, analytics, imports, documents and outbox in one prompt.

### Required pattern

- non-null `TenantId` after deterministic backfill;
- EF global query filter;
- SaveChanges interceptor/guard sets or verifies tenant;
- mismatched tenant write fails;
- unique and foreign-key constraints include tenant dimension where needed;
- `IgnoreQueryFilters()` prohibited except reviewed platform operations;
- two-tenant tests with identical business keys.

### Required proof

- Tenant A cannot read/update/delete Tenant B;
- same business key may exist in A and B;
- duplicate key inside one tenant fails;
- relationship cannot link entities across tenants;
- unresolved context cannot query/write tenant-owned data.

---

## MT05 - Audit and enforce tenant scope in raw SQL, Dapper, bulk and endpoint resource lookups

Status: WAITING
Ready after: first `MT04` entity slice is `DONE` and tenant context is available
Priority: P0 before shared SaaS
Type: backend security/data access/integration tests
Feature family: tenant-query-enforcement
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/MT05-<agent>.lock.md`
Commit suggestion: `fix(tenancy): bind tenant in data access paths`

### Goal

Prevent tenant filter bypass outside ordinary EF LINQ and resource-ID lookups.

### Required boundaries

- inventory all `FromSql*`, `ExecuteSql*`, Dapper/ADO.NET, raw Npgsql and bulk operations for the selected entity family;
- require parameterized `TenantId`;
- include tenant in joins, subqueries, updates, deletes, staging/reconcile operations;
- resource endpoints verify record ID plus tenant;
- no existence disclosure for another tenant;
- add static guard/test where practical to prevent new unscoped SQL.

### Required proof

Two tenants with identical IDs/business keys cannot cross-read or cross-mutate through any selected raw/bulk/endpoint path.

---

## MT06 - Tenant-qualify analytics cache keys, locks and invalidation

Status: WAITING
Ready after: `MT02` is `DONE` and a tenant context can be injected into cache consumers; may be implemented before full entity migration only in a dedicated-customer-compatible way
Priority: P0 before shared SaaS
Type: backend cache/contracts/tests
Feature family: tenant-cache-isolation
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/MT06-<agent>.lock.md`
Commit suggestion: `fix(cache): isolate tenant namespaces`

### Goal

Replace global customer-derived `analytics:*` ownership with canonical tenant-qualified namespaces.

### Required boundaries

- typed tenant key builder;
- `tenant:{tenantId}:...` for L1/L2 and stampede locks;
- tenant-local version tokens and report namespaces;
- tenant-local prefix invalidation;
- explicit separate platform-global invalidation capability;
- no free-form caller prefix or accidental empty/default tenant;
- migration strategy for old cache entries is “miss and refill”, not unsafe fallback.

### Required proof

- identical filters in Tenant A/B produce different keys/values;
- A invalidation does not remove B;
- L1 and Redis behavior agree;
- unresolved tenant cannot get/set/invalidate tenant-owned entries;
- logs do not expose secrets/payloads.

---

## MT07 - Carry TenantId through jobs, outbox, imports, connectors and durable checkpoints

Status: WAITING
Ready after: `MT03` is `DONE`; coordinate with `QDB04`–`QDB06` and select one bounded asynchronous family first
Priority: P0 before shared SaaS
Type: backend/workers/persistence/integration tests
Feature family: tenant-async-import-isolation
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/MT07-<agent>.lock.md`
Commit suggestion: `feat(tenancy): isolate background work`

### Goal

Make asynchronous processing independent of ambient HTTP/process state.

### Required boundaries

- every tenant-owned message/job/outbox/import batch carries non-empty TenantId;
- each execution creates a fresh DI/tenant scope;
- retry/claim/idempotency identities include tenant;
- outbox stored tenant and payload tenant must agree;
- source connections/mappings/checkpoints are tenant-owned;
- import files/staging/rejections are tenant-owned;
- one scheduler may enumerate active tenants, but each tenant execution is separate and bounded.

### Required proof

- sequential Tenant A then B jobs cannot leak context;
- retry remains bound to original tenant;
- conflicting stored/payload tenant fails;
- identical source keys/checkpoints in A/B do not collide;
- suspended tenant cannot start new scheduled work;
- one tenant failure does not advance another tenant checkpoint.

---

## MT08 - Tenant-isolate documents, report snapshots, exports and object/file storage

Status: WAITING
Ready after: `MT03` is `DONE` and the relevant report/document authorization task is complete
Priority: P0 before enabling shared report/export surfaces
Type: backend/storage/security/integration tests
Feature family: tenant-document-storage-isolation
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/MT08-<agent>.lock.md`
Commit suggestion: `feat(tenancy): isolate reports and files`

### Goal

Bind DB metadata, storage location and authorization for every customer file/report artifact.

### Required boundaries

- TenantId on document/report/export records;
- storage path under `tenants/{tenantId}/...`;
- lookup verifies tenant even for globally unique IDs;
- signed URL only after authorization, with expiry;
- tenant-scoped templates unless explicitly platform-global;
- retention/cleanup stays inside tenant prefix;
- no path-only authorization or global output directory.

### Required proof

- Tenant B cannot list/download/delete Tenant A artifact;
- guessed ID/path and traversal attempts fail;
- signed link cannot be minted cross-tenant;
- cleanup for A preserves B;
- same file/report name in A/B does not collide.

---

## MT09 - Add tenant provisioning, suspension, deletion, audit and restore runbooks/workflows

Status: WAITING
Ready after: `MT03`, `MT07` and `MT08` establish the owned resources that lifecycle operations must control
Priority: P1; P0 before production shared-SaaS onboarding/offboarding
Type: backend/ops/docs/integration tests
Feature family: tenant-lifecycle-operations
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/MT09-<agent>.lock.md`
Commit suggestion: `feat(tenancy): add tenant lifecycle workflow`

### Goal

Make onboarding/offboarding idempotent, auditable and complete across DB, cache, jobs, secrets and storage.

### Required boundaries

- explicit lifecycle states;
- provisioning creates required ownership roots/config/membership safely;
- suspension blocks new interactive and scheduled work without deleting data;
- deletion respects retention/legal hold and stops jobs first;
- secret revocation and storage/cache cleanup are included;
- tenant-level export/restore is tested or explicitly unsupported;
- break-glass/support access is separately audited.

### Required proof

- repeated provisioning/deletion commands are safe;
- partial failure resumes from durable state;
- suspended tenant cannot enqueue work;
- delete cannot target another tenant;
- restore with equal business keys does not overwrite another tenant;
- completion evidence contains no customer payload.

---

## MT10 - Add the two-tenant isolation suite and shared-SaaS go/no-go release gate

Status: WAITING
Ready after: `MT02`–`MT09` are `DONE` for every enabled production surface
Priority: P0 before any two-customer shared deployment
Type: security/QA/integration/deployed smoke/docs
Feature family: tenant-release-gate
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/MT10-<agent>.lock.md`
Commit suggestion: `test(tenancy): add shared saas isolation gate`

### Goal

Prove end-to-end isolation with two tenants containing deliberately colliding business/source identifiers.

### Required matrix

- authentication/membership and wrong tenant hint;
- EF and raw SQL read/write/delete;
- cache hit and invalidation;
- analytics/report/action resource lookup;
- outbox/job/retry;
- import connection/mapping/checkpoint;
- documents/exports/download/storage cleanup;
- suspension/deletion behavior;
- deployed smoke tied to exact SHA/version.

### Acceptance

- automated CI suite fails on any cross-tenant access;
- exact deployed release passes the bounded smoke;
- no enabled surface relies only on docs/unit proof;
- security review has no unresolved P0 tenant finding;
- release evidence declares either:
  - `PASS — shared SaaS allowed for the proven surfaces`, or
  - `BLOCKED — dedicated deployment per customer remains required`.

---

## MT11 - Add per-tenant quotas, rate limits, feature flags and usage metering

Status: WAITING
Ready after: `MT10` is `DONE` and product/plan requirements are approved
Priority: P2
Type: backend/product/ops/tests
Feature family: tenant-saas-controls
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/MT11-<agent>.lock.md`
Commit suggestion: `feat(tenancy): add tenant plan controls`

### Goal

Add operational fairness and entitlement controls without weakening isolation.

### Scope preview

- API/import/worker concurrency limits;
- storage/document/export quotas;
- feature/plan entitlements;
- usage events with tenant and idempotency identity;
- safe operator visibility;
- no billing-provider integration until metering truth is proven.

---

## MT12 - Support enterprise dedicated database/deployment topology without forking application contracts

Status: WAITING
Ready after: shared application tenant contracts are stable and a named enterprise customer justifies the topology
Priority: P2
Type: architecture/deployment/ops/tests
Feature family: tenant-enterprise-topology
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/MT12-<agent>.lock.md`
Commit suggestion: `docs(tenancy): plan dedicated enterprise topology`

### Goal

Keep `TenantId`, authorization, audit and lifecycle behavior consistent while allowing selected customers to use a dedicated database/deployment/network boundary.

### Required boundaries

- no separate business-code fork;
- tenant-to-deployment/database routing is server-controlled;
- migrations and backup/restore are orchestrated per deployment;
- connection pools/secrets are bounded and rotated;
- platform operations cannot accidentally route one tenant to another database;
- topology choice does not change API tenant authorization semantics.

## Completion definition

Trendplus is multi-tenant-ready only when it can prove:

- canonical tenant identity;
- trusted tenant resolution and membership authorization;
- tenant-owned persistence and query/write guards;
- cache, job, outbox, import and checkpoint isolation;
- document/report/export/storage isolation;
- lifecycle, backup/restore and audit behavior;
- two-tenant automated and deployed negative tests;
- an exact release gate permitting shared SaaS.

Having a `TenantId` column or a tenant selector in the UI alone is not multi-tenancy.
