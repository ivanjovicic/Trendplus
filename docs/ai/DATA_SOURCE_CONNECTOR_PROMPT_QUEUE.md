# Trendplus Data Source Connector Prompt Queue

Created: 2026-08-05  
Repository: `ivanjovicic/Trendplus`  
Queue purpose: evolve the existing Access reader into a safe multi-source import architecture without changing the internal PostgreSQL database or starting a broad rewrite.  
Current READY prompt: none (`QDB09` is IN_PROGRESS after the 2026-08-22 claim; `QDB07` stays WAITING until QDB09 and release gates clear)

## Global routing

This is a focused P1/P2 architecture queue. It does not outrank active P0 work.

Before claiming from this queue, confirm:

1. `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md` has no earlier repository-overlapping P0 `READY`, `IN_PROGRESS`, `PARTIAL` or `BLOCKED` task.
2. `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` has no earlier repository-overlapping P0 task.
3. The analytics reliability router has no earlier P0 task touching the same Access/import files.
4. There is no open PR, branch or local lock owning the prompt's exact feature family or paths.
5. Current Access import behavior is treated as shipped compatibility, not as disposable prototype code.

`QDB01` is docs/tests-only and may run in parallel with unrelated runtime work. Later prompts are sequential unless their own section explicitly says otherwise.
If this header says `Current READY prompt: none`, do not claim a later `WAITING` prompt. Treat the queue as parked until the blocker clears, or repair only the canonical routing metadata when that is the documented same-owner fix.

## Queue rules

- Follow `docs/ai/PROMPT_QUEUE_PROTOCOL.md` exactly.
- Use only `READY`, `WAITING`, `IN_PROGRESS`, `BLOCKED`, `PARTIAL`, `DONE`, `OBSOLETE`.
- One prompt per branch/commit unless a prompt explicitly permits a combined documentation update.
- Do not replace the internal PostgreSQL/Npgsql database in this queue.
- Do not add write-back, arbitrary SQL, CDC, Kafka, Debezium or bidirectional synchronization.
- Do not store or log real source credentials or customer row payloads.
- Provider support requires tests against the real engine; mocked ADO.NET tests alone are insufficient.
- If current code already satisfies a prompt, mark it `OBSOLETE` or narrow it based on current-main evidence instead of duplicating the owner.

---

## QDB01 - Characterize the current Access reader as a provider-neutral source contract

Status: DONE
Priority: P1
Type: architecture docs/tests
Feature family: data-source-connector-contract
Parallel-safe: yes, provided no other task owns the same Access test files
Owner: Cursor
Local lock: removed after DONE
Commit suggestion: `test(import): characterize source connector contract`

### Why

The current Access session abstraction already exposes the core behavior needed by future database connectors, but those guarantees are scattered across implementation and Access-specific tests. Renaming or adapting the interface without a compact behavioral safety net could silently change row-count truth, normalized lookup, cursor behavior or capability reporting.

This is the smallest useful first implementation: add a provider-neutral contract document and focused characterization tests without changing production runtime behavior.

### Current evidence

- `Api/Services/Access/IAccessDataReaderSession.cs` exposes table/column discovery, row-count mode, streaming rows and incremental read requests.
- `AccessDataSchema` resolves normalized aliases by ordinal rather than rebuilding dictionaries per lookup.
- `AccessRowCountResult` distinguishes `exact`, `sampled` and `unknown`.
- `WindowsAccessSession` reports predicate pushdown and builds parameterized ID/timestamp/composite cursor predicates.
- `MdbToolsCliSession` reports no predicate pushdown and uses the same logical session contract through CLI streaming.
- `Api.Tests/AccessReadQueryPushdownTests.cs` proves Windows SQL generation but does not define a complete provider-neutral compatibility contract.

### Fixed contract

The characterization must preserve these truths:

- table and column names remain source names, with normalized alias lookup as a separate concern;
- missing aliases return `false`/unknown rather than a fabricated value;
- exact, sampled and unknown row counts remain distinguishable;
- provider capabilities are explicit and never inferred from provider-name string comparisons in consumer tests;
- cursor modes preserve ID, timestamp, timestamp-plus-ID and full-scan fallback semantics;
- row streaming remains asynchronous and cancellation-aware;
- no real ODBC driver, `mdbtools` binary, customer file or database is required for this characterization task.

### Scope only

