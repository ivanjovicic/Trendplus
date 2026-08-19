# Data Source Connector Contract

Date: 2026-08-09
Repo: `ivanjovicic/Trendplus`
Owner prompt: `QDB01`
Status: characterization of the **current** Access reader seam; no provider-neutral production rename yet

## Purpose

Preserve the behavioral contract already implemented by the Access import reader so future SQL Server/PostgreSQL/MySQL connectors can adapt without silently changing:

- schema alias truth;
- row-count mode truth;
- capability reporting;
- incremental cursor / full-scan fallback semantics;
- cancellation-aware streaming expectations.

Internal Trendplus storage remains PostgreSQL. This document describes **source-reader** behavior only.

## Term mapping (provider-neutral → current Access types)

| Provider-neutral term | Current Access type / member |
|---|---|
| Source session | `IAccessDataReaderSession` |
| Source mode / dialect path | `IAccessDataReaderSession.Mode` (`windows` / `cli`) |
| Source file path | `IAccessDataReaderSession.SourceFilePath` |
| Predicate pushdown capability | `IAccessDataReaderSession.SupportsPredicatePushdown` |
| Source schema | `AccessDataSchema` |
| Source row | `AccessDataRow` |
| Row-count result | `AccessRowCountResult` (`exact` / `sampled` / `unknown`) |
| Incremental read request | `AccessReadQuery` |
| Windows pushdown SQL seam | `WindowsAccessSession.BuildSelectSqlFromColumns` |
| Pushdown eligibility helper | `AccessImportService.CanApplyAccessReadPushdown` |

## Behavioral invariants

### Schema and aliases

- Column names returned by discovery remain **source names**.
- Normalized alias lookup is a separate concern (`AccessImportService.Normalize` + ordinal map).
- Missing aliases return `false` / unknown; they must not fabricate values.
- `AccessDataRow.TryGetValue` / `TryGetValueNormalized` follow the same fail-closed lookup.

### Row counts

| Factory / mode | Meaning |
|---|---|
| `AccessRowCountResult.Exact(n)` | authoritative count |
| `AccessRowCountResult.Sampled(n)` | approximate/sampled count |
| `AccessRowCountResult.Unknown()` | count unavailable (`Mode=unknown`, `Count=0` is **not** a proven empty table) |

Consumers must distinguish modes. `Count == 0` with `unknown` is not the same as exact empty.

### Capabilities

| Implementation | `Mode` | `SupportsPredicatePushdown` |
|---|---|---|
| `WindowsAccessSession` | `windows` | `true` |
| `MdbToolsCliSession` | `cli` | `false` |

Capability must be read from the session property. Do not infer pushdown from provider-name string comparisons in consumer tests.

### Incremental cursors

`AccessReadQuery` models:

- `id`
- `timestamp`
- `timestamp_then_id` (composite)
- overlap window via `OverlapSeconds`
- alias lists for timestamp/id columns

Windows pushdown (`BuildSelectSqlFromColumns`):

- matching aliases → parameterized `WHERE` + deterministic `ORDER BY`;
- missing aliases → full-scan `SELECT *` with **no** parameters (no fake checkpoint advance).

Pushdown eligibility (`CanApplyAccessReadPushdown`) is false when required aliases/cursors are missing.

### Streaming

- `ReadRowsAsync` is asynchronous and cancellation-aware.
- Native ODBC/`mdbtools` streaming is not exercised in `QDB01` characterization tests.
- Future provider seams (`QDB02+`) must keep cancellation propagation.

## Explicit non-goals of QDB01

- no production Access/runtime file changes;
- no ODBC/`mdbtools`/customer file dependency in characterization tests;
- no write-back, arbitrary SQL, CDC or multi-provider EF Core;
- no rename to `Source*` contracts yet (`QDB02`).

## QDB02 seam (compatibility adapter)

Provider-neutral production types now live under `Api/Services/DataSources/`:

| Contract | Role |
|---|---|
| `ISourceDataSession` | provider-neutral read session |
| `SourceCapabilities` | capability flags (prefer over provider/mode switches) |
| `AccessSourceDataSessionAdapter` | wraps `IAccessDataReaderSession` (Windows + CLI) |

