# Backend CI Failure Triage

Date: 2026-08-06
Repo: `ivanjovicic/Trendplus`
Commit: `568f03c65891e96bf2c0f27592aeea96c2e58361`
Workflow run: `31080378321`
Workflow job: `92547604945`

## Context

The backend CI bootstrap issue is fixed. The GitHub Actions run now reaches test execution and fails on real assertions / runtime contract drift instead of restore/build.

CI environment from the workflow:

- .NET SDK: `8.0.x`
- PostgreSQL: `postgres:15`

Local repro environment used during triage:

- .NET SDK: `10.0.201`

## Summary

Observed total from the full backend test project run:

- `741 passed`
- `40 failed`

The failures cluster into four root-cause families:

1. Access import endpoint discovery/body-binding contract drift in the test host.
2. Analytics actions list filter/paging regression.
3. Inventory list empty-result regression.
4. Data quality top-offender SQL scope drift.

## Evidence By Family

### 1. Access import endpoint discovery/body-binding drift

Representative filter:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AccessImportAdminAuthorizationTests"
```

Result:

- `12 failed, 0 passed`
- The route builder throws before auth assertions run.

First failure signature:

```text
System.InvalidOperationException: Body was inferred but the method does not allow inferred body parameters.
```

Route factory details from the failure:

- `batchId` -> route
- `service` -> services
- `logService` -> body inferred
- `cache` -> services
- `logger` -> services
- `logTake` -> query string
- `severity` -> query string

Interpretation:

- This looks like a minimal-API discovery/binding contract issue in the test host, not a real auth assertion.
- Likely root cause: one or more AccessImport services used by endpoint discovery are not registered in the specific test host, so request delegate factory infers a body parameter on a route that does not allow it.

Additional affected filter:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AccessImportRunEndpointTests"
```

Observed failure:

- `PostRun_WhenStoragePreparationTimesOut_ReturnsGatewayTimeout`
- expected `GatewayTimeout`, actual `Unauthorized`

That looks like auth being evaluated before the timeout path, which is a contract mismatch worth a dedicated follow-up prompt.

### 2. Analytics actions list regression

Representative filter:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AnalyticsActionsCriticalWorkflowTests.List_AppliesCanonicalFiltersSearchPagingAndPriorityOrdering"
```

Result:

- `1 failed, 0 passed`

Failure:

- expected `totalCount = 2`
- actual `totalCount = 0`

Interpretation:

- The seeded rows are not surviving the current list query path.
- This is a deterministic assertion regression or fixture/query scope mismatch, not a transient failure.

### 3. Inventory list regression

Representative filter:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~InventoryListEndpointIntegrationTests"
```

Result:

- `5 failed, 1 passed`

Common signatures:

- expected item counts, actual `0`
- empty filter returns `meta.dataQualityStatus = null` instead of `insufficient_data`
- one test fails because the collection is empty

Interpretation:

- The seeded inventory fixture is not being surfaced by the endpoint anymore.
- Likely causes: filter/scope regression, query shape change, or a shared inventory endpoint contract change.

### 4. Data quality top-offender SQL scope drift

Representative filter:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~DataQualityPostgresIntegrationTests"
```

Result:

- `6 failed, 3 passed`

First failure signature:

```text
Npgsql.PostgresException : 42703: column p.DataOrigin does not exist
```

Stack trace points to:

- `Infrastructure.Services.AnalyticsDataQualityHealthService.GetTopOffendersAsync(...)`

Interpretation:

- This is a concrete SQL/schema contract mismatch.
- Existing queue ownership appears to be `RQ78` / `RQ06` from the analytics reliability queue family.

## Queue Ownership

Known ownership / mapping:

- Data quality top-offender scope drift and stale count/order expectation -> resolved via `RQ78` and `RQ77`.
- Access import body-binding/auth discovery issue -> no exact existing prompt found in the current queues during triage.
- Inventory list regression -> no exact existing prompt found in the current queues during triage.
- Analytics actions list regression -> no exact existing prompt found in the current queues during triage.

## Checks Run

- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AccessImportAdminAuthorizationTests"`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AccessImportRunEndpointTests"`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AnalyticsActionsCriticalWorkflowTests.List_AppliesCanonicalFiltersSearchPagingAndPriorityOrdering"`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~InventoryListEndpointIntegrationTests"`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~DataQualityPostgresIntegrationTests"`

## Follow-up

- Data quality is resolved for the triaged scope. The only remaining backend-CI follow-ups are Access import, inventory list, and analytics actions list, each needing a focused prompt because no exact current owner was found for those regressions.
