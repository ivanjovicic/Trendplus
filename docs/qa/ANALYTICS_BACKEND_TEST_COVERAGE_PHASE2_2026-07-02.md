# Analytics Backend Test Coverage — Phase 2

Date: 2026-07-02  
Repository: `ivanjovicic/Trendplus`

Status: implementation complete; authoritative build, test count and coverage percentage must come from the first successful GitHub Actions/Cobertura run.

## Goal

Expand backend protection for the main analytics screens beyond basic route and DTO checks. The added tests focus on financial correctness, decision safety, data-scope isolation, cache identity, stable error contracts and real PostgreSQL behavior.

## Added test suites

### Supplier Decision Hub

File: `Api.Tests/SupplierDecisionHubContractTests.cs`

Current commit: `4b1f26faf23ef6d6a87b4ef8538c30f2075379ff`

10 scenarios protect:

- invalid date ranges and negative revenue filters;
- UTC/date/text/data-scope normalization;
- revenue-weighted full-price and markdown shares;
- units-weighted sell-through;
- full-price-base weighted pre-markdown margin;
- capital-at-risk totals;
- explicit 30d-to-90d helper-dataset behavior;
- recommendation gating for fallback, low sample and missing supplier names;
- empty-data metadata;
- stable report URLs, filters, freshness warnings and error reports that do not fabricate business results.

### Data Quality health mathematics

File: `Api.Tests/AnalyticsDataQualityHealthServiceTests.cs`

Commit: `ccbd001d68ed888e4ef3bd0b062192cd9053b8a8`

6 deterministic EF tests protect:

- orphan article counting;
- missing-cost revenue and percentage;
- unknown-supplier revenue and percentage;
- `all`, `existing` and `imported` scope isolation;
- unknown-scope normalization;
- one-day lookback clamping;
- zero-sales behavior without NaN or Infinity.

### Analytics screen cache identity

File: `Api.Tests/AnalyticsScreenCacheKeyContractTests.cs`

Current commit: `9db3d3c18918111fba587cfe76eea850630b280c`

8 scenarios protect against cross-screen and cross-filter cache leakage:

- Product Decision Center period/store/supplier/top/scope dimensions;
- Supplier Decision summary normalized filter identity;
- Supplier ranking page/page-size/sort isolation;
- inventory store-comparison store-set normalization;
- inventory search/sort hashing without exposing user input;
- report cache-version invalidation;
- unknown-scope normalization;
- stable non-reversible cache fingerprints.

### Cached analytics failure contracts

File: `Api.Tests/CachedAnalyticsFailureContractTests.cs`

Current commit: `66cfc1b509b54f4cbbdb8e1900cf8d4938382bed`

5 HTTP integration scenarios protect:

- inventory balance failure response shape;
- inventory list error shape with normalized pagination;
- inventory insights error shape;
- explicit SQL timeout classification for top products;
- correlation-ID preservation or generation.

The tests deliberately inject a failing cache service and verify that screens receive HTTP 200 with an explicit failed `Meta` contract instead of an unstructured 500 or misleading success payload.

### Data Quality on real PostgreSQL

File: `Api.Tests/DataQualityPostgresIntegrationTests.cs`

Commit: `be8126555c50b607244ea25566fc6d730577d0c1`

5 Testcontainers scenarios protect provider-specific behavior that EF InMemory cannot validate:

- `ILIKE` search;
- CTE and window-count paging;
- `NULLS LAST`/revenue ordering behavior;
- missing supplier, missing shoe type and invalid-name classification;
- minimum-revenue threshold;
- `existing` versus `imported` scope isolation;
- top-offender revenue impact percentages;
- edit action URLs.

### Inventory analytics list screen

File: `Api.Tests/InventoryListEndpointIntegrationTests.cs`

Current commit: `514939cb9dd8fd78f976884807da80a1c6dbe19c`

6 HTTP integration scenarios protect:

- OOS stock-cover signals;
- sell-through calculated from sales and movement history;
- recommendation gating and reason codes;
- insufficient-data behavior without fabricated ratios;
- combined store/supplier/search filters;
- value sorting and deterministic pagination;
- explicit empty success metadata;
- invalid page/page-size clamping.

## Integration-test integrity

File: `Api.Tests/DatabaseInitializerP0IntegrationTests.cs`

Commit: `c95380289658732f10c07dcf78ae17ed441362dd`

The shared PostgreSQL fixture previously swallowed every Testcontainers startup exception. That allowed SQL integration tests to return early and appear green without executing any SQL.

The fixture now:

- remains tolerant for local environments without Docker;
- throws a clear infrastructure failure when `CI=true` and PostgreSQL cannot start;
- prevents false-green analytics builds.

## CI enforcement

File: `.github/workflows/analytics-tests.yml`

Commit: `3a418d4da1769a8391d7228a4b42d40f045378bc`

The workflow now explicitly sets `CI=true`, builds the complete solution, runs the complete `Api.Tests` project, collects Cobertura and LCOV, publishes line/branch coverage in the job summary and uploads TRX/coverage artifacts.

## New scenario count

Phase 2 adds **40 focused test scenarios**:

| Area | Scenarios |
|---|---:|
| Supplier Decision Hub | 10 |
| Data Quality health | 6 |
| Cache-key contracts | 8 |
| Failure contracts | 5 |
| Real PostgreSQL Data Quality | 5 |
| Inventory list and signals | 6 |
| **Total** | **40** |

This count excludes the substantial Product Decision, Decision Board, Central Actions and cached-sales tests added in Phase 1.

## Test quality rules used

- Assert business outcomes rather than implementation calls.
- Use exact financial totals where the formula is deterministic.
- Use ranges only for time-dependent or deliberately rounded confidence values.
- Cover success, empty, partial, invalid and failed states separately.
- Verify scope/filter isolation to prevent cross-tenant-style data leakage.
- Use real PostgreSQL for provider-specific SQL semantics.
- Keep fixtures deterministic and independent per test database.
- Never treat missing infrastructure as a passing CI test.
- Do not claim a coverage percentage until a real report exists.

## Commands

```bash
dotnet restore Trendplus2.sln
dotnet build Trendplus2.sln --configuration Release
dotnet test Api.Tests/Api.Tests.csproj \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --settings Api.Tests/coverage.runsettings \
  --results-directory TestResults
```

## Remaining highest-value gaps

1. Supplier Decision Hub SQL integration against the actual materialized views and fallback capability detection.
2. Concurrent Analytics Action upsert against the PostgreSQL filtered unique index.
3. Cache-hit, stale-cache and metadata propagation using the production cache implementation.
4. Dashboard bootstrap partial-source behavior with injected refresh/data-quality failures.
5. Query-count and latency budgets for Product Decision, Supplier Decision and Inventory list routes.
6. Migration compatibility checks for supplier views, action ledger columns and analytics refresh tables.

## Verification status

Changes were statically checked against current endpoint, DTO, entity and cache-key signatures. The repository was not restored or executed in this session, so no passing test count or coverage percentage is asserted here. GitHub Actions is the authoritative verifier.