- new `docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md`;
- new `Api.Tests/DataSourceConnectorContractTests.cs`;
- `Api.Tests/AccessReadQueryPushdownTests.cs` only when a small extraction avoids duplicating existing assertions;
- `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` final notes/status.

Maximum changed files: 4.

### Do not touch

- `Api/Services/Access/IAccessDataReaderSession.cs`;
- `Api/Services/Access/WindowsAccessSession.cs`;
- `Api/Services/Access/MdbToolsCliSession.cs`;
- `AccessImportService` production behavior;
- import endpoints, workers, database entities or migrations;
- source credentials or deployment configuration.

### Read first

- `.github/copilot-instructions.md`;
- `AGENTS.md`;
- `docs/ai/AGENT_START_HERE.md`;
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`;
- `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md`;
- `Api/Services/Access/IAccessDataReaderSession.cs`;
- `Api/Services/Access/WindowsAccessSession.cs`;
- `Api/Services/Access/MdbToolsCliSession.cs`;
- `Api.Tests/AccessReadQueryPushdownTests.cs`.

### Test-first contract

Mode: required.

Reproducer:

- add focused tests that describe provider-neutral behavior using only the existing value objects and deterministic static seams;
- the initial red state is the absence of a single contract suite proving row-count truth, schema alias behavior and capability/cursor expectations together;
- do not make the red state depend on a missing native driver or CLI executable.

Required first test names or equivalent explicit behaviors:

- `SourceSchema_NormalizedAliasLookup_PreservesMissingAsUnknown`;
- `SourceRowCount_ModePreservesExactSampledAndUnknown`;
- `SourceCapabilities_DistinguishWindowsPushdownFromCliFallback`;
- `IncrementalCursor_CompositeTimestampAndId_RemainsDeterministic`;
- `IncrementalCursor_MissingAlias_FallsBackWithoutFakeCheckpointAdvance`.

Red command:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~DataSourceConnectorContractTests
```

Red expectation: the new contract suite/required assertions do not exist on unchanged current main.

Green command: the same focused command passes without production code changes.

Counterexample proof: existing `AccessReadQueryPushdownTests` continue to pass.

### Do

1. Inventory the exact public behavior of `IAccessDataReaderSession`, `AccessDataSchema`, `AccessDataRow`, `AccessReadQuery` and `AccessRowCountResult`.
2. Write `DATA_SOURCE_CONNECTOR_CONTRACT.md` using provider-neutral terms while explicitly mapping each term to the current Access type.
3. Add focused tests covering schema alias lookup, row-count modes, current capabilities and cursor fallback/composite behavior.
4. Reuse deterministic static helpers and in-memory values. Do not invoke ODBC or `mdbtools`.
5. Record which behaviors still cannot be tested without a future provider-neutral seam; make them acceptance items for `QDB02` rather than modifying runtime now.
6. Keep every assertion about existing behavior, not a speculative redesign.

### Checks

```powershell
git diff --check
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~DataSourceConnectorContractTests
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AccessReadQueryPushdownTests
dotnet build Api.Tests/Api.Tests.csproj --configuration Release
```

If repository CI is red before tests execute, follow the backend-CI queue and record `PARTIAL` rather than claiming full validation.

### Acceptance

- one durable contract document explains the current source-reader behavior in provider-neutral language;
- focused tests prove normalized alias lookup, row-count truth, capability difference and incremental cursor/fallback semantics;
- no production source file changes;
- no native Access dependency is needed for the focused tests;
- future refactor prompts can cite exact tests as compatibility gates;
- final notes record checks, remaining untested behavior and the next prompt.

### Completion note

