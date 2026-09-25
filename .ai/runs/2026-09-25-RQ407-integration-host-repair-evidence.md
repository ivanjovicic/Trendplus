Task ID: RQ407-integration-host-repair
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-25
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Removed the Docker integration-host blocker by validating a fresh PostgreSQL 15 host on `127.0.0.1:55432`.
- Repaired the historical sale-dimension migration so it tolerates compatibility columns and indexes created by `DatabaseInitializer` before EF migrations.
- Updated Supplier Sales and Shoe Type live fixtures to the current lower-case sales schema, immutable sale attribution fields, canonical origins and deterministic identity state.
- Aligned Supplier/Shoe recommendation JSON projections with the lower-camel frontend contract and synchronized live golden contracts.
- Reconciled RQ407 queue truth from `BLOCKED` to `PARTIAL`; the full eight-route acceptance remains open.

## Files changed
- `Api.Tests/AnalyticsSupplierSalesIntegrationTests.cs`
- `Api.Tests/Fixtures/shoe-type-sales-stats-seed.sql`
- `Api.Tests/Fixtures/supplier-sales-stats-seed.sql`
- `Api.Tests/Golden/shoe-type-sales-stats.contract.json`
- `Api.Tests/Golden/supplier-sales-stats.contract.json`
- `Api/Endpoints/AllEndpoints.cs`
- `Infrastructure/Migrations/20260924120812_AddHistoricalSaleDimensionAttribution.cs`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

## Validation run
- `docker info` -> pass; Docker Desktop server is reachable.
- `docker exec trendplus-rq407-postgres pg_isready -U postgres -d trendplus_rq407` -> pass.
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter FullyQualifiedName~DatabaseMigrationBootstrapLifecycleSmokeTests --no-restore --verbosity minimal` -> pass (`1/1`).
- Supplier live suite with the Docker connection and Supplier fixture -> pass (`16/16`, `0` skipped).
- Shoe Type live suite with the Docker connection and Shoe fixture -> pass (`12/12`, `0` skipped).
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter FullyQualifiedName~OperationsAnalyticsProofPackTests --no-restore --verbosity minimal` -> pass (`6/6`).
- `git diff --check` -> pass.

## Validation not run
- All-eight-route RQ407 endpoint reconciliation -> not run; the current bounded fixture/test pack does not yet provide a combined all-route host proof.
- Frontend projection parity across all eight Operacije routes -> not run; this unblock stayed within the backend integration-host and Supplier/Shoe contract surface.
- Remote CI result -> not inspected; CI is residual risk, not a completion gate here.

## Documentation impact
- Updated the owning RQ407 queue entry with the new host evidence, truthful `PARTIAL` status and remaining acceptance gap.

## What was missed
- RQ407 is not complete: six other route families plus the full frontend projection reconciliation still need executable proof.

## Risks
- Supplier and Shoe fixtures truncate shared tables and are mutually exclusive; their live suites must run sequentially against an isolated database.
- Plain `postgres:15` does not provide the `vector` extension expected by the optional analytics initializer; full application startup can log that warning even though these focused live suites pass.

## Next
- Analytics Backend + Frontend + QA/Test Infrastructure: add the combined all-eight operations fixture/host proof and frontend projection parity checks, then reassess RQ407 for `DONE`.
