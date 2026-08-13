# Trendplus Data Source Connector Roadmap

Updated: 2026-08-05  
Status: approved architecture direction; implementation remains gated by the prompt queue  
Repository: `ivanjovicic/Trendplus`

## Decision

Trendplus keeps PostgreSQL as its internal operational and analytics database.

Support for SQL Server, PostgreSQL, MySQL/MariaDB, Microsoft Access and later APIs/files should be implemented as **read-only source connectors feeding the existing import, validation and persistence pipeline**.

Do not turn `TrendplusDbContext` into a multi-provider EF Core context in the first delivery waves. That would mix two separate concerns:

1. where Trendplus stores its own domain and analytics data;
2. where a customer currently stores products, sales, inventory and change-log records.

The first concern remains PostgreSQL/Npgsql. This roadmap addresses the second concern.

## Why this is the lowest-risk direction

The repository already has most of the difficult low-level source-reading behavior behind the Access session boundary:

- `Api/Services/Access/IAccessDataReaderSession.cs` exposes table discovery, column discovery, row-count capability, streaming row reads and incremental cursor input;
- `Api/Services/Access/WindowsAccessSession.cs` implements metadata discovery, parameterized incremental predicates and sequential streaming through ODBC;
- `Api/Services/Access/MdbToolsCliSession.cs` implements the same logical session through Linux `mdbtools` commands;
- `AccessDataSchema` normalizes source-column aliases once and resolves values by ordinal;
- `AccessReadQuery` already models ID, timestamp and composite timestamp-plus-ID cursors with an overlap window;
- `AccessRowCountResult` already distinguishes exact, sampled and unknown counts;
- the existing import pipeline, job queue, repair service and tests already handle cancellation, retries, validation, foreign keys and data-quality behavior.

The current abstraction is therefore functionally close to a generic connector contract, but its names, source identity model, configuration and SQL helpers remain Access-specific.

## Target architecture

```text
Customer source
  - Access / SQL Server / PostgreSQL / MySQL / API / file
        |
        v
Provider connector
  - connection test
  - schema discovery
  - capability declaration
  - bounded streaming reads
  - provider-specific SQL dialect
        |
        v
Provider-neutral source records
  - source identity
  - table/stream identity
  - schema fingerprint
  - stable external key
  - cursor/checkpoint metadata
        |
        v
Mapping and validation
  - source column aliases
  - canonical Trendplus fields
  - preview and validation errors
  - no silent coercion of unknown values
        |
        v
Existing import/application pipeline
  - batching
  - idempotent upsert
  - foreign-key guards
  - data-quality diagnostics
  - existing analytics facts and summaries
        |
        v
Trendplus PostgreSQL database
```

## Architecture boundaries

### Internal database boundary

- Keep `TrendplusDbContext`, EF Core migrations and internal SQL on PostgreSQL/Npgsql.
- Do not require SQL Server/MySQL/Oracle compatibility from internal migrations.
- Keep PostgreSQL extensions and provider-specific analytics optimizations isolated and documented.
- Treat migration of the Trendplus internal database as a separate portability project.

### Source connector boundary

A connector may:

- validate connection settings;
- list schemas/tables/streams;
- list columns and source types;
- report capabilities;
- read bounded pages or streams;
- apply supported cursor predicates;
- return provider-neutral values and metadata.

A connector must not:

- write to the customer source in the initial product;
- execute arbitrary user-provided SQL;
- change Trendplus business rules;
- decide product/supplier/inventory semantics;
- advance a checkpoint before the destination batch is committed;
- log credentials, complete connection strings or sensitive row payloads.

### Mapping boundary

Mapping configuration owns the relationship between a customer schema and Trendplus canonical fields.

Examples:

- customer `dbo.Artikli.Sifra` -> Trendplus product external ID;
- customer `dbo.Artikli.Naziv` -> product name;
- customer `dbo.Prodaja.Datum` -> sale timestamp;
- customer `dbo.Prodaja.Kolicina` -> quantity.