- Date: 2026-08-09
- Agent: Cursor
- Changed files:
  - `docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md`
  - `Api.Tests/DataSourceConnectorContractTests.cs`
  - `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
- Checks:
  - `dotnet test --filter FullyQualifiedName~DataSourceConnectorContractTests|FullyQualifiedName~AccessReadQueryPushdownTests` - pass (11/11)
  - `git diff --check` - pass
  - no production Access runtime files changed
- Remaining untested (for QDB02+ / real engines):
  - live ODBC metadata/streaming
  - live mdbtools CLI metadata/streaming
  - exact vs sampled count selection against real engines
- Next: `QDB02` READY

### Stop conditions

- stop if a test requires ODBC, `mdbtools`, a real customer file or external database;
- stop if production behavior must change to make the characterization pass;
- stop if another task owns `AccessReadQueryPushdownTests.cs`;
- stop at the fourth changed file;
- stop after the same command fails twice for the same reason.

---

## QDB02 - Introduce provider-neutral source contracts through an Access compatibility adapter

Status: DONE
Ready after: `QDB01` is `DONE` and its focused characterization suite is green
Priority: P1
Type: backend refactor/tests
Feature family: data-source-connector-abstraction
Parallel-safe: no
Owner: Cursor
Local lock: removed after DONE
Commit suggestion: `refactor(import): add provider-neutral source session`

### Goal

Add provider-neutral `Source*` contracts and adapt the current Access implementations without changing import results, cursor behavior, source-file handling or current API responses.

### Scope direction

Expected new area:

- `Api/Services/DataSources/` abstractions;
- an Access adapter or compatibility interface;
- focused compatibility tests;
- minimal consumer migration only where needed to prove the seam.

### Required boundaries

- old Access consumers continue to compile or have a small explicit migration path;
- no broad namespace/file rename;
- provider-specific quoting and process/ODBC behavior remain inside Access code;
- no connection-profile persistence, endpoint or UI work;
- no database migration.

### Acceptance preview

- Access Windows and CLI modes satisfy the same provider-neutral session interface through an adapter;
- current Access import tests remain green;
- provider capabilities replace new provider-name switches;
- the diff is small enough to review as an abstraction seam, not a rewrite.

### Completion note

- Date: 2026-08-09
- Agent: Cursor
- Changed files:
  - `Api/Services/DataSources/ISourceDataSession.cs`
  - `Api/Services/DataSources/AccessSourceDataSessionAdapter.cs`
  - `Api.Tests/SourceDataSessionAdapterTests.cs`
  - `docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md`
  - `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
- Checks:
  - `dotnet test --filter FullyQualifiedName~SourceDataSessionAdapterTests|FullyQualifiedName~DataSourceConnectorContractTests|FullyQualifiedName~AccessReadQueryPushdownTests` - pass (16/16)
  - `git diff --check` - pass (queued files)
  - no Access ODBC/CLI runtime rewrite; import consumers still use `IAccessDataReaderSession`
- Next: `QDB03` is READY because `QDB02` is DONE and `BCI01`/`BCI05` have green GitHub Actions evidence.

---

## QDB03 - Add a read-only SQL Server proof connector

Status: DONE
Ready after: `QDB02` is `DONE` and `BCI01`/`BCI05` have green GitHub Actions evidence
Priority: P1
Type: backend/integration tests
Feature family: sqlserver-source-connector
Parallel-safe: no
Owner: Cursor
Local lock: removed after DONE
Commit suggestion: `feat(import): add sql server source connector`
Promotion note: 2026-08-13 - `BCI01`/`BCI05` are DONE per `MASTER_ROADMAP.md` with green GHA `31674533356` on `f1f5a17`, so the read-only SQL Server proof connector is unblocked.

### Goal

Implement the first non-Access provider against the provider-neutral contract.

### Fixed first scope

- `Microsoft.Data.SqlClient` read-only connection;
- connection test;
- schema/table/column discovery;
- safe identifier quoting;
- bounded asynchronous streaming;
- ID, timestamp and timestamp-plus-key cursors;
- real SQL Server integration tests, preferably Testcontainers;
- no UI, persisted credentials, arbitrary SQL or scheduled sync.

### Acceptance preview

