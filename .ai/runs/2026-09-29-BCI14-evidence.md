Task ID: BCI14
Queue: docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
Date: 2026-09-29
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: pending
Main verification: pending
Evidence state: synchronized

## What was done
- Claimed BCI14 after BCI11-BCI13 were confirmed DONE and repaired the stale queue/roadmap pointer.
- Re-ran the backend test project and focused residual families using the local pgvector Testcontainers topology.
- Repaired test-owned schema/setup drift: `attribution_basis`, `supplier_id_at_sale` and `shoe_type_id_at_sale` are now present in the Access import fixture; the worker success fixture creates its legacy aggregate tables; dimensional aggregate SQL uses valid PostgreSQL quoted identifiers.
- Updated the lost-sales scope assertion to the established units-per-calendar-day contract (`0.43` for the seven-day request window).
- No production runtime code was changed.

## Files changed
- Api.Tests/AccessImportForeignKeyGuardTests.cs
- Api.Tests/AnalyticsAggregationWorkerTests.cs
- Api.Tests/LostSalesValidationScopePostgresIntegrationTests.cs
- docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
- MASTER_ROADMAP.md
- .ai/runs/2026-09-29-BCI14-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --no-restore --verbosity minimal --filter "FullyQualifiedName~AccessImportForeignKeyGuardTests|FullyQualifiedName~AnalyticsAggregationWorkerTests|FullyQualifiedName~LostSalesValidationScopePostgresIntegrationTests"` -> fail 2/11 before final fixture repair; worker/lost-sales/dimensional failures were resolved, leaving only the Access fixture.
- Same focused command after final fixture repair, with AccessImport class selected -> pass, 3/3.
- Earlier combined focused rerun after the first repairs -> pass 10/11.
- `git diff --check` -> pass for implementation files; existing queue claim lines contain markdown hard-break whitespace.
- Exact broad command was attempted with output redirected to `%TEMP%\trendplus-bci14-final-20260929.log`; it stalled without producing a summary and was interrupted once.

## Validation not run
- Remote CI/GitHub Actions -> not inspected; no connector result was available in this run.
- Fresh broad-suite green proof -> not available because the local broad run stalled.

## Documentation impact
- BCI14 queue status and completion note were synchronized to PARTIAL.
- MASTER_ROADMAP current BCI pointer was synchronized to BCI14.

## What was missed
- Broad provider/order lifecycle residuals remain unclassified because the final broad run stalled.
- BCI10 remains open; this run does not claim a green broad gate.

## Risks
- The broad suite may still contain EF service-provider/order contamination and environment-backed Neon credential failures observed in the prior run.
- Focused Testcontainers evidence does not prove exact-main CI topology equivalence.

## Next
- Run a fresh exact-main broad suite when the host lifecycle is available, then group remaining failures by provider/order/fixture family. Keep BCI14 PARTIAL until that evidence exists.
