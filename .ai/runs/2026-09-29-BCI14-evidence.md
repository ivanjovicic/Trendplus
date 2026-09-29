Task ID: BCI14
Queue: docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
Date: 2026-09-29
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: `33f6a629`
Main verification: passed - `origin/main` contains `33f6a629`
Evidence state: synchronized

## What was done
- Claimed BCI14 after BCI11-BCI13 were confirmed DONE and repaired the stale queue/roadmap pointer.
- Re-ran the backend test project and focused residual families using the local pgvector Testcontainers topology.
- Repaired test-owned schema/setup drift: `attribution_basis`, `supplier_id_at_sale` and `shoe_type_id_at_sale` are now present in the Access import fixture; the worker success fixture creates its legacy aggregate tables; dimensional aggregate SQL uses valid PostgreSQL quoted identifiers.
- Updated the lost-sales scope assertion to the established units-per-calendar-day contract (`0.43` for the seven-day request window).
- Re-entered BCI14 after the prior broad-suite attempt stalled, with the scope limited to exact-main broad evidence and provider/order isolation.
- Re-ran the exact backend Release suite: `1540 total / 1491 passed / 39 skipped / 10 failed`; the run completed red.
- Classified the dominant residual signals as environment/provider/order-sensitive: Neon `28P01` credential failures, InMemory/relational lifecycle warnings and integration-gated skips. A combined residual filter was cancelled after it became order/host-lifecycle sensitive and produced no final summary.
- Repaired test-owned AnalyticsDbContext isolation in `CachedAnalyticsCriticalEndpointsIntegrationTests`: the fixture now replaces the production Analytics registration with a per-factory InMemory database and seeds the required store/supplier dimensions.
- Reconciled the historical journal-scope fixture with the canonical inventory article-origin contract: imported and existing articles are now separate probe rows, while the test still proves that journal rows do not create false opening-stock/sell-through evidence.
- No production runtime code was changed.

## Files changed
- Api.Tests/AccessImportForeignKeyGuardTests.cs
- Api.Tests/AnalyticsAggregationWorkerTests.cs
- Api.Tests/LostSalesValidationScopePostgresIntegrationTests.cs
- docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
- MASTER_ROADMAP.md
- .ai/runs/2026-09-29-BCI14-evidence.md
- Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --no-restore --verbosity minimal --filter "FullyQualifiedName~AccessImportForeignKeyGuardTests|FullyQualifiedName~AnalyticsAggregationWorkerTests|FullyQualifiedName~LostSalesValidationScopePostgresIntegrationTests"` -> fail 2/11 before final fixture repair; worker/lost-sales/dimensional failures were resolved, leaving only the Access fixture.
- Same focused command after final fixture repair, with AccessImport class selected -> pass, 3/3.
- Earlier combined focused rerun after the first repairs -> pass 10/11.
- `git diff --check` -> pass for implementation files; existing queue claim lines contain markdown hard-break whitespace.
- Exact broad command was attempted with output redirected to `%TEMP%\trendplus-bci14-final-20260929.log`; it stalled without producing a summary and was interrupted once.
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --no-restore --verbosity minimal` -> fail, `1540 total / 1491 passed / 39 skipped / 10 failed`; exact broad suite completed red with Neon/provider/order residuals.
- Combined residual filter over the provider-sensitive families -> cancelled after approximately four minutes without a summary; output showed `CachedAnalyticsCriticalEndpointsIntegrationTests` entering an order/host-sensitive failure cluster.
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests.CachedInventoryList_RespectsArticleAndJournalDataScope"` -> pass, `1/1`.
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CachedAnalyticsCriticalEndpointsIntegrationTests"` -> pass, `17/17`.
- `git diff --check` -> pass.

## Validation not run
- Remote CI/GitHub Actions -> not inspected; no connector result was available in this run.
- Fresh broad-suite green proof -> not available because the local broad run stalled.

## Documentation impact
- BCI14 queue status and completion note were synchronized to PARTIAL.
- MASTER_ROADMAP current BCI pointer was synchronized to BCI14.
- BCI14 remains `PARTIAL`; the queue and roadmap record the re-entry, narrowed residual classification and the new focused proof.

## What was missed
- Broad provider/order lifecycle residuals remain unclassified because the final broad run stalled.
- BCI10 remains open; this run does not claim a green broad gate.
- The remaining Neon credential and provider/order lifecycle families were not reduced to one final root cause because the combined run stalled and remote CI was unavailable.

## Risks
- The broad suite may still contain EF service-provider/order contamination and environment-backed Neon credential failures observed in the prior run.
- Focused Testcontainers evidence does not prove exact-main CI topology equivalence.
- Current-main backend CI remains materially red outside the focused cached-analytics class.
- The test fixture now follows the documented article-origin scope contract; any desired dual-origin journal semantics require a separate analytics contract owner, not a BCI14 runtime workaround.

## Next
- Keep BCI14 `PARTIAL` and re-run the exact broad suite when the host/CI topology can complete; group remaining failures by provider, fixture and host lifecycle.
- Keep BCI10 open until fresh exact-main restore/build/test evidence is green.