- reserved identifiers, Unicode, nulls, decimals and timestamps are covered;
- cancellation and command timeout are proved;
- source credentials are not logged;
- full and incremental reads produce deterministic ordering;
- mocked tests are supplemental, not the provider-support proof.

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: read-only SQL Server `ISourceDataSession` landed; live LocalDB engine proved discovery, Unicode/nulls/decimals/timestamps, ID and timestamp-then-id cursors, cancellation and command timeout; quoting/SQL unit tests are supplemental
- Changed files: Api/Services/DataSources/SqlServerSourceDataSession.cs; Api/Services/DataSources/SqlServerIdentifier.cs; Api/Services/DataSources/SqlServerConnectionDiagnostics.cs; Api/Services/DataSources/ISourceDataSession.cs; Api/Api.csproj; Api.Tests/Api.Tests.csproj; Api.Tests/SqlServerSourceDataSessionSqlTests.cs; Api.Tests/SqlServerSourceDataSessionIntegrationTests.cs; docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md; docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md; docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md; MASTER_ROADMAP.md
- Checks run: git diff --check; dotnet test Api.Tests --filter FullyQualifiedName~SqlServerSourceDataSession (15 passed, live LocalDB); Access characterization 16 passed; node scripts/check-agent-instructions.mjs; node scripts/check-prompt-queues.mjs; node scripts/check-planning-architecture.mjs
- Checks not run: Testcontainers.MsSql path (Docker daemon was not running); full Api.Tests suite; npm frontend checks
- Run log: .ai/runs/2026-08-13-QDB03-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 355eccef9e792a7d43f480aa6a363a21cc9ad241
- Main verification: git rev-parse origin/main -> 096bf20d6908186cd3d7062ca6339c086522040f; work SHA 355eccef9e792a7d43f480aa6a363a21cc9ad241 is an ancestor
- Missed: Docker Testcontainers was not executed in this session; named discovery API is QDB04
- Follow-up: QDB04
- Residual risk: CI without LocalDB must use Docker Testcontainers or SQLSERVER_TEST_CONNECTION_STRING; ApplicationIntent=ReadOnly is requested but standalone LocalDB ignores Always On routing
- Prompt defect / scope repair: live proof used SQL Server LocalDB as equivalent real engine because Docker was unavailable; Testcontainers.MsSql remains the preferred CI path

---

## QDB04 - Add named source configuration and safe discovery endpoints

Status: DONE
Ready after: `QDB03` is `DONE`
Priority: P1
Type: backend/security/API tests
Feature family: data-source-connection-discovery
Parallel-safe: no
Owner: Cursor
Local lock: removed after DONE
Commit suggestion: `feat(import): add safe source discovery api`

### Goal

Allow an authorized administrator to select a named source, test connectivity and inspect schemas/tables/columns without importing rows or exposing secrets.

### Required boundaries

- start with environment-backed named profiles if durable secret storage is not yet approved;
- never return complete connection strings or credentials;
- return safe connection error categories;
- enforce backend authorization;
- rate-limit connection tests;
- no durable mapping or sync job in this prompt.

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: environment-backed named SQL Server profiles expose admin-only list/test/tables/columns APIs; secrets are omitted; connection tests are strictly rate-limited and return safe failure categories
- Changed files: Api/Services/DataSources/DataSourceConnectorOptions.cs; Api/Services/DataSources/SourceSessionFactory.cs; Api/Services/DataSources/NamedSourceDiscoveryService.cs; Api/Services/DataSources/ISourceDataSession.cs; Api/Services/DataSources/AccessSourceDataSessionAdapter.cs; Api/Endpoints/DataSourceDiscoveryEndpoints.cs; Api/Program.cs; Api.Tests/DataSourceDiscoveryAuthorizationTests.cs; docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md; docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md; docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md; MASTER_ROADMAP.md
- Checks run: git diff --check; dotnet test Api.Tests --filter FullyQualifiedName~DataSourceDiscovery|FullyQualifiedName~SourceDataSessionAdapterTests (12 passed); node scripts/check-agent-instructions.mjs; node scripts/check-prompt-queues.mjs; node scripts/check-planning-architecture.mjs
- Checks not run: full Api.Tests suite; npm frontend checks; durable secret storage; mapping/sync
- Run log: .ai/runs/2026-08-13-QDB04-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 355eccef9e792a7d43f480aa6a363a21cc9ad241
- Main verification: git rev-parse origin/main -> 096bf20d6908186cd3d7062ca6339c086522040f; work SHA 355eccef9e792a7d43f480aa6a363a21cc9ad241 is an ancestor
- Missed: no admin UI; Access named profiles are not discovery-backed yet; connection strings stay env/config-only
- Follow-up: QDB05
- Residual risk: operators must set DataSources__Sources__{name}__ConnectionString via environment; test-connection uses the existing strict rate-limit policy
- Prompt defect / scope repair: none

---

## QDB05 - Add deterministic mapping profile and bounded preview

Status: DONE
Ready after: `QDB04` is `DONE`
Priority: P1
Type: backend/data model/API tests
Feature family: source-mapping-preview
Parallel-safe: no
Owner: Cursor
Local lock: removed after DONE
Commit suggestion: `feat(import): add source mapping preview`

### Goal

Map source streams to canonical Trendplus entities and preview a small sample without durable business writes.

### Required first capabilities