Provider connectors should not contain customer-specific business mappings. They only expose source schema and values.

## Provider-neutral contracts

The eventual contract should use provider-neutral names, for example:

```csharp
public interface ISourceDataSession : IAsyncDisposable
{
    string Provider { get; }
    string SourceIdentity { get; }
    SourceCapabilities Capabilities { get; }

    Task TestConnectionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SourceStream>> GetStreamsAsync(CancellationToken ct = default);
    Task<SourceSchema> GetSchemaAsync(SourceStream stream, CancellationToken ct = default);
    Task<SourceRowCountResult> TryGetRowCountAsync(SourceStream stream, CancellationToken ct = default);
    IAsyncEnumerable<SourceDataRow> ReadRowsAsync(SourceReadRequest request, CancellationToken ct = default);
}
```

This is a target contract, not permission for a broad rename in one commit. The existing Access interface should remain available through a compatibility adapter until all current import consumers have migrated and regression tests are green.

## Capability model

Do not assume every source supports the same operations. Use an explicit capability object rather than provider-name switches spread through the import service.

Recommended capabilities:

- schema discovery;
- exact row count;
- predicate pushdown;
- stable primary-key cursor;
- timestamp cursor;
- composite timestamp-plus-key cursor;
- deterministic ordering;
- cancellation support;
- server-side page limit;
- snapshot consistency;
- CDC support, initially always false.

The existing `SupportsPredicatePushdown` and `AccessRowCountResult.Mode` are the starting point.

## Connection profile

Introduce a Trendplus-owned connection profile rather than spreading connection strings across jobs and configuration.

Recommended first model:

```text
DataSourceConnection
- Id
- Name
- Provider
- SecretReference or encrypted credential payload
- Host / database / default schema metadata
- Enabled
- CreatedAtUtc / UpdatedAtUtc
- LastConnectionTestAtUtc
- LastConnectionTestStatus
- Safe diagnostic message
```

Security rules:

- never return stored secrets to the frontend;
- never log complete connection strings;
- use a secret reference where deployment infrastructure supports it;
- otherwise encrypt at rest with an application-owned key outside the database;
- connection-test responses expose only safe error categories;
- use least-privilege, read-only source credentials;
- validate host/network policy before allowing cloud deployments to connect to private customer networks.

For the first implementation, environment-backed named profiles are acceptable. Database persistence and UI management can follow after the connector contract is proven.

## Mapping profile

Recommended model:

```text
SourceMappingProfile
- Id
- ConnectionId
- CanonicalEntity: Product | Supplier | Sale | InventoryMovement | PriceChange
- SourceSchema
- SourceTable
- ExternalKeyColumns
- CursorMode and cursor columns
- ColumnMappingsJson
- TransformVersion
- Enabled
- SchemaFingerprint
```

The mapping engine should initially support only small deterministic transforms:

- alias selection;
- null/empty normalization;
- invariant numeric/date parsing;
- trim/case normalization where explicitly configured;
- fixed default only when the business contract allows it.

Do not add arbitrary scripts, expressions or model-generated mapping logic in the first waves.

## Checkpoint and delivery truth

A source checkpoint is not merely the last row read. It is the last source position whose destination effects were durably committed.

Recommended checkpoint identity:

```text
ConnectionId + MappingProfileId + SourceStream
```

Recommended checkpoint data:

- cursor mode;
- timestamp;
- external key/tie-breaker;
- overlap window;
- schema fingerprint;
- last started/completed batch IDs;
- last successful synchronization time;
- safe failure category.

Rules:

1. Read a bounded batch from the current durable checkpoint.
2. Validate and transform it.
3. Apply idempotent destination writes.
4. Commit destination changes and import evidence.
5. Advance the checkpoint in the same durable completion boundary or through a recoverable outbox/commit record.
6. On failure, retry from the previous durable checkpoint with overlap and deduplication.

## Idempotency

