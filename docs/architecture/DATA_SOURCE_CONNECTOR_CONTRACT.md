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

## Remaining untested without a future seam

Recorded for provider tests (`QDB03+`):

- live ODBC metadata discovery and row streaming;
- live `mdbtools` CLI metadata/streaming;
- exact vs sampled count selection against real engines;
- end-to-end import pipeline interaction with checkpoints.

## Verification

```powershell
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~DataSourceConnectorContractTests
dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AccessReadQueryPushdownTests
```