- explicit table/stream selection;
- explicit external key and cursor selection;
- deterministic column aliases;
- schema fingerprint;
- field-level validation and rejection reasons;
- bounded preview;
- no arbitrary transform scripts or model-generated mappings.

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: admin-only mapping preview validates explicit table/key/cursor/field mappings, schema fingerprint, field rejection reasons and a bounded sample without durable writes or silent alias guessing
- Changed files: Api/Services/DataSources/SourceMappingPreviewService.cs; Api/Services/DataSources/SourceMappingPreviewDtos.cs; Api/Services/DataSources/CanonicalSourceEntities.cs; Api/Services/DataSources/SourceSchemaFingerprint.cs; Api/Services/DataSources/NamedSourceDiscoveryService.cs; Api/Endpoints/DataSourceDiscoveryEndpoints.cs; Api/Program.cs; Api.Tests/SourceMappingPreviewTests.cs; docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md; docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md; docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md; MASTER_ROADMAP.md; docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md; docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md; docs/ai/ANALYTICS_TEST_STRATEGY.md; docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md; .ai/runs/2026-08-13-QDB05-evidence.md
- Checks run: git diff --check; dotnet test Api.Tests --filter FullyQualifiedName~SourceMappingPreviewTests --no-build (6 passed); node scripts/check-agent-instructions.mjs; node scripts/check-prompt-queues.mjs; node scripts/check-planning-architecture.mjs
- Checks not run: full Api.Tests rebuild (blocked by unrelated InventoryEndpoints dirty-tree compile errors); npm frontend checks; durable mapping persistence; sync/checkpoints
- Run log: .ai/runs/2026-08-13-QDB05-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 355eccef9e792a7d43f480aa6a363a21cc9ad241
- Main verification: git rev-parse origin/main -> 096bf20d6908186cd3d7062ca6339c086522040f; work SHA 355eccef9e792a7d43f480aa6a363a21cc9ad241 is an ancestor
- Missed: mapping is request-scoped only; QDB06 durable checkpoints need an owner-approved migration
- Follow-up: QDB06 after owner approves a database migration
- Residual risk: preview returns a bounded mapped sample to authorized admins; rows are not logged or stored; full Api rebuild is currently blocked by unrelated InventoryEndpoints dirty-tree errors
- Prompt defect / scope repair: QDB06 was not auto-promoted to READY because it still requires owner approval of a database migration

---

## QDB06 - Add idempotent checkpointed incremental synchronization

Status: DONE
Ready after: `QDB05` is `DONE` and the owner approves a database migration
Priority: P1
Type: backend/persistence/workers/integration tests
Feature family: source-checkpoint-idempotency
Parallel-safe: no
Owner: Cursor Auto
Local lock: removed after DONE
Commit suggestion: `feat(import): add durable source checkpoints`
Promotion note: 2026-08-18 - owner approved the database migration and ran QDB06 before RQ96.

### Problem

Mapped source batches cannot yet retry or restart safely. Access cursors are keyed only by `TableKey`, so connection/mapping identities would collide, and a crash between destination write and cursor advance can duplicate or skip rows.

### Evidence

- `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md` defines checkpoint identity as `ConnectionId + MappingProfileId + SourceStream` and requires the checkpoint to be the last source position whose destination effects were committed.
- `AccessImportCursors` is keyed only by `TableKey` and must not be reused as the QDB06 identity.
- QDB05 mapping is request-scoped; there is no durable mapping store. `MappingProfileId` is a hash of the mapping document.

### Scope

- durable `SourceSyncCheckpoints` / `SourceSyncAppliedRows` model and migration
- checkpoint engine with crash, overlap, identity, schema-drift and metrics proofs
- EF store that commits destination rows and checkpoint in one transaction
- dedicated tenant scope `n/a_dedicated`; no caller-header tenant authority
- do not upsert Artikli/Prodaja in this slice; staging rows are the destination
- do not write back to customer sources; internal DB stays PostgreSQL

### Read first

- `docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md`
- `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `Api/Services/DataSources/SourceMappingPreviewService.cs`
- `Api/Services/Access/AccessImportCursorRepository.cs`

### Do

1. Persist checkpoints only after destination effects are staged for the same commit.
2. Make retry/restart idempotent via external-key payload hash (insert/update/skip).
3. Block the mapping when schema fingerprint drifts; do not apply new rows.
4. Keep connection and mapping identities from colliding.
5. Record read/inserted/updated/skipped/rejected metrics; rejected rows never become fake zeros.

### Tests

- crash before destination commit leaves checkpoint unchanged
- crash after destination commit is recoverable without duplicates
- timestamp overlap plus external-key deduplication works
- connection/mapping identities cannot collide
- schema fingerprint drift blocks the affected mapping
- metrics distinguish read, inserted, updated, skipped and rejected rows
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SourceCheckpointSyncEngineTests`

