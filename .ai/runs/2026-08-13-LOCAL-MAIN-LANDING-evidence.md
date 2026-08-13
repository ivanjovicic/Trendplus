Task ID: LOCAL-MAIN-LANDING
Queue: direct user request (review local changes, test, merge to main)
Date: 2026-08-13
Agent/tool: Cursor Grok 4.6
Model: Cursor Grok 4.6
Delivery target: main
Main commit SHA: 355eccef9e792a7d43f480aa6a363a21cc9ad241
Main verification: pending origin/main push

## What was done
- Reviewed the uncommitted STAB11/STAB12, QDB03-QDB05, and analytics fake-zero landing set.
- Registered `SourceMappingPreviewService` in discovery TestHost and marked the mapping-preview parameter `[FromServices]` so the data-source host can start.
- SQL Server live tests now return when no engine is available instead of throwing locally.
- Inventory primary load no longer clears a blocking error before the replacement fetch succeeds, and store-compare state no longer churns an empty array identity.
- STAB queue follow-up no longer points at already-done QDB03; P-UI-20 table status matches the READY header.
- Did not promote RQ100 or QDB06.

## Files changed
- Auth/document/logs: `Api/Endpoints/AdminAccessControl.cs`, `Api/Endpoints/AllEndpoints.cs`, `Api/Endpoints/DocumentEndpoints.cs`, `Infrastructure/Services/Documents/DocumentSecurityServices.cs`, related tests and frontend admin-key clients
- SQL Server discovery/mapping preview: `Api/Services/DataSources/*`, `Api/Endpoints/DataSourceDiscoveryEndpoints.cs`, `Api/Program.cs`, related tests
- Analytics fake-zero: PreNivelacija query failure meta, inventory status operational fallback warning, SDH/ColorSales/Inventory page presentation
- Queue/docs/evidence under `docs/ai/`, `docs/architecture/`, `.ai/runs/`

## Validation run
- git diff --check -> pass
- dotnet test Api.Tests focused STAB/QDB/PreNivelacija/InventoryList (CI=false) -> pass (66)
- dotnet test SqlServerSourceDataSessionIntegrationTests (CI=false) -> pass (1)
- npm run test -- --run focused STAB/QDB/analytics specs -> pass (30)
- npm run check:analytics-guardrails -> pass
- npm run build -> pass
- node scripts/check-agent-instructions.mjs --self-test and live -> pass
- node scripts/check-prompt-queues.mjs --self-test and live -> pass

## Validation not run
- Full `dotnet test Api.Tests` with `CI=true` -> not a product proof locally; Docker daemon is down so Postgres Testcontainers fixtures throw instead of skip
- Full Vitest `--run` suite -> 20 failures observed, mostly pre-existing (PilotReadiness, AnalyticsDashboard integration, AnalyticsMethodologyRegistry, analyticsApi AbortSignal, Inventory freshness lineage). Only the new Inventory partial-failure spec was repaired.
- `node scripts/check-planning-architecture.mjs` -> fail: `DEX: expected exactly one READY prompt, found 0`. Pre-existing after DEX17 Status=DONE with no DEX18; not invented here.

## What was missed
- Cached `/sales/daily` can still silently use operational fallback without meta.
- Dashboard bootstrap inventory can still hide Artikli fallback even though `UsedOperationalFallback` exists on the DTO.
- DEX17 queue header still says READY while the prompt Status is DONE; planning validator remains red until an owner writes the next DEX prompt or allows zero READY.

## Risks
- Document print/download with a valid signed token still uses anonymous `GetCurrent()` and skips ownership checks by design.
- SQL Server live proof in CI still requires Testcontainers, LocalDB, or `SQLSERVER_TEST_CONNECTION_STRING`.
- Frontend admin-key is cached in module/ref state after the operator prompt.

## Next
- Owner may promote RQ100 or approve QDB06 migration.
- P-UI-20 remains the current P-UI READY prompt.
- Do not start DEX16/RQ100/QDB06 from this landing.
