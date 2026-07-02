# Analytics Backend Test Coverage Expansion — 2026-07-02

Repository: `ivanjovicic/Trendplus`

Status: implementation completed; first full CI execution and measured baseline are still required.

## Why backend analytics tests are required

Frontend tests can prove that analytics screens render, filter and react correctly to a known response. They cannot prove that the response is mathematically correct or safe.

Backend tests are required to protect:

- revenue, unit, margin, inventory and impact calculations;
- store, supplier, period and data-scope isolation;
- recommendation and confidence rules;
- distinction between empty data, partial data and backend failure;
- idempotent action creation and duplicate prevention;
- action status/outcome audit behavior;
- API validation, response shape and metadata contracts;
- database-provider behavior, cache behavior and concurrency.

## New backend test suites

### 1. Critical cached analytics HTTP endpoints

File:

- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`

Commits:

- `cb8bb68cb9bb493024dcb65653ef522cd2682562`
- `92a6c263654365a5d98d2fa27ecdc046d554dc0b`

Coverage added:

- exact sales totals, transactions, units and averages;
- store-scope filtering;
- supplier-scope isolation with no cross-supplier revenue leakage;
- successful empty-period metadata instead of a fake backend error;
- independent top-product ranking by revenue and by units;
- exact inventory SKU, on-hand, low-stock, OOS and valuation figures;
- explicit empty inventory metadata;
- quick-insight best day, top product and low-stock alert;
- receipt-level transaction averages rather than line-level averages.

The tests call real HTTP routes through `WebApplicationFactory` and use a deterministic EF Core fixture.

### 2. Product Decision Center builder

File:

- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`

Commit:

- `9298b0486f182884d0fe76f08279a684a7ec04ba`

Coverage added:

- product decision generation from actual article, sale and stock records;
- exact revenue, unit, velocity, margin and stock-gap calculations;
- lost-sales estimate and expected-impact propagation;
- `REPLENISH` versus `FIX_DATA` decisions;
- confidence, reliability, drivers, reason codes and warning codes;
- stable recommendation/source identifiers;
- critical data-quality propagation to response metadata;
- honest `AnalyzedRows`, `TotalRows` and `IgnoredRowsCount` when `top` truncates output;
- explicit empty metadata for an unknown store;
- separation of `imported` and `existing` data scopes.

### 3. Executive Decision Board aggregation

File:

- `Api.Tests/DecisionBoardAggregationContractTests.cs`

Commits:

- `c0f43e7456ecfeec830bb341405ca94eeaed65d3`
- `dd96ef90658eff4b8a57049e24621af02343f26c`

Coverage added:

- action state matching by the exact `(sourceType, sourceKey)` tuple;
- protection against cross-domain source-key collisions;
- open and closed action projection;
- expected-impact section inclusion/exclusion;
- explicit `no_board_data` metadata;
- partial-source warning behavior without discarding valid cards;
- deterministic ordering, uniqueness and five-card section cap;
- prevention of insufficient-data recommendations entering high-impact sections through fabricated zero values.

### 4. Central Analytics Actions HTTP workflow

File:

- `Api.Tests/AnalyticsActionsCriticalWorkflowTests.cs`

Commit:

- `aaed0d47c24f4090da8b44c25cb95b00d31c5ca7`

Coverage added:

- idempotent open-action upsert;
- creation of a new action after the prior action is closed;
- legacy data-quality normalization (`fair` to `warning`);
- preservation of recommendation ledger fields;
- combined list filters, search, paging and priority ordering;
- invalid filter validation;
- source-status batch deduplication;
- preference for an open action over newer closed history;
- oversized status-batch rejection;
- exact status and P1-open counts;
- 404 behavior for missing action detail/status/outcome;
- outcome-note contract limit.

These are TestServer integration tests against the actual minimal API route mapping and actual `AnalyticsActionItemService`.

## CI and coverage changes

### Complete backend suite

File:

- `.github/workflows/analytics-tests.yml`

Commit:

- `945ff9abc7f85ea8867fa985ab9854ea74fc2ad9`

The previous workflow ran only tests tagged `Category=Unit` and `Category=Integration`. Existing untagged analytics tests could therefore be silently skipped.

The workflow now:

- builds the full solution;
- runs the complete `Api.Tests` project without category filters;
- starts PostgreSQL 15 for tests that need a real provider;
- supplies all known analytics connection-string aliases;
- collects Cobertura and LCOV coverage;
- writes line and branch coverage into the GitHub job summary;
- uploads TRX and coverage artifacts for 14 days.

### Coverage collector

Files:

- `Api.Tests/Api.Tests.csproj`
- `Api.Tests/coverage.runsettings`

Commits:

- `3161679459cc754b3206cd253dd4c5bf1eb6225b`
- `6e875f8524e7a9a2c0090ef747201d2600c6bc6f`
- `9b94b32caeed579337b3cf5098f7f5bc56c4b9b4`

`coverlet.collector` is configured with a current `Microsoft.NET.Test.Sdk` so the VSTest coverage collector and the .NET 8 CI environment use compatible tooling.

Coverage configuration excludes generated migrations, designer files and build output so the report measures maintainable application code instead of generated noise.

### Frontend analytics suite is now mandatory

File:

- `.github/workflows/analytics-quality-gates.yml`

Commit:

- `0b332254bbab67b6191f7283049b43a24de10089`

The workflow now runs:

1. `npm run test:analytics`
2. analytics guardrails and TypeScript checks
3. production frontend build

This connects the previously added analytics screen tests to CI instead of leaving them as optional local tests.

## Commands

Backend:

```bash
dotnet restore Trendplus2.sln
dotnet build Trendplus2.sln --configuration Release
dotnet test Api.Tests/Api.Tests.csproj \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --settings Api.Tests/coverage.runsettings \
  --results-directory TestResults
```

Frontend:

```bash
cd Klijent/clientapp
npm ci
npm run test:analytics
npm run check:analytics-guardrails
npm run build
```

## Coverage policy

No coverage percentage is claimed until the first complete Cobertura report is produced.

After that first successful run, the recommended policy is:

- establish the measured baseline for lines and branches;
- fail CI on any regression below that baseline;
- target at least 85% line coverage and 75% branch coverage for critical analytics decision, action and metadata modules;
- increase thresholds incrementally as PostgreSQL, cache-failure and concurrency tests are added;
- never raise coverage by testing trivial DTO getters while leaving financial or decision branches untested.

## Remaining high-value gaps

The next backend test batch should prioritize:

1. real PostgreSQL Product Decision and inventory SQL behavior;
2. concurrent analytics-action upsert against the filtered unique index;
3. supplier decision hub HTTP + SQL integration;
4. cache hit, stale-cache, factory failure and timeout behavior;
5. refresh-status/data-quality partial-source injection into the Decision Board endpoint;
6. query-count and latency budgets for the largest analytics routes;
7. migration compatibility tests for analytics views and action ledger columns.

## Verification status

The repository could not be cloned or restored in the local execution environment because DNS access to GitHub was unavailable. Therefore the new suite has been statically reviewed but not executed in this session.

The GitHub workflows have been configured to perform the authoritative build, complete test run and coverage collection. A passing test count or percentage must only be reported after those workflows produce actual results.