### Acceptance

- All six required proofs pass.
- Checkpoint identity is `ConnectionId + MappingProfileId + SourceStream`.
- Dedicated deployments persist `TenantScope = n/a_dedicated`.
- Access `TableKey` cursors remain unchanged compatibility.

### Dependencies

- `QDB05` DONE
- Owner migration approval 2026-08-18
- Do not start MT02 or invent `shared_saas`; MT07 owns shared tenant ownership later
- SQL Server end-to-end through this engine remains a later commercial gate, not this slice

### Completion note

- Date: 2026-08-18
- Status: DONE
- Completion: durable checkpoint identity, EF tables, and idempotent apply engine landed; required crash/overlap/identity/drift/metrics proofs pass against an in-memory store; destination is `SourceSyncAppliedRows` staging rather than Artikli/Prodaja upsert
- Changed files: Domain/Model/SourceSyncCheckpoint.cs; Domain/Model/SourceSyncAppliedRow.cs; Api/Services/DataSources/SourceCheckpointSyncContracts.cs; Api/Services/DataSources/SourceCheckpointSyncEngine.cs; Api/Services/DataSources/InMemorySourceSyncStore.cs; Api/Services/DataSources/EfSourceSyncStore.cs; Api/Services/DataSources/SourceCheckpointSyncService.cs; Api/Services/DataSources/SourceMappingProfileId.cs; Api.Tests/SourceCheckpointSyncEngineTests.cs; Infrastructure/Migrations/20260818120000_AddSourceSyncCheckpoints.cs; Infrastructure/Migrations/TrendplusDbContextModelSnapshot.cs; Infrastructure/DbContexts/TrendplusDbContext.cs; Api/Program.cs; docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md; docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md; docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md; MASTER_ROADMAP.md; docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md; docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md; docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md; docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md; docs/ai/ANALYTICS_TEST_STRATEGY.md; docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md; .ai/runs/2026-08-18-QDB06-evidence.md
- Contract/runtime behavior changed: yes; new checkpoint tables and apply API. Access import cursors are unchanged. Canonical Artikli/Prodaja upsert is not in this slice.
- Checks run: git diff --check; dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SourceCheckpointSyncEngineTests --no-restore (8 passed); node scripts/check-agent-instructions.mjs --self-test; node scripts/check-agent-instructions.mjs; node scripts/check-prompt-queues.mjs --self-test; node scripts/check-prompt-queues.mjs (261 tasks); node scripts/check-planning-architecture.mjs --self-test; node scripts/check-planning-architecture.mjs (71 planning tasks)
- Checks not run: full Api.Tests suite; live SQL Server e2e through the checkpoint engine; npm frontend checks; EF in-memory crash-split (production EF store uses one transaction)
- Run log: .ai/runs/2026-08-18-QDB06-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: dcda7e21eb0d8f367481418d446083e47df2a820
- Main verification: git rev-parse origin/main -> dcda7e21eb0d8f367481418d446083e47df2a820; work SHA dcda7e21eb0d8f367481418d446083e47df2a820 is an ancestor
- Missed: SQL Server end-to-end through this engine; Artikli/Prodaja destination upsert; admin UI (QDB07)
- Follow-up: QDB09 current READY for SQL Server checkpoint e2e; QDB07 remains WAITING after QDB09 plus authorization/release gates
- Residual risk: production workers must call `SourceCheckpointSyncService`; unused tables until a worker/e2e path applies batches. Split-commit crash proof is in-memory; EF store uses one transaction so that failure mode does not occur on the PostgreSQL path.
- Prompt defect / scope repair: expanded legacy Goal/Required proof into the eight required sections; destination bounded to staging rows to avoid a second owner (Access unique indexes are `DataOrigin='access'` only)
- Next: `RQ96`

---

## QDB07 - Add controlled admin connector experience

