# Analytics SQL Observability Timeouts

Date: 2026-08-05
Repo: `ivanjovicic/Trendplus`
Prompt: Q82
Status: docs/backend-observability audit only

## Scope checked

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/AllEndpoints.cs`
- `Api/Endpoints/DecisionBoardEndpoints.cs`
- `Api/Endpoints/InventoryEndpoints.cs`
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `Workers/NightlyAnalyticsRefreshWorker.cs`

## Shared pattern

There is no single timeout model today.

Observed patterns:

- hard SQL command timeouts on individual commands
- endpoint-level `CancellationTokenSource.CancelAfter(...)`
- request-aborted cancellation returning `499`
- explicit `Results.Problem(...)` with `503` on some long-running paths
- fallback `Meta` errors or empty payloads on some cached analytics paths
- worker refresh runs tracked through `AnalyticsRefreshRunRecorder`

That split is intentional in some places, but it is not normalized.

## Endpoint findings

### Supplier decision hub

File: `Api/Endpoints/SupplierDecisionHubEndpoints.cs`

- core supplier row query uses `command.CommandTimeout = 25`
- SQL timeout hits are logged through `SqlCommandLoggingHelper.LogSqlExecution(...)`
- timeout exceptions are converted into `SupplierDecisionUnavailableException` with `SQL_TIMEOUT`
- the API layer returns `503` problem responses for unavailable supplier details
- correlation IDs are applied to success/error meta or exposed in problem extensions

Interpretation:

- this path prefers a hard stop with a clear unavailable response rather than a silent fallback

### Supplier-sales-stats

File: `Api/Endpoints/AllEndpoints.cs`

- uses the shared request cancellation token
- returns `499` when the caller aborts the request
- returns `503` `Results.Problem(...)` for `TaskCanceledException` and `NpgsqlException`
- returns `500` `Results.Problem(...)` for unexpected exceptions
- logs elapsed time and request dimensions (`StoreId`, `SezonaId`, date range)
- cache key includes `dataScope` and active cost batch

Interpretation:

- this path is explicit about failure, but it does not use the same fallback vocabulary as the cached filter endpoints

### Dashboard bootstrap and filter endpoints

File: `Api/Endpoints/CachedAnalyticsEndpoints.cs`

- dashboard bootstrap returns `499` for request aborts
- dashboard bootstrap returns `AnalyticsResponseMetaDto` with `ANALYTICS_TIMEOUT` on timeout and `ANALYTICS_DB_UNAVAILABLE` on database failure
- supplier filters use `CancelAfter(TimeSpan.FromSeconds(10))`
- store filters use `CancelAfter(TimeSpan.FromSeconds(15))`
- supplier/store filter endpoints return `499` when the caller aborts
- supplier/store filter endpoints return empty arrays plus `X-Analytics-Fallback*` headers on timeout or database issues

Interpretation:

- these paths prefer degraded-success payloads for filter discovery, not hard failures

### Inventory and decision board

Files:

- `Api/Endpoints/InventoryEndpoints.cs`
- `Api/Endpoints/DecisionBoardEndpoints.cs`

Observed behavior:

- inventory balance returns `499` on caller cancellation
- inventory balance returns `AnalyticsResponseMetaFactory.Error(...)` on general exception
- decision board collects multiple sub-snapshots and logs warnings when any one of them fails
- decision board attaches a board-level meta with `BOARD_PARTIAL` when warnings are present

Interpretation:

- board composition is already partial-aware, but each sub-source can fail differently

## Worker findings

File: `Workers/NightlyAnalyticsRefreshWorker.cs`

- worker startup and idle loops respect `stoppingToken`
- worker delay loops treat `OperationCanceledException` as a normal shutdown path
- nightly refresh run is recorded with `Activity.Current?.Id` as `correlationId`
- every refresh/cleanup SQL command uses `ExecuteNonQueryAsync(..., _options.CommandTimeoutSeconds, ct)`
- failures are reported through `AnalyticsRefreshRunRecorder.MarkFailedAsync(...)`
- partial and success states also carry the same `correlationId`
- on cancellation, the worker records a failed run with `cancelled` and rethrows

Interpretation:

- the worker is already observability-rich, but its timeout behavior is configured separately from request paths

## Recommendations

These are follow-up candidates, not runtime changes from Q82:

1. Standardize which analytics endpoints should use `499`, `503`, or degraded `Meta` fallback.
2. Decide whether supplier-sales-stats should stay hard-fail or adopt a fallback meta contract.
3. Decide whether filter endpoints should keep empty-array fallback headers or move to explicit meta payloads.
4. Consider a shared helper or doc contract for `correlationId` propagation and timeout logging.
5. Keep worker command timeout and HTTP request timeout policies documented separately.

## Non-goals

- no SQL formula changes
- no cache TTL changes
- no worker scheduling rewrite
- no deployment workflow changes
