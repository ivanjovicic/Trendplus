Task ID: live-operations-proof
Queue: direct-user-request
Date: 2026-09-25
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- Enabled the local PostgreSQL integration path with `TRENDPLUS_RUN_INTEGRATION_TESTS=true`.
- Applied the existing `20260924120812_AddHistoricalSaleDimensionAttribution` EF migration to the local Docker `trendplus` database only.
- Repaired the RQ412 test oracle's nullable `unknown` bucket lookup so the live test can compare null dimension IDs safely.
- Ran the RQ412 four-test raw-fact oracle and the repository's deterministic all-eight Operations route reconciliation.
- Started the local API with startup database migration disabled and executed `POST /api/analytics/operations-integrity/probe`.

## Files changed
- `Api.Tests/SupplierShoeTypeIndependentOracleIntegrationTests.cs`
- `Api.Tests/OperationsAnalyticsAllRoutesIntegrationTests.cs`
- `.ai/runs/2026-09-25-live-operations-proof-evidence.md`

## Validation run
- `dotnet ef database update --project .\Infrastructure\Infrastructure.csproj --startup-project .\Api\Api.csproj --context TrendplusDbContext --connection <local Docker trendplus connection>` -> pass; local migration applied.
- `dotnet test Api.Tests/Api.Tests.csproj -c Release --filter FullyQualifiedName~SupplierShoeTypeIndependentOracleIntegrationTests` with `TRENDPLUS_RUN_INTEGRATION_TESTS=true` -> pass (`4/4`).
- `dotnet test Api.Tests/Api.Tests.csproj -c Release --filter FullyQualifiedName~OperationsAnalyticsAllRoutesIntegrationTests` with `TRENDPLUS_RUN_INTEGRATION_TESTS=true` -> pass (`1/1`, all eight route families).
- `POST http://127.0.0.1:8080/api/analytics/operations-integrity/probe` -> HTTP 200, `verified`, `blocksDecisionSignals=false`, revenue/units deltas zero.
- `GET http://127.0.0.1:8080/api/analytics/operations-integrity` -> same verified evidence and zero deltas.
- `git diff --check` -> pending final delivery check.

## Validation not run
- STAB16 / production live proof -> not run; external production gate and credentials/provider access are not available in this local workspace.
- Worker schedule execution in a deployed worker process -> not run; direct POST probe was used.
- Remote CI -> not inspected; not a named local acceptance gate.

## Documentation impact
- Added this run log. Existing queue/roadmap changes in the working tree were preserved and not included in this direct-user delivery.

## What was missed
- The RQ413 endpoint remains intentionally bounded to Supplier/Shoe Type. The eight-route proof is the separate deterministic all-routes reconciliation test; no product expansion of the RQ413 registry was made.

## Risks
- The proof uses a local Docker PostgreSQL fixture, not production data or production runtime topology.
- Local API startup still reports unrelated analytics supplier-decision repair/prewarm warnings; the requested bounded probe and all-route test completed successfully.

## Next
- External owner: execute STAB16/production live proof and attach its evidence before treating production readiness as closed.