Status: WAITING
Ready after: `QDB09` is `DONE` and authorization/release gates permit pilot UI work
Priority: P2
Type: frontend/backend UX tests
Feature family: data-source-admin-experience
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/QDB07-<agent>.lock.md`
Commit suggestion: `feat(import): add source connector admin flow`

### Problem

The backend now has discovery, mapping preview and checkpoint infrastructure, but operators still have no truthful first-party admin flow for that proven path. Without a bounded admin prompt, customers would be forced to use raw endpoints or improvised local tools, and a later UI could accidentally hide schema drift, partial sync or secret-handling boundaries.

### Evidence

- `QDB04` added named source discovery plus admin-only list/test/table/column APIs.
- `QDB05` added deterministic mapping preview.
- `QDB06` added durable checkpoints, but its completion note still missed admin UI.
- The 2026-08-20 audit confirmed that `QDB07` is still missing and must not outrun release/authorization gates.

### Scope

- the existing admin-only connector endpoints and their DTOs
- connector admin UI pages/components for discovery, mapping preview, manual sync controls and batch history
- focused backend/frontend tests for truthful state handling
- no stored secrets in the client, no arbitrary SQL, no tenant/shared-SaaS work

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md`
- `QDB04`/`QDB05`/`QDB06` completion notes in this queue
- current connector discovery and mapping preview endpoints

### Do

1. Expose only the already-proven backend flow:
   - connection list and health
   - test connection
   - schema/table browser
   - mapping editor
   - preview
   - manual/scheduled sync controls
   - batch history and safe recovery state
2. Keep partial/error/schema-drift states explicit; do not let the UI silently convert them into success.
3. Reuse backend authorization and safe error categories from `QDB04`; never surface stored secrets or raw connection strings.
4. Do not let the frontend construct SQL or invent checkpoint status.

### Tests

- focused backend endpoint tests only where the admin flow needs additive contract fields
- focused frontend/admin-flow tests for partial, blocked, schema-drift and secret-redaction states
- `git diff --check`
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release`

### Acceptance

- Operators can drive the proven connector flow without raw endpoint usage.
- The UI remains a consumer of backend truth and does not hide drift/partial/error states.
- Secrets and arbitrary SQL remain out of scope.

### Dependencies

- `QDB09` DONE first so the admin flow fronts a real SQL Server checkpoint path rather than only disconnected pieces.
- Authorization/release gates must still permit pilot admin UI work.

---

## QDB09 - Prove SQL Server end-to-end sync through the checkpoint engine

Status: IN_PROGRESS
Ready after: `QDB06` is `DONE` and the owner authorizes the first commercial/runtime follow-up before QDB07
Priority: P1
Type: backend/integration tests/workers
Feature family: sqlserver-checkpoint-e2e
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/QDB09-<agent>.lock.md`
Commit suggestion: `feat(import): prove sql server checkpoint sync end to end`
Promotion note: 2026-08-20 - owner-promoted from the pilot audit because `QDB06` closed the checkpoint engine but still left SQL Server end-to-end application and worker proof missing.

### Problem

`QDB06` proved checkpoint semantics in isolation, but no real SQL Server path currently drives discovery, mapping preview and checkpointed application all the way into staged destination rows. Production workers also still do not call the new sync service, so the commercial connector story remains incomplete.

### Evidence

- `QDB06` completion note explicitly missed:
  - SQL Server end-to-end through the checkpoint engine
  - production worker call into `SourceCheckpointSyncService`
  - admin UI (`QDB07`)
- `QDB03` proved read-only SQL Server source-session behavior, but not end-to-end staging through QDB06.
- The 2026-08-20 audit also reproduced current SQL Server contract drift in backend tests, which makes a fresh end-to-end re-entry necessary before the queue can claim no active QDB work.

### Scope

- SQL Server source-session/runtime files needed to drive checkpointed application
- checkpoint sync service/store files already introduced by `QDB06`
- the smallest production caller path needed to invoke checkpoint sync manually or from the existing worker surface
- focused SQL Server integration tests proving discovery -> mapping -> preview -> checkpoint apply into `SourceSyncAppliedRows`
- `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
- one dated `docs/qa/` or durable `.ai/runs/...` evidence note

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/architecture/DATA_SOURCE_CONNECTOR_CONTRACT.md`
- `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md`
- `QDB03` completion note
- `QDB06` completion note
- current SQL Server source-session and checkpoint sync files

### Do

1. Prove one named SQL Server profile can move from discovery/mapping preview into checkpointed staged application without inventing a second source contract.
2. Add the smallest production caller path needed to invoke the checkpoint sync service for that profile.
3. Keep destination bounded to `SourceSyncAppliedRows`; do not upsert `Artikli`/`Prodaja` in this prompt.
4. Preserve schema-drift blocking, idempotent replay and safe metrics from `QDB06`.
5. Keep secrets/redaction rules from `QDB04` and do not add arbitrary SQL or shared-SaaS tenant routing.