Each imported entity needs a stable source identity, such as:

```text
ConnectionId + MappingProfileId + SourceExternalKey
```

Use a unique constraint or equivalent durable guard. A retried batch must update/skip the same entity rather than create duplicates.

Where a source has no stable key, Trendplus should require an explicit import policy:

- full refresh into staging and reconcile;
- deterministic composite key;
- append-only with duplicate-risk warning;
- unsupported until the owner selects a safe policy.

Do not silently use row number as a persistent external key.

## Schema drift

Store a deterministic schema fingerprint based on normalized column names, source types and nullability where available.

At connection/mapping preview and before scheduled sync:

- unchanged fingerprint -> continue;
- additive unused column -> record informational drift;
- mapped column removed/renamed/type-incompatible -> block the affected mapping;
- key/cursor column changed -> block and require explicit review;
- never silently remap a column based only on similar spelling.

## Incremental synchronization modes

Support in this order:

1. **Full snapshot** — safest universal fallback for small sources.
2. **Stable numeric/string key cursor** — simple append/update streams with monotonic keys.
3. **Timestamp cursor with overlap** — handles late writes but requires deduplication.
4. **Timestamp plus key tie-breaker** — preferred when timestamps are not unique.
5. **CDC/log-based replication** — future enterprise option only after real scale requirements.

CDC, Kafka and Debezium are intentionally out of the first roadmap waves.

## Provider order

### Existing: Microsoft Access

Keep both current implementations:

- Windows ODBC for fast local/customer-network execution;
- Linux `mdbtools` for compatibility and controlled fallback.

The Access implementation becomes the first adapter proving the provider-neutral contract.

### First new provider: SQL Server

Reasons:

- common in retail, POS and ERP environments;
- mature `Microsoft.Data.SqlClient` driver;
- schemas, metadata and parameterized streaming are well supported;
- strong commercial value for Serbian/regional customers;
- can reuse most of the current cursor and streaming pipeline.

Initial support should be read-only and limited to configured tables/views.

### Second new provider: PostgreSQL

Use Npgsql in source mode, separate from the internal Trendplus connection. Do not share `TrendplusDbContext` or migrations with a customer source database.

### Third new provider: MySQL/MariaDB

Add only after the SQL Server/PostgreSQL dialect and contract have proven that provider behavior is properly isolated.

### Later providers

- Oracle when a named customer justifies licensing/testing effort;
- CSV/Excel through a file connector using the same mapping/checkpoint concepts;
- REST APIs through explicit typed adapters, pagination and rate-limit policies.

## Phased delivery

## Phase 0 — Contract characterization

Goal: protect current Access behavior before introducing provider-neutral names.

Deliverables:

- a concise connector contract document;
- characterization tests for schema normalization, row-count truth, capabilities and cursor semantics;
- inventory of consumers that depend on Access-named types;
- no production behavior change.

This is the smallest useful implementation and is represented by `QDB01` in `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`.

## Phase 1 — Provider-neutral seam with compatibility adapter

Goal: introduce generic source contracts without rewriting the import pipeline.

Deliverables:

- provider-neutral abstractions under a dedicated `DataSources` namespace;
- an Access adapter preserving the existing interface and behavior;
- no customer-source persistence or UI yet;
- all existing Access tests remain green.

Exit gate:

- current Access import behavior is unchanged;
- old consumers compile through the compatibility boundary;
- provider-specific SQL remains in provider-specific code.

## Phase 2 — SQL Server proof connector

Status: delivered 2026-08-13 (`QDB03`).

Goal: prove one non-Access relational source end to end in tests.

Deliverables:

- read-only SQL Server connection/session (`SqlServerSourceDataSession`);
- metadata discovery;
- safe identifier quoting;
- parameterized bounded/full and incremental reads;
- Testcontainers or equivalent real-engine integration tests (LocalDB used when Docker is unavailable);
- no arbitrary SQL endpoint.

Exit gate:

