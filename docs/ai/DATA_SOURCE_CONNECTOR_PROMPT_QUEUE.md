# Trendplus Data Source Connector Prompt Queue

Created: 2026-08-05  
Repository: `ivanjovicic/Trendplus`  
Queue purpose: evolve the existing Access reader into a safe multi-source import architecture without changing the internal PostgreSQL database or starting a broad rewrite.  
Current READY prompt: none

## Global routing

This is a focused P1/P2 architecture queue. It does not outrank active P0 work.

Before claiming from this queue, confirm:

1. `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md` has no earlier repository-overlapping P0 `READY`, `IN_PROGRESS`, `PARTIAL` or `BLOCKED` task.
2. `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` has no earlier repository-overlapping P0 task.
3. The analytics reliability router has no earlier P0 task touching the same Access/import files.
4. There is no open PR, branch or local lock owning the prompt's exact feature family or paths.
5. Current Access import behavior is treated as shipped compatibility, not as disposable prototype code.

`QDB01` is docs/tests-only and may run in parallel with unrelated runtime work. Later prompts are sequential unless their own section explicitly says otherwise.

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
- Next: `QDB03` stays WAITING until backend CI executes real tests and the open BCI gate clears

---

## QDB03 - Add a read-only SQL Server proof connector

Status: WAITING
Ready after: `QDB02` is `DONE` and backend CI executes real tests without an open BCI `PARTIAL`/`BLOCKED` gate (`BCI01`/`BCI05`)
Priority: P1
Type: backend/integration tests
Feature family: sqlserver-source-connector
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/QDB03-<agent>.lock.md`
Commit suggestion: `feat(import): add sql server source connector`
Demotion note: 2026-08-11 — not READY while `BCI05`/`BCI01` remain PARTIAL (GHA proof pending), despite local Docker suite green.

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

---

## QDB04 - Add named source configuration and safe discovery endpoints

Status: WAITING  
Ready after: `QDB03` is `DONE`  
Priority: P1  
Type: backend/security/API tests  
Feature family: data-source-connection-discovery  
Parallel-safe: no  
Owner: unassigned  
Local lock: `.ai/task-locks/QDB04-<agent>.lock.md`  
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

---

## QDB05 - Add deterministic mapping profile and bounded preview

Status: WAITING  
Ready after: `QDB04` is `DONE`  
Priority: P1  
Type: backend/data model/API tests  
Feature family: source-mapping-preview  
Parallel-safe: no  
Owner: unassigned  
Local lock: `.ai/task-locks/QDB05-<agent>.lock.md`  
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

---

## QDB06 - Add idempotent checkpointed incremental synchronization

Status: WAITING  
Ready after: `QDB05` is `DONE` and the owner approves a database migration  
Priority: P1  
Type: backend/persistence/workers/integration tests  
Feature family: source-checkpoint-idempotency  
Parallel-safe: no  
Owner: unassigned  
Local lock: `.ai/task-locks/QDB06-<agent>.lock.md`  
Commit suggestion: `feat(import): add durable source checkpoints`

### Goal

Persist checkpoints only after destination effects are durably committed and make retry/restart idempotent.

### Required proof

- crash before destination commit leaves checkpoint unchanged;
- crash after destination commit is recoverable without duplicates;
- timestamp overlap plus external-key deduplication works;
- account/source/mapping identities cannot collide;
- schema-key/cursor drift blocks the affected mapping;
- metrics distinguish read, inserted, updated, skipped and rejected rows.

---

## QDB07 - Add controlled admin connector experience

Status: WAITING  
Ready after: `QDB06` is `DONE` and authorization/release gates permit pilot UI work  
Priority: P2  
Type: frontend/backend UX tests  
Feature family: data-source-admin-experience  
Parallel-safe: no  
Owner: unassigned  
Local lock: `.ai/task-locks/QDB07-<agent>.lock.md`  
Commit suggestion: `feat(import): add source connector admin flow`

### Goal

Expose only the proven backend flow:

- connection list and health;
- test connection;
- schema/table browser;
- mapping editor;
- preview;
- manual/scheduled sync controls;
- batch history and safe recovery.

Do not let the frontend construct SQL, display stored secrets or hide partial/error/schema-drift states.

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