### Tests

- `git diff --check`
- focused SQL Server integration tests that prove:
  - discovery/mapping preview still work on the same named profile
  - first checkpointed apply writes staged rows and advances the checkpoint
  - replay/restart stays idempotent
  - schema drift blocks apply
  - worker/manual caller path reaches the same sync service
- nearest full backend test command for touched connector files

### Acceptance

- At least one named SQL Server profile can complete an end-to-end checkpointed sync into staged rows on the exact current main branch.
- The sync is callable from a real production-facing path, not only an internal helper test.
- Schema drift, idempotency and secret-redaction rules remain truthful.
- `QDB07` remains out of scope until this path is proven.

### Dependencies

- `QDB03` DONE.
- `QDB04` DONE.
- `QDB05` DONE.
- `QDB06` DONE.
- Do not silently expand this prompt into canonical `Artikli`/`Prodaja` upsert or tenant-owned shared-SaaS routing.

---

## QDB08 - Add onboarding mapping templates and import diagnostics pack

Status: WAITING
Ready after: `QDB07` is `DONE` and at least one end-to-end connector flow can already prove discovery, mapping, preview and sync truth
Priority: P2
Type: docs/backend/frontend contract/tests
Feature family: source-onboarding-diagnostics
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/QDB08-<agent>.lock.md`
Commit suggestion: `feat(import): add onboarding templates and diagnostics`

### Problem

Even with connector discovery and mapping flows in place, first-customer onboarding will still be brittle unless operators get deterministic mapping templates, import diagnostics and a safe explanation of what failed or remains unmapped.

### Evidence

- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md` keeps the current QDB order, then explicitly calls out onboarding mapping templates and import diagnostics as the remaining Gate-1 work needed to make first-customer integration repeatable.
- `QDB04`-`QDB07` cover named sources, mapping, checkpoints and admin flow, but this queue has no dedicated prompt for reusable onboarding presets or diagnostics truth.
- Without a bounded diagnostics contract, operators can be left with raw connector errors or ambiguous “import failed” states that do not explain schema drift, rejected rows, missing required fields or safe next actions.

### Scope

- `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md`
- `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
- backend diagnostics DTO/endpoints touched by the existing connector flow
- connector admin UI only where needed to surface truthful diagnostics/templates
- focused tests for deterministic template/diagnostics behavior

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md`
- `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md`
- `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
- current connector admin/runtime files landed by `QDB04`-`QDB07`

### Do

1. Add reusable mapping-template primitives for the first supported source families without introducing AI-generated mappings.
2. Define a deterministic diagnostics contract that distinguishes connection failure, schema drift, mapping gap, row rejection, checkpoint conflict and partial-success states.
3. Surface safe next-action guidance for operators, but do not expose secrets, raw source payloads or arbitrary SQL.
4. Keep the first version bounded to the existing single-customer/single-deployment connector model.
5. Add focused tests proving stable diagnostics categories and template selection inputs.

### Tests

- `git diff --check`
- focused backend connector diagnostics tests
- focused frontend/admin-flow tests only if new diagnostics UI branches are added
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release`

### Acceptance

- Operators can start from a deterministic mapping template instead of an empty configuration for the first supported source families.
- Import diagnostics distinguish the main failure classes without leaking secrets or raw payloads.
- UI/API messaging stays truthful about partial, blocked and rejected states.
- The prompt does not expand into generic ETL scripting, arbitrary transforms or multi-tenant connector orchestration.

### Dependencies

- `QDB07` DONE first so the admin flow exists before onboarding assistance is added.
- Earlier QDB prompts remain authoritative for source/session/checkpoint behavior.
- Any tenant-owned durable template catalog must wait for the corresponding MT authority if this prompt reaches that boundary.

## Queue completion definition

The roadmap is not complete merely because multiple drivers exist. Trendplus has credible multi-source support only when it can prove:

- provider-neutral contracts;
- at least one real non-Access provider;
- safe configuration and discovery;
- explicit mapping;
- durable idempotent checkpoints;
- deterministic onboarding templates and import diagnostics;
- schema-drift behavior;
- authorization, diagnostics and operational evidence;
- unchanged internal PostgreSQL ownership.