- connection failure categories are safe;
- cancellation and timeouts are tested;
- source row streaming is bounded;
- key/timestamp/tie-breaker behavior is deterministic.

## Phase 3 — Named connection and discovery API

Status: delivered 2026-08-13 (`QDB04`).

Goal: allow an administrator to configure and inspect a source without importing data.

Deliverables:

- named provider configuration (environment-backed profiles);
- test-connection endpoint;
- list schemas/tables/columns endpoint;
- safe diagnostics;
- authorization and audit logging;
- no returned secrets.

## Phase 4 — Mapping preview

Status: delivered 2026-08-13 (`QDB05`).

Goal: configure a source-to-Trendplus mapping with no durable writes.

Deliverables:

- mapping profile model;
- preview of a bounded row sample;
- field-level validation results;
- schema fingerprint;
- explicit key and cursor selection;
- no automatic guessing that changes data silently.

## Phase 5 — Durable incremental sync

Goal: run idempotent imports with recoverable checkpoints.

Deliverables:

- durable checkpoint model;
- batch commit/checkpoint boundary;
- overlap and deduplication;
- restart/retry tests;
- monitoring and safe failure states;
- per-connection/mapping metrics.

## Phase 6 — Admin experience and more providers

Goal: expose the proven backend capabilities through a controlled UI.

Deliverables:

- connection list and health;
- schema browser;
- mapping editor;
- preview;
- manual and scheduled sync controls;
- batch history, warnings and recovery actions;
- SQL Server first, then PostgreSQL/MySQL based on demand.

## Minimal changes with high value

The following changes provide value without a broad rewrite:

1. Add characterization tests and a provider-neutral contract document.
2. Add an explicit capability model instead of more Access/provider switches.
3. Add provider/source identity to future import metadata while preserving current `SourceFilePath` behavior.
4. Isolate SQL dialect functions from mapping/business logic.
5. Persist schema fingerprints before implementing automatic scheduled sync.
6. Use SQL Server as the first proof provider rather than implementing several incomplete providers in parallel.

## Explicit non-goals for the first waves

- replacing the internal PostgreSQL database;
- multi-provider EF Core migrations;
- write-back to customer databases;
- arbitrary SQL entered from the UI;
- automatic schema remapping;
- CDC/Kafka/Debezium infrastructure;
- bidirectional synchronization;
- cross-customer shared credentials;
- provider secrets in frontend configuration;
- LLM-generated mappings or SQL.

## Validation strategy

### Unit/contract tests

- alias and schema normalization;
- capability reporting;
- row-count exact/sampled/unknown truth;
- identifier validation and quoting;
- cursor predicate construction;
- timestamp overlap and tie-breaker semantics;
- cancellation propagation;
- safe error categorization;
- schema fingerprint stability/change detection;
- mapping validation and idempotency keys.

### Real-provider integration tests

Each relational provider needs tests against the real database engine, preferably through Testcontainers:

- connection and authentication;
- schema discovery;
- quoted identifiers and reserved words;
- nulls, decimals, dates and Unicode;
- deterministic ordering;
- full and incremental reads;
- timeout/cancellation;
- restart from durable checkpoint;
- duplicate prevention.

Do not claim provider support from mocked `DbConnection` tests alone.

## Operational metrics

Track per connection/mapping without logging row payloads:

- last successful sync time;
- source rows read;
- rows inserted/updated/skipped/rejected;
- batch duration;
- checkpoint lag;
- schema-drift state;
- safe error category;
- retry count;
- active provider and capability mode.

## Documentation ownership

This document owns the connector architecture and delivery order.

Related documents:

- `docs/PORTABILITY_AUDIT_AND_RECOMMENDATIONS.md` owns broader cloud/database/storage portability;
- `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` owns executable task sequencing;
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md` owns queue status and claim rules;
- current Access import documentation and tests remain authoritative for shipped behavior.

When implementation changes a contract described here, update this roadmap and the focused queue in the same delivery or record an explicit documentation follow-up.