Existing Access import consumers keep compiling against `IAccessDataReaderSession`. The adapter exposes `AccessSession` for gradual migration. ODBC/`mdbtools` behavior stays inside Access implementations.

Verification:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SourceDataSessionAdapterTests
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~DataSourceConnectorContractTests|FullyQualifiedName~AccessReadQueryPushdownTests
```

## QDB03 SQL Server proof connector

`SqlServerSourceDataSession` is the first non-Access `ISourceDataSession` implementation.

| Fact | Behavior |
|---|---|
| Provider | `sqlserver`, mode `read-only` |
| Connection | `Microsoft.Data.SqlClient` with `ApplicationIntent=ReadOnly`; no arbitrary SQL; no writes |
| Identity | server + database only; password and user id are omitted |
| Discovery | `INFORMATION_SCHEMA` tables/views and columns; table names are `schema.table` |
| Quoting | `[identifier]` with `]` escaped as `]]`; `;` / control characters rejected |
| Counts | exact `COUNT_BIG(*)` |
| Cursors | parameterized `@pN` predicates for `id`, `timestamp`, and `timestamp_then_id` |
| Bounds | async streaming plus optional `SourceReadQuery.MaxRows` (`SELECT TOP (@maxRows)`) |
| Failures | safe categories: authentication, timeout, network, unavailable, canceled, unknown |

Live-engine proof: `Api.Tests/SqlServerSourceDataSessionIntegrationTests.cs` (Testcontainers when Docker is available, else LocalDB / `SQLSERVER_TEST_CONNECTION_STRING`). Supplemental quoting/SQL tests do not replace that suite.

Verification:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SqlServerSourceDataSession
```

## QDB04 named source discovery API

Environment-backed named profiles under `DataSources:Sources:{name}` (`Provider`, `DisplayName`, `ConnectionString`) are listed and inspected through admin-only endpoints. Connection strings are never returned. Failed tests return safe categories (`authentication`, `timeout`, `network`, `unavailable`, `unknown`) and a generic message.

| Route | Behavior |
|---|---|
| `GET /api/data-sources` | named profiles, identity without credentials |
| `POST /api/data-sources/{name}/test-connection` | connectivity probe; `strict` rate limit |
| `GET /api/data-sources/{name}/tables` | schemas + `schema.table` names |
| `GET /api/data-sources/{name}/columns?table=` | column names for one table |

Authorization reuses `AdminAccessControl` (`X-Admin-Key` / Admin role). First supported provider is `sqlserver`. No mapping, preview write, or sync job.

Verification:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~DataSourceDiscovery
```

## QDB05 mapping preview

`POST /api/data-sources/{name}/mapping-preview` validates an explicit, request-scoped mapping and returns a bounded sample. It does not persist mappings, write Trendplus business rows, or auto-select source columns from canonical aliases.

Required request facts:

- `table` / `entity` (`artikli`, `prodaja_zaglavlje`, `prodaja_stavke`)
- `externalKeyColumn`
- `cursorMode` (`none` / `id` / `timestamp` / `timestamp_then_id`) plus named cursor columns
- explicit `fields[]` (`target` + `source`)

Field statuses are `ok` or `rejected` with reasons such as `source_column_missing`, `target_required_unmapped`, `duplicate_target`, `key_column_missing`. Schema fingerprint is `sha256:` of provider + table + sorted source column names. Preview max rows is 50. Canonical aliases may appear as suggestions on rejected required fields; they are never applied silently.

Verification:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SourceMappingPreviewTests
```

## Remaining untested without a later prompt

Recorded for later prompts (`QDB07+` / SQL Server e2e):

- live ODBC metadata discovery and row streaming;
- live `mdbtools` CLI metadata/streaming;
- end-to-end import pipeline from SQL Server through `SourceCheckpointSyncEngine` into Artikli/Prodaja;
- durable mapping-profile store (current `MappingProfileId` is a hash of the request mapping document).

QDB06 delivered 2026-08-18: checkpoint identity `ConnectionId + MappingProfileId + SourceStream`, idempotent `SourceSyncAppliedRows` staging, schema-drift block, and read/inserted/updated/skipped/rejected metrics. Access `TableKey` cursors remain compatibility-only.

## Verification

```powershell
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~DataSourceConnectorContractTests
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AccessReadQueryPushdownTests
```
